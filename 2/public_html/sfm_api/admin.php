<?php
// ============================================================
// 管理后台（需登录 + CSRF）
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

$pdo = getDB();
$ip = getClientIP();
cleanOldData();

function jsAlert($msg, $url = null) {
    $msg = json_encode($msg, JSON_UNESCAPED_UNICODE);
    $url = $url ? json_encode($url) : 'null';
    echo '<script>alert(' . $msg . '); if (' . $url . ') location.href=' . $url . '; else location.reload();</script>';
    exit;
}

// ========== 处理 POST 操作（兼容 JSON 和表单，必须有 CSRF） ==========
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $input = getInput();
    $action = is_array($input) ? ($input['action'] ?? '') : '';

    // 登录本身不需要 CSRF（尚未建立会话）
    if ($action === 'admin_login') {
        $result = adminLoginAttempt($ip, (string)($input['password'] ?? ''));
        jsAlert($result['code'] === 0 ? '登录成功' : $result['msg'], 'admin.php?r=' . time());
    }

    if (!is_array($input) || !checkCsrf($input)) {
        jsAlert('安全校验失败，请刷新页面后重试');
    }
    if ($action === 'delete_room' && !empty($input['room_id'])) {
        $result = adminDeleteRoom($ip);
        jsAlert($result['code'] === 0 ? '删除成功' : $result['msg']);
    }
    if ($action === 'ban_ip' && !empty($input['ban_ip'])) {
        $result = adminBanIP($ip);
        jsAlert($result['code'] === 0 ? '拉黑成功' : $result['msg']);
    }
    if ($action === 'unban_ip' && !empty($input['unban_ip'])) {
        $result = adminUnbanIP($ip);
        jsAlert($result['code'] === 0 ? '解除成功' : $result['msg']);
    }
    if ($action === 'set_announcement' && isset($input['announcement'])) {
        $result = adminSetAnnouncement($ip);
        jsAlert($result['code'] === 0 ? '公告已更新' : $result['msg']);
    }
    if ($action === 'export_logs') {
        $result = adminExportLogs($ip);
        if ($result['code'] === 0) {
            jsAlert('日志已导出，请到“客户端文件保险箱”下载：' . $result['filename'], '../files/');
        }
        jsAlert($result['msg']);
    }
    jsAlert('未知操作');
}

// ========== 数据 ==========
$configured = isAdminConfigured();
$data = $configured ? adminListRooms() : ['code' => -1, 'msg' => '管理员尚未配置'];
$authError = $data['code'] ?? -1;
$loginRequired = $configured && $authError === -1 && ($data['msg'] ?? '') === '请先登录';

if ($authError !== 0) {
    $rooms = [];
    $bans = [];
    $logs = [];
    $current_announcement = ['content' => '', 'created_at' => ''];
    $stats = ['total_rooms' => 0, 'total_players' => 0, 'total_bans' => 0];
} else {
    $rooms = $data['rooms'];
    $bans = $data['bans'];
    $logs = $data['logs'];
    $current_announcement = ['content' => $data['announcement'], 'created_at' => $data['announcement_time']];
    $stats = $data['stats'];
}

$isBcrypt = is_string(ADMIN_TOKEN) && (
    strncmp(ADMIN_TOKEN, '$2y$', 4) === 0 ||
    strncmp(ADMIN_TOKEN, '$2a$', 4) === 0 ||
    strncmp(ADMIN_TOKEN, '$2b$', 4) === 0
);
?>
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>SFM 管理后台</title>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <style>
        * { box-sizing: border-box; }
        body { font-family: 'Segoe UI', sans-serif; background:#0d0d1a; color:#eee; padding:20px; margin:0; }
        .container { max-width:1200px; margin:0 auto; }
        .header { display:flex; justify-content:space-between; align-items:center; padding:15px 20px; background:#1a1a2e; border-radius:10px; margin-bottom:20px; border-left:4px solid #e94560; flex-wrap:wrap; gap:10px; }
        .header h1 { margin:0; color:#e94560; font-size:22px; }
        .header .nav a { color:#aaa; text-decoration:none; padding:8px 16px; background:#2a2a4e; border-radius:6px; margin-left:8px; }
        .warn { background:#3a1a1a; border:1px solid #e94560; border-radius:8px; padding:14px 16px; margin-bottom:20px; color:#ffb0b0; }
        .notice { background:#3a2a1a; border:1px solid #ffa500; border-radius:8px; padding:14px 16px; margin-bottom:20px; color:#ffd9a0; }
        .section { background:#1a1a2e; border-radius:10px; padding:20px; margin-bottom:20px; }
        .section h2 { margin:0 0 12px; font-size:17px; border-bottom:1px solid #333; padding-bottom:8px; }
        .stats { display:grid; grid-template-columns:repeat(auto-fit,minmax(150px,1fr)); gap:12px; margin-bottom:20px; }
        .stat-card { background:#1a1a2e; padding:16px; border-radius:10px; text-align:center; }
        .stat-card .num { font-size:28px; font-weight:bold; color:#e94560; }
        .stat-card .label { color:#888; font-size:12px; margin-top:4px; }
        table { width:100%; border-collapse:collapse; font-size:13px; }
        th, td { padding:8px 10px; text-align:left; border-bottom:1px solid #2a2a4e; }
        th { background:#12122a; color:#aaa; font-size:11px; }
        tr:hover td { background:#1f1f3a; }
        .btn { padding:5px 12px; border:none; border-radius:4px; cursor:pointer; font-size:12px; }
        .btn-danger { background:#e94560; color:#fff; }
        .btn-success { background:#2ecc71; color:#fff; }
        .btn-gold { background:#f39c12; color:#fff; }
        .form-inline { display:flex; flex-wrap:wrap; gap:10px; align-items:center; margin-bottom:14px; }
        .form-inline input, .form-inline select { background:#2a2a4e; border:1px solid #444; color:#eee; padding:8px 10px; border-radius:6px; }
        .announcement-box { background:#1a1a3e; border:1px solid #e94560; border-radius:8px; padding:12px; margin:8px 0; }
        .announcement-box .content { font-size:15px; color:#ffd700; }
        .empty { color:#555; text-align:center; padding:24px; }
        .table-wrap { overflow-x:auto; }
    </style>
</head>
<body>
<div class="container">
    <div class="header">
        <h1>🎮 SFM 管理后台</h1>
        <div class="nav">
            <a href="../files/">📦 客户端文件保险箱</a>
            <a href="logout.php">🚪 退出登录</a>
        </div>
    </div>

    <?php if ($loginRequired): ?>
    <div class="section">
        <h2>🔒 管理员登录</h2>
        <p class="empty" style="text-align:left;padding:8px 0;">请输入管理密码（与游戏内 F8 菜单的管理员登录使用同一个密码）。</p>
        <form method="post" class="form-inline">
            <input type="hidden" name="action" value="admin_login">
            <input type="password" name="password" placeholder="管理密码" autocomplete="current-password" style="min-width:220px;">
            <button type="submit" class="btn btn-success">登录</button>
        </form>
    </div>
    <?php elseif (!$configured): ?>
    <div class="warn">
        <strong>⚠ 管理密码未配置，当前所有管理功能已安全锁定。</strong><br>
        请打开 <code>protected/config.php</code>，把 <code>$rawAdminToken</code> 设置为
        bcrypt 哈希（用 <a href="setup.php?k=<?= htmlspecialchars((string)SETUP_KEY) ?>">配置助手</a> 生成），
        修改 <code>SETUP_KEY</code> 后刷新本页。
    </div>
    <?php elseif (!$isBcrypt): ?>
    <div class="notice">
        <strong>建议：</strong>当前管理员密码是明文存储。建议用
        <a href="setup.php?k=<?= htmlspecialchars((string)SETUP_KEY) ?>">配置助手</a>
        生成 bcrypt 哈希后替换，防止数据库泄露时密码被直接读出。
    </div>
    <?php endif; ?>

    <?php if ($configured && $authError === 0): ?>

    <div class="section">
        <h2>📢 公告</h2>
        <?php if ($current_announcement['content']): ?>
        <div class="announcement-box"><div class="content"><?= htmlspecialchars($current_announcement['content']) ?></div></div>
        <?php else: ?>
        <div class="empty">暂无公告</div>
        <?php endif; ?>
        <form method="post" class="form-inline">
            <?= csrfField() ?>
            <input type="hidden" name="action" value="set_announcement">
            <input type="text" name="announcement" placeholder="输入新公告内容" style="flex:1;min-width:200px;">
            <button type="submit" class="btn btn-success">更新公告</button>
        </form>
    </div>

    <div class="stats">
        <div class="stat-card"><div class="num"><?= (int)$stats['total_rooms'] ?></div><div class="label">活跃房间</div></div>
        <div class="stat-card"><div class="num"><?= (int)$stats['total_players'] ?></div><div class="label">在线玩家</div></div>
        <div class="stat-card"><div class="num"><?= (int)$stats['total_bans'] ?></div><div class="label">已拉黑IP</div></div>
        <div class="stat-card"><div class="num"><?= (int)$pdo->query("SELECT COUNT(*) FROM chat_logs")->fetchColumn() ?></div><div class="label">聊天消息</div></div>
    </div>

    <div class="section">
        <h2>📋 房间列表</h2>
        <?php if (empty($rooms)): ?><div class="empty">暂无活跃房间</div>
        <?php else: ?>
        <div class="table-wrap"><table>
            <tr><th>ID</th><th>房间名</th><th>房主</th><th>地址</th><th>人数</th><th>状态</th><th>操作</th></tr>
            <?php foreach ($rooms as $r): ?>
            <tr>
                <td><?= htmlspecialchars($r['room_id']) ?></td>
                <td><?= htmlspecialchars($r['room_name']) ?></td>
                <td><?= htmlspecialchars($r['host_name']) ?></td>
                <td><?= htmlspecialchars($r['host_address']) ?></td>
                <td><?= htmlspecialchars($r['player_display'] ?? '') ?></td>
                <td><?= htmlspecialchars($r['status'] ?? '') ?></td>
                <td>
                    <form method="post" style="display:inline">
                        <?= csrfField() ?>
                        <input type="hidden" name="action" value="delete_room">
                        <input type="hidden" name="room_id" value="<?= htmlspecialchars($r['room_id']) ?>">
                        <button class="btn btn-danger">删除</button>
                    </form>
                </td>
            </tr>
            <?php endforeach; ?>
        </table></div>
        <?php endif; ?>
    </div>

    <div class="section">
        <h2>🚫 封禁管理</h2>
        <form method="post" class="form-inline">
            <?= csrfField() ?>
            <input type="hidden" name="action" value="ban_ip">
            <input type="text" name="ban_ip" placeholder="要拉黑的IP">
            <select name="days">
                <option value="1">1天</option><option value="3">3天</option>
                <option value="7" selected>7天</option><option value="30">30天</option>
                <option value="3650">永久</option>
            </select>
            <button class="btn btn-danger">拉黑</button>
        </form>
        <form method="post" class="form-inline">
            <?= csrfField() ?>
            <input type="hidden" name="action" value="unban_ip">
            <input type="text" name="unban_ip" placeholder="要解除的IP">
            <button class="btn btn-success">解除拉黑</button>
        </form>
        <?php if (empty($bans)): ?><div class="empty">暂无封禁</div>
        <?php else: ?>
        <div class="table-wrap"><table>
            <tr><th>IP</th><th>到期时间</th><th>原因</th><th>操作</th></tr>
            <?php foreach ($bans as $b): ?>
            <tr>
                <td><?= htmlspecialchars($b['ip']) ?></td>
                <td><?= htmlspecialchars($b['expire_at']) ?></td>
                <td><?= htmlspecialchars($b['reason_detail']) ?></td>
                <td>
                    <form method="post" style="display:inline">
                        <?= csrfField() ?>
                        <input type="hidden" name="action" value="unban_ip">
                        <input type="hidden" name="unban_ip" value="<?= htmlspecialchars($b['ip']) ?>">
                        <button class="btn btn-success">解除</button>
                    </form>
                </td>
            </tr>
            <?php endforeach; ?>
        </table></div>
        <?php endif; ?>
    </div>

    <div class="section">
        <h2>📜 操作日志（最近100条）</h2>
        <form method="post" class="form-inline">
            <?= csrfField() ?>
            <input type="hidden" name="action" value="export_logs">
            <button class="btn btn-gold">导出完整日志到保险箱</button>
            <span style="color:#888;font-size:12px;">导出后可到“客户端文件保险箱”下载，日志文件不会被外人访问。</span>
        </form>
        <?php if (empty($logs)): ?><div class="empty">暂无日志</div>
        <?php else: ?>
        <div class="table-wrap"><table>
            <tr><th>时间</th><th>IP</th><th>玩家</th><th>操作</th><th>详情</th></tr>
            <?php foreach ($logs as $l): ?>
            <tr>
                <td><?= htmlspecialchars($l['created_at']) ?></td>
                <td><?= htmlspecialchars($l['ip']) ?></td>
                <td><?= htmlspecialchars($l['player_name']) ?></td>
                <td><?= htmlspecialchars($l['action']) ?></td>
                <td><?= htmlspecialchars($l['detail']) ?></td>
            </tr>
            <?php endforeach; ?>
        </table></div>
        <?php endif; ?>
    </div>

    <?php else: ?>
    <div class="warn">
        <?= htmlspecialchars($data['msg'] ?? '暂无权限') ?>
    </div>
    <?php endif; ?>
</div>
</body>
</html>
