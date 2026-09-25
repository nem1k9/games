using System.Collections.Generic;
using Godot;
using SockGang.Rendering;

namespace SockGang.World
{
    /// <summary>
    /// A ribbon of toilet paper left on the floor behind a rolling roll. Visual only, built on every
    /// machine from the roll's (interpolated) movement, so it needs no network traffic.
    /// </summary>
    public partial class PaperTrail : Node
    {
        const float Step = 0.35f;
        const float Width = 0.34f;
        const int MaxPoints = 160;
        const float MaxHeight = 0.9f;

        readonly List<Vector3> points = new List<Vector3>();
        readonly HashSet<int> breaks = new HashSet<int>(); // no paper between point i-1 and i
        ArrayMesh mesh;
        Node3D roll;
        Vector3 last;
        bool started;

        public static void Attach(Prop p)
        {
            var t = new PaperTrail { Name = "PaperTrail", roll = p };
            p.AddChild(t);
        }

        public override void _Ready()
        {
            mesh = new ArrayMesh();
            var mi = new MeshInstance3D { Name = "PaperRibbon", Mesh = mesh, MaterialOverride = ModelLibrary.TintMaterial(new Color(0.96f, 0.95f, 0.92f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            // lives in the world, not under the roll, so the paper stays when the roll is flushed
            (GameWorld.Current?.FxRoot ?? (Node)GetTree().Root).AddChild(mi);
        }

        public override void _Process(double delta)
        {
            if (points.Count >= MaxPoints || roll == null) return;
            var pos = roll.GlobalPosition;
            if (pos.Y > MaxHeight)
            {
                started = false;
                return;
            }
            var floorPoint = FloorBelow(pos);
            if (!started)
            {
                started = true;
                last = floorPoint;
                if (points.Count > 0) breaks.Add(points.Count);
                points.Add(floorPoint);
                return;
            }
            var d = floorPoint - last;
            d.Y = 0;
            if (d.Length() < Step) return;
            if (d.Length() > 3f) breaks.Add(points.Count);
            points.Add(floorPoint);
            last = floorPoint;
            Rebuild();
        }

        static Vector3 FloorBelow(Vector3 p)
        {
            if (Phys.Raycast(p + Vector3.Up * 0.3f, Vector3.Down, 2f, Layers.Solid, out var hit)) return hit.Point + Vector3.Up * 0.015f;
            return new Vector3(p.X, 0.015f, p.Z);
        }

        void Rebuild()
        {
            int n = points.Count;
            if (n < 2) return;
            var verts = new Vector3[n * 2];
            var norms = new Vector3[n * 2];
            var idx = new List<int>((n - 1) * 12);
            for (int i = 0; i < n; i++)
            {
                var a = points[Mathf.Max(0, i - 1)];
                var b = points[Mathf.Min(n - 1, i + 1)];
                var dir = b - a;
                dir.Y = 0;
                if (dir.LengthSquared() < 1e-6f) dir = Vector3.Forward;
                var side = Vector3.Up.Cross(dir.Normalized()) * (Width * 0.5f);
                float wob = Mathf.Sin(i * 1.7f) * 0.04f;
                verts[i * 2] = points[i] - side + side.Normalized() * wob;
                verts[i * 2 + 1] = points[i] + side + side.Normalized() * wob;
                norms[i * 2] = norms[i * 2 + 1] = Vector3.Up;
                if (i < n - 1 && !breaks.Contains(i + 1))
                {
                    int v = i * 2;
                    // both windings: visible from above and below
                    idx.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3, v, v + 1, v + 2, v + 1, v + 3, v + 2 });
                }
            }
            mesh.ClearSurfaces();
            if (idx.Count == 0) return;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = verts;
            arrays[(int)Mesh.ArrayType.Normal] = norms;
            arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }
    }
}
