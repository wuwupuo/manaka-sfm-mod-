using System;
using System.Collections.Generic;
using UnityEngine;
using ExposureUnnoticed2.Object3D.Player.Scripts;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  玩法控制（模仿 V2 Functions 的 SetAction/SetStage/SetPlayerPosition/
    //  TriggerSexOrgasm/SetCrouch/SetSexPosition/BlockInput 等）：
    //  通过反射调用 OnlineCore 的玩法方法，避免直接依赖内部实现。
    // ====================================================================
    public static class SfmExtPlay
    {
        [SfmExtFunction("play.set_action")]
        public static SfmExtValue FnSetAction(SfmExtParams p, SfmExtValue unused)
            => Call("SetActionByName", p.Get("action").ToString());

        [SfmExtFunction("play.set_stage")]
        public static SfmExtValue FnSetStage(SfmExtParams p, SfmExtValue unused)
            => Call("SetStage", (int)p.Get("stage").ToFloat());

        [SfmExtFunction("play.set_position")]
        public static SfmExtValue FnSetPosition(SfmExtParams p, SfmExtValue unused)
        {
            var pos = new Vector3((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat());
            return Call("SetPlayerPosition", pos);
        }

        [SfmExtFunction("play.teleport")]
        public static SfmExtValue FnTeleport(SfmExtParams p, SfmExtValue unused) => FnSetPosition(p, unused);

        [SfmExtFunction("play.set_crouch")]
        public static SfmExtValue FnSetCrouch(SfmExtParams p, SfmExtValue unused)
            => Call("SetCrouch", p.Get("value").ToBool());

        [SfmExtFunction("play.trigger_orgasm")]
        public static SfmExtValue FnTriggerOrgasm(SfmExtParams p, SfmExtValue unused)
            => Call("TriggerOrgasm");

        [SfmExtFunction("play.set_sex_position")]
        public static SfmExtValue FnSetSexPosition(SfmExtParams p, SfmExtValue unused)
            => Call("SetSexPosition", (int)p.Get("position").ToFloat());

        [SfmExtFunction("play.deactivate_sex")]
        public static SfmExtValue FnDeactivateSex(SfmExtParams p, SfmExtValue unused)
            => Call("DeactivateSex");

        [SfmExtFunction("play.block_input")]
        public static SfmExtValue FnBlockInput(SfmExtParams p, SfmExtValue unused)
            => Call("BlockInput", p.Get("block").ToBool());

        [SfmExtFunction("play.gameover")]
        public static SfmExtValue FnGameOver(SfmExtParams p, SfmExtValue unused)
            => Call("TriggerGameOver");

        [SfmExtFunction("play.set_ecstasy")]
        public static SfmExtValue FnSetEcstasy(SfmExtParams p, SfmExtValue unused)
            => Call("SetEcstasy", (float)p.Get("value").ToFloat());

        [SfmExtFunction("play.set_moisture")]
        public static SfmExtValue FnSetMoisture(SfmExtParams p, SfmExtValue unused)
            => Call("SetMoisture", (float)p.Get("value").ToFloat());

        [SfmExtFunction("play.set_stamina")]
        public static SfmExtValue FnSetStamina(SfmExtParams p, SfmExtValue unused)
            => Call("SetStamina", (float)p.Get("value").ToFloat());

        [SfmExtFunction("play.get_ecstasy")]
        public static SfmExtValue FnGetEcstasy(SfmExtParams p, SfmExtValue unused)
            => new SfmExtValue(CallFloat("GetEcstasy"));

        [SfmExtFunction("play.get_moisture")]
        public static SfmExtValue FnGetMoisture(SfmExtParams p, SfmExtValue unused)
            => new SfmExtValue(CallFloat("GetMoisture"));

        [SfmExtFunction("play.get_stamina")]
        public static SfmExtValue FnGetStamina(SfmExtParams p, SfmExtValue unused)
            => new SfmExtValue(CallFloat("GetStamina"));

        // ---------- 直接调用 ----------
        private static SfmExtValue Call(string method, params object[] args)
        {
            try
            {
                var core = OnlineCore.Instance;
                if (core == null) return new SfmExtValue(false);
                switch (method)
                {
                    case "SetActionByName": return new SfmExtValue(core.SetActionByName((string)args[0]));
                    case "SetStage": return new SfmExtValue(core.SetStage((int)args[0]));
                    case "SetPlayerPosition": return new SfmExtValue(core.SetPlayerPosition((Vector3)args[0]));
                    case "SetCrouch": return new SfmExtValue(core.SetCrouch((bool)args[0]));
                    case "TriggerOrgasm": return new SfmExtValue(core.TriggerOrgasm());
                    case "DeactivateSex": return new SfmExtValue(core.DeactivateSex());
                    case "BlockInput": return new SfmExtValue(core.BlockInput((bool)args[0]));
                    case "TriggerGameOver": return new SfmExtValue(core.TriggerGameOver());
                    case "SetEcstasy": return new SfmExtValue(core.SetEcstasy((float)args[0]));
                    case "SetMoisture": return new SfmExtValue(core.SetMoisture((float)args[0]));
                    case "SetStamina": return new SfmExtValue(core.SetStamina((float)args[0]));
                    case "SetSexPosition": return new SfmExtValue(core.SetSexPosition((int)args[0]));
                }
                return new SfmExtValue(false);
            }
            catch { return new SfmExtValue(false); }
        }

        private static float CallFloat(string method)
        {
            try
            {
                var core = OnlineCore.Instance;
                if (core == null) return 0;
                switch (method)
                {
                    case "GetEcstasy": return core.GetEcstasy();
                    case "GetMoisture": return core.GetMoisture();
                    case "GetStamina": return core.GetStamina();
                }
                return 0;
            }
            catch { return 0; }
        }
    }

    // ====================================================================
    //  全局状态（模仿 V2 Globals 简化）：当前地图/时间/存档槽等
    // ====================================================================
    public static class SfmExtState
    {
        [SfmExtFunction("state.get_stage")]
        public static SfmExtValue FnGetStage(SfmExtParams p, SfmExtValue unused)
            => new SfmExtValue(GetStage());

        [SfmExtFunction("state.get_daytime")]
        public static SfmExtValue FnGetDaytime(SfmExtParams p, SfmExtValue unused)
            => new SfmExtValue(GetDaytime());

        [SfmExtFunction("state.set_daytime")]
        public static SfmExtValue FnSetDaytime(SfmExtParams p, SfmExtValue unused)
        {
            SetDaytime((float)p.Get("value").ToFloat());
            return SfmExtValue.Null;
        }

        [SfmExtFunction("state.get_players")]
        public static SfmExtValue FnGetPlayers(SfmExtParams p, SfmExtValue unused)
        {
            var v = new SfmExtValue(SfmExtValue.Type.List);
            var uids = OnlineCoreExt.GetGhostUids?.Invoke() ?? new List<string>();
            for (int i = 0; i < uids.Count; i++) v[i.ToString()] = new SfmExtValue(uids[i]);
            return v;
        }

        public static int GetStage()
        {
            try
            {
                var core = OnlineCore.Instance;
                return core != null ? core.CurrentStageIntPublic() : -1;
            }
            catch { return -1; }
        }

        public static float GetDaytime()
        {
            try { return OnlineCore.GetDaytime(); } catch { return 0f; }
        }

        public static void SetDaytime(float v)
        {
            try { OnlineCore.SetDaytime(v); } catch { }
        }
    }

    // ====================================================================
    //  玩家数据（模仿 V2 PlayerData / Functions GetPlayerData）：
    //  本地玩家属性读取 + 远端玩家信息
    // ====================================================================
    public static class SfmExtPlayer
    {
        [SfmExtFunction("player.get_uid")]
        public static SfmExtValue FnGetUid(SfmExtParams p, SfmExtValue unused)
            => new SfmExtValue(GetLocalUid());

        [SfmExtFunction("player.get_name")]
        public static SfmExtValue FnGetName(SfmExtParams p, SfmExtValue unused)
            => new SfmExtValue(GetLocalName());

        [SfmExtFunction("player.get_position")]
        public static SfmExtValue FnGetPosition(SfmExtParams p, SfmExtValue unused)
        {
            var pos = GetLocalPosition();
            return new SfmExtValue(SfmExtValue.Type.List)
            {
                ["x"] = new SfmExtValue(pos.x), ["y"] = new SfmExtValue(pos.y), ["z"] = new SfmExtValue(pos.z)
            };
        }

        [SfmExtFunction("player.is_ingame")]
        public static SfmExtValue FnIsInGame(SfmExtParams p, SfmExtValue unused)
            => new SfmExtValue(IsInGame());

        public static string GetLocalUid()
        {
            try
            {
                var core = OnlineCore.Instance;
                if (core == null) return "";
                return core.PeerId ?? "";
            }
            catch { return ""; }
        }

        public static string GetLocalName()
        {
            try
            {
                var core = OnlineCore.Instance;
                return core != null ? core.GetLocalNamePublic() : "";
            }
            catch { return ""; }
        }

        public static Vector3 GetLocalPosition()
        {
            try
            {
                var f = PlayerFacade.Instance;
                if (f != null && f.pca != null && f.pca.AvatorTransform != null)
                    return f.pca.AvatorTransform.position;
            }
            catch { }
            return Vector3.zero;
        }

        public static bool IsInGame()
        {
            try
            {
                var core = OnlineCore.Instance;
                return core != null && core.InGamePublic;
            }
            catch { return false; }
        }

        /// <summary>获取远端玩家位置（relay/直连兼容）。</summary>
        public static Vector3 GetGhostPosition(string uid)
        {
            try
            {
                var core = OnlineCore.Instance;
                return core != null ? core.GetGhostPosition(uid) : Vector3.zero;
            }
            catch { return Vector3.zero; }
        }
    }

    // ====================================================================
    //  聊天扩展（模仿 V2 ChatManager + 我们模组的 pub_chat）：
    //  发送公共/房间聊天，注册聊天回调
    // ====================================================================
    public static class SfmExtChat
    {
        private static readonly List<Action<string, string, string>> _handlers
            = new List<Action<string, string, string>>(); // (uid, name, text)

        public static void OnMessage(Action<string, string, string> handler) => _handlers.Add(handler);

        public static void Send(string text, bool toRoom = true)
        {
            try
            {
                if (toRoom) SfmExtBridge.SendChat?.Invoke(text);
            }
            catch { }
        }

        public static void SendPrivate(string uid, string text)
        {
            try { SfmExtBridge.SendPrivateChat?.Invoke(uid, text); }
            catch { }
        }

        // 由 OnlineCore 收到 pub_chat/chat 时调用
        internal static void HandleMessage(string uid, string name, string text)
        {
            foreach (var h in _handlers.ToArray())
            {
                try { h(uid, name, text); } catch { }
            }
        }

        [SfmExtFunction("chat.send")]
        public static SfmExtValue FnSend(SfmExtParams p, SfmExtValue unused)
        {
            Send(p.Get("text").ToString());
            return SfmExtValue.Null;
        }
    }

    // ====================================================================
    //  相机控制（模仿 V2 SetCamera）
    // ====================================================================
    public static class SfmExtCamera
    {
        [SfmExtFunction("camera.set_pos")]
        public static SfmExtValue FnSetPos(SfmExtParams p, SfmExtValue unused)
        {
            return SfmExtPlay.FnSetPosition(p, unused);
        }
    }
}
