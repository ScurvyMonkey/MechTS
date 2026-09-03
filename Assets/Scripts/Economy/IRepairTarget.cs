using MechTS.Units;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Implemented by entities a Crewman can repair: friendly buildings and the main
    /// building (issue #42). Mirrors <see cref="ISabotageTarget"/>'s shape, but for the
    /// opposite (friendly-only) direction.
    /// </summary>
    public interface IRepairTarget
    {
        /// <summary>This target's current world position, for the Crewman to move toward.</summary>
        Vector3 Position { get; }

        /// <summary>This target's owning faction — only a same-faction Crewman may repair it.</summary>
        Faction Faction { get; }

        /// <summary>This target's <see cref="Health"/> component, healed over time while assigned.</summary>
        Health HealthComponent { get; }
    }
}
