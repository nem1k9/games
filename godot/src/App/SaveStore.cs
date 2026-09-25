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

        /// <summary>Builds before "The Sock Gang" kept their files in a "Sock Gang" folder next to ours: bring them over once.</summary>
        public static void MigrateOldFolder()
        {
            try
            {
                string dir = OS.GetUserDataDir();
                string old = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dir) ?? "", "Sock Gang");
                if (old == dir || !System.IO.Directory.Exists(old)) return;
                System.IO.Directory.CreateDirectory(dir);
                foreach (var name in new[] { "sockgang_save.txt", "settings.cfg" })
                {
                    string from = System.IO.Path.Combine(old, name), to = System.IO.Path.Combine(dir, name);
                    if (System.IO.File.Exists(from) && !System.IO.File.Exists(to)) System.IO.File.Copy(from, to);
                }
            }
            catch (Exception e)
            {
                GD.PushWarning("[Save] could not move old files: " + e.Message);
            }
        }

        public static void Reset()
        {
            if (FileAccess.FileExists(Path)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(Path));
        }
    }
}
