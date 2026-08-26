import json, os, socket, ipaddress, socketserver, threading, time, urllib.request, urllib.error, hashlib, hmac, base64, random, zlib, struct, ssl, re, unicodedata, sys
from collections import defaultdict, deque
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.backends import default_backend



CFG_PATH = os.environ.get("SFM_CFG", "/opt/sfm-relay/config.json")
CMD_PATH = os.environ.get("SFM_CMD", "/opt/sfm-relay/commands.json")
STATE_PATH = os.environ.get("SFM_STATE", "/opt/sfm-relay/state.json")
LOG_PATH = os.environ.get("SFM_LOG", "/opt/sfm-relay/relay.log")

def load_cfg():
    with open(CFG_PATH, "r", encoding="utf-8") as f:
        return json.load(f)

def save_cfg(cfg):
    tmp = CFG_PATH + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
    os.replace(tmp, CFG_PATH)

def merge_cfg(patch):
    cfg = load_cfg()
    for k, v in patch.items():
        if v is None:
            cfg.pop(k, None)
        else:
            cfg[k] = v
    save_cfg(cfg)

CFG = load_cfg()
MASTER_REQUIRED = str(CFG.get("master_report", "")).strip()
if not MASTER_REQUIRED:
    print("[FATAL] master_report is required: the relay must share data with the master server to run. Please set master_report in config.json.", flush=True)
    sys.exit(1)
HOST = str(CFG.get("host", "0.0.0.0"))
PORT = int(CFG.get("port", 7000))
DOMAIN = str(CFG.get("domain", "wuwupuo.ccwu.cc"))
MASTER = str(CFG.get("master_report", "https://wuwupuo1.xtxt.xyz/public_html/sfm_api/relay_report.php"))
SECRET = str(CFG.get("secret", ""))
MAX_ONLINE = int(CFG.get("max_online", 100))
MAX_ROOMS = int(CFG.get("max_rooms", 50))
ROOM_MAX = int(CFG.get("room_max_players", 8))
HARD_ROOM_MAX = 10
EXT_RATE_LIMIT = int(CFG.get("ext_rate_limit", 200))  # 每 10 秒最多 200 条 mod 通信消息
STATE_TARGET_BPS = 200 * 1024
STATE_BURST_BPS = 1024 * 1024
ROOM_TIMEOUT = int(CFG.get("room_timeout", 120))
LAN_ONLY = bool(CFG.get("lan_only", False))

lock = threading.RLock()
rooms = {}
clients = {}
pubchat = []
announcement = {"title": "", "content": ""}
server_name = str(CFG.get("server_name", "SFM Relay"))
server_desc = str(CFG.get("server_desc", ""))
server_password = str(CFG.get("server_password", ""))
blacklist = []
mods = []
rejections = 0
socket_clients = {}


def load_chat_archive():
    try:
        with open(STATE_PATH, "r", encoding="utf-8") as f:
            saved = json.load(f).get("room_chat_archive", {})
        stamp = int(time.time())
        return {str(rid): item for rid, item in saved.items() if int(item.get("expires", 0)) > stamp}
    except Exception:
        return {}


room_chat_archive = load_chat_archive()


def load_chat_mutes():
    try:
        with open(STATE_PATH, "r", encoding="utf-8") as f:
            saved = json.load(f).get("chat_mutes", {})
        stamp = int(time.time())
        return {str(uid): item for uid, item in saved.items() if int(item.get("until", 0) or 0) > stamp}
    except Exception:
        return {}


chat_mutes = load_chat_mutes()

CAPTCHAS = {}
REPORT_ERROR_REPEAT = 300
_last_report_error = ""
_last_report_error_at = 0

DIGITS = {
    "0": ["01110","10001","10011","10101","11001","10001","01110"],
    "1": ["00100","01100","00100","00100","00100","00100","01110"],
    "2": ["01110","10001","00001","00010","00100","01000","11111"],
    "3": ["11111","00010","00100","00010","00001","10001","01110"],
    "4": ["00010","00110","01010","10010","11111","00010","00010"],
    "5": ["11111","10000","11110","00001","00001","10001","01110"],
    "6": ["00110","01000","10000","11110","10001","10001","01110"],
    "7": ["11111","00001","00010","00100","01000","01000","01000"],
    "8": ["01110","10001","10001","01110","10001","10001","01110"],
    "9": ["01110","10001","10001","01111","00001","00010","01100"],
}

def make_captcha():
    code = "".join(random.choice("0123456789") for _ in range(4))
    scale, cw, ch, margin, gap = 3, 5, 7, 4, 4
    img_w = margin * 2 + 4 * cw * scale + 3 * gap
    img_h = margin * 2 + ch * scale
    px = [[(240, 240, 240) for _ in range(img_w)] for _ in range(img_h)]
    for y in range(img_h):
        for x in range(img_w):
            if random.random() < 0.04:
                px[y][x] = (random.randint(0, 255),) * 3
    for idx, d in enumerate(code):
        glyph = DIGITS[d]
        x0 = margin + idx * (cw * scale + gap)
        y0 = margin + random.randint(0, img_h - margin - ch * scale)
        color = (random.randint(0, 90), random.randint(0, 90), random.randint(0, 90))
        for gy in range(ch):
            row = glyph[gy]
            for gx in range(cw):
                if row[gx] == "1":
                    for sy in range(scale):
                        for sx in range(scale):
                            y = y0 + gy * scale + sy
                            x = x0 + gx * scale + sx
                            if 0 <= y < img_h and 0 <= x < img_w:
                                px[y][x] = color
    raw = bytearray()
    for y in range(img_h):
        raw.append(0)
        for x in range(img_w):
            r, g, b = px[y][x]
            raw.extend([r, g, b])
    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        c += struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff)
        return c
    ihdr = struct.pack(">IIBBBBB", img_w, img_h, 8, 2, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(bytes(raw))) + chunk(b"IEND", b"")
    return code, base64.b64encode(png).decode()

IP_CONNS = {}
IP_MSG_TIMES = {}
MAX_CONN_PER_IP = 10
RATE_LIMIT = 4000
MAX_FRAME_BYTES = 256 * 1024
MAX_BUFFER_BYTES = 512 * 1024
MAX_CHAT_CHARS = 2000
MAX_SYNC_BYTES = 96 * 1024
CHAT_ARCHIVE_SECONDS = 24 * 60 * 60
NAME_RE = re.compile(r"^[A-Za-z0-9\u4e00-\u9fff]{1,20}$")
CHAT_PUNCTUATION = set(" \t\r\n，。！？、；：,.!?;:'()[]{}<>《》【】+-_=@#%&*/\\|~^$")

def now():
    return int(time.time())


def valid_name(value):
    return bool(NAME_RE.fullmatch(str(value or "").strip()))


def normalize_chat(value):
    text = unicodedata.normalize("NFKC", str(value or ""))
    text = text.replace("\u200b", "").replace("\u200c", "").replace("\u200d", "").replace("\ufeff", "")
    out = []
    for ch in text:
        code = ord(ch)
        if code < 32 and ch not in " \t\r\n":
            continue
        if ("a" <= ch.lower() <= "z") or ch.isdigit() or 0x4E00 <= code <= 0x9FFF or ch in CHAT_PUNCTUATION:
            out.append(ch)
        if len(out) >= MAX_CHAT_CHARS:
            break
    return "".join(out).replace("\t", " ").replace("\r", " ").replace("\n", " ").strip()


BLOCKED_CHAT_TERMS = tuple(
    "赌博 博彩 赌场 下注 彩金 棋牌 时时彩 บาคาร่า 六合 网赌 网堵 洗钱 跑分 加微信 微信号 加qq qq群 群号 扣扣群 私聊我 联系我 推广 引流 兼职 刷单 外挂 出售账号 账号交易 裸聊 约炮 http https www telegram discord whatsapp casino betting gambling freebonus joinmygroup contactme 傻逼 煞笔 沙比 脑残 狗东西 贱人 畜生 王八蛋 婊子 fuckyou fuckoff motherfucker asshole bitch bastard idiot moron retard killyourself nigger chink".split()
)


def moderation_key(value):
    text = unicodedata.normalize("NFKC", str(value or "")).lower()
    return "".join(ch for ch in text if ("a" <= ch <= "z") or ch.isdigit() or 0x4E00 <= ord(ch) <= 0x9FFF)


MUTE_LEVELS = [11 * 3600, 24 * 3600, 7 * 24 * 3600, 30 * 24 * 3600, 3650 * 24 * 3600]


def term_pattern(term):
    key = moderation_key(term)
    if not key or len(key) < 2:
        return None
    sep = r"[\s\u200b\u200c\u200d\ufeff]*"
    inner = sep.join(re.escape(ch) for ch in key)
    if key.isascii() and key.isalnum():
        return re.compile(r"(?<![a-z0-9])" + inner + r"(?![a-z0-9])", re.IGNORECASE)
    return re.compile(inner, re.IGNORECASE)


_TERM_PATTERNS_CACHE = None


def term_patterns():
    global _TERM_PATTERNS_CACHE
    if _TERM_PATTERNS_CACHE is None:
        _TERM_PATTERNS_CACHE = sorted(
            ((term, term_pattern(term)) for term in BLOCKED_CHAT_TERMS),
            key=lambda pair: -len(moderation_key(pair[0])),
        )
    return _TERM_PATTERNS_CACHE


def analyze_chat(value):
    # Longest-first matching: an embedded shorter keyword inside a longer one
    # counts only once (e.g. 他妈的 counts once, not 他妈的+妈的).
    text = str(value or "")
    hits = []
    remaining = text
    for term, pattern in term_patterns():
        if pattern is None:
            continue
        if pattern.search(remaining):
            hits.append(term)
            remaining = pattern.sub("*" * len(moderation_key(term)), remaining, count=1)
    masked = text
    for term, pattern in term_patterns():
        if pattern is None:
            continue
        masked = pattern.sub("*" * len(moderation_key(term)), masked)
    return hits, masked


def chat_mute_state(uid):
    key = str(uid)
    item = chat_mutes.get(key)
    if not item:
        return None
    until = int(item.get("until", 0) or 0)
    if until <= now():
        chat_mutes.pop(key, None)
        return None
    return item


def escalate_chat_mute(uid):
    key = str(uid)
    item = chat_mutes.get(key, {})
    level = int(item.get("level", 0) or 0)
    duration = MUTE_LEVELS[min(level, len(MUTE_LEVELS) - 1)]
    until = now() + duration
    chat_mutes[key] = {"level": level + 1, "until": until}
    persist_state()
    return until, level + 1


def format_mute_remaining(until):
    remain = max(0, int(until) - now())
    if remain <= 0:
        return "已到期"
    if remain >= 86400:
        return "%d 天" % (remain // 86400)
    if remain >= 3600:
        return "%d 小时" % (remain // 3600)
    return "%d 分钟" % max(1, remain // 60)


def compact_int_list(value, limit=8):
    out = []
    if not isinstance(value, list):
        return out
    for item in value[:limit]:
        try:
            out.append(int(item))
        except (TypeError, ValueError):
            out.append(0)
    return out


def compact_float_list(value, limit=8):
    out = []
    if not isinstance(value, list):
        return out
    for item in value[:limit]:
        try:
            out.append(round(max(-1000.0, min(1000.0, float(item))), 3))
        except (TypeError, ValueError):
            out.append(0.0)
    return out

def is_private_ip(ip):
    try:
        return ipaddress.ip_address(str(ip)).is_private
    except Exception:
        return False

def reload_dynamic():
    global server_name, server_desc, server_password, announcement, blacklist, mods
    try:
        c = load_cfg()
        server_name = str(c.get("server_name", "SFM Relay"))
        server_desc = str(c.get("server_desc", ""))
        server_password = str(c.get("server_password", ""))
        mods = c.get("mods", []) or []
        a = c.get("announcement", {"title": "", "content": ""})
        announcement = a if isinstance(a, dict) else {"title": "", "content": ""}
        blacklist = []
        for b in (c.get("blacklist", []) or []):
            if isinstance(b, dict):
                blacklist.append({
                    "type": str(b.get("type", "")),
                    "value": str(b.get("value", "")),
                    "until": int(b.get("until", 0) or 0),
                })
    except Exception:
        pass

def is_blacklisted(uid, name, ip):
    n = now()
    for b in list(blacklist):
        if b["until"] and b["until"] < n:
            continue
        v = b["value"]
        if b["type"] == "uid" and str(uid) == v:
            return b
        if b["type"] == "name" and str(name) == v:
            return b
        if b["type"] == "ip" and str(ip) == v:
            return b
    return None

def enc_payload(key, obj):
    plaintext = json.dumps(obj, ensure_ascii=False).encode("utf-8")
    iv = os.urandom(16)
    pad = 16 - (len(plaintext) % 16)
    plaintext += bytes([pad]) * pad
    enc = Cipher(algorithms.AES(key), modes.CBC(iv), backend=default_backend()).encryptor()
    ct = enc.update(plaintext) + enc.finalize()
    mac = hmac.new(key, iv + ct, hashlib.sha256).digest()
    return base64.b64encode(iv + ct + mac).decode()

def dec_payload(key, b64):
    data = base64.b64decode(b64)
    iv, ct, mac = data[:16], data[16:-32], data[-32:]
    if not hmac.compare_digest(hmac.new(key, iv + ct, hashlib.sha256).digest(), mac):
        raise ValueError("bad mac")
    dec = Cipher(algorithms.AES(key), modes.CBC(iv), backend=default_backend()).decryptor()
    plaintext = dec.update(ct) + dec.finalize()
    pad = plaintext[-1]
    plaintext = plaintext[:-pad]
    return json.loads(plaintext.decode("utf-8"))

def client_for_socket(fd):
    uid = socket_clients.get(id(fd))
    c = clients.get(uid) if uid is not None else None
    return c if c and c.get("sock") is fd else None


def send_plain(fd, o):
    try:
        payload = (json.dumps(o, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
        c = client_for_socket(fd)
        send_lock = c.get("_send_lock") if c else None
        if send_lock:
            with send_lock:
                fd.sendall(payload)
        else:
            fd.sendall(payload)
    except Exception:
        pass


def send(fd, o):
    try:
        c = client_for_socket(fd)
        key = c.get("key") if c else None
        envelope = {"e": enc_payload(key, o)} if key else o
        payload = (json.dumps(envelope, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
        send_lock = c.get("_send_lock") if c else None
        if send_lock:
            with send_lock:
                fd.sendall(payload)
        else:
            fd.sendall(payload)
    except Exception:
        pass


def broadcast_all(o, ex=None):
    with lock:
        recipients = [(u, c) for u, c in clients.items() if u != ex]
    for _, c in recipients:
        send(c["sock"], o)


def broadcast_room(rid, o, ex=None):
    with lock:
        room = rooms.get(rid)
        member_ids = list(room.get("players", {}).keys()) if room else []
        recipients = [(u, clients.get(u)) for u in member_ids if u != ex]
    for _, c in recipients:
        if c and c.get("room") == rid:
            send(c["sock"], o)

def allow_state_snapshot(rid, uid, payload):
    # Average optimization target is 200 KB/s per room, not a disconnecting hard cap.
    # A busy room may burst to 1 MB/s; even beyond that it keeps at least 5 Hz snapshots.
    r, c = rooms.get(rid), clients.get(uid)
    if not r or not c:
        return False
    stamp = time.monotonic()
    try:
        size = len(json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")) + 96
    except Exception:
        size = 1024
    avg = float(r.get("_avg_state_size", size)) * 0.9 + size * 0.1
    r["_avg_state_size"] = avg
    members, recipients = max(1, len(r.get("players", {}))), max(1, len(r.get("players", {})) - 1)
    hz = max(8.0, min(25.0, STATE_TARGET_BPS / max(1.0, avg * members * recipients)))
    at = float(r.get("_bw_at", stamp))
    if stamp - at >= 1.0:
        r["_bw_at"], r["_bw_bytes"] = stamp, 0
    if int(r.get("_bw_bytes", 0)) >= STATE_BURST_BPS:
        hz = 5.0
    if stamp - float(c.get("last_state_at", 0.0)) < 1.0 / hz:
        return False
    c["last_state_at"] = stamp
    r["_bw_bytes"] = int(r.get("_bw_bytes", 0)) + size * recipients
    return True

def room_summary():
    return [{"room_id": rid, "players": len(r["players"]), "max": min(HARD_ROOM_MAX, int(r["max"])), "has_password": 1 if r.get("password") else 0} for rid, r in rooms.items()]

def room_details():
    return [{"room_id": rid, "players": [{"uid": u, "name": n} for u, n in r["players"].items()], "max": r["max"]} for rid, r in rooms.items()]

def state():
    with lock:
        return {
            "server_name": server_name,
            "server_desc": server_desc,
            "announcement": announcement,
            "online": len(clients),
            "users": [{"uid": u, "name": c["name"], "ip": c.get("ip", ""), "room": c.get("room", "")} for u, c in clients.items()],
            "rooms": [{"room_id": rid, "players": [{"uid": u, "name": n} for u, n in r["players"].items()], "max": r["max"], "chat": r["chat"]} for rid, r in rooms.items()],
            "pubchat": pubchat[-100:],
            "room_chat_archive": room_chat_archive,
            "chat_mutes": chat_mutes,
        }

def archive_room_chat(rid, room):
    chat = list((room or {}).get("chat", []))[-100:]
    if chat:
        room_chat_archive[str(rid)] = {"expires": now() + CHAT_ARCHIVE_SECONDS, "chat": chat}


def cleanup_chat_archive():
    stamp = now()
    for rid in [rid for rid, item in room_chat_archive.items() if int(item.get("expires", 0)) <= stamp]:
        room_chat_archive.pop(rid, None)


def persist_state():
    tmp = STATE_PATH + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(state(), f, ensure_ascii=False)
    os.replace(tmp, STATE_PATH)

def log_event(kind, text):
    try:
        safe = str(text).replace("\r", "\\r").replace("\n", "\\n")
        with open(LOG_PATH, "a", encoding="utf-8") as f:
            f.write("[%s] %s %s\n" % (time.strftime("%Y-%m-%d %H:%M:%S"), kind, safe))
    except Exception:
        pass

def apply_command(c):
    global announcement
    if c.get("kick"):
        u = str(c["kick"])
        cu = clients.get(u)
        if cu:
            rid = cu.get("room", "")
            if rid and rid in rooms:
                rooms[rid]["players"].pop(u, None)
            send(cu["sock"], {"t": "kicked", "reason": str(c.get("reason", ""))})
            clients.pop(u, None)
            socket_clients.pop(id(cu["sock"]), None)
            try:
                cu["sock"].close()
            except Exception:
                pass
            if rid:
                broadcast_room(rid, {"t": "room_leave", "uid": u, "name": cu.get("name", "")}, u)
            log_event("kick", u)
    if c.get("delete_room"):
        rid = str(c["delete_room"])
        archive_room_chat(rid, rooms.get(rid))
        rooms.pop(rid, None)
        for u, cc in list(clients.items()):
            if cc.get("room") == rid:
                cc["room"] = ""
                send(cc["sock"], {"t": "room_closed", "room_id": rid})
        log_event("delete_room", rid)
    if c.get("create_room"):
        rid = str(c["create_room"]).strip()
        if rid and rid not in rooms:
            rooms[rid] = {"players": {}, "max": max(2, min(HARD_ROOM_MAX, ROOM_MAX, int(c.get("max_players", ROOM_MAX)))), "last": now(), "chat": []}
            log_event("create_room", rid)
    if c.get("announce"):
        a = c["announce"] or {}
        announcement = {"title": str(a.get("title", "")), "content": str(a.get("content", ""))}
        merge_cfg({"announcement": announcement})
        broadcast_all({"t": "announcement", "title": announcement["title"], "content": announcement["content"]})
        log_event("announce", announcement["title"])
    if c.get("broadcast"):
        text = normalize_chat(c["broadcast"])
        target = str(c.get("target", "all"))
        msg = {"t": "pub_chat", "uid": "0", "name": "官方", "text": text, "ts": now()}
        with lock:
            pubchat.append(msg)
            del pubchat[:-100]
        if target == "all":
            broadcast_all(msg)
        else:
            broadcast_room(target, msg)
        log_event("broadcast", target + " " + text)
    if c.get("blacklist_add"):
        b = c["blacklist_add"] or {}
        typ = str(b.get("type", ""))
        value = str(b.get("value", ""))
        until = int(b.get("until", 0) or 0)
        if typ and value:
            cfg = load_cfg()
            bl = [x for x in (cfg.get("blacklist", []) or []) if not (x.get("type") == typ and x.get("value") == value)]
            bl.append({"type": typ, "value": value, "until": until})
            merge_cfg({"blacklist": bl})
            reload_dynamic()
            log_event("blacklist_add", typ + " " + value)
    if c.get("blacklist_remove"):
        b = c["blacklist_remove"] or {}
        typ = str(b.get("type", ""))
        value = str(b.get("value", ""))
        if typ and value:
            cfg = load_cfg()
            bl = [x for x in (cfg.get("blacklist", []) or []) if not (x.get("type") == typ and x.get("value") == value)]
            merge_cfg({"blacklist": bl})
            reload_dynamic()
            log_event("blacklist_remove", typ + " " + value)

def process_commands():
    if not os.path.exists(CMD_PATH):
        return
    try:
        with open(CMD_PATH, "r", encoding="utf-8") as f:
            cmds = json.load(f)
        os.remove(CMD_PATH)
        for c in (cmds if isinstance(cmds, list) else [cmds]):
            try:
                apply_command(c)
            except Exception as e:
                log_event("cmd_err", str(e))
    except Exception as e:
        log_event("cmd_err", str(e))

def master_call(op, payload, timeout=8):
    if not MASTER.lower().startswith("https://"):
        raise ValueError("master_report must use https")
    body = dict(payload or {})
    body["op"] = str(op)
    raw = json.dumps(body, ensure_ascii=False, separators=(",", ":"), sort_keys=True).encode("utf-8")
    stamp = str(now())
    nonce = os.urandom(16).hex()
    signature = hmac.new(SECRET.encode("utf-8"), (stamp + "\n" + nonce + "\n").encode("utf-8") + raw, hashlib.sha256).hexdigest()
    req = urllib.request.Request(MASTER, data=raw, headers={
        "Content-Type": "application/json",
        "User-Agent": "SFMRelay/4.1.14",
        "X-SFM-Timestamp": stamp,
        "X-SFM-Nonce": nonce,
        "X-SFM-Signature": signature,
    })
    try:
        with urllib.request.urlopen(req, timeout=timeout, context=ssl.create_default_context()) as response:
            reply = response.read(262144).decode("utf-8", "replace")
    except urllib.error.HTTPError as exc:
        reply = exc.read(262144).decode("utf-8", "replace")
    result = json.loads(reply)
    if not isinstance(result, dict):
        raise ValueError("invalid master response")
    return result


def report():
    global _last_report_error, _last_report_error_at
    n = now()
    banned = sum(1 for b in blacklist if not b["until"] or b["until"] > n)
    payload = {
        "domain": DOMAIN,
        "online": len(clients),
        "rooms": room_summary(),
        "rooms_detail": room_details(),
        "max_online": MAX_ONLINE,
        "max_rooms": MAX_ROOMS,
        "server_name": server_name,
        "server_desc": server_desc,
        "port": PORT,
        "has_password": 1 if server_password else 0,
        "banned": banned,
        "rejected": rejections,
        "ts": n,
    }
    try:
        result = master_call("report", payload, 10)
        if int(result.get("code", -1)) != 0:
            raise ValueError("master rejected report: " + str(result.get("msg", "unknown")))
        if _last_report_error:
            log_event("report_ok", "master report recovered")
        _last_report_error = ""
        _last_report_error_at = 0
    except Exception as e:
        message = str(e)
        if message != _last_report_error or n - _last_report_error_at >= REPORT_ERROR_REPEAT:
            log_event("report_err", message)
            _last_report_error = message
            _last_report_error_at = n


def reporter():
    last_maintenance = 0
    last_report = 0
    while True:
        time.sleep(5)
        stamp = now()
        try:
            process_commands()
            if stamp - last_maintenance >= 5:
                last_maintenance = stamp
                reload_dynamic()
                cleanup_chat_archive()
                persist_state()
        except Exception as e:
            log_event("loop_err", str(e))
        if stamp - last_report >= 30:
            last_report = stamp
            try:
                report()
            except Exception as e:
                log_event("report_err", str(e))


class Handler(socketserver.BaseRequestHandler):
    def setup(self):
        self.buf = b""
        self.uid = None
        self.name = ""
        self.key = None
        self.peer_ip = self.client_address[0] if self.client_address else ""
        self.rate_times = defaultdict(deque)
        self.abusive = False
        try:
            self.request.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            self.request.setsockopt(socket.SOL_SOCKET, socket.SO_KEEPALIVE, 1)
        except Exception:
            pass

    def allow_rate(self, bucket, limit, window):
        stamp = time.monotonic()
        times = self.rate_times[bucket]
        while times and stamp - times[0] >= window:
            times.popleft()
        if len(times) >= limit:
            return False
        times.append(stamp)
        return True

    def handle(self):
        self.request.settimeout(60)  # 客户端每30秒心跳，60秒无流量视为掉线
        newline = bytes([10])
        while not self.abusive:
            try:
                d = self.request.recv(8192)
            except Exception:
                break
            if not d:
                break
            self.buf += d
            if len(self.buf) > MAX_BUFFER_BYTES:
                send(self.request, {"t": "rejected", "code": "frame_too_large", "msg": "消息过大"})
                break
            while newline in self.buf:
                line, self.buf = self.buf.split(newline, 1)
                frame_ok = len(line) <= MAX_FRAME_BYTES
                if not frame_ok or not self.allow_rate("all", RATE_LIMIT, 10.0):
                    # 流量过大时只丢弃/警告，不再直接踢下线。
                    stamp = time.time()
                    if frame_ok and stamp - getattr(self, "_rate_warn_at", 0.0) >= 1.0:
                        self._rate_warn_at = stamp
                        send(self.request, {"t": "rejected", "code": "rate_limited", "msg": "消息过大或发送过快"})
                    elif not frame_ok:
                        self._bad_frames = getattr(self, "_bad_frames", 0) + 1
                        if self._bad_frames >= 20:
                            send(self.request, {"t": "rejected", "code": "frame_too_large", "msg": "消息过大"})
                            self.abusive = True
                            break
                    continue
                try:
                    raw = json.loads(line.decode("utf-8"))
                    if self.key and isinstance(raw, dict) and "e" in raw:
                        raw = dec_payload(self.key, raw["e"])
                    if not isinstance(raw, dict):
                        raise ValueError("bad message")
                    size = len(json.dumps(raw, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
                    if size > MAX_FRAME_BYTES:
                        raise ValueError("message too large")
                    self.dispatch(raw)
                except Exception:
                    if self.allow_rate("errors", 5, 10.0):
                        send(self.request, {"t": "err", "m": "消息格式错误"})
        self.bye()
    def dispatch(self, m):
        t = m.get("t")
        if t == "whoami":
            if not self.allow_rate("whoami", 3, 60.0):
                send_plain(self.request, {"t": "rejected", "code": "rate_limited", "msg": "请求过于频繁"})
                self.abusive = True
                return
            send_plain(self.request, {"t": "ip", "ip": self.peer_ip})
            return
        if t == "hello":
            self.do_hello(m)
            return
        if self.uid is None:
            send(self.request, {"t": "rejected", "code": "not_hello", "msg": "连接验证尚未完成，请重新连接服务器"})
            return
        current = clients.get(self.uid)
        if current is None or current.get("sock") is not self.request:
            send_plain(self.request, {"t": "rejected", "code": "duplicate_login", "msg": "\u8be5\u8d26\u53f7\u5df2\u5728\u5176\u4ed6\u8fde\u63a5\u767b\u5f55"})
            try:
                self.request.shutdown(socket.SHUT_RDWR)
            except Exception:
                pass
            return
        current["last_seen"] = now()
        if t in ("pub_chat", "chat") and not self.allow_rate("chat", 2, 5.0):
            send(self.request, {"t": "err", "m": "发送过快，请稍后"})
            return
        if t in ("toy_invite", "toy_accept", "toy_reject", "toy_revoke", "toy_control", "room_kick") and not self.allow_rate("control", 60, 10.0):
            send(self.request, {"t": "err", "m": "操作过于频繁"})
            return
        if t == "captcha" and not self.allow_rate("captcha", 10, 60.0):
            send(self.request, {"t": "err", "m": "验证码请求过于频繁"})
            return
        if t == "ping":
            send(self.request, {"t": "pong", "online": len(clients), "max_online": MAX_ONLINE, "rooms": room_summary(), "server_name": server_name})
        elif t == "pub_chat":
            self.do_pub_chat(m)
        elif t == "captcha":
            self.do_captcha()
        elif t == "room_list":
            send(self.request, {"t": "room_list", "rooms": room_summary()})
        elif t == "room_create":
            self.do_room_create(m)
        elif t == "room_join":
            self.do_room_join(m)
        elif t == "room_leave":
            self.leave()
        elif t == "room_kick":
            self.do_room_kick(m)
        elif t == "toy_invite":
            self.do_toy_invite(m)
        elif t == "toy_accept":
            self.do_toy_accept(m)
        elif t == "toy_reject":
            self.do_toy_reject(m)
        elif t == "toy_revoke":
            self.do_toy_revoke()
        elif t == "toy_control":
            self.do_toy_control(m)
        elif t == "pos":
            self.do_pos(m)
        elif t == "npc_sync":
            self.do_npc_sync(m)
        elif t == "state_sync":
            rid = clients.get(self.uid, {}).get("room", "")
            if rid and allow_state_snapshot(rid, self.uid, m):
                m["uid"] = self.uid
                broadcast_room(rid, m, self.uid)
        elif t == "motion_sync":
            rid = clients.get(self.uid, {}).get("room", "")
            c = clients.get(self.uid)
            stamp = time.time()
            room = rooms.get(rid, {})
            player_count = len(room.get("players", {}))
            motion_hz = 16.0 if player_count <= 3 else (12.0 if player_count <= 5 else (10.0 if player_count <= 7 else 8.0))
            signature = (
                1 if m.get("moving") else 0,
                1 if m.get("crouch") else 0,
                1 if m.get("strafe") else 0,
                1 if m.get("dash") else 0,
                int(m.get("act", -1)),
                int(m.get("hash", 0)),
            )
            urgent = c is not None and signature != c.get("last_motion_sig")
            if rid and c and (urgent or stamp - float(c.get("last_motion_at", 0.0)) >= 1.0 / motion_hz):
                c["last_motion_at"] = stamp
                c["last_motion_sig"] = signature
                payload = {
                    "t": "motion_sync", "uid": self.uid, "slot": int(m.get("slot", 0)),
                    "vx": round(max(-9.0, min(9.0, float(m.get("vx", 0)))), 3),
                    "vz": round(max(-9.0, min(9.0, float(m.get("vz", 0)))), 3),
                    "ry": round(float(m.get("ry", 0)) % 360.0, 2),
                    "moving": signature[0],
                    "crouch": signature[1],
                    "strafe": signature[2],
                    "dash": signature[3],
                    "act": signature[4], "hash": signature[5],
                    "ms": round(max(-10.0, min(10.0, float(m.get("ms", 0)))), 3),
                    "lms": round(max(-10.0, min(10.0, float(m.get("lms", 1)))), 3),
                    "sx": round(max(-2.0, min(2.0, float(m.get("sx", 0)))), 3),
                    "sy": round(max(-2.0, min(2.0, float(m.get("sy", 0)))), 3),
                    "hpx": round(max(-100.0, min(100.0, float(m.get("hpx", 0)))), 3),
                    "hpy": round(max(-100.0, min(100.0, float(m.get("hpy", 0)))), 3),
                    "hpz": round(max(-100.0, min(100.0, float(m.get("hpz", 0)))), 3),
                    "hrx": round(max(-2.0, min(2.0, float(m.get("hrx", 0)))), 4),
                    "hry": round(max(-2.0, min(2.0, float(m.get("hry", 0)))), 4),
                    "hrz": round(max(-2.0, min(2.0, float(m.get("hrz", 0)))), 4),
                    "hrw": round(max(-2.0, min(2.0, float(m.get("hrw", 1)))), 4),
                }
                if all(k in m for k in ("x", "y", "z")):
                    payload["x"] = round(max(-100000.0, min(100000.0, float(m.get("x", 0)))), 3)
                    payload["y"] = round(max(-100000.0, min(100000.0, float(m.get("y", 0)))), 3)
                    payload["z"] = round(max(-100000.0, min(100000.0, float(m.get("z", 0)))), 3)
                broadcast_room(rid, payload, self.uid)
        elif t == "bone_sync":
            rid = clients.get(self.uid, {}).get("room", "")
            bc = clients.get(self.uid)
            bone_stamp = time.time()
            if rid and bc and bone_stamp - float(bc.get("last_bone_at", 0.0)) >= 1.0 / 6.0:
                bc["last_bone_at"] = bone_stamp
                broadcast_room(rid, {"t": "bone_sync", "uid": self.uid, "slot": int(m.get("slot", 0)), "q": m.get("q", [])}, self.uid)
        elif t == "action_sync":
            rid = clients.get(self.uid, {}).get("room", "")
            ac = clients.get(self.uid)
            act_stamp = time.time()
            if rid and ac and act_stamp - float(ac.get("last_action_at", 0.0)) >= 1.0 / 8.0:
                ac["last_action_at"] = act_stamp
                broadcast_room(rid, {
                    "t": "action_sync", "uid": self.uid, "slot": int(m.get("slot", 0)),
                    "act": int(m.get("act", -1)), "hash": int(m.get("hash", 0)),
                    "aid": int(m.get("aid", -1)), "apm": int(m.get("apm", -1)),
                    "old": int(m.get("old", -1)),
                    "ami": round(max(-1000.0, min(1000.0, float(m.get("ami", 0)))), 3),
                    "lh": compact_int_list(m.get("lh")),
                    "lt": compact_float_list(m.get("lt")),
                    "lw": compact_float_list(m.get("lw")),
                }, self.uid)
        elif t == "appearance_request":
            rid = clients.get(self.uid, {}).get("room", "")
            if rid:
                broadcast_room(rid, {"t": "appearance_request", "uid": self.uid}, self.uid)
        elif t == "room_setting":
            self.do_room_setting(m)
        elif t == "ext" or (t and t.startswith("ext_")):
            # 通用 mod 同步通道（内建支持，无需插件）：
            #   ext_evt/ext_score/ext_sync/ext_bone/ext_trigger/ext_npc/ext_team/
            #   ext_announce/ext_countdown/ext_gift/ext_spectate/ext_achievement/
            #   ext_vote/ext_task/ext_room/ext ...
            # 带 "to" 字段 → 定向发送；否则房间广播（附 uid 供接收端识别来源）
            # t=="ext" 且 ns 匹配插件 → 优先走插件路由
            handled = False
            if t == "ext":
                ns = str(m.get("ns", "")).strip()
                if ns in PLUGINS:
                    try:
                        dispatch_ext(clients.get(self.uid, {}), m)
                        handled = True
                    except Exception as ex:
                        send(self.request, {"t": "err", "m": f"插件处理异常: {ex}"})
                        handled = True
            if not handled:
                self.do_ext_forward(m)
        elif t.startswith("game_"):
            send(self.request, {"t": "err", "m": "该玩法已移除"})
        elif t in ("chat", "sync", "action"):
            self.do_room_msg(t, m)
        else:
            send(self.request, {"t": "err", "m": "未知消息类型"})

    def do_hello(self, m):
        global rejections
        uid = str(m.get("uid", "")).strip()
        # Preserve the exact account name for the one-time master permit.
        # NFKC is display-only; changing it before validation breaks some names.
        permit_name = str(m.get("name", "")).strip()[:40]
        name = unicodedata.normalize("NFKC", permit_name)[:40]
        sid = str(m.get("token", "")).strip().lower()
        if not uid.isdigit() or not permit_name or any(ord(ch) < 32 for ch in permit_name) or not re.fullmatch(r"[a-f0-9]{32}", sid):
            rejections += 1
            send_plain(self.request, {"t": "rejected", "code": "bad_params", "msg": "账号参数或联机许可无效"})
            return
        if LAN_ONLY and not is_private_ip(self.peer_ip):
            rejections += 1
            send_plain(self.request, {"t": "rejected", "code": "lan_only", "msg": "仅允许局域网访问"})
            return
        b = is_blacklisted(uid, name, self.peer_ip)
        if b:
            rejections += 1
            send_plain(self.request, {"t": "rejected", "code": "blacklisted", "msg": "你已被封禁", "until": b["until"]})
            return
        if server_password and str(m.get("password", "")) != server_password:
            rejections += 1
            send_plain(self.request, {"t": "rejected", "code": "wrong_password", "msg": "服务器密码错误"})
            return
        with lock:
            if len(clients) >= MAX_ONLINE and uid not in clients:
                rejections += 1
                send_plain(self.request, {"t": "rejected", "code": "full", "msg": "服务器已满"})
                return
            if sum(1 for c in clients.values() if c.get("ip") == self.peer_ip) >= MAX_CONN_PER_IP and uid not in clients:
                rejections += 1
                send_plain(self.request, {"t": "rejected", "code": "too_many_conns", "msg": "同一IP连接数过多"})
                return
        try:
            permit = master_call("validate", {
                "domain": DOMAIN, "port": PORT, "sid": sid, "uid": int(uid),
                "name": permit_name, "client_ip": self.peer_ip,
            }, 8)
        except Exception as exc:
            rejections += 1
            log_event("permit_err", str(exc))
            send_plain(self.request, {"t": "rejected", "code": "master_unavailable", "msg": "总服许可校验暂不可用，请稍后重试"})
            return
        if int(permit.get("code", -1)) != 0:
            rejections += 1
            log_event("permit_rejected", "uid=%s ip=%s code=%s msg=%s" % (uid, self.peer_ip, permit.get("code"), permit.get("msg")))
            send_plain(self.request, {"t": "rejected", "code": "permit_rejected", "msg": str(permit.get("msg", "联机许可被拒绝"))})
            return
        name = str(permit.get("username", name))[:40]
        key = os.urandom(32)
        with lock:
            if len(clients) >= MAX_ONLINE and uid not in clients:
                send_plain(self.request, {"t": "rejected", "code": "full", "msg": "服务器已满"})
                return
            previous = clients.get(uid)
            previous_sock = previous.get("sock") if previous else None
            if previous_sock is not None:
                socket_clients.pop(id(previous_sock), None)
            clients[uid] = {"name": name, "ip": self.peer_ip, "room": "", "sock": self.request, "key": key, "_send_lock": threading.Lock(), "last_state_at": 0.0, "last_motion_at": 0.0, "last_bone_at": 0.0, "last_action_at": 0.0}
            socket_clients[id(self.request)] = uid
        try:
            confirmed = master_call("confirm", {"domain": DOMAIN, "sid": sid}, 8)
        except Exception as exc:
            confirmed = {"code": -1, "msg": str(exc)}
        if int(confirmed.get("code", -1)) != 0:
            with lock:
                current = clients.get(uid)
                if current and current.get("sock") is self.request:
                    clients.pop(uid, None)
                    socket_clients.pop(id(self.request), None)
                    if previous and previous_sock is not None:
                        clients[uid] = previous
                        socket_clients[id(previous_sock)] = uid
            rejections += 1
            log_event("confirm_err", str(confirmed.get("msg", "confirm failed")))
            send_plain(self.request, {"t": "rejected", "code": "confirm_failed", "msg": "总服未确认本次连接，已安全断开"})
            return
        if previous_sock is not None and previous_sock is not self.request:
            send_plain(previous_sock, {"t": "rejected", "code": "duplicate_login", "msg": "该账号已在新连接登录"})
            try:
                previous_sock.shutdown(socket.SHUT_RDWR)
                previous_sock.close()
            except Exception:
                pass
        self.uid = uid
        self.name = name
        self.key = key
        send_plain(self.request, {"t": "ok", "online": len(clients), "max_online": MAX_ONLINE, "server_name": server_name, "server_desc": server_desc, "announcement": announcement, "pubchat": pubchat[-50:], "mods": plugin_list(), "key": base64.b64encode(key).decode()})
        broadcast_all({"t": "presence", "uid": uid, "name": name, "online": len(clients)}, uid)

    def do_pub_chat(self, m):
        text = normalize_chat(m.get("text", m.get("d", "")))
        if not text:
            return
        mute = chat_mute_state(self.uid)
        if mute:
            send(self.request, {"t": "err", "m": "你已被禁言，剩余 " + format_mute_remaining(mute["until"])})
            return
        hits, masked = analyze_chat(text)
        if len(hits) >= 3:
            until, level = escalate_chat_mute(self.uid)
            log_event("chat_mute", "uid=%s ip=%s level=%s hits=%s" % (self.uid, self.peer_ip, level, ",".join(hits)))
            send(self.request, {"t": "err", "m": "消息含多个违规词，已禁言 " + format_mute_remaining(until)})
            return
        if hits:
            text = masked
            log_event("chat_masked", "uid=%s hits=%s" % (self.uid, ",".join(hits)))
        msg = {"t": "pub_chat", "uid": self.uid, "name": self.name, "text": text, "ts": now()}
        # 插件钩子：!命令 走 on_command，普通消息走 on_chat
        if text.startswith("!"):
            parts = text[1:].split(None, 1)
            cmd = parts[0].lower()
            args = parts[1] if len(parts) > 1 else ""
            plugin_handle_command(clients.get(self.uid, {}), cmd, args)
        else:
            if plugin_handle_chat(clients.get(self.uid, {}), text):
                return  # 插件消费了消息，不广播
        with lock:
            pubchat.append(msg)
            del pubchat[:-100]
        broadcast_all(msg)

    def do_captcha(self):
        code, b64 = make_captcha()
        CAPTCHAS[self.uid] = (code, now() + 300)
        send(self.request, {"t": "captcha", "image": "data:image/png;base64," + b64, "expire": 300})

    def do_room_create(self, m):
        got = str(m.get("captcha", "")).strip()
        issued = CAPTCHAS.pop(self.uid, None)
        expect, expires = issued if isinstance(issued, tuple) else (issued, 0)
        if not expect or (expires and now() > expires) or got != expect:
            send(self.request, {"t": "err", "m": "验证码错误或已过期"})
            return
        if len(rooms) >= MAX_ROOMS:
            send(self.request, {"t": "err", "m": "房间数量已达上限"})
            return
        rid = str(m.get("room_id", "")).strip()
        if not rid:
            rid = "".join(random.choice("ABCDEFGHJKLMNPQRSTUVWXYZ23456789") for _ in range(6))
        pwd = str(m.get("password", ""))[:40]
        mx = max(2, min(HARD_ROOM_MAX, ROOM_MAX, int(m.get("max_players", ROOM_MAX))))
        with lock:
            while rid in rooms:
                rid = "".join(random.choice("ABCDEFGHJKLMNPQRSTUVWXYZ23456789") for _ in range(6))
            rooms[rid] = {"players": {}, "max": mx, "last": now(), "chat": [], "password": pwd, "host": self.uid, "toy": {}, "allow_game_bonuses": 0}
            if self.uid in clients:
                clients[self.uid]["room"] = rid
                rooms[rid]["players"][self.uid] = clients[self.uid]["name"]
        send(self.request, {"t": "room_created", "room_id": rid, "max": mx, "has_password": 1 if pwd else 0, "host": self.uid, "players": [{"uid": self.uid, "name": self.name}], "allow_game_bonuses": 0})
        broadcast_all({"t": "room_list", "rooms": room_summary()}, self.uid)
        try:
            plugin_call_all("on_room_create", clients.get(self.uid, {}), rooms.get(rid, {}))
        except Exception:
            pass

    def do_room_join(self, m):
        rid = str(m.get("room_id", ""))
        r = rooms.get(rid)
        if not r:
            send(self.request, {"t": "err", "m": "房间不存在"})
            return
        if r.get("password") and str(m.get("password", "")) != r["password"]:
            send(self.request, {"t": "err", "m": "房间密码错误"})
            return
        if len(r["players"]) >= max(2, min(HARD_ROOM_MAX, int(r.get("max", HARD_ROOM_MAX)))):
            send(self.request, {"t": "err", "m": "房间已满（最多 10 人）"})
            return
        with lock:
            clients[self.uid]["room"] = rid
            r["players"][self.uid] = clients[self.uid]["name"]
            r["last"] = now()
        players = [{"uid": u, "name": n} for u, n in r["players"].items()]
        send(self.request, {"t": "room_joined", "room_id": rid, "host": r.get("host", ""), "players": players, "allow_game_bonuses": r.get("allow_game_bonuses", 0)})
        broadcast_room(rid, {"t": "room_player_join", "uid": self.uid, "name": self.name, "players": players}, self.uid)
        broadcast_all({"t": "room_list", "rooms": room_summary()}, self.uid)
        try:
            plugin_call_all("on_join", clients.get(self.uid, {}))
        except Exception:
            pass

    def do_room_kick(self, m):
        rid = clients.get(self.uid, {}).get("room", "")
        r = rooms.get(rid)
        if not r or r.get("host") != self.uid:
            send(self.request, {"t": "err", "m": "只有房主可以踢人"})
            return
        target = str(m.get("uid", ""))
        if target == self.uid or target not in r["players"]:
            return
        tc = clients.get(target)
        r["players"].pop(target, None)
        if tc:
            tc["room"] = ""
            send(tc["sock"], {"t": "kicked_from_room", "room_id": rid})
        broadcast_room(rid, {"t": "room_leave", "uid": target, "name": (tc or {}).get("name", "")}, target)
        broadcast_all({"t": "room_list", "rooms": room_summary()}, self.uid)

    def do_room_msg(self, t, m):
        rid = clients.get(self.uid, {}).get("room", "")
        r = rooms.get(rid)
        if not r:
            send(self.request, {"t": "err", "m": "不在房间"})
            return
        r["last"] = now()
        if t == "chat":
            text = normalize_chat(m.get("d", ""))
            if not text:
                return
            mute = chat_mute_state(self.uid)
            if mute:
                send(self.request, {"t": "err", "m": "你已被禁言，剩余 " + format_mute_remaining(mute["until"])})
                return
            hits, masked = analyze_chat(text)
            if len(hits) >= 3:
                until, level = escalate_chat_mute(self.uid)
                log_event("chat_mute", "uid=%s ip=%s level=%s hits=%s" % (self.uid, self.peer_ip, level, ",".join(hits)))
                send(self.request, {"t": "err", "m": "消息含多个违规词，已禁言 " + format_mute_remaining(until)})
                return
            include_self = False
            if hits:
                text = masked
                include_self = True
                log_event("chat_masked", "uid=%s hits=%s" % (self.uid, ",".join(hits)))
            msg = {"uid": self.uid, "name": self.name, "text": text, "ts": now()}
            with lock:
                r["chat"].append(msg)
                del r["chat"][:-100]
            broadcast_room(rid, {"t": t, "uid": self.uid, "name": self.name, "d": text}, None if include_self else self.uid)
        else:
            data = m.get("d")
            if len(json.dumps(data, ensure_ascii=False, separators=(",", ":")).encode("utf-8")) > MAX_SYNC_BYTES:
                return
            broadcast_room(rid, {"t": t, "uid": self.uid, "name": self.name, "d": data}, self.uid)
        try:
            plugin_call_all("on_room_msg", clients.get(self.uid, {}), t, m)
        except Exception:
            pass

    def do_ext_forward(self, m):
        """通用 mod 同步通道转发（内建支持，无需插件）。

        规则：
          - 带 "to" 字段 → 定向发给目标玩家（同房间）
          - 否则 → 广播给房间内其他玩家
          - 未进房间 → 仅回执错误
          - 限频：每客户端 10 秒内最多 EXT_RATE_LIMIT 条
        """
        rid = clients.get(self.uid, {}).get("room", "")
        if not self.allow_rate("ext", EXT_RATE_LIMIT, 10.0):
            return  # 超频静默丢弃，避免拖垮服务器
        if not rid:
            send(self.request, {"t": "err", "m": "mod 通信需要先加入房间"})
            return
        to = str(m.get("to", "")).strip()
        payload = dict(m)
        payload["uid"] = self.uid
        payload["name"] = self.name
        if to:
            tc = clients.get(to)
            if tc and tc.get("room") == rid:
                send(tc["sock"], payload)
            return
        broadcast_room(rid, payload, self.uid)

    def do_pos(self, m):
        rid = clients.get(self.uid, {}).get("room", "")
        if not rid:
            return
        try:
            clients[self.uid]["pos"] = (float(m.get("x") or 0), float(m.get("y") or 0), float(m.get("z") or 0))
        except Exception:
            pass
        broadcast_room(rid, {
            "t": "pos",
            "uid": self.uid,
            "x": m.get("x"),
            "y": m.get("y"),
            "z": m.get("z"),
            "ry": m.get("ry"),
            "stage": m.get("stage"),
        }, self.uid)

    def do_npc_sync(self, m):
        rid = clients.get(self.uid, {}).get("room", "")
        r = rooms.get(rid)
        if not r:
            return
        stage = int(m.get("stage", -1))
        npcs = list(m.get("npcs", []))[:20]
        c = clients.get(self.uid)
        if c:
            c["npc_stage"] = stage
            c["npcs"] = npcs
        # 权威玩家：第一个进入该地图的人优先；房主同图时仍可接管
        if "npc_first" not in r:
            r["npc_first"] = {}
        if stage not in r["npc_first"]:
            r["npc_first"][stage] = self.uid
        authoritative = None
        first = r["npc_first"].get(stage)
        if first and first in r.get("players", {}):
            fc = clients.get(first)
            if fc and fc.get("npc_stage") == stage:
                authoritative = first
        if authoritative is None:
            host = r.get("host", "")
            if host and host in r.get("players", {}):
                hc = clients.get(host)
                if hc and hc.get("npc_stage") == stage:
                    authoritative = host
        if authoritative is None:
            for u in r.get("players", {}):
                uc = clients.get(u)
                if uc and uc.get("npc_stage") == stage:
                    authoritative = u
                    break
        if authoritative == self.uid:
            broadcast_room(rid, {"t": "npc_state", "stage": stage, "uid": self.uid, "npcs": npcs})

    def do_room_setting(self, m):
        rid = clients.get(self.uid, {}).get("room", "")
        r = rooms.get(rid)
        if not r or r.get("host") != self.uid:
            return
        changed = False
        if "allow_game_bonuses" in m:
            r["allow_game_bonuses"] = 1 if m.get("allow_game_bonuses") else 0
            changed = True
        if changed:
            broadcast_room(rid, {"t": "room_settings",
                                "allow_game_bonuses": r.get("allow_game_bonuses", 0)})

    def do_toy_invite(self, m):
        rid = clients.get(self.uid, {}).get("room", "")
        r = rooms.get(rid)
        if not r:
            return
        target = str(m.get("to", ""))
        if target == self.uid or target not in r["players"]:
            send(self.request, {"t": "err", "m": "目标不在房间"})
            return
        tc = clients.get(target)
        if tc:
            send(tc["sock"], {"t": "toy_invite", "from": self.uid, "from_name": self.name})

    def do_toy_accept(self, m):
        rid = clients.get(self.uid, {}).get("room", "")
        r = rooms.get(rid)
        if not r:
            return
        controller = str(m.get("from", ""))
        if controller not in r["players"]:
            return
        # 一个人只能授权给一个控制者；一个控制者最多控制 5 人
        existing = r.get("toy", {}).get(self.uid)
        if existing and existing != controller:
            send(self.request, {"t": "err", "m": "你已授权给其他控制者"})
            return
        if sum(1 for ctl in r.get("toy", {}).values() if ctl == controller) >= 5:
            send(self.request, {"t": "err", "m": "控制人数已达上限 5 人"})
            return
        with lock:
            r["toy"][self.uid] = controller
        cc = clients.get(controller)
        if cc:
            send(cc["sock"], {"t": "toy_accepted", "to": self.uid, "to_name": self.name})
        broadcast_room(rid, {"t": "toy_link", "controller": controller, "target": self.uid})

    def do_toy_reject(self, m):
        rid = clients.get(self.uid, {}).get("room", "")
        r = rooms.get(rid)
        if not r:
            return
        controller = str(m.get("from", ""))
        cc = clients.get(controller)
        if cc:
            send(cc["sock"], {"t": "toy_rejected", "to": self.uid, "to_name": self.name})

    def do_toy_revoke(self):
        rid = clients.get(self.uid, {}).get("room", "")
        r = rooms.get(rid)
        if not r:
            return
        toy = r.get("toy", {})
        controller = toy.pop(self.uid, None)
        if controller:
            cc = clients.get(controller)
            if cc:
                send(cc["sock"], {"t": "toy_revoked", "to": self.uid, "to_name": self.name})
            broadcast_room(rid, {"t": "toy_revoked", "controller": controller, "target": self.uid})
        for tgt in [x for x, ctl in toy.items() if ctl == self.uid]:
            toy.pop(tgt, None)
            tc = clients.get(tgt)
            if tc:
                send(tc["sock"], {"t": "toy_revoked", "controller": self.uid, "target": tgt})
            broadcast_room(rid, {"t": "toy_revoked", "controller": self.uid, "target": tgt})

    def do_toy_control(self, m):
        rid = clients.get(self.uid, {}).get("room", "")
        r = rooms.get(rid)
        if not r:
            return
        target = str(m.get("to", ""))
        if r.get("toy", {}).get(target) != self.uid:
            send(self.request, {"t": "err", "m": "对方未同意"})
            return
        if m.get("d") in ("ride", "follow"):
            return
        tc = clients.get(target)
        if tc:
            payload = {"t": "toy_state", "from": self.uid}
            for k, v in m.items():
                if k in ("t", "to"):
                    continue
                payload[k] = v
            if payload.get("d") == "fx":
                # 水特效等需要房间内所有人（含目标与房主）都看到
                broadcast_room(rid, payload, None)
                return
            send(tc["sock"], payload)
            broadcast_room(rid, {"t": "toy_state", "controller": self.uid, "target": target, "d": m.get("d")}, target)

    def leave(self):
        if not self.uid:
            return
        c = clients.get(self.uid)
        if not c or c.get("sock") is not self.request:
            return
        if c and c.get("room") and c["room"] in rooms:
            rid = c["room"]
            r = rooms[rid]
            if r.get("host") == self.uid:
                archive_room_chat(rid, r)
                rooms.pop(rid, None)
                try:
                    plugin_call_all("on_room_destroy", rid, r)
                except Exception:
                    pass
                for u, cc in list(clients.items()):
                    if cc.get("room") == rid:
                        cc["room"] = ""
                        if u != self.uid:
                            send(cc["sock"], {"t": "room_closed", "room_id": rid, "reason": "房主已离开"})
                c["room"] = ""
                broadcast_all({"t": "room_list", "rooms": room_summary()})
                return
            r["players"].pop(self.uid, None)
            r.get("toy", {}).pop(self.uid, None)
            for tgt in [x for x, ctl in r.get("toy", {}).items() if ctl == self.uid]:
                r.get("toy", {}).pop(tgt, None)
                tc = clients.get(tgt)
                if tc:
                    send(tc["sock"], {"t": "toy_revoked", "controller": self.uid, "target": tgt})
            broadcast_room(c["room"], {"t": "room_leave", "uid": self.uid, "name": c.get("name", "")}, self.uid)
            broadcast_all({"t": "room_list", "rooms": room_summary()}, self.uid)
            try:
                plugin_call_all("on_leave", c)
            except Exception:
                pass
            c["room"] = ""

    def bye(self):
        if self.uid:
            c = clients.get(self.uid)
            if c and c.get("sock") is self.request:
                self.leave()
                clients.pop(self.uid, None)
                socket_clients.pop(id(self.request), None)
                CAPTCHAS.pop(self.uid, None)
                broadcast_all({"t": "presence", "uid": self.uid, "online": len(clients)})
                broadcast_all({"t": "room_list", "rooms": room_summary()})

class ThreadingTCPServer(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True

# ========== SFM Online 插件系统 ==========
import importlib.util as _importlib_util

PLUGIN_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "plugins")
PLUGIN_DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "plugin_data")
PLUGINS = {}
_PLUGIN_TICKERS = {}      # name -> 下一个 tick 时间
_PLUGIN_DATA = {}         # 插件数据缓存
_PLUGIN_DATA_FILE = os.path.join(PLUGIN_DIR, "..", "plugin_data.json")
_PLUGIN_DATA_FILE = os.path.abspath(_PLUGIN_DATA_FILE)
PLUGIN_TICK_INTERVAL = 5.0

# ---------- 插件数据 API（持久化到 plugin_data.json） ----------
def plugin_data_get(name, key, default=None):
    _load_plugin_data()
    d = _PLUGIN_DATA.get(name, {})
    if isinstance(d, dict):
        return d.get(key, default)
    return default

def plugin_data_set(name, key, value):
    _load_plugin_data()
    if name not in _PLUGIN_DATA or not isinstance(_PLUGIN_DATA.get(name), dict):
        _PLUGIN_DATA[name] = {}
    _PLUGIN_DATA[name][key] = value
    _save_plugin_data()

def _load_plugin_data():
    if _PLUGIN_DATA:
        return
    try:
        if os.path.exists(_PLUGIN_DATA_FILE):
            with open(_PLUGIN_DATA_FILE, "r", encoding="utf-8") as f:
                _PLUGIN_DATA.update(json.load(f))
    except Exception as e:
        print(f"[plugin] 数据加载失败: {e}", flush=True)

def _save_plugin_data():
    try:
        with open(_PLUGIN_DATA_FILE, "w", encoding="utf-8") as f:
            json.dump(_PLUGIN_DATA, f, ensure_ascii=False, indent=2)
    except Exception as e:
        print(f"[plugin] 数据保存失败: {e}", flush=True)

# ---------- 插件 API：注入到每个插件模块 ----------
def _make_plugin_api(name):
    """给插件模块注入可用的 API 函数。"""
    api = {
        "name": name,
        # 发送
        "send": send,                          # send(sock, obj)
        "send_plain": send_plain,
        "broadcast_room": broadcast_room,      # broadcast_room(rid, obj, ex=None)
        "broadcast_all": broadcast_all,
        "send_to_uid": lambda uid, obj: _send_to_uid(uid, obj),
        "send_to_room": lambda rid, obj, ex=None: broadcast_room(rid, obj, ex),
        # 查询
        "clients": lambda: clients,
        "rooms": lambda: rooms,
        "now": now,
        "get_client": lambda uid: clients.get(str(uid)),
        "get_room": lambda rid: rooms.get(str(rid)),
        "client_room": lambda c: c.get("room", "") if c else "",
        "room_players": lambda rid: list(rooms.get(rid, {}).get("players", {}).keys()),
        # 数据
        "data_get": lambda key, default=None: plugin_data_get(name, key, default),
        "data_set": lambda key, value: plugin_data_set(name, key, value),
        # 插件间通信
        "send_to_plugin": lambda target, op, data=None: plugin_send_to(name, target, op, data),
        "broadcast_event": lambda evt, data=None, exclude=None: plugin_broadcast_event(evt, data, exclude),
        "list_plugins": lambda: list(PLUGINS.keys()),
        # 日志
        "log": lambda *a: print(f"[plugin:{name}]", *a, flush=True),
        "log_event": log_event,
    }
    return api

def _send_to_uid(uid, obj):
    c = clients.get(str(uid))
    if c and c.get("sock") is not None:
        send(c["sock"], obj)
        return True
    return False

# ---------- 插件生命周期 ----------
def load_plugins():
    """加载 plugins/ 目录下的所有 .py 插件。"""
    PLUGINS.clear()
    _PLUGIN_TICKERS.clear()
    if not os.path.isdir(PLUGIN_DIR):
        return 0, []
    errs = []
    ok = 0
    for fn in sorted(os.listdir(PLUGIN_DIR)):
        if not fn.endswith(".py"):
            continue
        path = os.path.join(PLUGIN_DIR, fn)
        name = fn[:-3]
        r = load_one_plugin(name, path)
        if r is True:
            ok += 1
        else:
            errs.append(r)
    return ok, errs

def load_one_plugin(name, path):
    """加载单个插件。成功返回 True，失败返回错误串。"""
    try:
        spec = _importlib_util.spec_from_file_location("sfm_plugin_" + name, path)
        mod = _importlib_util.module_from_spec(spec)
        sys.modules[spec.name] = mod
        # 注入插件 API
        for k, v in _make_plugin_api(name).items():
            setattr(mod, k, v)
        spec.loader.exec_module(mod)
        PLUGINS[name] = mod
        _PLUGIN_TICKERS[name] = now()
        print(f"[plugin] 已加载: {name} v{getattr(mod, 'VERSION', '?')} - {getattr(mod, 'DESC', '')}", flush=True)
        return True
    except Exception as e:
        print(f"[plugin] 加载失败 {name}: {e}", flush=True)
        return f"{name}: {e}"

def reload_plugin(name):
    """热重载单个插件。"""
    path = os.path.join(PLUGIN_DIR, name + ".py")
    if not os.path.exists(path):
        return f"插件 {name} 不存在"
    old = PLUGINS.pop(name, None)
    if old is not None and hasattr(old, "on_unload"):
        try:
            old.on_unload()
        except Exception:
            pass
    r = load_one_plugin(name, path)
    return "OK" if r is True else f"失败: {r}"

def unload_plugin(name):
    """卸载插件。"""
    mod = PLUGINS.pop(name, None)
    _PLUGIN_TICKERS.pop(name, None)
    if mod is not None and hasattr(mod, "on_unload"):
        try:
            mod.on_unload()
        except Exception:
            pass
    return True if mod is not None else False

def plugin_call(name, method, *args):
    mod = PLUGINS.get(name)
    if mod is None:
        return
    fn = getattr(mod, method, None)
    if fn is None:
        return
    try:
        fn(*args)
    except Exception as e:
        print(f"[plugin] {name}.{method} 异常: {e}", flush=True)

def plugin_call_all(method, *args):
    for name in list(PLUGINS.keys()):
        plugin_call(name, method, *args)

def plugin_tick():
    """周期性调用插件 on_tick（每 5 秒）。"""
    t = now()
    for name in list(PLUGINS.keys()):
        if t - _PLUGIN_TICKERS.get(name, 0) >= PLUGIN_TICK_INTERVAL:
            _PLUGIN_TICKERS[name] = t
            plugin_call(name, "on_tick")

def dispatch_ext(client, m):
    """把 t=='ext' 消息路由给对应命名空间的插件。
    支持插件间通信：
      - m["to_ns"] 指定目标插件（跨插件定向消息，from_ns 注入）
      - m["evt"]    广播事件给所有订阅 on_event 的插件
    """
    ns = str(m.get("ns", "")).strip()
    to_ns = str(m.get("to_ns", "")).strip()
    # 插件间定向通信：A -> B
    if to_ns and to_ns in PLUGINS and to_ns != ns:
        m = dict(m)
        m["from_ns"] = ns
        plugin_call(to_ns, "on_message", client, m)
        return
    # 插件事件广播：所有插件 on_event(evt, data)
    evt = m.get("evt")
    if evt is not None and not ns:
        data = m.get("data")
        for name in list(PLUGINS.keys()):
            fn = getattr(PLUGINS[name], "on_event", None)
            if fn is None:
                continue
            try:
                fn(evt, data)
            except Exception as e:
                print(f"[plugin] {name}.on_event 异常: {e}", flush=True)
        return
    if not ns or ns not in PLUGINS:
        if client and client.get("sock") is not None:
            send(client["sock"], {"t": "err", "m": f"未知插件命名空间: {ns}"})
        return
    plugin_call(ns, "on_message", client, m)


def plugin_list():
    """返回插件列表（供客户端检测服务器 mod）。"""
    out = []
    for name, mod in sorted(PLUGINS.items()):
        out.append({
            "name": name,
            "version": str(getattr(mod, "VERSION", "?")),
            "desc": str(getattr(mod, "DESC", "")),
            "author": str(getattr(mod, "AUTHOR", "")),
        })
    return out


def plugin_send_to(name, target, op, data=None):
    """插件间定向调用：插件 A 调用插件 B 的 on_plugin_message(op, data)。"""
    mod = PLUGINS.get(target)
    if mod is None:
        return False
    fn = getattr(mod, "on_plugin_message", None)
    if fn is None:
        return False
    try:
        fn(name, op, data)
        return True
    except Exception as e:
        print(f"[plugin] {name}->{target} 通信异常: {e}", flush=True)
        return False


def plugin_broadcast_event(evt, data=None, exclude=None):
    """插件事件广播：所有插件 on_event(evt, data)。"""
    for name in list(PLUGINS.keys()):
        if name == exclude:
            continue
        fn = getattr(PLUGINS[name], "on_event", None)
        if fn is None:
            continue
        try:
            fn(evt, data)
        except Exception as e:
            print(f"[plugin] {name}.on_event 异常: {e}", flush=True)

def plugin_handle_chat(client, text):
    """把聊天消息传给插件 on_chat；返回 True 表示插件消费了消息。"""
    consumed = False
    for name in list(PLUGINS.keys()):
        fn = getattr(PLUGINS[name], "on_chat", None)
        if fn is None:
            continue
        try:
            r = fn(client, text)
            if r is True:
                consumed = True
        except Exception as e:
            print(f"[plugin] {name}.on_chat 异常: {e}", flush=True)
    return consumed

def plugin_handle_command(client, cmd, args):
    """把聊天命令（!xxx）传给插件 on_command。"""
    for name in list(PLUGINS.keys()):
        fn = getattr(PLUGINS[name], "on_command", None)
        if fn is None:
            continue
        try:
            fn(client, cmd, args)
        except Exception as e:
            print(f"[plugin] {name}.on_command 异常: {e}", flush=True)


load_plugins()
# 后台线程：周期触发 on_tick
def _plugin_ticker_loop():
    while True:
        time.sleep(PLUGIN_TICK_INTERVAL)
        try:
            plugin_tick()
        except Exception:
            pass

threading.Thread(target=_plugin_ticker_loop, daemon=True).start()
# ========== 插件系统结束 ==========

reload_dynamic()
threading.Thread(target=reporter, daemon=True).start()
log_event("start", "relay %s:%s" % (HOST, PORT))
print("SFM relay %s:%s" % (HOST, PORT))
ThreadingTCPServer((HOST, PORT), Handler).serve_forever()