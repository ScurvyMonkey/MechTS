---
name: ux
description: Visual/usability review for MechTS changes tied to a GitHub issue — checks the rendered game against the GDD and basic usability heuristics, plus a static pass over code conventions. Invoke as /ux #<issue>.
---

# MechTS — UX & Identity Review Agent

When invoked with `/ux #N`, check the changes made for issue #N against usability conventions and, once established, visual identity. Look at both the rendered game and the code diff, then emit a machine-parseable result for `/handoff`. You review. You do NOT write app code or modify specs.

---

## Your Request

$ARGUMENTS

---

## Repo

`ScurvyMonkey/MechTS`

---

## Where This Skill Is Thin Right Now

MechTS has no defined visual identity yet, no reference layout, and no confirmed `unity-mcp` bridge connection for this project (see `CLAUDE.md`'s Unity MCP section). This skill currently reviews **structure and usability against whatever `docs/GDD.md` has actually decided**, not brand fit or a fixed reference composition — there isn't one yet. Expand this skill's checklist the same way `CLAUDE.md`'s conventions section grows, once real decisions exist to check against.

---

## Review Method

Two passes:
1. **Visual pass** — enter Play Mode, capture the affected scene, judge against whatever `docs/GDD.md` has actually decided plus general usability heuristics. Skip this pass entirely (see Step 1) if `unity-mcp` isn't connected for this project — don't fabricate a visual review from code alone.
2. **Static pass** — check the code diff against the conventions checklist.

---

## App Launch

Use the `run` skill's mechanism if `unity-mcp` (`Unity_*` tools) is available — don't reinvent it. If it isn't available, skip the visual pass and say so explicitly. Save any screenshots to the session scratchpad, never commit them.

---

## Step 1 — Determine Affected Surface

```
gh issue view <N> --repo ScurvyMonkey/MechTS
```

Read the "Affected Files" section from the `/ba` spec and the "Files changed" list from `/dev`'s completion comment — don't infer scope from a raw `git diff`, since the working tree may carry unrelated in-progress changes.

| File is under… | Treat as |
|---|---|
| `Assets/Scripts/Units/`, `Economy/`, `Battle/` (with a prefab/visual component) | Direct visual surface — capture the scene |
| `Assets/Scripts/UI/` | Direct visual surface — capture the relevant HUD/screen |
| `Assets/Scripts/Core/`, `Campaign/` (manager/state logic only, no new visuals) | Indirect — capture the scene anyway if round timing/state behavior changed, since that's observable even without new sprites |
| `.claude/skills/*.md`, `CLAUDE.md`, `docs/*.md` | No visual surface |

**If no visual surface, or `unity-mcp` isn't connected:** skip to Step 3 (static pass), then emit `UX RESULT: PASS (skipped — no visual surface changed)` or `UX RESULT: PASS (skipped — unity-mcp not connected)` as applicable, and go to Step 4.

---

## Step 2 — Visual Pass

1. Enter Play Mode via the `run` skill.
2. If the issue describes a specific interaction (a round transition, a unit being produced, a battle starting), drive it before capturing — an idle scene doesn't show the actual change.
3. Capture via `Unity_SceneView_Capture2DScene` (this is a 2D top-down project).
4. Judge against:
   - **Structural fit to whatever `docs/GDD.md` has actually decided** for the system involved — don't judge against an invented reference.
   - **Readability** — can you tell friendly units from enemy units from resources from environment at a glance? Is anything invisible, a magenta/missing-material placeholder, or overlapping unreadably?
   - **Usability heuristics** — is there feedback when a round transitions, a unit is produced, or combat resolves? Is the HUD (once it exists) legible against the background?

---

## Step 3 — Static Pass

Read the changed files and check against `CLAUDE.md`:

- [ ] Tunable values are in a ScriptableObject, not inline magic numbers
- [ ] No scene lookups inside `Update`/`FixedUpdate`
- [ ] New manager (if any) only instantiated via `Bootstrapper`
- [ ] Nothing here belongs to Phase 2 (Versus/PvP) per CLAUDE.md's Roadmap
- [ ] Nothing here assumes a core-loop mechanic that `docs/GDD.md` doesn't actually list as decided
- [ ] New UI elements don't hardcode strings that should obviously be data-driven later

---

## Step 4 — Emit Result

Post a comment:
```
gh issue comment <N> --repo ScurvyMonkey/MechTS --body "<UX Review body>"
```

Body format:
```
## UX Review
**Verdict:** PASS | FAIL
**Screens reviewed:** [or "none — no visual surface changed" / "none — unity-mcp not connected"]

### Findings
- [specific issue, or "None" if PASS]
```

Then end your own output with exactly one of:

**PASS:**
```
UX RESULT: PASS
```

**FAIL:**
```
UX RESULT: FAIL
DETAILS:
[specific findings — file/line or screen, and what's wrong]
```

---

## Output Contract (normative)

- `UX RESULT:` must appear verbatim, on its own line, near the end of output.
- No `UX RESULT:` line found → `/handoff` treats it as FAIL and escalates: `"/ux did not emit a parseable result — escalating for manual review."`
- No `FAILURE TYPE` distinction — every `/ux` FAIL follows the same retry path as a feature test failure.

---

## Error Handling

- `unity-mcp` not connected or Play Mode fails to launch → skip the visual pass per Step 1, don't fail the whole review over tooling that was never confirmed present for this project.
- A screen/interaction can't be reached or driven despite a live bridge → report it as a finding with specifics, don't crash the whole review.
- `gh` not authenticated → still perform the review and emit the result; note in chat that the comment couldn't be posted.
