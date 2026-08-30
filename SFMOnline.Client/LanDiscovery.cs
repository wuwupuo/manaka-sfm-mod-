using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SFMOnline
{
    internal sealed class LanRoomInfo
    {
        public string Address;
        public string Name;
        public int Port;
        public int Players;
        public int MaxPlayers;
        public bool HasPassword;
        public DateTime SeenUtc;
    }

    internal static class LanDiscovery
    {
        private const int DiscoveryPort = 27571;
        private const string Magic = "SFMOLAN2";
        private const string Query = "SFMOLAN2|QUERY";
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, LanRoomInfo> Rooms = new Dictionary<string, LanRoomInfo>();
        private static UdpClient _listener;
        private static UdpClient _sender;
        private static Thread _listenThread;
        private static Thread _sendThread;
        private static volatile bool _running;
        private static volatile bool _advertising;
        private static string _packet = "";

        public static void StartListener()
        {
            if (_running) return;
            try
            {
                _listener = new UdpClient();
                _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                _listener.Client.ReceiveTimeout = 1200;
                _running = true;
                _listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "SFM-LAN-Discovery" };
                _listenThread.Start();
                Probe();
            }
            catch (Exception ex) { PluginInfo.Warn("局域网房间发现启动失败: " + ex.Message); }
        }

        public static void StartAdvertising(string name, int port, int players, int maxPlayers, bool hasPassword)
        {
            UpdateAdvertising(name, port, players, maxPlayers, hasPassword);
            if (_advertising) return;
            _advertising = true;
            try
            {
                _sender = new UdpClient();
                _sender.EnableBroadcast = true;
                _sendThread = new Thread(SendLoop) { IsBackground = true, Name = "SFM-LAN-Advertise" };
                _sendThread.Start();
            }
            catch (Exception ex) { _advertising = false; PluginInfo.Warn("局域网房间广播失败: " + ex.Message); }
        }

        public static void UpdateAdvertising(string name, int port, int players, int maxPlayers, bool hasPassword)
        {
            string safeName = Convert.ToBase64String(Encoding.UTF8.GetBytes(name ?? "玩家"));
            _packet = Magic + "|" + port + "|" + players + "|" + maxPlayers + "|" + (hasPassword ? "1" : "0") + "|" + safeName;
        }

        public static void Probe()
        {
            new Thread(() =>
            {
                try
                {
                    using (var probe = new UdpClient(0))
                    {
                        probe.EnableBroadcast = true;
                        probe.Client.ReceiveTimeout = 350;
                        byte[] q = Encoding.UTF8.GetBytes(Query);
                        probe.Send(q, q.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
                        DateTime until = DateTime.UtcNow.AddMilliseconds(1100);
                        while (DateTime.UtcNow < until)
                        {
                            try
                            {
                                IPEndPoint source = new IPEndPoint(IPAddress.Any, 0);
                                ParsePacket(probe.Receive(ref source), source);
                            }
                            catch (SocketException) { }
                        }
                    }
                }
                catch (Exception ex) { PluginInfo.Warn("局域网主动搜索失败: " + ex.Message); }
            }) { IsBackground = true, Name = "SFM-LAN-Probe" }.Start();
        }

        public static void StopAdvertising()
        {
            _advertising = false;
            try { _sender?.Close(); } catch { }
            _sender = null;
        }

        public static List<LanRoomInfo> Snapshot()
        {
            var result = new List<LanRoomInfo>();
            lock (Sync)
            {
                DateTime cutoff = DateTime.UtcNow.AddSeconds(-7);
                var expired = new List<string>();
                foreach (var kv in Rooms)
                {
                    if (kv.Value.SeenUtc < cutoff) expired.Add(kv.Key);
                    else result.Add(kv.Value);
                }
                foreach (string key in expired) Rooms.Remove(key);
            }
            return result;
        }

        public static void StopAll()
        {
            StopAdvertising();
            _running = false;
            try { _listener?.Close(); } catch { }
            _listener = null;
            lock (Sync) Rooms.Clear();
        }

        private static void SendLoop()
        {
            var endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
            while (_advertising)
            {
                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(_packet ?? "");
                    if (bytes.Length > Magic.Length) _sender.Send(bytes, bytes.Length, endpoint);
                }
                catch { }
                for (int i = 0; i < 20 && _advertising; i++) Thread.Sleep(100);
            }
        }

        private static void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint source = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = _listener.Receive(ref source);
                    string text = Encoding.UTF8.GetString(bytes);
                    if (text == Query)
                    {
                        if (_advertising && !string.IsNullOrEmpty(_packet))
                        {
                            byte[] reply = Encoding.UTF8.GetBytes(_packet);
                            _listener.Send(reply, reply.Length, source);
                        }
                        continue;
                    }
                    ParsePacket(bytes, source);
                }
                catch (SocketException) { }
                catch { }
            }
        }

        private static void ParsePacket(byte[] bytes, IPEndPoint source)
        {
            string[] parts = Encoding.UTF8.GetString(bytes).Split('|');
            if (parts.Length != 6 || parts[0] != Magic) return;
            if (!int.TryParse(parts[1], out int port) || port < 1 || port > 65535) return;
            int.TryParse(parts[2], out int players);
            int.TryParse(parts[3], out int maxPlayers);
            string name;
            try { name = Encoding.UTF8.GetString(Convert.FromBase64String(parts[5])); }
            catch { name = "局域网房间"; }
            string key = source.Address + ":" + port;
            lock (Sync)
            {
                Rooms[key] = new LanRoomInfo
                {
                    Address = source.Address.ToString(), Name = name, Port = port,
                    Players = players, MaxPlayers = maxPlayers, HasPassword = parts[4] == "1",
                    SeenUtc = DateTime.UtcNow
                };
            }
        }
    }
}
