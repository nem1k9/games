using System.Collections.Generic;
using Gnomes.Core;
using Gnomes.Core.Protocol;
using Godot;
using SockGang.World;

namespace SockGang.Players
{
    /// <summary>Physics helpers shared by the host (applied to props) and clients (reaction on the gnome).</summary>
    public static class GrabPhysics
    {
        public static float Gravity = 24f;

        /// <summary>Force that drags a carried item's grab point towards the hold target.</summary>
        public static Vector3 HoldForce(Vector3 anchor, Vector3 anchorVel, Vector3 target, float mass, float strength, float gravityShare = 1f)
        {
            var err = target - anchor;
            var desiredVel = GMath.ClampLength(err * 11f, 9f);
            var f = (desiredVel - anchorVel) * mass * 12f;
            f += Vector3.Up * (mass * Gravity * gravityShare);
            return GMath.ClampLength(f, strength);
        }

        /// <summary>Tension of a yarn of the given length between a and b (only pulls). Force acts on a towards b.</summary>
        public static Vector3 YarnTension(Vector3 a, Vector3 aVel, Vector3 b, float length, float maxForce, float stiffness = 260f, float damping = 28f)
        {
            var d = b - a;
            float dist = d.Length();
            if (dist <= length || dist < 1e-4f) return Vector3.Zero;
            var dir = d / dist;
            float f = (dist - length) * stiffness - aVel.Dot(dir) * damping;
            return dir * Mathf.Clamp(f, 0f, maxForce);
        }
    }

    /// <summary>
    /// Common state of any gnome (local or remote) used by AI, networking and the HUD.
    /// Positions/velocities are Godot space; <see cref="Yaw"/> is a Godot Y rotation.
    /// </summary>
    public abstract partial class GnomeBody : CharacterBody3D
    {
        public byte PlayerId;
        public string PlayerName;
        public Color Hat;
        public GnomeAvatar Avatar;
        public CollisionShape3D Col;
        CapsuleShape3D capsule;
        public PlayerStatus Status;
        public float Hp = GameConsts.MaxHealth;
        public bool Crouching, Sprinting, Grounded, Struggling, ArmsRipped;
        public float Yaw, Pitch;
        public ArmState Arms;

        public abstract bool IsLocal { get; }

        public Vector3 Feet => GlobalPosition;
        public Vector3 Center => GlobalPosition + Vector3.Up * (Crouching ? 0.35f : 0.5f);
        public Vector3 HeadPos => GlobalPosition + Vector3.Up * (Crouching ? GameConsts.GnomeEyeCrouch : GameConsts.GnomeEye);
        public bool Alive => Status != PlayerStatus.Dead && Status != PlayerStatus.Home;
        public bool IsFree => Status == PlayerStatus.Free;
        public Basis YawBasis => GMath.YawBasis(Yaw);
        public Vector3 Forward => GMath.YawForward(Yaw);
        public Vector3 Right => YawBasis * Vector3.Right;
        public Vector3 LookDir => GMath.LookDir(Yaw, Pitch);

        protected void CreateBody(uint layer, uint mask)
        {
            CollisionLayer = layer;
            CollisionMask = mask;
            capsule = new CapsuleShape3D { Radius = GameConsts.GnomeRadius, Height = GameConsts.GnomeHeight };
            Col = new CollisionShape3D { Name = "Capsule", Shape = capsule, Position = new Vector3(0, GameConsts.GnomeHeight / 2, 0) };
            AddChild(Col);
            FloorMaxAngle = Mathf.DegToRad(55f);
            FloorSnapLength = 0.12f;
            SafeMargin = 0.02f;
        }

        protected void SetCrouchCollider(bool crouch)
        {
            if (capsule == null) return;
            float h = crouch ? GameConsts.GnomeCrouchHeight : GameConsts.GnomeHeight;
            if (Mathf.IsEqualApprox(capsule.Height, h)) return;
            capsule.Height = h;
            Col.Position = new Vector3(0, h / 2, 0);
        }

        /// <summary>World-space hand targets and yarn for the avatar from an arm state (network space).</summary>
        protected void AvatarHands(ArmState arms, Vector3 fallbackLook)
        {
            if (Avatar == null) return;
            var right = Right;
            var chest = GlobalPosition + Vector3.Up * (Crouching ? 0.4f : 0.55f) + Forward * 0.25f;
            Vector3 hand = fallbackLook;
            bool active = true, yarnOn = false;
            Vector3 yarnEnd = Vector3.Zero;
            Vector3? climbL = null, climbR = null;
            var world = GameWorld.Current;
            switch (arms.Mode)
            {
                case ArmMode.HoldProp:
                    var prop = world?.GetProp(arms.PropId);
                    hand = prop != null ? prop.ToGlobal(arms.Anchor.G()) : arms.Hand.G();
                    break;
                case ArmMode.Climb:
                    yarnOn = true;
                    yarnEnd = arms.Anchor.G();
                    var up = (yarnEnd - chest).Normalized();
                    hand = chest + up * 0.35f;
                    // hand over hand along the yarn: the phase follows the yarn length (replicated), so a
                    // climbing gnome visibly pulls himself up and a hanging one holds still
                    float ph = arms.Hand.x * 7f;
                    climbL = chest + up * (0.34f + 0.13f * Mathf.Sin(ph)) - right * 0.04f;
                    climbR = chest + up * (0.34f + 0.13f * Mathf.Sin(ph + Mathf.Pi)) + right * 0.04f;
                    break;
                case ArmMode.YarnProp:
                    var yp = world?.GetProp(arms.PropId);
                    yarnOn = yp != null;
                    yarnEnd = yp != null ? yp.ToGlobal(arms.Anchor.G()) : chest;
                    hand = chest + (yarnEnd - chest).Normalized() * 0.35f;
                    break;
                case ArmMode.YarnFly:
                    yarnOn = true;
                    yarnEnd = arms.Hand.G();
                    hand = chest + (yarnEnd - chest).Normalized() * 0.35f;
                    break;
                case ArmMode.Reach:
                    hand = arms.Hand.G();
                    break;
                default:
                    active = false;
                    break;
            }
            float spread = arms.Mode == ArmMode.HoldProp ? 0.1f : 0.06f;
            Avatar.HandsActive = active;
            Avatar.HandTargetL = climbL ?? hand - right * spread;
            Avatar.HandTargetR = climbR ?? hand + right * spread;
            Avatar.SetYarn(yarnOn, hand, yarnEnd);
        }

        protected void FeedAvatar()
        {
            if (Avatar == null) return;
            Avatar.Vel = Velocity;
            Avatar.Grounded = Grounded;
            Avatar.Crouch = Crouching;
            Avatar.Pitch = Pitch;
            Avatar.Status = Status;
            Avatar.Struggling = Struggling;
            Avatar.ArmsRipped = ArmsRipped;
            Avatar.Yaw = Yaw;
            Avatar.SetShown(Status != PlayerStatus.Dead && Status != PlayerStatus.Home);
        }

        public PFlags Flags
        {
            get
            {
                var f = PFlags.None;
                if (Grounded) f |= PFlags.Grounded;
                if (Crouching) f |= PFlags.Crouch;
                if (Sprinting) f |= PFlags.Sprint;
                if (Struggling) f |= PFlags.Struggling;
                if (ArmsRipped) f |= PFlags.ArmsRipped;
                return f;
            }
        }
    }

    /// <summary>Another player's gnome: a kinematic proxy whose pose is interpolated from network updates.</summary>
    public partial class RemoteGnome : GnomeBody
    {
        struct Sample
        {
            public float T;
            public Vector3 P;
            public float Yaw, Pitch;
            public PFlags F;
            public ArmState A;
            public Vector3 V;
        }

        readonly List<Sample> samples = new List<Sample>(16);
        public float InterpDelay = 0.1f;
        public override bool IsLocal => false;

        public static RemoteGnome Create(GameWorld w, byte id, string name, Color hat, Vector3 pos)
        {
            var g = new RemoteGnome { Name = $"Gnome_{id}", PlayerId = id, PlayerName = name, Hat = hat };
            w.GnomeRoot.AddChild(g);
            g.GlobalPosition = pos;
            g.CreateBody(Layers.Gnome, 0);
            g.Avatar = GnomeAvatar.Create(g, hat);
            w.Gnomes[id] = g;
            return g;
        }

        public void Push(float time, Vector3 pos, float yaw, float pitch, PFlags flags, ArmState arms, Vector3 vel)
        {
            if (samples.Count > 0 && time <= samples[samples.Count - 1].T) return;
            samples.Add(new Sample { T = time, P = pos, Yaw = yaw, Pitch = pitch, F = flags, A = arms, V = vel });
            if (samples.Count > 16) samples.RemoveAt(0);
        }

        public void SnapTo(Vector3 pos)
        {
            samples.Clear();
            GlobalPosition = pos;
        }

        /// <summary>Host: carried / trapped gnomes follow grandpa's hand or sit in their jar.</summary>
        public void Force(Vector3 pos) => GlobalPosition = pos;

        public void Tick(float now)
        {
            if (samples.Count == 0) return;
            float rt = now - InterpDelay;
            Sample a = samples[0], b = samples[0];
            for (int i = 0; i < samples.Count - 1; i++)
            {
                if (samples[i + 1].T >= rt)
                {
                    a = samples[i];
                    b = samples[i + 1];
                    break;
                }
                a = b = samples[i + 1];
            }
            float k = b.T > a.T ? Mathf.Clamp((rt - a.T) / (b.T - a.T), 0f, 1f) : 1f;
            Yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, k);
            Pitch = Mathf.Lerp(a.Pitch, b.Pitch, k);
            var f = k < 0.5f ? a.F : b.F;
            Grounded = (f & PFlags.Grounded) != 0;
            Crouching = (f & PFlags.Crouch) != 0;
            Sprinting = (f & PFlags.Sprint) != 0;
            Struggling = (f & PFlags.Struggling) != 0;
            ArmsRipped = (f & PFlags.ArmsRipped) != 0;
            Arms = b.A;
            Velocity = a.V.Lerp(b.V, k);
            GlobalPosition = a.P.Lerp(b.P, k);
            SetCrouchCollider(Crouching);
        }

        public override void _Process(double delta)
        {
            FeedAvatar();
            AvatarHands(Arms, HeadPos + LookDir * 0.4f);
        }
    }
}
