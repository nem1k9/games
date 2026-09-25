using System;
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
                        int i0 = s.Indices[t * 3];
                        var nrm = new V3(s.Normals[i0 * 3], s.Normals[i0 * 3 + 1], s.Normals[i0 * 3 + 2]);
                        var c = V3.Cross(P(1) - P(0), P(2) - P(0));
                        if (c.sqrMagnitude < 1e-14f) continue; // degenerate sliver
                        if (V3.Dot(c, nrm) <= 0) bad++;
                    }
                    Assert.True(bad == 0, $"{name}/{n.Name}: {bad} of {tris} triangles have winding opposite to their normal");
                }
                Assert.False(float.IsNaN(n.Position.x) || float.IsNaN(n.Rotation.w));
            }
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
