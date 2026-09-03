using MechTS.Units;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Drives a Saboteur's channel/interrupt/effect logic against an assigned sabotage
    /// target: travel to range, channel while stationary, apply the effect on completion.
    /// Interrupting (destroying) the Saboteur cancels the channel — this component and
    /// its GameObject are simply gone, so nothing further runs.
    /// </summary>
    public class SabotageAction : MonoBehaviour
    {
        [SerializeField] private SaboteurConfig _config;
        [SerializeField] private SabotageEffectConfig _effectConfig;

        private UnitBase _unit;
        private ISabotageTarget _target;
        private bool _isChanneling;
        private float _channelElapsed;

        /// <summary>
        /// Caches the sibling <see cref="UnitBase"/> component.
        /// </summary>
        private void Awake()
        {
            _unit = GetComponent<UnitBase>();
        }

        /// <summary>
        /// Applies this Saboteur's configured max health, move speed, and upkeep cost.
        /// </summary>
        private void Start()
        {
            _unit.HealthComponent?.SetMaxHealth(_config.maxHealth);
            _unit.SetBaseMoveSpeed(_config.moveSpeed);
            _unit.SetUpkeepCost(_config.oreUpkeepPerMinute, _config.biomassUpkeepPerMinute, _config.goldUpkeepPerMinute);
        }

        /// <summary>
        /// Assigns a new sabotage target and moves toward it, cancelling any current channel.
        /// </summary>
        /// <param name="target">The target to sabotage.</param>
        public void AssignTarget(ISabotageTarget target)
        {
            _target = target;
            _isChanneling = false;
            _channelElapsed = 0f;
            _unit.MoveTo(target.Position);
        }

        /// <summary>
        /// Advances travel-then-channel toward the assigned target each frame.
        /// </summary>
        private void Update()
        {
            if (_target == null) return;

            if (!_isChanneling)
            {
                if (_unit.HasArrived())
                {
                    _isChanneling = true;
                    _channelElapsed = 0f;
                    _unit.Stop();
                }
                return;
            }

            _channelElapsed += Time.deltaTime;
            if (_channelElapsed >= _config.sabotageChannelDuration)
            {
                _target.ApplySabotage(_effectConfig);
                _target = null;
                _isChanneling = false;
            }
        }
    }
}
