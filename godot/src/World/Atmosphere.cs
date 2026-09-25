using Godot;
using SockGang.Rendering;

namespace SockGang.World
{
    /// <summary>Lighting moods: moonlit night in the house, candle-lit village under the porch.</summary>
    public static class Atmosphere
    {
        public static Color NightSky = new Color(0.03f, 0.04f, 0.1f);
        public static float DarkVisionBoost;

        static Godot.Environment BaseEnv(Color bg, Color ambient, float fogDensity, Color fog)
        {
            return new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = bg,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = ambient,
                AmbientLightEnergy = 1f + DarkVisionBoost,
                FogEnabled = fogDensity > 0,
                FogLightColor = fog,
                FogDensity = fogDensity,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
                TonemapExposure = 1.05f,
                GlowEnabled = true,
                GlowIntensity = 0.6f,
                GlowBloom = 0.05f,
                GlowHdrThreshold = 1.1f,
                SsaoEnabled = true,
                SsaoRadius = 0.8f,
                SsaoIntensity = 1.2f,
            };
        }

        public static void Night(GameWorld w)
        {
            var env = new WorldEnvironment { Name = "Environment", Environment = BaseEnv(NightSky, new Color(0.3f, 0.32f, 0.48f), 0.0012f, NightSky) };
            w.AddChild(env);
            var moon = Sun(w, "Moon", new Color(0.6f, 0.7f, 1f), 0.55f, new Vector3(-32f, 160f, 0f));
            Sky(w, -moon.GlobalBasis.Z);
        }

        public static void Village(GameWorld w)
        {
            var env = new WorldEnvironment { Name = "Environment", Environment = BaseEnv(new Color(0.05f, 0.04f, 0.05f), new Color(0.36f, 0.3f, 0.27f), 0.004f, new Color(0.05f, 0.04f, 0.05f)) };
            w.AddChild(env);
            // moonlight slipping through the porch planks
            Sun(w, "Moonbeams", new Color(0.55f, 0.65f, 1f), 0.5f, new Vector3(-70f, -30f, 0f));
        }

        static DirectionalLight3D Sun(GameWorld w, string name, Color c, float energy, Vector3 rotDeg)
        {
            var l = new DirectionalLight3D { Name = name, LightColor = c, LightEnergy = energy, ShadowEnabled = true, ShadowBias = 0.05f, ShadowNormalBias = 1.5f };
            l.DirectionalShadowMaxDistance = 90f;
            w.AddChild(l);
            l.RotationDegrees = rotDeg;
            return l;
        }

        /// <summary>Stars (a point mesh) and a big friendly moon.</summary>
        static void Sky(GameWorld w, Vector3 lightDir)
        {
            var rng = new System.Random(42);
            int n = 700;
            var verts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float u = (float)rng.NextDouble(), v = (float)rng.NextDouble();
                float theta = u * Mathf.Tau, y = 0.08f + v * 0.92f;
                float r = Mathf.Sqrt(1 - y * y);
                verts[i] = new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r) * 300f;
            }
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = verts;
            var m = new ArrayMesh();
            m.AddSurfaceFromArrays(Mesh.PrimitiveType.Points, arrays);
            var starMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.92f, 1f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, UsePointSize = true, PointSize = 2f, DisableFog = true };
            var stars = new MeshInstance3D { Name = "Stars", Mesh = m, MaterialOverride = starMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 700f };
            w.AddChild(stars);
            stars.Position = new Vector3(28, 0, -40);
            var moonMat = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.93f, 0.8f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, DisableFog = true };
            var moon = new MeshInstance3D { Name = "MoonDisc", Mesh = new SphereMesh { Radius = 13f, Height = 26f }, MaterialOverride = moonMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            w.AddChild(moon);
            moon.Position = stars.Position - lightDir * 260f;
        }
    }
}
