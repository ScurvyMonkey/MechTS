using System.Collections.Generic;
using MechTS.Units;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// A per-building queue of units awaiting production. Deliberately ungated by
    /// round state — unlike harvesting and building placement, unit production
    /// continues from the Economy Round into the Battle Round.
    /// </summary>
    public class ProductionQueue : MonoBehaviour
    {
        private readonly Queue<UnitProductionDefinition> _queue = new Queue<UnitProductionDefinition>();

        private IProductionHost _host;
        private EconomyManager _economyManager;
        private UnitProductionDefinition _current;
        private float _elapsed;

        /// <summary>The unit currently in production, or null if the queue is idle.</summary>
        public UnitProductionDefinition CurrentItem => _current;

        /// <summary>The current item's production progress, from 0 to 1.</summary>
        public float CurrentProgress01 => _current != null ? Mathf.Clamp01(_elapsed / _current.productionTime) : 0f;

        /// <summary>The number of units waiting behind the one currently in production.</summary>
        public int QueueCount => _queue.Count;

        /// <summary>
        /// Caches sibling and scene references.
        /// </summary>
        private void Start()
        {
            _host = GetComponent<IProductionHost>();
            _economyManager = FindFirstObjectByType<EconomyManager>();
        }

        /// <summary>
        /// Attempts to enqueue a unit for production, spending its cost immediately if affordable.
        /// </summary>
        /// <param name="definition">The unit to queue.</param>
        public bool TryEnqueue(UnitProductionDefinition definition)
        {
            var state = _economyManager.GetState(_host.Faction);
            if (state == null || !state.Stockpile.CanAfford(definition.GetCost())) return false;

            state.Stockpile.Spend(definition.GetCost());
            _queue.Enqueue(definition);
            return true;
        }

        /// <summary>
        /// Advances production on the current queue head each frame and spawns the unit on completion.
        /// </summary>
        private void Update()
        {
            if (_host.IsDisabled) return;

            if (_current == null)
            {
                if (_queue.Count == 0) return;
                _current = _queue.Dequeue();
                _elapsed = 0f;
            }

            _elapsed += Time.deltaTime;
            if (_elapsed >= _current.productionTime)
            {
                var spawned = Instantiate(_current.GetPrefab(_host.Faction), transform.position, Quaternion.identity);
                var unit = spawned.GetComponent<UnitBase>();
                if (unit != null)
                {
                    unit.SetFaction(_host.Faction);
                    if (_host.RallyPoint.HasValue)
                    {
                        unit.MoveTo(_host.RallyPoint.Value);
                    }
                }
                // Skip the placeholder tint once this definition has real per-faction art
                // (an assigned enemyPrefabOverride implies both prefab and enemyPrefabOverride
                // are dedicated real art, per issue #81/#82's finding) — tinting would corrupt
                // it instead of substituting for art that doesn't exist yet.
                if (_current.enemyPrefabOverride == null)
                {
                    MechTS.Utilities.FactionColor.Apply(spawned, _host.Faction);
                }
                _current = null;
            }
        }
    }
}
