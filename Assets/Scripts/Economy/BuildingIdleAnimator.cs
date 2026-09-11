using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Gates a real, pack-authored ambient/idle animation on a sibling
    /// <see cref="BuildingInstance.IsDisabled"/> (issue #93) — reusable across every building
    /// whose animation the asset pack already ships but has no discrete "doing a specific
    /// thing right now" state to gate on (the Detection Building's spinning radar dish, the
    /// Economic Building's running machinery — confirmed via direct inspection to each already
    /// have a real <see cref="Animator"/>+<c>AnimatorController</c>, just never gated). Mirrors
    /// <see cref="ProductionAnimator"/>'s continuous-polling shape (no decaying window needed,
    /// unlike <see cref="Units.WeaponFireAnimator"/>) but polls the inverse condition: plays
    /// continuously whenever the building is actually operational, stopping while under
    /// construction or sabotage-disabled — the same two states <see cref="BuildingInstance.IsDisabled"/>
    /// already combines for every other system (production, power, vision) that cares whether a
    /// building is "really working" right now.
    /// </summary>
    public class BuildingIdleAnimator : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private BuildingInstance _building;
        private bool _wasOperational;

        /// <summary>
        /// Caches the sibling <see cref="BuildingInstance"/> and starts the animation disabled —
        /// it should only play once construction is complete, not from the moment a placement
        /// ghost becomes a real building.
        /// </summary>
        private void Start()
        {
            _building = GetComponent<BuildingInstance>();
            if (_animator != null) _animator.enabled = false;
        }

        /// <summary>
        /// Toggles the animation on/off as <see cref="BuildingInstance.IsDisabled"/> changes,
        /// resetting to the first frame before disabling — the same "reset before disable"
        /// technique <see cref="Units.HarvesterPartAnimator"/> already established, so it never
        /// freezes mid-animation.
        /// </summary>
        private void Update()
        {
            if (_building == null || _animator == null) return;

            bool isOperational = !_building.IsDisabled;
            if (isOperational == _wasOperational) return;
            _wasOperational = isOperational;

            if (isOperational)
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
