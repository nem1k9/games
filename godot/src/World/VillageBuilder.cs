using Gnomes.Core;
using Godot;
using SockGang.Rendering;

namespace SockGang.World
{
    /// <summary>The hub: the gnomes' village under the porch.</summary>
    public static class VillageBuilder
    {
        const float HS = GameConsts.HS;

        static void Lights(ModelInstance inst)
        {
            foreach (var lm in inst.Markers("LIGHT")) Furniture.MarkerLight(inst.Model?.Find(inst.NameOf(lm))?.Props, lm);
        }

        public static void Build(GameWorld w)
        {
            var root = w.StaticRoot;
            var village = ModelLibrary.Instantiate("village", root);
            Lights(village);

            var sock = ModelLibrary.Instantiate("greatSock", root);
            sock.Position = new V3(0, 0.3f, -1.55f * HS).G();
            Lights(sock);
            sock.AddChild(new GreatSockIdle());
            w.HighGnome = sock;

            var knit = ModelLibrary.Instantiate("knittingCorner", root);
            knit.Position = new V3(-2.2f * HS, 0, -0.9f * HS).G();
            knit.Rotation = new Vector3(0, Conv.Yaw(Mathf.DegToRad(35)), 0);
            w.CraftBench = knit;

            var tunnel = ModelLibrary.Instantiate("sockTunnel", root);
            tunnel.Position = new V3(0, 0, 2.05f * HS).G();
            tunnel.Rotation = new Vector3(0, Conv.Yaw(Mathf.Pi), 0);
            Lights(tunnel);
            w.PortalZone = tunnel.Node("ZONE_portal");

            var center = village.Node("SPAWN_center");
            var c = center != null ? center.GlobalPosition : new V3(0, 0.2f, 1.2f * HS).G();
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.Tau;
                w.Spawns.Add(c + new Vector3(Mathf.Cos(a) * 1.6f, 0.2f, Mathf.Sin(a) * 1.2f));
            }
            var size = new Vector3(7 * HS, 6, 5 * HS);
            w.PlayArea = new Aabb(new Vector3(0, 2, 0) - size / 2, size);
        }
    }

    /// <summary>The Great Sock sways gently and breathes.</summary>
    public partial class GreatSockIdle : Node
    {
        Node3D sock;
        Basis baseBasis;
        Vector3 baseScale;

        public override void _Ready()
        {
            sock = GetParent<Node3D>();
            baseBasis = sock.Basis.Orthonormalized();
            baseScale = sock.Scale;
        }

        public override void _Process(double delta)
        {
            float t = Clock.Now;
            var wobble = Basis.FromEuler(new Vector3(Mathf.DegToRad(Mathf.Sin(t * 0.9f) * 2.5f), Mathf.DegToRad(Mathf.Sin(t * 0.4f) * 6f), Mathf.DegToRad(Mathf.Sin(t * 0.7f) * 2f)));
            sock.Basis = (baseBasis * wobble).Scaled(new Vector3(baseScale.X, baseScale.Y * (1f + Mathf.Sin(t * 1.6f) * 0.02f), baseScale.Z));
        }
    }
}
