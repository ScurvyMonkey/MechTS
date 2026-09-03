using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// An upgrade that unlocks a specific building or unit for the researching faction,
    /// in addition to (not instead of) the existing <see cref="BuildingDefinition.producibleUnits"/>
    /// gating. Leave whichever field doesn't apply null — a single upgrade unlocks at most
    /// one building or one unit.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Tech/Unlock Upgrade")]
    public class UnlockUpgradeDefinition : UpgradeDefinition
    {
        public BuildingDefinition unlockedBuilding;
        public UnitProductionDefinition unlockedUnit;

        /// <summary>
        /// Registers whichever unlock this upgrade grants with <paramref name="techManager"/>
        /// for <paramref name="faction"/>.
        /// </summary>
        /// <param name="faction">The faction that researched this upgrade.</param>
        /// <param name="techManager">The tech manager to register the unlock with.</param>
        public override void Apply(Units.Faction faction, TechManager techManager)
        {
            if (unlockedBuilding != null) techManager.RegisterUnlockedBuilding(faction, unlockedBuilding);
            if (unlockedUnit != null) techManager.RegisterUnlockedUnit(faction, unlockedUnit);
        }

        /// <summary>
        /// Removes whichever unlock this upgrade granted (issue #54).
        /// </summary>
        /// <param name="faction">The faction reverting this upgrade.</param>
        /// <param name="techManager">The tech manager to remove the unlock from.</param>
        public override void Revert(Units.Faction faction, TechManager techManager)
        {
            if (unlockedBuilding != null) techManager.DeregisterUnlockedBuilding(faction, unlockedBuilding);
            if (unlockedUnit != null) techManager.DeregisterUnlockedUnit(faction, unlockedUnit);
        }
    }
}
