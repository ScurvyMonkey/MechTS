# MechTS — Game Design Document

*This document is the running design source of truth, kept in sync with confirmed `/ba` decisions. The Economy Round / Battle Round core loop is fully specced across GitHub issues #1–6 (arch-approved 2026-08-17); the hybrid economy layer (upkeep, area-based nodes, Auto-Extractor, locked pool/Silos, Battle-Round tech injection) is specced across issues #48–54 (shipped 2026-08-27) — see "What's Actually Decided" below. Content beyond these (unit roster expansion, campaign/mission structure, enemy AI behavior, weapon-type resistances) remains open; see "What's Not Decided Yet." Last synced against the shipped codebase 2026-08-27.

---

## Overview

MechTS is a top-down **Action RTS**. The founding idea, in the designer's own words:

> An RTS that focuses on Economy first, then Battle — an Economy Round, then a Battle Round utilizing that economy. Breaking away from the mad, crazy APM requirements of most RTS games by putting that pressure into single rounds instead of continuous real-time simultaneity.

The game ships with a full campaign, multiplayer, and a co-op mode. In co-op mode one player plays the general and the other player plays the commander. Commander is in charge of economy, General is in charge of army.

The project's asset base (`Assets/TopDownAssets/` — see `CLAUDE.md`'s Asset Roster) is what surfaced the RTS direction in the first place: a Harvester unit, APCs, drones, turrets, mechs, power armor, and a distinct alien faction all read as RTS/economy-and-combat pieces rather than a straight arena shooter roster.

---

## What's Actually Decided

- Genre: Action RTS, top-down, real-time.
- Structural loop: alternating **Economy Round** and **Battle Round**.
- Delivery order: Campaign (single-player, story-driven) first; Versus (PvP) and co-op (Commander/General split) are documented future vision but out of Phase 1 build scope — see `CLAUDE.md`'s Roadmap.
- Engine/tech: Unity 6, URP 2D, TopDownAssets FuturisticPack as the primary art source.
- **Economy Round contents** (issues #2, #3): harvester-driven gathering of three resources (Ore/Etherium, Biomass, Gold) from finite/depleting map nodes; a build system for economic/power-infrastructure buildings funded by those resources; a power/capacity system gating what can be built. The round's only combat-adjacent action is **espionage** — a directly-controlled Saboteur unit that sabotages enemy harvesters/nodes/economic buildings (never the enemy main building); no direct base assault is possible in this round.
- **Round length**: Economy Round is a fixed 15-minute timer (tunable). Battle Round has **no timer** — it ends only when a victory condition resolves.
- **How Battle Round consumes Economy Round output**: no new harvesting or economic/infrastructure building placement once Battle Round begins, but unit production continues, funded by whatever resource stockpile is left over. Additionally, a faction's **locked resource pool** (see Hybrid Economy Mechanics below) can be spent during Battle Round specifically — via the Injection panel — to research a new tech upgrade or revert an already-researched swappable upgrade and replace it with an alternative, funded independently of the normal stockpile.
- **Hybrid economy mechanics** (issues #48–54, 2026-08-24 to 2026-08-27): every unit has an ongoing upkeep cost (Ore/Biomass/Gold per minute), continuously draining its owning faction's stockpile during the Economy Round — a faction can legitimately go negative as the cost of an all-in strategy, but this alone never counts as elimination (a faction with an active army is never economy-eliminated, regardless of stockpile sign). Resource nodes are area-based: a scattered cluster of many small objects sharing one depleting pool, not a single sprite with depletion stages. Two ways to extract from a node: a Harvester (round-trip to a main building to deposit), or an **Auto-Extractor** building placed directly within a node's radius that passively extracts without any travel. A player can allocate a live-adjustable percentage of incoming resources (per type) into a **locked pool**, bounded by capacity from **Silo** buildings (one resource type per Silo) — this pool survives into Battle Round and is spent there via the Injection mechanic described above; nothing routed to a full/uncapacitated pool is ever lost, it simply falls back to the normal stockpile.
- **Win/loss conditions** (issue #6): a faction is eliminated when it has no buildings AND no units (true wipeout), or no economy (stockpile empty and no production buildings left). Surrender and map-objective-met are supported as explicit triggers for future AI/mission systems to call into. Simultaneous mutual defeat breaks the tie toward whichever faction earned more total resources over the mission.
- **Player agency during Battle Round**: direct real-time RTS unit control — click/drag-box selection, right-click move and attack orders, attack-move, control-group hotkeys, NavMesh-based (A\*) pathing. Same control model applies to any directly-controlled unit in either round (e.g. the Saboteur).
- **Campaign structure (partial)**: missions chain through a hub/mission-select map — completed missions are replayable, the next mission can be selected from there. Mission count and story content are still open (see below).
- **Faction structure**: two Phase 1 campaign factions — Humans (player) and Aliens (AI enemy). The Aliens run their own full parallel economy and combat presence (not a scripted static force) — though the actual AI decision-making logic behind that is still open.
- **CameraManager** (issue #10, shipped 2026-08-19): edge-of-screen and arrow-key pan, scroll-wheel zoom, both clamped to map bounds; SC2-style saved camera locations (`F1`-`F4` jump, `Ctrl+F1`-`F4` save). See `CLAUDE.md`'s Architecture section for the full shipped shape.
- **Technology/upgrade trees** (issues #13–15, spec'd 2026-08-19): a tiered, per-faction system — Humans and Aliens each get their own distinct tree, "crafted and tuned to focus on the factions' unique playstyles." Research is instant (no queue/timer) once affordable and prerequisites are met, and resets each mission (not persisted across the campaign — no save/meta-progression system exists yet). Upgrades are extensible by design: a `Gun → Gun Level 1 → Gun Level 2` stat-tier chain is the baseline example, plus upgrades can unlock specific units/buildings that are otherwise unavailable (in addition to, not instead of, the existing building→producible-units gating). **Correction (issue #54, 2026-08-27):** research is no longer Economy-Round-exclusive — a normal stockpile-funded upgrade is still only researchable during the Economy Round, but a faction can *also* research a new upgrade (or revert an already-researched, explicitly swappable one and replace it) during the Battle Round specifically, funded from its locked resource pool instead of the stockpile. See Hybrid Economy Mechanics above and Battle Round Mechanics below.

## What's Not Decided Yet

- **Unit roster (partially decided)** — Harvester (economy), the single generic Saboteur (espionage), Crewman (repair/construction), and three combat types (Ranger, Reaper, Dredge — Player faction only so far) are now real, buildable roster entries (issues #2, #3, #42, #43). Still open: further roster expansion beyond these, Mechs/Power Armor as distinct playable types beyond Dredge's `Mech1` art, and distinct Spy/Engineer/Terrorist roles (vs. today's one generic Saboteur).
- **Campaign mission structure** — mission count, story/setting, and actual content for each of the Game Mechanics section's Game Types (Defensive, Attack, Escort, Espionage, Support, Reinforce).
- **Enemy AI behavior** — how the Aliens faction actually decides what to harvest/build (economy), where to send saboteurs (espionage), and when to attack or surrender (battle). The systems all support this; the decision-making itself doesn't exist yet.
- **Actual tech tree content/balance** — the system (issues #13–15, extended #53–54) supports per-faction, tiered, extensible upgrades, including locked-pool-funded research and revertible/swappable choices during Battle Round; the specific upgrades in each faction's tree (beyond a small Damage I/II proof-of-concept) aren't designed yet.
- **Weapon-type resistances** — referenced 2026-08-27 as the intended future use case for swappable tech (e.g. an enemy composition resistant to Plasma-type damage, prompting a Battle-Round pivot to a Physical-type weapon upgrade instead). No per-weapon-type damage or resistance mechanic exists yet — today's `Weapon`/`Health` model is a single damage number with no type. This is a real system to design, not just content to author.
- **Multiplayer/co-op mechanics** — explicitly Phase 2, not touched.

None of the above should be inferred or built around speculatively — run `/ba` before any spec drafts against them.
## Game Mechanics

---
- Game Length: ~30 mins
- Victory Conditions: Enemy has no more buildings, enemy has no more units, enemy has no more economy, enemy has surrendered, map objective has been met. If all are a draw, then tie goes to the highest earning economy.
- Game Types: Defensive, Attack, Escort, Espionage, Support, Reinforce
- Players: Single player, Co-op Versus, PvP
- General Gameplay Loop: Player logs onto the game, they choose their faction, hit play campaign, game saved from their last play time, they choose to continue, game loads into the map, and the player plays the game until victory condtions are met.

---

## Economy Round Mechanics
---
- In the economy round the player will focus on building out the rounds economy to be used during the battle round. The main focus for this player is to build out harversters, set up power and building infrastructure, perform espionage attacks on the opposing player to impede their economy round. There is no all out attacking, rather the use of spies and engineers, terrorist etc to cause economical damage to the opposing player. This round will consist of 15 minutes, and at time end the game will transition to the battle round where the player will NOT be able to build out further economical buildings or have any impact on the economy. They are simply in charge of the army now and can build units to complete the mission. 
- Economy will be made up of 3 important sources: Ore (Etherium), Biomass(Trees,Oil,Plants), Gold (Money)
- Economist players will have 15 minutes to build an economy to support the upcoming battle. They will need to build harversters to gather Ore and Biomass, Gold to create buildings and fund the battle, as well as build all strucutres needed.
- Economy rounds will start with a number of resources at the beginning
- Economy rounds starts with the basic main building
- **Hybrid economy layer (issues #48–52, added 2026-08-24 to 2026-08-27):** every unit costs ongoing upkeep, continuously draining the stockpile — an army that overbuilds can legitimately run its economy negative, a deliberate all-in risk/reward tradeoff. Resource nodes are area-based scattered clusters that deplete as a shared pool. An Auto-Extractor building can be placed directly on a node to harvest it passively, without a Harvester. The player can also set aside a percentage of incoming resources (per type) into a locked pool, capped by Silo building capacity — this pool doesn't fund normal Economy Round spending; it's held in reserve specifically for the Battle Round Injection mechanic (see Battle Round Mechanics).
---

## Battle Round Mechanics
--- 
- In the battle round players will now focus on utilizing the economy that was built to begin building out thier forces. For the upcoming batttle. In this round the main focus is simply build the army you planned in the economy round and take action to secure the victory.
- Units will have the standard move related to RTS. Alpha * pathing, point and click movement with standard hot keys for micro control
- **Injection mechanic (issues #53–54, added 2026-08-27):** a faction's locked resource pool (set aside during the Economy Round) can be spent here, mid-battle, via a dedicated panel — either to research a new tech upgrade, or to revert an already-researched, explicitly swappable upgrade (free — the original research spend is a pure sunk cost) and inject a different one instead. This is the mechanic behind "the Economist and General work together to plan": having scouted the enemy's actual composition once the battle is underway, the player can pivot a tech choice that turns out to be wrong (e.g. a weapon type the enemy resists) without having had to predict it during the Economy Round. In Phase 1 (single-player) this is the same player making both calls; the eventual Phase 2 co-op split (Commander/General) is designed around this same mechanic, not built yet.




## Technology Upgrade Trees
---
*Specced 2026-08-19 across issues #13 (foundation), #14 (stat upgrades wired into gameplay), #15 (unit/building unlock-gating); extended 2026-08-27 by issues #53–54 — see "What's Actually Decided" above for the confirmed shape.*
- Each faction will have upgrade trees tied to their factions and crafted and tuned to focus on the factions unique playstyles.
- Tech tree will include ways for players to invest resources to gain access to further tech. Examples being Gun --> Gun Level 1 --> Gun Level 2. Each level representing an increse to base stats.
- Normal, stockpile-funded research is only accessible in the Economy Round. A separate, locked-pool-funded path (research a new upgrade, or revert an already-researched swappable one for a different upgrade) is accessible only in the Battle Round instead — see Battle Round Mechanics. The two funding sources are independent; a faction's normal stockpile and locked pool are tracked separately.
- No swappable (revertible) upgrade content has actually been authored yet — the mechanic exists, but today's proof-of-concept tree (Damage I/Damage II) isn't marked swappable. Real swappable content (e.g. mutually-exclusive weapon-type choices) is still open — see "What's Not Decided Yet."



## Roadmap

See `CLAUDE.md`'s Roadmap section (Phase 1 — Campaign, Phase 2 — Versus) for the phase breakdown. No further phase detail exists yet; it will be filled in as Phase 1's core-loop design lands.

---

## Reference: Asset Roster

See `CLAUDE.md`'s [Asset Roster](../CLAUDE.md#asset-roster-topdownassets) section — kept in one place to avoid drift between the two documents.
