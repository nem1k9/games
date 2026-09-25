using System;
using Gnomes.Core.Rules;
using Godot;

namespace SockGang.App
{
    /// <summary>The gang's progression lives in a small text file in the user data folder.</summary>
    public static class SaveStore
    {
        const string Path = "user://sockgang_save.txt";

        public static SaveData Load()
        {
            try
            {
                if (FileAccess.FileExists(Path)) return SaveData.Deserialize(FileAccess.GetFileAsString(Path));
            }
            catch (Exception e)
            {
                GD.PushWarning("[Save] could not read save: " + e.Message);
            }
            return new SaveData();
        }

        public static void Save(SaveData s)
        {
            try
            {
                using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
                f?.StoreString(s.Serialize());
            }
            catch (Exception e)
            {
                GD.PushWarning("[Save] could not write save: " + e.Message);
            }
        }

        public static void Reset()
        {
            if (FileAccess.FileExists(Path)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(Path));
        }
    }
}
