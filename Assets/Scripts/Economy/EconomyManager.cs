using System.Collections.Generic;
using MechTS.Core;
using MechTS.Units;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Owns each faction's economy state and the Economy Round timer. Gates harvesting
    /// and building placement to the Economy Round, while unit production (via
    /// <see cref="ProductionQueue"/>) is left ungated so it can continue into the Battle
    /// Round. Resets all faction state every time the Economy Round begins. Created by
    /// <see cref="Bootstrapper"/>. Building/main-building/resource-node registry
    /// bookkeeping and unit upkeep accrual live in composed <see cref="EconomyRegistry"/>/
    /// <see cref="UnitUpkeepTracker"/> instances (issue #61) — this class stays limited to
    /// per-faction state ownership, the round timer, and thin delegating wrappers.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        private MissionEconomyConfig _missionConfig;
        private GameManager _gameManager;
        private UnitManager _unitManager;
        private readonly Dictionary<Faction, FactionEconomyState> _factionStates = new Dictionary<Faction, FactionEconomyState>();
        private readonly EconomyRegistry _registry = new EconomyRegistry();
        private readonly UnitUpkeepTracker _upkeepTracker = new UnitUpkeepTracker();
        private float _roundTimeRemaining;
        private bool _roundTimerRunning;

        /// <summary>
        /// Whether the Economy Round is currently active. Harvesting and building
        /// placement are only legal while this is true.
        /// </summary>
        public bool IsEconomyRoundActive => _gameManager != null && _gameManager.CurrentState == GameState.EconomyRound;

        /// <summary>Seconds remaining in the current Economy Round countdown.</summary>
        public float RoundTimeRemaining => _roundTimeRemaining;

        private static readonly BuildingDefinition[] EmptyBuildings = new BuildingDefinition[0];

        /// <summary>The buildings placeable this mission, per <see cref="MissionEconomyConfig"/>.</summary>
        public IReadOnlyList<BuildingDefinition> BuildableBuildings =>
            _missionConfig != null && _missionConfig.buildableBuildings != null ? _missionConfig.buildableBuildings : EmptyBuildings;

        /// <summary>Each faction's main building, keyed by faction (see <see cref="UI.MinimapPanel"/>).</summary>
        public IReadOnlyDictionary<Faction, MainBuilding> MainBuildings => _registry.MainBuildings;

        /// <summary>
        /// Every <see cref="ResourceNode"/> currently on the map, world-wide (nodes have no
        /// owning faction). Unlike <see cref="FactionEconomyState.Buildings"/> and active
        /// units, this list is <b>not</b> touched by <see cref="CleanupExistingState"/> —
        /// nodes are persistent map content, not per-attempt spawned state, so a fresh
        /// Economy Round must never destroy or clear them.
        /// </summary>
        public IReadOnlyList<ResourceNode> ResourceNodes => _registry.ResourceNodes;

        /// <summary>
        /// Assigns this mission's economy config. Called by <see cref="Bootstrapper"/>
        /// immediately after creating this manager, since it's instantiated at runtime
        /// rather than from a prefab with an Inspector-assigned reference.
        /// </summary>
        /// <param name="missionConfig">The mission's Economy Round configuration.</param>
        public void Initialize(MissionEconomyConfig missionConfig)
        {
            _missionConfig = missionConfig;
        }

        /// <summary>
        /// Caches manager references and subscribes to state changes. Faction economy state
        /// is seeded only in response to an actual <see cref="GameState.EconomyRound"/> entry
        /// (see <see cref="HandleGameStateChanged"/>), not unconditionally here — this used to
        /// also call <see cref="InitializeFactionStates"/> directly, which meant a scene that
        /// transitions into <see cref="GameState.EconomyRound"/> during the same session (e.g.
        /// <see cref="Utilities.TestSetup"/>'s own explicit <see cref="GameManager.StartEconomyRound"/>
        /// call) initialized faction state twice — once here, once via the event — landing
        /// squarely in the deferred-<c>Destroy()</c> window where <c>TestSetup</c>'s
        /// <c>FindObjectsByType&lt;MainBuilding&gt;()</c> would see both the stale and fresh
        /// generation and double-spawn starter units (issue #34).
        /// </summary>
        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _unitManager = FindFirstObjectByType<UnitManager>();
            _gameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        /// <summary>
        /// Unsubscribes from the <see cref="GameManager"/>.
        /// </summary>
        private void OnDestroy()
        {
            if (_gameManager != null)
            {
                _gameManager.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        /// <summary>
        /// Ticks the Economy Round countdown and drains unit upkeep while that round is active.
        /// </summary>
        private void Update()
        {
            if (!_roundTimerRunning) return;

            _roundTimeRemaining -= Time.deltaTime;
            if (_roundTimeRemaining <= 0f)
            {
                _roundTimerRunning = false;
                _gameManager.StartBattleRound();
            }

            if (_unitManager != null)
            {
                _upkeepTracker.Tick(Time.deltaTime, _unitManager.ActiveUnits, GetState);
            }
        }

        /// <summary>
        /// Starts the round timer and resets all faction economy state each time a fresh
        /// Economy Round begins; stops the timer once the Battle Round begins.
        /// </summary>
        /// <param name="newState">The state the game just transitioned into.</param>
        private void HandleGameStateChanged(GameState newState)
        {
            if (newState == GameState.EconomyRound)
            {
                InitializeFactionStates();
                _roundTimeRemaining = _missionConfig.economyRoundDurationSeconds;
                _roundTimerRunning = true;
            }
            else if (newState == GameState.BattleRound)
            {
                _roundTimerRunning = false;
            }
        }

        /// <summary>
        /// Destroys any buildings/units left over from a previous mission or attempt,
        /// then resets every faction's economy state to the mission config's starting
        /// values and spawns each faction's main building at its painted
        /// <see cref="PlayerStartPoint"/> (issue #57).
        /// </summary>
        private void InitializeFactionStates()
        {
            CleanupExistingState();
            _factionStates.Clear();
            _registry.ClearMainBuildings();
            _upkeepTracker.Clear();

            var startPoints = FindObjectsByType<PlayerStartPoint>(FindObjectsSortMode.None);

            foreach (var start in _missionConfig.factionStarts)
            {
                var state = new FactionEconomyState(start.faction, start.startingOre, start.startingBiomass, start.startingGold);
                _factionStates[start.faction] = state;

                _registry.SpawnMainBuilding(start, startPoints);
            }
        }

        /// <summary>
        /// Destroys every currently tracked building, main building, and active unit, so a fresh Economy
        /// Round never inherits state from a previous mission or failed attempt.
        /// Deliberately does <b>not</b> touch <see cref="ResourceNodes"/> — nodes are
        /// persistent map content, not per-attempt spawned state (see
        /// <see cref="ResourceNodes"/>'s doc comment).
        /// </summary>
        private void CleanupExistingState()
        {
            foreach (var state in _factionStates.Values)
            {
                foreach (var building in new List<BuildingInstance>(state.Buildings))
                {
                    if (building != null) Destroy(building.gameObject);
                }
            }

            _registry.DestroyAllMainBuildings();

            if (_unitManager != null)
            {
                foreach (var unit in new List<UnitBase>(_unitManager.ActiveUnits))
                {
                    if (unit != null) Destroy(unit.gameObject);
                }
            }
        }

        /// <summary>
        /// Registers a resource node with this manager's world-wide node list.
        /// </summary>
        /// <param name="node">The node to register.</param>
        public void RegisterResourceNode(ResourceNode node)
        {
            _registry.RegisterResourceNode(node);
        }

        /// <summary>
        /// Deregisters a destroyed (depleted or sabotaged) resource node.
        /// </summary>
        /// <param name="node">The node to deregister.</param>
        public void DeregisterResourceNode(ResourceNode node)
        {
            _registry.DeregisterResourceNode(node);
        }

        /// <summary>
        /// Deregisters a main building destroyed by combat (issue #28), so
        /// <see cref="MainBuildings"/> never holds a stale entry for
        /// <see cref="UI.MinimapPanel"/>/<see cref="Battle.VictoryConditionChecker"/> to trip
        /// over.
        /// </summary>
        /// <param name="mainBuilding">The main building being deregistered.</param>
        public void DeregisterMainBuilding(MainBuilding mainBuilding)
        {
            _registry.DeregisterMainBuilding(mainBuilding);
        }

        /// <summary>
        /// Returns the given faction's live economy state, or null if it has none this mission.
        /// </summary>
        /// <param name="faction">The faction to look up.</param>
        public FactionEconomyState GetState(Faction faction)
        {
            return _factionStates.TryGetValue(faction, out var state) ? state : null;
        }

        /// <summary>
        /// Deposits harvested resources into a faction's stockpile — see
        /// <see cref="FactionEconomyState.Deposit"/> for the locked-pool split logic (moved
        /// there by issue #61).
        /// </summary>
        /// <param name="faction">The depositing faction.</param>
        /// <param name="type">The resource type deposited.</param>
        /// <param name="amount">The amount deposited.</param>
        public void Deposit(Faction faction, ResourceType type, int amount)
        {
            GetState(faction)?.Deposit(type, amount);
        }

        /// <summary>
        /// Registers a placed building with its owning faction's state — power footprint is
        /// applied separately, see <see cref="ApplyBuildingPowerFootprint"/> (issue #42).
        /// </summary>
        /// <param name="building">The building being registered.</param>
        public void RegisterBuilding(BuildingInstance building)
        {
            _registry.RegisterBuilding(GetState(building.Faction), building);
        }

        /// <summary>
        /// Applies a building's economic footprint — power (generated/consumed) and, for a
        /// Silo (issue #52), locked-pool capacity — to its faction's economy state.
        /// </summary>
        /// <param name="building">The building whose economic footprint should be applied.</param>
        public void ApplyBuildingPowerFootprint(BuildingInstance building)
        {
            _registry.ApplyBuildingPowerFootprint(GetState(building.Faction), building);
        }

        /// <summary>
        /// Deregisters a destroyed building and removes its power footprint.
        /// </summary>
        /// <param name="building">The building being deregistered.</param>
        public void DeregisterBuilding(BuildingInstance building)
        {
            _registry.DeregisterBuilding(GetState(building.Faction), building);
        }

        /// <summary>
        /// Whether the given faction owns at least one fully-constructed building whose
        /// <see cref="BuildingDefinition"/> matches the given predicate (issue #45 — shared by
        /// <see cref="TechManager.CanResearch"/>'s Technology Plant check and
        /// <see cref="UI.BuildMenuPanel"/>'s Robotics Plant check, so the "does this faction
        /// own a building with property X" scan lives in one place). A building still under
        /// construction (issue #42) never counts — matches its existing no-power/no-vision/
        /// no-production state until <see cref="BuildingInstance.CompleteConstruction"/>.
        /// </summary>
        /// <param name="faction">The faction to check.</param>
        /// <param name="predicate">Which building definitions count.</param>
        public bool FactionOwnsBuildingWhere(Faction faction, System.Func<BuildingDefinition, bool> predicate)
        {
            var state = GetState(faction);
            if (state == null) return false;

            foreach (var building in state.Buildings)
            {
                if (building == null || building.Definition == null || building.IsUnderConstruction) continue;
                if (predicate(building.Definition)) return true;
            }
            return false;
        }
    }
}
