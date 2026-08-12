<?php
// ============================================================
// 公共 API 入口（玩家访问）
// 所有请求返回统一 JSON：{ "code":0/…, "msg":…, ... }
// ============================================================
define('SFM_BOOT', true);

header('Content-Type: application/json; charset=utf-8');
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type, X-SFM-Token, X-SFM-CSRF');
header('X-Content-Type-Options: nosniff');
header('X-Frame-Options: DENY');
header('Referrer-Policy: no-referrer');
header('Cache-Control: no-store');

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit;
}

$protected_path = __DIR__ . '/../../protected/';
if (!file_exists($protected_path . 'config.php')) {
    http_response_code(500);
    die(json_encode(['code' => -1, 'msg' => '系统配置缺失']));
}

require_once $protected_path . 'config.php';
require_once $protected_path . 'functions.php';
require_once $protected_path . 'rooms.php';
require_once $protected_path . 'announcement.php';
require_once $protected_path . 'admin.php';

$pdo = getDB();
$ip = getClientIP();
$method = $_SERVER['REQUEST_METHOD'];

// 清理任务按概率执行（约1/5），减轻国外主机每请求的数据库压力
if (random_int(1, 5) === 1) {
    cleanOldData();
}

$action = $_GET['action'] ?? '';

try {
    switch ($action) {
        case 'ping':
            respond(['code' => 0, 'key' => 'ping_ok', 'name' => 'sfm', 'version' => '1', 'server_time' => time()]);

        case 'sync':
            if ($method !== 'POST') respond(['code' => -1, 'key' => 'method', 'msg' => '只支持POST']);
            $input = getInput();
            $room_id = is_array($input) ? ($input['room_id'] ?? '') : '';
            respond(syncData($ip, $_GET['version'] ?? '', $room_id));

        case 'list':
            respond(listRooms());

        case 'get_captcha':
            respond(getCaptcha($ip));

        case 'verify_captcha':
            if ($method !== 'POST') respond(['code' => -1, 'msg' => '只支持POST']);
            respond(verifyCaptcha($ip));

        case 'create':
            if ($method !== 'POST') respond(['code' => -1, 'msg' => '只支持POST']);
            respond(createRoom($ip));

        case 'join':
            if ($method !== 'POST') respond(['code' => -1, 'msg' => '只支持POST']);
            respond(joinRoom($ip));

        case 'leave':
            if ($method !== 'POST') respond(['code' => -1, 'msg' => '只支持POST']);
            respond(leaveRoom($ip));

        case 'heartbeat':
            if ($method !== 'POST') respond(['code' => -1, 'msg' => '只支持POST']);
            respond(heartbeat($ip));

        case 'chat':
            if ($method !== 'POST') respond(['code' => -1, 'msg' => '只支持POST']);
            respond(saveChat($ip));

        case 'get_chat':
            respond(getChat());

        case 'get_announcement':
            respond(getAnnouncement());

        // ========== 管理员 JSON 接口（需登录 + CSRF）==========
        case 'admin_info':
            respond(adminInfo());
        case 'admin_list':
            respond(adminListRooms());
        case 'admin_delete':
            if ($method !== 'POST') respond(['code' => -1, 'msg' => '只支持POST']);
            respond(adminDeleteRoom($ip));
        case 'admin_ban':
            if ($method !== 'POST') respond(['code' => -1, 'msg' => '只支持POST']);
            respond(adminBanIP($ip));
        case 'admin_unban':
            if ($method !== 'POST') respond(['code' => -1, 'msg' => '只支持POST']);
            respond(adminUnbanIP($ip));
        case 'admin_set_announcement':
            if ($method !== 'POST') respond(['code' => -1, 'msg' => '只支持POST']);
            respond(adminSetAnnouncement($ip));
        case 'admin_export_logs':
            if ($method !== 'POST') respond(['code' => -1, 'msg' => '只支持POST']);
            respond(adminExportLogs($ip));

        default:
            respond(['code' => -1, 'msg' => '未知操作', 'actions' => [
                'ping', 'sync', 'list', 'get_captcha', 'verify_captcha', 'create', 'join', 'leave',
                'heartbeat', 'chat', 'get_chat', 'get_announcement',
                'admin_info', 'admin_list', 'admin_delete', 'admin_ban', 'admin_unban',
                'admin_set_announcement', 'admin_export_logs'
            ]]);
    }
} catch (Throwable $e) {
    respond(['code' => -1, 'msg' => '服务器内部错误']);
}
