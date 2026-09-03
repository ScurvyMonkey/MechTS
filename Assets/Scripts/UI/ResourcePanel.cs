using MechTS.Core;
using MechTS.Economy;
using MechTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MechTS.UI
{
    /// <summary>
    /// Top-left readout of the player's resource stockpile, power usage, and Economy
    /// Round countdown timer.
    /// </summary>
    public class ResourcePanel : MonoBehaviour
    {
        private GameManager _gameManager;
        private EconomyManager _economyManager;
        private Text _resourceText;
        private Text _powerText;
        private Text _timerText;

        /// <summary>
        /// Builds the panel's background and text elements.
        /// </summary>
        private void Awake()
        {
            UIFactory.CreatePanel(transform, "Background", UIFactory.TopLeft, new Vector2(10f, -10f), new Vector2(260f, 90f));
            _resourceText = UIFactory.CreateText(transform, "Resources", UIFactory.TopLeft, new Vector2(18f, -16f), new Vector2(244f, 24f));
            _powerText = UIFactory.CreateText(transform, "Power", UIFactory.TopLeft, new Vector2(18f, -40f), new Vector2(244f, 24f));
            _timerText = UIFactory.CreateText(transform, "Timer", UIFactory.TopLeft, new Vector2(18f, -64f), new Vector2(244f, 24f));
        }

        /// <summary>
        /// Caches manager references.
        /// </summary>
        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _economyManager = FindFirstObjectByType<EconomyManager>();
        }

        /// <summary>
        /// Refreshes the resource, power, and timer readouts from live manager state. The
        /// resource readout already reflects continuous unit upkeep (issue #48) for free —
        /// it reads the stockpile's live amount every frame, including negative values from
        /// running an upkeep deficit — but is tinted red whenever any resource is negative,
        /// so an all-in strategy's cost stays legible at a glance rather than just a number
        /// that happens to have a minus sign.
        /// </summary>
        private void Update()
        {
            var state = _economyManager != null ? _economyManager.GetState(Faction.Player) : null;
            if (state == null) return;

            int ore = state.Stockpile.GetAmount(ResourceType.Ore);
            int biomass = state.Stockpile.GetAmount(ResourceType.Biomass);
            int gold = state.Stockpile.GetAmount(ResourceType.Gold);

            _resourceText.text = $"Ore {ore}   Biomass {biomass}   Gold {gold}";
            _resourceText.color = (ore < 0 || biomass < 0 || gold < 0) ? Color.red : Color.white;
            _powerText.text = $"Power {state.PowerUsed}/{state.PowerCapacity}";

            bool isEconomyRound = _gameManager != null && _gameManager.CurrentState == GameState.EconomyRound;
            _timerText.text = isEconomyRound ? $"Economy Round: {Mathf.Max(0f, _economyManager.RoundTimeRemaining):0}s" : string.Empty;
        }
    }
}
