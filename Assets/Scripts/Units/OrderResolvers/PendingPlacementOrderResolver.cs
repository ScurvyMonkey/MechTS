using MechTS.Economy;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// Thin adapter wrapping <see cref="BuildingPlacement.TryHandlePendingPlacementClick"/> as
    /// an <see cref="IRightClickOrderResolver"/> (issue #60) — highest priority in
    /// <see cref="AttackCommand"/>'s dispatch chain, unchanged from before this refactor.
    /// </summary>
    public class PendingPlacementOrderResolver : IRightClickOrderResolver
    {
        private readonly BuildingPlacement _buildingPlacement;
        private readonly LayerMask _groundLayerMask;

        /// <summary>
        /// Wraps the given <see cref="BuildingPlacement"/> and ground layer mask.
        /// </summary>
        /// <param name="buildingPlacement">The scene's <see cref="BuildingPlacement"/> component.</param>
        /// <param name="groundLayerMask">The ground layer mask to raycast against.</param>
        public PendingPlacementOrderResolver(BuildingPlacement buildingPlacement, LayerMask groundLayerMask)
        {
            _buildingPlacement = buildingPlacement;
            _groundLayerMask = groundLayerMask;
        }

        /// <summary>
        /// Delegates to <see cref="BuildingPlacement.TryHandlePendingPlacementClick"/>.
        /// </summary>
        /// <param name="ray">The right-click's camera ray.</param>
        public bool TryHandle(Ray ray)
        {
            return _buildingPlacement.TryHandlePendingPlacementClick(ray, _groundLayerMask);
        }
    }
}
