# Terrain Tools Cheat Sheet (MechTS)

Reference for `com.unity.terrain-tools` **5.3.3** (installed 2026-09-08, compatible with Unity 6000.3.11f1 — the package requires 2023.1+). Covers the package itself plus the bundled `Assets/TerrainSampleAssets` sample pack.

> **Before you sculpt: read the "MechTS-specific notes" section at the bottom first.** This project has an existing terrain/elevation setup (`OutdoorTerrain`, `Ground`, the Map Editor's Platform/Ramp system) that Terrain Tools sits alongside, not on top of.

---

## Where the tools live

Select the `OutdoorTerrain` GameObject → its **Terrain** component has a row of icons at the top of the Inspector:

`Create Neighbor Terrains | Paint Terrain | Paint Texture | Paint Trees (Details) | Terrain Settings`

**"Paint Terrain"** is the one that matters most — click its dropdown and every sculpting/erosion/transform/stamp tool below lives there. Terrain Tools adds **14 new tools** to that same dropdown alongside Unity's native ones (Raise/Lower, Smooth, Set Height, Flatten).

A separate standalone window, the **Terrain Toolbox**, is at `Window > Terrain > Terrain Toolbox`.

---

## Universal brush hotkeys (work in every sculpt/paint tool)

| Key | Effect |
|---|---|
| `A` | Adjust brush strength/opacity (drag) |
| `S` | Adjust brush size (drag) |
| `D` | Adjust brush rotation (drag) |
| `F` | Focus Scene view on the cursor's terrain position |
| `Ctrl` (hold) | Inverts the brush effect (e.g. lower instead of raise) |
| `Shift` (hold) | Temporarily switches to the Smooth brush |
| `Shift + A` | Terrain Layer eyedropper — sample a layer already on the terrain |

Every brush also exposes **Spacing** (distance between stamps while dragging) and **Scatter** (random jitter along the stroke) — useful for a natural, non-repetitive look when dragging a brush across a large area.

---

## Sculpting tools (shape height)

| Tool | What it does |
|---|---|
| **Raise/Lower** *(native)* | Basic add/subtract height. |
| **Set Height** *(native)* | Flattens to an exact absolute height. |
| **Smooth Height** *(native)* | Averages height with neighbors. |
| **Stamp Terrain** | See below — the main tool for this project's use case. |
| **Bridge** | Click two points; builds a land bridge/ridge connecting them. |
| **Clone** | Copies a region of existing terrain height and pastes it elsewhere. |
| **Noise** | Applies procedural noise (multiple noise/fractal types) to height — good for breaking up flat ground. |
| **Terrace** | Converts a slope into a series of flat steps. |

### Stamp Terrain — the key tool for "make it stampable"

This is the tool that turns either a **grayscale brush texture** or a **3D mesh** into a one-click height feature.

- **Stamp Mode**: `Brush` (a texture-based heightmap stamp — this is what the 42 sample-pack stamps under `Assets/TerrainSampleAssets/TerrainBrushes` are) or `Mesh` (drop in *any* `Mesh` asset and it imprints that shape into the heightmap).
- **Behavior**: `Set` (ignores existing height, stamps as-is), `Min` (only lowers), `Max` (only raises).
- **Stamp Height**: height of the stamp relative to the cursor, range **-600 to 600** (default 100) — scroll the mouse wheel while the tool is active to adjust live. Negative values carve a depression (crater, canyon) instead of raising a hill.
- **Blend Amount**: `0` = stamp fully overrides existing terrain under it; `1` = existing terrain is fully preserved (stamp blends in softly). This is your "how much does this feature respect what's already there" dial.
- **Mesh mode extras**: pick the `Mesh` asset, then scale/rotate it right in the Scene view — hold `C` while dragging the gizmo handles for interactive adjustment.
- **Mesh shape rule of thumb**: convex, single-surface shapes (a hill, a ridge, a dome, a crater bowl) work great. Meshes with holes, overhangs, or deep concave detail (an arch, a cave mouth) don't translate well — a heightmap can only store one height value per XZ position, so anything "under" another part of the same mesh gets silently flattened/ignored.

**This is the tool your new "detail meshes" should target** if they're meant to be stamped into terrain — see the Open Question in the accompanying spec about exactly what they're for.

---

## Erosion tools (simulate natural weathering)

| Tool | What it does |
|---|---|
| **Hydraulic Erosion** | Simulates water flowing over terrain and carrying sediment — carves realistic valleys/channels. |
| **Thermal Erosion** | Simulates loose sediment settling to a natural angle of repose (softens cliffs into slopes). |
| **Wind Erosion** | Simulates wind redistributing sediment (dune-like ripples/drift patterns). |

All three read more detail on a higher-resolution heightmap — the docs recommend **1025+ heightmap resolution** to see clean erosion results. Check `OutdoorTerrainData.asset`'s heightmap resolution before relying on these for fine detail.

---

## Transform tools (move/distort existing height, non-destructively re-sculpting)

| Tool | What it does |
|---|---|
| **Pinch** | Pulls height toward (or pushes away from) the brush center. |
| **Smudge** | Drags existing terrain features along the brush stroke path (like smudging wet paint). |
| **Twist** | Rotates terrain features around the brush center along the stroke. |

Good for taking a stamped feature and giving it a less-symmetrical, hand-sculpted look after the fact.

---

## Paint Texture (Terrain Layers) + Brush Mask Filters

`Paint Texture` applies one of your 16 `Assets/TerrainSampleAssets/TerrainLayers` materials (grass/sand/soil/snow/rocky variants) to the ground. On its own it's a plain brush; **Brush Mask Filters** (added by Terrain Tools) let you constrain *where* a stroke actually paints, based on the terrain's own shape:

**Terrain-based filters** (read the terrain itself):
- **Slope** — paint only steep/shallow areas
- **Concavity** — paint only valleys (or only ridges, inverted)
- **Height** — paint only above/below a Y threshold
- **Aspect** — paint only slopes facing a given compass direction
- **Layer** — paint based on what layer is already there

**Math filters** (combine/reshape the above): Add, Abs, Clamp, Complement, Max, Min, Negate, Power, Remap.

Filters apply top-to-bottom in the list and multiply together. Classic combo (straight from the sample pack's own tutorial): a **Slope** filter so grass only paints on shallow ground, plus a **Concavity** filter so a soil/rock layer only paints into valleys — one pass, no manual masking.

Filters are designed for texture painting, not height sculpting — the docs specifically warn they can produce artifacts if used to mask a height brush.

---

## Paint Details (scattering grass/foliage on the Terrain itself)

This is Terrain's **native** GPU-instanced scatter system — separate from both Stamp Terrain and from the Map Editor's own splash-brush prop scattering (`Assets/Editor/MapEditorWindow.cs`'s Props category).

Workflow:
1. Select **Paint Details** brush → **Edit Details** → **Add Detail Mesh**.
2. Pick a `Detail Prefab` (the sample pack ships 20 under `Assets/TerrainSampleAssets/Prefabs/{Trees,Rocks,Foliage}`), set Min/Max Width and Height for size variance, confirm **Use GPU Instancing** is checked (lets it use the prefab's real Shader Graph material instead of a flat billboard).
3. Paint directly on the terrain.

Tips from the sample pack's own docs: keep **Brush Size**, **Opacity**, and especially **Target Strength** low at first — it's very easy to generate far more instances than intended and tank frame rate. GPU instancing needs Unity 2021.2+ (fine on Unity 6).

**Note for this project**: this is a *different* mechanism from the Map Editor's `PaintSplash` (which instantiates real, individually-destructible `GameObject`s you can select/erase one at a time). Terrain Details are baked into the Terrain's own data — not real GameObjects, can't be individually clicked/erased/queried by gameplay code. Fine for decorative backdrop grass; wrong tool if something needs per-instance gameplay logic.

---

## Terrain Toolbox (`Window > Terrain > Terrain Toolbox`)

A standalone utility window, separate from the per-Terrain brush tools above:

- **Create New Terrain** — spin up a new Terrain from a preset or an imported heightmap.
- **Terrain Settings** — batch-apply settings (resolution, pixel error, material, etc.) across multiple terrain tiles at once.
- **Terrain Utilities** — import/export heightmaps and splatmaps (PNG/RAW round-trip).
- **Terrain Visualization** — inspection/analysis overlays (slope, height, etc.) for the currently selected terrain.

Mostly relevant if this project ever splits `OutdoorTerrain` into multiple tiles or needs to batch-tune settings — for a single-terrain setup like MechTS's current one, the per-Terrain brush tools above cover everything.

---

## Sample pack inventory (`Assets/TerrainSampleAssets`)

- **42 height stamps** (`TerrainBrushes/`) — canyon, mountain, plateau, rolling-hills categories. Usable immediately in Stamp Terrain's Brush mode, no setup needed.
- **16 Terrain Layers** (`TerrainLayers/`) — grass, sand, soil, snow, rocky variants. Some are already wired into `OutdoorTerrainData.asset` per issue #84.
- **20 vegetation prefabs** (`Prefabs/{Trees,Rocks,Foliage}/`) — grass, fern, bush, plus Trees/Rocks folders. Usable directly in Paint Details.

---

## MechTS-specific notes — read before sculpting

- **`OutdoorTerrain` is currently deliberately flat** (issue #84) and sits on the `Environment` layer with a real `TerrainCollider` — but that collider exists *only* so Unity's own Editor paint tools work (Terrain's texture/detail brushes require a live collider to paint, confirmed the hard way in issue #84). Nothing in gameplay code raycasts against the `Environment` layer, so it currently has zero gameplay effect either way.
- **`Ground`** (a separate, invisible, collider-only plane) is the real raycast/NavMesh/Map-Editor-paint surface. It stays exactly at world `Y = 0` and is what `SelectionController`/`AttackCommand`/`BuildingPlacement`/`NavMeshBakeUtility` all actually use.
- **Playable elevation today comes from the Map Editor's `Platform_Tier1`/`Ramp` prefabs** (issue #35), not from Terrain height — those are separate mesh objects on the `Ground` layer with their own NavMesh baking support, deliberately independent of whatever height `OutdoorTerrain` has.
- **Until the open question in the accompanying spec is resolved, treat any Terrain Tools sculpting on `OutdoorTerrain` as backdrop/visual-only** — mountains, canyons, and ridgelines *around* the playable Space Platform area, not *under* it. Sculpting height directly beneath a painted platform/ramp won't move the platform (it's a separate mesh) and could create a visual mismatch (platform floating or clipping into now-uneven ground).
