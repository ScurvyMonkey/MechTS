using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// Which kind of target (by <see cref="UnitBase.IsFlying"/>) a <see cref="Weapon"/> can
    /// engage (issue #30). Buildings/main buildings are always treated as ground targets.
    /// </summary>
    public enum WeaponTargetType
    {
        Ground,
        Air,
        GroundAndAir
    }

    /// <summary>
    /// Designer-tunable stats for a <see cref="Weapon"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Units/Weapon Config")]
    public class WeaponConfig : ScriptableObject
    {
        public float damage = 10f;
        public float fireRate = 1f;
        public float range = 10f;
        public WeaponTargetType targetType = WeaponTargetType.GroundAndAir;

        /// <summary>
        /// This weapon's damage type (issue #55) — fixed for the weapon's lifetime, never
        /// swapped at runtime. Determines which damage-type-scoped offense stat
        /// (<see cref="Economy.UpgradeStatType.PlasmaDamage"/>/<see cref="Economy.UpgradeStatType.PhysicalDamage"/>)
        /// applies to this weapon's damage, and which resistance stat the target's faction
        /// is checked against.
        /// </summary>
        public DamageType damageType = DamageType.Physical;

        [Header("Firing Effects")]
        public GameObject projectileEffectPrefab;
        public GameObject casingEffectPrefab;

        [Header("Firing Audio")]
        public AudioClip fireSound;
        public AudioClip impactSound;
    }
}
