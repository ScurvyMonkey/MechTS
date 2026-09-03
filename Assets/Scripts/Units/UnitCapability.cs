using System;

namespace MechTS.Units
{
    /// <summary>
    /// Flags describing what a unit is/can do, derived once from existing per-unit signals
    /// (<see cref="UnitBase.IsFlying"/>, a <see cref="Weapon"/>'s configured target type,
    /// component presence) rather than a separately-authored tag — see
    /// <see cref="UnitBase.Capabilities"/>. Used to scope tech-tree stat multipliers to a
    /// subset of units (issue #39). <see cref="Ground"/> and <see cref="Flying"/> are
    /// mutually exclusive by convention (mirrors <see cref="UnitBase.IsFlying"/>); the rest
    /// are independent and can combine freely.
    /// </summary>
    [Flags]
    public enum UnitCapability
    {
        None = 0,
        Ground = 1 << 0,
        Flying = 1 << 1,
        GroundAttacker = 1 << 2,
        AirAttacker = 1 << 3,
        Gatherer = 1 << 4,
        Builder = 1 << 5,
        Saboteur = 1 << 6,
    }
}
