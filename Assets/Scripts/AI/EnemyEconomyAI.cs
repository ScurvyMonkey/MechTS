using MechTS.Economy;
using MechTS.Units;
using UnityEngine;

namespace MechTS.AI
{
    /// <summary>
    /// Decides the Enemy faction's Harvester economy each decision tick (issue #79): keeps the
    /// Enemy Main Building's production queue topped up to a target Harvester count, and assigns
    /// every Enemy Harvester that currently needs a node to the nearest one with remaining yield.
    /// A plain composed class, not a MonoBehaviour — owned and ticked by
    /// <see cref="EnemyAIController"/>, mirroring <see cref="EconomyManager"/>'s own
    /// EconomyRegistry/UnitUpkeepTracker composition (issue #61). This is the first of what will
    /// be several staggered per-unit-type AI modules living alongside it in this folder.
    /// </summary>
    public class EnemyEconomyAI
    {
        private readonly EnemyAIEconomyConfig _config;
        private UnitProductionDefinition _cachedHarvesterDefinition;

        /// <summary>
        /// Stores the config this AI decides against.
        /// </summary>
        /// <param name="config">The Enemy faction's target Harvester count and decision cadence.</param>
        public EnemyEconomyAI(EnemyAIEconomyConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Runs one decision pass: tops up Harvester production at the Enemy's Main Building,
        /// then assigns every Enemy Harvester that currently needs a node. No-ops safely if the
        /// Enemy has no Main Building right now (not yet spawned, or destroyed mid-round).
        /// </summary>
        /// <param name="economyManager">Source of the Enemy's Main Building, resource nodes, and economy state.</param>
        /// <param name="unitManager">Source of every currently active unit.</param>
        public void Tick(EconomyManager economyManager, UnitManager unitManager)
        {
            if (economyManager == null || unitManager == null) return;
            if (!economyManager.MainBuildings.TryGetValue(Faction.Enemy, out var mainBuilding) || mainBuilding == null) return;

            TryProduceHarvester(unitManager, mainBuilding);
            AssignIdleHarvesters(economyManager, unitManager);
        }

        /// <summary>
        /// Queues another Harvester at the Enemy Main Building if the faction's current Harvester
        /// count (alive plus already queued) is under <see cref="EnemyAIEconomyConfig.targetHarvesterCount"/>.
        /// Affordability is left entirely to <see cref="ProductionQueue.TryEnqueue"/> — an
        /// unaffordable request simply fails and is retried on the next decision tick.
        /// </summary>
        private void TryProduceHarvester(UnitManager unitManager, MainBuilding mainBuilding)
        {
            var productionQueue = mainBuilding.Production;
            if (productionQueue == null) return;

            int projectedCount = CountAliveHarvesters(unitManager) + productionQueue.QueueCount;
            if (projectedCount >= _config.targetHarvesterCount) return;

            var harvesterDefinition = GetHarvesterDefinition(mainBuilding);
            if (harvesterDefinition == null) return;

            productionQueue.TryEnqueue(harvesterDefinition);
        }

        /// <summary>
        /// Returns the entry in the Main Building's producible-units list whose resolved Enemy
        /// prefab is a Harvester, identified by component type on the resolved prefab rather
        /// than display name or array position — <see cref="UnitProductionDefinition"/> carries
        /// no unit-type marker, and today's single-entry producible list would make a positional
        /// assumption silently correct until a second producible unit is ever added. Resolved
        /// once and cached — the Main Building's producible-units list is fixed per mission, so
        /// this never needs to re-search or re-resolve a prefab's components on later ticks.
        /// </summary>
        /// <param name="mainBuilding">The Main Building whose producible units to search.</param>
        private UnitProductionDefinition GetHarvesterDefinition(MainBuilding mainBuilding)
        {
            if (_cachedHarvesterDefinition != null) return _cachedHarvesterDefinition;

            var producibleUnits = mainBuilding.ProducibleUnits;
            if (producibleUnits == null) return null;

            foreach (var definition in producibleUnits)
            {
                if (definition == null) continue;
                var prefab = definition.GetPrefab(Faction.Enemy);
                if (prefab != null && prefab.GetComponent<HarvesterUnit>() != null)
                {
                    _cachedHarvesterDefinition = definition;
                    return _cachedHarvesterDefinition;
                }
            }
            return null;
        }

        /// <summary>
        /// Counts currently-alive Enemy-faction Harvesters.
        /// </summary>
        /// <param name="unitManager">Source of every currently active unit.</param>
        private int CountAliveHarvesters(UnitManager unitManager)
        {
            int count = 0;
            foreach (var unit in unitManager.ActiveUnits)
            {
                if (unit != null && unit.Faction == Faction.Enemy && unit is HarvesterUnit) count++;
            }
            return count;
        }

        /// <summary>
        /// Assigns every Enemy Harvester that currently needs a node (freshly produced, or its
        /// node depleted and it's gone idle) to the nearest resource node with remaining yield.
        /// </summary>
        /// <param name="economyManager">Source of the world's resource nodes.</param>
        /// <param name="unitManager">Source of every currently active unit.</param>
        private void AssignIdleHarvesters(EconomyManager economyManager, UnitManager unitManager)
        {
            foreach (var unit in unitManager.ActiveUnits)
            {
                if (unit == null || unit.Faction != Faction.Enemy) continue;

                var harvester = unit as HarvesterUnit;
                if (harvester == null) continue;

                var gatherBehavior = harvester.GatherBehavior;
                if (gatherBehavior == null || !gatherBehavior.NeedsNodeAssignment) continue;

                var nearestNode = FindNearestAvailableNode(economyManager, harvester.transform.position);
                if (nearestNode != null)
                {
                    harvester.AssignToNode(nearestNode);
                }
            }
        }

        /// <summary>
        /// Finds the resource node with remaining yield nearest the given position.
        /// </summary>
        /// <param name="economyManager">Source of the world's resource nodes.</param>
        /// <param name="position">The position to search from.</param>
        private ResourceNode FindNearestAvailableNode(EconomyManager economyManager, Vector3 position)
        {
            ResourceNode nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var node in economyManager.ResourceNodes)
            {
                if (node == null || node.IsDepleted) continue;

                float distance = Vector3.Distance(position, node.Position);
                if (distance < nearestDistance)
                {
                    nearest = node;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }
    }
}
