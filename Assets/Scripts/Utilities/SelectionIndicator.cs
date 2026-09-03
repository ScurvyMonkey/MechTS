using UnityEngine;

namespace MechTS.Utilities
{
    /// <summary>
    /// Shared runtime-created building-selection highlight — a flat golden hollow ring
    /// (see <see cref="RingMesh"/>). Used by <see cref="MechTS.Units.SelectionController"/>
    /// as one shared indicator reparented onto whichever building is currently selected.
    /// Units show faction/selection via their own merged ring instead
    /// (<see cref="MechTS.Units.UnitBase"/>), not this one.
    /// </summary>
    public static class SelectionIndicator
    {
        private static readonly Vector3 LocalPosition = new Vector3(0f, 0.02f, 0f);
        private static readonly Color RingColor = new Color(1f, 0.85f, 0.2f);
        private const float InnerRadius = 1.0f;
        private const float OuterRadius = 1.2f;

        private static Material _sharedMaterial;

        /// <summary>
        /// Creates a new selection ring parented under the given transform, initially inactive.
        /// </summary>
        /// <param name="parent">The transform to parent the ring under.</param>
        public static GameObject Create(Transform parent)
        {
            var ring = new GameObject("SelectionIndicator");
            ring.AddComponent<MeshFilter>().mesh = RingMesh.Create(InnerRadius, OuterRadius);

            var renderer = ring.AddComponent<MeshRenderer>();
            renderer.material = GetMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            AttachTo(ring, parent);
            ring.SetActive(false);
            return ring;
        }

        /// <summary>
        /// Reparents an existing ring (e.g. one shared indicator reused across buildings) under
        /// a new target and resets its local position/scale.
        /// </summary>
        /// <param name="ring">The ring GameObject to reparent.</param>
        /// <param name="parent">The transform to parent it under.</param>
        public static void AttachTo(GameObject ring, Transform parent)
        {
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = LocalPosition;
            ring.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Returns the shared unlit golden material used by every building selection ring,
        /// creating it once.
        /// </summary>
        private static Material GetMaterial()
        {
            if (_sharedMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                _sharedMaterial = new Material(shader) { name = "SelectionIndicatorMaterial" };
                _sharedMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _sharedMaterial.SetColor("_BaseColor", RingColor);
            }
            return _sharedMaterial;
        }
    }
}
