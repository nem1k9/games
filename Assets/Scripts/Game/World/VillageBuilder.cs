using Gnomes.Core;
using Gnomes.Rendering;
using UnityEngine;

namespace Gnomes.World
{
    /// <summary>The hub: the gnomes' village under the porch.</summary>
    public static class VillageBuilder
    {
        const float HS = GameConsts.HS;

        public static void Build(GameWorld w)
        {
            var root = w.StaticRoot;
            var village = ModelLibrary.Instantiate("village", root, Layers.Default);
            foreach (var lm in village.Markers("LIGHT")) HouseBuilder.AddMarkerLight(village, lm);

            var sock = ModelLibrary.Instantiate("greatSock", root, Layers.Default);
            sock.transform.SetPositionAndRotation(new Vector3(0, 0.3f, -1.55f * HS), Quaternion.identity);
            foreach (var lm in sock.Markers("LIGHT")) HouseBuilder.AddMarkerLight(sock, lm);
            sock.gameObject.AddComponent<GreatSockIdle>();
            w.HighGnome = sock.transform;

            var knit = ModelLibrary.Instantiate("knittingCorner", root, Layers.Default);
            knit.transform.SetPositionAndRotation(new Vector3(-2.2f * HS, 0, -0.9f * HS), Quaternion.Euler(0, 35, 0));
            w.CraftBench = knit.transform;

            var tunnel = ModelLibrary.Instantiate("sockTunnel", root, Layers.Default);
            tunnel.transform.SetPositionAndRotation(new Vector3(0, 0, 2.05f * HS), Quaternion.Euler(0, 180, 0));
            foreach (var lm in tunnel.Markers("LIGHT")) HouseBuilder.AddMarkerLight(tunnel, lm);
            var pz = tunnel.Node("ZONE_portal");
            w.PortalZone = pz ? pz.GetComponent<BoxCollider>() : null;

            var center = village.Node("SPAWN_center");
            var c = center ? center.position : new Vector3(0, 0.2f, 1.2f * HS);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                w.Spawns.Add(c + new Vector3(Mathf.Cos(a) * 1.6f, 0.2f, Mathf.Sin(a) * 1.2f));
            }
            w.PlayArea = new Bounds(new Vector3(0, 2, 0), new Vector3(7 * HS, 6, 5 * HS));
            w.FloorY = 0;
            Physics.SyncTransforms();
        }
    }

    /// <summary>The Great Sock sways gently and breathes.</summary>
    public class GreatSockIdle : MonoBehaviour
    {
        Quaternion baseRot;
        Vector3 baseScale;

        void Start()
        {
            baseRot = transform.rotation;
            baseScale = transform.localScale;
        }

        void Update()
        {
            float t = Time.time;
            transform.rotation = baseRot * Quaternion.Euler(Mathf.Sin(t * 0.9f) * 2.5f, Mathf.Sin(t * 0.4f) * 6f, Mathf.Sin(t * 0.7f) * 2f);
            transform.localScale = new Vector3(baseScale.x, baseScale.y * (1f + Mathf.Sin(t * 1.6f) * 0.02f), baseScale.z);
        }
    }
}
