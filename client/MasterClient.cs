using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SFMOnline
{
    public class MasterServerInfo
    {
        public int id;
        public string name = "";
        public string address = "";
        public int port = 80;
        public string region = "";
        public int max_players_per_room = 8;
        public int max_rooms = 20;
        public string required_mods = "";
        public int players;
        public int rooms;
        public int max_online;
        public int has_password;
        public string password = "";
        public int latency_ms = -1;
    }

    // Mod 总服客户端（版本/公告/服务器列表/在线数）
    internal static class MasterClient
    {
        public const string DefaultDomain = "wuwupuo1.xtxt.xyz";
        public const string StableUpdateUrl = "https://wuwupuo1.xtxt.xyz/public_html/sfm_api/update.php";
        private static readonly string[] CandidatePaths =
        {
            "/public_html/sfm_api/api.php",
            "/public_html/sfm_api/all.php",
            "/public_html/sfm_api/master.php",
            "/sfm_api/master.php",
            "/public_html/master.php",
            "/master.php"
        };
        private static string _base = "";
        private static string _gate = "";
        private static long _gateExpire = 0;
        private static string _proofNonce = "";
        private static int _proofBits = 0;
        private static readonly SemaphoreSlim ProofLock = new SemaphoreSlim(1, 1);
        private static readonly HttpClient Http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        })
        { Timeout = TimeSpan.FromSeconds(15) };

        static MasterClient()
        {
            try
            {
                ServicePointManager.SecurityProtocol |=
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
            }
            catch { }
        }

        public static string GetBase()
        {
            if (_base.Length > 0) return _base;
            // 立即返回默认路径，避免在主线程阻塞；后台再自动扫描更优路径
            return "https://" + DefaultDomain + "/public_html/sfm_api/api.php";
        }

        private static async Task EnsureBaseAsync()
        {
            if (_base.Length > 0) return;
            await Task.Run(() => DiscoverAndSetBase());
        }

        private static void DiscoverAndSetBase()
        {
            if (_base.Length > 0) return;
            // 总服只绑定作者自己的域名，不允许玩家改；自动扫描正确的 PHP 路径
            foreach (var path in CandidatePaths)
            {
                foreach (var scheme in new[] { "https", "http" })
                {
                    string url = scheme + "://" + DefaultDomain + path;
                    try
                    {
                        using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5)))
                        {
                            var resp = Http.GetAsync(url + "?action=ping", cts.Token).GetAwaiter().GetResult();
                            string text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            var d = MiniJson.ParseObject(text);
                            if (d != null && JsonHelper.Int(d, "code") == 0)
                            {
                                _base = url;
                                ClientLog.Write("总服接口已定位: " + url);
                                return;
                            }
                            ClientLog.Write("总服接口探测失败: " + url + " HTTP=" + (int)resp.StatusCode + " body=" + Truncate(text));
                        }
                    }
                    catch (Exception ex)
                    {
                        ClientLog.Write("总服接口探测异常: " + url + " -> " + ex.Message);
                    }
                }
            }
            _base = "https://" + DefaultDomain + "/public_html/sfm_api/api.php";
        }

        private static string Truncate(string s, int max = 300)
        {
            if (s == null) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        public static async Task<bool> Ping()
        {
            await EnsureBaseAsync();
            try
            {
                var d = await GetJson(GetBase() + "?action=ping");
                return d != null && JsonHelper.Int(d, "code") == 0;
            }
            catch { return false; }
        }

        public static async Task<bool> Handshake()
        {
            await EnsureBaseAsync();
            try
            {
                var d = await GetJson(GetBase() + "?action=handshake");
                if (d != null && JsonHelper.Int(d, "code") == 0)
                {
                    _gate = JsonHelper.Str(d, "gate");
                    _gateExpire = JsonHelper.Long(d, "gate_expire");
                    _proofNonce = JsonHelper.Str(d, "pow_nonce");
                    _proofBits = Math.Max(0, Math.Min(22, JsonHelper.Int(d, "pow_bits")));
                    return _gate.Length > 0;
                }
                ClientLog.Write("握手失败: " + (d != null ? JsonHelper.Str(d, "msg") : "无法解析"));
            }
            catch (Exception ex)
            {
                ClientLog.Write("握手异常: " + ex.Message);
            }
            return false;
        }

        private static async Task EnsureGateAsync()
        {
            if (_gate.Length > 0 && _gateExpire > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30) return;
            await Handshake();
        }

        private static bool NeedsClientProof(string action)
        {
            switch (action ?? "")
            {
                case "captcha":
                case "send_code":
                case "resend_code":
                case "register":
                case "login":
                case "forgot":
                case "relay_apply":
                    return true;
                default:
                    return false;
            }
        }

        private static bool HasLeadingZeroBits(byte[] hash, int bits)
        {
            int full = bits / 8;
            int tail = bits % 8;
            for (int i = 0; i < full; i++) if (hash[i] != 0) return false;
            return tail == 0 || (hash[full] & (0xFF << (8 - tail))) == 0;
        }

        private static long SolveClientProof(string gate, string proofNonce, string action, long timestamp,
            string requestNonce, int bits)
        {
            if (bits <= 0 || proofNonce.Length == 0) return -1;
            string prefix = gate + "|" + proofNonce + "|" + action + "|" + timestamp + "|" + requestNonce + "|";
            using (var sha = SHA256.Create())
            {
                for (long counter = 0; counter < 5000000; counter++)
                {
                    byte[] value = Encoding.UTF8.GetBytes(prefix + counter);
                    if (HasLeadingZeroBits(sha.ComputeHash(value), bits)) return counter;
                }
            }
            return -1;
        }

        // 检测 auth.php 是否为新版（含协议/滑块接口）
        public static async Task<(bool ok, string authVersion)> AuthPing()
        {
            await EnsureBaseAsync();
            try
            {
                var d = await GetJson(AuthUrl() + "?action=ping");
                if (d == null) return (false, "");
                if (JsonHelper.Int(d, "code") == 0)
                {
                    string v = JsonHelper.Str(d, "auth_version");
                    ClientLog.Write("auth.php 版本: " + (v.Length > 0 ? v : "旧版(无版本号)"));
                    return (true, v);
                }
                ClientLog.Write("auth.php ping 返回错误: code=" + JsonHelper.Int(d, "code") + " msg=" + JsonHelper.Str(d, "msg"));
                return (false, "");
            }
            catch (Exception ex)
            {
                ClientLog.Write("auth.php ping 异常: " + ex.Message);
                return (false, "");
            }
        }

        public static async Task<(bool ok, string title, string content, string time)> GetAnnouncement()
        {
            await EnsureBaseAsync();
            try
            {
                var d = await GetJson(GetBase() + "?action=announce");
                if (d == null || JsonHelper.Int(d, "code") != 0) return (false, "", "", "");
                return (true, JsonHelper.Str(d, "title"), JsonHelper.Str(d, "content"), JsonHelper.Str(d, "time"));
            }
            catch { return (false, "", "", ""); }
        }

        public static async Task<(bool ok, List<MasterServerInfo> servers, int page, int totalPages)> GetServers(int page)
        {
            await EnsureBaseAsync();
            var result = new List<MasterServerInfo>();
            try
            {
                var d = await GetJson(GetBase() + "?action=servers&page=" + page);
                if (d == null || JsonHelper.Int(d, "code") != 0) return (false, result, 1, 1);
                foreach (var r in JsonHelper.List(d, "servers"))
                {
                    result.Add(new MasterServerInfo
                    {
                        id = JsonHelper.Int(r, "id"),
                        name = JsonHelper.Str(r, "name"),
                        address = JsonHelper.Str(r, "address"),
                        port = JsonHelper.Int(r, "port", 80),
                        region = JsonHelper.Str(r, "region"),
                        max_players_per_room = JsonHelper.Int(r, "max_players_per_room", 8),
                        max_rooms = JsonHelper.Int(r, "max_rooms", 20),
                        required_mods = JsonHelper.Str(r, "required_mods"),
                        players = JsonHelper.Int(r, "players"),
                        rooms = JsonHelper.Int(r, "live_rooms"),
                        max_online = JsonHelper.Int(r, "max_online"),
                        has_password = JsonHelper.Int(r, "has_password"),
                        password = JsonHelper.Str(r, "password")
                    });
                }
                return (true, result, JsonHelper.Int(d, "page", 1), JsonHelper.Int(d, "total_pages", 1));
            }
            catch { return (false, result, 1, 1); }
        }

        public static async Task<(bool ok, int online)> Report(string version, string state, string serverAddr)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await Post(GetBase(), new Dictionary<string, object>
                {
                    ["action"] = "report",
                    ["version"] = version ?? "",
                    ["state"] = state ?? "main",
                    ["server_addr"] = serverAddr ?? ""
                });
                if (d == null || JsonHelper.Int(d, "code") != 0) return (false, 0);
                return (true, JsonHelper.Int(d, "online"));
            }
            catch { return (false, 0); }
        }

        private static readonly byte[] CryptoKey =
            SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes("SFM_MASTER_SECRET_9f3a7c1e"));

        public static string Encrypt(string plain)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = CryptoKey;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.GenerateIV();
                    byte[] pt = Encoding.UTF8.GetBytes(plain ?? "");
                    using (var enc = aes.CreateEncryptor())
                    {
                        byte[] ct = enc.TransformFinalBlock(pt, 0, pt.Length);
                        var all = new byte[16 + ct.Length];
                        Buffer.BlockCopy(aes.IV, 0, all, 0, 16);
                        Buffer.BlockCopy(ct, 0, all, 16, ct.Length);
                        return Convert.ToBase64String(all);
                    }
                }
            }
            catch { return ""; }
        }

        public static string Decrypt(string b64)
        {
            try
            {
                byte[] raw = Convert.FromBase64String(b64 ?? "");
                if (raw.Length < 17) return "";
                using (var aes = Aes.Create())
                {
                    aes.Key = CryptoKey;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    var iv = new byte[16];
                    Buffer.BlockCopy(raw, 0, iv, 0, 16);
                    aes.IV = iv;
                    using (var dec = aes.CreateDecryptor())
                    {
                        byte[] ct = new byte[raw.Length - 16];
                        Buffer.BlockCopy(raw, 16, ct, 0, ct.Length);
                        byte[] pt = dec.TransformFinalBlock(ct, 0, ct.Length);
                        return Encoding.UTF8.GetString(pt);
                    }
                }
            }
            catch { return ""; }
        }

        // 本地文件专用加密：密钥绑定本机+用户，避免硬编码密钥被反编译后直接解密本地凭据
        private static readonly byte[] LocalCryptoKey = BuildLocalKey();

        private static byte[] BuildLocalKey()
        {
            try
            {
                string seed = (Environment.MachineName ?? "m") + "|" + (Environment.UserName ?? "u") + "|SFM_LOCAL_7c3e_91f2";
                return SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(seed));
            }
            catch
            {
                return SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes("SFM_LOCAL_7c3e_91f2"));
            }
        }

        private static readonly byte[] LocalMacKey = BuildLocalMacKey();

        private static byte[] BuildLocalMacKey()
        {
            byte[] label = Encoding.UTF8.GetBytes("|SFM_LOCAL_MAC_V2");
            var material = new byte[LocalCryptoKey.Length + label.Length];
            Buffer.BlockCopy(LocalCryptoKey, 0, material, 0, LocalCryptoKey.Length);
            Buffer.BlockCopy(label, 0, material, LocalCryptoKey.Length, label.Length);
            using (var sha = SHA256.Create()) return sha.ComputeHash(material);
        }

        private static bool ConstantTimeEquals(byte[] a, int aOffset, byte[] b)
        {
            if (a == null || b == null || a.Length - aOffset != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < b.Length; i++) diff |= a[aOffset + i] ^ b[i];
            return diff == 0;
        }

        public static string EncryptLocal(string plain)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = LocalCryptoKey;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.GenerateIV();
                    byte[] pt = Encoding.UTF8.GetBytes(plain ?? "");
                    using (var enc = aes.CreateEncryptor())
                    {
                        byte[] ct = enc.TransformFinalBlock(pt, 0, pt.Length);
                        var signed = new byte[16 + ct.Length];
                        Buffer.BlockCopy(aes.IV, 0, signed, 0, 16);
                        Buffer.BlockCopy(ct, 0, signed, 16, ct.Length);
                        byte[] tag;
                        using (var mac = new HMACSHA256(LocalMacKey)) tag = mac.ComputeHash(signed);
                        var all = new byte[signed.Length + tag.Length];
                        Buffer.BlockCopy(signed, 0, all, 0, signed.Length);
                        Buffer.BlockCopy(tag, 0, all, signed.Length, tag.Length);
                        return "v2." + Convert.ToBase64String(all);
                    }
                }
            }
            catch { return ""; }
        }

        private static string DecryptLocalCipher(byte[] raw)
        {
            if (raw == null || raw.Length < 17) return "";
            using (var aes = Aes.Create())
            {
                aes.Key = LocalCryptoKey;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                var iv = new byte[16];
                Buffer.BlockCopy(raw, 0, iv, 0, 16);
                aes.IV = iv;
                using (var dec = aes.CreateDecryptor())
                {
                    byte[] ct = new byte[raw.Length - 16];
                    Buffer.BlockCopy(raw, 16, ct, 0, ct.Length);
                    byte[] pt = dec.TransformFinalBlock(ct, 0, ct.Length);
                    return Encoding.UTF8.GetString(pt);
                }
            }
        }

        public static string DecryptLocal(string encoded)
        {
            try
            {
                string value = encoded ?? "";
                if (!value.StartsWith("v2.", StringComparison.Ordinal))
                    return DecryptLocalCipher(Convert.FromBase64String(value)); // 兼容旧版本地文件

                byte[] all = Convert.FromBase64String(value.Substring(3));
                if (all.Length < 16 + 16 + 32) return "";
                int dataLength = all.Length - 32;
                byte[] expected;
                using (var mac = new HMACSHA256(LocalMacKey)) expected = mac.ComputeHash(all, 0, dataLength);
                if (!ConstantTimeEquals(all, dataLength, expected)) return "";
                var raw = new byte[dataLength];
                Buffer.BlockCopy(all, 0, raw, 0, dataLength);
                return DecryptLocalCipher(raw);
            }
            catch { return ""; }
        }

        public static bool StagedMatches(string md5)
        {
            try
            {
                var dir = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "SFMOnline_versions");
                if (!System.IO.Directory.Exists(dir)) return false;
                foreach (var f in System.IO.Directory.GetFiles(dir, "SFMOnline_*.dll"))
                {
                    using (var fs = System.IO.File.OpenRead(f))
                    using (var md = MD5.Create())
                    {
                        var h = BitConverter.ToString(md.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                        if (string.Equals(h, md5, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }
        public static string SelfMd5()
        {
            try
            {
                var path = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "plugins", "SFMOnline.dll");
                using (var fs = System.IO.File.OpenRead(path))
                using (var md5 = MD5.Create())
                    return BitConverter.ToString(md5.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
            }
            catch { return ""; }
        }

        public static async Task<(bool ok, string msg)> AdminUidChange(string token, string username, long uid, long newUid)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "admin_uid_change", ["token"] = token ?? "", ["username"] = username ?? "", ["uid"] = uid, ["new_uid"] = newUid });
                return (d.ok, d.ok ? "OK" : JsonHelper.Str(d.data, "msg"));
            }
            catch { return (false, "network error"); }
        }
        private static string AuthUrl()
        {
            string b = GetBase();
            return b.IndexOf("all.php", StringComparison.Ordinal) >= 0
                ? b
                : b.Replace("master.php", "auth.php");
        }

        public static async Task<(bool ok, Dictionary<string, object> data)> PostEncrypted(
            string url, Dictionary<string, object> body)
        {
            string action = body != null && body.TryGetValue("action", out var av) ? Convert.ToString(av) ?? "" : "";
            bool needsProof = NeedsClientProof(action);
            if (needsProof) await ProofLock.WaitAsync();
            try
            {
                if (needsProof)
                {
                    // 敏感操作每次使用独立的一次性挑战，防止抓包后重复提交。
                    if (!await Handshake()) return (false, null);
                }
                else
                {
                    await EnsureGateAsync();
                }

                for (int attempt = 0; attempt < 2; attempt++)
                {
                    long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    string requestNonce = Guid.NewGuid().ToString("N");
                    body["gate"] = _gate;
                    body["t"] = timestamp;
                    body["nonce"] = requestNonce;
                    body["client_version"] = PluginInfo.Version;

                    if (needsProof && _proofNonce.Length > 0 && _proofBits > 0)
                    {
                        long counter = await Task.Run(() =>
                            SolveClientProof(_gate, _proofNonce, action, timestamp, requestNonce, _proofBits));
                        if (counter < 0)
                        {
                            ClientLog.Write("客户端安全计算失败: " + action);
                            return (false, null);
                        }
                        body["pow_nonce"] = _proofNonce;
                        body["pow_bits"] = _proofBits;
                        body["pow_counter"] = counter;
                    }

                    string enc = Encrypt(MiniJson.Serialize(body));
                    if (enc.Length == 0) return (false, null);
                    var resp = await Http.PostAsync(url,
                        new FormUrlEncodedContent(new Dictionary<string, string> { ["enc"] = enc }));
                    string text = await resp.Content.ReadAsStringAsync();
                    var d = MiniJson.ParseObject(text);
                    int code = d != null ? JsonHelper.Int(d, "code") : -99;

                    if ((code == -5 || code == -7) && attempt == 0)
                    {
                        _gate = "";
                        _proofNonce = "";
                        if (!await Handshake()) return (false, d);
                        continue;
                    }

                    bool ok = d != null && code == 0;
                    if (!ok) ClientLog.Write("POST加密失败: [" + action + "] " + url + " HTTP=" + (int)resp.StatusCode + " body=" + Truncate(text));
                    return (ok, d);
                }
                return (false, null);
            }
            catch (Exception ex)
            {
                ClientLog.Write("POST加密异常: " + url + " -> " + ex.Message);
                return (false, null);
            }
            finally
            {
                if (needsProof)
                {
                    _gate = "";
                    _proofNonce = "";
                    _proofBits = 0;
                    ProofLock.Release();
                }
            }
        }

        public static async Task<(bool ok, string sid, string image, string msg)> Captcha()
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "captcha" });
                if (!d.ok) return (false, "", "", d.data != null ? JsonHelper.Str(d.data, "msg") : "请求失败");
                return (true, JsonHelper.Str(d.data, "sid"), JsonHelper.Str(d.data, "image"), "");
            }
            catch { return (false, "", "", "网络错误"); }
        }

        public static async Task<(bool ok, string msg)> ResendCode(string type, string email, string sid, string captcha)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "resend_code", ["type"] = type ?? "", ["email"] = email ?? "", ["sid"] = sid ?? "", ["captcha"] = captcha ?? "" });
                return (d.ok, d.ok ? "OK" : JsonHelper.Str(d.data, "msg"));
            }
            catch { return (false, "network error"); }
        }
        public static async Task<(bool ok, string msg)> SendCode(string type, string email, string sid, string captcha, string mb)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object>
                    {
                        ["action"] = "send_code", ["type"] = type, ["email"] = email,
                        ["sid"] = sid, ["captcha"] = captcha, ["motherboard_id"] = mb
                    });
                return (d.ok, d.ok ? "验证码已发送" : (d.data != null ? JsonHelper.Str(d.data, "msg") : "发送失败"));
            }
            catch { return (false, "网络错误"); }
        }

        public static async Task<(bool ok, string msg, string token, long uid, string username)> Register(
            string username, string email, string password, string code, bool agree, string mb)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object>
                    {
                        ["action"] = "register", ["username"] = username, ["email"] = email,
                        ["password"] = password, ["code"] = code, ["agreement"] = agree ? 1 : 0,
                        ["motherboard_id"] = mb
                    });
                if (d.ok) return (true, "注册成功", JsonHelper.Str(d.data, "token"),
                    JsonHelper.Int(d.data, "uid"), username);
                return (false, d.data != null ? JsonHelper.Str(d.data, "msg") : "注册失败", "", 0, "");
            }
            catch { return (false, "网络错误", "", 0, ""); }
        }

        public static async Task<(bool ok, string msg, string token, long uid, string username, string email)> Login(
            string account, string password, string code, string mb)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object>
                    {
                        ["action"] = "login", ["account"] = account, ["password"] = password ?? "",
                        ["code"] = code ?? "", ["motherboard_id"] = mb
                    });
                if (d.ok) return (true, "登录成功", JsonHelper.Str(d.data, "token"),
                    JsonHelper.Int(d.data, "uid"), JsonHelper.Str(d.data, "username"), JsonHelper.Str(d.data, "email"));
                return (false, d.data != null ? JsonHelper.Str(d.data, "msg") : "登录失败", "", 0, "", "");
            }
            catch { return (false, "网络错误", "", 0, "", ""); }
        }

        public static async Task<(bool ok, string msg, string token, long uid, string username, string email,
            string title, string titleColor, int online, int registered, int code)> LoginFull(string account, string password, string code, string mb,
            string agUserV, string agPrivacyV, string agNonce, string sid = "", string captcha = "")
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object>
                    {
                        ["action"] = "login", ["account"] = account, ["password"] = password ?? "",
                        ["code"] = code ?? "", ["motherboard_id"] = mb,
                        ["sid"] = sid ?? "", ["captcha"] = captcha ?? "",
                        ["ag_user_v"] = agUserV ?? "", ["ag_privacy_v"] = agPrivacyV ?? "", ["ag_nonce"] = agNonce ?? "",
                        ["ag_accept_user"] = 1, ["ag_accept_privacy"] = 1
                    });
                if (d.ok) return (true, "登录成功", JsonHelper.Str(d.data, "token"),
                    JsonHelper.Int(d.data, "uid"), JsonHelper.Str(d.data, "username"), JsonHelper.Str(d.data, "email"),
                    JsonHelper.Str(d.data, "title"), JsonHelper.Str(d.data, "title_color"), JsonHelper.Int(d.data, "online"), JsonHelper.Int(d.data, "registered"), 0);
                return (false, d.data != null ? JsonHelper.Str(d.data, "msg") : "登录失败", "", 0, "", "", "", "", 0, 0,
                    d.data != null ? JsonHelper.Int(d.data, "code") : -99);
            }
            catch { return (false, "网络错误", "", 0, "", "", "", "", 0, 0, -99); }
        }

        public static async Task<(bool ok, string msg, string token, long uid, string username, string email,
            string title, string titleColor, int online, int registered, int code)> RefreshLogin(
            string account, string password, string mb)
        {
            var agreement = await AgreementInfo();
            if (!agreement.ok)
                return (false, "无法刷新协议凭证", "", 0, "", "", "", "", 0, 0, -4);
            return await LoginFull(account, password, "", mb,
                agreement.userV, agreement.privacyV, agreement.nonce);
        }

        public static async Task<(bool ok, string msg, int code)> RegisterNoLogin(string username, string email, string password, string code, bool agree, string mb,
            string agUserV, string agPrivacyV, string agNonce, string sid = "", string captcha = "")
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object>
                    {
                        ["action"] = "register", ["username"] = username, ["email"] = email,
                        ["password"] = password, ["code"] = code, ["agreement"] = agree ? 1 : 0,
                        ["motherboard_id"] = mb, ["sid"] = sid ?? "", ["captcha"] = captcha ?? "",
                        ["ag_user_v"] = agUserV ?? "", ["ag_privacy_v"] = agPrivacyV ?? "", ["ag_nonce"] = agNonce ?? "",
                        ["ag_accept_user"] = 1, ["ag_accept_privacy"] = 1
                    });
                if (d.ok) return (true, "注册成功，请登录", 0);
                return (false, d.data != null ? JsonHelper.Str(d.data, "msg") : "注册失败",
                    d.data != null ? JsonHelper.Int(d.data, "code") : -99);
            }
            catch { return (false, "网络错误", -99); }
        }

        public static async Task<(bool ok, List<Dictionary<string, object>> credits)> Credits()
        {
            await EnsureBaseAsync();
            try
            {
                var d = await GetJson(GetBase() + "?action=credits");
                if (d == null || JsonHelper.Int(d, "code") != 0) return (false, new List<Dictionary<string, object>>());
                return (true, JsonHelper.List(d, "credits"));
            }
            catch { return (false, new List<Dictionary<string, object>>()); }
        }

        public static async Task<(bool ok, List<Dictionary<string, object>> logs)> Changelog()
        {
            await EnsureBaseAsync();
            try
            {
                var d = await GetJson(GetBase() + "?action=changelog");
                if (d == null || JsonHelper.Int(d, "code") != 0) return (false, new List<Dictionary<string, object>>());
                return (true, JsonHelper.List(d, "logs"));
            }
            catch { return (false, new List<Dictionary<string, object>>()); }
        }

        public static async Task<(bool ok, string msg)> PubChatSend(string token, string message)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(), new Dictionary<string, object>
                { ["action"] = "pubchat_send", ["token"] = token ?? "", ["message"] = message ?? "" });
                return (d.ok, d.ok ? "已发送" : (d.data != null ? JsonHelper.Str(d.data, "msg") : "发送失败"));
            }
            catch { return (false, "网络错误"); }
        }

        public static async Task<(bool ok, List<Dictionary<string, object>> messages)> PubChatList(long after)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(), new Dictionary<string, object>
                { ["action"] = "pubchat_list", ["after"] = after });
                if (!d.ok) return (false, new List<Dictionary<string, object>>());
                return (true, JsonHelper.List(d.data, "messages"));
            }
            catch { return (false, new List<Dictionary<string, object>>()); }
        }

        public static async Task<(bool ok, List<Dictionary<string, object>> users)> FriendSearch(string token, string kw)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(), new Dictionary<string, object>
                { ["action"] = "friend_search", ["token"] = token ?? "", ["kw"] = kw ?? "" });
                if (!d.ok) return (false, new List<Dictionary<string, object>>());
                return (true, JsonHelper.List(d.data, "users"));
            }
            catch { return (false, new List<Dictionary<string, object>>()); }
        }

        public static async Task<bool> FriendAdd(string token, long friendUid)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(), new Dictionary<string, object>
                { ["action"] = "friend_add", ["token"] = token ?? "", ["friend_uid"] = friendUid });
                return d.ok;
            }
            catch { return false; }
        }

        public static async Task<bool> FriendDelete(string token, long friendUid)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(), new Dictionary<string, object>
                { ["action"] = "friend_delete", ["token"] = token ?? "", ["friend_uid"] = friendUid });
                return d.ok;
            }
            catch { return false; }
        }

        public static async Task<(bool ok, List<Dictionary<string, object>> friends)> FriendList(string token)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "friend_list", ["token"] = token ?? "" });
                return d.ok
                    ? (true, JsonHelper.List(d.data, "friends"))
                    : (false, new List<Dictionary<string, object>>());
            }
            catch { return (false, new List<Dictionary<string, object>>()); }
        }

        public static async Task<(bool ok, List<Dictionary<string, object>> requests)> FriendRequests(string token)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "friend_requests", ["token"] = token ?? "" });
                return d.ok
                    ? (true, JsonHelper.List(d.data, "requests"))
                    : (false, new List<Dictionary<string, object>>());
            }
            catch { return (false, new List<Dictionary<string, object>>()); }
        }

        public static async Task<bool> FriendAccept(string token, long friendUid)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(), new Dictionary<string, object>
                { ["action"] = "friend_accept", ["token"] = token ?? "", ["friend_uid"] = friendUid });
                return d.ok;
            }
            catch { return false; }
        }

        public static async Task<bool> FriendReject(string token, long friendUid)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(), new Dictionary<string, object>
                { ["action"] = "friend_reject", ["token"] = token ?? "", ["friend_uid"] = friendUid });
                return d.ok;
            }
            catch { return false; }
        }

        public static async Task<(bool ok, bool hide, bool allow)> FriendSettingsGet(string token)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "friend_settings", ["token"] = token ?? "" });
                if (!d.ok) return (false, false, true);
                return (true, JsonHelper.Int(d.data, "hide_server") == 1, JsonHelper.Int(d.data, "allow_search") != 0);
            }
            catch { return (false, false, true); }
        }
        public static async Task<bool> FriendSettings(string token, bool hideServer, bool allowSearch)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(), new Dictionary<string, object>
                { ["action"] = "friend_settings", ["token"] = token ?? "", ["hide_server"] = hideServer ? 1 : 0, ["allow_search"] = allowSearch ? 1 : 0 });
                return d.ok;
            }
            catch { return false; }
        }

        public static async Task<(bool ok, string msg, int code)> Forgot(string email, string code, string newPass,
            string agUserV, string agPrivacyV, string agNonce)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object>
                    {
                        ["action"] = "forgot", ["email"] = email, ["code"] = code, ["password"] = newPass,
                        ["ag_user_v"] = agUserV ?? "", ["ag_privacy_v"] = agPrivacyV ?? "", ["ag_nonce"] = agNonce ?? "",
                        ["ag_accept_user"] = 1, ["ag_accept_privacy"] = 1
                    });
                if (d.ok) return (true, "密码已重置", 0);
                return (false, d.data != null ? JsonHelper.Str(d.data, "msg") : "重置失败",
                    d.data != null ? JsonHelper.Int(d.data, "code") : -99);
            }
            catch { return (false, "网络错误", -99); }
        }

        public static async Task<(bool ok, string msg, string username)> Rename(string token, string newName)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object>
                    {
                        ["action"] = "rename", ["token"] = token ?? "", ["username"] = newName ?? ""
                    });
                if (d.ok) return (true, "用户名已修改", JsonHelper.Str(d.data, "username"));
                return (false, d.data != null ? JsonHelper.Str(d.data, "msg") : "修改失败", "");
            }
            catch { return (false, "网络错误", ""); }
        }

        public static async Task<(bool ok, string msg)> ProfileSet(string token, string bio)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "profile_set", ["token"] = token ?? "", ["bio"] = bio ?? "" });
                return (d.ok, d.ok ? "简介已保存" : (d.data != null ? JsonHelper.Str(d.data, "msg") : "保存失败"));
            }
            catch { return (false, "网络错误"); }
        }

        public static async Task<Dictionary<string, object>> ProfileGet(long uid)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "profile_get", ["uid"] = uid });
                return d.ok && d.data != null ? JsonHelper.Object(d.data, "profile") : null;
            }
            catch { return null; }
        }

        public static async Task<(bool ok, string msg)> DmSend(string token, long toUid, string message)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "dm_send", ["token"] = token ?? "", ["to_uid"] = toUid, ["message"] = message ?? "" });
                return (d.ok, d.ok ? "已发送" : (d.data != null ? JsonHelper.Str(d.data, "msg") : "发送失败"));
            }
            catch { return (false, "网络错误"); }
        }

        public static async Task<List<Dictionary<string, object>>> DmList(string token, long peerUid, long after)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "dm_list", ["token"] = token ?? "", ["peer_uid"] = peerUid, ["after"] = after });
                return d.ok ? JsonHelper.List(d.data, "messages") : new List<Dictionary<string, object>>();
            }
            catch { return new List<Dictionary<string, object>>(); }
        }

        public static async Task<(bool ok, string msg)> ReportAdd(string token, long targetUid, string reason)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "report_add", ["token"] = token ?? "", ["target_uid"] = targetUid, ["reason"] = reason ?? "" });
                return (d.ok, d.ok ? "举报已提交" : (d.data != null ? JsonHelper.Str(d.data, "msg") : "举报失败"));
            }
            catch { return (false, "网络错误"); }
        }

        public static async Task<(bool ok, Dictionary<string, object> data)> AdminCall(string token, string username, string action, long uid, string extra, string titleColor = null)
        {
            await EnsureBaseAsync();
            try
            {
                var body = new Dictionary<string, object> { ["action"] = action, ["token"] = token ?? "", ["username"] = username ?? "", ["uid"] = uid, ["op"] = extra ?? "" };
                if (titleColor != null) body["title_color"] = titleColor;
                var d = await PostEncrypted(AuthUrl(), body);
                return (d.ok, d.data);
            }
            catch { return (false, null); }
        }

        public static async Task<(bool ok, string msg, int code, string token)> RelayApply(string domain, int port, string password, long uid, string username, string ip, string token)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "relay_apply", ["domain"] = domain ?? "", ["port"] = port, ["password"] = password ?? "", ["uid"] = uid, ["username"] = username ?? "", ["ip"] = ip ?? "", ["token"] = token ?? "" });
                bool ok = d.ok;
                string tk = ok ? JsonHelper.Str(d.data, "token") : "";
                if (ok && tk.Length == 0) tk = JsonHelper.Str(d.data, "sid");
                return (ok, d.ok ? "OK" : JsonHelper.Str(d.data, "msg"), JsonHelper.Int(d.data, "code", ok ? 0 : -1), tk);
            }
            catch { return (false, "network error", -1, ""); }
        }

        public static async Task<(bool ok, int online, int rooms, int maxOnline, int maxRooms, int latency)> RelayStats(string token, string url)
        {
            try
            {
                var d = await GetJson((url ?? "").TrimEnd('/') + "?action=stats");
                if (d == null || JsonHelper.Int(d, "code") != 0) return (false, 0, 0, 0, 0, -1);
                return (true, JsonHelper.Int(d, "online"), JsonHelper.Int(d, "rooms"), JsonHelper.Int(d, "max_online"), JsonHelper.Int(d, "max_rooms"), JsonHelper.Int(d, "latency_ms", -1));
            }
            catch { return (false, 0, 0, 0, 0, -1); }
        }
        public static async Task<List<Dictionary<string, object>>> AdminUsers(string token, string username, string kw)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "admin_users", ["token"] = token ?? "", ["username"] = username ?? "", ["kw"] = kw ?? "" });
                return d.ok ? JsonHelper.List(d.data, "users") : null;
            }
            catch { return null; }
        }

        public static async Task<List<Dictionary<string, object>>> AdminReportList(string token, string username)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "admin_report_list", ["token"] = token ?? "", ["username"] = username ?? "" });
                return d.ok ? JsonHelper.List(d.data, "reports") : null;
            }
            catch { return null; }
        }

        public static async Task<(bool ok, string msg)> AdminReportHandle(string token, string username, long id, string status, string note)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "admin_report_handle", ["token"] = token ?? "", ["username"] = username ?? "", ["id"] = id, ["status"] = status ?? "", ["note"] = note ?? "" });
                return (d.ok, d.ok ? "OK" : JsonHelper.Str(d.data, "msg"));
            }
            catch { return (false, "network error"); }
        }

        public static async Task<List<Dictionary<string, object>>> AdminPubchatList(string token, string username)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "admin_pubchat_list", ["token"] = token ?? "", ["username"] = username ?? "" });
                return d.ok ? JsonHelper.List(d.data, "messages") : null;
            }
            catch { return null; }
        }

        public static async Task<(bool ok, string msg)> AdminPubchatDelete(string token, string username, long id)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "admin_pubchat_del", ["token"] = token ?? "", ["username"] = username ?? "", ["id"] = id });
                return (d.ok, d.ok ? "OK" : JsonHelper.Str(d.data, "msg"));
            }
            catch { return (false, "network error"); }
        }
        public static async Task<(bool ok, string userV, string userUrl, string userTitle,
            string privacyV, string privacyUrl, string privacyTitle, string nonce)> AgreementInfo()
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "agreement_info" });
                if (d.ok && d.data != null)
                    return ParseAgreement(d.data);
                ClientLog.Write("auth.php 协议接口不可用，改用 master.php 获取协议");
                var d2 = await GetJson(GetBase() + "?action=agreement_info");
                if (d2 != null && JsonHelper.Int(d2, "code") == 0)
                    return ParseAgreement(d2);
                ClientLog.Write("服务器协议接口不可用（agreement_info 缺失）");
                return (false, "", "", "", "", "", "", "");
            }
            catch { return (false, "", "", "", "", "", "", ""); }
        }

        private static (bool ok, string userV, string userUrl, string userTitle,
            string privacyV, string privacyUrl, string privacyTitle, string nonce) ParseAgreement(Dictionary<string, object> data)
        {
            var u = JsonHelper.Object(data, "user");
            var p = JsonHelper.Object(data, "privacy");
            return (true,
                JsonHelper.Str(u, "version"), JsonHelper.Str(u, "url"), JsonHelper.Str(u, "title"),
                JsonHelper.Str(p, "version"), JsonHelper.Str(p, "url"), JsonHelper.Str(p, "title"),
                JsonHelper.Str(data, "nonce"));
        }

        public static async Task<(bool ok, Dictionary<string, object> data)> TokenCheck(string token)
        {
            await EnsureBaseAsync();
            try
            {
                return await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object> { ["action"] = "token_check", ["token"] = token ?? "" });
            }
            catch { return (false, null); }
        }

        public static async Task<(bool ok, int online, int code)> Heartbeat(string token, string version,
            string serverAddr, string serverName, string serverDesc)
        {
            await EnsureBaseAsync();
            try
            {
                var d = await PostEncrypted(AuthUrl(),
                    new Dictionary<string, object>
                    {
                        ["action"] = "heartbeat", ["token"] = token ?? "", ["version"] = version ?? "",
                        ["server_addr"] = serverAddr ?? "", ["server_name"] = serverName ?? "",
                        ["server_desc"] = serverDesc ?? ""
                    });
                if (!d.ok)
                {
                    return (false, 0, d.data != null ? JsonHelper.Int(d.data, "code") : -99);
                }
                return (true, JsonHelper.Int(d.data, "online"), 0);
            }
            catch { return (false, 0, -99); }
        }

        public static async Task<(bool ok, string version, string url, string md5, string note, int force, int forceReplace)> GetVersion()
        {
            string[] urls = { StableUpdateUrl, GetBase() + "?action=version" };
            foreach (var u in urls)
            {
                try
                {
                    var d = await GetJson(u);
                    if (d != null && JsonHelper.Int(d, "code") == 0)
                        return (true, JsonHelper.Str(d, "version"), JsonHelper.Str(d, "url"),
                            JsonHelper.Str(d, "md5"), JsonHelper.Str(d, "note"),
                            JsonHelper.Int(d, "force"), JsonHelper.Int(d, "force_replace"));
                }
                catch { }
            }
            return (false, "", "", "", "", 0, 0);
        }
        public static async Task<bool> Download(string url, string destPath)
        {
            await EnsureBaseAsync();
            try
            {
                using (var resp = await Http.GetAsync(url))
                {
                    if (!resp.IsSuccessStatusCode) return false;
                    byte[] bytes = await resp.Content.ReadAsByteArrayAsync();
                    System.IO.File.WriteAllBytes(destPath, bytes);
                    return bytes.Length > 1000;
                }
            }
            catch { return false; }
        }

        // 每个“域名:端口”在本次游戏进程中只探测一次，避免刷新列表反复打联机服。
        private static readonly ConcurrentDictionary<string, Task<int>> LatencyTasks =
            new ConcurrentDictionary<string, Task<int>>(StringComparer.OrdinalIgnoreCase);

        public static Task<int> MeasureLatency(string host, int port)
        {
            string key = (host ?? "").Trim().ToLowerInvariant() + ":" + port;
            return LatencyTasks.GetOrAdd(key, _ => MeasureLatencyCore(host, port));
        }

        private static async Task<int> MeasureLatencyCore(string host, int port)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using (var tcp = new TcpClient())
                {
                    var t = tcp.ConnectAsync(host, port);
                    var done = await Task.WhenAny(t, Task.Delay(1500));
                    if (done != t || !tcp.Connected) return -1;
                }
                sw.Stop();
                return (int)sw.ElapsedMilliseconds;
            }
            catch { return -1; }
        }

        private static async Task<Dictionary<string, object>> GetJson(string url)
        {
            try
            {
                var resp = await Http.GetAsync(url);
                string text = await resp.Content.ReadAsStringAsync();
                var d = MiniJson.ParseObject(text);
                if (d == null) ClientLog.Write("GET无法解析: " + url + " HTTP=" + (int)resp.StatusCode + " body=" + Truncate(text));
                return d;
            }
            catch (Exception ex)
            {
                ClientLog.Write("GET异常: " + url + " -> " + ex.Message);
                return null;
            }
        }

        private static async Task<Dictionary<string, object>> Post(string url, Dictionary<string, object> body)
        {
            try
            {
                var fd = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["payload"] = MiniJson.Serialize(body)
                });
                var resp = await Http.PostAsync(url, fd);
                string text = await resp.Content.ReadAsStringAsync();
                var d = MiniJson.ParseObject(text);
                if (d == null) ClientLog.Write("POST无法解析: " + url + " HTTP=" + (int)resp.StatusCode + " body=" + Truncate(text));
                return d;
            }
            catch (Exception ex)
            {
                ClientLog.Write("POST异常: " + url + " -> " + ex.Message);
                return null;
            }
        }
    }
}
