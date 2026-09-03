using System.Collections.Generic;
using MechTS.Core;
using MechTS.Economy;
using MechTS.Utilities;
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
        private const float RingSelectedScale = 1.15f;
        private const float RingDimBrightness = 0.5f;

        /// <summary>
        /// How far above whatever surface this unit is standing on its art is nudged, so it
        /// doesn't sit flush with that surface's own mesh. See <see cref="NudgeArtAboveSurface"/>.
        /// </summary>
        private const float ArtYOffset = 0.03f;

        [SerializeField] private Faction _faction;
        [SerializeField] private bool _isFlying;
        [SerializeField] private float _visionRadius = 10f;

        private static Material _sharedRingMaterial;

        private UnitManager _unitManager;
        private TechManager _techManager;
        private VisionManager _visionManager;
        private MeshRenderer _ringRenderer;
        private MaterialPropertyBlock _ringPropertyBlock;
        private List<Renderer> _artRenderers;
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
            RefreshRingAppearance();
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
        /// This unit's capability flags (issue #39), computed once in <see cref="Awake"/>
        /// from existing signals — never separately authored, so it can't drift out of sync
        /// with what the unit actually is. Passed to <see cref="Economy.TechManager.GetStatMultiplier"/>
        /// so a research upgrade can scope itself to a subset of units (e.g. ground-only).
        /// </summary>
        public UnitCapability Capabilities { get; private set; }

        /// <summary>
        /// Caches the NavMeshAgent and Health references, disables agent-to-agent avoidance
        /// for a flying unit (see <see cref="IsFlying"/>), and builds this unit's merged
        /// faction/selection ring — a hollow ring, sized from this unit's own
        /// <see cref="CapsuleCollider"/> radius where present, dim in the unit's faction
        /// color at rest and brighter/larger when selected (see <see cref="SetSelected"/>).
        /// Replaces the old separate filled-disc faction ring
        /// (<c>FactionColor.CreateFactionRing</c>) and hidden-until-selected indicator —
        /// merging them avoids stacking two overlapping rings under real sprite art.
        /// </summary>
        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            HealthComponent = GetComponent<Health>();

            if (_isFlying)
            {
                Agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            }

            BuildRing();
            RefreshRingAppearance();

            _artRenderers = new List<Renderer>(GetComponentsInChildren<Renderer>(true));
            _artRenderers.Remove(_ringRenderer);

            NudgeArtAboveSurface();
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

            var weapon = GetComponent<Weapon>();
            if (weapon != null && weapon.Config != null)
            {
                if (weapon.Config.targetType == WeaponTargetType.Ground || weapon.Config.targetType == WeaponTargetType.GroundAndAir)
                {
                    capabilities |= UnitCapability.GroundAttacker;
                }
                if (weapon.Config.targetType == WeaponTargetType.Air || weapon.Config.targetType == WeaponTargetType.GroundAndAir)
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
        /// Nudges each top-level art child (the direct children of this unit's own root that
        /// carry a renderer — either a single "Art" wrapper or several matched-parts siblings
        /// like Torso/Head/HandL/HandR, depending on the prefab's art convention) up by
        /// <see cref="ArtYOffset"/>, so the art never sits flush with the surface the unit is
        /// standing on. Every unit's art previously had zero offset from its own root, which
        /// is also exactly where <see cref="NavMeshAgent"/> places the unit — on flat Ground
        /// (world Y=0) this happened not to cause a visible problem, but standing on an
        /// elevated <c>Platform_Tier1</c> (issue #35) put the art's sprite quads flush with
        /// the platform's own solid mesh top, and the platform's opaque geometry consistently
        /// won the depth test, hiding the unit's art entirely — found via direct pixel-level
        /// verification, not a visual guess (a top-down render capture sampled at the unit's
        /// exact screen position showed only the platform's own material color, no sprite
        /// pixels at all). Walking each renderer up to its nearest ancestor that is a direct
        /// child of this unit's own transform (rather than nudging every renderer
        /// individually) avoids double-applying the offset to a nested child of an already-
        /// nudged parent (e.g. a hand parented under a torso).
        /// </summary>
        private void NudgeArtAboveSurface()
        {
            var nudged = new HashSet<Transform>();
            foreach (var renderer in _artRenderers)
            {
                if (renderer == null) continue;

                Transform topLevel = renderer.transform;
                while (topLevel.parent != null && topLevel.parent != transform)
                {
                    topLevel = topLevel.parent;
                }

                if (topLevel.parent == transform && nudged.Add(topLevel))
                {
                    topLevel.localPosition += Vector3.up * ArtYOffset;
                }
            }
        }

        /// <summary>
        /// Creates the ring GameObject (mesh + renderer), sized from this unit's collider footprint.
        /// </summary>
        private void BuildRing()
        {
            float radius = 0.5f;
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule != null) radius = capsule.radius;

            var ringGo = new GameObject("FactionRing");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            var meshFilter = ringGo.AddComponent<MeshFilter>();
            meshFilter.mesh = RingMesh.Create(radius * 1.4f, radius * 1.7f);

            _ringRenderer = ringGo.AddComponent<MeshRenderer>();
            _ringRenderer.material = GetRingMaterial();
            _ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ringPropertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Returns the shared unlit material used by every unit's ring, creating it once.
        /// </summary>
        private static Material GetRingMaterial()
        {
            if (_sharedRingMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                _sharedRingMaterial = new Material(shader) { name = "UnitRingMaterial" };
                _sharedRingMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }
            return _sharedRingMaterial;
        }

        /// <summary>
        /// Updates the ring's color (dim faction color at rest, full brightness when
        /// selected) and scale (a slight pop when selected) to reflect current state.
        /// </summary>
        private void RefreshRingAppearance()
        {
            if (_ringRenderer == null) return;

            Color baseColor = FactionColor.GetColor(_faction);
            Color color = IsSelected ? baseColor : baseColor * RingDimBrightness;

            _ringPropertyBlock.SetColor("_BaseColor", color);
            _ringRenderer.SetPropertyBlock(_ringPropertyBlock);

            float scale = IsSelected ? RingSelectedScale : 1f;
            _ringRenderer.transform.localScale = new Vector3(scale, 1f, scale);
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
            SetArtVisible(visible);
        }

        /// <summary>
        /// Enables or disables every art renderer and the faction/selection ring.
        /// </summary>
        /// <param name="visible">True to show, false to hide.</param>
        private void SetArtVisible(bool visible)
        {
            foreach (var renderer in _artRenderers)
            {
                if (renderer != null) renderer.enabled = visible;
            }

            if (_ringRenderer != null) _ringRenderer.enabled = visible;
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
            RefreshRingAppearance();
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
