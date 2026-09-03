using MechTS.Economy;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// The Saboteur unit: extends <see cref="UnitBase"/> with the ability to be assigned
    /// a sabotage target. Channel/effect logic lives in <see cref="SabotageAction"/>, per
    /// the project's Units/Economy folder split.
    /// </summary>
    [RequireComponent(typeof(SabotageAction))]
    public class SaboteurUnit : UnitBase
    {
        private SabotageAction _sabotageAction;

        /// <summary>
        /// Caches the sabotage-action component alongside the base NavMeshAgent.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            _sabotageAction = GetComponent<SabotageAction>();
        }

        /// <summary>
        /// Assigns this Saboteur to sabotage the given target, overriding any current assignment.
        /// </summary>
        /// <param name="target">The target to sabotage.</param>
        public void AssignSabotageTarget(ISabotageTarget target)
        {
            _sabotageAction.AssignTarget(target);
        }
    }
}
