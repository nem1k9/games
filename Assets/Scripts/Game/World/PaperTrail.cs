using System.Collections.Generic;
using UnityEngine;

namespace Gnomes.World
{
    /// <summary>
    /// A ribbon of toilet paper left on the floor behind a rolling roll. Purely visual and built on
    /// every machine from the roll's own (interpolated) movement, so it needs no network traffic.
    /// </summary>
    public class PaperTrail : MonoBehaviour
    {
        const float Step = 0.35f; // distance between ribbon points
        const float Width = 0.34f;
        const int MaxPoints = 160; // ~55 units of paper, then the roll is empty
        const float MaxHeight = 0.9f; // only while rolling on (or near) the floor

        readonly List<Vector3> points = new List<Vector3>();
        readonly HashSet<int> breaks = new HashSet<int>(); // no paper between point i-1 and i
        Mesh mesh;
        GameObject ribbon;
        Vector3 last;
        bool started;

        public static void Attach(Prop p)
        {
            p.gameObject.AddComponent<PaperTrail>();
        }

        void Start()
        {
            ribbon = new GameObject("PaperTrail");
            var w = GameWorld.Current;
            ribbon.transform.SetParent(w != null ? w.FxRoot : null, false);
            mesh = new Mesh { name = "paperTrail" };
            mesh.MarkDynamic();
            ribbon.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = ribbon.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Rendering.ModelLibrary.TintMaterial(new Color32(246, 244, 236, 255));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            if (points.Count >= MaxPoints) return;
            var pos = transform.position;
            if (pos.y > MaxHeight) { started = false; return; }
            var floorPoint = FloorBelow(pos);
            if (!started)
            {
                // (re)start a strip where the roll touched down
                started = true;
                last = floorPoint;
                if (points.Count > 0) breaks.Add(points.Count);
                points.Add(floorPoint);
                return;
            }
            var d = floorPoint - last;
            d.y = 0;
            if (d.magnitude < Step) return;
            if (d.magnitude > 3f) breaks.Add(points.Count); // teleported: new strip, no line through the air
            points.Add(floorPoint);
            last = floorPoint;
            Rebuild();
        }

        static Vector3 FloorBelow(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out var hit, 2f, Layers.World, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.015f;
            return new Vector3(p.x, 0.015f, p.z);
        }

        void Rebuild()
        {
            int n = points.Count;
            if (n < 2) return;
            var verts = new Vector3[n * 2];
            var norms = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var tris = new List<int>((n - 1) * 12);
            float along = 0;
            for (int i = 0; i < n; i++)
            {
                var a = points[Mathf.Max(0, i - 1)];
                var b = points[Mathf.Min(n - 1, i + 1)];
                var dir = b - a;
                dir.y = 0;
                if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
                var side = Vector3.Cross(Vector3.up, dir.normalized) * (Width * 0.5f);
                // a little wobble so it looks like crumpled paper, not a road marking
                float wob = Mathf.Sin(i * 1.7f) * 0.04f;
                verts[i * 2] = points[i] - side + side.normalized * wob;
                verts[i * 2 + 1] = points[i] + side + side.normalized * wob;
                norms[i * 2] = norms[i * 2 + 1] = Vector3.up;
                if (i > 0) along += Vector3.Distance(points[i], points[i - 1]);
                uvs[i * 2] = new Vector2(0, along);
                uvs[i * 2 + 1] = new Vector2(1, along);
                if (i < n - 1 && !breaks.Contains(i + 1))
                {
                    int v = i * 2;
                    // both windings: the ribbon is visible from above and below (e.g. through a glass table)
                    tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
                    tris.Add(v + 1); tris.Add(v + 2); tris.Add(v + 3);
                    tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
                    tris.Add(v + 1); tris.Add(v + 3); tris.Add(v + 2);
                }
            }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }
    }
}
