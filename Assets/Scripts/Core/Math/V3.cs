using System;

namespace Gnomes.Core
{
    /// <summary>Engine-free 3D vector (Unity-compatible layout and conventions: left-handed, Y up).</summary>
    [Serializable]
    public struct V3 : IEquatable<V3>
    {
        public float x, y, z;

        public V3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static readonly V3 zero = new V3(0, 0, 0);
        public static readonly V3 one = new V3(1, 1, 1);
        public static readonly V3 up = new V3(0, 1, 0);
        public static readonly V3 forward = new V3(0, 0, 1);

        public static V3 operator +(V3 a, V3 b) => new V3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static V3 operator -(V3 a) => new V3(-a.x, -a.y, -a.z);
        public static V3 operator *(V3 a, float s) => new V3(a.x * s, a.y * s, a.z * s);
        public static V3 operator *(float s, V3 a) => new V3(a.x * s, a.y * s, a.z * s);
        public static V3 operator /(V3 a, float s) => new V3(a.x / s, a.y / s, a.z / s);

        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public V3 normalized { get { float m = magnitude; return m > 1e-6f ? this / m : zero; } }

        public static float Dot(V3 a, V3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static V3 Cross(V3 a, V3 b) => new V3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Distance(V3 a, V3 b) => (a - b).magnitude;
        public static V3 Lerp(V3 a, V3 b, float t) => a + (b - a) * t;
        public static V3 Min(V3 a, V3 b) => new V3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
        public static V3 Max(V3 a, V3 b) => new V3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));

        public bool Equals(V3 o) => x == o.x && y == o.y && z == o.z;
        public override bool Equals(object obj) => obj is V3 o && Equals(o);
        public override int GetHashCode() => (x.GetHashCode() * 397 ^ y.GetHashCode()) * 397 ^ z.GetHashCode();
        public override string ToString() => $"({x:0.###}, {y:0.###}, {z:0.###})";
    }

    /// <summary>Engine-free quaternion (x, y, z, w) with Unity's conventions.</summary>
    [Serializable]
    public struct Q4
    {
        public float x, y, z, w;
        public Q4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static readonly Q4 identity = new Q4(0, 0, 0, 1);

        public static Q4 operator *(Q4 a, Q4 b) => new Q4(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
            a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);

        public static V3 operator *(Q4 q, V3 v)
        {
            // v' = q v q^-1
            var u = new V3(q.x, q.y, q.z);
            var t = 2f * V3.Cross(u, v);
            return v + q.w * t + V3.Cross(u, t);
        }

        public static Q4 AngleAxis(float radians, V3 axis)
        {
            axis = axis.normalized;
            float s = (float)Math.Sin(radians * 0.5f);
            return new Q4(axis.x * s, axis.y * s, axis.z * s, (float)Math.Cos(radians * 0.5f));
        }

        public static Q4 Yaw(float radians) => AngleAxis(radians, V3.up);

        public override string ToString() => $"({x:0.###}, {y:0.###}, {z:0.###}, {w:0.###})";
    }
}
