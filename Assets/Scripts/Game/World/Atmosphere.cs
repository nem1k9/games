using Gnomes.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gnomes.World
{
    /// <summary>Lighting moods: moonlit night in the house, candle-lit village under the porch.</summary>
    public static class Atmosphere
    {
        public static Color NightSky = new Color(0.03f, 0.04f, 0.1f);
        public static float DarkVisionBoost;

        public static void Night(GameWorld w)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.16f, 0.25f) * (1f + DarkVisionBoost);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = NightSky;
            RenderSettings.fogDensity = 0.0035f;
            RenderSettings.skybox = null;
            QualitySettings.shadowDistance = 90f;
            QualitySettings.pixelLightCount = 8;
            var moon = Sun(w, "Moon", new Color(0.6f, 0.7f, 1f), 0.55f, Quaternion.Euler(32f, 200f, 0f));
            Sky(w, moon.transform.forward);
        }

        public static void Village(GameWorld w)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.16f, 0.14f) * (1f + DarkVisionBoost);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.04f, 0.05f);
            RenderSettings.fogDensity = 0.008f;
            QualitySettings.shadowDistance = 40f;
            QualitySettings.pixelLightCount = 8;
            // moonlight slipping through the porch planks
            Sun(w, "Moonbeams", new Color(0.55f, 0.65f, 1f), 0.5f, Quaternion.Euler(70f, 30f, 0f));
        }

        static Light Sun(GameWorld w, string name, Color c, float intensity, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(w.transform, false);
            go.transform.rotation = rot;
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = c;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.85f;
            l.shadowBias = 0.05f;
            l.shadowNormalBias = 0.3f;
            return l;
        }

        /// <summary>Stars (a point mesh) and a big friendly moon.</summary>
        static void Sky(GameWorld w, Vector3 moonDir)
        {
            var rng = new System.Random(42);
            int n = 700;
            var verts = new Vector3[n];
            var idx = new int[n];
            var cols = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                float u = (float)rng.NextDouble(), v = (float)rng.NextDouble();
                float theta = u * Mathf.PI * 2f, y = 0.08f + v * 0.92f;
                float r = Mathf.Sqrt(1 - y * y);
                verts[i] = new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r) * 300f;
                idx[i] = i;
                byte b = (byte)(160 + rng.Next(95));
                cols[i] = new Color32(b, b, (byte)Mathf.Min(255, b + 20), 255);
            }
            var m = new Mesh { name = "stars" };
            m.vertices = verts;
            m.colors32 = cols;
            m.SetIndices(idx, MeshTopology.Points, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 700f);
            var go = new GameObject("Stars");
            go.transform.SetParent(w.transform, false);
            go.transform.position = new Vector3(28, 0, 40);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ModelLibrary.UnlitColor(new Color(0.9f, 0.92f, 1f));
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(moon.GetComponent<Collider>());
            moon.name = "MoonDisc";
            moon.transform.SetParent(w.transform, false);
            moon.transform.position = go.transform.position - moonDir * 260f;
            moon.transform.localScale = Vector3.one * 26f;
            moon.GetComponent<MeshRenderer>().sharedMaterial = ModelLibrary.UnlitColor(new Color(0.95f, 0.93f, 0.8f));
            moon.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
