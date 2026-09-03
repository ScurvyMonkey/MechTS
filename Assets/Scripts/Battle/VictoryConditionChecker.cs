using MechTS.Economy;
using MechTS.Units;

namespace MechTS.Battle
{
    /// <summary>
    /// Stateless helper that evaluates whether a faction is eliminated, per the
    /// corrected Battle Round rules (see arch review, issue #6): a faction is
    /// eliminated only on a true wipeout (no buildings AND no units AND no standing
    /// main building — issue #28) or a fully exhausted economy (no resources AND no
    /// production buildings AND no units — issue #48) — never on a momentary
    /// zero-units count alone, since production carryover means a faction with intact
    /// buildings can legitimately have zero current units. The economy-exhaustion
    /// check's own <c>noUnits</c> requirement (added in #48, alongside continuous unit
    /// upkeep) means a faction actively fighting with a negative stockpile — the cost
    /// of a deliberate all-in strategy — is never eliminated on economy grounds while
    /// it still has an army on the field.
    /// </summary>
    public static class VictoryConditionChecker
    {
        /// <summary>
        /// Returns whether the given faction is eliminated.
        /// </summary>
        /// <param name="faction">The faction to evaluate.</param>
        /// <param name="unitManager">The scene's UnitManager.</param>
        /// <param name="economyManager">The scene's EconomyManager.</param>
        public static bool IsEliminated(Faction faction, UnitManager unitManager, EconomyManager economyManager)
        {
            return GetEliminationReason(faction, unitManager, economyManager) != null;
        }

        /// <summary>
        /// Returns the specific reason the given faction is eliminated, or null if it isn't.
        /// Checks wipeout before economy exhaustion, matching <see cref="IsEliminated"/>'s
        /// combined check.
        /// </summary>
        /// <param name="faction">The faction to evaluate.</param>
        /// <param name="unitManager">The scene's UnitManager.</param>
        /// <param name="economyManager">The scene's EconomyManager.</param>
        public static VictoryReason? GetEliminationReason(Faction faction, UnitManager unitManager, EconomyManager economyManager)
        {
            var state = economyManager.GetState(faction);
            if (state == null) return null;

            bool noBuildings = state.Buildings.Count == 0;
            bool noUnits = unitManager.CountByFaction(faction) == 0;
            bool noMainBuilding = !economyManager.MainBuildings.TryGetValue(faction, out var mainBuilding) || mainBuilding == null;
            if (noBuildings && noUnits && noMainBuilding) return VictoryReason.Wipeout;

            // Stockpile.IsEmpty() has no visibility into LockedPool (issue #52) — a faction
            // sitting on a full Silo but an empty normal stockpile still reads as exhausted
            // here. Harmless today since the pool is unspendable, but revisit this once the
            // Injection Round (part 5 of the hybrid-economy sequence) makes it spendable.
            bool noEconomy = state.Stockpile.IsEmpty() && !state.HasProductionBuilding() && noUnits;
            if (noEconomy) return VictoryReason.EconomyExhausted;

            return null;
        }
    }
}
