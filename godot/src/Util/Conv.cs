using Gnomes.Core;
using Godot;

namespace SockGang
{
    /// <summary>
    /// The shared rules, layouts and Blender models use Unity space (left-handed, +Z forward).
    /// Godot is right-handed with -Z forward: we mirror Z at the boundary, which keeps "front" as front.
    /// Positions: (x, y, z) -> (x, y, -z). Rotations: (x, y, z, w) -> (-x, -y, z, w). Yaw: -yaw.
    /// </summary>
    public static class Conv
    {
        public static Vector3 G(this V3 v) => new Vector3(v.x, v.y, -v.z);
        public static Quaternion G(this Q4 q) => new Quaternion(-q.x, -q.y, q.z, q.w);
        public static V3 U(this Vector3 v) => new V3(v.X, v.Y, -v.Z);
        public static Q4 U(this Quaternion q) => new Q4(-q.X, -q.Y, q.Z, q.W);

        /// <summary>Unity-space yaw (radians, Quaternion.Euler(0, deg, 0)) to a Godot Y rotation.</summary>
        public static float Yaw(float unityYaw) => -unityYaw;

        /// <summary>Unity Quaternion.Euler(x, y, z) in degrees (local rotations of Unity-authored rigs), mirrored to Godot.</summary>
        public static Quaternion UEuler(float xDeg, float yDeg, float zDeg)
        {
            var gx = new Quaternion(Vector3.Right, -Mathf.DegToRad(xDeg));
            var gy = new Quaternion(Vector3.Up, -Mathf.DegToRad(yDeg));
            var gz = new Quaternion(Vector3.Back, Mathf.DegToRad(zDeg)); // Back = +Z: Unity z-rotations keep their sign
            return gy * gx * gz;
        }

        public static Color C(int rgb, float a = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        public static Color C8(byte r, byte g, byte b) => new Color(r / 255f, g / 255f, b / 255f);
    }
}
