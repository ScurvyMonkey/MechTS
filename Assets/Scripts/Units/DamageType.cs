namespace MechTS.Units
{
    /// <summary>
    /// The kind of damage a <see cref="Weapon"/> deals (issue #55). A unit's weapon type is
    /// fixed for its lifetime — what's tunable via research is which damage type a faction
    /// invests offense/defense stat upgrades into, not which type any specific weapon deals.
    /// </summary>
    public enum DamageType
    {
        Physical,
        Plasma
    }
}
