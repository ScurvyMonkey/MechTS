using MechTS.Economy;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// If the current selection is entirely Saboteurs and the raycast hit a valid sabotage
    /// target (never the enemy's own faction, never a main building), assigns every selected
    /// Saboteur to sabotage it (issue #3/#31, extracted into its own resolver by issue #60).
    /// </summary>
    public class SabotageOrderResolver : IRightClickOrderResolver
    {
        private readonly UnitManager _unitManager;
        private readonly LayerMask _targetableLayerMask;

        /// <summary>
        /// Wraps the given <see cref="UnitManager"/> and targetable layer mask.
        /// </summary>
        /// <param name="unitManager">The scene's <see cref="UnitManager"/> component.</param>
        /// <param name="targetableLayerMask">The layer mask units/buildings/main buildings share.</param>
        public SabotageOrderResolver(UnitManager unitManager, LayerMask targetableLayerMask)
        {
            _unitManager = unitManager;
            _targetableLayerMask = targetableLayerMask;
        }

        /// <summary>
        /// Resolves the sabotage order if the whole selection is Saboteurs and the click hit a
        /// valid, non-Player-owned, non-main-building sabotage target.
        /// </summary>
        /// <param name="ray">The right-click's camera ray.</param>
        public bool TryHandle(Ray ray)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, _targetableLayerMask)) return false;

            var sabotageTarget = hit.collider.GetComponentInParent<ISabotageTarget>();
            if (sabotageTarget == null) return false;
            if (hit.collider.GetComponentInParent<MainBuilding>() != null) return false;

            var targetUnit = hit.collider.GetComponentInParent<UnitBase>();
            if (targetUnit != null && targetUnit.Faction == Faction.Player) return false;

            var targetBuilding = hit.collider.GetComponentInParent<BuildingInstance>();
            if (targetBuilding != null && targetBuilding.Faction == Faction.Player) return false;

            if (_unitManager.SelectedUnits.Count == 0) return false;
            foreach (var unit in _unitManager.SelectedUnits)
            {
                if (!(unit is SaboteurUnit)) return false;
            }

            foreach (var unit in _unitManager.SelectedUnits)
            {
                ((SaboteurUnit)unit).AssignSabotageTarget(sabotageTarget);
            }
            return true;
        }
    }
}
