using System.Collections.Generic;
using System.Linq;
using MechTS.Core;
using MechTS.Economy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MechTS.Units
{
    /// <summary>
    /// Handles the Attack-Move (A), Patrol (P), Defend (D), and Return to Base (H) hotkeys
    /// (issue #26). Defend and Return to Base execute immediately through
    /// <see cref="UnitManager.StopSelected"/>/a direct <see cref="UnitBase.MoveTo"/> to the
    /// faction's main building — the same choke points every other order already funnels
    /// through, so patrol-cancellation there covers these two for free. Attack-Move and
    /// Patrol instead arm a pending mode, resolved by the <i>next right-click(s)</i> —
    /// routed through <see cref="AttackCommand.TryHandlePendingOrderClick"/> (called via
    /// <see cref="TryHandlePendingOrderClick"/> below) so they share one click with every
    /// other order instead of a second competing right-click handler. Left-click was
    /// considered and rejected during this issue's architecture review: it would collide
    /// with <see cref="SelectionController"/>'s unconditional left-click ownership.
    /// </summary>
    public class UnitOrderCommand : MonoBehaviour
    {
        private enum PendingOrder { None, AttackMove, Patrol }

        private UnitManager _unitManager;
        private GameManager _gameManager;
        private EconomyManager _economyManager;

        private PendingOrder _pendingOrder;
        private List<UnitBase> _patrolUnits = new List<UnitBase>();

        /// <summary>
        /// Caches the scene's <see cref="UnitManager"/>, <see cref="GameManager"/>, and
        /// <see cref="EconomyManager"/>.
        /// </summary>
        private void Start()
        {
            _unitManager = FindFirstObjectByType<UnitManager>();
            _gameManager = FindFirstObjectByType<GameManager>();
            _economyManager = FindFirstObjectByType<EconomyManager>();
        }

        /// <summary>
        /// Polls the Attack-Move/Patrol/Defend/Return-to-Base hotkeys each frame. Defend and
        /// Return to Base execute immediately; Attack-Move and Patrol arm a pending mode for
        /// <see cref="TryHandlePendingOrderClick"/> to resolve. Cancels an in-progress Patrol
        /// recording if the selection changes out from under it (e.g. the player left-clicks
        /// a different unit/group), matching the spec's "recording ends on reselection" rule.
        /// </summary>
        private void Update()
        {
            if (_gameManager == null || !_gameManager.IsRoundActive) return;

            if (_pendingOrder == PendingOrder.Patrol && !_patrolUnits.SequenceEqual(_unitManager.SelectedUnits))
            {
                _pendingOrder = PendingOrder.None;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.aKey.wasPressedThisFrame)
            {
                _pendingOrder = PendingOrder.AttackMove;
            }
            else if (keyboard.pKey.wasPressedThisFrame)
            {
                _pendingOrder = PendingOrder.Patrol;
                _patrolUnits = new List<UnitBase>(_unitManager.SelectedUnits);
            }
            else if (keyboard.dKey.wasPressedThisFrame)
            {
                _pendingOrder = PendingOrder.None;
                _unitManager.StopSelected();
            }
            else if (keyboard.hKey.wasPressedThisFrame)
            {
                _pendingOrder = PendingOrder.None;
                IssueReturnToBase();
            }
        }

        /// <summary>
        /// Moves every selected unit to its own faction's main building.
        /// </summary>
        private void IssueReturnToBase()
        {
            if (_economyManager == null) return;

            foreach (var unit in _unitManager.SelectedUnits)
            {
                if (_economyManager.MainBuildings.TryGetValue(unit.Faction, out var mainBuilding) && mainBuilding != null)
                {
                    UnitManager.CancelPatrol(unit);
                    UnitManager.ClearExplicitTarget(unit);
                    unit.MoveTo(mainBuilding.transform.position);
                }
            }
        }

        /// <summary>
        /// If Attack-Move or Patrol is currently armed, resolves this right-click against the
        /// given ground layer mask into the pending order and returns true (consuming the
        /// click) — Attack-Move clears itself after one click via the existing
        /// <see cref="UnitManager.MoveSelectedTo"/> (which also cancels any active patrol);
        /// Patrol stays armed so subsequent clicks keep adding waypoints. Returns false
        /// (nothing armed, or the click didn't land on ground) so <see cref="AttackCommand"/>
        /// falls through to its normal Sabotage/Attack/Move chain.
        /// </summary>
        /// <param name="ray">The right-click's camera ray.</param>
        /// <param name="groundLayerMask">The ground layer mask to raycast against.</param>
        public bool TryHandlePendingOrderClick(Ray ray, LayerMask groundLayerMask)
        {
            if (_pendingOrder == PendingOrder.None) return false;
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayerMask)) return false;

            if (_pendingOrder == PendingOrder.AttackMove)
            {
                _pendingOrder = PendingOrder.None;
                _unitManager.MoveSelectedTo(hit.point);
            }
            else
            {
                foreach (var unit in _patrolUnits)
                {
                    if (unit == null) continue;
                    var patrol = unit.GetComponent<PatrolBehavior>();
                    if (patrol == null)
                    {
                        UnitManager.ClearExplicitTarget(unit);
                        patrol = unit.gameObject.AddComponent<PatrolBehavior>();
                    }
                    patrol.AddWaypoint(hit.point);
                }
            }

            return true;
        }
    }
}
