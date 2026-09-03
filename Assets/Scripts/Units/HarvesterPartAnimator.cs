using UnityEngine;
using MechTS.Economy;

namespace MechTS.Units
{
    /// <summary>
    /// Drives part animation for the Harvester's real 3D model (issue #65/#66/#67/#68
    /// follow-up). Scrolls the tracks' tread texture along U proportional to move speed —
    /// the tracks mesh is a flat plate whose UVs tile the tread pattern many times along U
    /// (confirmed directly via <c>Mesh.uv</c>, range roughly [-6.6, 7.8]) while V stays in a
    /// tight 0-1 band, the standard setup for a scrolling tread texture. Physically rotating
    /// that flat plate (the original #66 approach) reads as tumbling, not rolling — a real
    /// live playtest confirmed it visually. For the claws, the model already ships a real,
    /// artist-authored grabbing animation on its own Animator — reusing it (see #67), not
    /// re-animating by hand. The pack's <c>AnimatorController</c> has zero parameters, so it
    /// can't gate itself; this component gates it externally by toggling
    /// <see cref="Animator.enabled"/> based on the sibling
    /// <see cref="HarvesterGatherBehavior.IsGathering"/>.
    /// </summary>
    public class HarvesterPartAnimator : MonoBehaviour
    {
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        [SerializeField] private Renderer _tracksRenderer;
        [SerializeField] private Animator _animator;

        private UnitBase _unit;
        private HarvesterGatherBehavior _gatherBehavior;
        private MaterialPropertyBlock _trackPropertyBlock;
        private Vector2 _trackBaseTiling = Vector2.one;
        private float _trackScrollSpeed;
        private float _trackScrollOffset;
        private bool _wasGathering;

        /// <summary>
        /// Caches sibling components, the tracks' authored texture tiling (so the scroll offset
        /// doesn't stomp its real scale), and starts the claw <see cref="Animator"/> disabled —
        /// it should only play while actively gathering, not from the moment the unit spawns.
        /// </summary>
        private void Start()
        {
            _unit = GetComponent<UnitBase>();
            _gatherBehavior = GetComponent<HarvesterGatherBehavior>();
            _trackPropertyBlock = new MaterialPropertyBlock();
            if (_tracksRenderer != null) _trackBaseTiling = _tracksRenderer.sharedMaterial.mainTextureScale;
            if (_animator != null) _animator.enabled = false;
        }

        /// <summary>
        /// Sets the track texture-scroll tuning from <see cref="HarvesterConfig"/>, pushed by
        /// <see cref="HarvesterGatherBehavior.Start"/> alongside its other config hookups —
        /// mirrors <see cref="VehicleAnimationDriver.SetTrackSpeedMultiplier"/>'s exact shape.
        /// </summary>
        /// <param name="trackScrollSpeed">Texture-widths/second the tread scrolls at the unit's full configured move speed.</param>
        public void SetTrackScrollSpeed(float trackScrollSpeed)
        {
            _trackScrollSpeed = trackScrollSpeed;
        }

        /// <summary>
        /// Scrolls the tracks' tread texture proportional to current move speed via a
        /// per-instance <see cref="MaterialPropertyBlock"/> (never mutates the shared
        /// material asset), and toggles the claw <see cref="Animator"/> on/off as
        /// <see cref="HarvesterGatherBehavior.IsGathering"/> changes.
        /// </summary>
        private void Update()
        {
            if (_tracksRenderer != null && _unit.Agent.speed > 0.01f)
            {
                float gain = _unit.Agent.velocity.magnitude / _unit.Agent.speed;
                _trackScrollOffset += _trackScrollSpeed * gain * Time.deltaTime;

                _tracksRenderer.GetPropertyBlock(_trackPropertyBlock);
                _trackPropertyBlock.SetVector(BaseMapStId, new Vector4(_trackBaseTiling.x, _trackBaseTiling.y, _trackScrollOffset, 0f));
                _tracksRenderer.SetPropertyBlock(_trackPropertyBlock);
            }

            bool isGathering = _gatherBehavior != null && _gatherBehavior.IsGathering;
            if (isGathering == _wasGathering || _animator == null) return;
            _wasGathering = isGathering;

            if (isGathering)
            {
                _animator.enabled = true;
            }
            else
            {
                _animator.Play(_animator.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, 0f);
                _animator.Update(0f);
                _animator.enabled = false;
            }
        }
    }
}
