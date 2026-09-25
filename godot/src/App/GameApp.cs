using System;
using System.Collections.Generic;
using Gnomes.Core;
using Gnomes.Core.Protocol;
using Gnomes.Core.Rules;
using Gnomes.Net;
using Godot;
using SockGang.Audio;
using SockGang.Players;
using SockGang.Session;
using SockGang.UI;
using SockGang.World;

namespace SockGang.App
{
    public enum AppScreen { Menu, HostSetup, JoinSetup, Connecting, Settings, Playing, Report }

    /// <summary>Application root: menus, session lifetime, transitions and notifications.</summary>
    public partial class GameApp : Node
    {
        public static GameApp I;
        public Settings Settings;
        AppScreen screen = AppScreen.Menu;
        public AppScreen SettingsReturn = AppScreen.Menu;
        public bool Paused, CraftOpen, SockOpen, ChatOpen, Loading;
        public string MenuMessage;
        public NightReport Report;
        public LanDiscovery Discovery;
        public Dictionary<string, string> Args;
        Node3D worlds;
        Camera3D menuCam;
        float menuOrbit;
        UiRoot ui;
        readonly List<(string text, float until, Color color)> toasts = new List<(string, float, Color)>();

        public GameSession Session => GameSession.I;
        public IReadOnlyList<(string text, float until, Color color)> Toasts => toasts;
        public event Action ScreenChanged;

        public AppScreen Screen
        {
            get => screen;
            set
            {
                if (screen == value) return;
                screen = value;
                ScreenChanged?.Invoke();
            }
        }

        public bool GameplayInput => Screen == AppScreen.Playing && !Paused && !CraftOpen && !SockOpen && !ChatOpen && !Loading && (GetWindow().HasFocus() || Keys.Simulated.Count > 0);

        public static Dictionary<string, string> UserArgs()
        {
            var d = new Dictionary<string, string>();
            foreach (var a in OS.GetCmdlineUserArgs())
            {
                var s = a.TrimStart('-');
                int eq = s.IndexOf('=');
                if (eq > 0) d[s.Substring(0, eq)] = s.Substring(eq + 1);
                else d[s] = "1";
            }
            return d;
        }

        public override void _Ready()
        {
            Args = UserArgs();
            if (Args.ContainsKey("shot-model") || Args.ContainsKey("shot-level") || Args.ContainsKey("dump-tex"))
            {
                AddChild(new Dev.ShotHarness { Args = Args });
                return;
            }
            I = this;
            ProcessMode = ProcessModeEnum.Always;
            Settings = Settings.Load();
            Loc.Current = Settings.Language;
            Engine.MaxFps = 144;
            ApplyVideo(Settings);
            worlds = new Node3D { Name = "Worlds" };
            AddChild(worlds);
            WorldLoader.Parent = worlds;
            Sfx.Create(this);
            Sfx.I.Volume = Settings.Volume;
            Keys.Watch(Key.W, Key.A, Key.S, Key.D, Key.Space, Key.Shift, Key.Ctrl, Key.C, Key.E, Key.F, Key.Q, Key.G, Key.H, Key.R, Key.T, Key.V, Key.Tab, Key.Escape, Key.Enter, Key.F3, Key.F6, Key.F7, Key.F8, Key.F9);
            ui = new UiRoot { Name = "UI" };
            AddChild(ui);
            AddChild(new DebugOverlay { Name = "Debug" });
            GetTree().AutoAcceptQuit = false;
            ShowMenuBackdrop();
            if (Args.ContainsKey("autotest")) AddChild(new Dev.AutoTest { Name = "AutoTest", Args = Args });
        }

        public static void ApplyVideo(Settings s)
        {
            var vp = ((SceneTree)Engine.GetMainLoop()).Root;
            vp.Msaa3D = s.Quality >= 2 ? Viewport.Msaa.Msaa4X : s.Quality == 1 ? Viewport.Msaa.Msaa2X : Viewport.Msaa.Disabled;
            vp.ScreenSpaceAA = s.Quality >= 1 ? Viewport.ScreenSpaceAAEnum.Disabled : Viewport.ScreenSpaceAAEnum.Fxaa;
            vp.PositionalShadowAtlasSize = s.Quality == 0 ? 1024 : 2048;
            RenderingServer.DirectionalShadowAtlasSetSize(s.Quality == 0 ? 1024 : s.Quality == 1 ? 2048 : 4096, true);
            DisplayServer.WindowSetMode(s.Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        }

        // ------------------------------------------------------------------ flow

        void ShowMenuBackdrop()
        {
            Paused = CraftOpen = SockOpen = ChatOpen = Loading = false;
            WorldLoader.Build(LevelKind.Hub, 1, false, null);
            if (menuCam == null)
            {
                menuCam = new Camera3D { Name = "MenuCamera", Fov = 60, Near = 0.05f, Far = 400f };
                AddChild(menuCam);
            }
            menuCam.Current = true;
            Sfx.I.SetMusic(2);
            Screen = AppScreen.Menu;
            ScreenChanged?.Invoke();
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
                Fail(Loc.T("portFailed") + " " + Settings.Port + ": " + e.Message);
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
            catch (Exception e) { GD.PushWarning(e.Message); }
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
            if (Session != null && Session.IsHost) SaveStore.Save(Session.Save);
            Session?.Shutdown();
            MenuMessage = null;
            ShowMenuBackdrop();
        }

        public void OnSessionLost(string error) => CallDeferred(nameof(FailDeferred), error);

        void FailDeferred(string error) => Fail(error);

        public void ShowLoading(bool on)
        {
            Loading = on;
            ui?.SetLoading(on);
        }

        public void OnLevelLoaded(LevelKind kind)
        {
            Paused = CraftOpen = SockOpen = ChatOpen = false;
            if (menuCam != null) menuCam.Current = false;
            if (LocalGnome.I?.Cam != null) LocalGnome.I.Cam.Current = true;
            Sfx.I.SetMusic(kind == LevelKind.House ? 1 : 2);
            var s = Session;
            if (s?.Save != null) Atmosphere.DarkVisionBoost = GearEffects.From(s.Save).DarkVision * 0.8f;
            Screen = AppScreen.Playing;
            ui?.OnLevelLoaded(kind);
            if (kind == LevelKind.House) Toast(Loc.T("stashHint"));
            else Toast(Loc.T("hub") + " — " + Loc.T("day") + " " + (s != null ? s.Night : 1));
        }

        public void OnNightEnd(NightReport r)
        {
            Report = r;
            toasts.Clear();
            Paused = CraftOpen = SockOpen = ChatOpen = false;
            Screen = AppScreen.Report;
            Sfx.I?.PlayUi(r.Passed ? SoundId.TaskDone : SoundId.Death);
        }

        public void ContinueFromReport()
        {
            if (Session != null && Session.IsHost) Session.Host.ContinueAfterReport();
        }

        public void OnTaskDone(TaskDef t)
        {
            Toast("+ " + Loc.T("taskDone") + ": " + t.Text(Loc.Current), new Color(0.6f, 1f, 0.5f));
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

        public void Toast(string text) => Toast(text, Colors.White);

        public void Toast(string text, Color c)
        {
            if (string.IsNullOrEmpty(text)) return;
            toasts.Add((text, Clock.Now + 4f, c));
            if (toasts.Count > 6) toasts.RemoveAt(0);
        }

        /// <summary>Run an action after a delay (on the main thread).</summary>
        public void Delay(float seconds, Action a)
        {
            var t = GetTree().CreateTimer(seconds);
            t.Timeout += () => a();
        }

        // ------------------------------------------------------------------ frame

        public override void _Input(InputEvent e) => Keys.OnInput(e);

        public override void _Process(double delta)
        {
            if (I != this) return;
            Clock.Advance(delta);
            Keys.Update();
            Phys.Space = GetViewport().World3D?.DirectSpaceState;
            Session?.Update(Clock.Dt);
            Discovery?.Poll();
            for (int i = toasts.Count - 1; i >= 0; i--) if (Clock.Now > toasts[i].until) toasts.RemoveAt(i);
            if (Sfx.I != null) Sfx.I.Volume = Settings.Volume;
            Loc.Current = Settings.Language;

            if (Screen == AppScreen.Playing)
            {
                if (Keys.Pressed(Key.Escape))
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
                if (Keys.Pressed(Key.Enter) && !ChatOpen && Session != null && !Session.IsSolo) ChatOpen = true;
            }
            var wantMode = GameplayInput ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
            if (Input.MouseMode != wantMode) Input.MouseMode = wantMode;

            // menu camera sways gently between the tunnel and the Great Sock, clear of the porch ceiling and walls
            if (Screen != AppScreen.Playing && Screen != AppScreen.Report && menuCam != null && menuCam.Current)
            {
                menuOrbit += Clock.Dt * 0.12f;
                float s = Mathf.Sin(Mathf.Sin(menuOrbit) * 0.9f);
                menuCam.GlobalPosition = new Vector3(s * 3f, 2.2f, -4.6f);
                menuCam.LookAt(new Vector3(-s * 1.2f, 0.8f, 5f), Vector3.Up);
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (I != this) return;
            Phys.Space = GetViewport().World3D?.DirectSpaceState;
            Session?.PhysicsUpdate((float)delta);
        }

        public override void _Notification(int what)
        {
            if (what == NotificationWMCloseRequest) Quit();
        }

        public void Quit()
        {
            Settings?.Save();
            if (Session != null && Session.IsHost) SaveStore.Save(Session.Save);
            Session?.Shutdown();
            StopDiscovery();
            GetTree().Quit();
        }
    }
}
