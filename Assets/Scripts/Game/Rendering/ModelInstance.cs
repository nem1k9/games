using System.Collections.Generic;
using Gnomes.Core.Models;
using UnityEngine;

namespace Gnomes.Rendering
{
    /// <summary>Runtime handle to an instantiated GMDL model: node lookup, markers, tinting.</summary>
    public class ModelInstance : MonoBehaviour
    {
        public ModelData Model;
        readonly Dictionary<string, Transform> nodes = new Dictionary<string, Transform>();
        readonly List<Renderer> renderers = new List<Renderer>();

        public IReadOnlyList<Renderer> Renderers => renderers;

        public void Init(Transform[] transforms)
        {
            nodes.Clear();
            renderers.Clear();
            foreach (var t in transforms)
            {
                if (!nodes.ContainsKey(t.name)) nodes[t.name] = t;
                var r = t.GetComponent<Renderer>();
                if (r) renderers.Add(r);
            }
        }

        public Transform Node(string name)
        {
            nodes.TryGetValue(name, out var t);
            return t;
        }

        /// <summary>All nodes with the given marker prefix (e.g. "SURF", "ZONE", "MECH").</summary>
        public List<Transform> Markers(string prefix)
        {
            var list = new List<Transform>();
            string p = prefix + "_";
            foreach (var kv in nodes)
                if (kv.Key.StartsWith(p)) list.Add(kv.Value);
            return list;
        }

        /// <summary>Recolour every tint submesh (player hat colour etc).</summary>
        public void SetTint(Color32 c)
        {
            if (Model == null) return;
            var mat = ModelLibrary.TintMaterial(c);
            foreach (var r in renderers)
            {
                var node = Model.Find(r.name);
                if (node == null) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length && i < node.Meshes.Length; i++)
                {
                    if (node.Meshes[i].Kind == MeshKind.Tint)
                    {
                        mats[i] = mat;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        public void SetLayerRecursive(int layer)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        public void SetShadows(bool cast)
        {
            foreach (var r in renderers) r.shadowCastingMode = cast ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void SetVisible(bool v)
        {
            foreach (var r in renderers) r.enabled = v;
        }
    }
}
