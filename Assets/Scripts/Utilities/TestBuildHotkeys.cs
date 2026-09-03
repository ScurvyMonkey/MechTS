using MechTS.Economy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MechTS.Utilities
{
    /// <summary>
    /// Manual-testing scaffolding only — not a real game system. Lets a tester pick a
    /// building to place with B (Economic) or N (Detection) since no build-menu UI exists yet.
    /// </summary>
    public class TestBuildHotkeys : MonoBehaviour
    {
        [SerializeField] private BuildingPlacement _buildingPlacement;
        [SerializeField] private BuildingDefinition _economicBuildingDefinition;
        [SerializeField] private BuildingDefinition _detectionBuildingDefinition;

        /// <summary>
        /// Polls B/N each frame to select a building for placement.
        /// </summary>
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.bKey.wasPressedThisFrame)
            {
                _buildingPlacement.SelectBuildingToPlace(_economicBuildingDefinition);
            }
            else if (keyboard.nKey.wasPressedThisFrame)
            {
                _buildingPlacement.SelectBuildingToPlace(_detectionBuildingDefinition);
            }
        }
    }
}
