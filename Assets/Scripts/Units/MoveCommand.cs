using MechTS.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MechTS.Units
{
    /// <summary>
    /// Issues a Stop order (S key) to the current selection via the new Input System.
    /// Right-click move/attack resolution lives in <see cref="AttackCommand"/> (issue #5),
    /// which took over right-click handling so a click can resolve to either an Attack
    /// or a plain Move order without both components racing on the same click.
    /// </summary>
    public class MoveCommand : MonoBehaviour
    {
        private UnitManager _unitManager;
        private GameManager _gameManager;

        /// <summary>
        /// Caches the scene's <see cref="UnitManager"/> and <see cref="GameManager"/>.
        /// </summary>
        private void Start()
        {
            _unitManager = FindFirstObjectByType<UnitManager>();
            _gameManager = FindFirstObjectByType<GameManager>();
        }

        /// <summary>
        /// Polls for the Stop hotkey each frame. Ignored outside an active round (e.g.
        /// while the Mission End screen is shown).
        /// </summary>
        private void Update()
        {
            if (_gameManager == null || !_gameManager.IsRoundActive) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.sKey.wasPressedThisFrame)
            {
                _unitManager.StopSelected();
            }
        }
    }
}
