namespace MechTS.Core
{
    /// <summary>
    /// Groups SFX for priority/volume tuning via <see cref="AudioCategoryConfig"/> — e.g.
    /// weapon fire should win over ambient engine hum when many sounds play at once.
    /// <see cref="Ambient"/>/<see cref="UI"/> exist for future use; no sound source in the
    /// project is wired to them yet.
    /// </summary>
    public enum SfxCategory
    {
        Weapon,
        Engine,
        Ambient,
        UI
    }
}
