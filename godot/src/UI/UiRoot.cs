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
    /// <summary>
    /// All screens and the HUD, built from Godot controls in code, in the game's handmade style: felt patches
    /// with stitched seams, knitted letters, yarn balls and paper notes (see <see cref="Style"/>).
    /// </summary>
    public partial class UiRoot : CanvasLayer
    {
        GameApp App => GameApp.I;
        GameSession S => GameSession.I;
        Control root, screenRoot, hudRoot, overlayRoot;
        VBoxContainer toastBox;
        Control loadingPanel;
        Label loadingTip;
        Flurry flurry;
        ColorRect vignette;
        AppScreen builtScreen = (AppScreen)(-1);
        string overlayKey = "";
        LevelKind? hudKind;
        bool tasksExpanded = true, creditsOpen, greeted;
        string[] localIps;
        int logoPokes;
        float lastLogoPoke;

        // HUD widgets
        Label chaosLabel, haulLabel, pocketLabel, grandpaLabel, stepsLabel, lightLabel, promptLabel, yarnLabel, villageLabel, waitLabel, tasksHead;
        IconView grandpaIcon, stepsIcon, lightIcon;
        HBoxContainer spareHats;
        VBoxContainer tasksList, playersBox;
        string tasksSig = "", playersSig = "", spareSig = "";
        MoonClock moon;
        ProgressBar holdBar, gripBar;
        Reticle crosshair;
        PanelContainer grandpaPanel, stealthPanel, tasksPanel, promptPanel;
        // join screen
        VBoxContainer lanList;
        string lanSig = "";
        Label connectingLabel;
        GnomePreview preview;
        readonly List<YarnSwatch> swatches = new List<YarnSwatch>();

        public override void _Ready()
        {
            Layer = 10;
            root = new Control { Name = "Root", Theme = Style.BuildTheme(), MouseFilter = Control.MouseFilterEnum.Ignore };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(root);
            vignette = new ColorRect { Name = "Vignette", MouseFilter = Control.MouseFilterEnum.Ignore, Material = VignetteMaterial() };
            vignette.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            root.AddChild(vignette);
            hudRoot = Layer0("Hud");
            screenRoot = Layer0("Screen");
            overlayRoot = Layer0("Overlay");
            toastBox = new VBoxContainer { Name = "Toasts", MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Begin };
            toastBox.AddThemeConstantOverride("separation", 6);
            toastBox.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            toastBox.OffsetLeft = -620;
            toastBox.OffsetRight = 620;
            toastBox.OffsetTop = 140;
            toastBox.OffsetBottom = 460;
            root.AddChild(toastBox);
            BuildLoading();
            flurry = new Flurry { Name = "Flurry" };
            root.AddChild(flurry);
            if (EasterEggs.Winter)
            {
                flurry.Snow = true;
                flurry.Rate = 14f;
            }
            App.ScreenChanged += () => builtScreen = (AppScreen)(-1);
        }

        Control Layer0(string name)
        {
            var c = new Control { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };
            c.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            root.AddChild(c);
            return c;
        }

        static ShaderMaterial VignetteMaterial()
        {
            var sh = new Shader
            {
                Code = @"
shader_type canvas_item;
void fragment() {
    vec2 d = UV - vec2(0.5);
    float v = smoothstep(0.35, 0.95, length(d * vec2(1.25, 1.0)));
    COLOR = vec4(0.05, 0.02, 0.06, v * 0.75);
}",
            };
            return new ShaderMaterial { Shader = sh };
        }

        // ------------------------------------------------------------------ building blocks

        static Label Text(string s, int size = 22, Color? color = null, bool wrap = false, Font font = null) => Style.Text(s, size, color, font, wrap);

        static Label Heading(string s, int size = 44, Color? color = null)
        {
            var l = Style.Text(s, size, color ?? Style.Gold, Style.Script);
            l.AddThemeConstantOverride("shadow_offset_x", 3);
            l.AddThemeConstantOverride("shadow_offset_y", 3);
            return l;
        }

        /// <summary>A felt-patch button with a hand-drawn icon, a colour of its own and a wobble on hover.</summary>
        static Button Btn(string text, System.Action onClick, float width = 420, float height = 64, Color? color = null, IconKind? icon = null)
        {
            var b = new Button { Text = text, CustomMinimumSize = new Vector2(width, height), FocusMode = Control.FocusModeEnum.None, ClipText = false };
            if (color.HasValue) Style.Colorize(b, color.Value);
            if (icon.HasValue)
            {
                var iv = new IconView(icon.Value, height * 0.62f);
                iv.Position = new Vector2(18, height * 0.19f);
                b.AddChild(iv);
                b.AddThemeConstantOverride("h_separation", 0);
                b.Alignment = HorizontalAlignment.Center;
            }
            b.Pressed += () =>
            {
                Audio.Sfx.I?.PlayUi(Audio.SoundId.Click, 0.6f);
                onClick();
            };
            return Wobble.Add(b);
        }

        /// <summary>A felt panel anchored at the top centre; returns its vertical box.</summary>
        VBoxContainer Panel(Control parent, float width, float top, float height = 0, Color? felt = null)
        {
            var p = new PanelContainer();
            if (felt.HasValue) p.AddThemeStyleboxOverride("panel", Style.FeltBox(felt.Value));
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

        /// <summary>A HUD patch in a screen corner.</summary>
        PanelContainer Corner(Control parent, Control.LayoutPreset preset, Vector2 offset, Vector2 size, bool paper = false)
        {
            var p = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            p.AddThemeStyleboxOverride("panel", paper ? Style.PaperBox(24, 16) : Style.FeltBox(new Color(Style.FeltDark, 0.82f), new Color(0.85f, 0.75f, 0.55f), 20, 12, 7));
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

        static HBoxContainer Row(params Control[] items)
        {
            var h = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            foreach (var c in items) h.AddChild(c);
            return h;
        }

        /// <summary>The knitted logo; poke it and it complains (then a stitch runs away).</summary>
        Control Logo(Control parent, Vector2 pos, int size = 76, bool center = false)
        {
            bool gone = logoPokes >= 12;
            string title = Loc.T("title");
            if (gone) title = title.Substring(0, title.Length - 1);
            var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Stop };
            box.AddThemeConstantOverride("separation", -8);
            // two knitted lines: "THE SOCK" over "GANG"
            int cut = title.LastIndexOf(' ');
            var words = cut > 0 ? new[] { title.Substring(0, cut), title.Substring(cut + 1) } : new[] { title };
            foreach (var line in words)
            {
                // words side by side with a narrow gap: the logo font's own space is a full letter wide
                // and each knitted word already leaves room for its shadow on the right
                var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                row.AddThemeConstantOverride("separation", -size / 8);
                foreach (var wd in line.Split(' '))
                    row.AddChild(Style.KnitText(wd, size, new Color(0.84f, 0.2f, 0.17f), new Color(0.98f, 0.93f, 0.83f), new Color(0.2f, 0.44f, 0.84f), new Color(0.95f, 0.72f, 0.18f)));
                if (center) row.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
                box.AddChild(row);
            }
            box.Position = pos;
            if (center)
            {
                box.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
                box.GrowHorizontal = Control.GrowDirection.Both;
                box.Alignment = BoxContainer.AlignmentMode.Center;
                box.OffsetTop = pos.Y;
            }
            box.GuiInput += e =>
            {
                if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    logoPokes = Clock.Now - lastLogoPoke < 2f ? logoPokes + 1 : Mathf.Max(1, logoPokes >= 12 ? 12 : 1);
                    lastLogoPoke = Clock.Now;
                    box.PivotOffset = box.Size / 2;
                    var tw = box.CreateTween().SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
                    box.Rotation = Mathf.DegToRad(GD.Randf() * 8f - 4f);
                    tw.TweenProperty(box, "rotation", 0f, 0.8f);
                    Audio.Sfx.I?.PlayUi(Audio.SoundId.Squeak, 0.5f);
                    var msg = EasterEggs.PokeLogo(logoPokes);
                    if (msg != null)
                    {
                        App.Toast(msg, Style.Gold);
                        if (logoPokes == 12) builtScreen = (AppScreen)(-1);
                    }
                }
            };
            parent.AddChild(box);
            return box;
        }

        static void Clear(Control c)
        {
            foreach (var ch in c.GetChildren()) ch.QueueFree();
        }

        // ------------------------------------------------------------------ loading

        void BuildLoading()
        {
            loadingPanel = new Control { Name = "Loading", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            loadingPanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            root.AddChild(loadingPanel);
            var dim = new ColorRect { Color = new Color(0.06f, 0.03f, 0.07f, 0.7f), MouseFilter = Control.MouseFilterEnum.Ignore };
            dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            loadingPanel.AddChild(dim);
            var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            v.SetAnchorsPreset(Control.LayoutPreset.Center);
            v.OffsetLeft = -420;
            v.OffsetRight = 420;
            v.OffsetTop = -200;
            v.OffsetBottom = 200;
            loadingPanel.AddChild(v);
            var sp = new YarnSpinner { CustomMinimumSize = new Vector2(140, 140), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
            v.AddChild(sp);
            var l = Heading(Loc.T("loading"), 46);
            l.HorizontalAlignment = HorizontalAlignment.Center;
            v.AddChild(l);
            var note = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            note.AddThemeStyleboxOverride("panel", Style.PaperBox());
            v.AddChild(note);
            loadingTip = Text("", 22, Style.Ink, true);
            loadingTip.HorizontalAlignment = HorizontalAlignment.Center;
            loadingTip.AddThemeConstantOverride("shadow_offset_x", 0);
            loadingTip.AddThemeConstantOverride("shadow_offset_y", 0);
            loadingTip.CustomMinimumSize = new Vector2(760, 0);
            note.AddChild(loadingTip);
        }

        public void SetLoading(bool on)
        {
            if (loadingPanel == null) return;
            if (on && !loadingPanel.Visible) loadingTip.Text = Loc.T("tipTitle") + ": " + EasterEggs.Tip();
            loadingPanel.Visible = on;
        }

        // ------------------------------------------------------------------ frame

        public override void _Input(InputEvent e)
        {
            if (App == null || App.Screen != AppScreen.Menu) return;
            if (e is InputEventKey k && k.Pressed && !k.Echo && EasterEggs.KonamiKey(k.PhysicalKeycode))
            {
                App.Settings.RainbowUnlocked = true;
                App.Settings.Hat = (byte)(App.Settings.Hat | GameSession.RainbowFlag);
                App.Settings.Save();
                App.Toast(Loc.T("eggKonami"), Style.Gold);
                flurry.Burst(160);
                Audio.Sfx.I?.PlayUi(Audio.SoundId.Sparkle, 1f);
                builtScreen = (AppScreen)(-1);
            }
        }

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
            vignette.Visible = App.Screen != AppScreen.Playing;
            if (App.Screen == AppScreen.Playing) UpdateHud();
            if (App.Screen == AppScreen.JoinSetup) UpdateLanList();
            if (App.Screen == AppScreen.Connecting && connectingLabel != null) connectingLabel.Text = Loc.T("connecting") + new string('.', (int)(Clock.Now * 2) % 4);
            UpdateToasts();
        }

        void UpdateToasts()
        {
            toastBox.Visible = App.Screen != AppScreen.Report && !App.Paused && !App.CraftOpen && !App.SockOpen && !creditsOpen;
            var list = App.Toasts;
            while (toastBox.GetChildCount() < list.Count)
            {
                var pill = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
                pill.AddThemeStyleboxOverride("panel", Style.FeltBox(new Color(Style.FeltDark, 0.88f), new Color(0.85f, 0.75f, 0.55f), 24, 8, 11));
                var l = Text("", 24, null, false, Style.Bold);
                l.HorizontalAlignment = HorizontalAlignment.Center;
                pill.AddChild(l);
                toastBox.AddChild(pill);
            }
            for (int i = 0; i < toastBox.GetChildCount(); i++)
            {
                var pill = (PanelContainer)toastBox.GetChild(i);
                var l = (Label)pill.GetChild(0);
                if (i < list.Count)
                {
                    var t = list[i];
                    pill.Visible = true;
                    l.Text = t.text;
                    float a = Mathf.Clamp(t.until - Clock.Now, 0f, 1f);
                    pill.Modulate = new Color(1, 1, 1, a);
                    l.AddThemeColorOverride("font_color", t.color);
                    l.AutowrapMode = t.text.Length > 70 ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
                    l.CustomMinimumSize = new Vector2(t.text.Length > 70 ? 1000 : 0, 0);
                }
                else pill.Visible = false;
            }
        }

        // ------------------------------------------------------------------ screens

        void BuildScreen()
        {
            builtScreen = App.Screen;
            Clear(screenRoot);
            lanList = null;
            connectingLabel = null;
            preview = null;
            swatches.Clear();
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
            var st = App.Settings;
            if (!greeted)
            {
                greeted = true;
                var hello = EasterEggs.CalendarGreeting();
                if (hello != null) App.Toast(hello, Style.Gold);
            }
            // --- left: the knitted logo and the felt buttons ---
            Logo(screenRoot, new Vector2(70, 34));
            var sub = Text(Loc.T("subtitle"), 24, new Color(0.98f, 0.9f, 0.78f), false, Style.Script);
            sub.Position = new Vector2(78, 222);
            screenRoot.AddChild(sub);
            var menu = new VBoxContainer();
            menu.AddThemeConstantOverride("separation", 14);
            menu.Position = new Vector2(80, 290);
            screenRoot.AddChild(menu);
            menu.AddChild(Btn(Loc.T("solo"), App.StartSolo, 470, 70, Style.Red, IconKind.Sock));
            menu.AddChild(Btn(Loc.T("host"), () => App.Screen = AppScreen.HostSetup, 470, 70, Style.Teal, IconKind.TwoHats));
            menu.AddChild(Btn(Loc.T("join"), () =>
            {
                App.Screen = AppScreen.JoinSetup;
                App.StartDiscovery();
            }, 470, 70, Style.Plum, IconKind.Spyglass));
            menu.AddChild(Btn(Loc.T("settings"), () =>
            {
                App.SettingsReturn = AppScreen.Menu;
                App.Screen = AppScreen.Settings;
            }, 470, 70, Style.Mustard, IconKind.Gear));
            menu.AddChild(Btn(Loc.T("quit"), App.Quit, 470, 70, new Color(0.42f, 0.33f, 0.3f), IconKind.Door));
            if (!string.IsNullOrEmpty(App.MenuMessage))
            {
                var msg = Text(App.MenuMessage, 20, Style.Bad, true);
                msg.CustomMinimumSize = new Vector2(470, 0);
                menu.AddChild(msg);
            }

            // --- right: your gnome on a thread spool, name and hat ---
            var card = new PanelContainer();
            card.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            card.OffsetLeft = -560;
            card.OffsetRight = -70;
            card.OffsetTop = 60;
            card.GrowHorizontal = Control.GrowDirection.Begin;
            screenRoot.AddChild(card);
            var v = new VBoxContainer();
            card.AddChild(v);
            var head = Heading(Loc.T("yourGnome"), 40);
            head.HorizontalAlignment = HorizontalAlignment.Center;
            v.AddChild(head);
            preview = new GnomePreview { CustomMinimumSize = new Vector2(420, 380), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
            preview.Poked += n =>
            {
                var msg = EasterEggs.PokeGnome(n);
                if (msg != null) App.Toast(msg, Style.Cream);
            };
            v.AddChild(preview);
            preview.SetHat(st.Hat);
            var hint = Text(Loc.T("pokeHint"), 16, new Color(1, 1, 1, 0.5f));
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            v.AddChild(hint);
            var name = new LineEdit { Text = st.Name ?? "", MaxLength = 20, CustomMinimumSize = new Vector2(360, 52), PlaceholderText = Loc.T("name"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            name.TextChanged += s => st.Name = s;
            name.TextSubmitted += s => NameEgg(s);
            name.FocusExited += () => NameEgg(name.Text);
            var dice = Btn("", () =>
            {
                name.Text = EasterEggs.RandomName(out bool legend);
                st.Name = name.Text;
                if (legend)
                {
                    App.Toast(Loc.T("legendName"), Style.Gold);
                    flurry.Burst(60);
                }
            }, 60, 52, Style.Mustard, IconKind.Dice);
            dice.TooltipText = Loc.T("randomName");
            v.AddChild(Row(name, dice));
            var hats = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            hats.AddThemeConstantOverride("separation", 2);
            v.AddChild(hats);
            int count = GameSession.HatCount + (st.RainbowUnlocked ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                bool rainbow = i == GameSession.HatCount;
                byte idx = rainbow ? (byte)((st.Hat & 0x7f) | GameSession.RainbowFlag) : (byte)i;
                var sw = new YarnSwatch { Yarn = GameSession.HatColor((byte)i), Rainbow = rainbow, CustomMinimumSize = new Vector2(50, 56) };
                sw.Selected = rainbow ? GameSession.IsRainbow(st.Hat) : !GameSession.IsRainbow(st.Hat) && (st.Hat & 0x7f) == i;
                sw.Pressed += () =>
                {
                    st.Hat = idx;
                    foreach (var o in swatches)
                    {
                        o.Selected = o == sw;
                        o.QueueRedraw();
                    }
                    preview?.SetHat(st.Hat);
                    Audio.Sfx.I?.PlayUi(Audio.SoundId.Click, 0.4f);
                };
                swatches.Add(sw);
                hats.AddChild(sw);
            }

            // --- bottom: controls on a strip of felt, the version on a sewn-in label ---
            var strip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            strip.AddThemeStyleboxOverride("panel", Style.FeltBox(new Color(Style.FeltDark, 0.8f), new Color(0.8f, 0.7f, 0.5f), 22, 10, 9));
            strip.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            strip.OffsetLeft = 60;
            strip.OffsetRight = -60;
            strip.OffsetTop = -118;
            strip.OffsetBottom = -14;
            screenRoot.AddChild(strip);
            strip.AddChild(Text(Loc.T("controls"), 17, new Color(1, 0.96f, 0.88f, 0.9f), true));
            var tag = new PanelContainer();
            tag.AddThemeStyleboxOverride("panel", Style.FeltBox(Style.Cream, Style.Red, 14, 4, 13));
            tag.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            tag.OffsetLeft = -80;
            tag.OffsetRight = -80;
            tag.OffsetTop = 14;
            tag.GrowHorizontal = Control.GrowDirection.Begin;
            tag.Rotation = Mathf.DegToRad(3);
            tag.MouseFilter = Control.MouseFilterEnum.Ignore;
            tag.AddChild(Text("v" + GameConsts.GameVersion + " · " + Loc.T("woolTag"), 15, Style.Ink));
            screenRoot.AddChild(tag);
        }

        void NameEgg(string name)
        {
            var r = EasterEggs.NameReaction(name);
            if (r != null) App.Toast(r, Style.Gold);
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
                        if (a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address)) list.Add(a.Address.ToString() + "|" + ni.Name);
                }
            }
            catch { /* not available on some platforms */ }
            if (list.Count == 0) list.Add("127.0.0.1|localhost");
            localIps = list.ToArray();
            return localIps;
        }

        void HostSetup()
        {
            Logo(screenRoot, new Vector2(0, 20), 54, true);
            var st = App.Settings;
            var v = Panel(screenRoot, 880, 210);
            v.AddChild(Row(new IconView(IconKind.TwoHats, 54), Heading(Loc.T("gangTitle"), 48)));
            v.AddChild(Text(Loc.T("gangInfo"), 21, null, true));
            var chips = new HFlowContainer();
            chips.AddThemeConstantOverride("h_separation", 12);
            chips.AddThemeConstantOverride("v_separation", 10);
            v.AddChild(chips);
            foreach (var entry in LocalIps())
            {
                var parts = entry.Split('|');
                string ip = parts[0];
                var chip = Btn(ip + "   (" + parts[1] + ")", () =>
                {
                    DisplayServer.ClipboardSet(ip);
                    App.Toast(Loc.T("copied") + "  " + ip, Style.Good);
                }, 0, 50, Style.Teal);
                chip.AddThemeFontSizeOverride("font_size", 22);
                chips.AddChild(chip);
            }
            var port = new LineEdit { Text = st.Port.ToString(), MaxLength = 6, CustomMinimumSize = new Vector2(150, 50) };
            v.AddChild(Row(Text(Loc.T("port") + " (UDP):", 22), port));
            var help = new PanelContainer();
            help.AddThemeStyleboxOverride("panel", Style.PaperBox(26, 18));
            var ht = Text(Loc.T("hostHelp"), 18, Style.Ink, true);
            ht.AddThemeConstantOverride("shadow_offset_x", 0);
            ht.AddThemeConstantOverride("shadow_offset_y", 0);
            help.AddChild(ht);
            v.AddChild(help);
            v.AddChild(Row(Btn(Loc.T("openTunnel"), () =>
            {
                if (int.TryParse(port.Text, out var p) && p > 0 && p < 65536) st.Port = p;
                App.StartHost();
            }, 420, 66, Style.Red, IconKind.Sock), Btn(Loc.T("back"), () => App.Screen = AppScreen.Menu, 260, 66, new Color(0.42f, 0.33f, 0.3f))));
        }

        void JoinSetup()
        {
            Logo(screenRoot, new Vector2(0, 20), 54, true);
            var st = App.Settings;
            var v = Panel(screenRoot, 880, 210);
            v.AddChild(Row(new IconView(IconKind.Spyglass, 54), Heading(Loc.T("joinTitle"), 48)));
            v.AddChild(Text(Loc.T("lanGames"), 24, Style.Gold, false, Style.Bold));
            lanList = new VBoxContainer();
            v.AddChild(lanList);
            lanSig = "?";
            v.AddChild(Text(Loc.T("joinBy"), 22, Style.Gold, false, Style.Bold));
            var ip = new LineEdit { Text = st.LastIp, MaxLength = 64, CustomMinimumSize = new Vector2(360, 52), PlaceholderText = Loc.T("address") };
            var port = new LineEdit { Text = st.Port.ToString(), MaxLength = 6, CustomMinimumSize = new Vector2(140, 52) };
            v.AddChild(Row(ip, Text(":", 28), port, Btn(Loc.T("connect"), () =>
            {
                int p = int.TryParse(port.Text, out var pp) ? pp : st.Port;
                App.StartJoin(ip.Text.Trim(), p);
            }, 240, 56, Style.Red)));
            var back = Btn(Loc.T("back"), () =>
            {
                App.StopDiscovery();
                App.Screen = AppScreen.Menu;
            }, 260, 60, new Color(0.42f, 0.33f, 0.3f));
            back.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            v.AddChild(back);
        }

        float nextPing;

        static string GnomesCount(int n)
        {
            if (Loc.Current == Lang.Ru) return n + " " + Loc.RuPlural(n, Loc.T("gnomeWord1"), Loc.T("gnomeWord2"), Loc.T("gnomeWord5"));
            return n + " " + (n == 1 ? Loc.T("gnomeWord1") : Loc.T("gnomeWord2"));
        }

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
            string sig = string.Join("|", hosts.Select(h => h.Info + h.Address + h.Port + h.Players));
            if (sig == lanSig) return;
            lanSig = sig;
            Clear(lanList);
            if (hosts.Count == 0)
            {
                var row = Row(new YarnSpinner { CustomMinimumSize = new Vector2(44, 44) }, Text(Loc.T("noLanGames"), 20, new Color(1, 1, 1, 0.7f)));
                lanList.AddChild(row);
            }
            foreach (var h in hosts)
            {
                var host = h;
                var b = Btn($"{Loc.T("gangOf")} «{host.Info}» · {GnomesCount(Mathf.Max(1, host.Players))} · {host.Address}", () => App.StartJoin(host.Address, host.Port), 820, 58, Style.Teal, IconKind.Hat);
                b.AddThemeFontSizeOverride("font_size", 22);
                lanList.AddChild(b);
            }
        }

        void Connecting()
        {
            Logo(screenRoot, new Vector2(0, 60), 60, true);
            var v = Panel(screenRoot, 620, 320);
            var sp = new YarnSpinner { CustomMinimumSize = new Vector2(120, 120), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
            v.AddChild(sp);
            connectingLabel = Heading(Loc.T("connecting"), 40);
            connectingLabel.HorizontalAlignment = HorizontalAlignment.Center;
            v.AddChild(connectingLabel);
            var tip = Text(EasterEggs.Tip(), 20, new Color(1, 0.95f, 0.85f, 0.85f), true);
            tip.HorizontalAlignment = HorizontalAlignment.Center;
            v.AddChild(tip);
            var back = Btn(Loc.T("back"), App.LeaveToMenu, 260, 60, new Color(0.42f, 0.33f, 0.3f));
            back.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            v.AddChild(back);
        }

        void SettingsScreen()
        {
            var st = App.Settings;
            var v = Panel(screenRoot, 860, 70);
            v.AddChild(Row(new IconView(IconKind.Gear, 50, Style.Mustard), Heading(Loc.T("settings"), 48)));
            v.AddChild(Row(Text(Loc.T("language"), 22), Btn(st.Language == Lang.Ru ? "Русский" : "English", () =>
            {
                st.Language = st.Language == Lang.Ru ? Lang.En : Lang.Ru;
                Loc.Current = st.Language;
                builtScreen = (AppScreen)(-1);
            }, 240, 50, Style.Teal)));
            Slider(v, Loc.T("sensitivity"), st.Sensitivity, 0.3f, 6f, x => st.Sensitivity = x);
            Slider(v, Loc.T("volume"), st.Volume, 0f, 1f, x => st.Volume = x);
            Slider(v, Loc.T("fov"), st.Fov, 55f, 100f, x => st.Fov = x);
            var inv = new CheckBox { Text = Loc.T("invertY"), ButtonPressed = st.InvertY, FocusMode = Control.FocusModeEnum.None };
            inv.Toggled += on => st.InvertY = on;
            var fs = new CheckBox { Text = Loc.T("fullscreen"), ButtonPressed = st.Fullscreen, FocusMode = Control.FocusModeEnum.None };
            fs.Toggled += on =>
            {
                st.Fullscreen = on;
                GameApp.ApplyVideo(st);
            };
            v.AddChild(Row(inv, new Control { CustomMinimumSize = new Vector2(40, 0) }, fs));
            var q = Row(Text(Loc.T("quality"), 22));
            string[] names = { Loc.T("qLow"), Loc.T("qMedium"), Loc.T("qHigh") };
            for (int i = 0; i < 3; i++)
            {
                int qi = i;
                var b = Btn(names[i], () =>
                {
                    st.Quality = qi;
                    GameApp.ApplyVideo(st);
                    builtScreen = (AppScreen)(-1);
                }, 160, 48, st.Quality == i ? Style.Red : new Color(0.4f, 0.32f, 0.36f));
                q.AddChild(b);
            }
            v.AddChild(q);
            var bottom = Row();
            if (App.SettingsReturn == AppScreen.Menu)
                bottom.AddChild(Btn(Loc.T("resetSave"), () =>
                {
                    SaveStore.Reset();
                    App.Toast(Loc.T("resetSave") + " — OK");
                }, 280, 52, new Color(0.45f, 0.3f, 0.3f)));
            bottom.AddChild(Btn(Loc.T("credits"), () => ShowCredits(), 220, 52, Style.Plum, IconKind.Heart));
            v.AddChild(bottom);
            var back = Btn(Loc.T("back"), () =>
            {
                st.Save();
                App.Screen = App.SettingsReturn;
            }, 260, 60, new Color(0.42f, 0.33f, 0.3f));
            back.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            v.AddChild(back);
        }

        void ShowCredits()
        {
            if (creditsOpen) return;
            creditsOpen = true;
            var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
            dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            screenRoot.AddChild(dim);
            var p = new PanelContainer();
            p.AddThemeStyleboxOverride("panel", Style.PaperBox(40, 30));
            p.SetAnchorsPreset(Control.LayoutPreset.Center);
            p.OffsetLeft = -460;
            p.OffsetRight = 460;
            p.OffsetTop = -260;
            p.GrowVertical = Control.GrowDirection.Both;
            p.Rotation = Mathf.DegToRad(-1.5f);
            screenRoot.AddChild(p);
            var v = new VBoxContainer();
            p.AddChild(v);
            v.AddChild(Heading(Loc.T("credits"), 46, Style.Red.Darkened(0.2f)));
            var t = Text(Loc.T("creditsText"), 22, Style.Ink, true);
            t.AddThemeConstantOverride("shadow_offset_x", 0);
            t.AddThemeConstantOverride("shadow_offset_y", 0);
            t.CustomMinimumSize = new Vector2(820, 0);
            v.AddChild(t);
            var ok = Btn(Loc.T("ok"), () =>
            {
                creditsOpen = false;
                dim.QueueFree();
                p.QueueFree();
            }, 200, 56, Style.Red);
            ok.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            v.AddChild(ok);
        }

        static void Slider(VBoxContainer v, string name, float value, float min, float max, System.Action<float> set)
        {
            var label = Text($"{name}: {value:0.0}", 22);
            label.CustomMinimumSize = new Vector2(330, 0);
            var s = new HSlider { MinValue = min, MaxValue = max, Step = 0.05, Value = value, CustomMinimumSize = new Vector2(420, 34), FocusMode = Control.FocusModeEnum.None };
            s.ValueChanged += x =>
            {
                set((float)x);
                label.Text = $"{name}: {x:0.0}";
            };
            v.AddChild(Row(label, s));
        }

        /// <summary>The morning report: a note on paper with the Great Sock's stamp.</summary>
        void ReportScreen()
        {
            var r = App.Report;
            if (r == null) return;
            var dim = new ColorRect { Color = new Color(0.05f, 0.03f, 0.07f, 0.82f) };
            dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            screenRoot.AddChild(dim);
            var p = new PanelContainer();
            p.AddThemeStyleboxOverride("panel", Style.PaperBox(44, 30));
            p.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            p.OffsetLeft = -500;
            p.OffsetRight = 500;
            p.OffsetTop = 40;
            p.Rotation = Mathf.DegToRad(-0.8f);
            screenRoot.AddChild(p);
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 6);
            p.AddChild(v);
            Label Ink(string s, int size, Color? c = null, bool wrap = false, Font f = null)
            {
                var l = Text(s, size, c ?? Style.Ink, wrap, f);
                l.AddThemeConstantOverride("shadow_offset_x", 0);
                l.AddThemeConstantOverride("shadow_offset_y", 0);
                return l;
            }
            v.AddChild(Ink(Loc.T("report"), 46, new Color(0.35f, 0.18f, 0.12f), false, Style.Script));
            int line = r.Chaos >= 18 ? 3 : r.Chaos >= 10 ? 2 : r.Chaos >= 5 ? 1 : 0;
            v.AddChild(Ink(Loc.T("grandpaMorning" + line), 24, null, true, Style.Bold));
            for (int i = 0; i < r.TaskIds.Length; i++)
            {
                var t = TaskCatalog.Get(r.TaskIds[i]);
                if (t == null) continue;
                bool done = i < r.TaskDone.Length && r.TaskDone[i];
                v.AddChild(Row(new IconView(done ? IconKind.Tick : IconKind.Box, 26, Style.Ink), Ink(t.Text(Loc.Current), 21, done ? Style.Moss.Darkened(0.35f) : new Color(0.55f, 0.2f, 0.15f))));
            }
            v.AddChild(Row(new IconView(IconKind.Chaos, 28, Style.Mustard), Ink($"{Loc.T("chaos")}: {Loc.ChaosCount(r.Chaos)}", 22, null, false, Style.Bold)));
            var mats = new List<string>();
            for (int i = 0; i < 5; i++) if (r.Haul[i] > 0) mats.Add($"{Loc.MatName((Mat)i)} +{r.Haul[i]}");
            var matLine = Ink($"{Loc.T("materials")}: {(mats.Count > 0 ? string.Join(", ", mats) : "—")}", 21, null, true);
            matLine.CustomMinimumSize = new Vector2(820, 0);
            v.AddChild(Row(new IconView(IconKind.Sack, 28, new Color(0.7f, 0.52f, 0.32f)), matLine));
            v.AddChild(Row(new IconView(IconKind.Heart, 28, Style.Red), Ink($"{Loc.MatName(Mat.Giggles)}: +{r.Giggles}", 21)));
            v.AddChild(Ink(r.Fired ? Loc.T("fired") : r.Passed ? Loc.T("verdictGood") : Loc.T("verdictBad"), 32, r.Passed ? Style.Moss.Darkened(0.3f) : Style.Red.Darkened(0.15f), true, Style.Script));
            if (S != null && S.IsHost)
            {
                var b = Btn(Loc.T("continue"), App.ContinueFromReport, 340, 64, Style.Red, IconKind.Sock);
                b.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
                v.AddChild(b);
            }
            else v.AddChild(Ink(Loc.T("waitHost"), 20));
            // the rubber stamp
            string stamp = r.Chaos >= 30 ? Loc.T("stampLegend") : r.Passed ? Loc.T("stampGood") : Loc.T("stampBad");
            var sc = r.Passed ? new Color(0.2f, 0.55f, 0.25f, 0.85f) : new Color(0.75f, 0.12f, 0.1f, 0.85f);
            var st = Text(stamp, 50, sc, false, Style.Logo);
            st.AddThemeConstantOverride("shadow_offset_x", 0);
            st.AddThemeConstantOverride("shadow_offset_y", 0);
            st.AddThemeColorOverride("font_outline_color", sc);
            st.AddThemeConstantOverride("outline_size", 2);
            st.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            st.OffsetLeft = 150;
            st.OffsetTop = 70;
            st.Rotation = Mathf.DegToRad(-14);
            st.Scale = new Vector2(1.6f, 1.6f);
            screenRoot.AddChild(st);
            var tw = st.CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(st, "scale", Vector2.One, 0.35f).SetDelay(0.4f);
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
            var dim = new ColorRect { Color = new Color(0.05f, 0.02f, 0.06f, 0.6f) };
            dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            overlayRoot.AddChild(dim);
            var v = Panel(overlayRoot, 620, 110);
            var head = Style.KnitText(Loc.T("paused").ToUpperInvariant(), 44, new Color(0.84f, 0.2f, 0.17f), new Color(0.98f, 0.93f, 0.83f), new Color(0.2f, 0.44f, 0.84f), new Color(0.95f, 0.72f, 0.18f), 14f);
            head.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            v.AddChild(head);
            v.AddChild(Btn(Loc.T("resume"), () => App.Paused = false, 560, 66, Style.Red, IconKind.Sock));
            v.AddChild(Btn(Loc.T("settings"), () =>
            {
                App.SettingsReturn = AppScreen.Playing;
                App.Paused = false;
                App.Screen = AppScreen.Settings;
            }, 560, 66, Style.Mustard, IconKind.Gear));
            v.AddChild(Btn(Loc.T("toMenu"), App.LeaveToMenu, 560, 66, new Color(0.42f, 0.33f, 0.3f), IconKind.Door));
            var note = new PanelContainer();
            note.AddThemeStyleboxOverride("panel", Style.PaperBox(24, 16));
            note.Rotation = Mathf.DegToRad(1.2f);
            var w = Text(Loc.T("gnomeWisdom") + ": «" + EasterEggs.Wisdom() + "»", 21, Style.Ink, true, Style.Bold);
            w.AddThemeConstantOverride("shadow_offset_x", 0);
            w.AddThemeConstantOverride("shadow_offset_y", 0);
            note.AddChild(w);
            v.AddChild(note);
            v.AddChild(Text(Loc.T("controls"), 16, new Color(1, 0.95f, 0.85f, 0.75f), true));
        }

        void Craft()
        {
            var s = S;
            if (s == null) return;
            var dim = new ColorRect { Color = new Color(0.05f, 0.02f, 0.06f, 0.5f) };
            dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            overlayRoot.AddChild(dim);
            var v = Panel(overlayRoot, 1060, 40, 820);
            v.AddChild(Row(new IconView(IconKind.Needle, 50), Heading(Loc.T("craftBench"), 46)));
            var mats = new HFlowContainer();
            mats.AddThemeConstantOverride("h_separation", 10);
            v.AddChild(mats);
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(1000, 520), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            v.AddChild(scroll);
            var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            scroll.AddChild(list);
            Color[] matCol = { new Color(0.9f, 0.85f, 0.7f), new Color(0.75f, 0.78f, 0.82f), Style.Red, Style.Gold, new Color(0.85f, 0.65f, 0.4f), new Color(1f, 0.6f, 0.75f) };
            void Refresh()
            {
                var save = s.Save;
                Clear(mats);
                for (int i = 0; i < 6; i++)
                {
                    var chip = new PanelContainer();
                    var cb = new StyleBoxFlat { BgColor = new Color(Style.FeltDark, 0.92f), BorderColor = matCol[i], ContentMarginLeft = 12, ContentMarginRight = 16, ContentMarginTop = 4, ContentMarginBottom = 4 };
                    cb.SetBorderWidthAll(2);
                    cb.SetCornerRadiusAll(18);
                    chip.AddThemeStyleboxOverride("panel", cb);
                    chip.AddChild(Row(new IconView(IconKind.Yarn, 24, matCol[i]), Text($"{Loc.MatName((Mat)i)}: {save.Materials[i]}", 20, null, false, Style.Bold)));
                    mats.AddChild(chip);
                }
                if (save.Potions > 0) mats.AddChild(Text(GearCatalog.Get(GearId.SleepDust).Name(Loc.Current) + ": " + save.Potions, 20, Style.Gold));
                Clear(list);
                foreach (var gd in GearCatalog.All)
                {
                    var gear = gd;
                    var row = new PanelContainer();
                    row.AddThemeStyleboxOverride("panel", Style.FeltBox(new Color(Style.Felt.Lightened(0.08f), 0.95f), new Color(0.8f, 0.7f, 0.5f), 22, 12, 40 + (int)gear.Id));
                    var h = new HBoxContainer();
                    row.AddChild(h);
                    var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    info.AddChild(Text(gear.Name(Loc.Current), 24, Style.Gold, false, Style.Bold));
                    info.AddChild(Text(gear.Desc(Loc.Current), 19, null, true));
                    var cost = new List<string>();
                    for (int i = 0; i < 6; i++) if (gear.Cost[i] > 0) cost.Add($"{Loc.MatName((Mat)i)} {gear.Cost[i]}");
                    info.AddChild(Text(string.Join(" · ", cost), 18, new Color(0.9f, 0.82f, 0.68f)));
                    h.AddChild(info);
                    bool can = save.CanCraft(gear, out var reason);
                    if (reason == "owned") h.AddChild(Row(new IconView(IconKind.Tick, 30, Style.Good), Text(Loc.T("owned"), 22, Style.Good, false, Style.Bold)));
                    else
                    {
                        var b = Btn(Loc.T("craft"), () =>
                        {
                            s.SendAction(new ActionMsg { Type = ActionType.Craft, I = (int)gear.Id });
                            GameApp.I.Delay(0.3f, () => { if (App.CraftOpen) overlayKey = ""; });
                        }, 200, 54, Style.Red, IconKind.Needle);
                        b.Disabled = !can;
                        h.AddChild(b);
                    }
                    list.AddChild(row);
                }
            }
            Refresh();
            if (!s.IsHost) v.AddChild(Text(Loc.T("craftHostOnly"), 18));
            var back = Btn(Loc.T("back"), () => App.CraftOpen = false, 260, 60, new Color(0.42f, 0.33f, 0.3f));
            back.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            v.AddChild(back);
        }

        /// <summary>The Great Sock speaks: a speech bubble of paper with its knitted name.</summary>
        void SockDialog()
        {
            var s = S;
            var p = new PanelContainer();
            p.AddThemeStyleboxOverride("panel", Style.PaperBox(40, 28));
            p.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            p.OffsetLeft = -520;
            p.OffsetRight = 520;
            p.OffsetTop = -60;
            p.OffsetBottom = -40;
            p.GrowVertical = Control.GrowDirection.Begin;
            overlayRoot.AddChild(p);
            var v = new VBoxContainer();
            p.AddChild(v);
            v.AddChild(Style.KnitText(Loc.T("greatSock").ToUpperInvariant(), 34, new Color(0.84f, 0.2f, 0.17f), new Color(0.96f, 0.9f, 0.8f), new Color(0.2f, 0.44f, 0.84f), new Color(0.95f, 0.72f, 0.18f), 12f));
            Label Ink(string t, int size, bool bold = false)
            {
                var l = Text(t, size, Style.Ink, true, bold ? Style.Bold : null);
                l.AddThemeConstantOverride("shadow_offset_x", 0);
                l.AddThemeConstantOverride("shadow_offset_y", 0);
                return l;
            }
            v.AddChild(Ink(Loc.T("sockSays"), 23, true));
            if (s?.Save != null)
            {
                v.AddChild(Ink($"{Loc.T("day")} {s.Save.Night}.  {Loc.T("strikes")}: {s.Save.Strikes}/{GameConsts.MaxStrikes}", 21));
                v.AddChild(Ink(Loc.T("sockTip" + (s.Save.Night % 6)), 21));
            }
            var ok = Btn(Loc.T("ok"), () => App.SockOpen = false, 220, 56, Style.Red);
            ok.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            v.AddChild(ok);
        }

        void Chat()
        {
            var edit = new LineEdit { MaxLength = 120, PlaceholderText = Loc.T("chat") };
            edit.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
            edit.OffsetLeft = 20;
            edit.OffsetRight = 660;
            edit.OffsetTop = -262;
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

        /// <summary>The aim point: a little ring of yarn that glows when something can be used.</summary>
        partial class Reticle : Control
        {
            public bool Hot;

            public Reticle()
            {
                MouseFilter = MouseFilterEnum.Ignore;
                Size = new Vector2(24, 24);
            }

            public override void _Draw()
            {
                var c = Size / 2;
                var col = Hot ? Style.Gold : new Color(1, 1, 1, 0.85f);
                DrawArc(c, Hot ? 7f : 5f, 0, Mathf.Tau, 20, new Color(0, 0, 0, 0.5f), 3.5f, true);
                DrawArc(c, Hot ? 7f : 5f, 0, Mathf.Tau, 20, col, 2f, true);
                if (Hot) DrawCircle(c, 2f, col);
            }
        }

        public void OnLevelLoaded(LevelKind kind)
        {
            hudKind = kind;
            Clear(hudRoot);
            chaosLabel = haulLabel = pocketLabel = grandpaLabel = stepsLabel = lightLabel = promptLabel = yarnLabel = villageLabel = waitLabel = tasksHead = null;
            tasksList = playersBox = null;
            spareHats = null;
            moon = null;
            tasksSig = playersSig = spareSig = "";
            crosshair = new Reticle();
            crosshair.SetAnchorsPreset(Control.LayoutPreset.Center);
            crosshair.OffsetLeft = -12;
            crosshair.OffsetTop = -12;
            crosshair.OffsetRight = 12;
            crosshair.OffsetBottom = 12;
            hudRoot.AddChild(crosshair);
            promptPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            promptPanel.AddThemeStyleboxOverride("panel", Style.FeltBox(new Color(Style.FeltDark, 0.8f), Style.Gold, 22, 8, 17));
            promptPanel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            promptPanel.OffsetTop = -330;
            promptPanel.OffsetBottom = -270;
            promptPanel.GrowHorizontal = Control.GrowDirection.Both;
            hudRoot.AddChild(promptPanel);
            promptLabel = Text("", 25, null, false, Style.Bold);
            promptLabel.HorizontalAlignment = HorizontalAlignment.Center;
            promptPanel.AddChild(promptLabel);
            holdBar = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(260, 12) };
            holdBar.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            holdBar.OffsetLeft = -130;
            holdBar.OffsetRight = 130;
            holdBar.OffsetTop = -258;
            holdBar.OffsetBottom = -246;
            hudRoot.AddChild(holdBar);
            yarnLabel = Text("", 18, null, false, Style.Bold);
            yarnLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
            yarnLabel.OffsetLeft = 30;
            yarnLabel.OffsetTop = 12;
            hudRoot.AddChild(yarnLabel);
            gripBar = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(130, 10) };
            var gripFill = new StyleBoxFlat { BgColor = new Color(0.5f, 0.9f, 1f) };
            gripFill.SetCornerRadiusAll(5);
            gripBar.AddThemeStyleboxOverride("fill", gripFill);
            gripBar.SetAnchorsPreset(Control.LayoutPreset.Center);
            gripBar.OffsetLeft = 30;
            gripBar.OffsetRight = 160;
            gripBar.OffsetTop = 44;
            gripBar.OffsetBottom = 54;
            hudRoot.AddChild(gripBar);
            var pb = Corner(hudRoot, Control.LayoutPreset.TopRight, new Vector2(14, kind == LevelKind.House ? 172 : 14), new Vector2(280, 40));
            playersBox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            playersBox.AddThemeConstantOverride("separation", 2);
            pb.AddChild(playersBox);
            pb.Visible = false;

            if (kind == LevelKind.House)
            {
                var cp = Corner(hudRoot, Control.LayoutPreset.CenterTop, new Vector2(0, 8), new Vector2(330, 110));
                cp.OffsetLeft = -165;
                cp.OffsetRight = 165;
                moon = new MoonClock { CustomMinimumSize = new Vector2(290, 92) };
                cp.AddChild(moon);

                // the prank list: a paper note in the corner
                tasksPanel = Corner(hudRoot, Control.LayoutPreset.TopLeft, new Vector2(14, 14), new Vector2(590, 60), true);
                tasksPanel.Rotation = Mathf.DegToRad(-0.6f);
                var tv = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                tv.AddThemeConstantOverride("separation", 2);
                tasksPanel.AddChild(tv);
                tasksHead = Text("", 26, new Color(0.35f, 0.18f, 0.12f), false, Style.Script);
                tasksHead.AddThemeConstantOverride("shadow_offset_x", 0);
                tasksHead.AddThemeConstantOverride("shadow_offset_y", 0);
                tv.AddChild(tasksHead);
                tasksList = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                tasksList.AddThemeConstantOverride("separation", 0);
                tv.AddChild(tasksList);

                var chaosPanel = Corner(hudRoot, Control.LayoutPreset.TopRight, new Vector2(14, 14), new Vector2(280, 140));
                var cv = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                cv.AddThemeConstantOverride("separation", 4);
                chaosPanel.AddChild(cv);
                chaosLabel = Text("", 22, null, false, Style.Bold);
                cv.AddChild(Row(new IconView(IconKind.Chaos, 30, Style.Gold), chaosLabel));
                haulLabel = Text("", 22, null, false, Style.Bold);
                cv.AddChild(Row(new IconView(IconKind.Sack, 30, new Color(0.85f, 0.65f, 0.4f)), haulLabel));
                spareHats = Row();
                cv.AddChild(spareHats);

                var pocketPanel = Corner(hudRoot, Control.LayoutPreset.BottomLeft, new Vector2(14, 14), new Vector2(660, 56));
                pocketLabel = Text("", 20, null, false, Style.Bold);
                pocketPanel.AddChild(Row(new IconView(IconKind.Pocket, 32, new Color(0.45f, 0.62f, 0.8f)), pocketLabel));

                grandpaPanel = Corner(hudRoot, Control.LayoutPreset.BottomRight, new Vector2(14, 14), new Vector2(440, 56));
                grandpaIcon = new IconView(IconKind.Zzz, 34, Style.Night);
                grandpaLabel = Text("", 23, null, false, Style.Bold);
                grandpaPanel.AddChild(Row(grandpaIcon, grandpaLabel));

                stealthPanel = Corner(hudRoot, Control.LayoutPreset.BottomRight, new Vector2(14, 80), new Vector2(440, 48));
                stepsIcon = new IconView(IconKind.Steps, 28);
                stepsLabel = Text("", 19, null, false, Style.Bold);
                stepsLabel.CustomMinimumSize = new Vector2(210, 0);
                lightIcon = new IconView(IconKind.Moon, 28);
                lightLabel = Text("", 19, null, false, Style.Bold);
                stealthPanel.AddChild(Row(stepsIcon, stepsLabel, lightIcon, lightLabel));
            }
            else
            {
                var vp = Corner(hudRoot, Control.LayoutPreset.TopLeft, new Vector2(14, 14), new Vector2(540, 250));
                var vv = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                vp.AddChild(vv);
                vv.AddChild(Heading(Loc.T("hub"), 34));
                villageLabel = Text("", 20, null, true);
                villageLabel.CustomMinimumSize = new Vector2(500, 0);
                vv.AddChild(villageLabel);
                waitLabel = Text("", 24, Style.Gold, false, Style.Bold);
                waitLabel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
                waitLabel.OffsetLeft = -340;
                waitLabel.OffsetRight = 340;
                waitLabel.OffsetTop = -64;
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
            bool hot = g?.Prompt != null;
            if (crosshair.Hot != hot)
            {
                crosshair.Hot = hot;
                crosshair.QueueRedraw();
            }
            promptLabel.Text = g?.Prompt ?? "";
            promptPanel.Visible = !string.IsNullOrEmpty(promptLabel.Text);
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
            UpdatePlayers(s);
            if (hudKind == LevelKind.House && w.Kind == LevelKind.House) HouseHud(s, w, g);
            else if (villageLabel != null) VillageHud(s);
        }

        void UpdatePlayers(GameSession s)
        {
            var box = playersBox;
            if (box == null) return;
            var panel = (Control)box.GetParent();
            panel.Visible = !s.IsSolo;
            if (s.IsSolo) return;
            string sig = string.Join("|", s.Players.Values.Select(p => p.Name + p.Hat + p.Status + p.PortalReady));
            if (sig == playersSig) return;
            playersSig = sig;
            Clear(box);
            box.AddChild(Text(Loc.T("theGang"), 24, Style.Gold, false, Style.Script));
            foreach (var p in s.Players.Values)
            {
                string st = p.Status == PlayerStatus.Trapped ? " · " + Loc.T("mechJar") : p.Status == PlayerStatus.Dead ? " · X" : p.Status == PlayerStatus.Home ? " · ~" : p.Status == PlayerStatus.Carried ? " · !" : "";
                if (hudKind == LevelKind.Hub && p.PortalReady) st += " ✓";
                var col = GameSession.IsRainbow(p.Hat) ? Style.Gold : GameSession.HatColor(p.Hat);
                box.AddChild(Row(new IconView(IconKind.Hat, 24, col), Text(p.Name + st, 19, null, false, Style.Bold)));
            }
        }

        void HouseHud(GameSession s, GameWorld w, LocalGnome g)
        {
            float timeLeft = s.IsHost ? (s.Host.Night != null ? s.Host.Night.TimeLeft : GameConsts.NightSeconds) : s.TimeLeft;
            float prog = 1f - timeLeft / GameConsts.NightSeconds;
            float hours = prog * 6f;
            string time = $"{(int)hours:00}:{(int)((hours - (int)hours) * 60):00}";
            if (moon != null && (moon.Time != time || Mathf.Abs(moon.Progress - prog) > 0.002f))
            {
                moon.Time = time;
                moon.Progress = prog;
                moon.QueueRedraw();
            }

            if (App.GameplayInput && Keys.Pressed(Key.Tab)) tasksExpanded = !tasksExpanded;
            var tasks = s.IsHost ? s.Host.Night?.Tasks : s.ClientTasks;
            if (tasks != null)
            {
                string sig = tasksExpanded + "|" + tasks.CompletedCount + "|" + string.Join(",", tasks.Progress) + Loc.Current;
                if (sig != tasksSig)
                {
                    tasksSig = sig;
                    tasksHead.Text = $"{Loc.T("tasks")}  {tasks.CompletedCount}/{GameConsts.TasksRequired}   [Tab]";
                    Clear(tasksList);
                    if (tasksExpanded)
                        for (int i = 0; i < tasks.Tasks.Count; i++)
                        {
                            var t = tasks.Tasks[i];
                            bool done = tasks.IsDone(i);
                            string prog2 = t.Goal > 1 ? $" ({tasks.Progress[i]}/{t.Goal})" : "";
                            var l = Text(t.Text(Loc.Current) + prog2, 19, done ? Style.Moss.Darkened(0.35f) : Style.Ink, false, Style.Bold);
                            l.AddThemeConstantOverride("shadow_offset_x", 0);
                            l.AddThemeConstantOverride("shadow_offset_y", 0);
                            var row = Row(new IconView(done ? IconKind.Tick : IconKind.Box, 24, Style.Ink), l);
                            for (int k = 0; k < t.Tier; k++) row.AddChild(new IconView(IconKind.Sock, 20, new Color(0.95f, 0.8f, 0.5f)));
                            tasksList.AddChild(row);
                        }
                }
            }
            int chaos = s.IsHost ? (s.Host.Night?.Chaos ?? 0) : ChaosFromTasks();
            chaosLabel.Text = $"{Loc.T("chaos")}: {chaos}";
            haulLabel.Text = s.IsHost ? $"{Loc.T("haul")}: {s.Host.Night?.HaulValue ?? 0}" : "";
            haulLabel.GetParent<Control>().Visible = s.IsHost;
            string spare = s.IsSolo ? s.SpareHats.ToString() : "";
            if (spare != spareSig)
            {
                spareSig = spare;
                Clear(spareHats);
                if (s.IsSolo)
                {
                    spareHats.AddChild(Text(Loc.T("spareHats") + ":", 19, null, false, Style.Bold));
                    for (int i = 0; i < s.SpareHats; i++) spareHats.AddChild(new IconView(IconKind.Hat, 26, Style.Red));
                }
            }

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
                IconKind icon;
                switch (om.Mode)
                {
                    case OldMan.St.Sleep:
                    case OldMan.St.Doze:
                        txt = Loc.T("oldManAsleep");
                        c = Style.Night;
                        icon = IconKind.Zzz;
                        break;
                    case OldMan.St.Chase:
                    case OldMan.St.Grab:
                    case OldMan.St.Swat:
                    case OldMan.St.Carry:
                        txt = Loc.T("oldManAlert");
                        c = new Color(1f, 0.4f, 0.33f);
                        icon = IconKind.Alert;
                        break;
                    case OldMan.St.Investigate:
                    case OldMan.St.Search:
                    case OldMan.St.WakeUp:
                        txt = Loc.T("oldManSuspicious");
                        c = new Color(1f, 0.85f, 0.38f);
                        icon = IconKind.Question;
                        break;
                    default:
                        txt = Loc.T("oldManAwake");
                        c = new Color(1f, 0.95f, 0.82f);
                        icon = IconKind.Eye;
                        break;
                }
                grandpaLabel.Text = txt;
                grandpaLabel.AddThemeColorOverride("font_color", c);
                grandpaIcon.Set(icon, c);
                // a gentle pulse while he is on your tail
                grandpaPanel.Modulate = icon == IconKind.Alert ? new Color(1, 0.85f + Mathf.Sin(Clock.Now * 10f) * 0.15f, 0.85f + Mathf.Sin(Clock.Now * 10f) * 0.15f) : Colors.White;
            }
            // stealth: how loud am I, am I lit?
            stealthPanel.Visible = om != null && g != null && g.Status == PlayerStatus.Free;
            if (stealthPanel.Visible)
            {
                float mul = GearEffects.From(s.Save).FootstepNoiseMul;
                bool moving = g.Velocity.Flat().LengthSquared() > 0.5f && g.Grounded;
                string step;
                Color sc;
                if (!moving || g.Crouching) { step = Loc.T(moving ? "stepQuiet" : "stepSilent"); sc = Style.Good; }
                else if (g.Sprinting && mul > 0.5f) { step = Loc.T("stepLoud"); sc = new Color(1f, 0.42f, 0.36f); }
                else if (mul <= 0.5f) { step = Loc.T("stepQuiet"); sc = Style.Good; }
                else { step = Loc.T("stepNormal"); sc = new Color(1f, 0.9f, 0.5f); }
                stepsLabel.Text = step;
                stepsLabel.AddThemeColorOverride("font_color", sc);
                stepsIcon.Set(IconKind.Steps, sc);
                bool lit = w.IsLit(g.Center) || om.InTorch(g.Center);
                lightLabel.Text = Loc.T(lit ? "inLight" : "inShadow");
                var lc = lit ? new Color(1f, 0.85f, 0.42f) : Style.Night;
                lightLabel.AddThemeColorOverride("font_color", lc);
                lightIcon.Set(lit ? IconKind.Sun : IconKind.Moon, lc);
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
            sb.Append($"{Loc.T("day")}: {save.Night}    {Loc.T("strikes")}: {save.Strikes}/{GameConsts.MaxStrikes}\n");
            for (int i = 0; i < 6; i++) sb.Append($"{Loc.MatName((Mat)i)}: {save.Materials[i]}").Append(i % 2 == 1 ? "\n" : "      ");
            sb.Append(Loc.T("portalHint"));
            villageLabel.Text = sb.ToString();
            waitLabel.Text = s.IsSolo ? "" : $"{Loc.T("waitingPlayers")}: {s.Players.Values.Count(p => p.PortalReady)}/{s.Players.Count}";
        }
    }
}
