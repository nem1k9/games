using Gnomes.Rendering;
using UnityEngine;

namespace Gnomes.World
{
    /// <summary>Tiny self-destroying visual effects made from low-poly bits (no particle shaders needed).</summary>
    public static class Fx
    {
        static Mesh cube, ico;

        static Mesh Cube()
        {
            if (cube != null) return cube;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return cube;
        }

        static Mesh Blob()
        {
            if (ico != null) return ico;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ico = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return ico;
        }

        static GameObject Piece(Transform parent, Mesh mesh, Material mat, Vector3 pos, float size)
        {
            var go = new GameObject("fx");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Physical debris shards (local only, not networked).</summary>
        public static void Shards(GameWorld w, Vector3 pos, string kind)
        {
            if (w == null) return;
            var mat = ModelLibrary.LitMaterial;
            int n = 8;
            for (int i = 0; i < n; i++)
            {
                var go = Piece(w.FxRoot, Cube(), mat, pos + Random.insideUnitSphere * 0.15f, Random.Range(0.04f, 0.1f));
                go.layer = Layers.Debris;
                var col = go.AddComponent<BoxCollider>();
                col.size = Vector3.one;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.02f;
                rb.SetVel(new Vector3(Random.Range(-3f, 3f), Random.Range(2f, 5f), Random.Range(-3f, 3f)));
                rb.angularVelocity = Random.insideUnitSphere * 20f;
                go.transform.rotation = Random.rotation;
                // tint shards by the item's palette: use a random light colour
                go.GetComponent<MeshRenderer>().sharedMaterial = ModelLibrary.TintMaterial(new Color32((byte)Random.Range(200, 255), (byte)Random.Range(200, 255), (byte)Random.Range(200, 255), 255));
                go.AddComponent<FxLife>().Init(6f, false);
            }
        }

        public static void Sparkles(GameWorld w, Vector3 pos, Color32 color, int n = 10, float speed = 3f)
        {
            if (w == null) return;
            var mat = ModelLibrary.GlowMaterial(color);
            for (int i = 0; i < n; i++)
            {
                var go = Piece(w.FxRoot, Blob(), mat, pos, Random.Range(0.05f, 0.12f));
                go.AddComponent<FxLife>().Init(Random.Range(0.6f, 1.1f), true, Random.onUnitSphere * speed + Vector3.up * speed * 0.5f, -3f);
            }
        }

        public static void Puff(GameWorld w, Vector3 pos, float size = 1f)
        {
            if (w == null) return;
            var mat = ModelLibrary.TintMaterial(new Color32(235, 235, 235, 255));
            for (int i = 0; i < 7; i++)
            {
                var go = Piece(w.FxRoot, Blob(), mat, pos + Random.insideUnitSphere * 0.3f * size, Random.Range(0.25f, 0.45f) * size);
                go.AddComponent<FxLife>().Init(Random.Range(0.5f, 0.9f), true, Random.onUnitSphere * 1.2f + Vector3.up * 0.8f, 0.5f, 1.6f);
            }
        }

        public static void Splash(GameWorld w, Vector3 pos)
        {
            if (w == null) return;
            var mat = ModelLibrary.TintMaterial(new Color32(120, 190, 235, 255));
            for (int i = 0; i < 12; i++)
            {
                var go = Piece(w.FxRoot, Blob(), mat, pos, Random.Range(0.06f, 0.14f));
                go.AddComponent<FxLife>().Init(Random.Range(0.5f, 0.9f), true, new Vector3(Random.Range(-2f, 2f), Random.Range(3f, 6f), Random.Range(-2f, 2f)), -18f);
            }
        }

        public static void Stars(GameWorld w, Vector3 pos)
        {
            // dizzy stars above a stunned head
            Sparkles(w, pos, new Color32(255, 230, 90, 255), 6, 1.5f);
        }
    }

    public class FxLife : MonoBehaviour
    {
        float life, age, gravity, grow;
        bool shrink;
        Vector3 vel, startScale;

        public void Init(float life, bool shrink, Vector3 vel = default, float gravity = 0f, float grow = 1f)
        {
            this.life = life;
            this.shrink = shrink;
            this.vel = vel;
            this.gravity = gravity;
            this.grow = grow;
            startScale = transform.localScale;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= life)
            {
                Destroy(gameObject);
                return;
            }
            if (GetComponent<Rigidbody>() == null)
            {
                vel.y += gravity * Time.deltaTime;
                transform.position += vel * Time.deltaTime;
            }
            float k = age / life;
            if (shrink) transform.localScale = startScale * Mathf.Lerp(1f, 0f, k * k) * Mathf.Lerp(1f, grow, k);
            else if (k > 0.8f) transform.localScale = startScale * (1f - (k - 0.8f) / 0.2f);
        }
    }
}
