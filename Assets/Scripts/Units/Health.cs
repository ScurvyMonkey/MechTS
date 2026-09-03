using System;
using MechTS.Core;
using MechTS.Economy;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// Tracks current/max health for a unit or building and raises an event on death.
    /// Destroys its own GameObject once health reaches zero. Resolves its owner's faction
    /// the same way <see cref="Weapon"/> does (checks every possible owner type — including
    /// <see cref="MainBuilding"/> as of issue #28), so it can look up a per-faction Health
    /// stat multiplier from <see cref="TechManager"/>.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] private float _maxHealth = 50f;

        private float _baseMaxHealth;
        private UnitBase _ownerUnit;
        private BuildingInstance _ownerBuilding;
        private MainBuilding _ownerMainBuilding;
        private TechManager _techManager;

        /// <summary>Raised when this entity's health reaches zero.</summary>
        public event Action OnDeath;

        /// <summary>This entity's current health.</summary>
        public float CurrentHealth { get; private set; }

        /// <summary>This entity's maximum health, after any researched Health stat multiplier.</summary>
        public float MaxHealth => _maxHealth;

        /// <summary>Whether this entity has already died.</summary>
        public bool IsDead { get; private set; }

        /// <summary>
        /// This entity's owning faction, resolved from whichever owner component is
        /// present — mirrors <see cref="Weapon.OwnerFaction"/> exactly. Defaults to
        /// <see cref="Faction.Player"/> if no owner component exists (shouldn't happen
        /// in practice; every unit/building/main building has exactly one). Read live
        /// (not cached at <see cref="Awake"/> time) since <see cref="MainBuilding.Faction"/>
        /// is set via its own <c>Initialize()</c> call, which runs after every sibling's
        /// <see cref="Awake"/> — caching this getter's result would race that assignment.
        /// </summary>
        public Faction OwnerFaction => _ownerUnit != null ? _ownerUnit.Faction
            : _ownerBuilding != null ? _ownerBuilding.Faction
            : _ownerMainBuilding != null ? _ownerMainBuilding.Faction
            : Faction.Player;

        /// <summary>
        /// Sets current health to max, and caches whichever owner component is present.
        /// </summary>
        private void Awake()
        {
            CurrentHealth = _maxHealth;
            _baseMaxHealth = _maxHealth;
            _ownerUnit = GetComponent<UnitBase>();
            _ownerBuilding = GetComponent<BuildingInstance>();
            _ownerMainBuilding = GetComponent<MainBuilding>();
        }

        /// <summary>
        /// Caches the scene's <see cref="TechManager"/> and subscribes to research events so
        /// a Health upgrade researched mid-round applies immediately, not just to units
        /// spawned afterward.
        /// </summary>
        private void Start()
        {
            _techManager = FindFirstObjectByType<TechManager>();
            if (_techManager != null) _techManager.OnUpgradeResearched += HandleUpgradeResearched;
        }

        /// <summary>
        /// Unsubscribes from the <see cref="TechManager"/>.
        /// </summary>
        private void OnDestroy()
        {
            if (_techManager != null) _techManager.OnUpgradeResearched -= HandleUpgradeResearched;
        }

        /// <summary>
        /// Re-applies the Health stat multiplier if the researching faction owns this entity.
        /// </summary>
        /// <param name="faction">The faction that just researched an upgrade.</param>
        private void HandleUpgradeResearched(Faction faction)
        {
            if (faction != OwnerFaction) return;
            ApplyHealthMultiplier();
        }

        /// <summary>
        /// Recomputes <see cref="MaxHealth"/> from <see cref="_baseMaxHealth"/> and the
        /// current Health stat multiplier, granting any positive increase directly to
        /// <see cref="CurrentHealth"/> as a bonus (rather than a full heal) so an already-
        /// damaged unit doesn't get topped off by an unrelated research.
        /// </summary>
        private void ApplyHealthMultiplier()
        {
            var callerCapabilities = _ownerUnit != null ? _ownerUnit.Capabilities : UnitCapability.None;
            float multiplier = _techManager != null ? _techManager.GetStatMultiplier(OwnerFaction, UpgradeStatType.Health, callerCapabilities) : 1f;
            float newMax = _baseMaxHealth * multiplier;
            float delta = newMax - _maxHealth;

            _maxHealth = newMax;
            if (delta > 0f) CurrentHealth = Mathf.Min(CurrentHealth + delta, _maxHealth);
        }

        /// <summary>
        /// Applies damage — scaled down by this entity's owning faction's researched
        /// resistance to <paramref name="damageType"/> (issue #55), floored at 0 so stacked/
        /// over-100% resistance never heals the target — raising <see cref="OnDeath"/> and
        /// destroying this GameObject once health reaches zero. No-ops if already dead.
        /// </summary>
        /// <param name="amount">The amount of damage to apply, before resistance.</param>
        /// <param name="source">The GameObject responsible for the damage, for future attribution.</param>
        /// <param name="damageType">The incoming attack's damage type.</param>
        public void Damage(float amount, GameObject source, DamageType damageType)
        {
            if (IsDead) return;

            var callerCapabilities = _ownerUnit != null ? _ownerUnit.Capabilities : UnitCapability.None;
            float resistanceMultiplier = _techManager != null
                ? _techManager.GetStatMultiplier(OwnerFaction, ResistanceStat(damageType), callerCapabilities)
                : 1f;
            float finalAmount = Mathf.Max(0f, amount * resistanceMultiplier);

            ApplyDamage(finalAmount);
        }

        /// <summary>
        /// Kills this entity outright, bypassing damage-type resistance entirely (issue #55)
        /// — for callers whose effect is meant to be absolute regardless of any researched
        /// resistance (e.g. <see cref="HarvesterUnit.ApplySabotage"/>'s self-destruct).
        /// A plain large <see cref="Damage"/> call would be silently no-op'd by 100%+
        /// resistance (any amount times a zero multiplier is zero); this isn't.
        /// </summary>
        /// <param name="source">The GameObject responsible for the kill, for future attribution.</param>
        public void Kill(GameObject source)
        {
            if (IsDead) return;
            ApplyDamage(CurrentHealth);
        }

        /// <summary>
        /// Maps a damage type to the resistance stat that reduces it (issue #55).
        /// </summary>
        /// <param name="damageType">The incoming attack's damage type.</param>
        private static UpgradeStatType ResistanceStat(DamageType damageType)
        {
            return damageType == DamageType.Plasma ? UpgradeStatType.PlasmaResistance : UpgradeStatType.PhysicalResistance;
        }

        /// <summary>
        /// The shared death path: subtracts already-resistance-adjusted damage and destroys
        /// this GameObject once health reaches zero.
        /// </summary>
        /// <param name="amount">The final amount to subtract from <see cref="CurrentHealth"/>.</param>
        private void ApplyDamage(float amount)
        {
            CurrentHealth -= amount;
            if (CurrentHealth <= 0f)
            {
                IsDead = true;
                OnDeath?.Invoke();
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Restores health, clamped to <see cref="MaxHealth"/>. Used by
        /// <see cref="Economy.CrewmanRepairBehavior"/> (issue #42) — the first repair/heal
        /// mechanic in the codebase. No-ops if already dead (a dead entity's GameObject is
        /// already destroyed by <see cref="Damage"/>, so this is mostly a defensive guard).
        /// </summary>
        /// <param name="amount">The amount of health to restore.</param>
        public void Heal(float amount)
        {
            if (IsDead) return;
            CurrentHealth = Mathf.Min(CurrentHealth + amount, _maxHealth);
        }

        /// <summary>
        /// Sets this entity's base max health, applies the current Health stat multiplier,
        /// and fully heals to the result. Used during initialization from a unit/building's
        /// config data.
        /// </summary>
        /// <param name="maxHealth">The new base max health value, before any stat multiplier.</param>
        public void SetMaxHealth(float maxHealth)
        {
            _baseMaxHealth = maxHealth;
            ApplyHealthMultiplier();
            CurrentHealth = _maxHealth;
        }
    }
}
