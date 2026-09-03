using System.Collections.Generic;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// Continuously spins a set of rotor/propeller transforms around their local Z
    /// axis. No pack script covers this (DroneX3 ships with no animation component at
    /// all), so this is new, minimal, and reusable across any future rotor-driven unit.
    /// </summary>
    public class PropellerSpin : MonoBehaviour
    {
        [SerializeField] private List<Transform> _propellers = new List<Transform>();
        [SerializeField] private float _degreesPerSecond = 720f;

        /// <summary>
        /// Rotates each configured propeller transform every frame.
        /// </summary>
        private void Update()
        {
            float delta = _degreesPerSecond * Time.deltaTime;
            foreach (var propeller in _propellers)
            {
                propeller.Rotate(0f, 0f, delta, Space.Self);
            }
        }
    }
}
