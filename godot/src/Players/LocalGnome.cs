using Gnomes.Core;
using Gnomes.Core.Protocol;
using Gnomes.Core.Rules;
using Godot;
using SockGang.App;
using SockGang.Audio;
using SockGang.NPC;
using SockGang.Session;
using SockGang.World;

namespace SockGang.Players
{
    /// <summary>The gnome controlled on this machine (client-authoritative movement).</summary>
    public partial class LocalGnome : GnomeBody
    {
        public override bool IsLocal => true;
        public static LocalGnome I;

        public Camera3D Cam;
        Node3D camPivot;
        public bool ThirdPerson;
        GameSession S => GameSession.I;
        GameWorld W => GameWorld.Current;
        GearEffects gear;

        // movement
        float lastGroundedTime = -10, jumpBufferedUntil = -10, lastVy;
        bool wantCrouch;
        public bool Gliding;
        float stunnedUntil;

        // hands & yarn
        ArmMode mode;
        ushort heldProp;
        Vector3 grabLocal; // prop-local grab / hook point (Godot space)
        float holdDist = GameConsts.HoldDistance;
        float yarnLen = 3f;
        Node3D hookParent;
        Vector3 hookLocal; // world point when hookParent == null
        float grip = GameConsts.GripTime;
        float strainTime, yarnCooldownUntil, kickCooldown, kickEndAt = -1, flyUntil;
        Vector3 reaction, flyFrom, flyTo;
        float lastSend;

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
        public bool Stunned => Clock.Now < stunnedUntil;
        public ArmMode Mode => mode;

        public static LocalGnome Create(GameWorld w, byte id, string name, Color hat, Vector3 pos, float yaw)
        {
            var g = new LocalGnome { Name = $"LocalGnome_{id}", PlayerId = id, PlayerName = name, Hat = hat, Yaw = yaw };
            w.GnomeRoot.AddChild(g);
            g.GlobalPosition = pos;
            g.CreateBody(Layers.LocalGnome, Layers.SolidForGnome);
            g.Avatar = GnomeAvatar.Create(g, hat);
            g.BuildCamera();
            g.RefreshGear();
            w.Gnomes[id] = g;
            I = g;
            return g;
        }

        public override void _ExitTree()
        {
            if (I == this) I = null;
        }

        public void RefreshGear() => gear = GearEffects.From(S?.Save);

        void BuildCamera()
        {
            camPivot = new Node3D { Name = "CamPivot", TopLevel = true };
            AddChild(camPivot);
            Cam = new Camera3D { Name = "GnomeCamera", Near = 0.03f, Far = 400f, Current = true };
            camPivot.AddChild(Cam);
            var listener = new AudioListener3D();
            Cam.AddChild(listener);
            listener.MakeCurrent();
            // a faint "night eyes" glow around the player so dark rooms stay playable (local only)
            var glow = new OmniLight3D { Name = "NightEyes", OmniRange = 7f, LightEnergy = 0.45f, LightColor = new Color(0.72f, 0.8f, 1f), ShadowEnabled = false, Position = new Vector3(0, 0.4f, -0.3f) };
            camPivot.AddChild(glow);
            Avatar.SetFirstPerson(true);
        }

        public void Stun(float seconds)
        {
            stunnedUntil = Mathf.Max(stunnedUntil, Clock.Now + seconds);
            DropEverything();
            Fx.Stars(W, HeadPos + Vector3.Up * 0.3f);
        }

        // ------------------------------------------------------------------ frame

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            var app = GameApp.I;
            bool inputEnabled = app != null && app.GameplayInput;
            var st = app?.Settings;
            float sens = st != null ? st.Sensitivity : 2f;
            Cam.Fov = st != null ? st.Fov : 75f;

            if (inputEnabled)
            {
                var md = Keys.MouseDelta;
                Yaw -= md.X * 0.1f * sens * Mathf.Pi / 180f;
                Pitch = Mathf.Clamp(Pitch - md.Y * 0.1f * sens * Mathf.Pi / 180f * (st != null && st.InvertY ? -1 : 1), -1.45f, 1.45f);
                if (Keys.Pressed(Key.V)) ThirdPerson = !ThirdPerson;
            }
            if (kickEndAt > 0 && Clock.Now >= kickEndAt)
            {
                kickEndAt = -1;
                if (mode == ArmMode.Reach) mode = ArmMode.Idle;
            }
            if (mode == ArmMode.YarnFly && Clock.Now >= flyUntil) mode = ArmMode.Idle;

            switch (Status)
            {
                case PlayerStatus.Free:
                    UpdateFree(inputEnabled && !Stunned, dt);
                    break;
                case PlayerStatus.Carried:
                case PlayerStatus.Trapped:
                    UpdateCaptured(inputEnabled, dt);
                    break;
                default:
                    UpdateGhost(inputEnabled, dt);
                    break;
            }
            UpdateCamera();
            FeedAvatar();
            AvatarHands(CurrentArms(), GlobalPosition + YawBasis * new Vector3(0, 0.3f, -0.3f));
            Avatar.FootstepVolume = Crouching ? 0.12f : Sprinting ? 0.8f : 0.35f;
            Avatar.Gliding = Gliding;

            if (S != null && Clock.Now - lastSend >= 1f / GameConsts.ClientStateHz)
            {
                lastSend = Clock.Now;
                S.SubmitLocalState(this);
            }
        }

        void UpdateFree(bool input, float dt)
        {
            Struggling = false;
            if (Col.Disabled) Col.Disabled = false;
            if (kickCooldown > 0) kickCooldown -= dt;
            ScanInteractables();
            if (!input)
            {
                wantCrouch = false;
                Sprinting = false;
                return;
            }
            wantCrouch = Keys.Held(Key.Ctrl) || Keys.Held(Key.C);
            Sprinting = Keys.Held(Key.Shift) && !Crouching;
            if (Keys.Pressed(Key.Space)) jumpBufferedUntil = Clock.Now + 0.15f;

            float wheel = Keys.Wheel;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                if (OnYarn) yarnLen = Mathf.Clamp(yarnLen - wheel * 0.45f, GameConsts.YarnMin, gear.YarnRange + 1f);
                else if (mode == ArmMode.HoldProp) holdDist = Mathf.Clamp(holdDist + wheel * 0.15f, 0.55f, 1.6f);
            }
            if (OnYarn && Keys.Held(Key.R)) yarnLen = Mathf.Max(GameConsts.YarnMin, yarnLen - 3f * dt);

            // hands: LMB picks up / drops
            if (Keys.MouseDown(0))
            {
                if (mode == ArmMode.HoldProp) Drop();
                else if (!OnYarn) TryPickUp();
            }
            // RMB: throw what you carry, otherwise the yarn hook
            if (Keys.MouseDown(1))
            {
                if (mode == ArmMode.HoldProp) Throw();
                else if (OnYarn) DetachYarn();
                else ShootYarn();
            }
            if (Keys.Pressed(Key.T) && OnYarn) TieYarn();
            if (Keys.Pressed(Key.F)) Kick();

            if (mode == ArmMode.HoldProp) CheckCarry(dt);
            if (mode == ArmMode.YarnProp) CheckYarnProp(dt);
            if (mode == ArmMode.Climb)
            {
                if (!Grounded) grip -= dt;
                if (grip <= 0)
                {
                    DetachYarn();
                    GameApp.I?.Toast(Loc.T("gripLost"));
                }
                if (Clock.Now < jumpBufferedUntil && !Grounded)
                {
                    jumpBufferedUntil = 0;
                    YarnJump();
                }
            }
            else if (Grounded) grip = Mathf.Min(GameConsts.GripTime, grip + dt * 3f);

            // interaction
            bool eDown = Keys.Pressed(Key.E), eHeld = Keys.Held(Key.E);
            bool jarLid = lookMech != null && lookMech.Role == "jar";
            if (eDown && !jarLid) Interact();
            if (eHeld && (jarLid || (nearGoHome && lookMech == null && lookProp == null)))
            {
                holdE += dt;
                float need = jarLid ? GameConsts.JarUnscrewSeconds : 1.5f;
                HoldProgress = Mathf.Clamp(holdE / need, 0f, 1f);
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
            if (Keys.Pressed(Key.Q)) S?.SendAction(new ActionMsg { Type = ActionType.DropPockets, A = (HeadPos + LookDir * 0.6f).U(), B = (LookDir * 2f).U() });
            if (Keys.Pressed(Key.G) && S?.Save != null && S.Save.Potions > 0)
                S.SendAction(new ActionMsg { Type = ActionType.Potion, A = (HeadPos + LookDir * 0.5f).U(), B = (LookDir * 11f + Vector3.Up * 2f + Velocity * 0.5f).U() });
            if (Keys.Pressed(Key.H)) S?.SendAction(new ActionMsg { Type = ActionType.Honk, A = HeadPos.U() });
        }

        void UpdateCaptured(bool input, float dt)
        {
            DropEverything();
            Col.Disabled = true;
            Crouching = Sprinting = Gliding = false;
            var target = ForcedPos;
            GlobalPosition = GlobalPosition.DistanceTo(target) > 6f ? target : GMath.Damp(GlobalPosition, target, 14f, dt);
            Velocity = Vector3.Zero;
            Grounded = false;
            Prompt = Status == PlayerStatus.Trapped ? Loc.T("struggle") : Loc.T("carriedStruggle");
            Struggling = input && Keys.Held(Key.F);
            if (input && Keys.Pressed(Key.F) && Clock.Now - struggleSend > 0.06f)
            {
                struggleSend = Clock.Now;
                S?.SendAction(new ActionMsg { Type = ActionType.Struggle });
                Sfx.I?.Play(SoundId.Struggle, HeadPos, 0.6f, GMath.Rand(0.9f, 1.3f));
            }
        }

        void UpdateGhost(bool input, float dt)
        {
            DropEverything();
            Col.Disabled = true;
            Prompt = Status == PlayerStatus.Dead ? (S != null && S.IsSolo ? Loc.T("deadSolo") : Loc.T("dead")) : Loc.T("wentHome");
            if (!input) return;
            float x = (Keys.Held(Key.D) ? 1 : 0) - (Keys.Held(Key.A) ? 1 : 0);
            float y = (Keys.Held(Key.Space) ? 1 : 0) - (Keys.Held(Key.Ctrl) ? 1 : 0);
            float z = (Keys.Held(Key.W) ? 1 : 0) - (Keys.Held(Key.S) ? 1 : 0);
            var look = LookDir;
            var right = Right;
            ghostPos += (look * z + right * x + Vector3.Up * y) * (Keys.Held(Key.Shift) ? 14f : 7f) * dt;
            if (W != null)
            {
                var b = W.PlayArea;
                var mn = b.Position;
                var mx = b.End;
                ghostPos = new Vector3(Mathf.Clamp(ghostPos.X, mn.X, mx.X), Mathf.Clamp(ghostPos.Y, 0.3f, 30f), Mathf.Clamp(ghostPos.Z, mn.Z, mx.Z));
            }
        }

        void UpdateCamera()
        {
            if (camPivot == null) return;
            bool ghost = Status == PlayerStatus.Dead || Status == PlayerStatus.Home;
            var rot = GMath.YawBasis(Yaw) * new Basis(Vector3.Right, Pitch);
            if (ghost)
            {
                if (ghostPos == Vector3.Zero) ghostPos = HeadPos + Vector3.Up * 2f;
                camPivot.GlobalTransform = new Transform3D(rot, ghostPos);
                Cam.Transform = Transform3D.Identity;
                return;
            }
            ghostPos = Vector3.Zero;
            float eye = Crouching ? GameConsts.GnomeEyeCrouch : GameConsts.GnomeEye;
            var eyePos = GlobalPosition + Vector3.Up * eye;
            camPivot.GlobalTransform = new Transform3D(rot, eyePos);
            if (ThirdPerson || Status != PlayerStatus.Free)
            {
                var back = rot * new Vector3(0.35f, 0.35f, 2.6f);
                float dist = back.Length();
                if (Phys.SphereCast(eyePos, 0.15f, back, dist, Layers.Solid, out var hit)) dist = Mathf.Max(0.3f, hit.Distance - 0.05f);
                Cam.GlobalTransform = new Transform3D(rot, eyePos + back.Normalized() * dist);
                if (Avatar.FirstPerson) Avatar.SetFirstPerson(false);
            }
            else
            {
                Cam.Transform = new Transform3D(Basis.Identity, new Vector3(0, 0, -0.12f));
                if (!Avatar.FirstPerson) Avatar.SetFirstPerson(true);
            }
        }

        // ------------------------------------------------------------------ physics

        public override void _PhysicsProcess(double delta)
        {
            if (Status != PlayerStatus.Free) return;
            float dt = (float)delta;
            if (!wantCrouch && Crouching)
            {
                if (!Phys.CheckSphere(GlobalPosition + Vector3.Up * (GameConsts.GnomeHeight - GameConsts.GnomeRadius + 0.05f), GameConsts.GnomeRadius * 0.9f, Layers.Solid)) Crouching = false;
            }
            else if (wantCrouch) Crouching = true;
            SetCrouchCollider(Crouching);

            var v = Velocity;
            var app = GameApp.I;
            bool input = app != null && app.GameplayInput && !Stunned;
            float ix = input ? (Keys.Held(Key.D) ? 1 : 0) - (Keys.Held(Key.A) ? 1 : 0) : 0;
            float iz = input ? (Keys.Held(Key.W) ? 1 : 0) - (Keys.Held(Key.S) ? 1 : 0) : 0;
            var wish = Right * ix + Forward * iz;
            if (wish.LengthSquared() > 1) wish = wish.Normalized();
            float speed = Crouching ? GameConsts.CrouchSpeed : Sprinting ? GameConsts.SprintSpeed : GameConsts.WalkSpeed;
            if (mode == ArmMode.HoldProp)
            {
                var p = W?.GetProp(heldProp);
                if (p != null && p.Def.Mass > 1.2f) speed *= Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(1.2f, 6f, p.Def.Mass / gear.StrengthMul));
            }
            var targetH = wish * speed;
            var h = v.Flat();
            if (mode == ArmMode.Climb && !Grounded) v += wish * 16f / GameConsts.GnomeMass * dt; // swing
            else if (Grounded)
            {
                h = GMath.MoveTowards(h, targetH, GameConsts.GroundAccel * dt);
                v = new Vector3(h.X, v.Y, h.Z);
            }
            else if (targetH.LengthSquared() > 0.01f)
            {
                var dv = GMath.ClampLength(targetH - h, GameConsts.AirAccel * (Gliding ? 1.6f : 1f) * dt);
                h += dv;
                v = new Vector3(h.X, v.Y, h.Z);
            }
            v.Y -= GrabPhysics.Gravity * dt;
            // jump
            if (Clock.Now < jumpBufferedUntil && Clock.Now - lastGroundedTime < GameConsts.CoyoteTime && mode != ArmMode.Climb)
            {
                v.Y = gear.JumpSpeed * (Crouching ? 0.8f : 1f);
                jumpBufferedUntil = 0;
                lastGroundedTime = -10;
                Sfx.I?.Play(SoundId.Jump, GlobalPosition, 0.35f);
            }
            // parachute hat: hold jump while falling
            Gliding = gear.Parachute && input && !Grounded && mode != ArmMode.Climb && Keys.Held(Key.Space) && v.Y < 0;
            if (Gliding) v.Y = Mathf.Max(v.Y, -GameConsts.GlideFallSpeed);

            if (mode == ArmMode.Climb) v += ClimbForce(v) / GameConsts.GnomeMass * dt;
            else if (mode == ArmMode.HoldProp) v += CarryReaction(dt) * 0.8f / GameConsts.GnomeMass * dt;
            else if (mode == ArmMode.YarnProp) v += YarnPropReaction(dt) / GameConsts.GnomeMass * dt;

            Velocity = v;
            MoveAndSlide();
            PushProps();
            bool wasGrounded = Grounded;
            Grounded = IsOnFloor();
            if (Grounded)
            {
                lastGroundedTime = Clock.Now;
                if (!wasGrounded && lastVy < -9f) Sfx.I?.Play(SoundId.Land, GlobalPosition, Mathf.Clamp(-lastVy / 16f, 0f, 1f));
            }
            lastVy = Velocity.Y;

            if (GlobalPosition.Y < -25f && W != null)
            {
                GlobalPosition = W.SpawnPoint(PlayerId);
                Velocity = Vector3.Zero;
            }
        }

        /// <summary>Walking into a loose item nudges it (host applies it directly; clients ask the host via a kick-less push).</summary>
        void PushProps()
        {
            if (S == null || !S.IsHost) return;
            for (int i = 0; i < GetSlideCollisionCount(); i++)
            {
                var c = GetSlideCollision(i);
                if (c.GetCollider() is Prop p && p.Authority && !p.Removed)
                {
                    var push = -c.GetNormal();
                    push.Y = Mathf.Max(0, push.Y);
                    p.ApplyImpulse(push * Mathf.Min(3f, Velocity.Flat().Length() * 0.3f) * GameConsts.GnomeMass * 0.1f, c.GetPosition() - p.GlobalPosition);
                }
            }
        }

        public Vector3 HandPos => GlobalPosition + Vector3.Up * GameConsts.GnomeShoulder * (Crouching ? 0.75f : 1f) + YawBasis * new Vector3(0.18f, 0, -0.1f);

        Vector3 HookWorld => hookParent != null && IsInstanceValid(hookParent) ? hookParent.ToGlobal(hookLocal) : hookLocal;

        Vector3 ClimbForce(Vector3 vel)
        {
            if (hookParent == null && hookLocal == Vector3.Zero)
            {
                DetachYarn();
                return Vector3.Zero;
            }
            var anchor = HookWorld;
            if (anchor.DistanceTo(HandPos) > gear.YarnRange + 4f)
            {
                SnapYarn();
                return Vector3.Zero;
            }
            var f = GrabPhysics.YarnTension(HandPos, vel, anchor, yarnLen, GameConsts.ClimbStrength * gear.StrengthMul);
            if (!Grounded) f += -vel * 1.1f;
            return f;
        }

        Vector3 CarryReaction(float dt)
        {
            var p = W?.GetProp(heldProp);
            if (p == null)
            {
                DropEverything();
                return Vector3.Zero;
            }
            var anchor = p.ToGlobal(grabLocal);
            var target = CarryTarget(p);
            var pv = p.Authority ? p.LinearVelocity + p.AngularVelocity.Cross(anchor - p.GlobalPosition) : Vector3.Zero;
            var f = GrabPhysics.HoldForce(anchor, pv, target, p.Def.Mass, GameConsts.HoldStrength * gear.StrengthMul);
            var r = -f;
            r.Y = Mathf.Min(r.Y, 0) * 0.5f;
            reaction = GMath.Damp(reaction, r * Mathf.Clamp(p.Def.Mass / (p.Def.Mass + GameConsts.GnomeMass), 0f, 1f), 10f, dt);
            if (Grounded) reaction.Y = Mathf.Min(0, reaction.Y);
            if (p.Authority && S != null && S.IsHost) S.Host?.ApplyHold(PlayerId, p, anchor, target);
            return reaction;
        }

        Vector3 YarnPropReaction(float dt)
        {
            var p = W?.GetProp(heldProp);
            if (p == null)
            {
                DetachYarn();
                return Vector3.Zero;
            }
            var anchor = p.ToGlobal(grabLocal);
            var pv = p.Authority ? p.LinearVelocity : Vector3.Zero;
            var onProp = GrabPhysics.YarnTension(anchor, pv, HandPos, yarnLen, GameConsts.YarnPullStrength * gear.StrengthMul);
            float share = Mathf.Clamp(p.Def.Mass / (p.Def.Mass + GameConsts.GnomeMass), 0f, 1f);
            reaction = GMath.Damp(reaction, -onProp * share, 10f, dt);
            if (p.Authority && S != null && S.IsHost) S.Host?.ApplyYarnPull(PlayerId, p, anchor, HandPos, yarnLen);
            return reaction;
        }

        Vector3 CarryTarget(Prop p)
        {
            float extra = p != null ? Mathf.Clamp(p.HalfHeight, 0f, 1.2f) : 0f;
            var flatLook = GMath.LookDir(Yaw, Mathf.Clamp(Pitch, -0.6f, 0.9f));
            return GlobalPosition + Vector3.Up * (0.55f + extra * 0.5f) + flatLook * (holdDist + extra);
        }

        // ------------------------------------------------------------------ hands

        const uint HandMask = Layers.Grabbable;

        void TryPickUp()
        {
            if (!Phys.SphereCast(HeadPos, 0.12f, LookDir, GameConsts.HandReach + 0.3f, HandMask, out var hit)) return;
            var prop = Phys.Owner<Prop>(hit.Collider);
            if (prop == null || prop.Removed) return;
            mode = ArmMode.HoldProp;
            heldProp = prop.Id;
            grabLocal = prop.ToLocal(hit.Point);
            holdDist = GameConsts.HoldDistance;
            strainTime = 0;
            Sfx.I?.Play(prop.Def.Has(ItemFlags.Squeaky) ? SoundId.Squeak : SoundId.Grab, hit.Point, 0.5f);
            if (prop.Def.Mass / gear.StrengthMul > 3.8f) GameApp.I?.Toast(Loc.T("tooHeavy"));
        }

        void Drop()
        {
            if (mode == ArmMode.HoldProp) S?.SendAction(new ActionMsg { Type = ActionType.Release, Id = heldProp });
            mode = ArmMode.Idle;
            heldProp = 0;
            reaction = Vector3.Zero;
        }

        void DropEverything()
        {
            if (mode == ArmMode.HoldProp) Drop();
            else if (OnYarn) DetachYarn();
            mode = ArmMode.Idle;
        }

        void CheckCarry(float dt)
        {
            var p = W?.GetProp(heldProp);
            if (p == null || p.Removed)
            {
                Drop();
                return;
            }
            var anchor = p.ToGlobal(grabLocal);
            float err = anchor.DistanceTo(CarryTarget(p));
            float limit = p.Authority ? 1.8f : 2.6f;
            strainTime = err > limit ? strainTime + dt : Mathf.Max(0, strainTime - dt * 2f);
            if (strainTime > 1.2f || err > 5f)
            {
                Drop(); // it slipped out of the gnome's hands
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
            var vel = LookDir * power + Vector3.Up * 1.5f + Velocity * 0.6f;
            S?.SendAction(new ActionMsg { Type = ActionType.Throw, Id = heldProp, A = vel.U() });
            Sfx.I?.Play(SoundId.Throw, HeadPos, 0.5f);
            mode = ArmMode.Idle;
            heldProp = 0;
        }

        void Kick()
        {
            if (kickCooldown > 0) return;
            kickCooldown = 0.5f;
            if (mode == ArmMode.Idle) mode = ArmMode.Reach;
            kickEndAt = Clock.Now + 0.2f;
            var dir = GMath.LookDir(Yaw, Mathf.Clamp(Pitch, -0.5f, 0.6f));
            var from = GlobalPosition + Vector3.Up * 0.45f;
            Avatar.Kick();
            if (!Phys.SphereCast(from, 0.18f, dir, 1.5f, HandMask, out var hit))
            {
                Sfx.I?.Play(SoundId.Throw, from, 0.3f, 1.5f);
                return;
            }
            Sfx.I?.Play(SoundId.Punch, hit.Point, 0.8f);
            var msg = new ActionMsg { Type = ActionType.Punch, A = hit.Point.U(), B = dir.U() };
            var prop = Phys.Owner<Prop>(hit.Collider);
            var npc = Phys.Owner<NpcBase>(hit.Collider);
            var mech = Mechanism.FromCollider(hit.Collider);
            var furn = Phys.Owner<Furniture>(hit.Collider);
            if (prop != null) { msg.I = 0; msg.Id = prop.Id; }
            else if (npc != null) { msg.I = 1; msg.Id = npc.NpcId; }
            else if (mech != null) { msg.I = 2; msg.Id = mech.Index; }
            else if (furn != null) { msg.I = 3; msg.Id = furn.Index; }
            else return;
            S?.SendAction(msg);
        }

        // ------------------------------------------------------------------ yarn

        void ShootYarn()
        {
            if (Clock.Now < yarnCooldownUntil) return;
            float range = gear.YarnRange;
            var eye = HeadPos;
            Sfx.I?.Play(SoundId.Throw, eye, 0.4f, 1.3f);
            if (!Phys.SphereCast(eye, 0.05f, LookDir, range, HandMask, out var hit))
            {
                // miss: the hook flies out and comes back
                flyFrom = HandPos;
                flyTo = eye + LookDir * range;
                flyUntil = Clock.Now + 0.35f;
                mode = ArmMode.YarnFly;
                return;
            }
            var prop = Phys.Owner<Prop>(hit.Collider);
            yarnLen = Mathf.Max(GameConsts.YarnMin, HandPos.DistanceTo(hit.Point));
            if (prop != null && !prop.Removed)
            {
                mode = ArmMode.YarnProp;
                heldProp = prop.Id;
                grabLocal = prop.ToLocal(hit.Point);
            }
            else
            {
                mode = ArmMode.Climb;
                var npc = Phys.Owner<NpcBase>(hit.Collider);
                bool moving = Mechanism.FromCollider(hit.Collider) != null || npc != null || Phys.Owner<GnomeBody>(hit.Collider) != null;
                hookParent = moving ? hit.Collider as Node3D : null;
                hookLocal = moving && hookParent != null ? hookParent.ToLocal(hit.Point) : hit.Point;
                if (npc != null) S?.SendAction(new ActionMsg { Type = ActionType.Punch, I = 1, Id = npc.NpcId, A = hit.Point.U() }); // grandpa feels it
            }
            strainTime = 0;
            Sfx.I?.Play(SoundId.Grab, hit.Point, 0.6f);
        }

        void DetachYarn()
        {
            if (mode == ArmMode.YarnProp) S?.SendAction(new ActionMsg { Type = ActionType.Release, Id = heldProp });
            mode = ArmMode.Idle;
            heldProp = 0;
            hookParent = null;
            hookLocal = Vector3.Zero;
            reaction = Vector3.Zero;
        }

        void SnapYarn()
        {
            DetachYarn();
            yarnCooldownUntil = Clock.Now + GameConsts.YarnSnapTime;
            Sfx.I?.Play(SoundId.ArmPop, HandPos, 0.9f);
            GameApp.I?.Toast(Loc.T("yarnSnapped"));
        }

        void CheckYarnProp(float dt)
        {
            var p = W?.GetProp(heldProp);
            if (p == null || p.Removed)
            {
                DetachYarn();
                return;
            }
            float over = p.ToGlobal(grabLocal).DistanceTo(HandPos) - yarnLen;
            strainTime = over > 2.5f ? strainTime + dt : Mathf.Max(0, strainTime - dt);
            if (strainTime > 2.5f || over > gear.YarnRange) SnapYarn();
        }

        void TieYarn()
        {
            if (!Phys.Raycast(HeadPos, LookDir, gear.YarnRange, HandMask, out var hit)) return;
            var target = Phys.Owner<Prop>(hit.Collider);
            var msg = new ActionMsg { Type = ActionType.Tie, B = hit.Point.U(), S = target != null ? target.Id.ToString() : "" };
            if (mode == ArmMode.YarnProp)
            {
                var p = W?.GetProp(heldProp);
                if (p == null || p == target) return;
                msg.Id = heldProp;
                msg.A = p.ToGlobal(grabLocal).U();
            }
            else
            {
                if (target == null) return; // world-to-world ties make no sense
                msg.Id = 0;
                msg.A = HookWorld.U();
            }
            S?.SendAction(msg);
            Sfx.I?.Play(SoundId.Grab, hit.Point, 0.7f, 0.8f);
            mode = ArmMode.Idle;
            heldProp = 0;
            hookParent = null;
            hookLocal = Vector3.Zero;
        }

        void YarnJump()
        {
            var anchor = HookWorld;
            var fwd = (anchor - GlobalPosition).Flat();
            if (fwd.LengthSquared() < 0.01f) fwd = Forward;
            fwd = fwd.Normalized();
            DetachYarn();
            var v = Velocity;
            var probe = anchor + fwd * 0.45f + Vector3.Up * 1.8f;
            if (Phys.Raycast(probe, Vector3.Down, 2.6f, Layers.Solid | Layers.Prop, out var top) && top.Normal.Y > 0.6f && top.Point.Y > GlobalPosition.Y + 0.2f)
            {
                float h = top.Point.Y + 0.25f - GlobalPosition.Y;
                float vy = Mathf.Sqrt(2f * GrabPhysics.Gravity * Mathf.Max(0.1f, h)) + 0.4f;
                v = Vector3.Up * Mathf.Min(vy, 11f) + fwd * 3.2f;
            }
            else v = new Vector3(v.X, gear.JumpSpeed * 0.9f, v.Z) + fwd * 1.5f;
            Velocity = v;
            Sfx.I?.Play(SoundId.Jump, GlobalPosition, 0.4f);
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
            float best = Mathf.Cos(Mathf.DegToRad(28f));
            foreach (var m in W.Mechs)
            {
                if (m.HumanOnly || m.Role == "mousetrap") continue;
                var to = m.HandleWorld - eye;
                float d = to.Length();
                if (d > GameConsts.MechReach || d < 0.01f) continue;
                float c = (to / d).Dot(dir);
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
            if (Phys.SphereCast(eye, 0.06f, dir, gear.YarnRange, HandMask, out var hit))
            {
                var p = Phys.Owner<Prop>(hit.Collider);
                if (p != null && hit.Distance < GameConsts.InteractRange)
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
            if (W.Kind == LevelKind.House && GlobalPosition.DistanceTo(W.GoHomePoint) < 3.5f)
            {
                nearGoHome = true;
                Prompt = Loc.T("goHome");
            }
            if (W.Kind == LevelKind.Hub)
            {
                if (W.CraftBench != null && GlobalPosition.DistanceTo(W.CraftBench.GlobalPosition) < 4.5f)
                {
                    nearCraft = true;
                    Prompt = "E — " + Loc.T("craftBench");
                }
                else if (W.HighGnome != null && GlobalPosition.DistanceTo(W.HighGnome.GlobalPosition) < 6f)
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
                case "trap": return m.Furn != null && m.Furn.Kind == "stove" ? Loc.T("mechOven") : Loc.T("mechFreezer");
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

        /// <summary>Current arms in network (Unity) space.</summary>
        ArmState CurrentArms()
        {
            var a = new ArmState { Mode = mode };
            switch (mode)
            {
                case ArmMode.HoldProp:
                    a.PropId = heldProp;
                    a.Anchor = grabLocal.U();
                    a.Hand = CarryTarget(W?.GetProp(heldProp)).U();
                    break;
                case ArmMode.Climb:
                    a.Anchor = HookWorld.U();
                    a.Hand = new V3(yarnLen, 0, 0);
                    break;
                case ArmMode.YarnProp:
                    a.PropId = heldProp;
                    a.Anchor = grabLocal.U();
                    a.Hand = new V3(yarnLen, 0, 0);
                    break;
                case ArmMode.YarnFly:
                    float k = 1f - Mathf.Clamp((flyUntil - Clock.Now) / 0.35f, 0f, 1f);
                    float out01 = k < 0.5f ? k * 2f : (1f - k) * 2f;
                    a.Anchor = flyFrom.U();
                    a.Hand = HandPos.Lerp(flyTo, out01).U();
                    break;
                case ArmMode.Reach:
                    a.Hand = (HeadPos + LookDir * 0.9f).U();
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
                ghostPos = HeadPos + Vector3.Up * 2.5f;
            }
            if (s == PlayerStatus.Free && old != PlayerStatus.Free)
            {
                Col.Disabled = false;
                Velocity = Vector3.Zero;
            }
        }

        public void Teleport(Vector3 pos, float yaw)
        {
            Col.Disabled = false;
            GlobalPosition = pos;
            Velocity = Vector3.Zero;
            Yaw = yaw;
            Pitch = 0;
        }
    }
}
