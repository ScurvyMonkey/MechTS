# MechTS — Game Design Document

*This document is a starting stub, not a finished design. MechTS's core loop has not been fully specced yet — see `CLAUDE.md`'s Current Phase section. Treat this file as the running design source of truth once `/ba` sessions start filling it in; until then it records only what's actually been decided.*

---

## Overview

MechTS is a top-down **Action RTS**. The founding idea, in the designer's own words:

> An RTS that focuses on Economy first, then Battle — an Economy Round, then a Battle Round utilizing that economy. Breaking away from the mad, crazy APM requirements of most RTS games by putting that pressure into single rounds instead of continuous real-time simultaneity.

The game ships as a **Campaign** first. A **Versus** (PvP) mode reusing the same round structure is planned but explicitly deferred — "phase 2 and way down the road."

The project's asset base (`Assets/TopDownAssets/` — see `CLAUDE.md`'s Asset Roster) is what surfaced the RTS direction in the first place: a Harvester unit, APCs, drones, turrets, mechs, power armor, and a distinct alien faction all read as RTS/economy-and-combat pieces rather than a straight arena shooter roster.

---

## What's Actually Decided

- Genre: Action RTS, top-down, real-time.
- Structural loop: alternating **Economy Round** and **Battle Round** — mechanics of each round, and how they connect, are **not yet decided**.
- Delivery order: Campaign (single-player, story-driven) first; Versus (PvP) second, much later.
- Engine/tech: Unity 6, URP 2D, TopDownAssets FuturisticPack as the primary art source.

## What's Not Decided Yet (first `/ba` agenda)

- What "Economy Round" actually consists of — resource gathering (via the Harvester?), base-building, production queues, tech/upgrades, or some combination.
- How long a round lasts, and what ends it (a timer, a threshold, a player-declared "ready").
- How the Battle Round consumes what the Economy Round produced — pre-built armies deployed at battle start? Reinforcements during battle? Persistent bases fought over directly?
- Win/loss conditions — per round, per mission, or both.
- Player agency during the Battle Round itself — is it still real-time strategy control (issuing orders), or does "Action" in Action RTS mean more direct, per-unit control (closer to the pack's action-shooter roots)?
- Campaign structure — mission count, story/setting, how missions chain (a map/hub, like the studio's other project's arena-to-arena flow, or a simpler linear mission select).
- Unit roster — which of the asset pack's candidates (Mechs, Power Armor infantry, vehicles, aircraft, turrets) become actual playable/buildable unit types, and what differentiates them mechanically.
- Faction structure — is the Aliens pack the campaign's enemy faction, a playable faction, or both (relevant for Versus later)?

None of the above should be inferred or built around speculatively — run `/ba` on the core loop before any gameplay system spec is drafted.

---

## Roadmap

See `CLAUDE.md`'s Roadmap section (Phase 1 — Campaign, Phase 2 — Versus) for the phase breakdown. No further phase detail exists yet; it will be filled in as Phase 1's core-loop design lands.

---

## Reference: Asset Roster

See `CLAUDE.md`'s [Asset Roster](../CLAUDE.md#asset-roster-topdownassets) section — kept in one place to avoid drift between the two documents.
