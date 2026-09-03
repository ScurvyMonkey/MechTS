using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace MechTS.Core
{
    /// <summary>
    /// Plays positional one-shot SFX (via a pooled <see cref="AudioSource"/>) and looping
    /// background music, with independent Master/SFX/Music volume multipliers, plus a
    /// per-<see cref="SfxCategory"/> native priority/volume/distance tuning (issue #33) so
    /// e.g. weapon fire can win over ambient engine hum when many sounds play at once.
    /// Created by <see cref="Bootstrapper"/>. No <c>AudioMixer</c> asset is used — see the
    /// completion report for why mixer authoring isn't available in this environment; volume
    /// control is a plain code-side multiplier instead, applied at playback time.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static readonly AudioCategoryEntry DefaultCategoryEntry = new AudioCategoryEntry
        {
            priority = 128,
            volumeMultiplier = 1f,
            minDistance = 1f,
            maxDistance = 500f
        };

        private AudioSource _musicSource;
        private IObjectPool<AudioSource> _sfxPool;
        private Dictionary<SfxCategory, AudioCategoryEntry> _categoryLookup;
        private float _masterVolume = 1f;
        private float _sfxVolume = 1f;
        private float _musicVolume = 1f;

        /// <summary>
        /// The combined Master × SFX volume multiplier, for any standalone
        /// <see cref="AudioSource"/> (e.g. <see cref="Units.VehicleAnimationDriver"/>'s
        /// engine loop) that isn't itself pooled/managed by this manager but still wants to
        /// respect global volume settings.
        /// </summary>
        public float EffectiveSfxVolume => _masterVolume * _sfxVolume;

        /// <summary>
        /// Assigns this manager's per-category priority/volume/distance tuning. Called by
        /// <see cref="Bootstrapper"/> immediately after creation, since it's instantiated at
        /// runtime rather than from a prefab with an Inspector-assigned reference. A null or
        /// empty config is fine — every category falls back to <see cref="DefaultCategoryEntry"/>.
        /// </summary>
        /// <param name="categoryConfig">The per-mission SFX category tuning, or null.</param>
        public void Initialize(AudioCategoryConfig categoryConfig)
        {
            _categoryLookup = new Dictionary<SfxCategory, AudioCategoryEntry>();
            if (categoryConfig == null || categoryConfig.categories == null) return;

            foreach (var entry in categoryConfig.categories)
            {
                _categoryLookup[entry.category] = entry;
            }
        }

        /// <summary>
        /// Returns the tuning for the given category, or <see cref="DefaultCategoryEntry"/>
        /// if none is configured for it.
        /// </summary>
        /// <param name="category">The category to look up.</param>
        private AudioCategoryEntry GetCategoryEntry(SfxCategory category)
        {
            if (_categoryLookup != null && _categoryLookup.TryGetValue(category, out var entry)) return entry;
            return DefaultCategoryEntry;
        }

        /// <summary>
        /// Returns the given category's volume multiplier, for any standalone
        /// <see cref="AudioSource"/> (e.g. <see cref="Units.VehicleAnimationDriver"/>'s
        /// engine loop) that computes its own volume each frame instead of going through
        /// <see cref="PlaySfx"/>.
        /// </summary>
        /// <param name="category">The category to look up.</param>
        public float GetCategoryVolumeMultiplier(SfxCategory category) => GetCategoryEntry(category).volumeMultiplier;

        /// <summary>
        /// Applies the given category's native priority and 3D min/max distance to a
        /// standalone (non-pooled) <see cref="AudioSource"/> — mirrors
        /// <see cref="GetCategoryVolumeMultiplier"/>'s role for volume. Pooled one-shot
        /// sources get this from <see cref="PlaySfx"/> instead, reapplied per-play since
        /// they're reused across categories.
        /// </summary>
        /// <param name="source">The standalone source to configure.</param>
        /// <param name="category">The category to apply.</param>
        public void ApplyCategorySettings(AudioSource source, SfxCategory category)
        {
            var entry = GetCategoryEntry(category);
            source.priority = entry.priority;
            source.minDistance = entry.minDistance;
            source.maxDistance = entry.maxDistance;
        }

        /// <summary>
        /// Builds the pooled SFX source and the persistent music source.
        /// </summary>
        private void Awake()
        {
            var musicGo = new GameObject("MusicSource");
            musicGo.transform.SetParent(transform, false);
            _musicSource = musicGo.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;

            _sfxPool = new ObjectPool<AudioSource>(
                CreateSfxSource,
                source => source.gameObject.SetActive(true),
                source => source.gameObject.SetActive(false),
                source => Destroy(source.gameObject),
                defaultCapacity: 8);
        }

        /// <summary>
        /// Creates a pooled, positional one-shot <see cref="AudioSource"/>.
        /// </summary>
        private AudioSource CreateSfxSource()
        {
            var go = new GameObject("PooledSfxSource");
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            return source;
        }

        /// <summary>
        /// Plays a one-shot SFX at a world position, releasing the pooled source back once
        /// the clip finishes. No-ops if the clip is null. The source is reused across
        /// categories, so <paramref name="category"/>'s priority/min/max-distance are
        /// reapplied on every call rather than assumed to persist from a prior play.
        /// </summary>
        /// <param name="clip">The clip to play, or null to no-op.</param>
        /// <param name="position">The world position to play the sound at.</param>
        /// <param name="category">Which <see cref="SfxCategory"/> this sound belongs to, driving its priority/volume/distance tuning.</param>
        /// <param name="volume">The clip's base playback volume, 0-1, before the category and Master/SFX multipliers are applied.</param>
        public void PlaySfx(AudioClip clip, Vector3 position, SfxCategory category, float volume = 1f)
        {
            if (clip == null) return;

            var entry = GetCategoryEntry(category);

            var source = _sfxPool.Get();
            source.transform.position = position;
            source.clip = clip;
            source.priority = entry.priority;
            source.minDistance = entry.minDistance;
            source.maxDistance = entry.maxDistance;
            source.volume = volume * entry.volumeMultiplier * EffectiveSfxVolume;
            source.Play();
            StartCoroutine(ReleaseWhenFinished(source, clip.length));
        }

        /// <summary>
        /// Releases a pooled SFX source back to its pool once its clip has finished playing.
        /// </summary>
        /// <param name="source">The source to release.</param>
        /// <param name="delay">Seconds to wait before releasing.</param>
        private IEnumerator ReleaseWhenFinished(AudioSource source, float delay)
        {
            yield return new WaitForSeconds(delay);
            _sfxPool.Release(source);
        }

        /// <summary>
        /// Plays looping background music, replacing whatever is currently playing. No-ops if the clip is null.
        /// </summary>
        /// <param name="clip">The music track to play.</param>
        public void PlayMusic(AudioClip clip)
        {
            if (clip == null) return;

            _musicSource.clip = clip;
            _musicSource.volume = _musicVolume * _masterVolume;
            _musicSource.Play();
        }

        /// <summary>
        /// Stops the currently playing music, if any.
        /// </summary>
        public void StopMusic()
        {
            _musicSource.Stop();
        }

        /// <summary>
        /// Sets the Master volume (0 silent to 1 full), immediately reapplying it to any currently playing music.
        /// </summary>
        /// <param name="linear01">The linear volume, 0-1.</param>
        public void SetMasterVolume(float linear01)
        {
            _masterVolume = Mathf.Clamp01(linear01);
            _musicSource.volume = _musicVolume * _masterVolume;
        }

        /// <summary>
        /// Sets the SFX volume (0 silent to 1 full). Applies to SFX played from this point on.
        /// </summary>
        /// <param name="linear01">The linear volume, 0-1.</param>
        public void SetSfxVolume(float linear01)
        {
            _sfxVolume = Mathf.Clamp01(linear01);
        }

        /// <summary>
        /// Sets the Music volume (0 silent to 1 full), immediately reapplying it to any currently playing music.
        /// </summary>
        /// <param name="linear01">The linear volume, 0-1.</param>
        public void SetMusicVolume(float linear01)
        {
            _musicVolume = Mathf.Clamp01(linear01);
            _musicSource.volume = _musicVolume * _masterVolume;
        }
    }
}
