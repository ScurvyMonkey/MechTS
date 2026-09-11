using System;
using MechTS.Core;
using MechTS.Economy;
using MechTS.Vision;
using UnityEngine;
using UnityEngine.Pool;

namespace MechTS.Units
{
    /// <summary>
    /// Auto-acquires and fires on the nearest enemy within range on a cooldown, applying
    /// damage directly via the target's <see cref="Health"/> component and spawning pooled
    /// bullet/casing-eject firing effects (see <see cref="BulletEffect"/>/
    /// <see cref="CasingEjectEffect"/>) if the owning <see cref="WeaponConfig"/> has them
    /// assigned. A new, independent component rather than a wrapper around the asset pack's
    /// Turret/Projectile classes — those are hardcoded to legacy-Input mouse-aim single-player
    /// control and have no damage-application hook, so they don't fit an auto-targeting RTS
    /// weapon (see the completion report for details). Targets are drawn from
    /// <see cref="UnitManager"/> and <see cref="EconomyManager"/>'s already-maintained
    /// registries rather than a scene-wide scan, so this stays safe to run every frame.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        [SerializeField] private WeaponConfig _config;
        [SerializeField] private Transform _muzzle;

        /// <summary>This weapon's tunable stats, for display (e.g. the unit detail panel).</summary>
        public WeaponConfig Config => _config;

        /// <summary>
        /// Raised every time this weapon actually fires a shot (issue #91) — the single choke
        /// point every shot passes through, <see cref="FireAt"/>. A weapon-fire-gated animator
        /// component subscribes to this to play a unit/building's real fire animation in sync
        /// with actual shots, rather than continuously or never.
        /// </summary>
        public event Action OnFired;

        private UnitManager _unitManager;
        private EconomyManager _economyManager;
        private AudioManager _audioManager;
        private TechManager _techManager;
        private VisionManager _visionManager;
        private UnitBase _ownerUnit;
        private BuildingInstance _ownerBuilding;
        private float _cooldownRemaining;
        private IObjectPool<GameObject> _bulletPool;
        private IObjectPool<GameObject> _casingPool;
        private Health _explicitTarget;

        /// <summary>
        /// Caches whichever owner (unit or building) this weapon is mounted on, and defaults
        /// the muzzle point to this transform if none was assigned in the Inspector.
        /// </summary>
        private void Awake()
        {
            _ownerUnit = GetComponent<UnitBase>();
            _ownerBuilding = GetComponent<BuildingInstance>();
            if (_muzzle == null) _muzzle = transform;
        }

        /// <summary>
        /// Caches the scene's <see cref="UnitManager"/> and <see cref="EconomyManager"/>, and
        /// builds the bullet/casing effect pools if this weapon's config has effect prefabs assigned.
        /// </summary>
        private void Start()
        {
            _unitManager = FindFirstObjectByType<UnitManager>();
            _economyManager = FindFirstObjectByType<EconomyManager>();
            _audioManager = FindFirstObjectByType<AudioManager>();
            _techManager = FindFirstObjectByType<TechManager>();
            _visionManager = FindFirstObjectByType<VisionManager>();

            _bulletPool = CreateEffectPool(_config.projectileEffectPrefab);
            _casingPool = CreateEffectPool(_config.casingEffectPrefab);
        }

        /// <summary>
        /// Creates a pool of the given effect prefab, or null if no prefab was assigned.
        /// </summary>
        /// <param name="prefab">The effect prefab to pool instances of.</param>
        private IObjectPool<GameObject> CreateEffectPool(GameObject prefab)
        {
            if (prefab == null) return null;

            return new ObjectPool<GameObject>(
                () => Instantiate(prefab),
                instance => instance.SetActive(true),
                instance => instance.SetActive(false),
                instance => Destroy(instance),
                defaultCapacity: 8);
        }

        /// <summary>
        /// This weapon's owning faction, resolved from whichever owner component is present.
        /// </summary>
        private Faction OwnerFaction => _ownerUnit != null ? _ownerUnit.Faction : _ownerBuilding.Faction;

        /// <summary>
        /// Returns the current researched multiplier for the given stat, for this weapon's
        /// owning faction. Read live at every point of use (fire, target-acquisition range,
        /// cooldown) rather than cached, since research can happen mid-round.
        /// </summary>
        /// <param name="statType">The stat to query.</param>
        private float GetStatMultiplier(UpgradeStatType statType)
        {
            if (_techManager == null) return 1f;
            var callerCapabilities = _ownerUnit != null ? _ownerUnit.Capabilities : UnitCapability.None;
            return _techManager.GetStatMultiplier(OwnerFaction, statType, callerCapabilities);
        }

        /// <summary>
        /// Maps a damage type to its offense-side stat (issue #55) — applies alongside, not
        /// instead of, the generic <see cref="UpgradeStatType.Damage"/> stat.
        /// </summary>
        /// <param name="damageType">The weapon's damage type.</param>
        private static UpgradeStatType DamageTypeStat(DamageType damageType)
        {
            return damageType == DamageType.Plasma ? UpgradeStatType.PlasmaDamage : UpgradeStatType.PhysicalDamage;
        }

        /// <summary>
        /// Locks this weapon onto a specific target (issue #31), overriding the normal
        /// nearest-in-range auto-acquire until it dies or a new order clears the lock (see
        /// <see cref="UnitManager.MoveSelectedTo"/>/<see cref="UnitManager.StopSelected"/>).
        /// A locked target that moves out of range is simply not engaged — no chase.
        /// </summary>
        /// <param name="target">The enemy Health component to lock onto.</param>
        public void SetExplicitTarget(Health target)
        {
            _explicitTarget = target;
        }

        /// <summary>
        /// Clears this weapon's explicit target lock, reverting to normal nearest-in-range
        /// auto-acquire.
        /// </summary>
        public void ClearExplicitTarget()
        {
            _explicitTarget = null;
        }

        /// <summary>
        /// Ticks the fire cooldown and engages this frame's resolved target, if any.
        /// </summary>
        private void Update()
        {
            _cooldownRemaining -= Time.deltaTime;

            var target = ResolveTarget();
            if (target == null) return;

            AimAt(target.transform.position);

            if (_cooldownRemaining <= 0f)
            {
                FireAt(target);
                float fireRate = _config.fireRate * GetStatMultiplier(UpgradeStatType.FireRate);
                _cooldownRemaining = 1f / Mathf.Max(fireRate, 0.01f);
            }
        }

        /// <summary>
        /// Resolves this frame's target: the explicit lock (issue #31) if one is set and its
        /// target is still alive — in range and currently visible to this weapon's faction
        /// only (issue #37), no nearest-in-range fallback while locked — clearing the lock
        /// once its target dies and falling back to <see cref="AcquireNearestEnemy"/>
        /// otherwise. A locked target that leaves vision is simply not engaged (same as
        /// leaving range), not un-locked — it re-engages automatically once back in sight.
        /// </summary>
        private Health ResolveTarget()
        {
            if (_explicitTarget != null)
            {
                if (_explicitTarget.IsDead)
                {
                    _explicitTarget = null;
                }
                else
                {
                    float range = _config.range * GetStatMultiplier(UpgradeStatType.Range);
                    float distance = Vector3.Distance(transform.position, _explicitTarget.transform.position);
                    bool inRange = distance <= range;
                    bool visible = _visionManager == null || _visionManager.IsVisible(OwnerFaction, _explicitTarget.transform.position);
                    return inRange && visible ? _explicitTarget : null;
                }
            }

            return AcquireNearestEnemy();
        }

        /// <summary>
        /// Yaws this weapon's transform to face the target, constrained to the ground plane
        /// (world Y flattened out) so it can never tip the unit out of the flat top-down
        /// orientation its sprite art depends on. A real bug fixed here: the previous
        /// `transform.up = direction` assignment pointed the whole unit's vertical axis at a
        /// roughly co-planar target, tipping it onto its side — invisible against the old
        /// placeholder capsule, but visibly wrong once real flat art (and the equally-tipped
        /// selection-ring cylinder) replaced it.
        /// </summary>
        /// <param name="targetPosition">The world position to face.</param>
        private void AimAt(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        /// <summary>
        /// Raises <see cref="OnFired"/>, applies damage, plays the firing sound, and spawns
        /// this shot's firing effects (bullet flight, casing eject), if their pools were built.
        /// </summary>
        /// <param name="target">The target being hit.</param>
        private void FireAt(Health target)
        {
            OnFired?.Invoke();

            float damageTypeMultiplier = GetStatMultiplier(DamageTypeStat(_config.damageType));
            target.Damage(_config.damage * GetStatMultiplier(UpgradeStatType.Damage) * damageTypeMultiplier, gameObject, _config.damageType);

            if (_audioManager != null) _audioManager.PlaySfx(_config.fireSound, _muzzle.position, SfxCategory.Weapon);

            if (_bulletPool != null)
            {
                var bullet = _bulletPool.Get();
                bullet.GetComponent<BulletEffect>().Fire(_muzzle.position, target.transform.position, _bulletPool, _audioManager, _config.impactSound);
            }

            if (_casingPool != null)
            {
                var casing = _casingPool.Get();
                casing.GetComponent<CasingEjectEffect>().Eject(_muzzle.position, transform.up, _casingPool);
            }
        }

        /// <summary>
        /// Finds the nearest enemy <see cref="Health"/> within range, checking the opposing
        /// faction's registered units, buildings, and main building (issue #28 — previously
        /// permanently invulnerable per issue #20) rather than scanning the whole scene.
        /// Skips a candidate whose flying state doesn't match this weapon's
        /// <see cref="WeaponConfig.targetType"/> (issue #30) — buildings/main buildings are
        /// always treated as ground targets.
        /// </summary>
        private Health AcquireNearestEnemy()
        {
            Faction enemyFaction = OwnerFaction == Faction.Player ? Faction.Enemy : Faction.Player;

            Health nearest = null;
            float nearestDistance = _config.range * GetStatMultiplier(UpgradeStatType.Range);

            if (_unitManager != null)
            {
                foreach (var unit in _unitManager.ActiveUnits)
                {
                    if (unit.Faction != enemyFaction) continue;
                    TryConsiderTarget(unit.HealthComponent, unit.IsFlying, ref nearest, ref nearestDistance);
                }
            }

            var enemyState = _economyManager != null ? _economyManager.GetState(enemyFaction) : null;
            if (enemyState != null)
            {
                foreach (var building in enemyState.Buildings)
                {
                    TryConsiderTarget(building.HealthComponent, false, ref nearest, ref nearestDistance);
                }
            }

            if (_economyManager != null && _economyManager.MainBuildings.TryGetValue(enemyFaction, out var mainBuilding) && mainBuilding != null)
            {
                TryConsiderTarget(mainBuilding.HealthComponent, false, ref nearest, ref nearestDistance);
            }

            return nearest;
        }

        /// <summary>
        /// Updates the nearest-target tracking if the candidate is alive, matches this
        /// weapon's allowed target type (issue #30), is currently visible to this weapon's
        /// faction (issue #37 — a nearer hidden enemy never blocks a farther visible one from
        /// being found, since this check happens per-candidate, not as a final gate on
        /// whatever's nearest overall), and is closer than the current best.
        /// </summary>
        /// <param name="candidate">The candidate target's Health component, if any.</param>
        /// <param name="isFlying">Whether the candidate is a flying unit.</param>
        /// <param name="nearest">The current nearest target, updated in place.</param>
        /// <param name="nearestDistance">The current nearest distance, updated in place.</param>
        private void TryConsiderTarget(Health candidate, bool isFlying, ref Health nearest, ref float nearestDistance)
        {
            if (candidate == null || candidate.IsDead) return;
            if (_config.targetType == WeaponTargetType.Ground && isFlying) return;
            if (_config.targetType == WeaponTargetType.Air && !isFlying) return;
            if (_visionManager != null && !_visionManager.IsVisible(OwnerFaction, candidate.transform.position)) return;

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance <= nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }
    }
}
