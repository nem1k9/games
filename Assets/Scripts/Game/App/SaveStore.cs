using System;
using System.IO;
using Gnomes.Core.Rules;
using UnityEngine;

namespace Gnomes.App
{
    /// <summary>The gang's progression lives in a small text file next to the player's data.</summary>
    public static class SaveStore
    {
        static string PathFor => System.IO.Path.Combine(Application.persistentDataPath, "sockgang_save.txt");

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(PathFor)) return SaveData.Deserialize(File.ReadAllText(PathFor));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] could not read save: " + e.Message);
            }
            return new SaveData();
        }

        public static void Save(SaveData s)
        {
            try
            {
                File.WriteAllText(PathFor, s.Serialize());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] could not write save: " + e.Message);
            }
        }

        public static void Reset()
        {
            try
            {
                if (File.Exists(PathFor)) File.Delete(PathFor);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
            }
        }
    }
}
