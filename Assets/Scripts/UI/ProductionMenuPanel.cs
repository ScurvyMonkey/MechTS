using System.Collections.Generic;
using MechTS.Economy;
using MechTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MechTS.UI
{
    /// <summary>
    /// Bottom-left menu listing each of the player's production hosts — production-capable
    /// buildings, plus the player's Main Building if it has any producible units configured
    /// (issue #41, e.g. the Harvester) — with buttons to queue that host's producible units
    /// and a readout of its current queue/progress. Rebuilds whenever the tracked host set
    /// changes. Docked directly above <see cref="BuildMenuPanel"/> in the same bottom-left corner (see
    /// <see cref="BuildMenuPanel.PanelHeight"/>) — stacked above rather than below it
    /// because <see cref="BuildMenuPanel"/>'s height is fixed for the whole mission while
    /// this panel's height changes as production buildings are built/lost, so only this
    /// panel ever needs to account for the other's size.
    /// </summary>
    public class ProductionMenuPanel : MonoBehaviour
    {
        private const float EntryHeight = 30f;
        private const float BottomMargin = 10f;
        private const float SectionSpacing = 10f;

        private EconomyManager _economyManager;
        private TechManager _techManager;
        private BuildMenuPanel _buildMenuPanel;
        private readonly List<MonoBehaviour> _trackedHosts = new List<MonoBehaviour>();
        private readonly List<ProductionQueue> _queues = new List<ProductionQueue>();
        private readonly List<Text> _progressTexts = new List<Text>();
        private readonly List<UnitButtonEntry> _unitButtons = new List<UnitButtonEntry>();

        /// <summary>
        /// Tracks one producible-unit button so its locked/unlocked state can be refreshed
        /// every frame without rebuilding the whole panel.
        /// </summary>
        private struct UnitButtonEntry
        {
            public Button Button;
            public Text Label;
            public UnitProductionDefinition Definition;
            public Faction Faction;
        }

        /// <summary>
        /// Caches the EconomyManager, TechManager, and BuildMenuPanel.
        /// </summary>
        private void Start()
        {
            _economyManager = FindFirstObjectByType<EconomyManager>();
            _techManager = FindFirstObjectByType<TechManager>();
            _buildMenuPanel = FindFirstObjectByType<BuildMenuPanel>();
        }

        /// <summary>
        /// The screen-Y offset this panel's content starts from — directly above
        /// <see cref="BuildMenuPanel"/>'s own section, so the two never overlap regardless
        /// of either panel's current height.
        /// </summary>
        private float BaseY => BottomMargin + (_buildMenuPanel != null ? _buildMenuPanel.PanelHeight + SectionSpacing : 0f);

        /// <summary>
        /// Rebuilds the panel if the set of production hosts (production buildings, plus the
        /// player's Main Building if it has any producible units — issue #41) changed;
        /// otherwise just refreshes each entry's queue/progress readout.
        /// </summary>
        private void Update()
        {
            if (_economyManager == null) return;

            var state = _economyManager.GetState(Faction.Player);
            if (state == null) return;

            var current = new List<MonoBehaviour>();
            foreach (var building in state.Buildings)
            {
                if (building.CanProduceUnits) current.Add(building);
            }

            if (_economyManager.MainBuildings.TryGetValue(Faction.Player, out var mainBuilding)
                && mainBuilding != null && mainBuilding.ProducibleUnits != null && mainBuilding.ProducibleUnits.Length > 0)
            {
                current.Add(mainBuilding);
            }

            if (!SameHosts(current))
            {
                Rebuild(current);
            }

            RefreshProgress();
            RefreshUnitLocks();
        }

        /// <summary>
        /// Returns whether the given host list matches what's currently tracked.
        /// </summary>
        /// <param name="current">The current set of production hosts.</param>
        private bool SameHosts(List<MonoBehaviour> current)
        {
            if (current.Count != _trackedHosts.Count) return false;
            for (int i = 0; i < current.Count; i++)
            {
                if (current[i] != _trackedHosts[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// Destroys and recreates every entry for the given set of production hosts.
        /// </summary>
        /// <param name="hosts">The production hosts (buildings and/or the Main Building) to display.</param>
        private void Rebuild(List<MonoBehaviour> hosts)
        {
            foreach (Transform child in transform) Destroy(child.gameObject);

            _trackedHosts.Clear();
            _trackedHosts.AddRange(hosts);
            _queues.Clear();
            _progressTexts.Clear();
            _unitButtons.Clear();

            float panelHeight = 20f + hosts.Count * EntryHeight * 2f;
            float baseY = BaseY;
            UIFactory.CreatePanel(transform, "Background", UIFactory.BottomLeft, new Vector2(10f, baseY), new Vector2(260f, panelHeight));

            float y = baseY + panelHeight - EntryHeight;
            foreach (var host in hosts)
            {
                if (host is BuildingInstance building)
                {
                    BuildEntry(building.name, building.Definition.displayName, building, building.Faction, building.Definition.producibleUnits, ref y);
                }
                else if (host is MainBuilding mainBuilding)
                {
                    BuildEntry(mainBuilding.name, "Main Building", mainBuilding, mainBuilding.Faction, mainBuilding.ProducibleUnits, ref y);
                }
            }
        }

        /// <summary>
        /// Builds one production host's label, unit buttons, and progress text.
        /// </summary>
        /// <param name="entryKey">A unique key for this entry's child object names.</param>
        /// <param name="displayName">The label shown for this host.</param>
        /// <param name="host">The production host (a <see cref="BuildingInstance"/> or <see cref="MainBuilding"/>) to read its <see cref="ProductionQueue"/> from.</param>
        /// <param name="faction">The host's owning faction, for unit-lock checks.</param>
        /// <param name="units">The units this host can produce.</param>
        /// <param name="y">The current vertical cursor, advanced by this entry's height.</param>
        private void BuildEntry(string entryKey, string displayName, MonoBehaviour host, Faction faction, UnitProductionDefinition[] units, ref float y)
        {
            UIFactory.CreateText(transform, $"{entryKey}_Label", UIFactory.BottomLeft, new Vector2(20f, y), new Vector2(240f, 20f)).text = displayName;
            y -= EntryHeight;

            var queue = host.GetComponent<ProductionQueue>();
            _queues.Add(queue);

            var progressText = UIFactory.CreateText(transform, $"{entryKey}_Progress", UIFactory.BottomLeft, new Vector2(20f, y), new Vector2(240f, 20f));
            _progressTexts.Add(progressText);
            y -= EntryHeight;

            if (units == null) return;

            foreach (var unit in units)
            {
                var definition = unit;
                var button = UIFactory.CreateButton(transform, $"{entryKey}_{definition.displayName}", definition.displayName,
                    UIFactory.BottomLeft, new Vector2(20f, y), new Vector2(160f, 24f),
                    () => queue.TryEnqueue(definition));
                _unitButtons.Add(new UnitButtonEntry
                {
                    Button = button,
                    Label = button.GetComponentInChildren<Text>(),
                    Definition = definition,
                    Faction = faction
                });
                y -= EntryHeight;
            }
        }

        /// <summary>
        /// Refreshes each tracked unit button's interactable state and label based on
        /// whether it's currently unlocked — a locked unit stays visible with a "(Locked)"
        /// suffix rather than disappearing, so the player knows research unlocks it.
        /// </summary>
        private void RefreshUnitLocks()
        {
            for (int i = 0; i < _unitButtons.Count; i++)
            {
                var entry = _unitButtons[i];
                if (entry.Button == null) continue;

                bool unlocked = _techManager == null || _techManager.IsUnlocked(entry.Faction, entry.Definition);
                entry.Button.interactable = unlocked;

                if (entry.Label != null)
                {
                    entry.Label.text = unlocked ? entry.Definition.displayName : $"{entry.Definition.displayName} (Locked)";
                }
            }
        }

        /// <summary>
        /// Updates each tracked building's queue/progress text without rebuilding buttons.
        /// </summary>
        private void RefreshProgress()
        {
            for (int i = 0; i < _queues.Count && i < _progressTexts.Count; i++)
            {
                var queue = _queues[i];
                if (queue == null) continue;

                string current = queue.CurrentItem != null
                    ? $"{queue.CurrentItem.displayName} {queue.CurrentProgress01 * 100f:0}%"
                    : "idle";
                _progressTexts[i].text = $"Queue: {queue.QueueCount} | {current}";
            }
        }
    }
}
