using Gnomes.Core;
using Gnomes.Core.Protocol;
using Godot;
using SockGang.App;
using SockGang.Audio;
using SockGang.Players;
using SockGang.World;

namespace SockGang.NPC
{
    /// <summary>
    /// Kesha the parrot sits in his cage in the living room and yells "THIEVES!" when he spots a gnome,
    /// which wakes grandpa. A towel over the cage or a cookie shuts him up.
    /// </summary>
    public partial class Parrot : Node
    {
        public enum St : byte { Idle = 0, Alarm = 1, Quiet = 2 }

        public byte NpcId = NpcBase.ParrotId;
        public bool Authority;
        public byte State;
        public Vector3 Look;
        Furniture cage;
        Node3D bird, eye;
        Quaternion birdRest;
        float quietUntil, suspicion, nextSquawk;
        GameWorld W => GameWorld.Current;

        public static Parrot Attach(GameWorld w, bool authority)
        {
            var cage = w.FindFurniture("parrotCage");
            if (cage == null) return null;
            var p = new Parrot { Name = "Parrot", Authority = authority, cage = cage };
            cage.AddChild(p);
            p.bird = cage.Model.Node("parrot");
            p.eye = cage.Model.Node("ANCHOR_eye");
            if (p.bird != null) p.birdRest = p.bird.Quaternion;
            w.Parrot = p;
            return p;
        }

        public bool Quiet => Clock.Now < quietUntil;

        public void Silence(float seconds)
        {
            quietUntil = Mathf.Max(quietUntil, Clock.Now + seconds);
            State = (byte)St.Quiet;
        }

        /// <summary>Host: called for every free gnome a few times per second.</summary>
        public void Watch(GnomeBody g)
        {
            if (!Authority || Quiet || eye == null) return;
            var from = eye.GlobalPosition;
            var to = g.Center - from;
            float d = to.Length();
            if (d > 17f || d < 1e-3f) return;
            if ((to / d).Dot(-cage.GlobalBasis.Z.Normalized()) < -0.2f) return; // behind him
            if (Phys.Raycast(from, to / d, d - 0.3f, Layers.Solid)) return;
            Look = g.Center;
            suspicion += (g.Crouching ? 0.12f : 0.3f) * (1f - d / 17f + 0.3f);
            if (suspicion > 1f && Clock.Now > nextSquawk) Squawk();
        }

        public void Squawk()
        {
            if (!Authority || Quiet) return;
            suspicion = 0;
            nextSquawk = Clock.Now + 6f;
            State = (byte)St.Alarm;
            var w = W;
            var p = eye != null ? eye.GlobalPosition : cage.GlobalPosition;
            w.EmitSound(SoundId.Squawk, p, 1f);
            w.Noise(p, 48f, NoiseKind.Alarm);
            GameApp.I?.Toast(Loc.T("parrotAlarm"));
            w.Session?.Broadcast(new EventMsg { Type = EvType.Message, S = "parrotAlarm", P = 255 });
        }

        public void WritePose(System.Collections.Generic.List<NpcPose> list)
        {
            list.Add(new NpcPose { Id = NpcId, Pos = cage.GlobalPosition.U(), State = State, Look = Look.U() });
        }

        public void PushPose(float t, NpcPose p)
        {
            State = p.State;
            Look = p.Look.G();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            if (Authority)
            {
                suspicion = Mathf.Max(0, suspicion - dt * 0.15f);
                if (State == (byte)St.Alarm && Clock.Now > nextSquawk - 4f) State = (byte)St.Idle;
                if (State == (byte)St.Quiet && !Quiet) State = (byte)St.Idle;
            }
            if (bird == null) return;
            // bob, look at the gnome, flap when alarmed
            float flap = State == (byte)St.Alarm ? Mathf.Sin(Clock.Now * 30f) * 20f : 0f;
            float yaw = 0;
            if (Look != Vector3.Zero)
            {
                var local = cage.ToLocal(Look);
                yaw = Mathf.Clamp(Mathf.RadToDeg(Mathf.Atan2(local.X, -local.Z)), -80, 80);
            }
            float bob = Mathf.Sin(Clock.Now * 2.5f) * (State == (byte)St.Quiet ? 1f : 6f);
            bird.Quaternion = birdRest * Conv.UEuler(bob + flap, yaw, 0);
        }
    }
}
