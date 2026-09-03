using System.Collections;
using System.Collections.Generic;
using MechTS.Units;
using MechTS.Utilities;
using MechTS.Vision;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Runtime component on a placed building. Links the building back to its
    /// definition and owning faction, registers/deregisters with the <see cref="EconomyManager"/>,
    /// and implements <see cref="ISabotageTarget"/> (non-main buildings only — a faction's
    /// main building never gets a BuildingInstance, so it's never a valid target).
    /// </summary>
    public class BuildingInstance : MonoBehaviour, ISabotageTarget, IProductionHost, IRepairTarget
    {
        [SerializeField] private BuildingDefinition _definition;
        [SerializeField] private Faction _faction;

        private EconomyManager _economyManager;
        private VisionManager _visionManager;
        private List<Renderer> _renderers;
        private GameObject _ghostMarker;
        private bool _isSabotageDisabled;
        private bool _powerFootprintApplied;

        /// <summary>The definition this building was placed from.</summary>
        public BuildingDefinition Definition => _definition;

        /// <summary>The faction that owns this building.</summary>
        public Faction Faction => _faction;

        /// <summary>Whether this building type can produce units (has a production queue).</summary>
        public bool CanProduceUnits => _definition != null && _definition.canProduceUnits;

        /// <summary>
        /// This building's <see cref="Health"/> component, if it has one, cached so combat
        /// systems never need a per-frame GetComponent lookup.
        /// </summary>
        public Health HealthComponent { get; private set; }

        /// <summary>ISabotageTarget/IRepairTarget position — this building's current world position.</summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// Whether this building is currently non-functional: temporarily disabled by a
        /// completed sabotage, or still under construction (issue #42 — see
        /// <see cref="IsUnderConstruction"/>). Checked by <see cref="ProductionQueue"/>, which
        /// already treats "disabled" as "don't tick production" regardless of the reason.
        /// </summary>
        public bool IsDisabled => _isSabotageDisabled || IsUnderConstruction;

        /// <summary>
        /// Whether this building is still being constructed by a <see cref="Units.CrewmanUnit"/>
        /// (issue #42) — while true, it contributes no power (see
        /// <see cref="EconomyManager.RegisterBuilding"/>/<see cref="CompleteConstruction"/>),
        /// no vision (see <see cref="Vision.VisionManager.MarkBuildingVision"/>), and no
        /// production (via <see cref="IsDisabled"/>). Defaults false so a hand-placed building
        /// (e.g. one pre-placed directly in the Editor rather than through the Crewman flow)
        /// is fully functional from its very first frame, matching pre-#42 behavior.
        /// </summary>
        public bool IsUnderConstruction { get; private set; }

        /// <summary>
        /// This building's construction progress, from 0 to 1 — 1 whenever
        /// <see cref="IsUnderConstruction"/> is false. Driven by
        /// <see cref="Units.CrewmanBuildBehavior"/> via <see cref="SetConstructionProgress"/>,
        /// read by <see cref="UI.SelectionPanel"/> for display, mirroring
        /// <see cref="ProductionQueue.CurrentProgress01"/>'s existing display pattern.
        /// </summary>
        public float ConstructionProgress01 { get; private set; } = 1f;

        /// <summary>
        /// This building's rally point, if set — units produced by its
        /// <see cref="ProductionQueue"/> walk here after spawning. Null means "spawn in
        /// place," today's default behavior.
        /// </summary>
        public Vector3? RallyPoint { get; private set; }

        private GameObject _rallyMarker;

        /// <summary>
        /// Assigns the definition and owning faction. Called by <see cref="BuildingPlacement"/>
        /// or <see cref="EconomyManager"/> right after this building is spawned.
        /// </summary>
        /// <param name="definition">This building's data definition.</param>
        /// <param name="faction">The faction placing this building.</param>
        public void Initialize(BuildingDefinition definition, Faction faction)
        {
            _definition = definition;
            _faction = faction;
        }

        /// <summary>
        /// Caches the Health component (applying this building's configured max health), its
        /// own renderers (for fog-of-war visibility toggling), and registers this building
        /// with the <see cref="EconomyManager"/> — applying its power footprint immediately
        /// only if it's not under construction (issue #42; a hand-placed building with
        /// <see cref="BeginConstruction"/> never called defaults to fully-functional).
        /// </summary>
        private void Start()
        {
            HealthComponent = GetComponent<Health>();
            if (HealthComponent != null && _definition != null)
            {
                HealthComponent.SetMaxHealth(_definition.maxHealth);
            }

            _renderers = new List<Renderer>(GetComponentsInChildren<Renderer>(true));

            _economyManager = FindFirstObjectByType<EconomyManager>();
            _economyManager.RegisterBuilding(this);
            if (!IsUnderConstruction)
            {
                ApplyPowerFootprintIfNeeded();
            }

            _visionManager = FindFirstObjectByType<VisionManager>();
        }

        /// <summary>
        /// Marks this building as under construction (issue #42) — called by whichever
        /// component spawns it (<see cref="Units.CrewmanBuildBehavior"/>) immediately after
        /// <see cref="Initialize"/>, before <see cref="Start"/> runs, mirroring the
        /// Initialize-then-Start convention this class and <see cref="MainBuilding"/> already use.
        /// </summary>
        public void BeginConstruction()
        {
            IsUnderConstruction = true;
            ConstructionProgress01 = 0f;
        }

        /// <summary>
        /// Updates this building's construction progress readout, from 0 to 1. Called every
        /// tick by <see cref="Units.CrewmanBuildBehavior"/> while constructing.
        /// </summary>
        /// <param name="progress01">The new progress, clamped to [0, 1].</param>
        public void SetConstructionProgress(float progress01)
        {
            ConstructionProgress01 = Mathf.Clamp01(progress01);
        }

        /// <summary>
        /// Marks construction complete and applies this building's power footprint, which was
        /// deliberately withheld until now (issue #42) so an under-construction building never
        /// contributes power capacity/draw.
        /// </summary>
        public void CompleteConstruction()
        {
            IsUnderConstruction = false;
            ConstructionProgress01 = 1f;
            ApplyPowerFootprintIfNeeded();
        }

        /// <summary>
        /// Applies this building's power footprint to its faction's economy state exactly
        /// once, whenever that first becomes valid (immediately in <see cref="Start"/> for a
        /// building that was never under construction, or on <see cref="CompleteConstruction"/>
        /// otherwise). <see cref="EconomyManager.DeregisterBuilding"/> checks
        /// <see cref="HasAppliedPowerFootprint"/> so it only ever subtracts a footprint that
        /// was actually added.
        /// </summary>
        private void ApplyPowerFootprintIfNeeded()
        {
            if (_powerFootprintApplied || _economyManager == null || _definition == null) return;
            _powerFootprintApplied = true;
            _economyManager.ApplyBuildingPowerFootprint(this);
        }

        /// <summary>Whether this building's power footprint has been applied — see <see cref="ApplyPowerFootprintIfNeeded"/>.</summary>
        public bool HasAppliedPowerFootprint => _powerFootprintApplied;

        /// <summary>
        /// Applies the Player faction's current fog-of-war state to this building's
        /// visibility (issue #36) — see <see cref="FogVisibility.Apply"/>.
        /// </summary>
        private void Update()
        {
            FogVisibility.Apply(_faction, transform.position, _visionManager, _renderers, ref _ghostMarker);
        }

        /// <summary>
        /// Deregisters this building from the <see cref="EconomyManager"/> and destroys its
        /// rally point marker and fog ghost marker, if any, when destroyed.
        /// </summary>
        private void OnDestroy()
        {
            _economyManager?.DeregisterBuilding(this);
            if (_rallyMarker != null) Destroy(_rallyMarker);
            if (_ghostMarker != null) Destroy(_ghostMarker);
        }

        /// <summary>
        /// Sets (or moves) this building's rally point, creating its marker the first time
        /// and repositioning it on subsequent calls. Called by <see cref="Units.AttackCommand"/>
        /// when this building is selected and the player right-clicks a ground location.
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
        /// Clears this building's rally point and destroys its marker. Called by
        /// <see cref="Units.AttackCommand"/> when this building is selected and the player
        /// right-clicks the building itself.
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
        /// A sabotaged building stops functioning (production pauses) for a tunable duration.
        /// </summary>
        /// <param name="effectConfig">Supplies the disable duration.</param>
        public void ApplySabotage(SabotageEffectConfig effectConfig)
        {
            StartCoroutine(DisableTemporarily(effectConfig.buildingDisableDuration));
        }

        /// <summary>
        /// Marks this building disabled for the given duration, then re-enables it.
        /// </summary>
        /// <param name="duration">Seconds to remain disabled.</param>
        private IEnumerator DisableTemporarily(float duration)
        {
            _isSabotageDisabled = true;
            yield return new WaitForSeconds(duration);
            _isSabotageDisabled = false;
        }
    }
}
