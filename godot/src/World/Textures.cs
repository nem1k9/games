using System;
using System.Collections.Generic;
using Gnomes.Core.Level;
using Godot;

namespace SockGang.World
{
    /// <summary>Procedurally painted, tileable textures for walls and floors (no image assets needed).</summary>
    public static class Textures
    {
        const int N = 256;
        static readonly Dictionary<string, ImageTexture> cache = new Dictionary<string, ImageTexture>();
        static readonly Dictionary<string, StandardMaterial3D> mats = new Dictionary<string, StandardMaterial3D>();

        struct Color32
        {
            public byte r, g, b;
            public Color32(byte r, byte g, byte b) { this.r = r; this.g = g; this.b = b; }
        }

        static float Hash(int x, int y, int seed = 0)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
            }
        }

        static float Lerpf(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Tileable value noise in [0,1].</summary>
        static float Noise(float x, float y, int period, int seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            int X0 = ((x0 % period) + period) % period, Y0 = ((y0 % period) + period) % period;
            int X1 = (X0 + 1) % period, Y1 = (Y0 + 1) % period;
            float a = Hash(X0, Y0, seed), b = Hash(X1, Y0, seed), c = Hash(X0, Y1, seed), d = Hash(X1, Y1, seed);
            return Lerpf(Lerpf(a, b, fx), Lerpf(c, d, fx), fy);
        }

        static float Fbm(int x, int y, int seed)
        {
            float v = 0, amp = 0.5f;
            int period = 8;
            for (int o = 0; o < 4; o++)
            {
                v += amp * Noise(x * period / (float)N, y * period / (float)N, period, seed + o);
                period *= 2;
                amp *= 0.5f;
            }
            return v;
        }

        static Color32 C(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

        static byte B(float v) => (byte)Math.Clamp(v, 0, 255);

        static Color32 Shade(Color32 c, float k) => new Color32(B(c.r * k), B(c.g * k), B(c.b * k));

        static Color32 Lerp(Color32 a, Color32 b, float t) => new Color32(B(a.r + (b.r - a.r) * t), B(a.g + (b.g - a.g) * t), B(a.b + (b.b - a.b) * t));

        delegate Color32 Painter(int x, int y);

        static ImageTexture Make(string name, Painter p)
        {
            if (cache.TryGetValue(name, out var t)) return t;
            var data = new byte[N * N * 4];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var c = p(x, y);
                    int i = ((N - 1 - y) * N + x) * 4;
                    data[i] = c.r;
                    data[i + 1] = c.g;
                    data[i + 2] = c.b;
                    data[i + 3] = 255;
                }
            var img = Image.CreateFromData(N, N, false, Image.Format.Rgba8, data);
            img.GenerateMipmaps();
            t = ImageTexture.CreateFromImage(img);
            t.ResourceName = name;
            cache[name] = t;
            return t;
        }

        public static ImageTexture Wall(WallStyle s)
        {
            switch (s)
            {
                case WallStyle.StripesBlue:
                    return Make("wall_stripes", (x, y) =>
                    {
                        var baseC = C(0xc9dcef);
                        int band = x % 64;
                        var c = band < 10 ? C(0x9fbfe0) : band > 30 && band < 34 ? C(0xe8f0f8) : baseC;
                        if ((x + 16) % 64 < 3 && y % 32 < 3) c = C(0x6f93c0);
                        return Shade(c, 0.92f + Fbm(x, y, 1) * 0.12f);
                    });
                case WallStyle.MintTile:
                    return Make("wall_mint", (x, y) =>
                    {
                        bool grout = x % 32 < 2 || y % 32 < 2;
                        var c = grout ? C(0xe8efe8) : C(0x9fd6c2);
                        return Shade(c, 0.95f + Hash(x / 32, y / 32, 3) * 0.08f + Fbm(x, y, 2) * 0.05f);
                    });
                case WallStyle.Yellow:
                    return Make("wall_yellow", (x, y) =>
                    {
                        var c = C(0xf2d98a);
                        if (x % 32 < 2) c = C(0xe8c26a);
                        int cx = x % 64 - 32, cy = y % 64 - 32;
                        if ((x / 64 + y / 64) % 2 == 0 && cx * cx + cy * cy < 30) c = C(0xd98a3a); // little oranges
                        return Shade(c, 0.93f + Fbm(x, y, 4) * 0.1f);
                    });
                case WallStyle.Damask:
                    return Make("wall_damask", (x, y) =>
                    {
                        var c = C(0x4f7a5a);
                        int dx = Math.Abs(x % 64 - 32), dy = Math.Abs(y % 64 - 32);
                        if (dx + dy < 22 && dx + dy > 16) c = C(0x7aa07c);
                        if (dx + dy < 6) c = C(0xc9b36a);
                        return Shade(c, 0.9f + Fbm(x, y, 5) * 0.14f);
                    });
                case WallStyle.Floral:
                    return Make("wall_floral", (x, y) =>
                    {
                        var c = C(0xe8c2c0);
                        for (int k = 0; k < 2; k++)
                        {
                            int ox = k * 32, oy = k * 32;
                            int fx = (x + ox) % 64 - 32, fy = (y + oy) % 64 - 32;
                            float r = (float)Math.Sqrt(fx * fx + fy * fy);
                            float ang = (float)Math.Atan2(fy, fx);
                            float petal = 7 + 4 * (float)Math.Cos(ang * 5);
                            if (r < petal) c = k == 0 ? C(0xd07a86) : C(0xf0e0c0);
                            if (r < 3) c = C(0xf2cf3c);
                            if (fx > 8 && fx < 18 && Math.Abs(fy - 10) < 2) c = C(0x6a9a6a);
                        }
                        return Shade(c, 0.93f + Fbm(x, y, 6) * 0.1f);
                    });
                case WallStyle.Panel:
                    return Make("wall_panel", (x, y) =>
                    {
                        int plank = x / 32;
                        var c = Shade(C(0x7a4a2e), 0.85f + Hash(plank, 0, 7) * 0.3f);
                        if (x % 32 < 2) c = C(0x4a2a18);
                        float grain = (float)Math.Sin((y * 0.08f + Fbm(x * 3, y, 8) * 6f)) * 0.08f;
                        return Shade(c, 1f + grain);
                    });
                default:
                    return Make("wall_plaster", (x, y) => Shade(C(0xe7dcc6), 0.9f + Fbm(x, y, 9) * 0.16f));
            }
        }

        public static ImageTexture Floor(FloorStyle s)
        {
            switch (s)
            {
                case FloorStyle.Wood:
                case FloorStyle.DarkWood:
                    bool dark = s == FloorStyle.DarkWood;
                    return Make(dark ? "floor_darkwood" : "floor_wood", (x, y) =>
                    {
                        int row = y / 32;
                        int offset = (row * 97) % 256;
                        int plank = ((x + offset) % 256) / 128;
                        var baseC = dark ? C(0x5a3522) : C(0xb07a4a);
                        var c = Shade(baseC, 0.82f + Hash(plank, row, 10) * 0.3f);
                        if (y % 32 < 2 || (x + offset) % 128 < 2) c = Shade(baseC, 0.55f);
                        float grain = (float)Math.Sin(x * 0.12f + Fbm(x, y * 4, 11) * 8f) * 0.06f;
                        return Shade(c, 1f + grain);
                    });
                case FloorStyle.Tile:
                    return Make("floor_tile", (x, y) =>
                    {
                        bool grout = x % 64 < 2 || y % 64 < 2;
                        var c = grout ? C(0x9a9a92) : C(0xe8e4d8);
                        return Shade(c, 0.95f + Hash(x / 64, y / 64, 12) * 0.08f + Fbm(x, y, 13) * 0.05f);
                    });
                case FloorStyle.Checker:
                    return Make("floor_checker", (x, y) =>
                    {
                        bool w = ((x / 64) + (y / 64)) % 2 == 0;
                        var c = w ? C(0xece8e0) : C(0x2d3240);
                        if (x % 64 < 1 || y % 64 < 1) c = C(0x888888);
                        return Shade(c, 0.95f + Fbm(x, y, 14) * 0.08f);
                    });
                case FloorStyle.Carpet:
                    return Make("floor_carpet", (x, y) => Shade(C(0x8a6a9a), 0.85f + Fbm(x, y, 15) * 0.25f + Hash(x, y, 16) * 0.06f));
                case FloorStyle.Grass:
                default:
                    return Make("floor_grass", (x, y) =>
                    {
                        float n = Fbm(x, y, 17);
                        var c = Lerp(C(0x3f7a2c), C(0x6aa845), n);
                        if (Hash(x, y, 18) > 0.93f) c = Shade(c, 1.25f);
                        if (Hash(x / 4, y / 4, 19) > 0.985f) c = C(0xf2e27a); // tiny flowers
                        return c;
                    });
            }
        }

        public static ImageTexture Ceiling() => Make("ceiling", (x, y) => Shade(C(0xefe9dc), 0.92f + Fbm(x, y, 20) * 0.1f));
        public static ImageTexture Baseboard() => Make("baseboard", (x, y) => Shade(C(0xf4efe4), y % 64 < 6 ? 0.8f : 1f));

        /// <summary>
        /// Lit material with the texture projected in world space (triplanar), so floors and walls tile
        /// seamlessly whatever their size. <paramref name="tile"/> = world units per texture repeat.
        /// </summary>
        public static StandardMaterial3D Mat(ImageTexture tex, float glossiness = 0.1f, float tile = 6f)
        {
            string key = tex.ResourceName + glossiness + "/" + tile;
            if (mats.TryGetValue(key, out var m)) return m;
            m = new StandardMaterial3D
            {
                AlbedoTexture = tex,
                Roughness = 1f - glossiness,
                Uv1Triplanar = true,
                Uv1WorldTriplanar = true,
                Uv1Scale = Vector3.One / tile,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            };
            mats[key] = m;
            return m;
        }
    }
}
