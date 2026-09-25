using System.Collections.Generic;
using Gnomes.Core;
using Gnomes.Core.Models;
using UnityEngine;

namespace Gnomes.Rendering
{
    /// <summary>
    /// Loads GMDL models exported from Blender (Resources/Models/*.bytes) and turns them into
    /// GameObject hierarchies. Meshes and materials are cached and shared between instances.
    /// Node name prefixes: COL_ (box collider), ZONE_ (trigger), SURF_/ANCHOR_/LIGHT_/SPAWN_ (markers),
    /// MECH_ (movable part: doors, buttons).
    /// </summary>
    public static class ModelLibrary
    {
        static readonly Dictionary<string, ModelData> models = new Dictionary<string, ModelData>();
        static readonly Dictionary<(string, int), Mesh> meshes = new Dictionary<(string, int), Mesh>();
        static readonly Dictionary<int, Material> tintMaterials = new Dictionary<int, Material>();
        static PaletteData palette;
        static Texture2D paletteTex;
        static Material litMat, emitMat, glassMat, tintBase;

        public static Texture2D PaletteTexture { get { EnsureMaterials(); return paletteTex; } }
        public static Material LitMaterial { get { EnsureMaterials(); return litMat; } }
        public static Material EmissiveMaterial { get { EnsureMaterials(); return emitMat; } }
        public static Material GlassMaterial { get { EnsureMaterials(); return glassMat; } }

        static readonly Dictionary<int, Material> glowMaterials = new Dictionary<int, Material>();

        /// <summary>Shared self-lit material (sparkles, stars): stays visible in the dark house.</summary>
        public static Material GlowMaterial(Color32 c)
        {
            int key = (c.r << 16) | (c.g << 8) | c.b;
            if (!glowMaterials.TryGetValue(key, out var m) || m == null)
            {
                m = UnlitColor(c);
                m.name = "Glow_" + key.ToString("x6");
                glowMaterials[key] = m;
            }
            return m;
        }

        /// <summary>A new flat unlit material of the given colour (sky objects, markers).</summary>
        public static Material UnlitColor(Color c)
        {
            var src = Resources.Load<Material>("Materials/GnomeUnlitColor");
            var m = src != null ? new Material(src) : new Material(FindShader("Unlit/Color", "Universal Render Pipeline/Unlit"));
            m.color = c;
            return m;
        }

        public static ModelData Get(string name)
        {
            if (models.TryGetValue(name, out var m)) return m;
            var ta = Resources.Load<TextAsset>("Models/" + name);
            if (ta == null)
            {
                Debug.LogError("[ModelLibrary] Missing model: " + name);
                return null;
            }
            m = ModelData.Parse(ta.bytes, name);
            models[name] = m;
            return m;
        }

        public static bool Exists(string name) => models.ContainsKey(name) || Resources.Load<TextAsset>("Models/" + name) != null;

        public static Shader FindShader(params string[] names)
        {
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s != null) return s;
            }
            return Shader.Find("Hidden/InternalErrorShader");
        }

        static void EnsureMaterials()
        {
            if (litMat != null) return;
            var ta = Resources.Load<TextAsset>("Models/palette");
            palette = ta != null ? PaletteData.Parse(ta.bytes) : new PaletteData { Colors = new[] { 0xff00ff } };
            int cells = PaletteData.Cells, px = 4;
            paletteTex = new Texture2D(cells * px, cells * px, TextureFormat.RGBA32, false, false)
            {
                name = "GnomePalette",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[cells * px * cells * px];
            for (int i = 0; i < cells * cells; i++)
            {
                int rgb = i < palette.Colors.Length ? palette.Colors[i] : 0xff00ff;
                var c = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
                int cx = i % cells, cy = i / cells;
                for (int y = 0; y < px; y++)
                    for (int x = 0; x < px; x++)
                        pixels[(cy * px + y) * cells * px + cx * px + x] = c;
            }
            paletteTex.SetPixels32(pixels);
            paletteTex.Apply(false, true);

            // Prefer material assets created by the editor setup (guarantees shader inclusion in builds).
            litMat = Resources.Load<Material>("Materials/GnomeLit");
            emitMat = Resources.Load<Material>("Materials/GnomeEmissive");
            glassMat = Resources.Load<Material>("Materials/GnomeGlass");
            litMat = litMat != null ? new Material(litMat) : new Material(FindShader("Standard", "Universal Render Pipeline/Lit", "Diffuse"));
            litMat.name = "GnomeLit";
            litMat.mainTexture = paletteTex;
            litMat.color = Color.white;
            if (litMat.HasProperty("_Glossiness")) litMat.SetFloat("_Glossiness", 0.12f);
            if (litMat.HasProperty("_Smoothness")) litMat.SetFloat("_Smoothness", 0.12f);
            if (litMat.HasProperty("_Metallic")) litMat.SetFloat("_Metallic", 0f);

            emitMat = emitMat != null ? new Material(emitMat) : new Material(FindShader("Unlit/Texture", "Universal Render Pipeline/Unlit"));
            emitMat.name = "GnomeEmissive";
            emitMat.mainTexture = paletteTex;

            glassMat = glassMat != null ? new Material(glassMat) : new Material(FindShader("Legacy Shaders/Transparent/Diffuse", "Transparent/Diffuse", "Standard"));
            glassMat.name = "GnomeGlass";
            glassMat.mainTexture = paletteTex;
            glassMat.color = new Color(1f, 1f, 1f, 0.35f);
            glassMat.renderQueue = 3000;

            tintBase = new Material(litMat) { name = "GnomeTint", mainTexture = null };
        }

        public static Material TintMaterial(Color32 c)
        {
            EnsureMaterials();
            int key = (c.r << 16) | (c.g << 8) | c.b;
            if (!tintMaterials.TryGetValue(key, out var m))
            {
                m = new Material(tintBase) { name = "Tint_" + key.ToString("x6"), color = c };
                tintMaterials[key] = m;
            }
            return m;
        }

        public static Material MaterialFor(MeshKind kind, int rgb)
        {
            EnsureMaterials();
            switch (kind)
            {
                case MeshKind.Emissive: return emitMat;
                case MeshKind.Glass: return glassMat;
                case MeshKind.Tint: return TintMaterial(new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255));
                default: return litMat;
            }
        }

        static Mesh BuildMesh(ModelData model, int nodeIndex)
        {
            var key = (model.Name, nodeIndex);
            if (meshes.TryGetValue(key, out var mesh)) return mesh;
            var node = model.Nodes[nodeIndex];
            int total = 0;
            foreach (var s in node.Meshes) total += s.VertexCount;
            var verts = new Vector3[total];
            var norms = new Vector3[total];
            var uvs = new Vector2[total];
            int o = 0;
            foreach (var s in node.Meshes)
            {
                for (int i = 0; i < s.VertexCount; i++)
                {
                    verts[o + i] = new Vector3(s.Positions[i * 3], s.Positions[i * 3 + 1], s.Positions[i * 3 + 2]);
                    norms[o + i] = new Vector3(s.Normals[i * 3], s.Normals[i * 3 + 1], s.Normals[i * 3 + 2]);
                    uvs[o + i] = new Vector2(s.Uvs[i * 2], s.Uvs[i * 2 + 1]);
                }
                o += s.VertexCount;
            }
            mesh = new Mesh { name = model.Name + "/" + node.Name };
            if (total > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.subMeshCount = node.Meshes.Length;
            o = 0;
            for (int si = 0; si < node.Meshes.Length; si++)
            {
                var s = node.Meshes[si];
                var idx = new int[s.Indices.Length];
                for (int k = 0; k < idx.Length; k++) idx[k] = s.Indices[k] + o;
                mesh.SetTriangles(idx, si, false);
                o += s.VertexCount;
            }
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            meshes[key] = mesh;
            return mesh;
        }

        /// <summary>Instantiate a model hierarchy. Colliders use <paramref name="colliderLayer"/>.</summary>
        public static ModelInstance Instantiate(string name, Transform parent = null, int colliderLayer = 0, bool withColliders = true)
        {
            var model = Get(name);
            if (model == null)
            {
                var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallback.name = "MISSING_" + name;
                if (parent) fallback.transform.SetParent(parent, false);
                return fallback.AddComponent<ModelInstance>();
            }
            var transforms = new Transform[model.Nodes.Length];
            ModelInstance inst = null;
            for (int i = 0; i < model.Nodes.Length; i++)
            {
                var n = model.Nodes[i];
                var go = new GameObject(n.Name);
                var t = go.transform;
                if (i == 0)
                {
                    if (parent) t.SetParent(parent, false);
                    inst = go.AddComponent<ModelInstance>();
                    inst.Model = model;
                }
                else
                {
                    t.SetParent(transforms[n.Parent], false);
                    t.localPosition = n.Position.ToUnity();
                    t.localRotation = n.Rotation.ToUnity();
                    t.localScale = n.Scale.ToUnity();
                }
                transforms[i] = t;
                string prefix = n.Prefix;
                if (prefix == "COL")
                {
                    if (withColliders)
                    {
                        go.layer = colliderLayer;
                        go.AddComponent<BoxCollider>();
                    }
                }
                else if (prefix == "ZONE")
                {
                    go.layer = Layers.Trigger;
                    var bc = go.AddComponent<BoxCollider>();
                    bc.isTrigger = true;
                }
                if (n.Meshes.Length > 0)
                {
                    go.layer = colliderLayer == Layers.Default ? Layers.Default : colliderLayer;
                    var mf = go.AddComponent<MeshFilter>();
                    mf.sharedMesh = BuildMesh(model, i);
                    var mr = go.AddComponent<MeshRenderer>();
                    var mats = new Material[n.Meshes.Length];
                    for (int s = 0; s < mats.Length; s++) mats[s] = MaterialFor(n.Meshes[s].Kind, n.Meshes[s].Rgb);
                    mr.sharedMaterials = mats;
                    bool glassOnly = mats.Length == 1 && n.Meshes[0].Kind == MeshKind.Glass;
                    mr.shadowCastingMode = glassOnly ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
            inst.Init(transforms);
            return inst;
        }
    }

    public static class CoreConvert
    {
        public static Vector3 ToUnity(this V3 v) => new Vector3(v.x, v.y, v.z);
        public static Quaternion ToUnity(this Q4 q) => new Quaternion(q.x, q.y, q.z, q.w);
        public static V3 ToCore(this Vector3 v) => new V3(v.x, v.y, v.z);
        public static Q4 ToCore(this Quaternion q) => new Q4(q.x, q.y, q.z, q.w);
        public static Color32 RgbToColor(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
