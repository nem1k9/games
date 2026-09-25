using System;
using System.Collections.Generic;
using Gnomes.Core.Models;
using Godot;

namespace SockGang.Rendering
{
    /// <summary>
    /// Materials for model surfaces: the palette colour, baked ambient occlusion (vertex colour R) and a
    /// procedurally generated, tileable detail height map per surface kind (knit, fabric, fur, wood...).
    /// The detail is projected tri-planar in object space and turned into bumps with screen-space
    /// derivatives, so meshes need no UV unwrap or tangents.
    /// </summary>
    public static class Surfaces
    {
        const int N = 256;

        const string ShaderCode = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D palette : source_color, filter_nearest;
uniform sampler2D detail : filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec4 tint : source_color = vec4(1.0);
uniform float use_tint = 0.0;
uniform float detail_scale = 4.0;
uniform float detail_albedo = 0.2;
uniform float detail_bump = 0.5;
uniform float rough = 0.85;
uniform float metal = 0.0;
uniform float spec = 0.35;
uniform float ao_strength = 0.8;
uniform float sheen = 0.0;

varying vec3 opos;
varying vec3 onrm;
varying float dscale;

void vertex() {
    opos = VERTEX;
    onrm = NORMAL;
    // per-model detail size: alpha 128 = x1, +32 per doubling (the stitches of a giant sock are big)
    dscale = detail_scale / exp2((COLOR.a * 255.0 - 128.0) / 32.0);
}

float height(vec3 p, vec3 n) {
    vec3 w = pow(abs(n), vec3(6.0));
    w /= (w.x + w.y + w.z + 1e-5);
    vec3 q = p * dscale;
    return texture(detail, q.zy).r * w.x + texture(detail, q.xz).r * w.y + texture(detail, q.xy).r * w.z;
}

void fragment() {
    vec3 base = mix(texture(palette, UV).rgb, tint.rgb, use_tint);
    float h = height(opos, normalize(onrm));
    float ao = mix(1.0, COLOR.r, ao_strength);
    ALBEDO = base * clamp(1.0 + (h - 0.5) * 2.0 * detail_albedo, 0.0, 2.0) * mix(1.0, ao, 0.65);
    // bump mapping on an unparametrised surface (Mikkelsen 2010): height gradient from screen derivatives
    float amp = detail_bump / dscale;
    vec3 dpdx = dFdx(VERTEX);
    vec3 dpdy = dFdy(VERTEX);
    vec3 r1 = cross(dpdy, NORMAL);
    vec3 r2 = cross(NORMAL, dpdx);
    float det = dot(dpdx, r1);
    vec3 grad = sign(det) * (dFdx(h) * amp * r1 + dFdy(h) * amp * r2);
    NORMAL = normalize(abs(det) * NORMAL - grad);
    ROUGHNESS = rough;
    METALLIC = metal;
    SPECULAR = spec;
    AO = ao;
    AO_LIGHT_AFFECT = 0.25;
    RIM = sheen;
    RIM_TINT = 0.6;
}
";

        struct Look
        {
            public string Tex;
            public float Scale, Albedo, Bump, Rough, Metal, Spec, Sheen;
        }

        static readonly Dictionary<MeshKind, Look> looks = new Dictionary<MeshKind, Look>
        {
            [MeshKind.Lit] = new Look { Tex = "paint", Scale = 1.2f, Albedo = 0.07f, Bump = 0.15f, Rough = 0.78f, Spec = 0.3f },
            [MeshKind.Tint] = new Look { Tex = "knit", Scale = 3.6f, Albedo = 0.22f, Bump = 0.9f, Rough = 0.95f, Spec = 0.2f, Sheen = 0.25f },
            [MeshKind.Knit] = new Look { Tex = "knit", Scale = 3.6f, Albedo = 0.22f, Bump = 0.9f, Rough = 0.95f, Spec = 0.2f, Sheen = 0.25f },
            [MeshKind.Fabric] = new Look { Tex = "fabric", Scale = 7f, Albedo = 0.12f, Bump = 0.5f, Rough = 0.92f, Spec = 0.25f, Sheen = 0.2f },
            [MeshKind.Fur] = new Look { Tex = "fur", Scale = 3.2f, Albedo = 0.28f, Bump = 0.8f, Rough = 1f, Spec = 0.15f, Sheen = 0.35f },
            [MeshKind.Skin] = new Look { Tex = "skin", Scale = 3f, Albedo = 0.06f, Bump = 0.12f, Rough = 0.6f, Spec = 0.35f, Sheen = 0.1f },
            [MeshKind.Hair] = new Look { Tex = "hair", Scale = 4f, Albedo = 0.18f, Bump = 0.6f, Rough = 0.9f, Spec = 0.3f, Sheen = 0.2f },
            [MeshKind.Wood] = new Look { Tex = "wood", Scale = 1.3f, Albedo = 0.2f, Bump = 0.2f, Rough = 0.62f, Spec = 0.35f },
            [MeshKind.Metal] = new Look { Tex = "brushed", Scale = 1.4f, Albedo = 0.08f, Bump = 0.08f, Rough = 0.38f, Metal = 0.35f, Spec = 0.6f },
            [MeshKind.Glossy] = new Look { Tex = "paint", Scale = 1f, Albedo = 0.03f, Bump = 0.04f, Rough = 0.22f, Spec = 0.6f },
            [MeshKind.Leather] = new Look { Tex = "leather", Scale = 5f, Albedo = 0.12f, Bump = 0.35f, Rough = 0.55f, Spec = 0.4f },
            [MeshKind.Stone] = new Look { Tex = "stone", Scale = 0.9f, Albedo = 0.3f, Bump = 0.6f, Rough = 0.95f, Spec = 0.2f },
        };

        static Shader shader;
        static readonly Dictionary<string, ImageTexture> textures = new Dictionary<string, ImageTexture>();
        static readonly Dictionary<MeshKind, ShaderMaterial> materials = new Dictionary<MeshKind, ShaderMaterial>();
        static readonly Dictionary<(MeshKind, int), ShaderMaterial> tinted = new Dictionary<(MeshKind, int), ShaderMaterial>();

        public static bool Handles(MeshKind kind) => looks.ContainsKey(kind);

        /// <summary>Shared material of a palette-coloured surface kind.</summary>
        public static ShaderMaterial For(MeshKind kind, Texture2D palette)
        {
            if (!looks.ContainsKey(kind)) kind = MeshKind.Lit;
            if (materials.TryGetValue(kind, out var m)) return m;
            m = Make(kind);
            m.SetShaderParameter("palette", palette);
            materials[kind] = m;
            return m;
        }

        /// <summary>Shared material of a surface recoloured at runtime (gnome hats, yarn).</summary>
        public static ShaderMaterial Tinted(MeshKind kind, Color c)
        {
            if (!looks.ContainsKey(kind)) kind = MeshKind.Lit;
            var key = (kind, unchecked((int)c.ToRgba32()));
            if (tinted.TryGetValue(key, out var m)) return m;
            m = Make(kind);
            m.SetShaderParameter("tint", c);
            m.SetShaderParameter("use_tint", 1f);
            tinted[key] = m;
            return m;
        }

        static ShaderMaterial Make(MeshKind kind)
        {
            shader ??= new Shader { Code = ShaderCode };
            var l = looks[kind];
            var m = new ShaderMaterial { Shader = shader, ResourceName = "Surface" + kind };
            m.SetShaderParameter("detail", Texture(l.Tex));
            m.SetShaderParameter("detail_scale", l.Scale);
            m.SetShaderParameter("detail_albedo", l.Albedo);
            m.SetShaderParameter("detail_bump", l.Bump);
            m.SetShaderParameter("rough", l.Rough);
            m.SetShaderParameter("metal", l.Metal);
            m.SetShaderParameter("spec", l.Spec);
            m.SetShaderParameter("sheen", l.Sheen);
            return m;
        }

        // ------------------------------------------------------------------ detail height maps

        /// <summary>Tileable grey-scale height map (0..1) by name.</summary>
        public static ImageTexture Texture(string name)
        {
            if (textures.TryGetValue(name, out var t)) return t;
            Func<int, int, float> f = name switch
            {
                "knit" => Knit,
                "fabric" => Fabric,
                "fur" => (x, y) => Strands(x, y, 64, 6, 11, 0.55f),
                "hair" => (x, y) => Strands(x, y, 48, 3, 12, 0.7f),
                "skin" => Skin,
                "wood" => Wood,
                "brushed" => (x, y) => 0.5f + (NoiseXY(x / (float)N * 128, y / (float)N * 2, 128, 2, 31) - 0.5f) * 0.6f + (Fbm(x, y, 32) - 0.5f) * 0.2f,
                "leather" => Leather,
                "stone" => Stone,
                _ => (x, y) => 0.35f + Fbm(x, y, 40) * 0.3f,
            };
            var data = new byte[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    data[y * N + x] = (byte)Math.Clamp(f(x, y) * 255f, 0f, 255f);
            var img = Image.CreateFromData(N, N, false, Image.Format.L8, data);
            img.GenerateMipmaps();
            t = ImageTexture.CreateFromImage(img);
            textures[name] = t;
            return t;
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
            }
        }

        static float Smooth(float t) => t * t * (3 - 2 * t);

        /// <summary>Value noise, periodic in x (px cells) and y (py cells).</summary>
        static float NoiseXY(float x, float y, int px, int py, int seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = Smooth(x - x0), fy = Smooth(y - y0);
            int X0 = ((x0 % px) + px) % px, Y0 = ((y0 % py) + py) % py;
            int X1 = (X0 + 1) % px, Y1 = (Y0 + 1) % py;
            float a = Hash(X0, Y0, seed), b = Hash(X1, Y0, seed), c = Hash(X0, Y1, seed), d = Hash(X1, Y1, seed);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        static float Fbm(int x, int y, int seed, int period = 8, int octaves = 4)
        {
            float v = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                v += amp * NoiseXY(x * period / (float)N, y * period / (float)N, period, period, seed + o);
                norm += amp;
                period *= 2;
                amp *= 0.5f;
            }
            return v / norm;
        }

        /// <summary>Distance to the nearest jittered cell point (tileable Worley noise), 0..~1.</summary>
        static float Worley(int x, int y, int cells, int seed)
        {
            float cs = N / (float)cells;
            float fx = x / cs, fy = y / cs;
            int cx = (int)Math.Floor(fx), cy = (int)Math.Floor(fy);
            float best = 9f;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int gx = cx + i, gy = cy + j;
                    int wx = ((gx % cells) + cells) % cells, wy = ((gy % cells) + cells) % cells;
                    float px = gx + Hash(wx, wy, seed), py = gy + Hash(wx, wy, seed + 7);
                    float d = (px - fx) * (px - fx) + (py - fy) * (py - fy);
                    if (d < best) best = d;
                }
            return (float)Math.Sqrt(best);
        }

        /// <summary>Stockinette knitting: columns of V-shaped stitches made of two slanted loops.</summary>
        static float Knit(int x, int y)
        {
            const int cols = 8, rows = 10;
            float u = x / (float)N * cols, v = y / (float)N * rows;
            int cu = (int)Math.Floor(u);
            float h = 0;
            for (int dv = -1; dv <= 1; dv++)
            {
                int cv = (int)Math.Floor(v) + dv;
                for (int side = -1; side <= 1; side += 2)
                {
                    float cx = cu + 0.5f + side * 0.23f, cy = cv + 0.5f;
                    float dx = u - cx, dy = v - cy;
                    float a = side * 0.55f; // legs lean apart towards the top: a V
                    float rx = dx * (float)Math.Cos(a) - dy * (float)Math.Sin(a);
                    float ry = dx * (float)Math.Sin(a) + dy * (float)Math.Cos(a);
                    float r = (rx / 0.21f) * (rx / 0.21f) + (ry / 0.58f) * (ry / 0.58f);
                    if (r < 1f) h = Math.Max(h, (float)Math.Sqrt(1f - r));
                }
            }
            float fibre = NoiseXY(x / (float)N * 96, y / (float)N * 24, 96, 24, 3);
            return 0.12f + h * 0.78f + (fibre - 0.5f) * 0.14f;
        }

        /// <summary>Plain weave: warp and weft threads alternately on top.</summary>
        static float Fabric(int x, int y)
        {
            const int t = 8; // pixels per thread
            int i = x / t, j = y / t;
            float fx = (x % t + 0.5f) / t, fy = (y % t + 0.5f) / t;
            bool warp = ((i + j) & 1) == 0;
            float across = warp ? fx : fy;
            float along = warp ? fy : fx;
            float h = (float)Math.Sin(across * Math.PI) * (0.75f + 0.25f * (float)Math.Sin(along * Math.PI));
            return 0.1f + h * 0.7f + (Fbm(x, y, 5, 16, 3) - 0.5f) * 0.25f;
        }

        /// <summary>Short soft strands leaning one way (fur, hair).</summary>
        static float Strands(int x, int y, int across, int along, int seed, float contrast)
        {
            float a = NoiseXY(x / (float)N * across, y / (float)N * along, across, along, seed);
            float b = NoiseXY(x / (float)N * across * 2, y / (float)N * along * 2, across * 2, along * 2, seed + 1);
            float v = a * 0.65f + b * 0.35f;
            return 0.5f + (v - 0.5f) * 2f * contrast;
        }

        static float Skin(int x, int y)
        {
            float pores = Worley(x, y, 48, 5);
            return 0.5f + (Fbm(x, y, 9, 4, 3) - 0.5f) * 0.5f + (pores < 0.18f ? -0.2f : 0f);
        }

        /// <summary>Wood grain: wavy growth lines plus fine fibres.</summary>
        static float Wood(int x, int y)
        {
            float warp = Fbm(x, y, 17, 2, 3);
            float lines = y / (float)N * 7f + warp * 2.5f; // integer multiples keep it tileable
            float ring = (float)(0.5 + 0.5 * Math.Sin(lines * Math.PI * 2));
            ring = (float)Math.Pow(ring, 4);
            float fibre = NoiseXY(x / (float)N * 8, y / (float)N * 128, 8, 128, 19);
            float fine = NoiseXY(x / (float)N * 16, y / (float)N * 256, 16, 256, 23);
            return 0.58f - ring * 0.3f + (fibre - 0.5f) * 0.28f + (fine - 0.5f) * 0.12f;
        }

        static float Leather(int x, int y)
        {
            float w = Worley(x, y, 24, 23);
            return 0.3f + Math.Min(w, 0.5f) * 1.1f + (Fbm(x, y, 29, 16, 2) - 0.5f) * 0.15f;
        }

        static float Stone(int x, int y)
        {
            float w = Worley(x, y, 6, 41);
            return 0.25f + Fbm(x, y, 43, 4, 5) * 0.6f + (w < 0.08f ? -0.15f : 0f);
        }
    }
}
