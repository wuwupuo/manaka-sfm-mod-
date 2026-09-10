using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SFMOnline
{
    public class ServerRoomInfo
    {
        public string room_id = "";
        public string room_name = "";
        public string host_name = "";
        public string host_address = "";
        public int port;
        public int has_password;
        public int max_players;
        public int current_players;
        public string player_display = "";
        public string password_status = "";
        public string game_version = "";
        public string created_at = "";
        public string expire_at = "";
        public string status = "";
    }

    public class ServerListResponse
    {
        public int code = -1;
        public string msg = "";
        public string server_name = "";
        public ServerRoomInfo[] rooms = new ServerRoomInfo[0];
        public int total;
        public int max_rooms;
        public int current_rooms;
        public int is_full;
        public int queue;
        public string queue_msg = "";
        public string announcement = "";
        public string announcement_time = "";
        public ServerChatMessage[] messages = new ServerChatMessage[0];
        public long server_time;
    }

    public class ServerChatMessage
    {
        public string room_id = "";
        public string player_id = "";
        public string player_name = "";
        public string ip = "";
        public string message = "";
        public string created_at = "";
    }

    public class ServerSettingsInfo
    {
        public int max_rooms_total = 100;
        public int max_rooms_per_ip = 1;
        public int max_rooms_per_hour = 5;
        public int room_lifetime = 43200;
        public int room_timeout = 60;
        public int max_players = 8;
        public int chat_log_days = 1;
        public int action_log_days = 2;
        public int captcha_expire = 3600;
    }

    public class CaptchaResult
    {
        public string imageBase64 = "";
        public string text = "";
    }

    public class RelayMsg
    {
        public string player_id = "";
        public byte type;
        public string payload = "";
    }

    public class RelayPollResult
    {
        public long after;
        public List<RelayMsg> messages = new List<RelayMsg>();
    }

    // 公共服务器 HTTP API 客户端（服务端保持原样，客户端这边完整重写）
    internal static class ServerAPI
    {
        private static string _apiBase = "";
        private static string _serverAddress = "";
        private static bool _isAdmin;
        private static string _myRoomToken = "";
        private static string _myRoomId = "";
        private static string _csrf = "";
        private static string _sessionId = "";
        private static int _serverPort;
        private static readonly HttpClient _http = new HttpClient(
            new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
        { Timeout = TimeSpan.FromSeconds(8) };

        public static void SetServerAddress(string address, int port = 0)
        {
            _serverAddress = address ?? "";
            _serverPort = port;
            string url = _serverAddress.Trim();
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "http://" + url;
            // 地址里已自带端口（如 域名:8080）时不再追加端口
            bool addressHasPort = false;
            int colon = _serverAddress.LastIndexOf(':');
            if (colon > 0 && colon < _serverAddress.Length - 1)
                addressHasPort = int.TryParse(_serverAddress.Substring(colon + 1), out _);
            if (port > 0 && port != 80 && port != 443 && !addressHasPort)
                url = url.TrimEnd('/') + ":" + port;
            _apiBase = url.TrimEnd('/') + "/sfm_api/index.php";
        }

        public static string GetServerAddress() => _serverAddress;
        public static string GetApiBase() => _apiBase;
        public static bool IsConnected() => !string.IsNullOrEmpty(_apiBase);
        public static bool IsAdmin() => _isAdmin;
        public static string GetMyRoomToken() => _myRoomToken;
        public static string GetMyRoomId() => _myRoomId;

        // 只用域名自动定位接口：依次尝试 https / http 下的 /sfm_api/index.php
        public static async Task<bool> ProbeAsync()
        {
            var candidates = BuildCandidates();
            foreach (var baseUrl in candidates)
            {
                try
                {
                    _apiBase = baseUrl;
                    using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        var req = new HttpRequestMessage(HttpMethod.Get, baseUrl + "?action=ping");
                        var resp = await _http.SendAsync(req, cts.Token);
                        string text = await resp.Content.ReadAsStringAsync();
                        var data = MiniJson.ParseObject(text);
                        if (data != null && JsonHelper.Int(data, "code") == 0)
                            return true;
                    }
                }
                catch { }
            }
            _apiBase = "";
            return false;
        }

        private static List<string> BuildCandidates()
        {
            var result = new List<string>();
            string raw = (_serverAddress ?? "").Trim().TrimEnd('/');
            if (raw.Length == 0) return result;

            string scheme = "";
            string hostPart = raw;
            if (raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                scheme = "https";
                hostPart = raw.Substring(8).TrimEnd('/');
            }
            else if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                scheme = "http";
                hostPart = raw.Substring(7).TrimEnd('/');
            }

            string hostOnly = hostPart;
            string path = "";
            int slash = hostPart.IndexOf('/');
            if (slash >= 0)
            {
                hostOnly = hostPart.Substring(0, slash);
                path = hostPart.Substring(slash).TrimEnd('/');
            }

            string hostName = hostOnly;
            bool hasExplicitPort = false;
            int colon = hostOnly.LastIndexOf(':');
            if (colon > 0 && int.TryParse(hostOnly.Substring(colon + 1), out _))
            {
                hostName = hostOnly.Substring(0, colon);
                hasExplicitPort = true;
            }

            string portSuffix = "";
            if (!hasExplicitPort && _serverPort > 0 && _serverPort != 80 && _serverPort != 443)
                portSuffix = ":" + _serverPort;

            // 常见部署路径自动扫描：根目录 / public_html / www
            var pathList = new List<string>();
            if (path.EndsWith(".php"))
                pathList.Add(path);
            else if (path.Length > 0)
                pathList.Add(path + "/sfm_api/index.php");
            else
            {
                pathList.Add("/sfm_api/index.php");
                pathList.Add("/public_html/sfm_api/index.php");
                pathList.Add("/www/sfm_api/index.php");
            }

            var schemeList = scheme == "https" ? new List<string> { "https" }
                : scheme == "http" ? new List<string> { "http" }
                : new List<string> { "https", "http" };

            foreach (var s in schemeList)
                foreach (var p in pathList)
                    result.Add(s + "://" + hostName + portSuffix + p);

            // 去重
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            result.RemoveAll(u => !seen.Add(u));
            return result;
        }

        private static bool AdminOk(Dictionary<string, object> data)
        {
            if (data == null || JsonHelper.Int(data, "code") != 0) return false;
            if (data.ContainsKey("csrf"))
                _csrf = JsonHelper.Str(data, "csrf");
            return true;
        }

        private static async Task<Dictionary<string, object>> GetJson(string url)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(_sessionId))
                req.Headers.TryAddWithoutValidation("X-SFM-Token", _sessionId);
            var resp = await _http.SendAsync(req);
            string text = await resp.Content.ReadAsStringAsync();
            return MiniJson.ParseObject(text);
        }

        private static async Task<Dictionary<string, object>> PostJson(string url, Dictionary<string, object> body)
        {
            // 部分虚拟主机读不到 JSON 请求体，统一改用表单 payload 提交，服务器两种格式都兼容
            var json = MiniJson.Serialize(body);
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["payload"] = json
            });
            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            if (!string.IsNullOrEmpty(_sessionId))
                req.Headers.TryAddWithoutValidation("X-SFM-Token", _sessionId);
            var resp = await _http.SendAsync(req);
            string text = await resp.Content.ReadAsStringAsync();
            return MiniJson.ParseObject(text);
        }

        public static async Task<bool> Login(string username, string password)
        {
            try
            {
                string loginUrl = _apiBase.Replace("index.php", "login.php");
                var data = await PostJson(loginUrl, new Dictionary<string, object>
                {
                    ["username"] = username ?? "",
                    ["password"] = password ?? ""
                });
                if (data != null && JsonHelper.Int(data, "code") == 0)
                {
                    _isAdmin = true;
                    _csrf = JsonHelper.Str(data, "csrf");
                    _sessionId = JsonHelper.Str(data, "token");
                    return true;
                }
                _isAdmin = false;
                _csrf = "";
                _sessionId = "";
                return false;
            }
            catch { return false; }
        }

        public static void Logout()
        {
            _isAdmin = false;
            _csrf = "";
            _sessionId = "";
        }

        public static async Task<ServerListResponse> ListRooms(string version = null)
        {
            var result = new ServerListResponse();
            try
            {
                string url = _apiBase + "?action=list";
                if (!string.IsNullOrEmpty(version))
                    url += "&version=" + Uri.EscapeDataString(version);
                var data = await GetJson(url);
                if (data == null) return result;

                result.code = JsonHelper.Int(data, "code");
                result.msg = JsonHelper.Str(data, "msg");
                result.total = JsonHelper.Int(data, "total");
                result.max_rooms = JsonHelper.Int(data, "max_rooms");
                result.current_rooms = JsonHelper.Int(data, "current_rooms");
                result.is_full = JsonHelper.Int(data, "is_full");
                result.queue = JsonHelper.Int(data, "queue");
                result.queue_msg = JsonHelper.Str(data, "queue_msg");
                result.announcement = JsonHelper.Str(data, "announcement");
                result.announcement_time = JsonHelper.Str(data, "announcement_time");
                result.server_name = JsonHelper.Str(data, "server_name");

                var list = JsonHelper.List(data, "rooms");
                var rooms = new List<ServerRoomInfo>();
                foreach (var r in list)
                {
                    rooms.Add(new ServerRoomInfo
                    {
                        room_id = JsonHelper.Str(r, "room_id"),
                        room_name = JsonHelper.Str(r, "room_name"),
                        host_name = JsonHelper.Str(r, "host_name"),
                        host_address = JsonHelper.Str(r, "host_address"),
                        port = JsonHelper.Int(r, "port"),
                        has_password = JsonHelper.Int(r, "has_password"),
                        max_players = JsonHelper.Int(r, "max_players"),
                        current_players = JsonHelper.Int(r, "current_players"),
                        player_display = JsonHelper.Str(r, "player_display"),
                        password_status = JsonHelper.Str(r, "password_status"),
                        game_version = JsonHelper.Str(r, "game_version"),
                        created_at = JsonHelper.Str(r, "created_at"),
                        expire_at = JsonHelper.Str(r, "expire_at"),
                        status = JsonHelper.Str(r, "status")
                    });
                }
                result.rooms = rooms.ToArray();
            }
            catch { }
            return result;
        }

        // 批量同步：房间列表 + 公告 + 服务器时间 +（可选）聊天，一次请求
        public static async Task<ServerListResponse> Sync(string version = null, string roomId = "")
        {
            var result = new ServerListResponse();
            try
            {
                var body = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(roomId)) body["room_id"] = roomId;
                string url = _apiBase + "?action=sync";
                if (!string.IsNullOrEmpty(version))
                    url += "&version=" + Uri.EscapeDataString(version);
                var data = await PostJson(url, body);
                if (data == null) return result;

                result.code = JsonHelper.Int(data, "code");
                result.msg = JsonHelper.Str(data, "msg");
                result.server_name = JsonHelper.Str(data, "server_name");
                result.total = JsonHelper.Int(data, "total");
                result.max_rooms = JsonHelper.Int(data, "max_rooms");
                result.current_rooms = JsonHelper.Int(data, "current_rooms");
                result.is_full = JsonHelper.Int(data, "is_full");
                result.queue = JsonHelper.Int(data, "queue");
                result.queue_msg = JsonHelper.Str(data, "queue_msg");
                result.announcement = JsonHelper.Str(data, "announcement");
                result.announcement_time = JsonHelper.Str(data, "announcement_time");
                result.server_time = JsonHelper.Long(data, "server_time");

                var list = JsonHelper.List(data, "rooms");
                var rooms = new List<ServerRoomInfo>();
                foreach (var r in list)
                {
                    rooms.Add(new ServerRoomInfo
                    {
                        room_id = JsonHelper.Str(r, "room_id"),
                        room_name = JsonHelper.Str(r, "room_name"),
                        host_name = JsonHelper.Str(r, "host_name"),
                        host_address = JsonHelper.Str(r, "host_address"),
                        port = JsonHelper.Int(r, "port"),
                        has_password = JsonHelper.Int(r, "has_password"),
                        max_players = JsonHelper.Int(r, "max_players"),
                        current_players = JsonHelper.Int(r, "current_players"),
                        player_display = JsonHelper.Str(r, "player_display"),
                        password_status = JsonHelper.Str(r, "password_status"),
                        game_version = JsonHelper.Str(r, "game_version"),
                        created_at = JsonHelper.Str(r, "created_at"),
                        expire_at = JsonHelper.Str(r, "expire_at"),
                        status = JsonHelper.Str(r, "status")
                    });
                }
                result.rooms = rooms.ToArray();

                var msgs = new List<ServerChatMessage>();
                foreach (var m in JsonHelper.List(data, "messages"))
                {
                    msgs.Add(new ServerChatMessage
                    {
                        player_name = JsonHelper.Str(m, "player_name"),
                        message = JsonHelper.Str(m, "message"),
                        created_at = JsonHelper.Str(m, "created_at")
                    });
                }
                result.messages = msgs.ToArray();
            }
            catch { }
            return result;
        }

        public static async Task<CaptchaResult> GetCaptcha()
        {
            var result = new CaptchaResult();
            try
            {
                var data = await GetJson(_apiBase + "?action=get_captcha");
                if (data == null || JsonHelper.Int(data, "code") != 0) return result;
                result.imageBase64 = JsonHelper.Str(data, "captcha_image");
                result.text = JsonHelper.Str(data, "captcha"); // 旧服务器文字兜底
                return result;
            }
            catch { return result; }
        }

        public static async Task<bool> VerifyCaptcha(string code)
        {
            try
            {
                var data = await PostJson(_apiBase + "?action=verify_captcha",
                    new Dictionary<string, object> { ["code"] = code ?? "" });
                return data != null && JsonHelper.Int(data, "code") == 0 && JsonHelper.Bool(data, "verified");
            }
            catch { return false; }
        }

        public static async Task<(bool ok, string roomId, string token, string expireAt, string errorKey, string error)> CreateRoom(
            string hostName, string roomName, int port, string password, int maxPlayers, string captcha,
            string publicAddress = "", string serverPassword = "")
        {
            try
            {
                var body = new Dictionary<string, object>
                {
                    ["host_name"] = hostName ?? "",
                    ["room_name"] = roomName ?? "",
                    ["port"] = port,
                    ["password"] = password ?? "",
                    ["max_players"] = maxPlayers,
                    ["captcha"] = captcha ?? "",
                    ["game_version"] = PluginInfo.Version,
                    ["server_password"] = serverPassword ?? ""
                };
                // 对外地址（樱花映射/frp 隧道地址），房主填写后加入方直接连这里
                if (!string.IsNullOrEmpty(publicAddress))
                    body["public_address"] = publicAddress;
                var data = await PostJson(_apiBase + "?action=create", body);
                if (data != null && JsonHelper.Int(data, "code") == 0)
                {
                    _myRoomToken = JsonHelper.Str(data, "token");
                    _myRoomId = JsonHelper.Str(data, "room_id");
                    return (true, _myRoomId, _myRoomToken, JsonHelper.Str(data, "expire_at"), null, null);
                }
                if (data != null && JsonHelper.Int(data, "need_captcha") == 1)
                    return (false, null, null, null, "need_captcha", null);
                return (false, null, null, null,
                    data != null && data.ContainsKey("key") ? JsonHelper.Str(data, "key") : "create_failed",
                    data != null && data.ContainsKey("msg") ? JsonHelper.Str(data, "msg") : "创建失败");
            }
            catch (Exception ex)
            {
                return (false, null, null, null, null, ex.Message);
            }
        }

        public static async Task<(bool ok, string roomId, string hostAddress, int port, string errorKey, string error)> JoinRoom(
            string roomId, string playerName, string playerId, string password, string serverPassword = "")
        {
            try
            {
                var body = new Dictionary<string, object>
                {
                    ["room_id"] = roomId ?? "",
                    ["player_name"] = playerName ?? "",
                    ["player_id"] = playerId ?? "",
                    ["password"] = password ?? "",
                    ["server_password"] = serverPassword ?? ""
                };
                var data = await PostJson(_apiBase + "?action=join", body);
                if (data != null && JsonHelper.Int(data, "code") == 0)
                    return (true, JsonHelper.Str(data, "room_id"), JsonHelper.Str(data, "host_address"), JsonHelper.Int(data, "port"), null, null);
                return (false, null, null, 0,
                    data != null && data.ContainsKey("key") ? JsonHelper.Str(data, "key") : "join_failed",
                    data != null && data.ContainsKey("msg") ? JsonHelper.Str(data, "msg") : "加入失败");
            }
            catch (Exception ex)
            {
                return (false, null, null, 0, null, ex.Message);
            }
        }

        public static async Task<bool> LeaveRoom(string roomId, string playerId, string token = null)
        {
            try
            {
                var body = new Dictionary<string, object>
                {
                    ["room_id"] = roomId ?? "",
                    ["player_id"] = playerId ?? ""
                };
                if (!string.IsNullOrEmpty(token)) body["token"] = token;
                var data = await PostJson(_apiBase + "?action=leave", body);
                if (data != null && JsonHelper.Int(data, "code") == 0)
                {
                    if (_myRoomId == roomId) { _myRoomId = ""; _myRoomToken = ""; }
                    return true;
                }
                return false;
            }
            catch { return false; }
        }

        public static async Task<bool> Heartbeat(string roomId, string token)
        {
            try
            {
                var body = new Dictionary<string, object>
                {
                    ["room_id"] = roomId ?? "",
                    ["token"] = token ?? ""
                };
                var data = await PostJson(_apiBase + "?action=heartbeat", body);
                return data != null && JsonHelper.Int(data, "code") == 0;
            }
            catch { return false; }
        }

        // 玩家在线心跳：进入房间后周期性上报，服务器据此统计真实在线人数
        public static async Task<bool> Presence(string roomId, string playerId)
        {
            try
            {
                var data = await PostJson(_apiBase + "?action=presence",
                    new Dictionary<string, object>
                    {
                        ["room_id"] = roomId ?? "",
                        ["player_id"] = playerId ?? ""
                    });
                return data != null && JsonHelper.Int(data, "code") == 0;
            }
            catch { return false; }
        }

        // TCP连接成功后的“我已连接”确认（服务端据此显示在线）
        public static async Task<bool> ConfirmJoin(string roomId, string playerId)
        {
            try
            {
                var data = await PostJson(_apiBase + "?action=confirm_join",
                    new Dictionary<string, object>
                    {
                        ["room_id"] = roomId ?? "",
                        ["player_id"] = playerId ?? ""
                    });
                return data != null && JsonHelper.Int(data, "code") == 0;
            }
            catch { return false; }
        }

        // 服务器中继：把一条消息交给服务器转发
        public static async Task<bool> RelaySend(string roomId, string playerId, byte type, string payloadBase64)
        {
            try
            {
                var data = await PostJson(_apiBase + "?action=relay_send",
                    new Dictionary<string, object>
                    {
                        ["room_id"] = roomId ?? "",
                        ["player_id"] = playerId ?? "",
                        ["type"] = (int)type,
                        ["payload"] = payloadBase64 ?? ""
                    });
                return data != null && JsonHelper.Int(data, "code") == 0;
            }
            catch { return false; }
        }

        // 服务器中继：拉取服务器转发的消息
        public static async Task<RelayPollResult> RelayPoll(string roomId, long after)
        {
            var result = new RelayPollResult { after = after };
            try
            {
                string url = _apiBase + "?action=relay_poll&room_id=" +
                             Uri.EscapeDataString(roomId ?? "") + "&after=" + after;
                var data = await GetJson(url);
                if (data == null || JsonHelper.Int(data, "code") != 0) return result;
                result.after = JsonHelper.Long(data, "after");
                foreach (var m in JsonHelper.List(data, "messages"))
                {
                    result.messages.Add(new RelayMsg
                    {
                        player_id = JsonHelper.Str(m, "player_id"),
                        type = (byte)JsonHelper.Int(m, "msg_type"),
                        payload = JsonHelper.Str(m, "payload")
                    });
                }
                return result;
            }
            catch { return result; }
        }

        public static async Task<bool> SendChat(string roomId, string playerId, string playerName, string message)
        {
            try
            {
                var body = new Dictionary<string, object>
                {
                    ["room_id"] = roomId ?? "",
                    ["player_id"] = playerId ?? "",
                    ["player_name"] = playerName ?? "",
                    ["message"] = message ?? ""
                };
                var data = await PostJson(_apiBase + "?action=chat", body);
                return data != null && JsonHelper.Int(data, "code") == 0;
            }
            catch { return false; }
        }

        public static async Task<ServerChatMessage[]> GetChat(string roomId, int limit = 50)
        {
            try
            {
                string url = _apiBase + "?action=get_chat&room_id=" +
                              Uri.EscapeDataString(roomId ?? "") + "&limit=" + limit;
                var data = await GetJson(url);
                if (data == null || JsonHelper.Int(data, "code") != 0) return null;

                var list = JsonHelper.List(data, "messages");
                var msgs = new List<ServerChatMessage>();
                foreach (var m in list)
                {
                    msgs.Add(new ServerChatMessage
                    {
                        room_id = JsonHelper.Str(m, "room_id"),
                        player_id = JsonHelper.Str(m, "player_id"),
                        player_name = JsonHelper.Str(m, "player_name"),
                        ip = JsonHelper.Str(m, "ip"),
                        message = JsonHelper.Str(m, "message"),
                        created_at = JsonHelper.Str(m, "created_at")
                    });
                }
                return msgs.ToArray();
            }
            catch { return null; }
        }

        public static async Task<(bool ok, string content, string time)> GetAnnouncement()
        {
            try
            {
                var data = await GetJson(_apiBase + "?action=get_announcement");
                if (data != null && JsonHelper.Int(data, "code") == 0)
                    return (true, JsonHelper.Str(data, "content"), JsonHelper.Str(data, "created_at"));
                return (false, "", "");
            }
            catch { return (false, "", ""); }
        }

        public static async Task<bool> AdminDeleteRoom(string roomId)
        {
            if (!_isAdmin) return false;
            try
            {
                var data = await PostJson(_apiBase + "?action=admin_delete",
                    new Dictionary<string, object> { ["room_id"] = roomId ?? "", ["csrf"] = _csrf });
                return AdminOk(data);
            }
            catch { return false; }
        }

        public static async Task<bool> AdminBanIP(string ip, string reason = "", int days = 7)
        {
            if (!_isAdmin) return false;
            try
            {
                var data = await PostJson(_apiBase + "?action=admin_ban",
                    new Dictionary<string, object>
                    {
                        ["ip"] = ip ?? "",
                        ["reason"] = reason ?? "",
                        ["days"] = days,
                        ["csrf"] = _csrf
                    });
                return AdminOk(data);
            }
            catch { return false; }
        }

        public static async Task<bool> AdminUnbanIP(string ip)
        {
            if (!_isAdmin) return false;
            try
            {
                var data = await PostJson(_apiBase + "?action=admin_unban",
                    new Dictionary<string, object> { ["ip"] = ip ?? "", ["csrf"] = _csrf });
                return AdminOk(data);
            }
            catch { return false; }
        }

        public static async Task<bool> AdminSetAnnouncement(string content)
        {
            if (!_isAdmin) return false;
            try
            {
                var data = await PostJson(_apiBase + "?action=admin_set_announcement",
                    new Dictionary<string, object> { ["content"] = content ?? "", ["csrf"] = _csrf });
                return AdminOk(data);
            }
            catch { return false; }
        }

        public static async Task<bool> AdminExportLogs()
        {
            if (!_isAdmin) return false;
            try
            {
                var data = await PostJson(_apiBase + "?action=admin_export_logs",
                    new Dictionary<string, object> { ["csrf"] = _csrf });
                return AdminOk(data);
            }
            catch { return false; }
        }

        public static async Task<bool> AdminClearAnnouncement()
        {
            if (!_isAdmin) return false;
            try
            {
                var data = await PostJson(_apiBase + "?action=admin_clear_announcement",
                    new Dictionary<string, object> { ["csrf"] = _csrf });
                return AdminOk(data);
            }
            catch { return false; }
        }

        public static async Task<ServerSettingsInfo> AdminGetSettings()
        {
            var result = new ServerSettingsInfo();
            if (!_isAdmin) return result;
            try
            {
                var data = await GetJson(_apiBase + "?action=admin_get_settings");
                if (data == null || JsonHelper.Int(data, "code") != 0) return result;
                if (data.TryGetValue("settings", out var so) && so is Dictionary<string, object> s)
                {
                    result.max_rooms_total = JsonHelper.Int(s, "max_rooms_total", result.max_rooms_total);
                    result.max_rooms_per_ip = JsonHelper.Int(s, "max_rooms_per_ip", result.max_rooms_per_ip);
                    result.max_rooms_per_hour = JsonHelper.Int(s, "max_rooms_per_hour", result.max_rooms_per_hour);
                    result.room_lifetime = JsonHelper.Int(s, "room_lifetime", result.room_lifetime);
                    result.room_timeout = JsonHelper.Int(s, "room_timeout", result.room_timeout);
                    result.max_players = JsonHelper.Int(s, "max_players", result.max_players);
                    result.chat_log_days = JsonHelper.Int(s, "chat_log_days", result.chat_log_days);
                    result.action_log_days = JsonHelper.Int(s, "action_log_days", result.action_log_days);
                    result.captcha_expire = JsonHelper.Int(s, "captcha_expire", result.captcha_expire);
                }
                return result;
            }
            catch { return result; }
        }

        public static async Task<bool> AdminSaveSettings(ServerSettingsInfo s)
        {
            if (!_isAdmin || s == null) return false;
            try
            {
                var body = new Dictionary<string, object>
                {
                    ["csrf"] = _csrf,
                    ["max_rooms_total"] = s.max_rooms_total,
                    ["max_rooms_per_ip"] = s.max_rooms_per_ip,
                    ["max_rooms_per_hour"] = s.max_rooms_per_hour,
                    ["room_lifetime"] = s.room_lifetime,
                    ["room_timeout"] = s.room_timeout,
                    ["max_players"] = s.max_players,
                    ["chat_log_days"] = s.chat_log_days,
                    ["action_log_days"] = s.action_log_days,
                    ["captcha_expire"] = s.captcha_expire
                };
                var data = await PostJson(_apiBase + "?action=admin_save_settings", body);
                return AdminOk(data);
            }
            catch { return false; }
        }

        public static async Task<List<ServerChatMessage>> AdminRoomChat(string roomId)
        {
            var result = new List<ServerChatMessage>();
            if (!_isAdmin) return result;
            try
            {
                var data = await PostJson(_apiBase + "?action=admin_room_chat",
                    new Dictionary<string, object> { ["room_id"] = roomId ?? "", ["csrf"] = _csrf });
                if (data == null || JsonHelper.Int(data, "code") != 0) return result;
                var list = JsonHelper.List(data, "messages");
                foreach (var m in list)
                {
                    result.Add(new ServerChatMessage
                    {
                        room_id = JsonHelper.Str(m, "room_id"),
                        player_id = JsonHelper.Str(m, "player_id"),
                        player_name = JsonHelper.Str(m, "player_name"),
                        ip = JsonHelper.Str(m, "ip"),
                        message = JsonHelper.Str(m, "message"),
                        created_at = JsonHelper.Str(m, "created_at")
                    });
                }
                return result;
            }
            catch { return result; }
        }

        public static async Task<bool> AdminSendRoomMessage(string roomId, string message)
        {
            if (!_isAdmin) return false;
            try
            {
                var data = await PostJson(_apiBase + "?action=admin_send_room_msg",
                    new Dictionary<string, object>
                    {
                        ["room_id"] = roomId ?? "",
                        ["message"] = message ?? "",
                        ["csrf"] = _csrf
                    });
                return AdminOk(data);
            }
            catch { return false; }
        }

        // 登录后向服务器确认管理权限与可用功能
        public static async Task<bool> GetAdminInfo()
        {
            if (!_isAdmin) return false;
            try
            {
                var data = await GetJson(_apiBase + "?action=admin_info");
                return data != null && JsonHelper.Int(data, "code") == 0;
            }
            catch { return false; }
        }

        public static string TranslateField(string fieldName) => Lang.GetFieldDisplay(fieldName);

        public static string TranslateRoomStatus(ServerRoomInfo room)
        {
            if (room == null) return Lang.Get("unknown");
            if (room.status == "active")
            {
                if (room.current_players >= room.max_players) return Lang.Get("field_status_full");
                return Lang.Get("field_status_active");
            }
            if (room.status == "closed") return Lang.Get("field_status_closed");
            if (room.status == "expired") return Lang.Get("field_status_expired");
            return string.IsNullOrEmpty(room.status) ? Lang.Get("field_status_active") : room.status;
        }

        public static string TranslatePasswordStatus(ServerRoomInfo room)
        {
            if (room != null && !string.IsNullOrEmpty(room.password_status))
            {
                string ps = room.password_status.ToLowerInvariant();
                if (ps == "yes" || ps == "1" || ps == "password")
                    return Lang.Get("field_password_yes");
                return Lang.Get("field_password_no");
            }
            return room != null && room.has_password == 1
                ? Lang.Get("field_password_yes")
                : Lang.Get("field_password_no");
        }
    }
}
