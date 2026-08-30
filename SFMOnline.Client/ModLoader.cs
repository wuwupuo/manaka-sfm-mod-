using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace SFMOnline
{
    // ====================================================================
    //  模组文件夹加载器：
    //  扫描游戏目录 SFMOnlineMods\ 下的所有 DLL，自动加载其中的
    //  BepInPlugin（BasePlugin 子类）模组。
    //  目录约定：
    //     <游戏目录>\SFMOnlineMods\        → 直接放 .dll 或子文件夹
    //     <游戏目录>\SFMOnlineMods\xxx\    → 每个模组一个子文件夹
    //  模组 DLL 依赖 SFMOnline.dll（前置框架）时，请复制到同目录或引用已安装的。
    // ====================================================================
    public static class ModLoader
    {
        public static void LoadMods()
        {
            try
            {
                string root = Path.Combine(BepInEx.Paths.GameRootPath, "SFMOnlineMods");
                if (!Directory.Exists(root))
                {
                    Directory.CreateDirectory(root);
                    File.WriteAllText(Path.Combine(root, "说明.txt"),
                        "SFM Online 模组文件夹\n====================\n" +
                        "把模组 DLL 放到本目录（可直接放，或每个模组一个子文件夹），\n" +
                        "启动游戏时自动加载。\n" +
                        "模组开发文档见 GitHub: wuwupuo/manaka-sfm-mod- 的 docs/ 目录。\n");
                    return;
                }
                var dlls = new List<string>();
                try { dlls.AddRange(Directory.GetFiles(root, "*.dll", SearchOption.TopDirectoryOnly)); } catch { }
                foreach (var sub in Directory.GetDirectories(root))
                {
                    try { dlls.AddRange(Directory.GetFiles(sub, "*.dll", SearchOption.TopDirectoryOnly)); } catch { }
                }
                int loaded = 0;
                foreach (var dll in dlls.OrderBy(x => x))
                {
                    try { if (LoadOne(dll)) loaded++; }
                    catch (Exception ex) { PluginInfo.Warn("模组加载失败 " + Path.GetFileName(dll) + ": " + ex.Message); }
                }
                if (loaded > 0) PluginInfo.Info("SFMOnlineMods: 已加载 " + loaded + " 个模组");
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("模组文件夹扫描失败: " + ex.Message);
            }
        }

        private static bool LoadOne(string dllPath)
        {
            try
            {
                var asm = Assembly.LoadFrom(dllPath);
                foreach (var type in asm.GetTypes())
                {
                    if (!type.IsSubclassOf(typeof(BasePlugin))) continue;
                    var attr = (BepInPlugin)type.GetCustomAttributes(typeof(BepInPlugin), false).FirstOrDefault();
                    if (attr == null) continue;
                    var plugin = (BasePlugin)Activator.CreateInstance(type);
                    if (plugin == null) continue;
                    plugin.Load();
                    string ver = "";
                    try { ver = " v" + attr.GetType().GetProperty("Version").GetValue(attr).ToString(); } catch { }
                    PluginInfo.Info("模组已加载: " + attr.Name + ver + " (" + Path.GetFileName(dllPath) + ")");
                    return true;
                }
                PluginInfo.Warn("未找到 BepInPlugin: " + Path.GetFileName(dllPath));
                return false;
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("模组 DLL 加载异常 " + Path.GetFileName(dllPath) + ": " + ex.Message);
                return false;
            }
        }
    }
}
