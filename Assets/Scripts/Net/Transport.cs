using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Gnomes.Core;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Gnomes.Net
{
    /// <summary>Minimal transport abstraction so the game never talks to LiteNetLib directly.</summary>
    public interface INetTransport : IDisposable
    {
        bool IsServer { get; }
        bool IsRunning { get; }
        /// <summary>Server: peer id; client: the server is always peer 0.</summary>
        event Action<int> PeerConnected;
        event Action<int, string> PeerDisconnected;
        event Action<int, byte[], int, int> Data;
        void Poll();
        void Send(int peer, byte[] data, int length, bool reliable);
        void SendToAll(byte[] data, int length, bool reliable, int exceptPeer = -1);
        void Disconnect(int peer);
        int PingMs(int peer);
        IEnumerable<int> Peers { get; }
    }

    public static class NetConst
    {
        public const string ConnectionKey = "sneaky-gnomes-v1";
        public const int ProtocolVersion = 1;
        public const string DiscoveryMagic = "GNOME?";
        public const string DiscoveryReply = "GNOME!";
    }

    public static class NetInfo
    {
        public const int ProtocolVersion = NetConst.ProtocolVersion;
    }

    /// <summary>LiteNetLib host or client.</summary>
    public sealed class LnlTransport : INetTransport, INetEventListener
    {
        readonly NetManager manager;
        readonly Dictionary<int, NetPeer> peers = new Dictionary<int, NetPeer>();
        readonly int maxPeers;
        NetPeer serverPeer;

        public bool IsServer { get; }
        public bool IsRunning => manager.IsRunning;
        public int Port => manager.LocalPort;
        public IEnumerable<int> Peers => peers.Keys;
        /// <summary>Server only: extra info returned to LAN discovery (host name, players...).</summary>
        public Func<string> DiscoveryInfo;

        public event Action<int> PeerConnected;
        public event Action<int, string> PeerDisconnected;
        public event Action<int, byte[], int, int> Data;

        LnlTransport(bool server, int maxPeers)
        {
            IsServer = server;
            this.maxPeers = maxPeers;
            manager = new NetManager(this)
            {
                AutoRecycle = true,
                UnconnectedMessagesEnabled = true,
                BroadcastReceiveEnabled = server,
                DisconnectTimeout = 10000,
                UpdateTime = 10,
                IPv6Enabled = false,
                ChannelsCount = 2,
            };
        }

        public static LnlTransport StartServer(int port, int maxPeers = GameConsts.MaxPlayers - 1)
        {
            var t = new LnlTransport(true, maxPeers);
            if (!t.manager.Start(port)) throw new SocketException((int)SocketError.AddressAlreadyInUse);
            return t;
        }

        public static LnlTransport Connect(string host, int port)
        {
            var t = new LnlTransport(false, 1);
            t.manager.Start();
            t.serverPeer = t.manager.Connect(host, port, NetConst.ConnectionKey);
            return t;
        }

        public void Poll() => manager.PollEvents();

        public void Send(int peer, byte[] data, int length, bool reliable)
        {
            var dm = reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Sequenced;
            byte ch = reliable ? (byte)0 : (byte)1;
            if (!IsServer)
            {
                if (serverPeer != null && serverPeer.ConnectionState == ConnectionState.Connected) serverPeer.Send(data, 0, length, ch, dm);
                return;
            }
            if (peers.TryGetValue(peer, out var p)) p.Send(data, 0, length, ch, dm);
        }

        public void SendToAll(byte[] data, int length, bool reliable, int exceptPeer = -1)
        {
            var dm = reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Sequenced;
            byte ch = reliable ? (byte)0 : (byte)1;
            if (!IsServer)
            {
                Send(0, data, length, reliable);
                return;
            }
            foreach (var kv in peers)
                if (kv.Key != exceptPeer) kv.Value.Send(data, 0, length, ch, dm);
        }

        public void Disconnect(int peer)
        {
            if (IsServer && peers.TryGetValue(peer, out var p)) manager.DisconnectPeer(p);
            else if (!IsServer) manager.DisconnectAll();
        }

        public int PingMs(int peer)
        {
            if (!IsServer) return serverPeer?.Ping ?? 0;
            return peers.TryGetValue(peer, out var p) ? p.Ping : 0;
        }

        public void Dispose()
        {
            try { manager.DisconnectAll(); } catch { /* ignore */ }
            manager.Stop();
        }

        // ---------------- INetEventListener ----------------
        void INetEventListener.OnPeerConnected(NetPeer peer)
        {
            int id = IsServer ? peer.Id : 0;
            peers[id] = peer;
            PeerConnected?.Invoke(id);
        }

        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            int id = IsServer ? peer.Id : 0;
            peers.Remove(id);
            PeerDisconnected?.Invoke(id, info.Reason.ToString());
        }

        void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }

        void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            int id = IsServer ? peer.Id : 0;
            var bytes = reader.GetRemainingBytes();
            Data?.Invoke(id, bytes, 0, bytes.Length);
        }

        void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            if (!IsServer) return;
            if (messageType != UnconnectedMessageType.Broadcast && messageType != UnconnectedMessageType.BasicMessage) return;
            string text;
            try { text = reader.GetString(64); } catch { return; }
            if (text != NetConst.DiscoveryMagic) return;
            var w = new NetDataWriter();
            w.Put(NetConst.DiscoveryReply);
            w.Put(DiscoveryInfo != null ? DiscoveryInfo() : "Gnome host");
            w.Put(peers.Count + 1);
            manager.SendUnconnectedMessage(w, remoteEndPoint);
        }

        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

        void INetEventListener.OnConnectionRequest(ConnectionRequest request)
        {
            if (!IsServer || peers.Count >= maxPeers)
            {
                request.Reject();
                return;
            }
            request.AcceptIfKey(NetConst.ConnectionKey);
        }
    }

    /// <summary>Finds hosts on the local network by UDP broadcast.</summary>
    public sealed class LanDiscovery : INetEventListener, IDisposable
    {
        public struct Found
        {
            public string Address;
            public int Port;
            public string Info;
            public int Players;
            public DateTime Seen;
        }

        readonly NetManager manager;
        readonly Dictionary<string, Found> found = new Dictionary<string, Found>();
        readonly int port;

        public LanDiscovery(int port)
        {
            this.port = port;
            manager = new NetManager(this) { UnconnectedMessagesEnabled = true, BroadcastReceiveEnabled = false, IPv6Enabled = false };
            manager.Start();
        }

        public IEnumerable<Found> Hosts
        {
            get
            {
                var now = DateTime.UtcNow;
                foreach (var f in found.Values)
                    if ((now - f.Seen).TotalSeconds < 6) yield return f;
            }
        }

        public void Ping()
        {
            var w = new NetDataWriter();
            w.Put(NetConst.DiscoveryMagic);
            manager.SendBroadcast(w, port);
        }

        /// <summary>Directly ask a known address (works across subnets where broadcast doesn't).</summary>
        public void Ask(string host)
        {
            var w = new NetDataWriter();
            w.Put(NetConst.DiscoveryMagic);
            try { manager.SendUnconnectedMessage(w, host, port); } catch { /* bad address */ }
        }

        public void Poll() => manager.PollEvents();

        public void Dispose() => manager.Stop();

        void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            try
            {
                if (reader.GetString(64) != NetConst.DiscoveryReply) return;
                var f = new Found { Address = remoteEndPoint.Address.ToString(), Port = remoteEndPoint.Port, Info = reader.GetString(128), Players = reader.GetInt(), Seen = DateTime.UtcNow };
                found[f.Address + ":" + f.Port] = f;
            }
            catch { /* malformed */ }
        }

        void INetEventListener.OnPeerConnected(NetPeer peer) { }
        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) { }
        void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }
        void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) { }
        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
        void INetEventListener.OnConnectionRequest(ConnectionRequest request) => request.Reject();
    }
}
