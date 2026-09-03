using MechTS.Economy;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// If the current selection is entirely Harvesters and the right-click hit a
    /// <see cref="ResourceNode"/>, redirects every selected Harvester to gather from it —
    /// overriding whatever it's currently doing (issue #38, extracted into its own resolver by
    /// issue #60). <see cref="ResourceNode"/> sits on its own physics layer (not the targetable
    /// layer other resolvers raycast against, same as <see cref="SelectionController"/>'s own
    /// resource-node fallback raycast), so this needs an independent raycast.
    /// </summary>
    public class HarvestRedirectOrderResolver : IRightClickOrderResolver
    {
        private readonly UnitManager _unitManager;
        private readonly LayerMask _resourceNodeLayerMask;

        /// <summary>
        /// Wraps the given <see cref="UnitManager"/> and resource-node layer mask.
        /// </summary>
        /// <param name="unitManager">The scene's <see cref="UnitManager"/> component.</param>
        /// <param name="resourceNodeLayerMask">The layer mask <see cref="ResourceNode"/> instances sit on.</param>
        public HarvestRedirectOrderResolver(UnitManager unitManager, LayerMask resourceNodeLayerMask)
        {
            _unitManager = unitManager;
            _resourceNodeLayerMask = resourceNodeLayerMask;
        }

        /// <summary>
        /// Resolves the redirect if the whole selection is Harvesters and the click hit a node.
        /// </summary>
        /// <param name="ray">The right-click's camera ray.</param>
        public bool TryHandle(Ray ray)
        {
            if (_unitManager.SelectedUnits.Count == 0) return false;
            foreach (var unit in _unitManager.SelectedUnits)
            {
                if (!(unit is HarvesterUnit)) return false;
            }

            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, _resourceNodeLayerMask)) return false;

            var node = hit.collider.GetComponentInParent<ResourceNode>();
            if (node == null) return false;

            foreach (var unit in _unitManager.SelectedUnits)
            {
                ((HarvesterUnit)unit).AssignToNode(node);
            }
            return true;
        }
    }
}
