using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Implemented by entities a Saboteur can target: harvesters, resource nodes, and
    /// non-main economic/power buildings. A faction's main building never implements this.
    /// </summary>
    public interface ISabotageTarget
    {
        /// <summary>This target's current world position, for the Saboteur to move toward.</summary>
        Vector3 Position { get; }

        /// <summary>
        /// Applies this target's sabotage effect once a Saboteur's channel completes.
        /// </summary>
        /// <param name="effectConfig">Designer-tunable magnitudes for the effect (e.g. disable duration).</param>
        void ApplySabotage(SabotageEffectConfig effectConfig);
    }
}
