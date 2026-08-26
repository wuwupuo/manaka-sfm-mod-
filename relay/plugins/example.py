# plugins/example.py —— 服务器插件模板（复制改名即可开始开发）
# 本文件演示了服务器插件的全部能力：事件钩子 / 聊天命令 / 数据持久化 / 插件通信 / 客户端消息
# 部署：放到服务器  run/plugins/  目录，重启 relay 服务自动加载（或改后热重载）

VERSION = "1.0"
DESC = "示例插件：演示所有钩子与 API"

# ---------- 生命周期 ----------
def on_tick():
    """每 5 秒调用一次（可做定时任务/状态广播）"""
    # log("tick")
    pass

def on_unload():
    """插件被卸载/重载时调用（清理资源）"""
    log("已卸载")

# ---------- 玩家事件 ----------
def on_join(client):
    """玩家进入服务器（client: {uid, name, sock, room}）"""
    log("玩家进入:", client.get("name"), client.get("uid"))
    # 欢迎私信
    send_to_uid(client["uid"], {"t": "ext_evt", "ns": "sfmext", "evt": "welcome", "data": "欢迎来到服务器！输入 !help 查看玩法"})

def on_leave(client):
    """玩家离开"""
    log("玩家离开:", client.get("name"))

def on_room_create(client, room):
    """建房（room: {rid, name, host, max, players, password}）"""
    log("建房:", room.get("name"), "房主:", client.get("name"))

def on_room_destroy(room):
    """房间销毁"""
    log("房间销毁:", room.get("rid"))

# ---------- 聊天 ----------
def on_chat(client, text):
    """聊天消息（返回 True = 吞掉该消息，不再广播）"""
    # 敏感词过滤示例
    if "违规词" in text:
        send_to_uid(client["uid"], {"t": "ext_evt", "ns": "sfmext", "evt": "chat_blocked", "data": "请文明发言"})
        return True  # 吞掉
    return False

def on_command(client, cmd, args):
    """聊天以 ! 开头的命令（cmd 为命令名，args 为剩余参数列表）"""
    rid = client_room(client)
    if cmd == "help":
        send_to_uid(client["uid"], {"t": "ext_evt", "ns": "sfmext", "evt": "help_text", "data": "可用命令: !coin !stats !answer"})
    elif cmd == "coin":
        import random
        r = random.choice(["正面", "反面"])
        send_to_uid(client["uid"], {"t": "ext_evt", "ns": "sfmext", "evt": "coin_result", "data": r})
    elif cmd == "stats":
        # 数据持久化示例
        n = data_get(client["uid"], 0)
        data_set(client["uid"], n + 1)
        send_to_uid(client["uid"], {"t": "ext_evt", "ns": "sfmext", "evt": "stats", "data": {"count": n + 1}})

# ---------- 客户端消息（net.send_to_plugin 触发） ----------
def on_message(client, m):
    """收到客户端发来的消息（ns 是本插件名时才路由到这里）
       m: {"t":"ext", "ns":"example", "op":"...", "data":...}"""
    op = m.get("op")
    if op == "submit_answer":
        # 示例：记录答题
        data_set(client["uid"] + "_ans", m.get("data"))
        send_to_uid(client["uid"], {"t": "ext_evt", "ns": "sfmext", "evt": "answer_ok", "data": "已记录"})
    elif op == "give_all_coin":
        # 服务器权威广播
        for uid in room_players(client_room(client)):
            send_to_uid(uid, {"t": "ext_evt", "ns": "sfmext", "evt": "coin_grant", "data": 10})

# ---------- 插件通信 ----------
def on_plugin_message(from_name, op, data):
    """其它插件调用 send_to_plugin("example", op, data) 时触发"""
    log("来自插件", from_name, ":", op, data)

def on_event(evt, data):
    """插件间广播事件"""
    pass
