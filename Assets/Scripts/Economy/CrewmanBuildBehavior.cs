using MechTS.Units;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Drives a Crewman's build order: travel to a site, then channel for
    /// <see cref="BuildingDefinition.buildTime"/> seconds before the building becomes fully
    /// functional (issue #42). Replaces <see cref="BuildingPlacement"/>'s old instant,
    /// unit-agnostic instantiation for every building going forward. If this Crewman is
    /// destroyed mid-construction, this component and its Update loop are simply gone —
    /// the building stays a permanent unfinished site, no auto-continuation, no refund,
    /// matching <see cref="SabotageAction"/>'s existing "interrupting = simply gone" precedent.
    /// </summary>
    public class CrewmanBuildBehavior : MonoBehaviour
    {
        private enum State { Idle, MovingToSite, Constructing }

        private UnitBase _unit;
        private BuildingDefinition _pendingDefinition;
        private Vector3 _pendingPosition;
        private BuildingInstance _siteInstance;
        private float _elapsed;
        private State _state = State.Idle;

        /// <summary>
        /// Caches the sibling <see cref="UnitBase"/> component.
        /// </summary>
        private void Awake()
        {
            _unit = GetComponent<UnitBase>();
        }

        /// <summary>
        /// Assigns a new build order and begins moving toward the site, overriding any
        /// current build order (the previous site, if any, stays exactly as unfinished/
        /// finished as it was — reassigning a Crewman never un-does prior construction).
        /// </summary>
        /// <param name="definition">The building to construct.</param>
        /// <param name="position">The world position to build at.</param>
        public void AssignBuildOrder(BuildingDefinition definition, Vector3 position)
        {
            _pendingDefinition = definition;
            _pendingPosition = position;
            _siteInstance = null;
            _elapsed = 0f;
            _state = State.MovingToSite;
            _unit.MoveTo(position);
        }

        /// <summary>
        /// Cancels tracking of any current build order — called by <see cref="CrewmanUnit"/>
        /// when this Crewman is given a repair target instead, since both behaviors drive the
        /// same <see cref="UnitBase.MoveTo"/>/<see cref="UnitBase.HasArrived"/> and can't run
        /// concurrently. If construction had already begun, the site stays exactly as
        /// unfinished as it was — same outcome as the Crewman being destroyed mid-construction,
        /// just triggered by reassignment instead.
        /// </summary>
        public void CancelBuildOrder()
        {
            _state = State.Idle;
            _siteInstance = null;
            _pendingDefinition = null;
        }

        /// <summary>
        /// Advances travel-then-construct toward the assigned site each frame.
        /// </summary>
        private void Update()
        {
            switch (_state)
            {
                case State.MovingToSite:
                    if (_unit.HasArrived())
                    {
                        BeginConstruction();
                    }
                    break;
                case State.Constructing:
                    TickConstruction();
                    break;
            }
        }

        /// <summary>
        /// Instantiates the building at the Crewman's arrival position, marked under
        /// construction — visible immediately but non-functional until the timer completes.
        /// </summary>
        private void BeginConstruction()
        {
            var instance = Instantiate(_pendingDefinition.prefab, _pendingPosition, Quaternion.identity);
            var buildingInstance = instance.GetComponent<BuildingInstance>();
            if (buildingInstance == null)
            {
                buildingInstance = instance.AddComponent<BuildingInstance>();
            }

            buildingInstance.Initialize(_pendingDefinition, _unit.Faction);
            buildingInstance.BeginConstruction();
            MechTS.Utilities.FactionColor.Apply(instance, _unit.Faction);

            _siteInstance = buildingInstance;
            _elapsed = 0f;
            _state = State.Constructing;
        }

        /// <summary>
        /// Advances construction progress and completes it once <see cref="BuildingDefinition.buildTime"/>
        /// elapses.
        /// </summary>
        private void TickConstruction()
        {
            if (_siteInstance == null)
            {
                _state = State.Idle;
                return;
            }

            _elapsed += Time.deltaTime;
            float buildTime = _pendingDefinition.buildTime;
            _siteInstance.SetConstructionProgress(buildTime > 0f ? _elapsed / buildTime : 1f);

            if (_elapsed >= buildTime)
            {
                _siteInstance.CompleteConstruction();
                _siteInstance = null;
                _pendingDefinition = null;
                _state = State.Idle;
            }
        }
    }
}
