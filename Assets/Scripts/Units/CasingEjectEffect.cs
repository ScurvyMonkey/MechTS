using UnityEngine;
using UnityEngine.Pool;

namespace MechTS.Units
{
    /// <summary>
    /// A pooled, code-driven shell casing that pops sideways from the weapon, spins a
    /// little, and fades out, then releases itself back to its pool. Purely a firing-feel
    /// flourish — not tied to hit detection or damage. Only spins its own transform; the
    /// visual sprite is a child with the project's standard fixed local (90,0,0) top-down
    /// tilt, so the spin and the tilt compose cleanly without interfering with each other.
    /// </summary>
    public class CasingEjectEffect : MonoBehaviour
    {
        [SerializeField] private float _lifetime = 0.5f;
        [SerializeField] private float _popDistance = 0.35f;
        [SerializeField] private float _spinSpeed = 540f;

        private SpriteRenderer _spriteRenderer;
        private IObjectPool<GameObject> _pool;
        private Vector3 _start;
        private Vector3 _popDirection;
        private float _elapsed;

        /// <summary>
        /// Caches the child SpriteRenderer used to fade the casing out over its lifetime.
        /// </summary>
        private void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// Configures and starts this casing's eject animation from the given origin, popping
        /// out sideways relative to the weapon's current facing direction.
        /// </summary>
        /// <param name="origin">The world position to eject from (the weapon's muzzle).</param>
        /// <param name="facing">The weapon's current facing direction, used to pick a sideways pop direction.</param>
        /// <param name="pool">The pool to release this instance back to once it finishes.</param>
        public void Eject(Vector3 origin, Vector3 facing, IObjectPool<GameObject> pool)
        {
            _pool = pool;
            _start = origin;
            _elapsed = 0f;

            Vector3 flatFacing = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.forward;
            Vector3 sideways = Vector3.Cross(Vector3.up, flatFacing);
            _popDirection = (sideways - Vector3.up * 0.4f).normalized;

            transform.SetPositionAndRotation(origin, Quaternion.identity);

            if (_spriteRenderer != null)
            {
                var color = _spriteRenderer.color;
                _spriteRenderer.color = new Color(color.r, color.g, color.b, 1f);
            }
        }

        /// <summary>
        /// Pops the casing outward, spins it, and fades it out over its lifetime.
        /// </summary>
        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _lifetime);

            transform.position = _start + _popDirection * _popDistance * t;
            transform.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.Self);

            if (_spriteRenderer != null)
            {
                var color = _spriteRenderer.color;
                _spriteRenderer.color = new Color(color.r, color.g, color.b, 1f - t);
            }

            if (t >= 1f)
            {
                _pool?.Release(gameObject);
            }
        }
    }
}
