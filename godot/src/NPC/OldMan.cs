using System.Collections.Generic;
using Gnomes.Core;
using Gnomes.Core.Protocol;
using Godot;
using SockGang.Audio;
using SockGang.Players;
using SockGang.Rendering;
using SockGang.Session;
using SockGang.World;

namespace SockGang.NPC
{
    /// <summary>
    /// Grandpa. Sleeps, wakes up from noise, patrols with a torch, undoes pranks, chases gnomes,
    /// pickles them in jars and - when he's had enough - uses the fly swatter.
    /// </summary>
    public partial class OldMan : NpcBase
    {
        public enum St : byte { Sleep, WakeUp, Patrol, Investigate, Chase, Grab, Carry, Jarring, Swat, Doze, Fix, GoBed, Search }

        ModelInstance model;
        Node3D hips, spine, neck, head, upArmL, upArmR, foreArmL, foreArmR, handL, handR, thighL, thighR, shinL, shinR;
        readonly Dictionary<Node3D, Quaternion> rest = new Dictionary<Node3D, Quaternion>();
        Vector3 hipsRestPos;
        NavAgent agent;
        CollisionShape3D col;
        SpotLight3D torch;
        Node3D swatter;
        Furniture bed;
        GameSession S => GameSession.I;
        HostLogic H => S?.Host;

        float wake;
        float stateTime;
        byte targetId = 255;
        Vector3 lastSeen;
        float lastSeenTime;
        readonly Dictionary<byte, float> detect = new Dictionary<byte, float>();
        byte carried = 255;
        int anger;
        Vector3 investigatePos;
        float investigateScore;
        float nextThink, nextSnore, nextGrumble;
        float patrolTime;
        Mechanism fixTarget;
        Furniture fixLamp;
        bool naturalWakeDone;
        Vector3 lastPosClient, velClient;
        float walkPhase;
        float blockedTime;

        public St Mode => (St)State;
        public Vector3 HandPos => handR != null ? handR.GlobalPosition + handR.GlobalBasis.Orthonormalized() * new Vector3(0, -0.5f, -0.1f) : GlobalPosition + Vector3.Up * 4f;
        public Vector3 Eye => head != null ? head.GlobalPosition + Vector3.Up * 0.2f : GlobalPosition + Vector3.Up * GameConsts.OldManEye;
        public bool Asleep => Mode == St.Sleep || Mode == St.Doze;
        Vector3 Fwd => GMath.YawForward(BodyYaw);

        public static OldMan Spawn(GameWorld w, bool authority)
        {
            var om = new OldMan { Name = "OldMan", NpcId = OldManId, Authority = authority, SyncToPhysics = false, CollisionLayer = Layers.Npc, CollisionMask = 0 };
            w.NpcRoot.AddChild(om);
            om.model = ModelLibrary.Instantiate("oldMan", om, ColliderMode.None);
            om.Bind();
            om.bed = w.FindFurniture("bed");
            om.col = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.95f, Height = GameConsts.OldManHeight }, Position = new Vector3(0, GameConsts.OldManHeight / 2, 0) };
            om.AddChild(om.col);
            om.BuildProps();
            if (authority)
            {
                om.agent = new NavAgent(w.GetWorld3D().NavigationMap) { Speed = GameConsts.OldManWalk, Acceleration = 22f, StoppingDistance = 0.6f, Enabled = false };
            }
            om.EnterSleep(true);
            w.OldMan = om;
            return om;
        }

        void Bind()
        {
            hips = model.Node("hips");
            spine = model.Node("spine");
            neck = model.Node("neck");
            head = model.Node("head");
            upArmL = model.Node("upperArmL");
            upArmR = model.Node("upperArmR");
            foreArmL = model.Node("foreArmL");
            foreArmR = model.Node("foreArmR");
            handL = model.Node("handL");
            handR = model.Node("handR");
            thighL = model.Node("thighL");
            thighR = model.Node("thighR");
            shinL = model.Node("shinL");
            shinR = model.Node("shinR");
            foreach (var t in new[] { hips, spine, neck, head, upArmL, upArmR, foreArmL, foreArmR, thighL, thighR, shinL, shinR })
                if (t != null) rest[t] = t.Quaternion;
            if (hips != null) hipsRestPos = hips.Position;
        }

        void BuildProps()
        {
            // torch in the left hand
            var tgo = new Node3D { Name = "Torch" };
            (handL ?? (Node3D)this).AddChild(tgo);
            tgo.Position = new Vector3(0, -0.45f, -0.1f);
            tgo.Quaternion = Conv.UEuler(80, 0, 0);
            torch = new SpotLight3D { SpotAngle = 24f, SpotRange = 34f, LightEnergy = 4f, LightColor = new Color(1f, 0.93f, 0.75f), ShadowEnabled = true, SpotAngleAttenuation = 0.6f };
            tgo.AddChild(torch);
            var body = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.5f, RadialSegments = 8 }, MaterialOverride = ModelLibrary.TintMaterial(Conv.C8(60, 60, 70)), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) };
            tgo.AddChild(body);
            // fly swatter in the right hand (shown when he's angry)
            swatter = new Node3D { Name = "Swatter", Position = new Vector3(0, -0.4f, -0.1f) };
            (handR ?? (Node3D)this).AddChild(swatter);
            swatter.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 2.2f, RadialSegments = 6 }, MaterialOverride = ModelLibrary.TintMaterial(Conv.C8(230, 200, 60)), Position = new Vector3(0, -1f, 0) });
            swatter.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.9f, 1.0f, 0.06f) }, MaterialOverride = ModelLibrary.TintMaterial(Conv.C8(210, 50, 50)), Position = new Vector3(0, -2.5f, 0) });
            swatter.Visible = false;
        }

        // ================================================================== host AI

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            stateTime += dt;
            if (Authority) Think(dt);
            else
            {
                velClient = (GlobalPosition - lastPosClient) / Mathf.Max(dt, 1e-4f);
                lastPosClient = GlobalPosition;
            }
            Animate(dt);
            if (Asleep && Clock.Now > nextSnore)
            {
                nextSnore = Clock.Now + 3.6f;
                Sfx.I?.Play(SoundId.Snore, Eye, 0.7f);
                Fx.Sparkles(W, Eye + Vector3.Up * 0.5f, new Color(0.78f, 0.86f, 1f), 2, 0.6f);
            }
            if (torch != null) torch.Visible = !Asleep && Mode != St.WakeUp;
            if (swatter != null) swatter.Visible = Mode == St.Swat || (Alert > 0.8f && !Asleep && Mode != St.Carry);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!Authority || agent == null || !agent.Enabled) return;
            float dt = (float)delta;
            var next = agent.Step(dt);
            var v = agent.Velocity.Flat();
            float yaw = BodyYaw;
            if (v.LengthSquared() > 0.05f) yaw = Mathf.LerpAngle(yaw, GMath.YawOf(v), 0.2f);
            else if (Look != Vector3.Zero && (Mode == St.Chase || Mode == St.Grab || Mode == St.Swat || Mode == St.Search))
            {
                var to = (Look - GlobalPosition).Flat();
                if (to.LengthSquared() > 0.01f) yaw = Mathf.LerpAngle(yaw, GMath.YawOf(to), 0.15f);
            }
            MoveBody(next, yaw);
        }

        void SetState(St s)
        {
            State = (byte)s;
            stateTime = 0;
        }

        float SpeedMul => 1f + Mathf.Min(0.35f, (Mathf.Max(1, S != null ? S.Night : 1) - 1) * 0.06f);
        float HearMul => W?.Night != null && W.Night.BankedByKind.ContainsKey("hearingAid") ? 0.5f : 1f;
        float ViewMul => W?.Night != null && W.Night.BankedByKind.ContainsKey("glasses") ? 0.45f : 1f;

        Transform3D SleepTransform()
        {
            // lying on his back on the bed: face up, head towards the headboard
            var fwd = -bed.GlobalBasis.Z.Normalized();
            var anchor = bed.AnchorPos("sleep", bed.GlobalPosition + Vector3.Up * 2.4f);
            var y = -fwd;
            var z = -Vector3.Up; // model front (-Z) faces the ceiling
            var x = y.Cross(z).Normalized();
            return new Transform3D(new Basis(x, y, z), anchor + fwd * 3.3f + Vector3.Up * 0.35f);
        }

        void EnterSleep(bool instant)
        {
            SetState(St.Sleep);
            wake = 0;
            if (agent != null) agent.Enabled = false;
            col.Disabled = true;
            if (bed == null) return;
            GlobalTransform = SleepTransform();
        }

        void StandUp(Vector3 near)
        {
            if (agent != null && agent.Sample(near, 6f, out var hit))
            {
                var bf = bed != null ? (-bed.GlobalBasis.Z).Flat() : Vector3.Forward;
                MoveBody(hit, GMath.YawOf(bf.LengthSquared() > 0.001f ? bf : Vector3.Forward));
                agent.Enabled = true;
                agent.Warp(hit);
            }
            col.Disabled = false;
        }

        void Go(Vector3 dest, float speed)
        {
            if (agent == null || !agent.Enabled) return;
            agent.Speed = speed * SpeedMul;
            if (agent.Sample(dest, 5f, out var hit)) agent.SetDestination(hit);
        }

        bool Arrived(float slack = 1.2f) => agent != null && agent.Enabled && !agent.PathPending && agent.RemainingDistance <= agent.StoppingDistance + slack;

        void Stop()
        {
            if (agent != null && agent.Enabled) agent.ResetPath();
        }

        void Think(float dt)
        {
            var w = W;
            if (w == null || H == null || w.Night == null) return;
            Alert = Mathf.Max(0, Alert - dt * 0.02f);
            var keys = new List<byte>(detect.Keys);
            foreach (var k in keys) detect[k] = Mathf.Max(0, detect[k] - dt * 0.35f);

            switch (Mode)
            {
                case St.Sleep:
                    wake = Mathf.Max(0, wake - dt * 0.03f);
                    if (!naturalWakeDone && w.Night.Progress01 > 0.3f && GMath.Rand01 < dt * 0.01f)
                    {
                        naturalWakeDone = true;
                        wake = 1f; // needs the toilet
                    }
                    if (wake >= 1f)
                    {
                        SetState(St.WakeUp);
                        w.EmitSound(SoundId.Grumble, Eye, 1f);
                    }
                    return;
                case St.WakeUp:
                    if (stateTime > 1.6f)
                    {
                        StandUp(w.Layout.Spots["bed"].G());
                        Alert = Mathf.Max(Alert, 0.4f);
                        SetState(investigateScore > 0 ? St.Investigate : St.Patrol);
                    }
                    return;
                case St.Doze:
                    if (stateTime > 25f)
                    {
                        col.Disabled = false;
                        if (agent != null)
                        {
                            agent.Enabled = true; // was switched off when he nodded off
                            if (agent.Sample(GlobalPosition, 4f, out var hit)) agent.Warp(hit);
                        }
                        SetState(St.Search);
                        Look = GlobalPosition + Fwd * 5f;
                    }
                    return;
            }

            // --- awake: look for gnomes ---
            if (Clock.Now > nextThink)
            {
                nextThink = Clock.Now + 0.15f;
                Scan(0.15f);
            }

            switch (Mode)
            {
                case St.Patrol:
                    patrolTime += dt;
                    if (fixTarget == null) fixTarget = FindPrankToFix();
                    if (fixTarget == null && fixLamp == null) fixLamp = FindDarkLamp();
                    if (fixTarget != null || fixLamp != null)
                    {
                        SetState(St.Fix);
                        break;
                    }
                    if (Arrived() || stateTime < 0.1f)
                    {
                        if (patrolTime > 60f && Alert < 0.2f)
                        {
                            SetState(St.GoBed);
                            Go(w.Layout.Spots["bed"].G(), GameConsts.OldManWalk);
                            break;
                        }
                        var spots = new List<V3>(w.Layout.Spots.Values);
                        Go(spots[GMath.RandInt(0, spots.Count)].G(), GameConsts.OldManWalk);
                    }
                    break;
                case St.Investigate:
                    Go(investigatePos, GameConsts.OldManSearch);
                    Look = investigatePos;
                    if (Arrived(2f) || stateTime > 12f)
                    {
                        investigateScore = 0;
                        SetState(St.Search);
                        Look = investigatePos;
                    }
                    break;
                case St.Search:
                    Stop();
                    Look = GlobalPosition + new Basis(Vector3.Up, Mathf.DegToRad(Mathf.Sin(stateTime * 1.5f) * 70f)) * Fwd * 6f;
                    if (stateTime > 4f)
                    {
                        SetState(St.Patrol);
                        patrolTime = 0;
                    }
                    break;
                case St.Chase:
                    Chase(dt);
                    break;
                case St.Grab:
                    Stop();
                    if (stateTime > 0.55f)
                    {
                        var g = Target();
                        if (g != null && H.CanBeCaught(g) && InReach(g, GameConsts.OldManReach + 0.6f))
                        {
                            H.Catch(g.PlayerId);
                            carried = g.PlayerId;
                            Anim = (byte)(carried + 1);
                            SetState(St.Carry);
                            w.EmitSound(SoundId.Shout, Eye, 1f);
                        }
                        else SetState(St.Chase);
                    }
                    break;
                case St.Carry:
                    Carry();
                    break;
                case St.Jarring:
                    Stop();
                    if (stateTime > 1.2f)
                    {
                        int jar = H.FreeJar();
                        if (carried != 255)
                        {
                            if (jar >= 0) H.PutInJar(carried, jar);
                            else H.Flatten(carried); // no jars left: SPLAT
                        }
                        carried = 255;
                        Anim = 0;
                        anger++;
                        Alert = 1f;
                        w.EmitSound(SoundId.Grumble, Eye, 1f);
                        SetState(St.Search);
                    }
                    break;
                case St.Swat:
                    Stop();
                    if (stateTime > 0.65f)
                    {
                        var g = Target();
                        if (g != null && g.Status == PlayerStatus.Free && InReach(g, GameConsts.SwatterReach))
                        {
                            H.Flatten(g.PlayerId);
                            w.Noise(g.GlobalPosition, 20f, NoiseKind.Impact);
                        }
                        else w.EmitSound(SoundId.Punch, HandPos, 1f);
                        targetId = 255;
                        SetState(St.Search);
                    }
                    break;
                case St.Fix:
                    FixPrank();
                    break;
                case St.GoBed:
                    if (Arrived(2f))
                    {
                        EnterSleep(false);
                        wake = 0.35f; // lighter sleep after being up
                    }
                    break;
            }
        }

        GnomeBody Target()
        {
            if (targetId == 255 || W == null) return null;
            return W.Gnomes.TryGetValue(targetId, out var g) ? g : null;
        }

        bool InReach(GnomeBody g, float reach)
        {
            var d = g.GlobalPosition - GlobalPosition;
            return d.Flat().Length() <= reach && g.GlobalPosition.Y <= GameConsts.OldManGrabMaxHeight;
        }

        /// <summary>Is this point inside the beam of his torch (and the torch is on)?</summary>
        public bool InTorch(Vector3 p)
        {
            if (torch == null || !torch.Visible) return false;
            var to = p - torch.GlobalPosition;
            var dir = -torch.GlobalBasis.Z.Normalized();
            return to.Length() < torch.SpotRange && Mathf.RadToDeg(dir.AngleTo(to)) < torch.SpotAngle;
        }

        void Scan(float dt)
        {
            var w = W;
            float viewDist = GameConsts.OldManViewDist * ViewMul;
            float cosFov = Mathf.Cos(Mathf.DegToRad(GameConsts.OldManFovDeg * 0.5f));
            var eye = Eye;
            var fwd = Fwd;
            foreach (var g in w.Gnomes.Values)
            {
                if (g.Status != PlayerStatus.Free) continue;
                var to = g.Center - eye;
                float dist = to.Length();
                if (dist > viewDist || dist < 1e-3f) continue;
                var dir = to / dist;
                var flatDir = dir.Flat();
                bool inCone = (flatDir.LengthSquared() > 1e-6f && flatDir.Normalized().Dot(fwd) > cosFov) || dist < 3.5f;
                if (!inCone) continue;
                if (Phys.Raycast(eye, dir, dist - 0.4f, Layers.Solid))
                {
                    var toHead = g.HeadPos - eye;
                    if (Phys.Raycast(eye, toHead, toHead.Length() - 0.3f, Layers.Solid)) continue;
                }
                bool inTorch = InTorch(g.Center);
                float vis = (1f - dist / viewDist) * (g.Crouching ? 0.45f : 1f) * (g.Velocity.LengthSquared() > 1f ? 1.4f : 0.8f) * (inTorch ? 2f : 0.8f);
                if (!inTorch && !w.IsLit(g.Center)) vis *= 0.55f; // dark rooms are the gang's friend
                if (H.IsHidden(g)) vis *= 0.15f;
                detect.TryGetValue(g.PlayerId, out float d0);
                float d1 = Mathf.Min(1.5f, d0 + vis * dt * 6f);
                detect[g.PlayerId] = d1;
                if (d1 >= 1f && Mode != St.Carry && Mode != St.Jarring && Mode != St.Swat && Mode != St.Grab)
                {
                    if (Mode != St.Chase)
                    {
                        W.EmitSound(SoundId.Shout, eye, 1f);
                        W.Noise(eye, 20f, NoiseKind.Voice);
                    }
                    targetId = g.PlayerId;
                    lastSeen = g.GlobalPosition;
                    lastSeenTime = Clock.Now;
                    Alert = 1f;
                    if (Mode != St.Chase) SetState(St.Chase);
                }
                if (targetId == g.PlayerId && d1 > 0.5f)
                {
                    lastSeen = g.GlobalPosition;
                    lastSeenTime = Clock.Now;
                }
            }
        }

        void Chase(float dt)
        {
            var g = Target();
            if (g == null || g.Status != PlayerStatus.Free)
            {
                targetId = 255;
                SetState(St.Search);
                return;
            }
            Look = g.Center;
            bool seen = Clock.Now - lastSeenTime < 1.6f;
            if (!seen)
            {
                Go(lastSeen, GameConsts.OldManRun);
                if (Arrived(1.5f) || Clock.Now - lastSeenTime > 6f)
                {
                    targetId = 255;
                    SetState(St.Search);
                }
                return;
            }
            Go(g.GlobalPosition, GameConsts.OldManRun);
            float flat = (g.GlobalPosition - GlobalPosition).Flat().Length();
            if (flat > GameConsts.OldManReach + 0.4f)
            {
                blockedTime = 0;
                return;
            }
            if (H.IsHidden(g) || g.GlobalPosition.Y > GameConsts.OldManGrabMaxHeight)
            {
                // under the bed or on top of the wardrobe: grumble and give up after a while
                blockedTime += dt;
                if (blockedTime > 3f)
                {
                    blockedTime = 0;
                    W.EmitSound(SoundId.Grumble, Eye, 1f);
                    anger++;
                    detect[g.PlayerId] = 0.3f;
                    targetId = 255;
                    SetState(St.Search);
                }
                return;
            }
            blockedTime = 0;
            if (anger >= 2 || H.FreeJar() < 0) SetState(St.Swat);
            else SetState(St.Grab);
        }

        void Carry()
        {
            var w = W;
            var shelf = w.FindFurniture("jarShelf");
            var slot = H.Slot(carried);
            if (slot == null || slot.Status != PlayerStatus.Carried)
            {
                carried = 255;
                Anim = 0;
                SetState(St.Search);
                return;
            }
            var dest = shelf != null ? shelf.AnchorPos("free", shelf.GlobalPosition) + (-shelf.GlobalBasis.Z.Normalized()) * 1.5f : GlobalPosition;
            Go(dest, GameConsts.OldManWalk * 1.2f);
            Look = shelf != null ? shelf.GlobalPosition : GlobalPosition + Fwd;
            if (Arrived(1.5f) || stateTime > 25f) SetState(St.Jarring);
        }

        /// <summary>A gnome wriggled out of his hand.</summary>
        public void DropCarried()
        {
            if (carried == 255) return;
            var id = carried;
            carried = 255;
            Anim = 0;
            H?.DropCarried(id, HandPos);
            anger++;
            W?.EmitSound(SoundId.Shout, Eye, 1f);
            targetId = id;
            SetState(St.Chase);
        }

        public void ForgetPlayer(byte id)
        {
            detect.Remove(id);
            if (targetId == id) targetId = 255;
            if (carried == id)
            {
                carried = 255;
                Anim = 0;
                SetState(St.Search);
            }
        }

        Mechanism FindPrankToFix()
        {
            foreach (var f in W.Furniture)
            {
                if (f.Kind == "tv" && f.TvOn) return f.Mechs[0];
                foreach (var m in f.Mechs)
                {
                    if ((m.Role == "faucet" || m.Role == "tubFaucet") && m.IsOpen) return m;
                    if (m.Role == "window" && m.IsOpen && GMath.Rand01 < 0.3f) return m;
                }
            }
            return null;
        }

        /// <summary>A lamp that is normally on but somebody switched off.</summary>
        Furniture FindDarkLamp()
        {
            foreach (var f in W.Furniture)
                if (f.IsLamp && !f.LightsOn && f.Placement != null && f.Placement.P("on", 1) > 0.5f) return f;
            return null;
        }

        void FixLamp()
        {
            if (fixLamp == null || fixLamp.LightsOn)
            {
                fixLamp = null;
                SetState(St.Patrol);
                return;
            }
            var at = fixLamp.GlobalPosition;
            Go(at, GameConsts.OldManWalk * 1.1f);
            Look = at + Vector3.Up * 3f;
            if ((GlobalPosition - at).Flat().Length() < 3.4f || stateTime > 20f)
            {
                fixLamp.SetLights(true);
                S?.Broadcast(new EventMsg { Type = EvType.Lamp, Id = fixLamp.Index, I = 1 });
                W.EmitSound(SoundId.Click, at + Vector3.Up * 3f, 0.8f);
                W.EmitSound(SoundId.Grumble, Eye, 1f);
                fixLamp = null;
                SetState(St.Search);
            }
        }

        void FixPrank()
        {
            if (fixTarget == null && fixLamp != null)
            {
                FixLamp();
                return;
            }
            if (fixTarget == null || !fixTarget.IsOpen)
            {
                fixTarget = null;
                SetState(St.Patrol);
                return;
            }
            var front = fixTarget.HandleWorld;
            Go(front, GameConsts.OldManWalk * 1.1f);
            Look = front;
            if ((GlobalPosition - front).Flat().Length() < 3.2f || stateTime > 20f)
            {
                W.HostSetMech(fixTarget, false);
                W.EmitSound(SoundId.Grumble, Eye, 1f);
                fixTarget = null;
                SetState(St.Search);
            }
        }

        // ================================================================== senses (host)

        public void Hear(Vector3 pos, float radius, NoiseKind kind)
        {
            if (!Authority) return;
            float r = radius * HearMul;
            float d = pos.DistanceTo(Eye);
            if (d > r) return;
            float loud = 1f - d / r;
            if (Mode == St.Sleep)
            {
                float sleepDepth = W?.Night != null ? Mathf.Lerp(0.55f, 1.2f, W.Night.Progress01) : 1f;
                float k = kind == NoiseKind.Break ? 0.9f : kind == NoiseKind.Clock || kind == NoiseKind.Voice ? 0.6f : kind == NoiseKind.Step ? 0.12f : 0.45f;
                wake += loud * k * sleepDepth;
                if (loud > 0.2f) Fx.Sparkles(W, Eye + Vector3.Up, new Color(1f, 0.94f, 0.6f), 1, 0.4f);
                if (loud > investigateScore)
                {
                    investigateScore = loud;
                    investigatePos = pos;
                }
                return;
            }
            if (Mode == St.Doze || Mode == St.WakeUp || Mode == St.Chase || Mode == St.Carry || Mode == St.Jarring || Mode == St.Grab || Mode == St.Swat) return;
            if (loud * (kind == NoiseKind.Step ? 0.5f : 1f) > 0.15f)
            {
                Alert = Mathf.Min(1f, Alert + loud * 0.5f);
                investigatePos = pos;
                investigateScore = loud;
                if (Mode != St.Investigate)
                {
                    SetState(St.Investigate);
                    if (Clock.Now > nextGrumble)
                    {
                        nextGrumble = Clock.Now + 4f;
                        W.EmitSound(SoundId.Grumble, Eye, 0.9f);
                    }
                }
            }
        }

        /// <summary>A gnome kicked or hooked him.</summary>
        public void OnPoked(byte pid, Vector3 point)
        {
            if (!Authority) return;
            if (Mode == St.Sleep) wake += 0.6f;
            else if (!Asleep)
            {
                detect[pid] = 1.2f;
                targetId = pid;
                lastSeen = point;
                lastSeenTime = Clock.Now;
                if (Mode == St.Patrol || Mode == St.Search || Mode == St.Investigate || Mode == St.Fix) SetState(St.Chase);
            }
        }

        public void SleepDust(Vector3 pos)
        {
            if (!Authority) return;
            if (pos.DistanceTo(GlobalPosition + Vector3.Up * 3f) > 8f || Mode == St.Sleep) return;
            if (carried != 255) DropCarried();
            Stop();
            if (agent != null) agent.Enabled = false;
            SetState(St.Doze);
            Alert = 0;
            W.EmitSound(SoundId.Snore, Eye, 1f);
        }

        /// <summary>Playtesting shortcuts (F8 / F9).</summary>
        public void DebugWake()
        {
            if (Authority && Mode == St.Sleep) wake = 2f; // > 1 so this frame's decay can't undo it
        }

        public void DebugSleep()
        {
            if (!Authority || Mode == St.Sleep) return;
            if (carried != 255) DropCarried();
            targetId = 255;
            EnterSleep(false);
        }

        // ================================================================== animation (everyone)

        void Animate(float dt)
        {
            var vel = Authority ? (agent != null && agent.Enabled ? agent.Velocity : Vector3.Zero) : velClient;
            float speed = vel.Flat().Length();
            walkPhase += speed * dt * 0.9f;
            bool lying = Mode == St.Sleep;
            bool sitting = Mode == St.Doze;
            float swing = Mathf.Clamp(speed / 4f, 0f, 1f) * (Mode == St.Chase ? 1.4f : 1f);
            float legA = lying ? 0 : Mathf.Sin(walkPhase) * 28f * swing;
            Rot(thighL, legA + (sitting ? -80f : 0), 0, 0);
            Rot(thighR, -legA + (sitting ? -80f : 0), 0, 0);
            Rot(shinL, Mathf.Max(0, -Mathf.Sin(walkPhase)) * 35f * swing + (sitting ? 70f : 0), 0, 0);
            Rot(shinR, Mathf.Max(0, Mathf.Sin(walkPhase)) * 35f * swing + (sitting ? 70f : 0), 0, 0);
            if (hips != null) hips.Position = hipsRestPos + Vector3.Up * (Mathf.Abs(Mathf.Sin(walkPhase)) * 0.12f * swing - (sitting ? 2.6f : 0f));
            float lean = Mode == St.Chase ? 12f : Mode == St.Search ? 0 : 4f * swing;
            float breathe = lying || sitting ? Mathf.Sin(Clock.Now * 1.7f) * 3f : 0f;
            Rot(spine, lean + breathe + (sitting ? 15f : 0), 0, 0);
            // head: look at the target / around, droop when dozing
            float headYaw = 0, headPitch = 0;
            if (Look != Vector3.Zero && !lying && !sitting)
            {
                var lg = ToLocal(Look);
                var local = new Vector3(lg.X, lg.Y, -lg.Z); // Unity-style local (z forward)
                headYaw = Mathf.Clamp(Mathf.RadToDeg(Mathf.Atan2(local.X, local.Z)), -60f, 60f);
                headPitch = Mathf.Clamp(-Mathf.RadToDeg(Mathf.Atan2(local.Y - 6f, new Vector2(local.X, local.Z).Length())), -30f, 45f);
            }
            if (sitting) headPitch = 35f;
            Rot(neck, headPitch * 0.4f, headYaw * 0.4f, 0);
            Rot(head, headPitch * 0.6f, headYaw * 0.6f, 0);
            // arms
            float armSwing = Mathf.Sin(walkPhase) * 25f * swing;
            float rArmX = -armSwing, lArmX = armSwing;
            float rFore = 0, lFore = -20f;
            switch (Mode)
            {
                case St.Grab:
                    rArmX = -95f;
                    rFore = -10f;
                    break;
                case St.Carry:
                case St.Jarring:
                    rArmX = -80f;
                    rFore = -40f;
                    break;
                case St.Swat:
                    float k = Mathf.Clamp(stateTime / 0.65f, 0f, 1f);
                    rArmX = k < 0.6f ? Mathf.Lerp(0, -170f, k / 0.6f) : Mathf.Lerp(-170f, -40f, (k - 0.6f) / 0.4f);
                    rFore = -20f;
                    break;
                case St.Chase:
                    rArmX = -60f + Mathf.Sin(Clock.Now * 12f) * 30f;
                    lArmX = -50f;
                    break;
                case St.Fix:
                    rArmX = -70f;
                    break;
                case St.Sleep:
                    rArmX = 0;
                    lArmX = 0;
                    break;
            }
            if (!lying && torch != null && torch.Visible) lArmX = Mathf.Min(lArmX, -55f); // hold the torch forward
            Rot(upArmR, rArmX, 0, 0);
            Rot(upArmL, lArmX, 0, 0);
            Rot(foreArmR, rFore, 0, 0);
            Rot(foreArmL, lFore, 0, 0);
        }

        void Rot(Node3D t, float x, float y, float z)
        {
            if (t == null || !rest.TryGetValue(t, out var r)) return;
            t.Quaternion = r * Conv.UEuler(x, y, z);
        }

        protected override void ApplyClientPose(Vector3 pos, float yaw)
        {
            if (Mode == St.Sleep && bed != null)
            {
                GlobalTransform = SleepTransform();
                col.Disabled = true;
                return;
            }
            col.Disabled = Mode == St.Doze;
            base.ApplyClientPose(pos, yaw);
        }
    }
}
