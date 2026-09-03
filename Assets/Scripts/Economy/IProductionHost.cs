using MechTS.Units;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// The minimal surface a <see cref="ProductionQueue"/> needs from whatever it's
    /// attached to — implemented by both <see cref="BuildingInstance"/> and
    /// <see cref="MainBuilding"/> (issue #41) so unit production isn't hard-wired to
    /// regular production buildings alone.
    /// </summary>
    public interface IProductionHost
    {
        /// <summary>The faction that owns this production host.</summary>
        Faction Faction { get; }

        /// <summary>Whether this host is temporarily disabled (e.g. by sabotage) and should pause production.</summary>
        bool IsDisabled { get; }

        /// <summary>Where produced units should walk to after spawning, if set.</summary>
        Vector3? RallyPoint { get; }
    }
}
