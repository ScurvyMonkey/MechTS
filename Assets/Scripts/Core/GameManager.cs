using System;
using UnityEngine;

namespace MechTS.Core
{
    /// <summary>
    /// Tracks and transitions between the game's high-level round states
    /// (MainMenu, EconomyRound, BattleRound, MissionComplete, GameOver).
    /// Created exclusively by <see cref="Bootstrapper"/>.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;

        /// <summary>
        /// Raised whenever the game successfully transitions to a new <see cref="GameState"/>.
        /// </summary>
        public event Action<GameState> OnGameStateChanged;

        /// <summary>
        /// The game's current high-level state.
        /// </summary>
        public GameState CurrentState { get; private set; } = GameState.MainMenu;

        /// <summary>Whether a round (Economy or Battle) is currently active — gameplay unit input is only valid while this is true.</summary>
        public bool IsRoundActive => CurrentState == GameState.EconomyRound || CurrentState == GameState.BattleRound;

        /// <summary>
        /// Destroys duplicate instances so only one <see cref="GameManager"/> persists across scene loads.
        /// </summary>
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        /// <summary>
        /// Clears the static instance reference if this was the surviving instance.
        /// </summary>
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Transitions from MainMenu, or from a completed/failed mission, into the Economy Round.
        /// </summary>
        public void StartEconomyRound()
        {
            TryTransition(GameState.EconomyRound, GameState.MainMenu, GameState.MissionComplete);
        }

        /// <summary>
        /// Transitions from the Economy Round into the Battle Round.
        /// </summary>
        public void StartBattleRound()
        {
            TryTransition(GameState.BattleRound, GameState.EconomyRound);
        }

        /// <summary>
        /// Transitions from the Battle Round into MissionComplete on a player win.
        /// </summary>
        public void CompleteMission()
        {
            TryTransition(GameState.MissionComplete, GameState.BattleRound);
        }

        /// <summary>
        /// Transitions from the Battle Round into GameOver on a player loss.
        /// </summary>
        public void TriggerGameOver()
        {
            TryTransition(GameState.GameOver, GameState.BattleRound);
        }

        /// <summary>
        /// Returns to the main menu from MissionComplete or GameOver.
        /// </summary>
        public void ReturnToMainMenu()
        {
            TryTransition(GameState.MainMenu, GameState.MissionComplete, GameState.GameOver);
        }

        /// <summary>
        /// Applies a state transition if the current state is one of the allowed origin states;
        /// otherwise logs a warning and leaves the state unchanged.
        /// </summary>
        /// <param name="target">The state to transition into.</param>
        /// <param name="allowedFrom">The states this transition is legal from.</param>
        private void TryTransition(GameState target, params GameState[] allowedFrom)
        {
            foreach (var from in allowedFrom)
            {
                if (CurrentState == from)
                {
                    CurrentState = target;
                    OnGameStateChanged?.Invoke(target);
                    return;
                }
            }

            Debug.LogWarning($"GameManager: rejected illegal transition {CurrentState} -> {target}.");
        }

        [ContextMenu("Debug/Start Economy Round")]
        private void DebugStartEconomyRound() => StartEconomyRound();

        [ContextMenu("Debug/Start Battle Round")]
        private void DebugStartBattleRound() => StartBattleRound();

        [ContextMenu("Debug/Complete Mission")]
        private void DebugCompleteMission() => CompleteMission();

        [ContextMenu("Debug/Trigger Game Over")]
        private void DebugTriggerGameOver() => TriggerGameOver();

        [ContextMenu("Debug/Return To Main Menu")]
        private void DebugReturnToMainMenu() => ReturnToMainMenu();
    }
}
