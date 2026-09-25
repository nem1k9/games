using System;
using Godot;

namespace SockGang
{
    public static class GMath
    {
        public static Vector3 Flat(this Vector3 v) => new Vector3(v.X, 0, v.Z);

        public static float Damp(float current, float target, float lambda, float dt) => Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));
        public static Vector3 Damp(Vector3 current, Vector3 target, float lambda, float dt) => current.Lerp(target, 1f - Mathf.Exp(-lambda * dt));

        public static Vector3 ClampLength(Vector3 v, float max) => v.LengthSquared() > max * max ? v.Normalized() * max : v;

        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float maxDelta)
        {
            var d = b - a;
            float len = d.Length();
            return len <= maxDelta || len < 1e-6f ? b : a + d / len * maxDelta;
        }

        public static float MoveTowards(float a, float b, float maxDelta) => Mathf.Abs(b - a) <= maxDelta ? b : a + Mathf.Sign(b - a) * maxDelta;

        /// <summary>Godot forward (-Z) for a yaw.</summary>
        public static Vector3 YawForward(float yaw) => new Vector3(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw));

        /// <summary>Yaw that makes -Z point along dir.</summary>
        public static float YawOf(Vector3 dir) => Mathf.Atan2(-dir.X, -dir.Z);

        /// <summary>Look direction for yaw (around Y) and pitch (positive = up).</summary>
        public static Vector3 LookDir(float yaw, float pitch)
        {
            float cp = Mathf.Cos(pitch);
            return new Vector3(-Mathf.Sin(yaw) * cp, Mathf.Sin(pitch), -Mathf.Cos(yaw) * cp);
        }

        public static Basis YawBasis(float yaw) => new Basis(Vector3.Up, yaw);

        /// <summary>Basis whose -Z looks along forward, with the given up hint (like Unity's LookRotation but Godot-style).</summary>
        public static Basis LookBasis(Vector3 forward, Vector3 up)
        {
            if (forward.LengthSquared() < 1e-8f) return Basis.Identity;
            var f = forward.Normalized();
            if (Mathf.Abs(f.Dot(up.Normalized())) > 0.999f) up = Mathf.Abs(f.Y) > 0.9f ? Vector3.Forward : Vector3.Up;
            return Basis.LookingAt(f, up);
        }

        static readonly Random rng = new Random();
        public static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        public static int RandInt(int a, int bExclusive) => rng.Next(a, bExclusive);
        public static float Rand01 => (float)rng.NextDouble();

        public static Vector3 RandInSphere()
        {
            while (true)
            {
                var v = new Vector3(Rand(-1, 1), Rand(-1, 1), Rand(-1, 1));
                if (v.LengthSquared() <= 1f) return v;
            }
        }

        public static Vector3 RandOnSphere()
        {
            var v = RandInSphere();
            return v.LengthSquared() < 1e-6f ? Vector3.Up : v.Normalized();
        }

        public static Quaternion RandRotation() => new Quaternion(RandOnSphere(), Rand(0, Mathf.Tau)).Normalized();
    }
}
