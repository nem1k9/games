using Gnomes.Core;
using UnityEngine;

namespace Gnomes.App
{
    /// <summary>Player preferences (stored in PlayerPrefs).</summary>
    public class Settings
    {
        public string Name = "Гном";
        public byte Hat;
        public Lang Language = Lang.Ru;
        public float Sensitivity = 2f;
        public float Volume = 0.8f;
        public bool InvertY;
        public float Fov = 75f;
        public string LastIp = "127.0.0.1";
        public int Port = GameConsts.DefaultPort;
        public int Quality = 2;

        public static Settings Load()
        {
            var s = new Settings
            {
                Name = PlayerPrefs.GetString("sg_name", "Гном" + Random.Range(10, 99)),
                Hat = (byte)PlayerPrefs.GetInt("sg_hat", Random.Range(0, 6)),
                Language = (Lang)PlayerPrefs.GetInt("sg_lang", 0),
                Sensitivity = PlayerPrefs.GetFloat("sg_sens", 2f),
                Volume = PlayerPrefs.GetFloat("sg_vol", 0.8f),
                InvertY = PlayerPrefs.GetInt("sg_invy", 0) == 1,
                Fov = PlayerPrefs.GetFloat("sg_fov", 75f),
                LastIp = PlayerPrefs.GetString("sg_ip", "127.0.0.1"),
                Port = PlayerPrefs.GetInt("sg_port", GameConsts.DefaultPort),
                Quality = PlayerPrefs.GetInt("sg_quality", 2),
            };
            return s;
        }

        public void Save()
        {
            PlayerPrefs.SetString("sg_name", Name);
            PlayerPrefs.SetInt("sg_hat", Hat);
            PlayerPrefs.SetInt("sg_lang", (int)Language);
            PlayerPrefs.SetFloat("sg_sens", Sensitivity);
            PlayerPrefs.SetFloat("sg_vol", Volume);
            PlayerPrefs.SetInt("sg_invy", InvertY ? 1 : 0);
            PlayerPrefs.SetFloat("sg_fov", Fov);
            PlayerPrefs.SetString("sg_ip", LastIp);
            PlayerPrefs.SetInt("sg_port", Port);
            PlayerPrefs.SetInt("sg_quality", Quality);
            PlayerPrefs.Save();
        }
    }
}
