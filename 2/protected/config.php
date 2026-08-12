<?php
// ============================================================
// 🔐 数据库配置 - 改成你的实际信息
// ============================================================

// 防直连守卫：所有 protected 文件只能由入口脚本（index.php/login.php 等）
// 加载。即使 .htaccess 失效，直接访问 /protected/ 下的任何文件也会被拦截。
if (!defined('SFM_BOOT')) {
    http_response_code(403);
    exit('Forbidden');
}

// ------------------------------------------------------------
// 配置加密密钥（BASE64，32字节）。留空 = 不加密，直接写明文。
// 生成：php -r "echo base64_encode(random_bytes(32));"
// 也可以用 /sfm_api/setup.php 网页助手生成。
// ------------------------------------------------------------
define('CONFIG_KEY', '');

// ------------------------------------------------------------
// 数据库（根据你的 phpMyAdmin 填写）
// 敏感值可写成 enc:密文（用 setup.php 生成，前提是 CONFIG_KEY 已设置）
// ------------------------------------------------------------
define('DB_HOST', 'localhost');        // 通常是 localhost
define('DB_NAME', 'serv7t1ctt9riuv');   // ← 你的数据库名
define('DB_USER', 'serv7t1ctt9riuv');   // ← 你的用户名
$rawDbPass = '你的数据库密码';           // ← 你的密码（可 enc: 密文）

// ------------------------------------------------------------
// 管理员密码（登录管理后台、客户端文件保险箱都用它）
// 强烈建议使用 bcrypt 哈希：浏览器打开
//   https://你的域名/sfm_api/setup.php?k=SETUP_KEY
// 生成后把 $2y$ 开头的结果粘到下面。
// 留空、仍为示例值、或明文长度不足 8 位时，系统会拒绝一切登录（安全关闭）。
// ------------------------------------------------------------
$rawAdminToken = '';

// ------------------------------------------------------------
// 安全设置
// ------------------------------------------------------------
// setup.php 的访问钥匙（务必改成任意长随机串，最好 20 位以上）。
// 用完 setup.php 后应删除该文件。
define('SETUP_KEY', 'CHANGE_ME_改成任意长随机字符串');

// 1=管理员会话绑定登录IP（防会话被偷用）；0=不绑定
define('SESSION_BIND_IP', 1);

// 管理员会话有效期（秒），默认 8 小时
define('SESSION_LIFETIME', 28800);

// 登录防爆破：窗口内最多尝试次数
define('ADMIN_LOGIN_MAX_TRIES', 5);
define('ADMIN_LOGIN_WINDOW', 300);

// 明文管理员密码最短长度（bcrypt 哈希不受此限制）
define('MIN_ADMIN_PASSWORD_LEN', 8);

// 客户端文件保险箱存储目录（在 protected 内，网址直接访问不到）
define('STORAGE_PATH', __DIR__ . '/storage');

// ------------------------------------------------------------
// 房间限制
// ------------------------------------------------------------
define('MAX_ROOMS_TOTAL', 100);
define('MAX_ROOMS_PER_IP', 1);
define('MAX_ROOMS_PER_HOUR', 5);
define('ROOM_LIFETIME', 43200);   // 12小时
define('ROOM_TIMEOUT', 60);       // 60秒无心跳删除
define('CAPTCHA_EXPIRE', 3600);   // 1小时

// 输入限制
define('MAX_CHAT_LENGTH', 1000);
define('MAX_NAME_LENGTH', 40);
define('MAX_ROOM_NAME_LENGTH', 40);
define('MAX_REQUEST_SIZE', 10485760);  // 10MB

// 日志保留天数
define('LOG_RETENTION_DAYS', 2);

// 黑名单文件路径
define('BLACKLIST_FILE', __DIR__ . '/blacklist.txt');

// 可信代理（CDN/反向代理的真实 IP 列表，逗号分隔）。
// 留空 = 忽略 X_FORWARDED_FOR，防止玩家伪造 IP 绕过封禁/限频。
define('TRUSTED_PROXIES', '');

// ------------------------------------------------------------
// 运行环境
// ------------------------------------------------------------
error_reporting(0);
ini_set('display_errors', 0);
ini_set('post_max_size', '10M');
ini_set('upload_max_filesize', '10M');
date_default_timezone_set('Asia/Shanghai');

// 如果配置了密钥，解密 enc: 开头的敏感值
if (defined('CONFIG_KEY') && CONFIG_KEY !== '') {
    require_once __DIR__ . '/security.php';
    define('DB_PASS', cfgResolve($rawDbPass));
    define('ADMIN_TOKEN', cfgResolve($rawAdminToken));
} else {
    define('DB_PASS', $rawDbPass);
    define('ADMIN_TOKEN', $rawAdminToken);
}
