using System.Collections.Generic;
using UnityEngine;

namespace Jolly.XR.Navigation
{
    /// <summary>A flat floor mesh from a plane-space outline (x, y → local x, z), front faces up.</summary>
    public static class FloorMesh
    {
        /// <summary>Rewrites mesh; vertices and triangles are reusable scratch lists. False (mesh untouched) if the
        /// outline does not triangulate.</summary>
        public static bool TryBuild(IReadOnlyList<Vector2> outline, Mesh mesh, List<Vector3> vertices, List<int> triangles)
        {
            if (!PolygonTriangulator.Triangulate(outline, triangles))
                return false;

            vertices.Clear();
            for (var i = 0; i < outline.Count; i++)
                vertices.Add(new Vector3(outline[i].x, 0f, outline[i].y));
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return true;
        }
    }
}
