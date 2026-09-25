using System.Collections.Generic;
using Gnomes.Core;
using Gnomes.Core.Models;
using Godot;

namespace SockGang.Rendering
{
    public enum ColliderMode
    {
        None, // visuals only
        Static, // furniture / scenery: StaticBody3D (moving MECH parts get AnimatableBody3D)
    }

    /// <summary>
    /// Loads GMDL models exported from Blender (res://models/*.bytes) and builds Godot node trees.
    /// Meshes and materials are cached and shared. Marker node prefixes: COL_ (box collider, scale = size),
    /// ZONE_ (trigger box), SURF_/ANCHOR_/LIGHT_/SPAWN_ (points), MECH_ (movable part).
    /// </summary>
    public static class ModelLibrary
    {
        static readonly Dictionary<string, ModelData> models = new Dictionary<string, ModelData>();
        static readonly Dictionary<(string, int), ArrayMesh> meshes = new Dictionary<(string, int), ArrayMesh>();
        static readonly Dictionary<int, StandardMaterial3D> tints = new Dictionary<int, StandardMaterial3D>();
        static readonly Dictionary<int, StandardMaterial3D> glows = new Dictionary<int, StandardMaterial3D>();
        static ImageTexture paletteTex;
        static StandardMaterial3D litMat, emitMat, glassMat;

        public static ModelData Get(string name)
        {
            if (models.TryGetValue(name, out var m)) return m;
            string path = "res://models/" + name + ".bytes";
            if (!FileAccess.FileExists(path))
            {
                GD.PushError("[ModelLibrary] Missing model: " + name);
                models[name] = null;
                return null;
            }
            m = ModelData.Parse(FileAccess.GetFileAsBytes(path), name);
            models[name] = m;
            return m;
        }

        public static bool Exists(string name) => models.TryGetValue(name, out var m) ? m != null : FileAccess.FileExists("res://models/" + name + ".bytes");

        // ------------------------------------------------------------------ materials

        static void EnsureMaterials()
        {
            if (litMat != null) return;
            PaletteData palette;
            var path = "res://models/palette.bytes";
            palette = FileAccess.FileExists(path) ? PaletteData.Parse(FileAccess.GetFileAsBytes(path)) : new PaletteData { Colors = new[] { 0xff00ff } };
            int cells = PaletteData.Cells, px = 4, size = cells * px;
            var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            for (int i = 0; i < cells * cells; i++)
            {
                int rgb = i < palette.Colors.Length ? palette.Colors[i] : 0xff00ff;
                var c = Conv.C(rgb);
                int cx = i % cells, cy = i / cells; // cy counts from the bottom (Unity/Blender UV space)
                for (int y = 0; y < px; y++)
                    for (int x = 0; x < px; x++)
                        img.SetPixel(cx * px + x, size - 1 - (cy * px + y), c);
            }
            paletteTex = ImageTexture.CreateFromImage(img);

            litMat = new StandardMaterial3D
            {
                ResourceName = "GnomeLit",
                AlbedoTexture = paletteTex,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
                Roughness = 0.88f,
                Metallic = 0f,
            };
            emitMat = new StandardMaterial3D
            {
                ResourceName = "GnomeEmissive",
                AlbedoTexture = paletteTex,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            glassMat = new StandardMaterial3D
            {
                ResourceName = "GnomeGlass",
                AlbedoTexture = paletteTex,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
                AlbedoColor = new Color(1, 1, 1, 0.35f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.1f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
        }

        public static StandardMaterial3D LitMaterial { get { EnsureMaterials(); return litMat; } }
        public static Texture2D PaletteTexture { get { EnsureMaterials(); return paletteTex; } }

        /// <summary>Shared plain-colour lit material (hats, trims, simple shapes).</summary>
        public static StandardMaterial3D TintMaterial(Color c)
        {
            int key = unchecked((int)c.ToRgba32());
            if (!tints.TryGetValue(key, out var m))
            {
                m = new StandardMaterial3D { AlbedoColor = c, Roughness = 0.85f };
                tints[key] = m;
            }
            return m;
        }

        /// <summary>Shared self-lit material (sparkles, stars, screens): visible in the dark.</summary>
        public static StandardMaterial3D GlowMaterial(Color c)
        {
            int key = unchecked((int)c.ToRgba32());
            if (!glows.TryGetValue(key, out var m))
            {
                m = new StandardMaterial3D { AlbedoColor = c, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
                glows[key] = m;
            }
            return m;
        }

        /// <summary>A new (unshared) unlit material, e.g. for a flickering TV screen.</summary>
        public static StandardMaterial3D UnlitColor(Color c) => new StandardMaterial3D { AlbedoColor = c, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };

        public static Material MaterialFor(MeshKind kind, int rgb)
        {
            EnsureMaterials();
            switch (kind)
            {
                case MeshKind.Emissive: return emitMat;
                case MeshKind.Glass: return glassMat;
                case MeshKind.Tint: return TintMaterial(Conv.C(rgb));
                default: return litMat;
            }
        }

        // ------------------------------------------------------------------ meshes

        /// <summary>Godot mesh of one node (one surface per GMDL submesh), Z mirrored and winding flipped.</summary>
        public static ArrayMesh BuildMesh(ModelData model, int nodeIndex)
        {
            var key = (model.Name, nodeIndex);
            if (meshes.TryGetValue(key, out var mesh)) return mesh;
            var node = model.Nodes[nodeIndex];
            mesh = new ArrayMesh { ResourceName = model.Name + "/" + node.Name };
            foreach (var s in node.Meshes)
            {
                int n = s.VertexCount;
                var verts = new Vector3[n];
                var norms = new Vector3[n];
                var uvs = new Vector2[n];
                for (int i = 0; i < n; i++)
                {
                    verts[i] = new Vector3(s.Positions[i * 3], s.Positions[i * 3 + 1], -s.Positions[i * 3 + 2]);
                    norms[i] = new Vector3(s.Normals[i * 3], s.Normals[i * 3 + 1], -s.Normals[i * 3 + 2]);
                    uvs[i] = new Vector2(s.Uvs[i * 2], 1f - s.Uvs[i * 2 + 1]);
                }
                var idx = new int[s.Indices.Length];
                for (int t = 0; t + 2 < idx.Length; t += 3)
                {
                    idx[t] = s.Indices[t];
                    idx[t + 1] = s.Indices[t + 2];
                    idx[t + 2] = s.Indices[t + 1];
                }
                var arrays = new Godot.Collections.Array();
                arrays.Resize((int)Mesh.ArrayType.Max);
                arrays[(int)Mesh.ArrayType.Vertex] = verts;
                arrays[(int)Mesh.ArrayType.Normal] = norms;
                arrays[(int)Mesh.ArrayType.TexUV] = uvs;
                arrays[(int)Mesh.ArrayType.Index] = idx;
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, MaterialFor(s.Kind, s.Rgb));
            }
            meshes[key] = mesh;
            return mesh;
        }

        /// <summary>Model-space (Godot) transform of a node, scale separated.</summary>
        public static Transform3D NodeTransform(ModelData m, int index, out Vector3 scale)
        {
            m.NodeToModel(index, out var p, out var q, out var s);
            scale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            return new Transform3D(new Basis(q.G().Normalized()), p.G());
        }

        /// <summary>COL_ boxes of a model in model space, excluding those inside MECH parts.</summary>
        public static List<(Transform3D xf, Vector3 size)> ColliderBoxes(ModelData m, bool includeMechParts = false)
        {
            var list = new List<(Transform3D, Vector3)>();
            for (int i = 0; i < m.Nodes.Length; i++)
            {
                if (m.Nodes[i].Prefix != "COL") continue;
                if (!includeMechParts && InsideMech(m, i) >= 0) continue;
                var xf = NodeTransform(m, i, out var size);
                list.Add((xf, size));
            }
            return list;
        }

        static int InsideMech(ModelData m, int i)
        {
            for (int p = m.Nodes[i].Parent; p >= 0; p = m.Nodes[p].Parent)
                if (m.Nodes[p].Prefix == "MECH") return p;
            return -1;
        }

        /// <summary>Points of all visible geometry (model space, Godot) for convex hulls.</summary>
        public static Vector3[] HullPoints(ModelData m)
        {
            var pts = new List<Vector3>();
            var seen = new HashSet<(int, int, int)>();
            for (int i = 0; i < m.Nodes.Length; i++)
            {
                var n = m.Nodes[i];
                if (n.Meshes.Length == 0) continue;
                m.NodeToModel(i, out var p, out var q, out var s);
                foreach (var sm in n.Meshes)
                {
                    if (sm.Kind == MeshKind.Emissive && n.Meshes.Length > 1) continue;
                    for (int v = 0; v < sm.VertexCount; v++)
                    {
                        var lp = new V3(sm.Positions[v * 3] * s.x, sm.Positions[v * 3 + 1] * s.y, sm.Positions[v * 3 + 2] * s.z);
                        var w = (p + q * lp).G();
                        if (seen.Add((Mathf.RoundToInt(w.X * 100), Mathf.RoundToInt(w.Y * 100), Mathf.RoundToInt(w.Z * 100)))) pts.Add(w);
                    }
                }
            }
            if (pts.Count < 4) pts.AddRange(new[] { new Vector3(-0.05f, -0.02f, -0.05f), new Vector3(0.05f, 0.02f, 0.05f), new Vector3(0.05f, -0.02f, -0.05f), new Vector3(-0.05f, 0.02f, 0.05f) });
            return pts.ToArray();
        }

        // ------------------------------------------------------------------ instancing

        /// <summary>Build the node tree of a model under parent. Static colliders go on <paramref name="layer"/>.</summary>
        public static ModelInstance Instantiate(string name, Node parent, ColliderMode colliders = ColliderMode.Static, uint layer = Layers.World)
        {
            var model = Get(name);
            var inst = new ModelInstance { Name = name };
            parent?.AddChild(inst);
            if (model == null)
            {
                var mi = new MeshInstance3D { Mesh = new BoxMesh(), Name = "MISSING" };
                inst.AddChild(mi);
                return inst;
            }
            inst.Model = model;
            var nodes = new Node3D[model.Nodes.Length];
            nodes[0] = inst;
            inst.Register(model.Nodes[0].Name, inst, model.Nodes[0]);
            if (model.Nodes[0].Meshes.Length > 0)
            {
                // geometry on the root node (most scenery models): a mesh child at the origin
                var rootMesh = new MeshInstance3D { Name = model.Nodes[0].Name + "_mesh", Mesh = BuildMesh(model, 0) };
                inst.AddChild(rootMesh);
                inst.Register(model.Nodes[0].Name + "_mesh", rootMesh, model.Nodes[0]);
            }
            for (int i = 1; i < model.Nodes.Length; i++)
            {
                var n = model.Nodes[i];
                Node3D go;
                if (n.Meshes.Length > 0)
                {
                    var mi = new MeshInstance3D { Mesh = BuildMesh(model, i) };
                    bool glassOnly = n.Meshes.Length == 1 && n.Meshes[0].Kind == MeshKind.Glass;
                    if (glassOnly) mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                    go = mi;
                }
                else go = new Node3D();
                go.Name = n.Name;
                go.Position = n.Position.G();
                go.Quaternion = n.Rotation.G().Normalized();
                go.Scale = new Vector3(n.Scale.x, n.Scale.y, n.Scale.z);
                nodes[n.Parent].AddChild(go);
                nodes[i] = go;
                inst.Register(n.Name, go, n);
            }
            if (colliders == ColliderMode.Static) AddStaticColliders(inst, model, nodes, layer);
            return inst;
        }

        static void AddStaticColliders(ModelInstance inst, ModelData model, Node3D[] nodes, uint layer)
        {
            StaticBody3D body = null;
            var mechBodies = new Dictionary<int, AnimatableBody3D>();
            for (int i = 0; i < model.Nodes.Length; i++)
            {
                if (model.Nodes[i].Prefix != "COL") continue;
                int mech = InsideMech(model, i);
                CollisionObject3D owner;
                Transform3D xf;
                Vector3 size;
                if (mech < 0)
                {
                    if (body == null)
                    {
                        body = new StaticBody3D { Name = "Colliders", CollisionLayer = layer, CollisionMask = 0 };
                        inst.AddChild(body);
                    }
                    owner = body;
                    xf = NodeTransform(model, i, out size);
                }
                else
                {
                    if (!mechBodies.TryGetValue(mech, out var ab))
                    {
                        ab = new AnimatableBody3D { Name = "MechBody", CollisionLayer = Layers.Mech, CollisionMask = 0, SyncToPhysics = false };
                        nodes[mech].AddChild(ab);
                        mechBodies[mech] = ab;
                    }
                    owner = ab;
                    // COL transform relative to its MECH node
                    var mechXf = NodeTransform(model, mech, out var mechScale);
                    var colXf = NodeTransform(model, i, out size);
                    xf = mechXf.AffineInverse() * colXf;
                    size = new Vector3(size.X / Mathf.Max(1e-4f, mechScale.X), size.Y / Mathf.Max(1e-4f, mechScale.Y), size.Z / Mathf.Max(1e-4f, mechScale.Z));
                }
                var cs = new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(Mathf.Max(0.01f, size.X), Mathf.Max(0.01f, size.Y), Mathf.Max(0.01f, size.Z)) }, Transform = xf };
                owner.AddChild(cs);
            }
        }

        /// <summary>Forget cached assets (tests / level reloads keep them; nothing to do normally).</summary>
        public static void Clear()
        {
            models.Clear();
            meshes.Clear();
        }
    }
}
