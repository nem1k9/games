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
    /// <summary>Physics hold force shared by the host (applied to props) and clients (reaction on the gnome).</summary>
    public static class GrabPhysics
    {
        public static Vector3 HoldForce(Vector3 anchor, Vector3 anchorVel, Vector3 target, float mass, float strength, float gravityShare = 1f)
        {
            var err = target - anchor;
            var desiredVel = Vector3.ClampMagnitude(err * 11f, 9f);
            var f = (desiredVel - anchorVel) * mass * 12f;
            f += Vector3.up * (mass * -Physics.gravity.y * gravityShare);
            return Vector3.ClampMagnitude(f, strength);
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
        Vector3 groundNormal = Vector3.up;
        float lastVy;
        bool wantCrouch;

        // arms
        ArmMode mode;
        float armLen = 1.4f;
        ushort heldProp;
        Vector3 holdAnchorLocal;
        Transform climbParent;
        Vector3 climbLocal;
        float grip = GameConsts.GripTime;
        float strainTime;
        float rippedUntil;
        float punchCooldown;
        Vector3 reaction;
        float lastSend;

        // interaction
        public string Prompt;
        public float HoldProgress; // 0..1 for "hold E" actions
        float holdE;
        Mechanism lookMech;
        Prop lookProp;
        bool nearRevive, nearCraft, nearHighGnome;
        public bool CraftMenuRequested;

        // carried / trapped / dead
        public Vector3 ForcedPos;
        public float ForcedYaw;
        float struggleSend;
        Vector3 ghostPos;
        float stepDistance;

        public ushort HeldProp => mode == ArmMode.HoldProp ? heldProp : (ushort)0;
        public float Grip01 => grip / GameConsts.GripTime;
        public bool Climbing => mode == ArmMode.Climb;
        public float ArmLength => armLen;
        public float ArmMax => gear.ArmMax;
        public bool RippedArms => Time.time < rippedUntil;

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
            armLen = Mathf.Clamp(armLen, GameConsts.ArmMin, gear.ArmMax);
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
            ApplyCameraMode();
        }

        void ApplyCameraMode()
        {
            Avatar.SetFirstPerson(!ThirdPerson);
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
                if (Input.GetKeyDown(KeyCode.V))
                {
                    ThirdPerson = !ThirdPerson;
                    ApplyCameraMode();
                }
            }

            switch (Status)
            {
                case PlayerStatus.Free:
                    UpdateFree(inputEnabled);
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
            Avatar.FootstepVolume = Crouching ? 0.15f : Sprinting ? 0.8f : 0.4f;

            // network state @30 Hz
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
            if (punchCooldown > 0) punchCooldown -= Time.deltaTime;
            if (!input)
            {
                wantCrouch = false;
                if (mode != ArmMode.Idle && !Input.GetMouseButton(0)) Release();
                return;
            }
            wantCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            Sprinting = Input.GetKey(KeyCode.LeftShift) && !Crouching;
            if (Input.GetKeyDown(KeyCode.Space)) jumpBufferedUntil = Time.time + 0.15f;

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) armLen = Mathf.Clamp(armLen + wheel * 0.3f, GameConsts.ArmMin, gear.ArmMax);

            // --- grabbing ---
            if (RippedArms)
            {
                if (mode != ArmMode.Idle) Release();
            }
            else if (Input.GetMouseButton(0))
            {
                if (mode == ArmMode.Idle || mode == ArmMode.Reach) TryGrab();
            }
            else if (mode != ArmMode.Idle) Release();

            if (Input.GetMouseButtonDown(1))
            {
                if (mode == ArmMode.HoldProp) Throw();
                else if (mode == ArmMode.Idle || mode == ArmMode.Reach) Punch();
            }

            if (mode == ArmMode.HoldProp) CheckHold();
            if (mode == ArmMode.Climb)
            {
                if (!Grounded) grip -= Time.deltaTime;
                if (grip <= 0)
                {
                    Release();
                    GameApp.I?.Toast(Loc.T("gripLost"));
                }
                if (Time.time < jumpBufferedUntil)
                {
                    jumpBufferedUntil = 0;
                    ClimbJump();
                }
            }
            else if (Grounded) grip = Mathf.Min(GameConsts.GripTime, grip + Time.deltaTime * 3f);

            // --- interaction ---
            ScanInteractables();
            if (Input.GetKeyDown(KeyCode.E)) Interact();
            if (Input.GetKey(KeyCode.E) && nearRevive && lookMech == null && lookProp == null && !AnyDeadTeammate())
            {
                holdE += Time.deltaTime;
                HoldProgress = Mathf.Clamp01(holdE / 1.5f);
                if (holdE >= 1.5f)
                {
                    holdE = -999;
                    S?.SendAction(new ActionMsg { Type = ActionType.GoHome, I = 1 });
                }
            }
            else
            {
                if (!Input.GetKey(KeyCode.E)) holdE = 0;
                HoldProgress = 0;
            }
            if (Input.GetKeyDown(KeyCode.Q)) S?.SendAction(new ActionMsg { Type = ActionType.DropPockets, A = (HeadPos + LookDir * 0.6f).ToCoreV(), B = (LookDir * 2f).ToCoreV() });
            if (Input.GetKeyDown(KeyCode.G) && S != null && S.Save != null && S.Save.Potions > 0)
                S.SendAction(new ActionMsg { Type = ActionType.Potion, A = (HeadPos + LookDir * 0.5f).ToCoreV(), B = (LookDir * 11f + Vector3.up * 2f + Velocity * 0.5f).ToCoreV() });
            if (Input.GetKeyDown(KeyCode.H)) S?.SendAction(new ActionMsg { Type = ActionType.Honk, A = HeadPos.ToCoreV() });
        }

        bool AnyDeadTeammate() => S != null && S.AnyDead;

        void UpdateCaptured(bool input)
        {
            if (mode != ArmMode.Idle) Release();
            Rb.isKinematic = true;
            Col.enabled = false;
            Crouching = false;
            Sprinting = false;
            var target = ForcedPos;
            transform.position = Vector3.Distance(transform.position, target) > 6f ? target : Compat.Damp(transform.position, target, 14f, Time.deltaTime);
            Velocity = Vector3.zero;
            Grounded = false;
            Prompt = Loc.T("struggle");
            Struggling = input && (Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.Space));
            if (input && (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space)) && Time.time - struggleSend > 0.06f)
            {
                struggleSend = Time.time;
                S?.SendAction(new ActionMsg { Type = ActionType.Struggle });
                Sfx.I?.Play(SoundId.Struggle, HeadPos, 0.6f, Random.Range(0.9f, 1.3f));
            }
        }

        void UpdateGhost(bool input)
        {
            if (mode != ArmMode.Idle) Release();
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
                Avatar.SetFirstPerson(false);
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
            // crouch (only stand up if there is room)
            if (!wantCrouch && Crouching)
            {
                if (!Physics.SphereCast(transform.position + Vector3.up * 0.3f, GameConsts.GnomeRadius * 0.9f, Vector3.up, out _, 0.5f, Layers.World, QueryTriggerInteraction.Ignore))
                    Crouching = false;
            }
            else if (wantCrouch) Crouching = true;
            SetCrouchCollider(Crouching);

            // ground check
            var origin = transform.position + Vector3.up * (GameConsts.GnomeRadius + 0.05f);
            Grounded = Physics.SphereCast(origin, GameConsts.GnomeRadius * 0.92f, Vector3.down, out var gh, 0.14f, Layers.SolidForGnome & ~(1 << Layers.LocalGnome), QueryTriggerInteraction.Ignore)
                       && gh.normal.y > 0.55f;
            if (Grounded)
            {
                groundNormal = gh.normal;
                lastGroundedTime = Time.time;
            }
            var v = Rb.Vel();
            var app = GameApp.I;
            bool input = app != null && app.GameplayInput;
            float ix = input ? (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0) : 0;
            float iz = input ? (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0) : 0;
            var wish = YawRot * new Vector3(ix, 0, iz);
            if (wish.sqrMagnitude > 1) wish.Normalize();
            float speed = Crouching ? GameConsts.CrouchSpeed : Sprinting ? GameConsts.SprintSpeed : GameConsts.WalkSpeed;
            if (mode == ArmMode.HoldProp)
            {
                var p = W?.GetProp(heldProp);
                if (p != null && p.Def.Mass > 1.5f) speed *= Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(1.5f, 6f, p.Def.Mass));
            }
            var targetH = wish * speed;
            var h = v.Flat();
            if (mode == ArmMode.Climb && !Grounded)
            {
                // swing a little while hanging
                Rb.AddForce(wish * 14f, ForceMode.Force);
            }
            else if (Grounded)
            {
                h = Vector3.MoveTowards(h, targetH, GameConsts.GroundAccel * dt);
                v = new Vector3(h.x, v.y, h.z);
                if (v.y > 0 && Time.time - lastGroundedTime < 0.05f && v.y < 1f) v.y = Mathf.Min(v.y, 0.5f); // stick to slopes
            }
            else
            {
                var dv = Vector3.ClampMagnitude(targetH - h, GameConsts.AirAccel * dt);
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
            if (Grounded && lastVy < -9f) Sfx.I?.Play(SoundId.Land, transform.position, Mathf.Clamp01(-lastVy / 16f));
            lastVy = v.y;
            Rb.SetVel(v);

            // arms physics
            if (mode == ArmMode.Climb) ClimbForce(dt);
            else if (mode == ArmMode.HoldProp) HoldReaction(dt);
            Velocity = Rb.Vel();

            // fell out of the world: back to the spawn
            if (transform.position.y < -25f && W != null)
            {
                Rb.position = W.SpawnPoint(PlayerId);
                Rb.SetVel(Vector3.zero);
            }
        }

        Vector3 Shoulder => transform.position + Vector3.up * GameConsts.GnomeShoulder * (Crouching ? 0.75f : 1f);

        Vector3 ClimbAnchor => climbParent != null ? climbParent.TransformPoint(climbLocal) : climbLocal;

        void ClimbForce(float dt)
        {
            if (climbParent == null && climbLocal == Vector3.zero)
            {
                Release();
                return;
            }
            var anchor = ClimbAnchor;
            var d = anchor - Shoulder;
            float dist = d.magnitude;
            if (dist > gear.ArmMax + 1.2f)
            {
                Release();
                return;
            }
            if (dist > armLen)
            {
                var dir = d / dist;
                float stretch = dist - armLen;
                float along = Vector3.Dot(Rb.Vel(), dir);
                float f = stretch * 260f - along * 28f;
                f = Mathf.Clamp(f, 0f, GameConsts.ClimbStrength * gear.StrengthMul);
                Rb.AddForce(dir * f, ForceMode.Force);
            }
            // a bit of air drag while hanging so swinging calms down
            if (!Grounded) Rb.AddForce(-Rb.Vel() * 1.2f, ForceMode.Force);
        }

        void HoldReaction(float dt)
        {
            var p = W?.GetProp(heldProp);
            if (p == null)
            {
                Release();
                return;
            }
            var anchor = p.transform.TransformPoint(holdAnchorLocal);
            var target = HoldTarget;
            var pv = p.Authority ? p.Rb.GetPointVelocity(anchor) : Vector3.zero;
            var f = GrabPhysics.HoldForce(anchor, pv, target, p.Def.Mass, GameConsts.HoldStrength * gear.StrengthMul);
            // Newton's third law: heavy things pull the gnome towards them (drag/climb), light ones barely matter
            var r = -f;
            r.y = Mathf.Min(r.y, 0) * 0.5f; // don't get launched upwards by pushing things down
            reaction = Compat.Damp(reaction, r * Mathf.Clamp01(p.Def.Mass / (p.Def.Mass + GameConsts.GnomeMass)), 10f, dt);
            if (Grounded) reaction.y = Mathf.Min(0, reaction.y);
            Rb.AddForce(reaction * 0.8f, ForceMode.Force);
            if (p.Authority && S != null && S.IsHost)
            {
                // the host applies the hold force to the real prop directly
                S.Host?.ApplyHold(PlayerId, p, anchor, target);
            }
        }

        public Vector3 HoldTarget => HeadPos + LookDir * armLen;

        // ------------------------------------------------------------------ arms actions

        void TryGrab()
        {
            float reach = gear.ArmMax + 0.35f;
            var eye = HeadPos;
            var dir = LookDir;
            int mask = Layers.Grabbable & ~(1 << Layers.LocalGnome);
            if (!Physics.SphereCast(eye, 0.07f, dir, out var hit, reach, mask, QueryTriggerInteraction.Ignore))
            {
                mode = ArmMode.Reach;
                return;
            }
            var prop = hit.collider.GetComponentInParent<Prop>();
            if (prop != null && !prop.Removed)
            {
                mode = ArmMode.HoldProp;
                heldProp = prop.Id;
                holdAnchorLocal = prop.transform.InverseTransformPoint(hit.point);
                armLen = Mathf.Clamp(hit.distance, GameConsts.ArmMin, gear.ArmMax);
                strainTime = 0;
                Sfx.I?.Play(prop.Def.Has(ItemFlags.Squeaky) ? SoundId.Squeak : SoundId.Grab, hit.point, 0.5f);
                return;
            }
            // anything else: hang on to it (walls, furniture, doors, grandpa, friends...)
            mode = ArmMode.Climb;
            var moving = hit.collider.GetComponentInParent<Mechanism>() != null || hit.collider.GetComponentInParent<NpcBase>() != null || hit.collider.GetComponentInParent<GnomeBody>() != null;
            if (moving)
            {
                climbParent = hit.collider.transform;
                climbLocal = climbParent.InverseTransformPoint(hit.point);
            }
            else
            {
                climbParent = null;
                climbLocal = hit.point;
            }
            armLen = Mathf.Clamp(Vector3.Distance(Shoulder, hit.point), GameConsts.ArmMin, gear.ArmMax);
            Sfx.I?.Play(SoundId.Grab, hit.point, 0.4f);
            var npc = hit.collider.GetComponentInParent<NpcBase>();
            if (npc != null) S?.SendAction(new ActionMsg { Type = ActionType.Punch, I = 1, Id = npc.NpcId, A = hit.point.ToCoreV(), B = Vector3.zero.ToCoreV() }); // grabbing grandpa annoys him
        }

        void Release()
        {
            if (mode == ArmMode.HoldProp) S?.SendAction(new ActionMsg { Type = ActionType.Release, Id = heldProp });
            mode = ArmMode.Idle;
            heldProp = 0;
            climbParent = null;
            climbLocal = Vector3.zero;
            reaction = Vector3.zero;
        }

        void CheckHold()
        {
            var p = W?.GetProp(heldProp);
            if (p == null || p.Removed)
            {
                Release();
                return;
            }
            var anchor = p.transform.TransformPoint(holdAnchorLocal);
            float err = Vector3.Distance(anchor, HoldTarget);
            float limit = p.Authority ? 2.2f : 2.8f; // clients see props slightly delayed
            strainTime = err > limit ? strainTime + Time.deltaTime : Mathf.Max(0, strainTime - Time.deltaTime * 2f);
            if (err > gear.ArmMax + 3f) strainTime = 99f;
            if (strainTime > 1.1f)
            {
                // too heavy or stuck: POP! the arms come off (they grow back)
                Release();
                rippedUntil = Time.time + GameConsts.ArmRegrowTime;
                ArmsRipped = true;
                Sfx.I?.Play(SoundId.ArmPop, HeadPos, 1f);
                GameApp.I?.Toast(Loc.T("armsRipped"));
                Fx.Puff(W, HeadPos + LookDir * 0.5f, 0.4f);
                Invoke(nameof(RegrowArms), GameConsts.ArmRegrowTime);
            }
        }

        void RegrowArms()
        {
            ArmsRipped = false;
            Sfx.I?.Play(SoundId.Pickup, HeadPos, 0.6f);
        }

        void Throw()
        {
            var p = W?.GetProp(heldProp);
            if (p == null)
            {
                Release();
                return;
            }
            float power = GameConsts.ThrowSpeed * Mathf.Clamp(2.5f / Mathf.Max(0.2f, p.Def.Mass), 0.35f, 1.35f) * gear.StrengthMul;
            var vel = LookDir * power + Vector3.up * 1.5f + Velocity * 0.6f;
            S?.SendAction(new ActionMsg { Type = ActionType.Throw, Id = heldProp, A = vel.ToCoreV() });
            Sfx.I?.Play(SoundId.Throw, HeadPos, 0.5f);
            mode = ArmMode.Idle;
            heldProp = 0;
        }

        void Punch()
        {
            if (punchCooldown > 0 || RippedArms) return;
            punchCooldown = 0.45f;
            var eye = HeadPos;
            var dir = LookDir;
            int mask = Layers.Grabbable & ~(1 << Layers.LocalGnome);
            // swing the arms forward for the animation
            mode = ArmMode.Reach;
            armLen = Mathf.Min(armLen, 1.2f);
            Invoke(nameof(EndPunch), 0.18f);
            if (!Physics.SphereCast(eye, 0.12f, dir, out var hit, 1.9f, mask, QueryTriggerInteraction.Ignore))
            {
                Sfx.I?.Play(SoundId.Throw, eye, 0.3f, 1.4f);
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

        void EndPunch()
        {
            if (mode == ArmMode.Reach && !Input.GetMouseButton(0)) mode = ArmMode.Idle;
        }

        void ClimbJump()
        {
            var anchor = ClimbAnchor;
            var fwd = (anchor - transform.position).Flat();
            if (fwd.sqrMagnitude < 0.01f) fwd = YawRot * Vector3.forward;
            fwd.Normalize();
            Release();
            var v = Rb.Vel();
            // look for a ledge just beyond the grab point and hop onto it
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
            nearRevive = nearCraft = nearHighGnome = false;
            Prompt = null;
            if (W == null) return;
            var eye = HeadPos;
            var dir = LookDir;
            // mechanisms: best handle inside a cone
            float best = Mathf.Cos(28f * Mathf.Deg2Rad);
            foreach (var m in W.Mechs)
            {
                if (m.HumanOnly) continue;
                var to = m.HandleWorld - eye;
                float d = to.magnitude;
                if (d > GameConsts.InteractRange || d < 0.01f) continue;
                float c = Vector3.Dot(to / d, dir);
                if (c > best)
                {
                    best = c;
                    lookMech = m;
                }
            }
            if (lookMech != null)
            {
                Prompt = Loc.T("interact") + " (" + MechName(lookMech) + ")";
                return;
            }
            if (Physics.SphereCast(eye, 0.06f, dir, out var hit, GameConsts.InteractRange, Layers.Grabbable & ~(1 << Layers.LocalGnome), QueryTriggerInteraction.Ignore))
            {
                var p = hit.collider.GetComponentInParent<Prop>();
                if (p != null && p.Def.Has(ItemFlags.Pocketable))
                {
                    lookProp = p;
                    Prompt = Loc.T("pocketIt") + ": " + p.Def.Name(Loc.Current);
                    return;
                }
                if (p != null) Prompt = Loc.T("grab") + ": " + p.Def.Name(Loc.Current) + (p.Def.Has(ItemFlags.Heavy) ? " (!)" : "");
            }
            if (W.Kind == LevelKind.House && Vector3.Distance(transform.position, W.RevivePoint) < 3.5f)
            {
                nearRevive = true;
                Prompt = AnyDeadTeammate() ? Loc.T("reviveAt") : Loc.T("goHome");
            }
            if (W.Kind == LevelKind.Hub)
            {
                if (W.CraftBench && Vector3.Distance(transform.position, W.CraftBench.position) < 4.5f)
                {
                    nearCraft = true;
                    Prompt = "E — " + Loc.T("craftBench");
                }
                else if (W.HighGnome && Vector3.Distance(transform.position, W.HighGnome.position) < 7f)
                {
                    nearHighGnome = true;
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
                S?.SendAction(new ActionMsg { Type = ActionType.Pocket, Id = lookProp.Id });
                return;
            }
            if (nearRevive && AnyDeadTeammate())
            {
                S?.SendAction(new ActionMsg { Type = ActionType.Revive });
                return;
            }
            if (nearCraft) CraftMenuRequested = true;
            if (nearHighGnome) GameApp.I?.ShowHighGnome();
        }

        ArmState CurrentArms()
        {
            var a = new ArmState { Mode = mode };
            switch (mode)
            {
                case ArmMode.HoldProp:
                    a.PropId = heldProp;
                    a.Anchor = holdAnchorLocal.ToCoreV();
                    a.Hand = HoldTarget.ToCoreV();
                    break;
                case ArmMode.Climb:
                    var anc = ClimbAnchor;
                    a.Anchor = anc.ToCoreV();
                    a.Hand = anc.ToCoreV();
                    break;
                case ArmMode.Reach:
                    a.Hand = (HeadPos + LookDir * armLen).ToCoreV();
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
            if (s != PlayerStatus.Free) Release();
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
