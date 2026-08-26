# SFM Online 服务器插件 API 速查

> 服务器：SFM Online 官方中继服务器（含插件系统；服务器本体闭源，插件接口开放）
> 插件目录：服务器 `plugins/*.py`，启动自动加载，支持热重载
> 每个插件是独立 Python 模块，模块级定义钩子函数即可。

## 1. 钩子（事件回调）

| 钩子 | 签名 | 说明 |
|---|---|---|
| `on_tick` | `()` | 每 5 秒调用 |
| `on_join` | `(client)` | 玩家进入服务器 |
| `on_leave` | `(client)` | 玩家离开 |
| `on_room_create` | `(client, room)` | 创建房间 |
| `on_room_destroy` | `(room)` | 房间销毁 |
| `on_chat` | `(client, text)` | 聊天（返回 True 吞掉消息） |
| `on_command` | `(client, cmd, args)` | `!命令` 聊天命令 |
| `on_message` | `(client, m)` | 客户端 `net.send_to_plugin` 消息（m: {"op":..,"data":..,"ns":..,"uid":..}） |
| `on_plugin_message` | `(from_name, op, data)` | 其它插件 `send_to_plugin` |
| `on_event` | `(evt, data)` | 插件广播事件 |
| `on_unload` | `()` | 卸载/重载时 |

## 2. API（自动注入到插件模块）

### 发送
| API | 说明 |
|---|---|
| `send(sock, obj)` | 向 socket 发 JSON 对象 |
| `send_plain(sock, text)` | 发纯文本 |
| `send_to_uid(uid, obj)` | 按 uid 定向发送 |
| `broadcast_room(rid, obj, ex=None)` | 房间广播（排除 ex uid） |
| `send_to_room(rid, obj, ex=None)` | 同 broadcast_room |
| `broadcast_all(obj)` | 全服广播 |

### 查询
| API | 说明 |
|---|---|
| `clients()` | 全部客户端 dict（uid → client） |
| `rooms()` | 全部房间 dict（rid → room） |
| `get_client(uid)` | 单个客户端 |
| `get_room(rid)` | 单个房间 |
| `client_room(c)` | 客户端所在房间 rid（空=没进房） |
| `room_players(rid)` | 房间玩家 uid 列表 |
| `now()` | 时间戳 |

### 数据持久化（自动写入 plugin_data.json）
| API | 说明 |
|---|---|
| `data_get(key, default=None)` | 读 |
| `data_set(key, value)` | 写（自动保存） |

### 插件间通信
| API | 说明 |
|---|---|
| `send_to_plugin(target, op, data=None)` | 调目标插件 `on_plugin_message` |
| `broadcast_event(evt, data=None, exclude=None)` | 广播事件给全部插件 `on_event` |
| `list_plugins()` | 已加载插件名列表 |

### 日志
| API | 说明 |
|---|---|
| `log(*args)` | 带插件名前缀打印 |
| `log_event(msg)` | 事件日志 |

## 3. 数据结构

```python
client = {"uid": str, "name": str, "sock": socket, "room": str}
room   = {"rid": str, "name": str, "host": str, "max": int,
          "players": {uid: client}, "password": str}
```

## 4. 与客户端通信（消息格式）

```json
// 服务器 → 客户端（客户端 SfmExtEvent.On(evt) 接收）
{"t": "ext_evt", "ns": "sfmext", "evt": "事件名", "data": 任意值}

// 客户端 → 服务器（客户端 SfmExtNet.SendToPlugin(ns, op, data)）
{"t": "ext", "ns": "插件名", "op": "操作名", "data": 任意值}
```

## 5. 管理命令（服务器控制台/HTTP，视部署方式）

- 插件热重载：重启 relay 或 `reload_plugin(name)`
- 客户端侧可用 `net.server_mods` / `net.is_server_mod` 检测服务器装了哪些插件

## 6. 完整示例

见同目录 `example.py`（含聊天命令、数据持久化、客户端互发消息、事件钩子全演示）。
