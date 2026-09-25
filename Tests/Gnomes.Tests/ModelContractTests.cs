using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gnomes.Core.Models;
using Xunit;

namespace Gnomes.Tests
{
    /// <summary>
    /// The game code looks up model nodes by name (bones, anchors, zones). Blender scripts create them.
    /// These tests keep both sides in sync so a renamed node can't silently break a mechanic.
    /// </summary>
    public class ModelContractTests
    {
        static readonly Dictionary<string, ModelData> models = Directory.GetFiles(TestPaths.Models, "*.bytes")
            .Where(f => Path.GetFileNameWithoutExtension(f) != "palette")
            .ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => ModelData.Parse(File.ReadAllBytes(f), Path.GetFileNameWithoutExtension(f)));

        static bool AnyModelHas(string node) => models.Values.Any(m => m.Find(node) != null);

        static IEnumerable<string> Sources() =>
            Directory.GetFiles(Path.Combine(TestPaths.Repo, "Assets", "Scripts", "Game"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText);

        [Fact]
        public void EveryNodeTheCodeLooksUpExists()
        {
            var lookups = new List<string>();
            foreach (var src in Sources())
            {
                foreach (Match m in Regex.Matches(src, @"\.Node\(""([A-Za-z0-9_]+)""\)")) lookups.Add(m.Groups[1].Value);
                foreach (Match m in Regex.Matches(src, @"AnchorPos\(""([A-Za-z0-9_]+)""")) lookups.Add("ANCHOR_" + m.Groups[1].Value);
                foreach (Match m in Regex.Matches(src, @"Zones\.TryGetValue\(""([A-Za-z0-9_]+)""")) lookups.Add("ZONE_" + m.Groups[1].Value);
            }
            Assert.True(lookups.Count > 40, "regexes found too few lookups: " + lookups.Count);
            var missing = lookups.Distinct().Where(n => !AnyModelHas(n)).ToList();
            Assert.True(missing.Count == 0, "nodes referenced in code but missing from every model: " + string.Join(", ", missing));
        }

        [Fact]
        public void EveryZoneAPrankNeedsExists()
        {
            var src = File.ReadAllText(Path.Combine(TestPaths.Repo, "Assets", "Scripts", "Core", "Rules", "Tasks.cs"));
            var zones = Regex.Matches(src, @"e\.Zone == ""([A-Za-z0-9_]+)""").Select(m => m.Groups[1].Value).Distinct().ToList();
            Assert.NotEmpty(zones);
            var missing = zones.Where(z => !AnyModelHas("ZONE_" + z)).ToList();
            Assert.True(missing.Count == 0, "pranks use zones no model has: " + string.Join(", ", missing));
        }

        [Fact]
        public void JarShelfHasAnAnchorPerLid()
        {
            var shelf = models["jarShelf"];
            var lids = shelf.Nodes.Where(n => n.Name.StartsWith("MECH_lid")).Select(n => n.Name.Substring("MECH_lid".Length)).ToList();
            Assert.Equal(3, lids.Count);
            // HostLogic.JarPos: "jar" + last char of the mech name
            foreach (var i in lids) Assert.NotNull(shelf.Find("ANCHOR_jar" + i));
            Assert.NotNull(shelf.Find("ANCHOR_free"));
        }

        [Fact]
        public void CharactersHaveTheirBones()
        {
            foreach (var b in new[] { "hips", "spine", "neck", "head", "upperArmL", "upperArmR", "foreArmL", "foreArmR", "handL", "handR", "thighL", "thighR", "shinL", "shinR" })
                Assert.True(models["oldMan"].Find(b) != null, "oldMan." + b);
            foreach (var b in new[] { "body", "head", "tail1", "tail2", "tail3", "legFL", "legFR", "legBL", "legBR" })
                Assert.True(models["cat"].Find(b) != null, "cat." + b);
            Assert.NotNull(models["parrotCage"].Find("parrot"));
            Assert.NotNull(models["bed"].Find("ANCHOR_sleep"));
        }
    }
}
