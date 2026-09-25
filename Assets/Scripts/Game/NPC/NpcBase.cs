using System.Collections.Generic;
using Gnomes.Core.Protocol;
using Gnomes.World;
using UnityEngine;

namespace Gnomes.NPC
{
    /// <summary>Common plumbing for NPCs: host simulates, clients interpolate replicated poses.</summary>
    public abstract class NpcBase : MonoBehaviour
    {
        public const byte OldManId = 0, CatId = 1, ParrotId = 2;
        public byte NpcId;
        public bool Authority;
        public byte State;
        public byte Anim;
        public Vector3 Look;
        public float Alert;

        struct Pose
        {
            public float T;
            public Vector3 P;
            public float Yaw;
            public NpcPose Raw;
        }

        readonly List<Pose> poses = new List<Pose>(8);
        protected GameWorld W => GameWorld.Current;

        public void WritePose(List<NpcPose> list)
        {
            list.Add(new NpcPose
            {
                Id = NpcId,
                Pos = transform.position.ToCoreV(),
                Yaw = transform.eulerAngles.y * Mathf.Deg2Rad,
                State = State,
                Anim = Anim,
                Look = Look.ToCoreV(),
                Alert = (byte)Mathf.Clamp(Alert * 255f, 0, 255),
            });
        }

        public void PushPose(float t, NpcPose p)
        {
            if (poses.Count > 0 && t <= poses[poses.Count - 1].T) return;
            poses.Add(new Pose { T = t, P = p.Pos.U(), Yaw = p.Yaw * Mathf.Rad2Deg, Raw = p });
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
            float k = b.T > a.T ? Mathf.Clamp01((renderTime - a.T) / (b.T - a.T)) : 1f;
            var pos = Vector3.Lerp(a.P, b.P, k);
            float yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, k);
            var raw = k < 0.5f ? a.Raw : b.Raw;
            State = raw.State;
            Anim = raw.Anim;
            Look = Vector3.Lerp(a.Raw.Look.U(), b.Raw.Look.U(), k);
            Alert = raw.Alert / 255f;
            ApplyClientPose(pos, yaw);
        }

        protected virtual void ApplyClientPose(Vector3 pos, float yawDeg)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.MovePosition(pos);
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yawDeg, 0));
        }
    }
}
