using MechTS.Core;
using MechTS.Economy;
using MechTS.Units;
using UnityEngine;

namespace MechTS.Utilities
{
    /// <summary>
    /// Debug-only OnGUI overlay showing round state, both factions' economy, and the
    /// current selection. Not the real game HUD — that's future UI/ work with real
    /// art and layout. Exists purely to make manually testing issues #1-6 practical.
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        private GameManager _gameManager;
        private EconomyManager _economyManager;
        private UnitManager _unitManager;

        /// <summary>
        /// Caches manager references.
        /// </summary>
        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _economyManager = FindFirstObjectByType<EconomyManager>();
            _unitManager = FindFirstObjectByType<UnitManager>();
        }

        /// <summary>
        /// Draws the side panel and, if the mission has resolved, a centered banner.
        /// </summary>
        private void OnGUI()
        {
            DrawSidePanel();
            DrawRoundBanner();
        }

        /// <summary>
        /// Draws the top-left panel: round state, each faction's economy, and the current selection.
        /// </summary>
        private void DrawSidePanel()
        {
            GUILayout.BeginArea(new Rect(10, 10, 340, 460));
            GUILayout.BeginVertical("box");

            GUILayout.Label("State: " + (_gameManager != null ? _gameManager.CurrentState.ToString() : "N/A"));
            GUILayout.Space(6);

            DrawFactionEconomy("PLAYER (blue)", Faction.Player);
            GUILayout.Space(6);
            DrawFactionEconomy("ENEMY (red)", Faction.Enemy);
            GUILayout.Space(6);

            DrawSelection();
            GUILayout.Space(6);

            GUILayout.Label("Hotkeys: B/N select building then click to place");
            GUILayout.Label("S stop | Ctrl+0-9 set group | 0-9 recall group");

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Draws one faction's resource stockpile, power usage, and building/unit counts.
        /// </summary>
        /// <param name="label">The heading to show for this faction.</param>
        /// <param name="faction">The faction to display.</param>
        private void DrawFactionEconomy(string label, Faction faction)
        {
            var state = _economyManager != null ? _economyManager.GetState(faction) : null;
            GUILayout.Label(label);

            if (state == null)
            {
                GUILayout.Label("  (no state yet)");
                return;
            }

            GUILayout.Label($"  Ore {state.Stockpile.GetAmount(ResourceType.Ore)}  Biomass {state.Stockpile.GetAmount(ResourceType.Biomass)}  Gold {state.Stockpile.GetAmount(ResourceType.Gold)}");
            int unitCount = _unitManager != null ? _unitManager.CountByFaction(faction) : 0;
            GUILayout.Label($"  Power {state.PowerUsed}/{state.PowerCapacity}   Buildings {state.Buildings.Count}   Units {unitCount}");
        }

        /// <summary>
        /// Draws the currently selected units and their health.
        /// </summary>
        private void DrawSelection()
        {
            if (_unitManager == null || _unitManager.SelectedUnits.Count == 0)
            {
                GUILayout.Label("Selection: none");
                return;
            }

            GUILayout.Label($"Selection: {_unitManager.SelectedUnits.Count} unit(s)");
            foreach (var unit in _unitManager.SelectedUnits)
            {
                var health = unit.HealthComponent;
                string hp = health != null ? $"{health.CurrentHealth:0}/{health.MaxHealth:0}" : "n/a";
                GUILayout.Label($"  {unit.name} [{unit.Faction}] HP {hp}");
            }
        }

        /// <summary>
        /// Draws a large centered banner when the mission has resolved.
        /// </summary>
        private void DrawRoundBanner()
        {
            if (_gameManager == null) return;

            string message = null;
            if (_gameManager.CurrentState == GameState.MissionComplete) message = "MISSION COMPLETE";
            else if (_gameManager.CurrentState == GameState.GameOver) message = "GAME OVER";
            if (message == null) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(0, Screen.height / 2f - 40f, Screen.width, 80f), message, style);
        }
    }
}
