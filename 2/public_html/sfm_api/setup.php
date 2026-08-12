<?php
// ============================================================
// 🔧 安全配置助手（首次部署用）
// 访问方式：https://你的域名/sfm_api/setup.php?k=你的SETUP_KEY
// 它只“生成”配置内容，不会修改任何文件。
// ⚠ 用完请立即删除本文件，并妥善保管 SETUP_KEY。
// ============================================================
define('SFM_BOOT', true);

$protected_path = __DIR__ . '/../../protected/';
if (!file_exists($protected_path . 'config.php')) {
    die('系统配置缺失');
}
require_once $protected_path . 'config.php';
require_once $protected_path . 'functions.php';

sendSecurityHeaders();
header('Cache-Control: no-store');

$ip = getClientIP();
if (isBanned($ip)) {
    http_response_code(403);
    die('IP已被封禁');
}
if (!checkRateLimit($ip, 'setup_view', 20, 60)) {
    http_response_code(429);
    die('访问过于频繁');
}

$key = $_GET['k'] ?? '';
if (!defined('SETUP_KEY') || SETUP_KEY === '' || SETUP_KEY === 'CHANGE_ME_改成任意长随机字符串'
    || !is_string($key) || !hash_equals(SETUP_KEY, $key)) {
    http_response_code(403);
    die('无权访问（钥匙错误）。请先修改 protected/config.php 里的 SETUP_KEY。');
}

$dbmsg = null;
if (isset($_GET['db'])) {
    try {
        $t = new PDO(
            "mysql:host=" . DB_HOST . ";dbname=" . DB_NAME . ";charset=utf8mb4",
            DB_USER,
            DB_PASS,
            [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
        );
        $t->query('SELECT 1');
        $dbmsg = '✅ 数据库连接成功';
    } catch (Throwable $e) {
        $dbmsg = '❌ 数据库连接失败：' . $e->getMessage();
    }
}

$hash = null;
$hashErr = null;
if ($_SERVER['REQUEST_METHOD'] === 'POST' && ($_POST['gen_hash'] ?? '') === '1') {
    $p1 = (string)($_POST['password'] ?? '');
    $p2 = (string)($_POST['password2'] ?? '');
    if ($p1 === '' || $p1 !== $p2) {
        $hashErr = '两次输入的密码不一致或为空';
    } elseif (strlen($p1) < 8) {
        $hashErr = '密码至少需要 8 位';
    } else {
        $hash = password_hash($p1, PASSWORD_DEFAULT);
    }
}

$enc = null;
$encErr = null;
if ($_SERVER['REQUEST_METHOD'] === 'POST' && ($_POST['gen_enc'] ?? '') === '1') {
    if (defined('CONFIG_KEY') && CONFIG_KEY !== '' && file_exists($protected_path . 'security.php')) {
        require_once $protected_path . 'security.php';
        if (function_exists('openssl_encrypt')) {
            $plain = (string)($_POST['enc_plain'] ?? '');
            $enc = $plain !== '' ? cfgEnc($plain, CONFIG_KEY) : null;
            if ($enc === null) $encErr = '加密失败：请检查 CONFIG_KEY 是否为合法的 32 字节密钥';
        } else {
            $encErr = '服务器未启用 OpenSSL 扩展，无法使用配置加密';
        }
    } else {
        $encErr = '未启用配置加密：请先在 config.php 设置 CONFIG_KEY';
    }
}

$newKey = bin2hex(random_bytes(12)); // 用于替换 SETUP_KEY 的随机串
$adminConfigured = isAdminConfigured();
$adminBcrypt = is_string(ADMIN_TOKEN) && (
    strncmp(ADMIN_TOKEN, '$2y$', 4) === 0 ||
    strncmp(ADMIN_TOKEN, '$2a$', 4) === 0 ||
    strncmp(ADMIN_TOKEN, '$2b$', 4) === 0
);
?>
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>SFM 安全配置助手</title>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <style>
        * { box-sizing: border-box; }
        body { font-family: 'Segoe UI', sans-serif; background:#0d0d1a; color:#eee; padding:20px; margin:0; }
        .container { max-width:760px; margin:0 auto; }
        .card { background:#1a1a2e; border-radius:10px; padding:20px; margin-bottom:20px; border-left:4px solid #e94560; }
        h1 { color:#e94560; font-size:22px; margin:0 0 6px; }
        h2 { font-size:16px; margin:0 0 12px; border-bottom:1px solid #333; padding-bottom:8px; }
        .warn { background:#3a1a1a; border:1px solid #e94560; border-radius:8px; padding:12px 14px; margin:14px 0; color:#ffb0b0; }
        .ok { background:#123a1e; border:1px solid #2ecc71; border-radius:8px; padding:12px 14px; margin:14px 0; color:#a0ffc0; }
        textarea, input[type=text], input[type=password] { width:100%; background:#2a2a4e; border:1px solid #444; color:#eee; padding:10px; border-radius:6px; font-family:Consolas,monospace; }
        textarea { min-height:90px; }
        .btn { background:#e94560; border:none; color:#fff; padding:10px 18px; border-radius:6px; cursor:pointer; }
        .btn.green { background:#2ecc71; }
        label { display:block; margin:10px 0 4px; color:#aaa; font-size:13px; }
        .muted { color:#777; font-size:12px; }
    </style>
</head>
<body>
<div class="container">
    <div class="card">
        <h1>🔧 SFM 安全配置助手</h1>
        <div class="muted">本页面只生成配置内容，不会修改服务器文件。所有结果请复制到 <code>protected/config.php</code>。</div>
        <div class="warn"><strong>⚠ 重要：</strong>配置完成后请通过 FTP 删除 <code>sfm_api/setup.php</code>，并保管好 SETUP_KEY。</div>
    </div>

    <div class="card">
        <h2>1️⃣ 当前状态</h2>
        <p>管理员密码：<?= $adminConfigured ? '✅ 已配置' : '❌ 未配置 / 仍是占位符' ?>
            <?= $adminBcrypt ? '（bcrypt 加密存储 ✅）' : '' ?></p>
        <p>配置加密 CONFIG_KEY：<?= (defined('CONFIG_KEY') && CONFIG_KEY !== '') ? '✅ 已启用' : '未启用（数据库/管理密码为明文）' ?></p>
        <p><a class="btn green" href="?k=<?= htmlspecialchars($key) ?>&db=1">测试数据库连接</a></p>
        <?php if ($dbmsg !== null): ?>
            <div class="<?= strpos($dbmsg, '✅') === 0 ? 'ok' : 'warn' ?>"><?= htmlspecialchars($dbmsg) ?></div>
        <?php endif; ?>
    </div>

    <div class="card">
        <h2>2️⃣ 生成管理员密码哈希（推荐）</h2>
        <p class="muted">生成后把下面 <code>$2y$</code> 开头的结果，整个替换到 config.php 的 <code>$rawAdminToken</code>。</p>
        <form method="post">
            <input type="hidden" name="gen_hash" value="1">
            <label>管理密码（至少8位，建议12位以上）</label>
            <input type="password" name="password" autocomplete="new-password">
            <label>再次输入</label>
            <input type="password" name="password2" autocomplete="new-password">
            <br><br><button class="btn">生成 bcrypt 哈希</button>
        </form>
        <?php if ($hashErr): ?><div class="warn"><?= htmlspecialchars($hashErr) ?></div><?php endif; ?>
        <?php if ($hash): ?>
            <div class="ok">复制下面整行到 config.php：</div>
            <textarea readonly onclick="this.select()"><?= htmlspecialchars($hash) ?></textarea>
        <?php endif; ?>
    </div>

    <div class="card">
        <h2>3️⃣ 加密数据库密码 / 其他敏感值（可选）</h2>
        <p class="muted">需要先在 config.php 设置 CONFIG_KEY。生成后以 <code>enc:密文</code> 形式填入 config.php。</p>
        <form method="post">
            <input type="hidden" name="gen_enc" value="1">
            <label>要加密的明文（例如数据库密码）</label>
            <input type="text" name="enc_plain" autocomplete="off">
            <br><br><button class="btn">生成 enc: 密文</button>
        </form>
        <?php if ($encErr): ?><div class="warn"><?= htmlspecialchars($encErr) ?></div><?php endif; ?>
        <?php if ($enc): ?>
            <div class="ok">复制下面整行（含 enc: 前缀）到 config.php：</div>
            <textarea readonly onclick="this.select()">enc:<?= htmlspecialchars($enc) ?></textarea>
        <?php endif; ?>
    </div>

    <div class="card">
        <h2>4️⃣ 更换 SETUP_KEY</h2>
        <p class="muted">把下面的随机串替换到 config.php 的 <code>define('SETUP_KEY', ...)</code> 中。</p>
        <textarea readonly onclick="this.select()"><?= htmlspecialchars($newKey) ?></textarea>
    </div>
</div>
</body>
</html>
