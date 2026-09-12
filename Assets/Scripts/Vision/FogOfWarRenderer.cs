using MechTS.Units;
using UnityEngine;
using UnityEngine.Rendering;

namespace MechTS.Vision
{
    /// <summary>
    /// Renders the Player faction's current <see cref="FogState"/> as a soft-edged shroud: a
    /// terrain-conforming grid mesh (one vertex per <see cref="VisionManager"/> grid-cell
    /// corner, each sampling real <c>OutdoorTerrain</c> height) sampling a small per-cell
    /// <see cref="Texture2D"/> built from <see cref="VisionManager"/>'s grid. Bilinear texture
    /// filtering blends between cells automatically, giving a soft "edge of vision" gradient
    /// instead of the grid's hard per-cell boundaries.
    /// </summary>
    /// <remarks>
    /// Replaces an earlier Tilemap-based implementation (issue #36) that never actually
    /// rendered in this project's real configured pipeline — confirmed via extensive direct
    /// pixel-level testing (a brand-new Tilemap, in isolation, with every material/sorting/mode
    /// combination tried, never drew a single pixel). This project's URP asset turned out to
    /// use a 3D <c>UniversalRendererData</c>, not the 2D Renderer <c>CLAUDE.md</c>'s
    /// conventions call for, which is the most likely reason the Tilemap's 2D-specific sprite
    /// shader never rendered. This mesh+texture approach instead reuses the exact
    /// Unlit + <c>Cull Off</c> material pattern already proven to work for every unit/selection
    /// ring in the project.
    /// </remarks>
    /// <remarks>
    /// **Terrain-conforming mesh (issue #97, shipped 2026-09-11):** the original implementation
    /// built a single flat quad at a fixed world <see cref="YOffset"/> — correct back when the
    /// level was a universally flat <c>Ground</c> plane at Y=0 (pre-issue #89), but wrong once
    /// <c>OutdoorTerrain</c> gained real variable elevation (issues #84/#89/#94): wherever real
    /// terrain height dropped at or below that fixed offset, the terrain no longer occluded the
    /// shroud in the depth buffer, so it rendered as a visible black/dark slab floating above
    /// low ground instead of being hidden beneath higher ground the way it correctly was
    /// elsewhere. Root-caused directly via `unity-mcp` (terrain data itself confirmed clean —
    /// 0% holes, 0 NaN heights, valid layers/alphamaps — ruling out a data-corruption bug
    /// before looking at rendering). Fixed by sampling <see cref="Terrain.SampleHeight"/> per
    /// grid-cell-corner vertex instead of using one constant Y for the whole mesh — every other
    /// aspect (the per-cell texture, the "update only changed cells" diffing, the material)
    /// is unchanged.
    /// </remarks>
    public class FogOfWarRenderer : MonoBehaviour
    {
        private static readonly Color UnexploredColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color ExploredColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color VisibleColor = new Color(0f, 0f, 0f, 0f);

        /// <summary>
        /// World-space Y offset above the real terrain surface (Ground at 0, Terrain-layer
        /// content around 0.01-0.02, unit/selection rings at 0.02) so the shroud unambiguously
        /// wins the depth test instead of Z-fighting with content at the same height —
        /// confirmed necessary directly: without this offset, the quad sat flush with the
        /// ground and produced visible interleaved banding artifacts at fog-state boundaries.
        /// Added on top of each vertex's own sampled terrain height (see
        /// <see cref="BuildTerrainConformingMesh"/>), not a single constant world Y anymore.
        /// </summary>
        private const float YOffset = 0.05f;

        private VisionManager _visionManager;
        private Texture2D _fogTexture;
        private Color32[] _pixelBuffer;
        private FogState[,] _lastRendered;

        /// <summary>
        /// Caches the scene's <see cref="VisionManager"/>.
        /// </summary>
        private void Start()
        {
            _visionManager = FindFirstObjectByType<VisionManager>();
        }

        /// <summary>
        /// Builds the terrain-conforming mesh and fog texture the first time
        /// <see cref="VisionManager"/>'s grid becomes available, then updates only the texture
        /// pixels whose <see cref="FogState"/> actually changed since the last tick.
        /// </summary>
        private void Update()
        {
            if (_visionManager == null || _visionManager.GridWidth <= 0 || _visionManager.GridHeight <= 0) return;
            if (_fogTexture == null) BuildQuadAndTexture();

            bool changed = false;
            int width = _visionManager.GridWidth;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < _visionManager.GridHeight; z++)
                {
                    var state = _visionManager.GetFogState(Faction.Player, _visionManager.CellToWorld(x, z));
                    if (_lastRendered[x, z] == state) continue;

                    _lastRendered[x, z] = state;
                    _pixelBuffer[z * width + x] = GetColorFor(state);
                    changed = true;
                }
            }

            if (changed)
            {
                _fogTexture.SetPixels32(_pixelBuffer);
                _fogTexture.Apply();
            }
        }

        /// <summary>
        /// Builds the fog texture (initialized fully Unexplored) and the terrain-conforming
        /// mesh/material covering <see cref="VisionManager"/>'s full grid extent.
        /// </summary>
        private void BuildQuadAndTexture()
        {
            int width = _visionManager.GridWidth;
            int height = _visionManager.GridHeight;

            _fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _fogTexture.filterMode = FilterMode.Bilinear;
            _fogTexture.wrapMode = TextureWrapMode.Clamp;
            _fogTexture.name = "FogOfWarTexture";

            _pixelBuffer = new Color32[width * height];
            for (int i = 0; i < _pixelBuffer.Length; i++) _pixelBuffer[i] = UnexploredColor;
            _fogTexture.SetPixels32(_pixelBuffer);
            _fogTexture.Apply();

            _lastRendered = new FogState[width, height];

            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            gameObject.AddComponent<MeshFilter>().mesh = BuildTerrainConformingMesh(width, height);

            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.material = BuildMaterial(_fogTexture);
        }

        /// <summary>
        /// Builds a transparent, double-sided Unlit material sampling the given texture —
        /// mirrors <see cref="Utilities.RingMesh"/>'s <c>Cull Off</c> convention, plus the
        /// transparent surface/blend setup a fog shroud needs.
        /// </summary>
        /// <param name="texture">The fog texture to sample as the base map.</param>
        private static Material BuildMaterial(Texture2D texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { name = "FogOfWarMaterial" };

            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetFloat("_Surface", 1f); // URP Unlit: 0 = Opaque, 1 = Transparent
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetInt("_ZWrite", 0);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);

            material.mainTexture = texture;
            material.color = Color.white;
            return material;
        }

        /// <summary>
        /// Builds a mesh with one vertex per grid-cell corner ((<paramref name="width"/>+1) by
        /// (<paramref name="height"/>+1) vertices, sharing vertices between adjacent cells so
        /// there are no seams), each sampling real <see cref="Terrain.SampleHeight"/> at its
        /// world XZ position plus <see cref="YOffset"/> — draping the shroud over the actual
        /// ground instead of sitting at one constant world height (issue #97). Falls back to a
        /// flat Y of just <see cref="YOffset"/> if no active <see cref="Terrain"/> exists,
        /// rather than throwing — mirrors <c>MapShellGenerator</c>'s existing "missing terrain
        /// is a handled case" precedent. UV coordinates map each vertex to its corresponding
        /// texel in the per-cell fog texture, identical to the original flat quad's mapping —
        /// only the mesh's vertex positions changed, not how it samples the texture.
        /// </summary>
        /// <param name="width">The vision grid's cell width.</param>
        /// <param name="height">The vision grid's cell height.</param>
        private Mesh BuildTerrainConformingMesh(int width, int height)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogWarning("MechTS: FogOfWarRenderer found no active Terrain — the shroud will render flat.");
            }

            Vector3 halfCell = new Vector3(VisionManager.CellSize * 0.5f, 0f, VisionManager.CellSize * 0.5f);
            Vector3 min = _visionManager.CellToWorld(0, 0) - halfCell;

            int vertsX = width + 1;
            int vertsZ = height + 1;
            var vertices = new Vector3[vertsX * vertsZ];
            var uv = new Vector2[vertsX * vertsZ];

            for (int j = 0; j < vertsZ; j++)
            {
                float worldZ = min.z + j * VisionManager.CellSize;
                for (int i = 0; i < vertsX; i++)
                {
                    float worldX = min.x + i * VisionManager.CellSize;
                    float terrainHeight = terrain != null ? terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) : 0f;

                    int index = j * vertsX + i;
                    vertices[index] = new Vector3(worldX, terrainHeight + YOffset, worldZ);
                    uv[index] = new Vector2(i / (float)width, j / (float)height);
                }
            }

            var triangles = new int[width * height * 6];
            int t = 0;
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    int a = j * vertsX + i;
                    int b = (j + 1) * vertsX + i;
                    int c = (j + 1) * vertsX + i + 1;
                    int d = j * vertsX + i + 1;

                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = c;
                    triangles[t++] = a;
                    triangles[t++] = c;
                    triangles[t++] = d;
                }
            }

            var mesh = new Mesh { name = "FogOfWarTerrainMesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Returns the shroud tint for a given fog state.
        /// </summary>
        private static Color GetColorFor(FogState state)
        {
            switch (state)
            {
                case FogState.Visible: return VisibleColor;
                case FogState.Explored: return ExploredColor;
                default: return UnexploredColor;
            }
        }
    }
}
