using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MechTS.EditorTools
{
    /// <summary>
    /// Bakes the scene's NavMesh with a finer voxel size than the project's default
    /// (0.1 instead of the default-derived ~0.1667), via the <c>MechTS/Bake NavMesh</c> menu
    /// item, and writes the result into the scene's existing persisted
    /// <c>NavMesh.asset</c> (under <c>Assets/Scenes/&lt;SceneName&gt;/</c>) rather than only
    /// loading it into the current session — matching what the Navigation window's own Bake
    /// button does, so the result survives an Editor restart and is included in builds.
    /// </summary>
    /// <remarks>
    /// Plain <see cref="UnityEditor.AI.NavMeshBuilder.BuildNavMesh"/> at the project's default
    /// voxel size (issue #35) reliably failed to connect a sloped <c>Ramp</c> prefab's top
    /// surface to a flat <c>Platform_Tier1</c>'s edge, even with generous physical geometry
    /// overlap between them — <see cref="NavMesh.CalculatePath"/> consistently returned
    /// <c>PathPartial</c>, stopping just short of the platform, across many different overlap
    /// amounts. The default voxel size proved too coarse to reliably rasterize that seam as
    /// connected; 0.1 (confirmed directly via repeated <see cref="NavMesh.CalculatePath"/>
    /// checks, including with a real designer-facing path from ground through the ramp onto
    /// the plateau) resolves it. <b>Use this menu item instead of the Navigation window's own
    /// Bake button whenever the map has any elevation</b> (a <c>Platform_Tier1</c>/<c>Ramp</c>
    /// pair) — flat-only scenes can still use either.
    /// </remarks>
    public static class NavMeshBakeUtility
    {
        private const float VoxelSize = 0.1f;

        /// <summary>
        /// The Map Editor brush category (see <c>MapBrushDefinition.category</c>) holding
        /// terrain-elevation content (<c>Platform_Tier1</c>, <c>Ramp</c>). Excluded from the
        /// "Flying" agent type's bake (see <see cref="Bake"/>'s remarks) — flying units don't
        /// scale a cliff, they simply aren't blocked by it, which means this geometry needs
        /// to not exist at all from that agent type's point of view, not be tuned to look
        /// "climbable." Ground-tier content elsewhere (buildings, resource nodes) is
        /// deliberately left untouched — flying only bypasses *elevation*, not every obstacle.
        /// </summary>
        private const string TerrainCategoryName = "Terrain";

        /// <summary>
        /// Bakes every registered NavMesh Agent Type in one pass — the default (Humanoid,
        /// id 0, sees every obstacle including terrain elevation) plus any additional ones a
        /// designer has authored in <c>ProjectSettings/NavMeshAreas.asset</c> (currently just
        /// "Flying", issue #40) — each into its own persisted <c>NavMeshData</c> asset
        /// (<c>NavMesh.asset</c> for agent 0, <c>NavMesh_&lt;Name&gt;.asset</c> for every other
        /// agent type), and loads all of them into the active session. For any agent type
        /// other than 0, sources under the Map Editor's <see cref="TerrainCategoryName"/>
        /// category (<c>Platform_Tier1</c>/<c>Ramp</c>) are filtered out of the collected
        /// source list *after* collection, before building — a flying unit's own NavMesh
        /// simply doesn't include cliffs at all, so pathing crosses straight over the same
        /// flat baseline a ground unit would need a ramp to reach, rather than attempting to
        /// make the cliff's own geometry "climbable" (confirmed via direct testing that no
        /// climb/slope tuning can make Recast treat a vertical wall as traversable terrain —
        /// see the issue #40 completion notes).
        /// <para>
        /// <b>Issue #89 deliberately does NOT extend this exclusion to <c>OutdoorTerrain</c>'s
        /// own sculpted-heightmap source, despite that being the original plan.</b> Tried and
        /// reverted after direct testing: with the old flat <c>Ground</c> GameObject retired
        /// (issue #89 — raycasts and NavMesh both moved to <c>OutdoorTerrain</c> as the real
        /// surface), excluding Terrain from the Flying bake left it with <i>zero</i> walkable
        /// geometry anywhere on the map, not just near sculpted elevation — Ground had been
        /// Flying's only other full-map-coverage source, and there is nothing left to fall
        /// back to once both are gone. Terrain doesn't create the same problem Platform_Tier1/
        /// Ramp does: a platform is a small, isolated obstacle sitting <i>on top of</i> an
        /// otherwise-flat, always-present surface, so excluding it restores a real flat
        /// pass-through underneath. Terrain <i>is</i> the surface itself — there's no "flat
        /// underneath" to fall back to once it's excluded. Recast's own slope-based
        /// voxelization already keeps a too-steep Terrain slope out of Flying's walkable mesh
        /// exactly the way it already excludes Platform_Tier1's cliff faces from Humanoid's
        /// (issue #35) — both agent types currently share the same 45° <c>agentSlope</c>
        /// setting, so Flying gets no special steep-terrain bypass today. If a future mission's
        /// sculpting creates a genuinely Flying-blocking slope, that's a real follow-up (a
        /// higher <c>agentSlope</c> on the Flying agent type is the likely fix, since — unlike
        /// Platform_Tier1's literal vertical wall, which issue #40 already proved can't be
        /// tuned around — a heightmap-based slope is never actually vertical, so a more
        /// permissive slope tolerance may well resolve it), not something this issue needed to
        /// solve pre-emptively.
        /// </para>
        /// Filtering post-collection rather than toggling
        /// each object's <c>NavigationStatic</c> flag before collecting is deliberate — that
        /// flag change was confirmed live to not actually affect <see cref="NavMeshBuilder.CollectSources"/>'s
        /// result within the same Editor session (read back as correctly cleared, sources
        /// still included it regardless), a real, non-obvious `unity-mcp` gotcha in the same
        /// family as the already-documented <c>Physics.SyncTransforms()</c> staleness issue.
        /// A second agent type's asset has no automatic scene-association load path the way
        /// agent 0's does — <see cref="Bootstrapper"/> loads it explicitly at runtime via its
        /// own serialized reference, wired once to the same asset this method writes to.
        /// </summary>
        [MenuItem("MechTS/Bake NavMesh")]
        public static void Bake()
        {
            Scene scene = SceneManager.GetActiveScene();
            string dir = $"Assets/Scenes/{scene.name}";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            NavMesh.RemoveAllNavMeshData();
            int settingsCount = NavMesh.GetSettingsCount();
            var bakedAssets = new List<NavMeshData>();
            var terrainObjects = FindTerrainCategoryObjects();

            for (int i = 0; i < settingsCount; i++)
            {
                var settings = NavMesh.GetSettingsByIndex(i);
                settings.overrideVoxelSize = true;
                settings.voxelSize = VoxelSize;
                string agentName = NavMesh.GetSettingsNameFromID(settings.agentTypeID);
                bool excludeTerrain = settings.agentTypeID != 0;

                var sources = new List<NavMeshBuildSource>();
                var markups = new List<NavMeshBuildMarkup>();
                Bounds bounds = new Bounds(Vector3.zero, new Vector3(200f, 50f, 200f));
                NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.RenderMeshes, 0, markups, sources);

                if (excludeTerrain)
                {
                    sources.RemoveAll(s => s.component != null && IsUnderAny(s.component.transform, terrainObjects));
                }

                string dataName = settings.agentTypeID == 0 ? "NavMesh" : $"NavMesh_{agentName}";
                var newData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                newData.name = dataName;

                string assetPath = settings.agentTypeID == 0 ? $"{dir}/NavMesh.asset" : $"{dir}/NavMesh_{agentName}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(newData, existing);
                    existing.name = dataName;
                    EditorUtility.SetDirty(existing);
                }
                else
                {
                    AssetDatabase.CreateAsset(newData, assetPath);
                    existing = newData;
                }

                bakedAssets.Add(existing);
                NavMesh.AddNavMeshData(existing);
                Debug.Log($"MechTS: baked agent '{agentName}' (id={settings.agentTypeID}) at voxelSize={VoxelSize} from {sources.Count} sources (terrain-elevation excluded: {excludeTerrain}), saved to {assetPath}.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"MechTS: NavMesh bake complete — {bakedAssets.Count} agent type(s). Wire each non-Humanoid asset into Bootstrapper's matching serialized field (e.g. _flyingNavMeshData) once so it also loads at runtime, not just in this Editor session.");
        }

        /// <summary>
        /// Finds every object painted under the Map Editor's <see cref="TerrainCategoryName"/>
        /// category (<c>MapContent/Terrain/*</c>) — the <c>Platform_Tier1</c>/<c>Ramp</c>
        /// instances a non-ground agent type's bake should exclude.
        /// </summary>
        private static List<GameObject> FindTerrainCategoryObjects()
        {
            var result = new List<GameObject>();
            var mapContent = GameObject.Find("MapContent");
            if (mapContent == null) return result;

            var terrainParent = mapContent.transform.Find(TerrainCategoryName);
            if (terrainParent == null) return result;

            foreach (Transform child in terrainParent)
            {
                result.Add(child.gameObject);
            }
            return result;
        }

        /// <summary>
        /// Whether <paramref name="target"/> is <paramref name="roots"/> itself or a
        /// descendant of any of them.
        /// </summary>
        private static bool IsUnderAny(Transform target, List<GameObject> roots)
        {
            foreach (var root in roots)
            {
                if (target == root.transform || target.IsChildOf(root.transform)) return true;
            }
            return false;
        }
    }
}
