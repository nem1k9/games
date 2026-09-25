using System;
using System.Collections.Generic;
using Gnomes.Audio;
using Gnomes.Core;
using Gnomes.Core.Level;
using Gnomes.Core.Protocol;
using Gnomes.Core.Rules;
using Gnomes.NPC;
using Gnomes.Players;
using Gnomes.Session;
using UnityEngine;

namespace Gnomes.World
{
    public enum NoiseKind : byte { Step, Impact, Break, Voice, Machine, Clock, Alarm, Tv, Water }

    public struct NoiseEvent
    {
        public Vector3 Pos;
        public float Radius;
        public NoiseKind Kind;
        public float Time;
    }

    /// <summary>
    /// Registry and host-side rules helpers for the currently loaded level (hub island or house).
    /// Exists on host and clients; <see cref="Authority"/> tells which side owns the simulation.
    /// </summary>
    public class GameWorld : MonoBehaviour
    {
        public static GameWorld Current;

        public LevelKind Kind;
        public int Seed;
        public bool Authority;
        public GameSession Session;
        public HouseLayout Layout;
        public NightState Night;

        public Transform PropsRoot, StaticRoot, NpcRoot, GnomeRoot, FxRoot;
        public readonly Dictionary<ushort, Prop> Props = new Dictionary<ushort, Prop>();
        public readonly List<Furniture> Furniture = new List<Furniture>();
        public readonly List<Mechanism> Mechs = new List<Mechanism>();
        public readonly Dictionary<byte, GnomeBody> Gnomes = new Dictionary<byte, GnomeBody>();
        public readonly List<NoiseEvent> Noises = new List<NoiseEvent>();

        public OldMan OldMan;
        public Cat Cat;
        public Roomba Roomba;

        public Vector3 MushroomPos;
        public Vector3 RevivePoint;
        public BoxCollider StashZone;
        public BoxCollider PortalZone;
        public Transform CraftBench, HighGnome;
        public readonly List<Vector3> Spawns = new List<Vector3>();
        public Bounds PlayArea;
        public float FloorY;

        ushort nextPropId = 1;
        float tipCheckTimer;

        public static GameWorld Create(LevelKind kind, int seed, bool authority, GameSession session)
        {
            var go = new GameObject(kind == LevelKind.House ? "House" : "HubIsland");
            var w = go.AddComponent<GameWorld>();
            w.Kind = kind;
            w.Seed = seed;
            w.Authority = authority;
            w.Session = session;
            w.StaticRoot = new GameObject("Static").transform;
            w.StaticRoot.SetParent(go.transform, false);
            w.PropsRoot = new GameObject("Props").transform;
            w.PropsRoot.SetParent(go.transform, false);
            w.NpcRoot = new GameObject("NPCs").transform;
            w.NpcRoot.SetParent(go.transform, false);
            w.GnomeRoot = new GameObject("Gnomes").transform;
            w.GnomeRoot.SetParent(go.transform, false);
            w.FxRoot = new GameObject("Fx").transform;
            w.FxRoot.SetParent(go.transform, false);
            Current = w;
            return w;
        }

        void OnDestroy()
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
            foreach (var mt in f.Model.Markers("MECH"))
            {
                var m = mt.gameObject.AddComponent<Mechanism>();
                m.Init((ushort)Mechs.Count, f);
                Mechs.Add(m);
                f.Mechs.Add(m);
            }
        }

        // ------------------------------------------------------------------ props

        public Prop SpawnProp(ushort id, string kind, Vector3 pos, Quaternion rot)
        {
            if (Props.ContainsKey(id)) return Props[id];
            var p = Prop.Create(this, id, kind, pos, rot, Authority);
            Props[id] = p;
            if (id >= nextPropId) nextPropId = (ushort)(id + 1);
            return p;
        }

        /// <summary>Host: spawn a new prop at runtime and tell clients.</summary>
        public Prop HostSpawnProp(string kind, Vector3 pos, Quaternion rot, Vector3 vel)
        {
            if (!Authority) return null;
            var id = AllocPropId();
            var p = SpawnProp(id, kind, pos, rot);
            p.SpawnTime = Time.time - 2f; // can break right away
            p.Rb.SetVel(vel);
            Session?.Broadcast(new EventMsg { Type = EvType.PropSpawned, Id = id, S = kind, Pos = pos.ToCoreV(), Rot = rot.ToCoreQ(), Vel = vel.ToCoreV() });
            return p;
        }

        /// <summary>Removes a prop locally (both sides). reason: 0 banked,1 flushed,2 pocketed,3 broken,4 despawn</summary>
        public void RemoveProp(ushort id, byte reason)
        {
            if (!Props.TryGetValue(id, out var p)) return;
            p.Removed = true;
            Props.Remove(id);
            Destroy(p.gameObject);
        }

        public void HostRemoveProp(ushort id, byte reason)
        {
            if (!Authority) return;
            RemoveProp(id, reason);
            Session?.Broadcast(new EventMsg { Type = EvType.PropRemoved, Id = id, P = reason });
        }

        public void OnPropImpact(Prop p, Vector3 point, float loudness, float speed)
        {
            if (!Authority) return;
            SoundId s = SoundId.Thud;
            if (p.Def.Yield[(int)Mat.Clonk] > 0) s = SoundId.Clonk;
            else if (p.Def.Yield[(int)Mat.Glint] > 0) s = SoundId.Clink;
            if (p.Def.Has(ItemFlags.Squeaky)) s = SoundId.Squeak;
            EmitSound(s, point, Mathf.Clamp01(0.25f + loudness));
            Noise(point, 6f + 26f * loudness, NoiseKind.Impact);
        }

        public void BreakProp(Prop p, Vector3 point)
        {
            if (!Authority || p.Removed) return;
            var def = p.Def;
            var pos = p.transform.position;
            HostRemoveProp(p.Id, 3);
            EmitSound(SoundId.Break, pos, 1f);
            Noise(pos, 34f * Mathf.Max(1f, def.Loudness), NoiseKind.Break);
            Session?.Broadcast(new EventMsg { Type = EvType.Sound, P = (byte)SoundId.Break, Pos = pos.ToCoreV(), F = -1 }); // F = -1 -> spawn shards fx
            Fx.Shards(this, pos, def.Kind);
            if (def.SpillKind != null)
            {
                for (int i = 0; i < def.SpillCount; i++)
                {
                    var v = new Vector3(UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(2f, 4f), UnityEngine.Random.Range(-2f, 2f));
                    HostSpawnProp(def.SpillKind, pos + Vector3.up * 0.2f + v * 0.05f, UnityEngine.Random.rotation, v);
                }
            }
            Session?.Host?.OnGameEvent(GameEvent.Broken(def.Kind));
        }

        public void RecoverFallenProp(Prop p)
        {
            if (!Authority) return;
            p.Rb.SetVel(Vector3.zero);
            p.transform.position = MushroomPos + new Vector3(2f, 2f, -2f);
        }

        // ------------------------------------------------------------------ mechanisms & furniture

        /// <summary>Host: toggle a mechanism and broadcast. Returns new state.</summary>
        public bool HostToggleMech(Mechanism m)
        {
            bool on = m.Toggle();
            Session?.Broadcast(new EventMsg { Type = EvType.Mech, Id = m.Index, I = on ? 1 : 0 });
            var snd = m.IsButton ? SoundId.Click : m.Role == "window" ? SoundId.Door : SoundId.Door;
            EmitSound(snd, m.transform.position, 0.8f);
            return on;
        }

        public void HostSetMech(Mechanism m, bool open)
        {
            if (m.IsOpen == open) return;
            m.SetState(open);
            Session?.Broadcast(new EventMsg { Type = EvType.Mech, Id = m.Index, I = open ? 1 : 0 });
            EmitSound(SoundId.Door, m.transform.position, 0.7f);
        }

        public void HostBreakFurniture(Furniture f)
        {
            f.SetBroken();
            Session?.Broadcast(new EventMsg { Type = EvType.FurnitureBroken, Id = f.Index });
            EmitSound(SoundId.Break, f.transform.position + Vector3.up * 3, 1f);
            Noise(f.transform.position, 40f, NoiseKind.Break);
            Session?.Host?.OnGameEvent(GameEvent.FurnitureBroken(f.Kind));
        }

        // ------------------------------------------------------------------ sound & noise

        /// <summary>Play a sound here and on every client.</summary>
        public void EmitSound(SoundId id, Vector3 pos, float volume)
        {
            Sfx.I?.Play(id, pos, volume);
            if (Authority) Session?.Broadcast(new EventMsg { Type = EvType.Sound, P = (byte)id, Pos = pos.ToCoreV(), F = volume });
        }

        /// <summary>Host: something audible happened (the old man and the cat can hear it).</summary>
        public void Noise(Vector3 pos, float radius, NoiseKind kind)
        {
            if (!Authority) return;
            Noises.Add(new NoiseEvent { Pos = pos, Radius = radius, Kind = kind, Time = Time.time });
            if (Noises.Count > 64) Noises.RemoveAt(0);
            OldMan?.Hear(pos, radius, kind);
            Cat?.Hear(pos, radius, kind);
        }

        void FixedUpdate()
        {
            if (!Authority) return;
            tipCheckTimer -= Time.fixedDeltaTime;
            if (tipCheckTimer <= 0)
            {
                tipCheckTimer = 0.5f;
                foreach (var p in Props.Values)
                    if (p.Def.HasTag("trashBin") && p.CheckTipped())
                    {
                        EmitSound(SoundId.Clonk, p.transform.position, 1f);
                        Noise(p.transform.position, 25f, NoiseKind.Impact);
                        Session?.Host?.OnGameEvent(GameEvent.Tipped(p.Def.Kind));
                    }
            }
        }

        public Vector3 SpawnPoint(int index)
        {
            if (Spawns.Count == 0) return Vector3.up;
            return Spawns[index % Spawns.Count];
        }

        public bool IsInStash(Vector3 p) => StashZone != null && StashZone.bounds.Contains(p);

        public static bool ZoneContains(BoxCollider zone, Vector3 worldPos)
        {
            if (zone == null) return false;
            var local = zone.transform.InverseTransformPoint(worldPos) - zone.center;
            var h = zone.size * 0.5f;
            return Mathf.Abs(local.x) <= h.x && Mathf.Abs(local.y) <= h.y && Mathf.Abs(local.z) <= h.z;
        }
    }

    public static class VecConv
    {
        public static V3 ToCoreV(this Vector3 v) => new V3(v.x, v.y, v.z);
        public static Q4 ToCoreQ(this Quaternion q) => new Q4(q.x, q.y, q.z, q.w);
        public static Vector3 U(this V3 v) => new Vector3(v.x, v.y, v.z);
        public static Quaternion U(this Q4 q) => new Quaternion(q.x, q.y, q.z, q.w);
    }
}
