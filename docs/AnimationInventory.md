# Animation Inventory (MechTS)

Full survey of every `AnimationClip` asset across `ScifiRTSSeriesMegaPackI/II/III` and `SpacePlatformKit` (85 clips total, confirmed via `AssetDatabase.FindAssets("t:AnimationClip", ...)` against the live project — not inferred from folder names), cross-referenced against which model each belongs to and whether that model is actually used anywhere in MechTS today. Supersedes the narrower pass from issues #91–#93, which only checked whether a prefab's `Animator` already had a `AnimatorController` assigned — it never looked at whether the *source* FBX carries more clips than what got wired up.

---

## Group 1 — Already wired, not yet gated in gameplay (issues #91–#93, arch-approved, not yet built)

These already have a real `Animator` + `AnimatorController` on the in-game prefab. The gap is purely code: nothing toggles `Animator.enabled` based on game state yet (the `HarvesterPartAnimator` pattern — see issue #66).

| Model (in-game unit/building) | Clip | Tracking issue |
|---|---|---|
| `BattleDroidBlue`/`Red` (Ranger) | `MinigunFire` | #91 |
| `LandScoutLvl1Blue`/`Red` (Reaper) | `LandScoutLvl1Firing` | #91 |
| `CannonTurretLvl1Blue`/`Red` (Machine Gun Turret) | `CannonTurretLvl1Fire` | #91 |
| `LandFactoryBlue`/`Red` (Barracks) | `LandFactoryProducing` | #92 |
| `AirFactoryBlue`/`Red` (Drone Factory) | `AirFactoryProducing` | #92 |
| `RadarLvl1Blue`/`Red` (Detection Building) | `RadarLvl1Rotate` | #93 |
| `ResourceExtractorBlue`/`Red` (Economic Building) | `ResourceExtractorWorking` | #93 |

`ResourceCollectorBlue`/`Red` (Harvester) is the one already shipped and gated (`HarvesterPartAnimator`, issues #66–68).

---

## Group 2 — New: real, currently-unused content on models already in the game

Found this pass, not previously known.

| Model (in-game unit/building) | Clip | What it needs |
|---|---|---|
| `BattleDroidBlue`/`Red` (Ranger) | `Dead` (in `BattleDroids.fbx`, separate from the standalone `MinigunFire.anim`) | Add a second state to the existing `MinigunBlue`/`MinigunFire` `AnimatorController` (or a new controller), triggered on death. Currently plays nothing on death. |
| `MechLvl1Blue`/`Red` (Dredge) | `MechLvl1Dead` | Dredge has **no `Animator` at all today** (confirmed) — this needs a new `Animator`+`AnimatorController` added from scratch, not just gating an existing one. |
| `Barricade`/`Hangar`/`HorizontalGate`/`TunnelDoor`/`TunnelGate` (all 5 `SpacePlatformKit` gate/door pieces) | Full `Open`/`Close`/`Opened`/`Closed`/`OpenCloseDEMO` set per piece (25 clips total) | These pieces aren't even wrapped as Map Editor brushes yet — issue #70 explicitly deferred them ("readme flags gates/doors as likely animated — needs its own investigation before wrapping"). **This investigation is that confirmation**: yes, real animation exists. Wrapping + wiring open/close (presumably gated on proximity or a trigger) is a real, self-contained follow-up. |

---

## Group 3 — Exists, but applying it means a bigger content decision, not a simple wire-up

| Model | Clip | Why it's not a simple wire-up |
|---|---|---|
| `Mine` (AutoExtractor) | `MineUpgradeCrusherActive` | Belongs to `MineUpgradeCrusher.fbx` — a distinct **upgrade-state mesh**, not the base `Mine.fbx` the current `MineBlue` prefab uses. Applying this means adding a visual upgrade-tier swap to the Auto-Extractor, not just gating an animation. |
| Crewman (`EngineerLvl3Static`) | `EngineerLvl3Working` (and `EngineerLvl1Working`/`EngineerLvl2Working`) | Belongs to the Engineer rig's **animated-arm** configuration (`EngineerArmElement1`/`2`). Crewman deliberately uses the **`EngineerArmStatic`** variant (no animated arm) — the "Static" in the prefab's own name is exactly this choice. Applying this means swapping Crewman to a different rig configuration, not gating existing content. |
| `RadarActive`, `ShieldGeneratorActive` (both under `BaseBuildings/DefenseBuildings`) | — | Confirmed via folder/model inspection: these belong to a **separate, currently-unused** standalone Radar/Shield defense-building model pair (`RadarLvl1.fbx`/`ShieldLvl1.fbf` under `DefenseBuildings`) — **not** the combined `RadarShieldLvl1` rig (`RadarShieldLvl1Radar.fbx`/`RadarShieldLvl1ShieldGenerator.fbx`) that `DetectionBuildingDefinition`/`TechnologyPlant` actually use today. Two different models that happen to share similar names. |

---

## Group 4 — Available, but for units/buildings not in the MechTS roster at all

Reference only — nothing to wire up unless/until one of these gets added as real content. All confirmed as real, complete clips, not placeholders.

- **Gunships** (Lvl1–3 fire animations) — no Gunship unit exists in MechTS
- **Naval** (Battleships/Destroyers/Frigates: Flak Turret, Turret Lvl1–3 fire) — no naval content at all
- **Artillery** (both the mobile unit version, `Artillery + Missile Launchers/`, and the stationary defense-building version, `BaseBuildings/DefenseBuildings/` — two distinct model sets, Lvl1–3 each) — no Artillery unit/building exists
- **Mobile Flak** (Lvl1–3 firing) — no Mobile Flak unit
- **Tanks** — Heavy/Medium/Light, Lvl1–3 each (9 fire clips) — no Tank unit (Dredge uses the Mech model, not Tank)
- **Land Fortress** (Flak/Heavy/Medium turret fire, Production, Radar Active) — no Land Fortress building
- **Shipyard** (`ShipyardProducing`) — no Shipyard building
- **Resource Lifter** (`ResourceLifterHarvesting`) — a second harvester-type unit MechTS doesn't use (Harvester already uses `ResourceCollector`)
- **Tactical Missile Defense** (fire) — no such building
- **Flak Turret** (building version, Lvl1–3), **Cannon Turret Lvl2/3**, **Radar Lvl2/3 Rotate**, **Mech Lvl2/Lvl3 Dead** — higher-tier variants of buildings/units MechTS currently only uses the Lvl1 version of

---

## Method

`AssetDatabase.FindAssets("t:AnimationClip", roots)` against `Assets/ScifiRTSSeriesMegaPackI/II/III` and `Assets/SpacePlatformKit`, grouped by containing file. Model attribution (which clip belongs to which actual in-game prefab, vs. a similarly-named-but-different model) was confirmed by cross-checking each ambiguous case's actual FBX folder contents and the specific model file each in-game prefab's `Art` child references — not assumed from clip/folder naming alone (the Radar/Shield case above is exactly the kind of naming trap that would have produced a wrong answer without this check).
