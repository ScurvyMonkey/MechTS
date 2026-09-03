## Drew's Requirements

Live backlog. Implemented items are compacted to their GitHub issue number, not deleted — check `ScurvyMonkey/MechTS` issues for full history. Everything under **Open** is unimplemented and fair game for `/ba` to spec next; **Needs Your Call** items are cases where a later decision changed or contradicted the original ask.

## Implemented

- Movement, mouse click/drag select — #4
- Mouse edge-of-screen camera pan, camera manager — #10
- Game manager (state machine) — #1
- Economy Round resources/production, player + enemy spawn base, resource/unit buildings, unit creation — #2
- Espionage / Saboteur — #3
- Combat resolution, building health — #5
- Battle Round victory conditions — #6
- In-round HUD — #7
- Mission end screen — #8
- Main menu (start/end UI) — #9
- Audio manager — #11
- Minimap — #12
- Technology upgrades (tree, stat wiring, unlock gating) — #13, #14, #15
- Enemy (Aliens) prefabs/roster — #16
- Terrain Tilemap visual layer — #17
- Map editor (paint prefabs onto the map) — #18
- HUD cleanup: removed duplicate/overlapping resource canvas, moved build+production menus to bottom-left, minimap to top-right, selection info into the control-group corner — #19
- Vehicle engine audio slot (`HarvesterConfig.engineSound`), matching `WeaponConfig`'s firing-audio pattern — #29
- Rally points / waypoints from production buildings — #23
- Select enemy units/buildings to inspect info — #24
- Flying-unit agent-to-agent avoidance disabled (`IsFlying`) — #25 (does **not** cover "flying passes over ground/cliffs" — see Open)
- Unit orders: attack-move, patrol, defend, return to base — #26
- Weapon ground/air/both targeting (`WeaponConfig.targetType`) — #30
- Right-click/Attack sets an explicit sticky target lock (not nearest-in-range) — #31
- Patrol path line indicator — #32
- Zoom-linked camera height, per-category audio priority/volume — #33
- Terrain elevation: platforms + ramps, NavMesh connectivity — #35
- Fog of War foundation — #36
- Fog-gated combat/inspection (can't target/select what you can't see) — #37
- Harvester resource-node redirect (right-click a node to reassign) — #38
- Capability-scoped tuning multipliers (research can target ground/flying/gatherer/etc.) — #39
- Flying units bypass terrain elevation (Saboteur is the first flying unit) — #40
- Harvesters buildable directly from the Main Building — #41
- Crewman unit: repair/heal, Crewman-mediated building placement/construction — #42
- Combat roster: Ranger, Reaper, Dredge — #43
- Barracks & Drone Factory (production buildings) — #44
- Technology Plant & Robotics Plant (research/turret ownership gates) — #45
- Building placement: right-click to confirm (not left-click), placement silhouette/ghost preview, overlap prevention — #46
- Harvester track roll animation smoothing (config-tunable rate, decoupled from raw movement speed) — #47
- Unit upkeep: every unit costs ongoing per-minute Ore/Biomass/Gold; a negative stockpile is an intentional all-in tradeoff (never affects victory conditions, but carries unclamped into Battle Round) — #48
- Area-based resource nodes: scattered cluster of many objects sharing one pool instead of single-sprite depletion swaps; Harvester still targets the area's center point — #49
- Harvester nearest-scatter-point targeting: Harvester walks to and gathers from whichever scattered instance in the area is nearest to it, re-picking nearest each return trip and rerouting if its specific target is destroyed mid-travel — #50
- Auto-Gathering Building (Auto-Extractor): a building placed within a resource node's radius that passively extracts its yield once per second, no Harvester round-trip — #51
- Locked Resource Pool & Silo Buildings: a per-resource-type locked pool filled by a live-adjustable % of incoming resources (Resource Allocation panel, hotkey `R`), capped by capacity from single-resource-type Silo buildings; overflow falls back to the normal stockpile, pool carries intact into Battle Round; spending it is deferred to the Injection Round — #52
- Injection Round (corrected): a Battle-Round-only panel (hotkey `I`, gated on `GameState.BattleRound` specifically) where the player injects new upgrades from the locked pool, or reverts an already-researched swappable upgrade (free) to inject a replacement — in response to what's been scouted of the enemy mid-battle. `#53` shipped the pool-funded research pipeline with the wrong round-gate and no revert capability; `#54` corrected both — #53, #54
- Weapon-Type Resistances (Plasma/Physical): a unit's weapon type stays fixed, but factions research damage-type-scoped offense/defense investments (reusing `StatUpgradeDefinition` — resistance is a negative-percentage stat) — this is what gives #54's swappable tech upgrades real strategic consequence. Dredge is the first Plasma-type weapon; everything else defaults Physical. `Health` gained a resistance-bypassing `Kill()` for effects meant to be absolute (sabotage self-destruct) — #55
- Splash Brushes for the Map Editor: an opt-in `MapBrushDefinition` property that scatters randomized-position/rotation/scale prop clusters per paint action instead of one prefab per click, with a scatter-radius preview gizmo — reuses the existing drag-paint loop unchanged. Designer-facing (Unity Editor tooling) only — #56
- Player Start Points for the Map Editor: painted "Player Start"/"Enemy Start" markers replace the old hand-typed `Vector3` Main Building spawn position — `EconomyManager` looks up each faction's marker at Economy Round start instead of reading a manual coordinate — #57
- Resource Node Selection Area Matches Its Scatter Radius: fixed a stale 0.5-radius root collider on all three resource node prefabs (real footprint is `scatterRadius`, 4) — `ResourceNode` now syncs its collider radius from `scatterRadius` every time `GenerateScatter()` runs. Also fixed `AttackCommand`'s redirect-to-node flow for free, same underlying collider — #58
- Biomass Depletion-Tier Art Cycling + Plant Props for the Map Editor: Biomass nodes now cycle every visible scatter instance together between Plant3/Plant2/Plant1 art as remaining yield crosses two tunable thresholds (layered on the existing #49 instance-count shrink), fully replacing `Prop_Tree` for Biomass; Plant1/2/3 also shipped as 6 new standalone Map Editor Props (plain + splash variants) — #59
- RPGW Decorative Props for the Map Editor: 3 new Props (Rock, Tree (Green), Bush) sliced from the newly-imported `RPGW_GL_v2.0` art pack's `decorative.png`, each with a plain + splash-cluster brush (6 new brushes total) — #62
- Splash brush drag-painting overlap fix: three layered causes (batch spacing not radius-aware, no minimum inter-instance spacing within a batch, and `RPGW_GL_v2.0`'s perspective-drawn art shearing under rotation) — Rock/Tree Green cluster brushes now disable `randomizeRotation` — #63
- 3D Asset Pipeline Validation Spike: confirmed feasibility of pivoting from flat 2D sprites to real 3D models (Mega Pack III materials converted to URP/Lit via the Render Pipeline Converter, first real scene light added) — GO decision; camera stays top-down orthographic for now, revisited once real content exists — #64
- 3D Roster Conversion, Player faction vertical slice: Main Building, Economic Building, Harvester, and Crewman converted to real 3D models (root+`Art`-child convention, identity rotation/scale); fixed two pre-existing bugs found along the way (non-uniform building root scale, dangling `VehicleAnimationDriver` references). Enemy/Alien faction stays 2D sprite art — no matching 3D alien asset exists yet — #65
- Harvester claw animation: first pass hand-rolled a claw-rotation script; corrected same day to instead gate the model's own real bundled `Animator`/`AnimationClip`, keyed off `HarvesterGatherBehavior.IsGathering` — #66, #67
- Harvester track motion fix: track mesh was being rotated (read as spinning/tumbling, not rolling) — switched to scrolling the tread texture's UV offset, since the mesh is a flat UV-tiled plate, not a wheel — #68
- Resource Node Crystal Art: scatter props replaced with real 3D crystal models (Mega Pack III), color-mapped per resource type, extending the tiered depletion-art system (biomass-only before this) to Ore and Gold — #69
- SpacePlatformKit Environment Art Integration: replaced the flat placeholder floor with real modular platform/structure art (Grey variant); the old `Ground` plane stays as an invisible NavMesh/raycast surface underneath; 4 floor tiles + 9 structure props registered as Map Editor brushes — #70
- Map Editor Grid Snap: opt-in grid-snapping (shared 8-unit lattice) for SpacePlatformKit brushes so modular pieces align edge-to-edge, with a live snap-preview gizmo — #71
- Platform Connector Brush: a dedicated bridge piece for the fixed gap between grid-snapped platform rows — #72
- Fixed a Map Editor category folder's own stray transform silently tilting every piece painted into it; category folders now force-reset to identity automatically — #73
- Map Editor erase scoped to the selected brush's own prefab type, so erasing no longer risks removing an unrelated overlapping piece — #74
- Top/Bottom wall brushes + flipped Left/Right wall and platform variants (8 new brushes) for fully enclosing rooms and varying floor tile orientation — #75
- Corrected the platform "Flipped" variant's rotation axis: Y-axis spin was a no-op (bilaterally symmetric top face); the real underside texture only reveals via a Z-axis 180° flip — #76, #77
- Fixed silent asset corruption from recreating 3 brush assets at freshly-deleted paths in the same Editor session — #78
- Tech debt: `AttackCommand`'s right-click dispatch chain refactored into a real `IRightClickOrderResolver` registry (7 order types, `Units/OrderResolvers/`) — zero behavior change — #60
- Tech debt: `EconomyManager`'s six accumulated responsibilities split into `EconomyRegistry` + `UnitUpkeepTracker` (plus a `FactionEconomyState.Deposit` move) — zero behavior change — #61

## Open (not yet implemented)

- Review Universal Sound FX library for more vehicle/weapon sound candidates beyond the current subset — not verified as done or not.
- **Hybrid economy sequence (design conversation 2026-08-24) — complete and synced into docs/GDD.md (2026-08-27).** All 5 parts shipped, dependency-ordered: Unit Upkeep (#48) → Area-based resource nodes (#49, + nearest-scatter-point targeting #50) → Auto-Extractor (#51) → Locked Resource Pool & Silos (#52) → Injection Round (#53, corrected by #54). Weapon-Type Resistances (#55) is what gives it real strategic payoff.
