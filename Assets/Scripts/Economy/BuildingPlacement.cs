using MechTS.Core;
using MechTS.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MechTS.Economy
{
    /// <summary>
    /// Lets the player select a building to place during the Economy Round. Right-click
    /// confirms placement — not left-click, which is <see cref="SelectionController"/>'s
    /// exclusive domain — via <see cref="TryHandlePendingPlacementClick"/>, called from
    /// <see cref="Units.AttackCommand"/>'s own right-click chain, mirroring
    /// <see cref="Units.UnitOrderCommand.TryHandlePendingOrderClick"/>'s established
    /// "arm a pending mode, resolve on the next right-click" pattern (issue #26) so this
    /// never races <see cref="SelectionController"/>'s own left-click ownership. Confirming
    /// placement spends resources immediately but no longer instantiates the building on the
    /// spot (issue #42) — instead issues the nearest selected <see cref="Units.CrewmanUnit"/>
    /// a build order, which travels to the site and constructs it over
    /// <see cref="BuildingDefinition.buildTime"/>. While a building is pending, a translucent
    /// <see cref="BuildingPlacementGhost"/> follows the cursor, tinted red where it would
    /// overlap an existing building or, for a <see cref="BuildingDefinition.requiresResourceNode"/>
    /// building (issue #51), fall outside every <see cref="ResourceNode"/>'s radius —
    /// <see cref="IsValidPlacement"/> also gates the real placement, so an invalid position can
    /// never actually be confirmed, not just discouraged cosmetically. Placement is refused
    /// outside the Economy Round, or if no Crewman is currently selected.
    /// </summary>
    public class BuildingPlacement : MonoBehaviour
    {
        [SerializeField] private LayerMask _groundLayerMask;
        [SerializeField] private LayerMask _buildingLayerMask;
        [SerializeField] private Faction _localFaction = Faction.Player;

        private EconomyManager _economyManager;
        private TechManager _techManager;
        private UnitManager _unitManager;
        private Camera _camera;
        private BuildingDefinition _pendingBuilding;
        private BuildingPlacementGhost _ghost;

        /// <summary>Whether a building is currently pending placement.</summary>
        public bool HasPendingBuilding => _pendingBuilding != null;

        /// <summary>
        /// Caches the scene's <see cref="EconomyManager"/>, <see cref="TechManager"/>,
        /// <see cref="UnitManager"/>, and main camera.
        /// </summary>
        private void Start()
        {
            _economyManager = FindFirstObjectByType<EconomyManager>();
            _techManager = FindFirstObjectByType<TechManager>();
            _unitManager = FindFirstObjectByType<UnitManager>();
            _camera = Camera.main;
        }

        /// <summary>
        /// Selects a building definition to place on the next right-click, spawning its
        /// placement ghost. Replaces any building/ghost already pending.
        /// </summary>
        /// <param name="definition">The building to place next.</param>
        public void SelectBuildingToPlace(BuildingDefinition definition)
        {
            if (_ghost != null) Destroy(_ghost.gameObject);

            _pendingBuilding = definition;
            var ghostPrefab = definition != null ? definition.GetPrefab(_localFaction) : null;
            _ghost = ghostPrefab != null ? BuildingPlacementGhost.Create(ghostPrefab) : null;
        }

        /// <summary>
        /// While a building is pending, follows the cursor's ground position with the
        /// placement ghost each frame and tints it to reflect whether that position is
        /// currently a legal placement. Does not itself confirm placement — see
        /// <see cref="TryHandlePendingPlacementClick"/>.
        /// </summary>
        private void Update()
        {
            if (_pendingBuilding == null || _ghost == null) return;
            if (!_economyManager.IsEconomyRoundActive) return;

            var mouse = Mouse.current;
            if (mouse == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, _groundLayerMask))
            {
                _ghost.SetPosition(hit.point);
                _ghost.SetValid(IsValidPlacement(hit.point, _pendingBuilding));
            }
        }

        /// <summary>
        /// If a building is currently pending placement, resolves this right-click into a
        /// placement attempt at the clicked ground position and returns true (consuming the
        /// click) — mirrors <see cref="Units.UnitOrderCommand.TryHandlePendingOrderClick"/>'s
        /// contract exactly: called by <see cref="Units.AttackCommand"/> before its normal
        /// Sabotage/Repair/Attack/Move chain. Returns false (nothing pending, or the click
        /// didn't land on ground) so that chain runs normally instead.
        /// </summary>
        /// <param name="ray">The right-click's camera ray.</param>
        /// <param name="groundLayerMask">The ground layer mask to raycast against.</param>
        public bool TryHandlePendingPlacementClick(Ray ray, LayerMask groundLayerMask)
        {
            if (_pendingBuilding == null) return false;
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayerMask)) return false;

            TryPlace(hit.point);
            return true;
        }

        /// <summary>
        /// Checks the position is clear of existing buildings, cost, power capacity, and that
        /// a Crewman is currently selected, then spends resources and issues the nearest
        /// selected Crewman a build order (issue #42) — the building itself doesn't exist yet;
        /// it's instantiated once that Crewman arrives (see
        /// <see cref="Units.CrewmanBuildBehavior.BeginConstruction"/>).
        /// </summary>
        /// <param name="position">The world position to place the building at.</param>
        private void TryPlace(Vector3 position)
        {
            var state = _economyManager.GetState(_localFaction);
            if (state == null) return;

            if (!IsValidPlacement(position, _pendingBuilding))
            {
                Debug.Log($"BuildingPlacement: {_pendingBuilding.displayName} cannot be placed at this position — placement refused.");
                return;
            }

            var crewman = FindNearestSelectedCrewman(position);
            if (crewman == null)
            {
                Debug.Log("BuildingPlacement: no Crewman currently selected — cancelling placement.");
                ClearPendingBuilding();
                return;
            }

            var cost = _pendingBuilding.GetCost();
            float powerMultiplier = _techManager != null ? _techManager.GetStatMultiplier(_localFaction, UpgradeStatType.PowerCapacity) : 1f;
            bool powerOk = PowerSystem.CanAfford(state, _pendingBuilding.powerGenerated, _pendingBuilding.powerConsumed, powerMultiplier);

            if (!state.Stockpile.CanAfford(cost) || !powerOk)
            {
                Debug.Log($"BuildingPlacement: cannot afford or insufficient power for {_pendingBuilding.displayName}.");
                return;
            }

            state.Stockpile.Spend(cost);
            crewman.AssignBuildOrder(_pendingBuilding, position);

            ClearPendingBuilding();
        }

        /// <summary>
        /// Clears the pending building selection and destroys its placement ghost, if any.
        /// </summary>
        private void ClearPendingBuilding()
        {
            _pendingBuilding = null;
            if (_ghost != null)
            {
                Destroy(_ghost.gameObject);
                _ghost = null;
            }
        }

        /// <summary>
        /// Whether <paramref name="definition"/> can legally be placed at <paramref name="position"/>:
        /// never overlapping an existing building, and — for a <see cref="BuildingDefinition.requiresResourceNode"/>
        /// definition (issue #51, the Auto-Extractor) — also within a registered
        /// <see cref="ResourceNode"/>'s radius. Both conditions are required (ANDed), not
        /// either/or, per the arch review for #51.
        /// </summary>
        /// <param name="position">The candidate placement position.</param>
        /// <param name="definition">The building being placed.</param>
        private bool IsValidPlacement(Vector3 position, BuildingDefinition definition)
        {
            if (WouldOverlapExistingBuilding(position, definition)) return false;
            if (definition.requiresResourceNode && !IsWithinResourceNode(position)) return false;
            return true;
        }

        /// <summary>
        /// Whether <paramref name="position"/> falls within any registered <see cref="ResourceNode"/>'s
        /// <see cref="ResourceNode.ScatterRadius"/> — a plain distance check against
        /// <see cref="EconomyManager.ResourceNodes"/>, not a physics query. <see cref="ResourceNode"/>
        /// sits on a different physics layer than <see cref="_buildingLayerMask"/> covers, so
        /// <see cref="WouldOverlapExistingBuilding"/>'s overlap box structurally cannot see nodes.
        /// </summary>
        /// <param name="position">The candidate placement position.</param>
        private bool IsWithinResourceNode(Vector3 position)
        {
            if (_economyManager == null) return false;

            foreach (var node in _economyManager.ResourceNodes)
            {
                if (node == null) continue;
                if (Vector3.Distance(position, node.Position) <= node.ScatterRadius) return true;
            }
            return false;
        }

        /// <summary>
        /// Whether placing <paramref name="definition"/>'s footprint at <paramref name="position"/>
        /// would overlap an existing building or main building, of either faction — checked
        /// against <see cref="_buildingLayerMask"/> (the same physics layer buildings, units,
        /// and the main building already share) via a box sized from the definition's own
        /// prefab <see cref="BoxCollider"/>, then filtered to only actual buildings so a unit
        /// standing on the spot doesn't block placement.
        /// </summary>
        /// <param name="position">The candidate placement position.</param>
        /// <param name="definition">The building being placed, for its footprint size.</param>
        private bool WouldOverlapExistingBuilding(Vector3 position, BuildingDefinition definition)
        {
            Vector3 halfExtents = GetFootprintHalfExtents(definition, _localFaction);
            var hits = Physics.OverlapBox(position + Vector3.up * halfExtents.y, halfExtents, Quaternion.identity, _buildingLayerMask);
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<BuildingInstance>() != null) return true;
                if (hit.GetComponentInParent<MainBuilding>() != null) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns half the world-space footprint of the given building definition's prefab,
        /// read from its <see cref="BoxCollider"/> (how every building's footprint is already
        /// defined), or a small default if it has none.
        /// </summary>
        /// <param name="definition">The building definition to measure.</param>
        /// <param name="faction">The placing faction, to resolve the correct prefab variant.</param>
        private static Vector3 GetFootprintHalfExtents(BuildingDefinition definition, Faction faction)
        {
            var prefab = definition != null ? definition.GetPrefab(faction) : null;
            if (prefab != null)
            {
                var box = prefab.GetComponent<BoxCollider>();
                if (box != null)
                {
                    return Vector3.Scale(box.size, prefab.transform.localScale) * 0.5f;
                }
            }
            return Vector3.one * 0.5f;
        }

        /// <summary>
        /// Returns the selected <see cref="Units.CrewmanUnit"/> closest to the given position,
        /// or null if none is currently selected — an already-selected non-Crewman doesn't
        /// block this (see <see cref="UI.BuildMenuPanel"/>'s own stricter all-Crewman gate on
        /// entering placement mode in the first place); this is a defensive re-check for the
        /// case where the selection changed between selecting a building and clicking ground.
        /// </summary>
        /// <param name="position">The position build orders are being resolved for.</param>
        private CrewmanUnit FindNearestSelectedCrewman(Vector3 position)
        {
            if (_unitManager == null) return null;

            CrewmanUnit nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (var unit in _unitManager.SelectedUnits)
            {
                if (!(unit is CrewmanUnit crewman)) continue;

                float distance = Vector3.Distance(unit.transform.position, position);
                if (distance < nearestDistance)
                {
                    nearest = crewman;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }
    }
}
