using System;
using System.Collections.Generic;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  事件总线：本地事件 + 联机事件（模仿 V2 Engine.SetEvent/GetEvent，
    //  扩展出可跨玩家广播的事件）
    // ====================================================================
    public static class SfmExtEvent
    {
        private sealed class Handler
        {
            public Action<SfmExtValue> Action;
        }

        private static readonly Dictionary<string, List<Handler>> _localHandlers
            = new Dictionary<string, List<Handler>>();

        private static readonly Dictionary<string, SfmExtValue> _lastValues
            = new Dictionary<string, SfmExtValue>();

        // ---------- 本地事件 ----------
        public static void On(string eventName, Action<SfmExtValue> handler)
        {
            if (!_localHandlers.TryGetValue(eventName, out var list))
            {
                list = new List<Handler>();
                _localHandlers[eventName] = list;
            }
            list.Add(new Handler { Action = handler });
            if (_lastValues.TryGetValue(eventName, out var v))
            {
                try { handler(v); } catch { }
            }
        }

        public static void Off(string eventName, Action<SfmExtValue> handler)
        {
            if (!_localHandlers.TryGetValue(eventName, out var list)) return;
            list.RemoveAll(h => h.Action == handler);
        }

        public static void Emit(string eventName, object value = null)
        {
            var v = value is SfmExtValue ev ? ev : new SfmExtValue(value ?? SfmExtValue.Null);
            _lastValues[eventName] = v;
            if (!_localHandlers.TryGetValue(eventName, out var list)) return;
            foreach (var h in list.ToArray())
            {
                try { h.Action(v); } catch { }
            }
            try { SfmExtHud.HandleVisibilityEvent(eventName); } catch { }
        }

        // ---------- 联机事件（广播给房间所有人） ----------
        /// <summary>广播一个联机事件给房间内所有玩家（含自己触发本地监听）。</summary>
        public static void EmitNet(string eventName, object value = null)
        {
            Emit(eventName, value);
            var payload = new Dictionary<string, object>
            {
                ["t"] = "ext_evt", ["ns"] = SfmExtEventBus.Namespace,
                ["evt"] = eventName, ["data"] = ToJsonObject(value)
            };
            SfmExtMsg.SendToRoom(payload);
        }
        internal static object ToJsonObject(object value)
        {
            if (value is SfmExtValue ev)
            {
                switch (ev.ValueType)
                {
                    case SfmExtValue.Type.Number: return ev.Number;
                    case SfmExtValue.Type.Bool: return ev.Bool;
                    case SfmExtValue.Type.String: return ev.String;
                    case SfmExtValue.Type.List:
                        var d = new Dictionary<string, object>();
                        if (ev.List != null) foreach (var kv in ev.List) d[kv.Key] = ToJsonObject(kv.Value);
                        return d;
                    default: return null;
                }
            }
            return value;
        }

        internal static SfmExtValue FromJsonObject(object o)
        {
            if (o is Dictionary<string, object> dict)
            {
                var v = new SfmExtValue(SfmExtValue.Type.List);
                foreach (var kv in dict) v[kv.Key] = FromJsonObject(kv.Value);
                return v;
            }
            if (o is string s) return new SfmExtValue(s);
            if (o is bool b) return new SfmExtValue(b);
            if (o is double d) return new SfmExtValue(d);
            if (o is long l) return new SfmExtValue((double)l);
            if (o is int i) return new SfmExtValue((double)i);
            return SfmExtValue.Null;
        }
    }

    // ====================================================================
    //  命名空间：联机事件的命名空间（供服务端路由）
    // ====================================================================
    public static class SfmExtEventBus
    {
        public const string Namespace = "sfmext";
    }

    // ====================================================================
    //  自定义消息通道：
    //  - SendToServer: 发给联机服务器（relay 转发给插件或处理）
    //  - SendToRoom:   广播给房间所有人
    //  - SendToPlayer: 发给指定玩家（uid）
    //  服务器收到 "t":"ext" 前缀的消息，路由给对应插件。
    //  直连模式（LAN）下用 MsgTypes.Control 的 command 子协议。
    // ====================================================================
    public static class SfmExtMsg
    {
        // 服务器消息类型前缀（relay.py 识别 ext_* 并转发）
        public const string ServerPrefix = "ext_";

        /// <summary>给联机服务器发自定义消息（经 relay 转发给房间或插件）。</summary>
        public static void SendToServer(Dictionary<string, object> payload)
        {
            try { SfmExtBridge.SendToServer?.Invoke(payload); } catch { }
        }

        /// <summary>广播给房间所有人（含服务器转发）。</summary>
        public static void SendToRoom(Dictionary<string, object> payload)
        {
            try { SfmExtBridge.SendToRoom?.Invoke(payload); } catch { }
        }

        /// <summary>发给指定玩家（relay 模式 uid；直连模式 peerId）。</summary>
        public static void SendToPlayer(string uid, Dictionary<string, object> payload)
        {
            try { SfmExtBridge.SendToPlayer?.Invoke(uid, payload); } catch { }
        }

        // ---------- 消息接收注册 ----------
        private static readonly Dictionary<string, List<Action<Dictionary<string, object>, string>>>
            _handlers = new Dictionary<string, List<Action<Dictionary<string, object>, string>>>();

        /// <summary>注册一个自定义消息处理器。type 形如 "ext_xxx" 或事件名。</summary>
        public static void On(string type, Action<Dictionary<string, object>, string> handler)
        {
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Action<Dictionary<string, object>, string>>();
                _handlers[type] = list;
            }
            list.Add(handler);
        }

        public static void Off(string type, Action<Dictionary<string, object>, string> handler)
        {
            if (!_handlers.TryGetValue(type, out var list)) return;
            list.RemoveAll(h => h == handler);
        }

        // 由 OnlineCore 每帧调用：把收到的 ext_* 消息分发到注册的处理器
        internal static void Dispatch(string type, Dictionary<string, object> msg, string fromUid)
        {
            if (!_handlers.TryGetValue(type, out var list)) return;
            foreach (var h in list.ToArray())
            {
                try { h(msg, fromUid); } catch { }
            }
        }

        internal static bool IsExtMessage(string t) =>
            t != null && (t.StartsWith("ext_", StringComparison.Ordinal));
    }
}
