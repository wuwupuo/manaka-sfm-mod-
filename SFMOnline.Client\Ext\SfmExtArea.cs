using System;
using System.Collections.Generic;
using UnityEngine;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  区域系统（模仿 V2 Area/AreaManager）：
    //  - Sphere / Cylinder / Cuboid 三种区域
    //  - 检测玩家(本地+远端)进入/离开/在区域内
    //  - 支持绑定事件回调（enter/leave/inside）
    // ====================================================================
    public enum SfmExtAreaShape { Sphere, Cylinder, Cuboid }

    public class SfmExtArea
    {
        public string Name;
        public SfmExtAreaShape Shape;
        public Vector3 Position;
        public float Radius;        // Sphere/Cylinder
        public float Height;        // Cylinder
        public Vector3 Size;        // Cuboid (half extents)
        public Quaternion Rotation;
        public bool Active = true;
        public int Stage;           // -1 = 任意地图

        // 事件（uid 为空表示本地玩家）
        public Action<string, SfmExtArea> OnEnter;
        public Action<string, SfmExtArea> OnLeave;
        public Action<string, SfmExtArea> OnInside;

        internal readonly HashSet<string> _inside = new HashSet<string>();

        public bool IsInside(Vector3 pos)
        {
            var local = Position - pos;
            var inv = Quaternion.Inverse(Rotation);
            var p = inv * local;
            switch (Shape)
            {
                case SfmExtAreaShape.Sphere:
                    return p.magnitude <= Radius;
                case SfmExtAreaShape.Cylinder:
                    return Mathf.Abs(p.y) <= Height * 0.5f &&
                           new Vector2(p.x, p.z).magnitude <= Radius;
                case SfmExtAreaShape.Cuboid:
                    return Mathf.Abs(p.x) <= Size.x * 0.5f &&
                           Mathf.Abs(p.y) <= Size.y * 0.5f &&
                           Mathf.Abs(p.z) <= Size.z * 0.5f;
            }
            return false;
        }
    }

    public static class SfmExtAreaManager
    {
        private static readonly Dictionary<string, SfmExtArea> _areas = new Dictionary<string, SfmExtArea>();

        public static SfmExtArea Create(string name, SfmExtAreaShape shape, Vector3 position,
            float radius = 1f, float height = 1f, Vector3? size = null, int stage = -1)
        {
            var a = new SfmExtArea
            {
                Name = name,
                Shape = shape,
                Position = position,
                Radius = radius,
                Height = height,
                Size = size ?? Vector3.one,
                Rotation = Quaternion.identity,
                Stage = stage
            };
            _areas[name] = a;
            SfmExtEvent.Emit("area_created", new SfmExtValue(name));
            return a;
        }

        public static SfmExtArea Get(string name) => _areas.TryGetValue(name, out var a) ? a : null;

        public static bool Remove(string name) => _areas.Remove(name);

        public static void Clear() => _areas.Clear();

        // ---------- 引擎函数 ----------
        [SfmExtFunction("area.create")]
        public static SfmExtValue FnCreate(SfmExtParams p, SfmExtValue unused)
        {
            var shape = p.Get("shape").ToString() switch
            {
                "cylinder" => SfmExtAreaShape.Cylinder,
                "cuboid" => SfmExtAreaShape.Cuboid,
                _ => SfmExtAreaShape.Sphere
            };
            var a = Create(p.Get("name").ToString(), shape,
                new Vector3((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat()),
                (float)p.Get("radius").ToFloat(), (float)p.Get("height").ToFloat(),
                null, (int)p.Get("stage").ToFloat());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("area.inside")]
        public static SfmExtValue FnInside(SfmExtParams p, SfmExtValue unused)
        {
            var a = Get(p.Get("name").ToString());
            if (a == null) return new SfmExtValue(false);
            return new SfmExtValue(a.IsInside(new Vector3(
                (float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat())));
        }

        [SfmExtFunction("area.remove")]
        public static SfmExtValue FnRemove(SfmExtParams p, SfmExtValue unused)
            => new SfmExtValue(Remove(p.Get("name").ToString()));

        // ---------- 内部：每帧检测（由 OnlineCore 调用） ----------
        internal static void Check(Vector3 localPos, string localUid, Func<string, Vector3> ghostPosGetter)
        {
            foreach (var a in _areas.Values)
            {
                if (!a.Active) continue;
                // 本地玩家
                bool lInside = a.IsInside(localPos);
                if (lInside)
                {
                    if (a._inside.Add(localUid))
                        a.OnEnter?.Invoke(localUid, a);
                    a.OnInside?.Invoke(localUid, a);
                }
                else if (a._inside.Remove(localUid))
                    a.OnLeave?.Invoke(localUid, a);
                // 远端玩家
                if (ghostPosGetter == null) continue;
                foreach (var uid in new List<string>(a._inside))
                {
                    if (uid == localUid) continue;
                    var gp = ghostPosGetter(uid);
                    if (a.IsInside(gp)) continue;
                    a._inside.Remove(uid);
                    a.OnLeave?.Invoke(uid, a);
                }
                if (OnlineCoreExt.GetGhostUids == null) continue;
                foreach (var uid in OnlineCoreExt.GetGhostUids())
                {
                    if (a._inside.Contains(uid)) continue;
                    if (a.IsInside(ghostPosGetter(uid)))
                    {
                        a._inside.Add(uid);
                        a.OnEnter?.Invoke(uid, a);
                    }
                }
            }
        }
    }

    // ====================================================================
    //  触发点系统（模仿 V1 CustomMissionArea 交互触发 + 联机）：
    //  - 交互触发（走到附近按键）
    //  - 触碰触发（进入范围立即触发）
    // ====================================================================
    public class SfmExtTrigger
    {
        public string Name;
        public Vector3 Position;
        public float Radius;
        public string PromptText = "按 [E] 触发";
        public bool TouchedTrigger;   // true=触碰即触发, false=按键触发
        public int Stage = -1;
        public Action<string, SfmExtTrigger> OnTrigger; // (uid, trigger)
        public Action<string, SfmExtTrigger> OnTouch;   // 进入范围时
        public Action<string, SfmExtTrigger> OnLeave;
        public bool Active = true;

        internal readonly HashSet<string> NearPlayers = new HashSet<string>();
    }

    public static class SfmExtTriggerManager
    {
        private static readonly Dictionary<string, SfmExtTrigger> _triggers = new Dictionary<string, SfmExtTrigger>();

        public static SfmExtTrigger Create(string name, Vector3 position, float radius,
            bool touched = false, string prompt = "按 [E] 触发")
        {
            var t = new SfmExtTrigger
            {
                Name = name, Position = position, Radius = radius,
                TouchedTrigger = touched, PromptText = prompt
            };
            _triggers[name] = t;
            return t;
        }

        public static SfmExtTrigger Get(string name) => _triggers.TryGetValue(name, out var t) ? t : null;
        public static bool Remove(string name) => _triggers.Remove(name);

        /// <summary>程序化触发一个触发点（等同玩家按下交互键）。</summary>
        public static void Fire(string name, string uid = "")
        {
            var t = Get(name);
            if (t == null) return;
            if (t.TouchedTrigger || t.NearPlayers.Count > 0 || uid.Length > 0)
                t.OnTrigger?.Invoke(uid.Length > 0 ? uid : "local", t);
            if (t.TouchedTrigger)
                SfmExtMsg.SendToRoom(new Dictionary<string, object>
                {
                    ["t"] = "ext_trigger", ["ns"] = SfmExtEventBus.Namespace,
                    ["name"] = name
                });
        }

        // 收到远端触发
        internal static void HandleRemoteTrigger(string name, string uid)
        {
            var t = Get(name);
            if (t == null) return;
            t.OnTrigger?.Invoke(uid, t);
        }

        [SfmExtFunction("trigger.create")]
        public static SfmExtValue FnCreate(SfmExtParams p, SfmExtValue unused)
        {
            var t = Create(p.Get("name").ToString(),
                new Vector3((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat()),
                (float)p.Get("radius").ToFloat(), p.Has("touched") && p.Get("touched").ToBool());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("trigger.fire")]
        public static SfmExtValue FnFire(SfmExtParams p, SfmExtValue unused)
        {
            Fire(p.Get("name").ToString(), p.Get("uid").ToString());
            return SfmExtValue.Null;
        }

        [SfmExtFunction("trigger.remove")]
        public static SfmExtValue FnRemove(SfmExtParams p, SfmExtValue unused)
            => new SfmExtValue(Remove(p.Get("name").ToString()));

        internal static void Check(Vector3 localPos, string localUid)
        {
            foreach (var t in _triggers.Values)
            {
                if (!t.Active) continue;
                bool near = Vector3.Distance(localPos, t.Position) <= t.Radius;
                if (near)
                {
                    bool isNew = t.NearPlayers.Add(localUid);
                    if (isNew)
                    {
                        t.OnTouch?.Invoke(localUid, t);
                        if (t.TouchedTrigger) Fire(t.Name, localUid);
                    }
                    if (t.TouchedTrigger) t.OnTrigger?.Invoke(localUid, t);
                }
                else if (t.NearPlayers.Remove(localUid))
                    t.OnLeave?.Invoke(localUid, t);
            }
        }

        /// <summary>由 OnlineCore 在玩家按 E 时调用（交互模式触发）。</summary>
        internal static void OnInteractKey()
        {
            foreach (var t in _triggers.Values)
            {
                if (!t.Active || t.TouchedTrigger) continue;
                if (t.NearPlayers.Contains("local"))
                    Fire(t.Name, "local");
            }
        }
    }
}
