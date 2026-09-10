using System;
using System.Collections.Generic;
using UnityEngine;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  桥接层：Ext 前置框架与联机核心（OnlineCore）解耦。
    //  OnlineCore.Awake() 时调用 SfmExtBridge.Install() 注册实现。
    //  桥接未注册时，前置框架仍可独立运行（本地玩法 API 可用）。
    // ====================================================================
    public static class SfmExtBridge
    {
        // ---------- 联机消息 ----------
        public static Action<Dictionary<string, object>> SendToServer;
        public static Action<Dictionary<string, object>> SendToRoom;
        public static Action<string, Dictionary<string, object>> SendToPlayer;

        // ---------- 玩家 ----------
        public static Func<string> GetLocalUid;
        public static Func<string> GetLocalName;
        public static Func<bool> IsInGame;
        public static Func<Vector3> GetLocalPosition;
        public static Func<string, Vector3> GetGhostPosition;
        public static Func<List<string>> GetGhostUids;
        public static Func<string, GameObject> GetGhostRoot;

        // ---------- 游戏状态 ----------
        public static Func<int> GetStage;
        public static Func<float> GetEcstasy;
        public static Func<float> GetMoisture;
        public static Func<float> GetStamina;
        public static Func<float> GetMental;
        public static Func<float> GetDaytime;
        public static Func<int> GetItemCount;

        // ---------- 游戏操作 ----------
        public static Func<bool> TriggerOrgasm;
        public static Func<bool> DeactivateSex;
        public static Func<bool> TriggerGameOver;
        public static Action<float> SetEcstasy;
        public static Action<float> SetMoisture;
        public static Action<float> SetStamina;
        public static Action<int> SetSexPosition;
        public static Action<int> SetStageAction;
        public static Action<string> SetActionByName;
        public static Action<Vector3> SetPlayerPosition;
        public static Action<bool> SetCrouch;
        public static Action<bool> SetDaytimeAction;
        public static Action<bool> BlockInput;
        public static Action<string, int, bool> SetAdultGoods;
        public static Action<string, bool> SetCosplay;
        public static Action<int> SetVibrator;
        public static Action<int> SetPiston;
        public static Action<string> LockHandcuffs;
        public static Action<string> UnlockHandcuffs;
        public static Action<int> SetItemCount;

        // ---------- 聊天 / 交互 ----------
        public static Action<string> SendChat;
        public static Action<string, string> SendPrivateChat;
        public static Action OnInteractKey;

        // ---------- 玩具控制（由联机核心注册） ----------
        public static Action<string, string, int, int> SendToyControl;   // (uid, cmd, stage, mode)
        public static Action<string> ToyRevoke;                          // 解除控制
        public static Action<string> ToyReject;                          // 拒绝控制
        public static Func<string> GetToyController;                     // 谁在控制我
        public static Func<bool> IsToyLinked;                            // 是否已链接玩具

        public static bool BridgeReady { get; private set; }

        /// <summary>由 OnlineCore.Awake 调用，注册联机实现。</summary>
        public static void Install(SfmExtBridgeImpl impl)
        {
            if (impl == null) return;
            SendToServer = impl.SendToServer;
            SendToRoom = impl.SendToRoom;
            SendToPlayer = impl.SendToPlayer;
            GetLocalUid = impl.GetLocalUid;
            GetLocalName = impl.GetLocalName;
            IsInGame = impl.IsInGame;
            GetLocalPosition = impl.GetLocalPosition;
            GetGhostPosition = impl.GetGhostPosition;
            GetGhostUids = impl.GetGhostUids;
            GetGhostRoot = impl.GetGhostRoot;
            GetStage = impl.GetStage;
            GetEcstasy = impl.GetEcstasy;
            GetMoisture = impl.GetMoisture;
            GetStamina = impl.GetStamina;
            GetMental = impl.GetMental;
            GetDaytime = impl.GetDaytime;
            GetItemCount = impl.GetItemCount;
            TriggerOrgasm = impl.TriggerOrgasm;
            DeactivateSex = impl.DeactivateSex;
            TriggerGameOver = impl.TriggerGameOver;
            SetEcstasy = impl.SetEcstasy;
            SetMoisture = impl.SetMoisture;
            SetStamina = impl.SetStamina;
            SetSexPosition = impl.SetSexPosition;
            SetStageAction = impl.SetStageAction;
            SetActionByName = impl.SetActionByName;
            SetPlayerPosition = impl.SetPlayerPosition;
            SetCrouch = impl.SetCrouch;
            SetDaytimeAction = impl.SetDaytimeAction;
            BlockInput = impl.BlockInput;
            SetAdultGoods = impl.SetAdultGoods;
            SetCosplay = impl.SetCosplay;
            SetVibrator = impl.SetVibrator;
            SetPiston = impl.SetPiston;
            LockHandcuffs = impl.LockHandcuffs;
            UnlockHandcuffs = impl.UnlockHandcuffs;
            SetItemCount = impl.SetItemCount;
            SendChat = impl.SendChat;
            SendPrivateChat = impl.SendPrivateChat;
            OnInteractKey = impl.OnInteractKey;
            SendToyControl = impl.SendToyControl;
            ToyRevoke = impl.ToyRevoke;
            ToyReject = impl.ToyReject;
            GetToyController = impl.GetToyController;
            IsToyLinked = impl.IsToyLinked;
            BridgeReady = true;
        }
    }

    /// <summary>联机核心实现的桥接载体。</summary>
    public sealed class SfmExtBridgeImpl
    {
        public Action<Dictionary<string, object>> SendToServer;
        public Action<Dictionary<string, object>> SendToRoom;
        public Action<string, Dictionary<string, object>> SendToPlayer;
        public Func<string> GetLocalUid;
        public Func<string> GetLocalName;
        public Func<bool> IsInGame;
        public Func<Vector3> GetLocalPosition;
        public Func<string, Vector3> GetGhostPosition;
        public Func<List<string>> GetGhostUids;
        public Func<string, GameObject> GetGhostRoot;
        public Func<int> GetStage;
        public Func<float> GetEcstasy;
        public Func<float> GetMoisture;
        public Func<float> GetStamina;
        public Func<float> GetMental;
        public Func<float> GetDaytime;
        public Func<int> GetItemCount;
        public Func<bool> TriggerOrgasm;
        public Func<bool> DeactivateSex;
        public Func<bool> TriggerGameOver;
        public Action<float> SetEcstasy;
        public Action<float> SetMoisture;
        public Action<float> SetStamina;
        public Action<int> SetSexPosition;
        public Action<int> SetStageAction;
        public Action<string> SetActionByName;
        public Action<Vector3> SetPlayerPosition;
        public Action<bool> SetCrouch;
        public Action<bool> SetDaytimeAction;
        public Action<bool> BlockInput;
        public Action<string, int, bool> SetAdultGoods;
        public Action<string, bool> SetCosplay;
        public Action<int> SetVibrator;
        public Action<int> SetPiston;
        public Action<string> LockHandcuffs;
        public Action<string> UnlockHandcuffs;
        public Action<int> SetItemCount;
        public Action<string> SendChat;
        public Action<string, string> SendPrivateChat;
        public Action OnInteractKey;
        public Action<string, string, int, int> SendToyControl;
        public Action<string> ToyRevoke;
        public Action<string> ToyReject;
        public Func<string> GetToyController;
        public Func<bool> IsToyLinked;
    }
}
