using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// Designer-tunable max health/move speed for a combat unit (issue #43) — reused as a
    /// separate asset instance per unit (Ranger/Reaper/Dredge) rather than three near-identical
    /// config classes, per the issue's own explicitly-sanctioned "shared config" option.
    /// Weapon stats live on the separate <see cref="WeaponConfig"/>, matching every other
    /// armed unit's split between a stat config and a weapon config.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Units/Combat Unit Config")]
    public class CombatUnitConfig : ScriptableObject
    {
        public float maxHealth = 100f;
        public float moveSpeed = 4f;

        /// <summary>
        /// This unit's ongoing upkeep cost per minute while active during the Economy Round
        /// (issue #48) — tuned per asset instance (Ranger/Reaper/Dredge), same as maxHealth/
        /// moveSpeed. Pushed into <see cref="UnitBase.SetUpkeepCost"/> from
        /// <see cref="CombatUnitStats.Start"/>, alongside the existing maxHealth/moveSpeed calls.
        /// </summary>
        [Header("Upkeep (per minute)")]
        public int oreUpkeepPerMinute = 1;
        public int biomassUpkeepPerMinute = 1;
        public int goldUpkeepPerMinute;
    }
}
