# -*- coding: utf-8 -*-
"""
SFM Relay 游戏模式扩展框架（gamemode.py）

设计目标：让别人一看就能加新玩法。每个玩法就是一个“模式对象”，
实现 start / vote / on_message / tick 四个入口即可，然后在 MODES 里注册。

房间游戏状态统一放在 rooms[rid]["game"]，字段结构见 HideAndSeek.build_state()。

新增玩法三步：
1) 复制 HideAndSeek，改成你的类名和 mode 名。
2) 实现 start()、on_vote()、on_message()、tick()。
3) 在文件末尾 MODES 字典里注册： MODES["你的mode名"] = 你的类(rooms)。
"""

import random
import time

# 这些名字在 relay.py 顶层定义，通过 set_ctx() 注入（避免循环 import）。
CTX = {
    "rooms": None,
    "clients": None,
    "send": None,
    "broadcast_room": None,
    "now": None,
}


def set_ctx(rooms, clients, send, broadcast_room, now):
    CTX["rooms"] = rooms
    CTX["clients"] = clients
    CTX["send"] = send
    CTX["broadcast_room"] = broadcast_room
    CTX["now"] = now


def _rooms():
    return CTX["rooms"]


def _clients():
    return CTX["clients"]


def _now():
    return CTX["now"]()


def _send(fd, o):
    CTX["send"](fd, o)


def _broadcast(rid, o, ex=None):
    CTX["broadcast_room"](rid, o, ex)


class BaseMode(object):
    """所有玩法的基类。子类只需实现下面这些方法。"""

    mode = "base"

    def __init__(self):
        pass

    def can_start(self, rid):
        """返回 (ok: bool, reason: str)。"""
        return True, ""

    def start(self, rid, proposer):
        """开局：分配角色、写 state、广播 game_state。"""
        raise NotImplementedError

    def on_vote(self, rid, uid, vote):
        """处理某人的投票，内部更新 votes，需要时自动开局。"""
        raise NotImplementedError

    def on_message(self, rid, uid, m):
        """处理 game_catch / game_escape 等玩法内消息。"""
        raise NotImplementedError

    def end_vote(self, rid, uid):
        g = self._g(rid)
        if not g or g.get("phase") != "playing":
            return
        g.setdefault("end_votes", set())
        g["end_votes"].add(uid)
        players = self._player_list(rid)
        if all(p in g["end_votes"] for p in players):
            _rooms()[rid]["game"] = None
            _broadcast(rid, {"t": "game_state", "mode": self.mode, "phase": "idle", "msg": "投票结束玩法"})
        else:
            _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "end_vote", "votes": len(g["end_votes"]), "need": len(players)})

    def on_leave(self, rid, uid):
        """有人离开房间/掉线时清理。"""
        pass

    def tick(self, rid):
        """由 relay 心跳调用，用于持续加速、超时等。"""
        pass


class HideAndSeek(BaseMode):
    mode = "hide_seek"

    # 规则参数（集中放在这里，方便改）
    MIN_PLAYERS = 3
    CATCHERS = lambda n: 1 if n < 6 else (2 if n < 8 else (3 if n < 12 else (4 if n < 16 else 5)))
    GAME_TIMEOUT = 20 * 60      # 单局最长 20 分钟
    CATCHER_TIMEOUT = 90        # 抓捕者断联等待 90 秒后结束
    CATCH_RATIO = 0.8            # 抓 80% 就算赢
    LIVES = 2                    # 每人两条命
    CATCH_RANGE = 2.0            # F 键判定距离（米）
    CATCHER_SPEED = 1.15
    HIDER_SPEED = 0.95
    BLINDFOLD_HIDER = True
    CATCH_STOP = 5               # 抓人者原地停止秒数
    CATCH_SLOW = 3               # 抓人者减速秒数
    HIDER_BOOST = 4              # 被抓者加速(2x)秒数
    HIDER_RED = 5                # 被抓者红色标记秒数
    HIDER_PENALTY = 10           # 被抓者原地停止秒数
    RAMP_INTERVAL = 20           # 抓不到人每 20 秒提速一次
    RAMP_STEP = 0.05             # 每次 +0.05
    CATCHER_MAX = 1.5            # 抓人者上限速度
    ESCAPE_POINTS = 5            # 需要开启的路口点数量
    EXITS = 2                    # 全开后随机开放 2 个出口
    MAP_STAGE = 11               # 商场（StageType=11）
    MVP_VOTE_SECONDS = 10        # 赛后投票阶段时长
    HEIGHT_SCALE = 1.0           # 强制所有人身高比例（1.0=不变，0.85=矮，1.15=高）
    LC_BAREYASUSA = 50.0         # 露出值（露出速率，越大越容易被发现；需实测微调到约 20 秒发现）
    LC_MAX_BAREYASUSA = 1000.0   # 被发现阈值
    LC_ATTR_OFF = True           # 强制关闭属性/强化系统
    LC_POINT_SECONDS = 90        # 每个 LC 点需停留 1.5 分钟
    LC_FOUND_PENALTY = 20        # 被 NPC 发现一次 +20 秒
    LC_POINT_MAX = 180           # 单个点上限 3 分钟

    def _g(self, rid):
        r = _rooms().get(rid)
        if not r:
            return None
        return r.get("game")

    def _player_list(self, rid):
        r = _rooms().get(rid)
        if not r:
            return []
        return list(r.get("players", {}).keys())

    def can_start(self, rid):
        n = len(self._player_list(rid))
        if n < self.MIN_PLAYERS:
            return False, "至少 3 人才能开始"
        return True, ""

    # ---- 开局流程 ----
    def propose(self, rid, uid):
        g = self._g(rid)
        if g and g.get("phase") in ("propose", "playing", "vote_mvp"):
            return False, "当前已有进行中的玩法"
        players = self._player_list(rid)
        ok, why = self.can_start(rid)
        if not ok:
            return False, why
        g = {
            "mode": self.mode,
            "phase": "propose",
            "proposer": uid,
            "votes": {p: ("yes" if p == uid else "") for p in players},
            "players": {},
            "catchers": [],
            "escape_opened": set(),
            "exits": [],
            "caught_count": 0,
            "escaped_count": 0,
            "catch_target": 0,
            "mvp_votes": {},
            "mvp_vote_until": 0,
            "started_at": _now(),
        }
        _rooms()[rid]["game"] = g
        _broadcast(rid, self._state_msg(rid))
        return True, "ok"

    def on_vote(self, rid, uid, vote):
        g = self._g(rid)
        if not g or g.get("phase") != "propose":
            return
        g["votes"][uid] = "yes" if vote == "yes" else "no"
        players = self._player_list(rid)
        all_voted = all(g["votes"].get(p) for p in players)
        if all(v == "yes" for v in g["votes"].values()):
            self._start(rid)
            return
        if all_voted:  # 有反对票 -> 取消
            _rooms()[rid]["game"] = None
            _broadcast(rid, {"t": "game_state", "mode": self.mode, "phase": "idle", "msg": "有人反对，玩法未开启"})
            return
        _broadcast(rid, {"t": "game_state", "mode": self.mode, "phase": "propose", "votes": g["votes"]})

    def _start(self, rid):
        players = self._player_list(rid)
        random.shuffle(players)
        n = len(players)
        nc = self.CATCHERS(n)
        catchers = players[:nc]
        hiders = players[nc:]
        hider_target = max(1, int(len(hiders) * self.CATCH_RATIO))  # 向下取整
        g = self._g(rid)
        g.update({
            "phase": "playing",
            "catchers": catchers,
            "catch_target": hider_target,
            "escape_opened": set(),
            "exits": [],
            "caught_count": 0,
            "escaped_count": 0,
            "mvp_votes": {},
            "started_at": _now(),
        })
        gp = g["players"] = {}
        for p in players:
            role = "catcher" if p in catchers else "hider"
            gp[p] = {
                "name": _clients().get(p, {}).get("name", p),
                "role": role,
                "lives": self.LIVES if role == "hider" else 0,
                "speed": self.CATCHER_SPEED if role == "catcher" else self.HIDER_SPEED,
                "blindfold": self.BLINDFOLD_HIDER and role == "hider",
                "height": self.HEIGHT_SCALE,
                "caught": 0,
                "escaped": False,
                "red_until": 0,
                "stop_until": 0,
                "slow_until": 0,
                "boost_until": 0,
                "last_catch": 0,
                "ramp": self.CATCHER_SPEED,
                "lc_point": -1,
                "lc_start": 0,
                "lc_total": 0,
                "lc_progress": 0,
                "lc_boost": 0,
            }
        _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "start",
                         "map": self.MAP_STAGE, "catchers": catchers, "players": gp})
        _broadcast(rid, self._state_msg(rid))

    # ---- 玩法内消息 ----
    def on_message(self, rid, uid, m):
        g = self._g(rid)
        if not g or g.get("phase") != "playing":
            return
        kind = m.get("kind")
        gp = g["players"].get(uid)
        if not gp:
            return
        now = _now()
        if kind == "catch":
            self._do_catch(rid, uid, str(m.get("target", "")), now)
        elif kind == "escape":
            self._do_escape(rid, uid, int(m.get("point", -1)), now)
        elif kind == "mvp":
            self._do_mvp(rid, uid, str(m.get("target", "")))
        elif kind == "lc":
            d = gp.get(uid)
            if d and d.get("lc_point", -1) >= 0:
                d["lc_boost"] = 1 if m.get("boost") else 0
        elif kind == "found":
            d = gp.get(uid)
            if d and d.get("lc_point", -1) >= 0:
                d["lc_total"] = min(self.LC_POINT_MAX, d.get("lc_total", self.LC_POINT_SECONDS) + self.LC_FOUND_PENALTY)
                _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "lc_penalty",
                                 "uid": uid, "seconds": d["lc_total"]})
            _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "found", "uid": uid})

    def _do_catch(self, rid, catcher_uid, target, now):
        g = self._g(rid)
        gp = g["players"]
        if catcher_uid not in g.get("catchers", []):
            return
        if gp.get(catcher_uid, {}).get("stop_until", 0) > now:
            return
        if target not in gp or gp[target]["role"] != "hider":
            return
        if gp[target].get("escaped"):
            return
        # 距离校验：用 relay 存下的最近位置
        cp = _clients().get(catcher_uid, {}).get("pos")
        tp = _clients().get(target, {}).get("pos")
        if cp and tp:
            d = ((cp[0]-tp[0])**2 + (cp[1]-tp[1])**2 + (cp[2]-tp[2])**2) ** 0.5
            if d > self.CATCH_RANGE:
                return
        gp[target]["lives"] = max(0, gp[target]["lives"] - 1)
        gp[target]["caught"] += 1
        if gp[target]["lives"] <= 0:
            g["caught_count"] += 1
            gp[target]["role"] = "out"
        else:
            gp[target]["red_until"] = now + self.HIDER_RED
            gp[target]["boost_until"] = now + self.HIDER_BOOST
            gp[target]["stop_until"] = now + self.HIDER_PENALTY
        gp[catcher_uid]["stop_until"] = now + self.CATCH_STOP
        gp[catcher_uid]["slow_until"] = now + self.CATCH_SLOW
        gp[catcher_uid]["last_catch"] = now
        _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "caught",
                         "catcher": catcher_uid, "target": target,
                         "caught_count": g["caught_count"], "target_count": g["catch_target"]})
        self._check_end(rid)
        _broadcast(rid, self._state_msg(rid))

    def _do_escape(self, rid, uid, point, now):
        g = self._g(rid)
        gp = g["players"]
        if gp.get(uid, {}).get("role") != "hider":
            return
        # 出口已开放：按 F 即逃离成功
        if g.get("exits"):
            gp[uid]["escaped"] = True
            g["escaped_count"] += 1
            _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "escaped", "uid": uid})
            self._check_end(rid)
            _broadcast(rid, self._state_msg(rid))
            return
        d = gp[uid]
        # 已在 LC 中，忽略重复按 F
        if d.get("lc_point", -1) >= 0:
            return
        opened = set(g["escape_opened"])
        nxt = None
        for i in range(self.ESCAPE_POINTS):
            if i not in opened:
                nxt = i
                break
        if nxt is None:
            return
        d["lc_point"] = nxt
        d["lc_start"] = now
        d["lc_total"] = self.LC_POINT_SECONDS
        d["lc_progress"] = 0
        d["lc_boost"] = 0
        _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "lc_start",
                         "uid": uid, "point": nxt, "seconds": self.LC_POINT_SECONDS})

    def _do_mvp(self, rid, uid, target):
        g = self._g(rid)
        if g.get("phase") != "vote_mvp":
            return
        if target in g.get("players", {}):
            g.setdefault("mvp_votes", {})[uid] = target
            _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "mvp", "votes": g["mvp_votes"]})

    def _check_end(self, rid):
        g = self._g(rid)
        catchers_won = g["caught_count"] >= g["catch_target"]
        hiders = [p for p, d in g["players"].items() if d["role"] in ("hider", "out")]
        all_out = all(g["players"][p].get("role") == "out" for p in hiders)
        if catchers_won or all_out:
            self._enter_mvp_vote(rid, "catchers")

    def _enter_mvp_vote(self, rid, winner):
        g = self._g(rid)
        g["phase"] = "vote_mvp"
        g["winner"] = winner
        g["mvp_vote_until"] = _now() + self.MVP_VOTE_SECONDS
        _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "end",
                         "winner": winner, "vote_seconds": self.MVP_VOTE_SECONDS,
                         "players": {u: d["name"] for u, d in g["players"].items()}})

    def tick(self, rid):
        g = self._g(rid)
        if not g or g.get("phase") != "playing":
            return
        now = _now()
        # 抓人者长期抓不到 -> 逐渐加速
        for c in g.get("catchers", []):
            d = g["players"].get(c)
            if not d:
                continue
            since = now - d.get("last_catch", g.get("started_at", now))
            steps = int(since // self.RAMP_INTERVAL)
            new_speed = min(self.CATCHER_MAX, self.CATCHER_SPEED + steps * self.RAMP_STEP)
            if new_speed != d["ramp"]:
                d["ramp"] = new_speed
                _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "speed",
                                 "uid": c, "speed": new_speed})
        # LC 点计时完成 -> 开点 / 开放出口（人前露出 2 倍速）
        for uid, d in g.get("players", {}).items():
            if d.get("lc_point", -1) >= 0:
                d["lc_progress"] = d.get("lc_progress", 0) + 5 * (2 if d.get("lc_boost") else 1)
                if d["lc_progress"] < d.get("lc_total", self.LC_POINT_SECONDS):
                    continue
                point = d["lc_point"]
                d["lc_point"] = -1
                d["lc_start"] = 0
                d["lc_total"] = 0
                d["lc_progress"] = 0
                d["lc_boost"] = 0
                opened = set(g.get("escape_opened", set()))
                opened.add(point)
                g["escape_opened"] = opened
                _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "point",
                                 "uid": uid, "opened": len(opened), "need": self.ESCAPE_POINTS})
                if len(opened) >= self.ESCAPE_POINTS:
                    all_points = list(range(self.ESCAPE_POINTS))
                    random.shuffle(all_points)
                    g["exits"] = all_points[:self.EXITS]
                    _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "exits", "exits": g["exits"]})
        # 单局超时结束
        if now - g.get("started_at", now) > self.GAME_TIMEOUT:
            _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "end", "winner": "timeout", "vote_seconds": 0})
            _rooms()[rid]["game"] = None
            _broadcast(rid, {"t": "game_state", "mode": self.mode, "phase": "idle", "msg": "时间到，玩法结束"})
            return
        # 抓捕者断联 90 秒 -> 结束
        for c in g.get("catchers", []):
            lc = clients.get(c, {}).get("last_seen", now)
            if now - lc > self.CATCHER_TIMEOUT:
                _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "end", "winner": "disconnect", "vote_seconds": 0})
                _rooms()[rid]["game"] = None
                _broadcast(rid, {"t": "game_state", "mode": self.mode, "phase": "idle", "msg": "抓捕者断联，玩法结束"})
                return
        # MVP 投票结束 -> 结算（平票随机）
        if g.get("phase") == "vote_mvp" and g.get("mvp_vote_until") and now >= g["mvp_vote_until"]:
            self._finish(rid)

    def _finish(self, rid):
        g = self._g(rid)
        tally = {}
        for target in g.get("mvp_votes", {}).values():
            tally[target] = tally.get(target, 0) + 1
        if tally:
            top = max(tally.values())
            winners = [u for u, c in tally.items() if c == top]
            mvp = random.choice(winners)
        else:
            mvp = ""
        _broadcast(rid, {"t": "game_event", "mode": self.mode, "kind": "finish", "mvp": mvp})
        _rooms()[rid]["game"] = None
        _broadcast(rid, {"t": "game_state", "mode": self.mode, "phase": "idle"})

    def on_leave(self, rid, uid):
        g = self._g(rid)
        if not g:
            return
        if g.get("phase") == "propose":
            g["votes"].pop(uid, None)
            players = self._player_list(rid)
            if all(g["votes"].get(p) == "yes" for p in players) and len(players) >= self.MIN_PLAYERS:
                self._start(rid)
            else:
                _broadcast(rid, self._state_msg(rid))
        elif g.get("phase") == "playing":
            gp = g["players"]
            if gp.get(uid, {}).get("role") == "hider" and not gp[uid].get("escaped"):
                # 掉线按被抓处理
                g["caught_count"] += 1
            gp.pop(uid, None)
            if len(self._player_list(rid)) < self.MIN_PLAYERS:
                _rooms()[rid]["game"] = None
                _broadcast(rid, {"t": "game_state", "mode": self.mode, "phase": "idle", "msg": "人数不足，玩法结束"})

    def _state_msg(self, rid):
        g = self._g(rid)
        if not g:
            return {"t": "game_state", "mode": self.mode, "phase": "idle"}
        return {
            "t": "game_state",
            "mode": self.mode,
            "phase": g.get("phase"),
            "catchers": g.get("catchers", []),
            "catch_target": g.get("catch_target", 0),
            "caught_count": g.get("caught_count", 0),
            "escaped_count": g.get("escaped_count", 0),
            "escape_opened": sorted(g.get("escape_opened", [])),
            "exits": g.get("exits", []),
            "players": g.get("players", {}),
            "votes": g.get("votes", {}),
            "lc": {"bareyasusa": self.LC_BAREYASUSA, "max_bareyasusa": self.LC_MAX_BAREYASUSA, "attr_off": 1 if self.LC_ATTR_OFF else 0},
        }


# ============ 玩法注册表 ============
MODES = {
    "hide_seek": HideAndSeek,
}


def handle(rid, uid, m):
    """relay.dispatch 里所有 game_* 消息都转发到这里。"""
    t = m.get("t")
    mode_name = str(m.get("mode", "hide_seek"))
    cls = MODES.get(mode_name)
    if not cls:
        _send(_clients().get(uid, {}).get("sock"), {"t": "err", "m": "未知玩法"})
        return
    obj = cls()
    if t == "game_propose":
        ok, why = obj.propose(rid, uid)
        if not ok:
            _send(_clients().get(uid, {}).get("sock"), {"t": "err", "m": why})
    elif t == "game_vote":
        obj.on_vote(rid, uid, str(m.get("vote", "no")))
    elif t in ("game_catch", "game_escape", "game_mvp", "game_found", "game_lc"):
        m2 = dict(m)
        m2["kind"] = t[len("game_"):]  # catch / escape / mvp
        obj.on_message(rid, uid, m2)
    elif t == "game_end_vote":
        obj.end_vote(rid, uid)
    elif t == "game_abort":
        r = _rooms().get(rid)
        if r and r.get("game") and r["game"].get("proposer") == uid:
            r["game"] = None
            _broadcast(rid, {"t": "game_state", "mode": mode_name, "phase": "idle"})


def tick_all():
    """relay 心跳里调用，处理持续加速、投票超时。"""
    for rid in list(_rooms().keys()):
        g = _rooms()[rid].get("game")
        if not g:
            continue
        cls = MODES.get(g.get("mode"))
        if cls:
            try:
                cls().tick(rid)
            except Exception:
                pass


def on_leave(rid, uid):
    g = _rooms().get(rid, {}).get("game")
    if not g:
        return
    cls = MODES.get(g.get("mode"))
    if cls:
        try:
            cls().on_leave(rid, uid)
        except Exception:
            pass
