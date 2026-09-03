namespace MechTS.Vision
{
    /// <summary>
    /// One map cell's current knowledge state for a given faction, per
    /// <see cref="VisionManager"/>. <see cref="Unexplored"/> cells have never been seen;
    /// <see cref="Explored"/> cells were seen before but aren't currently in vision (static
    /// structures are remembered there, mobile units are not); <see cref="Visible"/> cells
    /// are currently within vision.
    /// </summary>
    public enum FogState
    {
        Unexplored,
        Explored,
        Visible
    }
}
