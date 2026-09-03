using MechTS.Units;
using UnityEngine;
using UnityEngine.Rendering;

namespace MechTS.Vision
{
    /// <summary>
    /// Renders the Player faction's current <see cref="FogState"/> as a soft-edged shroud: a
    /// single flat quad (built the same way as <see cref="Utilities.RingMesh"/> — flat in the
    /// local XZ plane, normal facing +Y, no rotation needed) sampling a small per-cell
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
    public class FogOfWarRenderer : MonoBehaviour
    {
        private static readonly Color UnexploredColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color ExploredColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color VisibleColor = new Color(0f, 0f, 0f, 0f);

        /// <summary>
        /// World-space Y offset above everything else (Ground at 0, Terrain-layer content
        /// around 0.01-0.02, unit/selection rings at 0.02) so the shroud unambiguously wins
        /// the depth test instead of Z-fighting with content at the same height — confirmed
        /// necessary directly: without this offset, the quad sat at Y=0 (same as Ground) and
        /// produced visible interleaved banding artifacts at fog-state boundaries.
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
        /// Builds the quad mesh and fog texture the first time <see cref="VisionManager"/>'s
        /// grid becomes available, then updates only the texture pixels whose
        /// <see cref="FogState"/> actually changed since the last tick.
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
        /// Builds the fog texture (initialized fully Unexplored) and the flat quad mesh/material
        /// covering <see cref="VisionManager"/>'s full grid extent.
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

            transform.position = new Vector3(0f, YOffset, 0f);

            Vector3 halfCell = new Vector3(VisionManager.CellSize * 0.5f, 0f, VisionManager.CellSize * 0.5f);
            Vector3 min = _visionManager.CellToWorld(0, 0) - halfCell;
            Vector3 max = _visionManager.CellToWorld(width - 1, height - 1) + halfCell;

            gameObject.AddComponent<MeshFilter>().mesh = BuildFlatQuadMesh(min, max);

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
        /// Builds a flat rectangular mesh lying in the local XZ plane (normal +Y), spanning
        /// the given world-space corners — already oriented for this project's top-down
        /// camera, no rotation needed (same convention as <see cref="Utilities.RingMesh"/>).
        /// </summary>
        /// <param name="min">The rectangle's minimum world-space corner.</param>
        /// <param name="max">The rectangle's maximum world-space corner.</param>
        private static Mesh BuildFlatQuadMesh(Vector3 min, Vector3 max)
        {
            var vertices = new[]
            {
                new Vector3(min.x, 0f, min.z),
                new Vector3(min.x, 0f, max.z),
                new Vector3(max.x, 0f, max.z),
                new Vector3(max.x, 0f, min.z),
            };
            var uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
            };
            var triangles = new[] { 0, 1, 2, 0, 2, 3 };

            var mesh = new Mesh { name = "FogOfWarQuad" };
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
