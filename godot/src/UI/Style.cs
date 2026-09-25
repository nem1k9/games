using System;
using System.Collections.Generic;
using Godot;

namespace SockGang.UI
{
    /// <summary>
    /// The handmade look of the interface: felt patches with stitched borders, knitted letters, yarn and
    /// buttons. Every texture is painted in code (no image assets), the fonts ship as plain files.
    /// </summary>
    public static class Style
    {
        // ------------------------------------------------------------------ palette
        public static readonly Color Cream = new Color(0.98f, 0.94f, 0.85f);
        public static readonly Color Ink = new Color(0.22f, 0.14f, 0.11f);
        public static readonly Color Gold = new Color(1f, 0.84f, 0.38f);
        public static readonly Color Felt = new Color(0.25f, 0.17f, 0.26f); // aubergine felt
        public static readonly Color FeltDark = new Color(0.16f, 0.11f, 0.17f);
        public static readonly Color Thread = new Color(0.95f, 0.86f, 0.66f);
        public static readonly Color Red = new Color(0.8f, 0.28f, 0.22f);
        public static readonly Color Mustard = new Color(0.86f, 0.6f, 0.2f);
        public static readonly Color Teal = new Color(0.17f, 0.5f, 0.53f);
        public static readonly Color Moss = new Color(0.36f, 0.53f, 0.26f);
        public static readonly Color Plum = new Color(0.47f, 0.28f, 0.52f);
        public static readonly Color Paper = new Color(0.97f, 0.92f, 0.8f);
        public static readonly Color Good = new Color(0.58f, 0.92f, 0.52f);
        public static readonly Color Bad = new Color(1f, 0.55f, 0.47f);
        public static readonly Color Night = new Color(0.62f, 0.75f, 1f);

        // ------------------------------------------------------------------ fonts
        static FontFile regular, bold, script, logo;

        static FontFile LoadFont(string name)
        {
            var f = new FontFile();
            var path = "res://fonts/" + name + ".sgfont";
            if (FileAccess.FileExists(path)) f.Data = FileAccess.GetFileAsBytes(path);
            else GD.PushWarning("[Style] missing font " + path);
            return f;
        }

        public static Font Regular => regular ??= LoadFont("BalsamiqSans-Regular");
        public static Font Bold => bold ??= LoadFont("BalsamiqSans-Bold");
        /// <summary>Curly headings.</summary>
        public static Font Script => script ??= LoadFont("Lobster-Regular");
        /// <summary>Chunky letters for the knitted logo.</summary>
        public static Font Logo => logo ??= LoadFont("RubikMonoOne-Regular");

        // ------------------------------------------------------------------ procedural textures
        const int S = 96, M = 24; // texture size and nine-patch margin
        static readonly Dictionary<string, ImageTexture> cache = new Dictionary<string, ImageTexture>();

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
            }
        }

        /// <summary>Value noise with period 8 cells of 6 px = 48 px: the tiled middle of the patch repeats seamlessly.</summary>
        static float Fuzz(int x, int y, int seed)
        {
            float fx = x / 6f, fy = y / 6f;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            int X0 = ((x0 % 8) + 8) % 8, Y0 = ((y0 % 8) + 8) % 8, X1 = (X0 + 1) % 8, Y1 = (Y0 + 1) % 8;
            float a = Hash(X0, Y0, seed), b = Hash(X1, Y0, seed), c = Hash(X0, Y1, seed), d = Hash(X1, Y1, seed);
            return a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty;
        }

        /// <summary>Signed distance to a rounded rectangle [inset, S - inset] with corner radius r (negative inside).</summary>
        static float RoundRect(float x, float y, float inset, float r)
        {
            float cx = S / 2f, cy = S / 2f, hw = S / 2f - inset - r, hh = S / 2f - inset - r;
            float qx = Math.Abs(x - cx) - hw, qy = Math.Abs(y - cy) - hh;
            float ox = Math.Max(qx, 0), oy = Math.Max(qy, 0);
            return (float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0) - r;
        }

        /// <summary>Dashes along the stitch line: tiles with the nine-patch edges (period 12 px from the margin).</summary>
        static bool Dash(int x, int y)
        {
            bool cornerX = x < M || x >= S - M, cornerY = y < M || y >= S - M;
            if (cornerX && cornerY)
            {
                float cx = x < M ? M : S - M, cy = y < M ? M : S - M;
                float ang = Mathf.RadToDeg(Mathf.Atan2(y + 0.5f - cy, x + 0.5f - cx));
                return ((int)Math.Floor((ang + 360f) / 30f)) % 2 == 0;
            }
            int t = cornerY ? x - M : y - M;
            return ((t % 12) + 12) % 12 < 7;
        }

        /// <summary>
        /// A felt patch: rounded, fuzzy, with a dashed seam of thread around it. Used by panels and buttons
        /// as a nine-patch (margins 24 px, edges and middle tiled).
        /// </summary>
        public static ImageTexture FeltTexture(Color bg, Color thread, int seed = 1, bool stitches = true)
        {
            string key = $"felt{bg.ToHtml()}{thread.ToHtml()}{seed}{stitches}";
            if (cache.TryGetValue(key, out var t)) return t;
            var img = Image.CreateEmpty(S, S, false, Image.Format.Rgba8);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = RoundRect(x + 0.5f, y + 0.5f, 2f, 18f);
                    float alpha = Mathf.Clamp(0.5f - d, 0f, 1f) * bg.A;
                    if (alpha <= 0f)
                    {
                        img.SetPixel(x, y, new Color(0, 0, 0, 0));
                        continue;
                    }
                    float n = Fuzz(x, y, seed) * 0.7f + Hash(x, y, seed + 9) * 0.3f;
                    var c = bg * (0.9f + n * 0.16f);
                    // a soft rim: felt edges look a bit darker
                    c = c.Lerp(bg.Darkened(0.35f), Mathf.Clamp((d + 5f) / 5f, 0f, 1f) * 0.6f);
                    if (stitches)
                    {
                        float sd = Math.Abs(RoundRect(x + 0.5f, y + 0.5f, 10f, 10f));
                        if (sd < 1.6f && Dash(x, y))
                        {
                            float k = Mathf.Clamp(1.6f - sd, 0f, 1f);
                            c = c.Lerp(thread * (0.92f + Hash(x, y, 3) * 0.12f), k);
                        }
                        else if (sd < 2.6f && Dash(x, y - 1))
                            c = c.Darkened(0.12f); // the thread's little shadow
                    }
                    c.A = alpha;
                    img.SetPixel(x, y, c);
                }
            t = ImageTexture.CreateFromImage(img);
            cache[key] = t;
            return t;
        }

        /// <summary>A sheet of paper for notes (the prank list, the morning report).</summary>
        public static ImageTexture PaperTexture()
        {
            if (cache.TryGetValue("paper", out var t)) return t;
            var img = Image.CreateEmpty(S, S, false, Image.Format.Rgba8);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = RoundRect(x + 0.5f, y + 0.5f, 3f, 6f);
                    float alpha = Mathf.Clamp(0.5f - d, 0f, 1f);
                    float n = Fuzz(x, y, 21) * 0.6f + Hash(x, y, 22) * 0.4f;
                    var c = Paper * (0.94f + n * 0.08f);
                    // ruled lines every 24 px in the middle, like a notebook page
                    if (y >= M && y < S - M && (y - M) % 24 == 20) c = c.Lerp(new Color(0.55f, 0.7f, 0.9f), 0.35f);
                    if (d > -2.5f) c = c.Lerp(new Color(0.55f, 0.42f, 0.3f), 0.6f); // pencil border
                    c.A = alpha;
                    img.SetPixel(x, y, c);
                }
            t = ImageTexture.CreateFromImage(img);
            cache["paper"] = t;
            return t;
        }

        public static StyleBoxTexture FeltBox(Color bg, Color? thread = null, float padX = 26, float padY = 20, int seed = 1)
        {
            var sb = new StyleBoxTexture { Texture = FeltTexture(bg, thread ?? Thread, seed) };
            sb.TextureMarginLeft = sb.TextureMarginRight = sb.TextureMarginTop = sb.TextureMarginBottom = M;
            sb.AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.TileFit;
            sb.AxisStretchVertical = StyleBoxTexture.AxisStretchMode.TileFit;
            sb.ContentMarginLeft = sb.ContentMarginRight = padX;
            sb.ContentMarginTop = sb.ContentMarginBottom = padY;
            return sb;
        }

        public static StyleBoxTexture PaperBox(float padX = 30, float padY = 24)
        {
            var sb = new StyleBoxTexture { Texture = PaperTexture() };
            sb.TextureMarginLeft = sb.TextureMarginRight = sb.TextureMarginTop = sb.TextureMarginBottom = M;
            sb.AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.TileFit;
            sb.AxisStretchVertical = StyleBoxTexture.AxisStretchMode.TileFit;
            sb.ContentMarginLeft = sb.ContentMarginRight = padX;
            sb.ContentMarginTop = sb.ContentMarginBottom = padY;
            return sb;
        }

        static ImageTexture Icon(string key, int size, Func<float, float, Color> paint)
        {
            if (cache.TryGetValue(key, out var t)) return t;
            var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    img.SetPixel(x, y, paint(x + 0.5f, y + 0.5f));
            t = ImageTexture.CreateFromImage(img);
            cache[key] = t;
            return t;
        }

        /// <summary>Check box: a little stitched square, ticked with a thick thread.</summary>
        static ImageTexture CheckIcon(bool on) => Icon("check" + on, 30, (x, y) =>
        {
            float d = Math.Max(Math.Abs(x - 15f), Math.Abs(y - 15f));
            var c = new Color(0, 0, 0, 0);
            if (d < 12f) c = FeltDark;
            if (d > 9.5f && d < 12f) c = Thread;
            if (on)
            {
                // two strokes of a tick
                float a = SegDist(x, y, 8, 15, 13, 21), b = SegDist(x, y, 13, 21, 23, 8);
                if (Math.Min(a, b) < 2.4f) c = Gold;
            }
            return c;
        });

        static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy), 0f, 1f);
            float dx = px - (ax + vx * t), dy = py - (ay + vy * t);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>A sewing button with four holes: the slider grabber.</summary>
        static ImageTexture ButtonIcon(bool hot) => Icon("sbtn" + hot, 30, (x, y) =>
        {
            float r = (float)Math.Sqrt((x - 15) * (x - 15) + (y - 15) * (y - 15));
            var col = hot ? Gold : Mustard;
            if (r > 13f) return new Color(0, 0, 0, 0);
            var c = r > 10.5f ? col.Darkened(0.25f) : col;
            foreach (var (hx, hy) in new[] { (11f, 11f), (19f, 11f), (11f, 19f), (19f, 19f) })
                if ((x - hx) * (x - hx) + (y - hy) * (y - hy) < 4.2f) c = FeltDark;
            c.A = Mathf.Clamp(13.5f - r, 0f, 1f);
            return c;
        });

        // ------------------------------------------------------------------ theme

        public static Theme BuildTheme()
        {
            var t = new Theme();
            t.DefaultFont = Regular;
            t.DefaultFontSize = 22;
            t.SetStylebox("panel", "PanelContainer", FeltBox(new Color(Felt, 0.95f)));
            t.SetStylebox("panel", "Panel", FeltBox(new Color(Felt, 0.95f)));

            t.SetStylebox("normal", "Button", FeltBox(Red, null, 24, 12, 2));
            t.SetStylebox("hover", "Button", FeltBox(Red.Lightened(0.12f), Gold, 24, 12, 2));
            t.SetStylebox("pressed", "Button", FeltBox(Red.Darkened(0.2f), null, 24, 12, 2));
            t.SetStylebox("disabled", "Button", FeltBox(new Color(0.4f, 0.35f, 0.36f), new Color(0.7f, 0.66f, 0.6f), 24, 12, 2));
            t.SetStylebox("focus", "Button", new StyleBoxEmpty());
            t.SetFont("font", "Button", Bold);
            t.SetFontSize("font_size", "Button", 26);
            t.SetColor("font_color", "Button", Cream);
            t.SetColor("font_hover_color", "Button", Colors.White);
            t.SetColor("font_pressed_color", "Button", Cream);
            t.SetColor("font_disabled_color", "Button", new Color(0.85f, 0.8f, 0.75f, 0.7f));
            t.SetColor("font_outline_color", "Button", new Color(0.25f, 0.08f, 0.05f, 0.8f));
            t.SetConstant("outline_size", "Button", 4);

            t.SetColor("font_color", "Label", Cream);
            t.SetColor("font_shadow_color", "Label", new Color(0.08f, 0.04f, 0.06f, 0.65f));
            t.SetConstant("shadow_offset_x", "Label", 2);
            t.SetConstant("shadow_offset_y", "Label", 2);

            var edit = FeltBox(FeltDark, new Color(0.7f, 0.62f, 0.5f), 20, 10, 5);
            t.SetStylebox("normal", "LineEdit", edit);
            t.SetStylebox("focus", "LineEdit", FeltBox(FeltDark.Lightened(0.08f), Gold, 20, 10, 5));
            t.SetColor("font_color", "LineEdit", Colors.White);
            t.SetColor("caret_color", "LineEdit", Gold);
            t.SetColor("font_placeholder_color", "LineEdit", new Color(1, 1, 1, 0.4f));
            t.SetFont("font", "LineEdit", Bold);
            t.SetFontSize("font_size", "LineEdit", 24);

            var track = new StyleBoxFlat { BgColor = new Color(0.05f, 0.03f, 0.05f, 0.55f) };
            track.SetCornerRadiusAll(6);
            t.SetStylebox("background", "ProgressBar", track);
            var fill = new StyleBoxFlat { BgColor = Gold };
            fill.SetCornerRadiusAll(6);
            t.SetStylebox("fill", "ProgressBar", fill);

            var rail = new StyleBoxFlat { BgColor = new Color(0.1f, 0.06f, 0.1f, 0.8f), ContentMarginTop = 3, ContentMarginBottom = 3 };
            rail.SetCornerRadiusAll(4);
            t.SetStylebox("slider", "HSlider", rail);
            var railFill = new StyleBoxFlat { BgColor = Mustard, ContentMarginTop = 3, ContentMarginBottom = 3 };
            railFill.SetCornerRadiusAll(4);
            t.SetStylebox("grabber_area", "HSlider", railFill);
            t.SetStylebox("grabber_area_highlight", "HSlider", railFill);
            t.SetIcon("grabber", "HSlider", ButtonIcon(false));
            t.SetIcon("grabber_highlight", "HSlider", ButtonIcon(true));

            t.SetIcon("checked", "CheckBox", CheckIcon(true));
            t.SetIcon("unchecked", "CheckBox", CheckIcon(false));
            t.SetColor("font_color", "CheckBox", Cream);
            t.SetColor("font_hover_color", "CheckBox", Colors.White);
            t.SetColor("font_pressed_color", "CheckBox", Cream);
            t.SetColor("font_hover_pressed_color", "CheckBox", Colors.White);
            foreach (var st in new[] { "normal", "hover", "pressed", "hover_pressed", "focus" }) t.SetStylebox(st, "CheckBox", new StyleBoxEmpty());
            t.SetConstant("h_separation", "CheckBox", 12);

            var scroll = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.25f) };
            scroll.SetCornerRadiusAll(4);
            t.SetStylebox("scroll", "VScrollBar", scroll);
            var grab = new StyleBoxFlat { BgColor = Mustard };
            grab.SetCornerRadiusAll(4);
            t.SetStylebox("grabber", "VScrollBar", grab);
            t.SetStylebox("grabber_highlight", "VScrollBar", grab);
            t.SetStylebox("grabber_pressed", "VScrollBar", grab);

            t.SetConstant("separation", "VBoxContainer", 10);
            t.SetConstant("separation", "HBoxContainer", 10);
            return t;
        }

        /// <summary>Buttons in another felt colour (the menu uses a different colour per action).</summary>
        public static void Colorize(Button b, Color c)
        {
            b.AddThemeStyleboxOverride("normal", FeltBox(c, null, 24, 12, 2));
            b.AddThemeStyleboxOverride("hover", FeltBox(c.Lightened(0.14f), Gold, 24, 12, 2));
            b.AddThemeStyleboxOverride("pressed", FeltBox(c.Darkened(0.2f), null, 24, 12, 2));
        }

        // ------------------------------------------------------------------ knitted letters

        const string KnitShader = @"
shader_type canvas_item;
uniform sampler2D knit : repeat_enable, filter_linear_mipmap;
uniform vec4 c0 : source_color = vec4(0.85, 0.2, 0.17, 1.0);
uniform vec4 c1 : source_color = vec4(0.97, 0.92, 0.82, 1.0);
uniform vec4 c2 : source_color = vec4(0.2, 0.45, 0.85, 1.0);
uniform vec4 c3 : source_color = vec4(0.97, 0.92, 0.82, 1.0);
uniform float stripe = 20.0; // design pixels per stripe
uniform float wobble = 0.0;
void fragment() {
    float a = COLOR.a;
    float s = (1.0 / SCREEN_PIXEL_SIZE.y) / 900.0; // screen pixels per design pixel
    vec2 p = FRAGCOORD.xy / s;
    int band = int(floor((p.y + sin(p.x * 0.02 + TIME * wobble) * 3.0) / stripe)) % 4;
    vec4 c = band == 0 ? c0 : band == 1 ? c1 : band == 2 ? c2 : c3;
    float h = texture(knit, p / 48.0).a;
    COLOR = vec4(c.rgb * (0.72 + h * 0.42), a);
}
";
        static Shader knitShader;

        /// <summary>A label whose letters look knitted from striped yarn (the logo, big headings).</summary>
        public static Control KnitText(string text, int size, Color c0, Color c1, Color c2, Color c3, float stripe = 20f)
        {
            knitShader ??= new Shader { Code = KnitShader };
            var holder = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            // a dark "shadow" copy gives the knitted letters their thickness
            var shadow = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
            shadow.AddThemeFontOverride("font", Logo);
            shadow.AddThemeFontSizeOverride("font_size", size);
            shadow.AddThemeColorOverride("font_color", new Color(0.14f, 0.07f, 0.09f, 0.9f));
            shadow.AddThemeColorOverride("font_outline_color", new Color(0.14f, 0.07f, 0.09f, 0.9f));
            shadow.AddThemeConstantOverride("outline_size", size / 6);
            shadow.AddThemeConstantOverride("shadow_offset_x", 0);
            shadow.AddThemeConstantOverride("shadow_offset_y", 0);
            shadow.Position = new Vector2(size * 0.06f, size * 0.08f);
            holder.AddChild(shadow);
            var face = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
            face.AddThemeFontOverride("font", Logo);
            face.AddThemeFontSizeOverride("font_size", size);
            face.AddThemeConstantOverride("shadow_offset_x", 0);
            face.AddThemeConstantOverride("shadow_offset_y", 0);
            var m = new ShaderMaterial { Shader = knitShader };
            m.SetShaderParameter("knit", Rendering.Surfaces.Texture("knit"));
            m.SetShaderParameter("c0", c0);
            m.SetShaderParameter("c1", c1);
            m.SetShaderParameter("c2", c2);
            m.SetShaderParameter("c3", c3);
            m.SetShaderParameter("stripe", stripe);
            face.Material = m;
            holder.AddChild(face);
            // measure with the font itself: labels outside the tree report no size yet
            var ts = Logo.GetStringSize(text, HorizontalAlignment.Left, -1, size);
            holder.CustomMinimumSize = new Vector2(ts.X + size * 0.2f, Logo.GetHeight(size) * 0.92f) + shadow.Position;
            return holder;
        }

        /// <summary>A plain label in one of the house fonts.</summary>
        public static Label Text(string s, int size = 22, Color? color = null, Font font = null, bool wrap = false)
        {
            var l = new Label { Text = s, MouseFilter = Control.MouseFilterEnum.Ignore };
            if (font != null) l.AddThemeFontOverride("font", font);
            l.AddThemeFontSizeOverride("font_size", size);
            if (color.HasValue) l.AddThemeColorOverride("font_color", color.Value);
            if (wrap)
            {
                l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                l.CustomMinimumSize = new Vector2(100, 0);
            }
            return l;
        }
    }
}
