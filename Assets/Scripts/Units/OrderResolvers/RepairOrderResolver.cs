using MechTS.Economy;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// If the current selection is entirely Crewmen and the raycast hit a valid repair target —
    /// a friendly (Player-faction) building or main building currently below max health (issue
    /// #42) — assigns every selected Crewman to repair it (extracted into its own resolver by
    /// issue #60). Never conflicts with <see cref="SabotageOrderResolver"/>, which only ever
    /// matches a non-Player-owned target, so the two are already mutually exclusive by
    /// construction.
    /// </summary>
    public class RepairOrderResolver : IRightClickOrderResolver
    {
        private readonly UnitManager _unitManager;
        private readonly LayerMask _targetableLayerMask;

        /// <summary>
        /// Wraps the given <see cref="UnitManager"/> and targetable layer mask.
        /// </summary>
        /// <param name="unitManager">The scene's <see cref="UnitManager"/> component.</param>
        /// <param name="targetableLayerMask">The layer mask units/buildings/main buildings share.</param>
        public RepairOrderResolver(UnitManager unitManager, LayerMask targetableLayerMask)
        {
            _unitManager = unitManager;
            _targetableLayerMask = targetableLayerMask;
        }

        /// <summary>
        /// Resolves the repair order if the whole selection is Crewmen and the click hit a
        /// friendly, damaged repair target.
        /// </summary>
        /// <param name="ray">The right-click's camera ray.</param>
        public bool TryHandle(Ray ray)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, _targetableLayerMask)) return false;

            var repairTarget = hit.collider.GetComponentInParent<IRepairTarget>();
            if (repairTarget == null) return false;
            if (repairTarget.Faction != Faction.Player) return false;

            var health = repairTarget.HealthComponent;
            if (health == null || health.CurrentHealth >= health.MaxHealth) return false;

            if (_unitManager.SelectedUnits.Count == 0) return false;
            foreach (var unit in _unitManager.SelectedUnits)
            {
                if (!(unit is CrewmanUnit)) return false;
            }

            foreach (var unit in _unitManager.SelectedUnits)
            {
                ((CrewmanUnit)unit).AssignRepairTarget(repairTarget);
            }
            return true;
        }
    }
}
