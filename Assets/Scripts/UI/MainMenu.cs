using MechTS.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MechTS.UI
{
    /// <summary>
    /// The game's entry-point screen: title, faction select (Humans only in Phase 1),
    /// New Campaign, a stubbed Continue (save/load isn't implemented yet), and Quit.
    /// Visible only while <see cref="GameManager.CurrentState"/> is MainMenu.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        private GameManager _gameManager;

        /// <summary>
        /// Builds the screen's background, title, faction indicator, and buttons.
        /// </summary>
        private void Awake()
        {
            var background = UIFactory.CreatePanel(transform, "Background", Vector2.zero, Vector2.zero, Vector2.zero);
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            var title = UIFactory.CreateText(transform, "Title", new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(600f, 60f), 40);
            title.alignment = TextAnchor.MiddleCenter;
            title.text = "MechTS";

            var faction = UIFactory.CreateText(transform, "Faction", new Vector2(0.5f, 0.5f), new Vector2(0f, 100f), new Vector2(600f, 30f), 18);
            faction.alignment = TextAnchor.MiddleCenter;
            faction.text = "Faction: Humans";

            UIFactory.CreateButton(transform, "NewCampaignButton", "New Campaign", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(220f, 40f), HandleNewCampaignClicked);

            var continueButton = UIFactory.CreateButton(transform, "ContinueButton", "Continue", new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(220f, 40f), null);
            continueButton.interactable = false;

            var continueNote = UIFactory.CreateText(transform, "ContinueNote", new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(400f, 24f), 12);
            continueNote.alignment = TextAnchor.MiddleCenter;
            continueNote.text = "(save/load not implemented yet)";

            UIFactory.CreateButton(transform, "QuitButton", "Quit", new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(220f, 40f), HandleQuitClicked);
        }

        /// <summary>
        /// Caches the GameManager, subscribes to state changes, and applies the current state immediately.
        /// </summary>
        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _gameManager.OnGameStateChanged += HandleGameStateChanged;
            HandleGameStateChanged(_gameManager.CurrentState);
        }

        /// <summary>
        /// Unsubscribes from the GameManager.
        /// </summary>
        private void OnDestroy()
        {
            if (_gameManager != null) _gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        /// <summary>
        /// Shows this screen only while the game is at the Main Menu.
        /// </summary>
        /// <param name="state">The state the game just transitioned into.</param>
        private void HandleGameStateChanged(GameState state)
        {
            gameObject.SetActive(state == GameState.MainMenu);
        }

        /// <summary>
        /// Starts the Economy Round. Mission/campaign structure isn't decided yet, so this
        /// starts a round in whatever scene is currently loaded rather than loading a
        /// specific mission scene.
        /// </summary>
        private void HandleNewCampaignClicked()
        {
            _gameManager.StartEconomyRound();
        }

        /// <summary>
        /// Quits the application (no-op in the Editor outside of exiting Play Mode).
        /// </summary>
        private void HandleQuitClicked()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
