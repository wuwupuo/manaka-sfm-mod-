using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  任务系统（模仿 V1 CustomMission / V2 MissionPanel 简化）：
    //  - 创建任务（标题/描述/RP奖励）
    //  - 检查点（Checkpoint）
    //  - 条件（Condition：进区域/收集物品/按键等）
    //  - 进度同步（可广播）
    // ====================================================================
    public class SfmExtTask
    {
        public string Name;
        public string Title;
        public string Description;
        public double RPReward;
        public bool Active;
        public bool Synced;
        public List<SfmExtCheckpoint> Checkpoints = new List<SfmExtCheckpoint>();
        public int CurrentCheckpoint;
        public Action<SfmExtTask> OnComplete;
        public Action<SfmExtTask, int> OnCheckpointChanged;

        public double Progress =>
            Checkpoints.Count == 0 ? 0 :
            Math.Min(1.0, (double)CurrentCheckpoint / Checkpoints.Count);
    }

    public class SfmExtCheckpoint
    {
        public string Name;
        public Vector3 Position;
        public float Radius = 3f;
        public SfmExtTask Task;
        public Action<SfmExtTask, SfmExtCheckpoint> OnReach;
    }

    public static class SfmExtTaskManager
    {
        private static readonly Dictionary<string, SfmExtTask> _tasks = new Dictionary<string, SfmExtTask>();

        public static SfmExtTask Create(string name, string title, string description = "",
            double rpReward = 0, bool synced = false)
        {
            var t = new SfmExtTask
            {
                Name = name, Title = title, Description = description,
                RPReward = rpReward, Active = false, Synced = synced
            };
            _tasks[name] = t;
            return t;
        }

        public static SfmExtTask Get(string name) => _tasks.TryGetValue(name, out var t) ? t : null;

        public static bool Remove(string name) => _tasks.Remove(name);

        public static SfmExtCheckpoint AddCheckpoint(SfmExtTask task, string name, Vector3 position, float radius = 3f)
        {
            var c = new SfmExtCheckpoint { Name = name, Position = position, Radius = radius, Task = task };
            task.Checkpoints.Add(c);
            return c;
        }

        public static void SetCheckpoint(SfmExtTask task, int index)
        {
            if (task == null || index < 0 || index >= task.Checkpoints.Count) return;
            task.CurrentCheckpoint = index;
            task.OnCheckpointChanged?.Invoke(task, index);
            if (task.Synced)
                SfmExtMsg.SendToRoom(new Dictionary<string, object>
                {
                    ["t"] = "ext_task", ["ns"] = SfmExtEventBus.Namespace,
                    ["op"] = "checkpoint", ["task"] = task.Name, ["idx"] = index
                });
            if (index >= task.Checkpoints.Count - 1) Complete(task);
        }

        public static void Complete(SfmExtTask task)
        {
            if (task == null || !task.Active) return;
            task.Active = false;
            if (task.RPReward > 0 && SfmExtScore.HasTable("RP"))
                SfmExtScore.Add("RP", task.RPReward, task.Synced);
            task.OnComplete?.Invoke(task);
            SfmExtEvent.Emit("task_completed", new SfmExtValue(task.Name));
            if (task.Synced)
                SfmExtMsg.SendToRoom(new Dictionary<string, object>
                {
                    ["t"] = "ext_task", ["ns"] = SfmExtEventBus.Namespace,
                    ["op"] = "complete", ["task"] = task.Name
                });
        }

        public static void Start(SfmExtTask task)
        {
            if (task == null) return;
            task.Active = true;
            task.CurrentCheckpoint = 0;
            if (task.Synced)
                SfmExtMsg.SendToRoom(new Dictionary<string, object>
                {
                    ["t"] = "ext_task", ["ns"] = SfmExtEventBus.Namespace,
                    ["op"] = "start", ["task"] = task.Name
                });
        }

        // ---------- 引擎函数 ----------
        [SfmExtFunction("task.create")]
        public static SfmExtValue FnCreate(SfmExtParams p, SfmExtValue unused)
        {
            var t = Create(p.Get("name").ToString(), p.Get("title").ToString(),
                p.Get("desc").ToString(), p.Get("rp").ToFloat(),
                p.Has("synced") && p.Get("synced").ToBool());
            return new SfmExtValue(t.Name);
        }

        [SfmExtFunction("task.start")]
        public static SfmExtValue FnStart(SfmExtParams p, SfmExtValue unused)
        {
            Start(Get(p.Get("name").ToString()));
            return SfmExtValue.Null;
        }

        [SfmExtFunction("task.complete")]
        public static SfmExtValue FnComplete(SfmExtParams p, SfmExtValue unused)
        {
            Complete(Get(p.Get("name").ToString()));
            return SfmExtValue.Null;
        }

        [SfmExtFunction("task.add_checkpoint")]
        public static SfmExtValue FnAddCheckpoint(SfmExtParams p, SfmExtValue unused)
        {
            var t = Get(p.Get("name").ToString());
            if (t == null) return new SfmExtValue(false);
            AddCheckpoint(t, p.Get("checkpoint").ToString(),
                new Vector3((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat()),
                (float)p.Get("radius").ToFloat());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("task.get_progress")]
        public static SfmExtValue FnGetProgress(SfmExtParams p, SfmExtValue unused)
        {
            var t = Get(p.Get("name").ToString());
            return new SfmExtValue(t != null ? t.Progress : 0);
        }

        // ---------- 每帧检测检查点 ----------
        internal static void Update()
        {
            foreach (var task in _tasks.Values)
            {
                if (!task.Active || task.CurrentCheckpoint >= task.Checkpoints.Count) continue;
                var cp = task.Checkpoints[task.CurrentCheckpoint];
                var pos = SfmExtPlayer.GetLocalPosition();
                if (Vector3.Distance(pos, cp.Position) <= cp.Radius)
                {
                    cp.OnReach?.Invoke(task, cp);
                    SetCheckpoint(task, task.CurrentCheckpoint + 1);
                }
            }
        }
    }

    // ====================================================================
    //  NPC 系统（模仿 V2 CreateNPC / NPCManager 简化 + 联机 npc_state）：
    //  放置可见 NPC（胶囊体/场景模型复制），支持巡逻/对话文本
    // ====================================================================
    public class SfmExtNpc
    {
        public string Name;
        public GameObject Root;
        public Vector3 Position;
        public string DialogueText;
        public float InteractRadius = 2f;
        public Action<SfmExtNpc, string> OnInteract; // (npc, uid)
        public string SourceModel;  // 模型来源（scene:npcId / capsule / asset:路径）
    }

    public static class SfmExtNpcManager
    {
        private static readonly Dictionary<string, SfmExtNpc> _npcs = new Dictionary<string, SfmExtNpc>();
        private static readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>生成默认胶囊体 NPC。</summary>
        public static SfmExtNpc Spawn(string name, Vector3 position, string dialogue = "", Color? color = null, float scale = 1.6f)
        {
            if (_npcs.ContainsKey(name)) return _npcs[name];
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "SFMExt_NPC_" + name;
            go.transform.position = position;
            go.transform.localScale = new Vector3(scale * 0.6f, scale, scale * 0.6f);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color ?? new Color(0.3f, 0.7f, 1f, 0.9f);
            _spawned.Add(go);
            var npc = new SfmExtNpc { Name = name, Root = go, Position = position, DialogueText = dialogue, SourceModel = "capsule" };
            _npcs[name] = npc;
            return npc;
        }

        /// <summary>从场景中已有的 NPC 复制模型生成新 NPC（建模选取）。
        /// npcId 可通过 ListSceneNpcs() 获取。</summary>
        public static SfmExtNpc SpawnFromScene(string name, int npcId, Vector3 position, string dialogue = "")
        {
            if (_npcs.ContainsKey(name)) return _npcs[name];
            GameObject src = null;
            try
            {
                var nm = ExposureUnnoticed2.Object3D.NPC.Script.NpcManager.Instance;
                if (nm != null && nm.ExistNpcList != null)
                {
                    for (int i = 0; i < nm.ExistNpcList.Count; i++)
                    {
                        var npc = nm.ExistNpcList[i];
                        if (npc == null || npc.NpcComponent == null) continue;
                        int id = 0;
                        try { id = npc.NpcComponent.id; } catch { }
                        if (id <= 0) id = 100000 + i;
                        if (id == npcId) { src = npc.NpcComponent.gameObject; break; }
                    }
                }
            }
            catch { }
            return SpawnFromObject(name, src, position, dialogue, "scene:" + npcId);
        }

        /// <summary>从任意 GameObject 复制模型生成 NPC。</summary>
        public static SfmExtNpc SpawnFromObject(string name, GameObject source, Vector3 position, string dialogue = "", string sourceDesc = "object")
        {
            if (_npcs.ContainsKey(name)) return _npcs[name];
            GameObject go;
            if (source != null)
            {
                go = UnityEngine.Object.Instantiate(source);
                go.name = "SFMExt_NPC_" + name;
                var col = go.GetComponentInChildren<Collider>();
                if (col != null) UnityEngine.Object.Destroy(col);
                go.transform.position = position;
                go.transform.localScale = Vector3.one;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "SFMExt_NPC_" + name;
                go.transform.position = position;
                go.transform.localScale = new Vector3(0.6f, 1.6f, 0.6f);
            }
            _spawned.Add(go);
            var npc = new SfmExtNpc { Name = name, Root = go, Position = position, DialogueText = dialogue, SourceModel = sourceDesc };
            _npcs[name] = npc;
            return npc;
        }

        /// <summary>列出当前场景所有 NPC（id 和名字），供建模选取。</summary>
        public static List<(int Id, string Name)> ListSceneNpcs()
        {
            var list = new List<(int, string)>();
            try
            {
                var nm = ExposureUnnoticed2.Object3D.NPC.Script.NpcManager.Instance;
                if (nm != null && nm.ExistNpcList != null)
                {
                    for (int i = 0; i < nm.ExistNpcList.Count; i++)
                    {
                        var npc = nm.ExistNpcList[i];
                        if (npc == null || npc.NpcComponent == null) continue;
                        int id = 0;
                        try { id = npc.NpcComponent.id; } catch { }
                        if (id <= 0) id = 100000 + i;
                        list.Add((id, npc.NpcComponent.gameObject.name));
                    }
                }
            }
            catch { }
            return list;
        }

        /// <summary>生成 NPC 并广播给房间所有玩家（所有人看到同一个 NPC，含模型来源）。</summary>
        public static SfmExtNpc SpawnSynced(string name, Vector3 position, string dialogue = "", string sourceModel = "capsule")
        {
            var npc = Spawn(name, position, dialogue);
            if (sourceModel != "capsule") npc.SourceModel = sourceModel;
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_npc", ["ns"] = SfmExtEventBus.Namespace,
                ["op"] = "spawn", ["name"] = name,
                ["x"] = position.x, ["y"] = position.y, ["z"] = position.z,
                ["dialogue"] = dialogue ?? "", ["model"] = npc.SourceModel ?? "capsule"
            });
            return npc;
        }

        public static SfmExtNpc Get(string name) => _npcs.TryGetValue(name, out var n) ? n : null;

        /// <summary>生成 NPC 并广播给房间所有玩家（所有人看到同一个 NPC）。</summary>
        public static SfmExtNpc SpawnSynced(string name, Vector3 position, string dialogue = "")
        {
            var npc = Spawn(name, position, dialogue);
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_npc", ["ns"] = SfmExtEventBus.Namespace,
                ["op"] = "spawn", ["name"] = name,
                ["x"] = position.x, ["y"] = position.y, ["z"] = position.z,
                ["dialogue"] = dialogue ?? ""
            });
            return npc;
        }

        /// <summary>广播 NPC 位置（移动同步）。</summary>
        public static void SyncNpcPosition(string name)
        {
            var npc = Get(name);
            if (npc == null || npc.Root == null) return;
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_npc", ["ns"] = SfmExtEventBus.Namespace,
                ["op"] = "move", ["name"] = name,
                ["x"] = npc.Root.transform.position.x,
                ["y"] = npc.Root.transform.position.y,
                ["z"] = npc.Root.transform.position.z
            });
        }

        /// <summary>移除 NPC 并广播。</summary>
        public static void RemoveSynced(string name)
        {
            Remove(name);
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_npc", ["ns"] = SfmExtEventBus.Namespace,
                ["op"] = "remove", ["name"] = name
            });
        }

        // 收到远端 NPC 操作
        internal static void HandleRemoteNpc(string op, string name, Dictionary<string, object> m)
        {
            double GetD(string k)
            {
                if (m != null && m.TryGetValue(k, out var v))
                {
                    try { return Convert.ToDouble(v); } catch { }
                }
                return 0;
            }
            switch (op)
            {
                case "spawn":
                    {
                        var model = SfmExtRoom.Str(m, "model");
                        if (model.StartsWith("scene:"))
                        {
                            int.TryParse(model.Substring(6), out var npcId);
                            SpawnFromScene(name, npcId, new Vector3((float)GetD("x"), (float)GetD("y"), (float)GetD("z")), SfmExtRoom.Str(m, "dialogue"));
                        }
                        else
                        {
                            Spawn(name, new Vector3((float)GetD("x"), (float)GetD("y"), (float)GetD("z")), SfmExtRoom.Str(m, "dialogue"));
                        }
                    }
                    break;
                case "move":
                    var npc = Get(name);
                    if (npc != null && npc.Root != null)
                        npc.Root.transform.position = new Vector3((float)GetD("x"), (float)GetD("y"), (float)GetD("z"));
                    break;
                case "remove":
                    Remove(name);
                    break;
            }
        }

        public static void Remove(string name)
        {
            if (_npcs.TryGetValue(name, out var n))
            {
                UnityEngine.Object.Destroy(n.Root);
                _npcs.Remove(name);
            }
        }

        public static void Clear()
        {
            foreach (var go in _spawned) UnityEngine.Object.Destroy(go);
            _spawned.Clear();
            _npcs.Clear();
        }

        [SfmExtFunction("npc.spawn")]
        public static SfmExtValue FnSpawn(SfmExtParams p, SfmExtValue unused)
        {
            var n = Spawn(p.Get("name").ToString(),
                new Vector3((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat()),
                p.Get("dialogue").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("npc.spawn_from_scene")]
        public static SfmExtValue FnSpawnFromScene(SfmExtParams p, SfmExtValue unused)
        {
            var n = SpawnFromScene(p.Get("name").ToString(), (int)p.Get("npc_id").ToFloat(),
                new Vector3((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat()),
                p.Get("dialogue").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("npc.list_scene")]
        public static SfmExtValue FnListScene(SfmExtParams p, SfmExtValue unused)
        {
            var list = ListSceneNpcs();
            var v = new SfmExtValue(SfmExtValue.Type.List);
            for (int i = 0; i < list.Count; i++)
            {
                var n = new SfmExtValue(SfmExtValue.Type.List);
                n["id"] = new SfmExtValue(list[i].Id);
                n["name"] = new SfmExtValue(list[i].Name);
                v[i.ToString()] = n;
            }
            return v;
        }

        [SfmExtFunction("npc.spawn_synced")]
        public static SfmExtValue FnSpawnSynced(SfmExtParams p, SfmExtValue unused)
        {
            var n = SpawnSynced(p.Get("name").ToString(),
                new Vector3((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat()),
                p.Get("dialogue").ToString(), p.Get("model", "capsule").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("npc.remove")]
        public static SfmExtValue FnRemove(SfmExtParams p, SfmExtValue unused)
        {
            Remove(p.Get("name").ToString());
            return SfmExtValue.Null;
        }

        [SfmExtFunction("npc.interact")]
        public static SfmExtValue FnInteract(SfmExtParams p, SfmExtValue unused)
        {
            var n = Get(p.Get("name").ToString());
            n?.OnInteract?.Invoke(n, p.Get("uid").ToString());
            return SfmExtValue.Null;
        }

        // 检测玩家靠近 NPC + 房主权威位置广播（每 0.5 秒）
        internal static void Update()
        {
            var pos = SfmExtPlayer.GetLocalPosition();
            foreach (var npc in _npcs.Values)
            {
                if (npc.Root == null) continue;
                npc.Position = npc.Root.transform.position;
            }
            // 房主权威：只由房主广播 Ext NPC 位置（避免多人互相覆盖）
            if (!SfmExtBridge.BridgeReady || _syncNpcNextAt > UnityEngine.Time.unscaledTime) return;
            _syncNpcNextAt = UnityEngine.Time.unscaledTime + 0.5f;
            if (!SfmExtRoom.Host) return;
            foreach (var npc in _npcs.Values)
            {
                if (npc.Root == null) continue;
                SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
                {
                    ["t"] = "ext_npc", ["ns"] = SfmExtEventBus.Namespace,
                    ["op"] = "move", ["name"] = npc.Name,
                    ["x"] = npc.Root.transform.position.x,
                    ["y"] = npc.Root.transform.position.y,
                    ["z"] = npc.Root.transform.position.z
                });
            }
        }

        private static float _syncNpcNextAt;
    }

    // ====================================================================
    //  HUD 系统（模仿 V2 PluginGUI 的简易版）：
    //  - 创建屏幕文字标签（跟随/固定）
    //  - 创建简易窗口（列表/按钮）
    //  - 创建图片
    //  OnGUI 由 OnlineCore 桥接调用
    // ====================================================================
    public static class SfmExtHud
    {
        public sealed class HudText
        {
            public string Name;
            public string Text;
            public Vector2 Position;      // 屏幕坐标 (0-1)
            public Color Color = Color.white;
            public int Size = 18;
            public bool Visible = true;
            public bool WorldSpace;       // true=跟随世界坐标
            public Vector3 WorldPos;
            public float WorldRadius = 3f;
        }

        public sealed class HudWindow
        {
            public string Name;
            public string Title;
            public Rect Rect = new Rect(100, 100, 320, 240);
            public bool Visible;
            public List<HudButton> Buttons = new List<HudButton>();
            public string Content = "";
        }

        public sealed class HudButton
        {
            public string Text;
            public Action OnClick;
        }

        public sealed class HudBar
        {
            public string Name;
            public Vector2 Position;      // 屏幕坐标 (0-1)
            public Vector2 Size = new Vector2(200, 16);
            public float Value;           // 0-1
            public Color FillColor = Color.green;
            public Color BackColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            public string Label = "";
            public bool Visible = true;
        }

        public sealed class HudMarker
        {
            public string Name;
            public Vector3 WorldPos;
            public string Text = "";
            public Color Color = Color.yellow;
            public float ScreenRadius = 40f;
            public bool Visible = true;
        }

        public sealed class HudInput
        {
            public string Name;
            public Rect Rect = new Rect(100, 100, 220, 28);
            public string Text = "";
            public string Placeholder = "输入...";
            public bool Password;
            public int MaxLength = 64;
            public bool Visible = true;
            public bool Focused;
            public Action<string> OnSubmit;    // 回车确认
            public Action<string> OnChanged;   // 内容变化
            public bool SubmitOnEnter = true;
        }

        public sealed class HudImage
        {
            public string Name;
            public Rect Rect = new Rect(100, 100, 200, 100);   // 屏幕像素
            public Texture2D Texture;
            public string FilePath = "";
            public bool Visible = true;
            public bool Movable = false;   // 可拖动
            private Vector2 _dragOffset;

            public void UpdateDrag()
            {
                if (!Movable || Event.current == null) return;
                if (Event.current.type == EventType.MouseDown && Rect.Contains(Event.current.mousePosition))
                {
                    _dragOffset = Event.current.mousePosition - new Vector2(Rect.x, Rect.y);
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseDrag && _dragOffset != Vector2.zero)
                {
                    Rect.x = Event.current.mousePosition.x - _dragOffset.x;
                    Rect.y = Event.current.mousePosition.y - _dragOffset.y;
                }
                else if (Event.current.type == EventType.MouseUp)
                {
                    _dragOffset = Vector2.zero;
                }
            }
        }

        private static readonly Dictionary<string, HudImage> _images = new Dictionary<string, HudImage>();

        private static readonly Dictionary<string, HudBar> _bars = new Dictionary<string, HudBar>();
        private static readonly Dictionary<string, HudMarker> _markers = new Dictionary<string, HudMarker>();
        private static readonly List<(string, Color, float)> _toasts = new List<(string, Color, float)>();

        private static readonly Dictionary<string, HudText> _texts = new Dictionary<string, HudText>();
        private static readonly Dictionary<string, HudWindow> _windows = new Dictionary<string, HudWindow>();
        private static readonly GUIStyle _textStyle = new GUIStyle();
        private static readonly GUIStyle _windowStyle = new GUIStyle();
        private static readonly GUIStyle _buttonStyle = new GUIStyle();
        private static bool _stylesInit;
        private static string _dragWindow;
        private static Vector2 _dragOffset;

        public static HudText CreateText(string name, string text, Vector2 position, Color? color = null, int size = 18)
        {
            var t = new HudText
            {
                Name = name, Text = text, Position = position,
                Color = color ?? Color.white, Size = size
            };
            _texts[name] = t;
            return t;
        }

        public static HudWindow CreateWindow(string name, string title, Rect? rect = null)
        {
            var w = new HudWindow { Name = name, Title = title };
            if (rect.HasValue) w.Rect = rect.Value;
            _windows[name] = w;
            return w;
        }

        public static HudText GetText(string name) => _texts.TryGetValue(name, out var t) ? t : null;
        public static HudWindow GetWindow(string name) => _windows.TryGetValue(name, out var w) ? w : null;

        public static void RemoveText(string name) => _texts.Remove(name);
        public static void RemoveWindow(string name) => _windows.Remove(name);

        [SfmExtFunction("hud.text")]
        public static SfmExtValue FnText(SfmExtParams p, SfmExtValue unused)
        {
            var t = CreateText(p.Get("name").ToString(), p.Get("text").ToString(),
                new Vector2((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat()));
            if (p.Has("size")) t.Size = (int)p.Get("size").ToFloat();
            return new SfmExtValue(true);
        }

        [SfmExtFunction("hud.text_set")]
        public static SfmExtValue FnTextSet(SfmExtParams p, SfmExtValue unused)
        {
            var t = GetText(p.Get("name").ToString());
            if (t == null) return new SfmExtValue(false);
            t.Text = p.Get("text").ToString();
            return new SfmExtValue(true);
        }

        [SfmExtFunction("hud.window")]
        public static SfmExtValue FnWindow(SfmExtParams p, SfmExtValue unused)
        {
            var w = CreateWindow(p.Get("name").ToString(), p.Get("title").ToString());
            w.Visible = p.Has("show") && p.Get("show").ToBool();
            return new SfmExtValue(true);
        }

        [SfmExtFunction("hud.window_show")]
        public static SfmExtValue FnWindowShow(SfmExtParams p, SfmExtValue unused)
        {
            var w = GetWindow(p.Get("name").ToString());
            if (w == null) return new SfmExtValue(false);
            w.Visible = p.Get("show").ToBool();
            return new SfmExtValue(true);
        }

        [SfmExtFunction("hud.window_add_button")]
        public static SfmExtValue FnWindowAddButton(SfmExtParams p, SfmExtValue unused)
        {
            var w = GetWindow(p.Get("name").ToString());
            if (w == null) return new SfmExtValue(false);
            w.Buttons.Add(new HudButton { Text = p.Get("text").ToString() });
            return new SfmExtValue(true);
        }

        // ---------- 进度条 ----------
        public static HudBar CreateBar(string name, Vector2 position, float value = 0f, string label = "")
        {
            var b = new HudBar { Name = name, Position = position, Value = Mathf.Clamp01(value), Label = label };
            _bars[name] = b;
            return b;
        }

        public static HudBar GetBar(string name) => _bars.TryGetValue(name, out var b) ? b : null;
        public static void RemoveBar(string name) => _bars.Remove(name);

        public static void SetBarValue(string name, float value, string label = null)
        {
            var b = GetBar(name);
            if (b == null) return;
            b.Value = Mathf.Clamp01(value);
            if (label != null) b.Label = label;
        }

        [SfmExtFunction("hud.bar")]
        public static SfmExtValue FnBar(SfmExtParams p, SfmExtValue unused)
        {
            var b = CreateBar(p.Get("name").ToString(),
                new Vector2((float)p.Get("x", "0.5").ToFloat(), (float)p.Get("y", "0.2").ToFloat()),
                (float)p.Get("value", "0").ToFloat(), p.Get("label").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("hud.bar_set")]
        public static SfmExtValue FnBarSet(SfmExtParams p, SfmExtValue unused)
        {
            SetBarValue(p.Get("name").ToString(), (float)p.Get("value").ToFloat(), p.Get("label").ToString());
            return SfmExtValue.Null;
        }

        // ---------- 世界标记 ----------
        public static HudMarker CreateMarker(string name, Vector3 worldPos, string text = "")
        {
            var m = new HudMarker { Name = name, WorldPos = worldPos, Text = text };
            _markers[name] = m;
            return m;
        }

        public static HudMarker GetMarker(string name) => _markers.TryGetValue(name, out var m) ? m : null;
        public static void RemoveMarker(string name) => _markers.Remove(name);

        // ---------- 图片 ----------
        /// <summary>创建图片控件（FilePath 指向游戏目录下的 PNG/JPG，加载后显示）。</summary>
        public static HudImage CreateImage(string name, Rect rect, string filePath = "")
        {
            var img = new HudImage { Name = name, Rect = rect, FilePath = filePath };
            if (filePath.Length > 0) LoadImageTexture(img, filePath);
            _images[name] = img;
            return img;
        }

        /// <summary>从文件加载 PNG/JPG 到图片控件（路径相对游戏根目录或以 ~ 开头）。</summary>
        public static bool LoadImageTexture(HudImage img, string filePath)
        {
            try
            {
                string full = filePath;
                if (full.StartsWith("~")) full = full.Replace("~", BepInEx.Paths.GameRootPath);
                else if (!System.IO.Path.IsPathRooted(full)) full = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, full);
                if (!System.IO.File.Exists(full)) return false;
                var bytes = System.IO.File.ReadAllBytes(full);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (UnityEngine.ImageConversion.LoadImage(tex, bytes))
                {
                    img.Texture = tex;
                    img.FilePath = filePath;
                    return true;
                }
                UnityEngine.Object.Destroy(tex);
                return false;
            }
            catch { return false; }
        }

        public static HudImage GetImage(string name) => _images.TryGetValue(name, out var i) ? i : null;
        public static void RemoveImage(string name) => _images.Remove(name);

        [SfmExtFunction("hud.image")]
        public static SfmExtValue FnImage(SfmExtParams p, SfmExtValue unused)
        {
            var img = CreateImage(p.Get("name").ToString(),
                new Rect((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(),
                         (float)p.Get("w", "200").ToFloat(), (float)p.Get("h", "100").ToFloat()),
                p.Get("path").ToString());
            return new SfmExtValue(img.Texture != null);
        }

        // ---------- 输入框 ----------
        private static readonly Dictionary<string, HudInput> _inputs = new Dictionary<string, HudInput>();
        private static string _inputFocusName = "";

        public static HudInput CreateInput(string name, Rect rect, string placeholder = "输入...", bool password = false)
        {
            var inp = new HudInput { Name = name, Rect = rect, Placeholder = placeholder, Password = password };
            _inputs[name] = inp;
            return inp;
        }

        public static HudInput GetInput(string name) => _inputs.TryGetValue(name, out var i) ? i : null;
        public static void RemoveInput(string name) => _inputs.Remove(name);

        // ---------- 统一控件控制（显隐/查找） ----------
        /// <summary>按名称获取任意 HUD 控件（返回带 Visible 属性的对象，无则 null）。</summary>
        public static object GetCtrl(string name)
        {
            if (_texts.TryGetValue(name, out var t)) return t;
            if (_windows.TryGetValue(name, out var w)) return w;
            if (_images.TryGetValue(name, out var i)) return i;
            if (_bars.TryGetValue(name, out var b)) return b;
            if (_markers.TryGetValue(name, out var m)) return m;
            if (_inputs.TryGetValue(name, out var inp)) return inp;
            return null;
        }

        /// <summary>设置任意控件显隐（统一入口，供事件/条件/代码调用）。</summary>
        public static void SetCtrlVisible(string name, bool visible)
        {
            if (_texts.TryGetValue(name, out var t)) { t.Visible = visible; return; }
            if (_windows.TryGetValue(name, out var w)) { w.Visible = visible; return; }
            if (_images.TryGetValue(name, out var i)) { i.Visible = visible; return; }
            if (_bars.TryGetValue(name, out var b)) { b.Visible = visible; return; }
            if (_markers.TryGetValue(name, out var m)) { m.Visible = visible; return; }
            if (_inputs.TryGetValue(name, out var inp)) { inp.Visible = visible; return; }
        }

        public static void ShowCtrl(string name) => SetCtrlVisible(name, true);
        public static void HideCtrl(string name) => SetCtrlVisible(name, false);

        // 按键切换显隐绑定（Update 里检测）
        private static readonly Dictionary<KeyCode, string> _toggleKeys = new Dictionary<KeyCode, string>();

        /// <summary>绑定按键切换控件显隐（如 BindToggleKey("menu", KeyCode.F5)）。</summary>
        public static void BindToggleKey(string ctrlName, KeyCode key)
        {
            _toggleKeys[key] = ctrlName;
        }

        // 每帧检测切换按键
        [SfmExtUpdate]
        public static void UpdateToggleKeys()
        {
            if (_toggleKeys.Count == 0) return;
            try
            {
                foreach (var kv in _toggleKeys)
                {
                    if (UnityEngine.Input.GetKeyDown(kv.Key))
                    {
                        var name = kv.Value;
                        var c = GetCtrl(name);
                        if (c == null) continue;
                        bool vis = c is HudText t0 ? t0.Visible : c is HudWindow w0 ? w0.Visible : c is HudImage i0 ? i0.Visible : c is HudBar b0 ? b0.Visible : c is HudMarker m0 ? m0.Visible : c is HudInput inp0 ? inp0.Visible : false;
                        SetCtrlVisible(name, !vis);
                    }
                }
            }
            catch { }
        }

        public static string GetInputValue(string name)
        {
            var inp = GetInput(name);
            return inp != null ? inp.Text : "";
        }

        public static void SetInputValue(string name, string text)
        {
            var inp = GetInput(name);
            if (inp != null) inp.Text = text ?? "";
        }

        public static void FocusInput(string name) => _inputFocusName = name;

        /// <summary>提交输入框内容（触发 OnSubmit + input_submitted 事件）。</summary>
        public static void SubmitInput(HudInput inp)
        {
            if (inp == null) return;
            try { inp.OnSubmit?.Invoke(inp.Text); } catch { }
            SfmExtEvent.Emit("input_submitted", new SfmExtValue(SfmExtValue.Type.List)
            {
                ["name"] = new SfmExtValue(inp.Name),
                ["value"] = new SfmExtValue(inp.Text)
            });
        }

        [SfmExtFunction("hud.input")]
        public static SfmExtValue FnInput(SfmExtParams p, SfmExtValue unused)
        {
            var inp = CreateInput(p.Get("name").ToString(),
                new Rect((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(),
                         (float)p.Get("w", "220").ToFloat(), (float)p.Get("h", "28").ToFloat()),
                p.Get("placeholder", "输入...").ToString(), p.Get("password").ToBool());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("hud.input_set")]
        public static SfmExtValue FnInputSet(SfmExtParams p, SfmExtValue unused)
        {
            SetInputValue(p.Get("name").ToString(), p.Get("value").ToString());
            return SfmExtValue.Null;
        }

        // 返回值：获取输入框当前内容
        [SfmExtFunction("hud.input_get")]
        public static SfmExtValue FnInputGet(SfmExtParams p, SfmExtValue unused)
            => new SfmExtValue(GetInputValue(p.Get("name").ToString()));

        [SfmExtFunction("hud.input_focus")]
        public static SfmExtValue FnInputFocus(SfmExtParams p, SfmExtValue unused)
        {
            FocusInput(p.Get("name").ToString());
            return SfmExtValue.Null;
        }

        [SfmExtFunction("hud.marker")]
        public static SfmExtValue FnMarker(SfmExtParams p, SfmExtValue unused)
        {
            CreateMarker(p.Get("name").ToString(),
                new Vector3((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat()),
                p.Get("text").ToString());
            return new SfmExtValue(true);
        }

        // ---------- 通知 ----------
        public static void Toast(string text, float seconds = 4f, Color? color = null)
        {
            _toasts.Add((text, color ?? Color.white, Time.unscaledTime + seconds));
            while (_toasts.Count > 8) _toasts.RemoveAt(0);
        }

        [SfmExtFunction("hud.toast")]
        public static SfmExtValue FnToast(SfmExtParams p, SfmExtValue unused)
        {
            Toast(p.Get("text").ToString(), (float)p.Get("seconds", "4").ToFloat());
            return SfmExtValue.Null;
        }

        // ---------- OnGUI 绘制（由 OnlineCore 桥接） ----------
        internal static void Draw()
        {
            if (!_stylesInit) InitStyles();
            foreach (var t in _texts.Values)
            {
                if (!t.Visible) continue;
                Vector2 pos = t.Position;
                if (t.WorldSpace)
                {
                    var camA = Camera.main;
                    if (camA == null) continue;
                    var sp = camA.WorldToScreenPoint(t.WorldPos);
                    if (sp.z < 0) continue;
                    pos = new Vector2(sp.x / Screen.width, 1f - sp.y / Screen.height);
                }
                _textStyle.normal.textColor = t.Color;
                _textStyle.fontSize = t.Size;
                GUI.Label(new Rect(pos.x * Screen.width, pos.y * Screen.height, 800, 60), t.Text, _textStyle);
            }
            foreach (var w in _windows.Values)
            {
                if (!w.Visible) continue;
                var ww = w;
                // 标题栏（可拖动）
                GUI.Box(new Rect(ww.Rect.x, ww.Rect.y, ww.Rect.width, 24), ww.Title, _windowStyle);
                // 拖动逻辑：鼠标按住标题栏时移动窗口
                if (Event.current != null && Event.current.type == EventType.MouseDown &&
                    new Rect(ww.Rect.x, ww.Rect.y, ww.Rect.width, 24).Contains(Event.current.mousePosition))
                {
                    _dragWindow = ww.Name;
                    _dragOffset = Event.current.mousePosition - new Vector2(ww.Rect.x, ww.Rect.y);
                    Event.current.Use();
                }
                if (_dragWindow == ww.Name && Event.current != null && Event.current.type == EventType.MouseDrag)
                {
                    ww.Rect.x = Event.current.mousePosition.x - _dragOffset.x;
                    ww.Rect.y = Event.current.mousePosition.y - _dragOffset.y;
                }
                if (Event.current != null && Event.current.type == EventType.MouseUp && _dragWindow == ww.Name)
                {
                    _dragWindow = null;
                }
                // 内容区
                GUI.Box(new Rect(ww.Rect.x, ww.Rect.y + 24, ww.Rect.width, ww.Rect.height - 24), "", _windowStyle);
                GUI.Label(new Rect(ww.Rect.x + 10, ww.Rect.y + 32, ww.Rect.width - 20, 60), ww.Content);
                float by = ww.Rect.y + 56;
                foreach (var b in ww.Buttons)
                {
                    if (GUI.Button(new Rect(ww.Rect.x + 10, by, ww.Rect.width - 20, 28), b.Text))
                        b.OnClick?.Invoke();
                    by += 34;
                }
                // 关闭按钮
                if (GUI.Button(new Rect(ww.Rect.x + ww.Rect.width - 26, ww.Rect.y + 2, 22, 20), "X"))
                    ww.Visible = false;
            }
            // 图片
            foreach (var im in _images.Values)
            {
                if (!im.Visible || im.Texture == null) continue;
                im.UpdateDrag();
                GUI.DrawTexture(im.Rect, im.Texture);
            }
            // 输入框
            foreach (var inp in _inputs.Values)
            {
                if (!inp.Visible) continue;
                string controlName = "SFMExtInput_" + inp.Name;
                GUI.SetNextControlName(controlName);
                string shown = inp.Password ? new string('*', inp.Text.Length) : inp.Text;
                string result;
                if (inp.Password)
                    result = GUI.PasswordField(inp.Rect, shown, '*', 40);
                else
                    result = GUI.TextField(inp.Rect, shown, inp.MaxLength);
                // 占位符（空内容时显示灰色提示）
                if (result.Length == 0 && GUI.GetNameOfFocusedControl() != controlName)
                {
                    _textStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
                    _textStyle.fontSize = 14;
                    GUI.Label(inp.Rect, inp.Placeholder, _textStyle);
                }
                // 焦点管理
                bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
                if (_inputFocusName == inp.Name && !isFocused)
                {
                    GUI.FocusControl(controlName);
                    _inputFocusName = "";
                }
                if (isFocused)
                {
                    // 回车提交
                    if (inp.SubmitOnEnter && Event.current != null && Event.current.type == EventType.KeyDown &&
                        Event.current.keyCode == KeyCode.Return)
                    {
                        Event.current.Use();
                        GUI.FocusControl("");
                        SubmitInput(inp);
                    }
                }
                if (result != shown)
                {
                    inp.Text = result;
                    try { inp.OnChanged?.Invoke(inp.Text); } catch { }
                    SfmExtEvent.Emit("input_changed", new SfmExtValue(SfmExtValue.Type.List)
                    {
                        ["name"] = new SfmExtValue(inp.Name),
                        ["value"] = new SfmExtValue(inp.Text)
                    });
                }
            }
            // 进度条
            foreach (var b in _bars.Values)
            {
                if (!b.Visible) continue;
                var pos = new Vector2(b.Position.x * Screen.width, b.Position.y * Screen.height);
                var back = new Rect(pos.x, pos.y, b.Size.x, b.Size.y);
                GUI.DrawTexture(back, MakeTex(2, 2, b.BackColor));
                var fill = new Rect(pos.x, pos.y, b.Size.x * b.Value, b.Size.y);
                GUI.DrawTexture(fill, MakeTex(2, 2, b.FillColor));
                if (b.Label.Length > 0)
                    GUI.Label(new Rect(pos.x, pos.y - 18, b.Size.x, 18), b.Label);
            }
            // 世界标记（屏幕投影）
            var cam = Camera.main;
            if (cam != null)
            {
                foreach (var m in _markers.Values)
                {
                    if (!m.Visible) continue;
                    var sp = cam.WorldToScreenPoint(m.WorldPos);
                    if (sp.z < 0) continue;
                    var screen = new Vector2(sp.x / Screen.width, 1f - sp.y / Screen.height);
                    var r = new Rect(screen.x * Screen.width - m.ScreenRadius, screen.y * Screen.height - m.ScreenRadius, m.ScreenRadius * 2, m.ScreenRadius * 2);
                    GUI.DrawTexture(r, MakeTex(2, 2, m.Color));
                    if (m.Text.Length > 0)
                        GUI.Label(new Rect(r.x - 40, r.y - 20, r.width + 80, 18), m.Text);
                }
            }
            // 通知（右下角堆叠）
            float now2 = Time.unscaledTime;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                if (now2 > t.Item3) { _toasts.RemoveAt(i); continue; }
                var r2 = new Rect(Screen.width - 320, Screen.height - 40 - (_toasts.Count - i) * 34, 310, 28);
                GUI.Box(r2, "", _windowStyle);
                _textStyle.normal.textColor = t.Item2;
                _textStyle.fontSize = 15;
                GUI.Label(r2, t.Item1, _textStyle);
            }
        }

        private static void InitStyles()
        {
            _stylesInit = true;
            _textStyle.alignment = TextAnchor.UpperLeft;
            _windowStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.12f, 0.95f));
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var t = new Texture2D(w, h);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = col;
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // ====================================================================
        //  控件显隐控制系统：
        //  - 按键开关（按某键显示/隐藏）
        //  - 事件控制（收到某事件显示/隐藏）
        //  - 条件控制（满足条件才显示）
        //  - 代码控制（Show/Hide/Toggle API）
        //  适用于所有 HUD 控件（文本/窗口/图片/进度条/标记/输入框）
        // ====================================================================
        private sealed class VisibilityState
        {
            public KeyCode ToggleKey = KeyCode.None;
            public string ShowEvent = "";
            public string HideEvent = "";
            public Func<bool> Condition;
            public bool? Override;   // true=强制显示 false=强制隐藏 null=自动
            public bool KeyPrevDown;
        }

        private static readonly Dictionary<string, VisibilityState> _visStates
            = new Dictionary<string, VisibilityState>();

        /// <summary>绑定按键开关：按一次显示，再按隐藏。</summary>
        public static void BindVisibilityKey(string name, KeyCode key)
        {
            GetVis(name).ToggleKey = key;
        }

        /// <summary>绑定事件控制：收到 showEvent 显示，hideEvent 隐藏。</summary>
        public static void BindVisibilityEvent(string name, string showEvent, string hideEvent = "")
        {
            var s = GetVis(name);
            s.ShowEvent = showEvent ?? "";
            s.HideEvent = hideEvent ?? "";
        }

        /// <summary>绑定条件：满足条件才显示（每帧检查）。</summary>
        public static void BindVisibilityCondition(string name, Func<bool> condition)
        {
            GetVis(name).Condition = condition;
        }

        /// <summary>代码控制：强制显示。</summary>
        public static void ShowControl(string name)
        {
            GetVis(name).Override = true;
            SetControlVisible(name, true);
        }

        /// <summary>代码控制：强制隐藏。</summary>
        public static void HideControl(string name)
        {
            GetVis(name).Override = false;
            SetControlVisible(name, false);
        }

        /// <summary>代码控制：切换显示状态。</summary>
        public static void ToggleControl(string name)
        {
            var s = GetVis(name);
            s.Override = !IsControlVisible(name);
            SetControlVisible(name, s.Override.Value);
        }

        /// <summary>解除所有控制（恢复默认可见）。</summary>
        public static void ClearVisibilityControl(string name)
        {
            if (_visStates.Remove(name)) SetControlVisible(name, true);
        }

        private static VisibilityState GetVis(string name)
        {
            if (!_visStates.TryGetValue(name, out var s))
            {
                s = new VisibilityState();
                _visStates[name] = s;
            }
            return s;
        }

        private static bool IsControlVisible(string name)
        {
            if (_texts.TryGetValue(name, out var t)) return t.Visible;
            if (_windows.TryGetValue(name, out var w)) return w.Visible;
            if (_images.TryGetValue(name, out var i)) return i.Visible;
            if (_bars.TryGetValue(name, out var b)) return b.Visible;
            if (_markers.TryGetValue(name, out var m)) return m.Visible;
            if (_inputs.TryGetValue(name, out var ip)) return ip.Visible;
            return false;
        }

        private static void SetControlVisible(string name, bool visible)
        {
            if (_texts.TryGetValue(name, out var t)) t.Visible = visible;
            if (_windows.TryGetValue(name, out var w)) w.Visible = visible;
            if (_images.TryGetValue(name, out var i)) i.Visible = visible;
            if (_bars.TryGetValue(name, out var b)) b.Visible = visible;
            if (_markers.TryGetValue(name, out var m)) m.Visible = visible;
            if (_inputs.TryGetValue(name, out var ip)) ip.Visible = visible;
        }

        [SfmExtUpdate]
        public static void UpdateVisibility()
        {
            foreach (var kv in _visStates.ToArray())
            {
                var name = kv.Key;
                var s = kv.Value;
                // 1) 代码强制
                if (s.Override.HasValue)
                {
                    SetControlVisible(name, s.Override.Value);
                    continue;
                }
                // 2) 按键切换（边沿检测）
                if (s.ToggleKey != KeyCode.None)
                {
                    bool down = false;
                    try { down = UnityEngine.Input.GetKeyDown(s.ToggleKey); } catch { }
                    if (down)
                    {
                        SetControlVisible(name, !IsControlVisible(name));
                    }
                }
                // 3) 条件控制（优先级低于按键）
                if (s.Condition != null)
                {
                    bool ok = false;
                    try { ok = s.Condition(); } catch { }
                    if (IsControlVisible(name) != ok) SetControlVisible(name, ok);
                }
            }
        }

        // ---------- 显隐引擎函数 ----------
        [SfmExtFunction("hud.show")]
        public static SfmExtValue FnShow(SfmExtParams p, SfmExtValue u)
        {
            ShowControl(p.Get("name").ToString());
            return SfmExtValue.Null;
        }

        [SfmExtFunction("hud.hide")]
        public static SfmExtValue FnHide(SfmExtParams p, SfmExtValue u)
        {
            HideControl(p.Get("name").ToString());
            return SfmExtValue.Null;
        }

        [SfmExtFunction("hud.toggle")]
        public static SfmExtValue FnToggle(SfmExtParams p, SfmExtValue u)
        {
            ToggleControl(p.Get("name").ToString());
            return SfmExtValue.Null;
        }

        [SfmExtFunction("hud.bind_key")]
        public static SfmExtValue FnBindKey(SfmExtParams p, SfmExtValue u)
        {
            KeyCode key;
            if (Enum.TryParse(p.Get("key").ToString(), true, out key))
                BindVisibilityKey(p.Get("name").ToString(), key);
            return new SfmExtValue(true);
        }

        [SfmExtFunction("hud.bind_event")]
        public static SfmExtValue FnBindEvent(SfmExtParams p, SfmExtValue u)
        {
            BindVisibilityEvent(p.Get("name").ToString(), p.Get("show").ToString(), p.Get("hide").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("hud.bind_condition")]
        public static SfmExtValue FnBindCondition(SfmExtParams p, SfmExtValue u)
        {
            // 条件：score 比较，如 score.金币 > 5
            var expr = p.Get("expr").ToString();
            BindVisibilityCondition(p.Get("name").ToString(), () => EvalConditionExpr(expr));
            return new SfmExtValue(true);
        }

        private static bool EvalConditionExpr(string expr)
        {
            try
            {
                // 支持: score.表名 > 数值 / net.players >= 数值 / bool 表达式
                expr = expr.Trim();
                var parts = expr.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3) return false;
                double left = 0;
                if (parts[0].StartsWith("score."))
                {
                    left = SfmExtScore.Get(parts[0].Substring(6));
                }
                else if (parts[0].StartsWith("net.") && parts[0] == "net.players")
                {
                    left = (SfmExtBridge.GetGhostUids?.Invoke() ?? new List<string>()).Count;
                }
                else if (parts[0] == "stage")
                {
                    left = SfmExtBridge.GetStage != null ? SfmExtBridge.GetStage() : -1;
                }
                else
                {
                    double.TryParse(parts[0], out left);
                }
                double right;
                double.TryParse(parts[2], out right);
                switch (parts[1])
                {
                    case ">": return left > right;
                    case ">=": return left >= right;
                    case "<": return left < right;
                    case "<=": return left <= right;
                    case "==": return Math.Abs(left - right) < 0.0001;
                    case "!=": return Math.Abs(left - right) >= 0.0001;
                }
            }
            catch { }
            return false;
        }

        // 事件驱动显隐（由 OnlineCoreExt 收到事件时调用）
        internal static void HandleVisibilityEvent(string evt)
        {
            foreach (var kv in _visStates)
            {
                var s = kv.Value;
                if (s.ShowEvent.Length > 0 && s.ShowEvent == evt) SetControlVisible(kv.Key, true);
                if (s.HideEvent.Length > 0 && s.HideEvent == evt) SetControlVisible(kv.Key, false);
            }
        }
    }

    // ====================================================================
    //  OnlineCore 桥接类：供 OnlineCore 调用（内部），暴露给 Ext 命名空间
    // ====================================================================
    public static class OnlineCoreExt
    {
        // 由 OnlineCore.Awake 初始化：获取所有远端玩家 uid
        public static Func<List<string>> GetGhostUids;

        // 由 OnlineCore 每帧调用
        public static void TickUpdate()
        {
            SfmExt.EnsureInit();
            SfmExt.OnUpdate();
        }

        public static void TickLateUpdate() => SfmExt.OnLateUpdate();
        public static void TickGui() => SfmExt.OnGUI();

        // 由 OnlineCore 收到 ext_ 消息时调用
        public static void HandleExtMessage(string t, Dictionary<string, object> msg, string fromUid)
        {
            switch (t)
            {
                case "ext_evt":
                    if (msg.TryGetValue("evt", out var evt) && evt is string evName)
                    {
                        var data = msg.TryGetValue("data", out var d) ? d : null;
                        SfmExtEvent.Emit(evName, SfmExtEvent.FromJsonObject(data));
                        SfmExtHud.HandleVisibilityEvent(evName);
                    }
                    break;
                case "ext_score":
                    if (msg.TryGetValue("name", out var n) && n is string name &&
                        msg.TryGetValue("value", out var v))
                    {
                        double val = v is double dv ? dv : Convert.ToDouble(v);
                        SfmExtScore.HandleRemoteScore(name, val);
                    }
                    break;
                case "ext_bone":
                    SfmExtEvent.Emit("net_bone", SfmExtEvent.FromJsonObject(msg));
                    break;
                case "ext_trigger":
                    if (msg.TryGetValue("name", out var tn) && tn is string tName)
                        SfmExtTriggerManager.HandleRemoteTrigger(tName, fromUid);
                    break;
                case "ext_tp":
                {
                    // 传送命令（带坐标）：to 为空=全员
                    string target = SfmExtRoom.Str(msg, "to");
                    var core = OnlineCore.Instance;
                    if (core == null) break;
                    if (target.Length > 0 && target != core.PeerId && target != core.ToySelfIdPublic()) break;
                    var vx = (float)(msg.TryGetValue("x", out var xv) ? Convert.ToDouble(xv) : 0);
                    var vy = (float)(msg.TryGetValue("y", out var yv) ? Convert.ToDouble(yv) : 0);
                    var vz = (float)(msg.TryGetValue("z", out var zv) ? Convert.ToDouble(zv) : 0);
                    core.SetPlayerPosition(new UnityEngine.Vector3(vx, vy, vz));
                    break;
                }
                case "ext_play":
                {
                    // 玩法命令：to 为空=全员执行；否则仅目标玩家执行
                    string target = SfmExtRoom.Str(msg, "to");
                    string d = SfmExtRoom.Str(msg, "d");
                    if (d.Length == 0) break;
                    var core = OnlineCore.Instance;
                    if (core == null) break;
                    if (target.Length > 0 && target != core.PeerId && target != core.ToySelfIdPublic()) break;
                    core.ExtPlayLocal(d,
                        (int)(msg.TryGetValue("act", out var av) ? Convert.ToDouble(av) : 0),
                        (int)(msg.TryGetValue("stage", out var stv) ? Convert.ToDouble(stv) : 0),
                        (int)(msg.TryGetValue("mode", out var mv) ? Convert.ToDouble(mv) : 0),
                        msg.TryGetValue("on", out var ov) && (ov is bool ob ? ob : Convert.ToDouble(ov) != 0));
                    break;
                }
                case "ext_task":
                    SfmExtEvent.Emit("net_task", SfmExtEvent.FromJsonObject(msg));
                    break;
                case "ext_sync":
                    if (msg.TryGetValue("k", out var sk) && sk is string syncName && msg.TryGetValue("v", out var sv))
                        SfmExtSync.HandleRemoteSync(syncName, sv);
                    break;
                case "ext_npc":
                    if (msg.TryGetValue("op", out var no) && no is string npcOp && msg.TryGetValue("name", out var nn) && nn is string npcName)
                        SfmExtNpcManager.HandleRemoteNpc(npcOp, npcName, msg);
                    break;
                case "ext_room":
                    if (msg.TryGetValue("op", out var ro) && ro is string roomOp)
                    {
                        var rid = SfmExtRoom.Str(msg, "room_id");
                        var uid = SfmExtRoom.Str(msg, "uid");
                        var uname = SfmExtRoom.Str(msg, "name");
                        switch (roomOp)
                        {
                            case "join": SfmExtRoom.HandleJoin(rid, uid, uname); break;
                            case "leave": SfmExtRoom.HandleLeave(rid, uid); break;
                            case "closed": SfmExtRoom.HandleRoomClosed(rid); break;
                        }
                    }
                    break;
                case "ext_vote":
                    if (msg.TryGetValue("op", out var vo) && vo is string voteOp)
                        SfmExtVote.HandleRemote(voteOp, msg);
                    break;
                case "ext_team":
                    SfmExtGameplay.HandleRemoteTeam(SfmExtRoom.Str(msg, "uid"), SfmExtRoom.Str(msg, "team"));
                    break;
                case "ext_announce":
                    SfmExtHud.Toast(SfmExtRoom.Str(msg, "text"), 6f);
                    SfmExtEvent.Emit("announce", SfmExtEvent.FromJsonObject(msg));
                    break;
                case "ext_countdown":
                    if (SfmExtRoom.Str(msg, "op") == "start" && msg.TryGetValue("seconds", out var sec))
                    {
                        var cdName = SfmExtRoom.Str(msg, "name");
                        double s = 0; try { s = Convert.ToDouble(sec); } catch { }
                        SfmExtGameplay.StartCountdown(cdName, (float)s);
                    }
                    break;
                case "ext_gift":
                    SfmExtEvent.Emit("gift_received", SfmExtEvent.FromJsonObject(msg));
                    break;
                case "ext_spectate":
                    SfmExtEvent.Emit("spectate", SfmExtEvent.FromJsonObject(msg));
                    break;
                case "ext_achievement":
                    SfmExtEvent.Emit("achievement_unlocked", SfmExtEvent.FromJsonObject(msg));
                    break;
            }
            SfmExtMsg.Dispatch(t, msg, fromUid);
        }
    }
}
