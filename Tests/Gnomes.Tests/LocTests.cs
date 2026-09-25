using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gnomes.Core;
using Xunit;

namespace Gnomes.Tests
{
    public class LocTests
    {
        /// <summary>Every string key the game code shows (Loc.T, toasts, host messages) exists in RU and EN.</summary>
        [Fact]
        public void EveryKeyUsedInCodeIsTranslated()
        {
            var roots = new[] { Path.Combine(TestPaths.Repo, "Assets", "Scripts"), Path.Combine(TestPaths.Repo, "godot", "src") };
            var patterns = new[]
            {
                new Regex(@"Loc\.T\(""([A-Za-z0-9_]+)""\)"),
                new Regex(@"LocalToast\(""([A-Za-z0-9_]+)"""),
                new Regex(@"Message\(""([A-Za-z0-9_]+)"""),
                new Regex(@"Type = EvType\.Message, S = ""([A-Za-z0-9_]+)"""),
            };
            var missing = roots.Where(Directory.Exists).SelectMany(r => Directory.GetFiles(r, "*.cs", SearchOption.AllDirectories))
                .SelectMany(f => patterns.SelectMany(p => p.Matches(File.ReadAllText(f)).Select(m => (file: Path.GetFileName(f), key: m.Groups[1].Value))))
                .Where(x => !Loc.Has(x.key))
                .Select(x => x.key + " (" + x.file + ")")
                .Distinct()
                .ToList();
            Assert.True(missing.Count == 0, "untranslated keys: " + string.Join(", ", missing));
        }

        [Theory]
        [InlineData(1, "вещь"), InlineData(2, "вещи"), InlineData(4, "вещи"), InlineData(5, "вещей"), InlineData(0, "вещей")]
        [InlineData(11, "вещей"), InlineData(12, "вещей"), InlineData(14, "вещей"), InlineData(21, "вещь"), InlineData(22, "вещи"), InlineData(111, "вещей")]
        public void RussianPlurals(int n, string expected)
        {
            Assert.Equal(expected, Loc.RuPlural(n, "вещь", "вещи", "вещей"));
        }

        [Fact]
        public void ChaosCountReadsNaturally()
        {
            var was = Loc.Current;
            try
            {
                Loc.Current = Lang.Ru;
                Assert.Equal("2 вещи не на своих местах", Loc.ChaosCount(2));
                Loc.Current = Lang.En;
                Assert.Equal("1 thing out of place", Loc.ChaosCount(1));
            }
            finally { Loc.Current = was; }
        }

        [Fact]
        public void TranslationsAreNotEmpty()
        {
            foreach (var key in new[] { "title", "solo", "host", "join", "caught", "trapped", "dawn", "parrotAlarm" })
            {
                Assert.False(string.IsNullOrWhiteSpace(Loc.T(key, Lang.Ru)), key);
                Assert.False(string.IsNullOrWhiteSpace(Loc.T(key, Lang.En)), key);
            }
        }
    }
}
