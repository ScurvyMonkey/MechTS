---
name: test
description: Runs MechTS's Unity Test Framework suite for a given issue and emits a machine-parseable PASS/FAIL result for /handoff. Invoke as /test #<issue>.
---

# MechTS — Test Agent

When invoked with `/test #N`, run the automated test suite, determine which systems issue #N touched, classify failures as feature vs. regression, and emit a machine-parseable result for `/handoff`. You run tests. You do NOT write app code or modify specs.

---

## Your Request

$ARGUMENTS

---

## Repo

`ScurvyMonkey/MechTS`

---

## Test Infrastructure

Unity Test Framework (`com.unity.test-framework`) is installed but has never been exercised on this project — no test assemblies exist yet, and it's not yet confirmed whether a `unity-mcp` bridge is set up for MechTS the way it is for the studio's other Unity project (see `CLAUDE.md`'s Unity MCP section).

Expected test locations once they exist: `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/`, each with their own assembly definition referencing `UnityEngine.TestRunner`/`UnityEditor.TestRunner`.

If a `unity-mcp` bridge is available (`Unity_*` tools present), use it exactly as documented below. If it is not available, skip straight to the manual fallback and say so explicitly — don't guess at bridge tooling that may not be connected for this project.

---

## Step 1 — Check Whether Tests Exist

Search for any `*.asmdef` under `Assets/Tests/` (or any file referencing `[Test]`/`[UnityTest]`) via Glob/Grep, or `Unity_FindProjectAssets`/`Unity_Grep` if the bridge is available.

**If no tests exist yet:**
```
TEST RESULT: PASS
```
But say explicitly in your own chat output (not the machine-parsed block): `⚠️ No automated tests exist yet for MechTS — this is a pass-through, not a real verification. Manual Play Mode check via /run is the only current safety net.` `/handoff` will treat this as PASS and proceed, carrying your warning into its own notification.

## Step 2 — Identify Changed Systems

Read the issue (`gh issue view <N> --repo ScurvyMonkey/MechTS`) and its `/dev` completion comment for the "Files changed" list. Map changed files to systems (Core, Units, Economy, Battle, Campaign, UI) by folder per `CLAUDE.md`'s anticipated folder structure — once it's real.

## Step 3 — Run Tests

**Primary mechanism (if `unity-mcp` is connected):** via `Unity_RunCommand`, execute a `CommandScript` that calls `UnityEditor.TestTools.TestRunner.Api.TestRunnerApi`, filtered to `TestMode.EditMode` and `TestMode.PlayMode`. `TestRunnerApi.Execute` is callback-driven — you may need one `Unity_RunCommand` call to kick it off and a short follow-up read (console via `Unity_ReadConsole`, or a results file) once it completes.

**Manual fallback**, if the bridge isn't available or the above doesn't produce reliable results: ask the user to open **Window → General → Test Runner** in the Unity Editor, run all tests, and report back pass/fail counts and failure messages. Do not fabricate a result if you can't get real data — an unverifiable `/test` run should escalate as FAIL/regression per the Output Contract, not guess PASS.

## Step 4 — Classify Failures

Per failed test: if its system is in the changed-systems list from Step 2 → **feature failure**. Otherwise → **regression**. If both occur, emit `FAILURE TYPE: regression` (regressions take precedence).

## Step 5 — Emit Result

**All tests pass:**
```
TEST RESULT: PASS
```

**Any failure:**
```
TEST RESULT: FAIL
FAILURE TYPE: feature | regression
FAILED MODULES: [comma-separated system names, e.g. "Economy,Units"]
DETAILS:
[test names, error messages, assertion failures]
```

---

## Output Contract (normative)

- `TEST RESULT:` must appear verbatim, on its own line, at or near the end of output.
- `FAILURE TYPE:` must be exactly `feature` or `regression`.
- No `TEST RESULT:` line (e.g. the runner crashed or couldn't be reached) → `/handoff` treats it as regression and escalates.

---

## Error Handling

- `Unity_RunCommand` fails to compile/execute the test-runner script → `TEST RESULT: FAIL` / `FAILURE TYPE: regression` / `DETAILS: [error]` — don't guess a PASS.
- Unity Editor not reachable via `unity-mcp`, or the bridge isn't set up for this project → fall back to the manual path and say so; don't block the pipeline on tooling that was never confirmed present.
- `gh` not authenticated → still run tests and emit a result; issue reading is best-effort.

---

## Notes for the Developer

- When MechTS's first real tests get written, and once the `unity-mcp` bridge (if any) is confirmed working for this project, update this skill's caveats and record the working pattern in `memory/PATTERNS.md`.
- Keep spec/test files independently runnable per system, matching the folder-to-system mapping above, so `FAILED MODULES` stays accurate without manual remapping later.
