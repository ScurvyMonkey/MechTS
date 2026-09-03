using System.Collections.Generic;
using MechTS.Units;
using MechTS.Utilities;
using MechTS.Vision;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Marks a building as a faction's main building: the harvester deposit point, and
    /// explicitly excluded from valid Saboteur sabotage targets. Reverses issue #20's
    /// invulnerability decision (issue #28) — now carries a real <see cref="Health"/>
    /// component and is a valid <see cref="Units.Weapon"/> target, exactly like any other
    /// building, and its destruction factors into <see cref="Battle.VictoryConditionChecker"/>'s
    /// wipeout check. Still never carries a <see cref="BuildingInstance"/> (guarded at
    /// <see cref="EconomyManager.RegisterBuilding"/>) since it has no
    /// <see cref="BuildingDefinition"/> to back one — that separation is architectural
    /// (keeps it out of the build-menu-facing building list), not a targeting exclusion.
    /// </summary>
    public class MainBuilding : MonoBehaviour, IProductionHost, IRepairTarget
    {
        [SerializeField] private Faction _faction;

        private float _maxHealth;
        private float _visionRadius;
        private UnitProductionDefinition[] _producibleUnits;
        private EconomyManager _economyManager;
        private VisionManager _visionManager;
        private List<Renderer> _renderers;
        private GameObject _ghostMarker;
        private GameObject _rallyMarker;

        /// <summary>The faction this main building belongs to.</summary>
        public Faction Faction => _faction;

        /// <summary>IRepairTarget position — this main building's current world position (issue #42).</summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// How far this main building can see, contributing to its faction's fog of war
        /// (issue #36).
        /// </summary>
        public float VisionRadius => _visionRadius;

        /// <summary>
        /// This main building's <see cref="Health"/> component, if it has one, cached so
        /// combat systems never need a per-frame GetComponent lookup.
        /// </summary>
        public Health HealthComponent { get; private set; }

        /// <summary>
        /// This main building's self-added <see cref="ProductionQueue"/> (issue #41), cached
        /// so callers like <see cref="AI.EnemyEconomyAI"/> never need a per-tick
        /// <c>GetComponent</c> lookup.
        /// </summary>
        public ProductionQueue Production { get; private set; }

        /// <summary>
        /// The units this main building can produce directly (issue #41 — e.g. the
        /// Harvester), sourced from <see cref="MissionEconomyConfig.FactionEconomyStart.mainBuildingProducibleUnits"/>.
        /// Mirrors <see cref="BuildingDefinition.producibleUnits"/>'s shape for buildings
        /// that go through the regular build menu.
        /// </summary>
        public UnitProductionDefinition[] ProducibleUnits => _producibleUnits;

        /// <summary>A main building is never disabled by sabotage (see the class doc comment) — always false.</summary>
        public bool IsDisabled => false;

        /// <summary>
        /// This main building's rally point, if set — units produced by its
        /// <see cref="ProductionQueue"/> walk here after spawning. Mirrors
        /// <see cref="BuildingInstance.RallyPoint"/> exactly.
        /// </summary>
        public Vector3? RallyPoint { get; private set; }

        /// <summary>
        /// Assigns the owning faction, max health, vision radius, and producible-unit list.
        /// Called by <see cref="EconomyManager"/> right after spawning; the max health value
        /// is applied in <see cref="Start"/> once <see cref="HealthComponent"/> is cached,
        /// mirroring <see cref="BuildingInstance"/>'s own Initialize-then-Start shape.
        /// </summary>
        /// <param name="faction">The faction this main building belongs to.</param>
        /// <param name="maxHealth">This main building's configured max health.</param>
        /// <param name="visionRadius">This main building's configured vision radius.</param>
        /// <param name="producibleUnits">The units this main building can directly produce (issue #41).</param>
        public void Initialize(Faction faction, float maxHealth, float visionRadius, UnitProductionDefinition[] producibleUnits)
        {
            _faction = faction;
            _maxHealth = maxHealth;
            _visionRadius = visionRadius;
            _producibleUnits = producibleUnits;
        }

        /// <summary>
        /// Caches the Health component (applying this main building's configured max health),
        /// its own renderers (for fog-of-war visibility toggling), the scene's
        /// <see cref="EconomyManager"/>/<see cref="VisionManager"/>, and ensures a
        /// <see cref="ProductionQueue"/> is present so this main building can produce units
        /// directly (issue #41) exactly like any other production-capable building.
        /// </summary>
        private void Start()
        {
            HealthComponent = GetComponent<Health>();
            if (HealthComponent != null)
            {
                HealthComponent.SetMaxHealth(_maxHealth);
            }

            _renderers = new List<Renderer>(GetComponentsInChildren<Renderer>(true));

            _economyManager = FindFirstObjectByType<EconomyManager>();
            _visionManager = FindFirstObjectByType<VisionManager>();

            Production = GetComponent<ProductionQueue>();
            if (Production == null)
            {
                Production = gameObject.AddComponent<ProductionQueue>();
            }
        }

        /// <summary>
        /// Sets (or moves) this main building's rally point, creating its marker the first
        /// time and repositioning it on subsequent calls. Called by
        /// <see cref="Units.AttackCommand"/> when this main building is selected and the
        /// player right-clicks a ground location. Mirrors
        /// <see cref="BuildingInstance.SetRallyPoint"/> exactly.
        /// </summary>
        /// <param name="position">The world position units should walk to after spawning.</param>
        public void SetRallyPoint(Vector3 position)
        {
            RallyPoint = position;
            if (_rallyMarker == null)
            {
                _rallyMarker = RallyPointMarker.Create(position, _faction);
            }
            else
            {
                RallyPointMarker.SetPosition(_rallyMarker, position);
            }
        }

        /// <summary>
        /// Clears this main building's rally point and destroys its marker. Called by
        /// <see cref="Units.AttackCommand"/> when this main building is selected and the
        /// player right-clicks the building itself.
        /// </summary>
        public void ClearRallyPoint()
        {
            RallyPoint = null;
            if (_rallyMarker != null)
            {
                Destroy(_rallyMarker);
                _rallyMarker = null;
            }
        }

        /// <summary>
        /// Applies the Player faction's current fog-of-war state to this main building's
        /// visibility (issue #36) — always fully visible if this is the Player's own, otherwise
        /// gated via <see cref="FogVisibility.Apply"/>.
        /// </summary>
        private void Update()
        {
            FogVisibility.Apply(_faction, transform.position, _visionManager, _renderers, ref _ghostMarker);
        }

        /// <summary>
        /// Deregisters this main building from the <see cref="EconomyManager"/> when
        /// destroyed by combat (issue #28) — without this, a destroyed main building would
        /// linger as a stale entry in <see cref="EconomyManager.MainBuildings"/>, which
        /// <see cref="UI.MinimapPanel"/> and <see cref="Battle.VictoryConditionChecker"/>
        /// both read every frame/tick.
        /// </summary>
        private void OnDestroy()
        {
            _economyManager?.DeregisterMainBuilding(this);
            if (_ghostMarker != null) Destroy(_ghostMarker);
            if (_rallyMarker != null) Destroy(_rallyMarker);
        }
    }
}
