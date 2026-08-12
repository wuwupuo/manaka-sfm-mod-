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

$protected_path = __DIR__ . '/../../protected/';
if (file_exists($protected_path . 'config.php')) {
    require_once $protected_path . 'config.php';
    require_once $protected_path . 'functions.php';
    $logged = isAdminLoggedIn();
    respond([
        'code' => $logged ? 0 : -1,
        'msg' => $logged ? '已登录' : '未登录',
        'configured' => isAdminConfigured() ? 1 : 0
    ]);
}

die(json_encode(['code' => -1, 'msg' => '系统配置缺失']));
