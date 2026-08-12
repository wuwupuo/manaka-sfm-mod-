-- ============================================================
-- SFM 联机服务器数据库表（可在 phpMyAdmin 中导入）
-- 所有 CREATE TABLE 均为“不存在才创建”，重复执行不会破坏数据。
-- 如果之前已建过表，请只核对列名；本脚本不会修改旧表结构。
-- ============================================================

CREATE TABLE IF NOT EXISTS rooms (
  id INT AUTO_INCREMENT PRIMARY KEY,
  room_id VARCHAR(16) NOT NULL,
  room_name VARCHAR(40) NOT NULL DEFAULT '',
  host_name VARCHAR(40) NOT NULL DEFAULT '',
  host_address VARCHAR(191) NOT NULL DEFAULT '',
  port INT NOT NULL DEFAULT 0,
  password VARCHAR(255) NOT NULL DEFAULT '',
  max_players INT NOT NULL DEFAULT 8,
  current_players INT NOT NULL DEFAULT 0,
  ip VARCHAR(64) NOT NULL DEFAULT '',
  owner_token VARCHAR(128) NOT NULL DEFAULT '',
  has_password TINYINT NOT NULL DEFAULT 0,
  status VARCHAR(16) NOT NULL DEFAULT 'active',
  expire_at DATETIME NULL,
  game_version VARCHAR(32) NOT NULL DEFAULT '',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  last_update DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_room_id (room_id),
  KEY idx_room_status (status, last_update),
  KEY idx_room_ip (ip)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS room_players (
  id INT AUTO_INCREMENT PRIMARY KEY,
  room_id VARCHAR(16) NOT NULL,
  player_id VARCHAR(40) NOT NULL,
  player_name VARCHAR(40) NOT NULL DEFAULT '',
  ip VARCHAR(64) NOT NULL DEFAULT '',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_rp_room (room_id),
  UNIQUE KEY uq_rp_player (player_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS captcha (
  id INT AUTO_INCREMENT PRIMARY KEY,
  ip VARCHAR(64) NOT NULL DEFAULT '',
  code VARCHAR(8) NOT NULL DEFAULT '',
  verified TINYINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_captcha_ip (ip, verified, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS bans (
  id INT AUTO_INCREMENT PRIMARY KEY,
  ip VARCHAR(64) NOT NULL DEFAULT '',
  expire_at DATETIME NOT NULL,
  reason_detail VARCHAR(255) NOT NULL DEFAULT '',
  created_by VARCHAR(40) NOT NULL DEFAULT '',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_ban_ip (ip)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS failures (
  id INT AUTO_INCREMENT PRIMARY KEY,
  ip VARCHAR(64) NOT NULL DEFAULT '',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_fail_ip (ip, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS action_logs (
  id INT AUTO_INCREMENT PRIMARY KEY,
  ip VARCHAR(64) NOT NULL DEFAULT '',
  player_id VARCHAR(40) NOT NULL DEFAULT '',
  player_name VARCHAR(40) NOT NULL DEFAULT '',
  action VARCHAR(64) NOT NULL DEFAULT '',
  detail VARCHAR(255) NOT NULL DEFAULT '',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_log_ip (ip, created_at),
  KEY idx_log_action (action, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS chat_logs (
  id INT AUTO_INCREMENT PRIMARY KEY,
  room_id VARCHAR(16) NOT NULL,
  player_id VARCHAR(40) NOT NULL DEFAULT '',
  player_name VARCHAR(40) NOT NULL DEFAULT '',
  message TEXT NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_chat_room (room_id, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS announcements (
  id INT AUTO_INCREMENT PRIMARY KEY,
  content TEXT NOT NULL,
  created_by VARCHAR(40) NOT NULL DEFAULT '',
  status VARCHAR(16) NOT NULL DEFAULT 'active',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_ann_status (status, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS rate_limits (
  id INT AUTO_INCREMENT PRIMARY KEY,
  ip VARCHAR(64) NOT NULL,
  action VARCHAR(32) NOT NULL,
  hit_at DATETIME NOT NULL,
  KEY idx_ratelimit (ip, action, hit_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
