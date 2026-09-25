using System;
using System.Collections.Generic;
using Gnomes.Audio;
using Gnomes.Core;
using Gnomes.Core.Protocol;
using Gnomes.Core.Rules;
using Gnomes.Net;
using Gnomes.Players;
using Gnomes.Session;
using Gnomes.UI;
using Gnomes.World;
using UnityEngine;

namespace Gnomes.App
{
    /// <summary>Starts the game automatically when Play is pressed (no scene setup needed).</summary>
    public static class GameBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameApp.I != null) return;
            // switch off whatever the default scene had (camera, sun) - we build everything ourselves
#if UNITY_2023_1_OR_NEWER
            var cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            var cams = UnityEngine.Object.FindObjectsOfType<Camera>();
            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
#endif
            foreach (var cam in cams) cam.gameObject.SetActive(false);
            foreach (var l in lights) if (l.type == LightType.Directional) l.gameObject.SetActive(false);
            var go = new GameObject("SockGang");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<GameApp>();
        }
    }

    public enum AppScreen { Menu, HostSetup, JoinSetup, Connecting, Settings, Playing, Report }

    /// <summary>Application root: menus, session lifetime, transitions and notifications.</summary>
    public class GameApp : MonoBehaviour
    {
        public static GameApp I;
        public Settings Settings;
        public AppScreen Screen = AppScreen.Menu;
        public AppScreen SettingsReturn = AppScreen.Menu;
        public bool Paused, CraftOpen, SockOpen, ChatOpen;
        public string MenuMessage;
        public NightReport Report;
        public LanDiscovery Discovery;
        Camera menuCam;
        float menuOrbit;
        readonly List<(string text, float until, Color color)> toasts = new List<(string, float, Color)>();

        public GameSession Session => GameSession.I;
        public IReadOnlyList<(string text, float until, Color color)> Toasts => toasts;

        public bool GameplayInput => Screen == AppScreen.Playing && !Paused && !CraftOpen && !SockOpen && !ChatOpen && Application.isFocused;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 144;
            Application.runInBackground = true; // hosts must keep simulating when alt-tabbed
            Settings = Settings.Load();
            Loc.Current = Settings.Language;
            Layers.SetupCollisionMatrix();
            Physics.gravity = new Vector3(0, GameConsts.Gravity, 0);
            Time.fixedDeltaTime = 1f / 60f;
            Sfx.Create(transform);
            Sfx.I.Volume = Settings.Volume;
            gameObject.AddComponent<UiRoot>();
            ShowMenuBackdrop();
        }

        // ------------------------------------------------------------------ flow

        void ShowMenuBackdrop()
        {
            Screen = AppScreen.Menu;
            Paused = CraftOpen = SockOpen = false;
            WorldLoader.Build(LevelKind.Hub, 1, false, null);
            if (menuCam == null)
            {
                var go = new GameObject("MenuCamera");
                go.transform.SetParent(transform, false);
                menuCam = go.AddComponent<Camera>();
                menuCam.clearFlags = CameraClearFlags.SolidColor;
                menuCam.backgroundColor = new Color(0.04f, 0.035f, 0.05f);
                menuCam.nearClipPlane = 0.05f;
                menuCam.fieldOfView = 60f;
                go.AddComponent<AudioListener>();
            }
            menuCam.gameObject.SetActive(true);
            Sfx.I.SetMusic(2);
        }

        public void StartSolo()
        {
            Settings.Save();
            StopDiscovery();
            try
            {
                GameSession.StartSolo(Settings.Name, Settings.Hat, SaveStore.Load());
            }
            catch (Exception e)
            {
                Fail(e.Message);
            }
        }

        public void StartHost()
        {
            Settings.Save();
            StopDiscovery();
            try
            {
                GameSession.StartHost(Settings.Name, Settings.Hat, SaveStore.Load(), Settings.Port);
            }
            catch (Exception e)
            {
                Fail("Не удалось открыть порт " + Settings.Port + ": " + e.Message);
            }
        }

        public void StartJoin(string ip, int port)
        {
            Settings.LastIp = ip;
            Settings.Port = port;
            Settings.Save();
            StopDiscovery();
            try
            {
                GameSession.StartClient(Settings.Name, Settings.Hat, ip, port);
                Screen = AppScreen.Connecting;
            }
            catch (Exception e)
            {
                Fail(e.Message);
            }
        }

        public void StartDiscovery()
        {
            if (Discovery != null) return;
            try { Discovery = new LanDiscovery(Settings.Port); }
            catch (Exception e) { Debug.LogWarning(e.Message); }
        }

        public void StopDiscovery()
        {
            Discovery?.Dispose();
            Discovery = null;
        }

        void Fail(string message)
        {
            Session?.Shutdown();
            MenuMessage = message;
            ShowMenuBackdrop();
        }

        public void LeaveToMenu()
        {
            Session?.Shutdown();
            MenuMessage = null;
            ShowMenuBackdrop();
        }

        public void OnSessionLost(string error)
        {
            Fail(error);
        }

        public void OnLevelLoaded(LevelKind kind)
        {
            Screen = AppScreen.Playing;
            Paused = CraftOpen = SockOpen = false;
            if (menuCam) menuCam.gameObject.SetActive(false);
            Sfx.I.SetMusic(kind == LevelKind.House ? 1 : 2);
            var s = Session;
            if (s != null && s.Save != null) Atmosphere.DarkVisionBoost = GearEffects.From(s.Save).DarkVision * 0.8f;
            if (kind == LevelKind.House) Toast(Loc.T("stashHint"));
            else Toast(Loc.T("hub") + " — " + Loc.T("day") + " " + (s != null ? s.Night : 1));
        }

        public void OnNightEnd(NightReport r)
        {
            Report = r;
            Screen = AppScreen.Report;
            Paused = CraftOpen = SockOpen = false;
            Sfx.I?.PlayUi(r.Passed ? SoundId.TaskDone : SoundId.Death);
        }

        public void ContinueFromReport()
        {
            if (Session != null && Session.IsHost) Session.Host.ContinueAfterReport();
        }

        public void OnTaskDone(TaskDef t)
        {
            Toast("✓ " + Loc.T("taskDone") + ": " + t.Text(Loc.Current), new Color(0.6f, 1f, 0.5f));
            Sfx.I?.PlayUi(SoundId.TaskDone);
        }

        public void OnBanked(string kind, int value)
        {
            if (ItemDefs.TryGet(kind, out var d)) Toast(Loc.T("banked") + ": " + d.Name(Loc.Current) + " (+" + value + ")", new Color(1f, 0.9f, 0.5f));
        }

        public void ShowGreatSock()
        {
            SockOpen = true;
            Sfx.I?.PlayUi(SoundId.Sparkle);
        }

        public void Toast(string text) => Toast(text, Color.white);

        public void Toast(string text, Color c)
        {
            if (string.IsNullOrEmpty(text)) return;
            toasts.Add((text, Time.unscaledTime + 4f, c));
            if (toasts.Count > 6) toasts.RemoveAt(0);
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            Session?.Update();
            Discovery?.Poll();
            for (int i = toasts.Count - 1; i >= 0; i--) if (Time.unscaledTime > toasts[i].until) toasts.RemoveAt(i);
            if (Sfx.I != null) Sfx.I.Volume = Settings.Volume;
            Loc.Current = Settings.Language;

            if (Screen == AppScreen.Playing)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (CraftOpen || SockOpen || ChatOpen) CraftOpen = SockOpen = ChatOpen = false;
                    else Paused = !Paused;
                }
                var lg = LocalGnome.I;
                if (lg != null && lg.CraftMenuRequested)
                {
                    lg.CraftMenuRequested = false;
                    CraftOpen = true;
                }
                if (Input.GetKeyDown(KeyCode.Return) && !ChatOpen && Session != null && !Session.IsSolo) ChatOpen = true;
            }
            bool lockCursor = GameplayInput;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;

            // orbiting menu camera over the village
            if (Screen != AppScreen.Playing && Screen != AppScreen.Report && menuCam != null && menuCam.gameObject.activeSelf)
            {
                menuOrbit += Time.deltaTime * 0.08f;
                var c = new Vector3(0, 1.2f, -1f);
                menuCam.transform.position = c + new Vector3(Mathf.Sin(menuOrbit) * 9f, 1.6f + Mathf.Sin(menuOrbit * 0.7f) * 0.4f, Mathf.Cos(menuOrbit) * 7f);
                menuCam.transform.LookAt(c + Vector3.down * 0.4f);
            }
        }

        void FixedUpdate()
        {
            Session?.FixedUpdate();
        }

        void OnApplicationQuit()
        {
            Settings?.Save();
            if (Session != null && Session.IsHost) SaveStore.Save(Session.Save);
            Session?.Shutdown();
            StopDiscovery();
        }
    }
}
