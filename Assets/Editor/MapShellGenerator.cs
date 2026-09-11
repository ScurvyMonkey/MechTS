using MechTS.Economy;
using UnityEditor;
using UnityEngine;

namespace MechTS.EditorTools
{
    /// <summary>
    /// Generates a randomized terrain "shell" — sculpted elevation and foliage scatter — onto
    /// the scene's <c>OutdoorTerrain</c> in one pass (issue #94), so a designer can start
    /// map-building from a varied base instead of blank flat terrain. Pure Editor-time tooling,
    /// same category as <see cref="MapEditorWindow"/>/<see cref="NavMeshBakeUtility"/> — no
    /// runtime code path. Foliage placement goes through <see cref="MapEditorPainter.TryPaint"/>,
    /// the same entry point a designer's own mouse click uses, so generated content is ordinary,
    /// fully editable/erasable scene content afterward — nothing here is special-cased as
    /// "generated." Resource nodes, <c>PlayerStartPoint</c>s, buildings, and — as of issue #95 —
    /// <c>Platform_Tier1</c>/<c>Ramp</c> clusters are never touched; the first version of this
    /// tool did place Platform/Ramp clusters, but naive offset placement (no rotation, no
    /// awareness of either prefab's actual footprint) never produced a properly-connected
    /// result — those pieces are modular content meant for precise hand-placement (issue #35),
    /// and this tool doesn't attempt automatic geometry-aware alignment for them. Scope narrowed
    /// to environment/terrain only, per direct designer feedback the same day.
    /// </summary>
    public class MapShellGenerator : EditorWindow
    {
        private MapShellGenerationConfig _config;

        /// <summary>Opens the Map Shell Generator window.</summary>
        [MenuItem("MechTS/Generate Map Shell")]
        public static void ShowWindow()
        {
            GetWindow<MapShellGenerator>("Map Shell Generator");
        }

        /// <summary>Draws the config picker and Generate button.</summary>
        private void OnGUI()
        {
            _config = (MapShellGenerationConfig)EditorGUILayout.ObjectField(
                "Config", _config, typeof(MapShellGenerationConfig), false);

            if (_config == null)
            {
                EditorGUILayout.HelpBox("Assign a MapShellGenerationConfig asset to generate from.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("New Random Seed"))
            {
                _config.seed = Random.Range(int.MinValue, int.MaxValue);
                EditorUtility.SetDirty(_config);
            }

            if (GUILayout.Button("Generate"))
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Generate Map Shell",
                    "This overwrites OutdoorTerrain's height, texture, and Detail Mesh data, and adds new " +
                    "foliage instances. Existing hand-placed scene content is never removed. Continue?",
                    "Generate", "Cancel");
                if (proceed) Generate(_config);
            }
        }

        /// <summary>
        /// Runs the full generation pass: heightmap, layer blending, foliage scatter, and
        /// Detail Mesh vegetation, all seeded from <see cref="MapShellGenerationConfig.seed"/>
        /// so the same config reproduces the same result.
        /// </summary>
        /// <param name="config">The generation parameters to use.</param>
        private void Generate(MapShellGenerationConfig config)
        {
            var terrainGo = GameObject.Find("OutdoorTerrain");
            var terrain = terrainGo != null ? terrainGo.GetComponent<Terrain>() : null;
            if (terrain == null)
            {
                Debug.LogError("MechTS: MapShellGenerator found no 'OutdoorTerrain' GameObject with a Terrain component.");
                return;
            }

            var data = terrain.terrainData;
            var rng = new System.Random(config.seed);
            var painter = new MapEditorPainter();

            GenerateHeights(data, config, rng);
            BlendLayers(data);

            int foliageCount = PlaceFoliage(config, rng, terrain.transform.position, data, painter);
            PopulateDetailScatter(data, config, rng);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"MechTS: Map shell generated (seed={config.seed}) — {foliageCount} foliage cluster(s) placed.");
        }

        /// <summary>
        /// Writes a layered (3-octave) Perlin-noise heightmap, remapped into
        /// <see cref="MapShellGenerationConfig.minHeight"/>/<see cref="MapShellGenerationConfig.maxHeight"/>.
        /// Perlin noise itself has no seed parameter, so the seed instead offsets where in noise-space
        /// each octave samples from.
        /// </summary>
        private void GenerateHeights(TerrainData data, MapShellGenerationConfig config, System.Random rng)
        {
            int res = data.heightmapResolution;
            float offsetX = (float)rng.NextDouble() * 10000f;
            float offsetZ = (float)rng.NextDouble() * 10000f;
            var heights = new float[res, res];

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float value = 0f;
                    float amplitude = 1f;
                    float frequency = 1f;
                    float amplitudeSum = 0f;
                    for (int octave = 0; octave < 3; octave++)
                    {
                        float nx = (x + offsetX) * config.noiseScale * frequency;
                        float nz = (z + offsetZ) * config.noiseScale * frequency;
                        value += Mathf.PerlinNoise(nx, nz) * amplitude;
                        amplitudeSum += amplitude;
                        amplitude *= 0.5f;
                        frequency *= 2f;
                    }
                    value /= amplitudeSum;

                    float worldHeight = Mathf.Lerp(config.minHeight, config.maxHeight, value);
                    heights[z, x] = worldHeight / data.size.y;
                }
            }
            data.SetHeights(0, 0, heights);
        }

        /// <summary>
        /// Blends whatever <see cref="TerrainData.terrainLayers"/> are already assigned by
        /// height and slope — layer 0 favored at low elevation/shallow slope, layer 1 at
        /// higher elevation, layer 2 (if present) favored on steep slopes regardless of height,
        /// matching the height/slope-driven approach issue #84 already established for this
        /// terrain's base look. Degrades gracefully if fewer than 3 layers are assigned.
        /// </summary>
        private void BlendLayers(TerrainData data)
        {
            int layerCount = data.terrainLayers.Length;
            if (layerCount == 0) return;

            int alphaRes = data.alphamapResolution;
            var alphas = new float[alphaRes, alphaRes, layerCount];

            for (int z = 0; z < alphaRes; z++)
            {
                for (int x = 0; x < alphaRes; x++)
                {
                    float normX = x / (float)alphaRes;
                    float normZ = z / (float)alphaRes;
                    float height01 = Mathf.Clamp01(data.GetInterpolatedHeight(normX, normZ) / data.size.y);
                    float steepness = data.GetSteepness(normX, normZ);

                    var weights = new float[layerCount];
                    weights[0] = 1f;
                    if (layerCount >= 2)
                    {
                        float highWeight = Mathf.Clamp01(height01 * 2f);
                        weights[0] = 1f - highWeight;
                        weights[1] = highWeight;
                    }
                    if (layerCount >= 3)
                    {
                        float rockWeight = Mathf.Clamp01((steepness - 20f) / 40f);
                        weights[0] *= 1f - rockWeight;
                        weights[1] *= 1f - rockWeight;
                        weights[2] = rockWeight;
                    }

                    float sum = 0f;
                    for (int l = 0; l < layerCount; l++) sum += weights[l];
                    for (int l = 0; l < layerCount; l++) alphas[z, x, l] = sum > 0f ? weights[l] / sum : (l == 0 ? 1f : 0f);
                }
            }
            data.SetAlphamaps(0, 0, alphas);
        }

        /// <summary>
        /// Scatters <see cref="MapShellGenerationConfig.foliageClusterCount"/> splash-brush
        /// clusters (drawn randomly from <see cref="MapShellGenerationConfig.foliageBrushes"/>)
        /// across the terrain, skipping candidates on too-steep slopes. Placement goes through
        /// <see cref="MapEditorPainter.TryPaint"/>, exactly like a designer's own click.
        /// </summary>
        /// <returns>The number of foliage clusters actually placed.</returns>
        private int PlaceFoliage(MapShellGenerationConfig config, System.Random rng, Vector3 terrainOrigin, TerrainData data, MapEditorPainter painter)
        {
            if (config.foliageBrushes == null || config.foliageBrushes.Length == 0) return 0;

            int placedCount = 0;
            int maxAttempts = config.foliageClusterCount * 20;
            for (int attempt = 0; attempt < maxAttempts && placedCount < config.foliageClusterCount; attempt++)
            {
                float normX = (float)rng.NextDouble();
                float normZ = (float)rng.NextDouble();
                if (data.GetSteepness(normX, normZ) > config.maxFoliageSlope) continue;

                var candidate = new Vector3(terrainOrigin.x + normX * data.size.x, 0f, terrainOrigin.z + normZ * data.size.z);
                var brush = config.foliageBrushes[rng.Next(config.foliageBrushes.Length)];
                painter.TryPaint(brush, candidate, 0f);
                painter.EndDrag();
                placedCount++;
            }
            return placedCount;
        }

        /// <summary>
        /// Populates every registered Terrain Detail Mesh prototype (issue #88's 20-prefab
        /// vegetation set) with a noise-clumped density map, matching issue #84's "noise-clumped
        /// rather than uniformly scattered" approach for this terrain's vegetation.
        /// </summary>
        private void PopulateDetailScatter(TerrainData data, MapShellGenerationConfig config, System.Random rng)
        {
            int detailRes = data.detailResolution;
            int layerCount = data.detailPrototypes.Length;
            if (layerCount == 0) return;

            float offsetX = (float)rng.NextDouble() * 10000f;
            float offsetZ = (float)rng.NextDouble() * 10000f;

            for (int layer = 0; layer < layerCount; layer++)
            {
                var densities = new int[detailRes, detailRes];
                float layerOffsetX = offsetX + layer * 137.7f;
                for (int z = 0; z < detailRes; z++)
                {
                    for (int x = 0; x < detailRes; x++)
                    {
                        float n = Mathf.PerlinNoise((x + layerOffsetX) * config.noiseScale * 4f, (z + offsetZ) * config.noiseScale * 4f);
                        densities[z, x] = n > 0.6f ? Mathf.RoundToInt(n * 8f) : 0;
                    }
                }
                data.SetDetailLayer(0, 0, layer, densities);
            }
        }
    }
}
