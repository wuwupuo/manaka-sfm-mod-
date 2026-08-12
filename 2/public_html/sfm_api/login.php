<?php
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
if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    die(json_encode(['code' => -1, 'msg' => '只支持POST']));
}

$protected_path = __DIR__ . '/../../protected/';
if (!file_exists($protected_path . 'config.php')) {
    http_response_code(500);
    die(json_encode(['code' => -1, 'msg' => '系统配置缺失']));
}
require_once $protected_path . 'config.php';
require_once $protected_path . 'functions.php';

$ip = getClientIP();
$input = getInput();
$password = is_array($input) ? ($input['password'] ?? '') : '';

$result = adminLoginAttempt($ip, $password);
if ($result['code'] === 0) {
    // 游戏客户端用 Token（会话ID）代替 Cookie 鉴权
    $result['token'] = session_id();
}
respond($result);
