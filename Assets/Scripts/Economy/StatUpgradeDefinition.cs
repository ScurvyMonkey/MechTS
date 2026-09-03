using MechTS.Units;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// An upgrade that modifies a unit/building/economy stat for the researching faction
    /// (e.g. a "Damage I" upgrade). Registers itself with <see cref="TechManager"/> on
    /// research — reading the resulting multiplier at the point of use (Weapon, Health,
    /// etc.) is separate, later work; this type only tracks the modifier.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Tech/Stat Upgrade")]
    public class StatUpgradeDefinition : UpgradeDefinition
    {
        public UpgradeStatType statType;
        public ModifierType modifierType;
        public float value;

        /// <summary>
        /// Which units this upgrade applies to (issue #39) — <see cref="UnitCapability.None"/>
        /// (the default) means unfiltered, applying to every unit regardless of capability,
        /// matching every pre-existing upgrade asset's behavior with zero data migration needed.
        /// </summary>
        public UnitCapability targetCapabilities = UnitCapability.None;

        /// <summary>
        /// Registers this stat modifier with <paramref name="techManager"/> for
        /// <paramref name="faction"/>.
        /// </summary>
        /// <param name="faction">The faction that researched this upgrade.</param>
        /// <param name="techManager">The tech manager to register the modifier with.</param>
        public override void Apply(Units.Faction faction, TechManager techManager)
        {
            techManager.RegisterStatModifier(faction, statType, modifierType, value, targetCapabilities);
        }

        /// <summary>
        /// Removes exactly the stat modifier this upgrade registered (issue #54) — matched by
        /// value, since this instance already knows precisely what it registered.
        /// </summary>
        /// <param name="faction">The faction reverting this upgrade.</param>
        /// <param name="techManager">The tech manager to remove the modifier from.</param>
        public override void Revert(Units.Faction faction, TechManager techManager)
        {
            techManager.RemoveStatModifier(faction, statType, modifierType, value, targetCapabilities);
        }
    }
}
