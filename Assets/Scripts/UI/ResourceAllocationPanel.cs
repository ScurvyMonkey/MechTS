using MechTS.Core;
using MechTS.Economy;
using MechTS.Units;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MechTS.UI
{
    /// <summary>
    /// Toggleable (not always-visible, unlike the HUD's other panels) allocation control for
    /// the locked resource pool (issue #52) — opens/closes on the <c>R</c> hotkey, same shape
    /// as <see cref="TechPanel"/> since every HUD corner is already occupied. Shows, per
    /// resource type, the player's current allocation percentage (adjustable in 10% steps)
    /// and a live pool amount/capacity readout.
    /// </summary>
    public class ResourceAllocationPanel : MonoBehaviour
    {
        private const int Step = 10;
        private static readonly ResourceType[] AllTypes = { ResourceType.Ore, ResourceType.Biomass, ResourceType.Gold };
        private const float RowHeight = 32f;
        private const float PanelWidth = 360f;

        private GameManager _gameManager;
        private EconomyManager _economyManager;
        private RectTransform _panelRoot;
        private readonly Text[] _percentLabels = new Text[3];
        private readonly Text[] _poolLabels = new Text[3];
        private bool _isOpen;

        /// <summary>
        /// Caches manager references and builds the (initially closed) panel.
        /// </summary>
        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _economyManager = FindFirstObjectByType<EconomyManager>();

            BuildPanel();
            SetOpen(false);
        }

        /// <summary>
        /// Toggles the panel open/closed on the <c>R</c> hotkey and refreshes readouts while
        /// open. Ignored outside an active round, matching every other input system's gating
        /// convention.
        /// </summary>
        private void Update()
        {
            if (_gameManager == null || !_gameManager.IsRoundActive) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                SetOpen(!_isOpen);
            }

            if (_isOpen) RefreshReadouts();
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
            if (open) RefreshReadouts();
        }

        /// <summary>
        /// Builds the panel background and one row (label, -, percent, +, pool readout) per
        /// resource type.
        /// </summary>
        private void BuildPanel()
        {
            float panelHeight = 20f + AllTypes.Length * RowHeight;
            _panelRoot = UIFactory.CreatePanel(transform, "Background", UIFactory.Center, Vector2.zero, new Vector2(PanelWidth, panelHeight));

            for (int i = 0; i < AllTypes.Length; i++)
            {
                BuildRow(AllTypes[i], i, panelHeight);
            }
        }

        /// <summary>
        /// Builds one resource type's row: name label, decrement/increment buttons, live
        /// percent readout, and live pool amount/capacity readout.
        /// </summary>
        /// <param name="type">The resource type this row controls.</param>
        /// <param name="index">This row's position from the top.</param>
        /// <param name="panelHeight">The panel's total height, for top-anchored positioning.</param>
        private void BuildRow(ResourceType type, int index, float panelHeight)
        {
            float y = -(10f + index * RowHeight);

            var nameText = UIFactory.CreateText(_panelRoot, $"{type}_Name", UIFactory.TopLeft, new Vector2(10f, y), new Vector2(70f, RowHeight - 4f));
            nameText.text = type.ToString();

            UIFactory.CreateButton(_panelRoot, $"{type}_Minus", "-", UIFactory.TopLeft, new Vector2(85f, y), new Vector2(24f, RowHeight - 4f),
                () => AdjustAllocation(type, -Step));

            var percentText = UIFactory.CreateText(_panelRoot, $"{type}_Percent", UIFactory.TopLeft, new Vector2(115f, y), new Vector2(50f, RowHeight - 4f));
            percentText.alignment = TextAnchor.UpperCenter;
            _percentLabels[index] = percentText;

            UIFactory.CreateButton(_panelRoot, $"{type}_Plus", "+", UIFactory.TopLeft, new Vector2(170f, y), new Vector2(24f, RowHeight - 4f),
                () => AdjustAllocation(type, Step));

            var poolText = UIFactory.CreateText(_panelRoot, $"{type}_Pool", UIFactory.TopLeft, new Vector2(200f, y), new Vector2(150f, RowHeight - 4f));
            _poolLabels[index] = poolText;
        }

        /// <summary>
        /// Adjusts the Player faction's allocation percentage for the given resource type by
        /// <paramref name="delta"/>, clamped to [0, 100] inside <see cref="LockedResourcePool.SetAllocationPercent"/>.
        /// </summary>
        /// <param name="type">The resource type to adjust.</param>
        /// <param name="delta">The step to apply (positive or negative).</param>
        private void AdjustAllocation(ResourceType type, int delta)
        {
            var state = _economyManager != null ? _economyManager.GetState(Faction.Player) : null;
            if (state == null) return;

            int current = state.LockedPool.GetAllocationPercent(type);
            state.LockedPool.SetAllocationPercent(type, current + delta);
        }

        /// <summary>
        /// Refreshes each row's percent and pool amount/capacity readouts from live state.
        /// </summary>
        private void RefreshReadouts()
        {
            var state = _economyManager != null ? _economyManager.GetState(Faction.Player) : null;
            if (state == null) return;

            for (int i = 0; i < AllTypes.Length; i++)
            {
                var type = AllTypes[i];
                _percentLabels[i].text = $"{state.LockedPool.GetAllocationPercent(type)}%";
                _poolLabels[i].text = $"Pool: {state.LockedPool.GetAmount(type)}/{state.LockedPool.GetCapacity(type)}";
            }
        }
    }
}
