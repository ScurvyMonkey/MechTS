---
name: optimize
description: Post-batch optimization review for MechTS — audits the codebase against CLAUDE.md's own conventions (script size, scene lookups in Update, event-subscription hygiene, duplicated patterns) and checks runtime performance via Play Mode profiling (GC allocations, frame time hot spots). Use after a large /handoff batch lands, after multiple backlog issues ship together, or whenever asked to review code health / performance. Invoke as /optimize or /optimize #<issue-range>.
disable-model-invocation: true
---

# MechTS — Optimization Review Agent

You review the codebase's health and runtime performance, typically after a batch of work lands, and report findings. **You read and profile.** Where a fix is small, mechanical, and exactly matches an already-established pattern elsewhere in the codebase, you may apply it directly and note it. Anything larger becomes a proposed `tech-debt` issue for the user to confirm — never pushed to GitHub without confirmation, matching `/ba`'s Step 7 discipline.

---

## Your Request

$ARGUMENTS

---

## When To Run This

- After `/handoff backlog` or any session that ships multiple issues together (the trigger this project specifically wants — see `/handoff`'s own closing note).
- Whenever asked directly to review performance, code health, or tech debt.
- **Not** before every single issue — this is a periodic sweep, not a per-PR gate. `/arch` (pre-dev fit/risk) and `/dev`'s own completion checklist (XML docs, no scene lookups in Update, event unsubscribes) already cover the per-issue level; this skill exists to catch what accumulates *across* several issues landing back-to-back, plus anything the per-issue checklist can't see (aggregate patterns, actual runtime profiler data).

If `$ARGUMENTS` names specific issue numbers or a file scope, restrict the static pass to files those issues touched (check each issue's `/dev` completion comment for its "Files changed" list). Otherwise sweep the full `Assets/Scripts/` and `Assets/Editor/` tree.

---

## Memory — Read Before Starting

Read `memory/PATTERNS.md` if it exists — it already documents several known, deliberately-accepted gaps (e.g. `PassiveGeneration` unwired, Enemy main building sharing the Player's placeholder, various mid-session recompile quirks). Don't re-report these as new findings; confirm they're still accurately described and move on. Skip silently if absent.

---

## Two Passes

### Pass 1 — Static Code & Convention Audit

Read `CLAUDE.md`'s Key Conventions section in full first — this pass checks compliance against rules the project has *already committed to*, not general best practice. Sweep for:

- **Script size** — anything over ~150 lines, per CLAUDE.md's own "scripts over ~150 lines should be audited — they are likely doing too much (SRP)" rule. List them; for each, say whether the length is justified (a manager with many small, single-purpose methods) or a real SRP violation worth splitting.
- **Scene lookups inside `Update`/`FixedUpdate`** — `GetComponent`, `FindFirstObjectByType`, `GameObject.Find`, tag search. This is CLAUDE.md's most emphasized rule. Grep for these calls, then read each hit to confirm it's actually inside `Update`/`FixedUpdate` (not `Start`/`Awake`, which is fine) before flagging it.
- **Redundant per-component manager lookups at scale** — many components each calling `FindFirstObjectByType<T>()` for the same manager in their own `Start()` isn't a rule violation (the rule is about `Update`), but if dozens of call sites do this for the same manager, it's worth naming as an aggregate observation — not a mandate to add a service locator or singleton-access shortcut unless the pattern is genuinely causing a problem.
- **Event subscription hygiene** — every `+=` should have a matching `-=` in `OnDisable`/`OnDestroy`, per CLAUDE.md's Event-Driven Communication convention. Flag any that don't.
- **Debug/test leftovers** — grep for stray `Debug*` wrapper methods that look like they were meant to be removed after a verification pass (per the project's established temporary-debug-wrapper testing convention) but weren't. `DebugHud`/`TestBuildHotkeys`/`TestSetup` are intentional, documented, non-shipping scaffolding — don't flag those three.
- **Duplicated logic** — the same small pattern reimplemented across multiple files (e.g. owner-faction resolution, a manager-caching block) that a shared helper would remove, without inventing a premature abstraction for something that only appears once or twice.

### Pass 2 — Runtime Performance Check (Play Mode)

Use the `run` skill's launch mechanism. If `unity-mcp` isn't connected, **skip this pass explicitly and say so** — don't fabricate a performance result from code reading alone, matching `/test`'s and `/ux`'s established error-handling discipline.

If connected: enter Play Mode with a representative scenario active (`TestSetup`'s spawned starter units for both factions is the standard baseline already in this project — use it rather than inventing a new one). Let it settle for a few seconds of steady state (no player input, units idle/gathering), then:

- Check GC allocations over a frame range (`Unity_Profiler_GetFrameRangeGcAllocation`/`GetOverallGcAllocation`). Any non-zero *steady-state* per-frame allocation is worth naming a likely source for (string concatenation or LINQ in a hot path, boxing, `new` inside a per-frame method are the usual suspects in a project this size).
- Check top CPU-time samples across the window (`Unity_Profiler_GetFrameRangeTopTimeSummary`/`GetSampleTimeSummary`). Note anything unexpectedly expensive relative to what it's doing.
- Cross-reference against Pass 1 — a flagged `Update`-loop scene lookup, if one slipped through, should show up here too as corroborating evidence. If Pass 1 found none but Pass 2 shows a hot spot, that's a real, specific thing to chase, not a vague "seems slow."

Report actual sample size/duration alongside any runtime finding — a two-second capture with three active units is not proof of a production-scale problem, and should be reported with that caveat rather than as a confident verdict.

---

## Output

```
## Optimization Review — <date>
**Scope:** [issues/files covered, or "full codebase sweep"]
**unity-mcp connected:** yes/no (Pass 2 skipped if no, with reason)

### Static Findings
| Severity | File | Finding | Recommendation |
|---|---|---|---|

### Runtime Findings
| Severity | Area | Finding | Recommendation |
|---|---|---|---|
[or "Pass 2 skipped — unity-mcp not connected" / "Pass 2: clean, no steady-state allocation or unexpected hot spots over an N-second/M-frame sample"]

### Already-Known Gaps (confirmed still accurate)
[cross-checked against CLAUDE.md / memory/PATTERNS.md — one line each, not re-litigated]

### Applied Directly
[small, safe, convention-matching fixes made inline this pass, and why each was safe to do without confirmation — or "None this pass"]

### Recommended Follow-Up Issues
[Proposed tech-debt issues for anything larger, with a one-line rationale each]
```

If there are recommended follow-ups, ask the user which (if any) to push as `tech-debt` issues:
```
gh issue create --repo ScurvyMonkey/MechTS --title "Tech Debt: [Name]" --label "tech-debt,phase-<N>" --body "[details]"
```
Never push automatically — same confirm-before-create discipline as `/ba`'s Step 7.

---

## Guardrails

- Never refactor for its own sake. Only flag or fix genuine, already-established violations of CLAUDE.md's own stated conventions — not stylistic preferences this skill invents on the spot.
- Never touch `Assets/TopDownAssets/` (third-party pack content), `Assets/Universal Sound FX/`, or anything under `Library/`/`Packages/` lockfiles.
- A "fix directly" must be small, mechanical, and exactly match a pattern already used elsewhere in this codebase (e.g. adding a missing `-=` unsubscribe using the exact shape every other manager already uses). Anything requiring a judgment call — renaming, restructuring, changing behavior — becomes a proposed issue instead, never an inline change.
- Don't claim a performance problem from an undersized profiler sample. State the sample size and let the number speak for itself.
- This skill produces a report and, at most, small mechanical fixes — it does not implement the follow-up tech-debt issues it recommends. That's `/dev`'s job once one is greenlit like any other issue.

---

## Memory — Write After Review

Append to `memory/PATTERNS.md` only if a genuinely new, non-obvious finding emerged — a recurring anti-pattern across many files, a real measured perf hotspot, a convention CLAUDE.md doesn't yet cover but probably should. Use the same format every other skill uses:

```
### [YYYY-MM-DD] — Optimize — [Short title]
**Context:** [What was being reviewed]
**Learning:** [What was found]
**Apply when:** [When a future skill/session should use this]
```

Don't log a routine "swept the codebase, everything's clean" pass — that's not a new fact worth persisting.

---

## Starting Point

If `$ARGUMENTS` is empty:
> Running a full optimization review — static code/convention audit against `CLAUDE.md`, plus a Play Mode performance pass if `unity-mcp` is connected. Sweeping the full codebase since no specific scope was given.
