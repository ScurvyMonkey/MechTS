using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Designer-tunable stats for the Crewman unit (issue #42).
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Economy/Crewman Config")]
    public class CrewmanConfig : ScriptableObject
    {
        public float maxHealth = 60f;
        public float moveSpeed = 3.5f;

        /// <summary>How much health this Crewman restores per second while repairing a target.</summary>
        public float repairRatePerSecond = 10f;

        [Header("Production Cost")]
        public int oreCost;
        public int biomassCost;
        public int goldCost;
        public int powerConsumed;

        /// <summary>
        /// This Crewman's ongoing upkeep cost per minute while active during the Economy
        /// Round (issue #48). Pushed into <see cref="Units.UnitBase.SetUpkeepCost"/> from
        /// <see cref="CrewmanRepairBehavior.Start"/>, alongside the existing maxHealth/
        /// moveSpeed calls.
        /// </summary>
        [Header("Upkeep (per minute)")]
        public int oreUpkeepPerMinute;
        public int biomassUpkeepPerMinute = 1;
        public int goldUpkeepPerMinute;
    }
}
