using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace SFMOnline
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BasePlugin
    {
        public override void Load()
        {
            try
            {
                ClientLog.Init();
                System.Threading.ThreadPool.QueueUserWorkItem(_ => { try { TryApplyUpdate(); } catch { } });
                PluginInfo.Logger = Log;
                Settings.Bind(Config);
                AddComponent<OnlineBehaviour>();
                // Ext 前置框架：后台线程注册（不阻塞启动、不依赖游戏主循环）
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { SFMOnline.Ext.SfmExt.InitNow(); }
                    catch (Exception ex) { Log.LogWarning("Ext 初始化失败: " + ex.Message); }
                });
                // 模组文件夹：游戏目录\SFMOnlineMods\ 下的 DLL 自动加载（mod 即前置包）
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { ModLoader.LoadMods(); }
                    catch (Exception ex) { Log.LogWarning("模组文件夹加载失败: " + ex.Message); }
                });
                Log.LogInfo($"{PluginInfo.Name} v{PluginInfo.Version} 已加载。游戏内按 F10 打开联机菜单，F12 打开普通菜单，F11 打开聊天。");
                ClientLog.Write("模组加载 v" + PluginInfo.Version);
            }
            catch (Exception ex)
            {
                Log.LogError($"{PluginInfo.Name} v{PluginInfo.Version} 初始化失败（不会影响游戏运行）：" + ex);
                ClientLog.Write("初始化失败: " + ex);
            }
        }

        // 启动时检测更新文件夹：有新版 DLL 就替换插件本体（需重启后生效）
                        public static bool ReplaceFromStaging()
        {
            try
            {
                var verDir = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "SFMOnline_versions");
                if (!System.IO.Directory.Exists(verDir)) return false;
                var pluginPath = System.IO.Path.Combine(BepInEx.Paths.PluginPath, "SFMOnline.dll");
                string best = null; int[] bestV = new int[4];
                foreach (var f in System.IO.Directory.GetFiles(verDir, "SFMOnline_*.dll"))
                {
                    var parts = System.IO.Path.GetFileNameWithoutExtension(f).Replace("SFMOnline_", "").Split('.');
                    var v = new int[4];
                    for (int i = 0; i < 4 && i < parts.Length; i++) int.TryParse(parts[i], out v[i]);
                    bool newer = best == null;
                    if (!newer) for (int i = 0; i < 4; i++) { if (v[i] > bestV[i]) { newer = true; break; } if (v[i] < bestV[i]) break; }
                    if (newer) { best = f; bestV = v; }
                }
                if (best == null) return false;
                System.IO.File.Copy(best, pluginPath, true);
                foreach (var f in System.IO.Directory.GetFiles(verDir, "SFMOnline_*.dll")) System.IO.File.Delete(f);
                return true;
            }
            catch { return false; }
        }
                private void TryApplyUpdate()
        {
            try
            {
                var dir = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "SFMOnline_versions");
                var newDll = System.IO.Path.Combine(dir, "SFMOnline.dll");
                if (!System.IO.File.Exists(newDll)) return;
                var pluginPath = System.IO.Path.Combine(BepInEx.Paths.PluginPath, "SFMOnline.dll");
                try
                {
                    System.IO.File.Copy(newDll, pluginPath, true);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(dir, ".updated"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    System.IO.File.Delete(newDll);
                    Log.LogWarning("SFMOnline 已安装新版本文件，请重启游戏加载新版本。");
                }
                catch
                {
                    var bat = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "SFMOnline_replace.bat");
                    System.IO.File.WriteAllText(bat, "@echo off\r\ncd /d \"%~dp0\"\r\n:loop\r\ntimeout /t 3 /nobreak >nul\r\ncopy /Y \"BepInEx\\SFMOnline_versions\\SFMOnline.dll\" \"BepInEx\\plugins\\SFMOnline.dll\" >nul 2>&1\r\nif errorlevel 1 goto loop\r\ndel /Q \"BepInEx\\SFMOnline_versions\\SFMOnline.dll\"\r\necho SFMOnline 更新完成，请重新启动游戏。\r\npause\r\n", System.Text.Encoding.Default);
                    Log.LogWarning("SFMOnline 插件文件被占用，已生成 SFMOnline_replace.bat：退出游戏后双击完成替换。");
                }
            }
            catch (Exception ex) { Log.LogWarning("SFMOnline 自动更新失败: " + ex.Message); }
        }
    }
public static class PluginInfo
    {
        public const string GUID = "com.sfm.online";
        public const string Name = "SFM 在线联机";
        public const string Version = "1.0.11";
        internal static ManualLogSource Logger;

        internal static void Info(string msg) => Logger?.LogInfo(msg);
        internal static void Warn(string msg) => Logger?.LogWarning(msg);
        internal static void Error(string msg) => Logger?.LogError(msg);
    }

    // 客户端独立日志文件：BepInEx/SFMOnline_client.log
    internal static class ClientLog
    {
        private static readonly object LockObj = new object();
        private static string _file = "";

        internal static void Init()
        {
            try { _file = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "SFMOnline_client.log"); }
            catch { _file = "SFMOnline_client.log"; }
        }

        internal static void Write(string msg)
        {
            try
            {
                if (_file.Length == 0) Init();
                lock (LockObj)
                    System.IO.File.AppendAllText(_file,
                        "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + msg + Environment.NewLine);
            }
            catch { }
        }
    }
}

