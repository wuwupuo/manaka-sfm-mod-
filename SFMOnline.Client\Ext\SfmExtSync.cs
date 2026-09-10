using System;
using System.Collections.Generic;
using UnityEngine;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  自定义同步字段系统：
    //  注册任意命名数据（float/int/string/vector/bool），
    //  每帧自动广播给房间，收到远端更新自动写入本地。
    //  这是"创造各种玩法数据并同步"的通用通道。
    // ====================================================================
    public static class SfmExtSync
    {
        private sealed class SyncField
        {
            public string Name;
            public SfmExtValue.Type Kind;
            public SfmExtValue Value;
            public float Hz = 5f;
            public float NextSendAt;
            public bool Changed = true;
        }

        private static readonly Dictionary<string, SyncField> _fields = new Dictionary<string, SyncField>();
        private static readonly Dictionary<string, Action<string, SfmExtValue>> _handlers
            = new Dictionary<string, Action<string, SfmExtValue>>();

        // ---------- 注册 ----------
        public static void Register(string name, SfmExtValue.Type kind, object initial = null, float hz = 5f)
        {
            if (_fields.ContainsKey(name)) return;
            var f = new SyncField { Name = name, Kind = kind, Hz = hz };
            switch (kind)
            {
                case SfmExtValue.Type.Number: f.Value = new SfmExtValue(Convert.ToDouble(initial ?? 0)); break;
                case SfmExtValue.Type.Bool: f.Value = new SfmExtValue(Convert.ToBoolean(initial ?? false)); break;
                case SfmExtValue.Type.String: f.Value = new SfmExtValue(Convert.ToString(initial ?? "") ?? ""); break;
                default: f.Value = new SfmExtValue(0); break;
            }
            _fields[name] = f;
        }

        public static void Unregister(string name) => _fields.Remove(name);

        // ---------- 读写 ----------
        public static double GetFloat(string name)
        {
            return _fields.TryGetValue(name, out var f) ? f.Value.ToFloat() : 0;
        }

        public static string GetString(string name)
        {
            return _fields.TryGetValue(name, out var f) ? f.Value.ToString() : "";
        }

        public static bool GetBool(string name)
        {
            return _fields.TryGetValue(name, out var f) && f.Value.ToBool();
        }

        public static void Set(string name, object value)
        {
            if (!_fields.TryGetValue(name, out var f)) return;
            var nv = value is SfmExtValue ev ? ev : new SfmExtValue(value);
            if (Math.Abs(nv.ToFloat() - f.Value.ToFloat()) < 0.00001 && nv.ToString() == f.Value.ToString()) return;
            f.Value = nv;
            f.Changed = true;
            if (_handlers.TryGetValue(name, out var h)) h(name, nv);
        }

        public static void OnChanged(string name, Action<string, SfmExtValue> handler) => _handlers[name] = handler;

        // ---------- 引擎函数 ----------
        [SfmExtFunction("sync.register")]
        public static SfmExtValue FnRegister(SfmExtParams p, SfmExtValue u)
        {
            var kind = p.Get("kind").ToString() switch
            {
                "string" => SfmExtValue.Type.String,
                "bool" => SfmExtValue.Type.Bool,
                _ => SfmExtValue.Type.Number
            };
            Register(p.Get("name").ToString(), kind, p.Has("value") ? p.Get("value") : null, (float)p.Get("hz", "5").ToFloat());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("sync.set")]
        public static SfmExtValue FnSet(SfmExtParams p, SfmExtValue u)
        {
            Set(p.Get("name").ToString(), p.Get("value"));
            return SfmExtValue.Null;
        }

        [SfmExtFunction("sync.get")]
        public static SfmExtValue FnGet(SfmExtParams p, SfmExtValue u)
        {
            var name = p.Get("name").ToString();
            return _fields.TryGetValue(name, out var f) ? f.Value : SfmExtValue.Null;
        }

        [SfmExtFunction("sync.unregister")]
        public static SfmExtValue FnUnregister(SfmExtParams p, SfmExtValue u)
        {
            Unregister(p.Get("name").ToString());
            return SfmExtValue.Null;
        }

        // ---------- 每帧：广播变化 ----------
        [SfmExtUpdate]
        public static void Update()
        {
            if (!SfmExtBridge.BridgeReady) return;
            float now = Time.unscaledTime;
            foreach (var f in _fields.Values)
            {
                if (!f.Changed && now < f.NextSendAt) continue;
                if (now < f.NextSendAt) continue;
                f.NextSendAt = now + 1f / Math.Max(1f, f.Hz);
                if (!f.Changed) continue;
                f.Changed = false;
                var payload = new Dictionary<string, object>
                {
                    ["t"] = "ext_sync", ["ns"] = SfmExtEventBus.Namespace,
                    ["k"] = f.Name
                };
                switch (f.Kind)
                {
                    case SfmExtValue.Type.Number: payload["v"] = f.Value.Number; break;
                    case SfmExtValue.Type.Bool: payload["v"] = f.Value.Bool; break;
                    default: payload["v"] = f.Value.String; break;
                }
                SfmExtBridge.SendToRoom?.Invoke(payload);
            }
        }

        // 收到远端同步
        internal static void HandleRemoteSync(string name, object value)
        {
            if (!_fields.TryGetValue(name, out var f)) return;
            var nv = value is string s ? new SfmExtValue(s)
                : value is bool b ? new SfmExtValue(b)
                : value is double d ? new SfmExtValue(d)
                : value is long l ? new SfmExtValue((double)l)
                : new SfmExtValue(0);
            f.Value = nv;
            if (_handlers.TryGetValue(name, out var h)) h(name, nv);
        }
    }
}
