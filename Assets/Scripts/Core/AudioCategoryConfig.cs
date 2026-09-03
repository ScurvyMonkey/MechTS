using System;
using UnityEngine;

namespace MechTS.Core
{
    /// <summary>
    /// One <see cref="SfxCategory"/>'s tuning: native <see cref="AudioSource.priority"/>
    /// (0-256, lower wins when Unity has to choose which sounds are actually audible),
    /// a volume multiplier applied on top of <see cref="AudioManager"/>'s Master/SFX
    /// volume, and the 3D distance range that source falls off across.
    /// </summary>
    [Serializable]
    public class AudioCategoryEntry
    {
        public SfxCategory category;
        [Range(0, 256)] public int priority = 128;
        public float volumeMultiplier = 1f;
        public float minDistance = 5f;
        public float maxDistance = 50f;
    }

    /// <summary>
    /// Per-mission SFX category tuning, injected into <see cref="AudioManager"/> by
    /// <see cref="Bootstrapper"/> the same way <see cref="CameraConfig"/> and the other
    /// mission configs are.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Core/Audio Category Config")]
    public class AudioCategoryConfig : ScriptableObject
    {
        public AudioCategoryEntry[] categories;
    }
}
