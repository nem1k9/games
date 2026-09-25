using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Gnomes.EditorTools
{
    /// <summary>Makes sure materials, scene and build settings are in place before any build (menu, Build Settings or CI).</summary>
    public class SockGangPreBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report) => SockGangSetup.EnsureForBuild();
    }
}
