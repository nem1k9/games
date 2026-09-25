using UnityEngine;

namespace Gnomes
{
    /// <summary>
    /// Small helpers that hide Unity API renames between 2021/2022 and Unity 6
    /// (Rigidbody.velocity -> linearVelocity, drag -> linearDamping, PhysicMaterial -> PhysicsMaterial).
    /// </summary>
    public static class Compat
    {
#if UNITY_6000_0_OR_NEWER
        public static Vector3 Vel(this Rigidbody rb) => rb.linearVelocity;
        public static void SetVel(this Rigidbody rb, Vector3 v) => rb.linearVelocity = v;
        public static void SetDrag(this Rigidbody rb, float d) => rb.linearDamping = d;
        public static void SetAngularDrag(this Rigidbody rb, float d) => rb.angularDamping = d;
        public static float GetDrag(this Rigidbody rb) => rb.linearDamping;

        public static void SetFrictionless(Collider c)
        {
            c.sharedMaterial = Frictionless;
        }

        static PhysicsMaterial frictionless;
        public static PhysicsMaterial Frictionless
        {
            get
            {
                if (frictionless == null)
                    frictionless = new PhysicsMaterial("Frictionless") { dynamicFriction = 0, staticFriction = 0, bounciness = 0, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
                return frictionless;
            }
        }

        public static void SetMaterial(Collider c, float friction, float bounce)
        {
            c.sharedMaterial = new PhysicsMaterial { dynamicFriction = friction, staticFriction = friction * 1.1f, bounciness = bounce, bounceCombine = PhysicsMaterialCombine.Maximum };
        }

        public static T FindAny<T>() where T : Object => Object.FindFirstObjectByType<T>();
#else
        public static Vector3 Vel(this Rigidbody rb) => rb.velocity;
        public static void SetVel(this Rigidbody rb, Vector3 v) => rb.velocity = v;
        public static void SetDrag(this Rigidbody rb, float d) => rb.drag = d;
        public static void SetAngularDrag(this Rigidbody rb, float d) => rb.angularDrag = d;
        public static float GetDrag(this Rigidbody rb) => rb.drag;

        public static void SetFrictionless(Collider c)
        {
            c.sharedMaterial = Frictionless;
        }

        static PhysicMaterial frictionless;
        public static PhysicMaterial Frictionless
        {
            get
            {
                if (frictionless == null)
                    frictionless = new PhysicMaterial("Frictionless") { dynamicFriction = 0, staticFriction = 0, bounciness = 0, frictionCombine = PhysicMaterialCombine.Minimum, bounceCombine = PhysicMaterialCombine.Minimum };
                return frictionless;
            }
        }

        public static void SetMaterial(Collider c, float friction, float bounce)
        {
            c.sharedMaterial = new PhysicMaterial { dynamicFriction = friction, staticFriction = friction * 1.1f, bounciness = bounce, bounceCombine = PhysicMaterialCombine.Maximum };
        }

        public static T FindAny<T>() where T : Object => Object.FindObjectOfType<T>();
#endif

        public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0, v.z);

        public static float Damp(float current, float target, float lambda, float dt) => Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));
        public static Vector3 Damp(Vector3 current, Vector3 target, float lambda, float dt) => Vector3.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));
        public static Quaternion Damp(Quaternion current, Quaternion target, float lambda, float dt) => Quaternion.Slerp(current, target, 1f - Mathf.Exp(-lambda * dt));

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        public static void Destroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
