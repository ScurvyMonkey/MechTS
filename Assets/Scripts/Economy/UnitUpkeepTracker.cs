using System;
using System.Collections.Generic;
using MechTS.Units;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Per-faction fractional Ore/Biomass/Gold upkeep accrual and drain (issue #48), extracted
    /// from <see cref="EconomyManager"/> (issue #61) into its own composed class. Upkeep costs
    /// are tuned as "per minute" but applied continuously, so fractional amounts accrue per
    /// faction until they cross a whole resource unit — otherwise a small per-unit cost (e.g.
    /// 1/min) would truncate to zero every tick and never actually drain.
    /// </summary>
    public class UnitUpkeepTracker
    {
        private class Accrual
        {
            public float Ore;
            public float Biomass;
            public float Gold;
        }

        private readonly Dictionary<Faction, Accrual> _accrual = new Dictionary<Faction, Accrual>();
        private static readonly Faction[] AllFactions = { Faction.Player, Faction.Enemy };

        /// <summary>
        /// Clears every faction's accrued fractional debt — call on every fresh Economy Round
        /// entry so accrued debt never survives into a new attempt.
        /// </summary>
        public void Clear()
        {
            _accrual.Clear();
        }

        /// <summary>
        /// Drains each faction's stockpile by its active units' total upkeep cost for this
        /// tick. Spends directly via <see cref="ResourceStockpile.Spend"/> rather than gating
        /// on <see cref="ResourceStockpile.CanAfford"/> first — upkeep is allowed to push a
        /// faction's stockpile negative by design (the cost of an all-in strategy).
        /// </summary>
        /// <param name="deltaTime">Seconds elapsed since the last tick.</param>
        /// <param name="activeUnits">Every currently active unit, across both factions.</param>
        /// <param name="getState">Resolves a faction's live economy state, or null if it has none this mission.</param>
        public void Tick(float deltaTime, IEnumerable<UnitBase> activeUnits, Func<Faction, FactionEconomyState> getState)
        {
            var perMinuteTotals = new Dictionary<Faction, Accrual>();
            foreach (var unit in activeUnits)
            {
                if (!perMinuteTotals.TryGetValue(unit.Faction, out var totals))
                {
                    totals = new Accrual();
                    perMinuteTotals[unit.Faction] = totals;
                }
                totals.Ore += unit.UpkeepOrePerMinute;
                totals.Biomass += unit.UpkeepBiomassPerMinute;
                totals.Gold += unit.UpkeepGoldPerMinute;
            }

            float scale = deltaTime / 60f;
            foreach (var faction in AllFactions)
            {
                var state = getState(faction);
                if (state == null) continue;

                if (!_accrual.TryGetValue(faction, out var accrual))
                {
                    accrual = new Accrual();
                    _accrual[faction] = accrual;
                }

                perMinuteTotals.TryGetValue(faction, out var perMinute);
                accrual.Ore += (perMinute?.Ore ?? 0f) * scale;
                accrual.Biomass += (perMinute?.Biomass ?? 0f) * scale;
                accrual.Gold += (perMinute?.Gold ?? 0f) * scale;

                int oreSpend = Mathf.FloorToInt(accrual.Ore);
                int biomassSpend = Mathf.FloorToInt(accrual.Biomass);
                int goldSpend = Mathf.FloorToInt(accrual.Gold);
                if (oreSpend <= 0 && biomassSpend <= 0 && goldSpend <= 0) continue;

                var cost = new Dictionary<ResourceType, int>();
                if (oreSpend > 0) cost[ResourceType.Ore] = oreSpend;
                if (biomassSpend > 0) cost[ResourceType.Biomass] = biomassSpend;
                if (goldSpend > 0) cost[ResourceType.Gold] = goldSpend;
                state.Stockpile.Spend(cost);

                accrual.Ore -= oreSpend;
                accrual.Biomass -= biomassSpend;
                accrual.Gold -= goldSpend;
            }
        }
    }
}
