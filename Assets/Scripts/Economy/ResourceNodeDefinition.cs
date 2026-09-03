using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Designer-tunable definition of a resource node's type, finite yield, and
    /// scattered-cluster visual (a single lightweight object instanced many times
    /// across the node's footprint, shrinking in count as the pool is extracted).
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Economy/Resource Node Definition")]
    public class ResourceNodeDefinition : ScriptableObject
    {
        public ResourceType resourceType;
        public int yieldAmount = 5000;

        [Header("Scatter Visual")]
        [Tooltip("The single lightweight visual instance scattered many times to represent this node's yield (e.g. one tree sprite for a Biomass area). Ignored if useTieredScatterArt is set.")]
        public GameObject scatterObjectPrefab;
        [Tooltip("How many copies of scatterObjectPrefab to scatter across the node's footprint.")]
        public int scatterObjectCount = 15;
        [Tooltip("How far from the node's center, on the local XZ plane, scattered objects may be placed.")]
        public float scatterRadius = 4f;

        [Header("Tiered Depletion Art (issue #59)")]
        [Tooltip("If set, every visible scatter instance swaps together between tierPrefabHigh/Mid/Low as remaining yield crosses the two thresholds below, instead of scatterObjectPrefab. Additive opt-in — every node type that doesn't set this keeps using scatterObjectPrefab unchanged.")]
        public bool useTieredScatterArt;
        [Tooltip("Used while remaining yield fraction is at or above tierThresholdHigh.")]
        public GameObject tierPrefabHigh;
        [Tooltip("Used while remaining yield fraction is below tierThresholdHigh and at or above tierThresholdLow.")]
        public GameObject tierPrefabMid;
        [Tooltip("Used while remaining yield fraction is below tierThresholdLow.")]
        public GameObject tierPrefabLow;
        [Tooltip("Remaining yield fraction (0-1) at or above which tierPrefabHigh is used.")]
        public float tierThresholdHigh = 0.66f;
        [Tooltip("Remaining yield fraction (0-1) at or above which tierPrefabMid is used (below tierThresholdHigh).")]
        public float tierThresholdLow = 0.33f;
    }
}
