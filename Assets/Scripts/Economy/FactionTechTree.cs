using System;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// One faction's set of researchable upgrades for a mission. A plain serializable
    /// class rather than a ScriptableObject, mirroring <see cref="FactionEconomyStart"/>'s
    /// existing per-faction-array pattern on <see cref="MissionEconomyConfig"/>.
    /// </summary>
    [Serializable]
    public class FactionTechTree
    {
        public Units.Faction faction;
        public UpgradeDefinition[] availableUpgrades;
    }
}
