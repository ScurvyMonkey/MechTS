using UnityEngine;

namespace MechTS.Utilities
{
    /// <summary>
    /// Generates flat annulus (hollow ring) meshes lying in the local XZ plane, normal facing
    /// +Y — already oriented for this project's top-down camera without needing the sprite
    /// art's (90,0,0) tilt trick. Used for faction/selection indicators so they read as a
    /// clean hollow ring around a unit rather than a filled disc underneath it.
    /// </summary>
    public static class RingMesh
    {
        /// <summary>
        /// Creates a new ring mesh between the given inner and outer radii.
        /// </summary>
        /// <param name="innerRadius">The ring's inner (hole) radius.</param>
        /// <param name="outerRadius">The ring's outer radius.</param>
        /// <param name="segments">How many segments to build the circle from.</param>
        public static Mesh Create(float innerRadius, float outerRadius, int segments = 32)
        {
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);

                vertices[i * 2] = new Vector3(x * innerRadius, 0f, z * innerRadius);
                vertices[i * 2 + 1] = new Vector3(x * outerRadius, 0f, z * outerRadius);

                int next = (i + 1) % segments;
                int a = i * 2;
                int b = i * 2 + 1;
                int c = next * 2;
                int d = next * 2 + 1;

                int t = i * 6;
                triangles[t] = a;
                triangles[t + 1] = b;
                triangles[t + 2] = c;
                triangles[t + 3] = b;
                triangles[t + 4] = d;
                triangles[t + 5] = c;
            }

            var mesh = new Mesh { name = "RingMesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
