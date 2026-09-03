using MechTS.Economy;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// The Crewman unit (issue #42): extends <see cref="UnitBase"/> with the ability to be
    /// assigned a repair target or a build order. Both behaviors' state machines live in
    /// <see cref="CrewmanRepairBehavior"/>/<see cref="CrewmanBuildBehavior"/>, per the
    /// project's Units/Economy folder split (matching <see cref="HarvesterUnit"/>/
    /// <see cref="SaboteurUnit"/>'s precedent). Has no <see cref="Weapon"/>, same as
    /// <see cref="HarvesterUnit"/> — a Crewman can't fight.
    /// </summary>
    [RequireComponent(typeof(CrewmanRepairBehavior))]
    [RequireComponent(typeof(CrewmanBuildBehavior))]
    public class CrewmanUnit : UnitBase
    {
        private CrewmanRepairBehavior _repairBehavior;
        private CrewmanBuildBehavior _buildBehavior;

        /// <summary>
        /// Caches the repair/build behavior components alongside the base NavMeshAgent.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            _repairBehavior = GetComponent<CrewmanRepairBehavior>();
            _buildBehavior = GetComponent<CrewmanBuildBehavior>();
        }

        /// <summary>
        /// Assigns this Crewman to repair the given target, overriding any current repair
        /// assignment and cancelling any in-progress build order — both behaviors drive the
        /// same <see cref="UnitBase.MoveTo"/>/<see cref="UnitBase.HasArrived"/>, so only one
        /// can be active at a time (a Crewman can't travel to two places at once).
        /// </summary>
        /// <param name="target">The friendly, damaged target to repair.</param>
        public void AssignRepairTarget(IRepairTarget target)
        {
            _buildBehavior.CancelBuildOrder();
            _repairBehavior.AssignRepairTarget(target);
        }

        /// <summary>
        /// Assigns this Crewman a build order at the given position, overriding any current
        /// build order and cancelling any active repair assignment (see
        /// <see cref="AssignRepairTarget"/> for why the two can't run concurrently).
        /// </summary>
        /// <param name="definition">The building to construct.</param>
        /// <param name="position">The world position to build at.</param>
        public void AssignBuildOrder(BuildingDefinition definition, Vector3 position)
        {
            _repairBehavior.CancelRepair();
            _buildBehavior.AssignBuildOrder(definition, position);
        }
    }
}
