# CLAUDE.md — MechTS

## Project Overview

**MechTS** is a Unity 6 top-down Action RTS. The core structural idea (not yet fully specced — see Current Phase below): rather than the classic real-time-strategy demand of running economy and combat simultaneously under high APM, a match/mission is broken into distinct **Economy Rounds** (gather resources, build, produce/upgrade units) and **Battle Rounds** (deploy and fight with what the economy round produced). The project will ship a **Campaign** first; a **Versus** (PvP) mode using the same round structure is planned for a later phase, explicitly deferred.

Built with C# and Universal Render Pipeline (URP), 2D Renderer, Unity 6 (`6000.3.11f1`). The project imports the **TopDownAssets FuturisticPack** — a top-down sci-fi/mech asset pack (see [Asset Roster](#asset-roster-topdownassets) below) whose unit variety (harvester, APC, drones, turrets, mechs, power armor, aircraft, aliens-as-enemy-faction) is a large part of what suggested the RTS direction in the first place.

**Repo:** `ScurvyMonkey/MechTS` (GitHub — hosts both code and the Issues backlog)

---

## Current Phase: Phase 1 — Campaign (Pre-Production)

Phase 1's own sequencing:

- **Project scaffolding** — done. Repo/GitHub setup, `.claude/skills/` dev pipeline, this file, `docs/GDD.md`.
- **Core loop design (Economy Round / Battle Round)** — decided and specced. See `docs/GDD.md`'s "What's Actually Decided" for the full mechanic list.
- **Core loop implementation** — code-complete across GitHub issues #1–6 (Game State Machine, Economy Round resources/production, Espionage, Unit Selection & Command, Combat Resolution, Battle Round victory conditions). Written without `unity-mcp` available in that session, so **Play Mode verification, scene wiring (prefabs, ScriptableObject assets, layer masks, the `Bootstrapper` GameObject), and a compile check are still pending** — see each issue's GitHub comment for exact scene-wiring steps before treating it as done. Do not build further systems on top of this without confirming it actually compiles and runs first.
- Everything else in Phase 1 (unit roster beyond Harvester/Saboteur, campaign mission structure, enemy AI decision-making, audio, minimap) is **not started** and depends on the core loop above. `CameraManager` shipped 2026-08-19 (issue #10). Enemy AI decision-making began 2026-09-01 (issue #79, `EnemyAIController`) — Economy Round Harvester production/gathering is now AI-driven; espionage, build/repair, and Battle Round combat decision-making are still unbuilt, staggered one unit type at a time behind each unit's own validated player-controlled behavior.

**Phase 1 is the floor for the Campaign** — Versus mode (Phase 2) reuses whatever Phase 1 establishes for the round structure; it is not a separate game.

---

## Roadmap

| Phase | Adds |
|---|---|
| **1 — Campaign** (current) | Economy Round / Battle Round core loop, resource/production systems, unit roster, campaign missions + story, single-player only |
| **2 — Versus** (explicitly "way down the road" per the designer) | PvP using the same Economy/Battle round structure — matchmaking/lobby, balance pass, versus-specific UI. Do not build anything Versus-specific until Phase 1 has a working, played campaign loop. |

If a spec or a piece of work seems to require Phase 2 (networking, matchmaking, PvP balance), stop and flag it rather than building a placeholder for it — same discipline as the studio's other Unity project.

---

## Architecture

The core loop (issues #1–6) is code-complete as of 2026-08-17, pending Play Mode verification (see Current Phase above) — this section now describes the real shipped shape, not an anticipated one. `UIManager` (issue #7, In-Round HUD; issue #8, Mission End Screen; issue #9, Main Menu) shipped 2026-08-18 — all three UI issues from that pass are now complete. `CameraManager` (issue #10), `AudioManager` (issue #11), and the Minimap (issue #12) shipped 2026-08-19.

### Manager Hierarchy (shipped)

All managers are singletons created by `Bootstrapper.cs` via `DontDestroyOnLoad`. `Bootstrapper` instantiates each on a plain runtime `GameObject` (`new GameObject().AddComponent<T>()`) rather than from a prefab, since none currently need Inspector-serialized references beyond what `Bootstrapper` itself injects post-creation (e.g. `EconomyManager.Initialize(MissionEconomyConfig)`). **Do not create singletons outside this pattern.**

```
Bootstrapper
├── GameManager    — State machine: MainMenu, EconomyRound, BattleRound, MissionComplete, GameOver.
│                    event Action<GameState> OnGameStateChanged. Explicit transition methods
│                    (StartEconomyRound/StartBattleRound/CompleteMission/TriggerGameOver/ReturnToMainMenu),
│                    illegal transitions rejected + logged.
├── UnitManager    — Registry of active units, current selection, numbered control groups (0-9).
├── EconomyManager — Per-faction (Player/Enemy) FactionEconomyState: ResourceStockpile (Ore/Biomass/Gold
│                    + lifetime-earned), standing buildings, power capacity/used. Owns the Economy Round
│                    timer; resets all faction state on every EconomyRound entry (destroys leftover
│                    buildings/units from a prior mission/attempt first — Unity's deferred Destroy means
│                    DeregisterBuilding must guard against a stale callback landing after the reset,
│                    see the comment on that method). Each faction's `MainBuilding` (see `Economy/`)
│                    now carries a real `Health` component and is combat-targetable (issue #28, shipped
│                    2026-08-21 — reverses issue #20's original "permanently invulnerable" decision after
│                    seeing it in actual play). `MainBuilding.Initialize(faction, maxHealth)` stores its
│                    configured max health (`MissionEconomyConfig.FactionEconomyStart.mainBuildingMaxHealth`,
│                    default 500) for its own `Start()` to apply once its `Health` component is cached —
│                    the same Initialize-then-Start shape `BuildingInstance` already uses, not an
│                    external caller reaching into `Health` synchronously. `Health.OwnerFaction`'s
│                    owner-resolution chain gained a third branch for `MainBuilding` alongside `UnitBase`/
│                    `BuildingInstance` — every prior owner-resolving component only checked two owner
│                    types, so a `MainBuilding`'s `Health` would have silently misattributed itself to
│                    `Faction.Player` without this. `EconomyManager` gained `DeregisterMainBuilding`,
│                    called from `MainBuilding.OnDestroy()` (mirroring `BuildingInstance`/
│                    `DeregisterBuilding`'s existing stale-callback guard exactly) — without it, a
│                    combat-destroyed main building would linger in the `MainBuildings` dictionary and
│                    throw when `MinimapPanel`/`VictoryConditionChecker` next read it. **Main Building
│                    direct production** (issue #41, shipped 2026-08-24): the Main Building can now
│                    produce units directly (SC2-style command-center-produces-workers), starting with
│                    the Harvester — previously `ProductionQueue` was hard-wired to `BuildingInstance`
│                    (`[RequireComponent(typeof(BuildingInstance))]`, direct field type) while
│                    `MainBuilding` was architecturally separate with no producible-units list at all.
│                    Fixed via a new minimal `IProductionHost` interface (`Faction`/`IsDisabled`/
│                    `RallyPoint`), implemented by both `BuildingInstance` (already had all three as
│                    matching properties, zero change) and `MainBuilding` (new: `IsDisabled` is a
│                    hardcoded `false` — sabotage never targets the Main Building — and `RallyPoint`
│                    is new gettable/settable state with its own `SetRallyPoint`/`ClearRallyPoint`,
│                    mirroring `BuildingInstance`'s exactly, including its own `RallyPointMarker`).
│                    `ProductionQueue` now depends on `IProductionHost` instead of `BuildingInstance`
│                    directly — the `[RequireComponent]` attribute is gone (no longer meaningful once
│                    the dependency is an interface), with zero behavior change for existing production
│                    buildings, whose `ProductionQueue` still resolves the same `BuildingInstance` via
│                    `GetComponent<IProductionHost>()`. `MainBuilding.Initialize` gained a fourth
│                    parameter, `producibleUnits` (from the new `MissionEconomyConfig.FactionEconomyStart.mainBuildingProducibleUnits`
│                    field, mirroring `BuildingDefinition.producibleUnits`'s shape exactly), and
│                    `MainBuilding.Start()` self-adds a `ProductionQueue` component
│                    (`GetComponent<ProductionQueue>() == null` guard) rather than requiring it be
│                    pre-placed in the Editor — the Main Building is runtime-instantiated from a plain
│                    prefab reference (`FactionEconomyStart.mainBuildingPrefab`), not hand-authored per
│                    mission, so self-adding is the only option. `AttackCommand.TryHandleRallyPoint`
│                    gained a parallel branch for `SelectionController.SelectedMainBuilding` (already
│                    tracked since issue #24, just unused for this) alongside its existing
│                    `SelectedBuilding` branch — right-click sets/clears a rally point on a selected
│                    Main Building exactly like any other production building. `ProductionMenuPanel`
│                    was generalized from a hard-typed `List<BuildingInstance>` to `List<MonoBehaviour>`
│                    (resolving a hard-typing risk the `/arch` review for this batch had already
│                    flagged) so its tracked-set/rebuild logic works over either a `BuildingInstance` or
│                    a `MainBuilding` host uniformly — `BuildEntry` now takes an explicit display name
│                    and producible-units array instead of reading `.Definition.*` directly, since
│                    `MainBuilding` has no `BuildingDefinition` to read from (it shows as "Main
│                    Building" — a literal label, not a field, since there's nothing to source one
│                    from). The Main Building only appears in this panel once its producible-units list
│                    is non-empty, so a mission with no `mainBuildingProducibleUnits` configured shows
│                    nothing new. Verified end-to-end in Play Mode: queuing a Harvester from the Player
│                    Main Building spent exactly its configured Ore cost from the Player's stockpile,
│                    the produced `HarvesterUnit` spawned at the Main Building's exact position with
│                    `Faction.Player`, and its `NavMeshAgent.destination` matched a rally point set via
│                    `MainBuilding.SetRallyPoint` beforehand — confirming the full chain from UI-facing
│                    data down through `IProductionHost` resolves correctly for a non-`BuildingInstance`
│                    host. **Tooling note**: this project's `unity-mcp` `RunCommand` harness appears to
│                    wrap/pre-process submitted scripts in a way that throws an unrelated
│                    `NullReferenceException` — not caught by a try/catch inside the submitted script's
│                    own `Execute()` — the moment the script calls `System.Reflection`'s
│                    `Type.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)`
│                    (or presumably any private-instance reflection lookup) against a `MonoBehaviour`
│                    subtype; a plain Unity `GameObject.SendMessage("Update", SendMessageOptions.DontRequireReceiver)`
│                    reaches the same private `Update()` method without issue and was used instead to
│                    force `ProductionQueue`'s production-completion tick under this project's frozen-
│                    player-loop testing quirk (see the Unity MCP section below). Avoid raw
│                    `System.Reflection` private-member lookups in future `RunCommand` test scripts —
│                    prefer `SendMessage` for invoking a private Unity message method, and public
│                    API/property reads for everything else.
│                    **Crewman build & repair rework** (issue #42, shipped 2026-08-24):
│                    the biggest single change in this batch — every building's placement
│                    flow is now Crewman-mediated instead of instant/unit-agnostic, and a
│                    genuine repair/heal mechanic exists for the first time. New `CrewmanUnit`
│                    (`Units/`, no `Weapon`, same as `HarvesterUnit`) pairs two independent
│                    behavior components (`Economy/CrewmanRepairBehavior`,
│                    `Economy/CrewmanBuildBehavior`, mirroring the Units/Economy folder split
│                    `HarvesterUnit`/`HarvesterGatherBehavior` and `SaboteurUnit`/
│                    `SabotageAction` already established) — since both drive the same
│                    `UnitBase.MoveTo`/`HasArrived`, `CrewmanUnit.AssignRepairTarget`/
│                    `AssignBuildOrder` each cancel the other behavior first
│                    (`CancelBuildOrder`/`CancelRepair`) so a Crewman never tries to travel to
│                    two places at once. **Repair** (`CrewmanRepairBehavior`, mirrors
│                    `HarvesterGatherBehavior`'s travel-then-continuous-tick shape, not
│                    `SabotageAction`'s single-completion channel — repair has no fixed
│                    duration): a new `IRepairTarget` interface (`Faction`/`Position`/
│                    `HealthComponent`) is implemented by both `BuildingInstance` and
│                    `MainBuilding`; `Health` gained its first-ever `Heal(amount)` method
│                    (clamped to `MaxHealth`, confirmed no overheal via direct testing:
│                    300→310 after ~1s at a configured 10/s rate, then plateaus exactly at
│                    500/500 after many more ticks). `AttackCommand.TryHandleRepairTarget`
│                    (all-Crewman selection, friendly target below max health) sits alongside
│                    `TryHandleSabotageTarget` in the same targetable-layer raycast branch —
│                    the two are mutually exclusive by construction, since sabotage only ever
│                    matches a non-Player-owned target and repair only a Player-owned one.
│                    **Build rework**: `BuildingPlacement.TryPlace` no longer instantiates
│                    directly — it spends resources (unchanged timing) then calls
│                    `FindNearestSelectedCrewman(position).AssignBuildOrder(definition,
│                    position)`; `BuildMenuPanel`'s build buttons gate both `interactable` and
│                    the click handler on "selection is entirely Crewmen" (mirrors the
│                    existing Sabotage/Repair all-or-nothing convention), a no-op otherwise.
│                    `CrewmanBuildBehavior` travels to the site, then `Instantiate`s the
│                    building **at arrival** (visible immediately, matching how a real
│                    "building site" reads) via `BuildingInstance.BeginConstruction()`
│                    (`IsUnderConstruction = true`), ticks `SetConstructionProgress` each
│                    frame, and calls `CompleteConstruction()` once `BuildingDefinition.buildTime`
│                    elapses — a destroyed Crewman simply stops ticking (component gone with
│                    its GameObject), leaving a permanently unfinished site, no
│                    auto-continuation or refund, matching `SabotageAction`'s existing
│                    "interrupting = simply gone" precedent exactly. **Two real system gaps
│                    the `/arch` review flagged as non-negotiable conditions before dev
│                    started, both resolved:** (1) *Power timing* — `EconomyManager.RegisterBuilding`
│                    no longer applies power itself; power is now a separate
│                    `ApplyBuildingPowerFootprint(building)` call, made once, either
│                    immediately in `BuildingInstance.Start()` for a building that was never
│                    under construction (a hand-placed Editor building defaults to
│                    fully-functional) or from `CompleteConstruction()` otherwise — a new
│                    `HasAppliedPowerFootprint` flag on `BuildingInstance` lets
│                    `DeregisterBuilding` only ever subtract a footprint that was actually
│                    added. Verified directly: `PowerCapacity` stayed at 0 through an entire
│                    under-construction period, then jumped to exactly `+powerGenerated` the
│                    instant `CompleteConstruction()` ran. (2) *Vision* —
│                    `VisionManager.MarkBuildingVision` skips any `building.IsUnderConstruction`
│                    entry entirely; verified in isolation at a position with no other vision
│                    source (visible=false while under construction, visible=true immediately
│                    after `CompleteConstruction()`). `BuildingInstance.IsDisabled` now also
│                    returns true while under construction (alongside the existing sabotage
│                    case), so `ProductionQueue.Update()`'s existing `if (_host.IsDisabled)
│                    return;` gate blocks production for free — no new production-gating code
│                    needed. `SelectionPanel` shows a construction-progress readout (mirrors
│                    `ProductionQueue.CurrentProgress01`'s display pattern) in place of a
│                    building's normal power/production stats while `IsUnderConstruction`.
│                    Both existing building definitions (`EconomicBuildingDefinition`,
│                    `DetectionBuildingDefinition`) had `buildTime` bumped from the
│                    previously-inert default of `0` to `6` seconds — a `/dev`-time judgment
│                    call (the issue reused this existing-but-unconsumed field without
│                    specifying real values), flagged for the designer to re-tune. Crewman art
│                    uses `ArmoredSoldiers/Green/Light/GreenSoldierLight` (Player) and
│                    `AliensPack2/Alien4` (Enemy, previously unused by any other unit) — see
│                    Asset Roster below. **Testing note**: `CrewmanUnit` currently ships
│                    spawnable only via `TestSetup` (no production building can produce it
│                    yet, per the issue's own explicit sequencing — Barracks/Drone Factory
│                    come later); production-menu wiring is a follow-up once one exists.
│                    **Placement UX follow-up (issue #46, shipped 2026-08-24):**
│                    two real usability problems found in actual play. (1) Confirming
│                    placement was left-click, racing `SelectionController`'s own
│                    unconditional left-click ownership — a placement click also registered
│                    as a select/deselect click on whatever was under the cursor, and
│                    right-clicking a selected Crewman mid-placement did nothing at all.
│                    Fixed by moving confirmation to right-click, following the exact
│                    "arm a pending mode, resolve on `AttackCommand`'s next right-click"
│                    idiom `UnitOrderCommand.TryHandlePendingOrderClick` already established
│                    for Attack-Move/Patrol (issue #26) — `BuildingPlacement` no longer polls
│                    input in its own `Update()` at all; `TryHandlePendingPlacementClick`
│                    is called from `AttackCommand`'s own chain instead, first (highest
│                    priority, ahead of rally-point/pending-order/sabotage/repair/attack/move).
│                    This is now the third consumer of this idiom, worth flagging for
│                    promotion into a named CLAUDE.md convention if a fourth shows up. (2) No
│                    placement-overlap check existed at all — a building could be placed
│                    directly on top of another. Fixed via `BuildingPlacement.WouldOverlapExistingBuilding`
│                    (`Physics.OverlapBox`, sized from the definition's own prefab
│                    `BoxCollider`, against the shared Units layer buildings/units/main
│                    building already occupy, filtered to only `BuildingInstance`/
│                    `MainBuilding` hits so a unit standing on the spot doesn't block
│                    placement) — checked both in `TryPlace` (a hard refusal, no resource
│                    spend) and live every frame by the new placement ghost. **New
│                    `BuildingPlacementGhost`** (`Economy/`) — a translucent, stripped-down
│                    copy of the pending building's actual prefab (every `MonoBehaviour`
│                    destroyed and every `Collider` disabled immediately after `Instantiate`,
│                    so it never registers with `EconomyManager`/blocks its own overlap
│                    check) that follows the cursor's ground-raycast position while a
│                    building is pending, tinted green when clear and red when it would
│                    overlap — a live preview of the exact same check that gates the real
│                    placement, not just a cosmetic guess. Uses the alpha-blended Transparent
│                    URP Unlit material recipe already proven working in this project by
│                    `Vision.FogOfWarRenderer` (`_Surface=1`, override tag, render queue,
│                    `_ZWrite=0`, `_SrcBlend`/`_DstBlend`) rather than `FogGhostMarker`'s
│                    simpler solid-dimmed-color choice, since genuine translucency reads
│                    better for a "preview, not yet real" object than a flat dim tint would.
│                    Verified end-to-end in Play Mode: a right-click at a clear position
│                    correctly resolved into the full existing build order → travel →
│                    construct chain; an identical right-click directly on top of an
│                    already-built building was refused with zero resource spend and no
│                    second building created at that position.
│                    **Unit Upkeep (issue #48, shipped 2026-08-24):** the first piece of a
│                    designer-discussed hybrid-economy sequence (upkeep → area-based resource
│                    nodes → an auto-gathering building → a Battle-Round-locked resource pool →
│                    a future "Injection Round" — the rest not yet spec'd). Every `UnitBase`
│                    now has its own Ore/Biomass/Gold upkeep cost per minute
│                    (`UnitBase.SetUpkeepCost`, a plain field write like `SetBaseMoveSpeed` —
│                    no lazy-construction ordering concern, unlike `VehicleAnimationDriver.SetEngineSound`),
│                    sourced from that unit type's own existing Config asset
│                    (`HarvesterConfig`, `SaboteurConfig`, `CrewmanConfig`,
│                    `CombatUnitConfig` — Reaper/Dredge given distinct tuning from the shared
│                    class default, matching how their `maxHealth`/`moveSpeed` already differ
│                    per asset) and pushed in each unit's own Start(), alongside its existing
│                    maxHealth/moveSpeed calls. `EconomyManager.Update()` (already ticking the
│                    round timer) gained a new `ApplyUpkeep()`, called only while
│                    `_roundTimerRunning` — sums every active unit's upkeep per faction via
│                    `UnitManager.ActiveUnits` once per frame, then drains each faction's
│                    stockpile directly via `ResourceStockpile.Spend()` (not gated on
│                    `CanAfford` — upkeep is allowed to push a faction negative by design, see
│                    below). Costs are designer-tuned as "per minute" but applied every frame,
│                    so a private `UpkeepAccrual` (Ore/Biomass/Gold floats) per faction
│                    accrues the fractional amount each tick and only spends the whole-number
│                    part once it crosses 1 — without this, a small per-unit cost like 1/min
│                    would truncate to zero every single frame and never actually drain.
│                    Reset alongside every other per-round registry in
│                    `InitializeFactionStates()`, so accrued fractional debt never survives a
│                    fresh Economy Round entry. **Going negative is an intentional design
│                    outcome, not a bug to guard against** — confirmed directly with the
│                    designer: a faction that overbuilds its army (mass-early / full-tech-tree
│                    "all-in," the SC Zergling-rush/mass-Mech read) can legitimately run its
│                    stockpile negative, and that negative balance carries unclamped into
│                    Battle Round, blocking all further production for the rest of the mission
│                    — a deliberate risk/reward tradeoff, explicitly **not** softened with a
│                    floor-at-zero clamp at the Economy→Battle Round transition. `ResourcePanel`
│                    already reflected the drain for free (it reads `Stockpile.GetAmount` live
│                    every frame, including negative values) — the only addition there is a
│                    tint to red whenever any resource is negative, so the all-in state stays
│                    legible at a glance. **Real, non-obvious risk caught during `/arch`, not
│                    assumed correct from the spec's own framing:** the issue's own draft
│                    proposed fixing a feared `VictoryConditionChecker` interaction with "a
│                    carve-out for negative specifically" — but `ResourceStockpile.IsEmpty()`
│                    (a plain `> 0` check) already treats *any* non-positive amount uniformly,
│                    so a stockpile at exactly `[0,0,0]` and one at `[-1000,-1000,-1000]`
│                    already produced the identical `IsEmpty() == true` result before this
│                    issue ever existed — negative was never the actual differentiator. What
│                    upkeep really changes is *reachability*: before #48, the existing
│                    `[stockpile ≤ 0, no production building]` elimination state (from issue
│                    #6, see below) was only reachable through deliberate overspending; with
│                    continuous passive drain, it's reachable involuntarily, with an army still
│                    actively fighting. Fixed correctly, not by chasing the negative/zero
│                    distinction: `VictoryConditionChecker`'s `EconomyExhausted` check gained
│                    `&& noUnits` (reusing the `noUnits` bool the wipeout check one line above
│                    already computes) — a faction with an active army is never
│                    economy-eliminated, full stop, regardless of stockpile sign. **Verified
│                    end-to-end in Play Mode**, including one decisive confirmation that wasn't
│                    even deliberately staged: a long verification tick-loop happened to run
│                    past the mission's own Economy Round duration mid-test, and upkeep
│                    drain measurably stopped the exact instant `GameManager.CurrentState`
│                    flipped to `BattleRound` — direct proof of the "stops the moment Battle
│                    Round begins" criterion, not just a code-read assumption. Also confirmed:
│                    drain exactly matched computed per-faction totals over a controlled
│                    60-second tick window (6/3/3 Ore/Biomass/Gold, precise integer match);
│                    `CanAfford` already correctly blocks new spending the instant *any* one
│                    resource goes negative (zero new gating code needed — an existing
│                    behavior, not a #48 addition); `GetEliminationReason` returned `null` for
│                    a Player faction sitting deeply negative with a full active army; recovery
│                    to positive across all three resources correctly unblocked spending again.
│                    **Balance observation, not a defect:** at the default tuned values (a
│                    modest starter roster totaling ~6 Ore/min), the test mission's compressed
│                    300-second Economy Round can't actually drive a fresh 50-Ore economy
│                    negative through upkeep alone — reaching the "all-in" state as designed
│                    needs either the GDD's real 15-minute round length or a meaningfully
│                    larger army than `TestSetup`'s roster; flagged for the designer's
│                    attention when tuning against the real (not test-compressed) round length.
│                    **Area-Based Resource Nodes (issue #49, shipped 2026-08-25):** item 2 of
│                    the hybrid-economy sequence discussed with the designer (#48's completion
│                    notes). `ResourceNode`'s old single-sprite/depletion-stage-swap visual
│                    (`depletionStages`/`DepletionStage`, fully removed) is replaced by a
│                    scattered cluster: `ResourceNodeDefinition` gained `scatterObjectPrefab`/
│                    `scatterObjectCount`/`scatterRadius`; `ResourceNode.GenerateScatter()`
│                    (public, idempotent — rebuilds its tracked list from existing children
│                    instead of re-scattering if any already exist) instances that many copies
│                    at random XZ positions under itself and disables its own root placeholder
│                    `MeshRenderer` so the old single mesh doesn't render alongside the new
│                    cluster. `UpdateScatterVisibility()` destroys tracked instances down to
│                    `Ceil(remainingFraction * scatterObjectCount)` as `RemainingYield` drops —
│                    **must use the fixed original `scatterObjectCount`, not the live
│                    (already-shrinking) tracked-list count, as the fraction's base**; the
│                    first implementation used the live count and silently compounded (a node
│                    at 10% remaining ended up with 1 visible object instead of the correct 2),
│                    caught only by direct-state verification, not code review. Since Edit Mode
│                    never runs `Awake()` for a freshly-instantiated component (confirmed
│                    directly, see `memory/DECISIONS.md`'s 2026-08-25 entry), `MapEditorWindow.TryPaint`
│                    calls `GenerateScatter()` explicitly right after `PrefabUtility.InstantiatePrefab`
│                    so a painted node shows its cluster immediately rather than only after
│                    Play Mode starts. Scattered instances are nudged to local Y `+0.03`
│                    (`ResourceNode.ScatterYOffset`) rather than `0` — otherwise a sprite-based
│                    scatter instance (e.g. Biomass's `Prop_Tree`) sits exactly at `Ground`'s
│                    own `Y=0` and is fully occluded by it, the same ground-occlusion hazard
│                    CLAUDE.md's Sprite & Art Convention already documents for rings/unit art.
│                    Ore and Gold scatter instances are new small placeholder capsule prefabs
│                    (`Assets/Prefabs/ResourceScatter_Ore.prefab`/`_Gold.prefab`, no Collider,
│                    reusing the existing `OreNodeMaterial`/`GoldNodeMaterial`); Biomass reuses
│                    the existing `Prop_Tree.prefab` directly, per the issue's own "no new art
│                    sourcing" scope — see the Sprite & Art Convention section for the real,
│                    previously-undiscovered shader bug found and fixed in `Prop_Tree` along
│                    the way. Also fixed: `ResourceNode_Gold.asset` had `resourceType: 0`
│                    (Ore) instead of `2` (Gold) — correctly wired to the Gold prefab, wrong
│                    enum value in the data itself, caught by `/arch` before `/dev` started.
│                    The project's 4 pre-existing hand-configured (non-prefab-backed) resource
│                    nodes in the scene were brought in line the same way a freshly-painted one
│                    is — `GenerateScatter()` called explicitly, not left to a Play Mode
│                    `Awake()` that would only run once someone actually pressed Play.
│                    **Selection area bug fix (issue #58, shipped 2026-08-27):** all three node
│                    prefabs carried a stale root `CapsuleCollider` at `radius: 0.5` — a
│                    leftover from before this issue's own scattered-cluster redesign — against
│                    a real `scatterRadius` of `4`, so clicking anywhere outside a tiny
│                    0.5-radius center did nothing despite the visible cluster extending much
│                    further. `GenerateScatter()` now also sets `CapsuleCollider.radius` from
│                    `_definition.scatterRadius` every time it runs, so the two values can never
│                    drift apart again regardless of future tuning. Fixes both known raycast
│                    consumers of this same physical collider for free — `SelectionController`'s
│                    click-to-select and `AttackCommand.TryHandleRedirectToNode`'s (#38)
│                    right-click-to-redirect-Harvesters flow both independently raycast against
│                    `_resourceNodeLayerMask`, but hit the identical GameObject's collider, so
│                    neither needed its own code change. Verified end-to-end in Play Mode: a
│                    raycast 2 units off a node's center (inside the new radius, outside the old
│                    one) now correctly resolves to that node.
│                    **Biomass Depletion-Tier Art Cycling (issue #59, shipped 2026-08-27):**
│                    `ResourceNodeDefinition` gained an opt-in tiered-scatter-art mode
│                    (`useTieredScatterArt`, default false — every other node type unaffected):
│                    `tierPrefabHigh`/`tierPrefabMid`/`tierPrefabLow` (Biomass wired to the new
│                    `Prop_Plant3`/`Plant2`/`Plant1` prop prefabs) plus two thresholds
│                    (`tierThresholdHigh`/`tierThresholdLow`, defaulting to 0.66/0.33 — a
│                    `/dev`-time balance default, same precedent as every prior new-content
│                    tuning value in this project) fully replace `scatterObjectPrefab` for a
│                    tiered node; Biomass's own `scatterObjectPrefab` reference was cleared, no
│                    longer touching `Prop_Tree` at all. `ResourceNode` tracks a private
│                    `_currentTier` (int, -1 until first applied) and only re-swaps every
│                    currently-visible scatter instance (`Destroy`+`Instantiate` at each
│                    instance's existing local position, reusing the exact pattern
│                    `UpdateScatterVisibility()`'s own count-shrink already used) when the
│                    freshly computed tier actually differs from it — **required by `/arch`**,
│                    since `UpdateScatterVisibility()` runs on every `Extract()` call (once per
│                    Harvester gather tick), far more often than a threshold is actually
│                    crossed; an unguarded swap would have re-applied on nearly every tick for
│                    no visual change. `GenerateScatter()`'s initial scatter for a tiered node
│                    uses `tierPrefabHigh` directly (a fresh node starts at 100%, always at or
│                    above `tierThresholdHigh`) and pre-sets `_currentTier = 0` to match, so the
│                    very first `UpdateScatterVisibility()` call right after in `Awake()`
│                    doesn't immediately re-swap what it just created. Also shipped: `Plant1`/
│                    `Plant2`/`Plant3` as six new standalone Map Editor Props (plain +
│                    splash-cluster brushes each, matching `Prop_Tree`/`Brush_TreeCluster`'s
│                    (#56) exact shape and defaults) — pure content, zero `MapEditorWindow.cs`
│                    changes needed. **Migration**: the scene's 21 pre-existing Biomass nodes
│                    (accumulated well beyond the original 4-node baseline documented under
│                    issue #49 — flagged, not a concern for this issue) had their old `Prop_Tree`
│                    scatter children destroyed and regenerated fresh via `GenerateScatter()`,
│                    the same "bring existing content in line the way a freshly-painted one is"
│                    precedent #49 itself established. **Verification note**: this project's
│                    frozen-player-loop `unity-mcp` sessions can leave `Destroy()` calls
│                    permanently un-flushed within a single Play session (confirmed directly —
│                    `Time.frameCount` stayed at `1` across 5+ round-trips), making the raw
│                    scene hierarchy (`transform.childCount`, `GetChild(0)`) an unreliable
│                    verification signal for anything that calls `Destroy()` — it can show a
│                    growing superset of stale-but-not-yet-removed old instances alongside
│                    genuinely new ones. Verified correctness instead via a temporary debug
│                    accessor exposing the real internal tracked list (added, used, then
│                    removed before shipping) — confirmed the tracked count, tier, and sprites
│                    were exactly correct throughout a full High→Mid→Low depletion sequence,
│                    including zero churn on a same-tier re-extraction. See
│                    `memory/PATTERNS.md` for the full write-up, including a real scare where
│                    this same deferred-`Destroy()`-in-a-frozen-session behavior initially
│                    looked like it might have queued 21 resource nodes for real destruction
│                    (it hadn't — `Destroy()` is a hard no-op outside Play Mode, confirmed
│                    separately, not a deferred one).
│                    **Resource Node Crystal Art (issue #69, shipped 2026-08-30, 3D pivot
│                    follow-up):** replaces every resource node's scatter art with the real 3D
│                    `ResourceCrystal` models from Mega Pack III (`Assets/ScifiRTSSeriesMegaPackIII/
│                    Prefabs/ResourceUnits/ResourceCrystal/`), color-mapped Biomass→Green,
│                    Ore→Red, Gold→Gold — a designer-requested color swap, not a code change.
│                    Extends #59's tiered-depletion-art system (previously Biomass-only) to Ore
│                    and Gold as well, since the pack's Large/Medium/Small crystal size variants
│                    map directly onto the existing High/Mid/Low thresholds — both were
│                    previously on the older flat, non-tiered `scatterObjectPrefab` path. Zero
│                    changes to `ResourceNode.cs`/`ResourceNodeDefinition.cs` — purely new
│                    prefab/data assets. 9 new wrapper prefabs
│                    (`Assets/Prefabs/Prop_ResourceCrystal_{Green,Red,Gold}_{Large,Medium,Small}.prefab`),
│                    each a root+`Art`-child wrapper around the pack's own `ResourceCrystal*.prefab`
│                    at **identity rotation** (a real 3D model, unlike every prior scatter prop
│                    in this project, which was a flattened 2D sprite needing the `(90,0,0)`
│                    convention) and a **9x scale multiplier** — the pack's native crystal
│                    bounds are tiny (Large ≈0.04×0.13×0.04 world units) and read as invisible
│                    specks unscaled; confirmed visually via multi-angle scene captures of a
│                    live Red and Gold node before shipping. All 40 pre-existing scene nodes
│                    migrated via the same #59 precedent (old scatter children destroyed,
│                    `GenerateScatter()` re-run), confirmed via direct per-node child-count
│                    inspection (exactly 15 tracked children each, no leftover orphans —
│                    checking the real count directly rather than trusting an aggregate,
│                    per the issue #40 lesson about a hidden duplicate surviving an
│                    aggregate-count check). Tier-swap-on-depletion re-verified end-to-end on a
│                    live Ore node using the same temporary-debug-accessor technique #59
│                    established (`transform.childCount` is unreliable mid-Play-session here,
│                    per that entry's `Destroy()`-un-flushed finding) — confirmed correct
│                    Large→Medium→Small tier swaps in lockstep with tracked count 15→8→3 as
│                    yield dropped 100%→50%→20%. **Known, not fixed:** the designer's chat term
│                    "Eternium" for Ore doesn't match `docs/GDD.md`'s existing "Etherium"
│                    spelling — flagged, not resolved, since neither term exists anywhere in
│                    code (the enum is just `Ore`) and there's no player-facing display string
│                    yet to carry a rename.
│                    **Harvester Nearest-Scatter-Point Targeting (issue #50, shipped
│                    2026-08-25):** `HarvesterGatherBehavior` no longer walks to a resource
│                    node's center — `AssignToNode` and the "resume gathering" branch of
│                    `TickReturning` both call a new private `MoveToNearestScatterPoint()`,
│                    which queries a new `ResourceNode.GetNearestScatterTarget(Vector3, out
│                    GameObject)` for whichever currently-alive scattered instance is nearest
│                    the Harvester and moves there instead, falling back to the node's own
│                    center (with a null instance) if it has no scatter content configured at
│                    all. While still traveling (`MovingToNode` only — never after gathering
│                    has actually started), `TickMovingToNode` re-queries and reroutes if its
│                    specific tracked instance was destroyed out from under it (by its own
│                    concurrent extraction, another Harvester's, or sabotage) before arrival.
│                    **Real correctness gap caught by `/arch` before dev started, not found
│                    the hard way afterward:** a bare null-check on the tracked instance
│                    reference can't distinguish "this node has no scatter art, there was
│                    never a specific target" from "the instance I was walking to just got
│                    destroyed" — both read as `null` once a `UnityEngine.Object` is
│                    destroyed, thanks to its overloaded equality operator. Naively reacting
│                    to that null every tick would silently re-trigger a reroute query (and a
│                    fresh `Agent.SetDestination` call) on *every single tick* for any node
│                    with no `scatterObjectPrefab` configured. Fixed with an explicit
│                    `_hasScatterTarget` bool set only when the query actually returns a
│                    non-null instance, so the fallback-to-center case is stable and never
│                    re-queries. Verified directly, not just by code review: assigning a
│                    Harvester to a real scattered node confirmed its destination differs
│                    from — and sits closer to the Harvester than — the node's own center;
│                    destroying its specific tracked instance mid-travel correctly rerouted
│                    to a new instance exactly once; assigning to a temporary node built with
│                    no `scatterObjectPrefab` correctly left `_hasScatterTarget` false and
│                    held the query call count flat across 10 subsequent ticks (not
│                    incrementing once per tick). No claiming/reservation between multiple
│                    Harvesters on the same node — explicitly scoped out; gathering was
│                    already a per-Harvester pool tick with no per-instance exclusivity, so
│                    two Harvesters converging on the same nearest instance is cosmetic
│                    clustering, not a functional bug.
│                    **Auto-Gathering Building** (issue #51, shipped 2026-08-26): part 3 of
│                    the hybrid-economy sequence (#48 upkeep → #49/#50 area nodes → this) —
│                    a new "Auto-Extractor" `BuildingDefinition`/prefab that must be placed
│                    within a `ResourceNode`'s `scatterRadius` and passively extracts that
│                    node's yield once per second, depositing straight into the owning
│                    faction's stockpile with no Harvester round-trip. New sibling component
│                    `Economy/AutoGatherBehavior` (`[RequireComponent(typeof(BuildingInstance))]`,
│                    same shape as `Weapon` on `MachineGunTurretBuilding`, #45) binds to the
│                    nearest `EconomyManager.ResourceNodes` entry within radius in `Start()`
│                    and gates its own 1-second extraction tick on `IsEconomyRoundActive` and
│                    the sibling `BuildingInstance.IsDisabled` (construction/sabotage) exactly
│                    like `HarvesterGatherBehavior`/`ProductionQueue` already do. Two new
│                    additive `BuildingDefinition` fields: `requiresResourceNode` (mirrors
│                    `requiresRoboticsPlant`'s opt-in-gate pattern from #45) and
│                    `autoGatherRatePerSecond` (tuned to 2/sec — deliberately below a
│                    Harvester's raw 5/sec `gatherRate`, a `/dev`-time balance default flagged
│                    for a real tuning pass). **This is the first building whose placement is
│                    only valid when it overlaps something, not when it avoids overlap** —
│                    every prior `BuildingPlacement` check (#46) was "must not overlap an
│                    existing building." `ResourceNode` sits on a different physics layer
│                    (0/Default) than buildings/units (layer 9), so `WouldOverlapExistingBuilding`'s
│                    `Physics.OverlapBox` structurally cannot see nodes — the new check
│                    (`BuildingPlacement.IsWithinResourceNode`, gained a new public
│                    `ResourceNode.ScatterRadius` accessor) is a plain distance comparison
│                    against `EconomyManager.ResourceNodes` instead, ANDed with (not replacing)
│                    the existing overlap check in both `BuildingPlacementGhost`'s live preview
│                    and the real `TryPlace` gate — caught by the `/arch` review before `/dev`
│                    started, per its own conditions. A host node destroyed between placement
│                    confirm and construction completing (by Harvesters, sabotage, or natural
│                    depletion) leaves `AutoGatherBehavior` with no bound node at all — treated
│                    identically to a post-completion depleted node: inert, no error, no
│                    special teardown. Verified end-to-end in Play Mode via direct method
│                    calls (see `memory/PATTERNS.md`'s 2026-08-26 entry for a real `TestSetup`
│                    coroutine gotcha hit during this verification): correct nearest-node
│                    binding, exactly 2 Ore extracted and deposited once the 1-second gate was
│                    crossed, construction/sabotage-disable correctly blocked extraction and
│                    resumed it after `CompleteConstruction`, a node-less building never
│                    extracted and never threw, and the full 5-case placement-validity matrix
│                    (valid near a node, invalid overlapping a building, invalid with no node
│                    in range, and two regression checks confirming an ordinary building's
│                    placement is completely unaffected) all resolved correctly.
│                    **Locked Resource Pool & Silo Buildings** (issue #52, shipped 2026-08-27):
│                    part 4 of the hybrid-economy sequence — a per-faction, per-resource-type
│                    locked pool the player fills by diverting a live-adjustable percentage of
│                    incoming resources away from the normal spendable stockpile, bounded by
│                    capacity from a new single-resource-type **Silo** building (three assets:
│                    Ore/Biomass/Gold Silo, `BuildingDefinition.isSilo`/`siloResourceType`/
│                    `siloCapacity`). New `Economy/LockedResourcePool` runtime class (per-type
│                    amount/capacity/allocation-%, `Deposit()` caps at remaining capacity and
│                    returns how much actually banked) lives on a new
│                    `FactionEconomyState.LockedPool` property, a field-initializer instance —
│                    resets to empty/0% for free every time `InitializeFactionStates()`
│                    constructs a fresh state, no explicit reset code needed. `EconomyManager.Deposit`
│                    now splits every incoming deposit (Harvester and Auto-Extractor alike, its
│                    one existing choke point) at the faction's allocation % for that resource
│                    type before forwarding the rest to `Stockpile.Deposit` — the pooled portion
│                    still counts toward `Stockpile`'s lifetime-earned total (feeding the Battle
│                    Round tie-break) via a new `ResourceStockpile.RecordAdditionalEarned`, so a
│                    faction that diverts heavily to Silos doesn't look artificially poor in that
│                    tie-break. Silo capacity is applied/removed by the same
│                    `EconomyManager.ApplyBuildingPowerFootprint`/`DeregisterBuilding` hook power
│                    already uses (reused, not duplicated) — a sabotage-disabled Silo therefore
│                    keeps contributing capacity, deliberately matching the existing (documented,
│                    unfixed) power-capacity precedent rather than inventing a new exclusion rule;
│                    a Silo destroyed outright removes its capacity but never truncates an
│                    already-banked amount that now exceeds the reduced capacity — it just blocks
│                    further deposits of that type until capacity is rebuilt. New toggle-open
│                    **Resource Allocation** HUD panel (`UI/ResourceAllocationPanel`, hotkey `R`,
│                    `TechPanel`'s exact hidden-by-default/root-stays-active shape) shows a
│                    +/-10%-stepper and live pool amount/capacity readout per resource type.
│                    Spending the pool is explicitly out of scope — deferred to the not-yet-spec'd
│                    "Injection Round" (part 5) — so `VictoryConditionChecker`'s `EconomyExhausted`
│                    check deliberately still reads only `Stockpile.IsEmpty()` (a faction sitting
│                    on a full Silo but empty stockpile still reads exhausted today), flagged with
│                    a code comment rather than fixed, since the pool can't fund anything yet
│                    regardless. Verified end-to-end in Play Mode: Silo capacity aggregation
│                    (0→100 on placement), deposit split at 50% allocation (exactly 50/50 to
│                    pool/stockpile), overflow capping (a 200-unit deposit against 50 remaining
│                    room banked exactly 50, the rest correctly fell back to stockpile),
│                    lifetime-earned bookkeeping (a full 100 credited despite the 50/50 split),
│                    Silo destruction (capacity dropped to 0, banked amount stayed at 100,
│                    un-truncated), the +/- buttons' live wiring (clamped correctly at both 0
│                    and 100), Battle Round carryover (pool intact across the transition), and a
│                    fresh Economy Round entry (pool/capacity/allocation all reset to zero on a
│                    genuinely new `FactionEconomyState` instance).
│                    **Injection Round** (issue #53, shipped 2026-08-27): the final piece of
│                    the hybrid-economy sequence — a player-triggered mode entirely inside the
│                    Economy Round (deliberately no `GameManager.GameState` change, confirmed
│                    during `/ba` before drafting) that lets the player spend their locked pool
│                    (#52) specifically on Tech Research. New toggle-open `UI/InjectionPanel`
│                    (hotkey `I`, `TechPanel`'s exact hidden-by-default/root-stays-active shape)
│                    lists the identical tech tree as `TechPanel` — one shared `TechManager`
│                    pipeline, one `_researched` set, so an upgrade researched via either panel
│                    can't be researched again through the other. `TechManager.CanResearch`/
│                    `TryResearch` both gained an optional `useLockedPool: bool = false`
│                    parameter (default preserves `TechPanel`'s existing two-argument call sites
│                    unchanged, per the arch review) — when true, affordability/spend check
│                    against the faction's `LockedResourcePool` instead of `ResourceStockpile`;
│                    every other step (prerequisites, `HasResearchBuilding`/Technology Plant
│                    ownership — same gate applies to pool-funded research, no bypass, a `/dev`
│                    default confirmed reasonable) stays fully shared. `LockedResourcePool`
│                    gained `CanAfford`/`Spend`, mirroring `ResourceStockpile`'s exact shape —
│                    spending down the pool automatically frees room for future deposits, since
│                    `Deposit`'s room calculation (`capacity - currentAmount`) is already live,
│                    no special-case code needed. Verified end-to-end in Play Mode: researching
│                    an upgrade through `InjectionPanel` spent exactly its cost from the locked
│                    pool while leaving the normal stockpile completely untouched, correctly
│                    fired `OnUpgradeResearched`, and the exact same upgrade immediately read as
│                    already-researched (non-researchable) via the normal stockpile-funded path
│                    too — confirming the shared tracking. Separately confirmed zero regression:
│                    `TechPanel`'s own existing two-argument `CanResearch`/`TryResearch` calls
│                    (unchanged code) still correctly spent from the stockpile and left the
│                    locked pool untouched, satisfying a real prerequisite chain (Damage II
│                    requiring Damage I, itself researched moments earlier via the pool).
│                    **Corrected (issue #54, shipped 2026-08-27):** the round-availability and
│                    mechanic described above were wrong and are now fixed. `InjectionPanel`'s
│                    gate is `GameManager.CurrentState == GameState.BattleRound` (not the
│                    shared `IsRoundActive`, which would also permit Economy Round) — this is
│                    now genuinely a Battle-Round-only tactical pivot, matching the original
│                    2026-08-24 design note ("spendable only during Battle Round") that #53's
│                    `/ba` session never actually offered as a round-structure option.
│                    `UpgradeDefinition` gained `isSwappable` (bool, additive) and a new
│                    abstract `Revert(Faction, TechManager)` mirroring `Apply()` — implemented
│                    by both existing subclasses. `StatUpgradeDefinition.Revert` removes
│                    exactly the value-tuple it registered via a new
│                    `TechManager.RemoveStatModifier` (matched by value against
│                    `statType`/`modifierType`/`value`/`targetCapabilities` — no new per-upgrade
│                    tracking structure needed, since the reverting instance already knows
│                    precisely what it registered); `UnlockUpgradeDefinition.Revert` calls new
│                    `DeregisterUnlockedBuilding`/`DeregisterUnlockedUnit`, mirroring their
│                    Register counterparts exactly. `TechManager.TryRevert(Faction, UpgradeDefinition)`
│                    is the shared entry point: valid only if researched AND `isSwappable`,
│                    removes it from `_researched`, calls `Revert()`, and re-fires
│                    `OnUpgradeResearched` so live stat re-application (#14) resyncs downward
│                    correctly — reverting costs nothing, the original research spend is a pure
│                    sunk cost. `InjectionPanel` shows a "Revert" button next to every entry,
│                    hidden (`SetActive(false)`) for non-swappable upgrades and interactable
│                    only once researched. **Scope note:** this issue adds generic revert
│                    capability to the existing upgrade types only — no Plasma/Physical Weapon
│                    content was authored and no weapon-type/resistance mechanic exists; both
│                    shipped tech-tree upgrades (Damage I/II) remain `isSwappable = false`, a
│                    future content-authoring task once real weapon-type upgrades exist.
│                    Verified end-to-end in Play Mode via a fully synthetic, in-memory test
│                    upgrade (never touching the real `Damage I`/`Damage II` `.asset` files):
│                    researching it via the pool correctly applied its stat multiplier (1.0 →
│                    1.5), reverting correctly removed exactly that multiplier (1.5 → 1.0),
│                    re-fired `OnUpgradeResearched`, left the locked pool's spent amount
│                    unchanged (free), and correctly failed on a second revert attempt and on
│                    a non-swappable already-researched upgrade (`Damage I`, confirmed
│                    `isSwappable == false` on the real asset throughout testing). Also
│                    confirmed the round transition itself: `InjectionPanel`'s two Revert
│                    buttons (one per shipped upgrade) both start hidden, and
│                    `GameManager.CurrentState` correctly reads `BattleRound` after
│                    `StartBattleRound()`, satisfying the panel's gate condition.
│                    **Weapon-Type Resistances** (issue #55, shipped 2026-08-27): the payoff
│                    for #54's swappable tech — a unit's weapon type never changes, but
│                    factions research offense/defense investments scoped to a `DamageType`
│                    (new enum, `Units/DamageType.cs`: `Physical`/`Plasma`). New
│                    `WeaponConfig.damageType` (default `Physical`); four new `UpgradeStatType`
│                    values (`PlasmaDamage`/`PhysicalDamage` offense, `PlasmaResistance`/
│                    `PhysicalResistance` defense) — ordinary `StatUpgradeDefinition` assets,
│                    no new `UpgradeDefinition` subclass. `Weapon.FireAt` now multiplies by
│                    both the existing generic `Damage` stat and the type-specific one
│                    matching its own `_config.damageType` (fully additive — every existing
│                    Damage I/II upgrade keeps working unchanged) and passes `_config.damageType`
│                    to `Health.Damage`, which gained a `DamageType` parameter: before applying
│                    damage it multiplies the incoming amount by the defending faction's
│                    resistance multiplier for that type (reusing `GetStatMultiplier`'s
│                    existing formula unmodified — resistance upgrades are authored with a
│                    **negative** percentage value, e.g. `-0.5` for 50% resistance, since
│                    `(1 + percentSum)` already produces a damage-reducing multiplier from a
│                    negative input with zero code changes), floored at 0 so stacked/over-100%
│                    resistance never heals the target. **Real bug caught by `/arch` before
│                    `/dev` started, not found the hard way afterward:** `HarvesterUnit.ApplySabotage`'s
│                    self-destruct (`Damage(float.MaxValue, gameObject)`, doc-commented as an
│                    "absolute" effect) would have silently no-op'd at exactly 100%+ stacked
│                    resistance, since `float.MaxValue * 0 = 0`. Fixed with a new
│                    `Health.Kill(GameObject source)` — the shared death path with no
│                    resistance multiplier applied at all — and `ApplySabotage` now calls that
│                    instead. Content: `HumanPlasmaDamage`/`HumanPhysicalDamage` (+20%, both
│                    `isSwappable = true`, Human tree) and `AlienPlasmaResistance` (50%, Alien
│                    tree) added to `DefaultTechTree.asset`; `DredgeWeaponConfig.damageType`
│                    set to `Plasma` (matching the original Mechs-vs-Rangers design
│                    conversation) so the mechanic is actually reachable in play — every other
│                    weapon config stays the `Physical` default. Verified end-to-end in Play
│                    Mode via direct `Health.Damage`/`Kill` calls (reflection-free, since
│                    `Weapon.FireAt` is private and needs real cooldown/target-acquisition
│                    state this project's frozen-loop sessions can't force): 100 Plasma damage
│                    dealt in full with no resistance researched; exactly halved (100→50 taken)
│                    once 50% Plasma Resistance was researched; Physical damage confirmed
│                    completely unaffected by Plasma-specific resistance; stacked resistance
│                    past 100% (multiplier driven negative) correctly clamped `Damage()` to
│                    zero, never healing; `Kill()` correctly killed the target regardless of
│                    that same deeply-negative multiplier, confirming the bug fix; both new
│                    offense stat multipliers (`PlasmaDamage` 1.2, generic `Damage` 1.1)
│                    independently confirmed correct and ready to combine multiplicatively in
│                    `Weapon.FireAt`'s already-reviewed one-line expression.
│                    **Responsibility split (issue #61, shipped 2026-08-31):** after 11 issues,
│                    this class had grown six distinct responsibilities in one 472-line file —
│                    split down to its real core (per-faction state ownership, the round timer,
│                    and thin delegating wrappers) by extracting two new plain composed classes
│                    (neither a MonoBehaviour nor a `Bootstrapper`-created singleton — private
│                    fields on `EconomyManager`, per this project's singleton convention):
│                    `EconomyRegistry` (main-building dictionary, resource-node list, and
│                    building-registration/power-footprint methods) and `UnitUpkeepTracker`
│                    (the per-faction fractional upkeep accrual and drain tick). **A real,
│                    non-obvious distinction the `/arch` review caught before `/dev` started:**
│                    the main-building/resource-node collections were already independent
│                    `EconomyManager` fields and extract cleanly as genuinely-owned state, but
│                    a standing building's `Buildings`/`PowerCapacity`/`PowerUsed` are
│                    properties **on `FactionEconomyState` itself**, read directly by 8 other
│                    files (`VisionManager`, `MinimapPanel`, `ProductionMenuPanel`, `DebugHud`,
│                    `ResourcePanel`, `VictoryConditionChecker`, `Weapon`, `PowerSystem`) with
│                    zero indirection through `EconomyManager` — so `EconomyRegistry`'s
│                    `RegisterBuilding`/`DeregisterBuilding`/`ApplyBuildingPowerFootprint` take
│                    the caller's own `FactionEconomyState` as a parameter rather than holding a
│                    second, separately-owned building collection, keeping those 8 files'
│                    direct reads correct with zero changes. `FactionEconomyState` also gained
│                    a `Deposit(ResourceType, int)` method holding the locked-pool split logic
│                    that used to live in `EconomyManager.Deposit` (now a two-line delegation:
│                    `GetState(faction)?.Deposit(type, amount)`) — trivial to move since
│                    `Stockpile`/`LockedPool` were already public properties on the state object
│                    it operates on. Zero behavior change and zero external-caller changes
│                    throughout — verified end-to-end via direct calls (this project's frozen-
│                    player-loop sessions can't rely on `TestSetup`'s coroutine-driven spawn, so
│                    `GameManager.StartEconomyRound()` was called directly to force real state):
│                    both factions' Main Buildings spawned at their correct painted positions
│                    with correct health once `Start()` was force-run; placing a real
│                    `EconomicBuilding` correctly registered it and applied its power footprint
│                    (`PowerCapacity` 0→10) onto the *same* `FactionEconomyState` instance
│                    `EconomyManager.GetState` returns, then correctly reverted on
│                    `DeregisterBuilding`, with a second deregister call confirmed to no-op
│                    (the stale-callback guard survived the move intact); placing a real
│                    `OreSilo` correctly added 100 Ore of locked-pool capacity via the same
│                    footprint path; a 20-Ore deposit at 50% allocation split exactly 10/10
│                    between stockpile and pool while crediting the full 20 to lifetime-earned;
│                    a synthetic unit with 6/3/3 Ore/Biomass/Gold upkeep drained a 50/50/50
│                    stockpile to exactly 44/47/47 over a simulated 60-second window — the same
│                    precise integer match issue #48's original verification produced,
│                    confirming `UnitUpkeepTracker.Tick` is bit-for-bit identical to the
│                    `ApplyUpkeep` method it replaced.
├── BattleManager  — Evaluates faction elimination each frame during BattleRound via
│                    VictoryConditionChecker (Battle/): a faction is eliminated only on a true wipeout
│                    (no buildings AND no units AND no standing main building — issue #28) or a fully
│                    exhausted economy (no resources AND no production buildings AND no units —
│                    issue #48, so a faction actively fighting with a negative stockpile — the
│                    cost of a deliberate all-in strategy — is never eliminated on economy
│                    grounds) — never on a momentary zero-units snapshot alone, since production
│                    carryover means a faction can have zero current units without being
│                    eliminated. Simultaneous mutual defeat resolves via each faction's lifetime
│                    resources-earned. Exposes TriggerSurrender/TriggerObjectiveMet hooks for a
│                    future enemy-AI/mission system.
├── CameraManager  — Pan (edge-of-screen + arrow keys) and zoom (scroll wheel) for the
│                    `Main Camera`, both ultimately clamped to `CameraConfig.boundsMin`/
│                    `boundsMax` (a world-space X/Z rectangle). Purely input-driven — never
│                    moves the camera on its own. Also owns SC2-style saved camera locations:
│                    `Ctrl+F1`-`F4` saves the current view to a slot, `F1`-`F4` jumps back to
│                    it (no-op if empty), mirroring `ControlGroupController`'s Ctrl+digit shape
│                    exactly. A separate public `JumpToPosition(Vector3)` (position only, keeps
│                    current zoom) exists for the Minimap's click-to-jump (issue #12) — distinct
│                    from the F-key jump, which also restores zoom. Edge-of-screen panning is
│                    suppressed while `SelectionController.IsDragging` is true, so it can't
│                    fight an in-progress drag-select box. Caches `Camera.main` in `Start()`
│                    like every other manager that needs it (`SelectionController`) — `Main
│                    Camera` is a per-scene object, not `DontDestroyOnLoad` like this manager,
│                    so a future mission-to-mission scene reload would need this re-acquired
│                    (not an issue yet, no such reload exists). **Map size is a per-mission
│                    designer choice, not a project-wide constant** — `CameraConfig` is already
│                    injected per-mission via `Bootstrapper.Initialize(CameraConfig)`, so a new
│                    mission authors its own asset with its own `boundsMin`/`boundsMax` rather
│                    than reusing another mission's (must be kept in sync by hand with that
│                    mission's `Ground` plane extents and Tilemap-painted area — nothing derives
│                    one from the other yet). `CameraConfig.OnValidate` (Editor-only) warns, but
│                    does not block, if a config's span falls outside `MinRecommendedSpan`
│                    (60×60 — below this there's not enough room for a main building + a couple
│                    production buildings + resource nodes + unit maneuvering) or
│                    `MaxRecommendedSpan` (200×200 — past this, NavMesh bake cost and painted-
│                    object count start compounding, and it's untested territory for the map-
│                    editor/Tilemap pipeline). Current default/only mission map is 100×100.
│                    Bounds don't need to be square — a long corridor-style mission is fine.
│                    **Angled Perspective camera & dolly zoom** (issue #80, shipped 2026-09-02,
│                    replacing the original straight-down Orthographic camera from issue #10):
│                    a genuine SC2-style offset view, not a cosmetic tilt — `CameraManager` now
│                    tracks a ground-plane `_focusPoint` (Vector3, X/Z) and a `_distance` float
│                    as its real source of truth; `Camera.transform.position`/`.rotation` are
│                    *derived* from them every time either changes (`UpdateCameraTransform()`:
│                    `rotation = Quaternion.Euler(pitch, yaw, 0)`, `position = focusPoint -
│                    rotation * Vector3.forward * distance`) — pan/zoom/jump/saved-locations
│                    never set the camera's transform directly anymore, only `_focusPoint`/
│                    `_distance`. Pitch (`CameraConfig.cameraPitchDegrees`, default 50° from
│                    horizontal) and yaw (`cameraYawDegrees`, default 0°, matching the old
│                    straight-down camera's convention where 90°-pitch = looking straight down)
│                    are both fixed for the whole mission — no player-controlled rotation.
│                    Pan's `ArrowKeyPan`/`EdgePan` math is completely unchanged (still plain
│                    world-axis vectors) — it now moves `_focusPoint` instead of the camera's
│                    own position, which works because yaw never changes mid-mission. Zoom
│                    (`ApplyZoom`) now changes `_distance` (clamped to `CameraConfig.zoomDistanceMin`/
│                    `zoomDistanceMax`, default 12/40, starting at `defaultZoomDistance` = 18 —
│                    deliberately closer than the old default framing, since "enlarge the
│                    models for better definition" was the whole point) — a real dolly toward/
│                    away from the focus point with genuine perspective foreshortening, not the
│                    old orthographic-size shrink. `Start()` explicitly sets `_camera.orthographic
│                    = false`, `.fieldOfView` (new tunable, default 60°), and `.farClipPlane`
│                    (new tunable, default 300 — the previous scene-baked 100 was found to clip
│                    distant terrain once the camera could sit meaningfully offset from a large
│                    map's far edge) in code, rather than depending on the scene's `Main Camera`
│                    component still having the right values manually set in the Inspector —
│                    the feature is self-contained and can't silently regress to the old
│                    top-down view just because a scene got reverted or hand-edited.
│                    `SelectionController`/`AttackCommand`/`BuildingPlacement`'s click-to-select/
│                    move/place (`Camera.ScreenPointToRay`) and `SelectionController.SelectBox`'s
│                    drag-select (`Camera.WorldToScreenPoint`) both needed zero changes — both
│                    APIs are projection-agnostic, confirmed directly (not assumed) via a
│                    screen-center raycast landing exactly on the focus point in Play Mode.
│                    `MinimapPanel.UpdateViewportRect` **did** need a real fix — it previously
│                    read `_camera.orthographicSize` directly, meaningless under Perspective —
│                    now reads the new `CameraManager.FocusPoint`/`.Distance` public properties
│                    instead, approximating the visible area as a rectangle centered on the
│                    focus point (a true angled-frustum footprint is a trapezoid; the rectangle
│                    approximation is a deliberate, accepted simplification, not a bug). The
│                    old `minCameraHeight`/`maxCameraHeight`/`SyncHeightToZoom` audio-distance-
│                    proxy mechanism (issue #33) is gone entirely — height is now a real,
│                    correct byproduct of `_focusPoint`/pitch/`_distance`, so `AudioManager`'s
│                    3D positional SFX distance falloff is *more* accurate than before, not
│                    less. **Known, accepted gap** (a deliberate scope call, not an oversight):
│                    only the Player's Main Building/Economic Building/Harvester/Crewman are
│                    real 3D models (issue #65) — every other unit (Saboteur, Ranger/Reaper/
│                    Dredge, the entire Enemy faction) is still a flat ground-lying sprite,
│                    which doesn't billboard toward the camera and will read increasingly
│                    foreshortened from this angle until it's converted or billboarded later.
├── AudioManager   — Positional one-shot SFX via a pooled `AudioSource` (matching the
│                    project's `ObjectPool<T>` convention), plus a persistent looping music
│                    `AudioSource`. **No `AudioMixer` asset** — Unity's mixer-group/exposed-
│                    parameter authoring isn't reachable through any available scripting path
│                    in this environment (confirmed: `AudioMixer` isn't a `ScriptableObject`
│                    so direct instantiation fails to compile; `Unity_ManageAsset`'s Create
│                    action explicitly doesn't support it; no "Create > Audio Mixer" menu item
│                    is registered; `AudioMixerController` reflection fails silently, matching
│                    this project's established reflection-in-`RunCommand` limitation — see
│                    `memory/PATTERNS.md`'s 2026-08-19 "Audio Manager" entry). Volume control
│                    is a plain code-side Master/SFX/Music multiplier applied at playback time
│                    instead — functionally equivalent for this project's current needs, but
│                    a standalone `AudioSource` not created by `AudioManager` (e.g.
│                    `VehicleAnimationDriver`'s engine loop) must manually read
│                    `AudioManager.EffectiveSfxVolume` each frame to respect it, rather than
│                    getting it for free via mixer routing. Revisit with a real `AudioMixer`
│                    if it's ever hand-authored directly in the Editor UI by a human.
│                    **Per-category priority/volume/distance tuning** (issue #33, shipped
│                    2026-08-22): a new `SfxCategory` enum (`Weapon`/`Engine`/`Ambient`/`UI` —
│                    the latter two defined for extensibility, no sound source is wired to them
│                    yet) plus `AudioCategoryConfig` (`Assets/Data/Core/DefaultAudioCategoryConfig.asset`,
│                    one `AudioCategoryEntry` per category: native `AudioSource.priority`,
│                    a volume multiplier, and 3D `minDistance`/`maxDistance`), injected via
│                    `Bootstrapper.Initialize(AudioCategoryConfig)` exactly like the other
│                    mission configs. `PlaySfx` now takes a required `SfxCategory` and reapplies
│                    that category's priority/min/max-distance to the pooled source on every
│                    call — the pool is reused across categories, so these can't be set once at
│                    pool-creation time. `GetCategoryVolumeMultiplier`/`ApplyCategorySettings`
│                    exist for standalone (non-pooled) sources — `VehicleAnimationDriver`'s
│                    engine loop uses both: `ApplyCategorySettings` once when the loop
│                    `AudioSource` is first built (via a local `FindFirstObjectByType` fallback
│                    if `_audioManager` isn't cached yet, since `SetEngineSound` can run before
│                    this component's own `Start()` — see that method's doc comment), and
│                    `GetCategoryVolumeMultiplier` every frame alongside `EffectiveSfxVolume`.
│                    A category with no `AudioCategoryEntry` configured falls back to a sane
│                    default (`priority` 128, `volumeMultiplier` 1, `minDistance` 1,
│                    `maxDistance` 500 — Unity's own `AudioSource` defaults) rather than
│                    throwing. **Correction to the original spec**: the engine loop's
│                    `AudioSource` already had `spatialBlend = 1` (set since issue #29) — the
│                    `/ba` spec and `/arch` review both mistakenly characterized it as flat 2D,
│                    an error traced to an incomplete `grep -C 2` context window during
│                    speccing, not an actual code gap. What issue #33 actually added for the
│                    engine loop was category-driven priority/distance tuning and the
│                    zoom-height link, not `spatialBlend` itself.
├── TechManager    — Per-faction researched-upgrade tracking for the tiered tech tree
│                    (issue #13), reset every Economy Round entry exactly like
│                    `EconomyManager` (per-mission, never persisted across the campaign).
│                    `TechTreeConfig.factionTrees` (`FactionTechTree[]`, mirroring
│                    `MissionEconomyConfig.factionStarts`) gives Humans and Aliens
│                    genuinely separate trees, not one shared list. `UpgradeDefinition`
│                    is an abstract SO base (`displayName`/`tier`/`prerequisites`/cost,
│                    the same cost-field-+-`GetCost()` pattern as `UnitProductionDefinition`)
│                    with two concrete subclasses so far — `StatUpgradeDefinition`
│                    (registers a Flat- or Percentage-type modifier) and
│                    `UnlockUpgradeDefinition` (gates a `BuildingDefinition`/
│                    `UnitProductionDefinition`) — adding a third upgrade *type* later is
│                    one new subclass, no changes to `TechManager`/`TechPanel`.
│                    `TryResearch` validates prerequisites + affordability (via
│                    `EconomyManager`'s existing `ResourceStockpile.CanAfford`/`Spend`),
│                    then calls the upgrade's `Apply()`, which registers its effect back
│                    onto `TechManager` — `Apply()` never touches gameplay code directly.
│                    `GetStatMultiplier(Faction, UpgradeStatType)` combines every
│                    researched modifier for that stat as `(1 + sum(Flat)) * (1 + sum(Percentage))`
│                    — e.g. two Flat +0.1 and one Percentage +0.2 yields `1.2 * 1.2 = 1.44`.
│                    `IsUnlocked(Faction, building/unit)` defaults to **true** unless at
│                    least one `UnlockUpgradeDefinition` anywhere actually gates that
│                    specific building/unit — backward compatible with every building/unit
│                    that predates the tech tree, only locked once something explicitly
│                    references it. Stat multipliers are wired into gameplay (issue #14,
│                    shipped 2026-08-19): `Weapon` reads Damage/Range/FireRate live at every
│                    fire/acquire/cooldown (already read live pre-#14, just needed the
│                    multiplier inserted); `HarvesterGatherBehavior` reads GatherRate live
│                    every gather tick (same); `PowerSystem.CanAfford` gained an optional
│                    `capacityMultiplier` parameter, passed by `BuildMenuPanel`/
│                    `BuildingPlacement`. MoveSpeed and Health are different — neither was
│                    read live anywhere before #14 (both were one-time-baked-at-spawn), so
│                    both needed a genuinely new **event-driven** re-application instead of
│                    just inserting a multiplier: `TechManager.OnUpgradeResearched` fires
│                    after every successful `TryResearch`; `UnitBase` (new
│                    `SetBaseMoveSpeed(float)`, called by `HarvesterGatherBehavior`/
│                    `SabotageAction` instead of setting `Agent.speed` directly) and `Health`
│                    (new `OwnerFaction` property, mirroring `Weapon.OwnerFaction` exactly —
│                    `Health` had no owner/faction reference at all before #14) both
│                    subscribe and re-apply their affected stat only for their own faction.
│                    `Health`'s re-application is delta-preserving, not a full heal: a
│                    positive max-health increase adds the same delta directly to current
│                    health (an already-damaged unit gets the full numeric benefit without
│                    being topped off by an unrelated research) — verified directly
│                    (500/500 → take 50 damage → 450/500 → +20% Health research →
│                    550/600, not 600/600). **`PassiveGeneration` has no consuming code** —
│                    unlike the other seven `UpgradeStatType` values, no "passive resource
│                    generation" mechanic exists anywhere in this codebase (only active
│                    harvesting does); `TechManager` can track a multiplier for it, but
│                    nothing reads it, since inventing that mechanic wasn't part of #14's
│                    scope. Flagged as a real gap for whenever passive generation is designed.
                     **Capability-scoped multipliers (issue #39, shipped 2026-08-24):** a
                     researched `StatUpgradeDefinition` can now optionally target a subset of
                     units instead of always applying faction-wide, via a new `[Flags] enum
                     Units.UnitCapability` (`Ground`/`Flying`/`GroundAttacker`/`AirAttacker`/
                     `Gatherer`/`Builder`/`Saboteur` — `Ground`/`Flying` mutually exclusive by
                     convention, the rest independent). `UnitBase.Capabilities` computes this
                     once in `Awake()` entirely from existing signals (`IsFlying`, a `Weapon`'s
                     `Config.targetType`, `HarvesterGatherBehavior`/`SaboteurUnit` component
                     presence) — deliberately never a separately-authored tag, so it can't drift
                     out of sync with what a unit actually is. `Builder` has no consumer yet (no
                     unit sets it), same "ships ahead of its consumer" precedent as
                     `PassiveGeneration` above. `TechManager.RegisterStatModifier`/
                     `GetStatMultiplier` both gained an added `UnitCapability` parameter
                     (`requiredCapabilities` / `callerCapabilities`, defaulting to `None` =
                     unfiltered) — a modifier counts only if the caller's capabilities contain
                     *every* bit set in the modifier's own required set
                     (`(callerCapabilities & requiredCapabilities) == requiredCapabilities`),
                     so a `None`-registered modifier (every pre-#39 upgrade asset, unmigrated)
                     always counts regardless of what's passed. `StatUpgradeDefinition` gained
                     `targetCapabilities` (default `None`) passed straight through in `Apply()`.
                     The four existing per-unit `GetStatMultiplier` call sites (`Weapon`,
                     `Health`, `UnitBase`'s own `MoveSpeed`, `HarvesterGatherBehavior`) now pass
                     their owning unit's `Capabilities` — verified live: a `Ground`-filtered
                     Damage modifier applied a 1.5x multiplier to a `Ground`-capability caller
                     and correctly no-opped (1.0x) for a `Flying`-capability caller, while an
                     unfiltered modifier applied identically to both. **Known, documented
                     limitation:** a `Weapon` owned by a `BuildingInstance` (a defensive turret)
                     has no capability profile and always passes `UnitCapability.None` — a
                     capability-scoped upgrade never affects building-owned weapon stats, only
                     unit-owned ones. Not a bug — buildings weren't part of this issue's scope.
│                    Unlock-gating is wired into both build menus (issue #15, shipped
│                    2026-08-19): `BuildMenuPanel.Update()` and a new
│                    `ProductionMenuPanel.RefreshUnitLocks()` (called every `Update()`
│                    alongside the existing `RefreshProgress()`, since `Rebuild()` only runs
│                    on a building-set change and gating can flip independently of that) both
│                    check `TechManager.IsUnlocked` per entry and gate `Button.interactable`
│                    on it. A locked entry stays visible with a `" (Locked)"` label suffix
│                    (via `Button.GetComponentInChildren<Text>()`, cached alongside each
│                    button) rather than disappearing, per the issue's explicit "not silently
│                    missing" requirement. `ProductionMenuPanel` tracks each unit button in a
│                    new `UnitButtonEntry` struct (button, label, definition, owning
│                    building's faction) added to `BuildEntry`, since unlike `BuildMenuPanel`
│                    (one flat array built once in `Start()`) its buttons are rebuilt
│                    per-building inside a loop. Verified end-to-end in Play Mode with a
│                    temporary `UnlockUpgradeDefinition` gating a real building and unit:
│                    confirmed locked (non-interactable, "(Locked)" label) before
│                    `RegisterUnlockedBuilding`/`RegisterUnlockedUnit`, unlocked after, and an
│                    unrelated ungated building stayed interactable throughout.
├── UIManager      — Builds the entire Canvas/EventSystem/HUD hierarchy at runtime in code
                     (no hand-authored prefabs), matching the other managers' runtime-instantiation
                     convention. EventSystem uses InputSystemUIInputModule, not the default
                     StandaloneInputModule — required because this project's Input System settings
                     (activeInputHandler) disable the legacy Input Manager the default module depends
                     on. Hosts HUDController, which shows/hides the whole HUD on Economy/Battle Round
                     entry and owns ResourcePanel, SelectionPanel, BuildMenuPanel, ProductionMenuPanel,
                     MinimapPanel, TechPanel (all in UI/), plus MissionEndScreen (victory/defeat summary
                     on MissionComplete/GameOver) and MainMenu (title/faction-select/New Campaign/
                     Continue-stubbed/Quit, visible only in GameState.MainMenu). See UI/UIFactory.cs
                     for the shared runtime-UGUI-building helpers. **HUD corner layout** (reworked in
                     issue #19): ResourcePanel top-left (player-only — DebugHud's leftover dual-faction
                     OnGUI overlay was the "two overlapping resource displays" it was previously
                     confused with, not a second real panel), MinimapPanel top-right (moved off
                     BuildMenuPanel's old top-right slot), bottom-left holds both BuildMenuPanel and
                     ProductionMenuPanel stacked in one canvas — BuildMenuPanel docked at the very
                     bottom (its height is fixed for the whole mission, set once in `Start()`) with
                     ProductionMenuPanel directly above it via `BuildMenuPanel.PanelHeight` (a public
                     read-only property `ProductionMenuPanel` reads once per `Rebuild()`, not a
                     runtime-coordinated live layout) — stacked in this order specifically because
                     ProductionMenuPanel's own height is dynamic (grows/shrinks as production
                     buildings are built/lost), so only the dynamic panel needs to know about the
                     fixed one, never the other way around. SelectionPanel stays bottom-right (unit
                     stats/weapon/hotkeys, building stats/power/producible-units, main building, or
                     resource node remaining/total yield, plus the ten control-group indicators).
                     **Real bug fixed in #19:** `UIFactory.CreateRect` sets `pivot = anchor`, so for
                     any BottomRight/TopRight-pivoted child, `anchoredPosition.x` places that child's
                     *right* edge, not its left — `SelectionPanel`'s selection-count text, all ten
                     group indicators, and the detail card were authored assuming left-edge offsets,
                     so they rendered entirely outside/left of their own background panel. Fixed by
                     converting each to a right-edge offset (`left edge + width`); confirmed via
                     `RectTransform.GetWorldCorners` in Play Mode, not just by inspection.
                     **TechPanel** (issue #13) is the one HUD element that isn't
                     always-visible — hidden by default, toggled open/closed on the `T` hotkey
                     (`Keyboard.tKey`, gated on `GameManager.IsRoundActive` like every other input
                     system) rather than docked to a screen corner, since all five corners/positions
                     were already taken. Its own root `GameObject` stays active at all times so it
                     can keep listening for `T` even while its visual content (`Background`, a
                     centered `UIFactory.Center`-anchored panel) is hidden via `SetActive`. Entries
                     are built once from `TechManager.GetTree(Faction.Player)` (the tree's upgrade
                     set is fixed for the mission, unlike units/buildings) and refreshed each frame
                     only while open. **MinimapPanel** (issue #12) is icon-based, not a real rendered
                     view — colored square markers (`FactionColor.GetColor`) for units/buildings/main
                     buildings/resource nodes, positioned via `Mathf.InverseLerp`/`Lerp` against
                     `CameraManager.Config.boundsMin`/`boundsMax` (the same bounds Camera Manager uses
                     — no second "map size" definition), plus a translucent rect reflecting the main
                     camera's current visible area. Clicking it calls `CameraManager.JumpToPosition`.
                     Rebuilds its markers only when the tracked unit/building/main-building/node sets
                     change (compared via `SequenceEqual` against `UnitManager.ActiveUnits`,
                     `EconomyManager`'s building/main-building/node lists — all already-maintained
                     registries, never a scene-wide scan), matching `ProductionMenuPanel`'s existing
                     "rebuild only when the set changes" convention. `EconomyManager` gained a
                     world-wide `ResourceNode` registry (`RegisterResourceNode`/`DeregisterResourceNode`,
                     mirroring `RegisterBuilding`/`DeregisterBuilding`) and a per-faction `MainBuilding`
                     dictionary for this — `EconomyManager.CleanupExistingState()` must never touch
                     `ResourceNode`s (persistent map content, never destroyed on round reset), but as of
                     issue #22 it *does* also destroy stale `MainBuilding` instances on every fresh
                     Economy Round entry (the previously-documented "never cleaned up" staleness bug is
                     fixed — don't conflate the two: nodes stay permanently untouched by design, main
                     buildings are now cleaned up like everything else). `SelectionController`
                     also draws a live drag-select box overlay (a Canvas Image positioned via
                     `RectTransformUtility.ScreenPointToLocalPointInRectangle`), resolves a single
                     click into a unit/building/main-building selection (units, buildings, and the
                     main building all share the same physics layer — see `_unitLayerMask`) via one
                     raycast, falling back to a second raycast against `_resourceNodeLayerMask`
                     (`ResourceNode`s sit on the Default layer, not layer 9, and have no owning
                     faction — see `memory/PATTERNS.md`'s 2026-08-18 "Resource node selection" entry)
                     if the first raycast didn't resolve to anything, and gates all input (along with
                     `AttackCommand`/`MoveCommand`/`ControlGroupController`) on `GameManager.IsRoundActive`
                     and `EventSystem.IsPointerOverGameObject()`. A player-owned hit populates the
                     existing `SelectedBuilding`/`SelectedMainBuilding` (and, for units, the real
                     commandable `UnitManager.SelectedUnits`); an **enemy-owned** hit (issue #24)
                     populates entirely separate read-only properties instead —
                     `SelectedEnemyUnit`/`SelectedEnemyBuilding`/`SelectedEnemyMainBuilding` — deliberately
                     never touching `UnitManager.SelectedUnits` or the player-scoped building fields,
                     since every order-issuing system reads only those with no faction check of its own;
                     an inspected enemy unit's own selection ring is toggled directly via `SetSelected`
                     rather than through `UnitManager`. Both building/main-building and resource-node
                     selection (player-owned or enemy) reuse the same shared `Utilities.SelectionIndicator`
                     ring, reparented onto whichever is selected. Exposes a public `IsDragging` bool, read
                     by `CameraManager` to suppress edge-of-screen panning during an in-progress
                     drag-select box.
├── VisionManager  — Fog of War (issue #36, shipped 2026-08-22): computes each faction's
                     currently-visible area every tick as the union of vision-radius circles
                     around that faction's alive units (`UnitBase.VisionRadius`, a new
                     serialized field defaulting to 10, same shape as `_isFlying`), buildings
                     (`BuildingDefinition.visionRadius`, new — replaced, not added to, by the
                     existing `detectionRadius` when `isDetectionCapable` is true, finally
                     giving that dormant "Detection (Espionage counter)" field a real job),
                     and main building (`MissionEconomyConfig.FactionEconomyStart.mainBuildingVisionRadius`,
                     new, defaulting to 15, plumbed through `MainBuilding.Initialize`). Tracks
                     a per-faction 3-state grid (`Vision.FogState`: Unexplored/Explored/Visible)
                     sized from `CameraManager.Config`'s map bounds at the same 5×5 cell size
                     as the Terrain Tilemap (issue #17), so a future renderer/consumer never
                     needs a second definition of "the grid." Both factions' vision is computed
                     symmetrically (mirroring how Economy Round gives the Enemy a full parallel
                     economy) but only the Player faction's fog currently drives anything —
                     enemy AI decision-making doesn't exist yet to consume its own. **Reset
                     policy**: resets every faction's grid to fully Unexplored on every fresh
                     `GameState.EconomyRound` entry, subscribing to `GameManager.OnGameStateChanged`
                     exactly like `EconomyManager` does for its own per-round state — required
                     per the arch review, since `EconomyManager.InitializeFactionStates()` is
                     confirmed (via issue #34) to fire more than once per session, and a fog
                     system with no matching reset policy would either leak stale exploration
                     into a mission retry or get wiped unexpectedly by the same double-init
                     quirk. Verified directly in Play Mode: drove a real `EconomyRound` ->
                     `BattleRound` -> `MissionComplete` -> `EconomyRound` cycle and confirmed a
                     previously-`Explored` cell reset to `Unexplored`. Filters out units/buildings
                     whose `Health.IsDead` is already true but not yet destroyed (Unity's
                     deferred `Destroy()`), so a just-killed vision provider doesn't linger for
                     up to a frame. `IsVisible`/`GetFogState` are O(1) grid lookups (not
                     recomputed per call), since `Weapon.Update()` already iterates its
                     candidate list once per frame — issue #37 will call this per-candidate
                     inside that existing loop. **Rendering (corrected 2026-08-22 — see below)**:
                     `Vision.FogOfWarRenderer` builds a single flat quad mesh (in local XZ
                     plane, normal +Y, same convention as `Utilities.RingMesh` — no rotation
                     needed) covering `VisionManager`'s full grid extent, positioned at world Y
                     `0.05` (above `Terrain`'s `0.01` and the unit/selection rings' `0.02` —
                     confirmed necessary directly: at Y `0` it Z-fights with `Ground` and
                     produces visible banding artifacts at fog-state boundaries). It samples a
                     small `Texture2D` (one pixel per grid cell, `FilterMode.Bilinear`) built
                     from `VisionManager`'s per-cell `FogState`, updating only the pixels whose
                     state actually changed each tick. The material is `Universal Render
                     Pipeline/Unlit` with `_Cull Off` (the exact pattern `Utilities.RingMesh`
                     already established) plus a standard alpha-blended Transparent surface
                     setup. Bilinear filtering blends between the grid's cells automatically,
                     giving a soft "edge of vision" gradient with no extra code. **This replaces
                     a first implementation that used a Tilemap** (a `FogOfWar` Grid/Tilemap
                     sibling to the `Terrain` layer, using `Tilemap.SetColor`) **which never
                     actually rendered anything, in this project's real Play sessions** — found
                     only once a human played it and saw no shroud at all, and confirmed via
                     extensive direct pixel-level `RunCommand` testing (a brand-new Tilemap,
                     built from scratch in isolation, with every material/sorting-layer/render-
                     mode combination tried, never drew a single pixel; the identical
                     `MeshRenderer`+quad approach with the same shader family, by contrast,
                     rendered correctly on the first clean attempt). The most likely root cause:
                     this project's actual URP asset (`PC_RPAsset`) is configured with a 3D
                     `UniversalRendererData`, not the 2D Renderer `CLAUDE.md`'s own Key
                     Conventions call for ("URP shaders only, 2D Renderer") — the Tilemap's
                     auto-assigned `Universal Render Pipeline/2D/Sprite-Unlit-Default` shader is
                     2D-Renderer-specific and apparently doesn't draw under the 3D Universal
                     Renderer actually in use. **Not fully re-confirmed as the definitive root
                     cause** (the diagnostic session hit its own confounds — see
                     `memory/PATTERNS.md`'s 2026-08-22 entry for the full trail — including
                     Unity's deferred `Destroy()` leaving a leftover test object that
                     contaminated one intermediate result) — but the practical takeaway holds
                     regardless: **avoid `Tilemap`/`TilemapRenderer` for any future visual
                     layer in this project; use a `MeshRenderer` + plain mesh instead**, and
                     verify any new visual system by reading back actual rendered pixel values
                     (`Camera.Render()` to a manually-created `RenderTexture`, read back via
                     `Texture2D.ReadPixels`) rather than trusting `Tilemap`/component-level data
                     queries alone — those consistently reported correct values throughout even
                     while nothing was actually drawing on screen. **Known gap**: resource nodes
                     are visually dimmed by the fog shroud like everything else, even though the
                     original spec says they should always be fully visible — the single-quad
                     approach has no clean way to carve out an exception per-object; flagged,
                     not fixed, pending a design decision on whether that's actually still
                     desired. **Unit
                     visibility**: `UnitBase` gained an `Update()` (previously had none) that
                     hides an Enemy-owned unit's art + faction ring entirely outside the Player's
                     current vision — 2-state only, never remembered once out of sight, since a
                     frozen "ghost" of something that moves would be misleading. **Building
                     visibility**: `BuildingInstance`/`MainBuilding` share a new
                     `Utilities.FogVisibility.Apply` helper for the full 3-state treatment: full
                     detail while Visible, hidden real renderers + a dimmed static
                     `Utilities.FogGhostMarker` (a filled disc — `RingMesh.Create(0, radius)`
                     collapses cleanly into one, no new mesh code needed — tinted via a solid
                     dimmed color rather than alpha transparency; transparent URP materials are
                     now confirmed to work fine in this project — see the Rendering note above —
                     so this is just a simplicity choice at this point, not a risk-avoidance one)
                     at the last-known position while
                     Explored, nothing while Unexplored. **Minimap**: `MinimapPanel` now gates
                     enemy markers the same way (units require full Visible, buildings/main
                     buildings accept Visible or Explored) — its old "only rebuild when the
                     tracked set's membership changes" optimization no longer holds, since fog
                     state can change every tick with no membership change at all, so it now
                     rebuilds every tick instead (cheap at this project's current unit-count
                     scale). **Fog-gated combat/inspection** (issue #37, shipped 2026-08-22):
                     `Weapon.ResolveTarget()`'s two paths (the explicit target lock from issue
                     #31, and `AcquireNearestEnemy()`'s per-candidate `TryConsiderTarget`) both
                     require `VisionManager.IsVisible(OwnerFaction, ...)` in addition to their
                     existing range/target-type checks — a nearer hidden enemy never blocks a
                     farther visible one from being found, since the check is per-candidate,
                     not a final gate on whatever's nearest overall. Verified live: a target in
                     weapon range but outside a (temporarily shrunk) vision radius took zero
                     damage over several forced fire ticks; restoring vision let the very same
                     weapon engage it immediately. `SelectionController`'s enemy-unit/building/
                     main-building branches (issue #24) and `AttackCommand`'s enemy-attack
                     branches both gained the identical `IsVisible` check — a hidden enemy hit
                     resolves as if nothing were there for selection (verified live through the
                     real raycast path via a temporary debug wrapper, per issue #24's own
                     established verification method — confirmed both a hidden-enemy click
                     selects nothing and a visible-enemy click selects correctly), and for
                     `AttackCommand` specifically, an attack order on a hidden enemy falls
                     through to the existing ground-raycast fallback and becomes a plain Move
                     order instead (verified by code trace, not a live click simulation — same
                     `IsVisible` primitive already proven correct twice above).
└── EnemyAIController — The Enemy faction's AI, one module per unit type/system (issue #79,
                     shipped 2026-09-01, first module): only ticks while
                     `GameState.EconomyRound` is active, on a designer-tunable interval
                     (`EnemyAIEconomyConfig.decisionIntervalSeconds`, default 2s) rather than
                     every frame — resets its decision timer on every fresh Economy Round entry
                     via the same `OnGameStateChanged`-subscription convention `EconomyManager`/
                     `VisionManager`/`TechManager` already use. Composed of small plain classes
                     per module (mirroring `EconomyManager`'s `EconomyRegistry`/
                     `UnitUpkeepTracker` split, issue #61), not one growing script — future
                     staggered modules (Saboteur espionage, Crewman build/repair, combat units)
                     will land as new sibling classes here, once each unit type's player-
                     controlled behavior is independently validated first, per the agreed
                     process. **`AI/EnemyEconomyAI`** (the first module): each decision tick,
                     tops up the Enemy Main Building's `ProductionQueue` with Harvesters up to
                     `EnemyAIEconomyConfig.targetHarvesterCount` (default 3, counting alive +
                     already-queued so it never overshoots), then assigns every Enemy Harvester
                     whose `HarvesterGatherBehavior.NeedsNodeAssignment` is true (freshly
                     produced, or its node just depleted) to the nearest resource node with
                     remaining yield — reusing the exact `AssignToNode`/`ProductionQueue.TryEnqueue`
                     entry points a player's own UI already drives, so `HarvesterGatherBehavior`'s
                     gather/deposit state machine itself needed zero changes. Affordability is
                     left entirely to `TryEnqueue`'s own existing check — an unaffordable request
                     just fails and is retried next tick, no separate `CanAfford` call needed.
                     **Real gap found and fixed during `/arch`, not assumed correct from the
                     spec's own framing:** `UnitProductionDefinition` carries no unit-type
                     marker (only `displayName`/`prefab`/`enemyPrefabOverride`) — with exactly
                     one entry in `MainBuilding.ProducibleUnits` today, a naive "take the
                     first/only entry" would have worked by coincidence and broken silently the
                     moment a second producible unit is ever added to the Main Building. Fixed
                     by identifying the Harvester definition via a real component-type check on
                     the resolved prefab (`definition.GetPrefab(Faction.Enemy).GetComponent<HarvesterUnit>() != null`)
                     instead of a name or positional assumption — the same lookup every future
                     staggered AI module will need for its own unit type. `HarvesterGatherBehavior`
                     gained one new public accessor, `NeedsNodeAssignment` (`_state == State.Idle
                     && (_assignedNode == null || _assignedNode.IsDepleted)`), the only touch to
                     previously-shipped code this issue needed. Verified end-to-end in Play Mode
                     via direct `EnemyEconomyAI.Tick()` calls against the real running scene (this
                     environment's frozen/limited-tick player loop meant `ProductionQueue.Start()`
                     — self-added by `MainBuilding.Start()`, issue #41 — hadn't run yet on the
                     Enemy's Main Building at test time; forced via the established
                     `SendMessage("Start", ...)` workaround, not a real gameplay concern since
                     `decisionIntervalSeconds`'s 2-second default gives Unity's own next-frame
                     `Start()` pass ample time to run first in actual play): starting from
                     TestSetup's one pre-spawned, unassigned Enemy Harvester, three successive
                     ticks correctly queued Harvesters up to exactly the configured target
                     (queue count 0→1→2, Enemy Ore 50→35→20, exactly matching
                     `HarvesterProductionDefinition`'s 15-Ore cost each time) and then correctly
                     stopped queuing once alive+queued reached the target, holding stable across
                     further ticks; the pre-existing idle Harvester's `NeedsNodeAssignment`
                     flipped from `true` to `false` the instant the first tick ran, confirming
                     real node assignment, not just a queued production side effect.
                     **`/ux` correction (same day):** the first version called
                     `GetComponent<ProductionQueue>()`/`GetComponent<HarvesterGatherBehavior>()`
                     from inside `EnemyEconomyAI`'s decision-tick methods — technically inside
                     `Update`'s call chain (throttled to `decisionIntervalSeconds`, but still a
                     violation of this file's non-negotiable "no `GetComponent` in `Update`"
                     rule, and the first of several staggered AI modules that would have
                     inherited the pattern). Fixed by exposing both as cached public properties
                     on their owning types instead — `MainBuilding.Production` (set alongside
                     the existing `AddComponent<ProductionQueue>()` self-add in `Start()`,
                     mirroring `HealthComponent`'s existing cached-property shape) and
                     `HarvesterUnit.GatherBehavior` (exposing the field `Awake()` already
                     cached). The one remaining `GetComponent` call — checking a resolved
                     *prefab asset's* component to identify which `ProducibleUnits` entry is
                     the Harvester — is resolved once and cached on the `EnemyEconomyAI`
                     instance itself (`_cachedHarvesterDefinition`), never re-searched on later
                     ticks, since a mission's producible-units list never changes mid-mission.
```

### Folder Structure (shipped)
```
Assets/Scripts/
├── Core/    — Bootstrapper, GameManager, GameState, CameraManager, CameraConfig, AudioManager
├── Units/   — UnitBase (Faction, selection, NavMeshAgent, cached Health, IsFlying — issue
│              #25, disables agent-to-agent avoidance for a flying unit but keeps it on the
│              same NavMesh pathing as everything else; no unit in the roster uses it yet),
│              UnitManager (SetSelection/ClearSelection/MoveSelectedTo/StopSelected — the
│              latter two are the choke point every order funnels through, so they're also
│              where an active PatrolBehavior gets cancelled — issue #26), SelectionController
│              (click/drag-box select), MoveCommand (Stop hotkey only — see below),
│              ControlGroupController, UnitOrderCommand (Attack-Move `A`/Patrol `P`/Defend
│              `D`/Return to Base `H` — issue #26; Attack-Move and Patrol arm a pending mode
│              resolved by AttackCommand's next right-click(s), never left-click — see that
│              issue's architecture review for why), PatrolBehavior (added dynamically only to
│              a unit actually given a Patrol order; loops an ordered waypoint list forever),
│              AttackCommand (dispatches every right-click to the first
│              `IRightClickOrderResolver` in its ordered list that consumes it — issue #60,
│              replacing a hand-maintained `if/return` chain that had grown to 7 order types
│              across issues #23/#26/#38/#42/#46 plus the original #5/#31 Attack/Move — see
│              `Units/OrderResolvers/` below for the resolver classes and their priority
│              order), Health, Weapon, WeaponConfig, BulletEffect, CasingEjectEffect,
│              HarvesterUnit, SaboteurUnit, CrewmanUnit (issue #42, no Weapon — pairs
│              CrewmanRepairBehavior + CrewmanBuildBehavior, see Economy/), CombatUnitConfig,
│              CombatUnitStats (issue #43 — Ranger/Reaper/Dredge use plain UnitBase + Weapon,
│              same as TestCombatUnit, with CombatUnitStats applying max health/move speed
│              from a CombatUnitConfig asset in Start(), the first time combat units got real
│              MoveSpeed/Health stat-multiplier support instead of a directly-serialized
│              NavMeshAgent.speed), DamageType (issue #55 — Physical/Plasma enum, fixed per
│              weapon for its lifetime; see EconomyManager's Technology/upgrade-trees entry
│              for the full shipped shape)
├── Units/OrderResolvers/ — `IRightClickOrderResolver` (issue #60: `bool TryHandle(Ray ray)`)
│              and its 7 implementations, in the exact priority order `AttackCommand.Start()`
│              adds them: `PendingPlacementOrderResolver`/`PendingUnitOrderResolver` (thin
│              wrappers around `BuildingPlacement`/`UnitOrderCommand`'s own pre-existing
│              `TryHandlePendingPlacementClick`/`TryHandlePendingOrderClick` — both already
│              matched the `(Ray, LayerMask) -> bool` shape, so wrapping needed zero changes
│              to either component), `RallyPointOrderResolver`, `HarvestRedirectOrderResolver`,
│              `SabotageOrderResolver`, `RepairOrderResolver`, `AttackOrMoveOrderResolver`
│              (the fallback Attack/Move resolution, plus the `SetExplicitTargetOnSelection`/
│              `IsVisibleToPlayer` helpers it alone needs). Each resolver owns whatever
│              raycast(s)/layer masks it needs internally rather than sharing one precomputed
│              hit — deliberate, since right-click is a once-per-event input, not a per-frame
│              hot path, so a few independent raycasts per click cost nothing measurable.
│              `PendingPlacementOrderResolver`/`PendingUnitOrderResolver` are only added to
│              `AttackCommand`'s resolver list if their backing component actually exists in
│              the scene, preserving the pre-refactor code's own null-guarded behavior. Zero
│              behavior change from before this refactor — verified via direct `TryHandle(ray)`
│              calls against real scene state (Sabotage/Repair/Harvest-redirect selection-type
│              gating, the ground-fallback Move path, and the Attack path's vision-gate
│              correctly declining an unseen enemy), not simulated clicks — this project's
│              `Mouse.current` input cannot be synthesized in `unity-mcp` sessions, the same
│              established limitation as every other click-driven system here.
├── Economy/ — ResourceType, ResourceStockpile, BuildingDefinition, HarvesterConfig,
│              ResourceNodeDefinition, MissionEconomyConfig, UnitProductionDefinition,
│              FactionEconomyState, BuildingInstance, MainBuilding, ResourceNode,
│              EconomyManager, PowerSystem, BuildingPlacement, ProductionQueue,
│              HarvesterGatherBehavior, SaboteurConfig, SabotageEffectConfig,
│              ISabotageTarget, SabotageAction, TechManager, UpgradeDefinition,
│              StatUpgradeDefinition, UnlockUpgradeDefinition, UpgradeStatType,
│              FactionTechTree, TechTreeConfig, MapBrushDefinition, MapBrushPalette,
│              IProductionHost (issue #41 — the minimal Faction/IsDisabled/RallyPoint
│              surface ProductionQueue needs, implemented by both BuildingInstance and
│              MainBuilding), CrewmanConfig, CrewmanRepairBehavior, CrewmanBuildBehavior,
│              IRepairTarget (issue #42 — Faction/Position/HealthComponent, implemented by
│              BuildingInstance and MainBuilding, the repair-side mirror of ISabotageTarget),
│              BuildingPlacementGhost (translucent stripped-prefab placement preview, follows
│              the cursor while a building is pending, tinted by overlap validity),
│              AutoGatherBehavior (issue #51 — the Auto-Extractor's passive per-second
│              node extraction, a BuildingInstance-sibling component), LockedResourcePool
│              (issue #52 — per-faction, per-resource-type locked pool runtime tracker),
│              PlayerStartPoint (issue #57 — Map Editor-painted Faction marker EconomyManager
│              looks up to place each faction's Main Building; no runtime visual — its Scene
│              view visibility comes from MapEditorWindow.DrawStartPointMarkers, not its own
│              OnDrawGizmos, per that issue's same-day visibility fix), EconomyRegistry (issue
│              #61 — main-building/resource-node registries, plus building-registration/
│              power-footprint methods that operate on a caller-supplied FactionEconomyState
│              rather than owning a second copy of it), UnitUpkeepTracker (issue #61 — the
│              per-faction fractional upkeep accrual/drain tick, extracted from EconomyManager)
├── Battle/  — BattleManager, VictoryConditionChecker
├── Campaign/  — not started (mission definitions, story/progression)
├── UI/        — UIFactory (runtime UGUI-building helpers), UIManager, HUDController, ResourcePanel,
│                SelectionPanel, BuildMenuPanel, ProductionMenuPanel, MinimapPanel, TechPanel,
│                ResourceAllocationPanel (issue #52 — locked-pool allocation control, hotkey `R`),
│                InjectionPanel (issue #53 — locked-pool-funded tech research, hotkey `I`)
├── Vision/    — FogState (enum: Unexplored/Explored/Visible), VisionManager, FogOfWarRenderer
│                (issue #36, see the Architecture section's VisionManager entry for the full
│                shipped shape)
├── AI/        — EnemyAIController (issue #79, see the Architecture section's entry for the
│                full shipped shape), EnemyEconomyAI (first module — Harvester production +
│                node assignment), EnemyAIEconomyConfig (target Harvester count, decision
│                cadence). The landing spot for every future staggered per-unit-type AI
│                module (Saboteur, Crewman, combat units) as sibling classes.
└── Utilities/ — FactionColor, RingMesh, SelectionIndicator, RallyPointMarker (issue #23 —
│                a small per-instance colored ring at a production building's rally point;
│                unlike SelectionIndicator's one shared reused instance, each building with
│                an active rally point gets its own marker, since several can exist at once),
│                FogVisibility, FogGhostMarker (issue #36 — shared 3-state fog-visibility
│                toggle + remembered-structure marker for BuildingInstance/MainBuilding),
│                plus manual-testing scaffolding not meant to ship: DebugHud (OnGUI
                 debug overlay, predates the real UI/ HUD — its scene GameObject was disabled in
                 issue #19 once it started drawing over the real ResourcePanel; the script itself
                 stays available for manual re-enabling), TestBuildHotkeys (B/N hotkey building
                 select, predates the real build menu), TestSetup (auto-starts Economy Round and
                 spawns starter units on Play Mode entry)

Assets/Editor/ — first Editor-tooling script in this project (issue #18, shipped 2026-08-19):
MapEditorWindow (custom EditorWindow + Scene View tool, MechTS/Map Editor menu item). Any script
placed under a folder literally named Editor/ (anywhere in the hierarchy) is automatically
Editor-only and excluded from player builds — no .asmdef needed for that. NavMeshBakeUtility
(issue #35, MechTS/Bake NavMesh menu item) bakes at a finer voxel size than the project default
and writes the result into the scene's persisted NavMesh.asset — see the Terrain Elevation entry
above; use this instead of the Navigation window's Bake button on any map with elevation.
```

Manager access pattern (as shipped):
```csharp
// Cache in Start (not Awake — Bootstrapper's managers aren't guaranteed to exist yet during Awake)
private EconomyManager _economyManager;
void Start() { _economyManager = FindFirstObjectByType<EconomyManager>(); }
```

### 3D Model Conversion Convention (issue #65, shipped 2026-08-29)
The first real conversion of the roster from flat 2D sprites to genuine 3D models, following #64's confirmed GO — one vertical slice (`MainBuilding`, `EconomicBuilding`, `HarvesterUnit`, `CrewmanUnit`, all Player faction) using `Assets/ScifiRTSSeriesMegaPackIII/` (`CommandTowerBlue`, `ResourceExtractorBlue`, `ResourceCollectorBlue`, `EngineerLvl3StaticBlue` respectively). Camera stays pure top-down orthographic for this issue — the angled-camera rework is a separate, later issue in this sequence, deliberately sequenced after enough real 3D content exists to validate it against.

**The 3D convention is a real departure from the 2D Sprite & Art Convention above, not an extension of it**: a nested `Art` prefab instance under the unit/building root, at local position `(0,0,0)` with **identity rotation and identity scale** — no 90°-X flattening (that convention exists specifically for flat sprites lying on the ground plane; a real 3D model is already upright geometry and needs none of it), no `Sprites-Default` material requirement (the model's own PBR materials apply, converted to URP `Lit` via Unity's Render Pipeline Converter per #64), no Y-offset nudge (real geometry has genuine volume, doesn't compete for the same depth plane as `Ground`). The gameplay `Collider`/`NavMeshAgent` stay sized independently of the visual model's real footprint, matching the pre-existing unit convention (`HarvesterUnit`/`CrewmanUnit` already worked this way before #65) — the `Art` child's actual rendered `Bounds` are measured directly (`Renderer.bounds` across every renderer in the nested instance) to size a *building's* `BoxCollider` to match, since buildings previously had no independent art-vs-collider separation at all.

**Two real, precedent-confirmed bugs caught and fixed, not rediscovered the hard way:** (1) `MainBuilding.prefab`/`EconomicBuilding.prefab` were both flat single-GameObject placeholder cubes (`MeshFilter`/`MeshRenderer`/`Collider`/the gameplay script all on one object, no `Art` child) with **non-uniform root scale** — `(3,2,3)` and `(2,1.5,2)` respectively, the *exact* scale issue #45's turret-prefab stretching bug already happened on. Both were restructured to root+`Art`-child with the root reset to uniform `(1,1,1)` scale before the fix could recur, and their `BoxCollider` resized directly from the new `Art` child's measured bounds (`MainBuilding`: `CommandTowerBlue` measures `6.55×9.52×8.33`; `EconomicBuilding`: `ResourceExtractorBlue` measures `2.12×2.22×0.98`). (2) `HarvesterUnit.prefab`'s `VehicleAnimationDriver._tracks` was `[SerializeField]`-wired directly to two `WheelAnimation` components living inside the sprite `Art` child being replaced — the same dangling-reference bug shape already documented for the Enemy-variant art swap. Cleared via `SerializedObject`/`ClearArray()` *before* removing the old `Art` child, rather than after — `VehicleAnimationDriver` itself needed no code change, since its `Update()` loop over `_tracks` is already a no-op on an empty list; `SetEngineSound`/the engine-audio hookup (`HarvesterGatherBehavior.Start()`'s existing call) is unaffected and still functions, since real 3D models have no `WheelAnimation`/`Vibration`/`Smoke` at all and don't need the track-driving half of that component. `ResourceCollectorBlue`'s `CombineChildren` (the legacy runtime mesh-merge utility flagged during #64 as a dangling-reference risk for any future named-part reference) was stripped at the **source** prefab in `Assets/ScifiRTSSeriesMegaPackIII/`, not just the copy nested under `HarvesterUnit` — benefits any future use of that model. Confirmed via direct inspection that `CombineChildren` is *not* universal across the pack — present on `ResourceCollectorBlue`, absent on `CommandTowerBlue` and `EngineerLvl3StaticBlue` — check per-prefab, don't assume.

**Lighting**: the scene had zero `Light` components (an all-unlit-sprite project never needed one) — added one real `Directional Light` (intensity 1.5, soft shadows). Unlike #64's exploratory finding (which needed the Global Volume *disabled* to see anything, using an under-tuned intensity of 1.2), a real intensity-1.5 light alone was sufficient against the existing Volume Profile's actual settings (`Tonemapping` Neutral, `Bloom` 0.25, `Vignette` 0.2 — all comparatively mild on inspection) — the Volume didn't need touching at all once the light was properly set. Verified via a real Play Mode session with the actual shipped prefabs (not test copies): `MainBuilding`/`HarvesterUnit`/`CrewmanUnit` all spawn correctly for **both** factions via `TestSetup`'s normal path — confirming `EnemyHarvesterUnit`/`EnemyCrewmanUnit` (separate prefabs, untouched, still 2D Alien-sprite-art per the existing Enemy-variant convention) continue working unaffected, exactly as scoped. `VehicleAnimationDriver.Update()` force-ticked 10 times via `SendMessage` on the real spawned Player Harvester with zero exceptions, directly confirming the cleared `_tracks` fix holds under real repeated execution, not just a single frame.

**Follow-up (issue #66, shipped 2026-08-29, corrected same day per issue #67): track rotation + gating the model's own real claw animation, not a hand-rolled claw rotation.** The first version of this (`Units/HarvesterPartAnimator.cs`) hand-animated the claws with a sine-wave oscillation script — wrong, caught by the designer directly inspecting the imported model: `ResourceCollectorBlue`'s `Art` child ships with a real `Animator` + `AnimatorController` (`ResourceCollectorBlue.controller`) already assigned, whose one real state (`ResourceHarvesting`, its default state) plays a genuine artist-authored `AnimationClip` (`ResourceHarvesting.anim`, 1.5s looping) with real quaternion keyframes on **both** `ResourceCollectorClawLeft` and `ResourceCollectorClawRight` — confirmed directly via `AnimationUtility.GetCurveBindings`, not assumed. The hand-rolled script was fighting a better, already-authored animation on the exact same transforms. **The actual gap**: the pack's `AnimatorController` has zero parameters, so nothing gates its one state — as shipped, it would loop the grab animation constantly regardless of whether the unit is actually gathering. Fixed by rewriting `HarvesterPartAnimator` to toggle `Animator.enabled` based on `HarvesterGatherBehavior.IsGathering` instead of touching the claw transforms directly — `true` lets the real clip play, `false` resets to the state's first frame (`Animator.Play(...,0,0f)` + `Animator.Update(0f)`) before disabling, so it never freezes mid-grab. Track rotation (a `_tracks` transform spun around local X, proportional to `NavMeshAgent` speed) is unchanged from the first version — confirmed via the same clip-binding inspection that the pack's animation covers only the claws, nothing for the tracks, so that part of the original approach was correct and still needed. `HarvesterConfig`'s field group is now just `trackRotationSpeed` — the claw-specific tuning fields from the first version were removed since the claw animation plays at its own authored speed now, no tuning needed. **Verified this time with real evidence**: manually drove the actual `Animator` (`Play`/`Update` at specific normalized times, not the private gating logic) on a live instance and captured it at two points through the clip, confirming the claws visibly move between frames — the real animation genuinely plays and isn't broken. The `IsGathering`-driven toggle logic itself is still unverified live end-to-end (same documented frozen-player-loop/NavMesh limitation as before — `Agent.pathPending`/`remainingDistance` never resolve even for a trivial zero-distance path in this environment), but the two halves it depends on (the real clip playing correctly, and the toggle-off-idle path producing zero exceptions across 10 forced ticks) are both now confirmed directly. **Takeaway for any future pack-sourced unit conversion: check whether the source model already ships a real `Animator`/`AnimatorController` with actual clips before writing any hand-rolled animation script** — inspect via `AnimationUtility.GetCurveBindings` on its clips, don't assume a bundled `Animator` component is empty/unused just because the `AnimatorController` has no parameters to drive it externally.
**Third correction (issue #68, shipped 2026-08-29, same day): track motion was rotating the wrong thing entirely.** #66's track animation (`Transform.Rotate` on the `_tracks` object each frame) shipped without ever being seen live — the first real gameplay session showed the tracks visibly "just spinning" rather than reading as rolling. Root cause, found by directly inspecting `ResourceCollectorRTSTracks`'s actual mesh: it's a flat plate whose UVs tile the tread pattern many times along U (`Mesh.uv` range roughly `[-6.6, 7.8]`) while V stays in a tight `0-1` band — the standard setup for a **scrolling tread texture**, the classic real-game technique for faking tank-tread motion on a flat plate, not literal mesh rotation (which reads as tumbling for a flat shape, exactly what "just spinning" was). Fixed by replacing the `Transform.Rotate` call with a per-instance `MaterialPropertyBlock` scroll of `_BaseMap_ST`'s offset (zw components), preserving the material's real authored tiling (xy, cached once in `Start()` from `sharedMaterial.mainTextureScale`) rather than hardcoding it — never mutates the shared material asset, so every Harvester scrolls independently. `HarvesterPartAnimator`'s serialized field changed from a `Transform _tracks` to a `Renderer _tracksRenderer`; `HarvesterConfig.trackRotationSpeed` (degrees/sec) renamed to `trackScrollSpeed` (texture-widths/sec). **Verified with real pixel-level proof, not just visual inspection**: two static side-by-side captures at different offsets looked nearly identical to the eye (subtle texture, small render scale) — confirmed the mechanism genuinely works instead via direct pixel sampling at the same world point at two different offset values, showing a real, measurable color difference. This is expected and correct: continuous per-frame scrolling reads as motion over time even when two isolated static frames look nearly the same side by side — the same reason a real spinning wheel photographed at two random instants doesn't obviously prove it's spinning either. **Takeaway: for any future flat-plate "wheel"/"tread"/"conveyor" mesh from a pack, check the mesh's actual UVs (`Mesh.uv` range) before choosing between rotating the geometry and scrolling its texture** — a UV range that tiles far past `0-1` on one axis while staying tight on the other is the signal that texture-scrolling, not mesh rotation, is the intended technique.

**Enemy/Alien faction equivalent remains explicitly out of scope** — this pack has only faction-color variants of one generic sci-fi human-tech aesthetic, no alien silhouette; MechTS's Enemy faction is thematically Alien, not just a different paint job on the same tech. A real, separate art-sourcing question for later.

**Faction color convention for real 3D art (established 2026-09-02, issues #81/#82): color comes from which pre-authored model is instantiated, never from a runtime tint.** Real-art, faction-owned prefabs are organized into color-named folders — `Assets/Prefabs/Blue/` for Player, `Assets/Prefabs/Red/` for Enemy (Mega Pack III's own Blue/Red color variants are the source for each, per the Enemy-recolor decision covering the alien-art gap until real alien models are sourced separately). This is a deliberate departure from `Utilities/FactionColor.Apply` (`renderer.material.color = color` on every `MeshRenderer`), which is a placeholder-tint mechanism for content with no real per-faction art yet — its own doc comment already said as much, but nothing enforced it once real 3D `MeshRenderer` art started shipping via issue #65. Confirmed as a real, live gap during #82's `/arch` review: `FactionColor.Apply` is called unconditionally from three sites (`CrewmanBuildBehavior.BeginConstruction`, `EconomyRegistry`'s Main Building spawn, `ProductionQueue`'s unit production) with no check for whether the spawned prefab already has real per-faction art — meaning it was silently multiplying a flat tint onto `MainBuilding`/`HarvesterUnit`/`CrewmanUnit`'s real material the whole time since #65, undetected. **Any future real-art conversion must ensure `FactionColor.Apply` is skipped for that content once it has a genuine Blue/Red (or other real per-faction) prefab pair** — the folder split is the mechanism, not an additional layer on top of it. `BuildingDefinition` needed a new `enemyPrefabOverride`/`GetPrefab(Faction)` pair to even support this (mirroring `UnitProductionDefinition`'s existing identical pattern) — it had no per-faction prefab concept at all before #82.

**Enemy Faction Core 3D Conversion (issue #81, shipped 2026-09-02):** closes the gap #65 left open — Enemy's Main Building, Harvester, and Crewman are now real 3D Mega Pack III models in the Red color variant, matching Player's Blue. `MainBuilding.prefab`/`HarvesterUnit.prefab`/`CrewmanUnit.prefab` moved to `Assets/Prefabs/Blue/`; a new `Assets/Prefabs/Red/EnemyMainBuilding.prefab` was created (Player and Enemy previously shared the *literal same* Main Building prefab — confirmed via identical GUID in both `TestMissionEconomyConfig.asset` faction entries — so this was a real gap, not just an art swap: `TestMissionEconomyConfig`'s Enemy `mainBuildingPrefab` field now points at the new prefab). `EnemyHarvesterUnit.prefab`/`EnemyCrewmanUnit.prefab` (pre-existing) moved to `Assets/Prefabs/Red/` and had their `Art` child swapped to `ResourceCollectorRed`/`EngineerLvl3StaticRed`. **`EnemyHarvesterUnit` was also missing `HarvesterPartAnimator` entirely** — it only ever had `VehicleAnimationDriver` (with an already-empty, inert `_tracks`) — so the claw-gate/track-scroll behavior issues #66-#68 gave the Player Harvester had simply never existed for Enemy's. Added `HarvesterPartAnimator` and wired `_tracksRenderer` to `Art/ResourceCollectorRTSTracks` inside the new nested instance, mirroring Player's exact wiring — confirmed via direct inspection that Red's model shares the identical child hierarchy/naming as Blue's. Per this section's faction-color convention above, all three `FactionColor.Apply` call sites were fixed: removed entirely from `EconomyRegistry`'s Main Building spawn (a Main Building always has real per-faction art now, no placeholder path remains), and gated in `ProductionQueue`/`TestSetup` behind "does this definition/unit already have real per-faction art" (`enemyPrefabOverride != null` for `ProductionQueue`; an explicit `skipFactionTint` parameter for `TestSetup`'s Harvester/Crewman spawn calls) — Ranger/Reaper/Dredge/Saboteur (not yet converted, issue #83) are unaffected and keep their current tint behavior, which was already a no-op for their `SpriteRenderer`-based art regardless. Verified end-to-end in Play Mode: both factions' Main Building/Harvester/Crewman spawned correctly via `TestSetup` with zero console errors, Enemy's used `ResourceCollectorRed`/`CommandTowerRed`/`EngineerLvl3StaticRed` materials by name (confirmed via `sharedMaterial.name`, not just visual impression), and — the key regression check — every renderer's material name printed *without* Unity's `(Instance)` suffix, direct proof `FactionColor.Apply` no longer runs against any of the three (a tinted renderer would show an instanced material). `HarvesterPartAnimator.Update()` force-ticked 5x on the Enemy Harvester with zero exceptions, confirming the new `_tracksRenderer` wiring holds under real execution.

### Environment/Terrain Art — SpacePlatformKit (issue #70, shipped 2026-08-30)

Replaces the flat placeholder `Ground` plane + mostly-unused Terrain Tilemap (issue #17, never more than 1 painted tile) as the level's *visible* floor with `Assets/SpacePlatformKit` (Unity Asset Store, publisher msgdi — same publisher as `ScifiRTSSeriesMegaPackIII`), the Grey color variant, designer-confirmed. **The `Ground` GameObject is not deleted** — it stays exactly where it was (`(0,0,0)`, scale `(10,1,10)`, `Ground` layer, `MeshCollider`, `NavigationStatic`), now with a fully-transparent URP Unlit material (`Assets/Data/Terrain/GroundInvisibleMaterial.mat`, alpha `0`) instead of a disabled `MeshRenderer`. This is a deliberate, non-obvious choice: `Assets/Editor/MapEditorWindow.cs`'s brush placement raycasts against the `"Ground"` physics layer, so deleting `Ground` outright would leave nothing to paint onto (a chicken-and-egg problem) — keeping it as an invisible collision/raycast-target plane means every existing resource node, `PlayerStartPoint`, and the Map Editor's existing raycast/placement code needed **zero changes**. **A second, sharper reason surfaced during verification, not anticipated in the original plan: `Assets/Editor/NavMeshBakeUtility.cs` collects NavMesh sources via `NavMeshCollectGeometry.RenderMeshes`, not colliders** — the first attempt disabled `Ground`'s `MeshRenderer` component outright (simpler than authoring a transparent material), which silently dropped `Ground` from the NavMesh bake entirely (triangulation collapsed from ~12,000 vertices to 168; `NavMesh.SamplePosition` failed at the map origin). Any future "make this renderer invisible but keep it as NavMesh-walkable" need in this project must swap to a transparent material, never disable the `MeshRenderer` component — the two look identical in Play Mode but are not equivalent for NavMesh baking here.

**New wrapper prefabs** (`Assets/Prefabs/`, following the established root+`Art`-child convention from #65/#69, identity rotation since these are real 3D models): four floor tiles — `SpacePlatformSmall`/`Medium`/`Large`/`Intersection` — and nine `Prop_`-prefixed Structures pieces — `Prop_SideWallLeft`/`Right`, `Prop_Bridge`, `Prop_TunnelSmall`/`Large`, `Prop_BuildingSmall`/`Medium`/`Tower`, `Prop_Hangar`. **Two different pivot conventions coexist in this one pack, confirmed by direct `Renderer.bounds` inspection before building anything**: the floor slabs are vertically *centered* on their native pivot, so their `Art` child gets `localPosition.y = -halfHeight` (measured per-piece, not a shared constant) so the piece's walkable **top** surface lands at local `Y=0` — deliberately matching where `Ground`'s old top surface sat, so `Platform_Tier1`/`Ramp` (unchanged, still base-anchored at `Y=0`) continue to stack on top with zero repositioning. The nine Structures pieces are already base-anchored at the source (bottom ≈ `Y=0`, confirmed via the same bounds check), so their `Art` child stays at `Y=0`. Verified numerically post-placement (not just visually): floor tiles measured `max.y = 0.000` exactly; Structures measured `min.y` within 3–6cm of `0` (matching the source models' own tiny native overhang — same negligible-epsilon precedent `Platform_Tier1`/`Ramp` already established, not a new bug). Deliberately deferred to a later pass: `TunnelDoorGrey`/`TunnelGateGrey`/`HorizontalGateGrey` (readme flags gates/doors as likely animated — same caution issue #67 already taught this project, needs its own investigation before wrapping), `TurretPlatformGrey`/`FlyingGrey`, `PlatformFloor1`/`2Grey` (a deck-finish detail layer), and the remaining greeble pieces (`AntennaGrey`, `RingGrey`, fins, thrusters, barricade, `PlatformElement1-4Grey`, `PlatformJumpGrey`) — easy to add later via the identical wrapper pattern.

**Map Editor**: floor tiles registered into the existing `"Terrain"` category (gets the existing `worldPoint.y = 0f` force-to-zero paint behavior in `MapEditorWindow.TryPaint` for free — zero code changes to `MapEditorWindow.cs`). Structures pieces registered into a **new `"Structures"` category** (plain free-form string, no enum exists for categories — same zero-tooling-change precedent `"Spawn Points"` already established). Both share the `Ground` physics layer with the floor tiles rather than getting their own layer, to avoid rewiring every `LayerMask`-typed field across `AttackCommand`/`BuildingPlacement`/etc. for a purely cosmetic gain — Recast's own slope-based voxelization already excludes a wall/building's steep geometry from the walkable NavMesh region automatically (verified directly: a `NavMesh.CalculatePath` straight through a painted `Prop_BuildingMedium`'s footprint correctly detoured around it, 6 corners instead of 2, with zero extra obstacle-marking code — the same cliff-face precedent issue #35 already proved for `Platform_Tier1`).

**Real bug found and fixed post-verification, not caught until the designer actually used the tool**: the first "retire the old floor" pass used `GameObject.Find("Terrain")` to locate and disable the Tilemap object from issue #17 — but **two** GameObjects are named `"Terrain"` in this scene: the intended Tilemap root, and the Map Editor's own `MapContent/Terrain` category folder (auto-created by `GetOrCreateCategoryParent`, holding every terrain-category brush instance, old and new). `GameObject.Find` is not unique-name-safe and grabbed the category folder instead, disabling the entire thing — every terrain-category paint (the designer's new Space Platform tiles, and the pre-existing `Platform_Tier1`/`Ramp` demo stacks) was being created successfully but rendered invisible, reading as "Terrain isn't painting." Fixed by locating the real Tilemap object via its `Grid`+`Tilemap` components instead of name match, and re-enabling `MapContent/Terrain`. **Takeaway: never resolve a scene GameObject by a bare `GameObject.Find(name)` when that name is also a Map Editor category string (or any other tool-generated container name)** — disambiguate by component type or full hierarchy path instead. The pre-existing demo terrain (10 `Platform_Tier1` + 2 `Ramp`, confirmed by the designer as disposable placeholder content, not real level work) was cleared at the designer's request once painting was confirmed working, to give a clean canvas for the new kit.

**Grid Snap (issue #71, shipped 2026-08-30):** the designer's own hand-built layout experiment (`SpaceMapExampleLearn`) revealed the real intended workflow — SpacePlatformKit's pieces are modular and meant to tile edge-to-edge, but freehand raycast placement lands at whatever sub-unit position the cursor happens to hit, requiring eyeballed alignment (confirmed directly: the designer's manually-placed platforms were spaced 7.80–7.85 apart against an exact `8.00`-unit piece width). Two new additive fields: `MapBrushPalette.gridSize` (float, default `8`, matching the platform module width confirmed via direct bounds inspection — **shared across every grid-snapping brush**, not per-brush, so different piece types still land on the same coordinate lattice and their edges actually meet) and `MapBrushDefinition.useGridSnap` (bool, default `false` — every existing freehand/organic brush category — Props, Resources, Buildings, Spawn Points — is completely unaffected). `MapEditorWindow`'s existing paint pipeline gained one new step: `SnapToGrid(worldPoint)` rounds X/Z to the nearest multiple of `gridSize`, inserted after the existing Terrain-category Y-force (orthogonal concerns — a Structures piece placed atop an elevated surface still wants X/Z snapped without its Y being forced) and applied only on paint, never erase (erase always targets whatever's actually nearest the cursor). A new `DrawGridSnapPreview` (a yellow `Handles.DrawWireCube` at the snapped position, sized to one grid cell) mirrors the existing splash-radius preview's hover-tracking shape, so the designer sees exactly where a piece will land before clicking. All 13 SpacePlatformKit brushes (4 floor tiles, 9 Structures) opted in; the pre-existing `Platform_Tier1`/`Ramp`/every other category stayed non-snapping. The designer's example was re-snapped in place (each child's world X/Z rounded to the nearest `8`) to serve as a clean reference — confirmed visually via multi-angle capture, reads as a tidy aligned corridor with no eyeballing artifacts. **Known, flagged, not fixed**: the shared 8-unit grid is exact for X (piece width) but not for Z-axis row spacing when using `SpacePlatformLarge` (real depth `16.39`, not a clean multiple of `8`) — snapping two rows to grid positions `16` apart produces a `0.39`-unit overlap rather than the small gap the designer's original hand-placed layout used for its `PlatformFloor1` bridge connector. Not visually apparent in practice (the bridge deck piece's own footprint covers the seam regardless), but a real geometric mismatch worth knowing about before assuming the grid alone guarantees a clean fit on every axis — an asymmetric `gridSizeX`/`gridSizeZ` (or a Large-specific row pitch) is the likely fix if it ever becomes visually apparent, not yet built since it wasn't needed.

**Platform Connector brush (issue #72, shipped 2026-08-30):** the designer confirmed the bridge-connector gap between platform rows is always the same fixed size ("if something custom is needed it can be added directly" — i.e. deliberately not a general parametric feature), so `PlatformFloor1Grey`'s exact hand-worked-out recipe from `SpaceMapExampleLearn` — 180° Z rotation (flips which of its two differently-detailed edges faces the seam) and `(1.5,1,1.5)` scale (native ~5.4×1.67 footprint → ~8.1×2.51, matching the platform grid's 8-unit module width with enough depth to bridge/overlap a two-row gap) — is baked directly into a new wrapper prefab, `Assets/Prefabs/PlatformConnector.prefab`, registered as a normal `"Terrain"`-category, grid-snapping brush (`Brush_PlatformConnector.asset`). Unlike the other floor tiles, `PlatformFloor1`'s own native pivot is already almost exactly at its top surface (bounds span roughly `Y -0.04` to `+0.04`) — no large top-anchoring offset needed, just the same small positive nudge (`+0.03`) the project's `ScatterYOffset`/`ArtYOffset` convention already establishes, to avoid z-fighting with the platform tiles' own `Y=0` top surface right at the seam. Because it's grid-snapped onto the *same* shared lattice as the platform tiles, it needs no special positioning logic at all: platform rows spaced two grid cells apart (the designer's own convention, confirmed in `SpaceMapExampleLearn`) naturally leave the intermediate grid line free for a connector row. Verified by replacing all three of the designer's original manually-scaled/rotated `PlatformFloor1Grey` instances in `SpaceMapExampleLearn` with plain `PlatformConnector` brush instances at the same grid-snapped X/Z — visually identical result (multi-angle capture) and numerically confirmed flush placement (`Y` spans `-0.01` to `0.07`, footprint exactly `8.10×2.51` as designed).

**Real bug found by the designer (issue #73, shipped 2026-08-30): a Map Editor category folder's own transform, not any individual piece, was rotated — every piece painted into it inherited that in world space.** The designer reported freshly-painted `Space Platform (Large)` pieces coming out visibly diagonal even when painted directly from the tool, after an initial (incomplete) fix that only reset the individual pieces' own world rotation. Root cause, found by inspecting the actual hierarchy rather than trusting the visual symptom: `MapContent/Terrain` — the organizational folder `GetOrCreateCategoryParent` creates to hold every terrain-category brush instance — itself had a rotation of `317.97°`, almost certainly from an accidental Hierarchy-panel rotate-gizmo drag that landed on the folder instead of a piece inside it. Every child's own local transform was, and always had been, perfectly correct (confirmed: `PaintSingle` never touches rotation at all) — but `localRotation` composes with the parent's, so every existing AND every newly-painted piece inherited the folder's tilt in world space, which is exactly why the first, per-instance fix "held" only until the next fresh paint. A second, fully independent incident was found in the same sweep: an orphaned, empty `Terrain (1)` duplicate folder (Unity's own name-collision suffix), also carrying a stray non-identity transform, holding zero children (harmless, but confusing clutter — removed). **Fixed at the root, not just the symptom**: `GetOrCreateCategoryParent` now force-resets a category folder's local position/rotation/scale to identity on *every* call, logging a `Debug.LogWarning` if it ever finds one that had drifted — a category folder is purely organizational and must never carry a transform of its own, so this makes the entire class of mistake self-healing the moment anything is next painted into the affected category, rather than something that has to be manually diagnosed and patched per-incident again. Verified end-to-end: artificially re-tilted the `Terrain` folder to 45°, invoked the real compiled `GetOrCreateCategoryParent("Terrain")` (via a one-off reflection call used strictly as read-only verification of already-compiled behavior, not a functionality workaround), and confirmed the folder's rotation was back to identity immediately after, with zero further code changes needed. **Takeaway for any future Map Editor diagnosis**: when painted content looks visually wrong (rotated, offset, scaled) but individual pieces' own local transforms check out clean, check every ancestor up to `MapContent` itself before assuming the paint code is at fault — a parent-level corruption produces symptoms that look identical to a per-piece bug but "fixing" individual instances doesn't hold, since the next paint just inherits the same bad parent again.

**Erase now scoped to the selected brush (issue #74, shipped 2026-08-30):** `TryErase` previously destroyed whichever painted object was nearest the cursor, full stop, regardless of which brush created it — designer-reported as unsafe once the palette held enough overlapping/adjacent content (e.g. a `Space Platform (Large)` sitting near a `Ramp`): right-clicking to clean up one piece type risked taking out an unrelated neighbor instead. Fixed by requiring a brush to be selected (previously erase worked with nothing selected at all — now a no-op in that state, matching how painting already requires a selection) and filtering every candidate through `PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject) == _selectedBrush.prefab` before considering it for the nearest-distance comparison — an object painted by a different brush is now invisible to erase entirely, not just deprioritized. Verified directly (an `EditorWindow`'s private methods have no reliable reflection-invoke path in this project's `unity-mcp` sessions, confirmed again this issue — mirrored the exact matching+distance logic instead, per the established issue #56 precedent): placed a `SpacePlatformSmall` and a `SpacePlatformLarge` at *equal* distance from a test click point with `Space Platform (Large)` selected — only the Large instance matched and would be erased, the Small instance was completely ignored despite being equally close, confirming the type filter — not proximity — is now the deciding factor.

**Top/Bottom walls + flipped variants, 8 new brushes (issue #75, shipped 2026-08-30):** the designer needed two more wall positions to fully enclose a rectangular room (`Side Wall (Left)`/`(Right)` only covered two of four sides) plus a second orientation option per platform/wall — confirmed via direct mesh inspection that a 180° Y rotation on any of these pieces is a real, visible difference (the panel/chevron detail flips which way it points), not a no-op. `Side Wall (Top)`/`(Bottom)` (`Prop_SideWallTop`/`Bottom.prefab`, both wrapping `SideWallRightGrey` at 90°/270° Y — the exact rotations the designer's own `SpaceMapExampleLearn` had already validated for this purpose) complete the four-sided enclosure. `Side Wall (Left, Flipped)`/`(Right, Flipped)` (180° Y on the existing source meshes) and a `Flipped` (180° Y) variant of all four platform types (`Small`/`Medium`/`Large`/`Intersection`) round out the set — every wrapper reuses the exact same top-anchoring/base-anchoring Y-offset logic already established per piece type, since rotation around Y doesn't affect a piece's Y-extent. All 8 registered as ordinary grid-snapping brushes (`"Terrain"` for platforms, `"Structures"` for walls) — zero `MapEditorWindow.cs` changes needed, matching every prior brush-only addition this project has made. **Scope note, not yet confirmed with the designer**: `Top`/`Bottom` were built as exactly the two rotations needed for those two positions (90°/270°, already each other's 180° counterpart by construction) rather than additionally generating separate "Top Flipped"/"Bottom Flipped" entries — doing so would have collided in rotation value with the other position if built from the same source mesh. If the designer wants genuinely distinct Top/Bottom flip variants too, that's a quick follow-up (built from the opposite source mesh — `SideWallLeftGrey` instead of `SideWallRightGrey` — to avoid the collision). Verified end-to-end: built a complete test room (3 `SpacePlatformMedium` tiles, one deliberately flipped, framed by all four wall positions) and confirmed via multi-angle capture — full enclosure, correctly inward-facing wall detail on all sides, visible tread-direction variation between the normal and flipped floor tile.

**Correction, same day (issue #76 then #77): the platform "Flipped" brushes needed the correct rotation axis, not removal.** The wall "two sides" difference (confirmed real, above) doesn't generalize to the platform pieces via a **Y**-axis rotation — the designer first reported zero visible difference between `Space Platform (Large)` and `(Large, Flipped)`, confirmed by a direct side-by-side re-render: `SpacePlatformLargeGrey`'s top-down tread/perforation detail is genuinely bilaterally symmetric along its long axis under Y rotation, so that attempt was a real, correctly-diagnosed no-op (issue #76: all 4 platform `Flipped` prefabs/brushes deleted, `MapBrushPalette` 49 → 45). **The designer then clarified what they actually meant: the model's *underside* — a 180° rotation on the **Z** axis, not Y.** Re-verified directly (issue #77) and confirmed dramatically real this time: `SpacePlatformLargeGrey`'s underside is a completely different, more heavily-detailed panel texture (accent lights, chevron/directional markings) than the plain top-facing tread pattern — Z-axis rotation is the only one of the three that actually swaps which face points up, since Y only spins the piece in the horizontal plane and X would swap length-direction instead of revealing the underside. All 4 platform `Flipped` prefabs/brushes rebuilt with `Art.localRotation = (0,0,180)` instead of `(0,180,0)`, bounds re-measured post-rotation per the project's own measure-don't-assume convention (halfHeight values came out numerically identical to the un-rotated case, as expected for a shape symmetric top-to-bottom in magnitude even though the two faces look completely different) — `MapBrushPalette` back to 49 brushes, all 4 confirmed placing flush at `Y=0` with the genuinely-different underside now facing up. **Takeaway**: a "flip" brush's entire premise rests on which specific axis actually reveals the claimed difference — Y (spin in place), Z (flip upside-down), and X (flip end-to-end) are not interchangeable, and guessing wrong looks identical to correct in code (compiles, places, measures fine) with the only tell being the render itself. **Tooling note**: `AssetDatabase.DeleteAsset` triggers a blocked "user interactions are not supported for MCP tool calls" error in this project's `unity-mcp` sessions — confirmed directly, the call never completes and the asset survives untouched. Worked around by deleting the `.asset`/`.prefab` and their `.meta` files directly from disk (outside `unity-mcp`) followed by `AssetDatabase.Refresh()`, which Unity accepts cleanly. Add this to the project's running list of `unity-mcp` API limitations (alongside the already-documented reflection and Play Mode frozen-loop quirks) — use the filesystem-delete-then-refresh path for any future asset removal via this tool, not `AssetDatabase.DeleteAsset` directly.

**Follow-up bug, same day (issue #78): recreating assets at a just-deleted path corrupted 3 of the 4 rebuilt brush assets.** The designer reported 3 brushes miscategorized and the rebuilt set not appearing at all — direct file inspection found something worse than miscategorization: `Brush_SpacePlatformSmallFlipped`/`MediumFlipped`/`LargeFlipped` all had **every** `SerializedObject`-set field silently reverted to its C# default (`displayName`/`category` blank, `prefab` null, `useGridSnap` false) despite the build script logging a clean "Created brush ..." for each with no error — only `Brush_SpacePlatformIntersectionFlipped` (created last in the loop) had its edits actually persist. A blank `displayName` + blank `category` means the Map Editor's `string.IsNullOrEmpty(b.category) ? "Uncategorized" : b.category` grouping put all 3 under an unexpected "Uncategorized" header with an unlabeled button — reading as "not there," and a null `prefab` meant even clicking one would silently do nothing (`PaintSingle`'s existing null-guard). The underlying **prefabs were completely correct** (confirmed by reading `SpacePlatformLargeFlipped.prefab` directly — correct `NavigationStatic` flags, correct baked 180°-Z rotation, correct Y-offset) — the corruption was isolated entirely to the brush `MapBrushDefinition` assets. **Likely cause**: these 3 were recreated via `AssetDatabase.CreateAsset` + `SerializedObject` at paths that had been deleted via the raw-filesystem workaround (above) moments earlier in the same session, in a tight loop with only one `AssetDatabase.SaveAssets()` at the very end — consistent with a stale GUID/import-cache interaction from the delete-then-recreate sequence, though the exact mechanism wasn't conclusively isolated. **Fixed by directly rewriting the 3 corrupted `.asset` YAML files as plain text** (bypassing the `AssetDatabase`/`SerializedObject` path that produced the corruption entirely), using the reference IDs read directly from each broken asset's own already-correct `prefab`-adjacent files (`.prefab`/`.prefab.meta`) — then verified via a fresh `AssetDatabase.LoadAssetAtPath` read (not just trusting the edit) that all 4 now report correct `displayName`/`category`/`prefab`/`useGridSnap`, and confirmed via a real paint-test instantiation of each fixed brush's `prefab` that all three place flush at `Y=0` with the correct flipped-underside texture visible. **Takeaway, sharper than the general "verify, don't trust the log" lesson this project already holds**: a `CreateAsset`+`SerializedObject` batch loop's own per-iteration success log is not sufficient evidence even for that same iteration — read every created asset's fields back directly after any such batch, especially one recreating assets at paths freshly deleted via the raw-filesystem workaround in the same session.

### Notable deviations from the original issue specs, decided at implementation time
- **New Input System usage is direct device polling** (`Mouse.current`, `Keyboard.current`), not a generated `.inputactions` C# wrapper — authoring/validating that asset requires the Editor, which wasn't available. Still fully "new Input System," just without an actions asset. Revisit if an actions asset becomes worth the ceremony (e.g. rebindable keys).
- **`Weapon`/`Health` are new components, not wrappers around `Assets/TopDownAssets/Common/Turret.cs`/`Projectile.cs`.** Those classes are hardcoded to legacy `Input.mousePosition`/`Input.GetKeyDown` mouse-aim control and have no damage-application hook (`Projectile.Bang()`'s unit-damage code is commented out) — they don't fit an auto-targeting RTS weapon. `Weapon` draws targets from `UnitManager`/`EconomyManager`'s existing registries (never a scene-wide `FindObjectsByType` in `Update`) to stay convention-compliant.
- **No separate `DetectionSystem` component (issue #3).** `Weapon`'s generic auto-acquire-within-range-and-fire behavior already is instant detection + defensive response once attached to a detection-capable building's prefab — a dedicated detection component would just duplicate it. `BuildingDefinition.isDetectionCapable`/`detectionRadius` remain as design-facing metadata; the range that actually matters is the `WeaponConfig` on that prefab's `Weapon`.
- **Attack-move doesn't chase a moving target.** An Attack order (right-click an enemy) is a move-to-that-position command; `Weapon`'s always-on auto-acquire handles engaging once in range. Continuous pursuit of a retreating target isn't implemented.
- **Sabotage validity for resource nodes is unfiltered by faction** — nodes have no owning faction in the data model (either side's harvesters can work any node), so a Saboteur can target any node, not exclusively "the enemy's." Harvesters and buildings are correctly faction-filtered.
- **A sabotaged building's "disabled" state only pauses its `ProductionQueue`**, not its contribution to `EconomyManager`'s power totals — a disabled power building keeps counting toward capacity while disabled. Flagged for a future pass if that turns out to matter for balance.

---

## Key Conventions

These are carried over as **non-negotiable process/tech conventions** from the studio's proven setup, independent of MechTS's own not-yet-decided gameplay design:

### Unity 6 API
- Use `FindFirstObjectByType<T>()` — **not** the deprecated `FindObjectOfType<T>()`
- Use the new Input System — **not** legacy `Input.GetKey` / `Input.GetAxis`
- URP shaders only, 2D Renderer — no Standard shader materials

### Script Quality
- Every public method and every Unity message (`Awake`, `Start`, `Update`, etc.) **must** have an XML summary comment describing its purpose, parameters, and any side effects
- Scripts over ~150 lines should be audited — they are likely doing too much (SRP)
- Prefer many small focused components over one large script
- No `GetComponent<T>()`, `FindFirstObjectByType<T>()`, or any scene lookups inside `Update` / `FixedUpdate` — cache everything in `Awake`/`Start` per the ordering conventions the studio has already had to work out the hard way (pre-placed scene objects vs. Bootstrapper-created managers resolve each other safely in `Start()`, never `Awake()`, since Unity gives no cross-GameObject `Awake()` ordering guarantee)
- **Never call `gameObject.SetActive(false)` on a component's own GameObject from within that component's `Awake()`.** Unity only calls `Start()` on a component if its GameObject was active at least once — deactivating in `Awake()` permanently prevents `Start()` from ever running, which is fatal for anything that needs `Start()` to subscribe to an event that would later reactivate it (a real bug caught in issue #8's `MissionEndScreen`: it deactivated itself in `Awake()`, so it could never subscribe to `GameManager.OnGameStateChanged` in `Start()`, so nothing could ever show it again). If a component needs to start hidden, subscribe first in `Start()`, then call `SetActive(false)` at the end of that same `Start()` call.
- **Any component added at runtime via `AddComponent<T>()` must explicitly perform whatever setup that type's Editor-only `Reset()`/`OnValidate()` would normally do when added via the Inspector — those callbacks never fire for runtime-created components.** Caught a real, previously-invisible bug from this: `InputSystemUIInputModule` (created in `UIManager.BuildEventSystem()`) had every UGUI button in the game permanently unclickable because its Point/Click actions are normally wired by `AssignDefaultActions()`, called automatically from `Reset()` — silently never happened here until `AssignDefaultActions()` was added as an explicit call right after `AddComponent`. `button.onClick.Invoke()` in a test script proves nothing about this — it bypasses `EventSystem` routing entirely, so this class of bug survives every verification that doesn't test through real (or at least `EventSystem`-routed) input.

### ScriptableObject-First Design
Once real systems exist, tunable data (unit stats, resource costs, mission definitions, etc.) **must** live in ScriptableObject assets, never hardcoded — this will be enforced starting with the first `/arch` review, same as the studio's other project.

### Event-Driven Communication
| Situation | Use |
|---|---|
| Game/round state changes | C# `event Action` / `event Action<T>` |
| Designer-wired Inspector callbacks | `UnityEvent` |
| Cross-system broadcasts (unit killed, resource depleted, round ended) | ScriptableObject event channel (only once a genuine multi-consumer case exists — don't introduce this for a single-consumer broadcast; a plain C# event is the default) |

Always unsubscribe (`-=`) in `OnDisable` or `OnDestroy`.

### Sprite & Art Convention
- **Source priority**: reuse `Assets/TopDownAssets/` pack art first; only author new custom art (matching its painted look — gradient shading, rim highlight, outline) when the pack has no equivalent.
- **Filter Mode = Bilinear** — this style is painted/shaded, not flat.
- **Pixels Per Unit**: no fixed default — read the target object's existing `Collider2D` size and set PPU to match, preserving native aspect ratio.
- **Never tint real art.** `SpriteRenderer.color` must stay white (`RGBA 1,1,1,1`) on any sprite that is real painted/rendered art. Faction ownership and selection are shown via a single merged hollow ring (`MechTS.Utilities.RingMesh` + `Units.UnitBase`'s `FactionRing` child) parented under the unit — dim in the faction color at rest, brighter and slightly larger when selected — never by tinting the sprite itself. Buildings use the equivalent shared `Utilities.SelectionIndicator` ring (golden, hidden until selected) via `SelectionController`. **Both rings' local Y offset must stay positive** (currently `+0.02`) — `Ground` is a flat quad at world `Y = 0`, and a ring offset below that is invisible from the top-down camera, fully occluded by the opaque ground plane with no errors of any kind (see `memory/PATTERNS.md`'s 2026-08-18 "merged faction/selection ring" entry for the full diagnosis). **The same class of bug, found post-#35 (2026-08-23):** a unit's *art* (sprite parts, not just the ring) had zero Y offset from its own root — harmless while every unit only ever stood on flat `Ground` (world `Y=0`), but the first time a unit stood on an elevated `Platform_Tier1` (top surface at world `Y≈2.5`), its art sat flush with that surface's own solid mesh instead of a paper-thin plane, risking exactly the occlusion this section already warns about. `UnitBase.Awake()` now calls a new `NudgeArtAboveSurface()` that nudges each top-level art child (the single "Art" wrapper, or each matched-parts sibling like Torso/Head/HandL/HandR, depending on the prefab's convention — see the Real-art swap procedure below) up by `ArtYOffset` (`+0.03`), the same mechanism as the ring's own offset, applied generically via whatever renderers `Awake()` already collects into `_artRenderers` so it works for every unit prefab without touching each one by hand. Selection state uses the same layered-ring approach: `UnitBase` auto-creates a larger, higher-contrast ring (`SelectionIndicator`, golden yellow, scale 2.4 vs. the faction ring's 1.6) if none is assigned in the Inspector, toggled by `SetSelected()` — this reads as an outer highlight around the faction-colored disc.
- **Material/shader for `SpriteRenderer` content: use the built-in `Sprites-Default` material (`Sprites/Default` shader), never `Universal Render Pipeline/2D/Sprite-Unlit-Default` (issue #49, 2026-08-25).** Every confirmed-working unit sprite in this project (Harvester, TestCombatUnit, etc.) uses `Sprites/Default` — a check of `Prop_Tree.prefab` (the project's first standalone decorative prop, issue #18) found its `Art` child's `SpriteRenderer` had instead been auto-assigned the URP-2D-specific `Sprite-Unlit-Default` material, the exact shader family `memory/DECISIONS.md`'s 2026-08-22 Tilemap entry already identified as broken under this project's actual 3D `UniversalRendererData` — this is the same failure mode surfacing for a plain `SpriteRenderer`, not just `Tilemap`. Fixed via `AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat")` reassigned onto the prefab. This had apparently gone unnoticed since #18 shipped. **Apply when:** authoring or reviewing any new sprite-bearing prefab — explicitly check the `SpriteRenderer`'s assigned material/shader matches the working `Sprites/Default` convention rather than trusting whatever Unity auto-assigned on sprite import, since the wrong choice renders with zero console error or other symptom.
- **World/camera orientation (established with the Harvester, 2026-08-17; camera corrected by issue #80, 2026-09-02):** the pack's sprites are drawn for a genuine bird's-eye view. Art is parented under the unit's root, rotated `(90, 0, 0)` locally so it lies flat on the XZ ground plane, while the root keeps its 3D `Collider`/`NavMeshAgent` for gameplay logic — this flattening convention is unchanged and still required for every unit/prop that hasn't been converted to a real 3D model yet. **`Main Camera` is no longer straight-down Orthographic** — issue #80 deliberately moved it to a fixed-pitch, offset Perspective camera (SC2-style) once enough real 3D content existed to justify it (see the Architecture section's `CameraManager` entry for the full shipped shape). This section's original guidance ("don't reintroduce an angled camera, it reads wrong against top-down sprites") was correct for its time but is now superseded — the camera angle was a deliberate, requested change, not a regression to guard against. The real, still-live consequence: any content still using this flat-sprite convention (every unit except `MainBuilding`/`EconomicBuilding`/`HarvesterUnit`/`CrewmanUnit`, per issue #65's Player-only vertical slice) will read increasingly foreshortened/thin from the new angle, since a flat ground-lying sprite doesn't billboard toward the camera — a known, accepted gap (issue #80's own scope call), not a bug to fix reflexively when noticed.
- **Reusing the pack's per-part animation scripts** (`Vibration`, `Smoke`, `WheelAnimation`, all in `Assets/TopDownAssets/Common/Scripts/`): these three are safe to reuse as-is — no legacy-Input dependency. The pack's own orchestrators that drive them (`Vehicle.cs`, `Turret.cs`) *are* legacy-Input-bound (WASD/mouse-aim) and don't fit an AI/NavMeshAgent-piloted unit — write a small MechTS-side driver instead (see `MechTS.Units.VehicleAnimationDriver`, which replaces `Vehicle.cs` for the Harvester, sourcing `Gain()`/`Speed` from `NavMeshAgent.velocity` instead of `Input.GetAxisRaw`). Check what a pack orchestrator script actually depends on before assuming it's reusable — read it first. **`WheelAnimation.Speed` is not a real rolling speed — it's "frame-index advance per second" through a `List<Sprite>` flipbook** (`WheelAnimation.FixedUpdate()`: `_revolutions += Speed * Time.fixedDeltaTime; index = CeilToInt(_revolutions) % Sprites.Count`), confirmed the hard way (issue #47, shipped 2026-08-24): `VehicleAnimationDriver` originally fed it raw `NavMeshAgent.velocity.magnitude` directly, which tops out at the Harvester's configured `moveSpeed` (3.5) — against the Harvester's actual 5-frame track (`TrackL`/`TrackR`, confirmed via the prefab), that's only ~3.5 frame-changes/sec (each frame held ~285ms), well under the ~12-24/sec a flipbook needs to read as continuous motion, so the track visibly stepped rather than rolled — a real, designer-reported sound/visual mismatch (the engine audio's `gain`-based pitch/volume is genuinely continuous, unlike the track). Fixed with a new `HarvesterConfig.trackAnimationSpeedMultiplier` (default `5`, i.e. ~17.5 frame-changes/sec at max speed), pushed into a new `VehicleAnimationDriver.SetTrackSpeedMultiplier(float)` from `HarvesterGatherBehavior.Start()` alongside the existing `SetEngineSound` call — `Update()` now does `track.Speed = speed * _trackSpeedMultiplier` instead of `track.Speed = speed`. Unlike `SetEngineSound` (which needed its *entire* setup logic moved inside itself to be Start()-ordering-safe, since it lazily builds an `AudioSource` — see `memory/PATTERNS.md`'s 2026-08-21 entry), `SetTrackSpeedMultiplier` is a plain field write and needs no such treatment; it's already safe regardless of which component's `Start()` runs first, matching `UnitBase.SetBaseMoveSpeed`/`Health.SetMaxHealth`'s simpler shape. **Known verification gap**: `NavMeshAgent.velocity` cannot be observed *or forced* in this project's frozen-player-loop `unity-mcp` sessions — confirmed directly, setting it manually doesn't even persist one call to the next with zero `Update()` in between, since it's computed entirely by Unity's internal nav simulation tick, which never runs when `Time.frameCount` is stuck. Verified via code review (the multiplication is trivially correct) and end-to-end wiring confirmation (the config value round-trips correctly from the actual asset through to the driver) instead — same class of gap as issue #33's audio-output verification limitation (`memory/DECISIONS.md`, 2026-08-22).
- **Real-art swap procedure (established with the Harvester, confirmed again for TestCombatUnit/`BlueSoldierLight`, 2026-08-18):** load the target prefab via `PrefabUtility.LoadPrefabContents`, remove the placeholder `MeshFilter`/`MeshRenderer`, instantiate the pack prefab as a **nested prefab instance** child (`PrefabUtility.InstantiatePrefab`, named `Art`) rather than copying its sprite hierarchy by hand, set its local transform to position `(0,0,0)`, rotation `(90,0,0)`, and a scale that reads reasonably against the unit's existing `Collider` (0.5 has been the working default for both Harvester and TestCombatUnit so far), then `PrefabUtility.SaveAsPrefabAsset` back over the same path. To wire a private `[SerializeField]` reference (e.g. `Weapon._muzzle`) to a child of the newly-added art instance, use `SerializedObject`/`SerializedProperty` — don't make the field public just to reach it from an Editor script.
- **Matched-parts rigs may have no built-in animation at all** — `BlueSoldierLight.prefab` (like `DroneX3` before it) ships as pure `Transform`+`SpriteRenderer` parts (`Torso`, `Weapon`, `Head`, `HandR`, `HandL`) with zero attached scripts. That's fine — a static standing pose while the `NavMeshAgent` moves it is an acceptable first pass, same precedent as the Saboteur. Only reach for a driver script (`VehicleAnimationDriver`-style) if the source prefab actually ships per-part motion to orchestrate.
- **Weapon firing effects (bullet flight + shell-casing eject), established with `TestCombatUnit`, 2026-08-18:** `Common/Sprites/Projectiles/Ammo/<01-19|Special>/` each hold a matched `Bullet.png` + `Case.png` pair for one ammo *type* — they are not animation frames, so don't cycle through them per shot; pick one pair per weapon and stay consistent (`TestCombatUnit` uses `Ammo/01`, a generic rifle round). Neither effect needs an `Animator`/`AnimationClip` — `MechTS.Units.BulletEffect` (linear position lerp to the target, yawed via `Mathf.Atan2` on its own transform) and `MechTS.Units.CasingEjectEffect` (pop sideways + spin + fade) are plain `Time.deltaTime`-driven components, matching `VehicleAnimationDriver`'s style. Both are built and reused via `UnityEngine.Pool.ObjectPool<GameObject>` (owned by `Weapon`, one pool per effect type, built in `Start()` from `WeaponConfig.projectileEffectPrefab`/`casingEffectPrefab` — either may be left unassigned to opt a weapon out of effects entirely) rather than raw `Instantiate`/`Destroy` per shot. Each effect prefab is a small root+child pair: the root carries the effect script and handles position/rotation directly; a child named `Sprite` carries the `SpriteRenderer` at the standard fixed local `(90,0,0)` tilt — this way the root's own yaw/spin never has to be composed with the flattening rotation on the same `Transform`.
- **Terrain Tilemap layer (issue #17, shipped 2026-08-19):** a `Grid`/`Tilemap`/`TilemapRenderer` GameObject (`Terrain` → `Tilemap`) sits as a pure visual overlay above `Ground` — `Ground`'s `MeshRenderer`/`MeshCollider` are completely untouched, so raycasting/NavMesh/everything depending on `Ground`'s exact current setup carries zero risk. Same flattening pattern as unit art: the `Grid` GameObject is rotated `(90,0,0)` so its cells lie on the XZ plane instead of Unity's Tilemap-default XY plane, and sits at local Y `0.01` (positive, above `Ground`'s Y `0`) — verified by round-tripping `Tilemap.CellToWorld` on the painted area's corners and confirming they land exactly on `Ground`'s real world bounds (X/Z `[-50, 50]`, `Y = 0.01`). One placeholder `Tile` (`Assets/Data/Terrain/PlaceholderTile.asset`) uses a small solid-color `Sprite` whose color was sampled directly from `Ground`'s actual texture (`GridTexture.png`'s fill pixel, not a hand-picked approximation) via a temporary `TextureImporter.isReadable = true` toggle, restored afterward — keeps `Tile.color` at white per the "never tint real art" convention, same as everywhere else. Cell size `5×5` (not `1×1`) keeps the placeholder-painted area (`400` cells covering the full `100×100` `Ground`) a manageable bulk `SetTilesBlock` call; real terrain art can pick whatever grain it needs later, this is throwaway. A `TerrainPalette.prefab` (`Assets/Data/Terrain/`, built via `UnityEditor.Tilemaps.GridPaletteUtility.CreateNewPalette` — see Dependencies below for the package this needs) ships with the placeholder tile pre-populated at cell `(0,0,0)`, so `Window > 2D > Tile Palette` has something paintable the moment a designer opens it, matching "confirms the pipeline end-to-end." No runtime script reads or references any of this — confirmed zero Play Mode impact (clean console entering/exiting Play Mode with the layer present).
- **Map Editor painting tool (issue #18, shipped 2026-08-19):** `Assets/Editor/MapEditorWindow.cs` (`MechTS/Map Editor` menu item) is a custom `EditorWindow` + Scene View tool for painting registered prefabs directly onto the map — resource nodes, buildings, props — instead of hand-placing/hand-configuring each one. Brushes are data-driven (`Economy.MapBrushDefinition` wraps a prefab + display category; `Economy.MapBrushPalette` is the full set shown in the window, grouped by category) so adding a new paintable type later is authoring one asset, no tool-code changes. Implementation notes: uses `SceneView.duringSceneGui`, not `OnSceneGUI` on a `MonoBehaviour` (there's no scene-resident object to host it — the tool lives entirely in the `EditorWindow`); claims Scene View input for paint/erase clicks via `HandleUtility.AddDefaultControl(controlId)` so the click doesn't also orbit the camera or select whatever's under the cursor. Every placement goes through `Undo.RegisterCreatedObjectUndo` and every erase through `Undo.DestroyObjectImmediate`, so painted content is undoable via Unity's normal Ctrl+Z with no custom undo system. Painted instances are parented under an on-demand `MapContent/<category>` root, and erase only ever searches inside `MapContent` — it can never remove scene content the tool didn't paint. Disabled outright during Play Mode (`OnSceneGUI` returns immediately if `EditorApplication.isPlaying`) — this is level-authoring tooling only, never a runtime feature. Shipped with 5 starter brushes across all three categories: `ResourceNode_Ore`/`_Biomass`/`_Gold` (new prefabs — no `ResourceNode` prefab existed before this issue; the 4 nodes already in the scene are hand-configured instances, not prefab-backed, and are unaffected), `Prop_Tree` (new, first prop prefab in the project, same flattening convention as unit art), and the existing `EconomicBuilding`. **Updated in issue #35**: the world-point resolution changed from a mathematical `Plane(Vector3.up, Vector3.zero).Raycast` (flat `Y=0` only) to a real `Physics.Raycast` against the `Ground` layer (`LayerMask.GetMask("Ground")`), so painting resolves the actual surface under the cursor — bare Ground, a `Platform_Tier1`'s elevated top, or a `Ramp`'s sloped face, all three of which share that layer. **Splash-brush drag spacing is radius-aware, not the flat ordinary-brush constant (issue #63, found via #62's real-world use, shipped 2026-08-28):** `TryPaint`'s drag-repaint gate (`MinPaintSpacing`, `1.5f`) is correct for an ordinary brush — painting every ~1.5 units while dragging is exactly how a dense line of individual objects gets built up. It was wrong for a splash brush: one click already scatters `splashCount` instances across the brush's full `splashRadius`, so requiring only the same 1.5-unit gap before firing *another entire batch* meant a normal drag motion dropped many overlapping batches almost on top of each other — this is what actually caused the "trees placed strangely" report on #62's already-density-corrected `Brush_TreeGreenCluster`, a separate bug underneath the per-batch tuning fix, not fixed by it. `TryPaint` now requires `Mathf.Max(MinPaintSpacing, _selectedBrush.splashRadius)` between paint ticks specifically when `isSplashBrush` is true; ordinary brushes are unaffected. This applies to every splash brush in the project, not just #62's — `Brush_TreeCluster`/`Plant1Cluster`/`Plant2Cluster`/`Plant3Cluster` all get the fix for free with zero data changes, since it's the shared tool code, not a per-brush setting.
**Same issue #63, second and deeper fix, found within minutes of the first:** the drag-spacing fix stopped multiple batches from stacking, but a *single* batch's own `Random.insideUnitCircle` picks (no minimum-spacing guarantee between them) can still land 2-3 instances close enough to touch by pure chance, especially with a small `splashCount` — the identical visual symptom (chained/merged canopies), a third independent cause. `PaintSplash` now rejection-samples each candidate offset against every already-placed offset in the same batch (`splashRadius * 0.5` minimum gap, capped at 20 attempts, falls back to the last-tried candidate rather than silently placing fewer than `splashCount`) — proportional to each brush's own `splashRadius`, not a flat constant, so the existing smaller `Plant1/2/3Cluster` brushes (radius 3) aren't forced unnaturally sparse by a spacing rule tuned for full trees. Verified via 30 simulated batches (worst-case pairwise distance 4.01 against a 4.0 target) and a live repaint+capture showing 4 individually-distinguishable trees.
**Same issue #63, third and actual root cause:** the spacing fixes were correct, but a further designer screenshot showed the real problem — `randomizeRotation` itself. Isolating a single rotated tree (root Y≈90.8°) alone showed genuinely broken art: trunk sheared sideways, canopy bunched to one side, not a clean "facing a different direction" read; a Rock compared at 0° vs. 90° side-by-side showed identical breakage. **`RPGW_GL_v2.0`'s art (`decorative.png`) is not drawn as true orthographic top-down** — it's a fixed-perspective "2.5D" style (the RPG-Maker-tile convention: image-Y represents height/depth from one implied camera angle, not a real top-down silhouette), so spinning it around the world-vertical axis shears the flat picture rather than "turning the object," since only one viewing angle is baked into the pixels. This is categorically different from `Assets/TopDownAssets/` art (confirmed genuinely bird's-eye-view per this file's own World/camera orientation convention) — `Brush_TreeCluster` (pre-#62, a different source pack) doesn't share this problem and was correctly left untouched. Fixed by setting `randomizeRotation = false` on both `Brush_RockCluster` and `Brush_TreeGreenCluster` — clusters now vary only in position and scale, never rotation — and resetting rotation to identity on every already-painted real instance directly. **Any future brush sourced from `RPGW_GL_v2.0` (or any similarly perspective-drawn tile pack) must default `randomizeRotation` to false** — don't assume every sprite sheet is safe to spin just because prior packs (`TopDownAssets`) were.
**`Physics.SyncTransforms()` is called immediately before every such raycast** — confirmed necessary directly: a collider from a brush painted moments earlier in the same Editor session is not yet visible to `Physics.Raycast` in Edit Mode without it, so painting a building brush immediately after painting a Platform underneath it would otherwise silently resolve against bare Ground (`Y=0`) instead of the Platform's real top surface. This is a general Edit-Mode-scripting gotcha, not specific to this tool — any future Editor script that raycasts against just-created/just-moved colliders in the same call needs the same guard. **Real bug this raycast change introduced, found and fixed post-ship:** `TryErase` matched the nearest painted object via full 3D `Vector3.Distance` against `EraseRadius` (1.5) — fine for every brush painted flush against `Y≈0`, but a right-click on a tall object like `Platform_Tier1` naturally lands on its *visible top surface* (`Y≈2.5`), while the object's own pivot (base-anchored) sits at `Y=0`; that vertical gap alone exceeds `EraseRadius`, so right-click silently never matched it, no error. Fixed by comparing XZ-plane distance only (`Vector2.Distance` on `x`/`z`), which is what "erase what's under the cursor" should mean regardless of an object's height — verified by painting a fresh `Platform_Tier1` and erasing it via a simulated click on its actual top surface. **Second real bug, also found post-ship:** painting a `Terrain`-category brush (`Platform_Tier1`/`Ramp`) while the cursor hovered over *already-placed* elevated terrain (e.g. an existing `Ramp`'s sloped surface) resolved the raycast to that elevated height instead of bare ground, spawning the new piece floating at the wrong Y — its top ends up offset from every surrounding ramp's intended connection height, making it functionally unreachable (units path to its now-too-high base and stop, reading as "walking under" it from the top-down camera). Fixed by forcing `worldPoint.y = 0f` specifically for `Terrain`-category paints, regardless of what the raycast actually hit — both prefabs are base-anchored to start at `Y=0` by design, and this project explicitly doesn't support stacking one terrain tier on another yet (issue #35's own "More than 2 tiers" is out of scope). Props/buildings/resource nodes are unaffected and still correctly resolve to whatever elevated surface they're painted on top of.
- **Splash Brushes (issue #56, shipped 2026-08-27):** an opt-in `MapBrushDefinition` mode — the first extension to the Map Editor's data model since issue #18 — that scatters a randomized cluster of instances per paint tick instead of placing exactly one, framed by the designer as the first step toward a much larger future "player-facing map editor" vision that remains explicitly out of scope (no roadmap slot). Five new additive fields on `MapBrushDefinition`: `isSplashBrush` (bool, default false — every existing brush's behavior is completely unchanged), `splashCount`, `splashRadius`, `randomizeRotation`, and `scaleRange` (`Vector2`, **explicitly initialized to `new Vector2(1f, 1f)` in the field declaration** — Unity's own zero-value default for an unset `Vector2` would otherwise silently spawn every splash instance at zero scale, invisible, until a designer manually opened and fixed each asset by hand; confirmed directly that a pre-existing brush asset retroactively gaining this field deserializes it to the code's `(1,1)` default, not `(0,0)`). `MapEditorWindow.TryPaint` now branches into `PaintSingle` (the original one-instance behavior, unchanged) or `PaintSplash` based on `_selectedBrush.isSplashBrush` — the existing `MinPaintSpacing`/`_lastPaintPosition` drag-loop gating is untouched and still keys off the origin point only, so a splash brush's whole `splashCount` batch counts as one paint tick, exactly like an ordinary brush's single placement; slower dragging naturally produces denser, overlapping coverage by design, not something dampened. **Each scattered instance resolves its own surface height independently** — `Random.insideUnitCircle * splashRadius` (mirroring `ResourceNode.GenerateScatter`'s already-proven XZ-offset pattern, issue #49/#51) picks a candidate XZ position, then a fresh vertical `Physics.Raycast` (from a fixed height above that XZ position straight down, **not** a reuse of `HandleUtility.GUIPointToWorldRay`'s cursor-only ray, which has no meaning for an arbitrary offset elsewhere in the radius) resolves that instance's real Ground-layer height, so a splash painted near a `Platform_Tier1`/`Ramp` edge places each instance on whatever surface is actually beneath it; an instance whose raycast misses the Ground layer entirely (e.g. an offset past the map's edge) is silently skipped, not spawned at a guessed height. Erasing is completely unchanged — right-click still removes exactly the one nearest instance under the cursor via the existing `TryErase`, whether it came from a splash brush or not; no cluster-wide erase exists. **Radius-preview gizmo**, the one piece of this issue that couldn't just extend the existing paint/erase code path: `OnSceneGUI`'s early-return (`if (!isPaintEvent && !isEraseEvent) return;`) exits before any raycast on a mouse-hover-only event, which would have silently prevented the preview from ever showing before the first click — the entire stated point of a preview. Fixed two ways: `sceneView.wantsMouseMove = true` is now set unconditionally at the top of every `OnSceneGUI` call (the Scene View doesn't generate `MouseMove` events at all without this flag, a one-time Editor gotcha with no in-code symptom otherwise — cheap to set every call, since it's idempotent), and the new `DrawSplashRadiusPreview` (`Handles.DrawWireDisc` at the cursor's own resolved Ground-layer raycast position) is called unconditionally whenever a splash brush is selected, *ahead of* the paint/erase early-return, not nested inside it. Proof-of-concept content: `Brush_TreeCluster.asset` (category Props, `splashCount` 6, `splashRadius` 4, `randomizeRotation` true, `scaleRange` (0.8, 1.3)) reuses the existing `Prop_Tree` prefab rather than sourcing new art — same "no new art sourcing" precedent as issue #49's Biomass scatter — relying on per-instance rotation/scale variation alone to read as a naturally-varied cluster; the original single-instance `Brush_Tree` brush is untouched and still available separately. Verified end-to-end via direct `RunCommand` replication of `PaintSplash`'s exact algorithm against the real scene's Ground layer (an `EditorWindow`'s private methods have no `SendMessage`-style invocation path, so this mirrors the identical logic rather than reflecting into it, consistent with this project's established `unity-mcp` reflection caveat): all 6 configured instances placed with zero raycast misses, each landing at the real Ground-layer height, scale values confirmed within the configured `[0.8, 1.3]` range, and rotation variation confirmed across instances. The hover-triggered gizmo preview itself could not be driven the same way (Scene View mouse-hover input has no simulable path in this project's `unity-mcp` sessions, the same class of limitation already documented for `SelectionController`/`AttackCommand`) — verified by code-structure review only: confirmed the gizmo draw call sits ahead of, not inside, the paint/erase early-return.
- **Player Start Points (issue #57, shipped 2026-08-27; visibility fix same day):** replaces `MissionEconomyConfig.FactionEconomyStart`'s previous blind, hand-typed `Vector3 mainBuildingSpawnPosition` field (removed) with a Map Editor-painted marker as the sole source of truth for where each faction's Main Building spawns. New `Economy/PlayerStartPoint` — a `MonoBehaviour` with just a `Faction faction` field, no runtime renderer or collider, purely an authoring-time marker. Two dedicated prefabs (`PlayerStartMarker`, `EnemyStartMarker`, faction pre-set) registered as two Map Editor brushes (`Brush_PlayerStart`, `Brush_EnemyStart`) under a new "Spawn Points" category. `EconomyManager.InitializeFactionStates()` does a `FindObjectsByType<PlayerStartPoint>()` lookup per faction instead of reading the removed config field — a deliberate choice over a `Register`/`Deregister` registry (the pattern `ResourceNode`/`BuildingInstance` use): a registry on `EconomyManager` would need its own explicit exemption from `CleanupExistingState()`'s per-round destruction (the same class of risk already documented for `ResourceNodes`), while a transient lookup at spawn time sidesteps that whole question for free, and has direct precedent in `TestSetup.cs`'s own `FindObjectsByType<MainBuilding>()`. A faction with a configured `mainBuildingPrefab` but no matching start point logs a clear `Debug.LogError` and skips spawning that faction's Main Building — no silent fallback to the origin; multiple start points for the same faction use the first one found (via `FindObjectsByType`'s own unspecified enumeration order — not paint order) and log a `Debug.LogWarning` naming the count. **Visibility bug, found and fixed the same day via real designer usage, not caught by shipping verification:** `PlayerStartPoint`'s only visual was originally `OnDrawGizmos` (a faction-colored `Gizmos.DrawWireSphere`) — this was accepted at ship time as "this project's first `OnDrawGizmos` usage, confirmed rendering correctly" based on code-structure reasoning and the fact that Gizmos don't share URP's known rendering pitfalls, but that reasoning never actually checked the one thing that matters for Gizmos specifically: the Scene view's own "Gizmos" toolbar toggle (`SceneView.drawGizmos`), confirmed **off** in the designer's real Editor session — every `OnDrawGizmos` call in the project is silently skipped while it's off, with zero error or other symptom, so a freshly-painted start point looked like nothing had happened. Root-caused by directly querying `SceneView.lastActiveSceneView.drawGizmos` via `unity-mcp`, not guessed. **Fixed by adding `MapEditorWindow.DrawStartPointMarkers()`**, called unconditionally every `OnSceneGUI` call: iterates every `PlayerStartPoint` in the scene and draws a faction-colored `Handles.DrawWireDisc` at each one — `Handles` calls made from a window's own `OnSceneGUI`/`SceneView.duringSceneGui` are **not** gated by the Gizmos toolbar toggle, confirmed by the fact that issue #56's splash-radius preview (the same `Handles.DrawWireDisc` call shape) was visibly working the whole time under the identical Gizmos-off condition — the working/non-working split was `Handles` vs. `Gizmos`, not anything brush-specific. `PlayerStartPoint`'s own `OnDrawGizmos` was left in place as a harmless fallback for Scene-browsing without the Map Editor window open. **Real-world consequence of the original bug, not just a theoretical gap:** repeated clicking while trying to get visual confirmation left 51 scattered `PlayerStartPoint` markers in the scene (49 more than intended) — cleaned up back to exactly the original two, at `(-20,0,0)` Player / `(20,0,0)` Enemy. **Verify any future Editor-only visual that uses `OnDrawGizmos` in this project against `SceneView.drawGizmos`'s actual state, not just "does it compile and is Gizmos rendering conceptually unrelated to URP" — the toggle itself is a separate, real failure mode Gizmos have that Handles-based drawing from a custom tool's own `OnSceneGUI` does not.**
- **RPGW Decorative Props (issue #62, shipped 2026-08-28):** the project's first content pulled from the newly-imported `Assets/RPGW_GL_v2.0` pack ("RPG Worlds Grass Land" v2.0) — three new Map Editor Props (`Prop_Rock`, `Prop_Tree_Green`, `Prop_Bush`), each with a matching splash-cluster brush, sourced from `decorative.png`. **The pack's `Sliced/` folder name is misleading** — every sheet in the pack (`MainLev2.0.png`, `MainLev_autotiling.png`, `decorative.png`, `decorative_nograss.png`) actually imported as `spriteMode: Single` with an empty `spriteSheet.sprites` array; nothing was pre-sliced. `decorative.png` was sliced via `TextureImporter` scripting (`spriteImportMode = Multiple`, a hand-specified `spritesheet` array of three named `SpriteMetaData` rects) rather than the Sprite Editor UI, after first locating the three target shapes programmatically — a flood-fill connected-component scan over the texture's alpha channel (temporarily flipping `isReadable`, restored after) found every distinct blob's bounding rect, avoiding hand-guessed pixel coordinates. Also fixed the same import mistake `CLAUDE.md`'s Sprite & Art Convention already warns about: `decorative.png`'s `TextureImporter` had `filterMode: 0` (Point), corrected to Bilinear. All three prop prefabs follow `Prop_Tree`/`Plant1-3`'s exact shape (root + `Art` child, 90° X rotation, `Sprites-Default` material, no Collider) — including, per the `/arch` review for this issue, confirming that shape's real Y offset is `0`, not the positive nudge `RingMesh`/`ResourceNode` scatter instances need; matched that directly rather than inventing an offset. The two splash-cluster brushes (Rock, Tree Green) initially reused `Brush_TreeCluster`'s exact tuning (`splashCount=6`, `splashRadius=4`) — **corrected same-day after the designer saw it painted for real and flagged it as looking like a rotation bug.** It wasn't: isolating a single instance from a real user-painted cluster (toggling every sibling `SpriteRenderer` off but one) proved every instance's transform was already correct (`Art`'s world rotation is exactly `(90°, randomY, 0°)` — flat, just spun around the vertical axis) and rendered as a normal, clean tree on its own. The actual cause is `PaintSplash` having no minimum-spacing enforcement between scattered instances: at `radius=4`/`count=6`, a full tree's own irregular, multi-lobed canopy silhouette (unlike a small round plant icon) chains together with its neighbors' lobes into one connected snake-like blob the moment two instances land close together, which reads as "something's rotated wrong" even though nothing is. Retuned both `Brush_RockCluster` and `Brush_TreeGreenCluster` to `splashRadius=8` (count unchanged at 4, reduced from 6 during the same pass) — confirmed via repeated repaint-and-capture that this keeps individual instances visually separated as a natural loose grove rather than a merged mass. **`Brush_TreeCluster` (the original, pre-#62 precedent) was deliberately left untouched** — same tuning, likely the same underlying issue, but out of scope for this fix; flagged for whoever next touches it. Zero `MapEditorWindow.cs` changes throughout, exactly as scoped — the tool's `PaintSingle`/`PaintSplash`/`TryErase` are already fully generic over any registered `MapBrushDefinition`.
- **Terrain Elevation: Platforms & Ramps (issue #35, shipped 2026-08-22):** two new paintable Map Editor brushes — `Platform_Tier1.prefab` and `Ramp.prefab` (`Assets/Prefabs/`, registered via `Assets/Data/MapEditor/Brush_Platform_Tier1.asset`/`Brush_Ramp.asset` into the existing `MapBrushPalette`, category "Terrain") — give designers real 3D raised ground: a flat elevated plateau and a sloped connector, both on the `Ground` layer with `NavigationStatic` set, so units can path up onto them and combat/vision treat them as real elevation (not a visual-only overlay like the Terrain Tilemap). Each prefab is a root (`NavigationStatic`, no visual) + child `Visual` (`NavigationStatic`, mesh + `BoxCollider`, material `Assets/Data/Terrain/TerrainElevationMaterial.mat`) — a custom base-anchored box mesh (`Assets/Data/Terrain/PlatformTierMesh.asset`/`RampMesh.asset`), not Unity's built-in cube, so it can be scaled from the object's own base/edge instead of its center: `Platform_Tier1`'s mesh spans local `X,Z:[-0.5,0.5]`, `Y:[0,1]` (base-anchored in Y only); `Ramp`'s spans `X:[-0.5,0.5]`, `Y:[0,1]`, `Z:[0,1]` (base-anchored in both Y and Z), with the `Visual` child then rotated `(-26.57°, 0, 0)` (`Atan2(rise, run)`) to lay the slope down. **Three real, non-obvious bugs found and fixed during this issue, in order of severity:**
  1. **Triangle winding was inverted on both custom meshes, breaking rendering AND NavMesh baking simultaneously.** The mesh-building code computed each quad's winding using a right-hand-rule assumption, but Unity is left-handed — every face's *stored vertex normal* said "facing up/out" while its *actual geometric winding* (cross product of its own edges) said the opposite. Stored normals are a pure shading/rendering input; Recast's NavMesh voxelization reads a triangle's walkability from its real geometric winding, not the stored normal — so the platform's top face was simultaneously invisible (backface-culled from above) and completely absent from every NavMesh bake, with zero errors anywhere. Diagnosed by comparing an isolated built-in `PrimitiveType.Cube` (worked perfectly) against an isolated instance of the real prefab (produced zero walkable geometry) with otherwise-identical dimensions — confirmed the mesh itself, not scene context, was the cause. **Fixed by reversing the triangle index order in the shared `AddQuad` helper** (`(start, start+2, start+1)` / `(start, start+3, start+2)` instead of the naive `+1,+2` / `+2,+3` order). Any future custom-mesh-building code in this project must verify actual geometric winding (e.g. by testing whether a built-in primitive with the same intended normals renders/bakes correctly, then comparing), not just trust `SetNormals()` — the two are independent and Unity gives no compile-time or runtime warning when they disagree.
  2. **Both custom mesh assets had a null `MeshFilter.sharedMesh` at the prefab-asset level**, discovered while diagnosing bug #1 — the mesh was built in memory during original prefab construction but never persisted via `AssetDatabase.CreateAsset`, so the reference silently dropped on `PrefabUtility.SaveAsPrefabAsset`. No error at save time or on later load; the symptom was purely "nothing renders, nothing bakes." Any procedurally-built `Mesh` that needs to survive into a saved prefab **must** be saved as a real asset (`AssetDatabase.CreateAsset`) before being assigned to a `MeshFilter` that gets baked into a prefab — assigning an in-memory-only `Mesh` reference works fine for the rest of the same Editor session but is silently lost on save.
  3. **The project's default classic NavMesh bake (`UnityEditor.AI.NavMeshBuilder.BuildNavMesh()`, at the default ~0.1667 voxel size) reliably failed to connect the Ramp's sloped top to the Platform's flat edge**, even after fixing bugs #1–2 and after generous physical geometry overlap between the two (tested from a bare 0.18-unit gap up through several units of deliberate overlap) — `NavMesh.CalculatePath` from ground to platform consistently returned `PathPartial`, stopping just short of the plateau. Root cause: the default voxel size is too coarse to reliably rasterize the seam between a rotated sloped surface and a flat one as a single connected region — a known hard case for Recast-style voxelization, unrelated to bugs #1–2. **Fix: `Assets/Editor/NavMeshBakeUtility.cs`** (`MechTS/Bake NavMesh` menu item) bakes at voxel size `0.1` instead of the default, and — since a manual `NavMeshBuilder.BuildNavMeshData`+`NavMesh.AddNavMeshData` bake is session-only and does *not* persist like the Navigation window's own Bake button — writes the result into the scene's existing persisted `NavMesh.asset` (`Assets/Scenes/<SceneName>/NavMesh.asset`, creating one at that conventional path if none exists) via `EditorUtility.CopySerialized`, so it survives an Editor restart and is included in builds exactly like a normal classic bake. **Use this menu item instead of the Navigation window's own Bake button whenever the map has any elevation** (a `Platform_Tier1`/`Ramp` pair) — flat-only scenes can still use either, and this doesn't change the project's "no `NavMeshSurface`, no runtime baking" convention, just how the one-time Editor-time bake is invoked. Verified end-to-end: a full `NavMeshPath` from bare ground, up the ramp, onto the platform interior returns `PathComplete`; the platform's non-ramp cliff faces remain correctly unwalkable; a top-down render capture confirms both pieces render and cast shadows correctly post-winding-fix. **Known minor imperfection, not fixed (flagged, non-blocking):** the ramp slab's own thickness offsets its walkable top surface from the mathematically "ideal" ground-to-platform line by a small, roughly constant amount both at its low (ground) end and high (platform) end — harmless with the `0.1` voxel bake and well within the project's `0.75` Step Height tolerance, but a thinner slab or a tapered-wedge mesh would remove the offset entirely if a future pass wants a perfectly flush visual seam.
- **Flying units bypass terrain elevation entirely — a second NavMesh Agent Type, not a climb/slope tuning problem (issue #40, shipped 2026-08-24):** the Saboteur (already drone-art, confirmed by the designer as the intended first flying unit) got a real capability distinction from `IsFlying` (#25, which only ever disabled agent-to-agent avoidance): its `NavMeshAgent.agentTypeID` now points at a second registered Agent Type, "Flying" (`agentTypeID -1372625422`, authored via `NavMesh.CreateSettings()` in an earlier session and named by directly editing `ProjectSettings/NavMeshAreas.asset`'s `m_SettingNames` — `NavMesh.SetSettingsNameFromID` does not exist as a scripting API), with `baseOffset = 0.5`. **Design correction that reframed the whole approach:** the original technical target was "make a flying unit able to climb/cross a vertical cliff via climb/slope tuning" — extensively tested (climb=10/slope=89, then slope=90, Recast's own max) and conclusively a dead end; a genuinely vertical wall never becomes traversable no matter the tuning, `NavMesh.CalculatePath` always returned `PathPartial`. The designer corrected the actual intent: flying units don't climb elevation, they're simply never blocked by it (the stated long-term vision: capability-gated terrain passability generally — e.g. water passable only if flying — built on #39's `UnitCapability` flags). This reframes the fix from a tuning problem into an exclusion problem: **`Assets/Editor/NavMeshBakeUtility.cs` now bakes every registered Agent Type in one pass**, and for any agent type other than 0 (Humanoid), filters `NavMeshBuilder.CollectSources`'s result to drop every source under the Map Editor's "Terrain" brush category (`MapContent/Terrain/*` — `Platform_Tier1`/`Ramp` instances) before calling `BuildNavMeshData`, so the Flying agent's NavMesh literally doesn't contain elevation geometry at all — a flying unit crosses the same flat baseline a ground unit needs a ramp to reach. Each non-Humanoid agent type bakes to its own `NavMesh_<Name>.asset` (`Assets/Scenes/<SceneName>/`); `Bootstrapper` gained `[SerializeField] NavMeshData _flyingNavMeshData`, loaded via an explicit `NavMesh.AddNavMeshData()` call in `Awake()` — unlike agent 0's data, a second agent type's `NavMeshData` has no automatic scene-association load path, so this wiring is mandatory per additional agent type, not optional. **Two real, non-obvious bugs found and fixed:**
  1. **`StaticEditorFlags.NavigationStatic` toggled live via script does not affect the same-session result of `NavMeshBuilder.CollectSources`.** Confirmed directly: cleared the flag, read it back as correctly cleared, and `CollectSources` still returned the object regardless — a genuine `unity-mcp`/Editor-scripting gotcha in the same family as the already-documented `Physics.SyncTransforms()` staleness issue (issue #18's notes). Fixed by filtering the already-collected `List<NavMeshBuildSource>` directly (`sources.RemoveAll(...)`) instead of trying to influence collection via the flag.
  2. **A stray leftover GameObject with the same name as a legitimately-excluded object, sitting outside the expected parent, silently reintroduced the excluded geometry.** During isolated testing, a duplicate `IsolatedTestPlatform40` (from an earlier `Instantiate` call, never cleaned up) existed at scene root — not parented under `MapContent/Terrain` — alongside the correctly-parented one. The exclusion filter (which walks from `MapContent/Terrain`'s children) correctly ignored the orphan, so its render source survived filtering and polluted every subsequent Flying bake, even though the console log's source-count reduction (19→6) looked entirely correct at every step. Diagnosed only by dumping the *actual* post-filter source list's component names/paths/positions rather than trusting the aggregate count, and cross-checking `Resources.FindObjectsOfTypeAll<Transform>()` for every object sharing that name in the scene. **Takeaway: when a source-exclusion filter's logged count looks right but the output still contains the excluded content, suspect scene-content duplication (a leftover test object, a duplicate paste) before suspecting the filter logic itself** — print names and positions of what actually survives, not just how many.
  Verified end-to-end: `NavMesh_Flying.asset`, loaded in complete isolation, contains zero elevated-Y triangulation vertices; a Ground-agent path across a raised plateau region returns a 7-corner detour around its edge (`PathComplete`, corners as far as X=-21.3 to go around), while the identical start/end points under the Flying agent type return a straight 4-corner `PathComplete` path staying near the direct line between the two points, correctly ignoring the plateau's footprint.
- **Swapping art on a *duplicated* unit prefab (established with the Enemy variants, issue #16, 2026-08-19), not just a placeholder:** `EnemyHarvesterUnit.prefab`/`EnemySaboteurUnit.prefab`/`EnemyTestCombatUnit.prefab` were built by duplicating the existing Human prefab (`PrefabUtility.LoadPrefabContents` on the Human prefab, `SaveAsPrefabAsset` to a new path) and swapping only the `Art` child, same procedure as above. The new failure mode this uncovers: destroying the old `Art` child also destroys any of *its own children* that a sibling component's `[SerializeField]` list referenced directly — `VehicleAnimationDriver._tracks` (→ the old `TrackL`/`TrackR` `WheelAnimation`s) and `PropellerSpin._propellers` (→ the old `Propeller1-3` transforms) both went dangling, throwing `UnassignedReferenceException` every frame in Play Mode, silent at compile time and even at save time. `Weapon._muzzle` had the same exposure but didn't need a fix — it already null-guards to `transform` in `Awake()` (`if (_muzzle == null) _muzzle = transform;`), so losing that reference degrades gracefully instead of throwing. **After any art swap on a prefab whose root has sibling components that reference art-child descendants by `[SerializeField]`, explicitly check every such field** (`SerializedObject`, hunt for `objectReferenceValue == null && objectReferenceInstanceIDValue != 0`, or just enter Play Mode and read the console) rather than assuming "the art still looks right" means the whole prefab is fine — a dangling reference has no visual symptom at all until the referencing component's `Update()` actually runs.

---

## Asset Roster (TopDownAssets)

Inventory of what's already in `Assets/TopDownAssets/`, as of project setup — informed the RTS direction. Not a design commitment; `/ba` should confirm exact roster composition when specing the unit system.

- **`Common/`** — shared projectile/effect/audio infrastructure (`Projectile.cs`, `Shell.cs`, `Turret.cs`, `Vehicle.cs`), tank tracks/wheels art.
- **`FuturisticPack/Sprites/NewArrivals2/`** — `Harvester` (the asset that first suggested "gather resources" RTS-style), `APC`, `DroneX3`, `DroneX4`, `Helicopter`, `Hovercraft`, `MachineGunTurret`, `PlazmaTurret`, `RocketTurret`.
- **`FuturisticPack/Sprites/Vehicles/`** — `Vehicle1`–`Vehicle3`.
- **`FuturisticPack/Sprites/Aircrafts/`** — flying units.
- **`FuturisticPack/Sprites/Mechs/`** — `Mech1`–`Mech4`, each a matched Body/Head/LeftArm/RightArm set.
- **`FuturisticPack/Sprites/PowerArmor/`** — `Blue`/`Green`/`Red` × `Mark1`–`Mark4`, same matched-parts shape as Mechs — likely player/infantry-unit candidates.
- **`FuturisticPack/Sprites/ArmoredSoldiers/`** — infantry-scale units.
- **`FuturisticPack/Sprites/AliensPack1/`, `AliensPack2/`** — the Enemy faction's roster (issue #16, shipped 2026-08-19): `AliensPack1/AlienSpider1` (Enemy Harvester), `AliensPack2/Alien1` (Enemy Saboteur), `AliensPack2/Alien7` (Enemy combat unit), `AliensPack2/Alien4` (Enemy Crewman, issue #42) — all matched-parts `Transform`+`SpriteRenderer` rigs, no built-in animation (same static-pose precedent as `BlueSoldierLight`). Neither pack has any structure/building-shaped content — both are creature rigs only — so the Enemy main building still uses the same placeholder prefab as the Player's; flagged as a known gap for a future asset-sourcing pass, not force-fit.
- **`FuturisticPack/Sprites/ArmoredSoldiers/Green/Light/GreenSoldierLight`** — the Player Crewman's art (issue #42), same matched-parts/no-animation shape as `BlueSoldierLight` (used by `TestCombatUnit`) — Green was picked specifically to stay visually distinct from Blue's existing combat-unit use.
- **Technology Plant & Robotics Plant — building-ownership gates + turrets (issue #45, shipped 2026-08-24):** the first genuine building-ownership gate in the codebase — previously `TechManager.CanResearch` had zero building-ownership check at all, anyone could research from turn one. `BuildingDefinition` gains three new boolean fields: `enablesResearch` (Technology Plant), `enablesTurretConstruction` (Robotics Plant), and `requiresRoboticsPlant` (turret definitions) — the third wasn't in the issue's own explicit two-field list, but is necessary to actually distinguish "this building is a turret" from every other buildable definition; flagged as a needed addition beyond the literal spec. A new shared `EconomyManager.FactionOwnsBuildingWhere(Faction, Func<BuildingDefinition,bool>)` helper (skips any `IsUnderConstruction` building, matching every other ownership-sensitive check in the codebase) backs both gates from one place: `TechManager.CanResearch` now requires `HasResearchBuilding(faction)` in addition to its existing prerequisite/affordability checks, and `BuildMenuPanel` gates any `requiresRoboticsPlant` entry's `interactable` + shows a `"(Requires Robotics Plant)"` label suffix (kept visually distinct from the existing research-lock `"(Locked)"` suffix, since they're different gates) the same way tech-unlock gating already works. `TechPanel` gained a status line at the top ("Research locked — build a Technology Plant..." / "Research enabled") so the player understands *why* every research button is disabled, distinct from a per-upgrade affordability reason — the per-button `interactable` state itself needed zero panel-level code change, since it already flows through the now-extended `CanResearch`. **Turrets**: the first defensive structure in the codebase — `MachineGunTurretBuilding.prefab` reuses the pack's `MachineGunTurret` art (a clean, zero-script matched-parts rig, same static-pose precedent as every other unit-art swap) on a `BuildingInstance`+`Health`+`Weapon` shape (no `ProductionQueue` — turrets don't produce units), confirming the arch review's prediction that `Weapon`/`Health` already fully support a `BuildingInstance` owner with no new owner-resolution branch needed. **Real bug caught and fixed before shipping**: the turret prefab was built by duplicating `EconomicBuilding.prefab` (matching the Barracks/Drone Factory precedent), which carries a non-uniform `(2, 1.5, 2)` root scale sized for its placeholder cube mesh — after swapping the cube for the flattened `Art` child (matching the unit-art convention: 90° rotation, small Y offset), that inherited non-uniform scale would have stretched the sprite art asymmetrically. Fixed by resetting the root to a uniform scale and sizing the `BoxCollider` directly instead — any future building that swaps its placeholder cube for real flattened sprite art (rather than staying a primitive) needs the same fix, not just a straight copy of an existing cube-based building prefab. Verified end-to-end in Play Mode: research correctly blocked with no Technology Plant, then correctly unlocked and successfully completed after building one; `FactionOwnsBuildingWhere` toggled `False→True` for Robotics Plant exactly as expected; a turret's `Weapon` correctly auto-acquired and killed a nearby Enemy unit once Vision (issue #37's fog-gate) was established — confirmed a first attempt at this last check silently dealt zero damage purely because `VisionManager` hadn't been ticked in that particular test, not a code defect, resolved by ticking it before re-testing.
- **Barracks & Drone Factory (issue #44, shipped 2026-08-24):** two new production buildings, entirely data-only on the already-generic `BuildingDefinition.canProduceUnits`/`producibleUnits` system — no code changes at all beyond content. Barracks (`BarracksDefinition.asset`, brown placeholder cube, same `EconomicBuilding.prefab` shape/mesh/collider) produces Ranger/Reaper/Dredge (#43); Drone Factory (`DroneFactoryDefinition.asset`, steel-blue placeholder cube) produces the Saboteur — MechTS's only drone-type unit, per the Asset Roster (Reaper stays under Barracks despite its "scout" identity since its actual art, per #43, is infantry-scale, not drone-themed). **This issue is also where the first-ever `UnitProductionDefinition` wrapper assets for Ranger/Reaper/Dredge/Saboteur were created** (`Assets/Data/Units/*ProductionDefinition.asset`) — #43 explicitly deferred this ("production-menu wiring... deferred until Barracks/Drone Factory are spec'd next"), and Saboteur had never had one at all before now (always pre-placed only, per `TestSetup`). The Saboteur's production definition reuses `SaboteurConfig`'s own long-dormant `oreCost`/`biomassCost`/`goldCost`/`powerConsumed` fields (100/300/1000/1) — first time they've ever actually been consumed by anything; **flagged as a likely balance issue, not fixed**: 1000 gold is far more than the default mission's starting 50, making the Saboteur practically unproduceable at a Drone Factory under current starting resources (confirmed directly: `TryEnqueue` correctly returned `false` against a starting balance, and correctly returned `true` once resources were manually granted, spawning the unit) — a designer-tuning question, not a bug in the production system itself, which behaved exactly as spec'd (`CanAfford` correctly gates the queue). Both buildings registered as Map Editor "Buildings"-category brushes and added to `TestMissionEconomyConfig.buildableBuildings`, so they show up in `BuildMenuPanel` immediately. Verified end-to-end in Play Mode via the full #42 Crewman-build flow: a Crewman constructed each building, then `ProductionQueue.TryEnqueue` correctly spent the right resources and spawned the right unit type from each (`RangerUnit(Clone)` from Barracks at exactly `-20` Ore; `SaboteurUnit` from Drone Factory at exactly `-1000` Gold once affordable).
- **Combat Unit Roster Expansion — Ranger, Reaper, Dredge (issue #43, shipped 2026-08-24):** three new Player-only combat units (Enemy-faction equivalents explicitly out of scope), all built on the exact `Weapon`/`WeaponConfig`/`UnitBase` shape `TestCombatUnit` already proved — no new mechanics, purely new prefabs/configs/art (see `CombatUnitConfig`/`CombatUnitStats` above). Stat tuning: **Ranger** (standard infantry — `RedSoldierMedium` art, distinct from `TestCombatUnit`'s Blue and Crewman's Green) 120 HP, speed 4, 15 damage/1.0 fire-rate/12 range, `WeaponTargetType.Ground`; **Reaper** (fast scout — `DroneX3` art, previously unused) 70 HP, speed 6, 8 damage/2.0 fire-rate/10 range, `WeaponTargetType.Ground`; **Dredge** (heavy ground/air mech — `Mech1` art, previously unused) 200 HP, speed 2.5, 25 damage/0.7 fire-rate/14 range, `WeaponTargetType.GroundAndAir` — the first real (non-default) consumer of issue #30's ground/air targeting split. **Spec self-contradiction caught and resolved during `/dev`:** the issue's own acceptance criteria offered Ranger `Ground` *or* `GroundAndAir` "whichever's more generically useful, matching `TestCombatUnit`'s current default" — but a separate, more specific acceptance line stated "all three get `Ground | GroundAttacker`; Dredge additionally gets `AirAttacker`," which is only true if Ranger/Reaper are `Ground`-only. Initially built both defaulting to `GroundAndAir` (matching the first, looser instruction) and caught the mismatch by testing `UnitBase.Capabilities` directly in Play Mode — corrected both `WeaponConfig` assets to `Ground` before shipping, since the capability-derivation line was the more specific, directly-falsifiable requirement. Verified end-to-end in Play Mode: `Capabilities` reads exactly `Ground|GroundAttacker` for Ranger/Reaper and `Ground|GroundAttacker|AirAttacker` for Dredge. Existing building definitions/prior units untouched — `Weapon`, `WeaponConfig`, `AttackCommand`, and `UnitOrderCommand` needed zero changes, confirming they really were already unit-type-agnostic. Ships `TestSetup`-spawnable only (Player faction) — no production building can produce any of the three yet, deferred to Barracks/Drone Factory (#44) per the agreed sequencing.
- **`FuturisticPack/Sprites/Equipment/`** — `EnergyCell`, `FuelCanister`, `FuelTank`, `Light` — pickup/resource-prop candidates.
- **`FuturisticPack/Sprites/AdditionalWeapons/`** — weapon variants for the above.

---

## Unity MCP

The `unity-mcp` relay is configured globally in `~/.claude.json` (`C:\Users\andre\.unity\relay\relay_win.exe --mcp`) — that part isn't per-project. The Editor-side bridge comes from the `com.unity.ai.assistant` package, added to `Packages/manifest.json` on 2026-08-17 (matching the version working in `Buildables`: `2.6.0-pre.1`) and confirmed connected the same day.

**Known quirk: the player loop can be frozen in a headless session.** In the first verification pass, `Time.frameCount` stayed at `1` (and `Time.time` at `0`) indefinitely during Play Mode, even after 190+ real seconds — this Editor session has no window compositor pumping frames, so `Update()`/`Start()` never fire past the initial frame unless something forces them. `Bootstrapper`-created managers still get a real `Start()` (Unity runs all `Awake()`s then all `Start()`s once during the first frame, regardless of whether `Update()` ever ticks again), so they're fully testable via direct `RunCommand` calls. Anything instantiated afterward needs `gameObject.SendMessage("Start", SendMessageOptions.DontRequireReceiver)` forced manually, and its behavior verified via direct method calls rather than by waiting — `sleep`-and-poll does not work for simulated game time here. Input-driven systems (`SelectionController`, `AttackCommand`, `MoveCommand`, `ControlGroupController` — all of issue #4) can't be verified at all this way, since they need either real input events or a genuinely running player loop. Check `Time.frameCount` right after entering Play Mode before assuming otherwise; see `memory/PATTERNS.md` for the full pattern.

**Known quirk: editing a script mid-Play-Mode can silently lose a manager's C# event subscription while leaving its other state intact.** Confirmed during issue #15's testing (a ticking, non-frozen session this time — `Time.frameCount` was climbing normally): after editing `BuildMenuPanel.cs`/`ProductionMenuPanel.cs` and recompiling, `EconomyManager.GetState(Faction.Player)` returned `null` even mid-`BattleRound`, where it should have carried state over from the prior Economy Round. `EconomyManager`'s own `_gameManager` field reference was still valid (`IsEconomyRoundActive` read correctly), but its `_gameManager.OnGameStateChanged += HandleGameStateChanged` subscription from `Start()` had gone dead, so `GameManager.StartEconomyRound()` transitioned state correctly but silently never triggered `EconomyManager.InitializeFactionStates()`. `TechManager` had the identical issue independently (its `_unlockedUnits`/`_unlockedBuildings` survived a full Economy-Round-reset cycle that should have cleared them, because its own event subscription was equally dead). Both were fixed the established way — `gameObject.SendMessage("Start", SendMessageOptions.DontRequireReceiver)` on the affected manager, which re-subscribes as a side effect of re-running `Start()`. The takeaway: this is broader than the already-documented "recompile wipes non-serialized fields" pattern — it can specifically break a manager's event *subscription* while its plain field data stays intact, so a manager reacting correctly to direct field reads is not proof its event-driven reactions still work. After any mid-session script edit, don't just re-check a manager's fields — force `Start()` on every manager touched earlier in the session and re-verify its event-driven paths specifically (e.g. trigger the state transition and check the *downstream effect*, not just that the transition itself succeeded).

---

## Dependencies (UPM)

**Present:**
- Universal Render Pipeline (URP)
- Input System (new)
- AI Navigation (`com.unity.ai.navigation`) — used by `UnitBase`'s `NavMeshAgent`-driven movement (issue #4)
- AI Assistant (`com.unity.ai.assistant`, `2.6.0-pre.1`) — added 2026-08-17, hosts the Editor-side `unity-mcp` bridge; not yet resolved by Package Manager (needs the Editor opened once)
- 2D Tilemap Editor (`com.unity.2d.tilemap`, `1.0.0`) — added 2026-08-19 for issue #17 (Terrain Tilemap Foundation); provides `Window > 2D > Tile Palette` and the `UnityEditor.Tilemaps` namespace (`GridPaletteUtility` etc.). Wasn't present before — only the runtime-only `com.unity.modules.tilemap` built-in module was, which is enough for `UnityEngine.Tilemaps` (`Tile`/`Tilemap`/`TilemapRenderer`) but not the Editor-side palette tooling. If the Tile Palette window or `UnityEditor.Tilemaps` types are ever missing again, check this package specifically before assuming a scripting/compile issue.
- Timeline, Visual Scripting, Test Framework — present, unused so far

**Removed during setup:** `com.unity.collab-proxy` (Plastic SCM) — this project uses git/GitHub, not Plastic.

---

## Dev Pipeline

This project uses the same agent pipeline as the studio's other Unity project (BA → Architecture Review → Dev → Test → UX → Board), backed by GitHub Issues on `ScurvyMonkey/MechTS`:

| Skill | Responsibility |
|---|---|
| `/ba` | Requirements, spec definition, GitHub Issues management |
| `/arch #<issue>` | Architecture review — validates fit, risk, and phase-scope compliance before dev starts |
| `/dev #<issue>` | Implementation — reads issue, implements, closes when done |
| `/test #<issue>` | Runs Unity Test Framework suites, classifies failures as feature vs. regression |
| `/ux #<issue>` | Visual/identity review via Play Mode capture |
| `/board` | GitHub label/pipeline-stage management |
| `/triage` | Scores and ranks the open backlog |
| `/handoff #<issue>` (alias: `greenlight #<issue>`) | Drives an approved issue autonomously through dev → test → ux → ship |

See each skill's file in `.claude/skills/` for full detail. Recommended flow: `/ba <idea>` → `/arch` → `/board` creates the issue → `greenlight #N` to run the rest autonomously, or drive steps manually.

**Git convention (established 2026-09-02):** this project went from its initial scaffold commit to ~80 shipped issues with zero further commits — everything was sitting uncommitted on disk. Fixed by (1) committing all actual project content (`Assets/Scripts`, `Prefabs`, `Data`, `Editor`, `Scenes`, etc.) and (2) adding `/handoff`'s own Step 6.75, which commits and pushes automatically once an issue passes test+UX, before board close — so this can't silently pile up again. **Large third-party asset packs are deliberately excluded from git** (`.gitignore`: `TopDownAssets`, `RPGW_GL_v2.0`, `ScifiRTSSeriesMegaPackI`/`II`/`III`, `SpacePlatformKit`, `Universal Sound FX`) — they're re-importable from their source, and versioning ~5GB of vendor binaries would blow past GitHub's free Git LFS quota for no real benefit. If you add a new large purchased asset pack, add it to `.gitignore` the same way rather than letting it get committed by default.

**First real session should be `/ba` on the Economy Round / Battle Round core loop** — everything else in Phase 1 depends on it.

---

## Documentation References
- Unity Manual: https://docs.unity3d.com/Manual/UnityManual.html
- Scripting API: https://docs.unity3d.com/ScriptReference/index.html
- Design doc: `docs/GDD.md`
