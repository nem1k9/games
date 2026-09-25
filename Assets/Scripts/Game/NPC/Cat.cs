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
    /// Barsik the cat. Naps, wanders, stalks and pounces on gnomes. Can't resist a rolling ball of yarn
    /// and will happily be bribed with a sausage.
    /// </summary>
    public class Cat : NpcBase
    {
        public enum St : byte { Nap, Wander, Stalk, Pounce, Toy, Eat, Flee, Doze }

        ModelInstance model;
        Transform body, head, tail1, tail2, tail3, legFL, legFR, legBL, legBR;
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        NavMeshAgent agent;
        Rigidbody rb;
        Furniture bed;
        float stateTime, nextThink, walkPhase, napUntil;
        byte targetId = 255;
        ushort toyProp, foodProp;
        Vector3 lastPosClient, velClient;
        Vector3 fleeFrom;
        GameSession S => GameSession.I;

        public St Mode => (St)State;

        public static Cat Spawn(GameWorld w, bool authority)
        {
            var inst = ModelLibrary.Instantiate("cat", w.NpcRoot, Layers.NPC, withColliders: false);
            var c = inst.gameObject.AddComponent<Cat>();
            c.NpcId = CatId;
            c.Authority = authority;
            c.model = inst;
            inst.SetLayerRecursive(Layers.NPC);
            c.Bind();
            c.bed = w.FindFurniture("catBed");
            c.rb = inst.gameObject.AddComponent<Rigidbody>();
            c.rb.isKinematic = true;
            c.rb.interpolation = RigidbodyInterpolation.Interpolate;
            var col = inst.gameObject.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.radius = 0.5f;
            col.height = 2.0f;
            col.center = new Vector3(0, 0.75f, 0);
            var start = c.bed != null ? c.bed.transform.position : w.Layout.Spots["kitchenTable"].U();
            inst.transform.position = start;
            if (authority)
            {
                c.agent = inst.gameObject.AddComponent<NavMeshAgent>();
                c.agent.radius = 0.5f;
                c.agent.height = 1.4f;
                c.agent.speed = 3f;
                c.agent.acceleration = 30f;
                c.agent.angularSpeed = 500f;
                c.agent.stoppingDistance = 0.3f;
                c.agent.updatePosition = false;
                c.agent.updateRotation = false;
                if (NavMesh.SamplePosition(start, out var hit, 6f, NavMesh.AllAreas))
                {
                    c.agent.Warp(hit.position);
                    inst.transform.position = hit.position;
                }
                else c.agent.enabled = false;
            }
            c.State = (byte)St.Nap;
            c.napUntil = Time.time + Random.Range(40f, 90f);
            w.Cat = c;
            return c;
        }

        void Bind()
        {
            body = model.Node("body");
            head = model.Node("head");
            tail1 = model.Node("tail1");
            tail2 = model.Node("tail2");
            tail3 = model.Node("tail3");
            legFL = model.Node("legFL");
            legFR = model.Node("legFR");
            legBL = model.Node("legBL");
            legBR = model.Node("legBR");
            foreach (var t in new[] { body, head, tail1, tail2, tail3, legFL, legFR, legBL, legBR })
                if (t) rest[t] = t.localRotation;
        }

        void SetState(St s)
        {
            State = (byte)s;
            stateTime = 0;
        }

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
        }

        void FixedUpdate()
        {
            if (!Authority || agent == null || !agent.enabled) return;
            rb.MovePosition(agent.nextPosition);
            var v = agent.velocity.Flat();
            if (v.sqrMagnitude > 0.05f) rb.MoveRotation(Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(v), 0.25f));
        }

        void Go(Vector3 p, float speed)
        {
            if (agent == null || !agent.enabled) return;
            agent.speed = speed;
            if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }

        bool Arrived(float slack = 0.5f) => agent != null && agent.enabled && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + slack;

        void Think(float dt)
        {
            var w = W;
            if (w == null || w.Night == null || agent == null || !agent.enabled) return;
            if (Time.time > nextThink)
            {
                nextThink = Time.time + 0.25f;
                LookForFun();
            }
            switch (Mode)
            {
                case St.Nap:
                    agent.ResetPath();
                    if (Time.time > napUntil) SetState(St.Wander);
                    break;
                case St.Wander:
                    if (Arrived() || stateTime < 0.1f)
                    {
                        if (stateTime > 45f && Random.value < 0.3f)
                        {
                            napUntil = Time.time + Random.Range(30f, 60f);
                            Go(bed != null ? bed.transform.position : transform.position, 2f);
                            SetState(St.Nap);
                            break;
                        }
                        var spots = new List<V3>(w.Layout.Spots.Values);
                        Go(spots[Random.Range(0, spots.Count)].U() + Random.insideUnitSphere.Flat() * 4f, 2.2f);
                    }
                    break;
                case St.Stalk:
                {
                    var g = TargetGnome();
                    if (g == null || g.Status != PlayerStatus.Free || stateTime > 14f)
                    {
                        SetState(St.Wander);
                        break;
                    }
                    Look = g.Center;
                    float d = Vector3.Distance(transform.position, g.transform.position);
                    Go(g.transform.position, d > 5f ? 4.5f : 1.8f);
                    if (d < 2.4f && Mathf.Abs(g.transform.position.y - transform.position.y) < 1.5f) SetState(St.Pounce);
                    break;
                }
                case St.Pounce:
                {
                    var g = TargetGnome();
                    agent.ResetPath();
                    if (stateTime > 0.35f)
                    {
                        if (g != null && g.Status == PlayerStatus.Free && Vector3.Distance(transform.position, g.transform.position) < 2.8f)
                        {
                            var slot = S?.Host?.Slot(g.PlayerId);
                            if (slot != null) S.Host.StunPlayer(slot, 2f);
                            w.EmitSound(SoundId.Meow, transform.position + Vector3.up, 1f);
                            w.Noise(transform.position, 22f, NoiseKind.Voice);
                        }
                        SetState(St.Wander);
                        napUntil = Time.time + 20f;
                    }
                    break;
                }
                case St.Toy:
                {
                    var p = w.GetProp(toyProp);
                    if (p == null || stateTime > 12f)
                    {
                        SetState(St.Wander);
                        break;
                    }
                    Go(p.transform.position, 5f);
                    Look = p.transform.position;
                    if (Vector3.Distance(transform.position, p.transform.position) < 1.4f && p.Authority)
                    {
                        // bat the yarn ball around
                        p.Rb.AddForce((p.transform.position - transform.position).Flat().normalized * 3f + Vector3.up * 1.5f, ForceMode.Impulse);
                    }
                    break;
                }
                case St.Eat:
                {
                    var p = w.GetProp(foodProp);
                    if (p == null)
                    {
                        SetState(St.Nap);
                        napUntil = Time.time + 25f; // food coma
                        break;
                    }
                    Go(p.transform.position, 3.5f);
                    if (Vector3.Distance(transform.position, p.transform.position) < 1.3f && stateTime > 1f)
                    {
                        w.HostRemoveProp(p.Id, 4);
                        w.EmitSound(SoundId.Slurp, transform.position, 1f);
                    }
                    break;
                }
                case St.Flee:
                    Go(transform.position + (transform.position - fleeFrom).Flat().normalized * 10f, 6f);
                    if (stateTime > 4f) SetState(St.Wander);
                    break;
                case St.Doze:
                    agent.ResetPath();
                    if (stateTime > 30f) SetState(St.Wander);
                    break;
            }
        }

        GnomeBody TargetGnome() => targetId != 255 && W != null && W.Gnomes.TryGetValue(targetId, out var g) ? g : null;

        void LookForFun()
        {
            var w = W;
            if (Mode == St.Pounce || Mode == St.Eat || Mode == St.Flee || Mode == St.Doze) return;
            // food beats everything
            foreach (var p in w.Props.Values)
            {
                if (!p.Def.HasTag("food") || p.Def.Kind == "apple") continue;
                if (Vector3.Distance(p.transform.position, transform.position) < 9f && p.transform.position.y < 2f)
                {
                    foodProp = p.Id;
                    SetState(St.Eat);
                    return;
                }
            }
            if (Mode == St.Nap) return;
            // a moving yarn ball is irresistible
            foreach (var p in w.Props.Values)
            {
                if (!p.Def.HasTag("catToy")) continue;
                if (p.Rb.Vel().sqrMagnitude > 1f && Vector3.Distance(p.transform.position, transform.position) < 16f)
                {
                    toyProp = p.Id;
                    SetState(St.Toy);
                    return;
                }
            }
            if (Mode == St.Stalk || Mode == St.Toy) return;
            var fwd = transform.forward;
            foreach (var g in w.Gnomes.Values)
            {
                if (g.Status != PlayerStatus.Free) continue;
                var to = g.Center - (transform.position + Vector3.up);
                float d = to.magnitude;
                if (d > 14f || Vector3.Dot(to / d, fwd) < 0.2f) continue;
                if (g.Crouching && d > 5f) continue;
                if (Physics.Raycast(transform.position + Vector3.up, to / d, d - 0.3f, Layers.World, QueryTriggerInteraction.Ignore)) continue;
                targetId = g.PlayerId;
                SetState(St.Stalk);
                w.EmitSound(SoundId.Meow, transform.position + Vector3.up, 0.7f);
                return;
            }
        }

        public void Hear(Vector3 pos, float radius, NoiseKind kind)
        {
            if (!Authority || Mode != St.Nap) return;
            if (Vector3.Distance(pos, transform.position) < radius * 0.5f && kind != NoiseKind.Step) SetState(St.Wander);
        }

        public void OnKicked(Vector3 dir)
        {
            if (!Authority) return;
            fleeFrom = transform.position - dir * 3f;
            SetState(St.Flee);
            W.EmitSound(SoundId.Hiss, transform.position + Vector3.up, 1f);
            W.Noise(transform.position, 18f, NoiseKind.Voice);
        }

        public void SleepDust(Vector3 pos)
        {
            if (!Authority || Vector3.Distance(pos, transform.position) > 7f) return;
            SetState(St.Doze);
        }

        void Animate(float dt)
        {
            var vel = Authority ? (agent != null && agent.enabled ? agent.velocity : Vector3.zero) : velClient;
            float speed = vel.Flat().magnitude;
            walkPhase += speed * dt * 2.4f;
            float s = Mathf.Clamp01(speed / 3f);
            bool curled = Mode == St.Nap || Mode == St.Doze || Mode == St.Eat && stateTime > 1f;
            float a = Mathf.Sin(walkPhase) * 32f * s;
            Rot(legFL, curled ? -70 : a, 0, 0);
            Rot(legBR, curled ? 70 : a, 0, 0);
            Rot(legFR, curled ? -70 : -a, 0, 0);
            Rot(legBL, curled ? 70 : -a, 0, 0);
            float crouch = Mode == St.Stalk ? 12f : Mode == St.Pounce ? -20f + stateTime * 60f : 0f;
            Rot(body, crouch, 0, curled ? 0 : 0);
            if (body) body.localPosition = new Vector3(0, curled ? 0.45f : Mode == St.Stalk ? 0.6f : 0.72f, 0);
            float wag = Mathf.Sin(Time.time * (Mode == St.Stalk ? 9f : 2.2f)) * (Mode == St.Stalk ? 25f : 15f);
            Rot(tail1, 0, 0, wag * 0.5f);
            Rot(tail2, 0, 0, wag);
            Rot(tail3, 0, 0, wag * 1.3f);
            if (head && Look != Vector3.zero && !curled)
            {
                var local = transform.InverseTransformPoint(Look);
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -50, 50);
                Rot(head, 0, yaw, 0);
            }
            else Rot(head, curled ? 25f : 0, 0, curled ? 20f : 0);
        }

        void Rot(Transform t, float x, float y, float z)
        {
            if (t == null || !rest.TryGetValue(t, out var r)) return;
            t.localRotation = r * Quaternion.Euler(x, y, z);
        }
    }
}
