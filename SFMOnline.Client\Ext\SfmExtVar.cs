using System;
using System.Collections.Generic;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  运行时变量：图形化编程（积木）的变量系统
    //  创建变量 / 设置 / 修改 / 读取（支持跨事件共享，可联机同步）
    // ====================================================================
    public static class SfmExtVar
    {
        private sealed class Var
        {
            public string Name;
            public double Number;
            public string Text;
            public bool IsNumber = true;
        }

        private static readonly Dictionary<string, Var> _vars = new Dictionary<string, Var>();

        public static void Create(string name, object value = null)
        {
            if (_vars.ContainsKey(name)) return;
            var v = new Var { Name = name };
            Set(name, value);
            _vars[name] = v;
        }

        public static bool Exists(string name) => _vars.ContainsKey(name);

        public static void Set(string name, object value)
        {
            if (!_vars.TryGetValue(name, out var v)) { v = new Var { Name = name }; _vars[name] = v; }
            if (value is double d) { v.Number = d; v.IsNumber = true; }
            else if (value is int i) { v.Number = i; v.IsNumber = true; }
            else if (value is float f) { v.Number = f; v.IsNumber = true; }
            else if (value is bool b) { v.Text = b ? "true" : "false"; v.IsNumber = false; }
            else { v.Text = Convert.ToString(value) ?? ""; v.IsNumber = double.TryParse(v.Text, out var dv); if (v.IsNumber) v.Number = dv; }
            SfmExtEvent.Emit("var_changed", new SfmExtValue(SfmExtValue.Type.List)
            {
                ["name"] = new SfmExtValue(name),
                ["value"] = new SfmExtValue(GetRaw(name))
            });
        }

        public static void Add(string name, double delta)
        {
            if (!_vars.TryGetValue(name, out var v)) { v = new Var { Name = name }; _vars[name] = v; }
            v.Number += delta;
            v.IsNumber = true;
        }

        public static double GetFloat(string name)
        {
            if (!_vars.TryGetValue(name, out var v)) return 0;
            return v.IsNumber ? v.Number : (double.TryParse(v.Text, out var d) ? d : 0);
        }

        public static string GetString(string name)
        {
            if (!_vars.TryGetValue(name, out var v)) return "";
            return v.IsNumber ? v.Number.ToString("0.######") : v.Text;
        }

        public static object GetRaw(string name)
        {
            if (!_vars.TryGetValue(name, out var v)) return null;
            return v.IsNumber ? (object)v.Number : v.Text;
        }

        public static bool GetBool(string name)
        {
            if (!_vars.TryGetValue(name, out var v)) return false;
            return v.IsNumber ? v.Number != 0 : (v.Text == "true" || v.Text == "1");
        }

        public static void Delete(string name) => _vars.Remove(name);

        public static IEnumerable<string> Names => _vars.Keys;

        // ---------- 引擎函数 ----------
        [SfmExtFunction("var.create")]
        public static SfmExtValue FnCreate(SfmExtParams p, SfmExtValue u)
        {
            Create(p.Get("name").ToString(), p.Has("value") ? p.Get("value") : null);
            return new SfmExtValue(true);
        }

        [SfmExtFunction("var.set")]
        public static SfmExtValue FnSet(SfmExtParams p, SfmExtValue u)
        {
            Set(p.Get("name").ToString(), p.Get("value"));
            return SfmExtValue.Null;
        }

        [SfmExtFunction("var.add")]
        public static SfmExtValue FnAdd(SfmExtParams p, SfmExtValue u)
        {
            Add(p.Get("name").ToString(), p.Get("value").ToFloat());
            return SfmExtValue.Null;
        }

        [SfmExtFunction("var.get")]
        public static SfmExtValue FnGet(SfmExtParams p, SfmExtValue u)
        {
            var name = p.Get("name").ToString();
            if (!_vars.TryGetValue(name, out var v)) return SfmExtValue.Null;
            return v.IsNumber ? new SfmExtValue(v.Number) : new SfmExtValue(v.Text);
        }

        [SfmExtFunction("var.exists")]
        public static SfmExtValue FnExists(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(Exists(p.Get("name").ToString()));
    }

    // ====================================================================
    //  运行时列表：积木编程的列表系统
    // ====================================================================
    public static class SfmExtList
    {
        private static readonly Dictionary<string, List<string>> _lists = new Dictionary<string, List<string>>();

        public static void Create(string name)
        {
            if (!_lists.ContainsKey(name)) _lists[name] = new List<string>();
        }

        public static void Add(string name, object value)
        {
            if (!_lists.TryGetValue(name, out var l)) { l = new List<string>(); _lists[name] = l; }
            l.Add(Convert.ToString(value) ?? "");
        }

        public static void Remove(string name, object value)
        {
            if (_lists.TryGetValue(name, out var l)) l.Remove(Convert.ToString(value) ?? "");
        }

        public static string Get(string name, int index)
        {
            if (_lists.TryGetValue(name, out var l) && index >= 0 && index < l.Count) return l[index];
            return "";
        }

        public static int Length(string name)
        {
            return _lists.TryGetValue(name, out var l) ? l.Count : 0;
        }

        public static bool Contains(string name, object value)
        {
            return _lists.TryGetValue(name, out var l) && l.Contains(Convert.ToString(value) ?? "");
        }

        public static void Clear(string name)
        {
            if (_lists.TryGetValue(name, out var l)) l.Clear();
        }

        public static IEnumerable<string> Names => _lists.Keys;

        [SfmExtFunction("list.create")]
        public static SfmExtValue FnCreate(SfmExtParams p, SfmExtValue u)
        {
            Create(p.Get("name").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("list.add")]
        public static SfmExtValue FnAdd(SfmExtParams p, SfmExtValue u)
        {
            Add(p.Get("name").ToString(), p.Get("value"));
            return SfmExtValue.Null;
        }

        [SfmExtFunction("list.remove")]
        public static SfmExtValue FnRemove(SfmExtParams p, SfmExtValue u)
        {
            Remove(p.Get("name").ToString(), p.Get("value"));
            return SfmExtValue.Null;
        }

        [SfmExtFunction("list.get")]
        public static SfmExtValue FnGet(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(Get(p.Get("name").ToString(), (int)p.Get("index").ToFloat()));

        [SfmExtFunction("list.size")]
        public static SfmExtValue FnSize(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(Length(p.Get("name").ToString()));

        [SfmExtFunction("list.has")]
        public static SfmExtValue FnHas(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(Contains(p.Get("name").ToString(), p.Get("value")));

        [SfmExtFunction("list.clear")]
        public static SfmExtValue FnClear(SfmExtParams p, SfmExtValue u)
        {
            Clear(p.Get("name").ToString());
            return SfmExtValue.Null;
        }
    }
}
