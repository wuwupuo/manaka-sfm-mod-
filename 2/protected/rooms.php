<?php
if (!defined('SFM_BOOT')) {
    http_response_code(403);
    exit('Forbidden');
}

require_once __DIR__ . '/functions.php';

function listRooms() {
    if (!checkRateLimit(getClientIP(), 'list_rooms', 120, 60)) {
        return keyMsg(-1, 'too_frequent', '操作过于频繁');
    }
    return queryRooms(sanitizeAlnum($_GET['version'] ?? ''));
}

function queryRooms($version) {
    $pdo = getDB();
    $timeout = (int)ROOM_TIMEOUT;

    // 注意：password 列只用于内部判断 has_password，不会输出给客户端
    $sql = "SELECT room_id, room_name, host_name, host_address, port,
                   password, has_password, max_players, current_players, game_version,
                   created_at, expire_at,
                   CONCAT(current_players, '/', LEAST(max_players, 10)) as player_display,
                   CASE WHEN has_password = 1 THEN '有密码' ELSE '无密码' END as password_status
            FROM rooms
            WHERE status = 'active'
              AND current_players < LEAST(max_players, 10)
              AND last_update > NOW() - INTERVAL $timeout SECOND
              AND (expire_at IS NULL OR expire_at > NOW())";
    $params = [];
    if ($version) {
        $sql .= " AND game_version = ?";
        $params[] = $version;
    }
    $sql .= " ORDER BY current_players DESC, created_at ASC LIMIT 50";

    $stmt = $pdo->prepare($sql);
    $stmt->execute($params);
    $rooms = $stmt->fetchAll();

    foreach ($rooms as &$r) {
        $r['has_password'] = !empty($r['password']) ? 1 : 0;
        $r['password_status'] = !empty($r['password']) ? 'yes' : 'no';
        unset($r['password']);
    }
    unset($r);

    $stmt2 = $pdo->prepare("SELECT content, created_at FROM announcements WHERE status = 'active' ORDER BY created_at DESC LIMIT 1");
    $stmt2->execute();
    $announcement = $stmt2->fetch();

    $stmt3 = $pdo->prepare("SELECT COUNT(*) FROM rooms WHERE status = 'active'");
    $stmt3->execute();
    $current = $stmt3->fetchColumn();

    return [
        'code' => 0,
        'rooms' => $rooms,
        'total' => count($rooms),
        'max_rooms' => MAX_ROOMS_TOTAL,
        'current_rooms' => (int)$current,
        'is_full' => $current >= MAX_ROOMS_TOTAL ? 1 : 0,
        'queue' => $current >= MAX_ROOMS_TOTAL ? 1 : 0,
        'queue_msg' => $current >= MAX_ROOMS_TOTAL ? 'rooms_full' : '',
        'announcement' => $announcement ? $announcement['content'] : '',
        'announcement_time' => $announcement ? $announcement['created_at'] : ''
    ];
}

// 批量同步：房间列表 + 公告 + 服务器时间（+ 可选房间聊天），一次请求拿全
function syncData($ip, $version = '', $room_id = '') {
    if (!checkRateLimit($ip, 'sync', 60, 60)) {
        return keyMsg(-1, 'too_frequent', '操作过于频繁');
    }
    $out = queryRooms(sanitizeAlnum($version));
    $out['key'] = 'sync_ok';
    $out['msg'] = '同步成功';
    $out['server_time'] = time();
    $out['messages'] = [];
    $room_id = sanitizeAlnum($room_id);
    if ($room_id !== '') {
        $chat = queryChat($room_id, 50);
        if (($chat['code'] ?? -1) === 0) {
            $out['messages'] = $chat['messages'];
        }
    }
    return $out;
}

function createRoom($ip) {
    $pdo = getDB();

    if (isBanned($ip)) return keyMsg(-2, 'banned', 'IP已被封禁');
    if (!checkRateLimit($ip, 'create_room', 10, 60)) {
        return keyMsg(-1, 'too_frequent', '操作过于频繁');
    }

    $stmt = $pdo->prepare("SELECT COUNT(*) FROM rooms WHERE ip = ? AND status = 'active' AND last_update > NOW() - INTERVAL 3600 SECOND");
    $stmt->execute([$ip]);
    if ($stmt->fetchColumn() >= MAX_ROOMS_PER_IP) {
        return keyMsg(-1, 'room_limit_ip', '您已有一个活跃房间，请先关闭再创建');
    }

    $stmt = $pdo->prepare("SELECT COUNT(*) FROM rooms WHERE ip = ? AND created_at > NOW() - INTERVAL 1 HOUR");
    $stmt->execute([$ip]);
    if ($stmt->fetchColumn() >= MAX_ROOMS_PER_HOUR) {
        return keyMsg(-1, 'room_limit_hour', '每小时最多创建5个房间');
    }

    $stmt = $pdo->prepare("SELECT COUNT(*) FROM rooms WHERE status = 'active'");
    $stmt->execute();
    if ($stmt->fetchColumn() >= MAX_ROOMS_TOTAL) {
        return keyMsg(-1, 'rooms_full', '服务器房间已满（最多' . MAX_ROOMS_TOTAL . '个）', ['is_full' => true]);
    }

    $input = getInput();
    if (!$input) return keyMsg(-1, 'invalid_params', '无效请求');

    $captcha = sanitizeAlnum($input['captcha'] ?? '');
    $room_name = secureInput($input['room_name'] ?? '房间', MAX_ROOM_NAME_LENGTH);
    $host_name = secureInput($input['host_name'] ?? '', MAX_NAME_LENGTH);
    $password = sanitizePassword($input['password'] ?? '');
    // 房间密码以 bcrypt 哈希存储（旧明文房间仍可兼容验证）
    $passwordHash = empty($password) ? '' : password_hash($password, PASSWORD_DEFAULT);
    $port = sanitizePort($input['port'] ?? 0);
    $max_players = min(10, max(2, intval($input['max_players'] ?? 8)));
    $game_version = sanitizeAlnum($input['game_version'] ?? '1.0.37');
    // 对外地址（樱花映射/frp 等隧道地址），可选；不填则用服务器看到的 IP
    $public_address = sanitizePublicAddress($input['public_address'] ?? '');

    $stmt = $pdo->prepare("SELECT id FROM captcha WHERE ip = ? AND code = ? AND verified = 1 AND created_at > NOW() - INTERVAL 1 HOUR");
    $stmt->execute([$ip, $captcha]);
    if (!$stmt->fetch()) {
        return keyMsg(-1, 'need_captcha', '请先验证验证码', ['need_captcha' => true]);
    }

    if (empty($host_name) || $port === 0) {
        return keyMsg(-1, 'missing_params', '缺少必要参数');
    }

    $room_id = generateRoomId();
    $token = generateToken();
    $host_address = !empty($public_address) ? $public_address : $ip . ':' . $port;
    $hasPassword = !empty($passwordHash) ? 1 : 0;
    $expire_at = date('Y-m-d H:i:s', time() + (int)ROOM_LIFETIME);

    $stmt = $pdo->prepare("INSERT INTO rooms
        (room_id, room_name, host_name, host_address, port, password, max_players,
         current_players, ip, owner_token, has_password, status, expire_at, game_version)
        VALUES (?, ?, ?, ?, ?, ?, ?, 1, ?, ?, ?, 'active', ?, ?)");

    $ok = $stmt->execute([
        $room_id, $room_name, $host_name, $host_address, $port, $passwordHash, $max_players,
        $ip, $token, $hasPassword, $expire_at, $game_version
    ]);

    if ($ok) {
        logAction($ip, 'host', $host_name, '创建房间', "房间ID: $room_id, 房间名: $room_name, 地址: $host_address");
        $pdo->prepare("DELETE FROM captcha WHERE ip = ? AND code = ?")->execute([$ip, $captcha]);
        return keyMsg(0, 'room_created', '房间创建成功', [
            'room_id' => $room_id,
            'token' => $token,
            'expire_at' => $expire_at
        ]);
    }
    return keyMsg(-1, 'create_failed', '创建失败');
}

function joinRoom($ip) {
    $pdo = getDB();

    if (isBanned($ip)) return keyMsg(-2, 'banned', 'IP已被封禁');
    if (!checkRateLimit($ip, 'join_room', 30, 60)) {
        return keyMsg(-1, 'too_frequent', '操作过于频繁');
    }

    $input = getInput();
    if (!$input) return keyMsg(-1, 'invalid_params', '无效请求');

    $room_id = sanitizeAlnum($input['room_id'] ?? '');
    $player_name = secureInput($input['player_name'] ?? '玩家', MAX_NAME_LENGTH);
    $player_id = secureInput($input['player_id'] ?? '', MAX_NAME_LENGTH);
    $password = sanitizePassword($input['password'] ?? '');

    if (empty($room_id) || empty($player_id)) {
        return keyMsg(-1, 'missing_params', '缺少参数');
    }

    $stmt = $pdo->prepare("SELECT room_id, password, host_address, port, current_players, max_players, status, ip, host_name
                           FROM rooms WHERE room_id = ? AND status = 'active'
                           AND last_update > NOW() - INTERVAL " . (int)ROOM_TIMEOUT . " SECOND
                           AND (expire_at IS NULL OR expire_at > NOW())");
    $stmt->execute([$room_id]);
    $room = $stmt->fetch();

    if (!$room) return keyMsg(-1, 'room_not_found', '房间不存在或已过期');
    if ($room['status'] !== 'active') return keyMsg(-1, 'room_closed', '房间已关闭');

    if (!empty($room['password'])) {
        $pw = $room['password'];
        $pwOk = false;
        if (strncmp($pw, '$2y$', 4) === 0 || strncmp($pw, '$2a$', 4) === 0 || strncmp($pw, '$2b$', 4) === 0) {
            $pwOk = password_verify($password, $pw); // 新房间：哈希
        } else {
            $pwOk = hash_equals($pw, $password); // 旧房间：明文兼容
        }
        if (!$pwOk) {
            logAction($ip, $player_id, $player_name, '加入失败-密码错误', "房间ID: $room_id");
            recordFailure($ip);
            return keyMsg(-1, 'wrong_password', '密码错误');
        }
    }

    if ($room['current_players'] >= $room['max_players']) {
        return keyMsg(-1, 'room_full', '房间已满');
    }

    if ($room['ip'] === $ip) {
        return keyMsg(-1, 'cannot_join_self', '不能加入自己创建的房间');
    }

    // 先记录该玩家之前在哪些房间，再删除旧记录并递减旧房间人数
    $stmt = $pdo->prepare("SELECT room_id FROM room_players WHERE player_id = ?");
    $stmt->execute([$player_id]);
    $oldRooms = $stmt->fetchAll(PDO::FETCH_COLUMN);
    $pdo->prepare("DELETE FROM room_players WHERE player_id = ?")->execute([$player_id]);
    foreach ($oldRooms as $oldRid) {
        $pdo->prepare("UPDATE rooms SET current_players = current_players - 1 WHERE room_id = ? AND current_players > 0")->execute([$oldRid]);
    }

    $pdo->beginTransaction();
    try {
        $pdo->prepare("UPDATE rooms SET current_players = current_players + 1, last_update = NOW() WHERE room_id = ?")->execute([$room_id]);
        $pdo->prepare("INSERT INTO room_players (room_id, player_name, player_id, ip) VALUES (?, ?, ?, ?)")->execute([$room_id, $player_name, $player_id, $ip]);
        $pdo->commit();
        logAction($ip, $player_id, $player_name, '加入房间', "房间ID: $room_id");
        return keyMsg(0, 'join_ok', '加入成功', [
            'host_address' => $room['host_address'],
            'port' => $room['port'],
            'room_id' => $room_id
        ]);
    } catch (Exception $e) {
        $pdo->rollBack();
        return keyMsg(-1, 'join_failed', '加入失败');
    }
}

function leaveRoom($ip) {
    $pdo = getDB();
    $input = getInput();
    if (!$input) return keyMsg(-1, 'invalid_params', '无效请求');

    $room_id = sanitizeAlnum($input['room_id'] ?? '');
    $player_id = secureInput($input['player_id'] ?? '', MAX_NAME_LENGTH);
    $token = $input['token'] ?? '';

    if (empty($room_id)) return keyMsg(-1, 'missing_params', '缺少房间ID');

    $pdo->beginTransaction();
    try {
        $is_host = false;
        $host_name = '';

        if (!empty($token)) {
            $stmt = $pdo->prepare("SELECT owner_token, host_name FROM rooms WHERE room_id = ?");
            $stmt->execute([$room_id]);
            $room = $stmt->fetch();
            if ($room && !empty($room['owner_token']) && hash_equals($room['owner_token'], $token)) {
                $is_host = true;
                $host_name = $room['host_name'];
            }
        }

        if (!$is_host && !empty($player_id)) {
            $stmt = $pdo->prepare("SELECT ip, host_name FROM rooms WHERE room_id = ?");
            $stmt->execute([$room_id]);
            $room = $stmt->fetch();
            if ($room && $room['ip'] === $ip) {
                $is_host = true;
                $host_name = $room['host_name'];
            }
        }

        if ($is_host) {
            $pdo->prepare("DELETE FROM rooms WHERE room_id = ?")->execute([$room_id]);
            $pdo->prepare("DELETE FROM room_players WHERE room_id = ?")->execute([$room_id]);
            $pdo->prepare("DELETE FROM chat_logs WHERE room_id = ?")->execute([$room_id]);
            logAction($ip, 'host', $host_name ?: '房主', '关闭房间', "房间ID: $room_id");
            $pdo->commit();
            return keyMsg(0, 'room_closed', '房间已关闭');
        }

        $pdo->prepare("UPDATE rooms SET current_players = current_players - 1 WHERE room_id = ? AND current_players > 0")->execute([$room_id]);
        if (!empty($player_id)) {
            $pdo->prepare("DELETE FROM room_players WHERE room_id = ? AND player_id = ?")->execute([$room_id, $player_id]);
        }
        logAction($ip, $player_id, '', '离开房间', "房间ID: $room_id");
        $pdo->commit();
        return keyMsg(0, 'left_room', '已离开房间');
    } catch (Exception $e) {
        $pdo->rollBack();
        return keyMsg(-1, 'operation_failed', '操作失败');
    }
}

function heartbeat($ip) {
    $pdo = getDB();
    if (!checkRateLimit($ip, 'heartbeat', 360, 60)) {
        return keyMsg(-1, 'too_frequent', '操作过于频繁');
    }
    $input = getInput();
    if (!$input) return keyMsg(-1, 'invalid_params', '无效请求');

    $room_id = sanitizeAlnum($input['room_id'] ?? '');
    $token = $input['token'] ?? '';

    if (empty($room_id)) return keyMsg(-1, 'missing_params', '缺少房间ID');

    $stmt = $pdo->prepare("SELECT owner_token, ip FROM rooms WHERE room_id = ? AND status = 'active'");
    $stmt->execute([$room_id]);
    $room = $stmt->fetch();

    if (!$room) return keyMsg(-1, 'room_not_found', '房间不存在');

    // 优先要求 token；旧房间（无 token）才允许同 IP 续命
    $valid = false;
    if (!empty($room['owner_token']) && !empty($token) && hash_equals($room['owner_token'], $token)) $valid = true;
    if (!$valid && empty($room['owner_token']) && $room['ip'] === $ip) $valid = true;

    if (!$valid) return keyMsg(-1, 'auth_failed', '身份验证失败');

    $pdo->prepare("UPDATE rooms SET last_update = NOW() WHERE room_id = ?")->execute([$room_id]);
    return keyMsg(0, 'heartbeat_ok', '心跳正常');
}

function saveChat($ip) {
    $pdo = getDB();
    if (!checkRateLimit($ip, 'chat', 10, 10)) {
        return keyMsg(-1, 'too_frequent', '发言过于频繁');
    }
    $input = getInput();
    if (!$input) return keyMsg(-1, 'invalid_params', '无效请求');

    $room_id = sanitizeAlnum($input['room_id'] ?? '');
    $player_id = secureInput($input['player_id'] ?? '', MAX_NAME_LENGTH);
    $player_name = secureInput($input['player_name'] ?? '玩家', MAX_NAME_LENGTH);
    $message = sanitizeChat($input['message'] ?? '');

    if (empty($room_id) || empty($player_id) || empty($message)) {
        return keyMsg(-1, 'missing_params', '缺少参数');
    }

    $stmt = $pdo->prepare("SELECT 1 FROM rooms WHERE room_id = ? AND status = 'active'");
    $stmt->execute([$room_id]);
    if (!$stmt->fetch()) {
        return keyMsg(-1, 'room_not_found', '房间已不存在');
    }

    $pdo->prepare("INSERT INTO chat_logs (room_id, player_id, player_name, message, created_at)
                   VALUES (?, ?, ?, ?, NOW())")->execute([$room_id, $player_id, $player_name, $message]);
    return keyMsg(0, 'chat_sent', '发送成功');
}

function getChat() {
    if (!checkRateLimit(getClientIP(), 'get_chat', 120, 60)) {
        return keyMsg(-1, 'too_frequent', '操作过于频繁');
    }
    $room_id = sanitizeAlnum($_GET['room_id'] ?? '');
    $limit = min(100, max(1, intval($_GET['limit'] ?? 50)));
    return queryChat($room_id, $limit);
}

function queryChat($room_id, $limit = 50) {
    $pdo = getDB();
    $room_id = sanitizeAlnum($room_id);
    $limit = min(100, max(1, intval($limit)));

    if (empty($room_id)) return keyMsg(-1, 'room_required', '缺少房间ID');

    $stmt = $pdo->prepare("SELECT player_name, message, created_at FROM chat_logs
                           WHERE room_id = ? ORDER BY created_at DESC LIMIT ?");
    $stmt->bindValue(1, $room_id, PDO::PARAM_STR);
    $stmt->bindValue(2, $limit, PDO::PARAM_INT);
    $stmt->execute();
    $messages = $stmt->fetchAll();
    return keyMsg(0, 'chat_ok', '获取成功', ['messages' => array_reverse($messages)]);
}

function getCaptcha($ip) {
    $pdo = getDB();
    if (isBanned($ip)) return keyMsg(-2, 'banned', 'IP已被封禁');
    if (!checkRateLimit($ip, 'get_captcha', 30, 60)) {
        return keyMsg(-1, 'too_frequent', '操作过于频繁');
    }

    $stmt = $pdo->prepare("SELECT code FROM captcha WHERE ip = ? AND verified = 0 AND created_at > NOW() - INTERVAL 1 HOUR");
    $stmt->execute([$ip]);
    $row = $stmt->fetch();

    if ($row) {
        $code = $row['code'];
    } else {
        $code = generateCaptcha();
        $pdo->prepare("INSERT INTO captcha (ip, code, created_at) VALUES (?, ?, NOW())")->execute([$ip, $code]);
    }
    return keyMsg(0, 'captcha_get', '验证码已生成', ['captcha' => $code]);
}

function verifyCaptcha($ip) {
    if (!checkRateLimit($ip, 'verify_captcha', 30, 60)) {
        return keyMsg(-1, 'too_frequent', '操作过于频繁');
    }
    $input = getInput();
    $code = sanitizeAlnum($input['code'] ?? '');

    $pdo = getDB();
    $stmt = $pdo->prepare("SELECT id FROM captcha WHERE ip = ? AND code = ? AND verified = 0 AND created_at > NOW() - INTERVAL 1 HOUR");
    $stmt->execute([$ip, $code]);
    $row = $stmt->fetch();

    if ($row) {
        $pdo->prepare("UPDATE captcha SET verified = 1 WHERE id = ?")->execute([$row['id']]);
        return keyMsg(0, 'captcha_ok', '验证通过', ['verified' => 1]);
    }
    return keyMsg(-1, 'captcha_invalid', '验证码错误或已过期', ['verified' => 0]);
}
