# CLAUDE.md — MechTS

## Project Overview

**MechTS** is a Unity 6 top-down Action RTS. The core structural idea (not yet fully specced — see Current Phase below): rather than the classic real-time-strategy demand of running economy and combat simultaneously under high APM, a match/mission is broken into distinct **Economy Rounds** (gather resources, build, produce/upgrade units) and **Battle Rounds** (deploy and fight with what the economy round produced). The project will ship a **Campaign** first; a **Versus** (PvP) mode using the same round structure is planned for a later phase, explicitly deferred.

Built with C# and Universal Render Pipeline (URP), 2D Renderer, Unity 6 (`6000.3.11f1`). The project imports the **TopDownAssets FuturisticPack** — a top-down sci-fi/mech asset pack (see [Asset Roster](#asset-roster-topdownassets) below) whose unit variety (harvester, APC, drones, turrets, mechs, power armor, aircraft, aliens-as-enemy-faction) is a large part of what suggested the RTS direction in the first place.

**Repo:** `ScurvyMonkey/MechTS` (GitHub — hosts both code and the Issues backlog)

---

## Current Phase: Phase 1 — Campaign (Pre-Production)

Nothing has been built yet — this is a freshly-imported Unity project (default sample scene, only the asset pack's own demo scripts). Phase 1's own sequencing:

- **Project scaffolding** — *in progress*. Repo/GitHub setup, `.claude/skills/` dev pipeline, this file, `docs/GDD.md`. Once this is done, everything else routes through `/ba` → `/arch` → `/dev` as normal.
- **Core loop design (Economy Round / Battle Round)** — *not yet specced.* The user has the concept (economy round feeds a battle round) but the actual mechanics — round length/pacing, what "economy" consists of (harvesting? a single resource or several? base-building?), how battle-round unit composition is locked in, win/loss conditions per round vs. per mission — are all open. **This is the first real `/ba` session to run**, before any gameplay code starts. Don't infer or invent these mechanics elsewhere in this repo; if a spec needs one of these answers and it isn't decided yet, that's a blocking open question for `/ba`, not a place to guess.
- Everything else in Phase 1 (unit roster, base/production systems, first campaign mission, UI, AI) is **not started** and depends on the core loop design above.

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

**Nothing below is implemented yet.** This section states the *anticipated* shape, carried over from proven patterns on the studio's other Unity project (see Key Conventions), so the first `/arch` review has a baseline to check against — not a locked design. Expect this section to be rewritten once the core loop is specced and the first manager-scaffolding issue actually ships.

### Anticipated Manager Hierarchy

All managers are singletons created by `Bootstrapper.cs` via `DontDestroyOnLoad`. **Do not create singletons outside this pattern** once it's established.

```
Bootstrapper
├── GameManager      — Game state machine (MainMenu, EconomyRound, BattleRound, MissionComplete, GameOver — exact states TBD by the core-loop spec)
├── EconomyManager    — Resource income/spend, production queue (shape TBD)
├── BattleManager     — Battle-round sequencing, win/loss condition (shape TBD)
├── UnitManager       — Registry of active units, spawn/selection handling
├── CameraManager     — Top-down RTS camera (pan/zoom, not a fixed arena frame)
└── UIManager         — HUD, round-transition screens
```

### Anticipated Folder Structure
```
Assets/Scripts/
├── Core/      — Bootstrapper, all Managers, interfaces, base classes
├── Units/     — UnitBase, per-unit-type behavior, UnitFactory, selection/command handling
├── Economy/   — Resource nodes/harvesting, production/build queue, EconomyRound flow
├── Battle/    — BattleRound flow, combat resolution
├── Campaign/  — Mission definitions, story/progression state
├── UI/        — HUD, round-transition screens, main menu
└── Utilities/ — Debug helpers, editor tools
```

Manager access pattern (once managers exist):
```csharp
// Cache in Awake — never call FindFirstObjectByType<T>() in Update
private EconomyManager _economyManager;
void Awake() { _economyManager = FindFirstObjectByType<EconomyManager>(); }
```

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
- **Never tint real art.** `SpriteRenderer.color` must stay white (`RGBA 1,1,1,1`) on any sprite that is real painted/rendered art.

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
- **`FuturisticPack/Sprites/AliensPack1/`, `AliensPack2/`** — a distinct faction, a natural campaign-enemy candidate.
- **`FuturisticPack/Sprites/Equipment/`** — `EnergyCell`, `FuelCanister`, `FuelTank`, `Light` — pickup/resource-prop candidates.
- **`FuturisticPack/Sprites/AdditionalWeapons/`** — weapon variants for the above.

---

## Unity MCP

Not yet confirmed set up for this project (the studio's other Unity project uses a `unity-mcp` bridge for the `run`/`test`/`ux` skills — `Unity_*` tools). If those tools aren't available when a skill needs them, say so explicitly and fall back to asking the user to check the Editor manually, rather than assuming the bridge is connected.

---

## Dependencies (UPM)

**Present:**
- Universal Render Pipeline (URP)
- Input System (new)
- AI Navigation (`com.unity.ai.navigation`) — likely relevant for unit pathing once movement/commands exist
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

**First real session should be `/ba` on the Economy Round / Battle Round core loop** — everything else in Phase 1 depends on it.

---

## Documentation References
- Unity Manual: https://docs.unity3d.com/Manual/UnityManual.html
- Scripting API: https://docs.unity3d.com/ScriptReference/index.html
- Design doc: `docs/GDD.md`
