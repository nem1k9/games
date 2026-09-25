using System.Collections.Generic;
using Godot;
using SockGang.Rendering;

namespace SockGang.Dev
{
    /// <summary>
    /// Development helper: renders scenes to PNG from the command line, e.g.
    /// godot --path godot -- --shot-model=gnome --out=/tmp/gnome.png
    /// </summary>
    public partial class ShotHarness : Node3D
    {
        public Dictionary<string, string> Args;
        int frame;
        Camera3D cam;

        public override void _Ready()
        {
            if (Args.TryGetValue("font-sheet", out var fontDir))
            {
                var bg = new ColorRect { Color = new Color(0.16f, 0.12f, 0.18f), Size = new Vector2(1600, 1200) };
                var layer = new CanvasLayer();
                AddChild(layer);
                layer.AddChild(bg);
                var box = new VBoxContainer { Position = new Vector2(20, 10) };
                layer.AddChild(box);
                foreach (var f in System.IO.Directory.GetFiles(fontDir, "*.ttf"))
                {
                    var ff = new FontFile();
                    ff.Data = System.IO.File.ReadAllBytes(f);
                    var l = new Label { Text = System.IO.Path.GetFileNameWithoutExtension(f) + ":  НОСОЧНАЯ БАНДА — Играть одному! Ёжик, 123", };
                    l.AddThemeFontOverride("font", ff);
                    l.AddThemeFontSizeOverride("font_size", 40);
                    l.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.7f));
                    box.AddChild(l);
                }
                return;
            }
            if (Args.TryGetValue("dump-tex", out var texNames))
            {
                foreach (var tn in texNames.Split(','))
                    Rendering.Surfaces.Texture(tn).GetImage().SavePng((Args.TryGetValue("out", out var od) ? od : "/tmp") + "/tex_" + tn + ".png");
                GetTree().Quit();
                return;
            }
            if (Args.TryGetValue("shot-level", out var level))
            {
                World.WorldLoader.Parent = this;
                var w = World.WorldLoader.Build(level == "house" ? Gnomes.Core.Protocol.LevelKind.House : Gnomes.Core.Protocol.LevelKind.Hub, 12345, true, null);
                cam = new Camera3D { Fov = 70, Far = 500 };
                AddChild(cam);
                var eye = Args.TryGetValue("eye", out var e) ? Parse(e) : new Vector3(6, 2.2f, 6);
                var at = Args.TryGetValue("at", out var a) ? Parse(a) : Vector3.Zero;
                cam.LookAtFromPosition(eye, at, Vector3.Up);
                cam.Current = true;
                GD.Print("world children: ", w.StaticRoot.GetChildCount(), " env: ", GetViewport().World3D.Environment != null);
                return;
            }
            var env = new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.9f, 0.86f, 0.78f), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.5f, 0.5f, 0.55f), AmbientLightEnergy = 1f } };
            AddChild(env);
            var sun = new DirectionalLight3D { ShadowEnabled = !Args.ContainsKey("noshadow"), LightEnergy = 1.3f, ShadowBias = 0.05f, ShadowNormalBias = 1.5f };
            AddChild(sun);
            sun.RotationDegrees = new Vector3(-45, -35, 0);
            cam = new Camera3D { Fov = 40 };
            AddChild(cam);
            if (Args.TryGetValue("shot-model", out var name))
            {
                var inst = ModelLibrary.Instantiate(name, this);
                if (Args.TryGetValue("cull", out var cullMode))
                {
                    var dbg = new StandardMaterial3D { AlbedoTexture = ModelLibrary.PaletteTexture, TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                        CullMode = cullMode == "front" ? BaseMaterial3D.CullModeEnum.Front : cullMode == "none" ? BaseMaterial3D.CullModeEnum.Disabled : BaseMaterial3D.CullModeEnum.Back };
                    foreach (var (mi, _) in inst.Meshes) mi.MaterialOverride = dbg;
                }
                var m = inst.Model;
                m.Bounds(out var mn, out var mx);
                var c = (mn.G() + mx.G()) * 0.5f;
                float size = (mx.G() - mn.G()).Length();
                // look at the model's front (model front = -Z in Godot) from the front-left
                float yaw = Args.TryGetValue("yaw", out var ys) ? float.Parse(ys, System.Globalization.CultureInfo.InvariantCulture) : 31f;
                float pitch = Args.TryGetValue("pitch", out var ps) ? float.Parse(ps, System.Globalization.CultureInfo.InvariantCulture) : 17f;
                var dir = new Vector3(-Mathf.Sin(Mathf.DegToRad(yaw)) * Mathf.Cos(Mathf.DegToRad(pitch)), Mathf.Sin(Mathf.DegToRad(pitch)), -Mathf.Cos(Mathf.DegToRad(yaw)) * Mathf.Cos(Mathf.DegToRad(pitch)));
                var eye = c + dir * size * 1.6f;
                cam.LookAtFromPosition(eye, c, Vector3.Up);
                var ground = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(size * 8, size * 8) }, Position = new Vector3(c.X, mn.y, c.Z) };
                ground.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.75f, 0.68f, 0.55f) };
                AddChild(ground);
            }
        }

        static Vector3 Parse(string s)
        {
            var p = s.Split(',');
            return new Vector3(float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture));
        }

        public override void _Process(double delta)
        {
            if (++frame < 8) return;
            var img = GetViewport().GetTexture().GetImage();
            var outPath = Args.TryGetValue("out", out var o) ? o : "/tmp/shot.png";
            img.SavePng(outPath);
            GD.Print("saved ", outPath);
            GetTree().Quit();
        }
    }
}
