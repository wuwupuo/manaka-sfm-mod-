<?php
// ============================================================
// ⬇ 保险箱文件下载（需登录，禁止路径穿越）
// ============================================================
define('SFM_BOOT', true);

$protected_path = __DIR__ . '/../protected/';
if (!file_exists($protected_path . 'config.php')) {
    http_response_code(500);
    die('系统配置缺失');
}
require_once $protected_path . 'config.php';
require_once $protected_path . 'functions.php';

sendSecurityHeaders();
header('Cache-Control: no-store');

if (!isAdminLoggedIn()) {
    http_response_code(401);
    die('请先登录保险箱');
}

$ip = getClientIP();
if (!checkRateLimit($ip, 'file_download', 60, 60)) {
    http_response_code(429);
    die('下载过于频繁');
}

$name = isset($_GET['f']) ? basename((string)$_GET['f']) : '';
if ($name === '' || $name === '.' || $name === '..' || strpbrk($name, '/\\') !== false) {
    http_response_code(400);
    die('文件名不合法');
}

$base = realpath(rtrim((string)STORAGE_PATH, '/\\'));
$path = rtrim((string)STORAGE_PATH, '/\\') . DIRECTORY_SEPARATOR . $name;
$real = realpath($path);
if ($base === false || $real === false || strpos($real, $base . DIRECTORY_SEPARATOR) !== 0 || !is_file($real)) {
    http_response_code(404);
    die('文件不存在');
}

logAction($ip, 'admin', '管理员', '下载客户端文件', $name);
sendFileDownload($real, $name);
