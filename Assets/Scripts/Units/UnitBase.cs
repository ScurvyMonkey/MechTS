using MechTS.Core;
using MechTS.Economy;
using MechTS.Vision;
using UnityEngine;
using UnityEngine.AI;

namespace MechTS.Units
{
    /// <summary>
    /// Identifies which side owns a unit or building.
    /// </summary>
    public enum Faction
    {
        Player,
        Enemy
    }

    /// <summary>
    /// Common base for every controllable unit in MechTS. Handles faction ownership,
    /// selection state, and registration with the <see cref="UnitManager"/>.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class UnitBase : MonoBehaviour
    {
        /// <summary>
        /// The real visual altitude a flying unit (<see cref="IsFlying"/>) renders at, applied
        /// via <see cref="NavMeshAgent.baseOffset"/> in <see cref="Awake"/>. High enough to
        /// clear <c>Platform_Tier1</c>'s ~2.5-unit top surface with margin — a flying unit's
        /// own NavMesh Agent Type excludes terrain-category geometry entirely (issue #40), so
        /// its path can legitimately cross straight through a platform's footprint; without a
        /// real altitude it would visually clip through that solid geometry instead of flying
        /// over it.
        /// </summary>
        private const float FlightAltitude = 3.5f;

        [SerializeField] private Faction _faction;
        [SerializeField] private bool _isFlying;
        [SerializeField] private float _visionRadius = 10f;

        private UnitManager _unitManager;
        private TechManager _techManager;
        private VisionManager _visionManager;
        private UnitVisuals _visuals;
        private float _baseMoveSpeed;

        /// <summary>This unit's ongoing Ore upkeep cost per minute (issue #48). See <see cref="SetUpkeepCost"/>.</summary>
        public int UpkeepOrePerMinute { get; private set; }

        /// <summary>This unit's ongoing Biomass upkeep cost per minute (issue #48). See <see cref="SetUpkeepCost"/>.</summary>
        public int UpkeepBiomassPerMinute { get; private set; }

        /// <summary>This unit's ongoing Gold upkeep cost per minute (issue #48). See <see cref="SetUpkeepCost"/>.</summary>
        public int UpkeepGoldPerMinute { get; private set; }

        /// <summary>
        /// The faction that owns this unit.
        /// </summary>
        public Faction Faction => _faction;

        /// <summary>
        /// Sets this unit's owning faction and refreshes its ring color to match. Must be
        /// called immediately after instantiation (before <c>Start()</c> runs) by whatever
        /// spawns the unit dynamically — e.g. <see cref="Economy.ProductionQueue"/> — since a
        /// prefab's serialized faction value is fixed and can't reflect which faction
        /// actually produced this instance.
        /// </summary>
        /// <param name="faction">The faction to assign.</param>
        public void SetFaction(Faction faction)
        {
            _faction = faction;
            _visuals.RefreshRingAppearance(_faction, IsSelected);
        }

        /// <summary>
        /// Whether this unit is currently selected by its owning player.
        /// </summary>
        public bool IsSelected { get; private set; }

        /// <summary>
        /// Whether this unit is a flying unit — set per-prefab in the Inspector, defaults to
        /// false. A flying unit's <see cref="Agent"/> skips agent-to-agent avoidance (see
        /// <see cref="Awake"/>) so it doesn't divert its own path around clusters of ground
        /// units; it still paths across the same NavMesh terrain as everything else (issue
        /// #25 — no fully independent flight movement mode, and no targeting restriction).
        /// </summary>
        public bool IsFlying => _isFlying;

        /// <summary>
        /// How far this unit can see, in world units — one of the sources
        /// <see cref="Vision.VisionManager"/> unions together to compute this unit's
        /// faction's currently-visible area (issue #36).
        /// </summary>
        public float VisionRadius => _visionRadius;

        /// <summary>
        /// This unit's movement agent, used to issue move orders.
        /// </summary>
        public NavMeshAgent Agent { get; private set; }

        /// <summary>
        /// This unit's <see cref="Health"/> component, if it has one, cached so combat
        /// systems never need a per-frame GetComponent lookup.
        /// </summary>
        public Health HealthComponent { get; private set; }

        /// <summary>
        /// This unit's weapon component, if it has one, cached (issue #90) so UI/inspection
        /// code (e.g. <see cref="MechTS.UI.SelectionPanel"/>) never needs a per-frame
        /// GetComponent lookup — mirrors <see cref="HarvesterUnit.GatherBehavior"/>'s existing
        /// cached-property shape.
        /// </summary>
        public Weapon Weapon { get; private set; }

        /// <summary>
        /// This unit's capability flags (issue #39), computed once in <see cref="Awake"/>
        /// from existing signals — never separately authored, so it can't drift out of sync
        /// with what the unit actually is. Passed to <see cref="Economy.TechManager.GetStatMultiplier"/>
        /// so a research upgrade can scope itself to a subset of units (e.g. ground-only).
        /// </summary>
        public UnitCapability Capabilities { get; private set; }

        /// <summary>
        /// Caches the NavMeshAgent and Health references, disables agent-to-agent avoidance
        /// and sets a real hover altitude for a flying unit (see <see cref="IsFlying"/>,
        /// <see cref="FlightAltitude"/>), and builds this unit's merged faction/selection
        /// ring plus art-visibility state via <see cref="UnitVisuals"/> (issue #96 — extracted
        /// from this method; see that class for the ring/nudge details). Replaces the old
        /// separate filled-disc faction ring (<c>FactionColor.CreateFactionRing</c>) and
        /// hidden-until-selected indicator — merging them avoids stacking two overlapping
        /// rings under real sprite art.
        /// </summary>
        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            HealthComponent = GetComponent<Health>();

            // Every real 3D model's Art child is base-anchored at local (0,0,0) (see
            // CLAUDE.md's 3D Model Conversion Convention), so a ground unit must render flush
            // with the NavMesh; a flying unit gets a real, deliberate hover altitude instead.
            // Driven from code, not each prefab's own serialized default, so this can't go
            // stale again the way it did before issue #85.
            Agent.baseOffset = _isFlying ? FlightAltitude : 0f;

            if (_isFlying)
            {
                Agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            }

            _visuals = new UnitVisuals();
            _visuals.Build(transform, _faction);

            ComputeCapabilities();
        }

        /// <summary>
        /// Derives <see cref="Capabilities"/> from this unit's existing signals — never a
        /// separately-authored tag. <see cref="Builder"/> has no consumer yet (no unit sets
        /// it), same "ships ahead of its consumer" precedent as <see cref="Economy.UpgradeStatType.PassiveGeneration"/>.
        /// </summary>
        private void ComputeCapabilities()
        {
            var capabilities = _isFlying ? UnitCapability.Flying : UnitCapability.Ground;

            Weapon = GetComponent<Weapon>();
            if (Weapon != null && Weapon.Config != null)
            {
                if (Weapon.Config.targetType == WeaponTargetType.Ground || Weapon.Config.targetType == WeaponTargetType.GroundAndAir)
                {
                    capabilities |= UnitCapability.GroundAttacker;
                }
                if (Weapon.Config.targetType == WeaponTargetType.Air || Weapon.Config.targetType == WeaponTargetType.GroundAndAir)
                {
                    capabilities |= UnitCapability.AirAttacker;
                }
            }

            if (GetComponent<Economy.HarvesterGatherBehavior>() != null)
            {
                capabilities |= UnitCapability.Gatherer;
            }

            if (this is SaboteurUnit)
            {
                capabilities |= UnitCapability.Saboteur;
            }

            Capabilities = capabilities;
        }

        /// <summary>
        /// Finds the scene's <see cref="UnitManager"/> and registers this unit with it, and
        /// subscribes to <see cref="TechManager.OnUpgradeResearched"/> so a MoveSpeed
        /// upgrade researched mid-round applies immediately, not just to units spawned
        /// afterward.
        /// </summary>
        protected virtual void Start()
        {
            _unitManager = FindFirstObjectByType<UnitManager>();
            _unitManager.Register(this);

            _techManager = FindFirstObjectByType<TechManager>();
            if (_techManager != null) _techManager.OnUpgradeResearched += HandleUpgradeResearched;

            _visionManager = FindFirstObjectByType<VisionManager>();
        }

        /// <summary>
        /// Hides this unit's art and ring while it's outside the Player faction's current
        /// vision (issue #36) — Enemy-owned units only; the Player always sees their own
        /// units in full. Unlike buildings, a hidden unit is never remembered as a static
        /// "ghost" once it leaves vision, since a frozen snapshot of something that moves
        /// would be misleading rather than useful.
        /// </summary>
        protected virtual void Update()
        {
            if (_faction == Faction.Player || _visionManager == null) return;

            bool visible = _visionManager.IsVisible(Faction.Player, transform.position);
            _visuals.SetArtVisible(visible);
        }

        /// <summary>
        /// Deregisters this unit from the <see cref="UnitManager"/> and unsubscribes from
        /// the <see cref="TechManager"/> when destroyed.
        /// </summary>
        protected virtual void OnDestroy()
        {
            _unitManager?.Deregister(this);
            if (_techManager != null) _techManager.OnUpgradeResearched -= HandleUpgradeResearched;
        }

        /// <summary>
        /// Sets this unit's base move speed and applies the current MoveSpeed stat
        /// multiplier. Called by whichever component configures this unit's speed from its
        /// own config data (e.g. <see cref="HarvesterGatherBehavior"/>, <see cref="SabotageAction"/>)
        /// instead of setting <see cref="Agent"/>'s speed directly, so a later research can
        /// still find and re-apply it.
        /// </summary>
        /// <param name="speed">The base move speed, before any stat multiplier.</param>
        public void SetBaseMoveSpeed(float speed)
        {
            _baseMoveSpeed = speed;
            ApplyMoveSpeedMultiplier();
        }

        /// <summary>
        /// Re-applies the MoveSpeed stat multiplier if the researching faction owns this unit.
        /// </summary>
        /// <param name="faction">The faction that just researched an upgrade.</param>
        private void HandleUpgradeResearched(Faction faction)
        {
            if (faction != _faction) return;
            ApplyMoveSpeedMultiplier();
        }

        /// <summary>
        /// Recomputes <see cref="Agent"/>'s speed from <see cref="_baseMoveSpeed"/> and the
        /// current MoveSpeed stat multiplier.
        /// </summary>
        private void ApplyMoveSpeedMultiplier()
        {
            float multiplier = _techManager != null ? _techManager.GetStatMultiplier(_faction, UpgradeStatType.MoveSpeed, Capabilities) : 1f;
            Agent.speed = _baseMoveSpeed * multiplier;
        }

        /// <summary>
        /// Sets this unit's ongoing upkeep cost per minute (issue #48), read every tick by
        /// <see cref="Economy.EconomyManager"/>'s upkeep drain while the Economy Round is
        /// active. A plain field write — like <see cref="SetBaseMoveSpeed"/>, not
        /// <see cref="VehicleAnimationDriver.SetEngineSound"/> — so it's safe to call
        /// regardless of which sibling component's <c>Start()</c> runs first.
        /// </summary>
        /// <param name="orePerMinute">Ore upkeep cost per minute.</param>
        /// <param name="biomassPerMinute">Biomass upkeep cost per minute.</param>
        /// <param name="goldPerMinute">Gold upkeep cost per minute.</param>
        public void SetUpkeepCost(int orePerMinute, int biomassPerMinute, int goldPerMinute)
        {
            UpkeepOrePerMinute = orePerMinute;
            UpkeepBiomassPerMinute = biomassPerMinute;
            UpkeepGoldPerMinute = goldPerMinute;
        }

        /// <summary>
        /// Marks this unit as selected or deselected and refreshes its ring's appearance.
        /// </summary>
        /// <param name="selected">True to select, false to deselect.</param>
        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            _visuals.RefreshRingAppearance(_faction, IsSelected);
        }

        /// <summary>
        /// Issues a move order, sending this unit to the given world-space destination.
        /// </summary>
        /// <param name="destination">The target position on the NavMesh.</param>
        public virtual void MoveTo(Vector3 destination)
        {
            Agent.SetDestination(destination);
        }

        /// <summary>
        /// Halts this unit's current order.
        /// </summary>
        public virtual void Stop()
        {
            Agent.ResetPath();
        }

        /// <summary>
        /// Returns whether this unit has arrived at its current destination.
        /// </summary>
        public bool HasArrived()
        {
            return !Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance + 0.1f;
        }
    }
}
