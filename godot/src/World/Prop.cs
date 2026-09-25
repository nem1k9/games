using System.Collections.Generic;
using Gnomes.Core;
using Godot;
using SockGang.Rendering;

namespace SockGang.World
{
    /// <summary>
    /// A loose item. On the host it is a dynamic rigid body; on clients a frozen (kinematic) proxy
    /// interpolated from snapshots.
    /// </summary>
    public partial class Prop : RigidBody3D
    {
        public ushort Id;
        public ItemDef Def;
        public ModelInstance Model;
        public bool Authority;
        public float HalfHeight;
        public bool Removed;
        public int HeldMask; // players currently holding it (host side), bit per player id
        public byte LastTouchedBy = 255;
        public float SpawnTime;
        float lastImpact;
        bool tipped;
        Vector3 lastVel;

        struct Pose { public float T; public Vector3 P; public Quaternion R; }
        readonly List<Pose> poses = new List<Pose>(8);
        static readonly Dictionary<string, ConvexPolygonShape3D> hulls = new Dictionary<string, ConvexPolygonShape3D>();

        public bool IsHeld => HeldMask != 0;

        public static Prop Create(GameWorld world, ushort id, string kind, Vector3 pos, Quaternion rot, bool authority)
        {
            var def = ItemDefs.Get(kind);
            var p = new Prop { Name = $"Prop_{id}_{kind}", Id = id, Def = def, Authority = authority, SpawnTime = Clock.Now };
            p.CollisionLayer = Layers.Prop;
            p.CollisionMask = Layers.PropMask;
            p.Mass = Mathf.Max(0.02f, def.Mass);
            p.LinearDampMode = DampMode.Replace;
            p.AngularDampMode = DampMode.Replace;
            p.LinearDamp = 0.05f;
            p.AngularDamp = 0.4f;
            p.ContinuousCd = def.Mass < 0.6f;
            p.PhysicsMaterialOverride = new PhysicsMaterial { Friction = def.Friction, Bounce = def.Bounce };
            p.ContactMonitor = true;
            p.MaxContactsReported = 4;
            world.PropsRoot.AddChild(p);
            p.GlobalPosition = pos;
            p.Quaternion = rot.Normalized();
            p.Model = ModelLibrary.Instantiate(kind, p, ColliderMode.None);
            var model = p.Model.Model;
            if (model != null)
            {
                var boxes = ModelLibrary.ColliderBoxes(model);
                if (boxes.Count > 0)
                    foreach (var (xf, size) in boxes)
                        p.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size.Max(new Vector3(0.02f, 0.02f, 0.02f)) }, Transform = xf });
                else p.AddChild(new CollisionShape3D { Shape = Hull(model) });
                model.Bounds(out var mn, out _);
                p.HalfHeight = Mathf.Max(0.02f, -mn.y);
            }
            p.BodyEntered += p.OnBodyEntered;
            p.SetAuthority(authority);
            return p;
        }

        static ConvexPolygonShape3D Hull(Gnomes.Core.Models.ModelData model)
        {
            if (hulls.TryGetValue(model.Name, out var s)) return s;
            var pts = ModelLibrary.HullPoints(model);
            if (pts.Length > 160)
            {
                // keep the hull cheap: every n-th point is plenty for low-poly items
                var list = new List<Vector3>();
                int step = pts.Length / 160 + 1;
                for (int i = 0; i < pts.Length; i += step) list.Add(pts[i]);
                pts = list.ToArray();
            }
            s = new ConvexPolygonShape3D { Points = pts };
            hulls[model.Name] = s;
            return s;
        }

        public void SetAuthority(bool authority)
        {
            Authority = authority;
            Freeze = !authority;
            FreezeMode = FreezeModeEnum.Kinematic;
            if (!authority) poses.Clear();
        }

        // ---------------- host side ----------------

        void OnBodyEntered(Node other)
        {
            if (!Authority || Removed || GameWorld.Current == null) return;
            if (Clock.Now - SpawnTime < 1.5f) return; // settling at level start
            var otherVel = other is RigidBody3D rb ? rb.LinearVelocity : Vector3.Zero;
            float speed = (lastVel - otherVel).Length();
            if (speed > 2.5f && Clock.Now - lastImpact > 0.15f)
            {
                lastImpact = Clock.Now;
                float loud = Mathf.Clamp(speed / 12f, 0f, 1f) * Def.Loudness * Mathf.Clamp(Def.Mass, 0.3f, 3f);
                GameWorld.Current.OnPropImpact(this, GlobalPosition, loud);
            }
            if (Def.Has(ItemFlags.Breakable) && speed > Def.BreakImpulse * 3.3f)
                GameWorld.Current.BreakProp(this, GlobalPosition);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!Authority || Removed) return;
            lastVel = LinearVelocity;
            if (GlobalPosition.Y < -20f) GameWorld.Current?.RecoverFallenProp(this);
        }

        /// <summary>Host: did the item fall over (trash bin prank)?</summary>
        public bool CheckTipped()
        {
            if (tipped) return false;
            if (GlobalBasis.Y.Normalized().Dot(Vector3.Up) < 0.35f && LinearVelocity.LengthSquared() < 1f)
            {
                tipped = true;
                return true;
            }
            return false;
        }

        // ---------------- client side ----------------

        public void PushPose(float t, Vector3 p, Quaternion r)
        {
            if (poses.Count > 0 && t <= poses[poses.Count - 1].T) return;
            poses.Add(new Pose { T = t, P = p, R = r });
            if (poses.Count > 8) poses.RemoveAt(0);
        }

        public void Interpolate(float renderTime)
        {
            if (Authority || poses.Count == 0) return;
            if (poses.Count == 1 || renderTime <= poses[0].T)
            {
                ApplyPose(poses[0].P, poses[0].R);
                return;
            }
            for (int i = 0; i < poses.Count - 1; i++)
            {
                var a = poses[i];
                var b = poses[i + 1];
                if (renderTime >= a.T && renderTime <= b.T)
                {
                    float k = (renderTime - a.T) / Mathf.Max(1e-4f, b.T - a.T);
                    ApplyPose(a.P.Lerp(b.P, k), a.R.Slerp(b.R, k));
                    return;
                }
            }
            var last = poses[poses.Count - 1];
            ApplyPose(last.P, last.R);
        }

        void ApplyPose(Vector3 p, Quaternion r) => GlobalTransform = new Transform3D(new Basis(r.Normalized()), p);
    }
}
