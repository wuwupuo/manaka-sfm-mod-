using System;
using UnityEngine;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  游戏函数库（模仿 V2 Functions 的游戏状态/物品/动作类）：
    //  兴奋度/湿润度/体力/精神/物品/动作/服装/道具/技能/RP/相机
    // ====================================================================
    public static class SfmExtGame
    {
        // ---------- 兴奋度 / 湿润度 / 体力 / 精神 ----------
        [SfmExtFunction("game.get_ecstasy")] public static SfmExtValue FnGetEcstasy(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(SfmExtBridge.GetEcstasy != null ? SfmExtBridge.GetEcstasy() : 0f);
        [SfmExtFunction("game.set_ecstasy")] public static SfmExtValue FnSetEcstasy(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.SetEcstasy?.Invoke((float)p.Get("value").ToFloat()); return SfmExtValue.Null; }
        [SfmExtFunction("game.add_ecstasy")] public static SfmExtValue FnAddEcstasy(SfmExtParams p, SfmExtValue u)
        { float cur = SfmExtBridge.GetEcstasy != null ? SfmExtBridge.GetEcstasy() : 0f; SfmExtBridge.SetEcstasy?.Invoke(cur + (float)p.Get("value").ToFloat()); return SfmExtValue.Null; }

        [SfmExtFunction("game.get_moisture")] public static SfmExtValue FnGetMoisture(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(SfmExtBridge.GetMoisture != null ? SfmExtBridge.GetMoisture() : 0f);
        [SfmExtFunction("game.set_moisture")] public static SfmExtValue FnSetMoisture(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.SetMoisture?.Invoke((float)p.Get("value").ToFloat()); return SfmExtValue.Null; }
        [SfmExtFunction("game.add_moisture")] public static SfmExtValue FnAddMoisture(SfmExtParams p, SfmExtValue u)
        { float cur = SfmExtBridge.GetMoisture != null ? SfmExtBridge.GetMoisture() : 0f; SfmExtBridge.SetMoisture?.Invoke(cur + (float)p.Get("value").ToFloat()); return SfmExtValue.Null; }

        [SfmExtFunction("game.get_stamina")] public static SfmExtValue FnGetStamina(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(SfmExtBridge.GetStamina != null ? SfmExtBridge.GetStamina() : 0f);
        [SfmExtFunction("game.set_stamina")] public static SfmExtValue FnSetStamina(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.SetStamina?.Invoke((float)p.Get("value").ToFloat()); return SfmExtValue.Null; }
        [SfmExtFunction("game.add_stamina")] public static SfmExtValue FnAddStamina(SfmExtParams p, SfmExtValue u)
        { float cur = SfmExtBridge.GetStamina != null ? SfmExtBridge.GetStamina() : 0f; SfmExtBridge.SetStamina?.Invoke(cur + (float)p.Get("value").ToFloat()); return SfmExtValue.Null; }

        [SfmExtFunction("game.get_mental")] public static SfmExtValue FnGetMental(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(SfmExtBridge.GetMental != null ? SfmExtBridge.GetMental() : 0f);

        // ---------- 玩家状态 ----------
        [SfmExtFunction("game.is_ingame")] public static SfmExtValue FnIsInGame(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(SfmExtBridge.IsInGame != null && SfmExtBridge.IsInGame());
        [SfmExtFunction("game.get_stage")] public static SfmExtValue FnGetStage(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(SfmExtBridge.GetStage != null ? SfmExtBridge.GetStage() : -1);
        [SfmExtFunction("game.get_daytime")] public static SfmExtValue FnGetDaytime(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(SfmExtBridge.GetDaytime != null ? SfmExtBridge.GetDaytime() : 0f);
        [SfmExtFunction("game.set_daytime")] public static SfmExtValue FnSetDaytime(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.SetDaytimeAction?.Invoke(p.Get("value").ToFloat() >= 0.5f); return SfmExtValue.Null; }

        // ---------- 动作 ----------
        [SfmExtFunction("game.set_action")] public static SfmExtValue FnSetAction(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.SetActionByName?.Invoke(p.Get("action").ToString()); return SfmExtValue.Null; }
        [SfmExtFunction("game.set_stage")] public static SfmExtValue FnSetStage(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.SetStageAction?.Invoke((int)p.Get("stage").ToFloat()); return SfmExtValue.Null; }
        [SfmExtFunction("game.set_position")] public static SfmExtValue FnSetPosition(SfmExtParams p, SfmExtValue u)
        {
            SfmExtBridge.SetPlayerPosition?.Invoke(new Vector3((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat()));
            return SfmExtValue.Null;
        }
        [SfmExtFunction("game.teleport")] public static SfmExtValue FnTeleport(SfmExtParams p, SfmExtValue u) => FnSetPosition(p, u);
        [SfmExtFunction("game.get_position")] public static SfmExtValue FnGetPosition(SfmExtParams p, SfmExtValue u)
        {
            var pos = SfmExtBridge.GetLocalPosition != null ? SfmExtBridge.GetLocalPosition() : Vector3.zero;
            var v = new SfmExtValue(SfmExtValue.Type.List);
            v["x"] = new SfmExtValue(pos.x); v["y"] = new SfmExtValue(pos.y); v["z"] = new SfmExtValue(pos.z);
            return v;
        }
        [SfmExtFunction("game.set_crouch")] public static SfmExtValue FnSetCrouch(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.SetCrouch?.Invoke(p.Get("value").ToBool()); return SfmExtValue.Null; }
        [SfmExtFunction("game.trigger_orgasm")] public static SfmExtValue FnTriggerOrgasm(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(SfmExtBridge.TriggerOrgasm != null && SfmExtBridge.TriggerOrgasm());
        [SfmExtFunction("game.deactivate_sex")] public static SfmExtValue FnDeactivateSex(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(SfmExtBridge.DeactivateSex != null && SfmExtBridge.DeactivateSex());
        [SfmExtFunction("game.set_sex_position")] public static SfmExtValue FnSetSexPos(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.SetSexPosition?.Invoke((int)p.Get("position").ToFloat()); return SfmExtValue.Null; }
        [SfmExtFunction("game.gameover")] public static SfmExtValue FnGameOver(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(SfmExtBridge.TriggerGameOver != null && SfmExtBridge.TriggerGameOver());
        [SfmExtFunction("game.block_input")] public static SfmExtValue FnBlockInput(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.BlockInput?.Invoke(p.Get("block").ToBool()); return SfmExtValue.Null; }

        // ---------- 道具 ----------
        [SfmExtFunction("game.set_vibrator")] public static SfmExtValue FnSetVibrator(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.SetVibrator?.Invoke((int)p.Get("stage").ToFloat()); return SfmExtValue.Null; }
        [SfmExtFunction("game.set_piston")] public static SfmExtValue FnSetPiston(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.SetPiston?.Invoke((int)p.Get("stage").ToFloat()); return SfmExtValue.Null; }
        [SfmExtFunction("game.lock_handcuffs")] public static SfmExtValue FnLockHandcuffs(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.LockHandcuffs?.Invoke(p.Get("type").ToString()); return SfmExtValue.Null; }
        [SfmExtFunction("game.unlock_handcuffs")] public static SfmExtValue FnUnlockHandcuffs(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.UnlockHandcuffs?.Invoke(p.Get("type").ToString()); return SfmExtValue.Null; }
        [SfmExtFunction("game.set_adult_goods")] public static SfmExtValue FnSetAdultGoods(SfmExtParams p, SfmExtValue u)
        {
            SfmExtBridge.SetAdultGoods?.Invoke(p.Get("type").ToString(), (int)p.Get("stage").ToFloat(), p.Get("on").ToBool());
            return SfmExtValue.Null;
        }
        [SfmExtFunction("game.set_cosplay")] public static SfmExtValue FnSetCosplay(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.SetCosplay?.Invoke(p.Get("name").ToString(), p.Get("on").ToBool()); return SfmExtValue.Null; }

        // ---------- 物品数量 ----------
        [SfmExtFunction("game.get_item_count")] public static SfmExtValue FnGetItemCount(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(SfmExtBridge.GetItemCount != null ? SfmExtBridge.GetItemCount() : 0);
        [SfmExtFunction("game.set_item_count")] public static SfmExtValue FnSetItemCount(SfmExtParams p, SfmExtValue u)
        { SfmExtBridge.SetItemCount?.Invoke((int)p.Get("count").ToFloat()); return SfmExtValue.Null; }

        // ---------- 相机 ----------
        [SfmExtFunction("camera.get_position")] public static SfmExtValue FnCamGetPos(SfmExtParams p, SfmExtValue u)
        {
            try
            {
                var cam = Camera.main;
                if (cam == null) return SfmExtValue.Null;
                var v = new SfmExtValue(SfmExtValue.Type.List);
                v["x"] = new SfmExtValue(cam.transform.position.x);
                v["y"] = new SfmExtValue(cam.transform.position.y);
                v["z"] = new SfmExtValue(cam.transform.position.z);
                return v;
            }
            catch { return SfmExtValue.Null; }
        }
        [SfmExtFunction("camera.set_position")] public static SfmExtValue FnCamSetPos(SfmExtParams p, SfmExtValue u)
        {
            try
            {
                var cam = Camera.main;
                if (cam == null) return new SfmExtValue(false);
                cam.transform.position = new Vector3((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat());
                return new SfmExtValue(true);
            }
            catch { return new SfmExtValue(false); }
        }
        [SfmExtFunction("camera.get_rotation")] public static SfmExtValue FnCamGetRot(SfmExtParams p, SfmExtValue u)
        {
            try
            {
                var cam = Camera.main;
                if (cam == null) return SfmExtValue.Null;
                var e = cam.transform.eulerAngles;
                var v = new SfmExtValue(SfmExtValue.Type.List);
                v["x"] = new SfmExtValue(e.x); v["y"] = new SfmExtValue(e.y); v["z"] = new SfmExtValue(e.z);
                return v;
            }
            catch { return SfmExtValue.Null; }
        }
        [SfmExtFunction("camera.set_rotation")] public static SfmExtValue FnCamSetRot(SfmExtParams p, SfmExtValue u)
        {
            try
            {
                var cam = Camera.main;
                if (cam == null) return new SfmExtValue(false);
                cam.transform.rotation = Quaternion.Euler((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat());
                return new SfmExtValue(true);
            }
            catch { return new SfmExtValue(false); }
        }
    }
}
