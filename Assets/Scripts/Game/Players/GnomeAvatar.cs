using Gnomes.Core;
using Gnomes.Core.Protocol;
using Gnomes.Rendering;
using Gnomes.World;
using UnityEngine;

namespace Gnomes.Players
{
    /// <summary>
    /// Visual gnome: the Blender model plus procedural animation (walk cycle, crouch squash,
    /// floppy hat, stretchy arms reaching for whatever the hands hold).
    /// </summary>
    public class GnomeAvatar : MonoBehaviour
    {
        public ModelInstance Model;
        Transform body, head, hat, hatMid, hatTip, armL, armR, handL, handR, legL, legR;
        Quaternion hatMidRest, hatTipRest, bodyRest, headRest, legLRest, legRRest;
        Vector3 bodyRestPos;
        float walkPhase;
        Vector3 lastPos;
        Vector3 hatVel, hatOffset; // spring state for the floppy hat
        Vector3 smoothedVel;
        float squash = 1f;
        public Color32 HatColor;

        // inputs set by the owner every frame
        public Vector3 Velocity;
        public bool Grounded = true;
        public bool Crouch;
        public float Pitch;
        public PlayerStatus Status;
        public bool Struggling;
        public bool ArmsRipped;
        public Vector3 HandTargetL, HandTargetR;
        public bool HandsActive; // reaching/holding/climbing
        public bool FirstPerson; // hide head for the local camera

        public static GnomeAvatar Create(Transform parent, Color32 hat)
        {
            var inst = ModelLibrary.Instantiate("gnome", parent, Layers.IgnoreRaycast, withColliders: false);
            var av = inst.gameObject.AddComponent<GnomeAvatar>();
            av.Model = inst;
            av.HatColor = hat;
            inst.SetTint(hat);
            inst.SetLayerRecursive(Layers.IgnoreRaycast);
            av.Bind();
            return av;
        }

        void Bind()
        {
            body = Model.Node("body");
            head = Model.Node("head");
            hat = Model.Node("hat");
            hatMid = Model.Node("hatMid");
            hatTip = Model.Node("hatTip");
            armL = Model.Node("armL");
            armR = Model.Node("armR");
            handL = Model.Node("handL");
            handR = Model.Node("handR");
            legL = Model.Node("legL");
            legR = Model.Node("legR");
            if (hatMid) hatMidRest = hatMid.localRotation;
            if (hatTip) hatTipRest = hatTip.localRotation;
            if (body)
            {
                bodyRest = body.localRotation;
                bodyRestPos = body.localPosition;
            }
            if (head) headRest = head.localRotation;
            if (legL) legLRest = legL.localRotation;
            if (legR) legRRest = legR.localRotation;
            lastPos = transform.position;
            // hands and arms live in world space so they can stretch anywhere
            if (handL) handL.SetParent(transform.parent, true);
            if (handR) handR.SetParent(transform.parent, true);
        }

        void OnDestroy()
        {
            if (handL) Destroy(handL.gameObject);
            if (handR) Destroy(handR.gameObject);
        }

        public void SetVisible(bool v)
        {
            Model.SetVisible(v);
        }

        public void SetFirstPerson(bool fp)
        {
            FirstPerson = fp;
            foreach (var r in Model.Renderers)
            {
                bool isArm = r.transform == armL || r.transform == armR || r.transform == handL || r.transform == handR;
                r.shadowCastingMode = fp && !isArm ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (handL) handL.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (handR) handR.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var pos = transform.position;
            var moved = pos - lastPos;
            lastPos = pos;
            smoothedVel = Compat.Damp(smoothedVel, Velocity, 10f, dt);
            float speed = smoothedVel.Flat().magnitude;

            // --- walk cycle ---
            if (Grounded && speed > 0.3f) walkPhase += moved.Flat().magnitude * 5.2f;
            else walkPhase = Mathf.Lerp(walkPhase, Mathf.Round(walkPhase / Mathf.PI) * Mathf.PI, dt * 6f);
            float swing = Grounded ? Mathf.Clamp01(speed / 4f) : 0f;
            float legA = Mathf.Sin(walkPhase) * 38f * swing;
            bool carried = Status == PlayerStatus.Carried || Status == PlayerStatus.Trapped;
            if (carried || Struggling)
            {
                // frantic kicking
                float k = Mathf.Sin(Time.time * 22f) * 35f;
                legA = k;
            }
            else if (!Grounded) legA = 20f;
            if (legL) legL.localRotation = legLRest * Quaternion.Euler(legA, 0, 0);
            if (legR) legR.localRotation = legRRest * Quaternion.Euler(!Grounded && !carried ? -10f : -legA, 0, 0);

            // --- body bob, lean and crouch squash ---
            float targetSquash = Crouch ? 0.72f : 1f;
            squash = Compat.Damp(squash, targetSquash, 12f, dt);
            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.04f * swing;
            if (body)
            {
                body.localPosition = bodyRestPos * squash + Vector3.up * bob;
                var localVel = transform.InverseTransformDirection(smoothedVel);
                float lean = Mathf.Clamp(localVel.z * 2.5f, -10f, 14f);
                float roll = Mathf.Clamp(-localVel.x * 2.5f, -10f, 10f);
                if (carried) roll = Mathf.Sin(Time.time * 9f) * 15f;
                body.localRotation = bodyRest * Quaternion.Euler(lean, 0, roll);
            }
            transform.localScale = new Vector3(1f + (1f - squash) * 0.35f, squash, 1f + (1f - squash) * 0.35f);
            if (head) head.localRotation = headRest * Quaternion.Euler(Mathf.Clamp(Pitch * Mathf.Rad2Deg * 0.5f, -25f, 30f), 0, 0);

            // --- floppy hat: a damped spring driven by acceleration ---
            if (hatMid && hatTip)
            {
                var accel = (Velocity - smoothedVel);
                var force = -transform.InverseTransformDirection(accel) * 1.2f + new Vector3(0, 0, 0);
                if (carried) force += new Vector3(Mathf.Sin(Time.time * 13f), 0, Mathf.Cos(Time.time * 11f)) * 6f;
                hatVel += (force - hatOffset * 60f) * dt;
                hatVel *= Mathf.Exp(-6f * dt);
                hatOffset += hatVel * dt;
                hatOffset = Vector3.ClampMagnitude(hatOffset, 0.6f);
                hatMid.localRotation = hatMidRest * Quaternion.Euler(hatOffset.z * 40f, 0, -hatOffset.x * 40f);
                hatTip.localRotation = hatTipRest * Quaternion.Euler(hatOffset.z * 70f, 0, -hatOffset.x * 70f);
            }

            // --- stretchy arms ---
            UpdateArm(armL, handL, HandTargetL, -1f);
            UpdateArm(armR, handR, HandTargetR, 1f);
        }

        void UpdateArm(Transform arm, Transform hand, Vector3 target, float side)
        {
            if (!arm || !hand) return;
            if (ArmsRipped)
            {
                arm.localScale = new Vector3(1, 1, 0.05f);
                hand.gameObject.SetActive(false);
                return;
            }
            hand.gameObject.SetActive(true);
            Vector3 goal;
            bool carried = Status == PlayerStatus.Carried || Status == PlayerStatus.Trapped || Struggling;
            if (carried)
            {
                goal = arm.position + transform.rotation * new Vector3(side * 0.35f, 0.25f + Mathf.Sin(Time.time * 20f + side) * 0.2f, 0.1f);
            }
            else if (HandsActive)
            {
                goal = target;
            }
            else
            {
                float swingA = Mathf.Sin(walkPhase) * side * 0.12f * Mathf.Clamp01(smoothedVel.Flat().magnitude / 4f);
                goal = transform.TransformPoint(new Vector3(side * 0.27f, 0.3f * squash, 0.08f + swingA));
            }
            // soften hand motion a little
            hand.position = HandsActive && !carried ? goal : Compat.Damp(hand.position, goal, 18f, Time.deltaTime);
            var d = hand.position - arm.position;
            float len = Mathf.Max(0.02f, d.magnitude);
            if (d.sqrMagnitude > 1e-6f) arm.rotation = Quaternion.LookRotation(d, transform.up);
            arm.localScale = new Vector3(1f, 1f, len / Mathf.Max(0.01f, arm.parent != null ? arm.parent.lossyScale.z : 1f));
            hand.rotation = arm.rotation;
        }

        public Transform Head => head;
    }
}
