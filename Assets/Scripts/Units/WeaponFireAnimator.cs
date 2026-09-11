using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// Gates a real, pack-authored fire animation on a sibling <see cref="Weapon"/>'s
    /// <see cref="Weapon.OnFired"/> event (issue #91) — reusable across every unit/building
    /// whose fire animation the asset pack already ships (Ranger, Reaper, the Machine Gun
    /// Turret, confirmed via direct inspection to each already have a real
    /// <see cref="Animator"/>+<c>AnimatorController</c>, just never gated). Drives every
    /// <see cref="_animators"/> entry together — plural because Ranger's model has two
    /// independently-animated gun arms (confirmed via direct hierarchy inspection: two
    /// separate <c>Animator</c>s, one per arm), while Reaper/the Turret only need one. Mirrors
    /// <see cref="HarvesterPartAnimator"/>'s exact technique: toggle
    /// <see cref="Animator.enabled"/> rather than drive parameters, since these packs'
    /// controllers have zero parameters to drive externally. Each configured fire clip is
    /// short and looping (a spinning-barrel/muzzle-flash motion, not a one-shot animation), so
    /// rather than toggling on one shot at a time (which would flash on/off between individual
    /// shots at any real fire rate), this keeps the animation running for a short window after
    /// each shot and re-extends that window on every subsequent shot — a rapid burst reads as
    /// one continuous animation, and it stops shortly after the weapon actually stops firing.
    /// </summary>
    public class WeaponFireAnimator : MonoBehaviour
    {
        /// <summary>
        /// How long the animation keeps playing after the most recent shot before stopping.
        /// Re-extended on every new shot, so consecutive shots at any real fire rate read as
        /// one continuous animation rather than flashing on/off per shot.
        /// </summary>
        [SerializeField] private float _activeWindowSeconds = 0.3f;

        [SerializeField] private Animator[] _animators;

        private Weapon _weapon;
        private float _remainingActiveTime;

        /// <summary>
        /// Caches the sibling <see cref="Weapon"/>, subscribes to its <see cref="Weapon.OnFired"/>
        /// event, and starts every fire animation disabled — it should only play while actively
        /// firing, not from the moment this unit/building spawns.
        /// </summary>
        private void Start()
        {
            _weapon = GetComponent<Weapon>();
            if (_weapon != null) _weapon.OnFired += HandleFired;
            if (_animators == null) _animators = System.Array.Empty<Animator>();
            foreach (var animator in _animators)
            {
                if (animator != null) animator.enabled = false;
            }
        }

        /// <summary>Unsubscribes from <see cref="Weapon.OnFired"/>.</summary>
        private void OnDestroy()
        {
            if (_weapon != null) _weapon.OnFired -= HandleFired;
        }

        /// <summary>Resets the active window and (re)enables every fire animation.</summary>
        private void HandleFired()
        {
            _remainingActiveTime = _activeWindowSeconds;
            foreach (var animator in _animators)
            {
                if (animator != null) animator.enabled = true;
            }
        }

        /// <summary>
        /// Counts down the active window; once it lapses with no further shots, resets every
        /// fire animation to its first frame and disables it — the same "reset before disable"
        /// technique <see cref="HarvesterPartAnimator"/> already established, so it never
        /// freezes mid-animation.
        /// </summary>
        private void Update()
        {
            if (_remainingActiveTime <= 0f) return;

            _remainingActiveTime -= Time.deltaTime;
            if (_remainingActiveTime <= 0f)
            {
                foreach (var animator in _animators)
                {
                    if (animator == null) continue;
                    animator.Play(animator.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, 0f);
                    animator.Update(0f);
                    animator.enabled = false;
                }
            }
        }
    }
}
