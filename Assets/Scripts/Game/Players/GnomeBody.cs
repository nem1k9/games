using System.Collections.Generic;
using Gnomes.Core;
using Gnomes.Core.Protocol;
using Gnomes.World;
using UnityEngine;

namespace Gnomes.Players
{
    /// <summary>Common state of any gnome (local or remote) used by AI, networking and the HUD.</summary>
    public abstract class GnomeBody : MonoBehaviour
    {
        public byte PlayerId;
        public string PlayerName;
        public Color32 Hat;
        public GnomeAvatar Avatar;
        public Rigidbody Rb;
        public CapsuleCollider Col;
        public PlayerStatus Status;
        public float Hp = GameConsts.MaxHealth;
        public bool Crouching, Sprinting, Grounded, Struggling, ArmsRipped;
        public float Yaw, Pitch;
        public ArmState Arms;
        public Vector3 Velocity;

        public abstract bool IsLocal { get; }

        public Vector3 Feet => transform.position;
        public Vector3 Center => transform.position + Vector3.up * (Crouching ? 0.35f : 0.5f);
        public Vector3 HeadPos => transform.position + Vector3.up * (Crouching ? GameConsts.GnomeEyeCrouch : GameConsts.GnomeEye);
        public bool Alive => Status != PlayerStatus.Dead && Status != PlayerStatus.Home;
        public bool Free => Status == PlayerStatus.Free;
        public Quaternion YawRot => Quaternion.Euler(0, Yaw * Mathf.Rad2Deg, 0);
        public Vector3 LookDir => Quaternion.Euler(-Pitch * Mathf.Rad2Deg, Yaw * Mathf.Rad2Deg, 0) * Vector3.forward;

        protected void CreateBody(int layer, bool kinematic)
        {
            Rb = gameObject.AddComponent<Rigidbody>();
            Rb.mass = GameConsts.GnomeMass;
            Rb.constraints = RigidbodyConstraints.FreezeRotation;
            Rb.interpolation = RigidbodyInterpolation.Interpolate;
            Rb.collisionDetectionMode = kinematic ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
            Rb.isKinematic = kinematic;
            Col = gameObject.AddComponent<CapsuleCollider>();
            Col.radius = GameConsts.GnomeRadius;
            Col.height = GameConsts.GnomeHeight;
            Col.center = new Vector3(0, GameConsts.GnomeHeight / 2, 0);
            Compat.SetFrictionless(Col);
            gameObject.layer = layer;
        }

        protected void SetCrouchCollider(bool crouch)
        {
            if (Col == null) return;
            float h = crouch ? GameConsts.GnomeCrouchHeight : GameConsts.GnomeHeight;
            Col.height = h;
            Col.center = new Vector3(0, h / 2, 0);
        }

        /// <summary>World-space hand targets for the avatar from an arm state.</summary>
        protected void AvatarHands(ArmState arms, Vector3 fallbackLook)
        {
            if (Avatar == null) return;
            var right = YawRot * Vector3.right;
            Vector3 hand;
            bool active = true;
            switch (arms.Mode)
            {
                case ArmMode.HoldProp:
                    var prop = GameWorld.Current != null ? GameWorld.Current.GetProp(arms.PropId) : null;
                    hand = prop != null ? prop.transform.TransformPoint(arms.Anchor.U()) : arms.Hand.U();
                    break;
                case ArmMode.Climb:
                case ArmMode.Reach:
                    hand = arms.Hand.U();
                    break;
                default:
                    hand = fallbackLook;
                    active = false;
                    break;
            }
            float spread = arms.Mode == ArmMode.HoldProp ? 0.1f : 0.08f;
            Avatar.HandsActive = active;
            Avatar.HandTargetL = hand - right * spread;
            Avatar.HandTargetR = hand + right * spread;
        }

        protected void FeedAvatar()
        {
            if (Avatar == null) return;
            Avatar.Velocity = Velocity;
            Avatar.Grounded = Grounded;
            Avatar.Crouch = Crouching;
            Avatar.Pitch = Pitch;
            Avatar.Status = Status;
            Avatar.Struggling = Struggling;
            Avatar.ArmsRipped = ArmsRipped;
            Avatar.transform.rotation = YawRot;
            Avatar.SetVisible(Status != PlayerStatus.Dead && Status != PlayerStatus.Home);
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

    /// <summary>
    /// Another player's gnome: a kinematic body (so it still pushes props on the host) whose pose is
    /// interpolated from network updates.
    /// </summary>
    public class RemoteGnome : GnomeBody
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

        public static RemoteGnome Create(GameWorld w, byte id, string name, Color32 hat, Vector3 pos)
        {
            var go = new GameObject($"Gnome_{id}_{name}");
            go.transform.SetParent(w.GnomeRoot, false);
            go.transform.position = pos;
            var g = go.AddComponent<RemoteGnome>();
            g.PlayerId = id;
            g.PlayerName = name;
            g.Hat = hat;
            g.CreateBody(Layers.Gnome, true);
            g.Avatar = GnomeAvatar.Create(go.transform, hat);
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
            transform.position = pos;
            if (Rb) Rb.position = pos;
        }

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
            float k = b.T > a.T ? Mathf.Clamp01((rt - a.T) / (b.T - a.T)) : 1f;
            var p = Vector3.Lerp(a.P, b.P, k);
            Yaw = Mathf.LerpAngle(a.Yaw * Mathf.Rad2Deg, b.Yaw * Mathf.Rad2Deg, k) * Mathf.Deg2Rad;
            Pitch = Mathf.Lerp(a.Pitch, b.Pitch, k);
            var f = k < 0.5f ? a.F : b.F;
            Grounded = (f & PFlags.Grounded) != 0;
            Crouching = (f & PFlags.Crouch) != 0;
            Sprinting = (f & PFlags.Sprint) != 0;
            Struggling = (f & PFlags.Struggling) != 0;
            ArmsRipped = (f & PFlags.ArmsRipped) != 0;
            Arms = b.A;
            Velocity = Vector3.Lerp(a.V, b.V, k);
            if (Rb) Rb.MovePosition(p);
            else transform.position = p;
            SetCrouchCollider(Crouching);
        }

        void LateUpdate()
        {
            FeedAvatar();
            AvatarHands(Arms, HeadPos + LookDir * 0.4f);
        }
    }
}
