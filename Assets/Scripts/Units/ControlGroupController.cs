using MechTS.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MechTS.Units
{
    /// <summary>
    /// Assigns (Ctrl+0-9) and recalls (0-9) numbered control groups via the new Input System.
    /// </summary>
    public class ControlGroupController : MonoBehaviour
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
        /// Polls the number row each frame for control-group assign/recall input.
        /// Ignored outside an active round (e.g. while the Mission End screen is shown).
        /// </summary>
        private void Update()
        {
            if (_gameManager == null || !_gameManager.IsRoundActive) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool ctrlHeld = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;

            for (int i = 0; i <= 9; i++)
            {
                var digitKey = keyboard[(Key)((int)Key.Digit0 + i)];
                if (!digitKey.wasPressedThisFrame) continue;

                if (ctrlHeld)
                {
                    _unitManager.AssignControlGroup(i);
                }
                else
                {
                    _unitManager.RecallControlGroup(i);
                }
            }
        }
    }
}
