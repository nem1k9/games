using System.Collections.Generic;
using System.Globalization;
using Gnomes.Audio;
using Gnomes.Core.Level;
using Gnomes.Rendering;
using UnityEngine;

namespace Gnomes.World
{
    /// <summary>A placed piece of furniture plus its special behaviour (TV, oven, taps, clock...).</summary>
    public class Furniture : MonoBehaviour
    {
        public ushort Index;
        public string Kind;
        public Placement Placement;
        public ModelInstance Model;
        public readonly List<Mechanism> Mechs = new List<Mechanism>();
        public readonly Dictionary<string, BoxCollider> Zones = new Dictionary<string, BoxCollider>();
        public readonly Dictionary<string, Transform> Anchors = new Dictionary<string, Transform>();
        public readonly List<Light> Lights = new List<Light>();
        public readonly List<Transform> Surfaces = new List<Transform>();
        public float Hp;
        public bool Broken;
        public bool Hot; // oven heating (set by the old man)
        public float WaterLevel; // bathtub

        Transform screen, pendulum, fire, fish, water, ovenGlow, fridgeLight;
        Renderer screenRenderer;
        Material[] screenOff;
        Light tvLight;
        float waterBase;
        float lastHourChime = -1;
        static Material tvOnMat, tvBrokenMat;

        public string Id => Placement?.Id;

        public void Init(ushort index, Placement placement, ModelInstance model)
        {
            Index = index;
            Placement = placement;
            Model = model;
            Kind = placement.Model;
            var root = model.Model.Root;
            if (root.Props.TryGetValue("hp", out var hp)) float.TryParse(hp, NumberStyles.Float, CultureInfo.InvariantCulture, out Hp);
            foreach (var z in model.Markers("ZONE"))
            {
                var bc = z.GetComponent<BoxCollider>();
                if (bc) Zones[z.name.Substring(5)] = bc;
            }
            foreach (var a in model.Markers("ANCHOR")) Anchors[a.name.Substring(7)] = a;
            var surfs = model.Markers("SURF");
            surfs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            Surfaces.AddRange(surfs);
            foreach (var l in model.Markers("LIGHT"))
            {
                var node = model.Model.Find(l.name);
                var light = l.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.color = ParseColor(node.Props.TryGetValue("color", out var c) ? c : "ffe0b0");
                light.range = Prop(node.Props, "range", 12f);
                light.intensity = Prop(node.Props, "intensity", 1f) * 1.4f;
                bool on = Prop(node.Props, "on", 1f) > 0.5f;
                if (placement.Params.TryGetValue("on", out var pon)) on = pon > 0.5f;
                light.enabled = on;
                Lights.Add(light);
                // lamp bulbs look off when the light is off
            }
            screen = model.Node("screen");
            pendulum = model.Node("pendulum");
            fire = model.Node("fire");
            fish = model.Node("fish");
            water = model.Node("water");
            ovenGlow = model.Node("ovenGlow");
            fridgeLight = model.Node("fridgeLight");
            if (screen) screenRenderer = screen.GetComponent<Renderer>();
            if (screenRenderer) screenOff = screenRenderer.sharedMaterials;
            if (water) waterBase = water.localPosition.y;
            if (ovenGlow) ovenGlow.gameObject.SetActive(false);
            if (Kind == "tv")
            {
                var go = new GameObject("tvLight");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0, 1.1f, 1.6f);
                tvLight = go.AddComponent<Light>();
                tvLight.type = LightType.Point;
                tvLight.color = new Color(0.55f, 0.75f, 1f);
                tvLight.range = 14f;
                tvLight.intensity = 0;
                tvLight.shadows = LightShadows.None;
            }
        }

        static float Prop(Dictionary<string, string> p, string k, float d) =>
            p.TryGetValue(k, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : d;

        static Color ParseColor(string hex)
        {
            int rgb = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
        }

        public Mechanism Mech(string role)
        {
            foreach (var m in Mechs) if (m.Role == role) return m;
            return null;
        }

        public Mechanism MechByName(string nodeName)
        {
            foreach (var m in Mechs) if (m.name == nodeName) return m;
            return null;
        }

        public bool TvOn => Kind == "tv" && !Broken && Mechs.Count > 0 && Mechs[0].IsOpen;

        void Update()
        {
            float t = Time.time;
            switch (Kind)
            {
                case "tv":
                    UpdateTv(t);
                    break;
                case "grandfatherClock":
                    if (pendulum) pendulum.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI) * 12f);
                    ClockChime();
                    break;
                case "fireplace":
                    if (fire) fire.localScale = new Vector3(1f + Mathf.Sin(t * 7) * 0.05f, 1f + Mathf.PerlinNoise(t * 3, 0) * 0.3f, 1f);
                    foreach (var l in Lights) l.intensity = 1.4f * (0.85f + Mathf.PerlinNoise(t * 5f, 1.3f) * 0.35f);
                    break;
                case "fishTank":
                    if (fish) fish.localRotation = Quaternion.Euler(0, t * 40f, 0);
                    break;
                case "bathtub":
                    var tap = Mech("tubFaucet");
                    if (tap != null && tap.IsOpen) WaterLevel = Mathf.Min(1f, WaterLevel + Time.deltaTime / 40f);
                    if (water) water.localPosition = new Vector3(water.localPosition.x, waterBase + WaterLevel * 1.1f, water.localPosition.z);
                    break;
                case "stove":
                    if (ovenGlow) ovenGlow.gameObject.SetActive(Hot);
                    break;
                case "fridge":
                    var door = Mech("fridge");
                    bool open = door != null && door.Current > 0.1f;
                    if (fridgeLight) fridgeLight.gameObject.SetActive(open);
                    break;
            }
        }

        void UpdateTv(float t)
        {
            bool on = TvOn;
            if (screenRenderer)
            {
                if (tvOnMat == null)
                {
                    tvOnMat = new Material(ModelLibrary.EmissiveMaterial) { name = "TvOn", mainTexture = null, color = new Color(0.7f, 0.85f, 1f) };
                    tvBrokenMat = new Material(ModelLibrary.LitMaterial) { name = "TvBroken", mainTexture = null, color = new Color(0.05f, 0.05f, 0.06f) };
                }
                if (Broken) screenRenderer.sharedMaterial = tvBrokenMat;
                else if (on)
                {
                    screenRenderer.sharedMaterial = tvOnMat;
                    float flick = 0.6f + Mathf.PerlinNoise(t * 6f, 0) * 0.5f;
                    tvOnMat.color = new Color(0.55f * flick, 0.75f * flick, 1f * flick);
                }
                else screenRenderer.sharedMaterials = screenOff;
            }
            if (tvLight) tvLight.intensity = on ? 1.6f * (0.6f + Mathf.PerlinNoise(t * 6f, 2f) * 0.6f) : 0f;
        }

        void ClockChime()
        {
            var w = GameWorld.Current;
            if (w == null || !w.Authority || w.Night == null) return;
            int hour = (int)(w.Night.Progress01 * 6f);
            if (hour != lastHourChime)
            {
                if (lastHourChime >= 0)
                {
                    w.EmitSound(SoundId.Chime, transform.position + Vector3.up * 7f, 1f);
                    w.Noise(transform.position, 30f, NoiseKind.Clock);
                }
                lastHourChime = hour;
            }
        }

        /// <summary>Host: damage breakable furniture (the TV). Returns true when it breaks now.</summary>
        public bool Damage(float amount)
        {
            if (Broken || Hp <= 0) return false;
            Hp -= amount;
            if (Hp > 0) return false;
            Broken = true;
            return true;
        }

        public void SetBroken()
        {
            Broken = true;
            Hp = 0;
        }

        public Vector3 AnchorPos(string name, Vector3 fallback)
        {
            return Anchors.TryGetValue(name, out var a) ? a.position : fallback;
        }
    }
}
