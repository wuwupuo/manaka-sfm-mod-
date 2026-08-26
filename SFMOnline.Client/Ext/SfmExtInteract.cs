using System;
using System.Collections.Generic;
using UnityEngine;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  玩家交互 API：
    //  跟随请求 / 传送 / 远程动作 / 玩具控制 / 抚摸 / 手铐
    //  复用联机核心的 Control 通道（SendDirectControl）和 relay 的
    //  toy_control / ext 消息。
    // ====================================================================
    public static class SfmExtInteract
    {
        // 桥接：发送控制命令（由联机核心注册，直连/relay 通用）
        public static Action<string, string, string, int, bool> SendControlCommand;

        // ---------- 跟随 / 传送 ----------
        /// <summary>请求跟随某人（对方同意后传送）。</summary>
        public static void Follow(string uid)
        {
            SendControlCommand?.Invoke("control", uid, "follow", 0, false);
        }

        /// <summary>召集某人前往自己位置。</summary>
        public static void Summon(string uid)
        {
            SendControlCommand?.Invoke("control", uid, "summon", 0, false);
        }

        /// <summary>传送到某人身边（需对方允许或房主）。</summary>
        public static void TeleportTo(string uid)
        {
            SendControlCommand?.Invoke("control", uid, "teleport_to", 0, false);
        }

        // ---------- 远程动作 ----------
        /// <summary>让对方执行某个动作（ActionType 数字）。</summary>
        public static void RemoteAction(string uid, int actionType)
        {
            SendControlCommand?.Invoke("control", uid, "action", actionType, false);
        }

        /// <summary>让对方蹲下/站立。</summary>
        public static void RemoteCrouch(string uid, bool crouch)
        {
            SendControlCommand?.Invoke("control", uid, "crouch", 0, crouch);
        }

        // ---------- 抚摸 ----------
        public static void Finger(string uid, bool start)
        {
            SendControlCommand?.Invoke("control", uid, "finger", 0, start);
        }

        public static void FingerPleasure(string uid)
        {
            SendControlCommand?.Invoke("control", uid, "finger_pleasure", 0, false);
        }

        // ---------- 玩具控制 ----------
        /// <summary>请求控制对方的玩具。</summary>
        public static void ToyInvite(string uid)
        {
            SendControlCommand?.Invoke("control", uid, "invite", 0, false);
        }

        /// <summary>接受玩具控制请求。</summary>
        public static void ToyAccept(string uid)
        {
            SendControlCommand?.Invoke("control", uid, "accept", 0, false);
        }

        /// <summary>拒绝玩具控制请求。</summary>
        public static void ToyReject(string uid)
        {
            SfmExtBridge.ToyReject?.Invoke(uid);
            SendControlCommand?.Invoke("control", uid, "reject", 0, false);
        }

        /// <summary>解除与某人的玩具链接。</summary>
        public static void ToyRevoke(string uid)
        {
            SfmExtBridge.ToyRevoke?.Invoke(uid);
            SendControlCommand?.Invoke("control", uid, "revoke", 0, false);
        }

        /// <summary>当前谁在控制我的玩具（空=无人）。</summary>
        public static string ToyController => SfmExtBridge.GetToyController != null ? SfmExtBridge.GetToyController() : "";

        /// <summary>是否已建立玩具链接。</summary>
        public static bool ToyLinked => SfmExtBridge.IsToyLinked != null && SfmExtBridge.IsToyLinked();

        public static void ToyVibrate(string uid, int stage)
        {
            SfmExtBridge.SendToyControl?.Invoke(uid, "vibrate", stage, 0);
            SendControlCommand?.Invoke("control", uid, "vibrate", stage, false);
        }

        public static void ToyPiston(string uid, int stage)
        {
            SfmExtBridge.SendToyControl?.Invoke(uid, "piston", stage, 0);
            SendControlCommand?.Invoke("control", uid, "piston", stage, false);
        }

        public static void ToyUndress(string uid)
        {
            SfmExtBridge.SendToyControl?.Invoke(uid, "undress", 0, 0);
            SendControlCommand?.Invoke("control", uid, "undress", 0, false);
        }

        public static void ToyAllVibrate(int stage)
        {
            foreach (var uid in SfmExtBridge.GetGhostUids?.Invoke() ?? new List<string>())
                ToyVibrate(uid, stage);
        }

        public static void ToyAllPiston(int stage)
        {
            foreach (var uid in SfmExtBridge.GetGhostUids?.Invoke() ?? new List<string>())
                ToyPiston(uid, stage);
        }

        // ---------- 手铐 ----------
        public static void Handcuff(string uid)
        {
            SendControlCommand?.Invoke("control", uid, "handcuff", 0, false);
        }

        public static void UnlockHandcuffs(string uid)
        {
            SendControlCommand?.Invoke("control", uid, "unlock_handcuffs", 0, false);
        }

        // ---------- 引擎函数 ----------
        [SfmExtFunction("interact.follow")]
        public static SfmExtValue FnFollow(SfmExtParams p, SfmExtValue u)
        {
            Follow(p.Get("uid").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("interact.summon")]
        public static SfmExtValue FnSummon(SfmExtParams p, SfmExtValue u)
        {
            Summon(p.Get("uid").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("interact.teleport_to")]
        public static SfmExtValue FnTeleportTo(SfmExtParams p, SfmExtValue u)
        {
            TeleportTo(p.Get("uid").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("interact.action")]
        public static SfmExtValue FnRemoteAction(SfmExtParams p, SfmExtValue u)
        {
            RemoteAction(p.Get("uid").ToString(), (int)p.Get("action").ToFloat());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("interact.finger")]
        public static SfmExtValue FnFinger(SfmExtParams p, SfmExtValue u)
        {
            Finger(p.Get("uid").ToString(), p.Get("start").ToBool());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("interact.vibrate")]
        public static SfmExtValue FnVibrate(SfmExtParams p, SfmExtValue u)
        {
            ToyVibrate(p.Get("uid").ToString(), (int)p.Get("stage").ToFloat());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("interact.piston")]
        public static SfmExtValue FnPiston(SfmExtParams p, SfmExtValue u)
        {
            ToyPiston(p.Get("uid").ToString(), (int)p.Get("stage").ToFloat());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("interact.undress")]
        public static SfmExtValue FnUndress(SfmExtParams p, SfmExtValue u)
        {
            ToyUndress(p.Get("uid").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("interact.handcuff")]
        public static SfmExtValue FnHandcuff(SfmExtParams p, SfmExtValue u)
        {
            Handcuff(p.Get("uid").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("interact.toy_invite")]
        public static SfmExtValue FnToyInvite(SfmExtParams p, SfmExtValue u)
        {
            ToyInvite(p.Get("uid").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("interact.toy_accept")]
        public static SfmExtValue FnToyAccept(SfmExtParams p, SfmExtValue u)
        {
            ToyAccept(p.Get("uid").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("interact.toy_reject")]
        public static SfmExtValue FnToyReject(SfmExtParams p, SfmExtValue u)
        {
            ToyReject(p.Get("uid").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("interact.toy_revoke")]
        public static SfmExtValue FnToyRevoke(SfmExtParams p, SfmExtValue u)
        {
            ToyRevoke(p.Get("uid").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("interact.toy_controller")]
        public static SfmExtValue FnToyController(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(ToyController);

        [SfmExtFunction("interact.toy_linked")]
        public static SfmExtValue FnToyLinked(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(ToyLinked);

        [SfmExtFunction("interact.toy_all_vibrate")]
        public static SfmExtValue FnToyAllVibrate(SfmExtParams p, SfmExtValue u)
        {
            ToyAllVibrate((int)p.Get("stage").ToFloat());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("interact.toy_all_piston")]
        public static SfmExtValue FnToyAllPiston(SfmExtParams p, SfmExtValue u)
        {
            ToyAllPiston((int)p.Get("stage").ToFloat());
            return new SfmExtValue(true);
        }
    }
}
