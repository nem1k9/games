using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Gnomes.Core;
using Gnomes.Core.Protocol;
using Gnomes.Core.Rules;
using Godot;
using SockGang.App;
using SockGang.NPC;
using SockGang.Players;
using SockGang.Session;
using SockGang.World;

namespace SockGang.UI
{
    /// <summary>All screens and the HUD, built from Godot controls in code (no scene files needed).</summary>
    public partial class UiRoot : CanvasLayer
    {
        GameApp App => GameApp.I;
        GameSession S => GameSession.I;
        Theme theme;
        Control root, screenRoot, hudRoot, overlayRoot;
        VBoxContainer toastBox;
        Label loadingLabel;
        AppScreen builtScreen = (AppScreen)(-1);
        string overlayKey = "";
        LevelKind? hudKind;
        bool tasksExpanded = true;
        string[] localIps;

        // HUD widgets
        Label clockLabel, tasksLabel, chaosLabel, pocketLabel, grandpaLabel, stepsLabel, lightLabel, playersLabel, promptLabel, yarnLabel, villageLabel, waitLabel;
        ProgressBar clockBar, holdBar, gripBar;
        ColorRect crosshair;
        PanelContainer grandpaPanel, stealthPanel, tasksPanel;
        // join screen
        VBoxContainer lanList;
        string lanSig = "";
        Label connectingLabel;

        static readonly Color Gold = new Color(1f, 0.85f, 0.35f);
        static readonly Color Cream = new Color(0.97f, 0.95f, 0.9f);

        public override void _Ready()
        {
            Layer = 10;
            theme = BuildTheme();
            root = new Control { Name = "Root", Theme = theme, MouseFilter = Control.MouseFilterEnum.Ignore };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(root);
            hudRoot = Layer0("Hud");
            screenRoot = Layer0("Screen");
            overlayRoot = Layer0("Overlay");
            toastBox = new VBoxContainer { Name = "Toasts", MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Begin };
            toastBox.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            toastBox.OffsetLeft = -600;
            toastBox.OffsetRight = 600;
            toastBox.OffsetTop = 150;
            toastBox.OffsetBottom = 420;
            root.AddChild(toastBox);
            loadingLabel = Text(Loc.T("loading"), 40, Gold);
            loadingLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
            loadingLabel.HorizontalAlignment = HorizontalAlignment.Center;
            loadingLabel.Visible = false;
            root.AddChild(loadingLabel);
            App.ScreenChanged += () => builtScreen = (AppScreen)(-1);
        }

        Control Layer0(string name)
        {
            var c = new Control { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };
            c.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            root.AddChild(c);
            return c;
        }

        // ------------------------------------------------------------------ theme & helpers

        static StyleBoxFlat Box(Color bg, int radius = 12, int pad = 18)
        {
            var sb = new StyleBoxFlat { BgColor = bg };
            sb.SetCornerRadiusAll(radius);
            sb.ContentMarginLeft = sb.ContentMarginRight = pad;
            sb.ContentMarginTop = sb.ContentMarginBottom = pad * 0.75f;
            return sb;
        }

        static Theme BuildTheme()
        {
            var t = new Theme();
            var font = new SystemFont { FontNames = new[] { "Segoe UI", "Arial", "Noto Sans", "DejaVu Sans", "Liberation Sans", "sans-serif" }, FontWeight = 600 };
            t.DefaultFont = font;
            t.DefaultFontSize = 22;
            t.SetStylebox("panel", "PanelContainer", Box(new Color(0.09f, 0.07f, 0.1f, 0.9f)));
            t.SetStylebox("panel", "Panel", Box(new Color(0.09f, 0.07f, 0.1f, 0.9f)));
            var btn = Box(new Color(0.78f, 0.24f, 0.2f), 12, 16);
            t.SetStylebox("normal", "Button", btn);
            t.SetStylebox("hover", "Button", Box(new Color(0.9f, 0.34f, 0.26f), 12, 16));
            t.SetStylebox("pressed", "Button", Box(new Color(0.6f, 0.16f, 0.14f), 12, 16));
            t.SetStylebox("disabled", "Button", Box(new Color(0.35f, 0.3f, 0.3f), 12, 16));
            t.SetStylebox("focus", "Button", new StyleBoxEmpty());
            t.SetColor("font_color", "Button", new Color(1f, 0.97f, 0.9f));
            t.SetColor("font_hover_color", "Button", Colors.White);
            t.SetColor("font_pressed_color", "Button", Colors.White);
            t.SetFontSize("font_size", "Button", 26);
            t.SetColor("font_color", "Label", Cream);
            t.SetStylebox("normal", "LineEdit", Box(new Color(0.18f, 0.15f, 0.2f), 8, 10));
            t.SetStylebox("focus", "LineEdit", Box(new Color(0.24f, 0.2f, 0.27f), 8, 10));
            t.SetColor("font_color", "LineEdit", Colors.White);
            t.SetFontSize("font_size", "LineEdit", 24);
            t.SetStylebox("background", "ProgressBar", Box(new Color(0, 0, 0, 0.5f), 4, 0));
            t.SetStylebox("fill", "ProgressBar", Box(Gold, 4, 0));
            t.SetConstant("separation", "VBoxContainer", 10);
            t.SetConstant("separation", "HBoxContainer", 10);
            return t;
        }

        static Label Text(string s, int size = 22, Color? color = null, bool wrap = false)
        {
            var l = new Label { Text = s, MouseFilter = Control.MouseFilterEnum.Ignore };
            l.AddThemeFontSizeOverride("font_size", size);
            if (color.HasValue) l.AddThemeColorOverride("font_color", color.Value);
            l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
            l.AddThemeConstantOverride("shadow_offset_x", 2);
            l.AddThemeConstantOverride("shadow_offset_y", 2);
            if (wrap)
            {
                l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                l.CustomMinimumSize = new Vector2(100, 0);
            }
            return l;
        }

        static Button Btn(string text, System.Action onClick, float width = 420, float height = 60)
        {
            var b = new Button { Text = text, CustomMinimumSize = new Vector2(width, height), FocusMode = Control.FocusModeEnum.None };
            b.Pressed += () =>
            {
                Audio.Sfx.I?.PlayUi(Audio.SoundId.Click, 0.6f);
                onClick();
            };
            return b;
        }

        /// <summary>A centered panel with a vertical box inside.</summary>
        VBoxContainer Panel(Control parent, float width, float top, float height = 0)
        {
            var p = new PanelContainer();
            p.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            p.OffsetLeft = -width / 2;
            p.OffsetRight = width / 2;
            p.OffsetTop = top;
            if (height > 0) p.OffsetBottom = top + height;
            parent.AddChild(p);
            var v = new VBoxContainer();
            p.AddChild(v);
            return v;
        }

        PanelContainer Corner(Control parent, Control.LayoutPreset preset, Vector2 offset, Vector2 size)
        {
            var p = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            p.AddThemeStyleboxOverride("panel", Box(new Color(0, 0, 0, 0.55f), 8, 12));
            p.SetAnchorsPreset(preset);
            bool right = preset == Control.LayoutPreset.TopRight || preset == Control.LayoutPreset.BottomRight;
            bool bottom = preset == Control.LayoutPreset.BottomLeft || preset == Control.LayoutPreset.BottomRight;
            p.OffsetLeft = right ? -offset.X - size.X : offset.X;
            p.OffsetRight = right ? -offset.X : offset.X + size.X;
            p.OffsetTop = bottom ? -offset.Y - size.Y : offset.Y;
            p.OffsetBottom = bottom ? -offset.Y : offset.Y + size.Y;
            p.GrowHorizontal = right ? Control.GrowDirection.Begin : Control.GrowDirection.End;
            p.GrowVertical = bottom ? Control.GrowDirection.Begin : Control.GrowDirection.End;
            parent.AddChild(p);
            return p;
        }

        void Title(Control parent, float top)
        {
            var t = Text(Loc.T("title"), 84, Gold);
            t.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            t.OffsetLeft = -700;
            t.OffsetRight = 700;
            t.OffsetTop = top;
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.AddThemeConstantOverride("shadow_offset_x", 4);
            t.AddThemeConstantOverride("shadow_offset_y", 4);
            parent.AddChild(t);
            var st = Text(Loc.T("subtitle"), 26, new Color(0.95f, 0.9f, 0.85f));
            st.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            st.OffsetLeft = -600;
            st.OffsetRight = 600;
            st.OffsetTop = top + 110;
            st.HorizontalAlignment = HorizontalAlignment.Center;
            parent.AddChild(st);
        }

        static void Clear(Control c)
        {
            foreach (var ch in c.GetChildren()) ch.QueueFree();
        }

        public void SetLoading(bool on)
        {
            if (loadingLabel == null) return;
            loadingLabel.Text = Loc.T("loading");
            loadingLabel.Visible = on;
        }

        // ------------------------------------------------------------------ frame

        public override void _Process(double delta)
        {
            if (App == null) return;
            if (builtScreen != App.Screen) BuildScreen();
            string ok = App.Screen == AppScreen.Playing ? $"{App.Paused}{App.CraftOpen}{App.SockOpen}{App.ChatOpen}" : "";
            if (ok != overlayKey)
            {
                overlayKey = ok;
                BuildOverlays();
            }
            hudRoot.Visible = App.Screen == AppScreen.Playing;
            if (App.Screen == AppScreen.Playing) UpdateHud();
            if (App.Screen == AppScreen.JoinSetup) UpdateLanList();
            if (App.Screen == AppScreen.Connecting && connectingLabel != null) connectingLabel.Text = Loc.T("connecting") + new string('.', (int)(Clock.Now * 2) % 4);
            UpdateToasts();
        }

        void UpdateToasts()
        {
            toastBox.Visible = App.Screen != AppScreen.Report;
            var list = App.Toasts;
            while (toastBox.GetChildCount() < list.Count) toastBox.AddChild(Text("", 26));
            for (int i = 0; i < toastBox.GetChildCount(); i++)
            {
                var l = (Label)toastBox.GetChild(i);
                if (i < list.Count)
                {
                    var t = list[i];
                    l.Visible = true;
                    l.Text = t.text;
                    l.HorizontalAlignment = HorizontalAlignment.Center;
                    float a = Mathf.Clamp(t.until - Clock.Now, 0f, 1f);
                    l.AddThemeColorOverride("font_color", new Color(t.color.R, t.color.G, t.color.B, a));
                    l.CustomMinimumSize = new Vector2(1200, 0);
                }
                else l.Visible = false;
            }
        }

        // ------------------------------------------------------------------ screens

        void BuildScreen()
        {
            builtScreen = App.Screen;
            Clear(screenRoot);
            lanList = null;
            connectingLabel = null;
            switch (App.Screen)
            {
                case AppScreen.Menu: MainMenu(); break;
                case AppScreen.HostSetup: HostSetup(); break;
                case AppScreen.JoinSetup: JoinSetup(); break;
                case AppScreen.Connecting: Connecting(); break;
                case AppScreen.Settings: SettingsScreen(); break;
                case AppScreen.Report: ReportScreen(); break;
            }
        }

        void MainMenu()
        {
            Title(screenRoot, 40);
            var st = App.Settings;
            var v = Panel(screenRoot, 560, 250);
            v.AddChild(Text(Loc.T("name"), 20));
            var name = new LineEdit { Text = st.Name ?? "", MaxLength = 16, CustomMinimumSize = new Vector2(0, 44) };
            name.TextChanged += s => st.Name = s;
            v.AddChild(name);
            v.AddChild(Text(Loc.T("hatColor"), 20));
            var hats = new HBoxContainer();
            v.AddChild(hats);
            var hatButtons = new List<Button>();
            for (byte i = 0; i < GameSession.HatCount; i++)
            {
                byte idx = i;
                var b = new Button { CustomMinimumSize = new Vector2(52, 40), FocusMode = Control.FocusModeEnum.None };
                var col = GameSession.HatColor(i);
                b.AddThemeStyleboxOverride("normal", Box(col, 8, 4));
                b.AddThemeStyleboxOverride("hover", Box(col.Lightened(0.2f), 8, 4));
                b.AddThemeStyleboxOverride("pressed", Box(col.Darkened(0.2f), 8, 4));
                b.Text = st.Hat == i ? "^" : "";
                b.Pressed += () =>
                {
                    st.Hat = idx;
                    for (int k = 0; k < hatButtons.Count; k++) hatButtons[k].Text = k == idx ? "^" : "";
                };
                hatButtons.Add(b);
                hats.AddChild(b);
            }
            v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
            v.AddChild(Btn(Loc.T("solo"), App.StartSolo, 510));
            v.AddChild(Btn(Loc.T("host"), () => App.Screen = AppScreen.HostSetup, 510));
            v.AddChild(Btn(Loc.T("join"), () =>
            {
                App.Screen = AppScreen.JoinSetup;
                App.StartDiscovery();
            }, 510));
            v.AddChild(Btn(Loc.T("settings"), () =>
            {
                App.SettingsReturn = AppScreen.Menu;
                App.Screen = AppScreen.Settings;
            }, 510));
            v.AddChild(Btn(Loc.T("quit"), App.Quit, 510));
            if (!string.IsNullOrEmpty(App.MenuMessage)) v.AddChild(Text(App.MenuMessage, 20, new Color(1f, 0.6f, 0.5f), true));
            var help = Text(Loc.T("controls"), 18, null, true);
            help.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            help.OffsetLeft = 20;
            help.OffsetRight = -20;
            help.OffsetTop = -110;
            help.OffsetBottom = -10;
            screenRoot.AddChild(help);
            var ver = Text("v" + GameConsts.GameVersion + " · Godot", 18);
            ver.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            ver.OffsetLeft = -240;
            ver.OffsetTop = 10;
            screenRoot.AddChild(ver);
        }

        string[] LocalIps()
        {
            if (localIps != null) return localIps;
            var list = new List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var a in ni.GetIPProperties().UnicastAddresses)
                        if (a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address)) list.Add(a.Address + "  (" + ni.Name + ")");
                }
            }
            catch { /* not available on some platforms */ }
            if (list.Count == 0) list.Add("127.0.0.1");
            localIps = list.ToArray();
            return localIps;
        }

        void HostSetup()
        {
            Title(screenRoot, 20);
            var st = App.Settings;
            var v = Panel(screenRoot, 760, 230);
            v.AddChild(Text(Loc.T("host"), 34, Gold));
            v.AddChild(Text(Loc.T("hostInfo") + ":", 20, null, true));
            var row = new HBoxContainer();
            row.AddChild(Text(Loc.T("port"), 22));
            var port = new LineEdit { Text = st.Port.ToString(), MaxLength = 6, CustomMinimumSize = new Vector2(160, 44) };
            row.AddChild(port);
            v.AddChild(row);
            v.AddChild(Text("IP:", 20));
            foreach (var ip in LocalIps()) v.AddChild(Text("  " + ip, 22));
            v.AddChild(Text(Loc.T("hostHelp"), 18, null, true));
            var btns = new HBoxContainer();
            btns.AddChild(Btn(Loc.T("host"), () =>
            {
                if (int.TryParse(port.Text, out var p) && p > 0 && p < 65536) st.Port = p;
                App.StartHost();
            }, 320));
            btns.AddChild(Btn(Loc.T("back"), () => App.Screen = AppScreen.Menu, 240));
            v.AddChild(btns);
        }

        void JoinSetup()
        {
            Title(screenRoot, 20);
            var st = App.Settings;
            var v = Panel(screenRoot, 760, 230);
            v.AddChild(Text(Loc.T("join"), 34, Gold));
            var r1 = new HBoxContainer();
            r1.AddChild(new Label { Text = Loc.T("address"), CustomMinimumSize = new Vector2(220, 0) });
            var ip = new LineEdit { Text = st.LastIp, MaxLength = 64, CustomMinimumSize = new Vector2(320, 44) };
            r1.AddChild(ip);
            v.AddChild(r1);
            var r2 = new HBoxContainer();
            r2.AddChild(new Label { Text = Loc.T("port"), CustomMinimumSize = new Vector2(220, 0) });
            var port = new LineEdit { Text = st.Port.ToString(), MaxLength = 6, CustomMinimumSize = new Vector2(160, 44) };
            r2.AddChild(port);
            v.AddChild(r2);
            v.AddChild(Btn(Loc.T("connect"), () =>
            {
                int p = int.TryParse(port.Text, out var pp) ? pp : st.Port;
                App.StartJoin(ip.Text.Trim(), p);
            }, 320));
            v.AddChild(Text(Loc.T("lanGames"), 24, Gold));
            lanList = new VBoxContainer();
            v.AddChild(lanList);
            lanSig = "?";
            v.AddChild(Btn(Loc.T("back"), () =>
            {
                App.StopDiscovery();
                App.Screen = AppScreen.Menu;
            }, 240));
        }

        float nextPing;

        void UpdateLanList()
        {
            var d = App.Discovery;
            if (d == null || lanList == null) return;
            if (Clock.Now > nextPing)
            {
                nextPing = Clock.Now + 1f;
                d.Ping();
            }
            var hosts = d.Hosts.ToList();
            string sig = string.Join("|", hosts.Select(h => h.Info + h.Address + h.Port));
            if (sig == lanSig) return;
            lanSig = sig;
            Clear(lanList);
            if (hosts.Count == 0) lanList.AddChild(Text(Loc.T("noLanGames"), 20));
            foreach (var h in hosts)
            {
                var host = h;
                lanList.AddChild(Btn(host.Info + "   " + host.Address + ":" + host.Port, () => App.StartJoin(host.Address, host.Port), 700, 48));
            }
        }

        void Connecting()
        {
            Title(screenRoot, 60);
            var v = Panel(screenRoot, 500, 380);
            connectingLabel = Text(Loc.T("connecting"), 34, Gold);
            v.AddChild(connectingLabel);
            v.AddChild(Btn(Loc.T("back"), App.LeaveToMenu, 240));
        }

        void SettingsScreen()
        {
            var st = App.Settings;
            var v = Panel(screenRoot, 760, 110);
            v.AddChild(Text(Loc.T("settings"), 34, Gold));
            var lang = new HBoxContainer();
            lang.AddChild(new Label { Text = Loc.T("language"), CustomMinimumSize = new Vector2(300, 0) });
            lang.AddChild(Btn(st.Language == Lang.Ru ? "Русский" : "English", () =>
            {
                st.Language = st.Language == Lang.Ru ? Lang.En : Lang.Ru;
                Loc.Current = st.Language;
                builtScreen = (AppScreen)(-1);
            }, 220, 44));
            v.AddChild(lang);
            Slider(v, Loc.T("sensitivity"), st.Sensitivity, 0.3f, 6f, x => st.Sensitivity = x);
            Slider(v, Loc.T("volume"), st.Volume, 0f, 1f, x => st.Volume = x);
            Slider(v, Loc.T("fov"), st.Fov, 55f, 100f, x => st.Fov = x);
            var inv = new CheckBox { Text = Loc.T("invertY"), ButtonPressed = st.InvertY, FocusMode = Control.FocusModeEnum.None };
            inv.Toggled += on => st.InvertY = on;
            v.AddChild(inv);
            var fs = new CheckBox { Text = Loc.T("fullscreen"), ButtonPressed = st.Fullscreen, FocusMode = Control.FocusModeEnum.None };
            fs.Toggled += on =>
            {
                st.Fullscreen = on;
                GameApp.ApplyVideo(st);
            };
            v.AddChild(fs);
            var q = new HBoxContainer();
            q.AddChild(new Label { Text = Loc.T("quality"), CustomMinimumSize = new Vector2(300, 0) });
            string[] names = { Loc.T("qLow"), Loc.T("qMedium"), Loc.T("qHigh") };
            for (int i = 0; i < 3; i++)
            {
                int qi = i;
                var b = Btn(names[i], () =>
                {
                    st.Quality = qi;
                    GameApp.ApplyVideo(st);
                    builtScreen = (AppScreen)(-1);
                }, 130, 40);
                b.Modulate = st.Quality == i ? Colors.White : new Color(1, 1, 1, 0.5f);
                q.AddChild(b);
            }
            v.AddChild(q);
            if (App.SettingsReturn == AppScreen.Menu)
                v.AddChild(Btn(Loc.T("resetSave"), () =>
                {
                    SaveStore.Reset();
                    App.Toast(Loc.T("resetSave") + " — OK");
                }, 360, 44));
            v.AddChild(Btn(Loc.T("back"), () =>
            {
                st.Save();
                App.Screen = App.SettingsReturn;
            }, 240));
        }

        static void Slider(VBoxContainer v, string name, float value, float min, float max, System.Action<float> set)
        {
            var row = new HBoxContainer();
            var label = new Label { Text = $"{name}: {value:0.0}", CustomMinimumSize = new Vector2(300, 0) };
            var s = new HSlider { MinValue = min, MaxValue = max, Step = 0.05, Value = value, CustomMinimumSize = new Vector2(360, 30), FocusMode = Control.FocusModeEnum.None };
            s.ValueChanged += x =>
            {
                set((float)x);
                label.Text = $"{name}: {x:0.0}";
            };
            row.AddChild(label);
            row.AddChild(s);
            v.AddChild(row);
        }

        void ReportScreen()
        {
            var r = App.Report;
            if (r == null) return;
            var dim = new ColorRect { Color = new Color(0.02f, 0.02f, 0.05f, 0.85f) };
            dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            screenRoot.AddChild(dim);
            var v = Panel(screenRoot, 1000, 50);
            v.AddChild(Text(Loc.T("report"), 34, Gold));
            int line = r.Chaos >= 18 ? 3 : r.Chaos >= 10 ? 2 : r.Chaos >= 5 ? 1 : 0;
            v.AddChild(Text(Loc.T("grandpaMorning" + line), 22, null, true));
            for (int i = 0; i < r.TaskIds.Length; i++)
            {
                var t = TaskCatalog.Get(r.TaskIds[i]);
                if (t == null) continue;
                bool done = i < r.TaskDone.Length && r.TaskDone[i];
                v.AddChild(Text((done ? "[+] " : "[-] ") + t.Text(Loc.Current), 20, done ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.6f, 0.55f)));
            }
            v.AddChild(Text($"{Loc.T("chaos")}: {Loc.ChaosCount(r.Chaos)}", 22));
            var mats = new List<string>();
            for (int i = 0; i < 5; i++) if (r.Haul[i] > 0) mats.Add($"{Loc.MatName((Mat)i)} +{r.Haul[i]}");
            v.AddChild(Text($"{Loc.T("materials")}: {(mats.Count > 0 ? string.Join(", ", mats) : "—")}", 20, null, true));
            v.AddChild(Text($"{Loc.MatName(Mat.Giggles)}: +{r.Giggles}", 20));
            v.AddChild(Text(r.Fired ? Loc.T("fired") : r.Passed ? Loc.T("verdictGood") : Loc.T("verdictBad"), 32, r.Passed ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.5f, 0.45f), true));
            if (S != null && S.IsHost) v.AddChild(Btn(Loc.T("continue"), App.ContinueFromReport, 320));
            else v.AddChild(Text(Loc.T("waitHost"), 20));
        }

        // ------------------------------------------------------------------ overlays

        void BuildOverlays()
        {
            Clear(overlayRoot);
            if (App.Screen != AppScreen.Playing) return;
            if (App.Paused) Pause();
            if (App.CraftOpen) Craft();
            if (App.SockOpen) SockDialog();
            if (App.ChatOpen) Chat();
        }

        void Pause()
        {
            var dim = new ColorRect { Color = new Color(0, 0, 0, 0.5f) };
            dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            overlayRoot.AddChild(dim);
            var v = Panel(overlayRoot, 560, 180);
            v.AddChild(Btn(Loc.T("resume"), () => App.Paused = false, 510));
            v.AddChild(Btn(Loc.T("settings"), () =>
            {
                App.SettingsReturn = AppScreen.Playing;
                App.Paused = false;
                App.Screen = AppScreen.Settings;
            }, 510));
            v.AddChild(Btn(Loc.T("toMenu"), App.LeaveToMenu, 510));
            v.AddChild(Text(Loc.T("controls"), 18, null, true));
        }

        void Craft()
        {
            var s = S;
            if (s == null) return;
            var dim = new ColorRect { Color = new Color(0, 0, 0, 0.4f) };
            dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            overlayRoot.AddChild(dim);
            var v = Panel(overlayRoot, 980, 60, 780);
            v.AddChild(Text(Loc.T("craftBench"), 34, Gold));
            var mats = new Label();
            v.AddChild(mats);
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(940, 480), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            v.AddChild(scroll);
            var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            scroll.AddChild(list);
            void Refresh()
            {
                var save = s.Save;
                var parts = new List<string>();
                for (int i = 0; i < 6; i++) parts.Add($"{Loc.MatName((Mat)i)}: {save.Materials[i]}");
                string potions = save.Potions > 0 ? "    " + GearCatalog.Get(GearId.SleepDust).Name(Loc.Current) + ": " + save.Potions : "";
                mats.Text = string.Join("   ", parts) + potions;
                Clear(list);
                foreach (var gd in GearCatalog.All)
                {
                    var gear = gd;
                    var row = new PanelContainer();
                    row.AddThemeStyleboxOverride("panel", Box(new Color(0, 0, 0, 0.45f), 8, 10));
                    var h = new HBoxContainer();
                    row.AddChild(h);
                    var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    info.AddChild(Text(gear.Name(Loc.Current) + " — " + gear.Desc(Loc.Current), 20, null, true));
                    var cost = new List<string>();
                    for (int i = 0; i < 6; i++) if (gear.Cost[i] > 0) cost.Add($"{Loc.MatName((Mat)i)} {gear.Cost[i]}");
                    info.AddChild(Text(string.Join(", ", cost), 18, new Color(0.85f, 0.8f, 0.7f)));
                    h.AddChild(info);
                    bool can = save.CanCraft(gear, out var reason);
                    if (reason == "owned") h.AddChild(Text(Loc.T("owned"), 22, new Color(0.6f, 1f, 0.6f)));
                    else
                    {
                        var b = Btn(Loc.T("craft"), () =>
                        {
                            s.SendAction(new ActionMsg { Type = ActionType.Craft, I = (int)gear.Id });
                            GameApp.I.Delay(0.3f, () => { if (App.CraftOpen) overlayKey = ""; });
                        }, 200, 48);
                        b.Disabled = !can;
                        h.AddChild(b);
                    }
                    list.AddChild(row);
                }
            }
            Refresh();
            if (!s.IsHost) v.AddChild(Text(Loc.T("craftHostOnly"), 18));
            v.AddChild(Btn(Loc.T("back"), () => App.CraftOpen = false, 240));
        }

        void SockDialog()
        {
            var s = S;
            var p = new PanelContainer();
            p.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            p.OffsetLeft = -480;
            p.OffsetRight = 480;
            p.OffsetTop = -400;
            p.OffsetBottom = -40;
            overlayRoot.AddChild(p);
            var v = new VBoxContainer();
            p.AddChild(v);
            v.AddChild(Text(Loc.T("greatSock"), 34, Gold));
            v.AddChild(Text(Loc.T("sockSays"), 22, null, true));
            if (s?.Save != null)
            {
                v.AddChild(Text($"{Loc.T("day")} {s.Save.Night}.  {Loc.T("strikes")}: {s.Save.Strikes}/{GameConsts.MaxStrikes}", 20));
                v.AddChild(Text(Loc.T("sockTip" + (s.Save.Night % 6)), 20, new Color(0.9f, 0.95f, 1f), true));
            }
            v.AddChild(Btn(Loc.T("ok"), () => App.SockOpen = false, 200));
        }

        void Chat()
        {
            var edit = new LineEdit { MaxLength = 120, PlaceholderText = Loc.T("chat") };
            edit.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
            edit.OffsetLeft = 20;
            edit.OffsetRight = 640;
            edit.OffsetTop = -250;
            edit.OffsetBottom = -206;
            overlayRoot.AddChild(edit);
            edit.TextSubmitted += text =>
            {
                if (!string.IsNullOrWhiteSpace(text)) S?.SendAction(new ActionMsg { Type = ActionType.Chat, S = text });
                App.ChatOpen = false;
            };
            edit.CallDeferred(Control.MethodName.GrabFocus);
        }

        // ------------------------------------------------------------------ HUD

        public void OnLevelLoaded(LevelKind kind)
        {
            hudKind = kind;
            Clear(hudRoot);
            clockLabel = tasksLabel = chaosLabel = pocketLabel = grandpaLabel = stepsLabel = lightLabel = playersLabel = promptLabel = yarnLabel = villageLabel = waitLabel = null;
            crosshair = new ColorRect { Color = Colors.White, CustomMinimumSize = new Vector2(6, 6), MouseFilter = Control.MouseFilterEnum.Ignore };
            crosshair.SetAnchorsPreset(Control.LayoutPreset.Center);
            crosshair.OffsetLeft = -3;
            crosshair.OffsetTop = -3;
            crosshair.OffsetRight = 3;
            crosshair.OffsetBottom = 3;
            hudRoot.AddChild(crosshair);
            promptLabel = Text("", 26);
            promptLabel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            promptLabel.OffsetLeft = -600;
            promptLabel.OffsetRight = 600;
            promptLabel.OffsetTop = -300;
            promptLabel.HorizontalAlignment = HorizontalAlignment.Center;
            hudRoot.AddChild(promptLabel);
            holdBar = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(240, 10) };
            holdBar.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            holdBar.OffsetLeft = -120;
            holdBar.OffsetRight = 120;
            holdBar.OffsetTop = -255;
            holdBar.OffsetBottom = -245;
            hudRoot.AddChild(holdBar);
            yarnLabel = Text("", 18);
            yarnLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
            yarnLabel.OffsetLeft = 30;
            yarnLabel.OffsetTop = 10;
            hudRoot.AddChild(yarnLabel);
            gripBar = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(120, 8) };
            gripBar.AddThemeStyleboxOverride("fill", Box(new Color(0.5f, 0.9f, 1f), 4, 0));
            gripBar.SetAnchorsPreset(Control.LayoutPreset.Center);
            gripBar.OffsetLeft = 30;
            gripBar.OffsetRight = 150;
            gripBar.OffsetTop = 42;
            gripBar.OffsetBottom = 50;
            hudRoot.AddChild(gripBar);
            playersLabel = Text("", 18);
            playersLabel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            playersLabel.OffsetLeft = -300;
            playersLabel.OffsetTop = 160;
            hudRoot.AddChild(playersLabel);

            if (kind == LevelKind.House)
            {
                var cp = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                cp.AddThemeStyleboxOverride("panel", Box(new Color(0, 0, 0, 0.55f), 8, 12));
                cp.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
                cp.OffsetLeft = -150;
                cp.OffsetRight = 150;
                cp.OffsetTop = 12;
                hudRoot.AddChild(cp);
                var cv = new VBoxContainer();
                cp.AddChild(cv);
                clockLabel = Text("00:00", 34);
                clockLabel.HorizontalAlignment = HorizontalAlignment.Center;
                cv.AddChild(clockLabel);
                clockBar = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(260, 8) };
                clockBar.AddThemeStyleboxOverride("fill", Box(new Color(1f, 0.75f, 0.35f), 4, 0));
                cv.AddChild(clockBar);

                tasksPanel = Corner(hudRoot, Control.LayoutPreset.TopLeft, new Vector2(14, 14), new Vector2(580, 60));
                tasksLabel = Text("", 18);
                tasksPanel.AddChild(tasksLabel);

                var chaosPanel = Corner(hudRoot, Control.LayoutPreset.TopRight, new Vector2(14, 14), new Vector2(300, 110));
                chaosLabel = Text("", 20);
                chaosPanel.AddChild(chaosLabel);

                var pocketPanel = Corner(hudRoot, Control.LayoutPreset.BottomLeft, new Vector2(14, 14), new Vector2(640, 50));
                pocketLabel = Text("", 18);
                pocketPanel.AddChild(pocketLabel);

                grandpaPanel = Corner(hudRoot, Control.LayoutPreset.BottomRight, new Vector2(14, 14), new Vector2(430, 50));
                grandpaLabel = Text("", 22);
                grandpaPanel.AddChild(grandpaLabel);

                stealthPanel = Corner(hudRoot, Control.LayoutPreset.BottomRight, new Vector2(14, 74), new Vector2(430, 40));
                var sh = new HBoxContainer();
                stealthPanel.AddChild(sh);
                stepsLabel = Text("", 18);
                stepsLabel.CustomMinimumSize = new Vector2(230, 0);
                sh.AddChild(stepsLabel);
                lightLabel = Text("", 18);
                sh.AddChild(lightLabel);
            }
            else
            {
                var vp = Corner(hudRoot, Control.LayoutPreset.TopLeft, new Vector2(14, 14), new Vector2(520, 250));
                villageLabel = Text("", 20, null, true);
                villageLabel.CustomMinimumSize = new Vector2(490, 0);
                vp.AddChild(villageLabel);
                waitLabel = Text("", 22);
                waitLabel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
                waitLabel.OffsetLeft = -300;
                waitLabel.OffsetRight = 300;
                waitLabel.OffsetTop = -60;
                waitLabel.HorizontalAlignment = HorizontalAlignment.Center;
                hudRoot.AddChild(waitLabel);
            }
        }

        void UpdateHud()
        {
            var s = S;
            var w = GameWorld.Current;
            var g = LocalGnome.I;
            if (s == null || w == null || hudKind == null || crosshair == null) return;
            crosshair.Visible = g != null && g.Status == PlayerStatus.Free;
            crosshair.Color = g?.Prompt != null ? new Color(1f, 0.9f, 0.3f) : new Color(1, 1, 1, 0.8f);
            promptLabel.Text = g?.Prompt ?? "";
            holdBar.Visible = g != null && g.HoldProgress > 0;
            if (g != null) holdBar.Value = g.HoldProgress;
            bool yarn = g != null && g.OnYarn;
            yarnLabel.Visible = yarn;
            gripBar.Visible = yarn && g.Mode == ArmMode.Climb;
            if (yarn)
            {
                yarnLabel.Text = $"{Loc.T("yarn")}: {g.YarnLength:0.0} / {g.YarnRange:0}";
                gripBar.Value = g.Grip01;
            }
            if (!s.IsSolo)
            {
                var lines = new List<string>();
                foreach (var p in s.Players.Values)
                {
                    string st = p.Status == PlayerStatus.Trapped ? " [" + Loc.T("mechJar") + "]" : p.Status == PlayerStatus.Dead ? " [X]" : p.Status == PlayerStatus.Home ? " [~]" : p.Status == PlayerStatus.Carried ? " [!]" : "";
                    lines.Add("● " + p.Name + st);
                }
                playersLabel.Text = string.Join("\n", lines);
            }
            else playersLabel.Text = "";

            if (hudKind == LevelKind.House && w.Kind == LevelKind.House) HouseHud(s, w, g);
            else if (villageLabel != null) VillageHud(s);
        }

        void HouseHud(GameSession s, GameWorld w, LocalGnome g)
        {
            float timeLeft = s.IsHost ? (s.Host.Night != null ? s.Host.Night.TimeLeft : GameConsts.NightSeconds) : s.TimeLeft;
            float prog = 1f - timeLeft / GameConsts.NightSeconds;
            float hours = prog * 6f;
            clockLabel.Text = $"{(int)hours:00}:{(int)((hours - (int)hours) * 60):00}";
            clockBar.Value = prog;

            if (App.GameplayInput && Keys.Pressed(Key.Tab)) tasksExpanded = !tasksExpanded;
            var tasks = s.IsHost ? s.Host.Night?.Tasks : s.ClientTasks;
            if (tasks != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"{Loc.T("tasks")}  ({tasks.CompletedCount}/{GameConsts.TasksRequired} {Loc.T("tasksNeed")})   [Tab]");
                if (tasksExpanded)
                    for (int i = 0; i < tasks.Tasks.Count; i++)
                    {
                        var t = tasks.Tasks[i];
                        bool done = tasks.IsDone(i);
                        string prog2 = t.Goal > 1 ? $" ({tasks.Progress[i]}/{t.Goal})" : "";
                        sb.Append('\n').Append(done ? "[+] " : "[  ] ").Append(t.Text(Loc.Current)).Append(prog2).Append("  ").Append(new string('*', t.Tier));
                    }
                tasksLabel.Text = sb.ToString();
            }
            int chaos = s.IsHost ? (s.Host.Night?.Chaos ?? 0) : ChaosFromTasks();
            var ct = $"{Loc.T("chaos")}: {chaos}";
            if (s.IsHost) ct += $"\n{Loc.T("haul")}: {s.Host.Night?.HaulValue ?? 0}";
            if (s.IsSolo) ct += $"\n{Loc.T("spareHats")}: {s.SpareHats}";
            chaosLabel.Text = ct;

            var slot = s.LocalSlot;
            if (slot != null)
            {
                string items = slot.Pocket.Count == 0 ? "—" : string.Join(", ", slot.Pocket.Select(k => ItemDefs.TryGet(k, out var d) ? d.Name(Loc.Current) : k));
                pocketLabel.Text = $"{Loc.T("pocket")} ({slot.Pocket.Count}/{GearEffects.From(s.Save).PocketSize}): {items}";
            }

            var om = w.OldMan;
            if (om != null)
            {
                string txt;
                Color c;
                switch (om.Mode)
                {
                    case OldMan.St.Sleep:
                    case OldMan.St.Doze:
                        txt = "Zzz " + Loc.T("oldManAsleep");
                        c = new Color(0.6f, 0.8f, 1f);
                        break;
                    case OldMan.St.Chase:
                    case OldMan.St.Grab:
                    case OldMan.St.Swat:
                    case OldMan.St.Carry:
                        txt = "!!! " + Loc.T("oldManAlert");
                        c = new Color(1f, 0.35f, 0.3f);
                        break;
                    case OldMan.St.Investigate:
                    case OldMan.St.Search:
                    case OldMan.St.WakeUp:
                        txt = "? " + Loc.T("oldManSuspicious");
                        c = new Color(1f, 0.85f, 0.35f);
                        break;
                    default:
                        txt = Loc.T("oldManAwake");
                        c = new Color(1f, 0.95f, 0.8f);
                        break;
                }
                grandpaLabel.Text = txt;
                grandpaLabel.AddThemeColorOverride("font_color", c);
            }
            // stealth: how loud am I, am I lit?
            stealthPanel.Visible = om != null && g != null && g.Status == PlayerStatus.Free;
            if (stealthPanel.Visible)
            {
                float mul = GearEffects.From(s.Save).FootstepNoiseMul;
                bool moving = g.Velocity.Flat().LengthSquared() > 0.5f && g.Grounded;
                string step;
                Color sc;
                if (!moving || g.Crouching) { step = Loc.T(moving ? "stepQuiet" : "stepSilent"); sc = new Color(0.6f, 1f, 0.6f); }
                else if (g.Sprinting && mul > 0.5f) { step = Loc.T("stepLoud"); sc = new Color(1f, 0.4f, 0.35f); }
                else if (mul <= 0.5f) { step = Loc.T("stepQuiet"); sc = new Color(0.6f, 1f, 0.6f); }
                else { step = Loc.T("stepNormal"); sc = new Color(1f, 0.9f, 0.5f); }
                stepsLabel.Text = Loc.T("steps") + ": " + step;
                stepsLabel.AddThemeColorOverride("font_color", sc);
                bool lit = w.IsLit(g.Center) || om.InTorch(g.Center);
                lightLabel.Text = Loc.T(lit ? "inLight" : "inShadow");
                lightLabel.AddThemeColorOverride("font_color", lit ? new Color(1f, 0.85f, 0.4f) : new Color(0.6f, 0.7f, 1f));
            }
        }

        int ChaosFromTasks()
        {
            var t = S?.ClientTasks;
            if (t == null) return 0;
            int best = 0;
            for (int i = 0; i < t.Tasks.Count; i++) if (t.Tasks[i].Id.StartsWith("chaos")) best = Mathf.Max(best, t.Progress[i]);
            return best;
        }

        void VillageHud(GameSession s)
        {
            var save = s.Save;
            if (save == null) return;
            var sb = new System.Text.StringBuilder();
            sb.Append(Loc.T("hub")).Append('\n');
            sb.Append($"{Loc.T("day")}: {save.Night}    {Loc.T("strikes")}: {save.Strikes}/{GameConsts.MaxStrikes}\n");
            for (int i = 0; i < 6; i++) sb.Append($"{Loc.MatName((Mat)i)}: {save.Materials[i]}").Append(i % 2 == 1 ? "\n" : "      ");
            sb.Append(Loc.T("portalHint"));
            villageLabel.Text = sb.ToString();
            waitLabel.Text = s.IsSolo ? "" : $"{Loc.T("waitingPlayers")}: {s.Players.Values.Count(p => p.PortalReady)}/{s.Players.Count}";
        }
    }
}
