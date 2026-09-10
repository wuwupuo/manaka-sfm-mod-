using System;
using System.Collections.Generic;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  通用存档 API：任意键值对本地持久化（SFMOnlineExt/data/*.json）
    // ====================================================================
    public static class SfmExtSave
    {
        private static readonly Dictionary<string, Dictionary<string, object>> _cache
            = new Dictionary<string, Dictionary<string, object>>();

        private static string PathFor(string space)
        {
            return System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "SFMOnlineExt", "data", space + ".json");
        }

        private static Dictionary<string, object> Load(string space)
        {
            if (_cache.TryGetValue(space, out var d)) return d;
            d = new Dictionary<string, object>();
            try
            {
                var p = PathFor(space);
                if (System.IO.File.Exists(p))
                {
                    var parsed = MiniJson.ParseObject(System.IO.File.ReadAllText(p));
                    if (parsed != null) d = parsed;
                }
            }
            catch { }
            _cache[space] = d;
            return d;
        }

        private static void Save(string space)
        {
            try
            {
                var p = PathFor(space);
                var dir = System.IO.Path.GetDirectoryName(p);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(p, MiniJson.Serialize(Load(space)));
            }
            catch { }
        }

        // ---------- 读写 ----------
        public static void Set(string space, string key, object value)
        {
            var d = Load(space);
            d[key] = value;
            Save(space);
        }

        public static object Get(string space, string key, object def = null)
        {
            var d = Load(space);
            return d.TryGetValue(key, out var v) ? v : def;
        }

        public static double GetFloat(string space, string key, double def = 0)
        {
            var v = Get(space, key, null);
            if (v is double d) return d;
            if (v is long l) return l;
            if (v is int i) return i;
            double.TryParse(Convert.ToString(v), out var r);
            return r == 0 ? def : r;
        }

        public static string GetString(string space, string key, string def = "")
        {
            var v = Get(space, key, null);
            return v != null ? Convert.ToString(v) : def;
        }

        public static bool GetBool(string space, string key, bool def = false)
        {
            var v = Get(space, key, null);
            if (v is bool b) return b;
            return Convert.ToString(v) == "True" || Convert.ToString(v) == "true" || Convert.ToString(v) == "1" ? true : def;
        }

        public static void Remove(string space, string key)
        {
            var d = Load(space);
            if (d.Remove(key)) Save(space);
        }

        public static bool Has(string space, string key) => Load(space).ContainsKey(key);

        public static void Clear(string space)
        {
            _cache[space] = new Dictionary<string, object>();
            try { if (System.IO.File.Exists(PathFor(space))) System.IO.File.Delete(PathFor(space)); } catch { }
        }

        // ---------- 引擎函数 ----------
        [SfmExtFunction("save.set")]
        public static SfmExtValue FnSet(SfmExtParams p, SfmExtValue u)
        {
            Set(p.Get("space").ToString(), p.Get("key").ToString(), SfmExtEvent.ToJsonObject(p.Get("value")));
            return SfmExtValue.Null;
        }

        [SfmExtFunction("save.get")]
        public static SfmExtValue FnGet(SfmExtParams p, SfmExtValue u)
        {
            var v = Get(p.Get("space").ToString(), p.Get("key").ToString(), null);
            return SfmExtEvent.FromJsonObject(v);
        }

        [SfmExtFunction("save.has")]
        public static SfmExtValue FnHas(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(Has(p.Get("space").ToString(), p.Get("key").ToString()));

        [SfmExtFunction("save.remove")]
        public static SfmExtValue FnRemove(SfmExtParams p, SfmExtValue u)
        {
            Remove(p.Get("space").ToString(), p.Get("key").ToString());
            return SfmExtValue.Null;
        }
    }
}
