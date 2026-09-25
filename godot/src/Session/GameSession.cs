using System;
using System.Collections.Generic;
using Gnomes.Core;
using Gnomes.Core.Protocol;
using Gnomes.Core.Rules;
using Gnomes.Net;
using Godot;
using SockGang.App;
using SockGang.Audio;
using SockGang.NPC;
using SockGang.Players;
using SockGang.World;

namespace SockGang.Session
{
    /// <summary>Everything the host knows about one player.</summary>
    public class PlayerSlot
    {
        public byte Id;
        public string Name = "Гном";
        public byte Hat;
        public int Peer = -1; // -1 = the host's own player
        public PlayerStatus Status;
        public float Hp = GameConsts.MaxHealth;
        public readonly List<string> Pocket = new List<string>();
        public PlayerStateMsg State;
        public float StateTime;
        public bool HasState;
        public int Struggles;
        public int JarMech = -1;
        public float TrapTime;
        public bool PortalReady;
        public ushort HatProp;
        public float DeadTime;
        public float StepAccum;
        public Vector3 LastPos;
        public float LastVy;
        public float CreakCooldown;
        public Color Color => GameSession.HatColor(Hat);
    }

    /// <summary>
    /// Runs a game: solo (host without network), host (listen server) or client.
    /// Owns the transport, the player list, level loading and state replication.
    /// Network positions are in the shared (Unity) space: convert with <see cref="Conv"/>.
    /// </summary>
    public class GameSession
    {
        public static GameSession I;

        public bool IsHost, IsSolo;
        public byte LocalId;
        public INetTransport Net;
        public readonly Dictionary<byte, PlayerSlot> Players = new Dictionary<byte, PlayerSlot>();
        public SaveData Save = new SaveData();
        public HostLogic Host;
        public LevelKind Level;
        public int Seed, Night;
        public NightReport LastReport;
        public string Error;
        public bool Connected;
        public float TimeLeft;
        public TaskTracker ClientTasks;
        public int SpareHats;

        readonly NetWriter writer = new NetWriter(2048);
        readonly SnapshotMsg snapshot = new SnapshotMsg();
        uint snapSeq;
        float snapTimer;
        float timeOffset;
        bool haveOffset;
        public const float InterpDelay = 0.12f;
        float connectStart;
        readonly Dictionary<int, byte> peerToPlayer = new Dictionary<int, byte>();

        static readonly Color[] Hats =
        {
            Conv.C8(216, 52, 44), Conv.C8(47, 111, 214), Conv.C8(58, 168, 69), Conv.C8(226, 178, 30),
            Conv.C8(155, 68, 201), Conv.C8(239, 122, 36), Conv.C8(231, 154, 168), Conv.C8(40, 170, 170),
        };

        public static Color HatColor(byte i) => Hats[(i & 0x7f) % Hats.Length];

        /// <summary>The secret rainbow hat (found with the Konami code in the menu) rides on the high bit.</summary>
        public const byte RainbowFlag = 0x80;
        public static bool IsRainbow(byte hat) => (hat & RainbowFlag) != 0;

        /// <summary>Hue cycling in 36 steps (each step's material is cached and shared).</summary>
        public static Color RainbowColor(float t) => Color.FromHsv(Mathf.Floor(Mathf.PosMod(t * 0.2f, 1f) * 36f) / 36f, 0.72f, 0.95f);

        /// <summary>Hat details a gnome's avatar needs besides the colour (the rainbow animates).</summary>
        public static void DressGnome(GameWorld w, byte id, byte hat)
        {
            if (w != null && w.Gnomes.TryGetValue(id, out var g) && g.Avatar != null) g.Avatar.Rainbow = IsRainbow(hat);
        }
        public static int HatCount => Hats.Length;

        public PlayerSlot LocalSlot => Players.TryGetValue(LocalId, out var s) ? s : null;
        public float HostTime => IsHost ? Clock.Now : Clock.Now + timeOffset;
        public float RenderTime => HostTime - (IsHost ? 0f : InterpDelay);

        // ------------------------------------------------------------------ lifecycle

        public static GameSession StartSolo(string name, byte hat, SaveData save)
        {
            var s = new GameSession { IsHost = true, IsSolo = true, LocalId = 0, Save = save };
            I = s;
            s.Players[0] = new PlayerSlot { Id = 0, Name = name, Hat = hat };
            s.Host = new HostLogic(s);
            s.Host.GoToHub();
            return s;
        }

        public static GameSession StartHost(string name, byte hat, SaveData save, int port)
        {
            var s = new GameSession { IsHost = true, IsSolo = false, LocalId = 0, Save = save };
            I = s;
            var t = LnlTransport.StartServer(port);
            t.DiscoveryInfo = () => $"{name} — {s.Players.Count}/{GameConsts.MaxPlayers}";
            s.Net = t;
            t.PeerConnected += s.OnPeerConnected;
            t.PeerDisconnected += s.OnPeerDisconnected;
            t.Data += s.OnData;
            s.Players[0] = new PlayerSlot { Id = 0, Name = name, Hat = hat };
            s.Host = new HostLogic(s);
            s.Host.GoToHub();
            return s;
        }

        public static GameSession StartClient(string name, byte hat, string address, int port)
        {
            var s = new GameSession { IsHost = false, IsSolo = false, Save = new SaveData() };
            I = s;
            var t = LnlTransport.Connect(address, port);
            s.Net = t;
            s.connectStart = Clock.Now;
            t.PeerConnected += _ =>
            {
                var w = s.writer;
                w.Reset();
                new HelloMsg { Version = GameConsts.GameVersion, Name = name, Hat = hat }.Write(w);
                s.Net.Send(0, w.Buffer, w.Length, true);
            };
            t.PeerDisconnected += (_, reason) =>
            {
                s.Error = Loc.T("disconnected") + " (" + reason + ")";
                GameApp.I?.OnSessionLost(s.Error);
            };
            t.Data += s.OnData;
            return s;
        }

        public void Shutdown()
        {
            try { Net?.Dispose(); } catch (Exception e) { GD.PushWarning(e.Message); }
            Net = null;
            var w = GameWorld.Current;
            if (w != null && GodotObject.IsInstanceValid(w))
            {
                w.GetParent()?.RemoveChild(w);
                w.QueueFree();
            }
            GameWorld.Current = null;
            if (I == this) I = null;
        }

        // ------------------------------------------------------------------ per-frame

        public void Update(float dt)
        {
            Net?.Poll();
            if (!IsHost && !Connected && Clock.Now - connectStart > 12f && Error == null)
            {
                Error = Loc.T("disconnected");
                GameApp.I?.OnSessionLost(Error);
                return;
            }
            var w = GameWorld.Current;
            if (IsHost)
            {
                Host.Update(dt);
                snapTimer += dt;
                if (Net != null && snapTimer >= 1f / GameConsts.SnapshotHz)
                {
                    snapTimer = 0;
                    SendSnapshot();
                }
                if (w != null)
                    foreach (var g in w.Gnomes.Values)
                        if (g is RemoteGnome rg && rg.Status == PlayerStatus.Free) rg.Tick(Clock.Now);
            }
            else if (w != null)
            {
                float rt = RenderTime;
                foreach (var p in w.Props.Values) p.Interpolate(rt);
                foreach (var g in w.Gnomes.Values)
                    if (g is RemoteGnome rg) rg.Tick(HostTime);
                w.OldMan?.ClientTick(rt);
                w.Cat?.ClientTick(rt);
                TimeLeft = Mathf.Max(0, TimeLeft - dt);
            }
        }

        public void PhysicsUpdate(float dt)
        {
            if (IsHost) Host.PhysicsUpdate(dt);
        }

        // ------------------------------------------------------------------ sending

        public void Broadcast(EventMsg e)
        {
            if (!IsHost || Net == null) return;
            writer.Reset();
            e.Write(writer);
            Net.SendToAll(writer.Buffer, writer.Length, true);
        }

        public void SendEventTo(PlayerSlot p, EventMsg e)
        {
            if (!IsHost) return;
            if (p.Peer < 0)
            {
                ApplyEvent(e, true);
                return;
            }
            writer.Reset();
            e.Write(writer);
            Net?.Send(p.Peer, writer.Buffer, writer.Length, true);
        }

        public void SendAction(ActionMsg a)
        {
            if (IsHost)
            {
                Host.HandleAction(LocalId, a);
                return;
            }
            if (Net == null) return;
            writer.Reset();
            a.Write(writer);
            Net.Send(0, writer.Buffer, writer.Length, true);
        }

        public void SubmitLocalState(LocalGnome g)
        {
            var st = new PlayerStateMsg
            {
                Pos = g.GlobalPosition.U(),
                Vel = g.Velocity.U(),
                Yaw = g.Yaw,
                Pitch = g.Pitch,
                Flags = g.Flags | (g.ThirdPerson ? PFlags.ThirdPerson : 0),
                Arms = g.Arms,
            };
            if (IsHost)
            {
                if (Players.TryGetValue(LocalId, out var slot))
                {
                    slot.State = st;
                    slot.StateTime = Clock.Now;
                    slot.HasState = true;
                }
                return;
            }
            if (Net == null || !Connected) return;
            writer.Reset();
            st.Write(writer);
            Net.Send(0, writer.Buffer, writer.Length, false);
        }

        public void SendLoadLevel(LoadLevelMsg msg, PlayerSlot only = null)
        {
            if (Net == null) return;
            var w = new NetWriter(8192);
            msg.Write(w);
            if (only != null) Net.Send(only.Peer, w.Buffer, w.Length, true);
            else Net.SendToAll(w.Buffer, w.Length, true);
        }

        void SendSnapshot()
        {
            var w = GameWorld.Current;
            if (w == null) return;
            var s = snapshot;
            s.Clear();
            s.Seq = ++snapSeq;
            s.Time = Clock.Now;
            s.TimeLeft = w.Night != null ? w.Night.TimeLeft : 0;
            bool full = snapSeq % (GameConsts.SnapshotHz * 2) == 0; // refresh sleeping props every 2 s
            foreach (var p in w.Props.Values)
            {
                if (!full && p.Sleeping) continue;
                s.Props.Add(new PropPose { Id = p.Id, Pos = p.GlobalPosition.U(), Rot = p.Quaternion.U() });
            }
            foreach (var slot in Players.Values)
            {
                if (!w.Gnomes.TryGetValue(slot.Id, out var g)) continue;
                s.Players.Add(new PlayerPose
                {
                    Id = slot.Id,
                    Status = slot.Status,
                    Pos = g.GlobalPosition.U(),
                    Yaw = g.Yaw,
                    Pitch = g.Pitch,
                    Flags = g.Flags,
                    Arms = g.Arms,
                    Hp = (byte)Mathf.Clamp(slot.Hp, 0, 255),
                });
            }
            w.OldMan?.WritePose(s.Npcs);
            w.Cat?.WritePose(s.Npcs);
            w.Parrot?.WritePose(s.Npcs);
            if (s.Mechs.Length != w.Mechs.Count) s.Mechs = new byte[w.Mechs.Count];
            for (int i = 0; i < w.Mechs.Count; i++) s.Mechs[i] = w.Mechs[i].NetState;
            writer.Reset();
            s.Write(writer);
            // snapshots bigger than one packet go reliable (LiteNetLib only fragments reliable messages)
            Net.SendToAll(writer.Buffer, writer.Length, writer.Length > 1100);
        }

        // ------------------------------------------------------------------ receiving

        void OnPeerConnected(int peer)
        {
            // wait for Hello
        }

        void OnPeerDisconnected(int peer, string reason)
        {
            if (!peerToPlayer.TryGetValue(peer, out var id)) return;
            peerToPlayer.Remove(peer);
            if (Players.TryGetValue(id, out var slot))
            {
                Players.Remove(id);
                Host?.OnPlayerLeft(slot);
                Broadcast(new EventMsg { Type = EvType.PlayerLeft, P = id, S = slot.Name });
                GameApp.I?.Toast(slot.Name + " " + Loc.T("playerLeft"));
            }
        }

        void OnData(int peer, byte[] data, int offset, int count)
        {
            NetReader r;
            Msg type;
            try
            {
                r = new NetReader(data, offset, count);
                type = (Msg)r.U8();
            }
            catch
            {
                return;
            }
            try
            {
                if (IsHost) HostReceive(peer, type, r);
                else ClientReceive(type, r);
            }
            catch (Exception e)
            {
                GD.PushWarning($"[Net] bad {type} message: {e.Message}");
            }
        }

        void HostReceive(int peer, Msg type, NetReader r)
        {
            if (type == Msg.Hello)
            {
                var hello = HelloMsg.Read(r);
                if (peerToPlayer.ContainsKey(peer)) return;
                byte id = 1;
                while (Players.ContainsKey(id)) id++;
                var slot = new PlayerSlot { Id = id, Name = string.IsNullOrWhiteSpace(hello.Name) ? "Гном" + id : hello.Name.Trim(), Hat = hello.Hat, Peer = peer };
                var used = new HashSet<byte>();
                foreach (var p in Players.Values) used.Add((byte)((p.Hat & 0x7f) % HatCount));
                byte rainbow = (byte)(slot.Hat & RainbowFlag);
                for (int k = 0; k < HatCount && used.Contains((byte)((slot.Hat & 0x7f) % HatCount)); k++) slot.Hat = (byte)(((slot.Hat & 0x7f) + 1) % HatCount | rainbow);
                Players[id] = slot;
                peerToPlayer[peer] = id;
                writer.Reset();
                new WelcomeMsg { PlayerId = id, Version = GameConsts.GameVersion }.Write(writer);
                Net.Send(peer, writer.Buffer, writer.Length, true);
                Host.OnPlayerJoined(slot);
                GameApp.I?.Toast(slot.Name + " " + Loc.T("playerJoined"));
                return;
            }
            if (!peerToPlayer.TryGetValue(peer, out var pid) || !Players.TryGetValue(pid, out var sl)) return;
            switch (type)
            {
                case Msg.PlayerState:
                    var st = PlayerStateMsg.Read(r);
                    sl.State = st;
                    sl.StateTime = Clock.Now;
                    sl.HasState = true;
                    if (sl.Status == PlayerStatus.Free && GameWorld.Current != null && GameWorld.Current.Gnomes.TryGetValue(pid, out var g) && g is RemoteGnome rg)
                        rg.Push(Clock.Now, st.Pos.G(), st.Yaw, st.Pitch, st.Flags, st.Arms, st.Vel.G());
                    break;
                case Msg.Action:
                    Host.HandleAction(pid, ActionMsg.Read(r));
                    break;
            }
        }

        void ClientReceive(Msg type, NetReader r)
        {
            switch (type)
            {
                case Msg.Welcome:
                    var wm = WelcomeMsg.Read(r);
                    LocalId = wm.PlayerId;
                    Connected = true;
                    break;
                case Msg.LoadLevel:
                    ClientLoadLevel(LoadLevelMsg.Read(r));
                    break;
                case Msg.Snapshot:
                    ClientSnapshot(SnapshotMsg.Read(r));
                    break;
                case Msg.Event:
                    ApplyEvent(EventMsg.Read(r), false);
                    break;
            }
        }

        // ------------------------------------------------------------------ client side

        void ClientLoadLevel(LoadLevelMsg m)
        {
            Level = m.Level;
            Seed = m.Seed;
            Night = m.Night;
            Save = SaveData.Deserialize(m.Save);
            SpareHats = m.SpareHats;
            TimeLeft = m.TimeLeft;
            ClientTasks = TaskTracker.FromIds(m.TaskIds, m.TaskProgress);
            Players.Clear();
            foreach (var p in m.Players) Players[p.Id] = new PlayerSlot { Id = p.Id, Name = p.Name, Hat = p.Hat, Status = p.Status };
            var world = WorldLoader.Build(m.Level, m.Seed, false, this);
            foreach (var ps in m.Props) world.SpawnProp(ps.Id, ps.Kind, ps.Pos.G(), ps.Rot.G());
            foreach (var f in m.BrokenFurniture) if (f < world.Furniture.Count) world.Furniture[f].SetBroken();
            SpawnGnomes(world);
            haveOffset = false;
            GameApp.I?.OnLevelLoaded(m.Level);
        }

        /// <summary>Spawn gnome objects for all players (the local one controllable).</summary>
        public void SpawnGnomes(GameWorld world)
        {
            int i = 0;
            foreach (var p in Players.Values)
            {
                var pos = world.SpawnPoint(i++);
                float yaw = Conv.Yaw(Mathf.Pi); // face into the level: the house interior / the village and the Great Sock
                if (p.Id == LocalId) LocalGnome.Create(world, p.Id, p.Name, HatColor(p.Hat), pos, yaw);
                else RemoteGnome.Create(world, p.Id, p.Name, HatColor(p.Hat), pos);
                DressGnome(world, p.Id, p.Hat);
                if (world.Gnomes.TryGetValue(p.Id, out var g))
                {
                    g.Status = p.Status;
                    if (g is LocalGnome lg) lg.OnStatus(p.Status);
                }
            }
        }

        void ClientSnapshot(SnapshotMsg s)
        {
            var w = GameWorld.Current;
            if (w == null) return;
            float off = s.Time - Clock.Now;
            if (!haveOffset || Mathf.Abs(off - timeOffset) > 1f)
            {
                timeOffset = off;
                haveOffset = true;
            }
            else timeOffset = Mathf.Lerp(timeOffset, off, 0.05f);
            TimeLeft = s.TimeLeft;
            foreach (var pp in s.Props)
            {
                var p = w.GetProp(pp.Id);
                p?.PushPose(s.Time, pp.Pos.G(), pp.Rot.G());
            }
            foreach (var pl in s.Players)
            {
                if (Players.TryGetValue(pl.Id, out var slot))
                {
                    slot.Status = pl.Status;
                    slot.Hp = pl.Hp;
                }
                if (!w.Gnomes.TryGetValue(pl.Id, out var g)) continue;
                g.Hp = pl.Hp;
                if (g is LocalGnome lg)
                {
                    lg.OnStatus(pl.Status);
                    if (pl.Status != PlayerStatus.Free) lg.ForcedPos = pl.Pos.G();
                }
                else if (g is RemoteGnome rg)
                {
                    rg.Status = pl.Status;
                    rg.Push(s.Time, pl.Pos.G(), pl.Yaw, pl.Pitch, pl.Flags, pl.Arms, Vector3.Zero);
                }
            }
            foreach (var n in s.Npcs)
            {
                if (n.Id == NpcBase.OldManId) w.OldMan?.PushPose(s.Time, n);
                else if (n.Id == NpcBase.CatId) w.Cat?.PushPose(s.Time, n);
                else if (n.Id == NpcBase.ParrotId) w.Parrot?.PushPose(s.Time, n);
            }
            for (int i = 0; i < s.Mechs.Length && i < w.Mechs.Count; i++) w.Mechs[i].ApplyNet(s.Mechs[i]);
        }

        /// <summary>Apply a reliable event (clients; also used for the host's own player).</summary>
        public void ApplyEvent(EventMsg e, bool local)
        {
            var w = GameWorld.Current;
            switch (e.Type)
            {
                case EvType.PropRemoved:
                    if (!local) w?.RemoveProp(e.Id, e.P);
                    break;
                case EvType.PropSpawned:
                    if (!local && w != null)
                    {
                        var p = w.SpawnProp(e.Id, e.S, e.Pos.G(), e.Rot.G());
                        if (e.P > 0) p.Model.SetTint(HatColor((byte)(e.P - 1))); // a fallen gnome's hat
                    }
                    break;
                case EvType.Sound:
                    if (!local)
                    {
                        Sfx.I?.Play((SoundId)e.P, e.Pos.G(), e.F < 0 ? 1f : e.F);
                        if (e.F < 0 && e.P == (byte)SoundId.Break) Fx.Shards(w, e.Pos.G());
                    }
                    break;
                case EvType.TaskProgress:
                    if (!local && ClientTasks != null && e.P < ClientTasks.Progress.Length)
                    {
                        bool wasDone = ClientTasks.IsDone(e.P);
                        ClientTasks.Progress[e.P] = e.I;
                        if (!wasDone && ClientTasks.IsDone(e.P)) GameApp.I?.OnTaskDone(ClientTasks.Tasks[e.P]);
                    }
                    break;
                case EvType.Message:
                    if (e.P == 255 || e.P == LocalId) GameApp.I?.Toast(Loc.Has(e.S) ? Loc.T(e.S) : e.S);
                    break;
                case EvType.PlayerStatus:
                    if (Players.TryGetValue(e.P, out var ps))
                    {
                        ps.Status = (PlayerStatus)e.I;
                        ps.Hp = e.F;
                    }
                    if (w != null && w.Gnomes.TryGetValue(e.P, out var gg))
                    {
                        gg.Status = (PlayerStatus)e.I;
                        if (gg is LocalGnome lg)
                        {
                            lg.OnStatus((PlayerStatus)e.I);
                            if (e.Id == 1) lg.Teleport(e.Pos.G(), lg.Yaw);
                        }
                        else if (gg is RemoteGnome rg && e.Id == 1) rg.SnapTo(e.Pos.G());
                    }
                    break;
                case EvType.Pocket:
                    if (Players.TryGetValue(e.P, out var pp))
                    {
                        pp.Pocket.Clear();
                        if (!string.IsNullOrEmpty(e.S)) pp.Pocket.AddRange(e.S.Split(','));
                    }
                    break;
                case EvType.NightEnd:
                    LastReport = ReportCodec.Decode(e.S);
                    GameApp.I?.OnNightEnd(LastReport);
                    break;
                case EvType.SaveState:
                    Save = SaveData.Deserialize(e.S);
                    LocalGnome.I?.RefreshGear();
                    break;
                case EvType.PlayerJoined:
                    if (!Players.ContainsKey(e.P))
                    {
                        Players[e.P] = new PlayerSlot { Id = e.P, Name = e.S, Hat = (byte)e.I };
                        if (w != null && e.P != LocalId && !w.Gnomes.ContainsKey(e.P)) RemoteGnome.Create(w, e.P, e.S, HatColor((byte)e.I), w.SpawnPoint(e.P));
                        DressGnome(w, e.P, (byte)e.I);
                        GameApp.I?.Toast(e.S + " " + Loc.T("playerJoined"));
                    }
                    break;
                case EvType.PlayerLeft:
                    Players.Remove(e.P);
                    if (w != null && w.Gnomes.TryGetValue(e.P, out var left))
                    {
                        w.Gnomes.Remove(e.P);
                        left.QueueFree();
                    }
                    break;
                case EvType.FurnitureBroken:
                    if (!local && w != null && e.Id < w.Furniture.Count) w.Furniture[e.Id].SetBroken();
                    break;
                case EvType.SpareHats:
                    SpareHats = e.I;
                    break;
                case EvType.Mech:
                    if (!local && w != null && e.Id < w.Mechs.Count) w.Mechs[e.Id].ApplyNet((byte)(e.I != 0 ? 255 : 0));
                    break;
                case EvType.Banked:
                    GameApp.I?.OnBanked(e.S, e.I);
                    break;
                case EvType.Honk:
                    if (!local) Sfx.I?.Play(SoundId.Honk, e.Pos.G(), 1f);
                    break;
                case EvType.Tie:
                    if (!local && w != null) w.Ties.ApplyNet(e);
                    break;
                case EvType.Untie:
                    if (!local && w != null) w.Ties.Remove(e.Id, false);
                    break;
                case EvType.Stun:
                    if (e.P == LocalId) LocalGnome.I?.Stun(e.F);
                    break;
                case EvType.Lamp:
                    if (!local && w != null && e.Id < w.Furniture.Count) w.Furniture[e.Id].SetLights(e.I != 0);
                    break;
                case EvType.Chat:
                    GameApp.I?.Toast(e.S);
                    break;
            }
        }
    }
}
