using System.Collections.Generic;
using System.Globalization;
using Gnomes.Core.Level;
using Godot;
using SockGang.Audio;
using SockGang.Rendering;

namespace SockGang.World
{
    /// <summary>A placed piece of furniture plus its special behaviour (TV, clock, taps, lamps...).</summary>
    public partial class Furniture : Node3D
    {
        public ushort Index;
        public string Kind;
        public Placement Placement;
        public ModelInstance Model;
        public readonly List<Mechanism> Mechs = new List<Mechanism>();
        public readonly Dictionary<string, Node3D> Zones = new Dictionary<string, Node3D>();
        public readonly Dictionary<string, Node3D> Anchors = new Dictionary<string, Node3D>();
        public readonly List<OmniLight3D> Lights = new List<OmniLight3D>();
        public readonly List<Node3D> Surfaces = new List<Node3D>();
        public float Hp;
        public bool Broken;
        public float WaterLevel; // bathtub

        Node3D screen, pendulum, fire, fish, water, fridgeLight;
        MeshInstance3D screenMesh;
        OmniLight3D tvLight;
        float waterBase;
        float lastHourChime = -1;
        static StandardMaterial3D tvOnMat, tvBrokenMat;

        public string Id => Placement?.Id;

        public void Init(Placement placement, ModelInstance model)
        {
            Placement = placement;
            Model = model;
            Kind = placement.Model;
            var root = model.Model?.Root;
            if (root != null && root.Props.TryGetValue("hp", out var hp)) float.TryParse(hp, NumberStyles.Float, CultureInfo.InvariantCulture, out Hp);
            foreach (var z in model.Markers("ZONE")) Zones[model.NameOf(z).Substring(5)] = z;
            foreach (var a in model.Markers("ANCHOR")) Anchors[model.NameOf(a).Substring(7)] = a;
            var surfs = model.Markers("SURF");
            surfs.Sort((a, b) => string.CompareOrdinal(model.NameOf(a), model.NameOf(b)));
            Surfaces.AddRange(surfs);
            foreach (var l in model.Markers("LIGHT"))
            {
                var node = model.Model.Find(model.NameOf(l));
                var light = MarkerLight(node?.Props, l);
                bool on = Prop(node?.Props, "on", 1f) > 0.5f;
                if (placement.Params.TryGetValue("on", out var pon)) on = pon > 0.5f;
                light.Visible = on;
                Lights.Add(light);
            }
            screen = model.Node("screen");
            pendulum = model.Node("pendulum");
            fire = model.Node("fire");
            fish = model.Node("fish");
            water = model.Node("water");
            fridgeLight = model.Node("fridgeLight");
            screenMesh = screen as MeshInstance3D;
            if (water != null) waterBase = water.Position.Y;
            if (Kind == "tv")
            {
                tvLight = new OmniLight3D { Name = "tvLight", LightColor = new Color(0.55f, 0.75f, 1f), OmniRange = 14f, LightEnergy = 0, ShadowEnabled = false, Position = new Vector3(0, 1.1f, -1.6f) };
                AddChild(tvLight);
            }
        }

        /// <summary>A point light for a LIGHT_ marker (colour/range/intensity from the Blender node props).</summary>
        public static OmniLight3D MarkerLight(Dictionary<string, string> props, Node3D at, float energyScale = 1f)
        {
            var light = new OmniLight3D { Name = "Light", ShadowEnabled = false };
            light.LightColor = props != null && props.TryGetValue("color", out var c) ? Conv.C(int.Parse(c, NumberStyles.HexNumber, CultureInfo.InvariantCulture)) : new Color(1f, 0.88f, 0.7f);
            light.OmniRange = Prop(props, "range", 12f);
            light.LightEnergy = Prop(props, "intensity", 1f) * 1.4f * energyScale;
            light.OmniAttenuation = 1.2f;
            at.AddChild(light);
            // the marker may be scaled; keep the light itself unscaled
            light.Scale = new Vector3(1f / Mathf.Max(0.001f, at.Scale.X), 1f / Mathf.Max(0.001f, at.Scale.Y), 1f / Mathf.Max(0.001f, at.Scale.Z));
            return light;
        }

        static float Prop(Dictionary<string, string> p, string k, float d) =>
            p != null && p.TryGetValue(k, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : d;

        public Mechanism Mech(string role)
        {
            foreach (var m in Mechs) if (m.Role == role) return m;
            return null;
        }

        public bool TvOn => Kind == "tv" && !Broken && Mechs.Count > 0 && Mechs[0].IsOpen;

        public override void _Process(double delta)
        {
            float t = Clock.Now;
            switch (Kind)
            {
                case "tv":
                    UpdateTv(t);
                    break;
                case "grandfatherClock":
                    if (pendulum != null) pendulum.Rotation = new Vector3(0, 0, -Mathf.Sin(t * Mathf.Pi) * 0.21f);
                    ClockChime();
                    break;
                case "fireplace":
                    if (fire != null) fire.Scale = new Vector3(1f + Mathf.Sin(t * 7) * 0.05f, 1f + (Mathf.Sin(t * 3.1f) * 0.5f + Mathf.Sin(t * 7.3f) * 0.3f + 0.8f) * 0.17f, 1f);
                    foreach (var l in Lights) l.LightEnergy = 1.4f * (0.85f + (Mathf.Sin(t * 5.3f) * 0.5f + 0.5f) * 0.35f);
                    break;
                case "fishTank":
                    if (fish != null) fish.Rotation = new Vector3(0, -t * 0.7f, 0);
                    break;
                case "bathtub":
                    var tap = Mech("tubFaucet");
                    if (tap != null && tap.IsOpen) WaterLevel = Mathf.Min(1f, WaterLevel + (float)delta / 40f);
                    if (water != null) water.Position = new Vector3(water.Position.X, waterBase + WaterLevel * 1.1f, water.Position.Z);
                    break;
                case "fridge":
                    var door = Mech("fridge");
                    if (fridgeLight != null) fridgeLight.Visible = door != null && door.Current > 0.1f;
                    break;
            }
        }

        void UpdateTv(float t)
        {
            bool on = TvOn;
            if (screenMesh != null)
            {
                if (tvOnMat == null)
                {
                    tvOnMat = ModelLibrary.UnlitColor(new Color(0.7f, 0.85f, 1f));
                    tvBrokenMat = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.05f, 0.06f), Roughness = 0.3f };
                }
                if (Broken) screenMesh.MaterialOverride = tvBrokenMat;
                else if (on)
                {
                    screenMesh.MaterialOverride = tvOnMat;
                    float flick = 0.6f + (Mathf.Sin(t * 13f) * 0.3f + Mathf.Sin(t * 5.1f) * 0.2f + 0.5f) * 0.5f;
                    tvOnMat.AlbedoColor = new Color(0.55f * flick, 0.75f * flick, 1f * flick);
                }
                else screenMesh.MaterialOverride = null;
            }
            if (tvLight != null) tvLight.LightEnergy = on ? 1.6f * (0.6f + (Mathf.Sin(t * 9f) * 0.5f + 0.5f) * 0.6f) : 0f;
        }

        void ClockChime()
        {
            var w = GameWorld.Current;
            if (w == null || !w.Authority || w.Night == null) return;
            int hour = (int)(w.Night.Progress01 * 6f);
            if (hour == lastHourChime) return;
            if (lastHourChime >= 0)
            {
                w.EmitSound(SoundId.Chime, GlobalPosition + Vector3.Up * 7f, 1f);
                w.Noise(GlobalPosition, 30f, NoiseKind.Clock);
            }
            lastHourChime = hour;
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

        public bool IsLamp => Lights.Count > 0 && Kind.Contains("amp") && Kind != "ceilingLamp";
        public bool LightsOn => Lights.Count > 0 && Lights[0].Visible;

        public void SetLights(bool on)
        {
            foreach (var l in Lights) if (l != null) l.Visible = on;
        }

        public void SetBroken()
        {
            Broken = true;
            Hp = 0;
        }

        public Vector3 AnchorPos(string name, Vector3 fallback) => Anchors.TryGetValue(name, out var a) ? a.GlobalPosition : fallback;
    }
}
