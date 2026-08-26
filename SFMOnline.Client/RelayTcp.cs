using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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

        public static void Close()
        {
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
