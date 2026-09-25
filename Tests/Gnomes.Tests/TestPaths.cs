using System;
using System.IO;

namespace Gnomes.Tests
{
    public static class TestPaths
    {
        public static string Repo
        {
            get
            {
                var d = new DirectoryInfo(AppContext.BaseDirectory);
                while (d != null && !Directory.Exists(Path.Combine(d.FullName, "Assets")))
                    d = d.Parent;
                if (d == null) throw new InvalidOperationException("Repo root not found");
                return d.FullName;
            }
        }

        public static string Models => Path.Combine(Repo, "Assets", "Resources", "Models");
    }
}
