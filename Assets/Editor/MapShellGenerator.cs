using System.Collections.Generic;
using MechTS.Economy;
using UnityEditor;
using UnityEngine;

namespace MechTS.EditorTools
{
    /// <summary>
    /// Generates a randomized terrain "shell" — sculpted elevation, cliff/platform clusters,
    /// and foliage scatter — onto the scene's <c>OutdoorTerrain</c> in one pass (issue #94), so
    /// a designer can start map-building from a varied base instead of blank flat terrain. Pure
    /// Editor-time tooling, same category as <see cref="MapEditorWindow"/>/<see cref="NavMeshBakeUtility"/> —
    /// no runtime code path. Every placement goes through <see cref="MapEditorPainter.TryPaint"/>,
    /// the same entry point a designer's own mouse click uses, so generated content is ordinary,
    /// fully editable/erasable scene content afterward — nothing here is special-cased as
    /// "generated." Resource nodes, <c>PlayerStartPoint</c>s, and buildings are never touched —
    /// those stay entirely manual, per the issue's explicit scope.
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
                    "Platform/Ramp/foliage instances. Existing hand-placed scene content is never removed. Continue?",
                    "Generate", "Cancel");
                if (proceed) Generate(_config);
            }
        }

        /// <summary>
        /// Runs the full generation pass: heightmap, layer blending, platform/ramp clusters,
        /// foliage scatter, and Detail Mesh vegetation, all seeded from
        /// <see cref="MapShellGenerationConfig.seed"/> so the same config reproduces the same result.
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

            var platformPositions = PlacePlatforms(config, rng, terrain.transform.position, data, painter);
            PlaceFoliage(config, rng, terrain.transform.position, data, platformPositions, painter);
            PopulateDetailScatter(data, config, rng);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"MechTS: Map shell generated (seed={config.seed}) — {platformPositions.Count} platform cluster(s) placed.");
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
        /// Places <see cref="MapShellGenerationConfig.platformCount"/> Platform+Ramp clusters at
        /// random, mutually non-overlapping positions (a simple minimum-spacing rejection sample —
        /// no existing overlap-checking code applies to Terrain-category content, so this is new,
        /// self-contained logic, not reused). Placement itself goes through
        /// <see cref="MapEditorPainter.TryPaint"/>, exactly like a designer's own click.
        /// </summary>
        /// <returns>The XZ positions of every platform actually placed, for <see cref="PlaceFoliage"/> to avoid.</returns>
        private List<Vector3> PlacePlatforms(MapShellGenerationConfig config, System.Random rng, Vector3 terrainOrigin, TerrainData data, MapEditorPainter painter)
        {
            var placed = new List<Vector3>();
            if (config.platformBrush == null) return placed;

            int maxAttempts = config.platformCount * 20;
            for (int attempt = 0; attempt < maxAttempts && placed.Count < config.platformCount; attempt++)
            {
                float x = terrainOrigin.x + (float)rng.NextDouble() * data.size.x;
                float z = terrainOrigin.z + (float)rng.NextDouble() * data.size.z;
                var candidate = new Vector3(x, 0f, z);

                if (IsTooClose(candidate, placed, config.platformSpacing)) continue;

                painter.TryPaint(config.platformBrush, candidate, 0f);
                painter.EndDrag();
                placed.Add(candidate);

                if (config.rampBrush != null)
                {
                    var rampPos = candidate + new Vector3(config.platformSpacing * 0.3f, 0f, 0f);
                    painter.TryPaint(config.rampBrush, rampPos, 0f);
                    painter.EndDrag();
                }
            }
            return placed;
        }

        /// <summary>
        /// Scatters <see cref="MapShellGenerationConfig.foliageClusterCount"/> splash-brush
        /// clusters (drawn randomly from <see cref="MapShellGenerationConfig.foliageBrushes"/>)
        /// across the terrain, skipping candidates on too-steep slopes or too close to a
        /// placed platform.
        /// </summary>
        private void PlaceFoliage(MapShellGenerationConfig config, System.Random rng, Vector3 terrainOrigin, TerrainData data, List<Vector3> platformPositions, MapEditorPainter painter)
        {
            if (config.foliageBrushes == null || config.foliageBrushes.Length == 0) return;

            int placedCount = 0;
            int maxAttempts = config.foliageClusterCount * 20;
            for (int attempt = 0; attempt < maxAttempts && placedCount < config.foliageClusterCount; attempt++)
            {
                float normX = (float)rng.NextDouble();
                float normZ = (float)rng.NextDouble();
                if (data.GetSteepness(normX, normZ) > config.maxFoliageSlope) continue;

                var candidate = new Vector3(terrainOrigin.x + normX * data.size.x, 0f, terrainOrigin.z + normZ * data.size.z);
                if (IsTooClose(candidate, platformPositions, config.platformSpacing * 0.5f)) continue;

                var brush = config.foliageBrushes[rng.Next(config.foliageBrushes.Length)];
                painter.TryPaint(brush, candidate, 0f);
                painter.EndDrag();
                placedCount++;
            }
        }

        /// <summary>Whether <paramref name="candidate"/> is within <paramref name="minSpacing"/> (XZ only) of any position already in <paramref name="existing"/>.</summary>
        private static bool IsTooClose(Vector3 candidate, List<Vector3> existing, float minSpacing)
        {
            foreach (var p in existing)
            {
                if (Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(p.x, p.z)) < minSpacing) return true;
            }
            return false;
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
