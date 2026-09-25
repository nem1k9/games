using System.Collections.Generic;
using Gnomes.Audio;
using Gnomes.Core;
using Gnomes.Core.Protocol;
using Gnomes.Players;
using Gnomes.Rendering;
using Gnomes.Session;
using Gnomes.World;
using UnityEngine;
using UnityEngine.AI;

namespace Gnomes.NPC
{
    /// <summary>
    /// Grandpa. Sleeps, wakes up from noise, patrols with a torch, undoes pranks, chases gnomes,
    /// pickles them in jars and - when he's had enough - uses the fly swatter.
    /// </summary>
    public class OldMan : NpcBase
    {
        public enum St : byte { Sleep, WakeUp, Patrol, Investigate, Chase, Grab, Carry, Jarring, Swat, Doze, Fix, GoBed, Search }

        ModelInstance model;
        Transform hips, spine, neck, head, upArmL, upArmR, foreArmL, foreArmR, handL, handR, thighL, thighR, shinL, shinR;
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        Vector3 hipsRestPos;
        NavMeshAgent agent;
        Rigidbody rb;
        CapsuleCollider col;
        Light torch;
        Transform swatter;
        Furniture bed;
        GameSession S => GameSession.I;
        HostLogic H => S?.Host;

        float wake; // sleeping: reaches 1 -> wakes up
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
        bool naturalWakeDone;
        Vector3 lastPosClient;
        Vector3 velClient;
        float walkPhase;

        public St Mode => (St)State;
        public Vector3 HandPos => handR ? handR.position + handR.rotation * new Vector3(0, -0.5f, 0.1f) : transform.position + Vector3.up * 4f;
        public Vector3 Eye => head ? head.position + Vector3.up * 0.2f : transform.position + Vector3.up * GameConsts.OldManEye;
        public bool Asleep => Mode == St.Sleep || Mode == St.Doze;

        public static OldMan Spawn(GameWorld w, bool authority)
        {
            var inst = ModelLibrary.Instantiate("oldMan", w.NpcRoot, Layers.NPC, withColliders: false);
            var om = inst.gameObject.AddComponent<OldMan>();
            om.NpcId = OldManId;
            om.Authority = authority;
            om.model = inst;
            om.Bind();
            om.bed = w.FindFurniture("bed");
            inst.SetLayerRecursive(Layers.NPC);
            om.rb = inst.gameObject.AddComponent<Rigidbody>();
            om.rb.isKinematic = true;
            om.rb.interpolation = RigidbodyInterpolation.Interpolate;
            om.col = inst.gameObject.AddComponent<CapsuleCollider>();
            om.col.radius = 0.95f;
            om.col.height = GameConsts.OldManHeight;
            om.col.center = new Vector3(0, GameConsts.OldManHeight / 2, 0);
            om.BuildProps();
            if (authority)
            {
                om.agent = inst.gameObject.AddComponent<NavMeshAgent>();
                om.agent.radius = GameConsts.OldManRadius;
                om.agent.height = GameConsts.OldManHeight;
                om.agent.speed = GameConsts.OldManWalk;
                om.agent.acceleration = 22f;
                om.agent.angularSpeed = 300f;
                om.agent.stoppingDistance = 0.6f;
                om.agent.updatePosition = false;
                om.agent.updateRotation = false;
                om.agent.autoRepath = true;
                om.agent.enabled = false;
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
                if (t) rest[t] = t.localRotation;
            if (hips) hipsRestPos = hips.localPosition;
        }

        void BuildProps()
        {
            // torch in the left hand
            var tgo = new GameObject("Torch");
            tgo.transform.SetParent(handL ? handL : transform, false);
            tgo.transform.localPosition = new Vector3(0, -0.45f, 0.1f);
            tgo.transform.localRotation = Quaternion.Euler(80, 0, 0);
            torch = tgo.AddComponent<Light>();
            torch.type = LightType.Spot;
            torch.spotAngle = 48f;
            torch.range = 34f;
            torch.intensity = 2.2f;
            torch.color = new Color(1f, 0.93f, 0.75f);
            torch.shadows = LightShadows.None;
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(tgo.transform, false);
            body.transform.localScale = new Vector3(0.18f, 0.25f, 0.18f);
            body.transform.localRotation = Quaternion.Euler(90, 0, 0);
            body.GetComponent<MeshRenderer>().sharedMaterial = ModelLibrary.TintMaterial(new Color32(60, 60, 70, 255));
            // fly swatter in the right hand (shown when he's angry)
            swatter = new GameObject("Swatter").transform;
            swatter.SetParent(handR ? handR : transform, false);
            swatter.localPosition = new Vector3(0, -0.4f, 0.1f);
            var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(stick.GetComponent<Collider>());
            stick.transform.SetParent(swatter, false);
            stick.transform.localScale = new Vector3(0.06f, 1.1f, 0.06f);
            stick.transform.localPosition = new Vector3(0, -1.0f, 0);
            stick.GetComponent<MeshRenderer>().sharedMaterial = ModelLibrary.TintMaterial(new Color32(230, 200, 60, 255));
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(pad.GetComponent<Collider>());
            pad.transform.SetParent(swatter, false);
            pad.transform.localScale = new Vector3(0.9f, 1.0f, 0.06f);
            pad.transform.localPosition = new Vector3(0, -2.5f, 0);
            pad.GetComponent<MeshRenderer>().sharedMaterial = ModelLibrary.TintMaterial(new Color32(210, 50, 50, 255));
            swatter.gameObject.SetActive(false);
        }

        // ================================================================== host AI

        void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            if (Authority) Think(dt);
            else
            {
                velClient = (transform.position - lastPosClient) / Mathf.Max(dt, 1e-4f);
                lastPosClient = transform.position;
            }
            Animate(dt);
            if (Asleep && Time.time > nextSnore)
            {
                nextSnore = Time.time + 3.6f;
                Sfx.I?.Play(SoundId.Snore, Eye, 0.7f);
                Fx.Sparkles(W, Eye + Vector3.up * 0.5f, new Color32(200, 220, 255, 255), 2, 0.6f);
            }
            if (torch) torch.enabled = !Asleep && Mode != St.WakeUp;
            if (swatter) swatter.gameObject.SetActive(Mode == St.Swat || (Alert > 0.8f && !Asleep && Mode != St.Carry));
        }

        void FixedUpdate()
        {
            if (!Authority || agent == null || !agent.enabled) return;
            var next = agent.nextPosition;
            rb.MovePosition(next);
            var v = agent.velocity.Flat();
            if (v.sqrMagnitude > 0.05f) rb.MoveRotation(Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(v), 0.2f));
            else if (Look != Vector3.zero && (Mode == St.Chase || Mode == St.Grab || Mode == St.Swat || Mode == St.Search))
            {
                var to = (Look - transform.position).Flat();
                if (to.sqrMagnitude > 0.01f) rb.MoveRotation(Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 0.15f));
            }
        }

        void SetState(St s)
        {
            State = (byte)s;
            stateTime = 0;
        }

        float SpeedMul => 1f + Mathf.Min(0.35f, (Mathf.Max(1, S != null ? S.Night : 1) - 1) * 0.06f);
        float HearMul => (W != null && W.Night != null && W.Night.BankedByKind.ContainsKey("hearingAid")) ? 0.5f : 1f;
        float ViewMul => (W != null && W.Night != null && W.Night.BankedByKind.ContainsKey("glasses")) ? 0.45f : 1f;

        void EnterSleep(bool instant)
        {
            SetState(St.Sleep);
            wake = 0;
            if (agent) agent.enabled = false;
            col.enabled = false;
            if (bed == null) return;
            var anchor = bed.AnchorPos("sleep", bed.transform.position + Vector3.up * 2.4f);
            var fwd = bed.transform.forward;
            transform.SetPositionAndRotation(anchor + fwd * 3.3f + Vector3.up * 0.35f, Quaternion.LookRotation(Vector3.up, -fwd));
            rb.position = transform.position;
            rb.rotation = transform.rotation;
        }

        void StandUp(Vector3 near)
        {
            if (NavMesh.SamplePosition(near, out var hit, 6f, NavMesh.AllAreas))
            {
                transform.SetPositionAndRotation(hit.position, Quaternion.LookRotation((bed != null ? bed.transform.forward : Vector3.forward).Flat().normalized + Vector3.forward * 0.001f));
                rb.position = transform.position;
                rb.rotation = transform.rotation;
                agent.enabled = true;
                agent.Warp(hit.position);
            }
            col.enabled = true;
        }

        void Go(Vector3 dest, float speed)
        {
            if (agent == null || !agent.enabled) return;
            agent.speed = speed * SpeedMul;
            if (NavMesh.SamplePosition(dest, out var hit, 5f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }

        bool Arrived(float slack = 1.2f) => agent != null && agent.enabled && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + slack;

        void Stop()
        {
            if (agent != null && agent.enabled) agent.ResetPath();
        }

        void Think(float dt)
        {
            var w = W;
            if (w == null || H == null || w.Night == null) return;
            Alert = Mathf.Max(0, Alert - dt * 0.02f);
            // decay detection
            var keys = new List<byte>(detect.Keys);
            foreach (var k in keys) detect[k] = Mathf.Max(0, detect[k] - dt * 0.35f);

            switch (Mode)
            {
                case St.Sleep:
                    wake = Mathf.Max(0, wake - dt * 0.03f);
                    if (!naturalWakeDone && w.Night.Progress01 > 0.3f && Random.value < dt * 0.01f)
                    {
                        naturalWakeDone = true;
                        wake = 1f; // needs the toilet
                    }
                    if (wake >= 1f)
                    {
                        SetState(St.WakeUp);
                        Sfx.I?.Play(SoundId.Grumble, Eye, 1f);
                        w.EmitSound(SoundId.Grumble, Eye, 1f);
                    }
                    return;
                case St.WakeUp:
                    if (stateTime > 1.6f)
                    {
                        StandUp(w.Layout.Spots["bed"].U());
                        Alert = Mathf.Max(Alert, 0.4f);
                        if (investigateScore > 0) SetState(St.Investigate);
                        else SetState(St.Patrol);
                    }
                    return;
                case St.Doze:
                    if (stateTime > 25f)
                    {
                        col.enabled = true;
                        SetState(St.Search);
                        Look = transform.position + transform.forward * 5f;
                    }
                    return;
            }

            // --- awake: look for gnomes ---
            if (Time.time > nextThink)
            {
                nextThink = Time.time + 0.15f;
                Scan(0.15f);
            }

            switch (Mode)
            {
                case St.Patrol:
                    patrolTime += dt;
                    if (fixTarget == null) fixTarget = FindPrankToFix();
                    if (fixTarget != null)
                    {
                        SetState(St.Fix);
                        break;
                    }
                    if (Arrived() || stateTime < 0.1f)
                    {
                        if (patrolTime > 60f && Alert < 0.2f)
                        {
                            SetState(St.GoBed);
                            Go(w.Layout.Spots["bed"].U(), GameConsts.OldManWalk);
                            break;
                        }
                        var spots = new List<V3>(w.Layout.Spots.Values);
                        Go(spots[Random.Range(0, spots.Count)].U(), GameConsts.OldManWalk);
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
                    Look = transform.position + Quaternion.Euler(0, Mathf.Sin(stateTime * 1.5f) * 70f, 0) * transform.forward * 6f;
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
                            w.Noise(g.transform.position, 20f, NoiseKind.Impact);
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
            var d = g.transform.position - transform.position;
            return d.Flat().magnitude <= reach && g.transform.position.y <= GameConsts.OldManGrabMaxHeight;
        }

        void Scan(float dt)
        {
            var w = W;
            float viewDist = GameConsts.OldManViewDist * ViewMul;
            float cosFov = Mathf.Cos(GameConsts.OldManFovDeg * 0.5f * Mathf.Deg2Rad);
            var eye = Eye;
            var fwd = transform.forward;
            foreach (var g in w.Gnomes.Values)
            {
                if (g.Status != PlayerStatus.Free) continue;
                var to = g.Center - eye;
                float dist = to.magnitude;
                if (dist > viewDist) continue;
                var dir = to / dist;
                bool inCone = Vector3.Dot(dir.Flat().normalized, fwd) > cosFov || dist < 3.5f;
                if (!inCone) continue;
                if (Physics.Raycast(eye, dir, dist - 0.4f, Layers.World, QueryTriggerInteraction.Ignore))
                {
                    if (Physics.Raycast(eye, (g.HeadPos - eye).normalized, Vector3.Distance(eye, g.HeadPos) - 0.3f, Layers.World, QueryTriggerInteraction.Ignore)) continue;
                }
                bool inTorch = torch && torch.enabled && Vector3.Angle(torch.transform.forward, g.Center - torch.transform.position) < torch.spotAngle * 0.5f;
                float vis = (1f - dist / viewDist) * (g.Crouching ? 0.45f : 1f) * (g.Velocity.sqrMagnitude > 1f ? 1.4f : 0.8f) * (inTorch ? 2f : 0.8f);
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
                    lastSeen = g.transform.position;
                    lastSeenTime = Time.time;
                    Alert = 1f;
                    if (Mode != St.Chase) SetState(St.Chase);
                }
                if (targetId == g.PlayerId && d1 > 0.5f)
                {
                    lastSeen = g.transform.position;
                    lastSeenTime = Time.time;
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
            bool seen = Time.time - lastSeenTime < 1.6f;
            if (!seen)
            {
                Go(lastSeen, GameConsts.OldManRun);
                if (Arrived(1.5f) || Time.time - lastSeenTime > 6f)
                {
                    targetId = 255;
                    SetState(St.Search);
                }
                return;
            }
            Go(g.transform.position, GameConsts.OldManRun);
            float flat = (g.transform.position - transform.position).Flat().magnitude;
            if (flat <= GameConsts.OldManReach + 0.4f && g.transform.position.y <= GameConsts.OldManGrabMaxHeight)
            {
                if (H.IsHidden(g))
                {
                    // can't reach under the bed: grumble and give up after a while
                    if (stateTime > 3f)
                    {
                        W.EmitSound(SoundId.Grumble, Eye, 1f);
                        anger++;
                        targetId = 255;
                        SetState(St.Search);
                    }
                    return;
                }
                if (anger >= 2 || H.FreeJar() < 0) SetState(St.Swat);
                else SetState(St.Grab);
            }
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
            var dest = shelf != null ? shelf.AnchorPos("free", shelf.transform.position) + shelf.transform.forward * 1.5f : transform.position;
            Go(dest, GameConsts.OldManWalk * 1.2f);
            Look = shelf != null ? shelf.transform.position : transform.position + transform.forward;
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
            var w = W;
            foreach (var f in w.Furniture)
            {
                if (f.Kind == "tv" && f.TvOn) return f.Mechs[0];
                foreach (var m in f.Mechs)
                {
                    if ((m.Role == "faucet" || m.Role == "tubFaucet") && m.IsOpen) return m;
                    if (m.Role == "window" && m.IsOpen && Random.value < 0.3f) return m;
                }
            }
            return null;
        }

        void FixPrank()
        {
            if (fixTarget == null || !fixTarget.IsOpen)
            {
                fixTarget = null;
                SetState(St.Patrol);
                return;
            }
            var front = fixTarget.HandleWorld;
            Go(front, GameConsts.OldManWalk * 1.1f);
            Look = front;
            if ((transform.position - front).Flat().magnitude < 3.2f || stateTime > 20f)
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
            float d = Vector3.Distance(pos, Eye);
            if (d > r) return;
            float loud = 1f - d / r;
            if (Mode == St.Sleep)
            {
                float sleepDepth = W != null && W.Night != null ? Mathf.Lerp(0.55f, 1.2f, W.Night.Progress01) : 1f;
                float k = kind == NoiseKind.Break ? 0.9f : kind == NoiseKind.Clock || kind == NoiseKind.Voice ? 0.6f : kind == NoiseKind.Step ? 0.12f : 0.45f;
                wake += loud * k * sleepDepth;
                if (loud > 0.2f) Fx.Sparkles(W, Eye + Vector3.up, new Color32(255, 240, 150, 255), 1, 0.4f);
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
                    if (Time.time > nextGrumble)
                    {
                        nextGrumble = Time.time + 4f;
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
                lastSeenTime = Time.time;
                if (Mode == St.Patrol || Mode == St.Search || Mode == St.Investigate || Mode == St.Fix) SetState(St.Chase);
            }
        }

        public void SleepDust(Vector3 pos)
        {
            if (!Authority) return;
            if (Vector3.Distance(pos, transform.position + Vector3.up * 3f) > 8f || Mode == St.Sleep) return;
            if (carried != 255) DropCarried();
            Stop();
            if (agent) agent.enabled = false;
            SetState(St.Doze);
            Alert = 0;
            W.EmitSound(SoundId.Snore, Eye, 1f);
        }

        // ================================================================== animation (everyone)

        void Animate(float dt)
        {
            var vel = Authority ? (agent != null && agent.enabled ? agent.velocity : Vector3.zero) : velClient;
            float speed = vel.Flat().magnitude;
            walkPhase += speed * dt * 0.9f;
            bool lying = Mode == St.Sleep;
            bool sitting = Mode == St.Doze;
            float swing = Mathf.Clamp01(speed / 4f) * (Mode == St.Chase ? 1.4f : 1f);
            float legA = lying ? 0 : Mathf.Sin(walkPhase) * 28f * swing;
            Rot(thighL, legA + (sitting ? -80f : 0), 0, 0);
            Rot(thighR, -legA + (sitting ? -80f : 0), 0, 0);
            Rot(shinL, Mathf.Max(0, -Mathf.Sin(walkPhase)) * 35f * swing + (sitting ? 70f : 0), 0, 0);
            Rot(shinR, Mathf.Max(0, Mathf.Sin(walkPhase)) * 35f * swing + (sitting ? 70f : 0), 0, 0);
            if (hips) hips.localPosition = hipsRestPos + Vector3.up * (Mathf.Abs(Mathf.Sin(walkPhase)) * 0.12f * swing - (sitting ? 2.6f : 0f));
            float lean = Mode == St.Chase ? 12f : Mode == St.Search ? 0 : 4f * swing;
            float breathe = lying || sitting ? Mathf.Sin(Time.time * 1.7f) * 3f : 0f;
            Rot(spine, lean + breathe + (sitting ? 15f : 0), 0, 0);
            // head: look at the target / around, droop when dozing
            float headYaw = 0, headPitch = 0;
            if (Look != Vector3.zero && !lying && !sitting)
            {
                var local = transform.InverseTransformPoint(Look);
                headYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -60f, 60f);
                headPitch = Mathf.Clamp(-Mathf.Atan2(local.y - 6f, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -30f, 45f);
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
                    float k = Mathf.Clamp01(stateTime / 0.65f);
                    rArmX = k < 0.6f ? Mathf.Lerp(0, -170f, k / 0.6f) : Mathf.Lerp(-170f, -40f, (k - 0.6f) / 0.4f);
                    rFore = -20f;
                    break;
                case St.Chase:
                    rArmX = -60f + Mathf.Sin(Time.time * 12f) * 30f;
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
            if (!lying && Mode != St.Sleep && torch && torch.enabled) lArmX = Mathf.Min(lArmX, -55f); // hold the torch forward
            Rot(upArmR, rArmX, 0, 0);
            Rot(upArmL, lArmX, 0, 0);
            Rot(foreArmR, rFore, 0, 0);
            Rot(foreArmL, lFore, 0, 0);
            // the sleeping pose sits the root on the bed: handled on the host by EnterSleep; clients get the pose
        }

        void Rot(Transform t, float x, float y, float z)
        {
            if (t == null || !rest.TryGetValue(t, out var r)) return;
            t.localRotation = r * Quaternion.Euler(x, y, z);
        }

        protected override void ApplyClientPose(Vector3 pos, float yawDeg)
        {
            if (Mode == St.Sleep && bed != null)
            {
                var fwd = bed.transform.forward;
                transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.up, -fwd));
                col.enabled = false;
                return;
            }
            col.enabled = Mode != St.Doze;
            rb.MovePosition(pos);
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yawDeg, 0));
        }
    }
}
