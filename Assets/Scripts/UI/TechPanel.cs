using System.Collections.Generic;
using System.Linq;
using MechTS.Core;
using MechTS.Economy;
using MechTS.Units;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MechTS.UI
{
    /// <summary>
    /// Toggleable (not always-visible, unlike the HUD's other panels) tech-tree browser —
    /// opens/closes on the <c>T</c> hotkey. Lists the player's faction's own tree, grouped
    /// by tier, with a research button per upgrade. Entries are built once (the tree's
    /// upgrade set is fixed for the mission, unlike units/buildings), then refreshed each
    /// frame while open to reflect current researched/affordable state.
    /// </summary>
    public class TechPanel : MonoBehaviour
    {
        private const float EntryHeight = 26f;
        private const float PanelWidth = 420f;

        private GameManager _gameManager;
        private TechManager _techManager;
        private RectTransform _panelRoot;
        private Text _statusText;
        private readonly List<UpgradeDefinition> _entries = new List<UpgradeDefinition>();
        private readonly List<Button> _entryButtons = new List<Button>();
        private readonly List<Text> _entryLabels = new List<Text>();
        private bool _isOpen;

        /// <summary>
        /// Caches manager references and builds the (initially closed) tech tree panel.
        /// </summary>
        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _techManager = FindFirstObjectByType<TechManager>();

            BuildPanel();
            SetOpen(false);
        }

        /// <summary>
        /// Toggles the panel open/closed on the <c>T</c> hotkey and refreshes entry state
        /// while open. Ignored outside an active round, matching every other input system's
        /// gating convention.
        /// </summary>
        private void Update()
        {
            if (_gameManager == null || !_gameManager.IsRoundActive) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
            {
                SetOpen(!_isOpen);
            }

            if (_isOpen) RefreshEntries();
        }

        /// <summary>
        /// Shows or hides the panel's visual content. The <see cref="TechPanel"/>'s own
        /// GameObject stays active throughout so it can keep listening for the toggle key.
        /// </summary>
        /// <param name="open">Whether the panel should be visible.</param>
        private void SetOpen(bool open)
        {
            _isOpen = open;
            if (_panelRoot != null) _panelRoot.gameObject.SetActive(open);
            if (open) RefreshEntries();
        }

        /// <summary>
        /// Builds the panel background and one entry (label + research button) per upgrade
        /// in the player's faction's tree, grouped by tier with a header per tier.
        /// </summary>
        private void BuildPanel()
        {
            var tree = _techManager != null ? _techManager.GetTree(Faction.Player) : null;
            var sorted = tree != null ? tree.OrderBy(u => u.tier).ToList() : new List<UpgradeDefinition>();

            float panelHeight = 40f + EntryHeight + sorted.Count * EntryHeight + CountDistinctTiers(sorted) * EntryHeight;
            _panelRoot = UIFactory.CreatePanel(transform, "Background", UIFactory.Center, Vector2.zero, new Vector2(PanelWidth, panelHeight));

            _statusText = UIFactory.CreateText(_panelRoot, "Status", UIFactory.TopLeft, new Vector2(10f, -10f), new Vector2(PanelWidth - 20f, EntryHeight), 13);
            _statusText.color = Color.yellow;

            float y = panelHeight - 30f - EntryHeight;
            int lastTier = int.MinValue;
            foreach (var upgrade in sorted)
            {
                if (upgrade.tier != lastTier)
                {
                    lastTier = upgrade.tier;
                    var header = UIFactory.CreateText(_panelRoot, $"Tier{lastTier}_Header", UIFactory.TopLeft, new Vector2(10f, -( panelHeight - y)), new Vector2(PanelWidth - 20f, EntryHeight));
                    header.text = $"Tier {lastTier}";
                    header.fontSize = 16;
                    y -= EntryHeight;
                }

                BuildEntry(upgrade, panelHeight, ref y);
            }
        }

        /// <summary>
        /// Returns how many distinct tiers appear in the given sorted upgrade list, so the
        /// panel can size itself to fit one header row per tier.
        /// </summary>
        /// <param name="sorted">The upgrade list, already sorted by tier.</param>
        private int CountDistinctTiers(List<UpgradeDefinition> sorted)
        {
            int count = 0;
            int lastTier = int.MinValue;
            foreach (var upgrade in sorted)
            {
                if (upgrade.tier != lastTier)
                {
                    lastTier = upgrade.tier;
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Builds one upgrade's label and research button.
        /// </summary>
        /// <param name="upgrade">The upgrade to build an entry for.</param>
        /// <param name="panelHeight">The panel's total height, for top-anchored positioning.</param>
        /// <param name="y">The current vertical cursor from the panel's top, advanced by this entry's height.</param>
        private void BuildEntry(UpgradeDefinition upgrade, float panelHeight, ref float y)
        {
            var label = UIFactory.CreateText(_panelRoot, $"{upgrade.name}_Label", UIFactory.TopLeft, new Vector2(20f, -(panelHeight - y)), new Vector2(260f, EntryHeight), 12);
            _entries.Add(upgrade);
            _entryLabels.Add(label);

            var button = UIFactory.CreateButton(_panelRoot, $"{upgrade.name}_Button", "Research",
                UIFactory.TopLeft, new Vector2(300f, -(panelHeight - y)), new Vector2(100f, EntryHeight - 4f),
                () => _techManager.TryResearch(Faction.Player, upgrade));
            _entryButtons.Add(button);

            y -= EntryHeight;
        }

        /// <summary>
        /// Updates each entry's label text (researched/cost) and button interactability
        /// (affordable + prerequisites met, not already researched) from live state.
        /// </summary>
        private void RefreshEntries()
        {
            bool hasResearchBuilding = _techManager != null && _techManager.HasResearchBuilding(Faction.Player);
            _statusText.text = hasResearchBuilding
                ? "Research enabled (Technology Plant owned)"
                : "Research locked — build a Technology Plant to unlock spending on upgrades below.";

            for (int i = 0; i < _entries.Count; i++)
            {
                var upgrade = _entries[i];
                bool researched = _techManager.IsResearched(Faction.Player, upgrade);

                _entryLabels[i].text = researched
                    ? $"{upgrade.displayName} (Researched)"
                    : $"{upgrade.displayName} — Ore {upgrade.oreCost} Biomass {upgrade.biomassCost} Gold {upgrade.goldCost}";

                _entryButtons[i].interactable = !researched && _techManager.CanResearch(Faction.Player, upgrade);
                _entryButtons[i].GetComponentInChildren<Text>().text = researched ? "Done" : "Research";
            }
        }
    }
}
