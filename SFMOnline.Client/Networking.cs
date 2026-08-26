using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SFMOnline
{
    internal sealed class NetMsg
    {
        public byte Type;
        public byte[] Payload;
        public string SourceId;
    }

    internal sealed class PeerInfo
    {
        public string Id;
        public string Name;
        public bool IsHost;
        public long RttMs;
        public DateTime LastSeen = DateTime.UtcNow;
    }

    // 帧管道：TCP 或 PHP 邮箱两种实现，上层代码无感
    internal interface IFramePipe
    {
        bool ReadFrame(out byte[] frame);
        void WriteFrame(byte[] frame);
        void Close();
    }

    internal sealed class TcpFramePipe : IFramePipe
    {
        private readonly TcpClient _tcp;
        private readonly NetworkStream _s;
        public TcpFramePipe(TcpClient tcp) { _tcp = tcp; _s = tcp.GetStream(); }
        public bool ReadFrame(out byte[] frame)
        {
            frame = null;
            var head = new byte[4];
            if (!PacketCodec.ReadExact(_s, head, 4)) return false;
            int len = PacketCodec.ReadLength(head);
            if (len < 1 || len > 256 * 1024) return false;
            var body = new byte[len];
            if (!PacketCodec.ReadExact(_s, body, len)) return false;
            frame = new byte[4 + len];
            Buffer.BlockCopy(head, 0, frame, 0, 4);
            Buffer.BlockCopy(body, 0, frame, 4, len);
            return true;
        }
        public void WriteFrame(byte[] frame) { _s.Write(frame, 0, frame.Length); _s.Flush(); }
        public void Close() { try { _tcp.Close(); } catch { } }
    }

    internal sealed class HttpMailboxPipe : IFramePipe
    {
        private readonly string _relayBase;
        private readonly string _roomId;
        private readonly string _self;
        private readonly string _peer;   // 房主侧：指定玩家；游客侧：空=收所有人
        private readonly string _toOverride;
        private readonly string _who;
        private readonly Queue<byte[]> _inbox = new Queue<byte[]>();
        private readonly object _lock = new object();
        private long _after;
        private int _lastPollMs = 30;
        private volatile bool _closed;

        public HttpMailboxPipe(string relayBase, string roomId, string self, string peer, string toOverride, string who)
        {
            _relayBase = relayBase;
            _roomId = roomId;
            _self = self;
            _peer = peer;
            _toOverride = toOverride;
            _who = who;
        }

        public bool ReadFrame(out byte[] frame)
        {
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (!_closed)
            {
                lock (_lock)
                {
                    if (_inbox.Count > 0)
                    {
                        frame = _inbox.Dequeue();
                        return true;
                    }
                }
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var rows = RelayApi.Poll(_relayBase, _roomId, _self, _after);
                sw.Stop();
                int ms = (int)sw.ElapsedMilliseconds;
                if (ms > _lastPollMs) _lastPollMs = ms;
                if (rows != null && rows.Count > 0)
                {
                    lock (_lock)
                    {
                        foreach (var r in rows)
                        {
                            if (r.From == _self) continue;
                            if (_peer.Length > 0 && r.From != _peer) continue;
                            _after = Math.Max(_after, r.Id);
                            _inbox.Enqueue(r.Data);
                        }
                        if (_inbox.Count > 0)
                        {
                            frame = _inbox.Dequeue();
                            return true;
                        }
                    }
                }
                if (DateTime.UtcNow > deadline) break;
                Thread.Sleep(Math.Min(200, Math.Max(20, _lastPollMs)));
            }
            frame = null;
            return false;
        }

        public void WriteFrame(byte[] frame)
        {
            WriteFrames(new[] { frame });
        }

        public void WriteFrames(IList<byte[]> frames)
        {
            if (_closed || frames == null || frames.Count == 0) return;
            RelayApi.PostBatch(_relayBase, _roomId, _self, _toOverride, frames);
        }

        public void Close()
        {
            if (_closed) return;
            _closed = true;
            if (_who == "host")
                RelayApi.Bye(_relayBase, _roomId, "host", RelayApi.HostBox);
            else
                RelayApi.Bye(_relayBase, _roomId, "guest", _self);
        }
    }

    // PHP 邮箱中转 API（与游戏客户端同款 payload 提交，兼容虚拟主机）
    internal static class RelayApi
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        public static string Token = "";
        public static string HostBox = "";

        public static bool Hello(string baseUrl, string roomId, string role, out string guestId)
        {
            guestId = "";
            var d = Post(baseUrl, new Dictionary<string, object>
            {
                ["action"] = "hello",
                ["room_id"] = roomId,
                ["role"] = role,
                ["name"] = role == "host" ? "Host" : "Guest"
            });
            if (d != null && JsonHelper.Int(d, "code") == 0)
            {
                guestId = JsonHelper.Str(d, "guest_id");
                Token = JsonHelper.Str(d, "token");
                if (role == "host") HostBox = JsonHelper.Str(d, "box");
                return true;
            }
            return false;
        }

        public static bool HelloWithName(string baseUrl, string roomId, string role, string name, out string boxId)
        {
            boxId = "";
            var d = Post(baseUrl, new Dictionary<string, object>
            {
                ["action"] = "hello",
                ["room_id"] = roomId,
                ["role"] = role,
                ["name"] = name ?? ""
            });
            if (d != null && JsonHelper.Int(d, "code") == 0)
            {
                boxId = JsonHelper.Str(d, "guest_id");
                Token = JsonHelper.Str(d, "token");
                if (role == "host") HostBox = JsonHelper.Str(d, "box");
                return true;
            }
            return false;
        }

        public static List<string> Peers(string baseUrl, string roomId)
        {
            var result = new List<string>();
            var d = Post(baseUrl, new Dictionary<string, object>
            {
                ["action"] = "peers",
                ["room_id"] = roomId
            });
            if (d == null || JsonHelper.Int(d, "code") != 0) return result;
            var list = JsonHelper.List(d, "peers");
            foreach (var row in list)
                result.Add(JsonHelper.Str(row, "id"));
            return result;
        }

        public static List<(long Id, string From, byte[] Data)> Poll(string baseUrl, string roomId, string box, long after)
        {
            var result = new List<(long, string, byte[])>();
            var d = Post(baseUrl, new Dictionary<string, object>
            {
                ["action"] = "poll",
                ["room_id"] = roomId,
                ["box"] = box,
                ["token"] = Token,
                ["after"] = after
            });
            if (d == null || JsonHelper.Int(d, "code") != 0) return result;
            foreach (var row in JsonHelper.List(d, "rows"))
            {
                long id = JsonHelper.Int(row, "id");
                string from = JsonHelper.Str(row, "from");
                string b64 = JsonHelper.Str(row, "data");
                try { result.Add((id, from, Convert.FromBase64String(b64))); }
                catch { }
            }
            return result;
        }

        public static void Post(string baseUrl, string roomId, string from, string to, byte[] frame)
        {
            PostBatch(baseUrl, roomId, from, to, new List<byte[]> { frame });
        }

        public static void PostBatch(string baseUrl, string roomId, string from, string to, IList<byte[]> frames)
        {
            if (frames == null || frames.Count == 0) return;
            var sb = new StringBuilder();
            for (int i = 0; i < frames.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(Convert.ToBase64String(frames[i]));
            }
            Post(baseUrl, new Dictionary<string, object>
            {
                ["action"] = "post",
                ["room_id"] = roomId,
                ["box"] = from,
                ["to"] = to,
                ["token"] = Token,
                ["data"] = sb.ToString()
            });
        }

        public static void Bye(string baseUrl, string roomId, string who, string box)
        {
            Post(baseUrl, new Dictionary<string, object>
            {
                ["action"] = "bye",
                ["room_id"] = roomId,
                ["who"] = who,
                ["box"] = box,
                ["token"] = Token
            });
        }

        private static Dictionary<string, object> Post(string baseUrl, Dictionary<string, object> body)
        {
            try
            {
                var fd = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["payload"] = MiniJson.Serialize(body)
                });
                var resp = Http.PostAsync(baseUrl, fd).Result;
                string text = resp.Content.ReadAsStringAsync().Result;
                return MiniJson.ParseObject(text);
            }
            catch { return null; }
        }
    }

    internal static class NetPriority
    {
        // 实时状态可以稍后处理；聊天、控制、加入/离开、事件等可靠消息必须优先。
        public static bool IsReliablePriority(byte type) =>
            type != MsgTypes.Motion && type != MsgTypes.State;

        public static void EnqueueBounded<T>(ConcurrentQueue<T> queue, T item, int max)
        {
            while (queue.Count >= max && queue.TryDequeue(out _)) { }
            queue.Enqueue(item);
        }
    }

    internal sealed class ClientConn
    {
        public string Id;
        public string Name;
        public TcpClient Tcp;
        public IFramePipe Pipe;
        public Thread ReaderThread;
        public Thread WriterThread;
        public readonly ConcurrentQueue<byte[]> PriorityQueue = new ConcurrentQueue<byte[]>();
        public readonly ConcurrentQueue<byte[]> SendQueue = new ConcurrentQueue<byte[]>();
        public byte[] LatestMotion;
        public volatile bool Closed;
        public DateTime LastSeen = DateTime.UtcNow;
        public DateTime RateWindowAt = DateTime.UtcNow;
        public int RateCount;
        public DateTime ChatWindowAt = DateTime.UtcNow;
        public int ChatCount;
        public PeerInfo Info = new PeerInfo();

        public void Close()
        {
            if (Closed) return;
            Closed = true;
            try { Pipe?.Close(); } catch { }
            try { Tcp?.Close(); } catch { }
        }
    }

    internal sealed class NetHost : IDisposable
    {
        public readonly ConcurrentQueue<NetMsg> Inbound = new ConcurrentQueue<NetMsg>();
        private readonly ConcurrentQueue<NetMsg> _priorityInbound = new ConcurrentQueue<NetMsg>();
        private readonly ConcurrentDictionary<string, NetMsg> _latestMotionInbound = new ConcurrentDictionary<string, NetMsg>();
        private readonly object _lock = new object();
        private readonly Dictionary<string, ClientConn> _clients = new Dictionary<string, ClientConn>();
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;
        private int _port;
        private string _password = "";
        private string _serverName = "房主";
        private int _maxClients = 8;
        private int _clientSeq;
        private string _relayBase = "";
        private string _relayRoomId = "";
        private bool _relayMode = false;

        public bool Running => _running;
        public int Port => _port;

        public bool TryDequeue(out NetMsg message)
        {
            if (_priorityInbound.TryDequeue(out message)) return true;
            if (Inbound.TryDequeue(out message)) return true;
            foreach (var pair in _latestMotionInbound)
                if (_latestMotionInbound.TryRemove(pair.Key, out message)) return true;
            message = null;
            return false;
        }

        public bool Start(int port, string password, int maxClients, string serverName)
        {
            if (_running) return true;
            _port = port;
            _password = password ?? "";
            _serverName = string.IsNullOrWhiteSpace(serverName) ? "房主" : serverName.Trim();
            _maxClients = Math.Max(1, Math.Min(maxClients, 16));
            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                _running = true;
                _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
                _acceptThread.Start();
                return true;
            }
            catch (Exception ex)
            {
                PluginInfo.Error("主机启动失败: " + ex.Message);
                _running = false;
                return false;
            }
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            if (_relayMode && !string.IsNullOrEmpty(_relayRoomId))
                RelayApi.Bye(_relayBase, _relayRoomId, "host", "");
            List<ClientConn> all;
            lock (_lock) { all = new List<ClientConn>(_clients.Values); _clients.Clear(); }
            foreach (var c in all) c.Close();
            _acceptThread = null;
        }

        // PHP 邮箱中转模式：房主不监听端口，主动向 relay.php 登记，轮询发现玩家
        public bool StartRelay(string relayBase, string roomId, string password, int maxClients, string serverName)
        {
            if (_running) return true;
            _relayBase = relayBase;
            _relayRoomId = roomId;
            _relayMode = true;
            _password = password ?? "";
            _serverName = string.IsNullOrWhiteSpace(serverName) ? "房主" : serverName.Trim();
            _maxClients = Math.Max(1, Math.Min(maxClients, 16));
            if (!RelayApi.HelloWithName(relayBase, roomId, "host", serverName, out _))
            {
                PluginInfo.Error("PHP中转登记失败");
                return false;
            }
            _running = true;
            _acceptThread = new Thread(RelayPeerLoop) { IsBackground = true };
            _acceptThread.Start();
            PluginInfo.Info("PHP中转房间已登记: " + roomId);
            return true;
        }

        private void RelayPeerLoop()
        {
            while (_running)
            {
                try
                {
                    var peers = RelayApi.Peers(_relayBase, _relayRoomId);
                    var known = new HashSet<string>();
                    foreach (var gid in peers)
                    {
                        if (gid == RelayApi.HostBox) continue;
                        string id = "r" + gid;
                        known.Add(id);
                        lock (_lock)
                        {
                            if (_clients.ContainsKey(id)) continue;
                            if (_clients.Count >= _maxClients) break;
                            _clientSeq++;
                            var conn = new ClientConn
                            {
                                Id = id,
                                Pipe = new HttpMailboxPipe(_relayBase, _relayRoomId, RelayApi.HostBox, gid, gid, "host")
                            };
                            _clients[id] = conn;
                            conn.ReaderThread = new Thread(() => ClientReadLoop(conn)) { IsBackground = true };
                            conn.WriterThread = new Thread(() => ClientWriteLoop(conn)) { IsBackground = true };
                            conn.ReaderThread.Start();
                            conn.WriterThread.Start();
                        }
                    }
                }
                catch { }
                Thread.Sleep(2000);
            }
        }

        public void SendToClients(byte type, byte[] payload, string exceptPeerId)
        {
            var data = PacketCodec.Encode(type, payload);
            List<ClientConn> all;
            lock (_lock) { all = new List<ClientConn>(_clients.Values); }
            foreach (var c in all)
            {
                if (exceptPeerId != null && c.Id == exceptPeerId) continue;
                if (type == MsgTypes.Motion) Interlocked.Exchange(ref c.LatestMotion, data);
                else if (NetPriority.IsReliablePriority(type)) NetPriority.EnqueueBounded(c.PriorityQueue, data, 256);
                else NetPriority.EnqueueBounded(c.SendQueue, data, 128);
            }
        }

        // 房主踢出指定玩家
        public void KickClient(string peerId, string reason = "被房主踢出")
        {
            lock (_lock)
            {
                if (_clients.TryGetValue(peerId, out var conn))
                    RemoveClient(conn, reason);
            }
        }

        public void BroadcastPlayers()
        {
            var payload = BuildPlayersPayload();
            SendToClients(MsgTypes.Players, payload, null);
            Inbound.Enqueue(new NetMsg { Type = MsgTypes.Players, Payload = payload, SourceId = "server" });
        }

        public List<PeerInfo> GetPeers()
        {
            var list = new List<PeerInfo>
            {
                new PeerInfo { Id = "host", Name = _serverName, IsHost = true, RttMs = 0, LastSeen = DateTime.UtcNow }
            };
            lock (_lock)
            {
                foreach (var c in _clients.Values)
                {
                    if (c.Info != null) list.Add(c.Info);
                }
            }
            return list;
        }

        private byte[] BuildPlayersPayload()
        {
            var peers = GetPeers();
            var w = new WireWriter();
            w.WriteInt(peers.Count);
            foreach (var p in peers)
            {
                w.WriteString(p.Id);
                w.WriteString(p.Name);
                w.WriteBool(p.IsHost);
                w.WriteLong(p.RttMs);
            }
            return w.ToArray();
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient tcp = null;
                try
                {
                    tcp = _listener.AcceptTcpClient();
                }
                catch
                {
                    break;
                }
                if (!_running) { try { tcp.Close(); } catch { } break; }

                ClientConn conn;
                lock (_lock)
                {
                    if (_clients.Count >= _maxClients)
                    {
                        try
                        {
                            var s = tcp.GetStream();
                            var err = PacketBuilders.BuildErrorPayload("房间已满");
                            var data = PacketCodec.Encode(MsgTypes.Error, err);
                            s.Write(data, 0, data.Length);
                            tcp.Close();
                        }
                        catch { }
                        continue;
                    }
                    _clientSeq++;
                    conn = new ClientConn
                    {
                        Id = "c" + _clientSeq,
                        Tcp = tcp,
                        Pipe = new TcpFramePipe(tcp)
                    };
                    _clients[conn.Id] = conn;
                }

                try { tcp.NoDelay = true; } catch { }
                conn.ReaderThread = new Thread(() => ClientReadLoop(conn)) { IsBackground = true };
                conn.WriterThread = new Thread(() => ClientWriteLoop(conn)) { IsBackground = true };
                conn.ReaderThread.Start();
                conn.WriterThread.Start();
            }
        }

        private void ClientReadLoop(ClientConn conn)
        {
            bool helloDone = false;
            try
            {
                while (!conn.Closed && _running)
                {
                    if (!conn.Pipe.ReadFrame(out var packet)) break;
                    if (packet.Length < 5) break;
                    int len = PacketCodec.ReadLength(packet);
                    if (len < 1 || len > 256 * 1024) break;
                    conn.LastSeen = DateTime.UtcNow;
                    if ((conn.LastSeen - conn.RateWindowAt).TotalSeconds >= 10)
                    {
                        conn.RateWindowAt = conn.LastSeen;
                        conn.RateCount = 0;
                    }
                    if (++conn.RateCount > 600) break;
                    byte type = packet[4];
                    var payload = new byte[packet.Length - 5];
                    Buffer.BlockCopy(packet, 5, payload, 0, payload.Length);

                    if (!helloDone)
                    {
                        if (type != MsgTypes.Hello) break;
                        helloDone = HandleHello(conn, payload);
                        if (!helloDone) break;
                        continue;
                    }

                    if ((type == MsgTypes.Control && payload.Length > 1024) ||
                        (type == MsgTypes.Chat && payload.Length > 8192)) continue;
                    if (type == MsgTypes.State || type == MsgTypes.Event || type == MsgTypes.Chat ||
                        type == MsgTypes.Follow || type == MsgTypes.Motion || type == MsgTypes.Control ||
                        type == MsgTypes.Ping)
                    {
                        try
                        {
                            var identityReader = new WireReader(payload);
                            if (identityReader.ReadString() != conn.Id) continue;
                        }
                        catch { continue; }
                    }

                    if (type == MsgTypes.Chat)
                    {
                        var chatNow = DateTime.UtcNow;
                        if ((chatNow - conn.ChatWindowAt).TotalSeconds >= 5)
                        {
                            conn.ChatWindowAt = chatNow;
                            conn.ChatCount = 0;
                        }
                        if (++conn.ChatCount > 2)
                        {
                            var warning = PacketCodec.Encode(MsgTypes.Error, PacketBuilders.BuildErrorPayload("每5秒最多发送2条消息"));
                            NetPriority.EnqueueBounded(conn.PriorityQueue, warning, 256);
                            continue;
                        }
                    }

                    if (type == MsgTypes.Ping)
                    {
                        // 服务器以自己的身份回 Pong，客户端据此测量到房主的 RTT
                        var pong = PacketBuilders.BuildPongPayload("host", payload);
                        conn.SendQueue.Enqueue(PacketCodec.Encode(MsgTypes.Pong, pong));
                    }

                    var incoming = new NetMsg { Type = type, Payload = payload, SourceId = conn.Id };
                    if (type == MsgTypes.Motion) _latestMotionInbound[conn.Id] = incoming;
                    else if (NetPriority.IsReliablePriority(type)) NetPriority.EnqueueBounded(_priorityInbound, incoming, 512);
                    else NetPriority.EnqueueBounded(Inbound, incoming, 256);
                }
            }
            catch { }
            RemoveClient(conn, "连接断开");
        }

        private bool HandleHello(ClientConn conn, byte[] payload)
        {
            try
            {
                var r = new WireReader(payload);
                string version = r.ReadString();
                string name = r.ReadString();
                string password = r.ReadString();
                if (!string.IsNullOrEmpty(_password) && password != _password)
                {
                    SendErrorAndClose(conn, "密码错误");
                    return false;
                }
                conn.Name = string.IsNullOrWhiteSpace(name) ? "玩家" : name.Trim();
                conn.Info.Id = conn.Id;
                conn.Info.Name = conn.Name;
                conn.Info.IsHost = false;

                var w = new WireWriter();
                w.WriteString(conn.Id);
                w.WriteString(PluginInfo.Version);
                var peers = GetPeers();
                w.WriteInt(peers.Count);
                foreach (var p in peers)
                {
                    w.WriteString(p.Id);
                    w.WriteString(p.Name);
                    w.WriteBool(p.IsHost);
                    w.WriteLong(p.RttMs);
                }
                var welcome = w.ToArray();
                conn.SendQueue.Enqueue(PacketCodec.Encode(MsgTypes.Welcome, welcome));
                BroadcastPlayers();
                PluginInfo.Info($"玩家 {conn.Name} ({conn.Id}) 加入房间");
                return true;
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("Hello 解析失败: " + ex.Message);
                SendErrorAndClose(conn, "协议错误");
                return false;
            }
        }

        private void SendErrorAndClose(ClientConn conn, string message)
        {
            try
            {
                var data = PacketCodec.Encode(MsgTypes.Error, PacketBuilders.BuildErrorPayload(message));
                conn.Pipe.WriteFrame(data);
            }
            catch { }
            conn.Close();
        }

        private void ClientWriteLoop(ClientConn conn)
        {
            try
            {
                while (!conn.Closed)
                {
                    var batch = new List<byte[]>();
                    while (batch.Count < 10 && conn.PriorityQueue.TryDequeue(out var p)) batch.Add(p);
                    while (batch.Count < 10 && conn.SendQueue.TryDequeue(out var d)) batch.Add(d);
                    var latestMotion = Interlocked.Exchange(ref conn.LatestMotion, null);
                    if (latestMotion != null) batch.Add(latestMotion);
                    if (batch.Count > 0)
                    {
                        if (conn.Pipe is HttpMailboxPipe hmp)
                            hmp.WriteFrames(batch);
                        else
                            foreach (var b in batch) conn.Pipe.WriteFrame(b);
                    }
                    else
                    {
                        Thread.Sleep(5);
                    }
                }
            }
            catch { }
            conn.Close();
        }

        private void RemoveClient(ClientConn conn, string reason)
        {
            bool existed = false;
            lock (_lock)
            {
                if (_clients.TryGetValue(conn.Id, out var cur) && ReferenceEquals(cur, conn))
                {
                    _clients.Remove(conn.Id);
                    existed = true;
                }
            }
            conn.Close();
            if (existed)
            {
                PluginInfo.Info($"玩家 {conn.Name} ({conn.Id}) 离开: {reason}");
                var bye = PacketBuilders.BuildByePayload(conn.Id, reason);
                Inbound.Enqueue(new NetMsg { Type = MsgTypes.Bye, Payload = bye, SourceId = conn.Id });
                BroadcastPlayers();
            }
        }

        public void Dispose() => Stop();
    }

    internal sealed class NetClient : IDisposable
    {
        public readonly ConcurrentQueue<NetMsg> Inbound = new ConcurrentQueue<NetMsg>();
        private readonly ConcurrentQueue<NetMsg> _priorityInbound = new ConcurrentQueue<NetMsg>();
        private readonly ConcurrentQueue<byte[]> _prioritySendQueue = new ConcurrentQueue<byte[]>();
        private readonly ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();
        private NetMsg _latestMotionInbound;
        private byte[] _latestMotionSend;
        private TcpClient _tcp;
        private IFramePipe _pipe;
        private bool _relay;
        private Thread _readThread;
        private Thread _writeThread;
        private volatile bool _connected;
        private string _peerId = "";
        private DateTime _rateWindowAt = DateTime.UtcNow;
        private int _rateCount;

        public bool Connected => _connected;
        public string PeerId => _peerId;

        public bool TryDequeue(out NetMsg message)
        {
            if (_priorityInbound.TryDequeue(out message)) return true;
            if (Inbound.TryDequeue(out message)) return true;
            message = Interlocked.Exchange(ref _latestMotionInbound, null);
            return message != null;
        }

        public bool Connect(string host, int port, string nickname, string password, out string error)
        {
            error = null;
            if (_connected) { Disconnect(); }
            try
            {
                _tcp = new TcpClient { NoDelay = true };
                var connectTask = _tcp.ConnectAsync(host, port);
                if (!connectTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    error = "连接超时（5秒），请检查地址/端口/樱花映射是否开启";
                    Disconnect();
                    return false;
                }
                _pipe = new TcpFramePipe(_tcp);
                _relay = false;

                var w = new WireWriter();
                w.WriteString(PluginInfo.Version);
                w.WriteString(nickname);
                w.WriteString(password ?? "");
                var hello = PacketCodec.Encode(MsgTypes.Hello, w.ToArray());
                _pipe.WriteFrame(hello);

                _connected = true;
                _readThread = new Thread(ReadLoop) { IsBackground = true };
                _writeThread = new Thread(WriteLoop) { IsBackground = true };
                _readThread.Start();
                _writeThread.Start();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Disconnect();
                return false;
            }
        }

        public bool ConnectRelay(string relayBase, string roomId, string nickname, string password, out string error)
        {
            error = null;
            if (_connected) Disconnect();
            try
            {
                if (!RelayApi.HelloWithName(relayBase, roomId, "guest", nickname, out var guestId) || guestId.Length == 0)
                {
                    error = "PHP中转登记失败";
                    return false;
                }
                _relay = true;
                _pipe = new HttpMailboxPipe(relayBase, roomId, guestId, "", "all", "guest");
                var w = new WireWriter();
                w.WriteString(PluginInfo.Version);
                w.WriteString(nickname);
                w.WriteString(password ?? "");
                _pipe.WriteFrame(PacketCodec.Encode(MsgTypes.Hello, w.ToArray()));
                _connected = true;
                _readThread = new Thread(ReadLoop) { IsBackground = true };
                _writeThread = new Thread(WriteLoop) { IsBackground = true };
                _readThread.Start();
                _writeThread.Start();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Disconnect();
                return false;
            }
        }

        public void Send(byte type, byte[] payload)
        {
            if (!_connected || payload == null || payload.Length > 256 * 1024) return;
            if ((type == MsgTypes.Control && payload.Length > 1024) ||
                (type == MsgTypes.Chat && payload.Length > 8192)) return;
            var data = PacketCodec.Encode(type, payload);
            if (type == MsgTypes.Motion) Interlocked.Exchange(ref _latestMotionSend, data);
            else if (NetPriority.IsReliablePriority(type)) NetPriority.EnqueueBounded(_prioritySendQueue, data, 256);
            else NetPriority.EnqueueBounded(_sendQueue, data, 128);
        }

        public void Disconnect()
        {
            _connected = false;
            try { _pipe?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
            _pipe = null;
        }

        private void ReadLoop()
        {
            try
            {
                while (_connected)
                {
                    if (!_pipe.ReadFrame(out var packet)) break;
                    if (packet.Length < 5) break;
                    int len = PacketCodec.ReadLength(packet);
                    if (len < 1 || len > 256 * 1024) break;
                    var rateNow = DateTime.UtcNow;
                    if ((rateNow - _rateWindowAt).TotalSeconds >= 10)
                    {
                        _rateWindowAt = rateNow;
                        _rateCount = 0;
                    }
                    if (++_rateCount > 600) break;
                    byte type = packet[4];
                    var payload = new byte[packet.Length - 5];
                    Buffer.BlockCopy(packet, 5, payload, 0, payload.Length);

                    if (type == MsgTypes.Welcome)
                    {
            try
            {
                var r = new WireReader(payload);
                            _peerId = r.ReadString();
                            string serverVersion = r.ReadString();
                            Inbound.Enqueue(new NetMsg { Type = type, Payload = payload, SourceId = "host" });
                        }
                        catch { }
                        continue;
                    }

                    if (type == MsgTypes.Ping)
                    {
                        var responderId = _peerId;
                        var pong = PacketBuilders.BuildPongPayload(responderId, payload);
                        _sendQueue.Enqueue(PacketCodec.Encode(MsgTypes.Pong, pong));
                    }

                    var incoming = new NetMsg { Type = type, Payload = payload, SourceId = "host" };
                    if ((type == MsgTypes.Control && payload.Length > 1024) ||
                        (type == MsgTypes.Chat && payload.Length > 8192)) continue;
                    if (type == MsgTypes.Motion) Interlocked.Exchange(ref _latestMotionInbound, incoming);
                    else if (NetPriority.IsReliablePriority(type)) NetPriority.EnqueueBounded(_priorityInbound, incoming, 512);
                    else NetPriority.EnqueueBounded(Inbound, incoming, 256);
                }
            }
            catch { }
            finally
            {
                _connected = false;
                try { _pipe?.Close(); } catch { }
                try { _tcp?.Close(); } catch { }
                Inbound.Enqueue(new NetMsg { Type = MsgTypes.Bye, Payload = new byte[0], SourceId = "host" });
            }
        }

        private void WriteLoop()
        {
            try
            {
                while (_connected)
                {
                    var batch = new List<byte[]>();
                    while (batch.Count < 10 && _prioritySendQueue.TryDequeue(out var p)) batch.Add(p);
                    while (batch.Count < 10 && _sendQueue.TryDequeue(out var d)) batch.Add(d);
                    var latestMotion = Interlocked.Exchange(ref _latestMotionSend, null);
                    if (latestMotion != null) batch.Add(latestMotion);
                    if (batch.Count > 0)
                    {
                        if (_pipe is HttpMailboxPipe hmp)
                            hmp.WriteFrames(batch);
                        else
                            foreach (var b in batch) _pipe.WriteFrame(b);
                    }
                    else
                    {
                        Thread.Sleep(5);
                    }
                }
            }
            catch { }
        }

        public void Dispose() => Disconnect();
    }

    internal static class PacketBuilders
    {
        public static byte[] BuildErrorPayload(string message)
        {
            var w = new WireWriter();
            w.WriteString(message ?? "");
            return w.ToArray();
        }

        public static byte[] BuildByePayload(string senderId, string reason)
        {
            var w = new WireWriter();
            w.WriteString(senderId);
            w.WriteString(reason ?? "");
            return w.ToArray();
        }

        public static byte[] BuildPingPayload(string senderId, long tick)
        {
            var w = new WireWriter();
            w.WriteString(senderId);
            w.WriteLong(tick);
            return w.ToArray();
        }

        public static byte[] BuildPongPayload(string responderId, byte[] pingPayload)
        {
            string pingSender = "";
            long tick = 0;
            try
            {
                var r = new WireReader(pingPayload);
                pingSender = r.ReadString();
                tick = r.ReadLong();
            }
            catch { }
            var w = new WireWriter();
            w.WriteString(pingSender);
            w.WriteString(responderId);
            w.WriteLong(tick);
            return w.ToArray();
        }
    }
}
