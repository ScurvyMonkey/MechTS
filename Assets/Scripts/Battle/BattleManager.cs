using MechTS.Core;
using MechTS.Economy;
using MechTS.Units;
using UnityEngine;

namespace MechTS.Battle
{
    /// <summary>
    /// Activates during the Battle Round and continuously evaluates each faction's
    /// elimination status, resolving the round via <see cref="GameManager"/> the instant
    /// one side is eliminated (or both, via the economy tie-break). Also exposes
    /// explicit surrender/objective-met hooks for future AI and mission systems.
    /// Created by <see cref="Bootstrapper"/>.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        private GameManager _gameManager;
        private UnitManager _unitManager;
        private EconomyManager _economyManager;
        private bool _isActive;
        private bool _hasResolved;

        /// <summary>The reason the most recently resolved Battle Round ended, or null if none has resolved yet.</summary>
        public VictoryReason? LastVictoryReason { get; private set; }

        /// <summary>
        /// Caches manager references and subscribes to state changes.
        /// </summary>
        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _unitManager = FindFirstObjectByType<UnitManager>();
            _economyManager = FindFirstObjectByType<EconomyManager>();
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
        /// Starts or stops evaluating victory conditions as the game enters/leaves the Battle Round.
        /// </summary>
        /// <param name="newState">The state the game just transitioned into.</param>
        private void HandleGameStateChanged(GameState newState)
        {
            _isActive = newState == GameState.BattleRound;
            if (_isActive) _hasResolved = false;
        }

        /// <summary>
        /// Evaluates elimination for both factions each frame while the Battle Round is active.
        /// </summary>
        private void Update()
        {
            if (!_isActive || _hasResolved) return;

            var playerReason = VictoryConditionChecker.GetEliminationReason(Faction.Player, _unitManager, _economyManager);
            var enemyReason = VictoryConditionChecker.GetEliminationReason(Faction.Enemy, _unitManager, _economyManager);

            if (playerReason.HasValue && enemyReason.HasValue)
            {
                ResolveTieBreak();
            }
            else if (enemyReason.HasValue)
            {
                Resolve(playerWon: true, enemyReason.Value);
            }
            else if (playerReason.HasValue)
            {
                Resolve(playerWon: false, playerReason.Value);
            }
        }

        /// <summary>
        /// Called by a future enemy-AI system to end the round via surrender.
        /// </summary>
        /// <param name="surrenderingFaction">The faction that surrendered.</param>
        public void TriggerSurrender(Faction surrenderingFaction)
        {
            if (!_isActive || _hasResolved) return;
            Resolve(playerWon: surrenderingFaction != Faction.Player, VictoryReason.Surrender);
        }

        /// <summary>
        /// Called by a future mission system to end the round via a met map objective.
        /// </summary>
        /// <param name="achievingFaction">The faction that met the objective.</param>
        public void TriggerObjectiveMet(Faction achievingFaction)
        {
            if (!_isActive || _hasResolved) return;
            Resolve(playerWon: achievingFaction == Faction.Player, VictoryReason.ObjectiveMet);
        }

        /// <summary>
        /// Resolves a simultaneous mutual defeat by comparing each faction's lifetime
        /// total resources earned over the mission.
        /// </summary>
        private void ResolveTieBreak()
        {
            int playerEarned = _economyManager.GetState(Faction.Player)?.Stockpile.GetTotalEarned() ?? 0;
            int enemyEarned = _economyManager.GetState(Faction.Enemy)?.Stockpile.GetTotalEarned() ?? 0;
            Resolve(playerWon: playerEarned >= enemyEarned, VictoryReason.Tiebreak);
        }

        /// <summary>
        /// Resolves the Battle Round, recording why, then transitioning the game state accordingly.
        /// </summary>
        /// <param name="playerWon">True if the player won; false if the player lost.</param>
        /// <param name="reason">Which condition caused the round to resolve.</param>
        private void Resolve(bool playerWon, VictoryReason reason)
        {
            _hasResolved = true;
            LastVictoryReason = reason;
            if (playerWon)
            {
                _gameManager.CompleteMission();
            }
            else
            {
                _gameManager.TriggerGameOver();
            }
        }
    }
}
