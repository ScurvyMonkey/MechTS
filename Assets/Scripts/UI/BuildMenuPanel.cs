using MechTS.Economy;
using MechTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MechTS.UI
{
    /// <summary>
    /// Bottom-left menu of buildable buildings for the current mission, docked below
    /// <see cref="ProductionMenuPanel"/> in the same corner (see <see cref="PanelHeight"/>).
    /// Clicking a button selects that building for placement via
    /// <see cref="BuildingPlacement"/>; buttons are only interactable during the Economy
    /// Round, when affordable, and — as of issue #42's Crewman-mediated build rework — only
    /// while the current selection is entirely Crewmen (matches the existing Sabotage/Repair
    /// all-or-nothing selection convention); a click while that's not true is a no-op.
    /// </summary>
    public class BuildMenuPanel : MonoBehaviour
    {
        private const float ButtonHeight = 32f;
        private const float ButtonSpacing = 6f;
        private const float PanelWidth = 260f;

        private EconomyManager _economyManager;
        private TechManager _techManager;
        private UnitManager _unitManager;
        private BuildingPlacement _buildingPlacement;
        private BuildingDefinition[] _buildings;
        private Button[] _buttons;
        private Text[] _labels;

        /// <summary>
        /// This panel's current background height in pixels — fixed once built, since the
        /// mission's buildable-building list never changes mid-round. Read by
        /// <see cref="ProductionMenuPanel"/> to stack itself directly above this panel
        /// without either panel needing to poll the other's live <see cref="RectTransform"/>.
        /// </summary>
        public float PanelHeight { get; private set; }

        /// <summary>
        /// Caches manager references, then builds one button per buildable building.
        /// </summary>
        private void Start()
        {
            _economyManager = FindFirstObjectByType<EconomyManager>();
            _techManager = FindFirstObjectByType<TechManager>();
            _unitManager = FindFirstObjectByType<UnitManager>();
            _buildingPlacement = FindFirstObjectByType<BuildingPlacement>();

            var list = _economyManager != null ? _economyManager.BuildableBuildings : null;
            int count = list != null ? list.Count : 0;
            _buildings = new BuildingDefinition[count];
            _buttons = new Button[count];
            _labels = new Text[count];

            PanelHeight = 30f + count * (ButtonHeight + ButtonSpacing);
            UIFactory.CreatePanel(transform, "Background", UIFactory.BottomLeft, new Vector2(10f, 10f), new Vector2(PanelWidth, PanelHeight));

            float y = PanelHeight - ButtonHeight - 12f;
            for (int i = 0; i < count; i++)
            {
                _buildings[i] = list[i];
                var definition = list[i];
                _buttons[i] = UIFactory.CreateButton(transform, $"Build_{definition.displayName}", definition.displayName,
                    UIFactory.BottomLeft, new Vector2(20f, y), new Vector2(220f, ButtonHeight),
                    () => TrySelectForPlacement(definition));
                _labels[i] = _buttons[i].GetComponentInChildren<Text>();
                y -= ButtonHeight + ButtonSpacing;
            }
        }

        /// <summary>
        /// Disables build buttons outside the Economy Round, when the player can't afford
        /// them, when they're not yet unlocked by research (a locked building stays visible
        /// with a "(Locked)" suffix rather than disappearing, so the player knows research
        /// unlocks it), when the current selection isn't entirely Crewmen (issue #42), or —
        /// for a turret definition (<see cref="BuildingDefinition.requiresRoboticsPlant"/>,
        /// issue #45) — when the faction doesn't yet own a Robotics Plant (a "(Requires
        /// Robotics Plant)" suffix instead, kept distinct from the research-lock suffix since
        /// it's a different gate).
        /// </summary>
        private void Update()
        {
            if (_economyManager == null || _buildings.Length == 0) return;

            bool economyActive = _economyManager.IsEconomyRoundActive;
            bool crewmenSelected = SelectionIsAllCrewmen();
            bool hasRoboticsPlant = _economyManager.FactionOwnsBuildingWhere(Faction.Player, d => d.enablesTurretConstruction);
            var state = _economyManager.GetState(Faction.Player);
            float powerMultiplier = _techManager != null ? _techManager.GetStatMultiplier(Faction.Player, UpgradeStatType.PowerCapacity) : 1f;

            for (int i = 0; i < _buildings.Length; i++)
            {
                bool unlocked = _techManager == null || _techManager.IsUnlocked(Faction.Player, _buildings[i]);
                bool roboticsPlantOk = !_buildings[i].requiresRoboticsPlant || hasRoboticsPlant;
                bool affordable = state != null
                    && state.Stockpile.CanAfford(_buildings[i].GetCost())
                    && PowerSystem.CanAfford(state, _buildings[i].powerGenerated, _buildings[i].powerConsumed, powerMultiplier);
                _buttons[i].interactable = economyActive && affordable && unlocked && crewmenSelected && roboticsPlantOk;

                if (_labels[i] != null)
                {
                    _labels[i].text = !unlocked ? $"{_buildings[i].displayName} (Locked)"
                        : !roboticsPlantOk ? $"{_buildings[i].displayName} (Requires Robotics Plant)"
                        : _buildings[i].displayName;
                }
            }
        }

        /// <summary>
        /// Selects the given building for placement, but only if the current selection is
        /// entirely Crewmen (issue #42) — otherwise a no-op, matching the existing Sabotage/
        /// Repair all-or-nothing selection convention. Also gated via <see cref="Button.interactable"/>
        /// above, but re-checked here defensively since a click can still land in the same
        /// frame the selection changes.
        /// </summary>
        /// <param name="definition">The building to select for placement.</param>
        private void TrySelectForPlacement(BuildingDefinition definition)
        {
            if (!SelectionIsAllCrewmen()) return;
            _buildingPlacement.SelectBuildingToPlace(definition);
        }

        /// <summary>
        /// Whether the current selection is non-empty and entirely <see cref="CrewmanUnit"/>s.
        /// </summary>
        private bool SelectionIsAllCrewmen()
        {
            if (_unitManager == null || _unitManager.SelectedUnits.Count == 0) return false;
            foreach (var unit in _unitManager.SelectedUnits)
            {
                if (!(unit is CrewmanUnit)) return false;
            }
            return true;
        }
    }
}
