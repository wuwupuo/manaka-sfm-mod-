<?php
if (!defined('SFM_BOOT')) {
    http_response_code(403);
    exit('Forbidden');
}

// ============================================================
// 可选配置加密：AES-256-GCM
// 用途：保护 config.php 里的数据库密码/管理员密码等敏感值
//
// 生成密钥（在你的电脑上执行）：
//   php -r "echo base64_encode(random_bytes(32));"
// 生成加密值（把明文和密钥替换后执行）：
//   php -r "require 'security.php'; echo cfgEnc('你的明文', '你的BASE64密钥');"
//
// 注意：密钥文件同样在服务器上，这属于“防误看/防备份泄露”级别，
// 真正传输安全要靠 HTTPS；生产环境请把 protected 放到网站根目录之外。
// ============================================================

function cfgEnc($plain, $keyBase64) {
    $key = base64_decode($keyBase64);
    if ($key === false || strlen($key) !== 32) return null;
    $iv = random_bytes(12);
    $tag = '';
    $cipher = openssl_encrypt($plain, 'aes-256-gcm', $key, OPENSSL_RAW_DATA, $iv, $tag, '', 16);
    if ($cipher === false) return null;
    return base64_encode($iv . $tag . $cipher);
}

function cfgDec($cipherBase64, $keyBase64) {
    $key = base64_decode($keyBase64);
    if ($key === false || strlen($key) !== 32) return null;
    $raw = base64_decode($cipherBase64, true);
    if ($raw === false || strlen($raw) < 12 + 16) return null;
    $iv = substr($raw, 0, 12);
    $tag = substr($raw, 12, 16);
    $cipher = substr($raw, 12 + 16);
    $plain = openssl_decrypt($cipher, 'aes-256-gcm', $key, OPENSSL_RAW_DATA, $iv, $tag);
    return $plain === false ? null : $plain;
}

function cfgResolve($value) {
    if (!is_string($value) || strncmp($value, 'enc:', 4) !== 0) return $value;
    $dec = cfgDec(substr($value, 4), CONFIG_KEY);
    return $dec === null ? '' : $dec;
}
