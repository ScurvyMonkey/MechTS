using MechTS.Core;
using UnityEngine;
using UnityEngine.Pool;

namespace MechTS.Units
{
    /// <summary>
    /// A pooled, code-driven bullet sprite that flies in a straight line from its firing
    /// position to a target position, then releases itself back to its pool. No frame
    /// animation involved — a linear position lerp, matching the project's existing
    /// lightweight per-part animation driver style (e.g. VehicleAnimationDriver). Yaws only
    /// its own transform to face the travel direction; the visual sprite is a child with the
    /// project's standard fixed local (90,0,0) top-down tilt, so the two rotations compose
    /// cleanly without interfering with each other.
    /// </summary>
    public class BulletEffect : MonoBehaviour
    {
        [SerializeField] private float _speed = 40f;

        private IObjectPool<GameObject> _pool;
        private AudioManager _audioManager;
        private AudioClip _impactSound;
        private Vector3 _from;
        private Vector3 _to;
        private float _elapsed;
        private float _duration;

        /// <summary>
        /// Configures and starts this bullet's flight from <paramref name="from"/> to
        /// <paramref name="to"/>, yawed to face its direction of travel.
        /// </summary>
        /// <param name="from">The world position to start from (the weapon's muzzle).</param>
        /// <param name="to">The world position to fly toward (the target).</param>
        /// <param name="pool">The pool to release this instance back to once it arrives.</param>
        /// <param name="audioManager">Plays <paramref name="impactSound"/> on arrival, or null to play nothing.</param>
        /// <param name="impactSound">The sound to play at the impact point on arrival, or null for none.</param>
        public void Fire(Vector3 from, Vector3 to, IObjectPool<GameObject> pool, AudioManager audioManager, AudioClip impactSound)
        {
            _pool = pool;
            _audioManager = audioManager;
            _impactSound = impactSound;
            _from = from;
            _to = to;
            _elapsed = 0f;
            _duration = Mathf.Max(Vector3.Distance(from, to) / Mathf.Max(_speed, 0.01f), 0.01f);

            transform.position = from;

            Vector3 direction = to - from;
            if (direction.sqrMagnitude > 0.0001f)
            {
                float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        /// <summary>
        /// Advances the bullet toward its target, playing its impact sound and releasing it
        /// back to its pool on arrival.
        /// </summary>
        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            transform.position = Vector3.Lerp(_from, _to, t);

            if (t >= 1f)
            {
                if (_audioManager != null) _audioManager.PlaySfx(_impactSound, _to, SfxCategory.Weapon);
                _pool?.Release(gameObject);
            }
        }
    }
}
