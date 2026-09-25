using System.Collections.Generic;
using Gnomes.Core.Models;
using Godot;

namespace SockGang.Rendering
{
    /// <summary>An instantiated GMDL model: node lookup by name, markers, tinting and visibility.</summary>
    public partial class ModelInstance : Node3D
    {
        public ModelData Model;
        readonly Dictionary<string, Node3D> nodes = new Dictionary<string, Node3D>();
        readonly List<(string name, Node3D node)> all = new List<(string, Node3D)>();
        readonly List<(MeshInstance3D mi, NodeData data)> meshes = new List<(MeshInstance3D, NodeData)>();

        public IReadOnlyList<(MeshInstance3D mi, NodeData data)> Meshes => meshes;

        internal void Register(string name, Node3D node, NodeData data)
        {
            if (!nodes.ContainsKey(name)) nodes[name] = node;
            all.Add((name, node));
            if (node is MeshInstance3D mi) meshes.Add((mi, data));
        }

        public Node3D Node(string name) => nodes.TryGetValue(name, out var n) ? n : null;

        /// <summary>All nodes with the given marker prefix ("SURF", "ZONE", "MECH", ...), in model order.</summary>
        public List<Node3D> Markers(string prefix)
        {
            var list = new List<Node3D>();
            string p = prefix + "_";
            foreach (var (name, node) in all) if (name.StartsWith(p)) list.Add(node);
            return list;
        }

        public string NameOf(Node3D node)
        {
            foreach (var (name, n) in all) if (n == node) return name;
            return node.Name;
        }

        /// <summary>Recolour every tint submesh (hat colours).</summary>
        public void SetTint(Color c)
        {
            var mat = ModelLibrary.TintMaterial(c);
            foreach (var (mi, data) in meshes)
                for (int i = 0; i < data.Meshes.Length; i++)
                    if (data.Meshes[i].Kind == MeshKind.Tint) mi.SetSurfaceOverrideMaterial(i, mat);
        }

        public void SetShadows(bool cast)
        {
            foreach (var (mi, _) in meshes) mi.CastShadow = cast ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
        }

        public void SetMeshesVisible(bool v)
        {
            foreach (var (mi, _) in meshes) mi.Visible = v;
        }
    }
}
