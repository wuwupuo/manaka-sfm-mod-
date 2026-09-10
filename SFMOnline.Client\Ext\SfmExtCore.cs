using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  核心函数库（模仿 V2 FunctionsCore）：
    //  字符串 / 列表 / 变量 / 事件 / 线程 / 文件 / 系统 / 颜色
    // ====================================================================
    public static class SfmExtCore
    {
        // ---------- 日志 ----------
        [SfmExtFunction("system.log")] public static SfmExtValue FnLog(SfmExtParams p, SfmExtValue u) { SfmExt.Log(p.Get("text").ToString()); return SfmExtValue.Null; }
        [SfmExtFunction("system.warning")] public static SfmExtValue FnWarn(SfmExtParams p, SfmExtValue u) { SfmExt.Warn(p.Get("text").ToString()); return SfmExtValue.Null; }
        [SfmExtFunction("system.error")] public static SfmExtValue FnErr(SfmExtParams p, SfmExtValue u) { SfmExt.Error(p.Get("text").ToString()); return SfmExtValue.Null; }
        [SfmExtFunction("system.function_exists")] public static SfmExtValue FnFuncExists(SfmExtParams p, SfmExtValue u) => new SfmExtValue(SfmExt.HasFunction(p.Get("name").ToString()));
        [SfmExtFunction("system.ext_version")] public static SfmExtValue FnExtVer(SfmExtParams p, SfmExtValue u) => new SfmExtValue(SfmExt.ExtVersion);
        [SfmExtFunction("system.bridge_ready")] public static SfmExtValue FnBridgeReady(SfmExtParams p, SfmExtValue u) => new SfmExtValue(SfmExtBridge.BridgeReady);

        // ---------- 字符串 ----------
        [SfmExtFunction("string.format")] public static SfmExtValue FnFormat(SfmExtParams p, SfmExtValue u)
        {
            var fmt = p.Get("format").ToString();
            var args = new List<object>();
            for (int i = 0; i < 10; i++)
            {
                if (!p.Has("arg" + i)) break;
                var v = p.Get("arg" + i);
                args.Add(v.ValueType == SfmExtValue.Type.Number ? v.Number : v.ToString());
            }
            try { return new SfmExtValue(string.Format(fmt, args.ToArray())); }
            catch { return new SfmExtValue(fmt); }
        }
        [SfmExtFunction("string.length")] public static SfmExtValue FnLen(SfmExtParams p, SfmExtValue u) => new SfmExtValue(p.Get("value").ToString().Length);
        [SfmExtFunction("string.lower")] public static SfmExtValue FnLower(SfmExtParams p, SfmExtValue u) => new SfmExtValue(p.Get("value").ToString().ToLowerInvariant());
        [SfmExtFunction("string.upper")] public static SfmExtValue FnUpper(SfmExtParams p, SfmExtValue u) => new SfmExtValue(p.Get("value").ToString().ToUpperInvariant());
        [SfmExtFunction("string.substr")] public static SfmExtValue FnSubStr(SfmExtParams p, SfmExtValue u)
        {
            var s = p.Get("value").ToString();
            int start = (int)p.Get("start").ToFloat();
            if (p.Has("length"))
            {
                int len = (int)p.Get("length").ToFloat();
                if (start >= 0 && start + len <= s.Length) return new SfmExtValue(s.Substring(start, len));
                return SfmExtValue.Null;
            }
            if (start >= 0 && start <= s.Length) return new SfmExtValue(s.Substring(start));
            return SfmExtValue.Null;
        }
        [SfmExtFunction("string.find")] public static SfmExtValue FnFind(SfmExtParams p, SfmExtValue u)
        {
            var s = p.Get("value").ToString();
            var sub = p.Get("find").ToString();
            int idx = s.IndexOf(sub, StringComparison.Ordinal);
            return new SfmExtValue(idx);
        }
        [SfmExtFunction("string.contains")] public static SfmExtValue FnContains(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(p.Get("value").ToString().Contains(p.Get("find").ToString()));
        [SfmExtFunction("string.replace")] public static SfmExtValue FnReplace(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(p.Get("value").ToString().Replace(p.Get("from").ToString(), p.Get("to").ToString()));
        [SfmExtFunction("string.tonumber")] public static SfmExtValue FnToNum(SfmExtParams p, SfmExtValue u)
        {
            double d; return new SfmExtValue(double.TryParse(p.Get("value").ToString(), out d) ? d : 0);
        }
        [SfmExtFunction("string.tostring")] public static SfmExtValue FnToStr(SfmExtParams p, SfmExtValue u) => new SfmExtValue(p.Get("value").ToString());
        [SfmExtFunction("string.split")] public static SfmExtValue FnSplit(SfmExtParams p, SfmExtValue u)
        {
            var parts = p.Get("value").ToString().Split(new[] { p.Get("sep").ToString() }, StringSplitOptions.None);
            var v = new SfmExtValue(SfmExtValue.Type.List);
            for (int i = 0; i < parts.Length; i++) v[i.ToString()] = new SfmExtValue(parts[i]);
            return v;
        }
        [SfmExtFunction("string.join")] public static SfmExtValue FnJoin(SfmExtParams p, SfmExtValue u)
        {
            var list = new List<string>();
            for (int i = 0; i < 50; i++)
            {
                if (!p.Has("item" + i)) break;
                list.Add(p.Get("item" + i).ToString());
            }
            return new SfmExtValue(string.Join(p.Get("sep").ToString(), list.ToArray()));
        }

        // ---------- 列表 ----------
        [SfmExtFunction("list.create")] public static SfmExtValue FnCreateList(SfmExtParams p, SfmExtValue u) => new SfmExtValue(SfmExtValue.Type.List);
        [SfmExtFunction("list.get")] public static SfmExtValue FnListGet(SfmExtParams p, SfmExtValue u)
            => p.Get("list")[p.Get("key").ToString()];
        [SfmExtFunction("list.set")] public static SfmExtValue FnListSet(SfmExtParams p, SfmExtValue u)
        {
            var l = p.Get("list").AsList();
            l[p.Get("key").ToString()] = p.Get("value");
            return SfmExtValue.Null;
        }
        [SfmExtFunction("list.size")] public static SfmExtValue FnListSize(SfmExtParams p, SfmExtValue u)
        {
            var l = p.Get("list");
            return new SfmExtValue(l.List != null ? l.List.Count : 0);
        }
        [SfmExtFunction("list.has")] public static SfmExtValue FnListHas(SfmExtParams p, SfmExtValue u)
        {
            var l = p.Get("list");
            return new SfmExtValue(l.List != null && l.List.ContainsKey(p.Get("key").ToString()));
        }
        [SfmExtFunction("list.copy")] public static SfmExtValue FnListCopy(SfmExtParams p, SfmExtValue u)
        {
            var src = p.Get("list");
            var v = new SfmExtValue(SfmExtValue.Type.List);
            if (src.List != null) foreach (var kv in src.List) v[kv.Key] = kv.Value;
            return v;
        }
        [SfmExtFunction("list.json")] public static SfmExtValue FnListJson(SfmExtParams p, SfmExtValue u)
        {
            var v = p.Get("value");
            return SfmExtEvent.FromJsonObject(v);
        }

        // ---------- 事件 ----------
        [SfmExtFunction("event.set")] public static SfmExtValue FnEventSet(SfmExtParams p, SfmExtValue u)
        {
            SfmExtEvent.Emit(p.Get("event").ToString(), p.Get("value"));
            return SfmExtValue.Null;
        }
        [SfmExtFunction("event.get")] public static SfmExtValue FnEventGet(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(true); // 事件值通过 On 回调获得
        [SfmExtFunction("event.on")] public static SfmExtValue FnEventOn(SfmExtParams p, SfmExtValue u)
        {
            // 供 C# 侧调用：注册回调用 SfmExtEvent.On
            return SfmExtValue.Null;
        }
        [SfmExtFunction("event.emit")] public static SfmExtValue FnEventEmit(SfmExtParams p, SfmExtValue u)
        {
            SfmExtEvent.Emit(p.Get("event").ToString(), p.Get("value"));
            return SfmExtValue.Null;
        }
        [SfmExtFunction("event.emit_net")] public static SfmExtValue FnEventEmitNet(SfmExtParams p, SfmExtValue u)
        {
            SfmExtEvent.EmitNet(p.Get("event").ToString(), p.Get("value"));
            return SfmExtValue.Null;
        }

        // ---------- 文件 ----------
        [SfmExtFunction("file.exists")] public static SfmExtValue FnFileExists(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(System.IO.File.Exists(ResolvePath(p.Get("path").ToString())));
        [SfmExtFunction("file.read")] public static SfmExtValue FnFileRead(SfmExtParams p, SfmExtValue u)
        {
            try
            {
                var path = ResolvePath(p.Get("path").ToString());
                return System.IO.File.Exists(path) ? new SfmExtValue(System.IO.File.ReadAllText(path)) : SfmExtValue.Null;
            }
            catch { return SfmExtValue.Null; }
        }
        [SfmExtFunction("file.write")] public static SfmExtValue FnFileWrite(SfmExtParams p, SfmExtValue u)
        {
            try
            {
                var path = ResolvePath(p.Get("path").ToString());
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(path, p.Get("content").ToString());
                return new SfmExtValue(true);
            }
            catch { return new SfmExtValue(false); }
        }
        [SfmExtFunction("file.delete")] public static SfmExtValue FnFileDelete(SfmExtParams p, SfmExtValue u)
        {
            try { System.IO.File.Delete(ResolvePath(p.Get("path").ToString())); return new SfmExtValue(true); }
            catch { return new SfmExtValue(false); }
        }
        [SfmExtFunction("file.get_files")] public static SfmExtValue FnGetFiles(SfmExtParams p, SfmExtValue u)
        {
            try
            {
                var dir = ResolvePath(p.Get("dir").ToString());
                var pattern = p.Get("pattern").ToString();
                if (pattern.Length == 0) pattern = "*";
                var files = System.IO.Directory.GetFiles(dir, pattern);
                var v = new SfmExtValue(SfmExtValue.Type.List);
                for (int i = 0; i < files.Length; i++) v[i.ToString()] = new SfmExtValue(files[i]);
                return v;
            }
            catch { return new SfmExtValue(SfmExtValue.Type.List); }
        }
        [SfmExtFunction("file.extension")] public static SfmExtValue FnFileExt(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(System.IO.Path.GetExtension(p.Get("path").ToString()));

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            try
            {
                if (path.StartsWith("~")) return path.Replace("~", BepInEx.Paths.GameRootPath);
            }
            catch { }
            return path;
        }

        // ---------- 颜色 ----------
        [SfmExtFunction("color.rgb")] public static SfmExtValue FnColorRgb(SfmExtParams p, SfmExtValue u)
        {
            var c = new Color((float)p.Get("r").ToFloat(), (float)p.Get("g").ToFloat(), (float)p.Get("b").ToFloat(), 1f);
            return new SfmExtValue(c);
        }
        [SfmExtFunction("color.rgba")] public static SfmExtValue FnColorRgba(SfmExtParams p, SfmExtValue u)
        {
            var c = new Color((float)p.Get("r").ToFloat(), (float)p.Get("g").ToFloat(), (float)p.Get("b").ToFloat(), (float)p.Get("a").ToFloat());
            return new SfmExtValue(c);
        }

        // ---------- 变量/杂项 ----------
        [SfmExtFunction("misc.get_type")] public static SfmExtValue FnGetType(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(p.Get("value").ValueType.ToString());
        [SfmExtFunction("misc.is_null")] public static SfmExtValue FnIsNull(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(p.Get("value").IsNull);
        [SfmExtFunction("misc.wait_seconds")] public static SfmExtValue FnWait(SfmExtParams p, SfmExtValue u)
        {
            // 简单延迟执行（非阻塞）
            float sec = (float)p.Get("seconds").ToFloat();
            var evt = p.Get("event").ToString();
            var data = p.Get("data");
            SfmExtTimer.Delay(sec, () => SfmExtEvent.Emit(evt, data));
            return SfmExtValue.Null;
        }
        [SfmExtFunction("misc.every_seconds")] public static SfmExtValue FnEvery(SfmExtParams p, SfmExtValue u)
        {
            float sec = (float)p.Get("seconds").ToFloat();
            var evt = p.Get("event").ToString();
            var data = p.Get("data");
            SfmExtTimer.Every(sec, () => SfmExtEvent.Emit(evt, data));
            return SfmExtValue.Null;
        }
    }

    // ====================================================================
    //  定时器：Delay / Every（模仿 V2 的 Timer/监听器）
    // ====================================================================
    public static class SfmExtTimer
    {
        private sealed class Entry
        {
            public float Interval;
            public float NextAt;
            public Action Action;
            public bool Repeat;
            public bool Alive = true;
        }

        private static readonly List<Entry> _entries = new List<Entry>();

        public static void Delay(float seconds, Action action)
        {
            lock (_entries)
                _entries.Add(new Entry { Interval = seconds, NextAt = UnityEngine.Time.unscaledTime + seconds, Action = action, Repeat = false });
        }

        public static void Every(float seconds, Action action)
        {
            lock (_entries)
                _entries.Add(new Entry { Interval = seconds, NextAt = UnityEngine.Time.unscaledTime + seconds, Action = action, Repeat = true });
        }

        // 等待直到条件满足（每帧检测）
        public static void WaitUntil(Func<bool> condition, Action action, float timeout = 0f)
        {
            var start = UnityEngine.Time.unscaledTime;
            lock (_entries)
                _entries.Add(new Entry
                {
                    Interval = 0.1f,
                    NextAt = UnityEngine.Time.unscaledTime,
                    Action = () =>
                    {
                        bool done = (condition != null && condition());
                        if (!done && timeout > 0f && UnityEngine.Time.unscaledTime - start > timeout) done = true;
                        if (done)
                        {
                            try { action?.Invoke(); } catch { }
                            throw new StopWaitException();
                        }
                    },
                    Repeat = true,
                });
        }

        private sealed class StopWaitException : Exception { }

        [SfmExtUpdate]
        public static void Update()
        {
            float now = UnityEngine.Time.unscaledTime;
            lock (_entries)
            {
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    var e = _entries[i];
                    if (!e.Alive) { _entries.RemoveAt(i); continue; }
                    if (now >= e.NextAt)
                    {
                        try { e.Action(); }
                        catch (StopWaitException)
                        {
                            e.Alive = false;
                            _entries.RemoveAt(i);
                            continue;
                        }
                        catch { }
                        if (e.Repeat) e.NextAt = now + e.Interval;
                        else { e.Alive = false; _entries.RemoveAt(i); }
                    }
                }
            }
        }
    }
}
