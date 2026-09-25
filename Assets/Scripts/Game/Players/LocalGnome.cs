using Gnomes.App;
using Gnomes.Audio;
using Gnomes.Core;
using Gnomes.Core.Protocol;
using Gnomes.Core.Rules;
using Gnomes.NPC;
using Gnomes.Session;
using Gnomes.World;
using UnityEngine;

namespace Gnomes.Players
{
    /// <summary>Physics helpers shared by the host (applied to props) and clients (reaction on the gnome).</summary>
    public static class GrabPhysics
    {
        /// <summary>Force that drags a carried item's grab point towards the hold target.</summary>
        public static Vector3 HoldForce(Vector3 anchor, Vector3 anchorVel, Vector3 target, float mass, float strength, float gravityShare = 1f)
        {
            var err = target - anchor;
            var desiredVel = Vector3.ClampMagnitude(err * 11f, 9f);
            var f = (desiredVel - anchorVel) * mass * 12f;
            f += Vector3.up * (mass * -Physics.gravity.y * gravityShare);
            return Vector3.ClampMagnitude(f, strength);
        }

        /// <summary>Tension of a yarn of the given length between a and b (only pulls, never pushes). Force acts on a towards b.</summary>
        public static Vector3 YarnTension(Vector3 a, Vector3 aVel, Vector3 b, float length, float maxForce, float stiffness = 260f, float damping = 28f)
        {
            var d = b - a;
            float dist = d.magnitude;
            if (dist <= length || dist < 1e-4f) return Vector3.zero;
            var dir = d / dist;
            float f = (dist - length) * stiffness - Vector3.Dot(aVel, dir) * damping;
            return dir * Mathf.Clamp(f, 0f, maxForce);
        }
    }

    /// <summary>The gnome controlled on this machine (client-authoritative movement).</summary>
    public class LocalGnome : GnomeBody
    {
        public override bool IsLocal => true;
        public static LocalGnome I;

        public Camera Cam;
        Transform camPivot;
        public bool ThirdPerson;
        GameSession S => GameSession.I;
        GameWorld W => GameWorld.Current;
        GearEffects gear;

        // movement
        float lastGroundedTime = -10, jumpBufferedUntil = -10;
        float lastVy;
        bool wantCrouch;
        public bool Gliding;
        float stunnedUntil;

        // hands & yarn
        ArmMode mode;
        ushort heldProp; // carried item or item on the yarn
        Vector3 grabLocal; // prop-local grab / hook point
        float holdDist = GameConsts.HoldDistance;
        float yarnLen = 3f;
        Transform hookParent;
        Vector3 hookLocal; // world point when hookParent == null
        float grip = GameConsts.GripTime;
        float strainTime;
        float yarnCooldownUntil;
        float kickCooldown;
        Vector3 reaction;
        float lastSend;
        Vector3 flyFrom, flyTo;
        float flyUntil;

        // interaction
        public string Prompt;
        public float HoldProgress;
        float holdE;
        Mechanism lookMech;
        Prop lookProp;
        bool nearGoHome, nearCraft, nearSock;
        public bool CraftMenuRequested;

        // carried / trapped / dead
        public Vector3 ForcedPos;
        float struggleSend;
        Vector3 ghostPos;

        public ushort CarriedProp => mode == ArmMode.HoldProp ? heldProp : (ushort)0;
        public float Grip01 => grip / GameConsts.GripTime;
        public bool OnYarn => mode == ArmMode.Climb || mode == ArmMode.YarnProp;
        public float YarnLength => yarnLen;
        public float YarnRange => gear.YarnRange;
        public bool Stunned => Time.time < stunnedUntil;
        public ArmMode Mode => mode;

        public static LocalGnome Create(GameWorld w, byte id, string name, Color32 hat, Vector3 pos, float yaw)
        {
            var go = new GameObject($"LocalGnome_{id}");
            go.transform.SetParent(w.GnomeRoot, false);
            go.transform.position = pos;
            var g = go.AddComponent<LocalGnome>();
            g.PlayerId = id;
            g.PlayerName = name;
            g.Hat = hat;
            g.Yaw = yaw;
            g.CreateBody(Layers.LocalGnome, false);
            g.Avatar = GnomeAvatar.Create(go.transform, hat);
            g.BuildCamera();
            g.RefreshGear();
            w.Gnomes[id] = g;
            I = g;
            return g;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public void RefreshGear()
        {
            gear = GearEffects.From(S?.Save);
        }

        void BuildCamera()
        {
            camPivot = new GameObject("CamPivot").transform;
            camPivot.SetParent(transform, false);
            var camGo = new GameObject("GnomeCamera");
            camGo.transform.SetParent(camPivot, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.03f;
            Cam.farClipPlane = 400f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = RenderSettings.fogColor;
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";
            // a faint "night eyes" glow around the player so dark rooms stay playable (local only)
            var glow = new GameObject("NightEyes").AddComponent<Light>();
            glow.transform.SetParent(camPivot, false);
            glow.transform.localPosition = new Vector3(0, 0.4f, 0.3f);
            glow.type = LightType.Point;
            glow.range = 7f;
            glow.intensity = 0.45f;
            glow.color = new Color(0.72f, 0.8f, 1f);
            glow.shadows = LightShadows.None;
            glow.renderMode = LightRenderMode.ForcePixel;
            Avatar.SetFirstPerson(true);
        }

        public void Stun(float seconds)
        {
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
            DropEverything();
            Fx.Stars(W, HeadPos + Vector3.up * 0.3f);
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            var app = GameApp.I;
            bool inputEnabled = app != null && app.GameplayInput;
            var st = app != null ? app.Settings : null;
            float sens = st != null ? st.Sensitivity : 2f;
            if (Cam) Cam.fieldOfView = st != null ? st.Fov : 75f;

            if (inputEnabled)
            {
                float mx = Input.GetAxisRaw("Mouse X") * sens;
                float my = Input.GetAxisRaw("Mouse Y") * sens * (st != null && st.InvertY ? -1 : 1);
                Yaw += mx * Mathf.Deg2Rad;
                Pitch = Mathf.Clamp(Pitch + my * Mathf.Deg2Rad, -1.45f, 1.45f);
                if (Input.GetKeyDown(KeyCode.V)) ThirdPerson = !ThirdPerson;
            }

            switch (Status)
            {
                case PlayerStatus.Free:
                    UpdateFree(inputEnabled && !Stunned);
                    break;
                case PlayerStatus.Carried:
                case PlayerStatus.Trapped:
                    UpdateCaptured(inputEnabled);
                    break;
                default:
                    UpdateGhost(inputEnabled);
                    break;
            }
            UpdateCamera();
            FeedAvatar();
            AvatarHands(CurrentArms(), transform.position + YawRot * new Vector3(0, 0.3f, 0.3f));
            Avatar.FootstepVolume = Crouching ? 0.12f : Sprinting ? 0.8f : 0.35f;
            Avatar.Gliding = Gliding;

            if (S != null && Time.unscaledTime - lastSend >= 1f / GameConsts.ClientStateHz)
            {
                lastSend = Time.unscaledTime;
                S.SubmitLocalState(this);
            }
        }

        void UpdateFree(bool input)
        {
            Struggling = false;
            if (Rb.isKinematic)
            {
                Rb.isKinematic = false;
                Col.enabled = true;
            }
            if (kickCooldown > 0) kickCooldown -= Time.deltaTime;
            ScanInteractables();
            if (!input)
            {
                wantCrouch = false;
                Sprinting = false;
                return;
            }
            wantCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            Sprinting = Input.GetKey(KeyCode.LeftShift) && !Crouching;
            if (Input.GetKeyDown(KeyCode.Space)) jumpBufferedUntil = Time.time + 0.15f;

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                if (OnYarn) yarnLen = Mathf.Clamp(yarnLen - wheel * 0.45f, GameConsts.YarnMin, gear.YarnRange + 1f);
                else if (mode == ArmMode.HoldProp) holdDist = Mathf.Clamp(holdDist + wheel * 0.15f, 0.55f, 1.6f);
            }
            // reel in fast with R held
            if (OnYarn && Input.GetKey(KeyCode.R)) yarnLen = Mathf.Max(GameConsts.YarnMin, yarnLen - 3f * Time.deltaTime);

            // --- hands: LMB picks up / drops ---
            if (Input.GetMouseButtonDown(0))
            {
                if (mode == ArmMode.HoldProp) Drop();
                else if (!OnYarn) TryPickUp();
            }
            // --- RMB: throw what you carry, otherwise the yarn hook ---
            if (Input.GetMouseButtonDown(1))
            {
                if (mode == ArmMode.HoldProp) Throw();
                else if (OnYarn) DetachYarn();
                else ShootYarn();
            }
            if (Input.GetKeyDown(KeyCode.T) && OnYarn) TieYarn();
            if (Input.GetKeyDown(KeyCode.F)) Kick();

            if (mode == ArmMode.HoldProp) CheckCarry();
            if (mode == ArmMode.YarnProp) CheckYarnProp();
            if (mode == ArmMode.Climb)
            {
                if (!Grounded) grip -= Time.deltaTime;
                if (grip <= 0)
                {
                    DetachYarn();
                    GameApp.I?.Toast(Loc.T("gripLost"));
                }
                if (Time.time < jumpBufferedUntil && !Grounded)
                {
                    jumpBufferedUntil = 0;
                    YarnJump();
                }
            }
            else if (Grounded) grip = Mathf.Min(GameConsts.GripTime, grip + Time.deltaTime * 3f);

            // --- interaction ---
            bool eDown = Input.GetKeyDown(KeyCode.E), eHeld = Input.GetKey(KeyCode.E);
            bool jarLid = lookMech != null && lookMech.Role == "jar";
            if (eDown && !jarLid) Interact();
            if (eHeld && (jarLid || (nearGoHome && lookMech == null && lookProp == null)))
            {
                holdE += Time.deltaTime;
                float need = jarLid ? GameConsts.JarUnscrewSeconds : 1.5f;
                HoldProgress = Mathf.Clamp01(holdE / need);
                if (holdE >= need)
                {
                    holdE = -999;
                    if (jarLid) S?.SendAction(new ActionMsg { Type = ActionType.Unscrew, Id = lookMech.Index });
                    else S?.SendAction(new ActionMsg { Type = ActionType.GoHome, I = 1 });
                }
            }
            else
            {
                if (!eHeld) holdE = 0;
                HoldProgress = 0;
            }
            if (Input.GetKeyDown(KeyCode.Q)) S?.SendAction(new ActionMsg { Type = ActionType.DropPockets, A = (HeadPos + LookDir * 0.6f).ToCoreV(), B = (LookDir * 2f).ToCoreV() });
            if (Input.GetKeyDown(KeyCode.G) && S?.Save != null && S.Save.Potions > 0)
                S.SendAction(new ActionMsg { Type = ActionType.Potion, A = (HeadPos + LookDir * 0.5f).ToCoreV(), B = (LookDir * 11f + Vector3.up * 2f + Velocity * 0.5f).ToCoreV() });
            if (Input.GetKeyDown(KeyCode.H)) S?.SendAction(new ActionMsg { Type = ActionType.Honk, A = HeadPos.ToCoreV() });
        }

        void UpdateCaptured(bool input)
        {
            DropEverything();
            Rb.isKinematic = true;
            Col.enabled = false;
            Crouching = Sprinting = Gliding = false;
            var target = ForcedPos;
            transform.position = Vector3.Distance(transform.position, target) > 6f ? target : Compat.Damp(transform.position, target, 14f, Time.deltaTime);
            Velocity = Vector3.zero;
            Grounded = false;
            Prompt = Status == PlayerStatus.Trapped ? Loc.T("struggle") : Loc.T("carriedStruggle");
            Struggling = input && Input.GetKey(KeyCode.F);
            if (input && Input.GetKeyDown(KeyCode.F) && Time.time - struggleSend > 0.06f)
            {
                struggleSend = Time.time;
                S?.SendAction(new ActionMsg { Type = ActionType.Struggle });
                Sfx.I?.Play(SoundId.Struggle, HeadPos, 0.6f, Random.Range(0.9f, 1.3f));
            }
        }

        void UpdateGhost(bool input)
        {
            DropEverything();
            Rb.isKinematic = true;
            Col.enabled = false;
            Prompt = Status == PlayerStatus.Dead ? (S != null && S.IsSolo ? Loc.T("deadSolo") : Loc.T("dead")) : Loc.T("wentHome");
            if (!input) return;
            var move = new Vector3((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), (Input.GetKey(KeyCode.Space) ? 1 : 0) - (Input.GetKey(KeyCode.LeftControl) ? 1 : 0), (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            var rot = Quaternion.Euler(-Pitch * Mathf.Rad2Deg, Yaw * Mathf.Rad2Deg, 0);
            ghostPos += rot * move * (Input.GetKey(KeyCode.LeftShift) ? 14f : 7f) * Time.deltaTime;
            if (W != null)
            {
                var b = W.PlayArea;
                ghostPos = new Vector3(Mathf.Clamp(ghostPos.x, b.min.x, b.max.x), Mathf.Clamp(ghostPos.y, 0.3f, 30f), Mathf.Clamp(ghostPos.z, b.min.z, b.max.z));
            }
        }

        void UpdateCamera()
        {
            if (!camPivot) return;
            bool ghost = Status == PlayerStatus.Dead || Status == PlayerStatus.Home;
            var rot = Quaternion.Euler(-Pitch * Mathf.Rad2Deg, Yaw * Mathf.Rad2Deg, 0);
            if (ghost)
            {
                if (ghostPos == Vector3.zero) ghostPos = HeadPos + Vector3.up * 2f;
                camPivot.SetPositionAndRotation(ghostPos, rot);
                Cam.transform.localPosition = Vector3.zero;
                Cam.transform.localRotation = Quaternion.identity;
                return;
            }
            ghostPos = Vector3.zero;
            float eye = Crouching ? GameConsts.GnomeEyeCrouch : GameConsts.GnomeEye;
            var eyePos = transform.position + Vector3.up * eye;
            camPivot.SetPositionAndRotation(eyePos, rot);
            if (ThirdPerson || Status != PlayerStatus.Free)
            {
                var back = rot * new Vector3(0.35f, 0.35f, -2.6f);
                float dist = back.magnitude;
                if (Physics.SphereCast(eyePos, 0.15f, back.normalized, out var hit, dist, Layers.World, QueryTriggerInteraction.Ignore))
                    dist = Mathf.Max(0.3f, hit.distance - 0.05f);
                Cam.transform.position = eyePos + back.normalized * dist;
                Cam.transform.rotation = rot;
                if (Avatar.FirstPerson) Avatar.SetFirstPerson(false);
            }
            else
            {
                Cam.transform.localPosition = new Vector3(0, 0, 0.12f);
                Cam.transform.localRotation = Quaternion.identity;
                if (!Avatar.FirstPerson) Avatar.SetFirstPerson(true);
            }
        }

        // ------------------------------------------------------------------ physics

        void FixedUpdate()
        {
            if (Status != PlayerStatus.Free) return;
            float dt = Time.fixedDeltaTime;
            if (!wantCrouch && Crouching)
            {
                if (!Physics.SphereCast(transform.position + Vector3.up * 0.3f, GameConsts.GnomeRadius * 0.9f, Vector3.up, out _, 0.5f, Layers.World, QueryTriggerInteraction.Ignore))
                    Crouching = false;
            }
            else if (wantCrouch) Crouching = true;
            SetCrouchCollider(Crouching);

            var origin = transform.position + Vector3.up * (GameConsts.GnomeRadius + 0.05f);
            Grounded = Physics.SphereCast(origin, GameConsts.GnomeRadius * 0.92f, Vector3.down, out var gh, 0.14f, Layers.SolidForGnome & ~(1 << Layers.LocalGnome), QueryTriggerInteraction.Ignore)
                       && gh.normal.y > 0.55f;
            if (Grounded) lastGroundedTime = Time.time;

            var v = Rb.Vel();
            var app = GameApp.I;
            bool input = app != null && app.GameplayInput && !Stunned;
            float ix = input ? (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0) : 0;
            float iz = input ? (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0) : 0;
            var wish = YawRot * new Vector3(ix, 0, iz);
            if (wish.sqrMagnitude > 1) wish.Normalize();
            float speed = Crouching ? GameConsts.CrouchSpeed : Sprinting ? GameConsts.SprintSpeed : GameConsts.WalkSpeed;
            if (mode == ArmMode.HoldProp)
            {
                var p = W?.GetProp(heldProp);
                if (p != null && p.Def.Mass > 1.2f) speed *= Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(1.2f, 6f, p.Def.Mass / gear.StrengthMul));
            }
            var targetH = wish * speed;
            var h = v.Flat();
            if (mode == ArmMode.Climb && !Grounded)
            {
                Rb.AddForce(wish * 16f, ForceMode.Force); // swing
            }
            else if (Grounded)
            {
                h = Vector3.MoveTowards(h, targetH, GameConsts.GroundAccel * dt);
                v = new Vector3(h.x, v.y, h.z);
            }
            else
            {
                var dv = Vector3.ClampMagnitude(targetH - h, GameConsts.AirAccel * (Gliding ? 1.6f : 1f) * dt);
                if (targetH.sqrMagnitude > 0.01f) h += dv;
                v = new Vector3(h.x, v.y, h.z);
            }
            // jump
            if (Time.time < jumpBufferedUntil && Time.time - lastGroundedTime < GameConsts.CoyoteTime && mode != ArmMode.Climb)
            {
                v.y = gear.JumpSpeed * (Crouching ? 0.8f : 1f);
                jumpBufferedUntil = 0;
                lastGroundedTime = -10;
                Sfx.I?.Play(SoundId.Jump, transform.position, 0.35f);
            }
            // parachute hat: hold jump while falling
            Gliding = gear.Parachute && input && !Grounded && mode != ArmMode.Climb && Input.GetKey(KeyCode.Space) && v.y < 0;
            if (Gliding) v.y = Mathf.Max(v.y, -GameConsts.GlideFallSpeed);
            if (Grounded && lastVy < -9f) Sfx.I?.Play(SoundId.Land, transform.position, Mathf.Clamp01(-lastVy / 16f));
            lastVy = v.y;
            Rb.SetVel(v);

            if (mode == ArmMode.Climb) ClimbForce();
            else if (mode == ArmMode.HoldProp) CarryReaction(dt);
            else if (mode == ArmMode.YarnProp) YarnPropReaction(dt);
            Velocity = Rb.Vel();

            if (transform.position.y < -25f && W != null)
            {
                Rb.position = W.SpawnPoint(PlayerId);
                Rb.SetVel(Vector3.zero);
            }
        }

        public Vector3 HandPos => transform.position + Vector3.up * GameConsts.GnomeShoulder * (Crouching ? 0.75f : 1f) + YawRot * new Vector3(0.18f, 0, 0.1f);

        Vector3 HookWorld => hookParent != null ? hookParent.TransformPoint(hookLocal) : hookLocal;

        void ClimbForce()
        {
            if (hookParent == null && hookLocal == Vector3.zero)
            {
                DetachYarn();
                return;
            }
            var anchor = HookWorld;
            if (Vector3.Distance(anchor, HandPos) > gear.YarnRange + 4f)
            {
                SnapYarn();
                return;
            }
            var f = GrabPhysics.YarnTension(HandPos, Rb.Vel(), anchor, yarnLen, GameConsts.ClimbStrength * gear.StrengthMul);
            Rb.AddForce(f, ForceMode.Force);
            if (!Grounded) Rb.AddForce(-Rb.Vel() * 1.1f, ForceMode.Force);
        }

        void CarryReaction(float dt)
        {
            var p = W?.GetProp(heldProp);
            if (p == null)
            {
                DropEverything();
                return;
            }
            var anchor = p.transform.TransformPoint(grabLocal);
            var target = CarryTarget(p);
            var pv = p.Authority ? p.Rb.GetPointVelocity(anchor) : Vector3.zero;
            var f = GrabPhysics.HoldForce(anchor, pv, target, p.Def.Mass, GameConsts.HoldStrength * gear.StrengthMul);
            var r = -f;
            r.y = Mathf.Min(r.y, 0) * 0.5f;
            reaction = Compat.Damp(reaction, r * Mathf.Clamp01(p.Def.Mass / (p.Def.Mass + GameConsts.GnomeMass)), 10f, dt);
            if (Grounded) reaction.y = Mathf.Min(0, reaction.y);
            Rb.AddForce(reaction * 0.8f, ForceMode.Force);
            if (p.Authority && S != null && S.IsHost) S.Host?.ApplyHold(PlayerId, p, anchor, target);
        }

        void YarnPropReaction(float dt)
        {
            var p = W?.GetProp(heldProp);
            if (p == null)
            {
                DetachYarn();
                return;
            }
            var anchor = p.transform.TransformPoint(grabLocal);
            // the prop gets pulled towards the gnome (host applies it); the gnome feels the same pull towards the prop
            var pv = p.Authority ? p.Rb.GetPointVelocity(anchor) : Vector3.zero;
            var onProp = GrabPhysics.YarnTension(anchor, pv, HandPos, yarnLen, GameConsts.YarnPullStrength * gear.StrengthMul);
            float share = Mathf.Clamp01(p.Def.Mass / (p.Def.Mass + GameConsts.GnomeMass));
            reaction = Compat.Damp(reaction, -onProp * share, 10f, dt);
            Rb.AddForce(reaction, ForceMode.Force);
            if (p.Authority && S != null && S.IsHost) S.Host?.ApplyYarnPull(PlayerId, p, anchor, HandPos, yarnLen);
        }

        Vector3 CarryTarget(Prop p)
        {
            float extra = p != null ? Mathf.Clamp(p.HalfHeight, 0f, 1.2f) : 0f;
            var flatLook = Quaternion.Euler(-Mathf.Clamp(Pitch, -0.6f, 0.9f) * Mathf.Rad2Deg, Yaw * Mathf.Rad2Deg, 0) * Vector3.forward;
            return transform.position + Vector3.up * (0.55f + extra * 0.5f) + flatLook * (holdDist + extra);
        }

        // ------------------------------------------------------------------ hands

        void TryPickUp()
        {
            int mask = Layers.Grabbable & ~(1 << Layers.LocalGnome);
            if (!Physics.SphereCast(HeadPos, 0.12f, LookDir, out var hit, GameConsts.HandReach + 0.3f, mask, QueryTriggerInteraction.Ignore)) return;
            var prop = hit.collider.GetComponentInParent<Prop>();
            if (prop == null || prop.Removed) return;
            mode = ArmMode.HoldProp;
            heldProp = prop.Id;
            grabLocal = prop.transform.InverseTransformPoint(hit.point);
            holdDist = GameConsts.HoldDistance;
            strainTime = 0;
            Sfx.I?.Play(prop.Def.Has(ItemFlags.Squeaky) ? SoundId.Squeak : SoundId.Grab, hit.point, 0.5f);
            if (prop.Def.Mass / gear.StrengthMul > 3.8f) GameApp.I?.Toast(Loc.T("tooHeavy"));
        }

        void Drop()
        {
            if (mode == ArmMode.HoldProp) S?.SendAction(new ActionMsg { Type = ActionType.Release, Id = heldProp });
            mode = ArmMode.Idle;
            heldProp = 0;
            reaction = Vector3.zero;
        }

        void DropEverything()
        {
            if (mode == ArmMode.HoldProp) Drop();
            else if (OnYarn) DetachYarn();
            mode = ArmMode.Idle;
        }

        void CheckCarry()
        {
            var p = W?.GetProp(heldProp);
            if (p == null || p.Removed)
            {
                Drop();
                return;
            }
            var anchor = p.transform.TransformPoint(grabLocal);
            float err = Vector3.Distance(anchor, CarryTarget(p));
            float limit = p.Authority ? 1.8f : 2.6f;
            strainTime = err > limit ? strainTime + Time.deltaTime : Mathf.Max(0, strainTime - Time.deltaTime * 2f);
            if (strainTime > 1.2f || err > 5f)
            {
                // it slipped out of the gnome's hands
                Drop();
                Sfx.I?.Play(SoundId.Thud, anchor, 0.4f);
            }
        }

        void Throw()
        {
            var p = W?.GetProp(heldProp);
            if (p == null)
            {
                Drop();
                return;
            }
            float power = GameConsts.ThrowSpeed * Mathf.Clamp(2.5f / Mathf.Max(0.2f, p.Def.Mass), 0.35f, 1.35f) * gear.StrengthMul;
            var vel = LookDir * power + Vector3.up * 1.5f + Velocity * 0.6f;
            S?.SendAction(new ActionMsg { Type = ActionType.Throw, Id = heldProp, A = vel.ToCoreV() });
            Sfx.I?.Play(SoundId.Throw, HeadPos, 0.5f);
            mode = ArmMode.Idle;
            heldProp = 0;
        }

        void Kick()
        {
            if (kickCooldown > 0) return;
            kickCooldown = 0.5f;
            mode = mode == ArmMode.Idle ? ArmMode.Reach : mode;
            Invoke(nameof(EndKick), 0.2f);
            int mask = Layers.Grabbable & ~(1 << Layers.LocalGnome);
            var dir = Quaternion.Euler(-Mathf.Clamp(Pitch, -0.5f, 0.6f) * Mathf.Rad2Deg, Yaw * Mathf.Rad2Deg, 0) * Vector3.forward;
            var from = transform.position + Vector3.up * 0.45f;
            Avatar.Kick();
            if (!Physics.SphereCast(from, 0.18f, dir, out var hit, 1.5f, mask, QueryTriggerInteraction.Ignore))
            {
                Sfx.I?.Play(SoundId.Throw, from, 0.3f, 1.5f);
                return;
            }
            Sfx.I?.Play(SoundId.Punch, hit.point, 0.8f);
            var msg = new ActionMsg { Type = ActionType.Punch, A = hit.point.ToCoreV(), B = dir.ToCoreV() };
            var prop = hit.collider.GetComponentInParent<Prop>();
            var npc = hit.collider.GetComponentInParent<NpcBase>();
            var mech = hit.collider.GetComponentInParent<Mechanism>();
            var furn = hit.collider.GetComponentInParent<Furniture>();
            if (prop != null) { msg.I = 0; msg.Id = prop.Id; }
            else if (npc != null) { msg.I = 1; msg.Id = npc.NpcId; }
            else if (mech != null) { msg.I = 2; msg.Id = mech.Index; }
            else if (furn != null) { msg.I = 3; msg.Id = furn.Index; }
            else return;
            S?.SendAction(msg);
        }

        void EndKick()
        {
            if (mode == ArmMode.Reach) mode = ArmMode.Idle;
        }

        // ------------------------------------------------------------------ yarn

        void ShootYarn()
        {
            if (Time.time < yarnCooldownUntil) return;
            int mask = Layers.Grabbable & ~(1 << Layers.LocalGnome);
            float range = gear.YarnRange;
            var eye = HeadPos;
            Sfx.I?.Play(SoundId.Throw, eye, 0.4f, 1.3f);
            if (!Physics.SphereCast(eye, 0.05f, LookDir, out var hit, range, mask, QueryTriggerInteraction.Ignore))
            {
                // miss: the hook flies out and comes back
                flyFrom = HandPos;
                flyTo = eye + LookDir * range;
                flyUntil = Time.time + 0.35f;
                mode = ArmMode.YarnFly;
                Invoke(nameof(EndFly), 0.35f);
                return;
            }
            var prop = hit.collider.GetComponentInParent<Prop>();
            yarnLen = Mathf.Max(GameConsts.YarnMin, Vector3.Distance(HandPos, hit.point));
            if (prop != null && !prop.Removed)
            {
                mode = ArmMode.YarnProp;
                heldProp = prop.Id;
                grabLocal = prop.transform.InverseTransformPoint(hit.point);
            }
            else
            {
                mode = ArmMode.Climb;
                bool moving = hit.collider.GetComponentInParent<Mechanism>() != null || hit.collider.GetComponentInParent<NpcBase>() != null || hit.collider.GetComponentInParent<GnomeBody>() != null;
                hookParent = moving ? hit.collider.transform : null;
                hookLocal = moving ? hookParent.InverseTransformPoint(hit.point) : hit.point;
                var npc = hit.collider.GetComponentInParent<NpcBase>();
                if (npc != null) S?.SendAction(new ActionMsg { Type = ActionType.Punch, I = 1, Id = npc.NpcId, A = hit.point.ToCoreV() }); // grandpa feels it
            }
            strainTime = 0;
            Sfx.I?.Play(SoundId.Grab, hit.point, 0.6f);
        }

        void EndFly()
        {
            if (mode == ArmMode.YarnFly) mode = ArmMode.Idle;
        }

        void DetachYarn()
        {
            if (mode == ArmMode.YarnProp) S?.SendAction(new ActionMsg { Type = ActionType.Release, Id = heldProp });
            mode = ArmMode.Idle;
            heldProp = 0;
            hookParent = null;
            hookLocal = Vector3.zero;
            reaction = Vector3.zero;
        }

        void SnapYarn()
        {
            DetachYarn();
            yarnCooldownUntil = Time.time + GameConsts.YarnSnapTime;
            Sfx.I?.Play(SoundId.ArmPop, HandPos, 0.9f);
            GameApp.I?.Toast(Loc.T("yarnSnapped"));
        }

        void CheckYarnProp()
        {
            var p = W?.GetProp(heldProp);
            if (p == null || p.Removed)
            {
                DetachYarn();
                return;
            }
            var anchor = p.transform.TransformPoint(grabLocal);
            float over = Vector3.Distance(anchor, HandPos) - yarnLen;
            strainTime = over > 2.5f ? strainTime + Time.deltaTime : Mathf.Max(0, strainTime - Time.deltaTime);
            if (strainTime > 2.5f || over > gear.YarnRange) SnapYarn();
        }

        void TieYarn()
        {
            // tie the current yarn to whatever we look at
            int mask = Layers.Grabbable & ~(1 << Layers.LocalGnome);
            if (!Physics.Raycast(HeadPos, LookDir, out var hit, gear.YarnRange, mask, QueryTriggerInteraction.Ignore)) return;
            var target = hit.collider.GetComponentInParent<Prop>();
            var msg = new ActionMsg { Type = ActionType.Tie, B = hit.point.ToCoreV(), S = target != null ? target.Id.ToString() : "" };
            if (mode == ArmMode.YarnProp)
            {
                var p = W?.GetProp(heldProp);
                if (p == null || p == target) return;
                msg.Id = heldProp;
                msg.A = p.transform.TransformPoint(grabLocal).ToCoreV();
            }
            else
            {
                if (target == null) return; // world-to-world ties make no sense
                msg.Id = 0;
                msg.A = HookWorld.ToCoreV();
            }
            S?.SendAction(msg);
            Sfx.I?.Play(SoundId.Grab, hit.point, 0.7f, 0.8f);
            mode = ArmMode.Idle;
            heldProp = 0;
            hookParent = null;
            hookLocal = Vector3.zero;
        }

        void YarnJump()
        {
            var anchor = HookWorld;
            var fwd = (anchor - transform.position).Flat();
            if (fwd.sqrMagnitude < 0.01f) fwd = YawRot * Vector3.forward;
            fwd.Normalize();
            DetachYarn();
            var v = Rb.Vel();
            var probe = anchor + fwd * 0.45f + Vector3.up * 1.8f;
            if (Physics.Raycast(probe, Vector3.down, out var top, 2.6f, Layers.World | (1 << Layers.Prop), QueryTriggerInteraction.Ignore) && top.normal.y > 0.6f && top.point.y > transform.position.y + 0.2f)
            {
                float h = top.point.y + 0.25f - transform.position.y;
                float vy = Mathf.Sqrt(2f * -Physics.gravity.y * Mathf.Max(0.1f, h)) + 0.4f;
                v = Vector3.up * Mathf.Min(vy, 11f) + fwd * 3.2f;
            }
            else v = new Vector3(v.x, gear.JumpSpeed * 0.9f, v.z) + fwd * 1.5f;
            Rb.SetVel(v);
            Sfx.I?.Play(SoundId.Jump, transform.position, 0.4f);
        }

        // ------------------------------------------------------------------ interaction

        void ScanInteractables()
        {
            lookMech = null;
            lookProp = null;
            nearGoHome = nearCraft = nearSock = false;
            Prompt = null;
            if (W == null) return;
            var eye = HeadPos;
            var dir = LookDir;
            float best = Mathf.Cos(28f * Mathf.Deg2Rad);
            foreach (var m in W.Mechs)
            {
                if (m.HumanOnly || m.Role == "mousetrap") continue;
                var to = m.HandleWorld - eye;
                float d = to.magnitude;
                if (d > GameConsts.MechReach || d < 0.01f) continue;
                float c = Vector3.Dot(to / d, dir);
                if (c > best)
                {
                    best = c;
                    lookMech = m;
                }
            }
            if (lookMech != null)
            {
                Prompt = lookMech.Role == "jar" ? Loc.T("unscrew") : Loc.T("interact") + " (" + MechName(lookMech) + ")";
                return;
            }
            if (Physics.SphereCast(eye, 0.06f, dir, out var hit, gear.YarnRange, Layers.Grabbable & ~(1 << Layers.LocalGnome), QueryTriggerInteraction.Ignore))
            {
                var p = hit.collider.GetComponentInParent<Prop>();
                if (p != null && hit.distance < GameConsts.InteractRange)
                {
                    if (p.Def.Has(ItemFlags.Pocketable))
                    {
                        lookProp = p;
                        Prompt = Loc.T("pocketIt") + ": " + p.Def.Name(Loc.Current);
                        return;
                    }
                    Prompt = (p.Def.HasTag("gnomeHat") ? Loc.T("hatHere") : Loc.T("grab") + ": " + p.Def.Name(Loc.Current)) + (p.Def.Has(ItemFlags.Heavy) ? " (!)" : "");
                    return;
                }
                if (OnYarn) Prompt = Loc.T("tieHint");
            }
            if (W.Kind == LevelKind.House && Vector3.Distance(transform.position, W.GoHomePoint) < 3.5f)
            {
                nearGoHome = true;
                Prompt = Loc.T("goHome");
            }
            if (W.Kind == LevelKind.Hub)
            {
                if (W.CraftBench && Vector3.Distance(transform.position, W.CraftBench.position) < 4.5f)
                {
                    nearCraft = true;
                    Prompt = "E — " + Loc.T("craftBench");
                }
                else if (W.HighGnome && Vector3.Distance(transform.position, W.HighGnome.position) < 6f)
                {
                    nearSock = true;
                    Prompt = "E — " + Loc.T("highGnomeTalk");
                }
            }
        }

        static string MechName(Mechanism m)
        {
            switch (m.Role)
            {
                case "trap": return m.Owner != null && m.Owner.Kind == "stove" ? Loc.T("mechOven") : Loc.T("mechFreezer");
                case "fridge": return Loc.T("mechFridge");
                case "window": return Loc.T("mechWindow");
                case "tvPower": return Loc.T("mechTv");
                case "flush": return Loc.T("mechFlush");
                case "faucet":
                case "tubFaucet": return Loc.T("mechTap");
                case "safe": return m.Locked ? Loc.T("mechSafeLocked") : Loc.T("mechSafe");
                case "jar": return Loc.T("mechJar");
                default: return m.Role;
            }
        }

        void Interact()
        {
            if (lookMech != null)
            {
                S?.SendAction(new ActionMsg { Type = ActionType.Interact, Id = lookMech.Index });
                return;
            }
            if (lookProp != null)
            {
                if (mode == ArmMode.HoldProp && heldProp == lookProp.Id) Drop();
                S?.SendAction(new ActionMsg { Type = ActionType.Pocket, Id = lookProp.Id });
                return;
            }
            if (nearCraft) CraftMenuRequested = true;
            if (nearSock) GameApp.I?.ShowGreatSock();
        }

        ArmState CurrentArms()
        {
            var a = new ArmState { Mode = mode };
            switch (mode)
            {
                case ArmMode.HoldProp:
                    a.PropId = heldProp;
                    a.Anchor = grabLocal.ToCoreV();
                    var p = W?.GetProp(heldProp);
                    a.Hand = CarryTarget(p).ToCoreV();
                    break;
                case ArmMode.Climb:
                    a.Anchor = HookWorld.ToCoreV();
                    a.Hand = new V3(yarnLen, 0, 0);
                    break;
                case ArmMode.YarnProp:
                    a.PropId = heldProp;
                    a.Anchor = grabLocal.ToCoreV();
                    a.Hand = new V3(yarnLen, 0, 0);
                    break;
                case ArmMode.YarnFly:
                    float k = 1f - Mathf.Clamp01((flyUntil - Time.time) / 0.35f);
                    float out01 = k < 0.5f ? k * 2f : (1f - k) * 2f;
                    a.Anchor = flyFrom.ToCoreV();
                    a.Hand = Vector3.Lerp(HandPos, flyTo, out01).ToCoreV();
                    break;
                case ArmMode.Reach:
                    a.Hand = (HeadPos + LookDir * 0.9f).ToCoreV();
                    break;
            }
            Arms = a;
            return a;
        }

        // ------------------------------------------------------------------ status changes (from the session)

        public void OnStatus(PlayerStatus s)
        {
            if (Status == s) return;
            var old = Status;
            Status = s;
            if (s != PlayerStatus.Free) DropEverything();
            if (s == PlayerStatus.Dead)
            {
                Sfx.I?.PlayUi(SoundId.Death);
                ghostPos = HeadPos + Vector3.up * 2.5f;
            }
            if (s == PlayerStatus.Free && old != PlayerStatus.Free)
            {
                Rb.isKinematic = false;
                Col.enabled = true;
                Rb.SetVel(Vector3.zero);
            }
        }

        public void Teleport(Vector3 pos, float yaw)
        {
            Rb.isKinematic = false;
            Col.enabled = true;
            transform.position = pos;
            Rb.position = pos;
            Rb.SetVel(Vector3.zero);
            Yaw = yaw;
            Pitch = 0;
        }
    }
}
