using System.Globalization;
using UnityEngine;

namespace Gnomes.World
{
    /// <summary>
    /// A movable furniture part: hinged doors (oven, freezer, fridge, safe), sliding window sashes
    /// and toggle buttons (TV power, taps, flush). The host owns the target state; everyone animates.
    /// </summary>
    public class Mechanism : MonoBehaviour
    {
        public ushort Index;
        public string Role;
        public string Kind; // hinge | slide | button
        public char Axis = 'y';
        public float OpenAmount; // radians (hinge) or world units (slide)
        public bool HumanOnly;
        public Vector3 HandleLocal;
        public Furniture Owner;

        /// <summary>Target state 0..1 (host authoritative, replicated as a byte).</summary>
        public float Target;
        public float Current;
        public bool Locked; // safe before it is cracked
        public int LockHits; // punches needed to crack
        public float PressedTime = -10;

        Vector3 basePos;
        Quaternion baseRot;
        Rigidbody rb;

        public bool IsOpen => Target > 0.5f;
        public bool IsButton => Kind == "button";
        public Vector3 HandleWorld => transform.TransformPoint(HandleLocal);

        public void Init(ushort index, Furniture owner)
        {
            Index = index;
            Owner = owner;
            var props = owner.Model.Model.Find(name)?.Props;
            string Get(string k, string d) => props != null && props.TryGetValue(k, out var v) ? v : d;
            float F(string k, float d) => float.TryParse(Get(k, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : d;
            Role = Get("role", "door");
            Kind = Get("kind", "hinge");
            Axis = Get("axis", "y")[0];
            OpenAmount = F("open", 1.5f);
            HumanOnly = Get("humanOnly", "0") == "1";
            HandleLocal = new Vector3(F("hx", 0), F("hy", 0), F("hz", 0));
            basePos = transform.localPosition;
            baseRot = transform.localRotation;
            if (Kind != "button")
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            foreach (var c in GetComponentsInChildren<Collider>()) if (!c.isTrigger) c.gameObject.layer = Layers.Mech;
            if (Role == "safe")
            {
                Locked = true;
                LockHits = 5;
            }
        }

        void Update()
        {
            if (!IsButton) return;
            Current = Mathf.MoveTowards(Current, Target, 8f * Time.deltaTime);
            // buttons just bob when pressed
            float press = Mathf.Clamp01(1f - (Time.time - PressedTime) * 5f);
            transform.localPosition = basePos + transform.localRotation * Vector3.back * (0.02f * press);
        }

        void FixedUpdate()
        {
            if (IsButton) return;
            float speed = Role == "trap" ? 2.2f : 1.6f;
            Current = Mathf.MoveTowards(Current, Target, speed * Time.fixedDeltaTime);
            float eased = Current * Current * (3 - 2 * Current);
            Vector3 lp = basePos;
            Quaternion lr = baseRot;
            if (Kind == "hinge")
            {
                Vector3 ax = Axis == 'x' ? Vector3.right : Axis == 'z' ? Vector3.forward : Vector3.up;
                lr = baseRot * Quaternion.AngleAxis(OpenAmount * eased * Mathf.Rad2Deg, ax);
            }
            else if (Kind == "slide")
            {
                Vector3 ax = Axis == 'x' ? Vector3.right : Axis == 'z' ? Vector3.forward : Vector3.up;
                lp = basePos + baseRot * ax * (OpenAmount * eased);
            }
            var parent = transform.parent;
            Vector3 wp = parent ? parent.TransformPoint(lp) : lp;
            Quaternion wr = parent ? parent.rotation * lr : lr;
            if (rb != null)
            {
                rb.MovePosition(wp);
                rb.MoveRotation(wr);
            }
            else
            {
                transform.localPosition = lp;
                transform.localRotation = lr;
            }
        }

        /// <summary>Host: toggle. Returns the new on/open state.</summary>
        public bool Toggle()
        {
            PressedTime = Time.time;
            Target = Target > 0.5f ? 0f : 1f;
            return Target > 0.5f;
        }

        public void SetState(bool open)
        {
            Target = open ? 1f : 0f;
        }

        public byte NetState => (byte)Mathf.RoundToInt(Mathf.Clamp01(Target) * 255f);

        public void ApplyNet(byte b)
        {
            float t = b / 255f;
            if (IsButton && Mathf.Abs(t - Target) > 0.01f) PressedTime = Time.time;
            Target = t;
        }
    }
}
