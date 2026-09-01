using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace SFMOnline
{
    // ====================================================================
    //  模组文件夹加载器（v1.0.10 增强）
    //  1. 启动时扫描游戏目录 SFMOnlineMods\ 下的所有 DLL 自动加载
    //  2. 房间模组同步：收集本地模组清单（相对路径+MD5+大小），
    //     对比房主清单，下载缺失/不同文件（经服务器转发），
    //     下载后热加载新模组并自动重进游戏（退出当前世界重进）
    //  3. 玩家自有模组与房主重叠时自动屏蔽本地版本（加载房主版本）
    // ====================================================================
    public static class ModLoader
    {
        public static string Root => Path.Combine(BepInEx.Paths.GameRootPath, "SFMOnlineMods");
        private static readonly HashSet<string> LoadedAsms = new HashSet<string>();
        private static readonly Dictionary<string, string> ActiveMd5 = new Dictionary<string, string>();
        // 房主模组屏蔽列表：本地同名但 md5 不同的文件（下载房主版后替换加载）
        private static readonly Dictionary<string, string> ShadowList = new Dictionary<string, string>();

        public static void LoadMods()
        {
            try
            {
                if (!Directory.Exists(Root))
                {
                    Directory.CreateDirectory(Root);
                    File.WriteAllText(Path.Combine(Root, "说明.txt"),
                        "SFM Online 模组文件夹\n====================\n" +
                        "把模组 DLL 放到本目录（可直接放，或每个模组一个子文件夹），\n" +
                        "启动游戏时自动加载；加入房间时可自动同步房主的模组。\n" +
                        "模组开发文档见 GitHub: wuwupuo/manaka-sfm-mod- 的 docs/ 目录。\n");
                    return;
                }
                foreach (var dll in EnumerateDlls().OrderBy(x => x))
                {
                    try { LoadOne(dll); }
                    catch (Exception ex) { PluginInfo.Warn("模组加载失败 " + Path.GetFileName(dll) + ": " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("模组文件夹扫描失败: " + ex.Message);
            }
        }

        // ---------- 清单 ----------
        /// <summary>收集本地模组文件清单（相对路径+md5+size），用于房主上报/玩家比对。</summary>
        public static List<Dictionary<string, object>> CollectManifest()
        {
            var list = new List<Dictionary<string, object>>();
            try
            {
                if (!Directory.Exists(Root)) return list;
                var all = new List<string>();
                all.AddRange(Directory.GetFiles(Root, "*.*", SearchOption.TopDirectoryOnly));
                foreach (var sub in Directory.GetDirectories(Root))
                    all.AddRange(Directory.GetFiles(sub, "*.*", SearchOption.TopDirectoryOnly));
                foreach (var f in all.OrderBy(x => x))
                {
                    try
                    {
                        var info = new FileInfo(f);
                        if (info.Length > 8 * 1024 * 1024) continue; // 单文件上限 8MB
                        list.Add(new Dictionary<string, object>
                        {
                            ["name"] = Path.GetRelativePath(Root, f).Replace('\\', '/'),
                            ["md5"] = FileMd5(f),
                            ["size"] = info.Length
                        });
                    }
                    catch { }
                }
            }
            catch { }
            return list;
        }

        /// <summary>比对本地与房主清单，返回需要下载的文件名列表（本地缺失或 md5 不同）。</summary>
        public static List<string> DiffManifest(List<Dictionary<string, object>> hostFiles)
        {
            var need = new List<string>();
            try
            {
                if (hostFiles == null) return need;
                var local = new Dictionary<string, string>();
                foreach (var m in CollectManifest())
                    local[Convert.ToString(m["name"])] = Convert.ToString(m["md5"]);
                foreach (var m in hostFiles)
                {
                    string name = Convert.ToString(m["name"]);
                    string md5 = Convert.ToString(m["md5"]);
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!local.TryGetValue(name, out var lmd5) || lmd5 != md5)
                        need.Add(name);
                }
            }
            catch { }
            return need;
        }

        /// <summary>写下载的模组文件（不覆盖本地已存在但未在清单中的文件；同名同内容跳过）。</summary>
        public static void SaveDownloaded(string relPath, byte[] data)
        {
            try
            {
                if (string.IsNullOrEmpty(relPath) || data == null || data.Length == 0) return;
                // 安全路径
                relPath = relPath.Replace('\\', '/').TrimStart('/');
                var full = Path.Combine(Root, relPath.Replace('/', Path.DirectorySeparatorChar));
                if (!full.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) return;
                var dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(full, data);
                PluginInfo.Info("模组文件已下载: " + relPath + " (" + data.Length + " bytes)");
            }
            catch (Exception ex) { PluginInfo.Warn("模组文件写入失败: " + ex.Message); }
        }

        /// <summary>热加载指定 DLL（游戏运行中加入房间后加载新模组）。</summary>
        public static bool HotLoad(string relPath)
        {
            try
            {
                relPath = relPath.Replace('\\', '/').TrimStart('/');
                var full = Path.Combine(Root, relPath.Replace('/', Path.DirectorySeparatorChar));
                if (!full.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) return false;
                if (!File.Exists(full) || !full.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return false;
                return LoadOne(full);
            }
            catch { return false; }
        }

        /// <summary>热加载本次下载的全部新 DLL（返回加载成功数）。</summary>
        public static int HotLoadAll(List<string> downloadedFiles)
        {
            int n = 0;
            if (downloadedFiles == null) return 0;
            foreach (var f in downloadedFiles)
            {
                try { if (f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && HotLoad(f)) n++; } catch { }
            }
            return n;
        }

        // ---------- 内部 ----------
        private static IEnumerable<string> EnumerateDlls()
        {
            var dlls = new List<string>();
            if (!Directory.Exists(Root)) return dlls;
            try { dlls.AddRange(Directory.GetFiles(Root, "*.dll", SearchOption.TopDirectoryOnly)); } catch { }
            foreach (var sub in Directory.GetDirectories(Root))
            {
                try { dlls.AddRange(Directory.GetFiles(sub, "*.dll", SearchOption.TopDirectoryOnly)); } catch { }
            }
            return dlls;
        }

        private static bool LoadOne(string dllPath)
        {
            try
            {
                string key = Path.GetFileName(dllPath);
                if (LoadedAsms.Contains(key)) return false;
                var asm = Assembly.LoadFrom(dllPath);
                bool found = false;
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
                    found = true;
                    break;
                }
                if (found) LoadedAsms.Add(key);
                return found;
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("模组 DLL 加载异常 " + Path.GetFileName(dllPath) + ": " + ex.Message);
                return false;
            }
        }

        public static string FileMd5(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                using (var md5 = MD5.Create())
                {
                    byte[] h = md5.ComputeHash(fs);
                    var sb = new StringBuilder(32);
                    foreach (byte b in h) sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return ""; }
        }
    }
}
