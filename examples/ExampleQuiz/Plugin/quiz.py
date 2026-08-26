# plugins/quiz.py —— 答题游戏服务器插件
# 配合客户端模组 ExampleQuiz 使用；纯聊天也可玩（!quiz 出题 / !answer 答题）
# 本文件为服务器插件（不开源服务器本体，插件是开放给服主的玩法扩展）

VERSION = "1.0"
DESC = "答题游戏：!quiz 出题，!answer 答题"

import random

_QUESTIONS = [
    ("1+1 等于几？", "2"),
    ("天空是什么颜色？", "蓝色"),
    ("SFM 是哪个引擎？", "unity"),
    ("一年多少个月？", "12"),
    ("1 公里=几米？", "1000"),
]

def on_command(client, cmd, args):
    rid = client_room(client)
    if not rid:
        send_to_uid(client["uid"], {"t": "ext_evt", "ns": "sfmext", "evt": "quiz_wrong", "data": "请先进入房间"})
        return
    if cmd == "quiz":
        q, a = random.choice(_QUESTIONS)
        data_set("current_q", a)
        data_set("current_rid", rid)
        data_set("winner", "")
        broadcast_room(rid, {"t": "ext_evt", "ns": "sfmext", "evt": "quiz_question", "data": q})
        log("出题:", q)
    elif cmd == "answer":
        if data_get("current_rid") != rid or not data_get("current_q"):
            send_to_uid(client["uid"], {"t": "ext_evt", "ns": "sfmext", "evt": "quiz_wrong", "data": "还没有题目，用 !quiz 出题"})
            return
        if data_get("winner"):
            send_to_uid(client["uid"], {"t": "ext_evt", "ns": "sfmext", "evt": "quiz_wrong", "data": "已经有人答对了"})
            return
        ans = " ".join(args).lower().strip()
        if ans and ans == data_get("current_q", "").lower():
            data_set("winner", client["uid"])
            data_set(client["uid"] + "_score", data_get(client["uid"] + "_score", 0) + 10)
            broadcast_room(rid, {"t": "ext_evt", "ns": "sfmext", "evt": "quiz_win", "data": client["name"]})
            log("答对:", client["name"])
        else:
            send_to_uid(client["uid"], {"t": "ext_evt", "ns": "sfmext", "evt": "quiz_wrong", "data": "不对哦"})
    elif cmd == "score":
        sc = data_get(client["uid"] + "_score", 0)
        send_to_uid(client["uid"], {"t": "ext_evt", "ns": "sfmext", "evt": "quiz_score", "data": {"score": sc}})

def on_message(client, m):
    """客户端模组发来的消息（ExampleQuiz 可扩展：提交带 UI 的答案）"""
    op = m.get("op")
    if op == "submit":
        ans = str(m.get("data", "")).lower()
        if ans == data_get("current_q", "").lower() and not data_get("winner"):
            data_set("winner", client["uid"])
            data_set(client["uid"] + "_score", data_get(client["uid"] + "_score", 0) + 10)
            broadcast_room(client_room(client), {"t": "ext_evt", "ns": "sfmext", "evt": "quiz_win", "data": client["name"]})
