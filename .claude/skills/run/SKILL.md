---
name: run
description: Launch and verify MechTS in the Unity Editor via the unity-mcp bridge — enters Play Mode, captures the scene view, and reads the Editor console for errors/warnings. Use this whenever asked to run, test, or verify a change "in the game", "in Unity", or "in Play Mode", or to screenshot the current scene. Always use this instead of asking the user to manually press Play and check for you.
---

# Run — MechTS (Unity MCP)

MechTS is intended to be verified through a `unity-mcp` bridge (`Unity_*` tools), the same way the studio's other Unity project works — but **this has not yet been confirmed set up for MechTS specifically**. Check for the `Unity_*` tools before assuming they're connected; if they aren't, tell the user to set up the bridge (or open the project in Unity and check manually) rather than guessing at results.

## Steps

1. **Confirm the bridge is live.** Call `Unity_ManageEditor` with `Action: GetState`. If this errors, times out, or the tool isn't available at all, the bridge isn't connected for this project — tell the user, and either ask them to check manually or ask them to confirm `unity-mcp` should be set up here too. Don't guess at scene state without this.

2. **Enter Play Mode.** Call `Unity_ManageEditor` with `Action: Play`, `WaitForCompletion: true`.

3. **Capture what's happening.** MechTS is a **2D top-down** project — prefer `Unity_SceneView_Capture2DScene` (orthographic top-down capture of a world-space rect: `worldX`, `worldY`, `worldWidth`, `worldHeight`) so you get the scene as the player actually sees it. Center the capture on whatever's actually relevant (there's no fixed "arena" concept in this project yet — check the current scene's actual content/bounds rather than guessing a rect). Fall back to `Unity_Camera_Capture` (no `cameraInstanceID`) only if the 2D capture doesn't show what's needed (e.g. checking a UI canvas).

4. **Check the console.** Call `Unity_ReadConsole` with `Action: Get`, `Types: ["Error", "Warning"]`. Report any errors verbatim with file/line if present — don't paraphrase away stack traces the user might need.

5. **Report back**: what you did, the captured image, and a summary of console errors/warnings (or confirmation there were none). If the user asked you to verify a specific behavior, say explicitly whether you observed it or couldn't tell from a screenshot alone.

6. **Leave Play Mode running** unless the user asked for a one-shot check or explicitly wants it stopped — they may want to keep interacting. To stop, call `Unity_ManageEditor` with `Action: Stop`.

## Notes

- Prefer `Unity_ReadConsole` over `Unity_GetConsoleLogs` for error checks — it supports filtering by type and time, so you can scope to what happened during this run rather than the whole session's history.
- If you changed a script right before running, remember Unity recompiles on focus/external change — a stale error in the console may predate your fix. Check timestamps if unsure.
- This skill assumes one Unity Editor instance has MechTS open. If multiple Unity projects are open at once, `Unity_ManageEditor GetState` and friends operate on whichever editor the `unity-mcp` relay is currently bridged to — if project data looks wrong (e.g. wrong scene names), the wrong project may have focus.
- The `/ux` skill has its own, more structured visual review process built on top of this same capture mechanism — use `/ux #<issue>` instead of this skill when the task is a formal pre-ship design review, not just an ad-hoc check.
