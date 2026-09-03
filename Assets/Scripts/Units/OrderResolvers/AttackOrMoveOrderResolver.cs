using MechTS.Economy;
using MechTS.Vision;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// Lowest-priority resolver in <see cref="AttackCommand"/>'s dispatch chain (issue #60):
    /// resolves an enemy unit/Main Building hit into an Attack order (moving to it and locking
    /// every selected unit's <see cref="Weapon"/> onto it, issue #31), or falls back to a plain
    /// Move order at the clicked ground position. Moving units auto-engage any enemy within
    /// weapon range via <see cref="Weapon"/>'s own targeting, so an Attack order here is a
    /// move-to-target command rather than a continuous chase of a moving target.
    /// </summary>
    public class AttackOrMoveOrderResolver : IRightClickOrderResolver
    {
        private readonly UnitManager _unitManager;
        private readonly VisionManager _visionManager;
        private readonly LayerMask _targetableLayerMask;
        private readonly LayerMask _groundLayerMask;

        /// <summary>
        /// Wraps the given manager references and layer masks.
        /// </summary>
        /// <param name="unitManager">The scene's <see cref="UnitManager"/> component.</param>
        /// <param name="visionManager">The scene's <see cref="VisionManager"/> component, or null if none exists.</param>
        /// <param name="targetableLayerMask">The layer mask units/buildings/main buildings share.</param>
        /// <param name="groundLayerMask">The ground layer mask to raycast against.</param>
        public AttackOrMoveOrderResolver(UnitManager unitManager, VisionManager visionManager, LayerMask targetableLayerMask, LayerMask groundLayerMask)
        {
            _unitManager = unitManager;
            _visionManager = visionManager;
            _targetableLayerMask = targetableLayerMask;
            _groundLayerMask = groundLayerMask;
        }

        /// <summary>
        /// Resolves an Attack order against a visible enemy unit/Main Building, or a plain Move
        /// order at the clicked ground position otherwise.
        /// </summary>
        /// <param name="ray">The right-click's camera ray.</param>
        public bool TryHandle(Ray ray)
        {
            if (Physics.Raycast(ray, out RaycastHit targetHit, 1000f, _targetableLayerMask))
            {
                var unit = targetHit.collider.GetComponentInParent<UnitBase>();
                if (unit != null && unit.Faction != Faction.Player && IsVisibleToPlayer(unit.transform.position))
                {
                    _unitManager.MoveSelectedTo(unit.transform.position);
                    SetExplicitTargetOnSelection(unit.HealthComponent);
                    return true;
                }

                var mainBuilding = targetHit.collider.GetComponentInParent<MainBuilding>();
                if (mainBuilding != null && mainBuilding.Faction != Faction.Player && IsVisibleToPlayer(mainBuilding.transform.position))
                {
                    _unitManager.MoveSelectedTo(mainBuilding.transform.position);
                    SetExplicitTargetOnSelection(mainBuilding.HealthComponent);
                    return true;
                }
            }

            if (Physics.Raycast(ray, out RaycastHit groundHit, 1000f, _groundLayerMask))
            {
                _unitManager.MoveSelectedTo(groundHit.point);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Locks every selected unit's <see cref="Weapon"/> (if any) onto the given target
        /// (issue #31). Called after <see cref="UnitManager.MoveSelectedTo"/>, which already
        /// clears any previous lock as part of its own order-cancellation step — setting the
        /// new lock afterward avoids it being immediately wiped by that same call. A no-op for
        /// units without a Weapon (Harvesters, Saboteurs).
        /// </summary>
        /// <param name="target">The enemy Health component to lock onto.</param>
        private void SetExplicitTargetOnSelection(Health target)
        {
            if (target == null) return;
            foreach (var selectedUnit in _unitManager.SelectedUnits)
            {
                selectedUnit.GetComponent<Weapon>()?.SetExplicitTarget(target);
            }
        }

        /// <summary>
        /// Whether a world position is currently visible to the Player faction (issue #37) —
        /// gates Attack order resolution so a right-click on an unseen enemy falls through to a
        /// plain Move order instead. Always true if no <see cref="VisionManager"/> exists.
        /// </summary>
        /// <param name="worldPosition">The world position to check.</param>
        private bool IsVisibleToPlayer(Vector3 worldPosition)
        {
            return _visionManager == null || _visionManager.IsVisible(Faction.Player, worldPosition);
        }
    }
}
