using MechTS.Units;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Drives a Crewman's repair loop: move to an assigned friendly damaged target, then
    /// heal it continuously until it reaches max health or is reassigned. Mirrors
    /// <see cref="HarvesterGatherBehavior"/>'s travel-then-continuous-tick shape (issue #42),
    /// not <see cref="SabotageAction"/>'s single-completion channel — repair has no fixed
    /// duration, it just keeps ticking while assigned.
    /// </summary>
    public class CrewmanRepairBehavior : MonoBehaviour
    {
        private enum State { Idle, MovingToTarget, Repairing }

        [SerializeField] private CrewmanConfig _config;

        private UnitBase _unit;
        private IRepairTarget _target;
        private State _state = State.Idle;

        /// <summary>
        /// Caches the sibling <see cref="UnitBase"/> component.
        /// </summary>
        private void Awake()
        {
            _unit = GetComponent<UnitBase>();
        }

        /// <summary>
        /// Applies this Crewman's configured max health, move speed, and upkeep cost.
        /// </summary>
        private void Start()
        {
            _unit.HealthComponent?.SetMaxHealth(_config.maxHealth);
            _unit.SetBaseMoveSpeed(_config.moveSpeed);
            _unit.SetUpkeepCost(_config.oreUpkeepPerMinute, _config.biomassUpkeepPerMinute, _config.goldUpkeepPerMinute);
        }

        /// <summary>
        /// Assigns a new repair target and begins moving toward it, overriding any current assignment.
        /// </summary>
        /// <param name="target">The friendly, damaged target to repair.</param>
        public void AssignRepairTarget(IRepairTarget target)
        {
            _target = target;
            _state = State.MovingToTarget;
            _unit.MoveTo(target.Position);
        }

        /// <summary>
        /// Cancels any current repair assignment without affecting the target's health —
        /// called by <see cref="CrewmanUnit"/> when this Crewman is given a build order
        /// instead, since both behaviors drive the same <see cref="UnitBase.MoveTo"/>/
        /// <see cref="UnitBase.HasArrived"/> and can't run concurrently.
        /// </summary>
        public void CancelRepair()
        {
            _target = null;
            _state = State.Idle;
        }

        /// <summary>
        /// Advances travel-then-repair toward the assigned target each frame. Stops
        /// automatically once the target reaches max health — no explicit "repair complete"
        /// signal is needed since this is a continuous tick, not a single channel.
        /// </summary>
        private void Update()
        {
            if (_target == null) return;

            var health = _target.HealthComponent;
            if (health == null || health.CurrentHealth >= health.MaxHealth)
            {
                _target = null;
                _state = State.Idle;
                return;
            }

            if (_state == State.MovingToTarget)
            {
                if (_unit.HasArrived())
                {
                    _state = State.Repairing;
                }
                return;
            }

            if (_state == State.Repairing)
            {
                health.Heal(_config.repairRatePerSecond * Time.deltaTime);
            }
        }
    }
}
