using System.Collections.Generic;
using System.Linq;
using MechTS.Core;
using TopDownAssets.Common.Scripts;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// Drives the asset pack's Vibration/Smoke/track-roll components from this unit's
    /// actual NavMeshAgent speed. Stands in for the pack's own Vehicle.cs, which is
    /// hardcoded to legacy Input-driven WASD control and doesn't fit a NavMeshAgent-
    /// piloted RTS unit — Vibration/Smoke/WheelAnimation themselves have no such
    /// dependency and are reused unmodified. Also drives an engine-loop AudioSource, set via
    /// <see cref="SetEngineSound"/> (issue #29 — config-driven, replacing issue #27's
    /// hardcoded per-prefab clip assignment), with volume/pitch scaled by speed gain —
    /// mirroring the pack's own Vehicle.cs formula (`volume = 0.5 + 0.5*gain`, `pitch = 1 + gain`).
    /// </summary>
    public class VehicleAnimationDriver : MonoBehaviour
    {
        [SerializeField] private List<WheelAnimation> _tracks = new List<WheelAnimation>();

        private UnitBase _unit;
        private List<Vibration> _vibrations;
        private List<Smoke> _smoke;
        private AudioSource _engineSource;
        private AudioManager _audioManager;
        private float _trackSpeedMultiplier = 1f;

        /// <summary>
        /// Caches the owning unit and every Vibration/Smoke component found in children.
        /// </summary>
        private void Start()
        {
            _unit = GetComponent<UnitBase>();
            _vibrations = GetComponentsInChildren<Vibration>().ToList();
            _smoke = GetComponentsInChildren<Smoke>().ToList();
            _audioManager = FindFirstObjectByType<AudioManager>();

            _vibrations.ForEach(v => v.enabled = true);
        }

        /// <summary>
        /// Assigns (or replaces) this vehicle's engine-loop sound, building the loop
        /// <see cref="AudioSource"/> on first use. Safe to call before or after this
        /// component's own <see cref="Start"/> runs — <see cref="HarvesterGatherBehavior"/>
        /// calls this from its own <c>Start()</c>, and Unity gives no cross-component
        /// ordering guarantee between the two, unlike <see cref="UnitBase.SetBaseMoveSpeed"/>/
        /// <see cref="Health.SetMaxHealth"/> (plain field writes with no such build step).
        /// </summary>
        /// <param name="clip">The engine-loop clip to play, or null to silence the engine.</param>
        public void SetEngineSound(AudioClip clip)
        {
            if (clip == null)
            {
                if (_engineSource != null) _engineSource.Stop();
                return;
            }

            if (_engineSource == null)
            {
                _engineSource = gameObject.AddComponent<AudioSource>();
                _engineSource.loop = true;
                _engineSource.playOnAwake = false;
                _engineSource.spatialBlend = 1f;

                // May run before this component's own Start() has cached _audioManager (see
                // summary above) — look it up locally rather than depend on that ordering.
                var audioManager = _audioManager != null ? _audioManager : FindFirstObjectByType<AudioManager>();
                audioManager?.ApplyCategorySettings(_engineSource, SfxCategory.Engine);
            }

            _engineSource.clip = clip;
            _engineSource.Play();
        }

        /// <summary>
        /// Sets this vehicle's track-roll speed multiplier (issue #47) — rescales the raw
        /// movement speed fed to each <see cref="WheelAnimation"/> in <see cref="Update"/> so
        /// the flipbook advances fast enough to read as continuous rolling instead of a slow,
        /// visible step. A plain field write, unlike <see cref="SetEngineSound"/> — no
        /// sub-object to lazily construct, so it's already safe to call regardless of whether
        /// this component's own <see cref="Start"/> has run yet.
        /// </summary>
        /// <param name="multiplier">The track-roll speed multiplier, from <see cref="Economy.HarvesterConfig.trackAnimationSpeedMultiplier"/>.</param>
        public void SetTrackSpeedMultiplier(float multiplier)
        {
            _trackSpeedMultiplier = multiplier;
        }

        /// <summary>
        /// Scales vibration, exhaust smoke, track roll speed, and the engine-loop
        /// volume/pitch to the unit's current movement speed each frame.
        /// </summary>
        private void Update()
        {
            float speed = _unit.Agent.velocity.magnitude;
            float gain = Mathf.Clamp01(speed / Mathf.Max(_unit.Agent.speed, 0.01f));

            foreach (var vibration in _vibrations) vibration.Gain(gain);
            foreach (var smoke in _smoke) smoke.Gain(gain);
            foreach (var track in _tracks) track.Speed = speed * _trackSpeedMultiplier;

            if (_engineSource != null)
            {
                float sfxVolume = _audioManager != null ? _audioManager.EffectiveSfxVolume : 1f;
                float categoryVolume = _audioManager != null ? _audioManager.GetCategoryVolumeMultiplier(SfxCategory.Engine) : 1f;
                _engineSource.volume = (0.5f + 0.5f * gain) * sfxVolume * categoryVolume;
                _engineSource.pitch = 1f + gain;
            }
        }
    }
}
