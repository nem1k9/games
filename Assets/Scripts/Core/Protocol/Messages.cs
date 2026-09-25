using System;
using System.Collections.Generic;
using Gnomes.Core.Rules;

namespace Gnomes.Core.Protocol
{
    public enum Msg : byte
    {
        // client -> host
        Hello = 1,
        PlayerState = 2,
        Action = 3,
        // host -> client
        Welcome = 10,
        LoadLevel = 11,
        Snapshot = 12,
        Event = 13,
        Reject = 14,
    }

    public enum LevelKind : byte { Hub = 0, House = 1 }

    public enum PlayerStatus : byte
    {
        Free = 0,
        Carried = 1, // held by the old man
        Trapped = 2, // pickled in a jar on grandpa's shelf
        Dead = 3, // flattened by the fly swatter (a ghost until the hat reaches the yarn basket)
        Home = 4, // left the house (finished the night early)
    }

    public enum ArmMode : byte
    {
        Idle = 0,
        HoldProp = 1, // carrying a prop in the hands: Anchor = prop-local grab point, Hand = hold target
        Climb = 2, // yarn hooked onto the world: Anchor = world hook point, Hand = yarn length in x
        Reach = 3, // hands reaching forward (kick / empty grab)
        YarnProp = 4, // yarn hooked onto a prop: Anchor = prop-local hook point, Hand.x = yarn length
        YarnFly = 5, // yarn hook in flight: Anchor = start, Hand = current hook position
    }

    [Flags]
    public enum PFlags : byte
    {
        None = 0,
        Grounded = 1,
        Crouch = 2,
        Sprint = 4,
        ThirdPerson = 8,
        Struggling = 16,
        ArmsRipped = 32,
    }

    public struct ArmState
    {
        public ArmMode Mode;
        public ushort PropId;
        public V3 Anchor;
        public V3 Hand;

        public void Write(NetWriter w)
        {
            w.U8((byte)Mode);
            if (Mode == ArmMode.Idle) return;
            w.U16(PropId);
            w.Vec(Anchor);
            w.Vec(Hand);
        }

        public static ArmState Read(NetReader r)
        {
            var a = new ArmState { Mode = (ArmMode)r.U8() };
            if (a.Mode == ArmMode.Idle) return a;
            a.PropId = r.U16();
            a.Anchor = r.Vec();
            a.Hand = r.Vec();
            return a;
        }
    }

    /// <summary>Client-authoritative gnome state, sent ~30 Hz (unreliable).</summary>
    public struct PlayerStateMsg
    {
        public V3 Pos, Vel;
        public float Yaw, Pitch;
        public PFlags Flags;
        public ArmState Arms;

        public void Write(NetWriter w)
        {
            w.U8((byte)Msg.PlayerState);
            w.Vec(Pos);
            w.Vec(Vel);
            w.Angle(Yaw);
            w.I16((short)Math.Round(Pitch * 10000f));
            w.U8((byte)Flags);
            Arms.Write(w);
        }

        public static PlayerStateMsg Read(NetReader r) => new PlayerStateMsg
        {
            Pos = r.Vec(),
            Vel = r.Vec(),
            Yaw = r.Angle(),
            Pitch = r.I16() / 10000f,
            Flags = (PFlags)r.U8(),
            Arms = ArmState.Read(r),
        };
    }

    public enum ActionType : byte
    {
        Interact = 1, // Id = mechanism id
        Pocket = 2, // Id = prop id
        DropPockets = 3,
        Throw = 4, // Id = prop, A = velocity
        Punch = 5, // I = target kind (0 prop,1 npc,2 mech,3 furniture), Id, A = point, B = dir
        Struggle = 6,
        Revive = 7,
        GoHome = 8, // I = 1 vote to leave, 0 cancel
        Craft = 9, // I = gear id
        PortalReady = 10, // I = 1/0
        Potion = 11, // A = pos, B = velocity (sleep dust)
        Chat = 12, // S = text
        Honk = 13, // funny gnome noise
        Release = 14, // Id = prop released (for instant host update)
        Tie = 15, // tie yarn: Id = prop A (or 0), I = target kind (0 prop,1 world), A = world point A, B = world point B, S = "propB id" when tying to a prop
        Unscrew = 16, // Id = jar mech index (hold E on a lid)
    }

    public struct ActionMsg
    {
        public ActionType Type;
        public ushort Id;
        public int I;
        public V3 A, B;
        public string S;

        public void Write(NetWriter w)
        {
            w.U8((byte)Msg.Action);
            w.U8((byte)Type);
            w.U16(Id);
            w.I32(I);
            w.Vec(A);
            w.Vec(B);
            w.Str(S ?? "");
        }

        public static ActionMsg Read(NetReader r) => new ActionMsg
        {
            Type = (ActionType)r.U8(),
            Id = r.U16(),
            I = r.I32(),
            A = r.Vec(),
            B = r.Vec(),
            S = r.Str(),
        };
    }

    public struct HelloMsg
    {
        public string Version, Name;
        public byte Hat;

        public void Write(NetWriter w)
        {
            w.U8((byte)Msg.Hello);
            w.Str(Version);
            w.Str(Name);
            w.U8(Hat);
        }

        public static HelloMsg Read(NetReader r) => new HelloMsg { Version = r.Str(), Name = r.Str(), Hat = r.U8() };
    }

    public struct WelcomeMsg
    {
        public byte PlayerId;
        public string Version;

        public void Write(NetWriter w)
        {
            w.U8((byte)Msg.Welcome);
            w.U8(PlayerId);
            w.Str(Version);
        }

        public static WelcomeMsg Read(NetReader r) => new WelcomeMsg { PlayerId = r.U8(), Version = r.Str() };
    }

    public struct PropSpawn
    {
        public ushort Id;
        public string Kind;
        public V3 Pos;
        public Q4 Rot;
    }

    public struct PlayerInfo
    {
        public byte Id;
        public string Name;
        public byte Hat;
        public PlayerStatus Status;
    }

    /// <summary>Sent when a level starts (and to late joiners).</summary>
    public sealed class LoadLevelMsg
    {
        public LevelKind Level;
        public int Seed;
        public int Night;
        public string Save = ""; // SaveData.Serialize() of the host
        public string[] TaskIds = Array.Empty<string>();
        public int[] TaskProgress = Array.Empty<int>();
        public int SpareHats;
        public float TimeLeft;
        public List<PropSpawn> Props = new List<PropSpawn>();
        public List<PlayerInfo> Players = new List<PlayerInfo>();
        public List<ushort> BrokenFurniture = new List<ushort>();

        public void Write(NetWriter w)
        {
            w.U8((byte)Msg.LoadLevel);
            w.U8((byte)Level);
            w.I32(Seed);
            w.I32(Night);
            w.Str(Save);
            w.U8((byte)TaskIds.Length);
            for (int i = 0; i < TaskIds.Length; i++)
            {
                w.Str(TaskIds[i]);
                w.I32(i < TaskProgress.Length ? TaskProgress[i] : 0);
            }
            w.U8((byte)SpareHats);
            w.F32(TimeLeft);
            w.U16((ushort)Props.Count);
            foreach (var p in Props)
            {
                w.U16(p.Id);
                w.Str(p.Kind);
                w.Vec(p.Pos);
                w.Quat(p.Rot);
            }
            w.U8((byte)Players.Count);
            foreach (var p in Players)
            {
                w.U8(p.Id);
                w.Str(p.Name);
                w.U8(p.Hat);
                w.U8((byte)p.Status);
            }
            w.U16((ushort)BrokenFurniture.Count);
            foreach (var f in BrokenFurniture) w.U16(f);
        }

        public static LoadLevelMsg Read(NetReader r)
        {
            var m = new LoadLevelMsg
            {
                Level = (LevelKind)r.U8(),
                Seed = r.I32(),
                Night = r.I32(),
                Save = r.Str(),
            };
            int tc = r.U8();
            m.TaskIds = new string[tc];
            m.TaskProgress = new int[tc];
            for (int i = 0; i < tc; i++)
            {
                m.TaskIds[i] = r.Str();
                m.TaskProgress[i] = r.I32();
            }
            m.SpareHats = r.U8();
            m.TimeLeft = r.F32();
            int pc = r.U16();
            for (int i = 0; i < pc; i++)
                m.Props.Add(new PropSpawn { Id = r.U16(), Kind = r.Str(), Pos = r.Vec(), Rot = r.Quat() });
            int plc = r.U8();
            for (int i = 0; i < plc; i++)
                m.Players.Add(new PlayerInfo { Id = r.U8(), Name = r.Str(), Hat = r.U8(), Status = (PlayerStatus)r.U8() });
            int bf = r.U16();
            for (int i = 0; i < bf; i++) m.BrokenFurniture.Add(r.U16());
            return m;
        }
    }

    public struct PropPose
    {
        public ushort Id;
        public V3 Pos;
        public Q4 Rot;
    }

    public struct PlayerPose
    {
        public byte Id;
        public PlayerStatus Status;
        public V3 Pos;
        public float Yaw, Pitch;
        public PFlags Flags;
        public ArmState Arms;
        public byte Hp;
    }

    public struct NpcPose
    {
        public byte Id;
        public V3 Pos;
        public float Yaw;
        public byte State;
        public byte Anim; // animation-specific (e.g. carried player id + 1)
        public V3 Look; // where it looks / reaches
        public byte Alert; // 0..255 awareness
    }

    /// <summary>World state broadcast by the host (~20 Hz, unreliable).</summary>
    public sealed class SnapshotMsg
    {
        public uint Seq;
        public float Time;
        public float TimeLeft;
        public readonly List<PropPose> Props = new List<PropPose>();
        public readonly List<PlayerPose> Players = new List<PlayerPose>();
        public readonly List<NpcPose> Npcs = new List<NpcPose>();
        public byte[] Mechs = Array.Empty<byte>();

        public void Clear()
        {
            Props.Clear();
            Players.Clear();
            Npcs.Clear();
        }

        public void Write(NetWriter w)
        {
            w.U8((byte)Msg.Snapshot);
            w.I32(unchecked((int)Seq));
            w.F32(Time);
            w.F32(TimeLeft);
            w.U16((ushort)Props.Count);
            foreach (var p in Props)
            {
                w.U16(p.Id);
                w.Vec(p.Pos);
                w.Quat(p.Rot);
            }
            w.U8((byte)Players.Count);
            foreach (var p in Players)
            {
                w.U8(p.Id);
                w.U8((byte)p.Status);
                w.Vec(p.Pos);
                w.Angle(p.Yaw);
                w.I16((short)Math.Round(p.Pitch * 10000f));
                w.U8((byte)p.Flags);
                p.Arms.Write(w);
                w.U8(p.Hp);
            }
            w.U8((byte)Npcs.Count);
            foreach (var n in Npcs)
            {
                w.U8(n.Id);
                w.Vec(n.Pos);
                w.Angle(n.Yaw);
                w.U8(n.State);
                w.U8(n.Anim);
                w.Vec(n.Look);
                w.U8(n.Alert);
            }
            w.U16((ushort)Mechs.Length);
            w.Bytes(Mechs, 0, Mechs.Length);
        }

        public static SnapshotMsg Read(NetReader r)
        {
            var s = new SnapshotMsg { Seq = unchecked((uint)r.I32()), Time = r.F32(), TimeLeft = r.F32() };
            int pc = r.U16();
            for (int i = 0; i < pc; i++) s.Props.Add(new PropPose { Id = r.U16(), Pos = r.Vec(), Rot = r.Quat() });
            int plc = r.U8();
            for (int i = 0; i < plc; i++)
            {
                s.Players.Add(new PlayerPose
                {
                    Id = r.U8(),
                    Status = (PlayerStatus)r.U8(),
                    Pos = r.Vec(),
                    Yaw = r.Angle(),
                    Pitch = r.I16() / 10000f,
                    Flags = (PFlags)r.U8(),
                    Arms = ArmState.Read(r),
                    Hp = r.U8(),
                });
            }
            int nc = r.U8();
            for (int i = 0; i < nc; i++)
                s.Npcs.Add(new NpcPose { Id = r.U8(), Pos = r.Vec(), Yaw = r.Angle(), State = r.U8(), Anim = r.U8(), Look = r.Vec(), Alert = r.U8() });
            int mc = r.U16();
            s.Mechs = new byte[mc];
            for (int i = 0; i < mc; i++) s.Mechs[i] = r.U8();
            return s;
        }
    }

    public enum EvType : byte
    {
        PropRemoved = 1, // Id, P = reason (0 banked,1 flushed,2 pocketed,3 broken,4 despawn)
        PropSpawned = 2, // Id, S = kind, Pos, Rot, Vel
        Sound = 3, // P = sound id, Pos, F = volume
        TaskProgress = 4, // P = task index, I = progress
        Message = 5, // S = loc key, I = param, P = player id (255 = all)
        PlayerStatus = 6, // P = player id, I = status, Id = container mech, F = hp
        Pocket = 7, // P = player id, S = comma separated kinds
        NightEnd = 8, // S = serialized report
        SaveState = 9, // S = SaveData text (hub updates after crafting)
        PlayerJoined = 10, // P = id, S = name, I = hat
        PlayerLeft = 11, // P = id
        FurnitureBroken = 12, // Id = furniture index
        SpareHats = 13, // I = spare hats left
        Chat = 14, // P = id, S = text
        Mech = 15, // Id = mech index, I = state (instant change)
        Banked = 16, // S = kind, I = value (haul notification)
        Honk = 17, // P = player id, Pos
        Tie = 18, // Id = tie id, I = prop A (0 = world), P = unused, S = "propB" (or ""), Pos = world A / local A, Vel = world B / local B, F = length
        Stun = 19, // P = player id, F = seconds
        Untie = 20, // Id = tie id
        Lamp = 21, // Id = furniture index, I = 1 on / 0 off
    }

    public struct EventMsg
    {
        public EvType Type;
        public ushort Id;
        public byte P;
        public int I;
        public float F;
        public V3 Pos, Vel;
        public Q4 Rot;
        public string S;

        public void Write(NetWriter w)
        {
            w.U8((byte)Msg.Event);
            w.U8((byte)Type);
            w.U16(Id);
            w.U8(P);
            w.I32(I);
            w.F32(F);
            w.Vec(Pos);
            w.Vec(Vel);
            w.Quat(Rot);
            w.Str(S ?? "");
        }

        public static EventMsg Read(NetReader r) => new EventMsg
        {
            Type = (EvType)r.U8(),
            Id = r.U16(),
            P = r.U8(),
            I = r.I32(),
            F = r.F32(),
            Pos = r.Vec(),
            Vel = r.Vec(),
            Rot = r.Quat(),
            S = r.Str(),
        };
    }

    /// <summary>Text (de)serialisation of a night report for the NightEnd event.</summary>
    public static class ReportCodec
    {
        public static string Encode(NightReport r)
        {
            var parts = new List<string>
            {
                "n=" + r.Night,
                "done=" + r.TasksDone,
                "pass=" + (r.Passed ? 1 : 0),
                "value=" + r.HaulValue,
                "giggles=" + r.Giggles,
                "chaos=" + r.Chaos,
                "items=" + r.ItemsStolen,
                "caught=" + r.TimesCaught,
                "deaths=" + r.Deaths,
                "potions=" + r.PotionsUsed,
                "fired=" + (r.Fired ? 1 : 0),
                "dawn=" + (r.Dawn ? 1 : 0),
                "haul=" + string.Join(",", r.Haul),
                "tasks=" + string.Join(",", r.TaskIds),
            };
            var done = new string[r.TaskDone.Length];
            for (int i = 0; i < done.Length; i++) done[i] = r.TaskDone[i] ? "1" : "0";
            parts.Add("tdone=" + string.Join(",", done));
            return string.Join(";", parts);
        }

        public static NightReport Decode(string s)
        {
            var r = new NightReport();
            foreach (var part in s.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string k = part.Substring(0, eq), v = part.Substring(eq + 1);
                int.TryParse(v, out int n);
                switch (k)
                {
                    case "n": r.Night = n; break;
                    case "done": r.TasksDone = n; break;
                    case "pass": r.Passed = n == 1; break;
                    case "value": r.HaulValue = n; break;
                    case "giggles": r.Giggles = n; break;
                    case "chaos": r.Chaos = n; break;
                    case "items": r.ItemsStolen = n; break;
                    case "caught": r.TimesCaught = n; break;
                    case "deaths": r.Deaths = n; break;
                    case "potions": r.PotionsUsed = n; break;
                    case "fired": r.Fired = n == 1; break;
                    case "dawn": r.Dawn = n == 1; break;
                    case "haul":
                        var h = v.Split(',');
                        for (int i = 0; i < h.Length && i < 6; i++) int.TryParse(h[i], out r.Haul[i]);
                        break;
                    case "tasks": r.TaskIds = v.Length == 0 ? Array.Empty<string>() : v.Split(','); break;
                    case "tdone":
                        var d = v.Length == 0 ? Array.Empty<string>() : v.Split(',');
                        r.TaskDone = new bool[d.Length];
                        for (int i = 0; i < d.Length; i++) r.TaskDone[i] = d[i] == "1";
                        break;
                }
            }
            return r;
        }
    }
}
