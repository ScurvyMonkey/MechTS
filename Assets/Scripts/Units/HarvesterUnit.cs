using MechTS.Economy;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// The Harvester unit: extends <see cref="UnitBase"/> with the ability to be
    /// assigned to a resource node. The gather/deposit state machine itself lives in
    /// <see cref="HarvesterGatherBehavior"/>, per the project's Units/Economy folder split.
    /// </summary>
    [RequireComponent(typeof(HarvesterGatherBehavior))]
    public class HarvesterUnit : UnitBase, ISabotageTarget
    {
        private HarvesterGatherBehavior _gatherBehavior;

        /// <summary>ISabotageTarget position — this unit's current world position.</summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// This Harvester's gather/deposit state machine, cached so callers like
        /// <see cref="MechTS.AI.EnemyEconomyAI"/> never need a per-tick <c>GetComponent</c>
        /// lookup.
        /// </summary>
        public HarvesterGatherBehavior GatherBehavior => _gatherBehavior;

        /// <summary>
        /// Caches the gather-behavior component alongside the base NavMeshAgent.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            _gatherBehavior = GetComponent<HarvesterGatherBehavior>();
        }

        /// <summary>
        /// Assigns this harvester to gather from the given resource node, overriding any
        /// current assignment.
        /// </summary>
        /// <param name="node">The resource node to gather from.</param>
        public void AssignToNode(ResourceNode node)
        {
            _gatherBehavior.AssignToNode(node);
        }

        /// <summary>
        /// A sabotaged Harvester is destroyed outright.
        /// </summary>
        /// <param name="effectConfig">Unused for Harvesters — the effect is absolute.</param>
        public void ApplySabotage(SabotageEffectConfig effectConfig)
        {
            HealthComponent?.Kill(gameObject);
        }
    }
}
