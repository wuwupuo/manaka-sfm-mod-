import json, os, subprocess, urllib.parse, secrets, time, http.cookies
from http.server import BaseHTTPRequestHandler, HTTPServer

CFG = os.environ.get("SFM_CFG", "/opt/sfm-relay/config.json")
ST = os.environ.get("SFM_STATE", "/opt/sfm-relay/state.json")
CMD = os.environ.get("SFM_CMD", "/opt/sfm-relay/commands.json")
LOG = os.environ.get("SFM_LOG", "/opt/sfm-relay/relay.log")
ADMIN_PORT = int(os.environ.get("SFM_ADMIN_PORT", "7001"))
ADMIN_HOST = os.environ.get("SFM_ADMIN_HOST", "127.0.0.1")
BASE = os.environ.get("SFM_ADMIN_BASE", "/wuwupuo")
SESS = {}
LOGIN_FAIL = {}

S = {
    "title": ("联机服务器后台", "Relay Admin"),
    "overview": ("总览", "Overview"),
    "rooms": ("房间", "Rooms"),
    "social": ("社交", "Social"),
    "announce": ("公告", "Announcement"),
    "accounts": ("账号", "Accounts"),
    "mods": ("模组", "Mods"),
    "logs": ("日志", "Logs"),
    "settings": ("设置", "Settings"),
    "user": ("账号", "User"),
    "password": ("密码", "Password"),
    "login": ("登录", "Login"),
    "wrong_creds": ("账号或密码错误", "Wrong credentials"),
    "server_name": ("服务器名", "Server name"),
    "server_desc": ("简介", "Description"),
    "online": ("在线", "Online"),
    "room_count": ("房间数", "Rooms"),
    "players": ("人数", "Players"),
    "room_id": ("房间ID", "Room ID"),
    "max_players": ("人数上限", "Max players"),
    "create_room": ("创建房间", "Create room"),
    "delete": ("删除", "Delete"),
    "chat": ("聊天", "Chat"),
    "back": ("返回", "Back"),
    "title2": ("标题", "Title"),
    "content": ("内容", "Content"),
    "save_broadcast": ("保存并广播", "Save & broadcast"),
    "kick": ("踢出", "Kick"),
    "blacklist": ("黑名单", "Blacklist"),
    "type": ("类型", "Type"),
    "value": ("值", "Value"),
    "until": ("到期", "Until"),
    "remove": ("移除", "Remove"),
    "ban": ("拉黑", "Ban"),
    "ban_hours": ("封禁小时(0=永久)", "Hours (0=forever)"),
    "online_users": ("在线用户", "Online users"),
    "runtime_log": ("运行日志", "Runtime log"),
    "public_chat": ("服务器公屏", "Server public chat"),
    "room_chat": ("房间聊天", "Room chat"),
    "send_to": ("发送到", "Send to"),
    "server_wide": ("全服公屏", "Server wide"),
    "send": ("发送", "Send"),
    "join_password": ("连接密码(空=无)", "Join password (empty=none)"),
    "port": ("端口", "Port"),
    "max_online": ("最大在线", "Max online"),
    "max_rooms": ("最大房间", "Max rooms"),
    "room_players": ("每房人数", "Room players"),
    "admin_uids": ("管理员UID(逗号分隔)", "Admin UIDs (comma)"),
    "lan_only": ("仅局域网", "LAN only"),
    "mod_name": ("模组名", "Mod name"),
    "required": ("必需", "Required"),
    "checks": ("检测路径(每行一个，相对游戏根目录)", "Check paths (one per line, relative to game root)"),
    "mod_files": ("下载模组文件(每行一个，放到 mods/ 目录)", "Download files (one per line, put in mods/ dir)"),
    "new_mod": ("新增模组", "New mod"),
    "save_mods": ("保存模组", "Save mods"),
    "save": ("保存", "Save"),
    "save_restart": ("保存(改动端口/上限会重启服务)", "Save (port/caps changes restart service)"),
    "announcement": ("服务器公告", "Server announcement"),
    "name": ("名称", "Name"),
    "no_logs": ("暂无日志", "No logs yet"),
    "logout": ("退出登录", "Logout"),
}

def T(key, lang):
    pair = S.get(key, (key, key))
    return pair[0] if lang == "zh" else pair[1]

def lc():
    c = json.load(open(CFG, encoding="utf-8"))
    c.setdefault("admin_user", "wuwupuo")
    c.setdefault("admin_pass", "wuwupuo123qwe123-")
    c.setdefault("admin_ips", "")
    c.setdefault("mods", [])
    c.setdefault("server_name", "SFM Relay")
    c.setdefault("server_desc", "")
    c.setdefault("server_password", "")
    c.setdefault("announcement", {"title": "", "content": ""})
    c.setdefault("blacklist", [])
    c.setdefault("lan_only", False)
    c.setdefault("admin_uids", [])
    return c

def save_cfg(c):
    tmp = CFG + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(c, f, ensure_ascii=False, indent=2)
    os.replace(tmp, CFG)

def ls():
    try:
        return json.load(open(ST, encoding="utf-8"))
    except Exception:
        return {"online": 0, "users": [], "rooms": [], "pubchat": [], "announcement": {"title": "", "content": ""}, "server_name": "SFM Relay", "server_desc": ""}

def push_command(cmd):
    cmds = json.load(open(CMD, encoding="utf-8")) if os.path.exists(CMD) else []
    cmds.append(cmd)
    with open(CMD, "w", encoding="utf-8") as f:
        json.dump(cmds, f, ensure_ascii=False)

def esc(s):
    return (str(s).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;"))

def ip(h):
    x = h.headers.get("X-Forwarded-For", "")
    return (x.split(",")[0].strip() if x else h.client_address[0])

def ok_ip(h, c):
    ips = [i.strip() for i in str(c.get("admin_ips", "")).split(",") if i.strip()]
    return (not ips) or ip(h) in ips

def ok_sid(h):
    ck = http.cookies.SimpleCookie(h.headers.get("Cookie", ""))
    s = ck["sid"].value if "sid" in ck else None
    return bool(s and s in SESS and SESS[s] > time.time())

def tail_log(lang):
    try:
        with open(LOG, "r", encoding="utf-8", errors="ignore") as f:
            lines = f.readlines()[-200:]
        return "<br>".join(esc(x.rstrip("\n")) for x in lines)
    except Exception:
        return T("no_logs", lang)

class H(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def send_html(self, body, status=200):
        self.send_response(status)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body.encode("utf-8"))

    def nav(self, lang, active):
        tabs = ["overview", "rooms", "social", "announce", "accounts", "mods", "logs", "settings"]
        items = []
        for key in tabs:
            cls = ' style="font-weight:bold"' if key == active else ""
            items.append('<a href="/wuwupuo/?tab=%s&amp;lang=%s"%s>%s</a>' % (key, lang, cls, T(key, lang)))
        return '<div style="margin-bottom:12px">%s | <a href="/wuwupuo/?lang=en">EN</a> / <a href="/wuwupuo/?lang=zh">中文</a> | <a href="/wuwupuo/logout?lang=%s">%s</a></div>' % (" | ".join(items), lang, T("logout", lang))

    def login_form(self, lang, err=""):
        return ('<meta charset="utf-8"><h2>SFM %s</h2><form method="post">%s <input name="user"><br><br>%s <input type="password" name="pass"><br><br><button>%s</button></form>%s' %
                (T("title", lang), T("user", lang), T("password", lang), T("login", lang), err))

    def page(self, lang, active, body):
        return '<meta charset="utf-8"><h1>SFM %s</h1>%s%s' % (T("title", lang), self.nav(lang, active), body)

    def render_overview(self, lang, st):
        rows = "".join('<tr><td>%s</td><td>%d/%d</td><td><a href="/wuwupuo/?tab=social&amp;room=%s&amp;lang=%s">%s</a></td><td><form method="post" style="display:inline"><input type="hidden" name="cmd" value="delroom"><input type="hidden" name="room_id" value="%s"><input type="hidden" name="lang" value="%s"><button>%s</button></form></td></tr>' %
                       (esc(r["room_id"]), len(r["players"]), r["max"], urllib.parse.quote(r["room_id"]), lang, T("chat", lang), esc(r["room_id"]), lang, T("delete", lang)) for r in st["rooms"])
        create = ('<form method="post" style="margin-top:8px"><input type="hidden" name="cmd" value="create_room"><input type="hidden" name="lang" value="%s">%s <input name="room_id" placeholder="room_id"> %s <input name="max_players" value="8" size="3"> <button>%s</button></form>' %
                  (lang, T("room_id", lang), T("max_players", lang), T("create_room", lang)))
        return ('<p><b>%s</b>: %s &nbsp; <b>%s</b>: %s</p><p>%s: %d &nbsp; %s: %d</p><h3>%s</h3><table border="1" cellpadding="5"><tr><th>Room</th><th>%s</th><th></th><th></th></tr>%s</table>%s' %
                (T("server_name", lang), esc(st.get("server_name", "")), T("server_desc", lang), esc(st.get("server_desc", "")), T("online", lang), st.get("online", 0), T("room_count", lang), len(st["rooms"]), T("rooms", lang), T("players", lang), rows, create))

    def render_rooms(self, lang, st):
        blocks = []
        for r in st["rooms"]:
            players = ", ".join("%s(%s)" % (esc(p["name"]), p["uid"]) for p in r["players"])
            blocks.append('<div style="border:1px solid #888;margin:6px 0;padding:6px"><b>%s</b> (%d/%d) &nbsp; <a href="/wuwupuo/?tab=social&amp;room=%s&amp;lang=%s">%s</a> &nbsp; <form method="post" style="display:inline"><input type="hidden" name="cmd" value="delroom"><input type="hidden" name="room_id" value="%s"><input type="hidden" name="lang" value="%s"><button>%s</button></form><br>%s</div>' %
                          (esc(r["room_id"]), len(r["players"]), r["max"], urllib.parse.quote(r["room_id"]), lang, T("chat", lang), esc(r["room_id"]), lang, T("delete", lang), players))
        return '<h3>%s</h3>%s' % (T("rooms", lang), "".join(blocks) if blocks else "<p>%s</p>" % T("no_logs", lang))

    def render_announce(self, lang, st):
        a = st.get("announcement", {"title": "", "content": ""})
        return ('<h3>%s</h3><p><b>%s</b> %s<br>%s</p><form method="post"><input type="hidden" name="cmd" value="announce"><input type="hidden" name="lang" value="%s">%s <input name="title" value="%s"><br>%s <textarea name="content" rows="4" cols="50">%s</textarea><br><button>%s</button></form>' %
                (T("announcement", lang), T("title2", lang), esc(a.get("title", "")), esc(a.get("content", "")), lang, T("title2", lang), esc(a.get("title", "")), T("content", lang), esc(a.get("content", "")), T("save_broadcast", lang)))

    def render_accounts(self, lang, st, cfg):
        users = "".join('<tr><td>%s</td><td>%s</td><td>%s</td><td>%s</td><td><form method="post" style="display:inline"><input type="hidden" name="cmd" value="kick"><input type="hidden" name="uid" value="%s"><input type="hidden" name="lang" value="%s"><button>%s</button></form></td></tr>' %
                        (esc(u["uid"]), esc(u["name"]), esc(u.get("ip", "")), esc(u.get("room", "") or "-"), esc(u["uid"]), lang, T("kick", lang)) for u in st["users"])
        bl = "".join('<tr><td>%s</td><td>%s</td><td>%s</td><td><form method="post" style="display:inline"><input type="hidden" name="cmd" value="blacklist_remove"><input type="hidden" name="type" value="%s"><input type="hidden" name="value" value="%s"><input type="hidden" name="lang" value="%s"><button>%s</button></form></td></tr>' %
                      (esc(b.get("type", "")), esc(b.get("value", "")), b.get("until", 0), esc(b.get("type", "")), esc(b.get("value", "")), lang, T("remove", lang)) for b in cfg.get("blacklist", []))
        add = ('<form method="post" style="margin-top:8px"><input type="hidden" name="cmd" value="blacklist_add"><input type="hidden" name="lang" value="%s"><select name="type"><option value="uid">UID</option><option value="name">%s</option><option value="ip">IP</option></select><input name="value"> %s <input name="hours" value="0" size="3"><button>%s</button></form>' %
               (lang, T("name", lang), T("ban_hours", lang), T("ban", lang)))
        return ('<h3>%s</h3><table border="1" cellpadding="5"><tr><th>UID</th><th>%s</th><th>IP</th><th>%s</th><th></th></tr>%s</table><h3>%s</h3><table border="1" cellpadding="5"><tr><th>%s</th><th>%s</th><th>%s</th><th></th></tr>%s</table>%s' %
                (T("online_users", lang), T("name", lang), T("rooms", lang), users, T("blacklist", lang), T("type", lang), T("value", lang), T("until", lang), bl, add))

    def render_logs(self, lang):
        return '<h3>%s</h3><pre style="white-space:pre-wrap">%s</pre>' % (T("runtime_log", lang), tail_log(lang))

    def render_social(self, lang, st, room):
        if room:
            r = next((x for x in st["rooms"] if x["room_id"] == room), None)
            msgs = "".join("<li>%s(%s): %s</li>" % (esc(m.get("name", "")), esc(str(m.get("uid", ""))), esc(m.get("text", ""))) for m in (r["chat"] if r else []))
            return ('<h3>%s %s</h3><ul>%s</ul><p><a href="/wuwupuo/?tab=social&amp;lang=%s">%s</a></p>' % (T("room_chat", lang), esc(room), msgs, lang, T("back", lang)))
        pub = "".join('<li><b>%s</b>(%s): %s <span style="color:#888">[%s]</span></li>' % (esc(m.get("name", "")), esc(str(m.get("uid", ""))), esc(m.get("text", "")), m.get("ts", "")) for m in st.get("pubchat", []))
        roomopts = "".join('<option value="%s">%s</option>' % (esc(r["room_id"]), esc(r["room_id"])) for r in st["rooms"])
        send = ('<form method="post"><input type="hidden" name="cmd" value="broadcast"><input type="hidden" name="lang" value="%s">%s <select name="target"><option value="all">%s</option>%s</select><input name="text"><button>%s</button></form>' %
                (lang, T("send_to", lang), T("server_wide", lang), roomopts, T("send", lang)))
        return '<h3>%s</h3><ul>%s</ul>%s' % (T("public_chat", lang), pub, send)

    def render_mods(self, lang, cfg):
        mods = cfg.get("mods", []) or []
        fields = []
        for i, m in enumerate(mods):
            checks = "\n".join(m.get("checks", []))
            files = "\n".join(m.get("files", []))
            req = ' checked' if m.get("required") else ''
            fields.append('<fieldset style="margin:6px 0"><legend>%s %d</legend>%s <input name="name_%d" value="%s"><br>%s <input type="checkbox" name="required_%d" value="1"%s><br>%s<textarea name="files_%d" rows="2" cols="50">%s</textarea><br>%s<textarea name="checks_%d" rows="3" cols="50">%s</textarea><br><label><input type="checkbox" name="delete_%d" value="1"> %s</label></fieldset>' %
                          (T("mod_name", lang), i + 1, T("mod_name", lang), i, esc(m.get("name", "")), T("required", lang), i, req, T("mod_files", lang) + ": ", i, esc(files), T("checks", lang) + ": ", i, esc(checks), i, T("delete", lang)))
        new = ('<fieldset style="margin:6px 0"><legend>%s</legend>%s <input name="new_name"><br>%s <input type="checkbox" name="new_required" value="1"><br>%s<textarea name="new_files" rows="2" cols="50"></textarea><br>%s<textarea name="new_checks" rows="3" cols="50"></textarea></fieldset>' %
               (T("new_mod", lang), T("mod_name", lang), T("required", lang), T("mod_files", lang) + ": ", T("checks", lang) + ": "))
        return ('<form method="post"><input type="hidden" name="cmd" value="save_mods"><input type="hidden" name="lang" value="%s">%s%s<button>%s</button></form>' % (lang, "".join(fields), new, T("save_mods", lang)))

    def render_settings(self, lang, cfg):
        return ('<form method="post"><input type="hidden" name="cmd" value="save_settings"><input type="hidden" name="lang" value="%s"><table cellpadding="4">' % lang +
                '<tr><td>%s</td><td><input name="server_name" value="%s"></td></tr>' % (T("server_name", lang), esc(cfg.get("server_name", ""))) +
                '<tr><td>%s</td><td><input name="server_desc" value="%s"></td></tr>' % (T("server_desc", lang), esc(cfg.get("server_desc", ""))) +
                '<tr><td>%s</td><td><input name="server_password" value="%s"></td></tr>' % (T("join_password", lang), esc(cfg.get("server_password", ""))) +
                '<tr><td>%s</td><td><input name="port" value="%s"></td></tr>' % (T("port", lang), cfg.get("port", 7000)) +
                '<tr><td>%s</td><td><input name="max_online" value="%s"></td></tr>' % (T("max_online", lang), cfg.get("max_online", 100)) +
                '<tr><td>%s</td><td><input name="max_rooms" value="%s"></td></tr>' % (T("max_rooms", lang), cfg.get("max_rooms", 50)) +
                '<tr><td>%s</td><td><input name="room_max_players" value="%s"></td></tr>' % (T("room_players", lang), cfg.get("room_max_players", 8)) +
                '<tr><td>%s</td><td><input name="admin_uids" value="%s"></td></tr>' % (T("admin_uids", lang), ",".join(cfg.get("admin_uids", []))) +
                '<tr><td>%s</td><td><input type="checkbox" name="lan_only" value="1"%s></td></tr>' % (T("lan_only", lang), " checked" if cfg.get("lan_only") else "") +
                '</table><button>%s</button></form>' % T("save_restart", lang))

    def render_page(self, tab, lang, cfg):
        st = ls()
        if tab == "rooms":
            body = self.render_rooms(lang, st)
        elif tab == "announce":
            body = self.render_announce(lang, st)
        elif tab == "accounts":
            body = self.render_accounts(lang, st, cfg)
        elif tab == "mods":
            body = self.render_mods(lang, cfg)
        elif tab == "logs":
            body = self.render_logs(lang)
        elif tab == "social":
            body = self.render_social(lang, st, "")
        elif tab == "settings":
            body = self.render_settings(lang, cfg)
        else:
            body = self.render_overview(lang, st)
        return self.page(lang, tab, body)

    def do_GET(self):
        q = urllib.parse.urlparse(self.path)
        if not (q.path == BASE or q.path.startswith(BASE + "/")):
            self.send_html("404 Not Found", 404)
            return
        path = q.path[len(BASE):] or "/"
        p = urllib.parse.parse_qs(q.query)
        lang = p.get("lang", ["zh"])[0]
        cfg = lc()
        if not ok_ip(self, cfg):
            self.send_html("Forbidden IP", 403)
            return
        if path == "/logout":
            self.send_response(302)
            self.send_header("Set-Cookie", "sid=; Path=/; HttpOnly; Max-Age=0")
            self.send_header("Location", BASE + "/?lang=" + lang)
            self.end_headers()
            return
        if not ok_sid(self):
            self.send_html(self.login_form(lang))
            return
        if path.startswith("/room/"):
            room = urllib.parse.unquote(path[len("/room/"):])
            self.send_html(self.page(lang, "social", self.render_social(lang, ls(), room)))
            return
        tab = p.get("tab", ["overview"])[0]
        self.send_html(self.render_page(tab, lang, cfg))

    def do_POST(self):
        if not self.path.startswith(BASE):
            self.send_html("404 Not Found", 404)
            return
        n = int(self.headers.get("Content-Length", "0"))
        raw = self.rfile.read(n).decode("utf-8")
        b = urllib.parse.parse_qs(raw)
        cfg = lc()
        lang = b.get("lang", ["zh"])[0]
        if not ok_ip(self, cfg):
            self.send_html("Forbidden", 403)
            return
        if not ok_sid(self):
            if b.get("user", [""])[0] == cfg["admin_user"] and b.get("pass", [""])[0] == cfg["admin_pass"]:
                s = secrets.token_hex(16)
                SESS[s] = time.time() + 86400
                self.send_response(302)
                self.send_header("Set-Cookie", "sid=%s; Path=/; HttpOnly" % s)
                self.send_header("Location", BASE + "/?tab=overview&lang=" + lang)
                self.end_headers()
                return
            ipk = ip(self)
            now = time.time()
            arr = [x for x in LOGIN_FAIL.get(ipk, []) if now - x < 300]
            if len(arr) >= 10:
                self.send_html(self.login_form(lang, "<p>尝试过于频繁，请5分钟后再试</p>"), 429)
                return
            arr.append(now)
            LOGIN_FAIL[ipk] = arr
            self.send_html(self.login_form(lang, "<p>%s</p>" % T("wrong_creds", lang)))
            return
        cmd = b.get("cmd", [""])[0]
        if cmd == "kick":
            push_command({"kick": b.get("uid", [""])[0]})
        elif cmd == "delroom":
            push_command({"delete_room": b.get("room_id", [""])[0]})
        elif cmd == "create_room":
            rid = b.get("room_id", [""])[0].strip()
            if rid:
                try:
                    mx = int(b.get("max_players", ["8"])[0])
                except Exception:
                    mx = 8
                push_command({"create_room": rid, "max_players": mx})
        elif cmd == "announce":
            push_command({"announce": {"title": b.get("title", [""])[0], "content": b.get("content", [""])[0]}})
        elif cmd == "broadcast":
            push_command({"broadcast": b.get("text", [""])[0], "target": b.get("target", ["all"])[0]})
        elif cmd == "blacklist_add":
            typ = b.get("type", [""])[0]
            value = b.get("value", [""])[0].strip()
            hours = int(b.get("hours", ["0"])[0])
            until = int(time.time()) + hours * 3600 if hours > 0 else 0
            if typ in ("uid", "name", "ip") and value:
                push_command({"blacklist_add": {"type": typ, "value": value, "until": until}})
        elif cmd == "blacklist_remove":
            push_command({"blacklist_remove": {"type": b.get("type", [""])[0], "value": b.get("value", [""])[0]}})
        elif cmd == "save_mods":
            self.save_mods(b, cfg)
        elif cmd == "save_settings":
            self.save_settings(b, cfg)
        self.send_response(302)
        self.send_header("Location", BASE + "/?tab=%s&lang=%s" % (b.get("tab", ["overview"])[0], lang))
        self.end_headers()

    def save_mods(self, b, cfg):
        mods = []
        existing = cfg.get("mods", []) or []
        for i in range(len(existing)):
            if b.get("delete_%d" % i, ["0"])[0] == "1":
                continue
            name = b.get("name_%d" % i, [""])[0].strip()
            if not name:
                continue
            checks = [x.strip() for x in b.get("checks_%d" % i, [""])[0].splitlines() if x.strip()]
            files = [x.strip() for x in b.get("files_%d" % i, [""])[0].splitlines() if x.strip()]
            mods.append({"name": name, "required": b.get("required_%d" % i, ["0"])[0] == "1", "files": files, "checks": checks})
        new_name = b.get("new_name", [""])[0].strip()
        if new_name:
            checks = [x.strip() for x in b.get("new_checks", [""])[0].splitlines() if x.strip()]
            files = [x.strip() for x in b.get("new_files", [""])[0].splitlines() if x.strip()]
            mods.append({"name": new_name, "required": b.get("new_required", ["0"])[0] == "1", "files": files, "checks": checks})
        cfg["mods"] = mods
        save_cfg(cfg)

    def save_settings(self, b, cfg):
        def i(x, default):
            try:
                return int(b.get(x, [str(default)])[0])
            except Exception:
                return default
        new = dict(cfg)
        new["server_name"] = b.get("server_name", [cfg.get("server_name", "")])[0]
        new["server_desc"] = b.get("server_desc", [cfg.get("server_desc", "")])[0]
        new["server_password"] = b.get("server_password", [cfg.get("server_password", "")])[0]
        new["port"] = i("port", cfg.get("port", 7000))
        new["max_online"] = i("max_online", cfg.get("max_online", 100))
        new["max_rooms"] = i("max_rooms", cfg.get("max_rooms", 50))
        new["room_max_players"] = i("room_max_players", cfg.get("room_max_players", 8))
        new["admin_uids"] = [u for u in b.get("admin_uids", [""])[0].split(",") if u.strip()]
        new["lan_only"] = b.get("lan_only", ["0"])[0] in ("1", "on", "true")
        save_cfg(new)
        restart_keys = ("port", "max_online", "max_rooms", "room_max_players", "admin_uids", "lan_only")
        if any(new.get(k) != cfg.get(k) for k in restart_keys):
            subprocess.run(["systemctl", "restart", "sfm-relay"])

HTTPServer((ADMIN_HOST, ADMIN_PORT), H).serve_forever()