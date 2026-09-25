using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gnomes.EditorTools
{
    /// <summary>
    /// One-time project setup that runs when the project is opened: creates the material assets that
    /// keep the needed shaders in builds, the (empty) main scene, build settings and player settings.
    /// Everything else in the game is built from code at runtime, so after this you just press Play.
    /// </summary>
    [InitializeOnLoad]
    public static class SockGangSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        const string MaterialsDir = "Assets/Resources/Materials";
        const string DoneKey = "SockGang.SetupDone.v1";

        static SockGangSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Run(false);
            };
        }

        [MenuItem("Sock Gang/Repair project setup", false, 100)]
        static void RunFromMenu() => Run(true);

        [MenuItem("Sock Gang/Open main scene", false, 1)]
        static void OpenMain()
        {
            Run(false);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        static void Run(bool verbose)
        {
            bool changed = EnsureMaterials();
            changed |= EnsureScene();
            changed |= EnsureBuildScenes();
            string key = DoneKey + "." + Application.dataPath;
            bool firstTime = !EditorPrefs.GetBool(key, false);
            if (firstTime || verbose)
            {
                ApplyPlayerSettings();
                EditorPrefs.SetBool(key, true);
                changed = true;
            }
            if (changed) AssetDatabase.SaveAssets();

            // open the game scene instead of the throwaway "Untitled" one on first launch
            var active = SceneManager.GetActiveScene();
            if (firstTime && string.IsNullOrEmpty(active.path) && !active.isDirty) EditorSceneManager.OpenScene(ScenePath);

            EnsureLegacyInput();
            if (verbose) Debug.Log("[Sock Gang] Project setup is OK. Press Play to start the game.");
        }

        // ------------------------------------------------------------------ materials

        static bool EnsureMaterials()
        {
            bool changed = false;
            EnsureFolder("Assets/Resources");
            EnsureFolder(MaterialsDir);
            changed |= EnsureMaterial("GnomeLit", new[] { "Standard" }, m =>
            {
                m.color = Color.white;
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            });
            changed |= EnsureMaterial("GnomeEmissive", new[] { "Unlit/Texture" }, null);
            changed |= EnsureMaterial("GnomeGlass", new[] { "Legacy Shaders/Transparent/Diffuse", "Transparent/Diffuse" }, m =>
            {
                m.color = new Color(1f, 1f, 1f, 0.35f);
                m.renderQueue = 3000;
            });
            changed |= EnsureMaterial("GnomeUnlitColor", new[] { "Unlit/Color" }, m => m.color = Color.white);
            return changed;
        }

        static bool EnsureMaterial(string name, string[] shaders, System.Action<Material> setup)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return false;
            Shader shader = null;
            foreach (var s in shaders)
            {
                shader = Shader.Find(s);
                if (shader != null) break;
            }
            if (shader == null)
            {
                Debug.LogWarning("[Sock Gang] Shader not found for " + name + " (is the project using the Built-in render pipeline?)");
                return false;
            }
            var m = new Material(shader) { name = name };
            setup?.Invoke(m);
            AssetDatabase.CreateAsset(m, path);
            return true;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ scene + build settings

        /// <summary>The scene ships with the repo; recreate an empty one if it was deleted.</summary>
        static bool EnsureScene()
        {
            if (File.Exists(ScenePath)) return false;
            EnsureFolder("Assets/Scenes");
            File.WriteAllText(ScenePath, EmptySceneYaml);
            AssetDatabase.ImportAsset(ScenePath);
            return true;
        }

        const string EmptySceneYaml =
            "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" +
            "--- !u!104 &2\nRenderSettings:\n  m_ObjectHideFlags: 0\n  serializedVersion: 9\n  m_AmbientMode: 3\n  m_SkyboxMaterial: {fileID: 0}\n" +
            "--- !u!157 &3\nLightmapSettings:\n  m_ObjectHideFlags: 0\n  serializedVersion: 12\n  m_GIWorkflowMode: 1\n";

        static bool EnsureBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length > 0 && scenes[0].path == ScenePath && scenes[0].enabled) return false;
            if (!File.Exists(ScenePath)) return false;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return true;
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "Sock Gang";
            PlayerSettings.companyName = "SockGang";
            PlayerSettings.runInBackground = true; // the host keeps simulating when alt-tabbed
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        /// <summary>The game reads the classic Input Manager; make sure it is enabled.</summary>
        static void EnsureLegacyInput()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var p = so.FindProperty("activeInputHandler");
            if (p == null || p.intValue != 1) return; // 0 = old, 2 = both: fine
            p.intValue = 2;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            if (EditorUtility.DisplayDialog("Sock Gang",
                    "Игре нужен классический Input Manager. Включён режим \"Both\" — редактор нужно перезапустить.\n\n" +
                    "The game uses the classic Input Manager; Active Input Handling was set to \"Both\". Restart the editor now?",
                    "Перезапустить / Restart", "Позже / Later"))
                EditorApplication.OpenProject(Directory.GetCurrentDirectory());
        }
    }
}
