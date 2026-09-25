using System;
using System.Collections.Generic;
using Gnomes.Core;
using Gnomes.Core.Level;
using Gnomes.Core.Protocol;
using Godot;
using SockGang.Rendering;

namespace SockGang.World
{
    /// <summary>
    /// Builds grandpa's house (floors, walls, furniture, garden, porch) from a <see cref="HouseLayout"/>.
    /// The layout is in Unity space; everything is converted with <see cref="Conv"/> (Z mirrored).
    /// </summary>
    public static class HouseBuilder
    {
        const float WallTile = 5f;
        const float FloorTile = 6f;
        static BoxMesh unitBox;

        public static void Build(GameWorld w, HouseLayout L)
        {
            w.Layout = L;
            var root = w.StaticRoot;
            float H = L.WallHeight;

            // ---------- floors & ceilings ----------
            foreach (var r in L.Rooms)
            {
                var c = new V3((r.X0 + r.X1) / 2, 0, (r.Z0 + r.Z1) / 2).G();
                bool shiny = r.Floor == FloorStyle.Tile || r.Floor == FloorStyle.Checker;
                Box(root, "Floor_" + r.Id, c + new Vector3(0, -0.25f, 0), new Vector3(r.X1 - r.X0, 0.5f, r.Z1 - r.Z0), 0, Textures.Mat(Textures.Floor(r.Floor), shiny ? 0.35f : 0.12f, FloorTile), true);
                Box(root, "Ceiling_" + r.Id, c + new Vector3(0, H + 0.15f, 0), new Vector3(r.X1 - r.X0, 0.3f, r.Z1 - r.Z0), 0, Textures.Mat(Textures.Ceiling(), 0.1f, FloorTile), true, false);
            }

            // ---------- walls ----------
            foreach (var wall in L.Walls) BuildWall(root, L, wall, H);

            // ---------- furniture ----------
            foreach (var p in L.Furniture)
            {
                if (!ModelLibrary.Exists(p.Model))
                {
                    GD.PushWarning("[HouseBuilder] no model for " + p.Model);
                    continue;
                }
                var f = new Furniture { Name = "F_" + p.Id };
                root.AddChild(f);
                f.Position = p.Pos.G();
                f.Rotation = new Vector3(0, Conv.Yaw(p.RotY), 0);
                var inst = ModelLibrary.Instantiate(p.Model, f);
                if (p.Model == "window") inst.Scale = new Vector3(p.P("w", 1), p.P("h", 1), 1f);
                f.Init(p, inst);
                w.RegisterFurniture(f);
            }

            // ---------- roof & garden ----------
            var roof = ModelLibrary.Instantiate("roof", root, ColliderMode.None);
            roof.Position = new V3(7 * GameConsts.HS, H + 0.3f, 5 * GameConsts.HS).G();

            var g0 = L.WorldMin;
            var g1 = L.WorldMax;
            var gc = new V3((g0.x + g1.x) / 2, -0.52f, (g0.z + g1.z) / 2).G();
            Box(root, "Garden", gc, new Vector3(g1.x - g0.x + 40, 1f, g1.z - g0.z + 40), 0, Textures.Mat(Textures.Floor(FloorStyle.Grass), 0.05f, 8f), true);
            foreach (var d in L.Garden)
            {
                if (!ModelLibrary.Exists(d.Model)) continue;
                var inst = ModelLibrary.Instantiate(d.Model, root);
                inst.Position = d.Pos.G();
                inst.Rotation = new Vector3(0, Conv.Yaw(d.RotY), 0);
                inst.Scale = Vector3.One * d.Scale;
                foreach (var lm in inst.Markers("LIGHT")) Furniture.MarkerLight(inst.Model.Find(inst.NameOf(lm))?.Props, lm, 0.8f);
            }
            // the porch: the gang's village is underneath
            var porch = ModelLibrary.Instantiate("porch", root);
            porch.Position = L.Porch.G();
            var gap = porch.Node("ANCHOR_gap");
            w.GoHomePoint = gap != null ? gap.GlobalPosition : L.Porch.G() + new Vector3(-5f, 0, -7f);
            // yarn basket: fallen gnomes get re-knitted here
            var basket = ModelLibrary.Instantiate("yarnBasket", root);
            basket.Position = L.Mushroom.G();
            basket.Rotation = new Vector3(0, Conv.Yaw(Mathf.Pi), 0);
            foreach (var lm in basket.Markers("LIGHT")) Furniture.MarkerLight(basket.Model.Find(basket.NameOf(lm))?.Props, lm);
            w.ReviveZone = basket.Node("ZONE_revive");
            var rev = basket.Node("ANCHOR_revive");
            w.RevivePoint = rev != null ? rev.GlobalPosition : L.Mushroom.G() + new Vector3(0, 0, -2f);
            // the sardine-tin cart takes loot to the village
            var stash = ModelLibrary.Instantiate("stashBasket", root);
            stash.Position = new V3(L.StashCenter.x, 0, L.StashCenter.z).G();
            stash.Rotation = new Vector3(0, Conv.Yaw(Mathf.DegToRad(170)), 0);
            w.StashZone = stash.Node("ZONE_stash");

            foreach (var s in L.Spawns) w.Spawns.Add(s.G() + Vector3.Up * 0.2f);

            // invisible world bounds
            float bh = 40f;
            var mn = new V3(g0.x, 0, g0.z).G();
            var mx = new V3(g1.x, 0, g1.z).G();
            var lo = new Vector3(Mathf.Min(mn.X, mx.X), 0, Mathf.Min(mn.Z, mx.Z));
            var hi = new Vector3(Mathf.Max(mn.X, mx.X), 0, Mathf.Max(mn.Z, mx.Z));
            var mid = (lo + hi) / 2 + new Vector3(0, bh / 2 - 1, 0);
            var size = new Vector3(hi.X - lo.X, bh, hi.Z - lo.Z);
            InvisibleWall(root, mid + new Vector3(0, 0, size.Z / 2 + 1), new Vector3(size.X + 4, bh, 2));
            InvisibleWall(root, mid - new Vector3(0, 0, size.Z / 2 + 1), new Vector3(size.X + 4, bh, 2));
            InvisibleWall(root, mid + new Vector3(size.X / 2 + 1, 0, 0), new Vector3(2, bh, size.Z + 4));
            InvisibleWall(root, mid - new Vector3(size.X / 2 + 1, 0, 0), new Vector3(2, bh, size.Z + 4));
            w.PlayArea = new Aabb(mid - size / 2, size);

            BakeNavMesh(w, L);
        }

        static void InvisibleWall(Node3D root, Vector3 center, Vector3 size)
        {
            var body = new StaticBody3D { Name = "Bound", CollisionLayer = Layers.World, CollisionMask = 0 };
            root.AddChild(body);
            body.Position = center;
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            body.SetMeta("nonav", true);
        }

        /// <summary>A textured box with an optional collider (Godot space).</summary>
        public static MeshInstance3D Box(Node3D root, string name, Vector3 center, Vector3 size, float yaw, Material mat, bool collider, bool castShadow = true)
        {
            unitBox ??= new BoxMesh { Size = Vector3.One };
            var mi = new MeshInstance3D { Name = name, Mesh = unitBox, MaterialOverride = mat };
            if (!castShadow) mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            root.AddChild(mi);
            mi.Position = center;
            mi.Rotation = new Vector3(0, yaw, 0);
            mi.Scale = size;
            if (collider)
            {
                var body = new StaticBody3D { Name = name + "_col", CollisionLayer = Layers.World, CollisionMask = 0 };
                root.AddChild(body);
                body.Position = center;
                body.Rotation = new Vector3(0, yaw, 0);
                body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            }
            return mi;
        }

        // ------------------------------------------------------------------ walls

        static void BuildWall(Node3D root, HouseLayout L, Wall wall, float H)
        {
            float len = wall.Length;
            // work in Unity space (like the layout), convert each box at the end
            float dx = (wall.Bx - wall.Ax) / len, dz = (wall.Bz - wall.Az) / len;
            float nx = -dz, nz = dx; // normal (Unity LookRotation forward)
            float yawU = Mathf.Atan2(nx, nz);
            float yaw = Conv.Yaw(yawU);

            StandardMaterial3D SideMat(float cx, float cz, float sgn)
            {
                var room = L.RoomAt(cx + sgn * nx * (wall.Thick * 0.5f + 1f), cz + sgn * nz * (wall.Thick * 0.5f + 1f));
                return Textures.Mat(room != null ? Textures.Wall(room.Wall) : Textures.Wall(WallStyle.Plaster), 0.08f, WallTile);
            }

            var baseMat = Textures.Mat(Textures.Baseboard(), 0.3f, 4f);
            foreach (var s in wall.Solids(H))
            {
                float mid = (s.t0 + s.t1) / 2;
                float cx = wall.Ax + dx * mid, cz = wall.Az + dz * mid, cy = (s.y0 + s.y1) / 2;
                float slabLen = s.t1 - s.t0, slabH = s.y1 - s.y0;
                // two half-thickness slabs so each side shows its own room's wallpaper (outside: plaster)
                foreach (float sgn in new[] { 1f, -1f })
                {
                    float off = sgn * wall.Thick / 4;
                    var c = new V3(cx + nx * off, cy, cz + nz * off).G();
                    Box(root, $"Wall_{wall.Id}_{mid:0}", c, new Vector3(slabLen, slabH, wall.Thick / 2), yaw, SideMat(cx, cz, sgn), false);
                    if (s.y0 <= 0.001f)
                    {
                        // baseboards of crossing walls overlap in the corners: different heights keep their
                        // top faces apart (the same height would flicker)
                        float bh = Mathf.Abs(dx) > Mathf.Abs(dz) ? 0.44f : 0.456f;
                        float bo = sgn * (wall.Thick / 2 + 0.04f);
                        var bc = new V3(cx + nx * bo, bh / 2, cz + nz * bo).G();
                        Box(root, "Baseboard", bc, new Vector3(slabLen, bh, 0.08f), yaw, baseMat, false, false);
                    }
                }
                var body = new StaticBody3D { Name = "WallCol", CollisionLayer = Layers.World, CollisionMask = 0 };
                root.AddChild(body);
                body.Position = new V3(cx, cy, cz).G();
                body.Rotation = new Vector3(0, yaw, 0);
                body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(slabLen, slabH, wall.Thick) } });
            }
            // door frames / the painted-shut back door
            var trim = ModelLibrary.TintMaterial(Conv.C(0xece4d2));
            foreach (var o in wall.Openings)
            {
                if (o.Kind == OpeningKind.Window) continue;
                float w0 = o.T0, w1 = o.T1, fh = o.Y1, ft = wall.Thick + 0.12f;
                float mx = wall.Ax + dx * ((w0 + w1) / 2), mz = wall.Az + dz * ((w0 + w1) / 2);
                if (o.Kind == OpeningKind.FakeDoor)
                {
                    Box(root, "BackDoor", new V3(mx, fh / 2, mz).G(), new Vector3(w1 - w0, fh, wall.Thick * 0.6f), yaw, ModelLibrary.TintMaterial(Conv.C(0x784628)), true);
                    continue;
                }
                float jw = o.Kind == OpeningKind.GnomeHole ? 0.12f : 0.25f;
                float ax0 = wall.Ax + dx * (w0 - jw / 2), az0 = wall.Az + dz * (w0 - jw / 2);
                float ax1 = wall.Ax + dx * (w1 + jw / 2), az1 = wall.Az + dz * (w1 + jw / 2);
                Box(root, "Trim", new V3(ax0, fh / 2, az0).G(), new Vector3(jw, fh, ft), yaw, trim, false, false);
                Box(root, "Trim", new V3(ax1, fh / 2, az1).G(), new Vector3(jw, fh, ft), yaw, trim, false, false);
                Box(root, "Trim", new V3(mx, fh + jw / 2, mz).G(), new Vector3(w1 - w0 + jw * 2, jw, ft), yaw, trim, false, false);
            }
        }

        // ------------------------------------------------------------------ navigation

        public static void BakeNavMesh(GameWorld w, HouseLayout L)
        {
            var nm = new NavigationMesh
            {
                AgentRadius = 1.25f, // grandpa's radius (1.1) rounded up to the 0.25 cell size
                AgentHeight = GameConsts.OldManHeight,
                AgentMaxClimb = 1.0f,
                AgentMaxSlope = 40f,
                CellSize = 0.25f,
                CellHeight = 0.25f,
                GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.StaticColliders,
                GeometryCollisionMask = Layers.World,
                GeometrySourceGeometryMode = NavigationMesh.SourceGeometryMode.RootNodeChildren,
            };
            var min = new V3(L.HouseMin.x - 4f, -1f, L.HouseMax.z + 4f).G();
            var max = new V3(L.HouseMax.x + 4f, L.WallHeight - 0.2f, L.HouseMin.z - 4f).G();
            nm.FilterBakingAabb = new Aabb(min, max - min);
            var src = new NavigationMeshSourceGeometryData3D();
            NavigationServer3D.ParseSourceGeometryData(nm, src, w.StaticRoot);
            NavigationServer3D.BakeFromSourceGeometryData(nm, src);
            var region = new NavigationRegion3D { Name = "NavRegion", NavigationMesh = nm };
            w.AddChild(region);
        }

        // ------------------------------------------------------------------ items (host)

        /// <summary>Host: decide where every item of the night spawns (Unity-space results, like the layout).</summary>
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
                Vector3 pos = Vector3.Zero;
                bool ok = false;
                if (spawn.Furniture != null)
                {
                    var f = w.FindFurniture(spawn.Furniture);
                    if (f == null || f.Surfaces.Count == 0) continue;
                    var s = spawn.Surface >= 0 ? f.Surfaces[Math.Min(spawn.Surface, f.Surfaces.Count - 1)] : f.Surfaces[rng.Int(0, f.Surfaces.Count - 1)];
                    for (int tries = 0; tries < 10 && !ok; tries++)
                    {
                        var local = spawn.Jitter ? new Vector3(rng.Range(-0.5f, 0.5f), 0, rng.Range(-0.5f, 0.5f)) : Vector3.Zero;
                        pos = s.ToGlobal(local);
                        pos.Y = s.GlobalPosition.Y + half + 0.03f;
                        ok = Free(placed, pos, radius) || tries == 9;
                    }
                }
                else
                {
                    var room = L.Rooms.Find(r => r.Id == spawn.Room);
                    if (room == null) continue;
                    for (int tries = 0; tries < 25 && !ok; tries++)
                    {
                        if (spawn.FloorPos.HasValue && tries == 0) pos = spawn.FloorPos.Value.G();
                        else pos = new V3(rng.Range(room.X0 + 1.5f, room.X1 - 1.5f), 0, rng.Range(room.Z0 + 1.5f, room.Z1 - 1.5f)).G();
                        pos.Y = half + 0.03f;
                        ok = !Phys.CheckBox(pos + Vector3.Up * 0.5f, new Vector3(radius + 0.1f, 0.45f, radius + 0.1f), Layers.World) && Free(placed, pos, radius);
                    }
                    if (!ok) continue;
                }
                placed.Add((pos, radius));
                var rot = new Quaternion(Vector3.Up, rng.Range(0, Mathf.Tau));
                result.Add(new PropSpawn { Id = id++, Kind = spawn.Kind, Pos = pos.U(), Rot = rot.U() });
            }
            return result;
        }

        static bool Free(List<(Vector3 pos, float r)> placed, Vector3 p, float r)
        {
            foreach (var q in placed)
            {
                if (Mathf.Abs(q.pos.Y - p.Y) > 1.5f) continue;
                if ((q.pos - p).Flat().LengthSquared() < (q.r + r) * (q.r + r)) return false;
            }
            return true;
        }
    }
}
