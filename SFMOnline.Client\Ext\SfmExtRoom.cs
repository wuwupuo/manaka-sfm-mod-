using System;
using System.Collections.Generic;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  房间管理 API：创建/加入/离开/列表/踢人/房主/成员/设置
    // ====================================================================
    public static class SfmExtRoom
    {
        public sealed class RoomInfo
        {
            public string Id = "";
            public string Name = "";
            public string Host = "";
            public int PlayerCount;
            public int MaxPlayers;
            public bool HasPassword;
            public Dictionary<string, object> Raw;
        }

        // 桥接（由联机核心注册）
        public static Func<bool> IsConnected;
        public static Func<string> GetRoomId;
        public static Func<bool> IsHost;
        public static Func<List<Dictionary<string, object>>> GetRoomPlayers;
        public static Func<List<Dictionary<string, object>>> GetRoomList;
        public static Action<Dictionary<string, object>> SendMessage;

        private static readonly List<Action<string, string, string>> _joinListeners
            = new List<Action<string, string, string>>(); // (roomId, uid, name)
        private static readonly List<Action<string, string>> _leaveListeners
            = new List<Action<string, string>>(); // (roomId, uid)
        private static readonly List<Action<string>> _roomClosedListeners = new List<Action<string>>();

        public static bool Connected => IsConnected != null && IsConnected();
        public static string RoomId => GetRoomId != null ? GetRoomId() : "";
        public static bool Host => IsHost != null && IsHost();

        // ---------- 房间操作 ----------
        public static void Create(string name, int maxPlayers = 8, string password = "", int allowGameBonuses = 0)
        {
            var m = new Dictionary<string, object>
            {
                ["t"] = "room_create", ["name"] = name, ["max"] = maxPlayers,
                ["password"] = password ?? "", ["allow_game_bonuses"] = allowGameBonuses
            };
            SendMessage?.Invoke(m);
        }

        public static void Join(string roomId, string password = "")
        {
            var m = new Dictionary<string, object> { ["t"] = "room_join", ["room_id"] = roomId, ["password"] = password ?? "" };
            SendMessage?.Invoke(m);
        }

        public static void Leave()
        {
            SendMessage?.Invoke(new Dictionary<string, object> { ["t"] = "room_leave" });
        }

        public static void Kick(string uid)
        {
            SendMessage?.Invoke(new Dictionary<string, object> { ["t"] = "room_kick", ["uid"] = uid });
        }

        public static void SetRoomSetting(string key, object value)
        {
            SendMessage?.Invoke(new Dictionary<string, object> { ["t"] = "room_setting", [key] = value });
        }

        public static void SetAllowGameBonuses(bool allow)
        {
            SendMessage?.Invoke(new Dictionary<string, object> { ["t"] = "room_setting", ["allow_game_bonuses"] = allow ? 1 : 0 });
        }

        // ---------- 房间信息 ----------
        public static List<RoomInfo> GetRooms()
        {
            var list = new List<RoomInfo>();
            var raw = GetRoomList?.Invoke();
            if (raw == null) return list;
            foreach (var r in raw)
            {
                var ri = new RoomInfo { Raw = r };
                ri.Id = Str(r, "room_id");
                ri.Name = Str(r, "name");
                ri.Host = Str(r, "host");
                ri.PlayerCount = Int(r, "players", 0);
                ri.MaxPlayers = Int(r, "max", 8);
                ri.HasPassword = Int(r, "has_password", 0) != 0;
                list.Add(ri);
            }
            return list;
        }

        /// <summary>当前房间成员 uid 列表。</summary>
        public static List<string> GetPlayers()
        {
            var list = new List<string>();
            var raw = GetRoomPlayers?.Invoke();
            if (raw == null) return list;
            foreach (var p in raw)
            {
                var uid = Str(p, "uid");
                if (uid.Length > 0) list.Add(uid);
            }
            return list;
        }

        /// <summary>获取成员名字。</summary>
        public static string GetPlayerName(string uid)
        {
            var raw = GetRoomPlayers?.Invoke();
            if (raw == null) return uid;
            foreach (var p in raw)
                if (Str(p, "uid") == uid) return Str(p, "name");
            return uid;
        }

        // ---------- 事件 ----------
        public static void OnPlayerJoin(Action<string, string, string> handler) => _joinListeners.Add(handler);
        public static void OnPlayerLeave(Action<string, string> handler) => _leaveListeners.Add(handler);
        public static void OnRoomClosed(Action<string> handler) => _roomClosedListeners.Add(handler);

        internal static void HandleJoin(string roomId, string uid, string name)
        {
            foreach (var h in _joinListeners.ToArray()) { try { h(roomId, uid, name); } catch { } }
        }

        internal static void HandleLeave(string roomId, string uid)
        {
            foreach (var h in _leaveListeners.ToArray()) { try { h(roomId, uid); } catch { } }
        }

        internal static void HandleRoomClosed(string roomId)
        {
            foreach (var h in _roomClosedListeners.ToArray()) { try { h(roomId); } catch { } }
        }

        // ---------- 引擎函数 ----------
        [SfmExtFunction("net.room_create")]
        public static SfmExtValue FnCreate(SfmExtParams p, SfmExtValue u)
        {
            Create(p.Get("name").ToString(), (int)p.Get("max", "8").ToFloat(), p.Get("password").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("net.room_join")]
        public static SfmExtValue FnJoin(SfmExtParams p, SfmExtValue u)
        {
            Join(p.Get("room_id").ToString(), p.Get("password").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("net.room_leave")]
        public static SfmExtValue FnLeave(SfmExtParams p, SfmExtValue u)
        {
            Leave();
            return SfmExtValue.Null;
        }

        [SfmExtFunction("net.room_kick")]
        public static SfmExtValue FnKick(SfmExtParams p, SfmExtValue u)
        {
            Kick(p.Get("uid").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("net.room_get_rooms")]
        public static SfmExtValue FnGetRooms(SfmExtParams p, SfmExtValue u)
        {
            var v = new SfmExtValue(SfmExtValue.Type.List);
            var rooms = GetRooms();
            for (int i = 0; i < rooms.Count; i++)
            {
                var r = new SfmExtValue(SfmExtValue.Type.List);
                r["id"] = new SfmExtValue(rooms[i].Id);
                r["name"] = new SfmExtValue(rooms[i].Name);
                r["host"] = new SfmExtValue(rooms[i].Host);
                r["players"] = new SfmExtValue(rooms[i].PlayerCount);
                r["max"] = new SfmExtValue(rooms[i].MaxPlayers);
                r["password"] = new SfmExtValue(rooms[i].HasPassword ? 1 : 0);
                v[i.ToString()] = r;
            }
            return v;
        }

        [SfmExtFunction("net.room_is_host")]
        public static SfmExtValue FnIsHost(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Host);

        [SfmExtFunction("net.room_get_players")]
        public static SfmExtValue FnGetPlayers(SfmExtParams p, SfmExtValue u)
        {
            var v = new SfmExtValue(SfmExtValue.Type.List);
            var list = GetPlayers();
            for (int i = 0; i < list.Count; i++) v[i.ToString()] = new SfmExtValue(list[i]);
            return v;
        }

        [SfmExtFunction("net.room_get_player_name")]
        public static SfmExtValue FnGetPlayerName(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(GetPlayerName(p.Get("uid").ToString()));

        // ---------- 工具 ----------
        internal static string Str(Dictionary<string, object> d, string k)
            => d != null && d.TryGetValue(k, out var v) ? Convert.ToString(v) ?? "" : "";
        internal static int Int(Dictionary<string, object> d, string k, int def = 0)
        {
            if (d != null && d.TryGetValue(k, out var v))
            {
                try { return Convert.ToInt32(v); } catch { }
            }
            return def;
        }
    }
}
