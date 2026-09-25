using Gnomes.Core;
using Godot;

namespace SockGang.App
{
    /// <summary>Player preferences (user://settings.cfg).</summary>
    public class Settings
    {
        const string Path = "user://settings.cfg";
        public string Name = "Гном";
        public byte Hat;
        public Lang Language = Lang.Ru;
        public float Sensitivity = 2f;
        public float Volume = 0.8f;
        public bool InvertY;
        public float Fov = 75f;
        public string LastIp = "127.0.0.1";
        public int Port = GameConsts.DefaultPort;
        public int Quality = 2; // 0 low, 1 medium, 2 high
        public bool Fullscreen;
        /// <summary>The rainbow hat is found (Konami code in the menu).</summary>
        public bool RainbowUnlocked;

        public static Settings Load()
        {
            var cf = new ConfigFile();
            var s = new Settings();
            bool ok = cf.Load(Path) == Error.Ok;
            s.Name = (string)cf.GetValue("player", "name", "Гном" + GMath.RandInt(10, 99));
            s.Hat = (byte)(int)cf.GetValue("player", "hat", GMath.RandInt(0, 6));
            s.Language = (Lang)(int)cf.GetValue("game", "lang", (int)(OS.GetLocaleLanguage() == "ru" || OS.GetLocaleLanguage() == "uk" || OS.GetLocaleLanguage() == "be" || !ok ? Lang.Ru : Lang.En));
            s.Sensitivity = (float)cf.GetValue("game", "sens", 2f);
            s.Volume = (float)cf.GetValue("game", "volume", 0.8f);
            s.InvertY = (bool)cf.GetValue("game", "invertY", false);
            s.Fov = (float)cf.GetValue("game", "fov", 75f);
            s.LastIp = (string)cf.GetValue("net", "ip", "127.0.0.1");
            s.Port = (int)cf.GetValue("net", "port", GameConsts.DefaultPort);
            s.Quality = (int)cf.GetValue("video", "quality", 2);
            s.Fullscreen = (bool)cf.GetValue("video", "fullscreen", false);
            s.RainbowUnlocked = (bool)cf.GetValue("secrets", "rainbow", false);
            if (!s.RainbowUnlocked) s.Hat &= 0x7f;
            return s;
        }

        public void Save()
        {
            var cf = new ConfigFile();
            cf.SetValue("player", "name", Name ?? "Гном");
            cf.SetValue("player", "hat", (int)Hat);
            cf.SetValue("game", "lang", (int)Language);
            cf.SetValue("game", "sens", Sensitivity);
            cf.SetValue("game", "volume", Volume);
            cf.SetValue("game", "invertY", InvertY);
            cf.SetValue("game", "fov", Fov);
            cf.SetValue("net", "ip", LastIp ?? "127.0.0.1");
            cf.SetValue("net", "port", Port);
            cf.SetValue("video", "quality", Quality);
            cf.SetValue("video", "fullscreen", Fullscreen);
            cf.SetValue("secrets", "rainbow", RainbowUnlocked);
            cf.Save(Path);
        }
    }
}
