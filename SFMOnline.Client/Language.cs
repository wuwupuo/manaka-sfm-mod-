using System;
using System.Collections.Generic;

namespace SFMOnline
{
    public enum Language
    {
        Chinese,
        English
    }

    internal static class Lang
    {
        public static Language Current = Language.Chinese;

        private static readonly Dictionary<string, Dictionary<Language, string>> _strings = new Dictionary<string, Dictionary<Language, string>>();

        static Lang()
        {
            // ========== 通用 ==========
            Add("title", "SFM 在线联机", "SFM Online");
            Add("status", "状态", "Status");
            Add("nickname", "昵称", "Nickname");
            Add("port", "端口", "Port");
            Add("password", "密码", "Password");
            Add("max_players", "最大人数", "Max Players");
            Add("server_address", "服务器地址", "Server Address");
            Add("no_connection", "未联机", "Not Connected");
            Add("connected", "已连接", "Connected");
            Add("hosting", "已开房（我是主机）", "Hosting (I am host)");
            Add("connecting", "正在连接...", "Connecting...");
            Add("refresh", "刷新", "Refresh");
            Add("loading", "加载中...", "Loading...");
            Add("offline", "离线", "Offline");
            Add("online", "在线", "Online");
            Add("unknown", "未知", "Unknown");
            Add("yes", "是", "Yes");
            Add("no", "否", "No");
            Add("close", "关闭", "Close");
            Add("back", "返回", "Back");

            // ========== 菜单标题 ==========
            Add("menu_host", "── 主机（开房间）──", "── Host (Create Room) ──");
            Add("menu_join", "── 加入房间 ──", "── Join Room ──");
            Add("menu_server", "── 公共服务器 ──", "── Public Server ──");
            Add("menu_local", "── 本地测试 ──", "── Local Test ──");
            Add("menu_settings", "── 设置 ──", "── Settings ──");
            Add("menu_chat", "── 聊天 ──", "── Chat ──");
            Add("menu_chat_room", "房间", "Room");
            Add("menu_players", "── 玩家列表 ──", "── Player List ──");
            Add("menu_tunnel", "── 内网穿透 / 樱花映射 ──", "── Tunnel / SakuraFRP ──");

            // ========== 按钮 ==========
            Add("btn_create_room", "开启房间（我做主机）", "Create Room (Host)");
            Add("btn_close_room", "关闭房间", "Close Room");
            Add("btn_join_room", "连接房间", "Join Room");
            Add("btn_disconnect", "断开连接", "Disconnect");
            Add("btn_follow", "召集全员到我这里", "Summon All Here");
            Add("btn_send", "发送", "Send");
            Add("btn_confirm", "确定", "Confirm");
            Add("btn_cancel", "取消", "Cancel");
            Add("btn_connect_server", "连接服务器", "Connect Server");
            Add("btn_disconnect_server", "断开服务器", "Disconnect Server");
            Add("btn_create_server_room", "创建房间", "Create Room");
            Add("btn_delete_room", "删除房间", "Delete Room");
            Add("btn_refresh_list", "刷新列表", "Refresh List");
            Add("btn_admin_login", "管理员登录", "Admin Login");
            Add("btn_admin_logout", "退出管理", "Admin Logout");
            Add("btn_get_captcha", "获取验证码", "Get Captcha");
            Add("btn_verify", "验证", "Verify");
            Add("btn_copy_sakura", "复制樱花参数", "Copy Sakura Config");
            Add("btn_copy_frp", "复制frp参数", "Copy frp Config");
            Add("btn_toggle_sim", "生成模拟玩家（本地测试分身）", "Spawn Sim Player (Local Test)");
            Add("btn_remove_sim", "移除模拟玩家", "Remove Sim Player");
            Add("btn_ghost_debug", "分身调试", "Ghost Debug");
            Add("btn_dump_diag", "输出诊断", "Dump Diagnostic");
            Add("btn_copy", "复制", "Copy");

            // ========== 提示 ==========
            Add("toast_host_start", "已开启房间，端口 {0}。局域网玩家输入 {1}:{0} 加入", "Room opened on port {0}. LAN players enter {1}:{0}");
            Add("toast_host_stop", "房间已关闭", "Room closed");
            Add("toast_join_fail", "连接失败：{0}", "Connection failed: {0}");
            Add("toast_join_success", "已加入房间，我的ID: {0}", "Joined room, my ID: {0}");
            Add("toast_disconnect", "已断开连接", "Disconnected");
            Add("toast_room_full", "服务器房间已满（最多{0}个）", "Server full (max {0})");
            Add("toast_captcha", "验证码: {0}", "Captcha: {0}");
            Add("toast_captcha_image", "验证码已生成，请看图片并输入", "Captcha generated, look at the image and enter it");
            Add("toast_captcha_ok", "验证通过！可以创建房间了", "Verified! You can create a room now");
            Add("toast_captcha_fail", "验证码错误，请重新获取", "Invalid captcha, please retry");
            Add("toast_server_connect", "正在连接服务器 {0}:{1} ...", "Connecting to server {0}:{1} ...");
            Add("toast_server_connected", "已连接到服务器", "Connected to server");
            Add("toast_server_disconnected", "已断开服务器连接", "Disconnected from server");
            Add("toast_room_created", "房间创建成功！房间ID: {0}", "Room created! Room ID: {0}");
Add("toast_room_deleted", "房间已删除", "Room deleted");
Add("toast_room_deleted_by_admin", "房间已被管理员关闭", "Room closed by admin");
Add("toast_room_join", "已加入房间 {0}", "Joined room {0}");
Add("my_room", "我的房间", "My Room");
Add("btn_close_server_room", "关闭房间（服务器）", "Close Room (Server)");
            Add("toast_admin_login_ok", "管理员登录成功", "Admin login successful");
            Add("toast_admin_login_fail", "管理员登录失败", "Admin login failed");
            Add("toast_nickname_required", "请先在上方填写昵称再连接服务器", "Enter a nickname before connecting");
            Add("toast_copied", "已复制到剪贴板", "Copied to clipboard");
            Add("toast_clipboard_unavailable", "剪贴板不可用", "Clipboard unavailable");
            Add("toast_port_occupied", "开启房间失败：端口 {0} 可能被占用", "Failed to open room: port {0} may be in use");
            Add("toast_follow", "已召集全员", "Summoned all players");
            Add("toast_follow_timeout", "跟随传送超时，请手动前往", "Follow timeout, please go manually");
            Add("toast_follow_ok", "已跟随到目标场景", "Followed to target scene");
            Add("toast_cant_follow", "当前无法传送", "Cannot teleport now");
            Add("toast_sim_spawn", "已生成模拟玩家，按 F9 可查看分身调试信息", "Sim player spawned, press F9 for debug info");
            Add("toast_sim_remove", "已移除模拟玩家", "Removed sim player");
            Add("toast_ghost_debug_on", "分身调试信息已开启", "Ghost debug info enabled");
            Add("toast_ghost_debug_off", "分身调试信息已关闭", "Ghost debug info disabled");
            Add("toast_diag_done", "诊断已输出到日志", "Diagnostic output to log");
            Add("toast_reconnect", "连接断开，{0}/10 次重连中...", "Disconnected, reconnecting {0}/10...");
Add("toast_reconnect_fail", "自动重连失败，请按 F10 打开联机菜单", "Auto-reconnect failed, press F10 to connect");
            Add("toast_reconnect_ok", "重连成功", "Reconnected");
            Add("toast_follow_warp", "正在传送到 {0} ...", "Teleporting to {0} ...");
            Add("toast_follow_warp_fail", "传送失败: {0}", "Teleport failed: {0}");
            Add("toast_need_captcha", "请先获取并验证验证码", "Please get and verify captcha first");
            Add("toast_captcha_input", "请在聊天框输入验证码", "Please enter captcha in chat");
            Add("toast_room_closed", "房间已关闭", "Room closed");
            Add("toast_room_expired", "房间已过期", "Room expired");
            Add("toast_room_full_join", "房间已满", "Room full");
            Add("toast_wrong_password", "密码错误", "Wrong password");
            Add("toast_cant_join_self", "不能加入自己创建的房间", "Cannot join your own room");
            Add("toast_already_host", "你是主机，不能同时加入其他房间", "You are host, cannot join other rooms");
            Add("toast_unknown", "未知", "Unknown");

            // ========== 服务器下发字段键（客户端本地翻译） ==========
            Add("banned", "IP已被封禁", "IP is banned");
            Add("too_frequent", "操作过于频繁，请稍后再试", "Too many requests, try again later");
            Add("room_limit_ip", "您已有一个活跃房间，请先关闭再创建", "You already have an active room");
            Add("room_limit_hour", "每小时最多创建5个房间", "Max 5 rooms per hour");
            Add("rooms_full", "服务器房间已满（最多{0}个）", "Server full (max {0})");
            Add("invalid_params", "无效请求", "Invalid request");
            Add("missing_params", "缺少参数", "Missing parameters");
            Add("create_failed", "创建失败", "Failed to create room");
            Add("room_created", "房间创建成功！房间ID: {0}", "Room created! Room ID: {0}");
            Add("join_ok", "加入成功", "Joined");
            Add("join_failed", "加入失败", "Failed to join");
            Add("room_not_found", "房间不存在或已过期", "Room not found or expired");
            Add("room_full", "房间已满", "Room full");
            Add("left_room", "已离开房间", "Left room");
            Add("operation_failed", "操作失败", "Operation failed");
            Add("auth_failed", "身份验证失败", "Authentication failed");
            Add("heartbeat_ok", "心跳正常", "Heartbeat OK");
            Add("chat_sent", "发送成功", "Sent");
            Add("chat_ok", "获取成功", "OK");
            Add("room_required", "缺少房间ID", "Room ID required");
            Add("captcha_get", "验证码已生成", "Captcha generated");
            Add("captcha_invalid", "验证码错误或已过期", "Invalid or expired captcha");
            Add("sync_ok", "同步成功", "Synced");
            Add("admin_not_configured", "管理员尚未配置", "Admin not configured");
            Add("admin_login_denied", "管理会话已失效，请重新登录", "Admin session expired, login again");
            Add("toast_server_connect_fail", "无法自动定位服务器接口，请检查域名/端口", "Cannot locate server API, check domain/port");

            // ========== 房间列表字段 ==========
            Add("field_room_id", "房间ID", "Room ID");
            Add("field_room_name", "房间名", "Room Name");
Add("field_public_address", "对外地址(穿透)", "Tunnel Address");
Add("field_public_address_hint", "留空=自动用你的IP:端口；樱花映射填 节点地址:端口", "Empty = auto IP:port; SakuraFRP: node:port");
            Add("field_host_name", "房主", "Host");
            Add("field_player_display", "人数", "Players");
            Add("field_has_password", "密码状态", "Password");
            Add("field_password_yes", "有密码", "Has Password");
            Add("field_password_no", "无密码", "No Password");
            Add("field_status", "状态", "Status");
            Add("field_status_active", "活跃", "Active");
            Add("field_status_full", "已满", "Full");
            Add("field_status_closed", "已关闭", "Closed");
            Add("field_status_expired", "已过期", "Expired");
            Add("field_created_at", "创建时间", "Created");
            Add("field_expire_at", "过期时间", "Expires");
            Add("field_game_version", "版本", "Version");
            Add("field_announcement", "公告", "Announcement");

            // ========== 验证码 ==========
            Add("captcha_title", "验证（创建房间前需要）", "Verification (required before creating room)");
            Add("captcha_input", "输入验证码", "Enter captcha");
            Add("captcha_verified", "✅ 已验证", "✅ Verified");
            Add("captcha_get", "获取验证码", "Get Captcha");
            Add("captcha", "验证码", "Captcha");

            // ========== HUD ==========
Add("hud_host", "【主机】端口 {0} | 在线 {1} 人 | 按 F10 打开联机菜单", "[Host] Port {0} | Online {1} | Press F10");
Add("hud_client", "【已连接】{0} | 在线 {1} 人 | 按 F10 打开联机菜单", "[Connected] {0} | Online {1} | Press F10");
Add("hud_offline", "【未联机】按 F10 打开联机菜单", "[Offline] Press F10 for menu");
            Add("hud_player", "{0}{1} ({2}) {3}ms  {4}{5}{6}", "{0}{1} ({2}) {3}ms  {4}{5}{6}");
            Add("hud_ghost", " 分身({0}/{1})", " Ghost({0}/{1})");
            Add("hud_marker", " 位置标记({0}/{1})", " Marker({0}/{1})");
            Add("hud_no_ghost", " 无分身", " No ghost");
            Add("hud_diff_scene", "（不同场景）", "(Different scene)");

            // ========== 设置 ==========
            Add("setting_sync_actions", "同步队友动作（自己也做同样动作）", "Sync teammate actions");
            Add("setting_auto_follow", "自动跟随房主切换场景", "Auto-follow host");
            Add("setting_show_hud", "显示屏幕 HUD", "Show HUD");
            Add("setting_sync_rate", "同步频率", "Sync Rate");
            Add("setting_language", "语言 / Language", "Language");

            // ========== 服务器管理 ==========
            Add("server_admin", "服务器管理", "Server Admin");
            Add("server_admin_login", "管理员登录", "Admin Login");
            Add("server_username", "用户名", "Username");
            Add("server_password", "密码", "Password");
            Add("server_room_list", "服务器房间列表", "Server Room List");
            Add("server_announcement", "公告", "Announcement");
            Add("announce_expand", "展开公告", "Expand");
            Add("announce_collapse", "收起", "Collapse");
            Add("search", "搜索", "Search");
            Add("direct_join", "按ID加入", "Join by ID");
            Add("server_pwd", "服务器密码", "Server Password");
            Add("relay_mode", "PHP中转模式（不直连，经服务器转发，延迟较高）", "PHP Relay Mode (no direct IP, higher latency)");
            Add("relay_on", "PHP中转已开启，房间", "PHP relay on, room");
            Add("relay_fail", "PHP中转启动失败，请检查服务器 relay.php 是否存在", "PHP relay start failed, check relay.php");
            Add("menu_master", "Mod 总服", "Mod Master");
            Add("master_connect_btn", "连接总服", "Connect Master");
            Add("master_connecting_hint", "总服连接中...", "Connecting to master...");
            Add("master_connected", "已连接 Mod 总服", "Connected to Mod Master");
            Add("master_fail", "连接总服失败", "Master connect failed");
            Add("master_online", "总服在线", "Master Online");
            Add("master_server_list", "游戏服务器列表", "Game Servers");
            Add("master_no_server", "暂无可用服务器", "No servers available");
            Add("btn_prev_page", "◀ 上一页", "◀ Prev");
            Add("btn_next_page", "下一页 ▶", "Next ▶");
            Add("master_connecting", "正在连接服务器 {0}（失败自动重试，最多3次）", "Connecting {0} (3 tries)");
            Add("master_retry", "第 {0} 次连接失败，3 秒后重试...", "Try {0} failed, retry in 3s...");
            Add("master_connect_fail", "3 次连接均失败，无法连接该服务器", "3 attempts failed");
            Add("master_version", "Mod 版本", "Mod Version");
            Add("update_available", "有新版本", "Update available");
            Add("btn_update", "下载更新", "Update");
            Add("update_downloaded", "已下载到 BepInEx\\SFMOnline_versions。请完全退出游戏后，双击游戏目录里的 SFMOnline_replace.bat 完成替换（或手动覆盖到 BepInEx\\plugins）。", "Downloaded to SFMOnline_versions. Exit the game, then run SFMOnline_replace.bat.");
            Add("update_downloading", "正在下载新版本…", "Downloading update…");
            Add("update_downloaded_hint", "重启游戏后自动替换为新版本", "Will apply after restart");
            Add("update_fail", "更新下载失败", "Update download failed");
            Add("update_restart", "新版本已安装，请重启游戏以加载", "New version installed, restart the game");
            Add("update_force", "必须更新到最新版本才能使用", "Update required to continue");
            Add("auth_title", "账号登录 / 注册", "Account Login / Register");
            Add("auth_login_tab", "登录", "Login");
            Add("auth_register_tab", "注册", "Register");
            Add("auth_forgot_tab", "忘记密码", "Forgot");
            Add("email", "邮箱", "Email");
            Add("auth_code_login", "用验证码登录", "Login with code");
            Add("auth_code", "验证码", "Code");
            Add("auth_send_code", "发送验证码", "Send Code");
            Add("auth_pass2", "确认密码", "Confirm password");
            Add("auth_pass_mismatch", "两次输入的密码不相同，请重试", "Passwords do not match, please retry");
            Add("auth_register_hint", "注册须知：用户名4-20位英文/数字/下划线；密码至少6位；需输入邮箱验证码与图形验证码。", "Notes: username 4-20 letters/digits/underscore; password min 6; email code and captcha required.");
            Add("auth_get_slider", "获取图形验证码", "Get Captcha");
            Add("auth_slider_tip", "请输入图片中的验证码，看不清可点刷新", "Enter the captcha text; refresh if unclear");
            Add("auth_captcha", "图形验证码", "Captcha");
            Add("auth_refresh", "刷新", "Refresh");
            Add("auth_captcha_zoom", "放大验证码", "Zoom");
            Add("auth_captcha_close", "关闭", "Close");
            Add("auth_captcha_confirm", "确认", "OK");
            Add("auth_agree", "我已阅读并同意用户协议及法律责任条款", "I agree to the User Agreement");
            Add("remember_login", "记住我（下次自动登录）", "Remember me (auto login)");
            Add("admin_change_uid", "修改用户ID", "Change UID");
            Add("admin_new_uid", "新UID", "New UID");
            Add("tamper_warn", "客户端文件已被修改，功能已停用，请重新安装", "Client file modified, disabled. Reinstall.");
            Add("auth_agree_user", "我已阅读并同意《用户协议》", "I agree to the User Agreement");
            Add("auth_agree_privacy", "我已阅读并同意《隐私与法律责任条款》", "I agree to the Privacy & Legal Terms");
            Add("auth_view", "查看", "View");
            Add("auth_agreement_loading", "协议加载中，请稍候...", "Loading agreement, please wait...");
            Add("auth_agreement_outdated", "用户协议已更新，请重新勾选同意", "Agreement updated, please agree again");
            Add("auth_agree_ok", "已同意协议", "Agreement accepted");
            Add("auth_submit", "确认", "Submit");
            Add("auth_new_pass", "新密码", "New Password");
            Add("auth_email_required", "请输入邮箱", "Enter email");
            Add("auth_need_slider", "请先获取并输入图形验证码", "Get and enter the captcha first");
            Add("auth_code_sent", "验证码已发送", "Code sent");
            Add("resend_tip", "未收到？请查看垃圾箱。仍找不到可点右侧按钮，用备用接口重发同一验证码（每日3次）。", "Not received? Check spam. Or use backup sender to resend the same code (3x/day).");
            Add("resend_btn", "备用接口补发", "Backup resend");
            Add("auth_code_required", "请输入验证码", "Enter code");
            Add("auth_pass_required", "请输入密码", "Enter password");
            Add("auth_agree_required", "请先勾选同意用户协议", "Agree to the User Agreement first");
            Add("auth_fill_all", "请填写完整信息", "Fill all fields");
            Add("auth_reset_ok", "密码已重置，请登录", "Password reset, login now");
            Add("auth_logged_in", "欢迎", "Welcome");
            Add("account_info", "账号", "Account");
            Add("account_uid", "UID", "UID");
            Add("username", "用户名", "Username");
            Add("auth_account", "用户名 / 邮箱", "Username / Email");
            Add("auth_account_hint", "支持用户名或邮箱登录", "Login with username or email");
            Add("btn_rename", "修改用户名", "Rename");
            Add("rename_rule", "用户名仅限英文/数字/下划线，4-20位", "Username: 4-20 English letters, digits or _");
            Add("rename_ok", "用户名已修改", "Username changed");
            Add("rename_same", "新用户名与当前相同", "Username is unchanged");
            Add("btn_logout", "退出登录", "Logout");
            Add("menu_room", "房间", "Room");
Add("menu_online", "联机", "Online");
            Add("menu_friend", "好友", "Friends");
            Add("menu_pubchat", "公屏", "Lobby");
            Add("menu_credits", "鸣谢", "Credits");
            Add("menu_profile", "个人", "Profile");
            Add("menu_admin", "管理", "Admin");
            Add("menu_about", "关于", "About");
            Add("about_creator", "制作人员", "Creators");
            Add("about_server", "想要创建属于您的服务器？我们的联机服务器已开源，帮助您实现自己的服务器。", "Want your own server? Our relay server is open source.");
            Add("about_share", "注意：创建服务器时，您必须与我们的服务器进行数据共享。", "Note: creating a server requires data sharing with our server.");
            Add("about_sponsor", "欢迎您向我们赞助", "Welcome to sponsor us");
            Add("profile_bio", "个人简介", "Bio");
            Add("profile_save", "保存简介", "Save bio");
            Add("profile_report", "举报", "Report");
            Add("profile_report_reason", "举报理由", "Reason");
            Add("profile_view", "查看主页", "View profile");
            Add("dm_title", "私聊", "Private chat");
            Add("dm_send", "发送", "Send");
            Add("dm_open", "私聊", "DM");
            Add("admin_quick", "快捷管理", "Quick admin");
            Add("admin_mute", "禁言", "Mute");
            Add("admin_unmute", "解禁", "Unmute");
            Add("admin_no_perm", "你的管理员等级暂无可用权限，请在服务器后台为你的等级勾选权限", "No permissions granted for your admin level");
            Add("admin_ban", "封禁账号", "Ban account");
            Add("admin_unban", "解封账号", "Unban account");
            Add("admin_rename", "管理员改名", "Rename user");
            Add("admin_rename_btn", "改名", "Rename");
            Add("admin_set_title", "设置称号", "Set title");
            Add("admin_title_btn", "设置", "Set");
            Add("admin_level_set", "设置等级", "Set level");
            Add("admin_block_name", "屏蔽名称", "Block name");
            Add("admin_dm_view", "查看私聊", "View DMs");
            Add("admin_search_user", "按用户名查UID", "Search by name");
            Add("admin_search_btn", "搜索", "Search");
            Add("admin_target", "目标", "Target");
            Add("admin_need_uid", "请先输入或搜索目标UID", "Enter or search a target UID first");
            Add("admin_no_user", "未找到该用户", "User not found");
            Add("admin_reports", "举报审核", "Report review");
            Add("admin_report_refresh", "刷新举报", "Refresh reports");
            Add("admin_report_empty", "暂无举报", "No reports");
            Add("admin_report_pass", "通过", "Accept");
            Add("admin_report_reject", "驳回", "Reject");
            Add("admin_report_handled", "已处理", "Handled");
            Add("admin_pubchat_view", "公屏消息", "Public chat");
            Add("admin_pubchat_refresh", "刷新公屏", "Refresh chat");
            Add("admin_pubchat_del", "删除", "Delete");
            Add("admin_pubchat_empty", "暂无公屏消息", "No public messages");
            Add("friend_add", "添加", "Add");
            Add("friend_added", "好友申请已发送", "Friend request sent");
            Add("friend_requests", "好友申请", "Friend requests");
            Add("friend_accept", "同意", "Accept");
            Add("friend_reject", "拒绝", "Reject");
            Add("friend_request_sent", "申请已发送，等待对方同意", "Request sent, waiting");
            Add("friend_accept_ok", "已同意", "Accepted");
            Add("friend_reject_ok", "已拒绝", "Rejected");
            Add("friend_list", "刷新好友列表", "Refresh friends");
            Add("friend_delete", "删除", "Delete");
            Add("btn_back", "返回", "Back");
            Add("friend_settings", "好友设置", "Friend Settings");
            Add("friend_hide_server", "隐藏我所在服务器", "Hide my server");
            Add("friend_allow_search", "允许被搜索", "Allow search");
            Add("mods_required", "该服务器需要以下 Mod", "This server requires mods");
            Add("mods_list", "所需Mod", "Required Mods");
            Add("btn_install", "安装", "Install");
            Add("mods_restart", "请重启游戏后再尝试连接该服务器", "Restart the game before connecting");
            Add("domain_blocked", "该服务器域名已被封禁，已断开", "Server domain blocked, disconnected");
            Add("room_need_password", "该房间需要密码", "This room requires a password");
            Add("kick", "踢出", "Kick");
            Add("online_players", "在线玩家", "Online Players");
            Add("mod_sync_warn", "动作/服装同步需双方安装相同Mod（Mod在启动时加载）", "Animation/clothing sync requires same mods (mods load at start)");
            Add("server_admin_ops", "服务器管理操作", "Server Admin Actions");
            Add("admin_delete_room", "删除选中房间", "Delete Selected Room");
            Add("admin_select_room", "请先在列表中选中要删除的房间", "Select a room in the list first");
            Add("admin_ip_required", "请填写要操作的IP", "Enter an IP address");
            Add("admin_banned", "已拉黑", "Banned");
            Add("admin_unbanned", "已解除拉黑", "Unbanned");
            Add("admin_set_announcement", "更新公告", "Update Announcement");
            Add("admin_announcement_required", "请输入公告内容", "Enter announcement text");
            Add("admin_announcement_updated", "公告已更新", "Announcement updated");
            Add("admin_clear_announcement", "清除公告", "Clear Announcement");
            Add("admin_announcement_cleared", "公告已清除", "Announcement cleared");
            Add("admin_export_logs", "导出日志", "Export Logs");
            Add("admin_logs_exported", "日志已导出（到网页保险箱下载）", "Logs exported (download from web vault)");
            Add("room_id", "房间ID", "Room ID");
            Add("admin_query_chat", "查聊天", "View Chat");
            Add("admin_room_id_required", "请填写房间ID", "Enter a room ID");
            Add("admin_chat_empty", "该房间暂无聊天记录", "No chat records in this room");
            Add("message", "消息", "Message");
            Add("admin_msg_required", "请填写房间ID和消息内容", "Enter room ID and message");
            Add("admin_msg_sent", "消息已发送", "Message sent");
            Add("admin_server_settings", "服务器配置", "Server Settings");
            Add("admin_settings_loaded", "配置已读取", "Settings loaded");
            Add("admin_settings_invalid", "配置数值无效", "Invalid setting value");
            Add("admin_settings_saved", "配置已保存", "Settings saved");
            Add("setting_max_rooms_total", "全服房间数", "Max Rooms");
            Add("setting_max_rooms_per_ip", "每IP建房", "Rooms/IP");
            Add("setting_max_rooms_per_hour", "每IP/时", "Rooms/IP/h");
            Add("setting_room_lifetime", "房间存活秒", "Lifetime(s)");
            Add("setting_room_timeout", "心跳超时秒", "Timeout(s)");
            Add("setting_max_players", "默认人数", "Max Players");
            Add("setting_chat_log_days", "聊天天数", "Chat Days");
            Add("setting_action_log_days", "日志天数", "Log Days");
            Add("setting_captcha_expire", "验证码秒", "Captcha(s)");
            Add("btn_save", "保存", "Save");

            // ========== 语言选择 ==========
            Add("lang_chinese", "中文", "Chinese");
            Add("lang_english", "English", "English");
            Add("lang_switch", "切换语言", "Switch Language");

            // ========== 其他 ==========
            Add("lan_ip", "局域网IP", "LAN IP");
            Add("tunnel_hint", "开房后把本地端口 {0} 用 TCP 隧道映射出去", "Forward local port {0} via TCP tunnel");
Add("version_info", "v{0} · F10联机/房间 · F12普通菜单/关闭 · F11聊天 · F9分身调试 · Shift+F10诊断", "v{0} · F10 Online/Rooms · F12 Social/Close · F11 Chat · F9 Ghost Debug · Shift+F10 Dump");
            Add("no_rooms", "暂无公开房间", "No public rooms");
            Add("players_count", "在线 {0} 人", "Online {0}");
            Add("room_list_status", "共 {0} 个房间", "Total {0} rooms");
            Add("refreshing", "刷新中...", "Refreshing...");
            Add("refresh_failed", "获取失败，请检查网络", "Failed, check network");
            Add("room", "房间", "Room");
            Add("host", "主机", "Host");
            Add("client", "客户端", "Client");
            Add("id", "ID", "ID");
            Add("name", "名称", "Name");
            Add("type", "类型", "Type");
            Add("detail", "详情", "Detail");
            Add("action", "操作", "Action");
            Add("time", "时间", "Time");
            Add("ip", "IP地址", "IP Address");
            Add("reason", "原因", "Reason");
            Add("days", "天数", "Days");
            Add("permanent", "永久", "Permanent");
            Add("ban", "拉黑", "Ban");
            Add("unban", "解除拉黑", "Unban");
            Add("delete", "删除", "Delete");
            Add("confirm", "确认", "Confirm");
            Add("cancel", "取消", "Cancel");
            Add("error", "错误", "Error");
            Add("success", "成功", "Success");
            Add("warning", "警告", "Warning");
            Add("info", "信息", "Info");
            Add("room_name", "房间名", "Room Name");
            Add("players", "人数", "Players");
            Add("password_status", "密码状态", "Password Status");
            Add("no_password", "无密码", "No Password");
        }

        private static void Add(string key, string chinese, string english)
        {
            _strings[key] = new Dictionary<Language, string>
            {
                { Language.Chinese, chinese },
                { Language.English, english }
            };
        }

        public static string Get(string key, params object[] args)
        {
            if (_strings.TryGetValue(key, out var dict))
            {
                string text = dict.TryGetValue(Current, out var val) ? val : key;
                return args.Length > 0 ? string.Format(text, args) : text;
            }
            return key;
        }

        // 服务器只下发字段名（key），本地语言库翻译；缺翻译时用服务器中文兜底
        public static string GetFallback(string key, string fallback, params object[] args)
        {
            if (!string.IsNullOrEmpty(key) && _strings.TryGetValue(key, out var dict))
                return Get(key, args);
            return fallback;
        }

        public static void SetLanguage(Language lang)
        {
            Current = lang;
        }

        public static void ToggleLanguage()
        {
            Current = Current == Language.Chinese ? Language.English : Language.Chinese;
        }

        public static string GetFieldDisplay(string fieldName)
        {
            string key = "field_" + fieldName;
            if (_strings.ContainsKey(key))
            {
                return Get(key);
            }
            return fieldName;
        }
    }
}