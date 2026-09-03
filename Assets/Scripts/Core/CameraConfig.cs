using UnityEngine;

namespace MechTS.Core
{
    /// <summary>
    /// Designer-tunable stats for <see cref="CameraManager"/>. One asset per mission — map
    /// size is a per-mission designer choice, not a project-wide constant, so a new mission
    /// with different pacing/scale needs is expected to author its own <c>CameraConfig</c>
    /// with its own <see cref="boundsMin"/>/<see cref="boundsMax"/> rather than reuse another
    /// mission's. <see cref="boundsMin"/>/<see cref="boundsMax"/> must match that mission's
    /// <c>Ground</c> plane extents and Tilemap-painted area exactly — nothing currently
    /// derives one from the other.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Core/Camera Config")]
    public class CameraConfig : ScriptableObject
    {
        /// <summary>
        /// Soft guardrail on a mission's map span (<see cref="boundsMax"/> - <see cref="boundsMin"/>
        /// per axis), checked in <see cref="OnValidate"/>. Below the minimum there isn't room for
        /// a main building, a couple of production buildings, some resource nodes, and actual unit
        /// maneuvering. Above the maximum, NavMesh bake cost and painted-object count start
        /// compounding, and nothing in the map-editor/Tilemap pipeline has been exercised past this.
        /// Deliberately a warning, not an enforced clamp — a mission that genuinely needs to break
        /// this is a designer call, not a bug.
        /// </summary>
        public const float MinRecommendedSpan = 60f;
        public const float MaxRecommendedSpan = 200f;

        public float panSpeed = 20f;
        public float edgePanMarginPixels = 12f;
        public Vector2 boundsMin = new Vector2(-50f, -50f);
        public Vector2 boundsMax = new Vector2(50f, 50f);

        /// <summary>
        /// The camera's fixed downward tilt, in degrees from horizontal (0 = looking straight
        /// forward, 90 = looking straight down — the old top-down camera's exact angle).
        /// Issue #80's angled SC2-style view uses something between the two.
        /// </summary>
        public float cameraPitchDegrees = 50f;

        /// <summary>
        /// The camera's fixed horizontal facing direction, in degrees around the world Y axis.
        /// Not player-adjustable — see issue #80's Out of Scope (no free camera rotation).
        /// </summary>
        public float cameraYawDegrees = 0f;

        /// <summary>Perspective field of view, in degrees. Unrelated to zoom — see <see cref="zoomDistanceMin"/>/<see cref="zoomDistanceMax"/> for that.</summary>
        public float fieldOfView = 60f;

        /// <summary>
        /// The camera's far clip plane. Increased from Unity's/this project's old default (100)
        /// during issue #80 — an offset angled camera panned to a far corner of a large map can
        /// sit meaningfully farther from distant terrain than the old always-directly-overhead
        /// camera ever did, and 100 was found to clip terrain at typical zoom-out + pan
        /// combinations on a map near <see cref="MaxRecommendedSpan"/>.
        /// </summary>
        public float farClipPlane = 300f;

        /// <summary>
        /// How far the camera sits from its focus point (see <see cref="CameraManager"/>) — this
        /// is what scroll-wheel zoom now changes, replacing the old orthographic-size-based zoom.
        /// Getting closer to <see cref="zoomDistanceMin"/> physically moves the camera nearer the
        /// focus point (real perspective foreshortening), not just a flatter magnified view.
        /// </summary>
        public float zoomDistanceMin = 12f;
        public float zoomDistanceMax = 40f;
        public float zoomSpeed = 5f;

        /// <summary>
        /// The camera's starting distance, deliberately closer than the midpoint of
        /// <see cref="zoomDistanceMin"/>/<see cref="zoomDistanceMax"/> — issue #80's explicit goal
        /// was enlarging models for better definition versus the old default framing.
        /// </summary>
        public float defaultZoomDistance = 18f;

#if UNITY_EDITOR
        /// <summary>
        /// Warns in the console if this config's map span falls outside the recommended
        /// <see cref="MinRecommendedSpan"/>/<see cref="MaxRecommendedSpan"/> range. Editor-only
        /// and non-blocking — see the guardrail rationale on those constants.
        /// </summary>
        private void OnValidate()
        {
            float width = boundsMax.x - boundsMin.x;
            float height = boundsMax.y - boundsMin.y;

            if (width < MinRecommendedSpan || height < MinRecommendedSpan)
            {
                Debug.LogWarning($"[{name}] Map span ({width}x{height}) is below the recommended minimum " +
                    $"({MinRecommendedSpan}x{MinRecommendedSpan}) — may not leave enough room for a main " +
                    "building, production buildings, resource nodes, and unit maneuvering.", this);
            }

            if (width > MaxRecommendedSpan || height > MaxRecommendedSpan)
            {
                Debug.LogWarning($"[{name}] Map span ({width}x{height}) is above the recommended maximum " +
                    $"({MaxRecommendedSpan}x{MaxRecommendedSpan}) — NavMesh bake cost and painted-object " +
                    "count start compounding past this, and it's untested territory for the map-editor/Tilemap pipeline.", this);
            }
        }
#endif
    }
}
