using Godot;
using SockGang.Rendering;

namespace SockGang.World
{
    /// <summary>Tiny self-removing visual effects made from low-poly bits (no particle setup needed).</summary>
    public static class Fx
    {
        static BoxMesh cube;
        static SphereMesh blob;

        static Mesh Cube() => cube ??= new BoxMesh { Size = Vector3.One };
        static Mesh Blob() => blob ??= new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 4 };

        static FxLife Piece(Node3D parent, Mesh mesh, Material mat, Vector3 pos, float size)
        {
            var mi = new FxLife { Mesh = mesh, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            parent.AddChild(mi);
            mi.GlobalPosition = pos;
            mi.Scale = Vector3.One * size;
            return mi;
        }

        public static void Shards(GameWorld w, Vector3 pos)
        {
            if (w == null) return;
            for (int i = 0; i < 9; i++)
            {
                var c = new Color(GMath.Rand(0.78f, 1f), GMath.Rand(0.78f, 1f), GMath.Rand(0.78f, 1f));
                var fx = Piece(w.FxRoot, Cube(), ModelLibrary.TintMaterial(c), pos + GMath.RandInSphere() * 0.15f, GMath.Rand(0.05f, 0.12f));
                fx.Rotation = new Vector3(GMath.Rand(0, 6), GMath.Rand(0, 6), GMath.Rand(0, 6));
                fx.Init(3f, false, new Vector3(GMath.Rand(-3f, 3f), GMath.Rand(2f, 5f), GMath.Rand(-3f, 3f)), -20f, 1f, true);
            }
        }

        public static void Sparkles(GameWorld w, Vector3 pos, Color color, int n = 10, float speed = 3f)
        {
            if (w == null) return;
            var mat = ModelLibrary.GlowMaterial(color);
            for (int i = 0; i < n; i++)
                Piece(w.FxRoot, Blob(), mat, pos, GMath.Rand(0.05f, 0.12f)).Init(GMath.Rand(0.6f, 1.1f), true, GMath.RandOnSphere() * speed + Vector3.Up * speed * 0.5f, -3f);
        }

        public static void Puff(GameWorld w, Vector3 pos, float size = 1f)
        {
            if (w == null) return;
            var mat = ModelLibrary.TintMaterial(new Color(0.92f, 0.92f, 0.92f));
            for (int i = 0; i < 7; i++)
                Piece(w.FxRoot, Blob(), mat, pos + GMath.RandInSphere() * 0.3f * size, GMath.Rand(0.25f, 0.45f) * size).Init(GMath.Rand(0.5f, 0.9f), true, GMath.RandOnSphere() * 1.2f + Vector3.Up * 0.8f, 0.5f, 1.6f);
        }

        public static void Splash(GameWorld w, Vector3 pos)
        {
            if (w == null) return;
            var mat = ModelLibrary.TintMaterial(new Color(0.47f, 0.75f, 0.92f));
            for (int i = 0; i < 12; i++)
                Piece(w.FxRoot, Blob(), mat, pos, GMath.Rand(0.06f, 0.14f)).Init(GMath.Rand(0.5f, 0.9f), true, new Vector3(GMath.Rand(-2f, 2f), GMath.Rand(3f, 6f), GMath.Rand(-2f, 2f)), -18f);
        }

        /// <summary>Dizzy stars above a stunned head.</summary>
        public static void Stars(GameWorld w, Vector3 pos) => Sparkles(w, pos, new Color(1f, 0.9f, 0.35f), 6, 1.5f);
    }

    public partial class FxLife : MeshInstance3D
    {
        float life, age, gravity, grow;
        bool shrink, bounce;
        Vector3 vel, startScale, spin;

        public void Init(float life, bool shrink, Vector3 vel = default, float gravity = 0f, float grow = 1f, bool bounce = false)
        {
            this.life = life;
            this.shrink = shrink;
            this.vel = vel;
            this.gravity = gravity;
            this.grow = grow;
            this.bounce = bounce;
            startScale = Scale;
            spin = bounce ? GMath.RandInSphere() * 10f : Vector3.Zero;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            age += dt;
            if (age >= life)
            {
                QueueFree();
                return;
            }
            vel.Y += gravity * dt;
            var p = GlobalPosition + vel * dt;
            if (bounce && p.Y < 0.03f && vel.Y < 0)
            {
                // shards skitter on the floor
                p.Y = 0.03f;
                vel = new Vector3(vel.X * 0.5f, -vel.Y * 0.3f, vel.Z * 0.5f);
                spin *= 0.5f;
            }
            GlobalPosition = p;
            if (spin != Vector3.Zero) Rotation += spin * dt;
            float k = age / life;
            if (shrink) Scale = startScale * Mathf.Lerp(1f, 0f, k * k) * Mathf.Lerp(1f, grow, k);
            else if (k > 0.8f) Scale = startScale * Mathf.Max(0.01f, 1f - (k - 0.8f) / 0.2f);
        }
    }
}
