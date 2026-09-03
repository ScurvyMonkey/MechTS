using System;
using System.Collections.Generic;
using MechTS.Core;
using MechTS.Units;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Tracks each faction's researched tech-tree upgrades, gated to the Economy Round and
    /// reset every time a fresh one begins (per-mission, never persisted across the
    /// campaign). Created by <see cref="Bootstrapper"/>.
    /// </summary>
    public class TechManager : MonoBehaviour
    {
        private TechTreeConfig _config;
        private GameManager _gameManager;
        private EconomyManager _economyManager;

        /// <summary>
        /// Raised after a faction successfully researches any upgrade — lets stat-consuming
        /// components (<see cref="Units.UnitBase"/>, <see cref="Units.Health"/>) that bake a
        /// value in once at spawn (move speed, max health) re-apply it live instead of
        /// polling every frame. Components only affected by stats already read live every
        /// frame/use (<see cref="Units.Weapon"/>, <see cref="HarvesterGatherBehavior"/>'s
        /// gather tick) don't need to subscribe to this at all.
        /// </summary>
        public event Action<Faction> OnUpgradeResearched;

        private readonly Dictionary<Faction, HashSet<UpgradeDefinition>> _researched = new Dictionary<Faction, HashSet<UpgradeDefinition>>();
        private readonly Dictionary<(Faction, UpgradeStatType), List<(ModifierType type, float value, UnitCapability requiredCapabilities)>> _statModifiers = new Dictionary<(Faction, UpgradeStatType), List<(ModifierType, float, UnitCapability)>>();
        private readonly Dictionary<Faction, HashSet<BuildingDefinition>> _unlockedBuildings = new Dictionary<Faction, HashSet<BuildingDefinition>>();
        private readonly Dictionary<Faction, HashSet<UnitProductionDefinition>> _unlockedUnits = new Dictionary<Faction, HashSet<UnitProductionDefinition>>();

        /// <summary>
        /// Assigns this mission's tech tree config. Called by <see cref="Bootstrapper"/>
        /// immediately after creation, since it's instantiated at runtime rather than from
        /// a prefab with an Inspector-assigned reference.
        /// </summary>
        /// <param name="config">The mission's per-faction tech trees.</param>
        public void Initialize(TechTreeConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Caches manager references and subscribes to round-state changes.
        /// </summary>
        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _economyManager = FindFirstObjectByType<EconomyManager>();
            _gameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        /// <summary>
        /// Unsubscribes from the <see cref="GameManager"/>.
        /// </summary>
        private void OnDestroy()
        {
            if (_gameManager != null) _gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        /// <summary>
        /// Resets every faction's researched upgrades and derived state on every fresh
        /// Economy Round entry, mirroring <see cref="EconomyManager"/>'s own per-mission reset.
        /// </summary>
        /// <param name="newState">The state the game just transitioned into.</param>
        private void HandleGameStateChanged(GameState newState)
        {
            if (newState != GameState.EconomyRound) return;

            _researched.Clear();
            _statModifiers.Clear();
            _unlockedBuildings.Clear();
            _unlockedUnits.Clear();
        }

        /// <summary>
        /// Returns the given faction's available upgrades this mission, or an empty list if
        /// no tree is configured for that faction.
        /// </summary>
        /// <param name="faction">The faction to look up.</param>
        public IReadOnlyList<UpgradeDefinition> GetTree(Faction faction)
        {
            if (_config?.factionTrees == null) return System.Array.Empty<UpgradeDefinition>();

            foreach (var tree in _config.factionTrees)
            {
                if (tree.faction == faction) return tree.availableUpgrades ?? System.Array.Empty<UpgradeDefinition>();
            }
            return System.Array.Empty<UpgradeDefinition>();
        }

        /// <summary>
        /// Whether the given faction has already researched this upgrade.
        /// </summary>
        /// <param name="faction">The faction to check.</param>
        /// <param name="upgrade">The upgrade to check.</param>
        public bool IsResearched(Faction faction, UpgradeDefinition upgrade)
        {
            return _researched.TryGetValue(faction, out var set) && set.Contains(upgrade);
        }

        /// <summary>
        /// Whether the given faction currently owns a building that enables research (issue
        /// #45 — the Technology Plant), checked by <see cref="CanResearch"/> and read directly
        /// by <see cref="UI.TechPanel"/> to show *why* every entry is locked, distinct from a
        /// per-upgrade "can't afford it yet" reason.
        /// </summary>
        /// <param name="faction">The faction to check.</param>
        public bool HasResearchBuilding(Faction faction)
        {
            return _economyManager != null && _economyManager.FactionOwnsBuildingWhere(faction, d => d.enablesResearch);
        }

        /// <summary>
        /// Whether the given faction can currently research this upgrade — owns a building
        /// that enables research (issue #45), not already researched, every prerequisite
        /// already researched, and affordable.
        /// </summary>
        /// <param name="faction">The faction to check.</param>
        /// <param name="upgrade">The upgrade to check.</param>
        /// <param name="useLockedPool">
        /// Whether to check affordability against the faction's locked pool (issue #53 — the
        /// Injection Round) instead of its normal stockpile. Defaults to false, preserving
        /// every existing caller's behavior unchanged.
        /// </param>
        public bool CanResearch(Faction faction, UpgradeDefinition upgrade, bool useLockedPool = false)
        {
            if (upgrade == null || IsResearched(faction, upgrade)) return false;
            if (!HasResearchBuilding(faction)) return false;

            if (upgrade.prerequisites != null)
            {
                foreach (var prerequisite in upgrade.prerequisites)
                {
                    if (!IsResearched(faction, prerequisite)) return false;
                }
            }

            var state = _economyManager != null ? _economyManager.GetState(faction) : null;
            if (state == null) return false;

            return useLockedPool ? state.LockedPool.CanAfford(upgrade.GetCost()) : state.Stockpile.CanAfford(upgrade.GetCost());
        }

        /// <summary>
        /// Researches the given upgrade for the given faction: spends its cost, marks it
        /// researched, and applies its effect. No-op (returns false) if
        /// <see cref="CanResearch"/> would return false.
        /// </summary>
        /// <param name="faction">The researching faction.</param>
        /// <param name="upgrade">The upgrade to research.</param>
        /// <param name="useLockedPool">
        /// Whether to spend from the faction's locked pool (issue #53 — the Injection Round)
        /// instead of its normal stockpile. Defaults to false, preserving every existing
        /// caller's behavior unchanged.
        /// </param>
        public bool TryResearch(Faction faction, UpgradeDefinition upgrade, bool useLockedPool = false)
        {
            if (!CanResearch(faction, upgrade, useLockedPool)) return false;

            var state = _economyManager.GetState(faction);
            if (useLockedPool) state.LockedPool.Spend(upgrade.GetCost());
            else state.Stockpile.Spend(upgrade.GetCost());

            if (!_researched.TryGetValue(faction, out var set))
            {
                set = new HashSet<UpgradeDefinition>();
                _researched[faction] = set;
            }
            set.Add(upgrade);

            upgrade.Apply(faction, this);
            OnUpgradeResearched?.Invoke(faction);
            return true;
        }

        /// <summary>
        /// Reverts an already-researched, swappable upgrade for the given faction (issue #54
        /// — the Injection Round's Battle-Round tech-pivot mechanic): unmarks it researched,
        /// undoes its effect via <see cref="UpgradeDefinition.Revert"/>, and re-fires
        /// <see cref="OnUpgradeResearched"/> so live-reapplying stats (MoveSpeed/Health)
        /// resync downward correctly. No-op (returns false) if the upgrade isn't currently
        /// researched or isn't marked <see cref="UpgradeDefinition.isSwappable"/>. Reverting
        /// costs nothing — the original research spend is a pure sunk cost.
        /// </summary>
        /// <param name="faction">The faction reverting the upgrade.</param>
        /// <param name="upgrade">The upgrade to revert.</param>
        public bool TryRevert(Faction faction, UpgradeDefinition upgrade)
        {
            if (upgrade == null || !upgrade.isSwappable || !IsResearched(faction, upgrade)) return false;

            _researched[faction].Remove(upgrade);
            upgrade.Revert(faction, this);
            OnUpgradeResearched?.Invoke(faction);
            return true;
        }

        /// <summary>
        /// Registers a stat modifier for a faction, called by
        /// <see cref="StatUpgradeDefinition.Apply"/> on research.
        /// </summary>
        /// <param name="faction">The faction the modifier applies to.</param>
        /// <param name="statType">The stat being modified.</param>
        /// <param name="modifierType">Whether <paramref name="value"/> is a flat or percentage modifier.</param>
        /// <param name="value">The modifier's value.</param>
        /// <param name="requiredCapabilities">
        /// Which units this modifier applies to (issue #39) — <see cref="UnitCapability.None"/>
        /// (the default) means unfiltered, applying to every unit regardless of capability,
        /// matching this method's original behavior exactly.
        /// </param>
        public void RegisterStatModifier(Faction faction, UpgradeStatType statType, ModifierType modifierType, float value, UnitCapability requiredCapabilities = UnitCapability.None)
        {
            var key = (faction, statType);
            if (!_statModifiers.TryGetValue(key, out var list))
            {
                list = new List<(ModifierType, float, UnitCapability)>();
                _statModifiers[key] = list;
            }
            list.Add((modifierType, value, requiredCapabilities));
        }

        /// <summary>
        /// Removes exactly one stat modifier matching the given values (issue #54), called by
        /// <see cref="StatUpgradeDefinition.Revert"/> — the reverting upgrade already knows
        /// precisely what it registered, so this is a value-based removal, not a lookup by
        /// upgrade identity. Two upgrades registering an identical value tuple would be
        /// indistinguishable here, but also produce an identical computed multiplier, so
        /// removing either is harmless.
        /// </summary>
        /// <param name="faction">The faction the modifier applied to.</param>
        /// <param name="statType">The stat the modifier affected.</param>
        /// <param name="modifierType">Whether the modifier was flat or percentage.</param>
        /// <param name="value">The modifier's value.</param>
        /// <param name="requiredCapabilities">The modifier's capability filter, as originally registered.</param>
        public void RemoveStatModifier(Faction faction, UpgradeStatType statType, ModifierType modifierType, float value, UnitCapability requiredCapabilities = UnitCapability.None)
        {
            if (_statModifiers.TryGetValue((faction, statType), out var list))
            {
                list.Remove((modifierType, value, requiredCapabilities));
            }
        }

        /// <summary>
        /// Returns the combined multiplier for a faction/stat from every researched
        /// <see cref="StatUpgradeDefinition"/> affecting it: every Flat value is summed and
        /// added to a baseline of 1 first, then every Percentage value is summed and applied
        /// as a second, compounding multiplier — e.g. two Flat +0.1 modifiers and one
        /// Percentage +0.2 modifier yield <c>(1 + 0.1 + 0.1) * (1 + 0.2) = 1.44</c>. Returns
        /// 1 (no-op) if nothing is researched for that faction/stat.
        /// </summary>
        /// <param name="faction">The faction to query.</param>
        /// <param name="statType">The stat to query.</param>
        /// <param name="callerCapabilities">
        /// The querying unit's own capabilities (issue #39, <see cref="Units.UnitBase.Capabilities"/>)
        /// — a registered modifier only counts if <paramref name="callerCapabilities"/> contains
        /// every bit set in that modifier's own <c>requiredCapabilities</c>. A modifier
        /// registered with <see cref="UnitCapability.None"/> always counts, regardless of what's
        /// passed here — callers with no capability profile (buildings) can safely pass the
        /// default <see cref="UnitCapability.None"/> and still receive every unfiltered modifier.
        /// </param>
        public float GetStatMultiplier(Faction faction, UpgradeStatType statType, UnitCapability callerCapabilities = UnitCapability.None)
        {
            if (!_statModifiers.TryGetValue((faction, statType), out var list) || list.Count == 0) return 1f;

            float flatSum = 0f;
            float percentSum = 0f;
            foreach (var (modifierType, value, requiredCapabilities) in list)
            {
                if ((callerCapabilities & requiredCapabilities) != requiredCapabilities) continue;

                if (modifierType == ModifierType.Flat) flatSum += value;
                else percentSum += value;
            }

            return (1f + flatSum) * (1f + percentSum);
        }

        /// <summary>
        /// Registers a building as unlocked for a faction, called by
        /// <see cref="UnlockUpgradeDefinition.Apply"/> on research.
        /// </summary>
        /// <param name="faction">The faction the unlock applies to.</param>
        /// <param name="building">The building being unlocked.</param>
        public void RegisterUnlockedBuilding(Faction faction, BuildingDefinition building)
        {
            if (!_unlockedBuildings.TryGetValue(faction, out var set))
            {
                set = new HashSet<BuildingDefinition>();
                _unlockedBuildings[faction] = set;
            }
            set.Add(building);
        }

        /// <summary>
        /// Removes a building's unlocked status for a faction, called by
        /// <see cref="UnlockUpgradeDefinition.Revert"/> (issue #54).
        /// </summary>
        /// <param name="faction">The faction the unlock applied to.</param>
        /// <param name="building">The building to re-lock.</param>
        public void DeregisterUnlockedBuilding(Faction faction, BuildingDefinition building)
        {
            if (_unlockedBuildings.TryGetValue(faction, out var set)) set.Remove(building);
        }

        /// <summary>
        /// Registers a unit as unlocked for a faction, called by
        /// <see cref="UnlockUpgradeDefinition.Apply"/> on research.
        /// </summary>
        /// <param name="faction">The faction the unlock applies to.</param>
        /// <param name="unit">The unit being unlocked.</param>
        public void RegisterUnlockedUnit(Faction faction, UnitProductionDefinition unit)
        {
            if (!_unlockedUnits.TryGetValue(faction, out var set))
            {
                set = new HashSet<UnitProductionDefinition>();
                _unlockedUnits[faction] = set;
            }
            set.Add(unit);
        }

        /// <summary>
        /// Removes a unit's unlocked status for a faction, called by
        /// <see cref="UnlockUpgradeDefinition.Revert"/> (issue #54).
        /// </summary>
        /// <param name="faction">The faction the unlock applied to.</param>
        /// <param name="unit">The unit to re-lock.</param>
        public void DeregisterUnlockedUnit(Faction faction, UnitProductionDefinition unit)
        {
            if (_unlockedUnits.TryGetValue(faction, out var set)) set.Remove(unit);
        }

        /// <summary>
        /// Whether the given building is available to the given faction: true if no
        /// upgrade in any faction's tree gates it at all (backward compatible with every
        /// building that predates the tech tree), otherwise true only once that faction has
        /// researched the gating upgrade.
        /// </summary>
        /// <param name="faction">The faction to check.</param>
        /// <param name="building">The building to check.</param>
        public bool IsUnlocked(Faction faction, BuildingDefinition building)
        {
            if (_unlockedBuildings.TryGetValue(faction, out var set) && set.Contains(building)) return true;
            return !IsGatedByAnyUpgrade(building);
        }

        /// <summary>
        /// Whether the given unit is available to the given faction: true if no upgrade in
        /// any faction's tree gates it at all (backward compatible with every unit that
        /// predates the tech tree), otherwise true only once that faction has researched the
        /// gating upgrade.
        /// </summary>
        /// <param name="faction">The faction to check.</param>
        /// <param name="unit">The unit to check.</param>
        public bool IsUnlocked(Faction faction, UnitProductionDefinition unit)
        {
            if (_unlockedUnits.TryGetValue(faction, out var set) && set.Contains(unit)) return true;
            return !IsGatedByAnyUpgrade(unit);
        }

        /// <summary>
        /// Whether any <see cref="UnlockUpgradeDefinition"/> in any faction's tree gates this building.
        /// </summary>
        /// <param name="building">The building to check.</param>
        private bool IsGatedByAnyUpgrade(BuildingDefinition building)
        {
            if (_config?.factionTrees == null) return false;

            foreach (var tree in _config.factionTrees)
            {
                if (tree.availableUpgrades == null) continue;
                foreach (var upgrade in tree.availableUpgrades)
                {
                    if (upgrade is UnlockUpgradeDefinition unlock && unlock.unlockedBuilding == building) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether any <see cref="UnlockUpgradeDefinition"/> in any faction's tree gates this unit.
        /// </summary>
        /// <param name="unit">The unit to check.</param>
        private bool IsGatedByAnyUpgrade(UnitProductionDefinition unit)
        {
            if (_config?.factionTrees == null) return false;

            foreach (var tree in _config.factionTrees)
            {
                if (tree.availableUpgrades == null) continue;
                foreach (var upgrade in tree.availableUpgrades)
                {
                    if (upgrade is UnlockUpgradeDefinition unlock && unlock.unlockedUnit == unit) return true;
                }
            }
            return false;
        }
    }
}
