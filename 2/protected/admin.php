<?php
if (!defined('SFM_BOOT')) {
    http_response_code(403);
    exit('Forbidden');
}

require_once __DIR__ . '/functions.php';

// 所有管理操作统一入口：必须已配置 + 已登录 + 操作频率正常
function checkAdminAuth() {
    if (!isAdminConfigured()) {
        return ['code' => -1, 'msg' => '管理员尚未配置'];
    }
    if (!isAdminLoggedIn()) {
        return ['code' => -1, 'msg' => '请先登录'];
    }
    if (!checkRateLimit(getClientIP(), 'admin_action', 120, 60)) {
        return ['code' => -1, 'msg' => '操作过于频繁，请稍后再试'];
    }
    return null;
}

// 管理能力清单（客户端登录后先拉取，再决定显示哪些管理功能）
function adminInfo() {
    $auth = checkAdminAuth();
    if ($auth) return $auth;
    return [
        'code' => 0,
        'key' => 'admin_info',
        'msg' => '管理信息',
        'version' => '1.1.2',
        'panel_url' => 'admin.php',
        'actions' => [
            'admin_list', 'admin_delete', 'admin_ban', 'admin_unban',
            'admin_set_announcement', 'admin_export_logs'
        ]
    ];
}

function adminListRooms() {
    $auth = checkAdminAuth();
    if ($auth) return $auth;

    $pdo = getDB();
    $rooms = $pdo->query("SELECT *, CONCAT(current_players, '/', LEAST(max_players, 10)) as player_display
                          FROM rooms ORDER BY created_at DESC")->fetchAll();
    $bans = $pdo->query("SELECT * FROM bans ORDER BY expire_at DESC")->fetchAll();
    $logs = $pdo->query("SELECT * FROM action_logs ORDER BY created_at DESC LIMIT 100")->fetchAll();

    $stmt = $pdo->prepare("SELECT content, created_at FROM announcements WHERE status = 'active' ORDER BY created_at DESC LIMIT 1");
    $stmt->execute();
    $announcement = $stmt->fetch();

    return [
        'code' => 0,
        'rooms' => $rooms,
        'bans' => $bans,
        'logs' => $logs,
        'announcement' => $announcement ? $announcement['content'] : '',
        'announcement_time' => $announcement ? $announcement['created_at'] : '',
        'stats' => [
            'total_rooms' => count($rooms),
            'total_bans' => count($bans),
            'total_players' => $pdo->query("SELECT COUNT(DISTINCT player_id) FROM room_players")->fetchColumn(),
            'max_players_per_room' => 10
        ]
    ];
}

function adminDeleteRoom($ip) {
    $auth = checkAdminAuth();
    if ($auth) return $auth;
    if (!checkCsrf()) return ['code' => -1, 'msg' => '安全校验失败，请刷新页面重试'];

    $input = getInput();
    $room_id = sanitizeAlnum($input['room_id'] ?? '');

    if (empty($room_id)) return ['code' => -1, 'msg' => '缺少房间ID'];

    $pdo = getDB();
    $stmt = $pdo->prepare("SELECT host_name FROM rooms WHERE room_id = ?");
    $stmt->execute([$room_id]);
    $room = $stmt->fetch();

    if (!$room) return ['code' => -1, 'msg' => '房间不存在'];

    $pdo->prepare("DELETE FROM rooms WHERE room_id = ?")->execute([$room_id]);
    $pdo->prepare("DELETE FROM room_players WHERE room_id = ?")->execute([$room_id]);
    $pdo->prepare("DELETE FROM chat_logs WHERE room_id = ?")->execute([$room_id]);

    logAction($ip, 'admin', '管理员', '强制删除房间', "房间ID: $room_id");
    return ['code' => 0, 'msg' => '房间已删除'];
}

function adminBanIP($ip) {
    $auth = checkAdminAuth();
    if ($auth) return $auth;
    if (!checkCsrf()) return ['code' => -1, 'msg' => '安全校验失败，请刷新页面重试'];

    $input = getInput();
    $ban_ip = sanitizeIP($input['ip'] ?? '');
    $reason = secureInput($input['reason'] ?? '恶意行为', 255);
    $days = min(365, max(1, intval($input['days'] ?? 7)));

    if (empty($ban_ip)) return ['code' => -1, 'msg' => '缺少IP'];

    $pdo = getDB();
    $pdo->prepare("INSERT INTO bans (ip, expire_at, reason_detail, created_by)
                   VALUES (?, NOW() + INTERVAL ? DAY, ?, '管理员')
                   ON DUPLICATE KEY UPDATE expire_at = NOW() + INTERVAL ? DAY, reason_detail = ?")
        ->execute([$ban_ip, $days, $reason, $days, $reason]);

    logAction($ip, 'admin', '管理员', '拉黑IP', "IP: $ban_ip, 天数: $days");
    return ['code' => 0, 'msg' => "IP已拉黑 $days 天"];
}

function adminUnbanIP($ip) {
    $auth = checkAdminAuth();
    if ($auth) return $auth;
    if (!checkCsrf()) return ['code' => -1, 'msg' => '安全校验失败，请刷新页面重试'];

    $input = getInput();
    $ban_ip = sanitizeIP($input['ip'] ?? '');

    if (empty($ban_ip)) return ['code' => -1, 'msg' => '缺少IP'];

    $pdo = getDB();
    $pdo->prepare("DELETE FROM bans WHERE ip = ?")->execute([$ban_ip]);

    logAction($ip, 'admin', '管理员', '解除拉黑', "IP: $ban_ip");
    return ['code' => 0, 'msg' => '已解除拉黑'];
}

function adminSetAnnouncement($ip) {
    $auth = checkAdminAuth();
    if ($auth) return $auth;
    if (!checkCsrf()) return ['code' => -1, 'msg' => '安全校验失败，请刷新页面重试'];

    $input = getInput();
    $content = secureInput($input['content'] ?? '', 500);

    if (empty($content)) return ['code' => -1, 'msg' => '公告内容不能为空'];

    $pdo = getDB();
    $pdo->prepare("UPDATE announcements SET status = 'inactive' WHERE status = 'active'")->execute();
    $pdo->prepare("INSERT INTO announcements (content, created_by, created_at, status)
                   VALUES (?, '管理员', NOW(), 'active')")->execute([$content]);

    logAction($ip, 'admin', '管理员', '更新公告', '公告内容: ' . substr($content, 0, 50));
    return ['code' => 0, 'msg' => '公告已更新'];
}

// 把服务器日志导出成文本文件，放到保险箱目录供管理员下载
function adminExportLogs($ip) {
    $auth = checkAdminAuth();
    if ($auth) return $auth;
    if (!checkCsrf()) return ['code' => -1, 'msg' => '安全校验失败，请刷新页面重试'];

    $dir = rtrim((string)STORAGE_PATH, '/\\');
    if (!is_dir($dir) && !@mkdir($dir, 0750, true)) {
        return ['code' => -1, 'msg' => '无法创建日志目录'];
    }
    if (!is_writable($dir)) {
        return ['code' => -1, 'msg' => '日志目录不可写，请检查权限'];
    }

    $name = 'logs-' . date('Ymd-His') . '-' . bin2hex(random_bytes(3)) . '.txt';
    $path = $dir . DIRECTORY_SEPARATOR . $name;
    $fh = @fopen($path, 'wb');
    if (!$fh) return ['code' => -1, 'msg' => '无法写入日志文件'];

    $pdo = getDB();
    fwrite($fh, "SFM 服务器日志导出时间: " . date('Y-m-d H:i:s') . "\r\n\r\n");

    $sections = [
        '操作日志' => "SELECT ip, player_id, player_name, action, detail, created_at FROM action_logs ORDER BY created_at DESC LIMIT 500",
        '最近聊天' => "SELECT room_id, player_name, message, created_at FROM chat_logs ORDER BY created_at DESC LIMIT 500",
        '活跃房间' => "SELECT room_id, room_name, host_name, host_address, port, current_players, max_players, status, created_at, last_update FROM rooms ORDER BY created_at DESC LIMIT 200",
        '封禁列表' => "SELECT ip, expire_at, reason_detail, created_at FROM bans ORDER BY expire_at DESC LIMIT 200"
    ];

    foreach ($sections as $title => $sql) {
        fwrite($fh, "===== " . $title . " =====\r\n");
        try {
            $rows = $pdo->query($sql)->fetchAll();
            if (!$rows) {
                fwrite($fh, "(空)\r\n");
            } else {
                foreach ($rows as $r) {
                    $fields = [];
                    foreach ($r as $k => $v) {
                        if (is_scalar($v)) {
                            $fields[] = $k . '=' . str_replace(["\r", "\n"], ' ', (string)$v);
                        }
                    }
                    fwrite($fh, implode(' | ', $fields) . "\r\n");
                }
            }
        } catch (Throwable $e) {
            fwrite($fh, "(读取失败)\r\n");
        }
        fwrite($fh, "\r\n");
    }
    fclose($fh);

    logAction($ip, 'admin', '管理员', '导出日志', $name);
    return ['code' => 0, 'msg' => '日志已导出', 'filename' => $name];
}
