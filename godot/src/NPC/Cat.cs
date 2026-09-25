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
    /// Barsik the cat. Naps, wanders, stalks and pounces on gnomes. Can't resist a rolling ball of yarn
    /// and will happily be bribed with a sausage.
    /// </summary>
    public partial class Cat : NpcBase
    {
        public enum St : byte { Nap, Wander, Stalk, Pounce, Toy, Eat, Flee, Doze }

        ModelInstance model;
        Node3D body, head, tail1, tail2, tail3, legFL, legFR, legBL, legBR;
        readonly Dictionary<Node3D, Quaternion> rest = new Dictionary<Node3D, Quaternion>();
        NavAgent agent;
        Furniture bed;
        float stateTime, nextThink, walkPhase, napUntil;
        byte targetId = 255;
        ushort toyProp, foodProp;
        Vector3 lastPosClient, velClient, fleeFrom;
        GameSession S => GameSession.I;

        public St Mode => (St)State;

        public static Cat Spawn(GameWorld w, bool authority)
        {
            var c = new Cat { Name = "Cat", NpcId = CatId, Authority = authority, SyncToPhysics = false, CollisionLayer = Layers.Npc, CollisionMask = 0 };
            w.NpcRoot.AddChild(c);
            c.model = ModelLibrary.Instantiate("cat", c, ColliderMode.None);
            c.Bind();
            c.bed = w.FindFurniture("catBed");
            c.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.5f, Height = 2.0f }, Position = new Vector3(0, 0.75f, 0), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
            var start = c.bed != null ? c.bed.GlobalPosition : w.Layout.Spots["kitchenTable"].G();
            c.GlobalPosition = start;
            if (authority)
            {
                c.agent = new NavAgent(w.GetWorld3D().NavigationMap) { Speed = 3f, Acceleration = 30f, StoppingDistance = 0.3f };
                c.agent.Warp(start);
            }
            c.State = (byte)St.Nap;
            c.napUntil = Clock.Now + GMath.Rand(40f, 90f);
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
                if (t != null) rest[t] = t.Quaternion;
        }

        void SetState(St s)
        {
            State = (byte)s;
            stateTime = 0;
        }

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
        }

        bool navReady;

        public override void _PhysicsProcess(double delta)
        {
            if (!Authority || agent == null) return;
            if (!navReady)
            {
                // the navigation map syncs a frame after the bake: snap onto it once it exists
                if (agent.Sample(GlobalPosition, 6f, out var hit) && hit != Vector3.Zero)
                {
                    agent.Warp(hit);
                    navReady = true;
                }
                return;
            }
            var next = agent.Step((float)delta);
            var v = agent.Velocity.Flat();
            float yaw = v.LengthSquared() > 0.05f ? Mathf.LerpAngle(BodyYaw, GMath.YawOf(v), 0.25f) : BodyYaw;
            MoveBody(next, yaw);
        }

        void Go(Vector3 p, float speed)
        {
            if (agent == null || !navReady) return;
            agent.Speed = speed;
            if (agent.Sample(p, 4f, out var hit)) agent.SetDestination(hit);
        }

        bool Arrived(float slack = 0.5f) => agent != null && navReady && !agent.PathPending && agent.RemainingDistance <= agent.StoppingDistance + slack;

        void Think(float dt)
        {
            var w = W;
            if (w == null || w.Night == null || agent == null || !navReady) return;
            if (Clock.Now > nextThink)
            {
                nextThink = Clock.Now + 0.25f;
                LookForFun();
            }
            switch (Mode)
            {
                case St.Nap:
                    agent.ResetPath();
                    if (Clock.Now > napUntil) SetState(St.Wander);
                    break;
                case St.Wander:
                    if (Arrived() || stateTime < 0.1f)
                    {
                        if (stateTime > 45f && GMath.Rand01 < 0.3f)
                        {
                            napUntil = Clock.Now + GMath.Rand(30f, 60f);
                            Go(bed != null ? bed.GlobalPosition : GlobalPosition, 2f);
                            SetState(St.Nap);
                            break;
                        }
                        var spots = new List<V3>(w.Layout.Spots.Values);
                        Go(spots[GMath.RandInt(0, spots.Count)].G() + GMath.RandInSphere().Flat() * 4f, 2.2f);
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
                    float d = GlobalPosition.DistanceTo(g.GlobalPosition);
                    Go(g.GlobalPosition, d > 5f ? 4.5f : 1.8f);
                    if (d < 2.4f && Mathf.Abs(g.GlobalPosition.Y - GlobalPosition.Y) < 1.5f) SetState(St.Pounce);
                    break;
                }
                case St.Pounce:
                {
                    var g = TargetGnome();
                    agent.ResetPath();
                    if (stateTime > 0.35f)
                    {
                        if (g != null && g.Status == PlayerStatus.Free && GlobalPosition.DistanceTo(g.GlobalPosition) < 2.8f)
                        {
                            var slot = S?.Host?.Slot(g.PlayerId);
                            if (slot != null) S.Host.StunPlayer(slot, 2f);
                            w.EmitSound(SoundId.Meow, GlobalPosition + Vector3.Up, 1f);
                            w.Noise(GlobalPosition, 22f, NoiseKind.Voice);
                        }
                        SetState(St.Wander);
                        napUntil = Clock.Now + 20f;
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
                    Go(p.GlobalPosition, 5f);
                    Look = p.GlobalPosition;
                    if (GlobalPosition.DistanceTo(p.GlobalPosition) < 1.4f && p.Authority)
                        p.ApplyCentralImpulse((p.GlobalPosition - GlobalPosition).Flat().Normalized() * 3f + Vector3.Up * 1.5f); // bat it around
                    break;
                }
                case St.Eat:
                {
                    var p = w.GetProp(foodProp);
                    if (p == null)
                    {
                        SetState(St.Nap);
                        napUntil = Clock.Now + 25f; // food coma
                        break;
                    }
                    Go(p.GlobalPosition, 3.5f);
                    if (GlobalPosition.DistanceTo(p.GlobalPosition) < 1.3f && stateTime > 1f)
                    {
                        w.HostRemoveProp(p.Id, 4);
                        w.EmitSound(SoundId.Slurp, GlobalPosition, 1f);
                    }
                    break;
                }
                case St.Flee:
                    Go(GlobalPosition + (GlobalPosition - fleeFrom).Flat().Normalized() * 10f, 6f);
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
                if (p.GlobalPosition.DistanceTo(GlobalPosition) < 9f && p.GlobalPosition.Y < 2f)
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
                if (p.LinearVelocity.LengthSquared() > 1f && p.GlobalPosition.DistanceTo(GlobalPosition) < 16f)
                {
                    toyProp = p.Id;
                    SetState(St.Toy);
                    return;
                }
            }
            if (Mode == St.Stalk || Mode == St.Toy) return;
            var fwd = GMath.YawForward(BodyYaw);
            foreach (var g in w.Gnomes.Values)
            {
                if (g.Status != PlayerStatus.Free) continue;
                var eye = GlobalPosition + Vector3.Up;
                var to = g.Center - eye;
                float d = to.Length();
                if (d > 14f || d < 1e-3f || (to / d).Dot(fwd) < 0.2f) continue;
                if (g.Crouching && d > 5f) continue;
                if (Phys.Raycast(eye, to / d, d - 0.3f, Layers.Solid)) continue;
                targetId = g.PlayerId;
                SetState(St.Stalk);
                w.EmitSound(SoundId.Meow, eye, 0.7f);
                return;
            }
        }

        public void Hear(Vector3 pos, float radius, NoiseKind kind)
        {
            if (!Authority || Mode != St.Nap) return;
            if (pos.DistanceTo(GlobalPosition) < radius * 0.5f && kind != NoiseKind.Step) SetState(St.Wander);
        }

        public void OnKicked(Vector3 dir)
        {
            if (!Authority) return;
            fleeFrom = GlobalPosition - dir * 3f;
            SetState(St.Flee);
            W.EmitSound(SoundId.Hiss, GlobalPosition + Vector3.Up, 1f);
            W.Noise(GlobalPosition, 18f, NoiseKind.Voice);
        }

        public void SleepDust(Vector3 pos)
        {
            if (!Authority || pos.DistanceTo(GlobalPosition) > 7f) return;
            SetState(St.Doze);
        }

        void Animate(float dt)
        {
            var vel = Authority ? (agent != null ? agent.Velocity : Vector3.Zero) : velClient;
            float speed = vel.Flat().Length();
            walkPhase += speed * dt * 2.4f;
            float s = Mathf.Clamp(speed / 3f, 0f, 1f);
            bool curled = Mode == St.Nap || Mode == St.Doze || (Mode == St.Eat && stateTime > 1f);
            float a = Mathf.Sin(walkPhase) * 32f * s;
            Rot(legFL, curled ? -70 : a, 0, 0);
            Rot(legBR, curled ? 70 : a, 0, 0);
            Rot(legFR, curled ? -70 : -a, 0, 0);
            Rot(legBL, curled ? 70 : -a, 0, 0);
            float crouch = Mode == St.Stalk ? 12f : Mode == St.Pounce ? -20f + stateTime * 60f : 0f;
            Rot(body, crouch, 0, 0);
            if (body != null) body.Position = new Vector3(0, curled ? 0.45f : Mode == St.Stalk ? 0.6f : 0.72f, 0);
            float wag = Mathf.Sin(Clock.Now * (Mode == St.Stalk ? 9f : 2.2f)) * (Mode == St.Stalk ? 25f : 15f);
            Rot(tail1, 0, 0, wag * 0.5f);
            Rot(tail2, 0, 0, wag);
            Rot(tail3, 0, 0, wag * 1.3f);
            if (head != null && Look != Vector3.Zero && !curled)
            {
                var lg = ToLocal(Look);
                float yaw = Mathf.Clamp(Mathf.RadToDeg(Mathf.Atan2(lg.X, -lg.Z)), -50, 50);
                Rot(head, 0, yaw, 0);
            }
            else Rot(head, curled ? 25f : 0, 0, curled ? 20f : 0);
        }

        void Rot(Node3D t, float x, float y, float z)
        {
            if (t == null || !rest.TryGetValue(t, out var r)) return;
            t.Quaternion = r * Conv.UEuler(x, y, z);
        }
    }
}
