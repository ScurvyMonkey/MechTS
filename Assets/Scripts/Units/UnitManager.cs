using System.Collections.Generic;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// Tracks all active units, the player's current selection, and numbered control
    /// groups. Created by <see cref="MechTS.Core.Bootstrapper"/>.
    /// </summary>
    public class UnitManager : MonoBehaviour
    {
        private readonly List<UnitBase> _activeUnits = new List<UnitBase>();
        private readonly List<UnitBase> _selectedUnits = new List<UnitBase>();
        private readonly Dictionary<int, List<UnitBase>> _controlGroups = new Dictionary<int, List<UnitBase>>();

        /// <summary>
        /// All currently active, registered units.
        /// </summary>
        public IReadOnlyList<UnitBase> ActiveUnits => _activeUnits;

        /// <summary>
        /// The units currently selected by the player.
        /// </summary>
        public IReadOnlyList<UnitBase> SelectedUnits => _selectedUnits;

        /// <summary>
        /// Adds a unit to the active registry.
        /// </summary>
        /// <param name="unit">The unit to register.</param>
        public void Register(UnitBase unit)
        {
            if (!_activeUnits.Contains(unit))
            {
                _activeUnits.Add(unit);
            }
        }

        /// <summary>
        /// Removes a unit from the active registry, the current selection, and any control groups.
        /// </summary>
        /// <param name="unit">The unit to deregister.</param>
        public void Deregister(UnitBase unit)
        {
            _activeUnits.Remove(unit);
            _selectedUnits.Remove(unit);
            foreach (var group in _controlGroups.Values)
            {
                group.Remove(unit);
            }
        }

        /// <summary>
        /// Returns the number of active units belonging to the given faction.
        /// </summary>
        /// <param name="faction">The faction to count.</param>
        public int CountByFaction(Faction faction)
        {
            int count = 0;
            foreach (var unit in _activeUnits)
            {
                if (unit.Faction == faction) count++;
            }
            return count;
        }

        /// <summary>
        /// Clears the current selection and selects only the given units.
        /// </summary>
        /// <param name="units">The units to select.</param>
        public void SetSelection(IEnumerable<UnitBase> units)
        {
            ClearSelection();
            foreach (var unit in units)
            {
                _selectedUnits.Add(unit);
                unit.SetSelected(true);
            }
        }

        /// <summary>
        /// Deselects every currently selected unit.
        /// </summary>
        public void ClearSelection()
        {
            foreach (var unit in _selectedUnits)
            {
                unit.SetSelected(false);
            }
            _selectedUnits.Clear();
        }

        /// <summary>
        /// Issues a move order to every currently selected unit, spreading their
        /// individual destinations slightly so they don't stack on top of each other.
        /// Cancels any active <see cref="PatrolBehavior"/> and explicit
        /// <see cref="Weapon"/> target lock (issue #31) first — every order in this
        /// project funnels through here or <see cref="StopSelected"/>, so this is the one
        /// place a new order needs to override a unit's patrol loop or attack-target lock.
        /// </summary>
        /// <param name="destination">The clicked target position.</param>
        public void MoveSelectedTo(Vector3 destination)
        {
            const float spreadRadius = 1.5f;
            foreach (var unit in _selectedUnits)
            {
                CancelPatrol(unit);
                ClearExplicitTarget(unit);
                Vector2 offset = Random.insideUnitCircle * spreadRadius;
                Vector3 target = destination + new Vector3(offset.x, 0f, offset.y);
                unit.MoveTo(target);
            }
        }

        /// <summary>
        /// Halts every currently selected unit's current order, cancelling any active
        /// <see cref="PatrolBehavior"/> and explicit <see cref="Weapon"/> target lock first
        /// (see <see cref="MoveSelectedTo"/>).
        /// </summary>
        public void StopSelected()
        {
            foreach (var unit in _selectedUnits)
            {
                CancelPatrol(unit);
                ClearExplicitTarget(unit);
                unit.Stop();
            }
        }

        /// <summary>
        /// Destroys the given unit's <see cref="PatrolBehavior"/>, if it has one. Public so
        /// <see cref="UnitOrderCommand"/>'s Return to Base order (which issues a direct
        /// <see cref="UnitBase.MoveTo"/> per unit rather than going through
        /// <see cref="MoveSelectedTo"/>, since each unit may need its own faction's main
        /// building) can reuse this instead of duplicating it.
        /// </summary>
        /// <param name="unit">The unit to cancel patrol on.</param>
        public static void CancelPatrol(UnitBase unit)
        {
            var patrol = unit.GetComponent<PatrolBehavior>();
            if (patrol != null) Destroy(patrol);
        }

        /// <summary>
        /// Clears the given unit's explicit <see cref="Weapon"/> attack-target lock (issue
        /// #31), if it has one. A no-op for units without a <see cref="Weapon"/> component
        /// (Harvesters, Saboteurs), mirroring <see cref="CancelPatrol"/>'s null-safe shape.
        /// </summary>
        /// <param name="unit">The unit to clear the explicit target lock on.</param>
        public static void ClearExplicitTarget(UnitBase unit)
        {
            var weapon = unit.GetComponent<Weapon>();
            if (weapon != null) weapon.ClearExplicitTarget();
        }

        /// <summary>
        /// Assigns the current selection to a numbered control group, replacing its previous contents.
        /// </summary>
        /// <param name="groupNumber">The control group number (0-9).</param>
        public void AssignControlGroup(int groupNumber)
        {
            _controlGroups[groupNumber] = new List<UnitBase>(_selectedUnits);
        }

        /// <summary>
        /// Selects the units bound to a numbered control group, if any exist.
        /// </summary>
        /// <param name="groupNumber">The control group number (0-9).</param>
        public void RecallControlGroup(int groupNumber)
        {
            if (_controlGroups.TryGetValue(groupNumber, out var group))
            {
                SetSelection(group);
            }
        }

        /// <summary>
        /// Returns whether the given control group currently has any units assigned.
        /// </summary>
        /// <param name="groupNumber">The control group number (0-9).</param>
        public bool IsControlGroupAssigned(int groupNumber)
        {
            return _controlGroups.TryGetValue(groupNumber, out var group) && group.Count > 0;
        }
    }
}
