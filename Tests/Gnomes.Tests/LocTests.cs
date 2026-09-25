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
            var src = Path.Combine(TestPaths.Repo, "Assets", "Scripts");
            var patterns = new[]
            {
                new Regex(@"Loc\.T\(""([A-Za-z0-9_]+)""\)"),
                new Regex(@"LocalToast\(""([A-Za-z0-9_]+)"""),
                new Regex(@"Message\(""([A-Za-z0-9_]+)"""),
                new Regex(@"Type = EvType\.Message, S = ""([A-Za-z0-9_]+)"""),
            };
            var missing = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => patterns.SelectMany(p => p.Matches(File.ReadAllText(f)).Select(m => (file: Path.GetFileName(f), key: m.Groups[1].Value))))
                .Where(x => !Loc.Has(x.key))
                .Select(x => x.key + " (" + x.file + ")")
                .Distinct()
                .ToList();
            Assert.True(missing.Count == 0, "untranslated keys: " + string.Join(", ", missing));
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
