using System;
using System.Collections.Generic;
using System.Linq;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  积分表系统：
    //  - 创建多个命名积分表（如 "金币"、"经验"、"RP"）
    //  - 全局(跨存档) / 房间内同步两种模式
    //  - 本地 + 联机广播（同房间其它玩家看到最新值）
    //  - 模仿 V2 AddCurrentRP/SetCurrentRP/GetCurrentRP 的引擎函数
    // ====================================================================
    public static class SfmExtScore
    {
        public sealed class ScoreTable
        {
            public string Name;
            public double Value;
            public double Min;
            public double Max = double.MaxValue;
            public bool Synced;          // 是否联机同步
            public bool Persisted;       // 是否本地存档
            public Action<string, double, double> OnChanged; // (name, old, new)

            public ScoreTable(string name) { Name = name; }
        }

        private static readonly Dictionary<string, ScoreTable> _tables
            = new Dictionary<string, ScoreTable>();

        private static string _lastChangedTable = "";
        private static double _lastChangedOld;

        public static IReadOnlyCollection<string> TableNames => _tables.Keys.ToList().AsReadOnly();

        /// <summary>创建/获取积分表。</summary>
        public static ScoreTable CreateTable(string name, double initial = 0, bool synced = false, bool persisted = false)
        {
            if (_tables.TryGetValue(name, out var t)) return t;
            t = new ScoreTable(name) { Value = initial, Synced = synced, Persisted = persisted };
            if (persisted) t.Value = LoadPersisted(name, initial);
            _tables[name] = t;
            return t;
        }

        public static ScoreTable GetTable(string name)
        {
            return _tables.TryGetValue(name, out var t) ? t : null;
        }

        public static bool HasTable(string name) => _tables.ContainsKey(name);

        public static bool RemoveTable(string name) => _tables.Remove(name);

        // ---------- 值操作 ----------
        public static double Get(string name)
        {
            return _tables.TryGetValue(name, out var t) ? t.Value : 0;
        }

        public static void Set(string name, double value, bool broadcast = false)
        {
            if (!_tables.TryGetValue(name, out var t))
            {
                t = CreateTable(name);
                _tables[name] = t;
            }
            var clamped = Math.Max(t.Min, Math.Min(t.Max, value));
            if (Math.Abs(clamped - t.Value) < 0.000001) return;
            _lastChangedTable = name;
            _lastChangedOld = t.Value;
            t.Value = clamped;
            if (t.Persisted) SavePersisted(name, clamped);
            t.OnChanged?.Invoke(name, _lastChangedOld, clamped);
            SfmExtEvent.Emit("score_changed", new SfmExtValue(SfmExtValue.Type.List)
            {
                ["name"] = new SfmExtValue(name),
                ["old"] = new SfmExtValue(_lastChangedOld),
                ["value"] = new SfmExtValue(clamped)
            });
            if (broadcast && t.Synced)
                BroadcastScore(name, clamped);
        }

        public static void Add(string name, double delta, bool broadcast = false) => Set(name, Get(name) + delta, broadcast);
        public static void Sub(string name, double delta, bool broadcast = false) => Set(name, Get(name) - delta, broadcast);
        public static void Reset(string name, bool broadcast = false) => Set(name, 0, broadcast);

        // ---------- 引擎函数（供脚本/其它模组调用） ----------
        [SfmExtFunction("score.create")]
        public static SfmExtValue FnCreate(SfmExtParams p, SfmExtValue unused)
        {
            var name = p.Get("name").ToString();
            var t = CreateTable(name, p.Has("value") ? p.Get("value").ToFloat() : 0,
                p.Has("synced") && p.Get("synced").ToBool());
            return new SfmExtValue(name);
        }

        [SfmExtFunction("score.get")]
        public static SfmExtValue FnGet(SfmExtParams p, SfmExtValue unused) => new SfmExtValue(Get(p.Get("name").ToString()));

        [SfmExtFunction("score.set")]
        public static SfmExtValue FnSet(SfmExtParams p, SfmExtValue unused)
        {
            Set(p.Get("name").ToString(), p.Get("value").ToFloat(), p.Has("sync") && p.Get("sync").ToBool());
            return SfmExtValue.Null;
        }

        [SfmExtFunction("score.add")]
        public static SfmExtValue FnAdd(SfmExtParams p, SfmExtValue unused)
        {
            Add(p.Get("name").ToString(), p.Get("value").ToFloat(), p.Has("sync") && p.Get("sync").ToBool());
            return SfmExtValue.Null;
        }

        [SfmExtFunction("score.list")]
        public static SfmExtValue FnList(SfmExtParams p, SfmExtValue unused)
        {
            var v = new SfmExtValue(SfmExtValue.Type.List);
            foreach (var kv in _tables) v[kv.Key] = new SfmExtValue(kv.Value.Value);
            return v;
        }

        // ---------- 联机同步 ----------
        private static void BroadcastScore(string name, double value)
        {
            SfmExtMsg.SendToRoom(new Dictionary<string, object>
            {
                ["t"] = "ext_score", ["ns"] = SfmExtEventBus.Namespace,
                ["name"] = name, ["value"] = value
            });
        }

        // 收到远端积分更新（由 OnlineCore 的 ext_ 分发调用）
        internal static void HandleRemoteScore(string name, double value)
        {
            if (!_tables.TryGetValue(name, out var t)) return;
            t.Value = Math.Max(t.Min, Math.Min(t.Max, value));
        }

        // ---------- 存档 ----------
        private static string SavePath => System.IO.Path.Combine(
            BepInEx.Paths.GameRootPath, "SFMOnlineExt", "scores.json");

        private static double LoadPersisted(string name, double def)
        {
            try
            {
                if (!System.IO.File.Exists(SavePath)) return def;
                var doc = MiniJson.ParseObject(System.IO.File.ReadAllText(SavePath));
                if (doc != null && doc.ContainsKey(name) && doc[name] is double d) return d;
            }
            catch { }
            return def;
        }

        private static void SavePersisted(string name, double value)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(SavePath);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                var doc = System.IO.File.Exists(SavePath)
                    ? MiniJson.ParseObject(System.IO.File.ReadAllText(SavePath)) ?? new Dictionary<string, object>()
                    : new Dictionary<string, object>();
                doc[name] = value;
                System.IO.File.WriteAllText(SavePath, MiniJson.Serialize(doc));
            }
            catch { }
        }

        [SfmExtUpdate]
        public static void Update()
        {
            // 预留：自动广播节流由调用方控制，此处仅占位
        }
    }
}
