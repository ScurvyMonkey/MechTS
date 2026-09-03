using System.Collections.Generic;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Designer-tunable definition of a placeable building: its resource cost, power
    /// footprint, and optional detection or production capability.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Economy/Building Definition")]
    public class BuildingDefinition : ScriptableObject
    {
        public string displayName;
        public GameObject prefab;

        [Header("Cost")]
        public int oreCost;
        public int biomassCost;
        public int goldCost;

        [Header("Power")]
        public int powerGenerated;
        public int powerConsumed;

        public float buildTime;

        [Header("Combat")]
        public float maxHealth = 100f;

        /// <summary>
        /// This building's baseline vision radius, contributing to its owning faction's fog
        /// of war (issue #36). Replaced (not added to) by <see cref="detectionRadius"/> when
        /// <see cref="isDetectionCapable"/> is true.
        /// </summary>
        [Header("Vision")]
        public float visionRadius = 8f;

        [Header("Detection (Espionage counter)")]
        public bool isDetectionCapable;
        public float detectionRadius;

        [Header("Production")]
        public bool canProduceUnits;
        public UnitProductionDefinition[] producibleUnits;

        /// <summary>
        /// Whether owning at least one building with this flag set unlocks research for that
        /// faction (issue #45 — the Technology Plant). <see cref="TechManager.CanResearch"/>
        /// requires this in addition to its existing prerequisite/affordability checks.
        /// </summary>
        [Header("Ownership Gates")]
        public bool enablesResearch;

        /// <summary>
        /// Whether owning at least one building with this flag set unlocks turret
        /// construction for that faction (issue #45 — the Robotics Plant). Checked by
        /// <see cref="UI.BuildMenuPanel"/> against every buildable definition with
        /// <see cref="requiresRoboticsPlant"/> set.
        /// </summary>
        public bool enablesTurretConstruction;

        /// <summary>
        /// Whether placing this specific building requires the placing faction to already
        /// own a building with <see cref="enablesTurretConstruction"/> set (issue #45 —
        /// turret definitions). A separate, ownership-driven gate from
        /// <see cref="TechManager.IsUnlocked"/>'s research-driven one — not something the
        /// issue's own field list named explicitly, but required to actually distinguish
        /// "this building is a turret" from every other buildable definition; flagged in the
        /// completion report as a necessary addition beyond the issue's literal two-field ask.
        /// </summary>
        public bool requiresRoboticsPlant;

        /// <summary>
        /// Whether this building may only be placed within a <see cref="ResourceNode"/>'s
        /// <see cref="ResourceNodeDefinition.scatterRadius"/> (issue #51 — the Auto-Extractor).
        /// Checked by <see cref="BuildingPlacement"/> as an additional condition alongside its
        /// existing no-overlap-with-other-buildings check, not a replacement for it.
        /// </summary>
        [Header("Placement")]
        public bool requiresResourceNode;

        /// <summary>
        /// How much of its host <see cref="ResourceNode"/>'s yield this building extracts per
        /// second once constructed (issue #51 — only meaningful when <see cref="requiresResourceNode"/>
        /// is true). See <see cref="AutoGatherBehavior"/>.
        /// </summary>
        public float autoGatherRatePerSecond;

        /// <summary>
        /// Whether this building is a Silo (issue #52) — contributes <see cref="siloCapacity"/>
        /// toward its owning faction's locked-pool capacity for <see cref="siloResourceType"/>,
        /// applied/removed at the same moment as <see cref="powerGenerated"/>/<see cref="powerConsumed"/>
        /// (see <see cref="EconomyManager.ApplyBuildingPowerFootprint"/>) — including staying
        /// applied while sabotage-disabled, matching that same power-capacity precedent.
        /// </summary>
        [Header("Silo")]
        public bool isSilo;

        /// <summary>The single resource type this Silo stores (issue #52). Meaningless unless <see cref="isSilo"/> is true.</summary>
        public ResourceType siloResourceType;

        /// <summary>How much locked-pool capacity this Silo instance contributes for <see cref="siloResourceType"/> (issue #52).</summary>
        public int siloCapacity;

        /// <summary>
        /// Returns this building's resource cost as a lookup keyed by resource type.
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
    }
}
