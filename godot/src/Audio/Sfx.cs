using System;
using System.Collections.Generic;
using Godot;

namespace SockGang.Audio
{
    public enum SoundId : byte
    {
        Footstep, Jump, Land, Grab, Throw, Punch, Clonk, Thud, Clink, Break, Squeak, Flush, Water, TvStatic,
        Click, Door, ArmPop, Honk, Pickup, Bank, TaskDone, Grumble, Shout, Snore, Meow, Hiss, Roomba, Oven,
        Revive, Death, Chime, Alarm, Coins, Splash, Ding, Rooster, Struggle, Gulp, Sparkle, Slurp, Squawk,
    }

    /// <summary>All sounds are synthesised at startup, so the game needs no audio assets.</summary>
    public partial class Sfx : Node3D
    {
        public static Sfx I;
        const int Rate = 22050;
        readonly Dictionary<SoundId, AudioStreamWav> clips = new Dictionary<SoundId, AudioStreamWav>();
        readonly List<AudioStreamPlayer3D> pool = new List<AudioStreamPlayer3D>();
        readonly List<AudioStreamPlayer> uiPool = new List<AudioStreamPlayer>();
        AudioStreamPlayer music, ambience;
        AudioStreamWav musicNight, musicHub, crickets;
        int next, nextUi, musicWhich = -1;
        public float Volume = 0.8f;

        static float Clamp01(float v) => Mathf.Clamp(v, 0f, 1f);
        static float Repeat(float t, float len) => t - Mathf.Floor(t / len) * len;

        public static Sfx Create(Node parent)
        {
            var s = new Sfx { Name = "Sfx" };
            parent.AddChild(s);
            I = s;
            s.Build();
            return s;
        }

        void Build()
        {
            foreach (SoundId id in Enum.GetValues(typeof(SoundId))) clips[id] = Make(id);
            for (int i = 0; i < 28; i++)
            {
                var a = new AudioStreamPlayer3D { Name = "src" + i, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance, UnitSize = 7f, MaxDistance = 70f, DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled };
                AddChild(a);
                pool.Add(a);
            }
            for (int i = 0; i < 4; i++)
            {
                var u = new AudioStreamPlayer { Name = "ui" + i };
                AddChild(u);
                uiPool.Add(u);
            }
            music = new AudioStreamPlayer { Name = "music" };
            AddChild(music);
            ambience = new AudioStreamPlayer { Name = "ambience" };
            AddChild(ambience);
            crickets = Loop(Clip("crickets", 4f, (t, i) => (Noise(i) * 0.05f) + (Mathf.Sin(t * 2 * Mathf.Pi * 4200) * (Mathf.Sin(t * 2 * Mathf.Pi * 18) > 0.6f ? 0.08f : 0f) * (Mathf.Sin(t * 2 * Mathf.Pi * 0.9f) > 0 ? 1 : 0))));
            ambience.Stream = crickets;
            musicNight = Loop(MakeMusic(false));
            musicHub = Loop(MakeMusic(true));
        }

        static AudioStreamWav Loop(AudioStreamWav w)
        {
            w.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            w.LoopBegin = 0;
            w.LoopEnd = w.Data.Length / 2;
            return w;
        }

        public void Play(SoundId id, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            if (!clips.TryGetValue(id, out var clip)) return;
            var a = pool[next];
            next = (next + 1) % pool.Count;
            a.GlobalPosition = pos;
            a.PitchScale = pitch * GMath.Rand(0.94f, 1.06f);
            a.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.0001f, Clamp01(volume * Volume)));
            a.MaxDistance = id == SoundId.Chime || id == SoundId.Alarm || id == SoundId.Rooster ? 140f : id == SoundId.Footstep ? 25f : 70f;
            a.Stream = clip;
            a.Play();
        }

        public void PlayUi(SoundId id, float volume = 1f)
        {
            if (!clips.TryGetValue(id, out var clip)) return;
            var u = uiPool[nextUi];
            nextUi = (nextUi + 1) % uiPool.Count;
            u.Stream = clip;
            u.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.0001f, volume * Volume));
            u.Play();
        }

        public void SetMusic(int which) // 0 none, 1 night, 2 hub
        {
            if (which == musicWhich) return;
            musicWhich = which;
            var c = which == 1 ? musicNight : which == 2 ? musicHub : null;
            music.Stream = c;
            if (c != null) music.Play();
            else music.Stop();
            if (which == 1) ambience.Play();
            else ambience.Stop();
        }

        public override void _Process(double delta)
        {
            music.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.0001f, 0.22f * Volume));
            ambience.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.0001f, 0.12f * Volume));
        }

        // ------------------------------------------------------------ synthesis

        static uint seed = 12345;
        static float Noise(int i)
        {
            unchecked
            {
                seed = seed * 1664525u + 1013904223u + (uint)i;
                return ((seed >> 9) & 0x7fff) / 16383.5f - 1f;
            }
        }

        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);
        static float Sine(float t, float f) => Mathf.Sin(2 * Mathf.Pi * f * t);
        static float Saw(float t, float f) => 2f * (t * f - Mathf.Floor(t * f + 0.5f));
        static float Square(float t, float f) => Mathf.Sin(2 * Mathf.Pi * f * t) >= 0 ? 1f : -1f;

        static AudioStreamWav Clip(string name, float seconds, Func<float, int, float> f)
        {
            int n = Mathf.Max(1, (int)(seconds * Rate));
            var data = new float[n];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float v = f(t, i);
                lp += (v - lp) * 0.6f; // tame harsh aliasing a bit
                data[i] = Mathf.Clamp(lp, -1f, 1f);
            }
            // tiny fade-out to avoid clicks
            int fade = Mathf.Min(200, n / 4);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;
            var pcm = new byte[n * 2];
            for (int i = 0; i < n; i++)
            {
                short s = (short)(data[i] * 32767f);
                pcm[i * 2] = (byte)(s & 0xff);
                pcm[i * 2 + 1] = (byte)((s >> 8) & 0xff);
            }
            return new AudioStreamWav { ResourceName = name, Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = pcm };
        }

        AudioStreamWav Make(SoundId id)
        {
            string name = id.ToString();
            switch (id)
            {
                case SoundId.Footstep: return Clip(name, 0.07f, (t, i) => Noise(i) * Env(t, 0.003f, 0.018f) * 0.5f);
                case SoundId.Jump: return Clip(name, 0.18f, (t, i) => Sine(t, 320 + t * 2600) * Env(t, 0.005f, 0.07f) * 0.5f);
                case SoundId.Land: return Clip(name, 0.2f, (t, i) => (Sine(t, 70) * 0.8f + Noise(i) * 0.3f) * Env(t, 0.003f, 0.05f) * 0.7f);
                case SoundId.Grab: return Clip(name, 0.12f, (t, i) => Noise(i) * Env(t, 0.01f, 0.04f) * 0.35f * (0.5f + Sine(t, 40)));
                case SoundId.Throw: return Clip(name, 0.3f, (t, i) => Noise(i) * Mathf.Sin(t / 0.3f * Mathf.Pi) * 0.4f);
                case SoundId.Punch: return Clip(name, 0.15f, (t, i) => (Noise(i) * 0.6f + Sine(t, 120) * 0.6f) * Env(t, 0.002f, 0.035f));
                case SoundId.Clonk: return Clip(name, 0.6f, (t, i) => (Sine(t, 880) * 0.5f + Sine(t, 1370) * 0.35f + Sine(t, 2230) * 0.2f) * Env(t, 0.002f, 0.12f) * 0.7f);
                case SoundId.Thud: return Clip(name, 0.25f, (t, i) => (Sine(t, 90 - t * 100) * 0.9f + Noise(i) * 0.25f) * Env(t, 0.002f, 0.06f));
                case SoundId.Clink: return Clip(name, 0.3f, (t, i) => (Sine(t, 2600) * 0.5f + Sine(t, 3900) * 0.3f) * Env(t, 0.001f, 0.05f) * 0.6f);
                case SoundId.Break:
                    return Clip(name, 0.7f, (t, i) =>
                    {
                        float v = Noise(i) * Env(t, 0.001f, 0.08f) * 0.8f;
                        for (int k = 0; k < 6; k++) v += Sine(t, 1800 + k * 730) * Env(Mathf.Max(0, t - k * 0.04f), 0.001f, 0.06f) * 0.15f;
                        return v;
                    });
                case SoundId.Squeak: return Clip(name, 0.35f, (t, i) => Square(t, 1150 - t * 900 + Sine(t, 22) * 60) * Env(t, 0.01f, 0.15f) * 0.25f);
                case SoundId.Flush:
                    return Clip(name, 2.2f, (t, i) => Noise(i) * (0.35f + 0.25f * Sine(t, 3 + t * 2)) * Mathf.Min(1, t * 6) * Clamp01((2.2f - t) * 1.5f) * 0.6f);
                case SoundId.Water: return Clip(name, 1f, (t, i) => Noise(i) * (0.25f + 0.1f * Sine(t, 11)) * 0.5f);
                case SoundId.TvStatic: return Clip(name, 1f, (t, i) => Noise(i) * 0.25f + Sine(t, 60) * 0.05f);
                case SoundId.Click: return Clip(name, 0.05f, (t, i) => Noise(i) * Env(t, 0.001f, 0.006f) * 0.7f);
                case SoundId.Door: return Clip(name, 0.7f, (t, i) => Saw(t, 180 + Sine(t, 7) * 40 + t * 60) * Env(t, 0.05f, 0.3f) * 0.25f);
                case SoundId.ArmPop: return Clip(name, 0.3f, (t, i) => (Sine(t, 650 - t * 1600) * 0.8f + Noise(i) * 0.2f) * Env(t, 0.002f, 0.08f));
                case SoundId.Honk: return Clip(name, 0.4f, (t, i) => (Saw(t, 330 + Sine(t, 9) * 25) * 0.4f + Saw(t, 495) * 0.2f) * Env(t, 0.02f, 0.2f) * 0.5f);
                case SoundId.Pickup: return Clip(name, 0.12f, (t, i) => Sine(t, 700 + t * 3000) * Env(t, 0.003f, 0.05f) * 0.4f);
                case SoundId.Bank:
                case SoundId.Coins:
                    return Clip(name, 0.6f, (t, i) =>
                    {
                        float v = 0;
                        for (int k = 0; k < 4; k++) v += Sine(t, 2100 + k * 380) * Env(Mathf.Max(0, t - k * 0.08f), 0.001f, 0.08f) * (t > k * 0.08f ? 1 : 0);
                        return v * 0.3f;
                    });
                case SoundId.TaskDone:
                    return Clip(name, 0.9f, (t, i) =>
                    {
                        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                        int k = Mathf.Min(3, (int)(t / 0.12f));
                        float lt = t - k * 0.12f;
                        return (Sine(t, notes[k]) * 0.5f + Sine(t, notes[k] * 2) * 0.15f) * Env(lt, 0.005f, k == 3 ? 0.35f : 0.1f) * 0.5f;
                    });
                case SoundId.Grumble: return Clip(name, 0.9f, (t, i) => (Saw(t, 95 + Sine(t, 6) * 12) * 0.5f + Noise(i) * 0.05f) * Env(t, 0.05f, 0.35f) * (0.6f + 0.4f * Sine(t, 3.5f)) * 0.6f);
                case SoundId.Shout: return Clip(name, 0.6f, (t, i) => (Saw(t, 180 - t * 60) * 0.5f + Saw(t, 360 - t * 100) * 0.3f + Noise(i) * 0.08f) * Env(t, 0.01f, 0.25f) * 0.8f);
                case SoundId.Snore: return Clip(name, 2.4f, (t, i) => (Noise(i) * 0.4f + Saw(t, 60) * 0.3f) * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t / 2.4f * Mathf.Pi)), 2) * 0.5f);
                case SoundId.Meow: return Clip(name, 0.7f, (t, i) => (Sine(t, 650 + Mathf.Sin(t / 0.7f * Mathf.Pi) * 450) * 0.6f + Sine(t, 1300 + Mathf.Sin(t / 0.7f * Mathf.Pi) * 900) * 0.2f) * Env(t, 0.05f, 0.3f) * 0.5f);
                case SoundId.Hiss: return Clip(name, 0.6f, (t, i) => (Noise(i) - Noise(i + 1) * 0.9f) * Env(t, 0.02f, 0.25f) * 0.5f);
                case SoundId.Roomba: return Clip(name, 1f, (t, i) => (Saw(t, 110) * 0.2f + Noise(i) * 0.15f) * 0.6f);
                case SoundId.Oven: return Clip(name, 1f, (t, i) => (Noise(i) * 0.3f + Sine(t, 50) * 0.2f));
                case SoundId.Revive:
                    return Clip(name, 1.2f, (t, i) =>
                    {
                        float v = 0;
                        for (int k = 0; k < 6; k++) v += Sine(t, 600 * Mathf.Pow(1.26f, k)) * Env(Mathf.Max(0, t - k * 0.1f), 0.005f, 0.2f) * (t > k * 0.1f ? 1 : 0);
                        return v * 0.25f;
                    });
                case SoundId.Death:
                    return Clip(name, 1.6f, (t, i) =>
                    {
                        int k = Mathf.Min(3, (int)(t / 0.35f));
                        float f = 220 * Mathf.Pow(0.94f, k) - (k == 3 ? (t - 1.05f) * 30 : 0);
                        return Saw(t, f + Sine(t, 6) * (k == 3 ? 8 : 0)) * Env(t - k * 0.35f, 0.02f, 0.3f) * 0.35f;
                    });
                case SoundId.Chime: return Clip(name, 2.5f, (t, i) => (Sine(t, 523) * 0.5f + Sine(t, 1047) * 0.25f + Sine(t, 1569) * 0.15f + Sine(t, 2616) * 0.08f) * Env(t, 0.002f, 0.9f) * 0.6f);
                case SoundId.Alarm: return Clip(name, 2f, (t, i) => Square(t, 1400) * (Sine(t, 16) > 0 ? 1 : 0) * (Repeat(t, 0.5f) < 0.35f ? 1 : 0) * 0.2f);
                case SoundId.Splash: return Clip(name, 0.6f, (t, i) => (Noise(i) * Env(t, 0.003f, 0.12f) + Sine(t, 400 + Sine(t, 30) * 200) * Env(t, 0.05f, 0.15f) * 0.3f) * 0.6f);
                case SoundId.Ding: return Clip(name, 0.5f, (t, i) => (Sine(t, 1320) * 0.5f + Sine(t, 2640) * 0.2f) * Env(t, 0.002f, 0.15f) * 0.5f);
                case SoundId.Rooster:
                    return Clip(name, 1.6f, (t, i) =>
                    {
                        float f = t < 0.2f ? 500 : t < 0.4f ? 650 : t < 0.9f ? 800 + Sine(t, 8) * 30 : 700 - (t - 0.9f) * 400;
                        return (Saw(t, f) * 0.3f + Saw(t, f * 1.5f) * 0.15f) * Env(t, 0.02f, 0.9f) * (Repeat(t, 0.2f) < 0.18f ? 1 : 0.6f) * 0.6f;
                    });
                case SoundId.Struggle: return Clip(name, 0.2f, (t, i) => (Saw(t, 260 + Noise(i) * 30) * 0.3f + Noise(i) * 0.2f) * Env(t, 0.01f, 0.07f));
                case SoundId.Gulp: return Clip(name, 0.25f, (t, i) => Sine(t, 300 - t * 800) * Env(t, 0.01f, 0.08f) * 0.6f);
                case SoundId.Sparkle: return Clip(name, 0.5f, (t, i) => Sine(t, 1800 + Sine(t, 40) * 600) * Env(t, 0.005f, 0.15f) * 0.3f);
                case SoundId.Slurp: return Clip(name, 0.4f, (t, i) => Noise(i) * Sine(t, 30) * Env(t, 0.02f, 0.15f) * 0.5f);
                case SoundId.Squawk:
                    // "VO-RY!" - two harsh syllables
                    return Clip(name, 0.9f, (t, i) =>
                    {
                        float syl = t < 0.35f ? t : t - 0.45f;
                        if (t >= 0.35f && t < 0.45f) return 0f;
                        float f = t < 0.35f ? 900 + syl * 600 : 1250 - syl * 900;
                        return (Saw(t, f) * 0.35f + Square(t, f * 1.5f) * 0.15f + Noise(i) * 0.1f) * Env(syl, 0.01f, 0.18f) * 0.8f;
                    });
                default: return Clip(name, 0.1f, (t, i) => Noise(i) * Env(t, 0.001f, 0.02f));
            }
        }

        static AudioStreamWav MakeMusic(bool hub)
        {
            // Sneaky pizzicato bass + tiptoe melody on a minor scale (night) or a cosy major loop (hub).
            float bpm = hub ? 96 : 104;
            float beat = 60f / bpm;
            int bars = 8;
            float len = bars * 4 * beat;
            int[] scale = hub ? new[] { 0, 2, 4, 5, 7, 9, 11, 12 } : new[] { 0, 2, 3, 5, 7, 8, 10, 12 };
            float root = hub ? 196f : 174.6f; // G3 / F3
            int[] bass = hub ? new[] { 0, 4, 5, 3, 0, 4, 5, 4 } : new[] { 0, 0, 5, 4, 3, 3, 4, 4 };
            int[] mel = hub
                ? new[] { 4, 5, 7, 5, 4, -1, 2, 4, 5, 4, 2, 0, 2, -1, 4, -1, 7, 5, 4, 2, 4, 5, 7, -1, 5, 4, 2, 4, 0, -1, -1, -1 }
                : new[] { 0, -1, 2, 3, -1, 2, 0, -1, 4, -1, 3, 2, 0, -1, -1, -1, 3, -1, 4, 5, -1, 4, 3, -1, 2, 3, 4, -1, 0, -1, -1, -1 };
            return Clip(hub ? "musicHub" : "musicNight", len, (t, i) =>
            {
                float v = 0;
                int b = (int)(t / beat);
                float bt = t - b * beat;
                int bar = (b / 4) % bars;
                // pizzicato bass on every beat
                float bf = root / 2 * Mathf.Pow(2, scale[bass[bar] % scale.Length] / 12f);
                if (b % 2 == 1) bf *= 1.5f;
                v += (Sine(bt, bf) + 0.3f * Sine(bt, bf * 2)) * Env(bt, 0.004f, 0.12f) * 0.45f;
                // melody on eighths
                int e = (int)(t / (beat / 2));
                float et = t - e * beat / 2;
                int note = mel[e % mel.Length];
                if (note >= 0)
                {
                    float f = root * Mathf.Pow(2, scale[note % scale.Length] / 12f);
                    v += (Sine(et, f) * 0.6f + Sine(et, f * 3) * 0.1f) * Env(et, 0.003f, hub ? 0.2f : 0.09f) * 0.35f;
                }
                // soft brush on offbeats
                if (!hub && b % 2 == 1) v += Noise(i) * Env(bt, 0.001f, 0.02f) * 0.06f;
                return v * 0.8f;
            });
        }
    }
}
