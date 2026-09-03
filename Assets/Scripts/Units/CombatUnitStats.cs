using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// Applies a <see cref="CombatUnitConfig"/>'s max health and move speed to the sibling
    /// <see cref="UnitBase"/> (issue #43) — the combat-roster equivalent of
    /// <see cref="Economy.HarvesterGatherBehavior"/>/<see cref="Economy.SabotageAction"/>'s own
    /// config-application role, extracted as its own component (rather than folded into a
    /// unit subclass) since Ranger/Reaper/Dredge need no unique behavior beyond stat tuning —
    /// they use plain <see cref="UnitBase"/> + <see cref="Weapon"/>, same as
    /// <c>TestCombatUnit</c>. Routing max health/move speed through <see cref="UnitBase.SetBaseMoveSpeed"/>
    /// (rather than setting <see cref="UnitBase.Agent"/>'s speed directly, as
    /// <c>TestCombatUnit</c> does today with no config asset at all) gives every combat unit
    /// real MoveSpeed/Health stat-multiplier support for the first time.
    /// </summary>
    [RequireComponent(typeof(UnitBase))]
    public class CombatUnitStats : MonoBehaviour
    {
        [SerializeField] private CombatUnitConfig _config;

        private UnitBase _unit;

        /// <summary>
        /// Caches the sibling <see cref="UnitBase"/> component.
        /// </summary>
        private void Awake()
        {
            _unit = GetComponent<UnitBase>();
        }

        /// <summary>
        /// Applies this unit's configured max health, move speed, and upkeep cost.
        /// </summary>
        private void Start()
        {
            _unit.HealthComponent?.SetMaxHealth(_config.maxHealth);
            _unit.SetBaseMoveSpeed(_config.moveSpeed);
            _unit.SetUpkeepCost(_config.oreUpkeepPerMinute, _config.biomassUpkeepPerMinute, _config.goldUpkeepPerMinute);
        }
    }
}
