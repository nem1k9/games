using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Gnomes.Core.Models
{
    /// <summary>Material kinds exported by the Blender pipeline.</summary>
    public enum MeshKind : byte
    {
        Lit = (byte)'C',
        Emissive = (byte)'E',
        Glass = (byte)'G',
        Tint = (byte)'T',
    }

    public sealed class SubMeshData
    {
        public MeshKind Kind;
        public int Rgb;
        public float[] Positions; // xyz * n
        public float[] Normals; // xyz * n
        public float[] Uvs; // uv * n
        public int[] Indices;
        public int VertexCount => Positions.Length / 3;
    }

    public sealed class NodeData
    {
        public string Name;
        public int Parent;
        public V3 Position;
        public Q4 Rotation;
        public V3 Scale;
        public SubMeshData[] Meshes;
        public Dictionary<string, string> Props = new Dictionary<string, string>();

        /// <summary>Marker prefix, e.g. "COL", "ZONE", "SURF", "ANCHOR", "LIGHT", "MECH" or null.</summary>
        public string Prefix
        {
            get
            {
                int i = Name.IndexOf('_');
                if (i <= 0) return null;
                string p = Name.Substring(0, i);
                return p == "COL" || p == "ZONE" || p == "SURF" || p == "ANCHOR" || p == "LIGHT" || p == "MECH" || p == "SPAWN" ? p : null;
            }
        }

        /// <summary>Name without the marker prefix.</summary>
        public string BareName => Prefix == null ? Name : Name.Substring(Prefix.Length + 1);
    }

    /// <summary>Parsed GMDL model (see blender/gnomelib/export_gmdl.py).</summary>
    public sealed class ModelData
    {
        public const int SupportedVersion = 1;
        public string Name;
        public NodeData[] Nodes;

        public NodeData Root => Nodes[0];

        public NodeData Find(string name)
        {
            foreach (var n in Nodes) if (n.Name == name) return n;
            return null;
        }

        public IEnumerable<NodeData> WithPrefix(string prefix)
        {
            foreach (var n in Nodes) if (n.Prefix == prefix) yield return n;
        }

        public int TriangleCount
        {
            get
            {
                int t = 0;
                foreach (var n in Nodes) foreach (var m in n.Meshes) t += m.Indices.Length / 3;
                return t;
            }
        }

        public static ModelData Parse(byte[] data, string name = null)
        {
            using (var ms = new MemoryStream(data))
            using (var r = new BinaryReader(ms))
            {
                var magic = Encoding.ASCII.GetString(r.ReadBytes(4));
                if (magic != "GMDL") throw new InvalidDataException("Not a GMDL file: " + name);
                int ver = r.ReadInt32();
                if (ver != SupportedVersion) throw new InvalidDataException($"Unsupported GMDL version {ver} in {name}");
                int count = r.ReadInt32();
                if (count <= 0 || count > 100000) throw new InvalidDataException("Bad node count");
                var nodes = new NodeData[count];
                for (int i = 0; i < count; i++)
                {
                    var n = new NodeData();
                    n.Name = ReadString(r);
                    n.Parent = r.ReadInt32();
                    if (n.Parent >= i) throw new InvalidDataException($"Node {n.Name} parent {n.Parent} not before it");
                    n.Position = ReadV3(r);
                    n.Rotation = new Q4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    n.Scale = ReadV3(r);
                    int subCount = r.ReadInt32();
                    n.Meshes = new SubMeshData[subCount];
                    for (int s = 0; s < subCount; s++)
                    {
                        var m = new SubMeshData();
                        m.Kind = (MeshKind)r.ReadByte();
                        m.Rgb = r.ReadInt32();
                        int vc = r.ReadInt32();
                        m.Positions = ReadFloats(r, vc * 3);
                        m.Normals = ReadFloats(r, vc * 3);
                        m.Uvs = ReadFloats(r, vc * 2);
                        int ic = r.ReadInt32();
                        m.Indices = new int[ic];
                        for (int k = 0; k < ic; k++) m.Indices[k] = r.ReadInt32();
                        n.Meshes[s] = m;
                    }
                    int pc = r.ReadInt32();
                    for (int k = 0; k < pc; k++)
                    {
                        string key = ReadString(r);
                        n.Props[key] = ReadString(r);
                    }
                    nodes[i] = n;
                }
                return new ModelData { Name = name ?? nodes[0].Name, Nodes = nodes };
            }
        }

        static string ReadString(BinaryReader r)
        {
            int len = r.ReadInt32();
            return Encoding.UTF8.GetString(r.ReadBytes(len));
        }

        static V3 ReadV3(BinaryReader r) => new V3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        static float[] ReadFloats(BinaryReader r, int n)
        {
            var a = new float[n];
            var bytes = r.ReadBytes(n * 4);
            Buffer.BlockCopy(bytes, 0, a, 0, bytes.Length);
            return a;
        }

        /// <summary>Local-to-model transform of a node (composes parents).</summary>
        public void NodeToModel(int index, out V3 pos, out Q4 rot, out V3 scale)
        {
            var n = Nodes[index];
            if (n.Parent < 0)
            {
                pos = V3.zero; rot = Q4.identity; scale = V3.one; // root transform is ignored (placed by the game)
                return;
            }
            NodeToModel(n.Parent, out var pp, out var pr, out var ps);
            pos = pp + pr * new V3(n.Position.x * ps.x, n.Position.y * ps.y, n.Position.z * ps.z);
            rot = pr * n.Rotation;
            scale = new V3(ps.x * n.Scale.x, ps.y * n.Scale.y, ps.z * n.Scale.z);
        }

        /// <summary>Model-space bounds of all visible geometry.</summary>
        public void Bounds(out V3 min, out V3 max)
        {
            min = new V3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new V3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < Nodes.Length; i++)
            {
                var n = Nodes[i];
                if (n.Meshes.Length == 0) continue;
                NodeToModel(i, out var p, out var q, out var s);
                foreach (var m in n.Meshes)
                {
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        var lp = new V3(m.Positions[v * 3] * s.x, m.Positions[v * 3 + 1] * s.y, m.Positions[v * 3 + 2] * s.z);
                        var w = p + q * lp;
                        min = V3.Min(min, w);
                        max = V3.Max(max, w);
                    }
                }
            }
        }
    }

    /// <summary>Global colour palette used by all models (16x16 cells).</summary>
    public sealed class PaletteData
    {
        public const int Cells = 16;
        public int[] Colors;

        public static PaletteData Parse(byte[] data)
        {
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                var magic = Encoding.ASCII.GetString(r.ReadBytes(4));
                if (magic != "GPAL") throw new InvalidDataException("Not a palette file");
                int n = r.ReadInt32();
                var c = new int[n];
                for (int i = 0; i < n; i++) c[i] = r.ReadInt32();
                return new PaletteData { Colors = c };
            }
        }
    }
}
