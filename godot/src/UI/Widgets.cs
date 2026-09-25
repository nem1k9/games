using System;
using System.Collections.Generic;
using Godot;
using SockGang.Rendering;
using SockGang.Session;

namespace SockGang.UI
{
    public enum IconKind
    {
        Sock, Hat, TwoHats, Spyglass, Gear, Door, Moon, Sun, Eye, EyeClosed, Steps, Sack, Star, Yarn, Zzz,
        Alert, Question, Pocket, Chaos, Needle, Scroll, Dice, Heart, Box, Tick,
    }

    /// <summary>Small hand-drawn icons (vector, drawn with the canvas: crisp at any size).</summary>
    public partial class IconView : Control
    {
        public IconKind Kind;
        public Color Tint = Style.Cream;

        public IconView() => MouseFilter = MouseFilterEnum.Ignore;

        public IconView(IconKind kind, float size, Color? tint = null) : this()
        {
            Kind = kind;
            Tint = tint ?? Style.Cream;
            CustomMinimumSize = new Vector2(size, size);
            Size = CustomMinimumSize;
        }

        public void Set(IconKind kind, Color tint)
        {
            if (Kind == kind && Tint == tint) return;
            Kind = kind;
            Tint = tint;
            QueueRedraw();
        }

        public override void _Draw() => Icons.Draw(this, Kind, new Rect2(Vector2.Zero, Size), Tint);
    }

    public static class Icons
    {
        static readonly Color Line = new Color(0.12f, 0.06f, 0.08f, 0.9f);

        static Vector2[] Circle(Vector2 c, float r, int n = 24, float a0 = 0, float a1 = Mathf.Tau)
        {
            var p = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = a0 + (a1 - a0) * i / (n - (a1 - a0 >= Mathf.Tau - 0.001f ? 0 : 1));
                p[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return p;
        }

        static void Poly(CanvasItem ci, Vector2[] pts, Color fill, float outline = 2f)
        {
            ci.DrawColoredPolygon(pts, fill);
            if (outline > 0)
            {
                var closed = new Vector2[pts.Length + 1];
                pts.CopyTo(closed, 0);
                closed[pts.Length] = pts[0];
                ci.DrawPolyline(closed, Line, outline, true);
            }
        }

        public static void Draw(CanvasItem ci, IconKind kind, Rect2 r, Color tint)
        {
            float s = Mathf.Min(r.Size.X, r.Size.Y);
            var o = r.Position + (r.Size - new Vector2(s, s)) / 2;
            Vector2 P(float x, float y) => o + new Vector2(x, y) * s; // unit coordinates
            float w = Mathf.Max(1.5f, s * 0.06f);
            switch (kind)
            {
                case IconKind.Sock:
                {
                    var pts = new[] { P(0.36f, 0.08f), P(0.66f, 0.08f), P(0.66f, 0.58f), P(0.86f, 0.7f), P(0.88f, 0.84f), P(0.74f, 0.93f), P(0.36f, 0.9f), P(0.3f, 0.78f), P(0.36f, 0.6f) };
                    Poly(ci, pts, tint, w);
                    ci.DrawLine(P(0.36f, 0.22f), P(0.66f, 0.22f), Style.Red, w * 1.6f);
                    ci.DrawLine(P(0.36f, 0.34f), P(0.66f, 0.34f), Style.Red, w * 1.6f);
                    break;
                }
                case IconKind.Hat:
                {
                    Poly(ci, new[] { P(0.18f, 0.84f), P(0.82f, 0.84f), P(0.62f, 0.38f), P(0.82f, 0.18f), P(0.5f, 0.24f), P(0.34f, 0.4f) }, tint, w);
                    ci.DrawCircle(P(0.84f, 0.17f), s * 0.08f, Style.Cream);
                    ci.DrawLine(P(0.18f, 0.84f), P(0.82f, 0.84f), Style.Cream, w * 2.2f);
                    break;
                }
                case IconKind.TwoHats:
                    Draw(ci, IconKind.Hat, new Rect2(o + new Vector2(0, s * 0.18f), new Vector2(s * 0.7f, s * 0.7f)), Style.Teal);
                    Draw(ci, IconKind.Hat, new Rect2(o + new Vector2(s * 0.3f, s * 0.05f), new Vector2(s * 0.7f, s * 0.7f)), tint);
                    break;
                case IconKind.Spyglass:
                    ci.DrawLine(P(0.2f, 0.8f), P(0.62f, 0.38f), tint, s * 0.18f);
                    ci.DrawLine(P(0.52f, 0.48f), P(0.84f, 0.16f), tint.Darkened(0.2f), s * 0.26f);
                    ci.DrawArc(P(0.84f, 0.16f), s * 0.13f, 0, Mathf.Tau, 16, Line, w);
                    break;
                case IconKind.Gear:
                {
                    var c = P(0.5f, 0.5f);
                    var pts = new List<Vector2>();
                    for (int i = 0; i < 32; i++)
                    {
                        float a = i / 32f * Mathf.Tau;
                        float rr = (i / 2) % 2 == 0 ? 0.42f : 0.32f;
                        pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr * s);
                    }
                    Poly(ci, pts.ToArray(), tint, w);
                    ci.DrawCircle(c, s * 0.12f, Line);
                    break;
                }
                case IconKind.Door:
                    Poly(ci, new[] { P(0.26f, 0.9f), P(0.26f, 0.3f), P(0.5f, 0.1f), P(0.74f, 0.3f), P(0.74f, 0.9f) }, tint, w);
                    ci.DrawCircle(P(0.64f, 0.58f), s * 0.05f, Style.Gold);
                    break;
                case IconKind.Moon:
                {
                    var c = P(0.5f, 0.5f);
                    ci.DrawCircle(c, s * 0.4f, tint);
                    ci.DrawCircle(c + new Vector2(s * 0.2f, -s * 0.12f), s * 0.33f, new Color(0, 0, 0, 0));
                    // a crescent: cover with a disc of the background drawn as a transparent cut is impossible, so shade instead
                    ci.DrawCircle(c + new Vector2(s * 0.18f, -s * 0.1f), s * 0.32f, tint.Darkened(0.55f) with { A = 0.85f });
                    ci.DrawCircle(c + new Vector2(-s * 0.12f, s * 0.1f), s * 0.05f, tint.Darkened(0.2f));
                    break;
                }
                case IconKind.Sun:
                {
                    var c = P(0.5f, 0.5f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.Tau;
                        ci.DrawLine(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * 0.3f, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * 0.46f, tint, w * 1.4f);
                    }
                    ci.DrawCircle(c, s * 0.24f, tint);
                    break;
                }
                case IconKind.Eye:
                case IconKind.EyeClosed:
                {
                    var c = P(0.5f, 0.5f);
                    if (kind == IconKind.Eye)
                    {
                        var eye = new List<Vector2>();
                        for (int i = 0; i <= 12; i++) eye.Add(c + new Vector2(Mathf.Lerp(-0.42f, 0.42f, i / 12f), -Mathf.Sin(i / 12f * Mathf.Pi) * 0.26f) * s);
                        for (int i = 11; i > 0; i--) eye.Add(c + new Vector2(Mathf.Lerp(-0.42f, 0.42f, i / 12f), Mathf.Sin(i / 12f * Mathf.Pi) * 0.26f) * s);
                        Poly(ci, eye.ToArray(), Style.Cream, w);
                        ci.DrawCircle(c, s * 0.15f, tint);
                        ci.DrawCircle(c, s * 0.07f, Line);
                    }
                    else
                    {
                        var lid = new List<Vector2>();
                        for (int i = 0; i <= 12; i++) lid.Add(c + new Vector2(Mathf.Lerp(-0.42f, 0.42f, i / 12f), Mathf.Sin(i / 12f * Mathf.Pi) * 0.16f) * s);
                        ci.DrawPolyline(lid.ToArray(), tint, w * 1.5f, true);
                        for (int i = 0; i < 3; i++) ci.DrawLine(c + new Vector2(-0.2f + i * 0.2f, 0.2f) * s, c + new Vector2(-0.24f + i * 0.24f, 0.33f) * s, tint, w);
                    }
                    break;
                }
                case IconKind.Steps:
                    foreach (var (x, y, rot) in new[] { (0.32f, 0.62f, -0.2f), (0.64f, 0.34f, 0.2f) })
                    {
                        var c = P(x, y);
                        var sole = Circle(Vector2.Zero, s * 0.14f, 14);
                        for (int i = 0; i < sole.Length; i++) sole[i] = c + new Vector2(sole[i].X * 0.8f, sole[i].Y * 1.4f).Rotated(rot);
                        Poly(ci, sole, tint, w * 0.7f);
                        ci.DrawCircle(c + new Vector2(0, -s * 0.27f).Rotated(rot), s * 0.07f, tint);
                    }
                    break;
                case IconKind.Sack:
                    Poly(ci, new[] { P(0.3f, 0.36f), P(0.7f, 0.36f), P(0.86f, 0.62f), P(0.76f, 0.9f), P(0.24f, 0.9f), P(0.14f, 0.62f) }, tint, w);
                    Poly(ci, new[] { P(0.38f, 0.36f), P(0.62f, 0.36f), P(0.66f, 0.16f), P(0.34f, 0.16f) }, tint.Darkened(0.1f), w);
                    ci.DrawLine(P(0.34f, 0.36f), P(0.66f, 0.36f), Style.Ink, w * 1.6f);
                    break;
                case IconKind.Star:
                {
                    var c = P(0.5f, 0.53f);
                    var pts = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float a = -Mathf.Pi / 2 + i * Mathf.Pi / 5;
                        pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * (i % 2 == 0 ? 0.46f : 0.2f);
                    }
                    Poly(ci, pts, tint, w * 0.8f);
                    break;
                }
                case IconKind.Yarn:
                {
                    var c = P(0.5f, 0.5f);
                    ci.DrawCircle(c, s * 0.42f, tint);
                    for (int k = -2; k <= 2; k++) ci.DrawArc(c + new Vector2(k * s * 0.12f, 0), s * 0.4f, Mathf.Pi * 0.35f, Mathf.Pi * 0.65f, 8, tint.Darkened(0.3f), w);
                    ci.DrawArc(c, s * 0.3f, -0.5f, 1.2f, 10, tint.Lightened(0.3f), w);
                    ci.DrawArc(c, s * 0.42f, 0, Mathf.Tau, 24, Line, w);
                    ci.DrawPolyline(new[] { P(0.86f, 0.62f), P(0.96f, 0.8f), P(0.84f, 0.96f) }, tint, w, true);
                    break;
                }
                case IconKind.Zzz:
                {
                    var f = Style.Bold;
                    ci.DrawString(f, P(0.08f, 0.9f), "Z", HorizontalAlignment.Left, -1, (int)(s * 0.55f), tint);
                    ci.DrawString(f, P(0.44f, 0.58f), "z", HorizontalAlignment.Left, -1, (int)(s * 0.42f), tint);
                    ci.DrawString(f, P(0.72f, 0.32f), "z", HorizontalAlignment.Left, -1, (int)(s * 0.3f), tint);
                    break;
                }
                case IconKind.Alert:
                case IconKind.Question:
                {
                    ci.DrawCircle(P(0.5f, 0.5f), s * 0.44f, tint);
                    ci.DrawArc(P(0.5f, 0.5f), s * 0.44f, 0, Mathf.Tau, 24, Line, w);
                    string ch = kind == IconKind.Alert ? "!" : "?";
                    ci.DrawString(Style.Bold, P(0.33f, 0.8f), ch, HorizontalAlignment.Center, s * 0.34f, (int)(s * 0.66f), Style.Ink);
                    break;
                }
                case IconKind.Pocket:
                    Poly(ci, new[] { P(0.16f, 0.2f), P(0.84f, 0.2f), P(0.84f, 0.62f), P(0.5f, 0.9f), P(0.16f, 0.62f) }, tint, w);
                    ci.DrawDashedLine(P(0.24f, 0.3f), P(0.76f, 0.3f), Style.Cream, w, s * 0.08f);
                    break;
                case IconKind.Chaos:
                    Poly(ci, new[] { P(0.58f, 0.06f), P(0.24f, 0.56f), P(0.48f, 0.56f), P(0.38f, 0.94f), P(0.78f, 0.4f), P(0.52f, 0.4f) }, tint, w);
                    break;
                case IconKind.Needle:
                    ci.DrawLine(P(0.18f, 0.84f), P(0.8f, 0.2f), tint, w * 1.6f);
                    ci.DrawArc(P(0.8f, 0.2f), s * 0.07f, 0, Mathf.Tau, 10, tint, w);
                    ci.DrawPolyline(new[] { P(0.8f, 0.2f), P(0.6f, 0.3f), P(0.72f, 0.56f), P(0.5f, 0.7f) }, Style.Red, w, true);
                    break;
                case IconKind.Scroll:
                    Poly(ci, new[] { P(0.2f, 0.16f), P(0.8f, 0.16f), P(0.8f, 0.84f), P(0.2f, 0.84f) }, tint, w);
                    for (int i = 0; i < 4; i++) ci.DrawLine(P(0.3f, 0.3f + i * 0.14f), P(0.7f, 0.3f + i * 0.14f), Style.Ink with { A = 0.6f }, w * 0.8f);
                    break;
                case IconKind.Dice:
                {
                    Poly(ci, new[] { P(0.14f, 0.14f), P(0.86f, 0.14f), P(0.86f, 0.86f), P(0.14f, 0.86f) }, tint, w);
                    foreach (var (x, y) in new[] { (0.32f, 0.32f), (0.68f, 0.32f), (0.5f, 0.5f), (0.32f, 0.68f), (0.68f, 0.68f) })
                        ci.DrawCircle(P(x, y), s * 0.065f, Style.Ink);
                    break;
                }
                case IconKind.Box:
                case IconKind.Tick:
                {
                    var sq = new[] { P(0.16f, 0.2f), P(0.8f, 0.16f), P(0.84f, 0.8f), P(0.2f, 0.84f) };
                    var closed = new[] { sq[0], sq[1], sq[2], sq[3], sq[0] };
                    ci.DrawPolyline(closed, tint, w * 1.2f, true);
                    if (kind == IconKind.Tick) ci.DrawPolyline(new[] { P(0.26f, 0.5f), P(0.46f, 0.72f), P(0.9f, 0.08f) }, Style.Moss.Darkened(0.2f), w * 2.2f, true);
                    break;
                }
                case IconKind.Heart:
                {
                    var pts = new List<Vector2>();
                    for (int i = 0; i < 40; i++)
                    {
                        float t = i / 40f * Mathf.Tau;
                        float x = 16 * Mathf.Pow(Mathf.Sin(t), 3);
                        float y = 13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) - 2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t);
                        pts.Add(P(0.5f + x / 38f, 0.5f - y / 38f));
                    }
                    Poly(ci, pts.ToArray(), tint, w);
                    break;
                }
            }
        }
    }

    /// <summary>A ball of yarn to pick a hat colour (the rainbow one appears when a secret is found).</summary>
    public partial class YarnSwatch : Button
    {
        public Color Yarn;
        public bool Selected;
        public bool Rainbow;

        public YarnSwatch()
        {
            FocusMode = FocusModeEnum.None;
            CustomMinimumSize = new Vector2(56, 56);
            foreach (var st in new[] { "normal", "hover", "pressed", "focus", "disabled" }) AddThemeStyleboxOverride(st, new StyleBoxEmpty());
            MouseEntered += QueueRedraw;
            MouseExited += QueueRedraw;
        }

        public override void _Process(double delta)
        {
            if (Rainbow) QueueRedraw();
        }

        public override void _Draw()
        {
            var c = Size / 2;
            float r = Mathf.Min(Size.X, Size.Y) * (IsHovered() ? 0.42f : 0.38f);
            var col = Rainbow ? Color.FromHsv((float)(Time.GetTicksMsec() / 2000.0 % 1.0), 0.75f, 0.95f) : Yarn;
            if (Selected)
            {
                DrawCircle(c, r + 7, new Color(1, 1, 1, 0.12f));
                DrawArc(c, r + 6, 0, Mathf.Tau, 32, Style.Gold, 3f, true);
            }
            DrawCircle(c + new Vector2(2, 3), r, new Color(0, 0, 0, 0.35f));
            DrawCircle(c, r, col);
            for (int k = -2; k <= 2; k++) DrawArc(c + new Vector2(k * r * 0.28f, 0), r * 0.95f, Mathf.Pi * 0.3f, Mathf.Pi * 0.7f, 8, col.Darkened(0.28f), 2f, true);
            DrawArc(c, r * 0.72f, -0.6f, 1.1f, 10, col.Lightened(0.35f), 2f, true);
            DrawArc(c, r, 0, Mathf.Tau, 28, new Color(0.1f, 0.05f, 0.07f, 0.8f), 2f, true);
            if (Rainbow) DrawString(Style.Bold, c + new Vector2(-6, 8), "?", HorizontalAlignment.Left, -1, 22, Colors.White);
        }
    }

    /// <summary>Night progress: the moon crosses the sky from midnight to 6 am, the sun waits at the end.</summary>
    public partial class MoonClock : Control
    {
        public float Progress;
        public string Time = "00:00";

        public MoonClock()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            CustomMinimumSize = new Vector2(300, 96);
        }

        public override void _Draw()
        {
            var size = Size;
            var c = new Vector2(size.X / 2, size.Y - 14);
            float rx = size.X * 0.42f, ry = size.Y * 0.7f;
            var arc = new Vector2[33];
            for (int i = 0; i <= 32; i++)
            {
                float a = Mathf.Pi + Mathf.Pi * i / 32f;
                arc[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
            }
            for (int i = 0; i < 32; i += 2) DrawLine(arc[i], arc[i + 1], new Color(1, 1, 1, 0.35f), 2f, true);
            int upto = Mathf.Clamp((int)(Progress * 32), 0, 32);
            for (int i = 0; i < upto; i++) DrawLine(arc[i], arc[i + 1], Style.Gold with { A = 0.8f }, 3f, true);
            Icons.Draw(this, IconKind.Sun, new Rect2(arc[32] - new Vector2(13, 13), new Vector2(26, 26)), new Color(1f, 0.75f, 0.3f, 0.5f + Progress * 0.5f));
            float pa = Mathf.Pi + Mathf.Pi * Mathf.Clamp(Progress, 0f, 1f);
            var mp = c + new Vector2(Mathf.Cos(pa) * rx, Mathf.Sin(pa) * ry);
            DrawCircle(mp, 20, new Color(0.9f, 0.93f, 1f, 0.15f));
            Icons.Draw(this, IconKind.Moon, new Rect2(mp - new Vector2(16, 16), new Vector2(32, 32)), new Color(0.95f, 0.95f, 0.85f));
            var f = Style.Bold;
            var ts = f.GetStringSize(Time, HorizontalAlignment.Left, -1, 30);
            DrawString(f, c + new Vector2(-ts.X / 2 + 2, -4 + 2), Time, HorizontalAlignment.Left, -1, 30, new Color(0, 0, 0, 0.5f));
            DrawString(f, c + new Vector2(-ts.X / 2, -4), Time, HorizontalAlignment.Left, -1, 30, Style.Cream);
        }
    }

    /// <summary>A rolling ball of yarn with a trailing thread: the loading indicator.</summary>
    public partial class YarnSpinner : Control
    {
        public Color Yarn = Style.Red;

        public YarnSpinner()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            CustomMinimumSize = new Vector2(120, 120);
        }

        public override void _Process(double delta) => QueueRedraw();

        public override void _Draw()
        {
            float t = (float)(Time.GetTicksMsec() / 1000.0);
            var c = Size / 2;
            float r = Mathf.Min(Size.X, Size.Y) * 0.28f;
            var trail = new List<Vector2>();
            for (int i = 0; i < 24; i++)
            {
                float a = -t * 3f - i * 0.18f;
                float rr = r * 1.55f + Mathf.Sin(i * 0.7f + t * 4f) * 3f;
                trail.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
            }
            DrawPolyline(trail.ToArray(), Yarn.Lightened(0.2f), 3f, true);
            DrawCircle(c, r, Yarn);
            for (int k = 0; k < 4; k++)
            {
                float a = t * 2.4f + k * Mathf.Pi / 4;
                DrawArc(c, r * 0.94f, a, a + Mathf.Pi * 0.6f, 10, Yarn.Darkened(0.3f), 2.5f, true);
            }
            DrawArc(c, r, 0, Mathf.Tau, 24, new Color(0.1f, 0.05f, 0.07f, 0.8f), 2f, true);
        }
    }

    /// <summary>Falling bits of colourful yarn (secrets found) or snowflakes (winter holidays).</summary>
    public partial class Flurry : Control
    {
        struct Bit
        {
            public Vector2 P, V;
            public float Rot, Spin, Size, Life;
            public Color C;
        }

        readonly List<Bit> bits = new List<Bit>();
        readonly Random rng = new Random();
        public bool Snow;
        public float Rate; // bits per second (0: only bursts)
        float acc;

        public Flurry()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsPreset(LayoutPreset.FullRect);
        }

        public void Burst(int n)
        {
            for (int i = 0; i < n; i++) Spawn(true);
        }

        void Spawn(bool burst)
        {
            var size = GetViewportRect().Size;
            float x = (float)rng.NextDouble() * size.X;
            var b = new Bit
            {
                P = burst ? new Vector2(size.X / 2 + (float)(rng.NextDouble() - 0.5) * 200, size.Y * 0.45f) : new Vector2(x, -20),
                V = burst ? new Vector2((float)(rng.NextDouble() - 0.5) * 900, -300 - (float)rng.NextDouble() * 600) : new Vector2((float)(rng.NextDouble() - 0.5) * 30, 40 + (float)rng.NextDouble() * 60),
                Rot = (float)rng.NextDouble() * 6f,
                Spin = (float)(rng.NextDouble() - 0.5) * 8f,
                Size = Snow ? 2f + (float)rng.NextDouble() * 4f : 6f + (float)rng.NextDouble() * 8f,
                Life = burst ? 3f : 30f,
                C = Snow ? new Color(1, 1, 1, 0.8f) : Color.FromHsv((float)rng.NextDouble(), 0.7f, 1f),
            };
            bits.Add(b);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            acc += Rate * dt;
            while (acc >= 1f)
            {
                acc -= 1f;
                Spawn(false);
            }
            var size = GetViewportRect().Size;
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                var b = bits[i];
                if (!Snow) b.V.Y += 900f * dt;
                b.V.X += Mathf.Sin(b.Rot) * (Snow ? 12f : 0f) * dt;
                b.P += b.V * dt;
                b.Rot += b.Spin * dt;
                b.Life -= dt;
                if (b.Life <= 0 || b.P.Y > size.Y + 30) bits.RemoveAt(i);
                else bits[i] = b;
            }
            QueueRedraw();
        }

        public override void _Draw()
        {
            foreach (var b in bits)
            {
                if (Snow) DrawCircle(b.P, b.Size, b.C);
                else
                {
                    DrawSetTransform(b.P, b.Rot, Vector2.One);
                    DrawRect(new Rect2(-b.Size / 2, -b.Size / 5, b.Size, b.Size / 2.5f), b.C);
                    DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                }
            }
        }
    }

    /// <summary>
    /// Your gnome, alive in the menu: rotates on a thread spool, wears the chosen hat, hops when poked
    /// (poke him too often and he sneezes).
    /// </summary>
    public partial class GnomePreview : SubViewportContainer
    {
        SubViewport vp;
        Node3D pivot;
        ModelInstance gnome;
        byte hat;
        float hop, spinKick;
        int pokes;
        float lastPoke;
        Quaternion midRest, tipRest;
        bool restSaved;
        public event Action<int> Poked;

        public GnomePreview()
        {
            Stretch = true;
            CustomMinimumSize = new Vector2(360, 420);
            MouseFilter = MouseFilterEnum.Stop;
        }

        public override void _Ready()
        {
            vp = new SubViewport { OwnWorld3D = true, TransparentBg = true, Msaa3D = Viewport.Msaa.Msaa4X, Size = new Vector2I(360, 420) };
            AddChild(vp);
            var env = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.62f, 0.56f, 0.62f),
                AmbientLightEnergy = 1f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
            };
            vp.AddChild(new WorldEnvironment { Environment = env });
            var key = new DirectionalLight3D { LightEnergy = 1.4f, LightColor = new Color(1f, 0.92f, 0.8f), ShadowEnabled = false };
            vp.AddChild(key);
            key.RotationDegrees = new Vector3(-35, -30, 0);
            var rim = new OmniLight3D { LightEnergy = 1.6f, LightColor = new Color(0.55f, 0.65f, 1f), OmniRange = 4f, Position = new Vector3(-0.8f, 1.4f, -1.2f) };
            vp.AddChild(rim);
            var cam = new Camera3D { Fov = 30, Current = true };
            vp.AddChild(cam);
            cam.LookAtFromPosition(new Vector3(0, 0.9f, 2.9f), new Vector3(0, 0.62f, 0), Vector3.Up);
            // a wooden thread spool as a pedestal
            var wood = new StandardMaterial3D { AlbedoColor = new Color(0.78f, 0.58f, 0.37f), Roughness = 0.7f };
            var spool = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.06f, RadialSegments = 32 }, MaterialOverride = wood, Position = new Vector3(0, -0.03f, 0) };
            vp.AddChild(spool);
            var thread = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.4f, BottomRadius = 0.4f, Height = 0.18f, RadialSegments = 32 }, MaterialOverride = Surfaces.Tinted(Gnomes.Core.Models.MeshKind.Knit, Style.Teal), Position = new Vector3(0, -0.15f, 0) };
            vp.AddChild(thread);
            var spool2 = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.06f, RadialSegments = 32 }, MaterialOverride = wood, Position = new Vector3(0, -0.27f, 0) };
            vp.AddChild(spool2);
            pivot = new Node3D();
            vp.AddChild(pivot);
            gnome = ModelLibrary.Instantiate("gnome", pivot, ColliderMode.None);
            gnome.Rotation = new Vector3(0, Mathf.Pi, 0); // model front is -Z: face the camera
            SetHat(hat);
        }

        public void SetHat(byte h)
        {
            hat = h;
            if (gnome != null && !GameSession.IsRainbow(h)) gnome.SetTint(GameSession.HatColor(h));
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                hop = 1f;
                spinKick += 6f;
                pokes = Clock.Now - lastPoke < 1.5f ? pokes + 1 : 1;
                lastPoke = Clock.Now;
                Audio.Sfx.I?.PlayUi(Audio.SoundId.Jump, 0.5f);
                Poked?.Invoke(pokes);
            }
        }

        public override void _Process(double delta)
        {
            if (pivot == null) return;
            float dt = (float)delta, t = Clock.Now;
            spinKick = Mathf.MoveToward(spinKick, 0f, dt * 5f);
            float yaw = Mathf.Sin(t * 0.6f) * 0.5f + spinKick * 0.9f;
            // look a bit towards the mouse
            var m = GetLocalMousePosition();
            if (new Rect2(Vector2.Zero, Size).HasPoint(m)) yaw += (m.X / Size.X - 0.5f) * 0.8f;
            hop = Mathf.MoveToward(hop, 0f, dt * 2.2f);
            float h = Mathf.Sin((1f - hop) * Mathf.Pi) * (hop > 0 ? 0.35f : 0f);
            pivot.Rotation = new Vector3(0, yaw, 0);
            pivot.Position = new Vector3(0, h, 0);
            float squash = 1f + Mathf.Sin(t * 2.2f) * 0.015f;
            pivot.Scale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));
            if (GameSession.IsRainbow(hat) && gnome != null) gnome.SetTint(GameSession.RainbowColor(t));
            // the floppy hat sways (on top of its modelled droop) and flops when he lands
            var hatMid = gnome?.Node("hatMid");
            var hatTip = gnome?.Node("hatTip");
            if (hatMid != null && hatTip != null)
            {
                if (!restSaved)
                {
                    midRest = hatMid.Quaternion;
                    tipRest = hatTip.Quaternion;
                    restSaved = true;
                }
                hatMid.Quaternion = midRest * Conv.UEuler(Mathf.Sin(t * 1.7f) * 8f + hop * 25f, 0, Mathf.Sin(t * 1.3f) * 6f);
                hatTip.Quaternion = tipRest * Conv.UEuler(Mathf.Sin(t * 1.7f - 0.6f) * 12f + hop * 35f, 0, Mathf.Sin(t * 1.3f - 0.5f) * 9f);
            }
        }
    }

    /// <summary>Hover wobble for buttons: a felt patch leans and grows a little under the mouse.</summary>
    public static class Wobble
    {
        public static T Add<T>(T b, float lean = 1.5f) where T : Control
        {
            b.Resized += () => b.PivotOffset = b.Size / 2;
            b.MouseEntered += () =>
            {
                var tw = b.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                tw.TweenProperty(b, "scale", new Vector2(1.04f, 1.04f), 0.18f);
                tw.TweenProperty(b, "rotation", Mathf.DegToRad(GD.Randf() > 0.5f ? lean : -lean), 0.18f);
                Audio.Sfx.I?.PlayUi(Audio.SoundId.Click, 0.15f);
            };
            b.MouseExited += () =>
            {
                var tw = b.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine);
                tw.TweenProperty(b, "scale", Vector2.One, 0.15f);
                tw.TweenProperty(b, "rotation", 0f, 0.15f);
            };
            return b;
        }
    }
}
