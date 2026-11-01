using UnityEngine;

namespace Jolly.Presentation
{
    /// <summary>Unit quad centred on the origin in the XY plane, front face toward -Z, uv (0,0) bottom-left.</summary>
    static class QuadMesh
    {
        public static Mesh Create()
        {
            var mesh = new Mesh { name = "MaskQuad" };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
            });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
