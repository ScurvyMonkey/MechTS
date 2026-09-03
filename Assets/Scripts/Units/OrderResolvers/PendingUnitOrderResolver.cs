using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// Thin adapter wrapping <see cref="UnitOrderCommand.TryHandlePendingOrderClick"/> as an
    /// <see cref="IRightClickOrderResolver"/> (issue #60) — resolves an armed Attack-Move/
    /// Patrol order (issue #26), unchanged from before this refactor.
    /// </summary>
    public class PendingUnitOrderResolver : IRightClickOrderResolver
    {
        private readonly UnitOrderCommand _unitOrderCommand;
        private readonly LayerMask _groundLayerMask;

        /// <summary>
        /// Wraps the given <see cref="UnitOrderCommand"/> and ground layer mask.
        /// </summary>
        /// <param name="unitOrderCommand">The scene's <see cref="UnitOrderCommand"/> component.</param>
        /// <param name="groundLayerMask">The ground layer mask to raycast against.</param>
        public PendingUnitOrderResolver(UnitOrderCommand unitOrderCommand, LayerMask groundLayerMask)
        {
            _unitOrderCommand = unitOrderCommand;
            _groundLayerMask = groundLayerMask;
        }

        /// <summary>
        /// Delegates to <see cref="UnitOrderCommand.TryHandlePendingOrderClick"/>.
        /// </summary>
        /// <param name="ray">The right-click's camera ray.</param>
        public bool TryHandle(Ray ray)
        {
            return _unitOrderCommand.TryHandlePendingOrderClick(ray, _groundLayerMask);
        }
    }
}
