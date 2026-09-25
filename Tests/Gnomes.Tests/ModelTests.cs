using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gnomes.Core;
using Gnomes.Core.Models;
using Xunit;

namespace Gnomes.Tests
{
    public class ModelTests
    {
        static ModelData Load(string name) =>
            ModelData.Parse(File.ReadAllBytes(Path.Combine(TestPaths.Models, name + ".bytes")), name);

        public static TheoryData<string> AllModels()
        {
            var d = new TheoryData<string>();
            foreach (var f in Directory.GetFiles(TestPaths.Models, "*.bytes").OrderBy(x => x))
            {
                var n = Path.GetFileNameWithoutExtension(f);
                if (n != "palette") d.Add(n);
            }
            return d;
        }

        [Theory]
        [MemberData(nameof(AllModels))]
        public void EveryModelParsesAndIsWellFormed(string name)
        {
            var m = Load(name);
            Assert.True(m.Nodes.Length > 0);
            Assert.Equal(-1, m.Nodes[0].Parent);
            Assert.True(m.TriangleCount > 0, "model has no triangles");
            foreach (var n in m.Nodes)
            {
                foreach (var s in n.Meshes)
                {
                    Assert.Equal(0, s.Indices.Length % 3);
                    Assert.All(s.Indices, i => Assert.InRange(i, 0, s.VertexCount - 1));
                    Assert.Equal(s.VertexCount * 2, s.Uvs.Length);
                    Assert.True(s.Colors != null && s.Colors.Length == s.VertexCount * 4, $"{name}/{n.Name}: missing baked occlusion colours");
                    // Winding must agree with the stored face normal (Unity: front = cross(e1, e2) direction).
                    int bad = 0, tris = s.Indices.Length / 3;
                    for (int t = 0; t < tris; t++)
                    {
                        V3 P(int k) { int i = s.Indices[t * 3 + k]; return new V3(s.Positions[i * 3], s.Positions[i * 3 + 1], s.Positions[i * 3 + 2]); }
                        V3 N(int k) { int i = s.Indices[t * 3 + k]; return new V3(s.Normals[i * 3], s.Normals[i * 3 + 1], s.Normals[i * 3 + 2]); }
                        var nrm = N(0) + N(1) + N(2); // smooth meshes: the average vertex normal
                        var c = V3.Cross(P(1) - P(0), P(2) - P(0));
                        if (c.sqrMagnitude < 1e-14f) continue; // degenerate sliver
                        if (V3.Dot(c, nrm) <= 0) bad++;
                    }
                    // a smooth surface may have a sliver at a crease whose vertex normals disagree with it
                    Assert.True(bad <= tris / 100, $"{name}/{n.Name}: {bad} of {tris} triangles have winding opposite to their normal");
                }
                Assert.False(float.IsNaN(n.Position.x) || float.IsNaN(n.Rotation.w));
            }
        }

        /// <summary>
        /// Two visible triangles in the same plane, facing the same way and overlapping, fight for the depth
        /// buffer and flicker in game (a decal flush with the surface under it). tools/zfight.py finds them too.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllModels))]
        public void NoCoplanarOverlappingFaces(string name)
        {
            var m = Load(name);
            var tris = new List<(V3 a, V3 b, V3 c, V3 n, float d, string node)>();
            float size = 1f;
            for (int ni = 0; ni < m.Nodes.Length; ni++)
            {
                var node = m.Nodes[ni];
                if (node.Meshes.Length == 0) continue;
                m.NodeToModel(ni, out var p, out var q, out var sc);
                foreach (var s in node.Meshes)
                {
                    if (s.Kind == MeshKind.Glass) continue;
                    V3 W(int i) => p + q * new V3(s.Positions[i * 3] * sc.x, s.Positions[i * 3 + 1] * sc.y, s.Positions[i * 3 + 2] * sc.z);
                    for (int t = 0; t < s.Indices.Length; t += 3)
                    {
                        V3 a = W(s.Indices[t]), b = W(s.Indices[t + 1]), c = W(s.Indices[t + 2]);
                        var cr = V3.Cross(b - a, c - a);
                        float len = cr.magnitude;
                        if (len < 1e-9f) continue;
                        var n = cr / len;
                        tris.Add((a, b, c, n, V3.Dot(n, a), node.Name));
                        size = Math.Max(size, Math.Max(Math.Abs(a.x), Math.Max(Math.Abs(a.y), Math.Abs(a.z))));
                    }
                }
            }
            float eps = size * 2e-4f;
            var buckets = new Dictionary<(int, int, int, int), List<int>>();
            (int, int, int, int) Key(V3 n, float d, int dd = 0) => ((int)Math.Round(n.x * 50), (int)Math.Round(n.y * 50), (int)Math.Round(n.z * 50), (int)Math.Round(d / (eps * 4)) + dd);
            for (int i = 0; i < tris.Count; i++)
            {
                var k = Key(tris[i].n, tris[i].d);
                if (!buckets.TryGetValue(k, out var l)) buckets[k] = l = new List<int>();
                l.Add(i);
            }
            var bad = new List<string>();
            foreach (var kv in buckets)
            {
                var cand = new List<int>(kv.Value);
                foreach (int dd in new[] { -1, 1 })
                    if (buckets.TryGetValue((kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Key.Item4 + dd), out var more)) cand.AddRange(more);
                foreach (int x in kv.Value)
                    foreach (int y in cand)
                    {
                        if (y <= x) continue;
                        var A = tris[x];
                        var B = tris[y];
                        if (V3.Dot(A.n, B.n) < 0.999f || Math.Abs(A.d - B.d) > eps) continue;
                        float ov = OverlapArea(A.a, A.b, A.c, B.a, B.b, B.c, A.n);
                        if (ov > 1e-4f * size * size) bad.Add($"{A.node}/{B.node} near {A.a}");
                    }
            }
            Assert.True(bad.Count == 0, $"{name}: {bad.Count} coplanar overlapping triangle pairs (z-fighting), e.g. {string.Join("; ", bad.Take(3))}");
        }

        static float OverlapArea(V3 a0, V3 a1, V3 a2, V3 b0, V3 b1, V3 b2, V3 n)
        {
            int ax = Math.Abs(n.x) > Math.Abs(n.y) ? (Math.Abs(n.x) > Math.Abs(n.z) ? 0 : 2) : (Math.Abs(n.y) > Math.Abs(n.z) ? 1 : 2);
            (float, float) P(V3 v) => ax == 0 ? (v.y, v.z) : ax == 1 ? (v.x, v.z) : (v.x, v.y);
            var A = new List<(float x, float y)> { P(a0), P(a1), P(a2) };
            var B = new List<(float x, float y)> { P(b0), P(b1), P(b2) };
            float Signed(List<(float x, float y)> poly)
            {
                float s = 0;
                for (int i = 0; i < poly.Count; i++) s += poly[i].x * poly[(i + 1) % poly.Count].y - poly[(i + 1) % poly.Count].x * poly[i].y;
                return s / 2;
            }
            if (Signed(A) < 0) A.Reverse();
            if (Signed(B) < 0) B.Reverse();
            var poly = A;
            for (int k = 0; k < 3; k++)
            {
                var e0 = B[k];
                var e1 = B[(k + 1) % 3];
                var output = new List<(float x, float y)>();
                for (int i = 0; i < poly.Count; i++)
                {
                    var p = poly[i];
                    var q = poly[(i + 1) % poly.Count];
                    float sp = (e1.x - e0.x) * (p.y - e0.y) - (e1.y - e0.y) * (p.x - e0.x);
                    float sq = (e1.x - e0.x) * (q.y - e0.y) - (e1.y - e0.y) * (q.x - e0.x);
                    if (sp >= 0) output.Add(p);
                    if ((sp >= 0) != (sq >= 0))
                    {
                        float t = sp / (sp - sq);
                        output.Add((p.x + (q.x - p.x) * t, p.y + (q.y - p.y) * t));
                    }
                }
                poly = output;
                if (poly.Count < 3) return 0f;
            }
            return Math.Abs(Signed(poly));
        }

        [Fact]
        public void GnomeFacesForwardAndHasExpectedRig()
        {
            var m = Load("gnome");
            foreach (var node in new[] { "body", "head", "hat", "hatMid", "hatTip", "armL", "armR", "handL", "handR", "legL", "legR" })
                Assert.NotNull(m.Find(node));

            m.Bounds(out var min, out var max);
            Assert.InRange(min.y, -0.01f, 0.02f); // feet on the ground
            Assert.InRange(max.y, 1.15f, 1.5f); // with the hat

            // Left leg is on -X (Unity is left-handed, x = right)
            Assert.True(m.Find("legL").Position.x < 0);
            Assert.True(m.Find("legR").Position.x > 0);

            // The nose (distinct colour) must be on the +Z (front) side of the head.
            var pal = PaletteData.Parse(File.ReadAllBytes(Path.Combine(TestPaths.Models, "palette.bytes")));
            int ColorAt(SubMeshData s, int i)
            {
                int cx = (int)(s.Uvs[i * 2] * PaletteData.Cells), cy = (int)(s.Uvs[i * 2 + 1] * PaletteData.Cells);
                return pal.Colors[cy * PaletteData.Cells + cx];
            }
            var head = Array.IndexOf(m.Nodes, m.Find("head"));
            var nose = m.Nodes[head].Meshes.SelectMany(s => Enumerable.Range(0, s.VertexCount).Select(i => (s, i)))
                .Where(p => ColorAt(p.s, p.i) == 0xe86f63).Select(p => p.s.Positions[p.i * 3 + 2]).ToArray();
            Assert.NotEmpty(nose);
            Assert.True(nose.Average() > 0.1f, "nose should point to +Z");

            // The floppy hat droops backwards (-Z).
            m.NodeToModel(Array.IndexOf(m.Nodes, m.Find("hatTip")), out var tip, out _, out _);
            Assert.True(tip.z < -0.05f, $"hat tip should droop backwards, got {tip}");

            // Hat meshes are tinted per player.
            Assert.Contains(m.Find("hat").Meshes, s => s.Kind == MeshKind.Tint);
        }

        [Fact]
        public void OldManIsAGiant()
        {
            var m = Load("oldMan");
            m.Bounds(out var min, out var max);
            Assert.InRange(max.y - min.y, 6.2f, 8.5f);
            foreach (var node in new[] { "hips", "spine", "head", "upperArmL", "foreArmL", "handL", "thighR", "shinR", "footR" })
                Assert.NotNull(m.Find(node));
        }

        [Fact]
        public void PaletteCoversEveryColourUsedByModels()
        {
            var pal = PaletteData.Parse(File.ReadAllBytes(Path.Combine(TestPaths.Models, "palette.bytes")));
            Assert.InRange(pal.Colors.Length, 1, PaletteData.Cells * PaletteData.Cells);
            foreach (var f in Directory.GetFiles(TestPaths.Models, "*.bytes"))
            {
                if (f.EndsWith("palette.bytes")) continue;
                var m = ModelData.Parse(File.ReadAllBytes(f));
                foreach (var n in m.Nodes)
                foreach (var s in n.Meshes)
                {
                    if (s.Kind == MeshKind.Tint) continue;
                    for (int i = 0; i < s.VertexCount; i++)
                    {
                        float u = s.Uvs[i * 2], v = s.Uvs[i * 2 + 1];
                        // UVs must sit at cell centres so point sampling never bleeds.
                        Assert.InRange(u * PaletteData.Cells % 1f, 0.49f, 0.51f);
                        Assert.InRange(v * PaletteData.Cells % 1f, 0.49f, 0.51f);
                        int idx = (int)(v * PaletteData.Cells) * PaletteData.Cells + (int)(u * PaletteData.Cells);
                        Assert.True(idx < pal.Colors.Length, $"{f}: uv cell {idx} outside palette");
                    }
                }
            }
        }
    }
}

namespace Gnomes.Tests
{
    public class ModelCoverageTests
    {
        static bool Has(string name) => System.IO.File.Exists(System.IO.Path.Combine(TestPaths.Models, name + ".bytes"));

        [Fact]
        public void EveryItemAndFurnitureHasAModel()
        {
            foreach (var k in Gnomes.Core.ItemDefs.Kinds) Assert.True(Has(k), "missing item model: " + k);
            var L = Gnomes.Core.Level.HouseLayout.Generate(7);
            foreach (var f in L.Furniture) Assert.True(Has(f.Model), "missing furniture model: " + f.Model);
            foreach (var d in L.Garden) Assert.True(Has(d.Model), "missing garden model: " + d.Model);
            foreach (var m in new[] { "gnome", "oldMan", "cat", "greatSock", "village", "knittingCorner", "sockTunnel", "porch", "yarnBasket", "stashBasket", "roof" })
                Assert.True(Has(m), "missing model: " + m);
        }

        [Fact]
        public void InteractiveFurnitureHasItsMarkers()
        {
            Gnomes.Core.Models.ModelData M(string n) => Gnomes.Core.Models.ModelData.Parse(System.IO.File.ReadAllBytes(System.IO.Path.Combine(TestPaths.Models, n + ".bytes")), n);
            Assert.NotNull(M("jarShelf").Find("MECH_lid0"));
            Assert.NotNull(M("jarShelf").Find("ANCHOR_jar2"));
            Assert.NotNull(M("toilet").Find("ZONE_toiletBowl"));
            Assert.NotNull(M("fridge").Find("ZONE_freezer"));
            Assert.NotNull(M("fishTank").Find("ZONE_fishTank"));
            Assert.NotNull(M("plant").Find("ZONE_plantPot"));
            Assert.NotNull(M("catBed").Find("ZONE_catBed"));
            Assert.NotNull(M("parrotCage").Find("ZONE_cageTop"));
            Assert.NotNull(M("stashBasket").Find("ZONE_stash"));
            Assert.NotNull(M("yarnBasket").Find("ZONE_revive"));
            Assert.NotNull(M("sockTunnel").Find("ZONE_portal"));
            Assert.NotNull(M("mousetrap").Find("MECH_snap"));
            Assert.Equal("jar", M("jarShelf").Find("MECH_lid1").Props["role"]);
        }
    }
}
