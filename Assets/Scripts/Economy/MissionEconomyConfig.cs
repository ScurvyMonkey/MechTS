using System;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// One faction's starting Economy Round setup for a mission: starting resources and its
    /// main building's prefab. Where the main building spawns is no longer stored here — it
    /// comes from the matching <see cref="PlayerStartPoint"/> painted in the scene via the
    /// Map Editor (issue #57), found by faction at spawn time.
    /// </summary>
    [Serializable]
    public class FactionEconomyStart
    {
        public Units.Faction faction;
        public int startingOre;
        public int startingBiomass;
        public int startingGold;
        public GameObject mainBuildingPrefab;

        /// <summary>
        /// Max health for this faction's main building (issue #28) — notably higher than a
        /// regular building's, since it's the anchor structure. Designer-adjustable; not a
        /// final balance number.
        /// </summary>
        public float mainBuildingMaxHealth = 500f;

        /// <summary>
        /// This faction's main building's vision radius (issue #36) — designer-adjustable,
        /// not a final balance number.
        /// </summary>
        public float mainBuildingVisionRadius = 15f;

        /// <summary>
        /// The units this faction's main building can produce directly (issue #41 — SC2-style
        /// command-center-produces-workers), mirroring <see cref="BuildingDefinition.producibleUnits"/>'s
        /// shape. Left empty/unassigned, the main building simply shows no producible units,
        /// same as a building with no producible-units list configured.
        /// </summary>
        public UnitProductionDefinition[] mainBuildingProducibleUnits;
    }

    /// <summary>
    /// Designer-tunable per-mission Economy Round setup: each faction's starting
    /// resources and main building placement, and the round's duration.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Economy/Mission Economy Config")]
    public class MissionEconomyConfig : ScriptableObject
    {
        public float economyRoundDurationSeconds = 900f;
        public FactionEconomyStart[] factionStarts;

        /// <summary>The buildings the player can place this mission, shown in the build menu.</summary>
        public BuildingDefinition[] buildableBuildings;
    }
}
