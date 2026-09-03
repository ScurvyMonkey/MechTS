using System.Collections;
using MechTS.Core;
using MechTS.Economy;
using MechTS.Units;
using UnityEngine;

namespace MechTS.Utilities
{
    /// <summary>
    /// Manual-testing scaffolding only — not a real game system. Auto-starts the
    /// Economy Round and spawns one Harvester, one Saboteur, one test combat unit, and one
    /// Crewman (issue #42 — no production building can produce it yet, so it needs to be
    /// spawnable here to be testable at all) near each faction's main building, plus one
    /// Ranger/Reaper/Dredge (issue #43 — same "no production building yet" reasoning,
    /// Player faction only since these have no Enemy-variant art yet), so a tester has
    /// something to click and command immediately on entering Play Mode instead of an empty map.
    /// </summary>
    public class TestSetup : MonoBehaviour
    {
        [SerializeField] private GameObject _harvesterPrefab;
        [SerializeField] private GameObject _saboteurPrefab;
        [SerializeField] private GameObject _testCombatUnitPrefab;
        [SerializeField] private GameObject _crewmanPrefab;
        [SerializeField] private GameObject _rangerPrefab;
        [SerializeField] private GameObject _reaperPrefab;
        [SerializeField] private GameObject _dredgePrefab;

        [Header("Enemy variants (optional — falls back to the Human prefab above if unassigned)")]
        [SerializeField] private GameObject _enemyHarvesterPrefab;
        [SerializeField] private GameObject _enemySaboteurPrefab;
        [SerializeField] private GameObject _enemyTestCombatUnitPrefab;
        [SerializeField] private GameObject _enemyCrewmanPrefab;

        /// <summary>
        /// Waits one frame so every manager's own Start() has run and subscribed to
        /// GameManager events, then starts the Economy Round and spawns starter units.
        /// </summary>
        private IEnumerator Start()
        {
            yield return null;

            var gameManager = FindFirstObjectByType<GameManager>();
            gameManager.StartEconomyRound();

            foreach (var mainBuilding in FindObjectsByType<MainBuilding>(FindObjectsSortMode.None))
            {
                SpawnStarterUnits(mainBuilding);
            }
        }

        /// <summary>
        /// Spawns a Harvester, Saboteur, and test combat unit near the given main
        /// building, tinted with its faction's placeholder color. The Player's
        /// Harvester is auto-assigned to the nearest resource node for convenience.
        /// </summary>
        /// <param name="mainBuilding">The main building to spawn units near.</param>
        private void SpawnStarterUnits(MainBuilding mainBuilding)
        {
            Vector3 basePos = mainBuilding.transform.position;
            Faction faction = mainBuilding.Faction;

            var harvesterGo = SpawnUnit(SelectPrefab(_harvesterPrefab, _enemyHarvesterPrefab, faction), basePos + new Vector3(4f, 0f, 2f), faction, skipFactionTint: true);
            SpawnUnit(SelectPrefab(_saboteurPrefab, _enemySaboteurPrefab, faction), basePos + new Vector3(4f, 0f, -2f), faction);
            SpawnUnit(SelectPrefab(_testCombatUnitPrefab, _enemyTestCombatUnitPrefab, faction), basePos + new Vector3(6f, 0f, 0f), faction);
            SpawnUnit(SelectPrefab(_crewmanPrefab, _enemyCrewmanPrefab, faction), basePos + new Vector3(6f, 0f, -3f), faction, skipFactionTint: true);

            // Ranger/Reaper/Dredge (issue #43) have no Enemy-faction art yet — spawned for
            // Player only, matching the issue's explicit "Enemy equivalents out of scope."
            if (faction == Faction.Player)
            {
                SpawnUnit(_rangerPrefab, basePos + new Vector3(8f, 0f, 2f), faction);
                SpawnUnit(_reaperPrefab, basePos + new Vector3(8f, 0f, 0f), faction);
                SpawnUnit(_dredgePrefab, basePos + new Vector3(8f, 0f, -2f), faction);
            }

            if (faction == Faction.Player && harvesterGo != null)
            {
                var nearestNode = FindNearestResourceNode(basePos);
                if (nearestNode != null)
                {
                    harvesterGo.GetComponent<HarvesterUnit>().AssignToNode(nearestNode);
                }
            }
        }

        /// <summary>
        /// Returns the Enemy-variant prefab for <see cref="Faction.Enemy"/> if one is
        /// assigned, otherwise the Human prefab — mirrors
        /// <see cref="Economy.UnitProductionDefinition.GetPrefab"/>'s fallback behavior.
        /// </summary>
        /// <param name="humanPrefab">The default (Human) prefab.</param>
        /// <param name="enemyPrefab">The optional Enemy-variant prefab.</param>
        /// <param name="faction">The spawning faction.</param>
        private GameObject SelectPrefab(GameObject humanPrefab, GameObject enemyPrefab, Faction faction)
        {
            return faction == Faction.Enemy && enemyPrefab != null ? enemyPrefab : humanPrefab;
        }

        /// <summary>
        /// Instantiates a unit prefab, assigns its faction, and applies the placeholder faction
        /// color — unless <paramref name="skipFactionTint"/> is set, for a unit type that
        /// already has real per-faction art (issue #81) and would have that art's material
        /// corrupted by the tint instead of substituting for art that doesn't exist yet.
        /// </summary>
        /// <param name="prefab">The unit prefab to spawn.</param>
        /// <param name="position">The world position to spawn at.</param>
        /// <param name="faction">The faction to assign.</param>
        /// <param name="skipFactionTint">True if this unit type already has real per-faction art.</param>
        private GameObject SpawnUnit(GameObject prefab, Vector3 position, Faction faction, bool skipFactionTint = false)
        {
            if (prefab == null) return null;

            var go = Instantiate(prefab, position, Quaternion.identity);
            go.GetComponent<UnitBase>().SetFaction(faction);
            if (!skipFactionTint)
            {
                FactionColor.Apply(go, faction);
            }
            return go;
        }

        /// <summary>
        /// Finds the resource node closest to the given position, if any exist.
        /// </summary>
        /// <param name="position">The position to search from.</param>
        private ResourceNode FindNearestResourceNode(Vector3 position)
        {
            ResourceNode nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var node in FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
            {
                float distance = Vector3.Distance(position, node.transform.position);
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
