using BepInEx.Configuration;

namespace SFMOnline
{
    internal static class Settings
    {
        internal static ConfigEntry<string> Nickname;
        internal static ConfigEntry<string> HostAddress;
        internal static ConfigEntry<int> Port;
        internal static ConfigEntry<string> Password;
        internal static ConfigEntry<int> MaxPlayers;
        internal static ConfigEntry<bool> AutoFollowHost;
        internal static ConfigEntry<bool> SyncActions;
        internal static ConfigEntry<bool> ShowHud;
        internal static ConfigEntry<bool> AutoReconnect;
        internal static ConfigEntry<bool> RelayMode;
        internal static ConfigEntry<int> SyncRateHz;
        internal static ConfigEntry<string> Language;
        internal static ConfigEntry<string> ServerAddress;
        internal static ConfigEntry<int> ServerPort;
        internal static ConfigEntry<string> MasterUrl;
        internal static ConfigEntry<bool> AutoLogin;
        internal static ConfigEntry<int> UiFontSize;

        internal static void Bind(ConfigFile config)
        {            Nickname = config.Bind("联机", "昵称", "玩家", "房间内显示的名字");
            HostAddress = config.Bind("联机", "上次连接地址", "", "加入局域网/内网房间时填写的真实 主机地址:端口；留空可使用房间发现");
            Port = config.Bind("联机", "主机端口", 27570, "开房间监听的 TCP 端口（樱花映射需转发此端口）");
            Password = config.Bind("联机", "房间密码", "", "留空表示无密码");
            MaxPlayers = config.Bind("联机", "最大人数", 8, "最多允许加入的玩家人数（不含房主）");
            AutoFollowHost = config.Bind("联机", "自动跟随房主", false, "房主切换场景时自动跟随传送");
            SyncActions = config.Bind("联机", "同步队友动作", false, "队友开始动作时，自己的人物也执行相同动作");
            ShowHud = config.Bind("界面", "显示HUD", true, "是否在左上角显示联机状态");
            AutoReconnect = config.Bind("联机", "断线自动重连", true, "客户端断线后自动尝试重连");
            RelayMode = config.Bind("联机", "PHP中转模式", false, "不用直连IP，所有数据经PHP服务器转发（延迟较高，直连失败时用）");
            SyncRateHz = config.Bind("联机", "同步频率", 20, "每秒同步 10-30 次；推荐 20。动作发送状态字段，骨骼由接收端本地计算");
            if (SyncRateHz.Value < 10 || SyncRateHz.Value > 30) SyncRateHz.Value = 20;
            Language = config.Bind("界面", "语言", "Chinese", "界面语言: Chinese / English");
            ServerAddress = config.Bind("服务器", "地址", "", "公共服务器地址");
            ServerPort = config.Bind("服务器", "端口", 80, "公共服务器端口");
            MasterUrl = config.Bind("服务器", "总服地址", "", "Mod 总服域名（留空=wuwupuo1.xtxt.xyz）");
            AutoLogin = config.Bind("账号", "自动登录", true, "登录后保存Token，下次启动自动登录");
            UiFontSize = config.Bind("界面", "文字大小", 15, "联机菜单文字大小（13-20）");
            if (UiFontSize.Value < 13 || UiFontSize.Value > 20) UiFontSize.Value = 15;
        }
    }
}
