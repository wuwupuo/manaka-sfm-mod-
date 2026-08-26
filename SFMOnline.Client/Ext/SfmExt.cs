using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  SFM Online 联机拓展前置框架 - 核心入口
    //  模仿 V2(SFM_custom_mission_v2) 的 EngineFunction 注册模式，
    //  并增加联机扩展能力：事件总线、自定义消息通道、积分表、骨骼扩展、
    //  区域触发、玩法控制、HUD 等。
    //  其它模组引用 SFMOnline.dll 后调用 SfmExt.* 静态 API 即可。
    // ====================================================================
    public static class SfmExt
    {
        public const string ExtVersion = "1.0.0";

        // 拓展函数签名：Func<参数, 旧值, 返回值>（模仿 V2 的 Func<ProgramVariables, ProgramThreadBase, ProgramValue>）
        internal static readonly Dictionary<string, Func<SfmExtParams, SfmExtValue, SfmExtValue>> Functions
            = new Dictionary<string, Func<SfmExtParams, SfmExtValue, SfmExtValue>>();

        internal static readonly List<Action> UpdateFunctions = new List<Action>();
        internal static readonly List<Action> LateUpdateFunctions = new List<Action>();
        internal static readonly List<Action> GuiFunctions = new List<Action>();

        private static bool _ready;
        private static bool _initStarted;

        // ---------- 生命周期 ----------
        /// <summary>立即后台注册（由 Plugin.Load 后台线程调用，不依赖主循环）。</summary>
        internal static void InitNow()
        {
            if (_ready || _initStarted) return;
            _initStarted = true;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    RegisterAll();
                    _ready = true;
                    Info("SFM Online Ext v" + ExtVersion + " 已注册 " + Functions.Count + " 个拓展函数");
                }
                catch (Exception e)
                {
                    Error("拓展函数注册失败: " + e);
                }
            });
        }

        /// <summary>延迟到游戏空闲时初始化（兼容旧调用，Plugin.Load 已用 InitNow）。</summary>
        internal static void EnsureInit()
        {
            if (_ready || _initStarted) return;
            InitNow();
        }

        internal static void OnUpdate()
        {
            if (!_ready) return;
            for (int i = 0; i < UpdateFunctions.Count; i++)
            {
                try { UpdateFunctions[i](); } catch { }
            }
        }

        // 集中注册所有内置函数库
        private static void RegisterAll()
        {
            RegisterFunctionsOfType(typeof(SfmExtScore));
            RegisterFunctionsOfType(typeof(SfmExtBone));
            RegisterFunctionsOfType(typeof(SfmExtAreaManager));
            RegisterFunctionsOfType(typeof(SfmExtTriggerManager));
            RegisterFunctionsOfType(typeof(SfmExtPlay));
            RegisterFunctionsOfType(typeof(SfmExtState));
            RegisterFunctionsOfType(typeof(SfmExtPlayer));
            RegisterFunctionsOfType(typeof(SfmExtChat));
            RegisterFunctionsOfType(typeof(SfmExtCamera));
            RegisterFunctionsOfType(typeof(SfmExtTaskManager));
            RegisterFunctionsOfType(typeof(SfmExtNpcManager));
            RegisterFunctionsOfType(typeof(SfmExtHud));
            RegisterFunctionsOfType(typeof(SfmExtMath));
            RegisterFunctionsOfType(typeof(SfmExtCore));
            RegisterFunctionsOfType(typeof(SfmExtTimer));
            RegisterFunctionsOfType(typeof(SfmExtGame));
            RegisterFunctionsOfType(typeof(SfmExtNet));
            RegisterFunctionsOfType(typeof(SfmExtRoom));
            RegisterFunctionsOfType(typeof(SfmExtSync));
            RegisterFunctionsOfType(typeof(SfmExtInteract));
            RegisterFunctionsOfType(typeof(SfmExtSave));
            RegisterFunctionsOfType(typeof(SfmExtRandom));
            RegisterFunctionsOfType(typeof(SfmExtVote));
            RegisterFunctionsOfType(typeof(SfmExtGameplay));
            RegisterFunctionsOfType(typeof(SfmExtVar));
            RegisterFunctionsOfType(typeof(SfmExtList));
            RegisterFunctionsOfType(typeof(SfmExtPhone));
            RegisterFunctionsOfType(typeof(SfmExtRemote));
        }

        internal static void OnLateUpdate()
        {
            if (!_ready) return;
            for (int i = 0; i < LateUpdateFunctions.Count; i++)
            {
                try { LateUpdateFunctions[i](); } catch { }
            }
        }

        internal static void OnGUI()
        {
            if (!_ready) return;
            for (int i = 0; i < GuiFunctions.Count; i++)
            {
                try { GuiFunctions[i](); } catch { }
            }
        }

        // ---------- 函数注册（模仿 V2 Engine.RegisterAll，带静态缓存） ----------
        private static readonly Dictionary<Type, List<MethodInfo>> _methodCache
            = new Dictionary<Type, List<MethodInfo>>();

        public static void RegisterFunctionsOfType(Type t)
        {
            // 反射缓存：每个类型只扫描一次
            List<MethodInfo> methods;
            lock (_methodCache)
            {
                if (!_methodCache.TryGetValue(t, out methods))
                {
                    methods = t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).ToList();
                    _methodCache[t] = methods;
                }
            }
            foreach (var m in methods)
            {
                var attr = (SfmExtFunctionAttribute)m.GetCustomAttributes(typeof(SfmExtFunctionAttribute), false).FirstOrDefault();
                if (attr != null)
                {
                    string name = string.IsNullOrEmpty(attr.Name) ? m.Name : attr.Name;
                    RegisterFunction(name, (Func<SfmExtParams, SfmExtValue, SfmExtValue>)
                        Delegate.CreateDelegate(typeof(Func<SfmExtParams, SfmExtValue, SfmExtValue>), m));
                }
                if (m.GetCustomAttributes(typeof(SfmExtUpdateAttribute), false).Length > 0)
                {
                    UpdateFunctions.Add((Action)Delegate.CreateDelegate(typeof(Action), m));
                }
            }
        }

        /// <summary>注册一个拓展函数（其它模组在 OnLoad 时调用）。</summary>
        public static void RegisterFunction(string name, Func<SfmExtParams, SfmExtValue, SfmExtValue> func)
        {
            if (Functions.ContainsKey(name))
            {
                Warn("拓展函数已存在: " + name + "（覆盖）");
                Functions[name] = func;
            }
            else
            {
                Functions.Add(name, func);
            }
        }

        public static bool HasFunction(string name) => Functions.ContainsKey(name);

        public static SfmExtValue CallFunction(string name, SfmExtParams parameters)
        {
            if (Functions.TryGetValue(name, out var f)) return f(parameters, SfmExtValue.Null);
            throw new Exception("拓展函数 \"" + name + "\" 不存在");
        }

        // ---------- 日志 ----------
        public static void Info(string msg) => PluginInfo.Info("[Ext] " + msg);
        public static void Warn(string msg) => PluginInfo.Warn("[Ext] " + msg);
        public static void Error(string msg) => PluginInfo.Error("[Ext] " + msg);
        public static void Log(string msg) => PluginInfo.Info("[Ext] " + msg);
    }

    // ====================================================================
    //  特性：模仿 V2 的 EngineFunctionAttribute / UpdateFunctionAttribute
    // ====================================================================
    [AttributeUsage(AttributeTargets.Method)]
    public class SfmExtFunctionAttribute : Attribute
    {
        public string Name;
        public SfmExtFunctionAttribute() { }
        public SfmExtFunctionAttribute(string name) { Name = name; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class SfmExtUpdateAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class SfmExtLateUpdateAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class SfmExtGuiAttribute : Attribute { }

    // ====================================================================
    //  参数容器：模仿 V2 ProgramVariables 的简易版
    // ====================================================================
    public class SfmExtParams
    {
        private readonly Dictionary<string, SfmExtValue> _vars = new Dictionary<string, SfmExtValue>();

        public SfmExtParams() { }
        public SfmExtParams(params (string, object)[] items)
        {
            foreach (var (k, v) in items) Set(k, v);
        }

        public SfmExtValue this[string key]
        {
            get => Get(key);
            set => Set(key, value);
        }

        public SfmExtParams Set(string key, object value)
        {
            _vars[key] = value is SfmExtValue v ? v : new SfmExtValue(value);
            return this;
        }

        public bool Has(string key) => _vars.ContainsKey(key);
        public SfmExtValue Get(string key) => _vars.TryGetValue(key, out var v) ? v : SfmExtValue.Null;

        /// <summary>带默认值的读取：没有时返回默认值字符串。</summary>
        public SfmExtValue Get(string key, string def)
            => _vars.TryGetValue(key, out var v) ? v : new SfmExtValue(def);
        public IEnumerable<string> Keys => _vars.Keys;
    }

    // ====================================================================
    //  值类型：模仿 V2 ProgramValue 的简易版（Number/String/Bool/List/Object）
    // ====================================================================
    public class SfmExtValue
    {
        public enum Type { Null, Number, String, Bool, List, Object, BoneRef, AreaRef, TaskRef, NpcRef, PlayerRef }

        public Type ValueType;
        public double Number;
        public string String;
        public bool Bool;
        public Dictionary<string, SfmExtValue> List;
        public object ObjectRef;

        public static readonly SfmExtValue Null = new SfmExtValue();

        public SfmExtValue() { ValueType = Type.Null; }
        public SfmExtValue(double n) { ValueType = Type.Number; Number = n; }
        public SfmExtValue(int n) { ValueType = Type.Number; Number = n; }
        public SfmExtValue(float n) { ValueType = Type.Number; Number = n; }
        public SfmExtValue(bool b) { ValueType = Type.Bool; Bool = b; }
        public SfmExtValue(string s) { ValueType = Type.String; String = s ?? ""; }
        public SfmExtValue(Type t) { ValueType = t; }
        public SfmExtValue(object o) { ObjectRef = o; ValueType = Type.Object; }

        public bool IsNull => ValueType == Type.Null;
        public double ToFloat() => ValueType == Type.Number ? Number : (ValueType == Type.Bool ? (Bool ? 1 : 0) : (double.TryParse(String, out var d) ? d : 0));
        public bool ToBool() => ValueType == Type.Bool ? Bool : (ValueType == Type.Number ? Number != 0 : !string.IsNullOrEmpty(String));
        public override string ToString() => ValueType == Type.Number ? Number.ToString("0.######") : ValueType == Type.Bool ? (Bool ? "true" : "false") : ValueType == Type.Null ? "null" : String;

        public SfmExtValue this[string key]
        {
            get
            {
                if (List == null) return Null;
                return List.TryGetValue(key, out var v) ? v : Null;
            }
            set
            {
                if (List == null) List = new Dictionary<string, SfmExtValue>();
                List[key] = value;
            }
        }

        public SfmExtValue AsList()
        {
            if (ValueType == Type.List) return this;
            var v = new SfmExtValue(Type.List);
            v.List = new Dictionary<string, SfmExtValue>();
            return v;
        }
    }
}
