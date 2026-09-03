using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace SFMOnline
{
    internal static class RelayTcp
    {
        private sealed class ConnectionState
        {
            public TcpClient Tcp;
            public StreamWriter Writer;
            public volatile bool On;
            public volatile byte[] SessionKey;
            public readonly ConcurrentQueue<object> Outbox = new ConcurrentQueue<object>();
            public readonly AutoResetEvent SendSignal = new AutoResetEvent(false);
            public object LatestState;
            public object LatestMotion;
            public object LatestNpc;
            public int PendingCount;
        }

        private const int MaxQueuedMessages = 256;
        private const int MaxQueuedIncoming = 512;
        private static ConnectionState _connection;
        public static readonly ConcurrentQueue<string> Inbox = new ConcurrentQueue<string>();
        private static readonly ConcurrentDictionary<string, string> LatestRealtime = new ConcurrentDictionary<string, string>();
        private static int _pendingIncoming;
        public static string LastError { get; private set; } = "";

        // ========== UDP 数据面通道（v1.0.10） ==========
        // 客户端绑定本机 UDP 8001 出战；发往服务器 UDP 8000 入站
        // 高频数据（state/motion/bone/action/npc/pos）走 UDP，其余走 TCP
        public const int UdpLocalPort = 8001;
        public const int UdpRemotePort = 8000;
        private static UdpClient _udp;
        private static string _udpServerHost = "";
        private static int _udpRemotePort = UdpRemotePort;
        private static volatile bool _udpOn;
        private static readonly ConcurrentQueue<string> UdpInbox = new ConcurrentQueue<string>();
        private static readonly ConcurrentDictionary<string, string> UdpRealtime = new ConcurrentDictionary<string, string>();
        private static Thread _udpRecvThread;
        private static string _udpUid = "";
        private static string _udpRoom = "";

        /// <summary>设置 UDP 身份与房间（建房/入房/换房时更新，用于 UDP 包头部）。</summary>
        public static void SetUdpIdentity(string uid, string room)
        {
            _udpUid = uid ?? "";
            _udpRoom = room ?? "";
        }

        public static bool UdpActive => _udpOn && _udp != null && _udpHealthy;

        // UDP 健康探测：发 udp_ping 等 udp_pong 回执；连续失败标记不可用→回退 TCP
        private static volatile bool _udpHealthy = true;
        private static volatile float _lastUdpPongAt;
        private static float _udpProbeAt = -999f;

        /// <summary>发送 UDP 探测包并检测回执（由 OnlineCore 每 3 秒调用）。</summary>
        public static bool UdpProbe()
        {
            float now = UnityEngine.Time.unscaledTime;
            try
            {
                if (!_udpOn || _udp == null) { _udpHealthy = false; return false; }
                if (now - _udpProbeAt < 3f)
                {
                    // 探测间隔内：有回执=健康；超 8 秒无回执=不可用
                    if (_udpHealthy && now - _lastUdpPongAt > 8f) _udpHealthy = false;
                    return _udpHealthy;
                }
                _udpProbeAt = now;
                string uid = _udpUid;
                string room = _udpRoom;
                byte[] payload = Encoding.UTF8.GetBytes(uid + "\n" + room + "\n" + "{\"t\":\"udp_ping\"}");
                try { _udp.Send(payload, payload.Length, _udpServerHost, _udpRemotePort); } catch { _udpHealthy = false; }
                if (now - _lastUdpPongAt > 8f) _udpHealthy = false;
                return _udpHealthy;
            }
            catch { _udpHealthy = false; return false; }
        }

        /// <summary>记录收到 UDP 数据（收到任何包视为通路健康）。</summary>
        public static void NoteUdpAlive()
        {
            _lastUdpPongAt = UnityEngine.Time.unscaledTime;
            _udpHealthy = true;
        }

        /// <summary>启动 UDP 通道（连接成功后调用；host 为服务器地址，udpPort 来自服务器 ok 响应）。</summary>
        public static void StartUdp(string host, int udpPort = 0)
        {
            try
            {
                StopUdp();
                _udpServerHost = host ?? "";
                _udpRemotePort = udpPort > 0 ? udpPort : UdpRemotePort;
                _udp = new UdpClient(UdpLocalPort);
                _udp.Client.SendTimeout = 1000;
                _udpOn = true;
                _udpRecvThread = new Thread(UdpRecvLoop) { IsBackground = true, Name = "SFM Relay UDP Recv" };
                _udpRecvThread.Start();
            }
            catch { _udpOn = false; }
        }

        public static void StopUdp()
        {
            try
            {
                _udpOn = false;
                var u = _udp;
                _udp = null;
                try { u?.Close(); } catch { }
                while (UdpInbox.TryDequeue(out _)) { }
                UdpRealtime.Clear();
            }
            catch { }
        }

        private static void UdpRecvLoop()
        {
            try
            {
                while (_udpOn && _udp != null)
                {
                    IPEndPoint remote = null;
                    byte[] data;
                    try { data = _udp.Receive(ref remote); }
                    catch { break; }
                    if (data == null || data.Length == 0) continue;
                    try
                    {
                        string line = Encoding.UTF8.GetString(data);
                        NoteUdpAlive();
                        if (TryRealtimeKey(line, out var key))
                            UdpRealtime[key] = line;
                        else
                            UdpInbox.Enqueue(line);
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>经 UDP 发送（高频数据）。包格式: uid\nroom\njson</summary>
        public static bool SendUdp(string uid, string room, object o)
        {
            try
            {
                if (!_udpOn || _udp == null || string.IsNullOrEmpty(_udpServerHost) || o == null) return false;
                string json = MiniJson.Serialize(o);
                byte[] payload = Encoding.UTF8.GetBytes(uid + "\n" + (room ?? "") + "\n" + json);
                _udp.Send(payload, payload.Length, _udpServerHost, _udpRemotePort);
                return true;
            }
            catch { return false; }
        }

        /// <summary>取出收到的 UDP 数据（由 OnlineCore 轮询）。</summary>
        public static bool TryDequeueUdp(out string line)
        {
            return UdpInbox.TryDequeue(out line);
        }

        /// <summary>取最新 UDP 实时数据（按键去重，保留最新）。</summary>
        public static bool TryGetLatestUdp(string key, out string line)
        {
            return UdpRealtime.TryRemove(key, out line);
        }

        public static bool Connected
        {
            get
            {
                var c = _connection;
                return c != null && c.On;
            }
        }

        public static bool Connect(string host, int port)
        {
            return Connect(host, port, out _);
        }

        public static bool Connect(string host, int port, out string peerIp)
        {
            peerIp = "";
            try
            {
                LastError = "";
                Close();
                while (Inbox.TryDequeue(out _)) { }
                LatestRealtime.Clear();
                Interlocked.Exchange(ref _pendingIncoming, 0);
                var c = new ConnectionState { Tcp = new TcpClient(), On = true };
                c.Tcp.NoDelay = true;
                var connectTask = c.Tcp.ConnectAsync(host, port);
                if (!connectTask.Wait(TimeSpan.FromSeconds(6)) || !c.Tcp.Connected)
                    throw new TimeoutException("连接超时");
                var stream = c.Tcp.GetStream();
                c.Writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                c.Writer.WriteLine("{\"t\":\"whoami\"}");
                c.Tcp.ReceiveTimeout = 6000;
                var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
                string greeting = reader.ReadLine();
                var identity = MiniJson.ParseObject(greeting);
                if (identity == null || JsonHelper.Str(identity, "t") != "ip")
                    throw new IOException("联机服地址探测失败");
                peerIp = JsonHelper.Str(identity, "ip");
                if (!System.Net.IPAddress.TryParse(peerIp, out _))
                    throw new IOException("联机服返回了无效地址");
                c.Tcp.ReceiveTimeout = 0;
                _connection = c;
                new Thread(() => ReadLoop(c)) { IsBackground = true, Name = "SFM Relay Read" }.Start();
                new Thread(() => WriteLoop(c)) { IsBackground = true, Name = "SFM Relay Write" }.Start();
                return true;
            }
            catch (Exception ex)
            {
                peerIp = "";
                LastError = ex.GetBaseException().Message;
                Close();
                return false;
            }
        }

        public static void Send(object o)
        {
            try
            {
                var c = _connection;
                if (c == null || !c.On || o == null) return;
                if (o is Dictionary<string, object> message && message.TryGetValue("t", out var type))
                {
                    string kind = type == null ? "" : type.ToString();
                    // UDP 高频通道：state/motion/bone/action/npc/pos 走 UDP（减少 TCP 压力与 7000 封锁影响）
                    if (UdpActive && (kind == "state_sync" || kind == "motion_sync" || kind == "bone_sync" ||
                        kind == "action_sync" || kind == "npc_sync" || kind == "pos"))
                    {
                        string uid = _udpUid;
                        string room = _udpRoom;
                        if (uid.Length > 0)
                        {
                            SendUdp(uid, room, o);
                            return;
                        }
                    }
                    if (string.Equals(kind, "state_sync", StringComparison.Ordinal))
                        Interlocked.Exchange(ref c.LatestState, o);
                    else if (string.Equals(kind, "motion_sync", StringComparison.Ordinal))
                        Interlocked.Exchange(ref c.LatestMotion, o);
                    else if (string.Equals(kind, "npc_sync", StringComparison.Ordinal))
                        Interlocked.Exchange(ref c.LatestNpc, o);
                    else
                    {
                        if (Interlocked.Increment(ref c.PendingCount) > MaxQueuedMessages)
                        {
                            Interlocked.Decrement(ref c.PendingCount);
                            return;
                        }
                        c.Outbox.Enqueue(o);
                    }
                }
                else
                {
                    if (Interlocked.Increment(ref c.PendingCount) > MaxQueuedMessages)
                    {
                        Interlocked.Decrement(ref c.PendingCount);
                        return;
                    }
                    c.Outbox.Enqueue(o);
                }
                c.SendSignal.Set();
            }
            catch { }
        }

        public static void Hello(long uid, string name, string ip, string token, string pwd)
        {
            Send(new Dictionary<string, object>
            {
                ["t"] = "hello", ["uid"] = uid, ["name"] = name, ["ip"] = ip, ["token"] = token, ["password"] = pwd
            });
        }

        /// <summary>通知服务器本客户端的 UDP 源地址（经 TCP 安全通道登记）。</summary>
        public static void RegisterUdp(string room)
        {
            try
            {
                SetUdpIdentity(PeerUidCache, room);
                var local = _udp != null ? (System.Net.IPEndPoint)_udp.Client.LocalEndPoint : null;
                Send(new Dictionary<string, object>
                {
                    ["t"] = "udp_register", ["ip"] = LocalIpCache, ["port"] = local != null ? local.Port : UdpLocalPort, ["room"] = room ?? ""
                });
            }
            catch { }
        }

        private static string PeerUidCache = "";
        private static string LocalIpCache = "";

        /// <summary>连接成功后缓存 uid/本机 IP（由 OnlineCore 设置）。</summary>
        public static void CacheIdentity(string uid, string localIp)
        {
            PeerUidCache = uid ?? "";
            LocalIpCache = localIp ?? "";
        }

        public static void Close()
        {
            StopUdp();
            var c = Interlocked.Exchange(ref _connection, null);
            if (c == null) return;
            c.On = false;
            try { c.SendSignal.Set(); } catch { }
            try { c.Tcp?.Close(); } catch { }
        }

        private static void ReadLoop(ConnectionState c)
        {
            try
            {
                var r = new StreamReader(c.Tcp.GetStream(), Encoding.UTF8);
                while (c.On)
                {
                    string line = r.ReadLine();
                    if (line == null) break;
                    string processed = ProcessIncoming(c, line);
                    if (!string.IsNullOrEmpty(processed))
                    {
                        if (TryRealtimeKey(processed, out var key))
                            LatestRealtime[key] = processed;
                        else
                            EnqueueIncoming(processed);
                    }
                }
            }
            catch { }
            c.On = false;
            if (ReferenceEquals(_connection, c))
                Interlocked.CompareExchange(ref _connection, null, c);
            try { c.SendSignal.Set(); } catch { }
        }

        private static void WriteLoop(ConnectionState c)
        {
            try
            {
                while (c.On)
                {
                    bool wrote = false;
                    if (c.Outbox.TryDequeue(out var message))
                    {
                        Interlocked.Decrement(ref c.PendingCount);
                        WriteOne(c, message);
                        wrote = true;
                    }
                    var latestMotion = Interlocked.Exchange(ref c.LatestMotion, null);
                    if (latestMotion != null)
                    {
                        WriteOne(c, latestMotion);
                        wrote = true;
                    }
                    var latestNpc = Interlocked.Exchange(ref c.LatestNpc, null);
                    if (latestNpc != null)
                    {
                        WriteOne(c, latestNpc);
                        wrote = true;
                    }
                    var latestState = Interlocked.Exchange(ref c.LatestState, null);
                    if (latestState != null)
                    {
                        WriteOne(c, latestState);
                        wrote = true;
                    }
                    if (!wrote) c.SendSignal.WaitOne(100);
                }
            }
            catch { }
            c.On = false;
            try { c.Tcp?.Close(); } catch { }
        }

        private static void WriteOne(ConnectionState c, object message)
        {
            if (!c.On || c.Writer == null) return;
            var key = c.SessionKey;
            if (key != null)
            {
                string plain = MiniJson.Serialize(message);
                c.Writer.WriteLine(MiniJson.Serialize(new Dictionary<string, object> { ["e"] = EncryptSession(key, plain) }));
            }
            else
            {
                c.Writer.WriteLine(MiniJson.Serialize(message));
            }
        }

        public static bool TryDequeue(out string line)
        {
            if (Inbox.TryDequeue(out line))
            {
                Interlocked.Decrement(ref _pendingIncoming);
                return true;
            }
            foreach (var pair in LatestRealtime)
            {
                if (LatestRealtime.TryRemove(pair.Key, out line)) return true;
            }
            line = null;
            return false;
        }

        private static void EnqueueIncoming(string line)
        {
            int count = Interlocked.Increment(ref _pendingIncoming);
            if (count > MaxQueuedIncoming && Inbox.TryDequeue(out _))
                Interlocked.Decrement(ref _pendingIncoming);
            Inbox.Enqueue(line);
        }

        private static bool TryRealtimeKey(string line, out string key)
        {
            key = null;
            try
            {
                var d = MiniJson.ParseObject(line);
                if (d == null) return false;
                string t = JsonHelper.Str(d, "t");
                if (t != "motion_sync" && t != "state_sync" && t != "npc_state" && t != "pos")
                    return false;
                string uid = JsonHelper.Str(d, "uid");
                if (uid.Length == 0) uid = JsonHelper.Int(d, "stage", -1).ToString();
                key = t + ":" + uid;
                return true;
            }
            catch { return false; }
        }

        private static string ProcessIncoming(ConnectionState c, string line)
        {
            try
            {
                var d = MiniJson.ParseObject(line);
                if (d != null)
                {
                    if (c.SessionKey == null && JsonHelper.Str(d, "t") == "ok" && JsonHelper.Str(d, "key").Length > 0)
                    {
                        c.SessionKey = Convert.FromBase64String(JsonHelper.Str(d, "key"));
                        return line;
                    }
                    if (c.SessionKey != null && d.ContainsKey("e"))
                    {
                        return DecryptSession(c.SessionKey, JsonHelper.Str(d, "e"));
                    }
                }
            }
            catch { }
            return line;
        }

        private static string EncryptSession(byte[] key, string json)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();
                byte[] iv = aes.IV;
                using (var enc = aes.CreateEncryptor())
                {
                    byte[] pt = Encoding.UTF8.GetBytes(json);
                    byte[] ct = enc.TransformFinalBlock(pt, 0, pt.Length);
                    byte[] ivCt = new byte[iv.Length + ct.Length];
                    Buffer.BlockCopy(iv, 0, ivCt, 0, iv.Length);
                    Buffer.BlockCopy(ct, 0, ivCt, iv.Length, ct.Length);
                    byte[] mac;
                    using (var hmac = new HMACSHA256(key))
                        mac = hmac.ComputeHash(ivCt);
                    byte[] all = new byte[ivCt.Length + mac.Length];
                    Buffer.BlockCopy(ivCt, 0, all, 0, ivCt.Length);
                    Buffer.BlockCopy(mac, 0, all, ivCt.Length, mac.Length);
                    return Convert.ToBase64String(all);
                }
            }
        }

        private static string DecryptSession(byte[] key, string b64)
        {
            byte[] data = Convert.FromBase64String(b64);
            if (data.Length < 48) return "";
            byte[] iv = new byte[16];
            byte[] mac = new byte[32];
            byte[] ct = new byte[data.Length - 48];
            Buffer.BlockCopy(data, 0, iv, 0, 16);
            Buffer.BlockCopy(data, 16, ct, 0, ct.Length);
            Buffer.BlockCopy(data, data.Length - 32, mac, 0, 32);
            byte[] expect;
            using (var hmac = new HMACSHA256(key))
                expect = hmac.ComputeHash(data, 0, data.Length - 32);
            bool ok = mac.Length == expect.Length;
            for (int i = 0; i < mac.Length && i < expect.Length; i++) ok &= (mac[i] == expect[i]);
            if (!ok) return "";
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.IV = iv;
                using (var dec = aes.CreateDecryptor())
                {
                    byte[] pt = dec.TransformFinalBlock(ct, 0, ct.Length);
                    return Encoding.UTF8.GetString(pt);
                }
            }
        }
    }
}
