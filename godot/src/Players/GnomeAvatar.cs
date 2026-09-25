using Gnomes.Core.Protocol;
using Godot;
using SockGang.Audio;
using SockGang.Rendering;

namespace SockGang.Players
{
    /// <summary>
    /// Visual gnome: the Blender model plus procedural animation (walk cycle, crouch squash,
    /// floppy hat, stretchy arms reaching for whatever the hands hold, the yarn hook).
    /// </summary>
    public partial class GnomeAvatar : Node3D
    {
        public ModelInstance Model;
        Node3D body, head, hat, hatMid, hatTip, armL, armR, handL, handR, legL, legR;
        Quaternion hatMidRest, hatTipRest, bodyRest, headRest, legLRest, legRRest;
        Vector3 bodyRestPos;
        float walkPhase, lastStepPhase;
        Vector3 lastPos, hatVel, hatOffset, smoothedVel;
        float squash = 1f;
        public Color HatColor;

        // inputs set by the owner every frame
        public float Yaw;
        public Vector3 Vel;
        public bool Grounded = true;
        public bool Crouch;
        public float Pitch;
        public PlayerStatus Status;
        public bool Struggling, ArmsRipped;
        public Vector3 HandTargetL, HandTargetR;
        public bool HandsActive;
        public bool FirstPerson;
        public float FootstepVolume = 0.35f;
        public bool Gliding;
        float kickUntil;
        Node3D yarn, hook;
        bool yarnActive;
        Vector3 yarnFrom, yarnTo;
        static CylinderMesh yarnMesh;
        static SphereMesh hookMesh;

        public static GnomeAvatar Create(Node3D owner, Color hat)
        {
            var av = new GnomeAvatar { Name = "Avatar", HatColor = hat };
            owner.AddChild(av);
            av.Model = ModelLibrary.Instantiate("gnome", av, ColliderMode.None);
            av.Model.SetTint(hat);
            av.Bind();
            av.BuildYarn();
            return av;
        }

        void BuildYarn()
        {
            var mat = ModelLibrary.TintMaterial(HatColor);
            yarnMesh ??= new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.015f, Height = 1f, RadialSegments = 6, Rings = 1 };
            hookMesh ??= new SphereMesh { Radius = 0.045f, Height = 0.09f, RadialSegments = 8, Rings = 4 };
            yarn = new Node3D { Name = "YarnPivot", TopLevel = true, Visible = false };
            AddChild(yarn);
            var y = new MeshInstance3D { Mesh = yarnMesh, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            y.Rotation = new Vector3(Mathf.Pi / 2, 0, 0); // cylinder Y -> pivot -Z after the offset below
            y.Position = new Vector3(0, 0, -0.5f);
            yarn.AddChild(y);
            hook = new MeshInstance3D { Name = "YarnHook", Mesh = hookMesh, MaterialOverride = mat, TopLevel = true, Visible = false };
            AddChild(hook);
        }

        public void SetYarn(bool active, Vector3 from, Vector3 to)
        {
            yarnActive = active;
            yarnFrom = from;
            yarnTo = to;
        }

        public void Kick() => kickUntil = Clock.Now + 0.28f;

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
            if (hatMid != null) hatMidRest = hatMid.Quaternion;
            if (hatTip != null) hatTipRest = hatTip.Quaternion;
            if (body != null)
            {
                bodyRest = body.Quaternion;
                bodyRestPos = body.Position;
            }
            if (head != null) headRest = head.Quaternion;
            if (legL != null) legLRest = legL.Quaternion;
            if (legR != null) legRRest = legR.Quaternion;
            // hands live in world space so they can stretch anywhere
            if (handL != null) handL.TopLevel = true;
            if (handR != null) handR.TopLevel = true;
            lastPos = GlobalPosition;
        }

        public void SetShown(bool v) => Visible = v;

        public void SetFirstPerson(bool fp)
        {
            FirstPerson = fp;
            foreach (var (mi, _) in Model.Meshes)
            {
                bool isArm = mi == armL || mi == armR || mi == handL || mi == handR || IsUnder(mi, armL) || IsUnder(mi, armR) || IsUnder(mi, handL) || IsUnder(mi, handR);
                mi.CastShadow = fp && !isArm ? GeometryInstance3D.ShadowCastingSetting.ShadowsOnly : GeometryInstance3D.ShadowCastingSetting.On;
            }
        }

        static bool IsUnder(Node n, Node ancestor)
        {
            if (ancestor == null) return false;
            for (var p = n.GetParent(); p != null; p = p.GetParent()) if (p == ancestor) return true;
            return false;
        }

        /// <summary>World velocity in the avatar's Unity-style local frame (x right, z forward).</summary>
        Vector3 LocalU(Vector3 v)
        {
            var l = GMath.YawBasis(Yaw).Inverse() * v;
            return new Vector3(l.X, l.Y, -l.Z);
        }

        public override void _Process(double delta)
        {
            float dt = Mathf.Max((float)delta, 1e-4f);
            var pos = GlobalPosition;
            var moved = pos - lastPos;
            lastPos = pos;
            smoothedVel = GMath.Damp(smoothedVel, Vel, 10f, dt);
            float speed = smoothedVel.Flat().Length();
            float t = Clock.Now;

            // --- walk cycle ---
            if (Grounded && speed > 0.3f) walkPhase += moved.Flat().Length() * 5.2f;
            else walkPhase = Mathf.Lerp(walkPhase, Mathf.Round(walkPhase / Mathf.Pi) * Mathf.Pi, dt * 6f);
            float swing = Grounded ? Mathf.Clamp(speed / 4f, 0f, 1f) : 0f;
            float legA = Mathf.Sin(walkPhase) * 38f * swing;
            bool carried = Status == PlayerStatus.Carried || Status == PlayerStatus.Trapped;
            if (carried || Struggling) legA = Mathf.Sin(t * 22f) * 35f; // frantic kicking
            else if (!Grounded) legA = 20f;
            if (Grounded && speed > 1f && !carried)
            {
                float stepIndex = Mathf.Floor(walkPhase / Mathf.Pi);
                if (stepIndex != lastStepPhase)
                {
                    lastStepPhase = stepIndex;
                    Sfx.I?.Play(SoundId.Footstep, pos, FootstepVolume * Mathf.Clamp(speed / 5f, 0f, 1f));
                }
            }
            float kick = t < kickUntil ? Mathf.Sin((kickUntil - t) / 0.28f * Mathf.Pi) * -75f : 0f;
            if (legL != null) legL.Quaternion = legLRest * Conv.UEuler(legA, 0, 0);
            if (legR != null) legR.Quaternion = legRRest * Conv.UEuler((!Grounded && !carried ? -10f : -legA) + kick, 0, 0);

            // --- body bob, lean and crouch squash ---
            squash = GMath.Damp(squash, Crouch ? 0.72f : 1f, 12f, dt);
            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.04f * swing;
            var lv = LocalU(smoothedVel);
            if (body != null)
            {
                body.Position = bodyRestPos * squash + Vector3.Up * bob;
                float lean = Mathf.Clamp(lv.Z * 2.5f, -10f, 14f);
                float roll = carried ? Mathf.Sin(t * 9f) * 15f : Mathf.Clamp(-lv.X * 2.5f, -10f, 10f);
                body.Quaternion = bodyRest * Conv.UEuler(lean, 0, roll);
            }
            Basis = GMath.YawBasis(Yaw) * Basis.FromScale(new Vector3(1f + (1f - squash) * 0.35f, squash, 1f + (1f - squash) * 0.35f));
            if (head != null) head.Quaternion = headRest * new Quaternion(Vector3.Right, Mathf.Clamp(Pitch * 0.5f, -0.44f, 0.52f));

            // --- floppy hat: a damped spring driven by acceleration ---
            if (hat != null) hat.Scale = GMath.Damp(hat.Scale, Gliding ? new Vector3(2.2f, 0.55f, 2.2f) : Vector3.One, 12f, dt);
            if (hatMid != null && hatTip != null)
            {
                var force = -LocalU(Vel - smoothedVel) * 1.2f;
                if (carried) force += new Vector3(Mathf.Sin(t * 13f), 0, Mathf.Cos(t * 11f)) * 6f;
                hatVel += (force - hatOffset * 60f) * dt;
                hatVel *= Mathf.Exp(-6f * dt);
                hatOffset += hatVel * dt;
                hatOffset = GMath.ClampLength(hatOffset, 0.6f);
                hatMid.Quaternion = hatMidRest * Conv.UEuler(hatOffset.Z * 40f, 0, -hatOffset.X * 40f);
                hatTip.Quaternion = hatTipRest * Conv.UEuler(hatOffset.Z * 70f, 0, -hatOffset.X * 70f);
            }

            // --- arms ---
            UpdateArm(armL, handL, HandTargetL, -1f, dt, t);
            UpdateArm(armR, handR, HandTargetR, 1f, dt, t);

            // --- yarn ---
            yarn.Visible = yarnActive;
            hook.Visible = yarnActive;
            if (yarnActive)
            {
                var from = handR != null ? handR.GlobalPosition : yarnFrom;
                var d = yarnTo - from;
                yarn.GlobalPosition = from;
                if (d.LengthSquared() > 1e-6f)
                {
                    var b = GMath.LookBasis(d, Vector3.Up);
                    b.Z *= Mathf.Max(0.01f, d.Length());
                    yarn.GlobalBasis = b;
                }
                hook.GlobalPosition = yarnTo;
            }
        }

        void UpdateArm(Node3D arm, Node3D hand, Vector3 target, float side, float dt, float t)
        {
            if (arm == null || hand == null) return;
            if (ArmsRipped)
            {
                arm.Scale = new Vector3(1, 1, 0.05f);
                hand.Visible = false;
                return;
            }
            hand.Visible = true;
            var yawB = GMath.YawBasis(Yaw);
            bool carried = Status == PlayerStatus.Carried || Status == PlayerStatus.Trapped || Struggling;
            Vector3 goal;
            if (carried) goal = arm.GlobalPosition + yawB * new Vector3(side * 0.35f, 0.25f + Mathf.Sin(t * 20f + side) * 0.2f, -0.1f);
            else if (HandsActive) goal = target;
            else
            {
                float swingA = Mathf.Sin(walkPhase) * side * 0.12f * Mathf.Clamp(smoothedVel.Flat().Length() / 4f, 0f, 1f);
                goal = GlobalPosition + yawB * new Vector3(side * 0.27f, 0.3f * squash, -(0.08f + swingA));
            }
            hand.GlobalPosition = HandsActive && !carried ? goal : GMath.Damp(hand.GlobalPosition, goal, 18f, dt);
            var d = hand.GlobalPosition - arm.GlobalPosition;
            float len = Mathf.Max(0.02f, d.Length());
            if (d.LengthSquared() > 1e-6f)
            {
                var b = GMath.LookBasis(d, Vector3.Up);
                hand.GlobalBasis = b;
                b.Z *= len;
                arm.GlobalBasis = b;
            }
        }

        public Node3D Head => head;

        public override void _ExitTree()
        {
            // TopLevel children are freed with us; nothing else to clean
        }
    }
}
