using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Gnomes.App;
using Gnomes.Core;
using Gnomes.Core.Protocol;
using Gnomes.Core.Rules;
using Gnomes.NPC;
using Gnomes.Players;
using Gnomes.Session;
using Gnomes.World;
using UnityEngine;

namespace Gnomes.UI
{
    /// <summary>All screens and the HUD, drawn with IMGUI (no prefabs or UI assets needed).</summary>
    public class UiRoot : MonoBehaviour
    {
        GameApp App => GameApp.I;
        GameSession S => GameSession.I;
        float u; // UI scale
        GUIStyle panel, button, title, subtitle, label, small, big, center, toastStyle, dark, tagStyle;
        Texture2D white;
        string ipField, portField, chatField = "";
        Vector2 craftScroll;
        bool tasksExpanded = true;
        string[] localIps;

        // ------------------------------------------------------------------ styles

        static Texture2D Tex(Color c, int size = 1, int radius = 0)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = 1f;
                    if (radius > 0)
                    {
                        float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                        float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                        a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    }
                    px[y * size + x] = new Color(c.r, c.g, c.b, c.a * a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        void Styles()
        {
            float nu = Mathf.Max(0.55f, UnityEngine.Screen.height / 1080f);
            if (panel != null && Mathf.Abs(nu - u) < 0.01f) return;
            u = nu;
            white = Tex(Color.white);
            panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset((int)(22 * u), (int)(22 * u), (int)(18 * u), (int)(18 * u)), border = new RectOffset(10, 10, 10, 10) };
            panel.normal.background = Tex(new Color(0.09f, 0.07f, 0.1f, 0.88f), 24, 10);
            button = new GUIStyle(GUI.skin.button) { fontSize = (int)(26 * u), fontStyle = FontStyle.Bold, border = new RectOffset(10, 10, 10, 10), padding = new RectOffset(12, 12, 8, 8) };
            button.normal.background = Tex(new Color(0.78f, 0.24f, 0.2f, 1f), 24, 10);
            button.hover.background = Tex(new Color(0.9f, 0.34f, 0.26f, 1f), 24, 10);
            button.active.background = Tex(new Color(0.6f, 0.16f, 0.14f, 1f), 24, 10);
            button.normal.textColor = button.hover.textColor = button.active.textColor = new Color(1f, 0.97f, 0.9f);
            title = new GUIStyle(GUI.skin.label) { fontSize = (int)(84 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(1f, 0.85f, 0.35f);
            subtitle = new GUIStyle(GUI.skin.label) { fontSize = (int)(26 * u), fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            subtitle.normal.textColor = new Color(0.95f, 0.9f, 0.85f);
            label = new GUIStyle(GUI.skin.label) { fontSize = (int)(24 * u), wordWrap = true, richText = true };
            label.normal.textColor = new Color(0.97f, 0.95f, 0.9f);
            small = new GUIStyle(label) { fontSize = (int)(19 * u) };
            big = new GUIStyle(label) { fontSize = (int)(34 * u), fontStyle = FontStyle.Bold };
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            toastStyle = new GUIStyle(label) { fontSize = (int)(26 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            dark = new GUIStyle(GUI.skin.box);
            dark.normal.background = Tex(new Color(0f, 0f, 0f, 0.55f), 24, 8);
            dark.border = new RectOffset(8, 8, 8, 8);
            tagStyle = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.skin.textField.fontSize = (int)(24 * u);
            GUI.skin.horizontalSlider.fixedHeight = 14 * u;
            GUI.skin.horizontalSliderThumb.fixedHeight = 22 * u;
            GUI.skin.horizontalSliderThumb.fixedWidth = 22 * u;
            GUI.skin.toggle.fontSize = (int)(24 * u);
        }

        void Shadowed(Rect r, string text, GUIStyle st)
        {
            var c = st.normal.textColor;
            st.normal.textColor = new Color(0, 0, 0, 0.6f);
            GUI.Label(new Rect(r.x + 3 * u, r.y + 3 * u, r.width, r.height), text, st);
            st.normal.textColor = c;
            GUI.Label(r, text, st);
        }

        bool Btn(string text, float w = 420, float h = 64) => GUILayout.Button(text, button, GUILayout.Width(w * u), GUILayout.Height(h * u));

        void Bar(Rect r, float v, Color c)
        {
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(r, white);
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height), white);
            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------ root

        void OnGUI()
        {
            if (App == null) return;
            Styles();
            GUI.depth = 0;
            switch (App.Screen)
            {
                case AppScreen.Menu: MainMenu(); break;
                case AppScreen.HostSetup: HostSetup(); break;
                case AppScreen.JoinSetup: JoinSetup(); break;
                case AppScreen.Connecting: Connecting(); break;
                case AppScreen.Settings: SettingsScreen(); break;
                case AppScreen.Playing:
                    Hud();
                    if (App.Paused) Pause();
                    if (App.CraftOpen) Craft();
                    if (App.SockOpen) SockDialog();
                    if (App.ChatOpen) Chat();
                    break;
                case AppScreen.Report: ReportScreen(); break;
            }
            Toasts();
        }

        void Toasts()
        {
            float y = UnityEngine.Screen.height * 0.16f;
            foreach (var t in App.Toasts)
            {
                float a = Mathf.Clamp01(t.until - Time.unscaledTime);
                var r = new Rect(0, y, UnityEngine.Screen.width, 40 * u);
                toastStyle.normal.textColor = new Color(0, 0, 0, 0.7f * a);
                GUI.Label(new Rect(r.x + 2 * u, r.y + 2 * u, r.width, r.height), t.text, toastStyle);
                toastStyle.normal.textColor = new Color(t.color.r, t.color.g, t.color.b, a);
                GUI.Label(r, t.text, toastStyle);
                y += 42 * u;
            }
        }

        // ------------------------------------------------------------------ menus

        void TitleBlock(float y)
        {
            float w = UnityEngine.Screen.width;
            Shadowed(new Rect(0, y, w, 110 * u), Loc.T("title"), title);
            GUI.Label(new Rect(w * 0.15f, y + 105 * u, w * 0.7f, 40 * u), Loc.T("subtitle"), subtitle);
        }

        void MainMenu()
        {
            TitleBlock(UnityEngine.Screen.height * 0.07f);
            var st = App.Settings;
            float w = 560 * u;
            var area = new Rect((UnityEngine.Screen.width - w) / 2, UnityEngine.Screen.height * 0.3f, w, UnityEngine.Screen.height * 0.66f);
            GUILayout.BeginArea(area, panel);
            GUILayout.Label(Loc.T("name"), small);
            st.Name = GUILayout.TextField(st.Name ?? "", 16, GUILayout.Height(40 * u));
            GUILayout.Space(6 * u);
            GUILayout.Label(Loc.T("hatColor"), small);
            GUILayout.BeginHorizontal();
            for (byte i = 0; i < GameSession.HatCount; i++)
            {
                GUI.color = GameSession.HatColor(i);
                if (GUILayout.Button(st.Hat == i ? "▲" : "", GUILayout.Width(52 * u), GUILayout.Height(40 * u))) st.Hat = i;
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.Space(14 * u);
            if (Btn(Loc.T("solo"), 510)) App.StartSolo();
            GUILayout.Space(6 * u);
            if (Btn(Loc.T("host"), 510)) App.Screen = AppScreen.HostSetup;
            GUILayout.Space(6 * u);
            if (Btn(Loc.T("join"), 510))
            {
                App.Screen = AppScreen.JoinSetup;
                App.StartDiscovery();
            }
            GUILayout.Space(6 * u);
            if (Btn(Loc.T("settings"), 510))
            {
                App.SettingsReturn = AppScreen.Menu;
                App.Screen = AppScreen.Settings;
            }
            GUILayout.Space(6 * u);
            if (Btn(Loc.T("quit"), 510)) Application.Quit();
            if (!string.IsNullOrEmpty(App.MenuMessage))
            {
                GUILayout.Space(8 * u);
                GUI.color = new Color(1f, 0.6f, 0.5f);
                GUILayout.Label(App.MenuMessage, small);
                GUI.color = Color.white;
            }
            GUILayout.EndArea();
            GUI.Label(new Rect(20 * u, UnityEngine.Screen.height - 110 * u, UnityEngine.Screen.width - 40 * u, 100 * u), Loc.T("controls"), small);
            GUI.Label(new Rect(UnityEngine.Screen.width - 220 * u, 10 * u, 210 * u, 30 * u), "v" + GameConsts.GameVersion, small);
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
            TitleBlock(UnityEngine.Screen.height * 0.05f);
            var st = App.Settings;
            float w = 700 * u;
            GUILayout.BeginArea(new Rect((UnityEngine.Screen.width - w) / 2, UnityEngine.Screen.height * 0.28f, w, UnityEngine.Screen.height * 0.64f), panel);
            GUILayout.Label("<b>" + Loc.T("host") + "</b>", big);
            GUILayout.Label(Loc.T("hostInfo") + ":", small);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("port"), label, GUILayout.Width(120 * u));
            portField = GUILayout.TextField(portField ?? st.Port.ToString(), 6, GUILayout.Width(160 * u), GUILayout.Height(40 * u));
            GUILayout.EndHorizontal();
            GUILayout.Space(8 * u);
            GUILayout.Label("IP:", small);
            foreach (var ip in LocalIps()) GUILayout.Label("  " + ip, label);
            GUILayout.Space(6 * u);
            GUILayout.Label(Loc.T("hostHelp"), small);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (Btn(Loc.T("host"), 320))
            {
                if (int.TryParse(portField, out var port) && port > 0 && port < 65536) st.Port = port;
                App.StartHost();
            }
            GUILayout.Space(10 * u);
            if (Btn(Loc.T("back"), 240)) App.Screen = AppScreen.Menu;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void JoinSetup()
        {
            TitleBlock(UnityEngine.Screen.height * 0.05f);
            var st = App.Settings;
            float w = 700 * u;
            GUILayout.BeginArea(new Rect((UnityEngine.Screen.width - w) / 2, UnityEngine.Screen.height * 0.28f, w, UnityEngine.Screen.height * 0.64f), panel);
            GUILayout.Label("<b>" + Loc.T("join") + "</b>", big);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("address"), label, GUILayout.Width(220 * u));
            ipField = GUILayout.TextField(ipField ?? st.LastIp, 64, GUILayout.Width(300 * u), GUILayout.Height(40 * u));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("port"), label, GUILayout.Width(220 * u));
            portField = GUILayout.TextField(portField ?? st.Port.ToString(), 6, GUILayout.Width(160 * u), GUILayout.Height(40 * u));
            GUILayout.EndHorizontal();
            GUILayout.Space(8 * u);
            if (Btn(Loc.T("connect"), 320))
            {
                int port = int.TryParse(portField, out var p) ? p : st.Port;
                App.StartJoin((ipField ?? st.LastIp).Trim(), port);
            }
            GUILayout.Space(12 * u);
            GUILayout.Label("<b>" + Loc.T("lanGames") + "</b>", label);
            var d = App.Discovery;
            if (d != null)
            {
                if (Time.frameCount % 60 == 0) d.Ping();
                var hosts = d.Hosts.ToList();
                if (hosts.Count == 0) GUILayout.Label(Loc.T("noLanGames"), small);
                foreach (var h in hosts)
                    if (GUILayout.Button(h.Info + "   " + h.Address + ":" + h.Port, button, GUILayout.Height(44 * u)))
                        App.StartJoin(h.Address, h.Port);
            }
            GUILayout.FlexibleSpace();
            if (Btn(Loc.T("back"), 240))
            {
                App.StopDiscovery();
                App.Screen = AppScreen.Menu;
            }
            GUILayout.EndArea();
        }

        void Connecting()
        {
            TitleBlock(UnityEngine.Screen.height * 0.1f);
            float w = 500 * u;
            GUILayout.BeginArea(new Rect((UnityEngine.Screen.width - w) / 2, UnityEngine.Screen.height * 0.45f, w, 200 * u), panel);
            GUILayout.Label(Loc.T("connecting") + new string('.', (int)(Time.time * 2) % 4), big);
            if (Btn(Loc.T("back"), 240)) App.LeaveToMenu();
            GUILayout.EndArea();
        }

        void SettingsScreen()
        {
            var st = App.Settings;
            float w = 700 * u;
            GUILayout.BeginArea(new Rect((UnityEngine.Screen.width - w) / 2, UnityEngine.Screen.height * 0.15f, w, UnityEngine.Screen.height * 0.72f), panel);
            GUILayout.Label("<b>" + Loc.T("settings") + "</b>", big);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("language"), label, GUILayout.Width(300 * u));
            if (GUILayout.Button(st.Language == Lang.Ru ? "Русский" : "English", button, GUILayout.Width(220 * u), GUILayout.Height(44 * u)))
                st.Language = st.Language == Lang.Ru ? Lang.En : Lang.Ru;
            GUILayout.EndHorizontal();
            Slider(Loc.T("sensitivity"), ref st.Sensitivity, 0.3f, 6f);
            Slider(Loc.T("volume"), ref st.Volume, 0f, 1f);
            Slider(Loc.T("fov"), ref st.Fov, 55f, 100f);
            st.InvertY = GUILayout.Toggle(st.InvertY, " " + Loc.T("invertY"));
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("quality"), label, GUILayout.Width(300 * u));
            string[] q = { "Low", "Medium", "High" };
            for (int i = 0; i < 3; i++)
            {
                GUI.color = st.Quality == i ? Color.white : new Color(1, 1, 1, 0.55f);
                if (GUILayout.Button(q[i], button, GUILayout.Width(120 * u), GUILayout.Height(40 * u)))
                {
                    st.Quality = i;
                    ApplyQuality(i);
                }
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.Space(10 * u);
            if (App.SettingsReturn == AppScreen.Menu && GUILayout.Button(Loc.T("resetSave"), button, GUILayout.Width(360 * u), GUILayout.Height(44 * u)))
            {
                SaveStore.Reset();
                App.Toast(Loc.T("resetSave") + " ✓");
            }
            GUILayout.FlexibleSpace();
            if (Btn(Loc.T("back"), 240))
            {
                st.Save();
                App.Screen = App.SettingsReturn;
            }
            GUILayout.EndArea();
        }

        public static void ApplyQuality(int q)
        {
            QualitySettings.shadows = q == 0 ? ShadowQuality.Disable : ShadowQuality.All;
            QualitySettings.shadowResolution = q == 2 ? ShadowResolution.High : ShadowResolution.Medium;
            QualitySettings.antiAliasing = q == 2 ? 4 : q == 1 ? 2 : 0;
            QualitySettings.pixelLightCount = q == 0 ? 3 : q == 1 ? 6 : 10;
        }

        void Slider(string name, ref float v, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name + ": " + v.ToString("0.0"), label, GUILayout.Width(300 * u));
            v = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(330 * u));
            GUILayout.EndHorizontal();
            GUILayout.Space(8 * u);
        }

        void Pause()
        {
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), white);
            GUI.color = Color.white;
            float w = 520 * u;
            GUILayout.BeginArea(new Rect((UnityEngine.Screen.width - w) / 2, UnityEngine.Screen.height * 0.25f, w, 480 * u), panel);
            if (Btn(Loc.T("resume"), 470)) App.Paused = false;
            GUILayout.Space(8 * u);
            if (Btn(Loc.T("settings"), 470))
            {
                App.SettingsReturn = AppScreen.Playing;
                App.Screen = AppScreen.Settings;
                App.Paused = false;
            }
            GUILayout.Space(8 * u);
            if (Btn(Loc.T("toMenu"), 470)) App.LeaveToMenu();
            GUILayout.Space(10 * u);
            GUILayout.Label(Loc.T("controls"), small);
            GUILayout.EndArea();
        }

        void Chat()
        {
            var r = new Rect(20 * u, UnityEngine.Screen.height - 200 * u, 600 * u, 44 * u);
            GUI.SetNextControlName("chat");
            chatField = GUI.TextField(r, chatField, 120);
            GUI.FocusControl("chat");
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return)
            {
                if (!string.IsNullOrWhiteSpace(chatField)) S?.SendAction(new ActionMsg { Type = ActionType.Chat, S = chatField });
                chatField = "";
                App.ChatOpen = false;
                e.Use();
            }
        }

        // ------------------------------------------------------------------ HUD

        void Hud()
        {
            var s = S;
            var w = GameWorld.Current;
            var g = LocalGnome.I;
            if (s == null || w == null) return;
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;

            // crosshair
            if (g != null && g.Status == PlayerStatus.Free)
            {
                float cs = 6 * u;
                GUI.color = g.Prompt != null ? new Color(1f, 0.9f, 0.3f) : new Color(1, 1, 1, 0.8f);
                GUI.DrawTexture(new Rect(sw / 2 - cs / 2, sh / 2 - cs / 2, cs, cs), white);
                GUI.color = Color.white;
            }

            if (w.Kind == LevelKind.House) HouseHud(s, w, g, sw, sh);
            else VillageHud(s, g, sw, sh);

            // prompt + hold bar
            if (g != null && !string.IsNullOrEmpty(g.Prompt))
            {
                var r = new Rect(sw * 0.2f, sh * 0.62f, sw * 0.6f, 40 * u);
                Shadowed(r, g.Prompt, center);
                if (g.HoldProgress > 0) Bar(new Rect(sw / 2 - 120 * u, sh * 0.62f + 44 * u, 240 * u, 10 * u), g.HoldProgress, new Color(1f, 0.85f, 0.3f));
            }
            // yarn length / grip
            if (g != null && g.OnYarn)
            {
                GUI.Label(new Rect(sw / 2 + 30 * u, sh / 2 + 10 * u, 300 * u, 30 * u), $"{Loc.T("yarn")}: {g.YarnLength:0.0} / {g.YarnRange:0}", small);
                if (g.Mode == ArmMode.Climb) Bar(new Rect(sw / 2 + 30 * u, sh / 2 + 42 * u, 120 * u, 8 * u), g.Grip01, new Color(0.5f, 0.9f, 1f));
            }
            // players (co-op)
            if (!s.IsSolo)
            {
                float y = 150 * u;
                foreach (var p in s.Players.Values)
                {
                    GUI.color = p.Color;
                    GUI.DrawTexture(new Rect(sw - 300 * u, y + 8 * u, 16 * u, 16 * u), white);
                    GUI.color = Color.white;
                    string st = p.Status == PlayerStatus.Trapped ? " [" + Loc.T("mechJar") + "]" : p.Status == PlayerStatus.Dead ? " [X]" : p.Status == PlayerStatus.Home ? " [~]" : p.Status == PlayerStatus.Carried ? " [!]" : "";
                    GUI.Label(new Rect(sw - 276 * u, y, 270 * u, 32 * u), p.Name + st, small);
                    y += 30 * u;
                }
            }
        }

        void HouseHud(GameSession s, GameWorld w, LocalGnome g, float sw, float sh)
        {
            // clock
            float timeLeft = s.IsHost ? (s.Host.Night != null ? s.Host.Night.TimeLeft : 0) : s.TimeLeft;
            float prog = 1f - timeLeft / GameConsts.NightSeconds;
            float hours = prog * 6f;
            string clock = $"{(int)hours:00}:{(int)((hours - (int)hours) * 60):00}";
            var cr = new Rect(sw / 2 - 150 * u, 12 * u, 300 * u, 70 * u);
            GUI.Box(cr, GUIContent.none, dark);
            GUI.Label(new Rect(cr.x, cr.y + 2 * u, cr.width, 40 * u), clock, new GUIStyle(big) { alignment = TextAnchor.MiddleCenter });
            Bar(new Rect(cr.x + 20 * u, cr.y + 48 * u, cr.width - 40 * u, 8 * u), prog, new Color(1f, 0.75f, 0.35f));

            // pranks list
            if (Input.GetKeyDown(KeyCode.Tab)) tasksExpanded = !tasksExpanded;
            var tasks = s.IsHost ? s.Host.Night?.Tasks : s.ClientTasks;
            if (tasks != null)
            {
                float lh = 30 * u;
                float h = tasksExpanded ? (tasks.Tasks.Count + 1) * lh + 24 * u : lh + 20 * u;
                var tr = new Rect(14 * u, 14 * u, 560 * u, h);
                GUI.Box(tr, GUIContent.none, dark);
                GUI.Label(new Rect(tr.x + 12 * u, tr.y + 6 * u, tr.width, lh), $"<b>{Loc.T("tasks")}</b>  ({tasks.CompletedCount}/{GameConsts.TasksRequired} {Loc.T("tasksNeed")})   [Tab]", small);
                if (tasksExpanded)
                    for (int i = 0; i < tasks.Tasks.Count; i++)
                    {
                        var t = tasks.Tasks[i];
                        bool done = tasks.IsDone(i);
                        string stars = new string('*', t.Tier);
                        string prog2 = t.Goal > 1 ? $" ({tasks.Progress[i]}/{t.Goal})" : "";
                        GUI.color = done ? new Color(0.55f, 1f, 0.55f) : Color.white;
                        GUI.Label(new Rect(tr.x + 12 * u, tr.y + 10 * u + (i + 1) * lh, tr.width - 20 * u, lh), (done ? "✓ " : "○ ") + t.Text(Loc.Current) + prog2 + "  " + stars, small);
                    }
                GUI.color = Color.white;
            }

            // chaos + loot
            int chaos = s.IsHost ? (s.Host.Night?.Chaos ?? 0) : ChaosFromTasks();
            int loot = s.IsHost ? (s.Host.Night?.HaulValue ?? 0) : 0;
            var lr = new Rect(sw - 314 * u, 14 * u, 300 * u, 124 * u);
            GUI.Box(lr, GUIContent.none, dark);
            GUI.Label(new Rect(lr.x + 12 * u, lr.y + 8 * u, lr.width, 30 * u), $"{Loc.T("chaos")}: <b>{chaos}</b>", label);
            if (s.IsHost) GUI.Label(new Rect(lr.x + 12 * u, lr.y + 42 * u, lr.width, 30 * u), $"{Loc.T("haul")}: <b>{loot}</b>", label);
            if (s.IsSolo) GUI.Label(new Rect(lr.x + 12 * u, lr.y + 76 * u, lr.width, 30 * u), $"{Loc.T("spareHats")}: <b>{s.SpareHats}</b>", label);

            // pockets
            var slot = s.LocalSlot;
            if (slot != null)
            {
                var pr = new Rect(14 * u, sh - 66 * u, 620 * u, 52 * u);
                GUI.Box(pr, GUIContent.none, dark);
                string items = slot.Pocket.Count == 0 ? "—" : string.Join(", ", slot.Pocket.Select(k => ItemDefs.TryGet(k, out var d) ? d.Name(Loc.Current) : k));
                GUI.Label(new Rect(pr.x + 12 * u, pr.y + 10 * u, pr.width - 20 * u, 36 * u), $"{Loc.T("pocket")} ({slot.Pocket.Count}/{GearEffects.From(s.Save).PocketSize}): {items}", small);
            }

            // stealth: how loud am I, am I lit?
            var om = w.OldMan;
            if (om != null && g != null && g.Status == PlayerStatus.Free)
            {
                float mul = GearEffects.From(s.Save).FootstepNoiseMul;
                bool moving = g.Velocity.Flat().sqrMagnitude > 0.5f && g.Grounded;
                string step;
                Color sc;
                if (!moving || g.Crouching) { step = Loc.T(moving ? "stepQuiet" : "stepSilent"); sc = new Color(0.6f, 1f, 0.6f); }
                else if (g.Sprinting && mul > 0.5f) { step = Loc.T("stepLoud"); sc = new Color(1f, 0.4f, 0.35f); }
                else if (mul <= 0.5f) { step = Loc.T("stepQuiet"); sc = new Color(0.6f, 1f, 0.6f); }
                else { step = Loc.T("stepNormal"); sc = new Color(1f, 0.9f, 0.5f); }
                bool lit = w.IsLit(g.Center) || om.InTorch(g.Center);
                var nr = new Rect(sw - 434 * u, sh - 108 * u, 420 * u, 36 * u);
                GUI.Box(nr, GUIContent.none, dark);
                GUI.color = sc;
                GUI.Label(new Rect(nr.x + 12 * u, nr.y + 5 * u, 200 * u, 28 * u), Loc.T("steps") + ": " + step, small);
                GUI.color = lit ? new Color(1f, 0.85f, 0.4f) : new Color(0.6f, 0.7f, 1f);
                GUI.Label(new Rect(nr.x + 220 * u, nr.y + 5 * u, 190 * u, 28 * u), Loc.T(lit ? "inLight" : "inShadow"), small);
                GUI.color = Color.white;
            }

            // grandpa state
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
                var or = new Rect(sw - 434 * u, sh - 66 * u, 420 * u, 52 * u);
                GUI.Box(or, GUIContent.none, dark);
                GUI.color = c;
                GUI.Label(new Rect(or.x + 12 * u, or.y + 10 * u, or.width, 36 * u), txt, label);
                GUI.color = Color.white;
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

        void VillageHud(GameSession s, LocalGnome g, float sw, float sh)
        {
            var save = s.Save;
            if (save == null) return;
            var r = new Rect(14 * u, 14 * u, 470 * u, 250 * u);
            GUI.Box(r, GUIContent.none, dark);
            GUI.Label(new Rect(r.x + 14 * u, r.y + 8 * u, r.width, 36 * u), $"<b>{Loc.T("hub")}</b>", label);
            GUI.Label(new Rect(r.x + 14 * u, r.y + 42 * u, r.width, 30 * u), $"{Loc.T("day")}: {save.Night}    {Loc.T("strikes")}: {save.Strikes}/{GameConsts.MaxStrikes}", small);
            for (int i = 0; i < 6; i++)
                GUI.Label(new Rect(r.x + 14 * u + (i % 2) * 220 * u, r.y + 80 * u + (i / 2) * 30 * u, 220 * u, 30 * u), $"{Loc.MatName((Mat)i)}: <b>{save.Materials[i]}</b>", small);
            GUI.Label(new Rect(r.x + 14 * u, r.y + 176 * u, r.width - 20 * u, 60 * u), Loc.T("portalHint"), small);
            if (!s.IsSolo)
            {
                int ready = s.Players.Values.Count(p => p.PortalReady);
                GUI.Label(new Rect(sw / 2 - 300 * u, sh - 60 * u, 600 * u, 40 * u), $"{Loc.T("waitingPlayers")}: {ready}/{s.Players.Count}", center);
            }
        }

        // ------------------------------------------------------------------ overlays

        void Craft()
        {
            var s = S;
            if (s == null) return;
            var save = s.Save;
            float w = 900 * u, h = UnityEngine.Screen.height * 0.8f;
            GUILayout.BeginArea(new Rect((UnityEngine.Screen.width - w) / 2, (UnityEngine.Screen.height - h) / 2, w, h), panel);
            GUILayout.Label("<b>" + Loc.T("craftBench") + "</b>", big);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 6; i++) GUILayout.Label($"{Loc.MatName((Mat)i)}: <b>{save.Materials[i]}</b>", small, GUILayout.Width(140 * u));
            GUILayout.EndHorizontal();
            if (save.Potions > 0) GUILayout.Label(GearCatalog.Get(GearId.SleepDust).Name(Loc.Current) + ": " + save.Potions, small);
            craftScroll = GUILayout.BeginScrollView(craftScroll);
            foreach (var gd in GearCatalog.All)
            {
                GUILayout.BeginHorizontal(dark);
                GUILayout.BeginVertical(GUILayout.Width(560 * u));
                GUILayout.Label("<b>" + gd.Name(Loc.Current) + "</b> — " + gd.Desc(Loc.Current), small);
                var cost = new List<string>();
                for (int i = 0; i < 6; i++) if (gd.Cost[i] > 0) cost.Add($"{Loc.MatName((Mat)i)} {gd.Cost[i]}");
                GUILayout.Label(string.Join(", ", cost), small);
                GUILayout.EndVertical();
                bool can = save.CanCraft(gd, out var reason);
                if (reason == "owned") GUILayout.Label(Loc.T("owned"), label, GUILayout.Width(200 * u));
                else
                {
                    GUI.enabled = can;
                    if (GUILayout.Button(Loc.T("craft"), button, GUILayout.Width(200 * u), GUILayout.Height(50 * u)))
                        s.SendAction(new ActionMsg { Type = ActionType.Craft, I = (int)gd.Id });
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(4 * u);
            }
            GUILayout.EndScrollView();
            if (!s.IsHost) GUILayout.Label(Loc.T("craftHostOnly"), small);
            if (Btn(Loc.T("back"), 240)) App.CraftOpen = false;
            GUILayout.EndArea();
        }

        void SockDialog()
        {
            var s = S;
            float w = 900 * u;
            GUILayout.BeginArea(new Rect((UnityEngine.Screen.width - w) / 2, UnityEngine.Screen.height * 0.5f, w, UnityEngine.Screen.height * 0.45f), panel);
            GUILayout.Label("<b>" + Loc.T("greatSock") + "</b>", big);
            GUILayout.Label(Loc.T("sockSays"), label);
            if (s != null && s.Save != null)
            {
                GUILayout.Space(6 * u);
                GUILayout.Label($"{Loc.T("day")} {s.Save.Night}.  {Loc.T("strikes")}: {s.Save.Strikes}/{GameConsts.MaxStrikes}", small);
                GUILayout.Label(Loc.T("sockTip" + (s.Save.Night % 6)), small);
            }
            GUILayout.FlexibleSpace();
            if (Btn(Loc.T("ok"), 200)) App.SockOpen = false;
            GUILayout.EndArea();
        }

        void ReportScreen()
        {
            var r = App.Report;
            if (r == null) return;
            GUI.color = new Color(0.02f, 0.02f, 0.05f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), white);
            GUI.color = Color.white;
            float w = 1000 * u, h = UnityEngine.Screen.height * 0.86f;
            GUILayout.BeginArea(new Rect((UnityEngine.Screen.width - w) / 2, (UnityEngine.Screen.height - h) / 2, w, h), panel);
            GUILayout.Label("<b>" + Loc.T("report") + "</b>", big);
            int line = r.Chaos >= 18 ? 3 : r.Chaos >= 10 ? 2 : r.Chaos >= 5 ? 1 : 0;
            GUILayout.Label(Loc.T("grandpaMorning" + line), label);
            GUILayout.Space(8 * u);
            for (int i = 0; i < r.TaskIds.Length; i++)
            {
                var t = TaskCatalog.Get(r.TaskIds[i]);
                if (t == null) continue;
                bool done = i < r.TaskDone.Length && r.TaskDone[i];
                GUI.color = done ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.6f, 0.55f);
                GUILayout.Label((done ? "✓ " : "✗ ") + t.Text(Loc.Current), small);
            }
            GUI.color = Color.white;
            GUILayout.Space(8 * u);
            GUILayout.Label($"{Loc.T("chaos")}: <b>{r.Chaos}</b> {Loc.T("chaosReport")}", label);
            var mats = new List<string>();
            for (int i = 0; i < 5; i++) if (r.Haul[i] > 0) mats.Add($"{Loc.MatName((Mat)i)} +{r.Haul[i]}");
            GUILayout.Label($"{Loc.T("materials")}: {(mats.Count > 0 ? string.Join(", ", mats) : "—")}", small);
            GUILayout.Label($"{Loc.MatName(Mat.Giggles)}: +{r.Giggles}", small);
            GUILayout.Space(10 * u);
            GUI.color = r.Passed ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.5f, 0.45f);
            GUILayout.Label("<b>" + (r.Fired ? Loc.T("fired") : r.Passed ? Loc.T("verdictGood") : Loc.T("verdictBad")) + "</b>", big);
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            if (S != null && S.IsHost)
            {
                if (Btn(Loc.T("continue"), 320)) App.ContinueFromReport();
            }
            else GUILayout.Label(Loc.T("waitHost"), small);
            GUILayout.EndArea();
        }
    }
}
