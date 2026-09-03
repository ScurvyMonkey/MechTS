using System.Collections.Generic;
using MechTS.Units;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Owns the building, main-building, and resource-node registries extracted from
    /// <see cref="EconomyManager"/> (issue #61) — a plain composed class, not a
    /// MonoBehaviour or Bootstrapper-created singleton, held as a private field on
    /// <see cref="EconomyManager"/>. Its main-building/resource-node dictionaries are
    /// genuinely owned here (they were already independent <see cref="EconomyManager"/>
    /// fields, with every external reader going through <see cref="EconomyManager"/>'s
    /// public properties). Its building-registration methods are different: standing
    /// buildings/power live on <see cref="FactionEconomyState.Buildings"/>/
    /// <see cref="FactionEconomyState.PowerCapacity"/>/<see cref="FactionEconomyState.PowerUsed"/>,
    /// read directly by several other systems (<see cref="Vision.VisionManager"/>,
    /// <see cref="UI.MinimapPanel"/>, <see cref="UI.ProductionMenuPanel"/>,
    /// <see cref="Battle.VictoryConditionChecker"/>, <see cref="Units.Weapon"/>,
    /// <see cref="PowerSystem"/>) — so those methods operate on the
    /// <see cref="FactionEconomyState"/> passed in by <see cref="EconomyManager"/>, never a
    /// second collection of their own, per the arch review for #61.
    /// </summary>
    public class EconomyRegistry
    {
        private readonly Dictionary<Faction, MainBuilding> _mainBuildings = new Dictionary<Faction, MainBuilding>();
        private readonly List<ResourceNode> _resourceNodes = new List<ResourceNode>();

        /// <summary>Each faction's main building, keyed by faction.</summary>
        public IReadOnlyDictionary<Faction, MainBuilding> MainBuildings => _mainBuildings;

        /// <summary>
        /// Every <see cref="ResourceNode"/> currently on the map, world-wide. Never cleared by
        /// <see cref="ClearMainBuildings"/>/<see cref="DestroyAllMainBuildings"/> — nodes are
        /// persistent map content, not per-attempt spawned state.
        /// </summary>
        public IReadOnlyList<ResourceNode> ResourceNodes => _resourceNodes;

        /// <summary>
        /// Registers a placed building with the given faction state — power footprint is
        /// applied separately, see <see cref="ApplyBuildingPowerFootprint"/>. Refuses to
        /// register anything that also carries a <see cref="MainBuilding"/> component — a main
        /// building is combat-targetable via <see cref="MainBuildings"/>, not this list.
        /// </summary>
        /// <param name="state">The registering building's faction's economy state, or null if it has none this mission.</param>
        /// <param name="building">The building being registered.</param>
        public void RegisterBuilding(FactionEconomyState state, BuildingInstance building)
        {
            if (building.GetComponent<MainBuilding>() != null)
            {
                Debug.LogError($"EconomyRegistry: refused to register {building.name} as a BuildingInstance — it's a MainBuilding, which has no BuildingDefinition to back one.");
                return;
            }

            if (state == null) return;
            state.Buildings.Add(building);
        }

        /// <summary>
        /// Applies a building's economic footprint — power (generated/consumed) and, for a
        /// Silo, locked-pool capacity — to the given faction state.
        /// </summary>
        /// <param name="state">The building's faction's economy state, or null if it has none this mission.</param>
        /// <param name="building">The building whose economic footprint should be applied.</param>
        public void ApplyBuildingPowerFootprint(FactionEconomyState state, BuildingInstance building)
        {
            if (state == null) return;

            state.PowerCapacity += building.Definition.powerGenerated;
            state.PowerUsed += building.Definition.powerConsumed;

            if (building.Definition.isSilo)
            {
                state.LockedPool.AddCapacity(building.Definition.siloResourceType, building.Definition.siloCapacity);
            }
        }

        /// <summary>
        /// Deregisters a destroyed building and removes its power footprint from the given
        /// faction state. Unity defers `Destroy` to end-of-frame, so a building destroyed
        /// during a fresh Economy Round's cleanup can still fire this after that faction's
        /// state has already been replaced — the `Remove` check below detects that stale case
        /// and no-ops instead of corrupting the new state's power totals.
        /// </summary>
        /// <param name="state">The building's faction's economy state, or null if it has none this mission.</param>
        /// <param name="building">The building being deregistered.</param>
        public void DeregisterBuilding(FactionEconomyState state, BuildingInstance building)
        {
            if (state == null || !state.Buildings.Remove(building)) return;

            if (building.HasAppliedPowerFootprint)
            {
                state.PowerCapacity -= building.Definition.powerGenerated;
                state.PowerUsed -= building.Definition.powerConsumed;

                if (building.Definition.isSilo)
                {
                    state.LockedPool.RemoveCapacity(building.Definition.siloResourceType, building.Definition.siloCapacity);
                }
            }
        }

        /// <summary>
        /// Registers a resource node with this registry's world-wide node list.
        /// </summary>
        /// <param name="node">The node to register.</param>
        public void RegisterResourceNode(ResourceNode node)
        {
            if (!_resourceNodes.Contains(node))
            {
                _resourceNodes.Add(node);
            }
        }

        /// <summary>
        /// Deregisters a destroyed (depleted or sabotaged) resource node.
        /// </summary>
        /// <param name="node">The node to deregister.</param>
        public void DeregisterResourceNode(ResourceNode node)
        {
            _resourceNodes.Remove(node);
        }

        /// <summary>
        /// Spawns the given faction's Main Building at its painted <see cref="PlayerStartPoint"/>,
        /// if one is configured. A faction with no matching start point logs a clear error and
        /// skips spawning; multiple start points for the same faction use the first one found,
        /// logging a warning naming the count.
        /// </summary>
        /// <param name="start">The faction's starting economy configuration.</param>
        /// <param name="startPoints">Every <see cref="PlayerStartPoint"/> currently in the scene.</param>
        public void SpawnMainBuilding(FactionEconomyStart start, PlayerStartPoint[] startPoints)
        {
            if (start.mainBuildingPrefab == null) return;

            Vector3? spawnPosition = null;
            int matchCount = 0;
            foreach (var startPoint in startPoints)
            {
                if (startPoint.faction != start.faction) continue;
                matchCount++;
                if (spawnPosition == null)
                {
                    spawnPosition = startPoint.transform.position;
                }
            }

            if (matchCount == 0)
            {
                Debug.LogError($"EconomyRegistry: no PlayerStartPoint found for faction {start.faction} — its Main Building was not spawned. Paint a start point via the Map Editor.");
                return;
            }
            if (matchCount > 1)
            {
                Debug.LogWarning($"EconomyRegistry: {matchCount} PlayerStartPoints found for faction {start.faction} — using the first one found.");
            }

            var building = Object.Instantiate(start.mainBuildingPrefab, spawnPosition.Value, Quaternion.identity);
            var mainBuilding = building.GetComponent<MainBuilding>();
            if (mainBuilding == null)
            {
                mainBuilding = building.AddComponent<MainBuilding>();
            }
            mainBuilding.Initialize(start.faction, start.mainBuildingMaxHealth, start.mainBuildingVisionRadius, start.mainBuildingProducibleUnits);
            // No FactionColor.Apply here (issue #81) — every mission's Main Building now has
            // real per-faction art (Assets/Prefabs/Blue|Red), so tinting would corrupt its
            // real material instead of substituting for one that doesn't exist yet.
            _mainBuildings[start.faction] = mainBuilding;
        }

        /// <summary>
        /// Deregisters a main building destroyed by combat, so <see cref="MainBuildings"/>
        /// never holds a stale entry. Only removes the entry if it still points at this exact
        /// instance — guards against a leftover main building from a previous mission/attempt
        /// being destroyed after a fresh Economy Round has already spawned a new one.
        /// </summary>
        /// <param name="mainBuilding">The main building being deregistered.</param>
        public void DeregisterMainBuilding(MainBuilding mainBuilding)
        {
            if (_mainBuildings.TryGetValue(mainBuilding.Faction, out var current) && current == mainBuilding)
            {
                _mainBuildings.Remove(mainBuilding.Faction);
            }
        }

        /// <summary>
        /// Clears the tracked main-building dictionary without destroying anything — call
        /// immediately before <see cref="DestroyAllMainBuildings"/> is no longer needed, i.e.
        /// after it has already run for this reset. Never touches <see cref="ResourceNodes"/>,
        /// which persists across every Economy Round reset by design.
        /// </summary>
        public void ClearMainBuildings()
        {
            _mainBuildings.Clear();
        }

        /// <summary>
        /// Destroys every currently tracked main building, so a fresh Economy Round never
        /// inherits one from a previous mission or failed attempt. Never touches
        /// <see cref="ResourceNodes"/>, which persists across every Economy Round reset by
        /// design.
        /// </summary>
        public void DestroyAllMainBuildings()
        {
            foreach (var mainBuilding in _mainBuildings.Values)
            {
                if (mainBuilding != null) Object.Destroy(mainBuilding.gameObject);
            }
        }
    }
}
