using System.Collections.Generic;
using Gnomes.Core;
using Gnomes.Core.Level;
using Gnomes.Core.Protocol;
using Gnomes.Core.Rules;
using Godot;
using SockGang.Audio;
using SockGang.NPC;
using SockGang.Players;
using SockGang.Session;

namespace SockGang.World
{
    public enum NoiseKind : byte { Step, Impact, Break, Voice, Machine, Clock, Alarm, Tv, Water }

    /// <summary>
    /// Registry and host-side rule helpers for the loaded level (village or house).
    /// Exists on host and clients; <see cref="Authority"/> tells which side owns the simulation.
    /// </summary>
    public partial class GameWorld : Node3D
    {
        public static GameWorld Current;

        public LevelKind Kind;
        public int Seed;
        public bool Authority;
        public GameSession Session;
        public HouseLayout Layout;
        public NightState Night;

        public Node3D StaticRoot, PropsRoot, NpcRoot, GnomeRoot, FxRoot;
        public readonly Dictionary<ushort, Prop> Props = new Dictionary<ushort, Prop>();
        public readonly List<Furniture> Furniture = new List<Furniture>();
        public readonly List<Mechanism> Mechs = new List<Mechanism>();
        public readonly Dictionary<byte, GnomeBody> Gnomes = new Dictionary<byte, GnomeBody>();

        public OldMan OldMan;
        public Cat Cat;
        public Parrot Parrot;
        public Ties Ties;

        public Vector3 GoHomePoint; // the gap under the porch
        public Vector3 RevivePoint; // in front of the yarn basket
        public Node3D ReviveZone, StashZone, PortalZone; // unit-box markers (scale = size)
        public Node3D CraftBench, HighGnome;
        public readonly List<Vector3> Spawns = new List<Vector3>();
        public Aabb PlayArea;

        ushort nextPropId = 1;
        float tipCheckTimer;

        public static GameWorld Create(Node parent, LevelKind kind, int seed, bool authority, GameSession session)
        {
            var w = new GameWorld { Name = kind == LevelKind.House ? "House" : "Village", Kind = kind, Seed = seed, Authority = authority, Session = session };
            parent.AddChild(w);
            w.StaticRoot = w.Child("Static");
            w.PropsRoot = w.Child("Props");
            w.NpcRoot = w.Child("NPCs");
            w.GnomeRoot = w.Child("Gnomes");
            w.FxRoot = w.Child("Fx");
            w.Ties = new Ties { Name = "Ties", World = w };
            w.AddChild(w.Ties);
            Current = w;
            return w;
        }

        Node3D Child(string name)
        {
            var n = new Node3D { Name = name };
            AddChild(n);
            return n;
        }

        public override void _ExitTree()
        {
            if (Current == this) Current = null;
        }

        // ------------------------------------------------------------------ lookup

        public Furniture FindFurniture(string id)
        {
            foreach (var f in Furniture) if (f.Id == id) return f;
            return null;
        }

        public Furniture FirstOfKind(string kind)
        {
            foreach (var f in Furniture) if (f.Kind == kind) return f;
            return null;
        }

        public Prop GetProp(ushort id) => Props.TryGetValue(id, out var p) ? p : null;
        public Mechanism GetMech(int index) => index >= 0 && index < Mechs.Count ? Mechs[index] : null;

        public ushort AllocPropId()
        {
            while (Props.ContainsKey(nextPropId) || nextPropId == 0) nextPropId++;
            return nextPropId++;
        }

        public void RegisterFurniture(Furniture f)
        {
            f.Index = (ushort)Furniture.Count;
            Furniture.Add(f);
            foreach (var part in f.Model.Markers("MECH"))
            {
                var m = new Mechanism { Name = "Mechanism" };
                part.AddChild(m);
                m.Init((ushort)Mechs.Count, f, part, f.Model.NameOf(part));
                Mechs.Add(m);
                f.Mechs.Add(m);
            }
        }

        /// <summary>Room (Unity-space layout) at a Godot position.</summary>
        public Room RoomAt(Vector3 p) => Layout?.RoomAt(p.X, -p.Z);

        // ------------------------------------------------------------------ props

        public Prop SpawnProp(ushort id, string kind, Vector3 pos, Quaternion rot)
        {
            if (Props.TryGetValue(id, out var existing)) return existing;
            var p = Prop.Create(this, id, kind, pos, rot, Authority);
            Props[id] = p;
            if (id >= nextPropId) nextPropId = (ushort)(id + 1);
            return p;
        }

        /// <summary>Host: spawn a new prop at runtime and tell clients. tint = hat colour index + 1 (0 = none).</summary>
        public Prop HostSpawnProp(string kind, Vector3 pos, Quaternion rot, Vector3 vel, byte tint = 0)
        {
            if (!Authority) return null;
            var id = AllocPropId();
            var p = SpawnProp(id, kind, pos, rot);
            p.SpawnTime = Clock.Now - 2f; // can break right away
            p.LinearVelocity = vel;
            if (tint > 0) p.Model.SetTint(GameSession.HatColor((byte)(tint - 1)));
            Session?.Broadcast(new EventMsg { Type = EvType.PropSpawned, Id = id, S = kind, P = tint, Pos = pos.U(), Rot = rot.U(), Vel = vel.U() });
            return p;
        }

        /// <summary>Removes a prop locally (both sides). reason: 0 banked, 1 flushed, 2 pocketed, 3 broken, 4 despawn.</summary>
        public void RemoveProp(ushort id, byte reason)
        {
            if (!Props.TryGetValue(id, out var p)) return;
            p.Removed = true;
            Props.Remove(id);
            Ties?.OnPropRemoved(id);
            p.QueueFree();
        }

        public void HostRemoveProp(ushort id, byte reason)
        {
            if (!Authority) return;
            RemoveProp(id, reason);
            Session?.Broadcast(new EventMsg { Type = EvType.PropRemoved, Id = id, P = reason });
        }

        public void OnPropImpact(Prop p, Vector3 point, float loudness)
        {
            if (!Authority) return;
            SoundId s = SoundId.Thud;
            if (p.Def.Yield[(int)Mat.Bolts] > 0) s = SoundId.Clonk;
            else if (p.Def.Yield[(int)Mat.Glitter] > 0) s = SoundId.Clink;
            if (p.Def.Has(ItemFlags.Squeaky)) s = SoundId.Squeak;
            EmitSound(s, point, Mathf.Clamp(0.25f + loudness, 0f, 1f));
            Noise(point, 6f + 26f * loudness, NoiseKind.Impact);
        }

        public void BreakProp(Prop p, Vector3 point)
        {
            if (!Authority || p.Removed) return;
            var def = p.Def;
            var pos = p.GlobalPosition;
            HostRemoveProp(p.Id, 3);
            if (def.HasTag("sleepDust"))
            {
                EmitSound(SoundId.Sparkle, pos, 1f);
                Session?.Host?.OnSleepDust(pos);
                return;
            }
            EmitSound(SoundId.Break, pos, 1f);
            Noise(pos, 34f * Mathf.Max(1f, def.Loudness), NoiseKind.Break);
            Session?.Broadcast(new EventMsg { Type = EvType.Sound, P = (byte)SoundId.Break, Pos = pos.U(), F = -1 }); // F = -1 -> shards fx
            Fx.Shards(this, pos);
            if (def.SpillKind != null)
            {
                for (int i = 0; i < def.SpillCount; i++)
                {
                    var v = new Vector3(GMath.Rand(-2f, 2f), GMath.Rand(2f, 4f), GMath.Rand(-2f, 2f));
                    HostSpawnProp(def.SpillKind, pos + Vector3.Up * 0.2f + v * 0.05f, GMath.RandRotation(), v);
                }
            }
            Session?.Host?.OnGameEvent(GameEvent.Broken(def.Kind));
        }

        public void RecoverFallenProp(Prop p)
        {
            if (!Authority) return;
            p.LinearVelocity = Vector3.Zero;
            p.GlobalPosition = GoHomePoint + new Vector3(2f, 2f, -2f);
        }

        // ------------------------------------------------------------------ mechanisms & furniture

        /// <summary>Host: toggle a mechanism and broadcast. Returns the new state.</summary>
        public bool HostToggleMech(Mechanism m)
        {
            bool on = m.Toggle();
            Session?.Broadcast(new EventMsg { Type = EvType.Mech, Id = m.Index, I = on ? 1 : 0 });
            EmitSound(m.IsButton ? SoundId.Click : SoundId.Door, m.GlobalPosition, 0.8f);
            return on;
        }

        public void HostSetMech(Mechanism m, bool open)
        {
            if (m.IsOpen == open) return;
            m.SetState(open);
            Session?.Broadcast(new EventMsg { Type = EvType.Mech, Id = m.Index, I = open ? 1 : 0 });
            EmitSound(SoundId.Door, m.GlobalPosition, 0.7f);
        }

        public void HostBreakFurniture(Furniture f)
        {
            f.SetBroken();
            Session?.Broadcast(new EventMsg { Type = EvType.FurnitureBroken, Id = f.Index });
            EmitSound(SoundId.Break, f.GlobalPosition + Vector3.Up * 3, 1f);
            Noise(f.GlobalPosition, 40f, NoiseKind.Break);
            Session?.Host?.OnGameEvent(GameEvent.FurnitureBroken(f.Kind));
        }

        // ------------------------------------------------------------------ sound & noise

        /// <summary>Play a sound here and on every client.</summary>
        public void EmitSound(SoundId id, Vector3 pos, float volume)
        {
            Sfx.I?.Play(id, pos, volume);
            if (Authority) Session?.Broadcast(new EventMsg { Type = EvType.Sound, P = (byte)id, Pos = pos.U(), F = volume });
        }

        /// <summary>Host: something audible happened (grandpa and the cat can hear it).</summary>
        public void Noise(Vector3 pos, float radius, NoiseKind kind)
        {
            if (!Authority) return;
            OldMan?.Hear(pos, radius, kind);
            Cat?.Hear(pos, radius, kind);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!Authority) return;
            tipCheckTimer -= (float)delta;
            if (tipCheckTimer > 0) return;
            tipCheckTimer = 0.5f;
            foreach (var p in Props.Values)
                if (p.Def.HasTag("trashBin") && p.CheckTipped())
                {
                    EmitSound(SoundId.Clonk, p.GlobalPosition, 1f);
                    Noise(p.GlobalPosition, 25f, NoiseKind.Impact);
                    Session?.Host?.OnGameEvent(GameEvent.Tipped(p.Def.Kind));
                }
        }

        public Vector3 SpawnPoint(int index) => Spawns.Count == 0 ? Vector3.Up : Spawns[index % Spawns.Count];

        /// <summary>Point inside a zone marker (a unit box scaled to the zone size)?</summary>
        public static bool ZoneContains(Node3D zone, Vector3 worldPos)
        {
            if (zone == null || !IsInstanceValid(zone)) return false;
            var local = zone.GlobalTransform.AffineInverse() * worldPos;
            return Mathf.Abs(local.X) <= 0.5f && Mathf.Abs(local.Y) <= 0.5f && Mathf.Abs(local.Z) <= 0.5f;
        }

        /// <summary>Is a point close to a switched-on lamp (well lit)? Used for stealth.</summary>
        public bool IsLit(Vector3 p)
        {
            foreach (var f in Furniture)
                foreach (var l in f.Lights)
                    if (l != null && l.Visible && l.LightEnergy > 0.1f && (l.GlobalPosition - p).LengthSquared() < l.OmniRange * l.OmniRange * 0.36f)
                        return true;
            return false;
        }
    }
}
