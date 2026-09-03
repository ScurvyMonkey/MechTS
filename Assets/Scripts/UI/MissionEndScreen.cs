using MechTS.Battle;
using MechTS.Core;
using MechTS.Economy;
using MechTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MechTS.UI
{
    /// <summary>
    /// Victory/defeat summary shown when the mission resolves (MissionComplete or
    /// GameOver), reporting which condition ended the round and each faction's
    /// lifetime resources earned. Blocks the underlying HUD, which hides itself on
    /// this same state transition, and unit-command input, which checks
    /// <see cref="GameManager.IsRoundActive"/> and so already stops responding once
    /// the round is no longer Economy/Battle.
    /// </summary>
    public class MissionEndScreen : MonoBehaviour
    {
        private GameManager _gameManager;
        private EconomyManager _economyManager;
        private BattleManager _battleManager;
        private Text _titleText;
        private Text _reasonText;
        private Text _statsText;

        /// <summary>
        /// Builds the screen's background, text elements, and return button. Deliberately
        /// does NOT deactivate the GameObject here — Unity never calls Start() on a
        /// component whose GameObject was deactivated before its first active frame, which
        /// would permanently prevent this screen from ever subscribing to
        /// <see cref="GameManager.OnGameStateChanged"/> and therefore never able to
        /// reactivate itself. Hiding happens at the end of Start() instead, after subscribing.
        /// </summary>
        private void Awake()
        {
            var background = UIFactory.CreatePanel(transform, "Background", Vector2.zero, Vector2.zero, Vector2.zero);
            var backgroundRect = background;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

            _titleText = UIFactory.CreateText(transform, "Title", new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(600f, 60f), 36);
            _titleText.alignment = TextAnchor.MiddleCenter;

            _reasonText = UIFactory.CreateText(transform, "Reason", new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(600f, 30f), 18);
            _reasonText.alignment = TextAnchor.MiddleCenter;

            _statsText = UIFactory.CreateText(transform, "Stats", new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(600f, 40f), 16);
            _statsText.alignment = TextAnchor.MiddleCenter;

            UIFactory.CreateButton(transform, "ReturnButton", "Return to Main Menu", new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(220f, 40f), HandleReturnClicked);
        }

        /// <summary>
        /// Caches manager references, subscribes to state changes, then hides the screen
        /// until the mission actually resolves.
        /// </summary>
        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _economyManager = FindFirstObjectByType<EconomyManager>();
            _battleManager = FindFirstObjectByType<BattleManager>();
            _gameManager.OnGameStateChanged += HandleGameStateChanged;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Unsubscribes from the GameManager.
        /// </summary>
        private void OnDestroy()
        {
            if (_gameManager != null) _gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        /// <summary>
        /// Shows and populates the screen on MissionComplete/GameOver; hides it otherwise.
        /// </summary>
        /// <param name="state">The state the game just transitioned into.</param>
        private void HandleGameStateChanged(GameState state)
        {
            bool resolved = state == GameState.MissionComplete || state == GameState.GameOver;
            gameObject.SetActive(resolved);
            if (resolved) Populate(state);
        }

        /// <summary>
        /// Fills in the title, resolution reason, and each faction's lifetime resource totals.
        /// </summary>
        /// <param name="state">The resolved state (MissionComplete or GameOver).</param>
        private void Populate(GameState state)
        {
            _titleText.text = state == GameState.MissionComplete ? "MISSION COMPLETE" : "GAME OVER";
            _reasonText.text = DescribeReason(_battleManager != null ? _battleManager.LastVictoryReason : null);

            int playerEarned = _economyManager != null ? _economyManager.GetState(Faction.Player)?.Stockpile.GetTotalEarned() ?? 0 : 0;
            int enemyEarned = _economyManager != null ? _economyManager.GetState(Faction.Enemy)?.Stockpile.GetTotalEarned() ?? 0 : 0;
            _statsText.text = $"Lifetime resources earned — Player: {playerEarned}   Enemy: {enemyEarned}";
        }

        /// <summary>
        /// Returns a player-facing description of why the round resolved.
        /// </summary>
        /// <param name="reason">The recorded resolution reason, if any.</param>
        private string DescribeReason(VictoryReason? reason)
        {
            switch (reason)
            {
                case VictoryReason.Wipeout: return "A faction was completely wiped out.";
                case VictoryReason.EconomyExhausted: return "A faction's economy was exhausted.";
                case VictoryReason.Surrender: return "A faction surrendered.";
                case VictoryReason.ObjectiveMet: return "The mission objective was met.";
                case VictoryReason.Tiebreak: return "Both factions fell simultaneously — resolved by lifetime resources earned.";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// Returns to the Main Menu.
        /// </summary>
        private void HandleReturnClicked()
        {
            _gameManager.ReturnToMainMenu();
        }
    }
}
