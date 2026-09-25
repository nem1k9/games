using Gnomes.App;
using Gnomes.Core.Protocol;
using Gnomes.NPC;
using Gnomes.Players;
using Gnomes.Session;
using Gnomes.World;
using UnityEngine;

namespace Gnomes.UI
{
    /// <summary>
    /// Playtesting helpers. F3 toggles the overlay (FPS, ping, NPC states). Host / solo only:
    /// F6 start the night, F7 end the night, F8 wake grandpa, F9 put him back to sleep.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        bool show;
        float fps = 60f;
        GUIStyle style;

        void Update()
        {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(1e-4f, Time.unscaledDeltaTime), 0.05f);
            if (Input.GetKeyDown(KeyCode.F3)) show = !show;
            var s = GameSession.I;
            if (s == null || !s.IsHost || GameApp.I == null || GameApp.I.Screen != AppScreen.Playing) return;
            var w = GameWorld.Current;
            if (Input.GetKeyDown(KeyCode.F6) && w != null && w.Kind == LevelKind.Hub) s.Host.StartNight();
            if (Input.GetKeyDown(KeyCode.F7) && w != null && w.Kind == LevelKind.House) s.Host.DebugEndNight();
            if (Input.GetKeyDown(KeyCode.F8)) w?.OldMan?.DebugWake();
            if (Input.GetKeyDown(KeyCode.F9)) w?.OldMan?.DebugSleep();
        }

        void OnGUI()
        {
            if (!show) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, Screen.height / 60), richText = true };
                style.normal.textColor = new Color(0.8f, 1f, 0.8f);
            }
            var s = GameSession.I;
            var w = GameWorld.Current;
            var sb = new System.Text.StringBuilder();
            sb.Append($"<b>FPS</b> {fps:0}");
            if (s != null)
            {
                sb.Append(s.IsSolo ? "   solo" : s.IsHost ? $"   host, {s.Players.Count} players" : $"   client #{s.LocalId}");
                if (!s.IsHost && s.Net != null) sb.Append($"   ping {s.Net.PingMs(0)} ms");
            }
            if (w != null)
            {
                sb.Append($"\n{w.Kind}  props {w.Props.Count}  furniture {w.Furniture.Count}  mechs {w.Mechs.Count}");
                if (w.Night != null) sb.Append($"  time left {w.Night.TimeLeft:0}s  chaos {w.Night.Chaos}");
                if (w.OldMan != null) sb.Append($"\ngrandpa: {w.OldMan.Mode}  alert {w.OldMan.Alert:0.00}");
                if (w.Cat != null) sb.Append($"   cat: {w.Cat.Mode}");
                if (w.Parrot != null) sb.Append($"   parrot: {(Parrot.St)w.Parrot.State}");
                var lg = LocalGnome.I;
                if (lg != null) sb.Append($"\nme: {lg.Status} {lg.transform.position.x:0.0},{lg.transform.position.y:0.0},{lg.transform.position.z:0.0}  arms {lg.Mode}  grounded {lg.Grounded}");
            }
            if (s != null && s.IsHost) sb.Append("\n<i>F6 night · F7 end night · F8 wake grandpa · F9 sleep</i>");
            var r = new Rect(10, 10, Screen.width * 0.6f, Screen.height * 0.3f);
            var c = style.normal.textColor;
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), sb.ToString(), style);
            style.normal.textColor = c;
            GUI.Label(r, sb.ToString(), style);
        }
    }
}
