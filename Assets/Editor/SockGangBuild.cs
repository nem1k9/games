using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Gnomes.EditorTools
{
    /// <summary>One-click player builds into Builds/&lt;platform&gt;/ (also usable from the command line via -executeMethod).</summary>
    public static class SockGangBuild
    {
        [MenuItem("Sock Gang/Build/Windows (x64)", false, 20)]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/SockGang.exe");

        [MenuItem("Sock Gang/Build/Linux (x64)", false, 21)]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/SockGang.x86_64");

        [MenuItem("Sock Gang/Build/macOS", false, 22)]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/SockGang.app");

        static void Build(BuildTarget target, string output)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SockGangSetup.ScenePath },
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log("[Sock Gang] Build OK: " + output + " (" + (s.totalSize / (1024 * 1024)) + " MB)");
                if (!Application.isBatchMode) EditorUtility.RevealInFinder(output);
            }
            else
            {
                Debug.LogError("[Sock Gang] Build failed: " + s.result + ", errors: " + s.totalErrors);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
