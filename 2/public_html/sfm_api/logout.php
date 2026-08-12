<?php
define('SFM_BOOT', true);

$protected_path = __DIR__ . '/../../protected/';
if (!file_exists($protected_path . 'config.php')) {
    die('系统配置缺失');
}
require_once $protected_path . 'config.php';
require_once $protected_path . 'functions.php';

secureSessionStart(adminSessionToken());
$_SESSION = [];
session_destroy();
setcookie(session_name(), '', time() - 42000, '/');

header('Location: admin.php');
exit;
