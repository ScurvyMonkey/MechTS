using MechTS.Economy;
using MechTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MechTS.UI
{
    /// <summary>
    /// Bottom-right readout of the current unit selection, which numbered control groups
    /// (0-9) currently have units assigned, and — when exactly one unit, one building, the
    /// main building, a resource node, or an enemy unit/building/main building is selected
    /// (issue #24) — a detail card with its stats (and command hotkeys, for a player unit
    /// only — an enemy can't be commanded) via <see cref="SelectionController.SelectedBuilding"/>/
    /// <see cref="SelectionController.SelectedMainBuilding"/>/
    /// <see cref="SelectionController.SelectedResourceNode"/>/
    /// <see cref="SelectionController.SelectedEnemyUnit"/>/
    /// <see cref="SelectionController.SelectedEnemyBuilding"/>/
    /// <see cref="SelectionController.SelectedEnemyMainBuilding"/>. Moved here from
    /// bottom-left so it doesn't overlap <see cref="ProductionMenuPanel"/> (moved to
    /// bottom-left in the same pass) — see the completion report for this layout swap. A
    /// selected building still under construction (issue #42) shows a progress readout
    /// instead of its normal power/production stats, mirroring
    /// <see cref="ProductionQueue.CurrentProgress01"/>'s existing display pattern.
    /// </summary>
    public class SelectionPanel : MonoBehaviour
    {
        private const int GroupCount = 10;
        private const string HotkeyReference = "Move/Attack: Right-click\nStop: S   Defend: D\nAttack-Move: A + click\nPatrol: P + click(s)   Return: H\nSet Group: Ctrl+0-9   Recall Group: 0-9";

        private UnitManager _unitManager;
        private EconomyManager _economyManager;
        private SelectionController _selectionController;
        private Text _selectionText;
        private Text[] _groupIndicators;
        private RectTransform _detailCard;
        private Text _detailStatsText;
        private Text _hotkeyText;
        private UnitBase _detailUnit;
        private Weapon _detailWeapon;
        private BuildingInstance _detailBuilding;
        private MainBuilding _detailMainBuilding;
        private ResourceNode _detailResourceNode;
        private UnitBase _detailEnemyUnit;
        private Weapon _detailEnemyWeapon;
        private BuildingInstance _detailEnemyBuilding;
        private MainBuilding _detailEnemyMainBuilding;

        /// <summary>
        /// Builds the panel's background, selection text, control-group indicators, and the
        /// (initially hidden) single-unit detail card.
        /// </summary>
        private void Awake()
        {
            UIFactory.CreatePanel(transform, "Background", UIFactory.BottomRight, new Vector2(-10f, 10f), new Vector2(340f, 262f));

            // UIFactory.CreateRect sets pivot = anchor, so for this BottomRight-pivoted panel,
            // anchoredPosition.x below places each child's RIGHT edge, not its left — every
            // x-offset here is (intended left edge + width), not the left edge itself.
            _selectionText = UIFactory.CreateText(transform, "Selection", UIFactory.BottomRight, new Vector2(-10f, 222f), new Vector2(320f, 24f));

            _groupIndicators = new Text[GroupCount];
            for (int i = 0; i < GroupCount; i++)
            {
                var indicator = UIFactory.CreateText(transform, $"Group{i}", UIFactory.BottomRight, new Vector2(-306f + i * 28f, 10f), new Vector2(24f, 24f));
                indicator.alignment = TextAnchor.MiddleCenter;
                indicator.text = i.ToString();
                _groupIndicators[i] = indicator;
            }

            _detailCard = UIFactory.CreateRect(transform, UIFactory.BottomRight, new Vector2(-10f, 44f), new Vector2(320f, 172f));

            _detailStatsText = UIFactory.CreateText(_detailCard, "Stats", UIFactory.BottomLeft, Vector2.zero, new Vector2(320f, 96f));
            var statsRect = _detailStatsText.GetComponent<RectTransform>();
            statsRect.anchorMin = Vector2.zero;
            statsRect.anchorMax = Vector2.one;
            statsRect.offsetMin = new Vector2(0f, 76f);
            statsRect.offsetMax = Vector2.zero;

            _hotkeyText = UIFactory.CreateText(_detailCard, "Hotkeys", UIFactory.BottomLeft, Vector2.zero, new Vector2(320f, 76f), 12);
            _hotkeyText.text = HotkeyReference;

            _detailCard.gameObject.SetActive(false);
        }

        /// <summary>
        /// Caches the UnitManager and SelectionController.
        /// </summary>
        private void Start()
        {
            _unitManager = FindFirstObjectByType<UnitManager>();
            _economyManager = FindFirstObjectByType<EconomyManager>();
            _selectionController = FindFirstObjectByType<SelectionController>();
        }

        /// <summary>
        /// Refreshes the selection summary, control-group highlight state, and (if the
        /// selected unit changed) the single-unit detail card.
        /// </summary>
        private void Update()
        {
            if (_unitManager == null) return;

            _selectionText.text = _unitManager.SelectedUnits.Count == 0
                ? "Selection: none"
                : $"Selection: {_unitManager.SelectedUnits.Count} unit(s)";

            for (int i = 0; i < GroupCount; i++)
            {
                _groupIndicators[i].color = _unitManager.IsControlGroupAssigned(i) ? Color.yellow : Color.white;
            }

            UnitBase currentUnit = _unitManager.SelectedUnits.Count == 1 ? _unitManager.SelectedUnits[0] : null;
            BuildingInstance currentBuilding = currentUnit == null && _selectionController != null ? _selectionController.SelectedBuilding : null;
            MainBuilding currentMainBuilding = currentUnit == null && currentBuilding == null && _selectionController != null ? _selectionController.SelectedMainBuilding : null;
            ResourceNode currentResourceNode = currentUnit == null && currentBuilding == null && currentMainBuilding == null && _selectionController != null ? _selectionController.SelectedResourceNode : null;
            bool noneOfTheAbove = currentUnit == null && currentBuilding == null && currentMainBuilding == null && currentResourceNode == null;
            UnitBase currentEnemyUnit = noneOfTheAbove && _selectionController != null ? _selectionController.SelectedEnemyUnit : null;
            BuildingInstance currentEnemyBuilding = noneOfTheAbove && currentEnemyUnit == null && _selectionController != null ? _selectionController.SelectedEnemyBuilding : null;
            MainBuilding currentEnemyMainBuilding = noneOfTheAbove && currentEnemyUnit == null && currentEnemyBuilding == null && _selectionController != null ? _selectionController.SelectedEnemyMainBuilding : null;

            if (currentUnit != _detailUnit || currentBuilding != _detailBuilding || currentMainBuilding != _detailMainBuilding || currentResourceNode != _detailResourceNode
                || currentEnemyUnit != _detailEnemyUnit || currentEnemyBuilding != _detailEnemyBuilding || currentEnemyMainBuilding != _detailEnemyMainBuilding)
            {
                _detailUnit = currentUnit;
                _detailWeapon = currentUnit != null ? currentUnit.Weapon : null;
                _detailBuilding = currentBuilding;
                _detailMainBuilding = currentMainBuilding;
                _detailResourceNode = currentResourceNode;
                _detailEnemyUnit = currentEnemyUnit;
                _detailEnemyWeapon = currentEnemyUnit != null ? currentEnemyUnit.Weapon : null;
                _detailEnemyBuilding = currentEnemyBuilding;
                _detailEnemyMainBuilding = currentEnemyMainBuilding;

                bool anySelected = currentUnit != null || currentBuilding != null || currentMainBuilding != null || currentResourceNode != null
                    || currentEnemyUnit != null || currentEnemyBuilding != null || currentEnemyMainBuilding != null;
                _detailCard.gameObject.SetActive(anySelected);
                _hotkeyText.gameObject.SetActive(currentUnit != null);
            }

            if (_detailUnit != null) RefreshUnitDetailStats(_detailUnit, _detailWeapon);
            else if (_detailBuilding != null) RefreshBuildingDetailStats(_detailBuilding);
            else if (_detailMainBuilding != null) RefreshMainBuildingDetailStats(_detailMainBuilding);
            else if (_detailResourceNode != null) RefreshResourceNodeDetailStats();
            else if (_detailEnemyUnit != null) RefreshUnitDetailStats(_detailEnemyUnit, _detailEnemyWeapon);
            else if (_detailEnemyBuilding != null) RefreshBuildingDetailStats(_detailEnemyBuilding);
            else if (_detailEnemyMainBuilding != null) RefreshMainBuildingDetailStats(_detailEnemyMainBuilding);
        }

        /// <summary>
        /// Rebuilds the detail card's stats text from the given unit's live state — shared
        /// by both the player's own selected unit and an inspected enemy unit (issue #24),
        /// since the stats themselves are already faction-agnostic.
        /// </summary>
        /// <param name="unit">The unit to display.</param>
        /// <param name="weapon">The unit's <see cref="Weapon"/> component, if any.</param>
        private void RefreshUnitDetailStats(UnitBase unit, Weapon weapon)
        {
            string name = unit.name.Replace("(Clone)", string.Empty).Trim();
            var health = unit.HealthComponent;
            string hp = health != null ? $"{health.CurrentHealth:0}/{health.MaxHealth:0}" : "n/a";
            float moveSpeed = unit.Agent != null ? unit.Agent.speed : 0f;

            string weaponLine = string.Empty;
            if (weapon != null && weapon.Config != null)
            {
                var config = weapon.Config;
                weaponLine = $"\nDamage {config.damage:0}   Range {config.range:0}   Fire Rate {config.fireRate:0.0}/s";
            }

            _detailStatsText.text = $"{name}\n{unit.Faction}   HP {hp}   Speed {moveSpeed:0.0}{weaponLine}";
        }

        /// <summary>
        /// Rebuilds the detail card's stats text from the given building's live state,
        /// including which units (if any) it can produce — shared by both the player's own
        /// selected building and an inspected enemy building (issue #24).
        /// </summary>
        /// <param name="building">The building to display.</param>
        private void RefreshBuildingDetailStats(BuildingInstance building)
        {
            var definition = building.Definition;
            var health = building.HealthComponent;
            string hp = health != null ? $"{health.CurrentHealth:0}/{health.MaxHealth:0}" : "n/a";

            if (building.IsUnderConstruction)
            {
                _detailStatsText.text = $"{definition.displayName} (Under Construction)\n{building.Faction}   HP {hp}\nProgress: {building.ConstructionProgress01 * 100f:0}%";
                return;
            }

            string builds = "None";
            if (building.CanProduceUnits && definition.producibleUnits != null && definition.producibleUnits.Length > 0)
            {
                var names = new string[definition.producibleUnits.Length];
                for (int i = 0; i < names.Length; i++) names[i] = definition.producibleUnits[i].displayName;
                builds = string.Join(", ", names);
            }

            _detailStatsText.text = $"{definition.displayName}\n{building.Faction}   HP {hp}\nPower +{definition.powerGenerated}/-{definition.powerConsumed}\nBuilds: {builds}";
        }

        /// <summary>
        /// Rebuilds the detail card's stats text for the given main building, including the
        /// mission's overall buildable list — the main building has no
        /// <see cref="BuildingDefinition"/> of its own (see <see cref="Economy.EconomyManager.BuildableBuildings"/>).
        /// Shared by both the player's own main building and an inspected enemy main
        /// building (issue #24).
        /// </summary>
        /// <param name="mainBuilding">The main building to display.</param>
        private void RefreshMainBuildingDetailStats(MainBuilding mainBuilding)
        {
            string builds = "None";
            if (_economyManager != null && _economyManager.BuildableBuildings.Count > 0)
            {
                var list = _economyManager.BuildableBuildings;
                var names = new string[list.Count];
                for (int i = 0; i < names.Length; i++) names[i] = list[i].displayName;
                builds = string.Join(", ", names);
            }

            var health = mainBuilding.HealthComponent;
            string hp = health != null ? $"{health.CurrentHealth:0}/{health.MaxHealth:0}" : "n/a";

            _detailStatsText.text = $"Main Building\n{mainBuilding.Faction}   HP {hp}\nCan Build: {builds}";
        }

        /// <summary>
        /// Rebuilds the detail card's stats text for the currently detailed resource node,
        /// showing its remaining vs. total yield. Nodes have no faction (see
        /// <see cref="ResourceNode"/>) so this never shows an owner line.
        /// </summary>
        private void RefreshResourceNodeDetailStats()
        {
            _detailStatsText.text = $"{_detailResourceNode.ResourceType} Node\nRemaining: {_detailResourceNode.RemainingYield} / {_detailResourceNode.TotalYield}";
        }
    }
}
