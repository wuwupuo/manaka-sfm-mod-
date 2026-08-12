<?php
if (!defined('SFM_BOOT')) {
    http_response_code(403);
    exit('Forbidden');
}

require_once __DIR__ . '/functions.php';

function getAnnouncement() {
    $pdo = getDB();
    $stmt = $pdo->prepare("SELECT content, created_at FROM announcements WHERE status = 'active' ORDER BY created_at DESC LIMIT 1");
    $stmt->execute();
    $row = $stmt->fetch();
    
    if ($row) {
        return ['code' => 0, 'content' => $row['content'], 'created_at' => $row['created_at']];
    }
    return ['code' => 0, 'content' => '', 'created_at' => ''];
}
