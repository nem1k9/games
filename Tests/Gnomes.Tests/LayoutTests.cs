using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gnomes.Core;
using Gnomes.Core.Level;
using Gnomes.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace Gnomes.Tests
{
    /// <summary>
    /// Checks generated houses the way a level designer would: nothing stands inside anything else,
    /// furniture stays in its room and doorways are walkable. Footprints come from the real models.
    /// </summary>
    public class LayoutTests
    {
        readonly ITestOutputHelper output;
        public LayoutTests(ITestOutputHelper output) => this.output = output;

        static readonly Dictionary<string, ModelData> cache = new Dictionary<string, ModelData>();

        static ModelData Model(string name)
        {
            if (!cache.TryGetValue(name, out var m))
                cache[name] = m = ModelData.Parse(File.ReadAllBytes(Path.Combine(TestPaths.Models, name + ".bytes")), name);
            return m;
        }

        struct Rect
        {
            public string Id;
            public float X0, Z0, X1, Z1;
            public bool Overlaps(Rect o, float slack) => X0 + slack < o.X1 && o.X0 + slack < X1 && Z0 + slack < o.Z1 && o.Z0 + slack < Z1;
            public override string ToString() => $"{Id} [{X0:0.0}..{X1:0.0}] x [{Z0:0.0}..{Z1:0.0}]";
        }

        /// <summary>World-space XZ bounds of a placed model (its mesh, rotated by the placement yaw).</summary>
        static Rect Footprint(Placement p, float shrink = 0f)
        {
            Model(p.Model).Bounds(out var mn, out var mx);
            float c = (float)Math.Cos(p.RotY), s = (float)Math.Sin(p.RotY);
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            foreach (var lx in new[] { mn.x, mx.x })
            foreach (var lz in new[] { mn.z, mx.z })
            {
                // Unity yaw: x' = x cos + z sin, z' = -x sin + z cos
                float wx = p.Pos.x + lx * c + lz * s;
                float wz = p.Pos.z - lx * s + lz * c;
                x0 = Math.Min(x0, wx);
                x1 = Math.Max(x1, wx);
                z0 = Math.Min(z0, wz);
                z1 = Math.Max(z1, wz);
            }
            return new Rect { Id = p.Id, X0 = x0 + shrink, Z0 = z0 + shrink, X1 = x1 - shrink, Z1 = z1 - shrink };
        }

        static bool IsFloorFurniture(Placement p) =>
            p.Pos.y < 0.3f && !p.Model.StartsWith("rug") && p.Model != "window" && p.Model != "creakyBoard" && p.Model != "mousetrap";

        public static TheoryData<int> Seeds()
        {
            var d = new TheoryData<int>();
            for (int s = 1; s <= 40; s++) d.Add(s * 7919);
            return d;
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void FurnitureDoesNotIntersect(int seed)
        {
            var L = HouseLayout.Generate(seed);
            var floor = L.Furniture.Where(IsFloorFurniture).Select(p => Footprint(p, 0.15f)).ToList();
            var bad = new List<string>();
            for (int i = 0; i < floor.Count; i++)
                for (int j = i + 1; j < floor.Count; j++)
                    if (floor[i].Overlaps(floor[j], 0.1f)) bad.Add(floor[i] + "  <->  " + floor[j]);
            foreach (var b in bad) output.WriteLine(b);
            Assert.True(bad.Count == 0, $"seed {seed}: {bad.Count} overlaps, first: {bad.FirstOrDefault()}");
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void HazardsAreOutInTheOpen(int seed)
        {
            // a mousetrap or a creaky board under a wardrobe would be invisible and pointless
            var L = HouseLayout.Generate(seed);
            var floor = L.Furniture.Where(IsFloorFurniture).Select(p => Footprint(p, 0.1f)).ToList();
            var bad = new List<string>();
            foreach (var h in L.Furniture.Where(p => p.Model == "mousetrap" || p.Model == "creakyBoard"))
            {
                var r = Footprint(h, 0.2f);
                foreach (var f in floor)
                    if (r.Overlaps(f, 0f)) bad.Add(h.Id + " under " + f);
            }
            foreach (var b in bad) output.WriteLine(b);
            Assert.True(bad.Count == 0, $"seed {seed}: {string.Join("; ", bad)}");
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void FurnitureStaysInsideItsRoom(int seed)
        {
            var L = HouseLayout.Generate(seed);
            var bad = new List<string>();
            foreach (var p in L.Furniture.Where(IsFloorFurniture))
            {
                var room = L.Rooms.FirstOrDefault(r => r.Id == p.Room);
                if (room == null) continue;
                var f = Footprint(p, 0.2f);
                const float wall = 0.3f; // half the wall thickness
                if (f.X0 < room.X0 + wall - 0.05f || f.X1 > room.X1 - wall + 0.05f || f.Z0 < room.Z0 + wall - 0.05f || f.Z1 > room.Z1 - wall + 0.05f)
                    bad.Add($"{f} sticks out of {room.Id} [{room.X0}..{room.X1}] x [{room.Z0}..{room.Z1}]");
            }
            foreach (var b in bad) output.WriteLine(b);
            Assert.True(bad.Count == 0, $"seed {seed}: {string.Join("; ", bad)}");
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void DoorwaysAreClear(int seed)
        {
            var L = HouseLayout.Generate(seed);
            var floor = L.Furniture.Where(IsFloorFurniture).Select(p => Footprint(p, 0.15f)).ToList();
            var bad = new List<string>();
            foreach (var w in L.Walls)
            {
                float len = w.Length;
                float dx = (w.Bx - w.Ax) / len, dz = (w.Bz - w.Az) / len;
                foreach (var o in w.Openings)
                {
                    if (o.Kind != OpeningKind.Door && o.Kind != OpeningKind.GnomeHole) continue;
                    // the doorway plus 1.5 units of floor on both sides
                    float ax = w.Ax + dx * o.T0, az = w.Az + dz * o.T0, bx = w.Ax + dx * o.T1, bz = w.Az + dz * o.T1;
                    float nx = -dz * 1.5f, nz = dx * 1.5f;
                    var zone = new Rect
                    {
                        Id = w.Id + "/" + o.Kind,
                        X0 = Math.Min(Math.Min(ax, bx) - Math.Abs(nx), Math.Min(ax, bx) + Math.Abs(nx)),
                        X1 = Math.Max(Math.Max(ax, bx) - Math.Abs(nx), Math.Max(ax, bx) + Math.Abs(nx)),
                        Z0 = Math.Min(Math.Min(az, bz) - Math.Abs(nz), Math.Min(az, bz) + Math.Abs(nz)),
                        Z1 = Math.Max(Math.Max(az, bz) - Math.Abs(nz), Math.Max(az, bz) + Math.Abs(nz)),
                    };
                    foreach (var f in floor)
                        if (zone.Overlaps(f, 0.05f)) bad.Add(f + " blocks " + zone);
                }
            }
            foreach (var b in bad) output.WriteLine(b);
            Assert.True(bad.Count == 0, $"seed {seed}: {string.Join("; ", bad)}");
        }
    }
}
