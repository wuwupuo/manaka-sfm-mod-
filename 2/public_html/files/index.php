<?php
// ============================================================
// 📦 客户端文件保险箱
// 输入管理员密码后才能浏览/下载 protected/storage 里的文件
// ============================================================
define('SFM_BOOT', true);

$protected_path = __DIR__ . '/../protected/';
if (!file_exists($protected_path . 'config.php')) {
    die('系统配置缺失');
}
require_once $protected_path . 'config.php';
require_once $protected_path . 'functions.php';

sendSecurityHeaders();
header('Cache-Control: no-store');

$ip = getClientIP();
$error = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $password = $_POST['password'] ?? '';
    $result = adminLoginAttempt($ip, (string)$password);
    if ($result['code'] !== 0) $error = $result['msg'];
}

$loggedIn = isAdminLoggedIn();
$configured = isAdminConfigured();

$files = [];
if ($loggedIn) {
    $dir = rtrim((string)STORAGE_PATH, '/\\');
    if (is_dir($dir)) {
        foreach (scandir($dir) as $name) {
            if ($name === '.' || $name === '..') continue;
            $p = $dir . DIRECTORY_SEPARATOR . $name;
            if (is_file($p)) {
                $files[] = [
                    'name' => $name,
                    'size' => filesize($p),
                    'time' => date('Y-m-d H:i:s', filemtime($p))
                ];
            }
        }
        usort($files, function ($a, $b) { return strcmp($b['time'], $a['time']); });
    }
}

function fmtSize($n) {
    if ($n >= 1048576) return round($n / 1048576, 2) . ' MB';
    if ($n >= 1024) return round($n / 1024, 1) . ' KB';
    return $n . ' B';
}
?>
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>SFM 客户端文件保险箱</title>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <style>
        * { box-sizing: border-box; }
        body { font-family: 'Segoe UI', sans-serif; background:#0d0d1a; color:#eee; padding:20px; margin:0; }
        .container { max-width:760px; margin:0 auto; }
        .card { background:#1a1a2e; border-radius:10px; padding:24px; margin-bottom:20px; border-left:4px solid #e94560; }
        h1 { color:#e94560; font-size:22px; margin:0 0 6px; }
        .muted { color:#777; font-size:13px; }
        .warn { background:#3a1a1a; border:1px solid #e94560; border-radius:8px; padding:12px 14px; margin:14px 0; color:#ffb0b0; }
        input[type=password] { width:100%; background:#2a2a4e; border:1px solid #444; color:#eee; padding:12px; border-radius:6px; margin:10px 0; }
        .btn { background:#e94560; border:none; color:#fff; padding:10px 20px; border-radius:6px; cursor:pointer; }
        table { width:100%; border-collapse:collapse; font-size:13px; }
        th, td { padding:10px; text-align:left; border-bottom:1px solid #2a2a4e; }
        th { background:#12122a; color:#aaa; font-size:11px; }
        a.dl { color:#2ecc71; text-decoration:none; font-weight:bold; }
        .nav a { color:#aaa; text-decoration:none; padding:8px 14px; background:#2a2a4e; border-radius:6px; margin-left:8px; }
        .header { display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:10px; margin-bottom:20px; }
        .header h1 { margin:0; }
        .empty { color:#555; text-align:center; padding:24px; }
    </style>
</head>
<body>
<div class="container">
    <div class="header">
        <h1>📦 SFM 客户端文件保险箱</h1>
        <div class="nav">
            <a href="../sfm_api/admin.php">🎮 管理后台</a>
            <?php if ($loggedIn): ?><a href="logout.php">🚪 退出登录</a><?php endif; ?>
        </div>
    </div>

    <?php if (!$configured): ?>
        <div class="card">
            <div class="warn">管理员密码尚未配置，本保险箱已锁定。请先按部署说明设置 <code>protected/config.php</code> 中的管理密码。</div>
        </div>
    <?php elseif (!$loggedIn): ?>
        <div class="card">
            <h1>🔒 请输入管理密码</h1>
            <p class="muted">密码与管理后台相同。只有验证通过后才能浏览和下载客户端文件、配置与日志。</p>
            <?php if ($error): ?><div class="warn"><?= htmlspecialchars($error) ?></div><?php endif; ?>
            <form method="post">
                <input type="password" name="password" placeholder="管理密码" autocomplete="current-password" autofocus>
                <button class="btn">进入保险箱</button>
            </form>
        </div>
    <?php else: ?>
        <div class="card">
            <h1>📋 文件列表</h1>
            <p class="muted">把要私发给朋友的文件（客户端压缩包、配置模板等）用 FTP 上传到 <code>protected/storage/</code>，即可在这里下载。</p>
            <?php if (empty($files)): ?>
                <div class="empty">保险箱是空的。请用 FTP 上传文件到 protected/storage/</div>
            <?php else: ?>
                <table>
                    <tr><th>文件名</th><th>大小</th><th>更新时间</th><th>操作</th></tr>
                    <?php foreach ($files as $f): ?>
                    <tr>
                        <td><?= htmlspecialchars($f['name']) ?></td>
                        <td><?= fmtSize($f['size']) ?></td>
                        <td><?= htmlspecialchars($f['time']) ?></td>
                        <td><a class="dl" href="download.php?f=<?= rawurlencode($f['name']) ?>">⬇ 下载</a></td>
                    </tr>
                    <?php endforeach; ?>
                </table>
            <?php endif; ?>
        </div>
    <?php endif; ?>
</div>
</body>
</html>
