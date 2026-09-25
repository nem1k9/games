using Gnomes.Core.Protocol;
using Godot;
using SockGang.App;
using SockGang.Players;
using SockGang.Session;
using SockGang.World;

namespace SockGang.UI
{
    /// <summary>
    /// Playtesting helpers. F3 toggles the overlay (FPS, ping, NPC states). Host / solo only:
    /// F6 start the night, F7 end the night, F8 wake grandpa, F9 put him back to sleep.
    /// </summary>
    public partial class DebugOverlay : CanvasLayer
    {
        Label label;

        public override void _Ready()
        {
            Layer = 20;
            label = new Label { Position = new Vector2(10, 10), Visible = false };
            label.AddThemeFontSizeOverride("font_size", 16);
            label.AddThemeColorOverride("font_color", new Color(0.8f, 1f, 0.8f));
            label.AddThemeColorOverride("font_shadow_color", Colors.Black);
            AddChild(label);
        }

        public override void _Process(double delta)
        {
            if (Keys.Pressed(Key.F3)) label.Visible = !label.Visible;
            var s = GameSession.I;
            var app = GameApp.I;
            if (s != null && s.IsHost && app != null && app.Screen == AppScreen.Playing)
            {
                var w = GameWorld.Current;
                if (Keys.Pressed(Key.F6) && w != null && w.Kind == LevelKind.Hub) s.Host.StartNight();
                if (Keys.Pressed(Key.F7) && w != null && w.Kind == LevelKind.House) s.Host.DebugEndNight();
                if (Keys.Pressed(Key.F8)) w?.OldMan?.DebugWake();
                if (Keys.Pressed(Key.F9)) w?.OldMan?.DebugSleep();
            }
            if (!label.Visible) return;
            var sb = new System.Text.StringBuilder();
            sb.Append($"FPS {Engine.GetFramesPerSecond():0}");
            if (s != null)
            {
                sb.Append(s.IsSolo ? "   solo" : s.IsHost ? $"   host, {s.Players.Count} players" : $"   client #{s.LocalId}");
                if (!s.IsHost && s.Net != null) sb.Append($"   ping {s.Net.PingMs(0)} ms");
            }
            var world = GameWorld.Current;
            if (world != null)
            {
                sb.Append($"\n{world.Kind}  props {world.Props.Count}  furniture {world.Furniture.Count}  mechs {world.Mechs.Count}");
                if (world.Night != null) sb.Append($"  time left {world.Night.TimeLeft:0}s  chaos {world.Night.Chaos}");
                if (world.OldMan != null) sb.Append($"\ngrandpa: {world.OldMan.Mode}  alert {world.OldMan.Alert:0.00}");
                if (world.Cat != null) sb.Append($"   cat: {world.Cat.Mode}");
                var lg = LocalGnome.I;
                if (lg != null) sb.Append($"\nme: {lg.Status} {lg.GlobalPosition.X:0.0},{lg.GlobalPosition.Y:0.0},{lg.GlobalPosition.Z:0.0}  arms {lg.Mode}  grounded {lg.Grounded}");
            }
            if (s != null && s.IsHost) sb.Append("\nF6 night · F7 end night · F8 wake grandpa · F9 sleep");
            label.Text = sb.ToString();
        }
    }
}
