using MechTS.Core;
using MechTS.Economy;
using MechTS.Units;
using UnityEngine;

namespace MechTS.AI
{
    /// <summary>
    /// Drives the Enemy faction's AI, one module per unit type/system — issue #79 ships the
    /// first, <see cref="EnemyEconomyAI"/> (Harvester production and node assignment). Created
    /// by <see cref="Bootstrapper"/> like every other manager singleton. Only ticks while the
    /// Economy Round is active, on a designer-tunable interval rather than every frame — future
    /// staggered modules (Saboteur espionage, Crewman build/repair, combat units) will plug in
    /// as sibling fields alongside <see cref="_economyAI"/>, not by growing this class into one
    /// monolith.
    /// </summary>
    public class EnemyAIController : MonoBehaviour
    {
        private EnemyAIEconomyConfig _economyConfig;
        private EnemyEconomyAI _economyAI;
        private GameManager _gameManager;
        private EconomyManager _economyManager;
        private UnitManager _unitManager;
        private float _decisionTimer;

        /// <summary>
        /// Assigns this mission's AI config. Called by <see cref="Bootstrapper"/> immediately
        /// after creating this manager, since it's instantiated at runtime rather than from a
        /// prefab with an Inspector-assigned reference.
        /// </summary>
        /// <param name="economyConfig">The Enemy faction's economic AI tuning.</param>
        public void Initialize(EnemyAIEconomyConfig economyConfig)
        {
            _economyConfig = economyConfig;
            _economyAI = new EnemyEconomyAI(economyConfig);
        }

        /// <summary>
        /// Caches manager references and subscribes to state changes.
        /// </summary>
        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _economyManager = FindFirstObjectByType<EconomyManager>();
            _unitManager = FindFirstObjectByType<UnitManager>();

            if (_gameManager != null)
            {
                _gameManager.OnGameStateChanged += HandleGameStateChanged;
            }
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
        /// Resets the decision timer on every fresh Economy Round entry, so a leftover partial
        /// timer from a prior round/attempt never carries over.
        /// </summary>
        /// <param name="newState">The state the game just transitioned into.</param>
        private void HandleGameStateChanged(GameState newState)
        {
            if (newState == GameState.EconomyRound)
            {
                _decisionTimer = 0f;
            }
        }

        /// <summary>
        /// Ticks every AI module on a fixed decision interval while the Economy Round is
        /// active — never every frame, and never outside the Economy Round (this controller has
        /// no Battle Round behavior yet).
        /// </summary>
        private void Update()
        {
            if (_gameManager == null || _gameManager.CurrentState != GameState.EconomyRound) return;
            if (_economyConfig == null || _economyAI == null) return;

            _decisionTimer += Time.deltaTime;
            if (_decisionTimer < _economyConfig.decisionIntervalSeconds) return;
            _decisionTimer = 0f;

            _economyAI.Tick(_economyManager, _unitManager);
        }
    }
}
