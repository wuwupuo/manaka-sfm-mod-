using System;
using System.Collections.Generic;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  V2 兼容层（手机短信系统）：
    //  - 检测游戏是否安装 V2 前置包（SFM_custom_mission_v2.dll）
    //  - 已安装：调用 V2 的手机短信接口（反射）
    //  - 未安装：提示需要 V2 前置包
    // ====================================================================
    public static class SfmExtPhone
    {
        private static bool? _v2Present;

        /// <summary>是否已安装 V2 前置包（SFM_custom_mission_v2）。</summary>
        public static bool V2Present
        {
            get
            {
                if (_v2Present.HasValue) return _v2Present.Value;
                bool ok = false;
                try
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var name = asm.GetName().Name;
                        if (name != null && name.IndexOf("SFM_custom_mission_v2", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            ok = true;
                            break;
                        }
                    }
                }
                catch { }
                _v2Present = ok;
                return ok;
            }
        }

        /// <summary>检测 V2 前置包（模组加载时调用，缺失时输出警告）。</summary>
        public static bool CheckV2()
        {
            if (V2Present) return true;
            SfmExt.Warn("需要 V2 前置包（SFM_custom_mission_v2）才能使用手机短信功能！请安装 V2 模组。");
            SfmExtHud.Toast("需要 V2 前置包才能使用手机功能", 6f, UnityEngine.Color.yellow);
            return false;
        }

        /// <summary>发送手机短信（调用 V2 短信系统；无 V2 时仅提示）。</summary>
        public static void Send(string user, string text)
        {
            if (!CheckV2()) return;
            try
            {
                // 通过 V2 的事件/短信通道：转发给 V2 的 MessengerChat
                // V2 提供 EngineFunction "CreateMessengerChat" 和 ChatManager，
                // 这里使用事件桥接（V2 若集成 SFMExt 则监听此事件）
                SfmExtEvent.Emit("v2_phone_send", new SfmExtValue(SfmExtValue.Type.List)
                {
                    ["user"] = new SfmExtValue(user),
                    ["text"] = new SfmExtValue(text)
                });
            }
            catch { }
        }

        /// <summary>创建手机对话（调用 V2 手机系统；无 V2 时仅提示）。</summary>
        public static void CreateChat(string name)
        {
            if (!CheckV2()) return;
            try
            {
                SfmExtEvent.Emit("v2_phone_chat", new SfmExtValue(SfmExtValue.Type.List)
                {
                    ["name"] = new SfmExtValue(name)
                });
            }
            catch { }
        }

        [SfmExtFunction("phone.send")]
        public static SfmExtValue FnSend(SfmExtParams p, SfmExtValue u)
        {
            Send(p.Get("user").ToString(), p.Get("text").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("phone.chat")]
        public static SfmExtValue FnChat(SfmExtParams p, SfmExtValue u)
        {
            CreateChat(p.Get("name").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("phone.v2_present")]
        public static SfmExtValue FnV2Present(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(V2Present);
    }
}
