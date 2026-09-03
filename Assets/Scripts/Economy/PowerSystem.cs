using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Stateless helper for power-capacity checks against a faction's current economy state.
    /// </summary>
    public static class PowerSystem
    {
        /// <summary>
        /// Returns whether adding a building/unit with the given power footprint would
        /// keep the faction's power usage within its capacity.
        /// </summary>
        /// <param name="state">The faction's current economy state.</param>
        /// <param name="powerGenerated">The power the new building would generate.</param>
        /// <param name="powerConsumed">The power the new building would consume.</param>
        /// <param name="capacityMultiplier">The faction's current PowerCapacity stat
        /// multiplier from <see cref="TechManager.GetStatMultiplier"/> — defaults to 1
        /// (no-op) for callers that don't have a <see cref="TechManager"/> reference handy.</param>
        public static bool CanAfford(FactionEconomyState state, int powerGenerated, int powerConsumed, float capacityMultiplier = 1f)
        {
            int projectedCapacity = Mathf.RoundToInt(state.PowerCapacity * capacityMultiplier) + powerGenerated;
            int projectedUsed = state.PowerUsed + powerConsumed;
            return projectedUsed <= projectedCapacity;
        }
    }
}
