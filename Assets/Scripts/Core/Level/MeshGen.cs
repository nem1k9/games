using System.Collections.Generic;

namespace Gnomes.Core.Level
{
    /// <summary>Engine-free mesh data (Unity conventions: left-handed, front = Cross(v1-v0, v2-v0) along the normal).</summary>
    public sealed class MeshArrays
    {
        public readonly List<V3> Positions = new List<V3>();
        public readonly List<V3> Normals = new List<V3>();
        public readonly List<float> Uvs = new List<float>(); // u,v pairs
        public readonly List<List<int>> SubMeshes = new List<List<int>>();

        public MeshArrays(int submeshes)
        {
            for (int i = 0; i < submeshes; i++) SubMeshes.Add(new List<int>());
        }

        /// <summary>Adds a quad centred at c spanning right*w and up*h, facing <paramref name="normal"/>.</summary>
        public void Quad(int sub, V3 c, V3 right, V3 up, V3 normal, float w, float h, float u0, float v0, float tile)
        {
            int i = Positions.Count;
            var r = right * (w / 2);
            var u = up * (h / 2);
            Positions.Add(c - r - u);
            Positions.Add(c + r - u);
            Positions.Add(c + r + u);
            Positions.Add(c - r + u);
            for (int k = 0; k < 4; k++) Normals.Add(normal);
            Uvs.Add(u0 / tile); Uvs.Add(v0 / tile);
            Uvs.Add((u0 + w) / tile); Uvs.Add(v0 / tile);
            Uvs.Add((u0 + w) / tile); Uvs.Add((v0 + h) / tile);
            Uvs.Add(u0 / tile); Uvs.Add((v0 + h) / tile);
            var tris = SubMeshes[sub];
            // (0,1,2) produces Cross(right, up); (0,2,1) the opposite direction.
            if (V3.Dot(V3.Cross(right, up), normal) > 0)
            {
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
            else
            {
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
            }
        }
    }

    public static class MeshGen
    {
        /// <summary>
        /// Wall slab centred on the origin: local X along the wall, Y up, Z through the wall.
        /// Submesh 0 = +Z side, 1 = -Z side, 2 = edges. UVs are in world units / tile.
        /// </summary>
        public static MeshArrays WallSlab(V3 size, float t0, float y0, float tile)
        {
            var m = new MeshArrays(3);
            var h = size * 0.5f;
            var X = new V3(1, 0, 0);
            var Y = new V3(0, 1, 0);
            var Z = new V3(0, 0, 1);
            m.Quad(0, new V3(0, 0, h.z), -X, Y, Z, size.x, size.y, -(t0 + size.x), y0, tile);
            m.Quad(1, new V3(0, 0, -h.z), X, Y, -Z, size.x, size.y, t0, y0, tile);
            m.Quad(2, new V3(h.x, 0, 0), Z, Y, X, size.z, size.y, 0, y0, tile);
            m.Quad(2, new V3(-h.x, 0, 0), -Z, Y, -X, size.z, size.y, 0, y0, tile);
            m.Quad(2, new V3(0, h.y, 0), X, Z, Y, size.x, size.z, t0, 0, tile);
            m.Quad(2, new V3(0, -h.y, 0), X, -Z, -Y, size.x, size.z, t0, 0, tile);
            return m;
        }
    }
}
