using MechTS.Core;
using UnityEngine;

namespace MechTS.UI
{
    /// <summary>
    /// Top-level HUD container: visible only during the Economy and Battle Rounds, and
    /// hosts the resource, selection, build-menu, production-menu, minimap, tech,
    /// resource-allocation, and injection panels as children.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        private GameManager _gameManager;

        /// <summary>
        /// Creates the child panel GameObjects, each stretched to fill the HUD area so
        /// their own content can anchor to whichever screen corner they need.
        /// </summary>
        private void Awake()
        {
            CreatePanel<ResourcePanel>("ResourcePanel");
            CreatePanel<SelectionPanel>("SelectionPanel");
            CreatePanel<BuildMenuPanel>("BuildMenuPanel");
            CreatePanel<ProductionMenuPanel>("ProductionMenuPanel");
            CreatePanel<MinimapPanel>("MinimapPanel");
            CreatePanel<TechPanel>("TechPanel");
            CreatePanel<ResourceAllocationPanel>("ResourceAllocationPanel");
            CreatePanel<InjectionPanel>("InjectionPanel");
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
        /// Shows the HUD only during the Economy Round and Battle Round.
        /// </summary>
        /// <param name="state">The state the game just transitioned into.</param>
        private void HandleGameStateChanged(GameState state)
        {
            gameObject.SetActive(state == GameState.EconomyRound || state == GameState.BattleRound);
        }

        /// <summary>
        /// Creates a full-stretch child GameObject under the HUD with the given panel component attached.
        /// </summary>
        private T CreatePanel<T>(string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return go.AddComponent<T>();
        }
    }
}
