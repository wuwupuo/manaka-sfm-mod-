using System;
using System.Collections.Generic;
using UnityEngine;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  联机函数库（SFM Online 独有，V2 没有）：
    //  消息发送 / 玩家列表 / 积分同步 / 骨骼同步 / 事件广播 / 房间
    // ====================================================================
    public static class SfmExtNet
    {
        // ---------- 消息发送 ----------
        [SfmExtFunction("net.send_to_server")]
        public static SfmExtValue FnSendToServer(SfmExtParams p, SfmExtValue u)
        {
            var msg = BuildMsg(p);
            SfmExtBridge.SendToServer?.Invoke(msg);
            return new SfmExtValue(true);
        }

        [SfmExtFunction("net.send_to_room")]
        public static SfmExtValue FnSendToRoom(SfmExtParams p, SfmExtValue u)
        {
            var msg = BuildMsg(p);
            SfmExtBridge.SendToRoom?.Invoke(msg);
            return new SfmExtValue(true);
        }

        [SfmExtFunction("net.send_to_player")]
        public static SfmExtValue FnSendToPlayer(SfmExtParams p, SfmExtValue u)
        {
            var msg = BuildMsg(p);
            var uid = p.Get("uid").ToString();
            if (uid.Length > 0) SfmExtBridge.SendToPlayer?.Invoke(uid, msg);
            return new SfmExtValue(true);
        }

        [SfmExtFunction("net.broadcast_event")]
        public static SfmExtValue FnBroadcastEvent(SfmExtParams p, SfmExtValue u)
        {
            var evt = p.Get("event").ToString();
            var data = SfmExtEvent.ToJsonObject(p.Get("data"));
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_evt", ["ns"] = SfmExtEventBus.Namespace,
                ["evt"] = evt, ["data"] = data
            });
            return new SfmExtValue(true);
        }

        [SfmExtFunction("net.send_custom")]
        public static SfmExtValue FnSendCustom(SfmExtParams p, SfmExtValue u)
        {
            var type = p.Get("type").ToString();
            var msg = new Dictionary<string, object> { ["t"] = type };
            // 把参数里所有 s_/n_/b_ 前缀的键并入消息
            foreach (var k in p.Keys)
            {
                if (k.StartsWith("s_") || k.StartsWith("n_") || k.StartsWith("b_"))
                    msg[k.Substring(2)] = SfmExtEvent.ToJsonObject(p.Get(k));
            }
            SfmExtBridge.SendToRoom?.Invoke(msg);
            return new SfmExtValue(true);
        }

        private static Dictionary<string, object> BuildMsg(SfmExtParams p)
        {
            var msg = new Dictionary<string, object>();
            var type = p.Get("type").ToString();
            msg["t"] = type.Length > 0 ? type : "ext_custom";
            if (p.Has("ns")) msg["ns"] = p.Get("ns").ToString();
            if (p.Has("op")) msg["op"] = p.Get("op").ToString();
            if (p.Has("data")) msg["data"] = SfmExtEvent.ToJsonObject(p.Get("data"));
            foreach (var k in p.Keys)
            {
                if (k.StartsWith("s_") || k.StartsWith("n_") || k.StartsWith("b_"))
                    msg[k.Substring(2)] = SfmExtEvent.ToJsonObject(p.Get(k));
            }
            return msg;
        }

        // ---------- 玩家 ----------
        [SfmExtFunction("net.get_uid")]
        public static SfmExtValue FnGetUid(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(SfmExtBridge.GetLocalUid != null ? SfmExtBridge.GetLocalUid() : "");

        [SfmExtFunction("net.get_name")]
        public static SfmExtValue FnGetName(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(SfmExtBridge.GetLocalName != null ? SfmExtBridge.GetLocalName() : "");

        [SfmExtFunction("net.get_players")]
        public static SfmExtValue FnGetPlayers(SfmExtParams p, SfmExtValue u)
        {
            var v = new SfmExtValue(SfmExtValue.Type.List);
            var uids = SfmExtBridge.GetGhostUids?.Invoke() ?? new List<string>();
            for (int i = 0; i < uids.Count; i++) v[i.ToString()] = new SfmExtValue(uids[i]);
            return v;
        }

        [SfmExtFunction("net.get_player_count")]
        public static SfmExtValue FnGetPlayerCount(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue((SfmExtBridge.GetGhostUids?.Invoke() ?? new List<string>()).Count);

        [SfmExtFunction("net.get_player_position")]
        public static SfmExtValue FnGetPlayerPos(SfmExtParams p, SfmExtValue u)
        {
            var uid = p.Get("uid").ToString();
            var pos = SfmExtBridge.GetGhostPosition != null ? SfmExtBridge.GetGhostPosition(uid) : Vector3.zero;
            var v = new SfmExtValue(SfmExtValue.Type.List);
            v["x"] = new SfmExtValue(pos.x); v["y"] = new SfmExtValue(pos.y); v["z"] = new SfmExtValue(pos.z);
            return v;
        }

        [SfmExtFunction("net.get_player_distance")]
        public static SfmExtValue FnGetPlayerDist(SfmExtParams p, SfmExtValue u)
        {
            var uid = p.Get("uid").ToString();
            var local = SfmExtBridge.GetLocalPosition != null ? SfmExtBridge.GetLocalPosition() : Vector3.zero;
            var other = SfmExtBridge.GetGhostPosition != null ? SfmExtBridge.GetGhostPosition(uid) : Vector3.zero;
            return new SfmExtValue(Vector3.Distance(local, other));
        }

        [SfmExtFunction("net.is_connected")]
        public static SfmExtValue FnIsConnected(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(SfmExtBridge.BridgeReady);

        [SfmExtFunction("net.get_player_name")]
        public static SfmExtValue FnGetPlayerName(SfmExtParams p, SfmExtValue u)
        {
            var uid = p.Get("uid").ToString();
            try
            {
                var c = OnlineCore.Instance;
                if (c == null) return new SfmExtValue("");
                if (uid.Length == 0 || uid == c.PeerId) return new SfmExtValue(c.GetLocalNamePublic());
                return new SfmExtValue(c.GetGamePlayerNamePublic(uid));
            }
            catch { return new SfmExtValue(""); }
        }

        [SfmExtFunction("net.find_uid")]
        public static SfmExtValue FnFindUid(SfmExtParams p, SfmExtValue u)
        {
            var name = p.Get("name").ToString();
            try
            {
                var c = OnlineCore.Instance;
                if (c == null) return new SfmExtValue("");
                var uids = c.GetGhostUids();
                if (uids == null) return new SfmExtValue("");
                foreach (var uid in uids)
                {
                    if (c.GetGamePlayerNamePublic(uid) == name)
                        return new SfmExtValue(uid);
                }
                if (c.GetLocalNamePublic() == name) return new SfmExtValue(c.PeerId);
                return new SfmExtValue("");
            }
            catch { return new SfmExtValue(""); }
        }

        [SfmExtFunction("net.get_players_info")]
        public static SfmExtValue FnGetPlayersInfo(SfmExtParams p, SfmExtValue u)
        {
            var v = new SfmExtValue(SfmExtValue.Type.List);
            try
            {
                var c = OnlineCore.Instance;
                if (c == null) return v;
                var uids = c.GetGhostUids();
                if (uids == null) return v;
                for (int i = 0; i < uids.Count; i++)
                {
                    var uid = uids[i];
                    var pos = SfmExtBridge.GetGhostPosition != null ? SfmExtBridge.GetGhostPosition(uid) : Vector3.zero;
                    var item = new SfmExtValue(SfmExtValue.Type.List);
                    item["uid"] = new SfmExtValue(uid);
                    item["name"] = new SfmExtValue(c.GetGamePlayerNamePublic(uid));
                    item["x"] = new SfmExtValue(pos.x);
                    item["y"] = new SfmExtValue(pos.y);
                    item["z"] = new SfmExtValue(pos.z);
                    v[i.ToString()] = item;
                }
            }
            catch { }
            return v;
        }

        [SfmExtFunction("net.get_local_position")]
        public static SfmExtValue FnGetLocalPos(SfmExtParams p, SfmExtValue u)
        {
            var pos = SfmExtBridge.GetLocalPosition != null ? SfmExtBridge.GetLocalPosition() : Vector3.zero;
            var v = new SfmExtValue(SfmExtValue.Type.List);
            v["x"] = new SfmExtValue(pos.x); v["y"] = new SfmExtValue(pos.y); v["z"] = new SfmExtValue(pos.z);
            return v;
        }

        // ---------- 积分同步 ----------
        [SfmExtFunction("net.score_sync")]
        public static SfmExtValue FnScoreSync(SfmExtParams p, SfmExtValue u)
        {
            var name = p.Get("name").ToString();
            var value = p.Get("value").ToFloat();
            SfmExtScore.Set(name, value, true);
            return new SfmExtValue(true);
        }

        [SfmExtFunction("net.score_broadcast")]
        public static SfmExtValue FnScoreBroadcast(SfmExtParams p, SfmExtValue u)
        {
            var name = p.Get("name").ToString();
            var value = SfmExtScore.Get(name);
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_score", ["ns"] = SfmExtEventBus.Namespace,
                ["name"] = name, ["value"] = value
            });
            return new SfmExtValue(true);
        }

        // ---------- 骨骼同步 ----------
        [SfmExtFunction("net.bone_sync")]
        public static SfmExtValue FnBoneSync(SfmExtParams p, SfmExtValue u)
        {
            var bone = p.Get("bone").ToString();
            var rot = Quaternion.Euler((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat());
            var t = SfmExtBone.FindLocalBone(bone);
            if (t == null) return new SfmExtValue(false);
            t.localRotation = rot;
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_bone", ["ns"] = SfmExtEventBus.Namespace,
                ["op"] = "rot", ["bone"] = bone,
                ["x"] = rot.x, ["y"] = rot.y, ["z"] = rot.z, ["w"] = rot.w
            });
            return new SfmExtValue(true);
        }

        // ---------- 聊天 ----------
        [SfmExtFunction("net.send_chat")]
        public static SfmExtValue FnSendChat(SfmExtParams p, SfmExtValue u)
        {
            var text = p.Get("text").ToString();
            var uid = p.Get("uid").ToString();
            if (uid.Length > 0) SfmExtBridge.SendPrivateChat?.Invoke(uid, text);
            else SfmExtBridge.SendChat?.Invoke(text);
            return new SfmExtValue(true);
        }

        // ---------- 触发点/区域联机 ----------
        [SfmExtFunction("net.trigger")]
        public static SfmExtValue FnNetTrigger(SfmExtParams p, SfmExtValue u)
        {
            var name = p.Get("name").ToString();
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_trigger", ["ns"] = SfmExtEventBus.Namespace,
                ["name"] = name
            });
            return new SfmExtValue(true);
        }

        // ---------- 房间 ----------
        [SfmExtFunction("net.room_broadcast")]
        public static SfmExtValue FnRoomBroadcast(SfmExtParams p, SfmExtValue u) => FnBroadcastEvent(p, u);

        // ====================================================================
        //  服务器插件（mod）检测与通信
        // ====================================================================
        private static List<Dictionary<string, object>> _serverMods = new List<Dictionary<string, object>>();

        /// <summary>由联机核心在连接成功时设置服务器插件列表。</summary>
        public static void SetServerMods(List<Dictionary<string, object>> mods)
        {
            _serverMods = mods ?? new List<Dictionary<string, object>>();
        }

        /// <summary>获取服务器上加载的插件（mod）列表。</summary>
        public static List<Dictionary<string, object>> GetServerMods() => _serverMods;

        /// <summary>服务器是否加载了指定插件。</summary>
        public static bool IsServerMod(string name)
        {
            foreach (var m in _serverMods)
                if (SfmExtRoom.Str(m, "name") == name) return true;
            return false;
        }

        /// <summary>获取服务器插件版本。</summary>
        public static string GetServerModVersion(string name)
        {
            foreach (var m in _serverMods)
                if (SfmExtRoom.Str(m, "name") == name) return SfmExtRoom.Str(m, "version");
            return "";
        }

        /// <summary>发送消息给指定服务器插件（插件 on_message 处理）。</summary>
        public static void SendToPlugin(string ns, string op, object data = null)
        {
            var msg = new Dictionary<string, object>
            {
                ["t"] = "ext", ["ns"] = ns, ["op"] = op,
                ["data"] = SfmExtEvent.ToJsonObject(data)
            };
            SfmExtBridge.SendToServer?.Invoke(msg);
        }

        /// <summary>发送插件间通信消息（服务器转发给目标插件，from_ns 自动注入）。</summary>
        public static void SendPluginToPlugin(string fromNs, string toNs, string op, object data = null)
        {
            var msg = new Dictionary<string, object>
            {
                ["t"] = "ext", ["ns"] = fromNs, ["to_ns"] = toNs, ["op"] = op,
                ["data"] = SfmExtEvent.ToJsonObject(data)
            };
            SfmExtBridge.SendToServer?.Invoke(msg);
        }

        // ---------- 引擎函数 ----------
        [SfmExtFunction("net.server_mods")]
        public static SfmExtValue FnServerMods(SfmExtParams p, SfmExtValue u)
        {
            var v = new SfmExtValue(SfmExtValue.Type.List);
            for (int i = 0; i < _serverMods.Count; i++)
            {
                var m = new SfmExtValue(SfmExtValue.Type.List);
                m["name"] = new SfmExtValue(SfmExtRoom.Str(_serverMods[i], "name"));
                m["version"] = new SfmExtValue(SfmExtRoom.Str(_serverMods[i], "version"));
                m["desc"] = new SfmExtValue(SfmExtRoom.Str(_serverMods[i], "desc"));
                v[i.ToString()] = m;
            }
            return v;
        }

        [SfmExtFunction("net.is_server_mod")]
        public static SfmExtValue FnIsServerMod(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(IsServerMod(p.Get("name").ToString()));

        [SfmExtFunction("net.send_to_plugin")]
        public static SfmExtValue FnSendToPlugin(SfmExtParams p, SfmExtValue u)
        {
            SendToPlugin(p.Get("ns").ToString(), p.Get("op").ToString(), p.Get("data"));
            return new SfmExtValue(true);
        }

        [SfmExtFunction("net.plugin_call")]
        public static SfmExtValue FnPluginCall(SfmExtParams p, SfmExtValue u)
        {
            SendPluginToPlugin(p.Get("from").ToString(), p.Get("to").ToString(),
                p.Get("op").ToString(), p.Get("data"));
            return new SfmExtValue(true);
        }
    }
}
