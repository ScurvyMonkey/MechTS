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
    /// Toggleable (not always-visible) Injection Round panel — opens/closes on the <c>I</c>
    /// hotkey, same shape as <see cref="TechPanel"/>, reachable only during the Battle Round
    /// (issue #54 — a Battle-Round tactical pivot funded by resources set aside during the
    /// Economy Round, corrected from #53's original Economy-Round framing). Lists the exact
    /// same tech tree as <see cref="TechPanel"/> (one shared <see cref="TechManager"/>
    /// pipeline, one `_researched` set — an upgrade researched here can't be researched again
    /// there, and vice versa), but every entry's affordability/spend here draws from the
    /// faction's locked pool (<see cref="Economy.LockedResourcePool"/>, #52) instead of its
    /// normal stockpile. Already-researched, swappable upgrades (issue #54 —
    /// <see cref="UpgradeDefinition.isSwappable"/>) also get a free "Revert" option, letting
    /// the player pivot away from a choice that turned out wrong (e.g. a weapon-type
    /// upgrade the enemy resists) before injecting a replacement.
    /// </summary>
    public class InjectionPanel : MonoBehaviour
    {
        private const float EntryHeight = 26f;
        private const float PanelWidth = 420f;

        private GameManager _gameManager;
        private TechManager _techManager;
        private EconomyManager _economyManager;
        private RectTransform _panelRoot;
        private Text _statusText;
        private Text _poolText;
        private readonly List<UpgradeDefinition> _entries = new List<UpgradeDefinition>();
        private readonly List<Button> _entryButtons = new List<Button>();
        private readonly List<Text> _entryLabels = new List<Text>();
        private readonly List<Button> _revertButtons = new List<Button>();
        private bool _isOpen;

        /// <summary>
        /// Caches manager references and builds the (initially closed) injection panel.
        /// </summary>
        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _techManager = FindFirstObjectByType<TechManager>();
            _economyManager = FindFirstObjectByType<EconomyManager>();

            BuildPanel();
            SetOpen(false);
        }

        /// <summary>
        /// Toggles the panel open/closed on the <c>I</c> hotkey and refreshes entry state
        /// while open. Only reachable during the Battle Round (issue #54) — this is a
        /// tactical pivot funded by resources set aside during the Economy Round, not an
        /// everyday research option, so it deliberately does not use the shared
        /// <see cref="GameManager.IsRoundActive"/> gate every other panel uses.
        /// </summary>
        private void Update()
        {
            if (_gameManager == null || _gameManager.CurrentState != GameState.BattleRound) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.iKey.wasPressedThisFrame)
            {
                SetOpen(!_isOpen);
            }

            if (_isOpen) RefreshEntries();
        }

        /// <summary>
        /// Shows or hides the panel's visual content. This component's own GameObject stays
        /// active throughout so it can keep listening for the toggle key.
        /// </summary>
        /// <param name="open">Whether the panel should be visible.</param>
        private void SetOpen(bool open)
        {
            _isOpen = open;
            if (_panelRoot != null) _panelRoot.gameObject.SetActive(open);
            if (open) RefreshEntries();
        }

        /// <summary>
        /// Builds the panel background, pool readout, and one entry (label + research
        /// button) per upgrade in the player's faction's tree, grouped by tier with a header
        /// per tier — same layout as <see cref="TechPanel"/>.
        /// </summary>
        private void BuildPanel()
        {
            var tree = _techManager != null ? _techManager.GetTree(Faction.Player) : null;
            var sorted = tree != null ? tree.OrderBy(u => u.tier).ToList() : new List<UpgradeDefinition>();

            float panelHeight = 40f + EntryHeight + EntryHeight + sorted.Count * EntryHeight + CountDistinctTiers(sorted) * EntryHeight;
            _panelRoot = UIFactory.CreatePanel(transform, "Background", UIFactory.Center, Vector2.zero, new Vector2(PanelWidth, panelHeight));

            _statusText = UIFactory.CreateText(_panelRoot, "Status", UIFactory.TopLeft, new Vector2(10f, -10f), new Vector2(PanelWidth - 20f, EntryHeight), 13);
            _statusText.color = Color.yellow;
            _poolText = UIFactory.CreateText(_panelRoot, "Pool", UIFactory.TopLeft, new Vector2(10f, -10f - EntryHeight), new Vector2(PanelWidth - 20f, EntryHeight), 13);

            float y = panelHeight - 30f - EntryHeight - EntryHeight;
            int lastTier = int.MinValue;
            foreach (var upgrade in sorted)
            {
                if (upgrade.tier != lastTier)
                {
                    lastTier = upgrade.tier;
                    var header = UIFactory.CreateText(_panelRoot, $"Tier{lastTier}_Header", UIFactory.TopLeft, new Vector2(10f, -(panelHeight - y)), new Vector2(PanelWidth - 20f, EntryHeight));
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
        /// Builds one upgrade's label, pool-funded "Inject" button, and — for a swappable
        /// upgrade — a free "Revert" button, only ever interactable once that upgrade is
        /// actually researched.
        /// </summary>
        /// <param name="upgrade">The upgrade to build an entry for.</param>
        /// <param name="panelHeight">The panel's total height, for top-anchored positioning.</param>
        /// <param name="y">The current vertical cursor from the panel's top, advanced by this entry's height.</param>
        private void BuildEntry(UpgradeDefinition upgrade, float panelHeight, ref float y)
        {
            var label = UIFactory.CreateText(_panelRoot, $"{upgrade.name}_Label", UIFactory.TopLeft, new Vector2(20f, -(panelHeight - y)), new Vector2(180f, EntryHeight), 12);
            _entries.Add(upgrade);
            _entryLabels.Add(label);

            var injectButton = UIFactory.CreateButton(_panelRoot, $"{upgrade.name}_Inject", "Inject",
                UIFactory.TopLeft, new Vector2(210f, -(panelHeight - y)), new Vector2(90f, EntryHeight - 4f),
                () => _techManager.TryResearch(Faction.Player, upgrade, useLockedPool: true));
            _entryButtons.Add(injectButton);

            var revertButton = UIFactory.CreateButton(_panelRoot, $"{upgrade.name}_Revert", "Revert",
                UIFactory.TopLeft, new Vector2(310f, -(panelHeight - y)), new Vector2(90f, EntryHeight - 4f),
                () => _techManager.TryRevert(Faction.Player, upgrade));
            revertButton.gameObject.SetActive(upgrade.isSwappable);
            _revertButtons.Add(revertButton);

            y -= EntryHeight;
        }

        /// <summary>
        /// Refreshes the pool readout and each entry's label text (researched/cost) and
        /// button interactability (affordable from the locked pool + prerequisites met, not
        /// already researched) from live state.
        /// </summary>
        private void RefreshEntries()
        {
            var state = _economyManager != null ? _economyManager.GetState(Faction.Player) : null;
            bool hasResearchBuilding = _techManager != null && _techManager.HasResearchBuilding(Faction.Player);
            _statusText.text = hasResearchBuilding
                ? "Research enabled (Technology Plant owned) — funded from your locked pool"
                : "Research locked — build a Technology Plant to unlock spending on upgrades below.";

            if (state != null)
            {
                _poolText.text = $"Pool: Ore {state.LockedPool.GetAmount(ResourceType.Ore)}   " +
                                  $"Biomass {state.LockedPool.GetAmount(ResourceType.Biomass)}   " +
                                  $"Gold {state.LockedPool.GetAmount(ResourceType.Gold)}";
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                var upgrade = _entries[i];
                bool researched = _techManager.IsResearched(Faction.Player, upgrade);

                _entryLabels[i].text = researched
                    ? $"{upgrade.displayName} (Researched)"
                    : $"{upgrade.displayName} — Ore {upgrade.oreCost} Biomass {upgrade.biomassCost} Gold {upgrade.goldCost}";

                _entryButtons[i].interactable = !researched && _techManager.CanResearch(Faction.Player, upgrade, useLockedPool: true);
                _entryButtons[i].GetComponentInChildren<Text>().text = researched ? "Done" : "Inject";

                if (upgrade.isSwappable) _revertButtons[i].interactable = researched;
            }
        }
    }
}
