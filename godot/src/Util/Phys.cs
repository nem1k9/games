using Godot;
using Godot.Collections;

namespace SockGang
{
    public struct Hit
    {
        public Vector3 Point;
        public Vector3 Normal;
        public Node Collider;
        public float Distance;
    }

    /// <summary>Physics queries (raycast, sphere cast, overlap) in the current 3D world.</summary>
    public static class Phys
    {
        public static PhysicsDirectSpaceState3D Space;
        static readonly SphereShape3D sphere = new SphereShape3D();
        static readonly BoxShape3D box = new BoxShape3D();

        public static bool Raycast(Vector3 from, Vector3 dir, float dist, uint mask, out Hit hit, Array<Rid> exclude = null)
        {
            hit = default;
            if (Space == null || dist <= 0) return false;
            var q = PhysicsRayQueryParameters3D.Create(from, from + dir.Normalized() * dist, mask, exclude ?? new Array<Rid>());
            q.CollideWithAreas = false;
            var r = Space.IntersectRay(q);
            if (r.Count == 0) return false;
            hit.Point = (Vector3)r["position"];
            hit.Normal = (Vector3)r["normal"];
            hit.Collider = r["collider"].AsGodotObject() as Node;
            hit.Distance = from.DistanceTo(hit.Point);
            return true;
        }

        public static bool Raycast(Vector3 from, Vector3 dir, float dist, uint mask) => Raycast(from, dir, dist, mask, out _);

        /// <summary>Sweep a sphere along dir; reports the first thing it touches.</summary>
        public static bool SphereCast(Vector3 from, float radius, Vector3 dir, float dist, uint mask, out Hit hit, Array<Rid> exclude = null)
        {
            hit = default;
            if (Space == null || dist <= 0) return false;
            dir = dir.Normalized();
            sphere.Radius = radius;
            var q = new PhysicsShapeQueryParameters3D
            {
                Shape = sphere,
                Transform = new Transform3D(Basis.Identity, from),
                Motion = dir * dist,
                CollisionMask = mask,
                CollideWithAreas = false,
            };
            if (exclude != null) q.Exclude = exclude;
            var frac = Space.CastMotion(q); // [safe, unsafe] fractions; 1 = nothing in the way
            if (frac.Length < 2 || frac[1] >= 1f) return false;
            float travel = dist * frac[1];
            q.Transform = new Transform3D(Basis.Identity, from + dir * (travel + radius * 0.25f));
            q.Motion = Vector3.Zero;
            var info = Space.GetRestInfo(q);
            if (info.Count == 0)
            {
                // fall back to a ray to find what we touched
                if (!Raycast(from, dir, dist + radius, mask, out hit, exclude)) return false;
                return true;
            }
            hit.Point = (Vector3)info["point"];
            hit.Normal = (Vector3)info["normal"];
            var id = (ulong)info["collider_id"];
            hit.Collider = GodotObject.InstanceFromId(id) as Node;
            hit.Distance = travel;
            return true;
        }

        public static bool CheckBox(Vector3 center, Vector3 halfExtents, uint mask)
        {
            if (Space == null) return false;
            box.Size = halfExtents * 2f;
            var q = new PhysicsShapeQueryParameters3D { Shape = box, Transform = new Transform3D(Basis.Identity, center), CollisionMask = mask, CollideWithAreas = false };
            return Space.IntersectShape(q, 1).Count > 0;
        }

        public static bool CheckSphere(Vector3 center, float radius, uint mask)
        {
            if (Space == null) return false;
            sphere.Radius = radius;
            var q = new PhysicsShapeQueryParameters3D { Shape = sphere, Transform = new Transform3D(Basis.Identity, center), CollisionMask = mask, CollideWithAreas = false };
            return Space.IntersectShape(q, 1).Count > 0;
        }

        /// <summary>Walk up from a collider to the first ancestor of type T (the collider itself included).</summary>
        public static T Owner<T>(Node n) where T : class
        {
            while (n != null)
            {
                if (n is T t) return t;
                n = n.GetParent();
            }
            return null;
        }
    }
}
