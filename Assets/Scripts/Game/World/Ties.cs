using System.Collections.Generic;
using Gnomes.Core.Protocol;
using Gnomes.Rendering;
using Gnomes.Session;
using UnityEngine;

namespace Gnomes.World
{
    /// <summary>
    /// Yarn ties between two props, or a prop and a point in the world.
    /// Host: a SpringJoint acting like a rope. Everyone: a stretched yarn cylinder.
    /// </summary>
    public class Ties : MonoBehaviour
    {
        public const int MaxTies = 16;

        class Tie
        {
            public ushort Id;
            public ushort PropA, PropB; // 0 = world
            public Vector3 LocalA, LocalB; // prop-local, or world when the prop is 0
            public float Length;
            public byte Color;
            public SpringJoint Joint;
            public Transform Visual;
        }

        readonly List<Tie> ties = new List<Tie>();
        ushort nextId = 1;
        GameWorld world;

        public static Ties Create(GameWorld w)
        {
            var t = w.gameObject.AddComponent<Ties>();
            t.world = w;
            return t;
        }

        public int Count => ties.Count;

        /// <summary>Host: tie A to B. Returns the event to broadcast, or null.</summary>
        public EventMsg? HostCreate(Prop a, Vector3 worldA, Prop b, Vector3 worldB, byte color)
        {
            if (a == null && b == null) return null;
            if (a == null)
            {
                // always keep the prop on side A
                a = b;
                b = null;
                var tmp = worldA;
                worldA = worldB;
                worldB = tmp;
            }
            while (ties.Count >= MaxTies) Remove(ties[0].Id, true);
            var tie = new Tie
            {
                Id = nextId++,
                PropA = a.Id,
                PropB = b != null ? b.Id : (ushort)0,
                LocalA = a.transform.InverseTransformPoint(worldA),
                LocalB = b != null ? b.transform.InverseTransformPoint(worldB) : worldB,
                Length = Vector3.Distance(worldA, worldB) + 0.1f,
                Color = color,
            };
            AddJoint(tie, a, b);
            AddVisual(tie);
            ties.Add(tie);
            return new EventMsg
            {
                Type = EvType.Tie,
                Id = tie.Id,
                I = tie.PropA,
                S = tie.PropB.ToString(),
                Pos = tie.LocalA.ToCoreV(),
                Vel = tie.LocalB.ToCoreV(),
                F = tie.Length,
                P = color,
            };
        }

        void AddJoint(Tie tie, Prop a, Prop b)
        {
            if (!world.Authority) return;
            var j = a.gameObject.AddComponent<SpringJoint>();
            j.autoConfigureConnectedAnchor = false;
            j.anchor = tie.LocalA;
            j.connectedBody = b != null ? b.Rb : null;
            j.connectedAnchor = tie.LocalB;
            j.spring = 600f * Mathf.Max(0.3f, a.Rb.mass);
            j.damper = 20f;
            j.minDistance = 0f;
            j.maxDistance = tie.Length;
            j.tolerance = 0.02f;
            j.enableCollision = true;
            tie.Joint = j;
        }

        void AddVisual(Tie tie)
        {
            var pivot = new GameObject("Tie_" + tie.Id).transform;
            pivot.SetParent(transform, false);
            var y = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(y.GetComponent<Collider>());
            y.transform.SetParent(pivot, false);
            y.transform.localRotation = Quaternion.Euler(90, 0, 0);
            y.transform.localPosition = new Vector3(0, 0, 0.5f);
            y.transform.localScale = new Vector3(0.035f, 0.5f, 0.035f);
            y.GetComponent<MeshRenderer>().sharedMaterial = ModelLibrary.TintMaterial(GameSession.HatColor(tie.Color));
            y.layer = Layers.IgnoreRaycast;
            tie.Visual = pivot;
        }

        /// <summary>Client: create the tie from a host event (visual only).</summary>
        public void ApplyNet(EventMsg e)
        {
            if (ties.Exists(t => t.Id == e.Id)) return;
            ushort.TryParse(e.S, out var pb);
            var tie = new Tie { Id = e.Id, PropA = (ushort)e.I, PropB = pb, LocalA = e.Pos.U(), LocalB = e.Vel.U(), Length = e.F, Color = e.P };
            AddVisual(tie);
            ties.Add(tie);
            if (e.Id >= nextId) nextId = (ushort)(e.Id + 1);
        }

        public void Remove(ushort id, bool broadcast)
        {
            var t = ties.Find(x => x.Id == id);
            if (t == null) return;
            if (t.Joint) Destroy(t.Joint);
            if (t.Visual) Destroy(t.Visual.gameObject);
            ties.Remove(t);
            if (broadcast && world.Authority) world.Session?.Broadcast(new EventMsg { Type = EvType.Untie, Id = id });
        }

        /// <summary>Remove every tie involving a prop (it got banked, broken, flushed...).</summary>
        public void OnPropRemoved(ushort propId)
        {
            for (int i = ties.Count - 1; i >= 0; i--)
                if (ties[i].PropA == propId || ties[i].PropB == propId) Remove(ties[i].Id, false);
        }

        /// <summary>Events to replay for a late joiner.</summary>
        public IEnumerable<EventMsg> AllAsEvents()
        {
            foreach (var t in ties)
                yield return new EventMsg { Type = EvType.Tie, Id = t.Id, I = t.PropA, S = t.PropB.ToString(), Pos = t.LocalA.ToCoreV(), Vel = t.LocalB.ToCoreV(), F = t.Length, P = t.Color };
        }

        void LateUpdate()
        {
            foreach (var t in ties)
            {
                var pa = world.GetProp(t.PropA);
                if (pa == null || t.Visual == null) continue;
                var a = pa.transform.TransformPoint(t.LocalA);
                Vector3 b;
                if (t.PropB != 0)
                {
                    var pb = world.GetProp(t.PropB);
                    if (pb == null) continue;
                    b = pb.transform.TransformPoint(t.LocalB);
                }
                else b = t.LocalB;
                var d = b - a;
                t.Visual.position = a;
                if (d.sqrMagnitude > 1e-6f) t.Visual.rotation = Quaternion.LookRotation(d);
                t.Visual.localScale = new Vector3(1, 1, Mathf.Max(0.01f, d.magnitude));
            }
        }
    }
}
