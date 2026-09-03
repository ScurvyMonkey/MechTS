using System.Collections.Generic;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// A finite, depletable resource node that harvesters can be assigned to. Its yield is
    /// represented visually as a scattered cluster of many lightweight objects (see
    /// <see cref="ResourceNodeDefinition.scatterObjectPrefab"/>) rather than a single sprite —
    /// individual scattered objects are destroyed as the pool drains, so the cluster visibly
    /// shrinks toward zero as <see cref="RemainingYield"/> approaches zero.
    /// </summary>
    public class ResourceNode : MonoBehaviour, ISabotageTarget
    {
        /// <summary>
        /// Matches the small positive Y offset convention CLAUDE.md's Sprite &amp; Art Convention
        /// establishes for rings/unit art, keeping a flat sprite-art scatter instance from
        /// rendering at exactly Ground's own Y=0 and being fully occluded by it.
        /// </summary>
        private const float ScatterYOffset = 0.03f;

        [SerializeField] private ResourceNodeDefinition _definition;

        private int _remainingYield;
        private EconomyManager _economyManager;
        private List<GameObject> _scatterObjects;

        /// <summary>
        /// The tiered-depletion-art tier (0=high/1=mid/2=low) every currently-visible scatter
        /// instance was last swapped to, or -1 if never applied. Compared against the freshly
        /// computed tier each <see cref="UpdateScatterVisibility"/> call so the swap in
        /// <see cref="ApplyScatterTier"/> only runs on an actual tier change — not on every
        /// gather tick, which would otherwise Destroy+Instantiate every visible instance far
        /// more often than a threshold is actually crossed (flagged by /arch, issue #59).
        /// </summary>
        private int _currentTier = -1;

        /// <summary>The resource type this node yields.</summary>
        public ResourceType ResourceType => _definition.resourceType;

        /// <summary>Whether this node still has yield remaining.</summary>
        public bool IsDepleted => _remainingYield <= 0;

        /// <summary>How much yield this node currently has left to extract.</summary>
        public int RemainingYield => _remainingYield;

        /// <summary>This node's original yield amount, before any extraction.</summary>
        public int TotalYield => _definition.yieldAmount;

        /// <summary>
        /// How far from this node's center, on the local XZ plane, its footprint extends
        /// (issue #51 — the radius an Auto-Extractor must be placed within, and searches
        /// against to find its host node).
        /// </summary>
        public float ScatterRadius => _definition.scatterRadius;

        /// <summary>ISabotageTarget position — this node's current world position.</summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// Seeds the remaining yield from the node's definition, generates the scattered
        /// visual cluster if it doesn't already exist (see <see cref="GenerateScatter"/>),
        /// and syncs visible scatter count to the (full) starting yield.
        /// </summary>
        private void Awake()
        {
            _remainingYield = _definition.yieldAmount;
            GenerateScatter();
            UpdateScatterVisibility();
        }

        /// <summary>
        /// Registers this node with the scene's <see cref="EconomyManager"/> so systems like
        /// <see cref="UI.MinimapPanel"/> can enumerate all nodes without a scene-wide scan.
        /// Done in <c>Start()</c>, not <c>Awake()</c> — <see cref="EconomyManager"/> is a
        /// <see cref="Core.Bootstrapper"/>-created manager with no cross-GameObject
        /// <c>Awake()</c> ordering guarantee against this pre-placed scene object.
        /// </summary>
        private void Start()
        {
            _economyManager = FindFirstObjectByType<EconomyManager>();
            _economyManager?.RegisterResourceNode(this);
        }

        /// <summary>
        /// Deregisters this node when it's destroyed (depleted or sabotaged).
        /// </summary>
        private void OnDestroy()
        {
            _economyManager?.DeregisterResourceNode(this);
        }

        /// <summary>
        /// Removes up to <paramref name="amount"/> from the node's remaining yield,
        /// updates the visible scatter count, and returns how much was actually
        /// extracted. Destroys the node (and every remaining scattered object with it,
        /// as its children) once exhausted.
        /// </summary>
        /// <param name="amount">The amount a harvester is attempting to extract.</param>
        public int Extract(int amount)
        {
            int extracted = Mathf.Min(amount, _remainingYield);
            _remainingYield -= extracted;

            if (_remainingYield <= 0)
            {
                Destroy(gameObject);
            }
            else
            {
                UpdateScatterVisibility();
            }

            return extracted;
        }

        /// <summary>
        /// Scatters <see cref="ResourceNodeDefinition.scatterObjectCount"/> copies of
        /// <see cref="ResourceNodeDefinition.scatterObjectPrefab"/> at random positions within
        /// <see cref="ResourceNodeDefinition.scatterRadius"/> on the local XZ plane, parented
        /// under this node, and hides this node's own placeholder <see cref="MeshRenderer"/>
        /// so it doesn't render alongside the cluster it's being replaced by. Safe to call more
        /// than once (from both <see cref="Awake"/> and, for a node freshly painted via the Map
        /// Editor, an explicit call right after placement — Edit Mode never runs <c>Awake()</c>
        /// for a newly instantiated component, confirmed directly, so relying on <c>Awake()</c>
        /// alone would leave a painted area showing no scatter until Play Mode starts): if this
        /// node already has children — because a prior call already scattered them — it just
        /// rebuilds the tracked list from those existing children instead of scattering a
        /// second set on top.
        /// </summary>
        public void GenerateScatter()
        {
            if (TryGetComponent<MeshRenderer>(out var placeholderRenderer))
            {
                placeholderRenderer.enabled = false;
            }

            // Keeps the click-selection hit area (SelectionController, AttackCommand's
            // redirect-to-node flow) matching the actual visible cluster footprint — the
            // prefab's own authored collider radius is a stale placeholder from before this
            // node became a scattered cluster (issue #49) and must never be trusted directly.
            if (TryGetComponent<CapsuleCollider>(out var capsuleCollider))
            {
                capsuleCollider.radius = _definition.scatterRadius;
            }

            if (transform.childCount > 0)
            {
                _scatterObjects = new List<GameObject>(transform.childCount);
                for (int i = 0; i < transform.childCount; i++)
                {
                    _scatterObjects.Add(transform.GetChild(i).gameObject);
                }
                return;
            }

            _scatterObjects = new List<GameObject>(_definition.scatterObjectCount);
            // A fresh node starts at 100% remaining yield, always at or above tierThresholdHigh,
            // so the initial scatter is generated directly with the high tier's art — avoiding
            // an immediate, wasteful Destroy+Instantiate the moment UpdateScatterVisibility()
            // runs right after this in Awake().
            GameObject initialPrefab = _definition.useTieredScatterArt ? _definition.tierPrefabHigh : _definition.scatterObjectPrefab;
            if (initialPrefab == null) return;

            for (int i = 0; i < _definition.scatterObjectCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * _definition.scatterRadius;
                GameObject scatterInstance = Instantiate(initialPrefab, transform);
                // ScatterYOffset keeps flat sprite-art scatter instances (e.g. Prop_Tree) from
                // sitting at exactly Ground's own Y=0 and being fully occluded by it — the same
                // ground-occlusion hazard CLAUDE.md's Sprite & Art Convention already documents
                // for rings/unit art. Harmless for a mesh-based scatter instance (e.g. the Ore/
                // Gold placeholder capsules), which has real vertical extent already.
                scatterInstance.transform.localPosition = new Vector3(offset.x, ScatterYOffset, offset.y);
                _scatterObjects.Add(scatterInstance);
            }

            if (_definition.useTieredScatterArt) _currentTier = 0;
        }

        /// <summary>
        /// Destroys scattered objects from the tracked set until the number remaining
        /// matches the current remaining-yield fraction (rounded up, so at least one object
        /// stays visible until the node is fully exhausted and destroyed outright), then —
        /// for a node using tiered depletion art — swaps every still-visible instance to the
        /// tier matching the new remaining fraction, if that tier actually changed.
        /// </summary>
        private void UpdateScatterVisibility()
        {
            if (_scatterObjects == null || _scatterObjects.Count == 0) return;

            float remainingFraction = _definition.yieldAmount > 0 ? (float)_remainingYield / _definition.yieldAmount : 0f;
            int targetVisibleCount = Mathf.CeilToInt(remainingFraction * _definition.scatterObjectCount);

            while (_scatterObjects.Count > targetVisibleCount)
            {
                int lastIndex = _scatterObjects.Count - 1;
                GameObject scatterInstance = _scatterObjects[lastIndex];
                _scatterObjects.RemoveAt(lastIndex);
                if (scatterInstance != null) Destroy(scatterInstance);
            }

            if (_definition.useTieredScatterArt)
            {
                int newTier = ComputeTierIndex(remainingFraction);
                if (newTier != _currentTier)
                {
                    ApplyScatterTier(newTier);
                }
            }
        }

        /// <summary>
        /// Maps a remaining-yield fraction to a depletion tier index: 0 (high, at or above
        /// <see cref="ResourceNodeDefinition.tierThresholdHigh"/>), 1 (mid, at or above
        /// <see cref="ResourceNodeDefinition.tierThresholdLow"/>), or 2 (low, below that).
        /// </summary>
        /// <param name="remainingFraction">This node's current remaining yield, as a 0-1 fraction of its total.</param>
        private int ComputeTierIndex(float remainingFraction)
        {
            if (remainingFraction >= _definition.tierThresholdHigh) return 0;
            if (remainingFraction >= _definition.tierThresholdLow) return 1;
            return 2;
        }

        /// <summary>
        /// Returns the tier prefab for a <see cref="ComputeTierIndex"/> result.
        /// </summary>
        /// <param name="tierIndex">0 (high), 1 (mid), or 2 (low).</param>
        private GameObject GetTierPrefab(int tierIndex)
        {
            switch (tierIndex)
            {
                case 0: return _definition.tierPrefabHigh;
                case 1: return _definition.tierPrefabMid;
                default: return _definition.tierPrefabLow;
            }
        }

        /// <summary>
        /// Destroys and re-instantiates every currently-tracked scatter instance with the given
        /// tier's prefab, at each instance's existing local position — swapping the whole
        /// cluster's art together without re-randomizing layout. Only called when
        /// <see cref="ComputeTierIndex"/> actually changes (see <see cref="UpdateScatterVisibility"/>),
        /// never unconditionally, since this is a real Destroy+Instantiate cost per instance.
        /// </summary>
        /// <param name="tierIndex">The new tier (0=high, 1=mid, 2=low) to swap every instance to.</param>
        private void ApplyScatterTier(int tierIndex)
        {
            GameObject tierPrefab = GetTierPrefab(tierIndex);
            _currentTier = tierIndex;
            if (tierPrefab == null) return;

            for (int i = 0; i < _scatterObjects.Count; i++)
            {
                GameObject oldInstance = _scatterObjects[i];
                if (oldInstance == null) continue;

                Vector3 localPosition = oldInstance.transform.localPosition;
                Destroy(oldInstance);

                GameObject newInstance = Instantiate(tierPrefab, transform);
                newInstance.transform.localPosition = localPosition;
                _scatterObjects[i] = newInstance;
            }
        }

        /// <summary>
        /// Finds the currently-alive scattered instance nearest to <paramref name="fromPosition"/>,
        /// for a Harvester to travel to and gather from instead of this node's own center (issue
        /// #50). Falls back to this node's own <see cref="Position"/> with <paramref name="targetInstance"/>
        /// set to <c>null</c> if there are no scattered instances at all (e.g. no <see cref="ResourceNodeDefinition.scatterObjectPrefab"/>
        /// configured) — identical to the pre-#50 behavior, so a misconfigured node still works.
        /// </summary>
        /// <param name="fromPosition">The world position to measure distance from (typically the Harvester's own position).</param>
        /// <param name="targetInstance">The nearest live scattered instance found, or <c>null</c> if none exist.</param>
        public Vector3 GetNearestScatterTarget(Vector3 fromPosition, out GameObject targetInstance)
        {
            targetInstance = null;
            if (_scatterObjects == null || _scatterObjects.Count == 0) return transform.position;

            float nearestSqrDistance = float.MaxValue;
            foreach (var scatterInstance in _scatterObjects)
            {
                if (scatterInstance == null) continue;

                float sqrDistance = (scatterInstance.transform.position - fromPosition).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    targetInstance = scatterInstance;
                }
            }

            return targetInstance != null ? targetInstance.transform.position : transform.position;
        }

        /// <summary>
        /// Immediately empties this node's remaining yield.
        /// </summary>
        public void DrainAll()
        {
            _remainingYield = 0;
            Destroy(gameObject);
        }

        /// <summary>
        /// A sabotaged node has its remaining yield fully drained.
        /// </summary>
        /// <param name="effectConfig">Unused for nodes — the effect is absolute.</param>
        public void ApplySabotage(SabotageEffectConfig effectConfig)
        {
            DrainAll();
        }
    }
}
