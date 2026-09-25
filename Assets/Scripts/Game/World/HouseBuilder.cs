using System.Collections.Generic;
using Gnomes.Core;
using Gnomes.Core.Level;
using Gnomes.Core.Protocol;
using Gnomes.Rendering;
using UnityEngine;
using UnityEngine.AI;

namespace Gnomes.World
{
    /// <summary>Builds the old man's house (static geometry, furniture, garden) from a <see cref="HouseLayout"/>.</summary>
    public static class HouseBuilder
    {
        const float WallTile = 5f;
        const float FloorTile = 6f;

        public static void Build(GameWorld w, HouseLayout L)
        {
            w.Layout = L;
            var root = w.StaticRoot;
            float H = L.WallHeight;

            // ---------- floors & ceilings ----------
            foreach (var r in L.Rooms)
            {
                var floor = BoxObj(root, "Floor_" + r.Id, new Vector3((r.X0 + r.X1) / 2, -0.25f, (r.Z0 + r.Z1) / 2), new Vector3(r.X1 - r.X0, 0.5f, r.Z1 - r.Z0), Quaternion.identity,
                    Textures.Mat(Textures.Floor(r.Floor), r.Floor == FloorStyle.Tile || r.Floor == FloorStyle.Checker ? 0.35f : 0.12f), FloorTile, true);
                floor.layer = Layers.Default;
                BoxObj(root, "Ceiling_" + r.Id, new Vector3((r.X0 + r.X1) / 2, H + 0.15f, (r.Z0 + r.Z1) / 2), new Vector3(r.X1 - r.X0, 0.3f, r.Z1 - r.Z0), Quaternion.identity,
                    Textures.Mat(Textures.Ceiling()), FloorTile, true);
            }

            // ---------- walls ----------
            foreach (var wall in L.Walls) BuildWall(root, L, wall, H);

            // ---------- furniture ----------
            foreach (var p in L.Furniture)
            {
                if (!ModelLibrary.Exists(p.Model))
                {
                    Debug.LogWarning("[HouseBuilder] no model for " + p.Model);
                    continue;
                }
                var inst = ModelLibrary.Instantiate(p.Model, root, Layers.Default);
                inst.name = "F_" + p.Id;
                inst.transform.SetPositionAndRotation(p.Pos.U(), Quaternion.Euler(0, p.RotY * Mathf.Rad2Deg, 0));
                if (p.Model == "window") inst.transform.localScale = new Vector3(p.P("w", 1), p.P("h", 1), 1f);
                var f = inst.gameObject.AddComponent<Furniture>();
                f.Init(0, p, inst);
                w.RegisterFurniture(f);
            }

            // ---------- roof & garden ----------
            var roof = ModelLibrary.Instantiate("roof", root, Layers.Default, withColliders: false);
            roof.transform.position = new Vector3(7 * GameConsts.HS, H + 0.3f, 5 * GameConsts.HS);

            var g0 = L.WorldMin.U();
            var g1 = L.WorldMax.U();
            var ground = BoxObj(root, "Garden", new Vector3((g0.x + g1.x) / 2, -0.52f, (g0.z + g1.z) / 2), new Vector3(g1.x - g0.x + 40, 1f, g1.z - g0.z + 40), Quaternion.identity,
                Textures.Mat(Textures.Floor(FloorStyle.Grass)), 8f, true);
            ground.layer = Layers.Default;
            // foundation strip around the house so the floor edge looks solid
            foreach (var d in L.Garden)
            {
                if (!ModelLibrary.Exists(d.Model)) continue;
                var inst = ModelLibrary.Instantiate(d.Model, root, Layers.Default);
                inst.transform.SetPositionAndRotation(d.Pos.U(), Quaternion.Euler(0, d.RotY * Mathf.Rad2Deg, 0));
                inst.transform.localScale = Vector3.one * d.Scale;
                // garden lamps light up the yard
                foreach (var lm in inst.Markers("LIGHT")) AddMarkerLight(inst, lm);
            }
            var mush = ModelLibrary.Instantiate("mushroomHouse", root, Layers.Default);
            mush.transform.SetPositionAndRotation(L.Mushroom.U(), Quaternion.Euler(0, 180, 0));
            foreach (var lm in mush.Markers("LIGHT")) AddMarkerLight(mush, lm);
            w.MushroomPos = L.Mushroom.U();
            var rev = mush.Node("ANCHOR_revive");
            w.RevivePoint = rev ? rev.position : L.Mushroom.U() + Vector3.back * 2f;
            var stash = ModelLibrary.Instantiate("stashBasket", root, Layers.Default);
            stash.transform.SetPositionAndRotation(new Vector3(L.StashCenter.x, 0, L.StashCenter.z), Quaternion.Euler(0, 200, 0));
            var sz = stash.Node("ZONE_stash");
            w.StashZone = sz ? sz.GetComponent<BoxCollider>() : null;

            foreach (var s in L.Spawns) w.Spawns.Add(s.U() + Vector3.up * 0.2f);

            // invisible world bounds
            float bh = 40f;
            var mid = new Vector3((g0.x + g1.x) / 2, bh / 2 - 1, (g0.z + g1.z) / 2);
            var size = new Vector3(g1.x - g0.x, bh, g1.z - g0.z);
            InvisibleWall(root, mid + new Vector3(0, 0, size.z / 2 + 1), new Vector3(size.x + 4, bh, 2));
            InvisibleWall(root, mid - new Vector3(0, 0, size.z / 2 + 1), new Vector3(size.x + 4, bh, 2));
            InvisibleWall(root, mid + new Vector3(size.x / 2 + 1, 0, 0), new Vector3(2, bh, size.z + 4));
            InvisibleWall(root, mid - new Vector3(size.x / 2 + 1, 0, 0), new Vector3(2, bh, size.z + 4));
            w.PlayArea = new Bounds(mid, size);
            w.FloorY = 0;

            BakeNavMesh(L);
        }

        static void AddMarkerLight(ModelInstance inst, Transform lm)
        {
            var node = inst.Model.Find(lm.name);
            var light = lm.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            if (node != null && node.Props.TryGetValue("color", out var c))
            {
                int rgb = int.Parse(c, System.Globalization.NumberStyles.HexNumber);
                light.color = new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
            }
            light.range = node != null && node.Props.TryGetValue("range", out var r) ? float.Parse(r, System.Globalization.CultureInfo.InvariantCulture) : 12f;
            light.intensity = node != null && node.Props.TryGetValue("intensity", out var it) ? float.Parse(it, System.Globalization.CultureInfo.InvariantCulture) * 1.4f : 1.2f;
        }

        static void InvisibleWall(Transform root, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Bound");
            go.transform.SetParent(root, false);
            go.transform.position = center;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
        }

        // ------------------------------------------------------------------ walls

        static void BuildWall(Transform root, HouseLayout L, Wall wall, float H)
        {
            float len = wall.Length;
            var dir = new Vector3((wall.Bx - wall.Ax) / len, 0, (wall.Bz - wall.Az) / len);
            var normalA = new Vector3(-dir.z, 0, dir.x); // local +Z side
            var a = new Vector3(wall.Ax, 0, wall.Az);
            // LookRotation(forward = normalA) has right = up x forward = dir, so local +X runs along the wall
            var rot = Quaternion.LookRotation(normalA, Vector3.up);

            Material SideMat(Vector3 center, Vector3 n)
            {
                var sample = center + n * (wall.Thick * 0.5f + 1.0f);
                var room = L.RoomAt(sample.x, sample.z);
                return Textures.Mat(room != null ? Textures.Wall(room.Wall) : Textures.Wall(WallStyle.Plaster));
            }

            var edgeMat = Textures.Mat(Textures.Wall(WallStyle.Plaster));
            var baseMat = Textures.Mat(Textures.Baseboard(), 0.3f);
            foreach (var s in wall.Solids(H))
            {
                float mid = (s.t0 + s.t1) / 2;
                var center = a + dir * mid + Vector3.up * ((s.y0 + s.y1) / 2);
                var size = new Vector3(s.t1 - s.t0, s.y1 - s.y0, wall.Thick);
                var go = new GameObject($"Wall_{wall.Id}_{mid:0}");
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(center, rot);
                var mesh = WallMesh(size, s.t0, s.y0);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                var n = rot * Vector3.forward;
                mr.sharedMaterials = new[] { SideMat(center, n), SideMat(center, -n), edgeMat };
                var bc = go.AddComponent<BoxCollider>();
                bc.size = size;
                // baseboards on both sides
                if (s.y0 <= 0.001f)
                {
                    foreach (float side in new[] { 1f, -1f })
                    {
                        var bb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Object.Destroy(bb.GetComponent<Collider>());
                        bb.name = "Baseboard";
                        bb.transform.SetParent(go.transform, false);
                        bb.transform.localPosition = new Vector3(0, -size.y / 2 + 0.22f, side * (size.z / 2 + 0.04f));
                        bb.transform.localScale = new Vector3(size.x, 0.44f, 0.08f);
                        bb.GetComponent<MeshRenderer>().sharedMaterial = baseMat;
                    }
                }
            }
            // door frames / window trims
            var trim = ModelLibrary.TintMaterial(new Color32(236, 228, 210, 255));
            foreach (var o in wall.Openings)
            {
                if (o.Kind == OpeningKind.Window) continue;
                float w0 = o.T0, w1 = o.T1;
                var c0 = a + dir * w0;
                var c1 = a + dir * w1;
                float fh = o.Y1, ft = wall.Thick + 0.12f;
                if (o.Kind == OpeningKind.FakeDoor)
                {
                    // a painted-shut back door on the kitchen wall (visual, blocks the opening)
                    var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    door.name = "BackDoor";
                    door.transform.SetParent(root, false);
                    door.transform.SetPositionAndRotation(a + dir * ((w0 + w1) / 2) + Vector3.up * fh / 2, rot);
                    door.transform.localScale = new Vector3(w1 - w0, fh, wall.Thick * 0.6f);
                    door.GetComponent<MeshRenderer>().sharedMaterial = ModelLibrary.TintMaterial(new Color32(120, 70, 40, 255));
                    continue;
                }
                float jw = o.Kind == OpeningKind.GnomeHole ? 0.12f : 0.25f;
                Trim(root, c0 + Vector3.up * fh / 2 - dir * jw / 2, new Vector3(jw, fh, ft), rot, trim);
                Trim(root, c1 + Vector3.up * fh / 2 + dir * jw / 2, new Vector3(jw, fh, ft), rot, trim);
                Trim(root, a + dir * ((w0 + w1) / 2) + Vector3.up * (fh + jw / 2), new Vector3(w1 - w0 + jw * 2, jw, ft), rot, trim);
            }
        }

        static void Trim(Transform root, Vector3 pos, Vector3 size, Quaternion rot, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Trim";
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        /// <summary>Box mesh with world-scaled UVs and 3 submeshes: +Z side, -Z side, edges (built by Core.MeshGen).</summary>
        static Mesh WallMesh(Vector3 size, float t0, float y0)
        {
            var a = MeshGen.WallSlab(size.ToCoreV(), t0, y0, WallTile);
            var m = new Mesh { name = "wall" };
            var v = new List<Vector3>(a.Positions.Count);
            var n = new List<Vector3>(a.Normals.Count);
            var uv = new List<Vector2>(a.Positions.Count);
            for (int i = 0; i < a.Positions.Count; i++)
            {
                v.Add(a.Positions[i].U());
                n.Add(a.Normals[i].U());
                uv.Add(new Vector2(a.Uvs[i * 2], a.Uvs[i * 2 + 1]));
            }
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.subMeshCount = a.SubMeshes.Count;
            for (int i = 0; i < a.SubMeshes.Count; i++) m.SetTriangles(a.SubMeshes[i], i);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Box with a tiling texture and optional collider.</summary>
        public static GameObject BoxObj(Transform root, string name, Vector3 center, Vector3 size, Quaternion rot, Material mat, float tile, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.transform.localScale = size;
            var mf = go.GetComponent<MeshFilter>();
            // rebuild UVs so the texture tiles in world units on the top/bottom faces
            var mesh = Object.Instantiate(mf.sharedMesh);
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var uvs = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var wv = Vector3.Scale(verts[i], size);
                var nn = normals[i];
                if (Mathf.Abs(nn.y) > 0.5f) uvs[i] = new Vector2((wv.x + center.x) / tile, (wv.z + center.z) / tile);
                else if (Mathf.Abs(nn.x) > 0.5f) uvs[i] = new Vector2((wv.z + center.z) / tile, (wv.y + center.y) / tile);
                else uvs[i] = new Vector2((wv.x + center.x) / tile, (wv.y + center.y) / tile);
            }
            mesh.uv = uvs;
            mf.sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        // ------------------------------------------------------------------ navigation

        static NavMeshDataInstance navInstance;

        public static void BakeNavMesh(HouseLayout L)
        {
            if (navInstance.valid) navInstance.Remove();
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = GameConsts.OldManRadius;
            settings.agentHeight = GameConsts.OldManHeight;
            settings.agentClimb = 0.9f;
            settings.agentSlope = 40f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.3f;
            var bounds = new Bounds(new Vector3(28, 5, 20), new Vector3(60, 14, 44));
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
            NavMeshBuilder.CollectSources(bounds, 1 << Layers.Default, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data != null) navInstance = NavMesh.AddNavMeshData(data);
            else Debug.LogWarning("[HouseBuilder] NavMesh bake failed");
        }

        public static void ClearNavMesh()
        {
            if (navInstance.valid) navInstance.Remove();
        }

        // ------------------------------------------------------------------ items (host)

        /// <summary>Host: decide where every item of the night spawns.</summary>
        public static List<PropSpawn> PlaceItems(GameWorld w, HouseLayout L, int seed)
        {
            var rng = new Rng(seed * 31 + 7);
            var result = new List<PropSpawn>();
            var placed = new List<(Vector3 pos, float r)>();
            ushort id = 1;
            foreach (var spawn in L.Items)
            {
                var model = ModelLibrary.Get(spawn.Kind);
                if (model == null) continue;
                model.Bounds(out var mn, out var mx);
                float half = -mn.y;
                float radius = Mathf.Max(mx.x - mn.x, mx.z - mn.z) * 0.5f;
                Vector3 pos = Vector3.zero;
                bool ok = false;
                if (spawn.Furniture != null)
                {
                    var f = w.FindFurniture(spawn.Furniture);
                    if (f == null || f.Surfaces.Count == 0) continue;
                    var s = spawn.Surface >= 0 ? f.Surfaces[Mathf.Min(spawn.Surface, f.Surfaces.Count - 1)] : f.Surfaces[rng.Int(0, f.Surfaces.Count - 1)];
                    for (int tries = 0; tries < 10 && !ok; tries++)
                    {
                        var local = spawn.Jitter ? new Vector3(rng.Range(-0.5f, 0.5f), 0, rng.Range(-0.5f, 0.5f)) : Vector3.zero;
                        pos = s.TransformPoint(local);
                        pos.y = s.position.y + half + 0.03f;
                        ok = Free(placed, pos, radius) || tries == 9;
                    }
                }
                else
                {
                    var room = L.Rooms.Find(r => r.Id == spawn.Room);
                    if (room == null) continue;
                    for (int tries = 0; tries < 25 && !ok; tries++)
                    {
                        if (spawn.FloorPos.HasValue && tries == 0) pos = spawn.FloorPos.Value.U();
                        else pos = new Vector3(rng.Range(room.X0 + 1.5f, room.X1 - 1.5f), 0, rng.Range(room.Z0 + 1.5f, room.Z1 - 1.5f));
                        pos.y = half + 0.03f;
                        ok = !Physics.CheckBox(pos + Vector3.up * 0.5f, new Vector3(radius + 0.1f, 0.45f, radius + 0.1f), 1 << Layers.Default, QueryTriggerInteraction.Ignore) && Free(placed, pos, radius);
                    }
                    if (!ok) continue;
                }
                placed.Add((pos, radius));
                var rot = Quaternion.Euler(0, rng.Range(0, 360f), 0);
                result.Add(new PropSpawn { Id = id++, Kind = spawn.Kind, Pos = pos.ToCoreV(), Rot = rot.ToCoreQ() });
            }
            return result;
        }

        static bool Free(List<(Vector3 pos, float r)> placed, Vector3 p, float r)
        {
            foreach (var q in placed)
            {
                if (Mathf.Abs(q.pos.y - p.y) > 1.5f) continue;
                if ((q.pos - p).Flat().sqrMagnitude < (q.r + r) * (q.r + r)) return false;
            }
            return true;
        }
    }
}
