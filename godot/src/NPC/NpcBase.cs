using System.Collections.Generic;
using Gnomes.Core.Protocol;
using Godot;
using SockGang.World;

namespace SockGang.NPC
{
    /// <summary>Common plumbing for NPCs: the host simulates, clients interpolate replicated poses.</summary>
    public abstract partial class NpcBase : AnimatableBody3D
    {
        public const byte OldManId = 0, CatId = 1, ParrotId = 2;
        public byte NpcId;
        public bool Authority;
        public byte State;
        public byte Anim;
        public Vector3 Look;
        public float Alert;
        /// <summary>Godot yaw of the body (network yaw is the same number).</summary>
        public float BodyYaw;

        struct Pose
        {
            public float T;
            public Vector3 P;
            public float Yaw;
            public NpcPose Raw;
        }

        readonly List<Pose> poses = new List<Pose>(8);
        protected GameWorld W => GameWorld.Current;

        public virtual void WritePose(List<NpcPose> list)
        {
            list.Add(new NpcPose
            {
                Id = NpcId,
                Pos = GlobalPosition.U(),
                Yaw = BodyYaw,
                State = State,
                Anim = Anim,
                Look = Look.U(),
                Alert = (byte)Mathf.Clamp(Alert * 255f, 0, 255),
            });
        }

        public void PushPose(float t, NpcPose p)
        {
            if (poses.Count > 0 && t <= poses[poses.Count - 1].T) return;
            poses.Add(new Pose { T = t, P = p.Pos.G(), Yaw = p.Yaw, Raw = p });
            if (poses.Count > 8) poses.RemoveAt(0);
        }

        /// <summary>Client: interpolate the replicated pose at render time.</summary>
        public void ClientTick(float renderTime)
        {
            if (Authority || poses.Count == 0) return;
            Pose a = poses[0], b = poses[0];
            for (int i = 0; i < poses.Count - 1; i++)
            {
                if (poses[i + 1].T >= renderTime)
                {
                    a = poses[i];
                    b = poses[i + 1];
                    break;
                }
                a = b = poses[i + 1];
            }
            float k = b.T > a.T ? Mathf.Clamp((renderTime - a.T) / (b.T - a.T), 0f, 1f) : 1f;
            var raw = k < 0.5f ? a.Raw : b.Raw;
            State = raw.State;
            Anim = raw.Anim;
            Look = a.Raw.Look.G().Lerp(b.Raw.Look.G(), k);
            Alert = raw.Alert / 255f;
            ApplyClientPose(a.P.Lerp(b.P, k), Mathf.LerpAngle(a.Yaw, b.Yaw, k));
        }

        protected virtual void ApplyClientPose(Vector3 pos, float yaw)
        {
            BodyYaw = yaw;
            GlobalTransform = new Transform3D(GMath.YawBasis(yaw), pos);
        }

        /// <summary>Host: move and face (kinematic, pushes props when SyncToPhysics is on).</summary>
        protected void MoveBody(Vector3 pos, float yaw)
        {
            BodyYaw = yaw;
            GlobalTransform = new Transform3D(GMath.YawBasis(yaw), pos);
        }
    }
}
