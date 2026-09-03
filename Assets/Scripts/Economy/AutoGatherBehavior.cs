using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Sibling component on a <see cref="BuildingInstance"/> whose <see cref="BuildingDefinition.requiresResourceNode"/>
    /// is true (issue #51 — the Auto-Extractor). Passively extracts its host
    /// <see cref="ResourceNode"/>'s yield once per second and deposits it directly into the
    /// owning faction's stockpile — no Harvester round-trip required. Harvesters may still
    /// gather from the same node concurrently; both draw from the one shared pool.
    /// </summary>
    [RequireComponent(typeof(BuildingInstance))]
    public class AutoGatherBehavior : MonoBehaviour
    {
        private BuildingInstance _building;
        private EconomyManager _economyManager;
        private ResourceNode _hostNode;
        private float _gatherTimer;

        /// <summary>
        /// Caches the sibling <see cref="BuildingInstance"/> and binds to the nearest
        /// registered <see cref="ResourceNode"/> whose radius contains this building's
        /// position. If none is found — the node was destroyed between placement confirm and
        /// construction completing (by Harvesters, sabotage, or natural depletion) — this
        /// building simply never extracts, identical to a node that depletes after binding;
        /// no error, no special teardown.
        /// </summary>
        private void Start()
        {
            _building = GetComponent<BuildingInstance>();
            _economyManager = FindFirstObjectByType<EconomyManager>();
            _hostNode = FindNearestNode();
        }

        /// <summary>
        /// Finds the closest registered <see cref="ResourceNode"/> whose
        /// <see cref="ResourceNode.ScatterRadius"/> contains this building's position.
        /// </summary>
        private ResourceNode FindNearestNode()
        {
            if (_economyManager == null) return null;

            ResourceNode nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (var node in _economyManager.ResourceNodes)
            {
                if (node == null) continue;

                float distance = Vector3.Distance(transform.position, node.Position);
                if (distance <= node.ScatterRadius && distance < nearestDistance)
                {
                    nearest = node;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        /// <summary>
        /// Extracts from the host node once per second while the Economy Round is active and
        /// this building isn't under construction or sabotage-disabled, depositing straight
        /// into the owning faction's stockpile. Silently does nothing once the host node
        /// depletes (or was never found) — the building just sits there, still repairable and
        /// vision-contributing via its unrelated existing components.
        /// </summary>
        private void Update()
        {
            if (_hostNode == null || _hostNode.IsDepleted) return;
            if (_economyManager == null || !_economyManager.IsEconomyRoundActive) return;
            if (_building.IsDisabled) return;

            _gatherTimer += Time.deltaTime;
            if (_gatherTimer < 1f) return;
            _gatherTimer = 0f;

            int extracted = _hostNode.Extract(Mathf.RoundToInt(_building.Definition.autoGatherRatePerSecond));
            if (extracted > 0)
            {
                _economyManager.Deposit(_building.Faction, _hostNode.ResourceType, extracted);
            }
        }
    }
}
