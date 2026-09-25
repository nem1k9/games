using System.Collections.Generic;
using Gnomes.Core;
using Gnomes.Core.Models;
using Gnomes.Rendering;
using UnityEngine;

namespace Gnomes.World
{
    /// <summary>
    /// A loose physics item. On the host it is a dynamic rigidbody; on clients it is a kinematic
    /// proxy interpolated from snapshots.
    /// </summary>
    public class Prop : MonoBehaviour
    {
        public ushort Id;
        public ItemDef Def;
        public Rigidbody Rb;
        public ModelInstance Model;
        public bool Authority;
        public float HalfHeight;
        public bool Removed;

        /// <summary>Players currently holding this prop (host side), bit per player id.</summary>
        public int HeldMask;
        public float LastHeldTime = -99;
        public byte LastTouchedBy = 255;
        float lastImpact;
        bool tipped;
        public float SpawnTime;

        // --- client interpolation ---
        struct Pose { public float T; public Vector3 P; public Quaternion R; }
        readonly List<Pose> poses = new List<Pose>(8);

        static readonly Dictionary<string, Mesh> hullCache = new Dictionary<string, Mesh>();

        public bool IsHeld => HeldMask != 0;

        public static Prop Create(GameWorld world, ushort id, string kind, Vector3 pos, Quaternion rot, bool authority)
        {
            var def = ItemDefs.Get(kind);
            var inst = ModelLibrary.Instantiate(kind, world.PropsRoot, Layers.Prop);
            var go = inst.gameObject;
            go.name = $"Prop_{id}_{kind}";
            go.transform.SetPositionAndRotation(pos, rot);
            var p = go.AddComponent<Prop>();
            p.Id = id;
            p.Def = def;
            p.Model = inst;
            p.Authority = authority;
            p.SpawnTime = Time.time;
            inst.SetLayerRecursive(Layers.Prop);

            // colliders: COL_ boxes from the model, otherwise a convex hull of the mesh
            bool hasBoxes = inst.Markers("COL").Count > 0;
            Collider main = null;
            if (!hasBoxes)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = HullFor(inst.Model);
                mc.convex = true;
                main = mc;
            }
            float friction = def.Friction, bounce = def.Bounce;
            foreach (var c in go.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                Compat.SetMaterial(c, friction, bounce);
            }
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.02f, def.Mass);
            rb.SetDrag(0.05f);
            rb.SetAngularDrag(0.4f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = def.Mass < 0.3f ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.ContinuousSpeculative;
            rb.maxDepenetrationVelocity = 4f;
            p.Rb = rb;
            inst.Model.Bounds(out var mn, out var mx);
            p.HalfHeight = Mathf.Max(0.02f, -mn.y);
            p.SetAuthority(authority);
            if (main == null && hasBoxes) main = go.GetComponentInChildren<Collider>();
            return p;
        }

        static Mesh HullFor(ModelData model)
        {
            if (hullCache.TryGetValue(model.Name, out var m)) return m;
            var verts = new List<Vector3>();
            for (int i = 0; i < model.Nodes.Length; i++)
            {
                var n = model.Nodes[i];
                if (n.Meshes.Length == 0) continue;
                model.NodeToModel(i, out var p, out var q, out var s);
                foreach (var sm in n.Meshes)
                {
                    if (sm.Kind == MeshKind.Emissive && n.Meshes.Length > 1) continue;
                    for (int v = 0; v < sm.VertexCount; v++)
                    {
                        var lp = new V3(sm.Positions[v * 3] * s.x, sm.Positions[v * 3 + 1] * s.y, sm.Positions[v * 3 + 2] * s.z);
                        verts.Add((p + q * lp).ToUnity());
                    }
                }
            }
            // dedupe + decimate very dense meshes to keep hull cooking fast
            var uniq = new List<Vector3>();
            var seen = new HashSet<Vector3Int>();
            foreach (var v in verts)
            {
                var k = new Vector3Int(Mathf.RoundToInt(v.x * 200), Mathf.RoundToInt(v.y * 200), Mathf.RoundToInt(v.z * 200));
                if (seen.Add(k)) uniq.Add(v);
            }
            if (uniq.Count < 4)
            {
                // degenerate (flat) item: make a tiny box
                uniq.AddRange(new[] { new Vector3(-0.05f, -0.02f, -0.05f), new Vector3(0.05f, 0.02f, 0.05f), new Vector3(0.05f, -0.02f, -0.05f), new Vector3(-0.05f, 0.02f, 0.05f) });
            }
            // every vertex must be referenced by a triangle so the hull cooker sees the whole point cloud
            var tris = new List<int>();
            int nv = uniq.Count;
            for (int i = 0; i < nv; i++) { tris.Add(i); tris.Add((i + 1) % nv); tris.Add((i + 2) % nv); }
            m = new Mesh { name = "hull_" + model.Name };
            m.SetVertices(uniq);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            hullCache[model.Name] = m;
            return m;
        }

        public void SetAuthority(bool authority)
        {
            Authority = authority;
            Rb.isKinematic = !authority;
            Rb.interpolation = authority ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            if (!authority) poses.Clear();
        }

        // ---------------- host side ----------------

        void OnCollisionEnter(Collision c)
        {
            if (!Authority || Removed || GameWorld.Current == null) return;
            float speed = c.relativeVelocity.magnitude;
            if (Time.time - SpawnTime < 1.5f) return; // settling at level start
            if (speed > 2.5f && Time.time - lastImpact > 0.15f)
            {
                lastImpact = Time.time;
                float loud = Mathf.Clamp01(speed / 12f) * Def.Loudness * Mathf.Clamp(Def.Mass, 0.3f, 3f);
                GameWorld.Current.OnPropImpact(this, c.GetContact(0).point, loud, speed);
            }
            if (Def.Has(ItemFlags.Breakable) && speed > Def.BreakImpulse * 3.3f)
            {
                // being gently carried by a gnome should not shatter things
                GameWorld.Current.BreakProp(this, c.GetContact(0).point);
            }
        }

        void FixedUpdate()
        {
            if (!Authority || Removed) return;
            if (HeldMask != 0) LastHeldTime = Time.time;
            // fall out of the world -> respawn in the garden to avoid losing task items
            if (transform.position.y < -20f) GameWorld.Current?.RecoverFallenProp(this);
        }

        /// <summary>Host: check if the item fell over (trash bin task).</summary>
        public bool CheckTipped()
        {
            if (tipped) return false;
            if (Vector3.Dot(transform.up, Vector3.up) < 0.35f && Rb.Vel().sqrMagnitude < 1f)
            {
                tipped = true;
                return true;
            }
            return false;
        }

        // ---------------- client side ----------------

        public void PushPose(float t, Vector3 p, Quaternion r)
        {
            if (poses.Count > 0 && t <= poses[poses.Count - 1].T) return;
            poses.Add(new Pose { T = t, P = p, R = r });
            if (poses.Count > 8) poses.RemoveAt(0);
        }

        public void Interpolate(float renderTime)
        {
            if (Authority || poses.Count == 0) return;
            if (poses.Count == 1 || renderTime <= poses[0].T)
            {
                ApplyPose(poses[0].P, poses[0].R);
                return;
            }
            for (int i = 0; i < poses.Count - 1; i++)
            {
                var a = poses[i];
                var b = poses[i + 1];
                if (renderTime >= a.T && renderTime <= b.T)
                {
                    float k = (renderTime - a.T) / Mathf.Max(1e-4f, b.T - a.T);
                    ApplyPose(Vector3.Lerp(a.P, b.P, k), Quaternion.Slerp(a.R, b.R, k));
                    return;
                }
            }
            var last = poses[poses.Count - 1];
            ApplyPose(last.P, last.R);
        }

        void ApplyPose(Vector3 p, Quaternion r)
        {
            Rb.MovePosition(p);
            Rb.MoveRotation(r);
            transform.SetPositionAndRotation(p, r);
        }
    }
}
