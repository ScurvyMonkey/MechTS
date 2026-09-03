using System.Collections.Generic;
using MechTS.Units;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Designer-tunable definition of a unit that can be queued for production at a
    /// production-capable building. Usable during both the Economy Round and the
    /// Battle Round — unit production is never gated to a single round.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Economy/Unit Production Definition")]
    public class UnitProductionDefinition : ScriptableObject
    {
        public string displayName;
        public GameObject prefab;

        /// <summary>
        /// Optional Enemy-faction prefab variant (distinct art, mechanically identical) —
        /// used by <see cref="ProductionQueue"/> instead of <see cref="prefab"/> when
        /// producing for <see cref="Faction.Enemy"/>. Left unassigned, every unit falls
        /// back to <see cref="prefab"/> for both factions, same as before this field existed.
        /// </summary>
        public GameObject enemyPrefabOverride;

        [Header("Cost")]
        public int oreCost;
        public int biomassCost;
        public int goldCost;
        public int powerConsumed;

        public float productionTime = 5f;

        /// <summary>
        /// Returns this unit's resource cost as a lookup keyed by resource type.
        /// </summary>
        public Dictionary<ResourceType, int> GetCost()
        {
            return new Dictionary<ResourceType, int>
            {
                { ResourceType.Ore, oreCost },
                { ResourceType.Biomass, biomassCost },
                { ResourceType.Gold, goldCost }
            };
        }

        /// <summary>
        /// Returns the prefab to spawn for the given faction — <see cref="enemyPrefabOverride"/>
        /// for <see cref="Faction.Enemy"/> if one is assigned, otherwise <see cref="prefab"/>.
        /// </summary>
        /// <param name="faction">The producing faction.</param>
        public GameObject GetPrefab(Faction faction)
        {
            return faction == Faction.Enemy && enemyPrefabOverride != null ? enemyPrefabOverride : prefab;
        }
    }
}
