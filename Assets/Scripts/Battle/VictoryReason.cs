namespace MechTS.Battle
{
    /// <summary>
    /// Which specific condition caused a Battle Round to resolve, for display on the
    /// Mission End screen and any future mission/AI post-mortem logic.
    /// </summary>
    public enum VictoryReason
    {
        Wipeout,
        EconomyExhausted,
        Surrender,
        ObjectiveMet,
        Tiebreak
    }
}
