using System.Collections.Generic;
using MechTS.Core;
using MechTS.Economy;
using MechTS.Units;
using UnityEngine;

namespace MechTS.Vision
{
    /// <summary>
    /// Computes each faction's currently-visible area as the union of vision-radius circles
    /// around that faction's alive units, buildings, and main building, and tracks a
    /// per-faction 3-state (<see cref="FogState"/>) grid so previously-seen ground stays
    /// remembered as <see cref="FogState.Explored"/> once vision moves on. Both factions'
    /// vision is computed symmetrically (matching how Economy Round gives the Enemy a full
    /// parallel economy) but only the Player faction's fog currently drives any rendering or
    /// gameplay gate — enemy AI decision-making doesn't exist yet to consume its own fog.
    /// Created by <see cref="Bootstrapper"/>.
    /// </summary>
    public class VisionManager : MonoBehaviour
    {
        /// <summary>
        /// The fog grid's cell size, matching the Terrain Tilemap's cell size (issue #17) so
        /// the fog shroud renderer can align 1:1 with it.
        /// </summary>
        public const float CellSize = 5f;

        private GameManager _gameManager;
        private UnitManager _unitManager;
        private EconomyManager _economyManager;
        private CameraManager _cameraManager;

        private readonly Dictionary<Faction, FogState[,]> _fogGrids = new Dictionary<Faction, FogState[,]>();
        private static readonly Faction[] AllFactions = { Faction.Player, Faction.Enemy };

        private Vector2 _boundsMin;
        private int _gridWidth;
        private int _gridHeight;
        private bool _gridReady;

        /// <summary>The fog grid's world-space extent on the X axis (number of cells).</summary>
        public int GridWidth => _gridWidth;

        /// <summary>The fog grid's world-space extent on the Z axis (number of cells).</summary>
        public int GridHeight => _gridHeight;

        /// <summary>
        /// Caches manager references, subscribes to state changes, and builds the fog grid
        /// from <see cref="CameraManager"/>'s configured map bounds.
        /// </summary>
        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _unitManager = FindFirstObjectByType<UnitManager>();
            _economyManager = FindFirstObjectByType<EconomyManager>();
            _cameraManager = FindFirstObjectByType<CameraManager>();

            if (_gameManager != null) _gameManager.OnGameStateChanged += HandleGameStateChanged;

            BuildGrids();
        }

        /// <summary>
        /// Unsubscribes from the <see cref="GameManager"/>.
        /// </summary>
        private void OnDestroy()
        {
            if (_gameManager != null) _gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        /// <summary>
        /// Resets every faction's fog memory to fully <see cref="FogState.Unexplored"/> on
        /// every fresh Economy Round entry — mirroring <see cref="EconomyManager"/>'s own
        /// per-round reset exactly, so a mission retry doesn't inherit stale exploration from
        /// a previous attempt. The very next <see cref="Update"/> immediately re-derives
        /// whatever's actually visible right now from live unit/building positions.
        /// </summary>
        /// <param name="newState">The state the game just transitioned into.</param>
        private void HandleGameStateChanged(GameState newState)
        {
            if (newState == GameState.EconomyRound) ResetFog();
        }

        /// <summary>
        /// Allocates (or reallocates) both factions' fog grids sized from
        /// <see cref="CameraManager.Config"/>'s map bounds. Safe to call again if the camera
        /// config wasn't ready the first time.
        /// </summary>
        private void BuildGrids()
        {
            if (_cameraManager == null || _cameraManager.Config == null) return;

            var config = _cameraManager.Config;
            _boundsMin = config.boundsMin;
            _gridWidth = Mathf.Max(1, Mathf.CeilToInt((config.boundsMax.x - config.boundsMin.x) / CellSize));
            _gridHeight = Mathf.Max(1, Mathf.CeilToInt((config.boundsMax.y - config.boundsMin.y) / CellSize));

            _fogGrids[Faction.Player] = new FogState[_gridWidth, _gridHeight];
            _fogGrids[Faction.Enemy] = new FogState[_gridWidth, _gridHeight];
            _gridReady = true;
        }

        /// <summary>
        /// Resets every faction's grid to fully <see cref="FogState.Unexplored"/>, building
        /// the grid first if it isn't ready yet.
        /// </summary>
        private void ResetFog()
        {
            if (!_gridReady) BuildGrids();
            if (!_gridReady) return;

            foreach (var grid in _fogGrids.Values)
            {
                for (int x = 0; x < _gridWidth; x++)
                {
                    for (int z = 0; z < _gridHeight; z++) grid[x, z] = FogState.Unexplored;
                }
            }
        }

        /// <summary>
        /// Recomputes both factions' currently-visible cells every tick from live
        /// unit/building/main-building positions and vision radii.
        /// </summary>
        private void Update()
        {
            if (!_gridReady)
            {
                BuildGrids();
                return;
            }

            RecomputeVisibility();
        }

        /// <summary>
        /// Downgrades every cell that was <see cref="FogState.Visible"/> a moment ago to
        /// <see cref="FogState.Explored"/> (so it's remembered, not reset to
        /// <see cref="FogState.Unexplored"/>), then re-marks <see cref="FogState.Visible"/>
        /// every cell actually within vision this tick.
        /// </summary>
        private void RecomputeVisibility()
        {
            foreach (var grid in _fogGrids.Values)
            {
                for (int x = 0; x < _gridWidth; x++)
                {
                    for (int z = 0; z < _gridHeight; z++)
                    {
                        if (grid[x, z] == FogState.Visible) grid[x, z] = FogState.Explored;
                    }
                }
            }

            MarkUnitVision();
            MarkBuildingVision();
        }

        /// <summary>
        /// Marks vision from every alive unit. Skips a unit whose <see cref="Health"/> is
        /// already dead but not yet destroyed (Unity's <c>Destroy()</c> is deferred to
        /// end-of-frame, so a just-killed unit can still appear in
        /// <see cref="UnitManager.ActiveUnits"/> for up to a frame).
        /// </summary>
        private void MarkUnitVision()
        {
            if (_unitManager == null) return;

            foreach (var unit in _unitManager.ActiveUnits)
            {
                if (unit == null) continue;
                if (unit.HealthComponent != null && unit.HealthComponent.IsDead) continue;

                MarkVisible(unit.Faction, unit.transform.position, unit.VisionRadius);
            }
        }

        /// <summary>
        /// Marks vision from every alive, fully-constructed building and main building, for
        /// both factions. A detection-capable building's <see cref="BuildingDefinition.detectionRadius"/>
        /// replaces (not adds to) its baseline <see cref="BuildingDefinition.visionRadius"/>.
        /// A building still under construction (issue #42) contributes no vision at all —
        /// matches its no-power/no-production state until <see cref="BuildingInstance.CompleteConstruction"/>.
        /// </summary>
        private void MarkBuildingVision()
        {
            if (_economyManager == null) return;

            foreach (var faction in AllFactions)
            {
                var state = _economyManager.GetState(faction);
                if (state == null) continue;

                foreach (var building in state.Buildings)
                {
                    if (building == null || building.Definition == null) continue;
                    if (building.HealthComponent != null && building.HealthComponent.IsDead) continue;
                    if (building.IsUnderConstruction) continue;

                    float radius = building.Definition.isDetectionCapable
                        ? building.Definition.detectionRadius
                        : building.Definition.visionRadius;
                    MarkVisible(faction, building.transform.position, radius);
                }
            }

            foreach (var kv in _economyManager.MainBuildings)
            {
                var mainBuilding = kv.Value;
                if (mainBuilding == null) continue;
                if (mainBuilding.HealthComponent != null && mainBuilding.HealthComponent.IsDead) continue;

                MarkVisible(kv.Key, mainBuilding.transform.position, mainBuilding.VisionRadius);
            }
        }

        /// <summary>
        /// Marks every cell within <paramref name="radius"/> of <paramref name="worldPosition"/>
        /// as <see cref="FogState.Visible"/> for the given faction.
        /// </summary>
        /// <param name="faction">The faction gaining vision.</param>
        /// <param name="worldPosition">The vision source's world position.</param>
        /// <param name="radius">The vision radius, in world units.</param>
        private void MarkVisible(Faction faction, Vector3 worldPosition, float radius)
        {
            if (radius <= 0f || !_fogGrids.TryGetValue(faction, out var grid)) return;

            WorldToCell(worldPosition, out int centerX, out int centerZ);
            int cellRadius = Mathf.CeilToInt(radius / CellSize) + 1;

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                int x = centerX + dx;
                if (x < 0 || x >= _gridWidth) continue;

                for (int dz = -cellRadius; dz <= cellRadius; dz++)
                {
                    int z = centerZ + dz;
                    if (z < 0 || z >= _gridHeight) continue;

                    if (Vector3.Distance(CellToWorld(x, z), worldPosition) <= radius)
                    {
                        grid[x, z] = FogState.Visible;
                    }
                }
            }
        }

        /// <summary>
        /// Converts a world position to its containing cell coordinates.
        /// </summary>
        private void WorldToCell(Vector3 worldPosition, out int x, out int z)
        {
            x = Mathf.FloorToInt((worldPosition.x - _boundsMin.x) / CellSize);
            z = Mathf.FloorToInt((worldPosition.z - _boundsMin.y) / CellSize);
        }

        /// <summary>
        /// Returns a cell's world-space center position.
        /// </summary>
        /// <param name="x">The cell's X coordinate.</param>
        /// <param name="z">The cell's Z coordinate.</param>
        public Vector3 CellToWorld(int x, int z)
        {
            float worldX = _boundsMin.x + (x + 0.5f) * CellSize;
            float worldZ = _boundsMin.y + (z + 0.5f) * CellSize;
            return new Vector3(worldX, 0f, worldZ);
        }

        /// <summary>
        /// Returns the given faction's current <see cref="FogState"/> at a world position.
        /// Positions outside the grid bounds are always <see cref="FogState.Unexplored"/>.
        /// </summary>
        /// <param name="faction">The faction whose knowledge to query.</param>
        /// <param name="worldPosition">The world position to check.</param>
        public FogState GetFogState(Faction faction, Vector3 worldPosition)
        {
            if (!_gridReady || !_fogGrids.TryGetValue(faction, out var grid)) return FogState.Unexplored;

            WorldToCell(worldPosition, out int x, out int z);
            if (x < 0 || x >= _gridWidth || z < 0 || z >= _gridHeight) return FogState.Unexplored;

            return grid[x, z];
        }

        /// <summary>
        /// Returns whether a world position is currently (this tick) visible to the given faction.
        /// </summary>
        /// <param name="faction">The faction whose vision to query.</param>
        /// <param name="worldPosition">The world position to check.</param>
        public bool IsVisible(Faction faction, Vector3 worldPosition)
        {
            return GetFogState(faction, worldPosition) == FogState.Visible;
        }
    }
}
