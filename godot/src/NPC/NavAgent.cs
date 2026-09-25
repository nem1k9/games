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
            Vector3 desired = Vector3.Zero;
            if (HasPath)
            {
                var target = path[corner];
                var to = target - Pos;
                to.Y = 0;
                float remaining = RemainingDistance;
                while (to.Length() < 0.25f && corner < path.Length - 1)
                {
                    corner++;
                    target = path[corner];
                    to = target - Pos;
                    to.Y = 0;
                }
                if (remaining > StoppingDistance && to.LengthSquared() > 1e-6f)
                {
                    float slow = Mathf.Clamp(remaining / 1.5f, 0.3f, 1f); // ease into the destination
                    desired = to.Normalized() * Speed * slow;
                }
                // follow the navmesh height
                Pos.Y = Mathf.Lerp(Pos.Y, target.Y, Mathf.Clamp(dt * 8f, 0f, 1f));
            }
            Velocity = GMath.MoveTowards(Velocity, desired, Acceleration * dt);
            Pos += Velocity * dt;
            return Pos;
        }
    }
}
