using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Gates a real, pack-authored production animation on a sibling <see cref="ProductionQueue"/>'s
    /// <see cref="ProductionQueue.CurrentItem"/> (issue #92) — reusable across every production
    /// building whose animation the asset pack already ships (Barracks, Drone Factory, confirmed
    /// via direct inspection to each already have a real <see cref="Animator"/>+<c>AnimatorController</c>,
    /// just never gated). Mirrors <see cref="Units.HarvesterPartAnimator"/>'s exact technique:
    /// toggle <see cref="Animator.enabled"/> rather than drive parameters, since these packs'
    /// controllers have zero parameters to drive externally. Unlike
    /// <see cref="Units.WeaponFireAnimator"/>'s decaying-window gate (needed because firing is a
    /// series of discrete shot events), <see cref="ProductionQueue.CurrentItem"/> is already a
    /// continuously-readable "is something in production right now" state, so this polls it
    /// directly each frame — no timer needed.
    /// </summary>
    public class ProductionAnimator : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private ProductionQueue _productionQueue;
        private bool _wasProducing;

        /// <summary>
        /// Caches the sibling <see cref="ProductionQueue"/> and starts the production animation
        /// disabled — it should only play while something is actually queued, not from the
        /// moment this building spawns.
        /// </summary>
        private void Start()
        {
            _productionQueue = GetComponent<ProductionQueue>();
            if (_animator != null) _animator.enabled = false;
        }

        /// <summary>
        /// Toggles the animation on/off as <see cref="ProductionQueue.CurrentItem"/> changes
        /// between null and non-null, resetting to the first frame before disabling — the same
        /// "reset before disable" technique <see cref="Units.HarvesterPartAnimator"/> already
        /// established, so it never freezes mid-animation.
        /// </summary>
        private void Update()
        {
            if (_productionQueue == null || _animator == null) return;

            bool isProducing = _productionQueue.CurrentItem != null;
            if (isProducing == _wasProducing) return;
            _wasProducing = isProducing;

            if (isProducing)
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
