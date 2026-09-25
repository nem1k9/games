using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Gnomes.Core;
using Gnomes.Core.Protocol;
using Gnomes.Core.Rules;
using Gnomes.Net;
using Xunit;

namespace Gnomes.Tests
{
    public class ProtocolTests
    {
        static NetReader Roundtrip(NetWriter w, Msg expected)
        {
            var r = new NetReader(w.ToArray());
            Assert.Equal(expected, (Msg)r.U8());
            return r;
        }

        [Fact]
        public void PrimitivesRoundTrip()
        {
            var w = new NetWriter(4);
            w.U8(200); w.Bool(true); w.U16(65000); w.I16(-1234); w.I32(-123456789); w.F32(3.25f);
            w.Str("Привет, гном!"); w.Vec(new V3(1, -2, 3.5f)); w.Angle(-1f);
            var r = new NetReader(w.ToArray());
            Assert.Equal(200, r.U8());
            Assert.True(r.Bool());
            Assert.Equal(65000, r.U16());
            Assert.Equal(-1234, r.I16());
            Assert.Equal(-123456789, r.I32());
            Assert.Equal(3.25f, r.F32());
            Assert.Equal("Привет, гном!", r.Str());
            Assert.Equal(new V3(1, -2, 3.5f), r.Vec());
            Assert.InRange(r.Angle(), 2 * Math.PI - 1 - 0.001, 2 * Math.PI - 1 + 0.001);
            Assert.True(r.AtEnd);
            Assert.Throws<IndexOutOfRangeException>(() => r.U8());
        }

        [Fact]
        public void QuaternionPackingIsAccurate()
        {
            var q = Q4.AngleAxis(1.2f, new V3(0.3f, 1, -0.2f));
            var w = new NetWriter();
            w.Quat(q);
            var q2 = new NetReader(w.ToArray()).Quat();
            float dot = Math.Abs(q.x * q2.x + q.y * q2.y + q.z * q2.z + q.w * q2.w);
            Assert.True(dot > 0.9999f, "dot " + dot);
        }

        [Fact]
        public void PlayerStateRoundTrip()
        {
            var m = new PlayerStateMsg
            {
                Pos = new V3(10, 1, 20), Vel = new V3(0, -3, 1), Yaw = 2.5f, Pitch = -0.7f, Flags = PFlags.Crouch | PFlags.Grounded,
                Arms = new ArmState { Mode = ArmMode.HoldProp, PropId = 42, Anchor = new V3(0.1f, 0, 0), Hand = new V3(11, 1.5f, 21) },
            };
            var w = new NetWriter();
            m.Write(w);
            var m2 = PlayerStateMsg.Read(Roundtrip(w, Msg.PlayerState));
            Assert.Equal(m.Pos, m2.Pos);
            Assert.Equal(m.Flags, m2.Flags);
            Assert.Equal(ArmMode.HoldProp, m2.Arms.Mode);
            Assert.Equal(42, m2.Arms.PropId);
            Assert.Equal(m.Arms.Hand, m2.Arms.Hand);
            Assert.InRange(m2.Yaw, 2.499f, 2.501f);
            Assert.InRange(m2.Pitch, -0.7001f, -0.6999f);
        }

        [Fact]
        public void SnapshotAndLoadLevelRoundTrip()
        {
            var s = new SnapshotMsg { Seq = 7, Time = 12.5f, TimeLeft = 300, Mechs = new byte[] { 0, 128, 255 } };
            for (ushort i = 0; i < 100; i++) s.Props.Add(new PropPose { Id = i, Pos = new V3(i, 1, 2), Rot = Q4.Yaw(i * 0.1f) });
            s.Players.Add(new PlayerPose { Id = 1, Status = PlayerStatus.Trapped, Pos = new V3(1, 2, 3), Hp = 77, Arms = new ArmState { Mode = ArmMode.Climb, Anchor = new V3(5, 5, 5), Hand = new V3(4, 4, 4) } });
            s.Npcs.Add(new NpcPose { Id = 0, Pos = new V3(3, 0, 3), Yaw = 1, State = 4, Anim = 2, Alert = 200 });
            var w = new NetWriter();
            s.Write(w);
            var s2 = SnapshotMsg.Read(Roundtrip(w, Msg.Snapshot));
            Assert.Equal(7u, s2.Seq);
            Assert.Equal(100, s2.Props.Count);
            Assert.Equal(new V3(99, 1, 2), s2.Props[99].Pos);
            Assert.Equal(PlayerStatus.Trapped, s2.Players[0].Status);
            Assert.Equal(77, s2.Players[0].Hp);
            Assert.Equal(ArmMode.Climb, s2.Players[0].Arms.Mode);
            Assert.Equal(200, s2.Npcs[0].Alert);
            Assert.Equal(new byte[] { 0, 128, 255 }, s2.Mechs);
            Assert.True(w.Length < 100 * 22 + 200, "snapshot too big: " + w.Length);

            var l = new LoadLevelMsg { Level = LevelKind.House, Seed = 99, Night = 3, Save = new SaveData { Night = 3 }.Serialize(), TaskIds = new[] { "flush", "loot30" }, TaskProgress = new[] { 0, 12 }, Spores = 2, TimeLeft = 400 };
            l.Props.Add(new PropSpawn { Id = 5, Kind = "dentures", Pos = new V3(1, 2, 3), Rot = Q4.identity });
            l.Players.Add(new PlayerInfo { Id = 0, Name = "Хост", Hat = 2, Status = PlayerStatus.Free });
            l.BrokenFurniture.Add(3);
            w = new NetWriter();
            l.Write(w);
            var l2 = LoadLevelMsg.Read(Roundtrip(w, Msg.LoadLevel));
            Assert.Equal(LevelKind.House, l2.Level);
            Assert.Equal(3, SaveData.Deserialize(l2.Save).Night);
            Assert.Equal(new[] { "flush", "loot30" }, l2.TaskIds);
            Assert.Equal(12, l2.TaskProgress[1]);
            Assert.Equal("dentures", l2.Props[0].Kind);
            Assert.Equal("Хост", l2.Players[0].Name);
            Assert.Equal(new ushort[] { 3 }, l2.BrokenFurniture);
        }

        [Fact]
        public void EventsActionsAndReportsRoundTrip()
        {
            var e = new EventMsg { Type = EvType.PropSpawned, Id = 9, P = 3, I = -5, F = 0.5f, Pos = new V3(1, 1, 1), Vel = new V3(0, 5, 0), Rot = Q4.identity, S = "coins" };
            var w = new NetWriter();
            e.Write(w);
            var e2 = EventMsg.Read(Roundtrip(w, Msg.Event));
            Assert.Equal(EvType.PropSpawned, e2.Type);
            Assert.Equal("coins", e2.S);
            Assert.Equal(new V3(0, 5, 0), e2.Vel);

            var a = new ActionMsg { Type = ActionType.Throw, Id = 17, A = new V3(0, 3, 9), S = null };
            w = new NetWriter();
            a.Write(w);
            var a2 = ActionMsg.Read(Roundtrip(w, Msg.Action));
            Assert.Equal(ActionType.Throw, a2.Type);
            Assert.Equal(17, a2.Id);
            Assert.Equal("", a2.S);

            var rep = new NightReport { Night = 2, TaskIds = new[] { "flush", "breakTv" }, TaskDone = new[] { true, false }, TasksDone = 1, Passed = false, HaulValue = 33, Gnomium = 2, Fired = true };
            rep.Haul[3] = 7;
            var rep2 = ReportCodec.Decode(ReportCodec.Encode(rep));
            Assert.Equal(2, rep2.Night);
            Assert.Equal(new[] { "flush", "breakTv" }, rep2.TaskIds);
            Assert.Equal(new[] { true, false }, rep2.TaskDone);
            Assert.Equal(33, rep2.HaulValue);
            Assert.Equal(7, rep2.Haul[3]);
            Assert.True(rep2.Fired);
        }
    }

    public class TransportTests
    {
        static void PumpUntil(Func<bool> cond, IEnumerable<INetTransport> ts, int ms = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (!cond() && sw.ElapsedMilliseconds < ms)
            {
                foreach (var t in ts) t.Poll();
                Thread.Sleep(5);
            }
        }

        [Fact]
        public void HostAndTwoClientsExchangeMessages()
        {
            int port = 30000 + new Random().Next(5000);
            using var server = LnlTransport.StartServer(port);
            using var c1 = LnlTransport.Connect("127.0.0.1", port);
            using var c2 = LnlTransport.Connect("127.0.0.1", port);
            var all = new INetTransport[] { server, c1, c2 };
            var serverPeers = new List<int>();
            server.PeerConnected += id => serverPeers.Add(id);
            bool c1Up = false, c2Up = false;
            c1.PeerConnected += _ => c1Up = true;
            c2.PeerConnected += _ => c2Up = true;
            PumpUntil(() => serverPeers.Count == 2 && c1Up && c2Up, all);
            Assert.Equal(2, serverPeers.Count);

            // client -> host: hello
            var got = new List<(int peer, string name)>();
            server.Data += (peer, data, off, len) =>
            {
                var r = new NetReader(data, off, len);
                if ((Msg)r.U8() == Msg.Hello) got.Add((peer, HelloMsg.Read(r).Name));
            };
            var w = new NetWriter();
            new HelloMsg { Version = GameConsts.GameVersion, Name = "Бородач", Hat = 1 }.Write(w);
            c1.Send(0, w.Buffer, w.Length, true);
            PumpUntil(() => got.Count == 1, all);
            Assert.Single(got);
            Assert.Equal("Бородач", got[0].name);

            // host -> all: big snapshot (fragmentation is only allowed on reliable, keep unreliable small)
            int c1Snaps = 0, c2Events = 0;
            c1.Data += (p, d, o, l) => { if ((Msg)d[o] == Msg.Snapshot) c1Snaps++; };
            c2.Data += (p, d, o, l) => { if ((Msg)d[o] == Msg.Event) c2Events++; };
            var snap = new SnapshotMsg();
            for (ushort i = 0; i < 20; i++) snap.Props.Add(new PropPose { Id = i, Pos = new V3(i, 0, 0), Rot = Q4.identity });
            w.Reset();
            snap.Write(w);
            server.SendToAll(w.Buffer, w.Length, false);
            var ev = new NetWriter();
            new EventMsg { Type = EvType.Message, S = "taskDone" }.Write(ev);
            server.SendToAll(ev.Buffer, ev.Length, true);
            PumpUntil(() => c1Snaps >= 1 && c2Events >= 1, all);
            Assert.True(c1Snaps >= 1);
            Assert.Equal(1, c2Events);

            // a reliable message bigger than the MTU must arrive intact (level load with many props)
            var load = new LoadLevelMsg { Level = LevelKind.House, Seed = 5 };
            for (ushort i = 0; i < 300; i++) load.Props.Add(new PropSpawn { Id = i, Kind = "plate", Pos = new V3(i, 0, 0), Rot = Q4.identity });
            var lw = new NetWriter();
            load.Write(lw);
            LoadLevelMsg received = null;
            c2.Data += (p, d, o, l) =>
            {
                var r = new NetReader(d, o, l);
                if ((Msg)r.U8() == Msg.LoadLevel) received = LoadLevelMsg.Read(r);
            };
            server.Send(serverPeers[1], lw.Buffer, lw.Length, true);
            PumpUntil(() => received != null, all);
            Assert.NotNull(received);
            Assert.Equal(300, received.Props.Count);

            // disconnect is noticed by the host
            var left = new List<int>();
            server.PeerDisconnected += (id, reason) => left.Add(id);
            c1.Dispose();
            PumpUntil(() => left.Count == 1, all.Where(t => t != c1), 12000);
            Assert.Single(left);
        }

        [Fact]
        public void LanDiscoveryFindsHostByDirectQuery()
        {
            int port = 35000 + new Random().Next(5000);
            using var server = LnlTransport.StartServer(port);
            server.DiscoveryInfo = () => "Хитрый хост";
            using var disc = new LanDiscovery(port);
            var sw = Stopwatch.StartNew();
            while (!disc.Hosts.Any() && sw.ElapsedMilliseconds < 4000)
            {
                disc.Ask("127.0.0.1");
                disc.Ping();
                for (int i = 0; i < 10; i++)
                {
                    server.Poll();
                    disc.Poll();
                    Thread.Sleep(10);
                }
            }
            var h = disc.Hosts.FirstOrDefault();
            Assert.Equal("Хитрый хост", h.Info);
            Assert.Equal(1, h.Players);
        }
    }
}
