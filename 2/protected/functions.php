<?php
if (!defined('SFM_BOOT')) {
    http_response_code(403);
    exit('Forbidden');
}

require_once __DIR__ . '/config.php';

function getDB() {
    static $pdo = null;
    if ($pdo === null) {
        try {
            $pdo = new PDO(
                "mysql:host=" . DB_HOST . ";dbname=" . DB_NAME . ";charset=utf8mb4",
                DB_USER,
                DB_PASS,
                [
                    PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
                    PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
                    PDO::ATTR_EMULATE_PREPARES => false
                ]
            );
        } catch (PDOException $e) {
            http_response_code(500);
            die(json_encode(['code' => -1, 'msg' => '数据库连接失败']));
        }
    }
    return $pdo;
}

// 统一 JSON 输出 + 安全响应头
function respond($data) {
    if (!headers_sent()) {
        header('Content-Type: application/json; charset=utf-8');
        header('X-Content-Type-Options: nosniff');
        header('X-Frame-Options: DENY');
        header('Referrer-Policy: no-referrer');
        header('Cache-Control: no-store');
        header('Vary: Accept-Encoding');
    }
    $out = json_encode($data, JSON_UNESCAPED_UNICODE);
    // 浏览器/客户端支持 gzip 时压缩响应，省流量、降延迟
    if (function_exists('gzencode')
        && !empty($_SERVER['HTTP_ACCEPT_ENCODING'])
        && strpos($_SERVER['HTTP_ACCEPT_ENCODING'], 'gzip') !== false) {
        $gz = gzencode($out, 6);
        if ($gz !== false) {
            header('Content-Encoding: gzip');
            echo $gz;
            exit;
        }
    }
    echo $out;
    exit;
}

// 统一消息格式：key=给客户端翻译用的字段名，msg=中文兜底文案
function keyMsg($code, $key, $msg, $extra = []) {
    return array_merge(['code' => $code, 'key' => $key, 'msg' => $msg], $extra);
}

// 通用安全响应头（网页用）
function sendSecurityHeaders() {
    if (headers_sent()) return;
    header('X-Content-Type-Options: nosniff');
    header('X-Frame-Options: DENY');
    header('Referrer-Policy: no-referrer');
    header('Permissions-Policy: geolocation=(), microphone=(), camera=()');
}

// 同时兼容 JSON body 和表单提交，并限制请求体大小
function getInput() {
    $raw = file_get_contents('php://input');
    if (strlen($raw) > (int)MAX_REQUEST_SIZE) {
        respond(['code' => -1, 'msg' => '请求内容过大']);
    }
    if (!empty($raw)) {
        $json = json_decode($raw, true);
        if (is_array($json)) return $json;
    }
    return $_POST;
}

function getClientIP() {
    $ip = $_SERVER['REMOTE_ADDR'] ?? '0.0.0.0';
    // 只有来自可信代理时才信任 X_FORWARDED_FOR，防止伪造
    $proxies = array_filter(array_map('trim', explode(',', TRUSTED_PROXIES)));
    if (!empty($proxies) && in_array($ip, $proxies, true)) {
        $forwarded = $_SERVER['HTTP_X_FORWARDED_FOR'] ?? '';
        if ($forwarded) {
            $ips = explode(',', $forwarded);
            $first = trim($ips[0]);
            if (filter_var($first, FILTER_VALIDATE_IP)) $ip = $first;
        }
    }
    return $ip;
}

function isBanned($ip) {
    $pdo = getDB();
    $stmt = $pdo->prepare("SELECT 1 FROM bans WHERE ip = ? AND expire_at > NOW()");
    $stmt->execute([$ip]);
    return $stmt->fetch() !== false;
}

function logAction($ip, $playerId, $playerName, $action, $detail = '') {
    $pdo = getDB();
    $stmt = $pdo->prepare("INSERT INTO action_logs (ip, player_id, player_name, action, detail, created_at) VALUES (?, ?, ?, ?, ?, NOW())");
    $stmt->execute([$ip, $playerId, $playerName, $action, $detail]);
}

function recordFailure($ip) {
    $pdo = getDB();
    $pdo->prepare("INSERT INTO failures (ip, created_at) VALUES (?, NOW())")->execute([$ip]);
    $stmt = $pdo->prepare("SELECT COUNT(*) FROM failures WHERE ip = ? AND created_at > NOW() - INTERVAL 10 MINUTE");
    $stmt->execute([$ip]);
    if ($stmt->fetchColumn() >= 5) {
        $pdo->prepare("INSERT INTO bans (ip, expire_at, reason_detail) VALUES (?, NOW() + INTERVAL 10 MINUTE, '多次失败')")->execute([$ip]);
        $pdo->prepare("DELETE FROM failures WHERE ip = ?")->execute([$ip]);
    }
}

function generateRoomId() { return strtoupper(substr(bin2hex(random_bytes(4)), 0, 8)); }
function generateCaptcha() { $chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; return substr(str_shuffle($chars), 0, 6); }
function generateToken() { return bin2hex(random_bytes(16)); }

// ============================================================
// 清理过期数据（公共入口每次请求调用）
// ============================================================
function cleanOldData() {
    $pdo = getDB();
    $lifetime = (int)ROOM_LIFETIME;
    $timeout = (int)ROOM_TIMEOUT;
    $captchaExpire = (int)CAPTCHA_EXPIRE;
    $logDays = (int)LOG_RETENTION_DAYS;

    $pdo->exec("DELETE FROM rooms WHERE created_at < NOW() - INTERVAL $lifetime SECOND");
    $deleted = $pdo->exec("DELETE FROM rooms WHERE status = 'active' AND last_update < NOW() - INTERVAL $timeout SECOND");
    if ($deleted > 0) {
        $pdo->exec("DELETE FROM room_players WHERE room_id NOT IN (SELECT room_id FROM rooms)");
        $pdo->exec("DELETE FROM chat_logs WHERE room_id NOT IN (SELECT room_id FROM rooms)");
    }
    $pdo->exec("DELETE FROM captcha WHERE created_at < NOW() - INTERVAL $captchaExpire SECOND");
    $pdo->exec("DELETE FROM action_logs WHERE created_at < NOW() - INTERVAL $logDays DAY");
    $pdo->exec("DELETE FROM chat_logs WHERE created_at < NOW() - INTERVAL $logDays DAY");
    try {
        $pdo->exec("DELETE FROM rate_limits WHERE hit_at < NOW() - INTERVAL 10 MINUTE");
    } catch (Throwable $e) {
        // 限流表不存在时忽略（首次部署尚未导入 schema 的情况）
    }
}

// ============================================================
// 黑名单
// ============================================================
function loadBlacklist() {
    if (!file_exists(BLACKLIST_FILE)) return [];
    $content = file_get_contents(BLACKLIST_FILE);
    if (!$content) return [];
    $lines = explode("\n", $content);
    $result = [];
    foreach ($lines as $line) {
        $line = trim($line);
        if ($line && substr($line, 0, 1) !== '#') $result[] = $line;
    }
    return $result;
}

function containsBlacklist($input) {
    if (!is_string($input)) return false;
    $blacklist = loadBlacklist();
    $input = strtolower($input);
    foreach ($blacklist as $word) {
        if (strpos($input, strtolower($word)) !== false) return true;
    }
    return false;
}

// ============================================================
// 输入过滤
// ============================================================
function sanitizeString($input, $maxLen = 40) {
    if (!is_string($input)) return '';
    $input = strip_tags($input);
    $input = preg_replace('/[^\p{L}\p{N}\s\-_.!?，。！？、：；""''()（）\@\#\$\%\^\&\*\+\=\/]/u', '', $input);
    return mb_substr(trim($input), 0, $maxLen);
}

function sanitizeAlnum($input) { return preg_replace('/[^A-Za-z0-9]/', '', $input); }
function sanitizeIP($input) { return filter_var($input, FILTER_VALIDATE_IP) ? $input : ''; }
function sanitizePort($input) { $p = intval($input); return ($p >= 1024 && $p <= 65535) ? $p : 0; }
function sanitizePassword($input) { return substr(preg_replace('/[^\x20-\x7E]/', '', $input), 0, 64); }
function sanitizeChat($input) {
    $input = strip_tags($input);
    $input = preg_replace('/[^\p{L}\p{N}\s\-_.!?，。！？、：；""''()（）\@\#\$\%\^\&\*\+\=\/]/u', '', $input);
    return mb_substr(trim($input), 0, (int)MAX_CHAT_LENGTH);
}

// 对外地址（穿透/隧道地址）：只允许 主机名:端口、IP:端口 或 [IPv6]:端口，
// 禁止 http://、路径、空格等，防止注入或诱导跳转。
function sanitizePublicAddress($input) {
    if (!is_string($input)) return '';
    $input = trim($input);
    if (preg_match('#^[A-Za-z0-9.\-]+(?::[0-9]{1,5})?$#', $input)) {
        return mb_substr($input, 0, 128);
    }
    if (preg_match('#^\[[0-9A-Fa-f:]+\](?::[0-9]{1,5})?$#', $input)) {
        return mb_substr($input, 0, 128);
    }
    return '';
}

function secureInput($input, $maxLen = 40) {
    if (containsBlacklist($input)) return '';
    return sanitizeString($input, $maxLen);
}

// ============================================================
// 频率限制
// ============================================================
function checkRateLimit($ip, $action, $limit = 30, $window = 60) {
    $pdo = getDB();
    $limit = (int)$limit;
    $window = (int)$window;

    // 独立限流表（首次使用自动创建），不依赖操作日志
    static $tableReady = null;
    if ($tableReady === null) {
        try {
            $pdo->exec("CREATE TABLE IF NOT EXISTS rate_limits (
                id INT AUTO_INCREMENT PRIMARY KEY,
                ip VARCHAR(64) NOT NULL,
                action VARCHAR(32) NOT NULL,
                hit_at DATETIME NOT NULL,
                KEY idx_ratelimit (ip, action, hit_at)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
            $pdo->query("SELECT 1 FROM rate_limits LIMIT 1");
            $tableReady = true;
        } catch (Throwable $e) {
            // 建表或查询失败时放弃限流，避免整个站点不可用
            $tableReady = false;
        }
    }
    if (!$tableReady) return true;

    $stmt = $pdo->prepare("SELECT COUNT(*) FROM rate_limits WHERE ip = ? AND action = ? AND hit_at > NOW() - INTERVAL $window SECOND");
    $stmt->execute([$ip, $action]);
    if ($stmt->fetchColumn() >= $limit) return false;

    $pdo->prepare("INSERT INTO rate_limits (ip, action, hit_at) VALUES (?, ?, NOW())")->execute([$ip, $action]);
    return true;
}

// ============================================================
// 安全会话（Cookie 或客户端 Token 两种方式）
// ============================================================
function isHttps() {
    return (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off')
        || (($_SERVER['HTTP_X_FORWARDED_PROTO'] ?? '') === 'https');
}

// 游戏客户端（Unity HttpClient）不自动保存 Cookie，因此登录后返回
// 会话 Token，客户端通过 X-SFM-Token 请求头带回来。
function adminSessionToken() {
    $h = $_SERVER['HTTP_X_SFM_TOKEN'] ?? '';
    if (is_string($h) && $h !== '' && preg_match('/^[a-zA-Z0-9,-]{16,128}$/', $h)) {
        return $h;
    }
    return null;
}

function secureSessionStart($token = null) {
    if (session_status() !== PHP_SESSION_NONE) return;
    $useToken = ($token !== null);
    ini_set('session.use_strict_mode', '1');
    if ($useToken) {
        // 客户端 Token 模式：不写 Cookie，只按请求头里的会话ID加载
        ini_set('session.use_cookies', '0');
        session_id($token);
    } else {
        $secure = isHttps();
        session_set_cookie_params([
            'lifetime' => 0,
            'path' => '/',
            'httponly' => true,
            'secure' => $secure,
            'samesite' => 'Lax'
        ]);
    }
    session_name('SFMADMIN');
    session_start();
}

function isAdminLoggedIn() {
    $token = adminSessionToken();
    if ($token !== null) {
        secureSessionStart($token);
    } else {
        secureSessionStart();
    }
    if (empty($_SESSION['admin_logged']) || $_SESSION['admin_logged'] !== true) return false;
    if (isset($_SESSION['login_time']) && time() - (int)$_SESSION['login_time'] > (int)SESSION_LIFETIME) {
        $_SESSION = [];
        session_destroy();
        return false;
    }
    if ((int)SESSION_BIND_IP === 1) {
        $bindIp = $_SESSION['admin_ip'] ?? '';
        if ($bindIp === '' || !hash_equals($bindIp, getClientIP())) {
            $_SESSION = [];
            session_destroy();
            return false;
        }
    }
    $_SESSION['login_time'] = time();
    return true;
}

// 管理员是否已真正配置（空值/占位符/过短明文一律视为未配置 → 拒绝登录）
function isAdminConfigured() {
    if (!defined('ADMIN_TOKEN') || !is_string(ADMIN_TOKEN)) return false;
    $t = ADMIN_TOKEN;
    if ($t === '') return false;
    if (strncmp($t, '$2y$', 4) === 0 || strncmp($t, '$2a$', 4) === 0 || strncmp($t, '$2b$', 4) === 0) {
        return strlen($t) >= 40;
    }
    $placeholder = ['你的管理密码', 'changeme', 'password', 'admin', '123456', '12345678', '00000000'];
    if (in_array(strtolower(trim($t)), $placeholder, true)) return false;
    return strlen($t) >= (int)MIN_ADMIN_PASSWORD_LEN;
}

function adminVerifyPassword($password) {
    if (!isAdminConfigured()) return false;
    $t = ADMIN_TOKEN;
    if (strncmp($t, '$2y$', 4) === 0 || strncmp($t, '$2a$', 4) === 0 || strncmp($t, '$2b$', 4) === 0) {
        return is_string($password) && $password !== '' && password_verify($password, $t);
    }
    return is_string($password) && $password !== '' && hash_equals($t, $password);
}

// 统一登录入口：防爆破 + 会话重建 + 返回 CSRF/会话 Token
function adminLoginAttempt($ip, $password) {
    if (!isAdminConfigured()) {
        return ['code' => -1, 'msg' => '管理员尚未配置，请先按部署说明设置管理密码'];
    }
    if (isBanned($ip)) {
        return ['code' => -2, 'msg' => 'IP已被临时封禁，请稍后再试'];
    }
    if (!checkRateLimit($ip, 'admin_login', (int)ADMIN_LOGIN_MAX_TRIES, (int)ADMIN_LOGIN_WINDOW)) {
        usleep(random_int(400000, 800000));
        return ['code' => -1, 'msg' => '尝试过于频繁，请5分钟后再试'];
    }
    if (adminVerifyPassword($password)) {
        secureSessionStart(adminSessionToken());
        session_regenerate_id(true);
        $_SESSION['admin_logged'] = true;
        $_SESSION['login_time'] = time();
        $_SESSION['admin_ip'] = $ip;
        $_SESSION['csrf'] = bin2hex(random_bytes(16));
        logAction($ip, 'admin', '管理员', 'admin_login', '登录成功');
        return ['code' => 0, 'msg' => '登录成功', 'csrf' => $_SESSION['csrf']];
    }
    logAction($ip, 'admin', '未知', 'admin_login', '密码错误');
    recordFailure($ip);
    usleep(random_int(500000, 1000000));
    return ['code' => -1, 'msg' => '密码错误'];
}

// CSRF：浏览器页面用隐藏字段，游戏客户端用 X-SFM-CSRF 请求头
function getCsrfToken() {
    if (!isAdminLoggedIn()) return '';
    return $_SESSION['csrf'] ?? '';
}

function checkCsrf($input = null) {
    if (!isAdminLoggedIn()) return false;
    if ($input === null) $input = getInput();
    $token = is_array($input) ? ($input['csrf'] ?? '') : '';
    if ($token === '') $token = $_SERVER['HTTP_X_SFM_CSRF'] ?? '';
    if (!is_string($token) || $token === '') return false;
    return isset($_SESSION['csrf']) && is_string($_SESSION['csrf']) && hash_equals($_SESSION['csrf'], $token);
}

function csrfField() {
    return '<input type="hidden" name="csrf" value="' . htmlspecialchars(getCsrfToken(), ENT_QUOTES, 'UTF-8') . '">';
}

// ============================================================
// 文件下载（保险箱专用）
// ============================================================
function sendFileDownload($path, $downloadName) {
    if (!is_file($path)) return false;
    header('Content-Type: application/octet-stream');
    header('Content-Disposition: attachment; filename="' . basename($downloadName) . '"');
    header('Content-Length: ' . filesize($path));
    header('X-Content-Type-Options: nosniff');
    header('Cache-Control: no-store');
    readfile($path);
    exit;
}
