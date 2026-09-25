using Godot;

namespace SockGang.NPC
{
    /// <summary>
    /// Minimal path follower on the baked navigation mesh (like Unity's NavMeshAgent with
    /// updatePosition = false): the owner reads <see cref="Pos"/> each physics step.
    /// </summary>
    public class NavAgent
    {
        public bool Enabled = true;
        public Vector3 Pos;
        public Vector3 Velocity;
        public float Speed = 3f;
        public float Acceleration = 22f;
        public float StoppingDistance = 0.6f;
        Vector3[] path = System.Array.Empty<Vector3>();
        int corner;
        Vector3 destination;
        bool hasDestination;
        float repathAt;
        Rid map;

        public NavAgent(Rid map) => this.map = map;

        /// <summary>The navigation map synchronises a frame after a bake; queries before that fail.</summary>
        public bool MapReady => NavigationServer3D.MapGetIterationId(map) > 0;

        public bool HasPath => hasDestination && corner < path.Length;
        public bool PathPending => hasDestination && path.Length == 0;

        public float RemainingDistance
        {
            get
            {
                if (!hasDestination) return 0f;
                if (path.Length == 0) return float.PositiveInfinity;
                float d = 0;
                var p = Pos;
                for (int i = corner; i < path.Length; i++)
                {
                    d += p.Flat().DistanceTo(path[i].Flat());
                    p = path[i];
                }
                return d;
            }
        }

        public static bool SamplePosition(Rid map, Vector3 p, float maxDist, out Vector3 hit)
        {
            hit = p;
            if (NavigationServer3D.MapGetIterationId(map) == 0) return false;
            hit = NavigationServer3D.MapGetClosestPoint(map, p);
            return hit.DistanceTo(p) <= maxDist;
        }

        public bool Sample(Vector3 p, float maxDist, out Vector3 hit) => SamplePosition(map, p, maxDist, out hit);

        public void SetDestination(Vector3 dest)
        {
            if (hasDestination && dest.DistanceTo(destination) < 0.3f && path.Length > 0 && Clock.Now < repathAt) return;
            destination = dest;
            hasDestination = true;
            Repath();
        }

        void Repath()
        {
            repathAt = Clock.Now + 0.6f;
            if (!MapReady) return;
            path = NavigationServer3D.MapGetPath(map, Pos, destination, true);
            corner = path.Length > 1 ? 1 : 0;
        }

        public void ResetPath()
        {
            hasDestination = false;
            path = System.Array.Empty<Vector3>();
            corner = 0;
        }

        public void Warp(Vector3 p)
        {
            Pos = p;
            Velocity = Vector3.Zero;
            ResetPath();
        }

        /// <summary>Advance along the path; returns the new position.</summary>
        public Vector3 Step(float dt)
        {
            if (!Enabled) return Pos;
            if (hasDestination && (path.Length == 0 || Clock.Now >= repathAt)) Repath();
            float speed = Velocity.Length();
            float wantSpeed = 0f;
            Vector3 dir = speed > 1e-4f ? Velocity / speed : Vector3.Zero;
            if (HasPath)
            {
                // skip corners we are close to or have already passed (so we never orbit a corner)
                float reach = Mathf.Max(0.35f, speed * 0.2f);
                while (corner < path.Length - 1 && (Flat(path[corner] - Pos).Length() < reach || Passed(corner))) corner++;
                var target = path[corner];
                var to = Flat(target - Pos);
                float remaining = RemainingDistance;
                if (remaining > StoppingDistance && to.LengthSquared() > 1e-6f)
                {
                    dir = to.Normalized(); // steer straight at the corner; the body turns smoothly on its own
                    wantSpeed = Speed * Mathf.Clamp(remaining / 1.5f, 0.3f, 1f); // ease into the destination
                }
                // follow the navmesh height
                Pos.Y = Mathf.Lerp(Pos.Y, target.Y, Mathf.Clamp(dt * 8f, 0f, 1f));
            }
            speed = Mathf.MoveToward(speed, wantSpeed, Acceleration * dt);
            Velocity = dir * speed;
            Pos += Velocity * dt;
            return Pos;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.X, 0, v.Z);

        /// <summary>True once we are beyond the corner along the segment leading into it.</summary>
        bool Passed(int i)
        {
            if (i <= 0) return false;
            var seg = Flat(path[i] - path[i - 1]);
            return seg.LengthSquared() > 1e-4f && seg.Dot(Flat(Pos - path[i])) > 0f;
        }
    }
}
