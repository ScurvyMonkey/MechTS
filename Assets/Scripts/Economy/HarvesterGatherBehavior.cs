using MechTS.Core;
using MechTS.Units;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Drives a Harvester's travel/gather/deposit loop: move to an assigned node, gather
    /// up to carry capacity, return to the faction's main building, deposit, repeat.
    /// Only active during the Economy Round — freezes in place once the Battle Round begins.
    /// </summary>
    public class HarvesterGatherBehavior : MonoBehaviour
    {
        [SerializeField] private HarvesterConfig _config;

        private enum State { Idle, MovingToNode, Gathering, ReturningToDeposit }

        private UnitBase _unit;
        private EconomyManager _economyManager;
        private TechManager _techManager;
        private ResourceNode _assignedNode;
        private Transform _depositPoint;
        private State _state = State.Idle;
        private int _carriedAmount;
        private float _gatherTimer;
        private GameObject _targetScatterInstance;

        /// <summary>
        /// Whether <see cref="_targetScatterInstance"/> was ever actually assigned a specific
        /// scattered instance to walk to, as opposed to the node having none at all (issue #50).
        /// A bare null-check on <see cref="_targetScatterInstance"/> alone can't tell "this node
        /// has no scatter art configured, there was never a specific target" apart from "the
        /// instance I was walking to just got destroyed" — both read as null once a Unity object
        /// is destroyed. This flag disambiguates them so a node with no scatter instances never
        /// re-triggers a reroute (and a fresh <c>Agent.SetDestination</c> call) on every tick.
        /// </summary>
        private bool _hasScatterTarget;

        /// <summary>
        /// True while this Harvester is actively extracting from its assigned node — read by
        /// <see cref="Units.HarvesterPartAnimator"/> (issue #65 follow-up) to drive the claw
        /// animation only during genuine gathering, not while idle/moving/returning.
        /// </summary>
        public bool IsGathering => _state == State.Gathering;

        /// <summary>
        /// True while idle with no usable node assigned — either never assigned one (freshly
        /// spawned) or its assigned node has depleted. Read by
        /// <see cref="MechTS.AI.EnemyEconomyAI"/> (issue #79) to find Harvesters that need a
        /// fresh node assignment.
        /// </summary>
        public bool NeedsNodeAssignment => _state == State.Idle && (_assignedNode == null || _assignedNode.IsDepleted);

        /// <summary>
        /// Caches the sibling <see cref="UnitBase"/> component.
        /// </summary>
        private void Awake()
        {
            _unit = GetComponent<UnitBase>();
        }

        /// <summary>
        /// Caches the <see cref="EconomyManager"/> and locates this faction's main building
        /// to use as the deposit point.
        /// </summary>
        private void Start()
        {
            _economyManager = FindFirstObjectByType<EconomyManager>();
            _techManager = FindFirstObjectByType<TechManager>();
            _unit.HealthComponent?.SetMaxHealth(_config.maxHealth);
            _unit.SetBaseMoveSpeed(_config.moveSpeed);
            _unit.SetUpkeepCost(_config.oreUpkeepPerMinute, _config.biomassUpkeepPerMinute, _config.goldUpkeepPerMinute);
            var vehicleAnimationDriver = GetComponent<VehicleAnimationDriver>();
            vehicleAnimationDriver?.SetEngineSound(_config.engineSound);
            vehicleAnimationDriver?.SetTrackSpeedMultiplier(_config.trackAnimationSpeedMultiplier);

            var partAnimator = GetComponent<HarvesterPartAnimator>();
            partAnimator?.SetTrackScrollSpeed(_config.trackScrollSpeed);

            var mainBuilding = FindMainBuildingForFaction(_unit.Faction);
            _depositPoint = mainBuilding != null ? mainBuilding.transform : transform;
        }

        /// <summary>
        /// Locates a faction's main building in the scene to use as this harvester's deposit point.
        /// </summary>
        /// <param name="faction">The faction to find a main building for.</param>
        private MainBuilding FindMainBuildingForFaction(Faction faction)
        {
            foreach (var building in FindObjectsByType<MainBuilding>(FindObjectsSortMode.None))
            {
                if (building.Faction == faction) return building;
            }
            return null;
        }

        /// <summary>
        /// Assigns a new node to gather from and begins moving toward its nearest currently-alive
        /// scattered instance (issue #50), rather than the node's own center point.
        /// </summary>
        /// <param name="node">The resource node to gather from.</param>
        public void AssignToNode(ResourceNode node)
        {
            _assignedNode = node;
            _state = State.MovingToNode;
            MoveToNearestScatterPoint();
        }

        /// <summary>
        /// Queries <see cref="_assignedNode"/> for its nearest currently-alive scattered instance
        /// to this Harvester's own position and moves toward it, tracking which instance was
        /// targeted (issue #50). Requires <see cref="_assignedNode"/> to already be non-null.
        /// </summary>
        private void MoveToNearestScatterPoint()
        {
            Vector3 destination = _assignedNode.GetNearestScatterTarget(transform.position, out _targetScatterInstance);
            _hasScatterTarget = _targetScatterInstance != null;
            _unit.MoveTo(destination);
        }

        /// <summary>
        /// Advances the gather/travel/deposit state machine each frame, while the Economy
        /// Round is active.
        /// </summary>
        private void Update()
        {
            if (!_economyManager.IsEconomyRoundActive) return;

            switch (_state)
            {
                case State.MovingToNode:
                    TickMovingToNode();
                    break;
                case State.Gathering:
                    TickGathering();
                    break;
                case State.ReturningToDeposit:
                    TickReturning();
                    break;
            }
        }

        /// <summary>
        /// Checks whether the harvester has arrived at its targeted scattered instance and
        /// begins gathering. Reroutes to a new nearest instance first if the one it was walking
        /// toward was destroyed out from under it — by this Harvester's own concurrent gathering
        /// elsewhere, another Harvester, or sabotage — before it arrived (issue #50). Once
        /// gathering actually starts, no further rerouting happens even if the instance it
        /// stopped at is later destroyed; the gather tick is a pure shared-pool timer, not tied
        /// to any specific instance.
        /// </summary>
        private void TickMovingToNode()
        {
            if (_assignedNode == null) { _state = State.Idle; return; }

            if (_hasScatterTarget && _targetScatterInstance == null)
            {
                MoveToNearestScatterPoint();
            }

            if (_unit.HasArrived())
            {
                _state = State.Gathering;
                _gatherTimer = 0f;
            }
        }

        /// <summary>
        /// Accrues carried resources over time until at capacity or the node is depleted,
        /// then heads back to deposit.
        /// </summary>
        private void TickGathering()
        {
            if (_assignedNode == null || _assignedNode.IsDepleted)
            {
                if (_carriedAmount > 0)
                {
                    _state = State.ReturningToDeposit;
                    _unit.MoveTo(_depositPoint.position);
                }
                else
                {
                    _state = State.Idle;
                }
                return;
            }

            _gatherTimer += Time.deltaTime;
            if (_gatherTimer < 1f) return;

            _gatherTimer = 0f;
            int room = _config.carryCapacity - _carriedAmount;
            float multiplier = _techManager != null ? _techManager.GetStatMultiplier(_unit.Faction, UpgradeStatType.GatherRate, _unit.Capabilities) : 1f;
            int extracted = _assignedNode.Extract(Mathf.Min(Mathf.RoundToInt(_config.gatherRate * multiplier), room));
            _carriedAmount += extracted;

            if (_carriedAmount >= _config.carryCapacity || _assignedNode.IsDepleted)
            {
                _state = State.ReturningToDeposit;
                _unit.MoveTo(_depositPoint.position);
            }
        }

        /// <summary>
        /// Checks whether the harvester has arrived at the deposit point, deposits its
        /// carried resources, and resumes gathering from its assigned node if it still has yield.
        /// </summary>
        private void TickReturning()
        {
            if (!_unit.HasArrived()) return;

            _economyManager.Deposit(_unit.Faction, _assignedNode != null ? _assignedNode.ResourceType : ResourceType.Ore, _carriedAmount);
            _carriedAmount = 0;

            if (_assignedNode != null && !_assignedNode.IsDepleted)
            {
                _state = State.MovingToNode;
                MoveToNearestScatterPoint();
            }
            else
            {
                _state = State.Idle;
            }
        }
    }
}
