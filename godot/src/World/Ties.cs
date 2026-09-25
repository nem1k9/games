using System.Collections.Generic;
using Gnomes.Core.Protocol;
using Godot;
using SockGang.Rendering;
using SockGang.Session;

namespace SockGang.World
{
    /// <summary>
    /// Yarn ties between two props, or a prop and a point in the world.
    /// Host: a one-sided spring (a rope). Everyone: a stretched yarn cylinder.
    /// </summary>
    public partial class Ties : Node3D
    {
        public const int MaxTies = 16;
        public GameWorld World;

        class Tie
        {
            public ushort Id;
            public ushort PropA, PropB; // 0 = world
            public Vector3 LocalA, LocalB; // prop-local, or world when the prop is 0
            public float Length;
            public byte Color;
            public Node3D Visual;
        }

        readonly List<Tie> ties = new List<Tie>();
        ushort nextId = 1;
        static CylinderMesh yarnMesh;

        public int Count => ties.Count;

        /// <summary>Host: tie A to B. Returns the event to broadcast, or null.</summary>
        public EventMsg? HostCreate(Prop a, Vector3 worldA, Prop b, Vector3 worldB, byte color)
        {
            if (a == null && b == null) return null;
            if (a == null)
            {
                a = b;
                b = null;
                (worldA, worldB) = (worldB, worldA);
            }
            while (ties.Count >= MaxTies) Remove(ties[0].Id, true);
            var tie = new Tie
            {
                Id = nextId++,
                PropA = a.Id,
                PropB = b != null ? b.Id : (ushort)0,
                LocalA = a.ToLocal(worldA),
                LocalB = b != null ? b.ToLocal(worldB) : worldB,
                Length = worldA.DistanceTo(worldB) + 0.1f,
                Color = color,
            };
            AddVisual(tie);
            ties.Add(tie);
            return ToEvent(tie);
        }

        static EventMsg ToEvent(Tie t) => new EventMsg
        {
            Type = EvType.Tie,
            Id = t.Id,
            I = t.PropA,
            S = t.PropB.ToString(),
            Pos = t.LocalA.U(),
            Vel = t.LocalB.U(),
            F = t.Length,
            P = t.Color,
        };

        void AddVisual(Tie tie)
        {
            yarnMesh ??= new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.035f, Height = 1f, RadialSegments = 6, Rings = 1 };
            var pivot = new Node3D { Name = "Tie_" + tie.Id };
            AddChild(pivot);
            var y = new MeshInstance3D { Mesh = yarnMesh, MaterialOverride = ModelLibrary.TintMaterial(GameSession.HatColor(tie.Color)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            y.Rotation = new Vector3(Mathf.Pi / 2, 0, 0);
            y.Position = new Vector3(0, 0, -0.5f);
            pivot.AddChild(y);
            tie.Visual = pivot;
        }

        /// <summary>Client: create the tie from a host event (visual only).</summary>
        public void ApplyNet(EventMsg e)
        {
            if (ties.Exists(t => t.Id == e.Id)) return;
            ushort.TryParse(e.S, out var pb);
            var tie = new Tie { Id = e.Id, PropA = (ushort)e.I, PropB = pb, LocalA = e.Pos.G(), LocalB = e.Vel.G(), Length = e.F, Color = e.P };
            AddVisual(tie);
            ties.Add(tie);
            if (e.Id >= nextId) nextId = (ushort)(e.Id + 1);
        }

        public void Remove(ushort id, bool broadcast)
        {
            var t = ties.Find(x => x.Id == id);
            if (t == null) return;
            t.Visual?.QueueFree();
            ties.Remove(t);
            if (broadcast && World.Authority) World.Session?.Broadcast(new EventMsg { Type = EvType.Untie, Id = id });
        }

        public void OnPropRemoved(ushort propId)
        {
            for (int i = ties.Count - 1; i >= 0; i--)
                if (ties[i].PropA == propId || ties[i].PropB == propId) Remove(ties[i].Id, false);
        }

        public IEnumerable<EventMsg> AllAsEvents()
        {
            foreach (var t in ties) yield return ToEvent(t);
        }

        bool Ends(Tie t, out Vector3 a, out Vector3 b, out Prop pa, out Prop pb)
        {
            a = b = Vector3.Zero;
            pa = World.GetProp(t.PropA);
            pb = t.PropB != 0 ? World.GetProp(t.PropB) : null;
            if (pa == null || (t.PropB != 0 && pb == null)) return false;
            a = pa.ToGlobal(t.LocalA);
            b = pb != null ? pb.ToGlobal(t.LocalB) : t.LocalB;
            return true;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!World.Authority) return;
            foreach (var t in ties)
            {
                if (!Ends(t, out var a, out var b, out var pa, out var pb)) continue;
                var d = b - a;
                float dist = d.Length();
                if (dist <= t.Length || dist < 1e-4f) continue;
                var dir = d / dist;
                // rope: pull the two ends together only when stretched
                float stretch = dist - t.Length;
                var relVel = (pb != null ? pb.LinearVelocity : Vector3.Zero) - pa.LinearVelocity;
                float f = stretch * 600f * Mathf.Max(0.3f, pa.Mass) + relVel.Dot(dir) * 20f;
                if (f <= 0) continue;
                f = Mathf.Min(f, 400f);
                pa.ApplyForce(dir * f, a - pa.GlobalPosition);
                pb?.ApplyForce(-dir * f, b - pb.GlobalPosition);
            }
        }

        public override void _Process(double delta)
        {
            foreach (var t in ties)
            {
                if (t.Visual == null || !Ends(t, out var a, out var b, out _, out _)) continue;
                var d = b - a;
                t.Visual.GlobalPosition = a;
                if (d.LengthSquared() < 1e-6f) continue;
                var basis = GMath.LookBasis(d, Vector3.Up);
                basis.Z *= Mathf.Max(0.01f, d.Length()); // stretch along the local Z axis only
                t.Visual.GlobalBasis = basis;
            }
        }
    }
}
