namespace MechTS.Economy
{
    /// <summary>
    /// A unit/building/economy stat a <see cref="StatUpgradeDefinition"/> can modify.
    /// </summary>
    public enum UpgradeStatType
    {
        Damage,
        Range,
        FireRate,
        Health,
        MoveSpeed,
        GatherRate,
        PowerCapacity,
        PassiveGeneration,

        /// <summary>
        /// Damage-type-scoped offense/defense (issue #55) — applies alongside (not instead
        /// of) the generic <see cref="Damage"/> stat, only to weapons/damage of the matching
        /// <see cref="Units.DamageType"/>. Resistance upgrades must use a negative
        /// <see cref="ModifierType.Percentage"/> value for <see cref="TechManager.GetStatMultiplier"/>'s
        /// existing formula to reduce (not increase) damage taken.
        /// </summary>
        PlasmaDamage,
        PhysicalDamage,
        PlasmaResistance,
        PhysicalResistance
    }

    /// <summary>
    /// How a <see cref="StatUpgradeDefinition"/>'s value combines into
    /// <see cref="TechManager.GetStatMultiplier"/>'s result — see that method's doc
    /// comment for the exact combination formula.
    /// </summary>
    public enum ModifierType
    {
        Flat,
        Percentage
    }
}
