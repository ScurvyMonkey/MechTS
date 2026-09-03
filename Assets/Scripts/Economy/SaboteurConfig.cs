using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Designer-tunable stats for the Saboteur unit.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Economy/Saboteur Config")]
    public class SaboteurConfig : ScriptableObject
    {
        public float maxHealth = 30f;
        public float moveSpeed = 4f;
        public float sabotageRange = 1.5f;
        public float sabotageChannelDuration = 3f;

        [Header("Production Cost")]
        public int oreCost;
        public int biomassCost;
        public int goldCost;
        public int powerConsumed;

        /// <summary>
        /// This Saboteur's ongoing upkeep cost per minute while active during the Economy
        /// Round (issue #48). Pushed into <see cref="Units.UnitBase.SetUpkeepCost"/> from
        /// <see cref="SabotageAction.Start"/>, alongside the existing maxHealth/moveSpeed calls.
        /// </summary>
        [Header("Upkeep (per minute)")]
        public int oreUpkeepPerMinute;
        public int biomassUpkeepPerMinute;
        public int goldUpkeepPerMinute = 2;
    }
}
