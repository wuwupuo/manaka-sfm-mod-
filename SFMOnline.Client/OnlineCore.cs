using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using ExposureUnnoticed2.Master.Action;
using ExposureUnnoticed2.Master.AdultGoods;
using ExposureUnnoticed2.Master.Cosplay;
using ExposureUnnoticed2.Master.Skill;
using ExposureUnnoticed2.Master.Stage;
using ExposureUnnoticed2.Object3D.AdultGoods;
using ExposureUnnoticed2.Object3D.NPC.Script;
using ExposureUnnoticed2.Object3D.DropedCoat;
using ExposureUnnoticed2.Object3D.PeeDecal;
using ExposureUnnoticed2.Object3D.ScenePlops.Elevator;
using ExposureUnnoticed2.Object3D.Player.Scripts;
using ExposureUnnoticed2.Object3D.Player.Scripts.Costume;
using ExposureUnnoticed2.Object3D.Player.Scripts.Other;
using ExposureUnnoticed2.Object3D.Portal;
using ExposureUnnoticed2.Object3D.RoutePoint;
using ExposureUnnoticed2.Scripts.InGame;
using ExposureUnnoticed2.Scripts.Mission;

namespace SFMOnline
{
    internal sealed class OnlineCore
    {
        // ===== 拓展框架桥接 =====
        internal static OnlineCore Instance;
        public static OnlineCore CoreInstance => Instance;

        public Transform FindLocalBonePublic(string name) => FindLocalBone(name);
        public GameObject GetGhostRootByUid(string uid)
        {
            if (_relayGhosts.TryGetValue(uid, out var g) && g != null && g.Root != null) return g.Root;
            if (_ghosts.TryGetValue(uid, out var g2) && g2 != null && g2.Root != null) return g2.Root;
            return null;
        }
        public Vector3 GetGhostPosition(string uid)
        {
            var go = GetGhostRootByUid(uid);
            return go != null ? go.transform.position : (_relayPositions.TryGetValue(uid, out var rp) ? new Vector3(rp.X, rp.Y, rp.Z) : Vector3.zero);
        }
        public System.Collections.Generic.List<string> GetGhostUids()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var k in _relayGhosts.Keys) list.Add(k);
            foreach (var k in _ghosts.Keys) if (!list.Contains(k)) list.Add(k);
            return list;
        }
        public bool SetPlayerPosition(Vector3 pos)
        {
            try
            {
                var avatar = PlayerFacade.Instance != null && PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.AvatorTransform : null;
                if (avatar == null) return false;
                avatar.position = pos;
                return true;
            }
            catch { return false; }
        }
        public bool SetCrouch(bool value)
        {
            try
            {
                PlayerFacade.Instance.TransAction(value ? ActionType.SitDown : ActionType.StandUp);
                return true;
            }
            catch { return false; }
        }
        public bool TriggerOrgasm()
        {
            try { PlayerFacade.Instance.TransAction(ActionType.OldOnaniNormal); return true; }
            catch { return false; }
        }
        public bool DeactivateSex()
        {
            try { PlayerFacade.Instance.TransAction(ActionType.PoseEnd); return true; }
            catch { return false; }
        }
        public bool BlockInput(bool block)
        {
            try
            {
                if (block) UnityEngine.Input.ResetInputAxes();
                return true;
            }
            catch { return false; }
        }
        public bool SetStage(int stage)
        {
            try
            {
                if (!InGame) return false;
                var cur = CurrentStageInt();
                if (cur == stage) return true;
                var stc = InGameManager.Instance != null ? InGameManager.Instance.StageTransController : null;
                if (stc == null || !stc.IsAbleTransStage()) return false;
                stc.TransStage((StageType)cur, (StageType)stage, null, 0.2f, null);
                return true;
            }
            catch { return false; }
        }
        public bool SetActionByName(string action)
        {
            try
            {
                if (int.TryParse(action, out int id)) { PlayerFacade.Instance.TransAction((ActionType)id); return true; }
                if (Enum.TryParse<ActionType>(action, true, out var at)) { PlayerFacade.Instance.TransAction(at); return true; }
                return false;
            }
            catch { return false; }
        }
        public float GetEcstasy()
        {
            try { var g = GetGameStateData(); return g != null ? g.PlayerEcstasy : 0f; }
            catch { return 0f; }
        }
        public float GetMoisture()
        {
            try { var g = GetGameStateData(); return g != null ? g.PlayerMoisture : 0f; }
            catch { return 0f; }
        }
        public float GetStamina()
        {
            try { var g = GetGameStateData(); return g != null ? g.PlayerLife : 0f; }
            catch { return 0f; }
        }
        public bool SetEcstasy(float v) { try { var g = GetGameStateData(); if (g != null) { g.PlayerEcstasy = v; return true; } return false; } catch { return false; } }
        public bool SetMoisture(float v) { try { var g = GetGameStateData(); if (g != null) { g.PlayerMoisture = v; return true; } return false; } catch { return false; } }
        public bool SetStamina(float v) { try { var g = GetGameStateData(); if (g != null) { g.PlayerLife = (int)v; return true; } return false; } catch { return false; } }
        public bool SetSexPosition(int pos) { return false; }
        public bool TriggerGameOver() { return false; }
        private static GameStateData GetGameStateData()
        {
            try
            {
                var prop = typeof(GameStateData).GetProperty("GameStateData", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                return prop != null ? prop.GetValue(null) as GameStateData : null;
            }
            catch { return null; }
        }
        public int CurrentStageIntPublic() => CurrentStageInt();
        public string GetLocalNamePublic()
        {
            try { return _nickname; }
            catch { return ""; }
        }
        public bool InGamePublic => InGame;
        public static float GetDaytime()
        {
            try
            {
                var gsd = typeof(GameStateData).GetProperty("GameStateData", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.GetValue(null) as GameStateData;
                return gsd != null && gsd.IsDaytime ? 1f : 0f;
            }
            catch { return 0f; }
        }
        public static void SetDaytime(float v)
        {
            try
            {
                var gsd = typeof(GameStateData).GetProperty("GameStateData", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.GetValue(null) as GameStateData;
                if (gsd != null) gsd.IsDaytime = v >= 0.5f;
            }
            catch { }
        }

        // ========== Ext 桥接注册（直连 + relay 双模式） ==========
        [HideFromIl2Cpp]
        private void InstallExtBridge()
        {
            try
            {
                var impl = new SFMOnline.Ext.SfmExtBridgeImpl();
                // 消息发送：根据当前模式路由
                impl.SendToServer = payload =>
                {
                    try { SendExtDirect(payload, ""); } catch { }
                };
                impl.SendToRoom = payload =>
                {
                    try
                    {
                        if (_relayMode) RelayTcp.Send(payload);
                        else SendExtDirect(payload, "");
                    }
                    catch { }
                };
                impl.SendToPlayer = (uid, payload) =>
                {
                    try
                    {
                        if (_relayMode) RelayTcp.Send(new Dictionary<string, object>(payload) { ["to"] = uid });
                        else SendExtDirect(payload, uid);
                    }
                    catch { }
                };
                // 玩家信息
                impl.GetLocalUid = () => PeerId;
                impl.GetLocalName = () => _nickname;
                impl.IsInGame = () => InGame;
                impl.GetLocalPosition = () =>
                {
                    try
                    {
                        var f = PlayerFacade.Instance;
                        return f != null && f.pca != null && f.pca.AvatorTransform != null ? f.pca.AvatorTransform.position : Vector3.zero;
                    }
                    catch { return Vector3.zero; }
                };
                impl.GetGhostPosition = uid => GetGhostPosition(uid);
                impl.GetGhostUids = () => GetGhostUids();
                impl.GetGhostRoot = uid => GetGhostRootByUid(uid);
                impl.GetStage = () => CurrentStageInt();
                impl.GetEcstasy = GetEcstasy;
                impl.GetMoisture = GetMoisture;
                impl.GetStamina = GetStamina;
                impl.GetMental = () => 0f;
                impl.GetDaytime = () => GetDaytime();
                impl.GetItemCount = () => 0;
                // 游戏操作
                impl.TriggerOrgasm = TriggerOrgasm;
                impl.DeactivateSex = DeactivateSex;
                impl.TriggerGameOver = () => false;
                impl.SetEcstasy = v => { try { SetEcstasy(v); } catch { } };
                impl.SetMoisture = v => { try { SetMoisture(v); } catch { } };
                impl.SetStamina = v => { try { SetStamina(v); } catch { } };
                impl.SetSexPosition = pos => { };
                impl.SetStageAction = stage => { try { SetStage(stage); } catch { } };
                impl.SetActionByName = action => { try { SetActionByName(action); } catch { } };
                impl.SetPlayerPosition = pos => { try { SetPlayerPosition(pos); } catch { } };
                impl.SetCrouch = value => { try { SetCrouch(value); } catch { } };
                impl.SetDaytimeAction = value => { try { SetDaytime(value ? 1f : 0f); } catch { } };
                impl.BlockInput = value => { try { BlockInput(value); } catch { } };
                impl.SetAdultGoods = (type, stage, on) =>
                {
                    try
                    {
                        // 映射道具名 → MAdultGoodsType
                        var t = MAdultGoodsType.EyeMask;
                        switch (type)
                        {
                            case "Vibrator": t = MAdultGoodsType.Vibrator; break;
                            case "PistonPussy": t = MAdultGoodsType.PistonPussy; break;
                            case "PistonAnal": t = MAdultGoodsType.PistonAnal; break;
                            case "PistonFuta": t = MAdultGoodsType.PistonFuta; break;
                            case "EyeMask": t = MAdultGoodsType.EyeMask; break;
                            case "Handcuff": t = MAdultGoodsType.Handcuff; break;
                            case "KeyHandcuff": t = MAdultGoodsType.KeyHandcuff; break;
                            case "TimerHandcuff": t = MAdultGoodsType.TimerHandcuff; break;
                            default:
                                if (Enum.TryParse<MAdultGoodsType>(type, true, out var parsed)) t = parsed;
                                break;
                        }
                        var pf = PlayerFacade.Instance;
                        if (pf == null) return;
                        pf.ForceChangeAdultGoods(t, on);
                        // 关联动作（戴/摘）
                        if (on)
                        {
                            if (t == MAdultGoodsType.EyeMask) pf.TransAction(ActionType.AttachEyeMask);
                            else if (t == MAdultGoodsType.AnalPlug) pf.TransAction(ActionType.InsertAnalPlug);
                            else if (t == MAdultGoodsType.Vibrator) pf.TransAction(ActionType.SwitchVibrator);
                            else if (t == MAdultGoodsType.Handcuff || t == MAdultGoodsType.KeyHandcuff || t == MAdultGoodsType.TimerHandcuff) pf.TransAction(ActionType.AttachHandcuffs);
                        }
                        else
                        {
                            if (t == MAdultGoodsType.AnalPlug) pf.TransAction(ActionType.ExtractAnalPlug);
                            else if (t == MAdultGoodsType.EyeMask) pf.TransAction(ActionType.AttachEyeMask);
                        }
                    }
                    catch { }
                };
                impl.SetCosplay = (name, on) =>
                {
                    try
                    {
                        // 映射服装名 → CosplayType（邪教徒等）
                        var ct = CosplayType.Cultist;
                        if (!string.IsNullOrEmpty(name))
                        {
                            if (!Enum.TryParse<CosplayType>(name, true, out ct)) ct = CosplayType.Cultist;
                        }
                        var pf = PlayerFacade.Instance;
                        if (pf == null) return;
                        pf.SetCosplayActive(ct, -1, on); // -1 = 整套
                    }
                    catch { }
                };
                impl.SetVibrator = stage => { };
                impl.SetPiston = stage => { };
                impl.LockHandcuffs = type => { };
                impl.UnlockHandcuffs = type => { };
                impl.SetItemCount = count => { };
                // 聊天 / 交互
                impl.SendChat = text =>
                {
                    try
                    {
                        if (_relayMode) RelayTcp.Send(new Dictionary<string, object> { ["t"] = "pub_chat", ["m"] = text });
                        else SendExtDirect(new Dictionary<string, object> { ["t"] = "ext_evt", ["ns"] = "chat", ["evt"] = "send", ["data"] = text }, "");
                    }
                    catch { }
                };
                impl.SendPrivateChat = (uid, text) =>
                {
                    try
                    {
                        if (_relayMode) RelayTcp.Send(new Dictionary<string, object> { ["t"] = "pub_chat", ["m"] = text, ["to"] = uid });
                        else SendExtDirect(new Dictionary<string, object> { ["t"] = "ext_evt", ["ns"] = "chat", ["evt"] = "send", ["data"] = text }, uid);
                    }
                    catch { }
                };
                impl.OnInteractKey = () => { };
                // 玩具控制（relay 走 toy_control，直连走 Control）
                impl.SendToyControl = (uid, cmd, stage, mode) =>
                {
                    try
                    {
                        if (_relayMode)
                        {
                            RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = uid, ["d"] = cmd, ["stage"] = stage, ["mode"] = mode, ["on"] = stage > 0 });
                        }
                        else
                        {
                            SendDirectControl("control", uid, cmd, stage, stage > 0);
                        }
                    }
                    catch { }
                };
                impl.ToyRevoke = uid =>
                {
                    try
                    {
                        if (_relayMode) RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_revoke", ["to"] = uid });
                    }
                    catch { }
                };
                impl.ToyReject = uid =>
                {
                    try
                    {
                        if (_relayMode) RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_reject", ["to"] = uid });
                    }
                    catch { }
                };
                impl.GetToyController = () => _toyLinkedController;
                impl.IsToyLinked = () => _toyLinkedController.Length > 0 || _toyLinkedTarget.Length > 0;
                SFMOnline.Ext.SfmExtBridge.Install(impl);
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("Ext 桥接注册失败: " + ex.Message);
            }
        }

        // 直连模式发送 Ext 消息（Control 封装，target 空=广播）
        [HideFromIl2Cpp]
        private void SendExtDirect(Dictionary<string, object> payload, string target)
        {
            if (!Connected) return;
            try
            {
                var json = MiniJson.Serialize(payload);
                var w = new WireWriter();
                w.WriteString(PeerId);
                w.WriteString(target ?? "");
                w.WriteString("ext");
                w.WriteString(json);
                w.WriteInt(0);
                w.WriteBool(false);
                if (IsHosting)
                {
                    if (target.Length > 0) _host.SendToClients(MsgTypes.Control, w.ToArray(), null);
                    else _host.SendToClients(MsgTypes.Control, w.ToArray(), null);
                }
                else if (IsClient)
                {
                    _client.Send(MsgTypes.Control, w.ToArray());
                }
            }
            catch { }
        }

        // ========== 网络核心 ==========
        private NetHost _host;
        private NetClient _client;
        private readonly Queue<NetMsg> _drain = new Queue<NetMsg>();

        // ========== 玩家数据 ==========
        private readonly Dictionary<string, PeerInfo> _peers = new Dictionary<string, PeerInfo>();
        private readonly Dictionary<string, RemoteState> _lastStates = new Dictionary<string, RemoteState>();
        private readonly Dictionary<string, GhostPlayer> _ghosts = new Dictionary<string, GhostPlayer>();
        private readonly Dictionary<string, float> _ghostCreateTimes = new Dictionary<string, float>();
        private readonly HashSet<string> _ghostWarned = new HashSet<string>();
        private readonly HashSet<string> _ghostToasted = new HashSet<string>();
        private GameObject _ghostRoot;
        private readonly Dictionary<string, int> _followedHostStage = new Dictionary<string, int>();
        private int _lastLocalAvatarId = int.MinValue;
        private bool _sceneGuardInitialized;
        private bool _sceneWasInGame;
        private int _sceneObservedStage = int.MinValue;
        private float _sceneSyncBlockedUntil = -999f;

        // ========== 动画参数 ==========
        private List<AnimParamDef> _floatParams = new List<AnimParamDef>();
        private List<AnimParamDef> _intParams = new List<AnimParamDef>();
        private List<AnimParamDef> _boolParams = new List<AnimParamDef>();
        private bool _paramBuilt;
        private float _lastWarnTime;

        // ========== UI ==========
        private bool _showMenu;
        private bool _onlineMenuOnly;
        private Rect _menuRect = new Rect(30f, 30f, 460f, 900f);
        private Font _font;
        private GUIStyle _fieldLabelStyle = null;
        private GUIStyle _wrapStyle = null;
        private string _focusedField = "";
        private float _lastImeOpenTime = -999f;
        private int _lastTextFrame = -1;

        // ========== 用户输入 ==========
        private string _nickname = "玩家";
        private string _portText = "27570";
        private string _maxPlayersText = "8";
        private string _passwordText = "";
        private string _addressText = "";
        private string _chatInput = "";
        private readonly Queue<float> _chatSendTimes = new Queue<float>();
        private readonly List<string> _chatMessages = new List<string>();
        private readonly List<(string text, float expire)> _toasts = new List<(string, float)>();
        private GUIStyle _toastStyle;

        // ========== 状态同步 ==========
        private float _lastStateTime;
        private int _directStateSyncCount;
        private string _lastDirectAppearanceSig = "";
        private string _lastRelayAppearanceSig = "";
        private float _appearanceProbeAt=-999f;
        private int _appearanceProbeAvatarId=int.MinValue;
        private string _appearanceProbeSig="";
        private string _appearanceRequestRoom = "";
        private int _lastRelaySentAction = int.MinValue;
        private readonly Dictionary<string, int> _relayActionHints = new Dictionary<string, int>();
        private float _lastPingTime;
        private float _lastLanAdvertise;
        private bool _eventsInitialized;
        private int _lastAction = int.MinValue;
        private int _lastPlayerState = int.MinValue;
        private int _lastClothesType = int.MinValue;
        private int _lastClothesB = int.MinValue;
        private int _lastStage = int.MinValue;
        private int _lastSex = int.MinValue;
        private int _lastGameOver = int.MinValue;
        private readonly HashSet<int> _sentMissions = new HashSet<int>();

        // ========== 跟随 ==========
        private int _pendingFollowStage = -1;
        private Vector3 _pendingFollowPos;
        private float _pendingFollowRot;
        private float _pendingFollowTime;
        private bool _pendingHandcuff;
        private int _pendingHandcuffMode;
        private int _pendingHandcuffDuration;
        private float _pendingHandcuffAt;
        private float _pendingHandcuffDeadline;

        // ========== 重连 ==========
        private bool _clientAutoReconnect;
        private int _reconnectAttempts;
        private float _reconnectAt;
        private string _reconnectAddress;

        // ========== 模拟玩家 ==========
        private bool _simEnabled;
        private const string SimPeerId = "sim";
        private float _lastSimUpdateTime;
        private int _lastSimAvatarId = int.MinValue;
        private string _hostPeerId = "host";

        // ========== 骨骼同步 ==========
        private List<string> _sendBonePaths = new List<string>();
        private List<Transform> _sendBoneTransforms = new List<Transform>();
        private int _boneListAvatarId = int.MinValue;
        private int _sourceCoreBoneCount = -1;
        private float _lastPathsCollectTime = -1f;
        private List<string> _cachedActivePaths = new List<string>();

        // ========== 服务器相关 ==========
        private string _serverAddress = "";
        private string _serverName = "";
        private string _serverPortText = "80";
        private bool _isServerConnected = false;
        private List<ServerRoomInfo> _serverRooms = new List<ServerRoomInfo>();
        private string _serverRoomListStatus = "";
        private string _serverCaptchaInput = "";
        private string _serverCaptchaDisplay = "";
        private Texture2D _serverCaptchaTex = null;
        private string _serverCaptchaImageBase64 = "";
        private bool _serverCaptchaVerified = false;
    private bool _authNeedCaptcha = false;
        private string _serverAdminPassword = "";
        private string _serverAdminUser = "";
        private string _serverAdminIpInput = "";
        private string _serverAdminAnnouncement = "";
        private string _serverAdminRoomIdInput = "";
        private string _serverAdminRoomMsgInput = "";
        private List<ServerChatMessage> _serverAdminChat = new List<ServerChatMessage>();
        private string _serverAdminChatStatus = "";
        private bool _showServerSettings = false;
        private bool _showChatMenu = false;
        private string _chatTab = "room";
        private bool _announceExpanded = false;
        private bool _masterAnnExpand = false;
        private Vector2 _masterAnnScroll = Vector2.zero;
        private Vector2 _announceScroll = Vector2.zero;
        private string _announceCachedText = "";
        private float _announceContentHeight = 150f;
        private ServerSettingsInfo _serverSettings = new ServerSettingsInfo();
        private string _cfgMaxRoomsTotal = "100";
        private string _cfgMaxRoomsPerIp = "1";
        private string _cfgMaxRoomsPerHour = "5";
        private string _cfgRoomLifetime = "43200";
        private string _cfgRoomTimeout = "60";
        private string _cfgMaxPlayers = "8";
        private string _cfgChatLogDays = "1";
        private string _cfgActionLogDays = "2";
        private string _cfgCaptchaExpire = "3600";
        private bool _serverIsAdmin = false;
        private float _menuScrollY = 0f;
        private float _menuContentHeight = 0f;
        private int _menuErrors = 0;
        private bool _menuDisabled = false;
        private UnityEngine.EventSystems.EventSystem _uiEventSystem;
        private bool _uiEventSystemWasEnabled = false;
        private bool _cursorCaptured, _cursorWasVisible;
        private CursorLockMode _cursorWasLocked = CursorLockMode.None;
        private float _backgroundReadyAt = 8f;
        private bool _lanListenerStarted;
        private int _lastCharFrame = -1;
        private string _lastTextField = "";
        private Dictionary<string, int> _fieldLastFrame = new Dictionary<string, int>();
        private string _lastCharField = "";
        private string _lastCharText = "";
        private float _lastCharTime = -1f;
        private int _lastBackspaceFrame = -1;
        private float _lastBackspaceTime = -1f;
        private int _lastEnterFrame = -1;
        private string _serverCreateRoomName = "";
        private string _serverCreateRoomPassword = "";
        private string _serverPublicAddress = "";
        private string _serverCreateMaxPlayersText = "8";
        private string _serverMyRoomPassword = "";
        private string _serverJoinPasswordInput = "";
        private string _serverRoomSearch = "";
        private string _serverDirectJoinId = "";
        private string _serverDirectJoinPwd = "";
        private string _serverJoinServerPwd = "";
        private string _joinPwdPromptRoom = "";
        private string _joinPwdPromptInput = "";
        private string _selectedServerRoomId = "";
        private string _serverAnnouncement = "";
        private float _lastServerRefreshTime = 0;
        private bool _isRefreshingServer = false;
        private string _serverChatInput = "";
        private string _lastRelayChatLine = "";
        private List<string> _serverChatMessages = new List<string>();
        private float _serverHeartbeatTime = 0;
        private float _lastPresenceTime = 0;
        private string _serverMyRoomId = "";
        private string _serverMyRoomToken = "";
        private string _serverJoinPlayerId = "";
        private bool _serverIsHosting = false;

        // ========== Mod 总服 ==========
        private bool _masterConnected = false;
        private bool _masterBusy = false;
        private bool _masterDataPending = true;
        private string _masterAnnTitle = "";
    private string _masterAnnCachedText = "";
    private float _masterAnnCachedWidth = -1f;
    private float _masterAnnCachedHeight = 0f;
        private string _masterAnnContent = "";
        private List<MasterServerInfo> _masterServers = new List<MasterServerInfo>();
        private int _masterPage = 1;
        private int _manualServerRefreshCount = 0;
        private float _lastManualServerRefreshAt = -999f;
        private int _masterTotalPages = 1;
        private int _masterOnline = 0;
        private string _masterCustomAddr = "";
        private string _relayToken = "";
        private bool _relayConnected = false;
        private bool _relayConnecting = false;
        private bool _relayConnectFlowBusy = false;
        private float _relayConnectStartedAt = -999f;
        private List<string> _relayChat = new List<string>();
        private string _relayChatInput = "";
        private string _relayRoomInput = "";
        private List<Dictionary<string, object>> _relayRooms = new List<Dictionary<string, object>>();
        private string _relayRoomPassword = "";
    private string _joinPwdRoomId = "";
    private string _joinPwdInput = "";
    private string _toyInviteFrom = "";
    private string _toyInviteFromName = "";
    private string _toyLinkedTarget = "";
    private string _toyLinkedController = "";
    private readonly List<string> _toyLinkedTargets = new List<string>();
    private int _toyVibrateStage = 0;
    private bool _toyAdvancedExpanded = true;
    private bool _toyActionExpanded = false;
    private Vector2 _toyActionScroll = Vector2.zero;
    private string _toyActionCustom = "";
    private int _undressDegree = 0;
    private bool _handcuffExpanded = false;
    private float _lastToyErrorLogAt = -999f;
    private float _lastRelayRoomListAt = -999f;
    private bool _toyCollar = false;
    private bool _toyNeverBreak = false;
    private float _leashOverSince = 0f;
    private bool _fingerActive = false;
    private float _fingerUntil = 0f;
    private Vector3 _fingerTarget = Vector3.zero;
    private string _fingerTargetUid = "";
    private bool _fingerTwo = false;
    private bool _fingerInfinite = false;
    private string _fingerHand = "L";
    private float _fingerSideRandomUntil = -999f;
    private float _fingerLastPleasureAt = -999f;
    private float _fingerOscT = 0f;
    private bool _beingFingered = false;
    private float _beingFingeredUntil = 0f;
    private float _fingerMoodAt = 0f;
    private float _lastFingerNavAt = -999f;
    private float _lastFingerCancelToastAt = -999f;
    private Vector3 _selfPussyLocalOffset = new Vector3(0f, -0.35f, 0f);
    private Vector3 _selfAnalLocalOffset = new Vector3(0f, -0.35f, 0.05f);
    private bool _bodyOffsetsComputed = false;
    private bool _boneInventoryDumped = false;
    private LineRenderer _leashLine = null;
    private string _rideTarget = "";
    private string _pendingRideTarget = "";
    private string _rideMode = "follow";
    private float _rideLastSend = -999f;
    private LineRenderer _rideRing = null;
    private bool _isRidden = false;
    private bool _ridePoseCaptured = false;
    private Quaternion _rideLegL, _rideLegR, _rideLowerLegL, _rideLowerLegR;
    private static readonly string[] _vibStages = { "关闭", "轻微", "中", "重" };
    private bool _forceClimax = false;
    private bool _forceCrouch = false;
    private bool _forceFollow = false;
    private bool _showMap = false;
    private int _viewStage = -1;
    private bool _mapHasStart = false;
    private Vector3 _mapStartPos = Vector3.zero;
    private int _mapStartStage = -1;
    private string _followTargetUid = "";
    private Vector3 _followTargetPos = Vector3.zero;
    private float _lastFollowSend = -999f;
    private float _lastPosSend = -999f;
    private float _lastNpcSync = -999f;
    private float _lastTimeSyncAt = -999f;
    private string _lastNpcSig = "";
    private float _lastStateSync = -999f;
    private int _stateSyncCount = 0;
    private float _lastRelayKeyframe = -999f;
    private float _lastMotionSend = -999f;
    private float _lastRelayMotionSend = -999f;
    private float _lastMotionSampleAt = -999f;
    private Vector3 _lastMotionSamplePos = Vector3.zero;
    private Vector3 _cachedMotionVelocity = Vector3.zero;
    private float _cachedMotionRotY = 0f;
    private int _cachedMotionAction = -1;
    private int _cachedMotionHash = 0;
    private bool _cachedMotionMoving = false;
    private bool _cachedMotionCrouch = false;
    private bool _cachedMotionStrafe = false;
    private bool _cachedMotionDash = false;
    private bool _cachedMotionEcstasy = false;
    private float _cachedGroundOffset = 0f;
    private float _cachedAnimatorDeltaY = 0f;
    private float _lastGroundCalcAt = -999f;
    private Vector3 _cachedHipsLocal = Vector3.zero;
    private Quaternion _cachedHipsLocalRot = Quaternion.identity;
    private readonly Dictionary<string, Transform> _localBoneCache = new Dictionary<string, Transform>();
    private readonly Dictionary<string, float> _relayGroundY = new Dictionary<string, float>();
    private float _cachedAnimatorMoveSpeed = 0f;
    private float _cachedAnimatorLocomotionSpeed = 1f;
    private float _cachedAnimatorStrafeX = 0f;
    private float _cachedAnimatorStrafeY = 0f;
    private int _cachedAnimatorActionId = -1;
    private int _cachedAnimatorAction = -1;
    private int _cachedAnimatorOldActionId = -1;
    private float _cachedAnimatorAnotherMotion = 0f;
    private int _cachedMotionFrame = -1;
    private int[] _cachedMotionLayerHashes = new int[0];
    private float[] _cachedMotionLayerTimes = new float[0];
    private float[] _cachedMotionLayerWeights = new float[0];
    private int[] _lastRelaySentLayerHashes = new int[0];
    private int _lastRelaySentHash = int.MinValue;
    private bool _lastRelaySentMoving;
    private bool _lastRelaySentCrouch;
    private bool _lastRelaySentStrafe;
    private bool _lastRelaySentDash;
    private int _lastRelaySentActionId = int.MinValue;
    private int _lastRelaySentActionParam = int.MinValue;
    private int _lastRelaySentOldActionId = int.MinValue;
    private float _lastRelaySentAnotherMotion = float.NaN;
    private bool _relayMotionStateSent;
    private float _lastMotionAnimSampleAt = -999f;
    private float _lastRelayMotionPosAt = -999f;
    private float _lastRelayActionSyncAt = -999f;
    private float _lastRelayBoneAt = -999f;
    private List<float> _lastRelayBoneQ;
    private float _lastRelayBoneChangedAt = -999f;
    private readonly Dictionary<int, List<Vector3>> _syncNpcs = new Dictionary<int, List<Vector3>>();
    private readonly Dictionary<int, Vector3> _syncNpcTargets = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, Vector3> _syncNpcVelocity = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, float> _syncNpcRotY = new Dictionary<int, float>();
    private readonly Dictionary<int, bool> _syncNpcMoving = new Dictionary<int, bool>();
    private readonly Dictionary<int, int> _syncNpcActionHash = new Dictionary<int, int>();
    private readonly Dictionary<int, int> _npcLastAppliedHash = new Dictionary<int, int>();
    private readonly Dictionary<int, Vector3> _npcLastSentPos = new Dictionary<int, Vector3>();
    private float _npcLastSampleAt = -999f;
    private string _syncNpcAuthority = "";
    private int _syncNpcStage = -1;
    private readonly Dictionary<string, RelayPos> _relayPositions = new Dictionary<string, RelayPos>();
    private bool _roomAllowRide = false; // 兼容旧方法；界面与服务器均已关闭
    private bool _roomAllowGameBonuses = false;
    private string _roomSection = "relay";
    private readonly Dictionary<SkillType, bool> _roomSkillSnapshot = new Dictionary<SkillType, bool>();
    private bool _roomSkillsSuppressed = false;
    private float _lastRoomSkillPolicy = -999f;
    private readonly Dictionary<string, GhostPlayer> _relayGhosts = new Dictionary<string, GhostPlayer>();
    private readonly Dictionary<string, float> _relayGhostLastSeen = new Dictionary<string, float>();
    private readonly Dictionary<string, float> _relayGhostIgnoreUntil = new Dictionary<string, float>();
    private float _mapCacheAt = -999f;
    private float _lastGhostUpdate = -999f;
    private float _lastGhostVisLogAt = -999f;
    private Vector3 _lastNavDest = Vector3.zero;
    private bool _hasNavDest = false;
    private readonly List<Vector3> _mapNpcPts = new List<Vector3>();
    private readonly List<Vector3> _mapPortalPts = new List<Vector3>();
    private readonly List<Vector3> _mapRoutePts = new List<Vector3>();
    private readonly List<Vector3> _mapDoorPts = new List<Vector3>();
    private readonly List<Vector3> _mapObstaclePts = new List<Vector3>();
    private readonly List<Vector3> _mapObstacleSizes = new List<Vector3>();
    private readonly List<Vector3> _mapWallCenters = new List<Vector3>();
    private readonly List<Vector3> _mapWallSizes = new List<Vector3>();
    private readonly List<Vector3> _mapFitPts = new List<Vector3>();
    private static Texture2D _mapCircleTex;
    private static Texture2D _mapRingTex;
    private static Texture2D _mapTriangleTex;
    private string _gameMode = "";
    private string _gamePhase = "idle";
    private string _gameRole = "";
    private int _gameLives = 0;
    private bool _gameBlindfold = false;
    private float _gameSpeed = 1f;
    private bool _gameCaughtRed = false;
    private float _gameRedUntil = 0f;
    private float _gameStopUntil = 0f;
    private float _gameSlowUntil = 0f;
    private float _gameBoostUntil = 0f;
    private float _gameLastFKey = 0f;
    private int _gameCaughtCount = 0;
    private int _gameCatchTarget = 0;
    private int _gameEscapedCount = 0;
    private string _gameWinner = "";
    private string _gameMvp = "";
    private Color _redOrigColor = Color.white;
    private bool _redOrigColorSaved = false;
    private readonly List<string> _gameLog = new List<string>();
    private readonly List<string> _gameCatchers = new List<string>();
    private readonly HashSet<string> _gameRedPlayers = new HashSet<string>();
    private float _lcBareyasusa = 50f;
    private float _lcMaxBareyasusa = 1000f;
    private bool _lcAttrOff = true;
    private bool _lcReduceSet = false;
    private bool _gameFoundSent = false;
    private float _lcLastApply = -999f;
    private int _gameLcPoint = -1;
    private float _gameLcSeconds = 0f;
    private float _gameLcStartAt = 0f;
    private string _gameNotice = "";
    private float _gameNoticeUntil = 0f;
    private float _gameHeight = 1f;
    private GUIStyle _noticeStyle = null;
    private GUIStyle _hintStyle = null;
    private GUIStyle _hudStyle = null;
        private string _relayCaptchaInput = "";
        private Texture2D _relayCaptchaTex = null;
        private Vector2 _relayChatScroll = Vector2.zero;
        private string _relayHostUid = "";
        private List<Dictionary<string, object>> _relayPlayers = new List<Dictionary<string, object>>();
        private string _relayServerName = "";
        private string _relayServerHost = "";
        // ===== 房间模组同步（v1.0.10） =====
        private List<Dictionary<string, object>> _hostModList = null;   // 房主模组清单
        private List<string> _modNeedList = new List<string>();          // 需要下载的文件
        private int _modDownloadIndex = 0;
        private string _modDownloadError = "";
        private bool _modPromptOpen = false;
        private bool _modDownloading = false;
        private bool _modPromptModeInstall = true;   // true=安装提示 false=下载中
        private float _modReloadAfter = -1f;         // 下载完成后延迟重进游戏
        private float _modDownloadStartedAt = -1f;   // 下载开始时间（超时保护）
        // ===== 掉落道具同步（v1.0.10） =====
        private class RemoteDrop
        {
            public string Type;      // DropItemType 名
            public string Owner;     // 归属玩家 uid
            public string OwnerName; // 归属玩家名
            public Vector3 Pos;
            public float CreatedAt;
            public UnityEngine.GameObject Marker;
        }
        private readonly Dictionary<string, RemoteDrop> _remoteDrops = new Dictionary<string, RemoteDrop>();
        private string _dropSig = "";
        private float _lastDropScanAt = -999f;
        private float _lastDropBroadcastAt = -999f;
        private bool _dropAllowOthers = true;              // 是否允许他人拾取
        private readonly HashSet<string> _dropAllowUids = new HashSet<string>();  // 指定允许的玩家（空=全部）
        private float _lastDropPermBroadcastAt = -999f;
        private List<Dictionary<string, object>> _relayServerMods = new List<Dictionary<string, object>>();
        private string _relayAnnounceTitle = "";
        private string _relayAnnounceContent = "";
        private int _relayOnline = 0;
        private int _relayMaxOnline = 0;
        private int _masterRelayOnline = 0;
        private int _masterRelayMaxOnline = 0;
        private float _lastRelayHttpStats = 0f;
        private float _lastRelayStats = 0f;
        private string _masterLatestVersion = "";
        private string _masterLatestUrl = "";
        private string _masterLatestNote = "";
        private bool _masterUpdateReady = false;
        private bool _masterUpdateDownloaded = false;
        private bool _masterUpdateDownloading = false;
        private bool _masterClientTampered = false;
        private bool _modDisabled = false;
        private bool _masterReplaceDone = false;
        private bool _masterPendingRestart = false;
        private bool _masterForceUpdate = false;
        private float _lastMasterReport = 0;
        private float _lastMasterAttempt = -999f;

        // ========== 账号登录门 ==========
        private bool _loggedIn = false;
        private bool _authBusy = false;
        private string _authMode = "login";
        private bool _authUseCode = false;
        private string _authAccount = "";
        private string _authPass = "";
        private string _authPass2 = "";
        private string _authEmail = "";
        private string _authCode = "";
        private bool _authAgree = false;
        private string _authMsg = "";
        private float _codeSentAt = -999f;
        private bool _codeActionTaken = false;
        private string _authSid = "";
        private string _authCaptchaImage = "";
        private string _authCaptcha = "";
        private string _authToken = "";
        private string _authServerVersion = "";
        private bool _authServerOld = false;
        private bool _authAgreementMissing = false;
        private float _lastAuthServerCheck = -999f;
        private long _authUid = 0;
        private string _authUsername = "";
        private string _authSavedPass = "";
        private bool _reloginBusy = false;
        private string _authEmailBound = "";
        private Texture2D _authCaptchaTex = null;
        private int _authOnline = 0;
        private int _authRegistered = 0;
        private string _authTitle = "";
    private string _authTitleColor = "";
        private bool _authIsAdmin = false;
        private int _authAdminLevel = 0;
        private List<string> _authAdminActions = new List<string>();
        private bool _captchaBig = false;
        private string _bioInput = "";
        private long _dmPeerUid = 0;
        private string _dmInput = "";
        private List<string> _dmList = new List<string>();
        private long _dmLastId = 0;
        private bool _dmLoading = false;
        private float _lastDmRefresh = -999f;
        private float _lastFriendRefresh = -999f;
        private long _profileUid = 0;
        private string _profileInfo = "";
        private string _reportReason = "";
        private long _adminSearchUid = 0;
        private string _adminRenameInput = "";
        private string _adminTitleInput = "";
    private float _adminTitleR = 1f;
    private float _adminTitleG = 1f;
    private float _adminTitleB = 1f;
        private string _adminWordInput = "";
        private string _adminSearchName = "";
        private string _adminNewUid = "";
        private bool _adminUsersLoaded = false;
        private List<Dictionary<string, object>> _adminUsers = new List<Dictionary<string, object>>();
        private List<Dictionary<string, object>> _adminReports = new List<Dictionary<string, object>>();
        private bool _adminReportsLoaded = false;
        private List<Dictionary<string, object>> _adminPubchat = new List<Dictionary<string, object>>();
        private bool _adminPubchatLoaded = false;
        private string _adminDmView = "";
        private List<Dictionary<string, object>> _friendList = new List<Dictionary<string, object>>();
        private List<Dictionary<string, object>> _friendRequests = new List<Dictionary<string, object>>();
        private bool _friendHideServer = false;
        private bool _friendAllowSearch = true;
        // ========== 协议（云端更新，强制重新同意） ==========
        private string _agUserV = "";
        private string _agPrivacyV = "";
        private string _agUserUrl = "https://wuwupuo1.xtxt.xyz/public_html/sfm_api/agreement.php?t=user";
        private string _agPrivacyUrl = "https://wuwupuo1.xtxt.xyz/public_html/sfm_api/agreement.php?t=privacy";
        private string _agUserTitle = "";
        private string _agPrivacyTitle = "";
        private string _agNonce = "";
        private bool _agLoaded = false;
        private bool _agAcceptUser = false;
        private bool _agAcceptPrivacy = false;
        private string _agLocalUserV = "";
        private string _agLocalPrivacyV = "";
        private string _agNotice = "";
        private float _lastAgreementFetch = -999f;
        private string _renameInput = "";
        private string _menuTab = "online";
        private float _tabScroll = 0f;
        private string _pubInput = "";
        private List<Dictionary<string, object>> _pubMsgs = new List<Dictionary<string, object>>();
        private long _pubAfter = 0;
        private string _friendKw = "";
        private List<Dictionary<string, object>> _friendResults = new List<Dictionary<string, object>>();
        private List<Dictionary<string, object>> _credits = new List<Dictionary<string, object>>();
        private string _modsPromptHost = "";
        private int _modsPromptPort = 0;
        private string _modsPromptList = "";
        private float _lastPubPoll = 0;
        private bool _relayModeEnabled = false;
        private bool _relayMode = false;
        private string _relayPlayerId = "";
        private string _relayRoomId = "";
        private long _relayLastId = 0;
        private float _relayLastSendTime = 0;
        private float _relayLastPollTime = 0;
        private readonly Queue<(byte type, byte[] payload)> _relayOutQueue = new Queue<(byte, byte[])>();
        // ========== UI状态 ==========
        private float _uiX, _uiY, _uiW;
        private string _syncRateText = "10";
        private bool _syncActions;
        private bool _autoFollow;
        private bool _showHud;
        private bool _ghostDebug;
        private GUIStyle _tagStyle;
        private GUIStyle _announceStyle;
        private string _languageButtonText = "中文/English";

        // ========== UI探测 ==========
        private bool _uiProbed;
        private bool _canLabel = true;
        private bool _canTextField = true;
        private int _manualInputFrame = -1;
        private GUIStyle _inputTextStyle;
        private IntPtr _nativeEdit = IntPtr.Zero;
        private IntPtr _nativeEditParent = IntPtr.Zero;
        private IntPtr _nativeEditFont = IntPtr.Zero;
        private string _nativeEditField = "";
        private string _nativeEditOriginal = "";
        private bool _nativeEditMasked;
        private bool _nativeSubmitPending;
        private bool _nativeCancelPending;
        private bool _nativeEnterDown;
        private bool _nativeEscapeDown;
        private bool _nativeFocusLostPending;
        private int _nativeEditSeenFrame = -1;
        private NativeWndProc _nativeWndProc;
        private IntPtr _nativeOldWndProc = IntPtr.Zero;
        private bool _runInBackgroundOverridden;
        private bool _runInBackgroundWas;
        private bool _canPassword = true;
        private bool _canButton = true;
        private bool _canToggle = true;
        private bool _canControlName = true;
        private bool _canClipboard = true;
        private bool _canEventChar;
        private bool _canEventKey;
        private bool _canEventMouse;
        private bool _canInput;
        private bool _startupAuthPending;
        private bool _lanIpResolvePending;
        private bool _agreementLocalPending;
        private bool _languageInitialized;
        private float _uiReadyAt;

        // ========== 属性 ==========
        [HideFromIl2Cpp]
        private bool InGame => PlayerFacade.Instance != null && PlayerFacade.Instance.pca != null && PlayerFacade.Instance.pca.AvatorTransform != null;
        [HideFromIl2Cpp]
        private bool SceneSyncReady => InGame && Time.unscaledTime >= _sceneSyncBlockedUntil;
        [HideFromIl2Cpp]
        private bool IsHosting => _host != null && _host.Running;
        [HideFromIl2Cpp]
        private bool IsClient => _client != null && _client.Connected;
    [HideFromIl2Cpp]
    public string PeerId => _relayMode ? _relayPlayerId : (IsHosting ? "host" : (_client != null ? _client.PeerId : ""));
        [HideFromIl2Cpp]
        private bool Connected => IsHosting || IsClient;
        // ========== Unity生命周期 ==========

internal void Awake()
{
    Instance = this;
    SFMOnline.Ext.OnlineCoreExt.GetGhostUids = () => GetGhostUids();
    InstallExtBridge();
    _nickname = Settings.Nickname.Value;
    _portText = Settings.Port.Value.ToString();
    _maxPlayersText = Settings.MaxPlayers.Value.ToString();
    _passwordText = Settings.Password.Value;
    _addressText = Settings.HostAddress.Value;
    if (string.IsNullOrWhiteSpace(_addressText) || _addressText.StartsWith("127.0.0.1"))
    {
        _addressText = "";
        _lanIpResolvePending = true;
    }
    _syncRateText = Settings.SyncRateHz.Value.ToString();
    _syncActions = Settings.SyncActions.Value;
    _autoFollow = Settings.AutoFollowHost.Value;
    _showHud = Settings.ShowHud.Value;
    _startupAuthPending = Settings.AutoLogin.Value;
    _agreementLocalPending = true;
    _uiReadyAt = Time.realtimeSinceStartup + 2.5f;
    
    // 语言表和本地协议延迟加载，避免在游戏启动主线程一次性构造大量字典、网络与加密对象。
    
    // 加载服务器设置
    _serverAddress = Settings.ServerAddress.Value;
    if (string.Equals(_serverAddress.Trim(), "wuwupuo.ccwu.cc", StringComparison.OrdinalIgnoreCase))
    {
        _serverAddress = "wuwupuo.cc.cd";
        Settings.ServerAddress.Value = _serverAddress;
    }
    _serverPortText = Settings.ServerPort.Value.ToString();
    
    try { Animator.StringToHash("MoveSpeed"); } catch { }
    _backgroundReadyAt = Time.realtimeSinceStartup + 8f;
}
[HideFromIl2Cpp]
private void EnsureLanDiscovery(){if(_lanListenerStarted)return;LanDiscovery.StartListener();_lanListenerStarted=true;}

internal void OnDestroy()
{
    CloseNativeEditor();
    try
    {
        if (_nativeEditFont != IntPtr.Zero) DeleteObject(_nativeEditFont);
        _nativeEditFont = IntPtr.Zero;
    }
    catch { }
    _host?.Stop();
    _client?.Disconnect();
    LanDiscovery.StopAll();
    RemoveAllGhosts();
    if (_serverIsHosting) LeaveServerRoom();
}

internal void Update()
{
    // F10 在 Update 里检测（不依赖 OnGUI 事件流），确保联机菜单一定响应
    try
    {
        if (_canInput && Input.GetKeyDown(KeyCode.F10))
        {
            if (!_loggedIn)
            {
                _onlineMenuOnly = false;
                _showMenu = true;
                _focusedField = "";
                _menuScrollY = 0f;
                Toast("请先登录；登录后按 F10 可连接服务器、创建或加入房间");
            }
            else
            {
                bool closeOnlineMenu = _showMenu && _onlineMenuOnly;
                _onlineMenuOnly = true;
                _showMenu = !closeOnlineMenu;
                if (_showMenu) _menuTab = "room";
                _focusedField = "";
                if (!_showMenu) _menuScrollY = 0f;
            }
        }
    }
    catch { }
    
    // 模组同步：下载完成延迟后自动重进游戏（退出当前世界回主菜单）
    try
    {
        if (_modReloadAfter > 0f && Time.unscaledTime >= _modReloadAfter)
        {
            _modReloadAfter = -1f;
            Toast("模组已更新，正在重进游戏...");
            ReturnToTitle();
        }
        if (_modDownloading && Time.unscaledTime - _modDownloadStartedAt > 30f)
        {
            _modDownloading = false;
            _modDownloadError = "下载超时";
            AddRelayLine("[模组] 下载超时");
        }
    }
    catch { }
    // 掉落道具同步（v1.0.10）
    try
    {
        UpdateDropSync();
        if (_canInput && Input.GetKeyDown(KeyCode.F1))
        {
            CollectMyDrops();
        }
    }
    catch { }
SafeUpdate("native_input", UpdateNativeEditor);
    SafeUpdate("scene_guard", UpdateSceneTransitionGuard);
    _drain.Clear();
    int directPacketsLeft = 96;
    int directPacketsHandled = 0;
    long directPollStarted = Stopwatch.GetTimestamp();
    if (_host != null)
    {
        while (directPacketsLeft > 0 && _host.TryDequeue(out var m))
        {
            directPacketsLeft--;
            directPacketsHandled++;
            if (m.SourceId != "server")
                _host.SendToClients(m.Type, m.Payload, m.SourceId);
            _drain.Enqueue(m);
            if (directPacketsHandled >= 8 &&
                (Stopwatch.GetTimestamp() - directPollStarted) / (double)Stopwatch.Frequency >= 0.0025) break;
        }
    }
    bool directBudgetAvailable = directPacketsHandled < 8 ||
        (Stopwatch.GetTimestamp() - directPollStarted) / (double)Stopwatch.Frequency < 0.0025;
    if (_client != null && directBudgetAvailable)
    {
        while (directPacketsLeft > 0 && _client.TryDequeue(out var m))
        {
            directPacketsLeft--;
            directPacketsHandled++;
            _drain.Enqueue(m);
            if (directPacketsHandled >= 8 &&
                (Stopwatch.GetTimestamp() - directPollStarted) / (double)Stopwatch.Frequency >= 0.0025) break;
        }
    }
    while (_drain.Count > 0) HandleRemote(_drain.Dequeue());
    int mainActionsLeft = 24;
    while (mainActionsLeft-- > 0 && _mainQueue.TryDequeue(out var act))
    {
        try { act(); }
        catch (Exception ex) { PluginInfo.Warn("main queue: " + ex); }
    }
    SafeUpdate("ext_tick", SFMOnline.Ext.OnlineCoreExt.TickUpdate);

    bool directSession = Connected;
    bool relaySession = _relayConnected && _relayRoomId.Length > 0;
    if (directSession)
    {
        SafeUpdate("ping", UpdatePing);
        SafeUpdate("state", UpdateStateSend);
        SafeUpdate("events", CheckEvents);
        SafeUpdate("follow", UpdatePendingFollow);
        SafeUpdate("pending_handcuff", UpdatePendingHandcuff);
    }
    if (IsHosting) SafeUpdate("lan_advertise", UpdateLanAdvertisement);
    if (_clientAutoReconnect) SafeUpdate("reconnect", UpdateAutoReconnect);
    if (_simEnabled) SafeUpdate("sim", UpdateSimPlayer);
    if (_isServerConnected)
    {
        SafeUpdate("server_heartbeat", UpdateServerHeartbeat);
        SafeUpdate("presence", UpdateServerPresence);
    }

    float startupPhase = Time.realtimeSinceStartup - _backgroundReadyAt;
    if (_lanIpResolvePending && startupPhase >= 0f)
    {
        _lanIpResolvePending = false;
        // 网卡枚举放到后台，避免机械硬盘/异常网卡驱动拖住游戏主线程。
        Task.Run(() => GetLanIp()).ContinueWith(t =>
        {
            if (t.Status == TaskStatus.RanToCompletion)
            {
                string lanIp = t.Result;
                _mainQueue.Enqueue(() => { if (lanIp != "未知") _addressText = lanIp + ":" + Settings.Port.Value; });
            }
        });
    }
    if (_agreementLocalPending && startupPhase >= 0.5f)
    {
        _agreementLocalPending = false;
        LoadAgreementLocal();
    }
    if (_startupAuthPending && startupPhase >= 1f)
    {
        _startupAuthPending = false;
        LoadSavedToken();
    }
    bool servicesReady = _showMenu || _showChatMenu || _loggedIn || _relayConnected;
    if (!_lanListenerStarted && (_showMenu || _showChatMenu))
        SafeUpdate("lan_listener", EnsureLanDiscovery);
    if(servicesReady){SafeUpdate("master_report",UpdateMasterReport);SafeUpdate("master_auto",UpdateMasterAuto);SafeUpdate("agreement",UpdateAgreement);SafeUpdate("authsrv",UpdateAuthServerCheck);SafeUpdate("pubpoll",UpdatePubPoll);}
    if(servicesReady||_relayConnected||_relayConnecting)SafeUpdate("relaypoll",UpdateRelayPoll);
    if (relaySession && SceneSyncReady)
    {
        SafeUpdate("time_sync", SendTimeSync);
        SafeUpdate("npc_sync",SendNpcSync);
        SafeUpdate("bone_send",SendRelayBones);
        SafeUpdate("appearance_handshake",UpdateRelayAppearanceHandshake);
        SafeUpdate("motion_sync", SendRelayMotion);
        SafeUpdate("state_sync", SendRelayState);
        SafeUpdate("room_skill_policy", UpdateRoomSkillPolicy);
        SafeUpdate("game_effect", UpdateGameEffect);
        SafeUpdate("relay_ghosts", UpdateRelayGhosts);
        if (_relayConnected && _relayRoomId.Length == 0 && Time.unscaledTime - _lastRelayRoomListAt > 45f)
        {
            _lastRelayRoomListAt = Time.unscaledTime;
            RelayTcp.Send(new Dictionary<string, object> { ["t"] = "room_list" });
        }
        SafeUpdate("lc_mode", UpdateLcMode);
    }
    if (_ghostDebug && !_boneInventoryDumped && SceneSyncReady && (Connected || _relayConnected) && startupPhase >= 5f)
    {
        _boneInventoryDumped = true;
        EnsureBodyOffsets();
        SafeUpdate("bones", DumpBoneInventory);
    }
    UpdateEventSystemBlock();

    // 总服数据只在实际打开菜单时才拉取，避免游戏启动/后台空转时浪费性能与流量
    if (_showMenu && _masterConnected && _masterDataPending)
    {
        _masterDataPending = false;
        RefreshMasterData();
    }

    // 菜单/聊天/在房间时 15 秒刷新一次；否则 30 秒一次，降低卡顿和流量
    bool needFresh = _showMenu || _showChatMenu || !string.IsNullOrEmpty(_serverMyRoomId);
    if (_isServerConnected && string.IsNullOrEmpty(_serverMyRoomId) && !_isRefreshingServer &&
        Time.unscaledTime - _lastServerRefreshTime > (needFresh ? 20f : 45f))
    {
        _lastServerRefreshTime = Time.unscaledTime;
        RefreshServerRoomList();
    }
}

[HideFromIl2Cpp]
private void UpdateServerPresence()
{
    if (!_isServerConnected || string.IsNullOrEmpty(_serverMyRoomId) || string.IsNullOrEmpty(_serverJoinPlayerId)) return;
    if (Time.unscaledTime - _lastPresenceTime < 30f) return;
    _lastPresenceTime = Time.unscaledTime;
    var roomId = _serverMyRoomId;
    var playerId = _serverJoinPlayerId;
    RunServer(() => ServerAPI.Presence(roomId, playerId), ok => { }, err => { });
}

[HideFromIl2Cpp]
private void UpdateSceneTransitionGuard()
{
    try
    {
        bool inGame = InGame;
        int stage = inGame ? CurrentStageInt() : -1;
        if (!_sceneGuardInitialized)
        {
            _sceneGuardInitialized = true;
            _sceneWasInGame = inGame;
            _sceneObservedStage = stage;
            if (inGame) _sceneSyncBlockedUntil = Time.unscaledTime + 1.5f;
            return;
        }

        bool changed = inGame != _sceneWasInGame ||
            (inGame && _sceneWasInGame && stage != _sceneObservedStage);
        if (changed)
        {
            _sceneSyncBlockedUntil = Time.unscaledTime + (inGame ? 2.5f : 4f);
            ClearSceneSyncReferences();
            ClientLog.Write("地图切换保护: " + _sceneObservedStage + " -> " + stage +
                "，暂停分身/动画同步");
        }
        _sceneWasInGame = inGame;
        _sceneObservedStage = stage;
    }
    catch
    {
        _sceneSyncBlockedUntil = Time.unscaledTime + 3f;
    }
}

[HideFromIl2Cpp]
private void ClearSceneSyncReferences()
{
    // 场景卸载期间不主动遍历或 Destroy 旧模型，避免触碰已经被 Unity 原生层释放的对象。
    _ghosts.Clear();
    _relayGhosts.Clear();
    _lastStates.Clear();
    _relayPositions.Clear();
    _relayActionHints.Clear();
    _ghostCreateTimes.Clear();
    _ghostWarned.Clear();
    _ghostToasted.Clear();
    _ghostRoot = null;
    _syncNpcs.Clear();
    _syncNpcTargets.Clear();
    _syncNpcVelocity.Clear();
    _syncNpcRotY.Clear();
    _syncNpcMoving.Clear();
    _syncNpcActionHash.Clear();
    _npcLastAppliedHash.Clear();
    _npcLastSentPos.Clear();
    _leashLine = null;
    _rideRing = null;
    _lastLocalAvatarId = int.MinValue;
    _lastMotionSampleAt = -999f;
    _cachedMotionFrame = -1;
    _stateSyncCount = 0;
    _lastRelayAppearanceSig = "";
    _appearanceRequestRoom = "";
    _bodyOffsetsComputed = false;
    CloseNativeEditor();
}

[HideFromIl2Cpp]
private void UpdateEventSystemBlock()
{
    try
    {
        bool anyOpen = _showMenu || _showChatMenu;
        if(anyOpen)
        {
            if (!string.IsNullOrEmpty(_focusedField))
            {
                try { Input.ResetInputAxes(); Input.imeCompositionMode = UnityEngine.IMECompositionMode.On; } catch { }
            }
            if(!_cursorCaptured){_cursorWasVisible=Cursor.visible;_cursorWasLocked=Cursor.lockState;_cursorCaptured=true;}
            Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
            if (_uiEventSystem == null)
                _uiEventSystem = UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (_uiEventSystem != null && _uiEventSystem.enabled)
            {
                _uiEventSystem.enabled = false;
                _uiEventSystemWasEnabled = true;
            }
        }
        else
        {
            if(_uiEventSystem!=null&&_uiEventSystemWasEnabled){_uiEventSystem.enabled=true;_uiEventSystemWasEnabled=false;}
            if(_cursorCaptured){Cursor.visible=_cursorWasVisible;Cursor.lockState=_cursorWasLocked;_cursorCaptured=false;}
        }
    }
    catch { }
}

internal void LateUpdate()
{
    if (!SceneSyncReady) return;
    try
    {
        foreach (var g in _ghosts.Values)
            if (g != null) g.LateApply();
        foreach (var g in _relayGhosts.Values)
            if (g != null) g.LateApply();
        float ghostDt = Mathf.Clamp(Time.deltaTime, 0f, 0.1f);
        foreach (var g in _ghosts.Values)
            if (g != null) g.TickAnimator(ghostDt);
        foreach (var g in _relayGhosts.Values)
            if (g != null) g.TickAnimator(ghostDt);
    }
    catch { }
    ApplySyncedNpcs();
    try { SFMOnline.Ext.OnlineCoreExt.TickLateUpdate(); } catch { }

}

[HideFromIl2Cpp]
private void UpdateFingerReach()
{
    if (!InGame) return;
    float now = Time.unscaledTime;
    if (_beingFingered && (_beingFingeredUntil < 0f || now <= _beingFingeredUntil))
    {
        if (now - _fingerMoodAt >= 1.2f)
        {
            _fingerMoodAt = now;
            try { PlayerFacade.Instance.AddAddMoisture(8f); } catch { }
        }
    }
    if (!_fingerActive) return;
    if (_fingerUntil > 0f && now > _fingerUntil) { StopFingerLocal(); return; }
    try
    {
        var pca = PlayerFacade.Instance.pca;
        var referencer = pca != null ? pca.PlayerAvatarObjectReferencer : null;
        if (referencer == null || pca == null) return;
        var self = pca.AvatorTransform;
        Vector3 target = _fingerTarget;
        if (_fingerTargetUid.Length > 0)
        {
            GhostPlayer g = null;
            if (!_relayGhosts.TryGetValue(_fingerTargetUid, out g) || g == null || g.Root == null)
                _ghosts.TryGetValue(_fingerTargetUid, out g);
            if (g != null && g.Root != null)
            {
                try
                {
                    var pelvis = FindBoneIn(g.Root.transform, "Pelvis", "Hips", "Bip001 Pelvis");
                    if (pelvis != null) target = pelvis.position + new Vector3(0f, -0.05f, 0.03f);
                    else target = g.Root.transform.position + new Vector3(0f, -0.15f, 0.1f);
                }
                catch { }
            }
            else if (_relayPositions.TryGetValue(_fingerTargetUid, out var rp2))
                target = new Vector3(rp2.X, rp2.Y, rp2.Z) + new Vector3(0f, -0.15f, 0.08f);
        }
        _fingerTarget = target;
        Vector3 to = target - self.position;
        Vector3 flatTo = to;
        flatTo.y = 0f;
        float dist = flatTo.magnitude;
        Vector3 local = Quaternion.Inverse(self.rotation) * flatTo;
        local.y = 0f;
        float ax = local.x;
        float az = local.z;
        // 正左/正右：无法抠的角度，自动取消，避免手臂穿模
        if (Mathf.Abs(az) < 0.6f && Mathf.Abs(ax) > 0.85f)
        {
            if (now - _lastFingerCancelToastAt > 2f)
            {
                _lastFingerCancelToastAt = now;
                Toast("目标在正侧面，无法抠，已自动取消");
            }
            StopFingering();
            return;
        }
        // 太远：自动断开
        if (dist > 8f)
        {
            if (now - _lastFingerCancelToastAt > 2f)
            {
                _lastFingerCancelToastAt = now;
                Toast("距离过远，已自动取消抠");
            }
            StopFingering();
            return;
        }
        // 离得远：自动走近
        if (dist > 1.5f && now - _lastFingerNavAt >= 0.5f)
        {
            _lastFingerNavAt = now;
            StartNavFollow(target);
        }
        if (flatTo.sqrMagnitude > 0.01f)
        {
            float yaw = Mathf.Atan2(flatTo.x, flatTo.z) * Mathf.Rad2Deg;
            try { PlayerFacade.Instance.SmoothRotateY(yaw); } catch { }
        }
        string hand = ax > 0.35f ? "L" : (ax < -0.35f ? "R" : "L");
        _fingerHand = hand;
        Transform sh = hand == "R" ? referencer.ShoulderR : referencer.ShoulderL;
        Transform up = hand == "R" ? referencer.UpperArmR : referencer.UpperArmL;
        Transform lo = hand == "R" ? referencer.LowerArmR : referencer.LowerArmL;
        Transform th = hand == "R" ? referencer.ThumbR : FindLocalBone("Thumb_L");
        if (sh == null || up == null || lo == null) return;
        Vector3 sPos = sh.position;
        Vector3 sToT = target - sPos;
        float reach = sToT.magnitude;
        Vector3 sDir = reach > 0.001f ? sToT / reach : Vector3.forward;
        float l1 = Vector3.Distance(up.position, sPos);
        float l2 = Vector3.Distance(lo.position, up.position);
        if (l1 < 0.08f) l1 = 0.28f;
        if (l2 < 0.08f) l2 = 0.26f;
        _fingerOscT += Time.unscaledDeltaTime * (_fingerTwo ? 7f : 4.5f);
        float osc = Mathf.Sin(_fingerOscT) * (_fingerTwo ? 0.055f : 0.03f);
        Vector3 aimTarget = target + sDir * osc;
        Vector3 sToAim = aimTarget - sPos;
        float aimDist = sToAim.magnitude;
        Vector3 aimDir = aimDist > 0.001f ? sToAim / aimDist : sDir;
        if (aimDist >= l1 + l2 - 0.02f)
        {
            // 离得远：手臂伸直
            var straight = Quaternion.LookRotation(aimDir);
            up.rotation = Quaternion.Slerp(up.rotation, straight, 0.3f);
            lo.rotation = Quaternion.Slerp(lo.rotation, straight, 0.3f);
            sh.rotation = Quaternion.Slerp(sh.rotation, straight, 0.22f);
        }
        else
        {
            // 离得近：手臂弯折，肘窝朝上
            float cosE = Mathf.Clamp((l1 * l1 + aimDist * aimDist - l2 * l2) / (2f * l1 * aimDist), -1f, 1f);
            float eAng = Mathf.Acos(cosE);
            Vector3 upDir = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(aimDir, upDir)) > 0.9f) upDir = self.right;
            Vector3 elbowPos = sPos + aimDir * (l1 * cosE) + upDir * (l1 * Mathf.Sin(eAng) * 0.9f);
            up.rotation = Quaternion.Slerp(up.rotation, Quaternion.LookRotation(elbowPos - sPos), 0.3f);
            lo.rotation = Quaternion.Slerp(lo.rotation, Quaternion.LookRotation(aimTarget - elbowPos), 0.3f);
            sh.rotation = Quaternion.Slerp(sh.rotation, up.rotation, 0.22f);
        }
        if (th != null)
        {
            Vector3 handPos = lo.position;
            Vector3 d2 = target - handPos;
            if (d2.sqrMagnitude > 0.001f)
                th.rotation = Quaternion.Slerp(th.rotation, Quaternion.LookRotation(d2), 0.35f);
        }
        if (_fingerTargetUid.Length > 0 && now - _fingerLastPleasureAt >= 1.2f)
        {
            _fingerLastPleasureAt = now;
            if (_relayConnected && _relayRoomId.Length > 0)
                RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = _fingerTargetUid, ["d"] = "finger_pleasure" });
            else
                SendDirectControl("control", _fingerTargetUid, "finger_pleasure", 0, false);
        }
        if (_fingerInfinite) _fingerUntil = -1f;
    }
    catch { }
}
[HideFromIl2Cpp]
private void StopFingerLocal()
{
    _fingerActive = false;
    _fingerInfinite = false;
    _fingerUntil = 0f;
    _fingerTargetUid = "";
}

[HideFromIl2Cpp]
private void StartFingering(string uid, bool two)
{
    if (!InGame || uid.Length == 0) { Toast("先选中要抠的玩家"); return; }
    if (uid == ToySelfId()) return;
    _fingerTargetUid = uid;
    _fingerTwo = two;
    _fingerActive = true;
    _fingerInfinite = true;
    _fingerUntil = -1f;
    _fingerLastPleasureAt = -999f;
    EnsureBodyOffsets();
    try
    {
        var self = PlayerFacade.Instance.pca.AvatorTransform.position;
        if (_relayConnected && _relayRoomId.Length > 0)
            RelayTcp.Send(new Dictionary<string, object>
            {
                ["t"] = "toy_control", ["to"] = uid, ["d"] = "finger", ["on"] = true,
                ["stage"] = CurrentStageInt(),
                ["x"] = (float)Math.Round(self.x, 2), ["y"] = (float)Math.Round(self.y, 2), ["z"] = (float)Math.Round(self.z, 2)
            });
        else
            SendDirectControl("control", uid, "finger", 1, true);
    }
    catch { }
    Toast("开始抠 " + (_relayConnected ? GetGamePlayerName(uid) : GetPeerName(uid)) + (two ? "（双指）" : ""));
}

[HideFromIl2Cpp]
private void StopFingering()
{
    string uid = _fingerTargetUid;
    StopFingerLocal();
    if (uid.Length == 0 || uid == ToySelfId()) return;
    try
    {
        if (_relayConnected && _relayRoomId.Length > 0)
            RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = uid, ["d"] = "finger", ["on"] = false });
        else
            SendDirectControl("control", uid, "finger", 0, false);
    }
    catch { }
    Toast("停止抠");
}

[HideFromIl2Cpp]
private void ApplyFingerRemote(Dictionary<string, object> m)
{
    bool on = JsonHelper.Bool(m, "on");
    if (on)
    {
        int stage = JsonHelper.Int(m, "stage", -1);
        if (stage >= 0 && (!InGame || CurrentStageInt() != stage))
        {
            DoFollow(stage, new Vector3((float)JsonHelper.Double(m, "x"), (float)JsonHelper.Double(m, "y"), (float)JsonHelper.Double(m, "z")), 0f);
        }
        else if (m.ContainsKey("x") && InGame)
        {
            Vector3 pos = new Vector3((float)JsonHelper.Double(m, "x"), (float)JsonHelper.Double(m, "y"), (float)JsonHelper.Double(m, "z"));
            StartNavFollow(pos);
        }
        _beingFingered = true;
        _beingFingeredUntil = -1f;
        _fingerMoodAt = 0f;
    }
    else
    {
        _beingFingered = false;
        _beingFingeredUntil = 0f;
    }
}

[HideFromIl2Cpp]
private void ApplyFingerPleasure()
{
    if (!InGame) return;
    try { PlayerFacade.Instance.AddAddMoisture(8f); } catch { }
}

[HideFromIl2Cpp]
private List<string> NearAuthorizedTargets()
{
    var list = new List<string>();
    if (!InGame || PlayerFacade.Instance.pca == null) return list;
    var self = PlayerFacade.Instance.pca.AvatorTransform.position;
    foreach (var uid in _toyLinkedTargets)
    {
        Vector3 p = Vector3.zero;
        bool found = false;
        if (_relayGhosts.TryGetValue(uid, out var rg) && rg != null && rg.Root != null) { p = rg.Root.transform.position; found = true; }
        else if (_ghosts.TryGetValue(uid, out var lg) && lg != null && lg.Root != null) { p = lg.Root.transform.position; found = true; }
        else if (_relayPositions.TryGetValue(uid, out var rp)) { p = new Vector3(rp.X, rp.Y, rp.Z); found = true; }
        if (found && Vector3.Distance(self, p) < 10f) list.Add(uid);
    }
    return list;
}

[HideFromIl2Cpp]
private void SelectFingerNearest()
{
    var list = NearAuthorizedTargets();
    if (list.Count == 0) { Toast("附近 10 米内没有已授权控制的玩家（先请求控制）"); return; }
    string best = list[0];
    float bd = float.MaxValue;
    try
    {
        var self = PlayerFacade.Instance.pca.AvatorTransform.position;
        foreach (var u in list)
        {
            Vector3 p = Vector3.zero;
            if (_relayGhosts.TryGetValue(u, out var rg) && rg != null && rg.Root != null) p = rg.Root.transform.position;
            else if (_ghosts.TryGetValue(u, out var lg) && lg != null && lg.Root != null) p = lg.Root.transform.position;
            else if (_relayPositions.TryGetValue(u, out var rp)) p = new Vector3(rp.X, rp.Y, rp.Z);
            float d = Vector3.Distance(self, p);
            if (d < bd) { bd = d; best = u; }
        }
    }
    catch { }
    _fingerTargetUid = best;
    Toast("已选中 " + (_relayConnected ? GetGamePlayerName(best) : GetPeerName(best)));
}

[HideFromIl2Cpp]
private void SelectFingerNext()
{
    var list = NearAuthorizedTargets();
    if (list.Count == 0) { Toast("附近没有可切换的目标"); return; }
    int idx = list.IndexOf(_fingerTargetUid);
    string next = list[(idx + 1) % list.Count];
    _fingerTargetUid = next;
    Toast("已切换 " + (_relayConnected ? GetGamePlayerName(next) : GetPeerName(next)));
}

[HideFromIl2Cpp]
private void ToggleFingerHotkey()
{
    if (_fingerActive) { StopFingering(); return; }
    if (_fingerTargetUid.Length == 0) SelectFingerNearest();
    if (_fingerTargetUid.Length == 0) return;
    StartFingering(_fingerTargetUid, true);
}

[HideFromIl2Cpp]
private static Transform FindBoneIn(Transform root, params string[] names)
{
    if (root == null) return null;
    try
    {
        var stack = new Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            if (t == null) continue;
            try
            {
                if (t.name != null)
                    foreach (var n in names)
                        if (t.name == n) return t;
            }
            catch { }
            for (int i = t.childCount - 1; i >= 0; i--) stack.Push(t.GetChild(i));
        }
    }
    catch { }
    return null;
}
[HideFromIl2Cpp]
private void RequestLeash()
{
    string target = NearestCrouchingPlayer();
    if (target.Length == 0) { Toast("附近没有蹲下的人可牵引（可先在玩家列表点“控制”）"); return; }
    RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_invite", ["to"] = target });
    Toast("已向 " + GetGamePlayerName(target) + " 发送牵引请求，等对方同意");
}

[HideFromIl2Cpp]
private string NearestCrouchingPlayer()
{
    if (!InGame) return "";
    var self = PlayerFacade.Instance.pca.AvatorTransform.position;
    string best = "";
    float bestD = float.MaxValue;
    foreach (var kv in _relayPositions)
    {
        if (kv.Key == _authUid.ToString()) continue;
        bool crouching = _lastStates.TryGetValue(kv.Key, out var st) && st.IsCrouch;
        if (!crouching) continue;
        float d = Vector3.SqrMagnitude(new Vector3(kv.Value.X, kv.Value.Y, kv.Value.Z) - self);
        if (d < bestD) { bestD = d; best = kv.Key; }
    }
    return best;
}

[HideFromIl2Cpp]
private void ToggleRide()
{
    if (_rideTarget.Length > 0) { StopRide(); return; }
    string target = _toyLinkedTarget;
    if (target.Length == 0) target = NearestRideTarget(3.5f);
    if (target.Length == 0) { Toast("3.5 米内没有可选玩家，请在联机房间的玩家列表点“骑乘”"); return; }
    RequestOrStartRide(target);
}

[HideFromIl2Cpp]
private string NearestRideTarget(float maxDistance)
{
    if (!InGame) return "";
    Vector3 self = PlayerFacade.Instance.pca.AvatorTransform.position;
    string best = "";
    float bestSq = maxDistance * maxDistance;
    int stage = CurrentStageInt();
    foreach (var kv in _relayPositions)
    {
        if (kv.Key == _authUid.ToString() || (kv.Value.Stage >= 0 && kv.Value.Stage != stage)) continue;
        float d = Vector3.SqrMagnitude(new Vector3(kv.Value.X, kv.Value.Y, kv.Value.Z) - self);
        if (d <= bestSq) { bestSq = d; best = kv.Key; }
    }
    return best;
}

[HideFromIl2Cpp]
private void RequestOrStartRide(string targetUid)
{
    if (string.IsNullOrEmpty(targetUid)) return;
    if (_toyLinkedTargets.Contains(targetUid))
    {
        _toyLinkedTarget = targetUid;
        StartRide(targetUid);
        return;
    }
    _pendingRideTarget = targetUid;
    RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_invite", ["to"] = targetUid });
    Toast("已向 " + GetGamePlayerName(targetUid) + " 发送骑乘请求，等待对方同意");
}

[HideFromIl2Cpp]
private void ToggleRide(string targetUid)
{
    if (_rideTarget.Length > 0) { StopRide(); return; }
    RequestOrStartRide(targetUid);
}

[HideFromIl2Cpp]
private void StartRide(string targetUid)
{
    if (!_roomAllowRide) { Toast("该房间不允许被骑"); return; }
    CaptureRidePose();
    _rideTarget = targetUid;
    _rideMode = "follow";
    _rideLastSend = -999f;
    RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = targetUid, ["d"] = "ride", ["on"] = true });
    Toast("已开始骑乘，F2 再次按下可停止");
}

[HideFromIl2Cpp]
private void StopRide()
{
    if (_rideTarget.Length > 0)
        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = _rideTarget, ["d"] = "ride", ["on"] = false });
    _rideTarget = "";
    _rideMode = "follow";
    RestoreRidePose();
    StopNavFollow();
    if (_rideRing != null) _rideRing.enabled = false;
}

[HideFromIl2Cpp]
private void SetRideMode(string mode)
{
    if (_rideTarget.Length == 0) return;
    _rideMode = mode;
    if (mode == "control")
    {
        var t = PlayerFacade.Instance.pca.AvatorTransform;
        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = _rideTarget, ["d"] = "follow", ["on"] = true, ["stage"] = CurrentStageInt(), ["x"] = t.position.x, ["y"] = t.position.y, ["z"] = t.position.z });
        Toast("自主控制：被骑者会跟随你移动");
    }
    else
    {
        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = _rideTarget, ["d"] = "follow", ["on"] = false });
        Toast("跟随模式：你跟随被骑者移动");
    }
}

[HideFromIl2Cpp]
private void ApplyRide(bool on)
{
    try
    {
        if (on)
        {
            _isRidden = true;
            ApplyCrouch(true); // 被骑者：四肢爬行/蹲下（同 C 键）
        }
        else
        {
            _isRidden = false;
            ApplyCrouch(false);
        }
    }
    catch (Exception ex) { PluginInfo.Warn("骑乘姿势失败: " + ex.Message); }
}

[HideFromIl2Cpp]
private void UpdateRide()
{
    if (_rideTarget.Length == 0 || !InGame) return;
    if (!_relayPositions.TryGetValue(_rideTarget, out var rp)) return;
    var self = PlayerFacade.Instance.pca.AvatorTransform;
    Vector3 target = new Vector3(rp.X, rp.Y, rp.Z);
    float dist = Vector3.Distance(self.position, target);

    if (_rideMode == "control")
    {
        if (Time.unscaledTime - _rideLastSend > 2f)
        {
            _rideLastSend = Time.unscaledTime;
            RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = _rideTarget, ["d"] = "follow", ["on"] = true, ["stage"] = CurrentStageInt(), ["x"] = self.position.x, ["y"] = self.position.y, ["z"] = self.position.z });
        }
        return;
    }

    if (dist > 1.3f) { StartNavFollow(target); return; }
    StopNavFollow();
    Quaternion facing = Quaternion.Euler(0f, rp.RotY, 0f);
    Vector3 anchor = target + Vector3.down * 0.35f;
    if (_relayGhosts.TryGetValue(_rideTarget, out var rideGhost) && rideGhost != null)
        rideGhost.TryGetRideAnchor(out anchor);
    Vector3 pelvisOffset = Vector3.zero;
    try
    {
        var rr = PlayerFacade.Instance.pca.PlayerAvatarObjectReferencer;
        if (rr != null && rr.LegL != null && rr.LegR != null)
            pelvisOffset = (rr.LegL.position + rr.LegR.position) * 0.5f - self.position;
    }
    catch { }
    Vector3 mountPos = anchor - pelvisOffset + Vector3.up * 0.12f + facing * Vector3.back * 0.10f;
    if (Vector3.Distance(self.position, mountPos) > 0.08f)
        TryWarp(mountPos, rp.RotY);
}

[HideFromIl2Cpp]
private void CaptureRidePose()
{
    try
    {
        var r = PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.PlayerAvatarObjectReferencer : null;
        if (r == null) return;
        if (r.LegL != null) _rideLegL = r.LegL.localRotation;
        if (r.LegR != null) _rideLegR = r.LegR.localRotation;
        if (r.LowerLegL != null) _rideLowerLegL = r.LowerLegL.localRotation;
        if (r.LowerLegR != null) _rideLowerLegR = r.LowerLegR.localRotation;
        _ridePoseCaptured = true;
    }
    catch { }
}

[HideFromIl2Cpp]
private void RestoreRidePose()
{
    if (!_ridePoseCaptured || !InGame) return;
    try
    {
        var r = PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.PlayerAvatarObjectReferencer : null;
        if (r == null) return;
        if (r.LegL != null) r.LegL.localRotation = _rideLegL;
        if (r.LegR != null) r.LegR.localRotation = _rideLegR;
        if (r.LowerLegL != null) r.LowerLegL.localRotation = _rideLowerLegL;
        if (r.LowerLegR != null) r.LowerLegR.localRotation = _rideLowerLegR;
    }
    catch { }
    _ridePoseCaptured = false;
}

[HideFromIl2Cpp]
private void UpdateRidePose()
{
    if (_rideTarget.Length == 0 || !InGame || !_ridePoseCaptured) return;
    try
    {
        var r = PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.PlayerAvatarObjectReferencer : null;
        if (r == null) return;
        float k = 1f - Mathf.Exp(-18f * Mathf.Max(0.001f, Time.unscaledDeltaTime));
        Quaternion thighL = _rideLegL * Quaternion.Euler(48f, 0f, 0f);
        Quaternion thighR = _rideLegR * Quaternion.Euler(48f, 0f, 0f);
        Quaternion calfL = _rideLowerLegL * Quaternion.Euler(-92f, 0f, 0f);
        Quaternion calfR = _rideLowerLegR * Quaternion.Euler(-92f, 0f, 0f);
        if (r.LegL != null) r.LegL.localRotation = Quaternion.Slerp(r.LegL.localRotation, thighL, k);
        if (r.LegR != null) r.LegR.localRotation = Quaternion.Slerp(r.LegR.localRotation, thighR, k);
        if (r.LowerLegL != null) r.LowerLegL.localRotation = Quaternion.Slerp(r.LowerLegL.localRotation, calfL, k);
        if (r.LowerLegR != null) r.LowerLegR.localRotation = Quaternion.Slerp(r.LowerLegR.localRotation, calfR, k);
    }
    catch { }
}

[HideFromIl2Cpp]
private void UpdateRideRing()
{
    bool targetCrouching = _lastStates.TryGetValue(_rideTarget, out var _rst) && _rst.IsCrouch;
    bool show = _rideTarget.Length > 0 && InGame && targetCrouching && _relayPositions.ContainsKey(_rideTarget);
    if (!show)
    {
        if (_rideRing != null) _rideRing.enabled = false;
        return;
    }
    try
    {
        if (_rideRing == null)
        {
            var go = new GameObject("SFM_RideRing");
            _rideRing = go.AddComponent<LineRenderer>();
            _rideRing.startColor = Color.cyan;
            _rideRing.endColor = Color.cyan;
            _rideRing.startWidth = 0.08f;
            _rideRing.endWidth = 0.08f;
            _rideRing.loop = true;
            _rideRing.positionCount = 32;
            try { var sh = Shader.Find("Sprites/Default"); if (sh != null) _rideRing.material = new Material(sh); } catch { }
        }
        _rideRing.enabled = true;
        var rp = _relayPositions[_rideTarget];
        Vector3 center = new Vector3(rp.X, rp.Y + 0.12f, rp.Z);
        for (int i = 0; i < 32; i++)
        {
            float a = i / 32f * Mathf.PI * 2f;
            _rideRing.SetPosition(i, center + new Vector3(Mathf.Cos(a) * 0.45f, 0f, Mathf.Sin(a) * 0.45f));
        }
    }
    catch { }
}

[HideFromIl2Cpp]
private void EnsureBodyOffsets()
{
    if (_bodyOffsetsComputed) return;
    _bodyOffsetsComputed = true;
    try
    {
        var pca = PlayerFacade.Instance != null ? PlayerFacade.Instance.pca : null;
        var referencer = pca != null ? pca.PlayerAvatarObjectReferencer : null;
        var root = pca != null ? pca.AvatorTransform : null;
        if (referencer != null && root != null)
        {
            if (referencer.DildoTargetPussy != null)
                _selfPussyLocalOffset = root.InverseTransformPoint(referencer.DildoTargetPussy.position);
            if (referencer.DildoTargetAnal != null)
                _selfAnalLocalOffset = root.InverseTransformPoint(referencer.DildoTargetAnal.position);
            ClientLog.Write("B点偏移 pussy=" + _selfPussyLocalOffset + " anal=" + _selfAnalLocalOffset);
        }
    }
    catch { }
}

[HideFromIl2Cpp]
private void DumpBoneInventory()
{
    try
    {
        var pca = PlayerFacade.Instance != null ? PlayerFacade.Instance.pca : null;
        var referencer = pca != null ? pca.PlayerAvatarObjectReferencer : null;
        var root = pca != null ? pca.AvatorTransform : null;
        var sb = new System.Text.StringBuilder();
        sb.Append("玩家骨骼清单 | 根=").Append(root != null ? root.name : "null");
        if (referencer != null)
        {
            Transform[] ts = {
                referencer.Hip, referencer.Spine, referencer.Chest, referencer.Neck, referencer.Head,
                referencer.ShoulderL, referencer.ShoulderR,
                referencer.UpperArmL, referencer.UpperArmR,
                referencer.LowerArmL, referencer.LowerArmR,
                referencer.ThumbR,
                referencer.LegL, referencer.LegR,
                referencer.LowerLegL, referencer.LowerLegR,
                referencer.FootL, referencer.FootR,
                referencer.ToeL, referencer.ToeR,
                referencer.DildoTargetPussy, referencer.DildoTargetAnal, referencer.AnalParent, referencer.StrangeSightPoint
            };
            string[] names = {
                "Hip","Spine","Chest","Neck","Head",
                "ShoulderL","ShoulderR",
                "UpperArmL","UpperArmR",
                "LowerArmL","LowerArmR",
                "ThumbR","LegL","LegR","LowerLegL","LowerLegR","FootL","FootR","ToeL","ToeR",
                "DildoTargetPussy","DildoTargetAnal","AnalParent","StrangeSightPoint"
            };
            for (int i = 0; i < ts.Length && i < names.Length; i++)
            {
                var t = ts[i];
                if (t == null) { sb.Append(names[i]).Append("=null "); continue; }
                string path = root != null ? RelativePath(root, t) : "";
                sb.Append(names[i]).Append("=").Append(t.name).Append("[").Append(path).Append("] ");
            }
        }
        ClientLog.Write(sb.ToString());
        PluginInfo.Info(sb.ToString());
        DumpNpcBones();
    }
    catch (Exception ex) { ClientLog.Write("骨骼清单失败: " + ex); }
}

[HideFromIl2Cpp]
private void DumpNpcBones()
{
    try
    {
        var npcs = UnityEngine.Object.FindObjectsOfType<NpcComponent>();
        int count = npcs != null ? npcs.Length : 0;
        var sb = new System.Text.StringBuilder();
        sb.Append("NPC骨骼清单 | 数量=").Append(count);
        if (npcs != null)
        {
            int shown = 0;
            foreach (var nc in npcs)
            {
                if (nc == null || shown >= 3) break;
                shown++;
                try
                {
                    sb.Append(" | NPC").Append(nc.id);
                    var av = nc.AvaterObject;
                    var ac = av != null ? av.GetComponentInChildren<NpcAvatarComponent>(true) : null;
                    if (ac != null)
                    {
                        sb.Append(" Head=").Append(ac.Head != null ? ac.Head.name : "null");
                        sb.Append(" Eye=").Append(ac.Eye != null ? ac.Eye.name : "null");
                        sb.Append(" HandL=").Append(ac.HandL != null ? ac.HandL.name : "null");
                        sb.Append(" HandR=").Append(ac.HandR != null ? ac.HandR.name : "null");
                        sb.Append(" Chip=").Append(ac.MeshChipo != null ? ac.MeshChipo.name : "null");
                    }
                    if (av != null)
                    {
                        var smrs = av.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                        int bones = 0;
                        if (smrs != null)
                            foreach (var s in smrs)
                                if (s != null && s.bones != null) bones += s.bones.Length;
                        sb.Append(" SkinnedBones=").Append(bones);
                    }
                }
                catch { }
            }
        }
        ClientLog.Write(sb.ToString());
        PluginInfo.Info(sb.ToString());
    }
    catch { }
}

[HideFromIl2Cpp]
private void SafeUpdate(string what, Action act)
{
    try { act(); }
    catch (Exception ex)
    {
        if (Time.unscaledTime - _lastWarnTime > 5f)
        {
            _lastWarnTime = Time.unscaledTime;
            PluginInfo.Warn(what + " error: " + ex);
        }
    }
}

[HideFromIl2Cpp]
private void UpdateLanAdvertisement()
{
    if (!IsHosting || Time.unscaledTime - _lastLanAdvertise < 1f) return;
    _lastLanAdvertise = Time.unscaledTime;
    LanDiscovery.UpdateAdvertising(_nickname, _host.Port, OnlineCount(),
        ParseInt(_maxPlayersText, Settings.MaxPlayers.Value), !string.IsNullOrEmpty(_passwordText));
}

// ========== 心跳 ==========
[HideFromIl2Cpp]
private void UpdatePing()
{
    if (!Connected) return;
    if (Time.unscaledTime - _lastPingTime >= 1f)
    {
        _lastPingTime = Time.unscaledTime;
        var w = new WireWriter();
        w.WriteString(PeerId);
        w.WriteLong(Stopwatch.GetTimestamp());
        Send(MsgTypes.Ping, w.ToArray());
    }
}

// ========== 状态发送 ==========
[HideFromIl2Cpp]
private void UpdateStateSend()
{
    if (!Connected || !SceneSyncReady || string.IsNullOrEmpty(PeerId)) return;
    int hz = Math.Max(10, Math.Min(30, Settings.SyncRateHz.Value));
    if (Time.unscaledTime - _lastMotionSend >= 1f / hz)
    {
        _lastMotionSend = Time.unscaledTime;
        SendDirectMotion();
    }
    if (Time.unscaledTime - _lastStateTime >= 1f)
    {
        _lastStateTime = Time.unscaledTime;
        SendState();
    }
}
[HideFromIl2Cpp]
private void RefreshLocalMotion()
{
    if (_cachedMotionFrame == Time.frameCount || !InGame) return;
    _cachedMotionFrame = Time.frameCount;
    try
    {
        var pca = PlayerFacade.Instance.pca;
        var avatar = pca.AvatorTransform;
        var anim = pca.Animator;
        float now = Time.unscaledTime;
        Vector3 pos = avatar.position;
        Vector3 velocity = Vector3.zero;
        if (_lastMotionSampleAt > 0f)
        {
            float dt = now - _lastMotionSampleAt;
            if (dt > 0.005f && dt < 0.5f) velocity = (pos - _lastMotionSamplePos) / dt;
        }
        velocity.y = 0f;
        _cachedMotionVelocity = Vector3.ClampMagnitude(velocity, 9f);
        _cachedMotionMoving = _cachedMotionVelocity.sqrMagnitude > 0.01f;
        _cachedMotionRotY = avatar.eulerAngles.y;
        var ps = pca.PlayerState;
        _cachedMotionCrouch = ps != null && ps.IsCrouch;
        _cachedMotionStrafe = ps != null && ps.IsStrafe;
        _cachedMotionDash = ps != null && ps.IsDash;
        _cachedMotionEcstasy = ps != null && ps.IsEcstasyMotion;
        if (Time.unscaledTime - _lastGroundCalcAt >= 0.1f)
        {
            _lastGroundCalcAt = Time.unscaledTime;
            try
            {
                float lowest = pos.y;
                var renderers = PlayerFacade.Instance.pca.GameObject.GetComponentsInChildren<Renderer>(true);
                foreach (var rr in renderers)
                {
                    if (rr == null || !rr.enabled) continue;
                    try { if (rr.bounds.min.y < lowest) lowest = rr.bounds.min.y; } catch { }
                }
                foreach (var bn in new[] { "Foot_L", "Foot_R", "Toe_L", "Toe_R" })
                {
                    var bt = FindLocalBone(bn);
                    if (bt != null && bt.position.y < lowest) lowest = bt.position.y;
                }
                float rootDrop = Mathf.Max(0f, -_cachedAnimatorDeltaY);
                _cachedGroundOffset = Mathf.Clamp(pos.y - lowest + rootDrop, 0f, 2f);
            }
            catch { _cachedGroundOffset = 0f; }
            try
            {
                var r = PlayerFacade.Instance.pca.PlayerAvatarObjectReferencer;
                if (r != null && r.Hip != null)
                {
                    _cachedHipsLocal = r.Hip.localPosition;
                    _cachedHipsLocalRot = r.Hip.localRotation;
                }
            }
            catch { }
        }
        _cachedMotionAction = ps != null && ps.CurrentAction != null ? (int)ps.CurrentAction.Type : -1;
        if (anim != null)
        {
            try { _cachedAnimatorMoveSpeed = anim.GetFloat(Animator.StringToHash("MoveSpeed")); } catch { _cachedAnimatorMoveSpeed = _cachedMotionMoving ? Mathf.Clamp(_cachedMotionVelocity.magnitude, 0.65f, 2.5f) : 0f; }
            try { _cachedAnimatorLocomotionSpeed = anim.GetFloat(Animator.StringToHash("LocomotionMotionSpeed")); } catch { _cachedAnimatorLocomotionSpeed = 1f; }
            try { _cachedAnimatorStrafeX = anim.GetFloat(Animator.StringToHash("StrafeX")); } catch { _cachedAnimatorStrafeX = 0f; }
            try { _cachedAnimatorStrafeY = anim.GetFloat(Animator.StringToHash("StrafeY")); } catch { _cachedAnimatorStrafeY = 0f; }
            try { _cachedAnimatorActionId = anim.GetInteger(Animator.StringToHash("ActionId")); } catch { _cachedAnimatorActionId = _cachedMotionAction; }
            try { _cachedAnimatorAction = anim.GetInteger(Animator.StringToHash("Action")); } catch { _cachedAnimatorAction = _cachedMotionAction; }
            try { _cachedAnimatorOldActionId = anim.GetInteger(Animator.StringToHash("OldActionId")); } catch { _cachedAnimatorOldActionId = -1; }
            try { _cachedAnimatorAnotherMotion = anim.GetFloat(Animator.StringToHash("AnotherMotionIndex")); } catch { _cachedAnimatorAnotherMotion = 0f; }
            try { _cachedAnimatorDeltaY = anim.deltaPosition.y; } catch { _cachedAnimatorDeltaY = 0f; }
        }
        if (anim != null && (now - _lastMotionAnimSampleAt >= 0.08f || _cachedMotionLayerHashes.Length == 0))
        {
            _lastMotionAnimSampleAt = now;
            int layerCount = Math.Min(8, Math.Max(0, anim.layerCount));
            _cachedMotionLayerHashes = new int[layerCount];
            _cachedMotionLayerTimes = new float[layerCount];
            _cachedMotionLayerWeights = new float[layerCount];
            for (int i = 0; i < layerCount; i++)
            {
                try
                {
                    var info = anim.GetCurrentAnimatorStateInfo(i);
                    _cachedMotionLayerHashes[i] = info.shortNameHash;
                    _cachedMotionLayerTimes[i] = info.normalizedTime;
                    _cachedMotionLayerWeights[i] = anim.GetLayerWeight(i);
                }
                catch { }
            }
        }
        _cachedMotionHash = _cachedMotionLayerHashes.Length > 0 ? _cachedMotionLayerHashes[0] : 0;
        _lastMotionSamplePos = pos;
        _lastMotionSampleAt = now;
    }
    catch { }
}

[HideFromIl2Cpp]
private void SendDirectMotion()
{
    RefreshLocalMotion();
    var w = new WireWriter();
    w.WriteString(PeerId);
    w.WriteFloat(_cachedMotionVelocity.x);
    w.WriteFloat(_cachedMotionVelocity.z);
    w.WriteFloat(_cachedMotionRotY);
    w.WriteBool(_cachedMotionMoving);
    w.WriteBool(_cachedMotionCrouch);
    w.WriteInt(_cachedMotionAction);
    w.WriteInt(_cachedMotionHash);
    // Optional compact Animator fields. No bone transforms are transmitted.
    w.WriteBool(_cachedMotionStrafe);
    w.WriteBool(_cachedMotionDash);
    w.WriteFloat(_cachedAnimatorMoveSpeed);
    w.WriteFloat(_cachedAnimatorLocomotionSpeed);
    w.WriteFloat(_cachedAnimatorStrafeX);
    w.WriteFloat(_cachedAnimatorStrafeY);
    w.WriteInt(_cachedAnimatorActionId);
    w.WriteInt(_cachedAnimatorAction);
    w.WriteInt(_cachedAnimatorOldActionId);
    w.WriteFloat(_cachedAnimatorAnotherMotion);
    Send(MsgTypes.Motion, w.ToArray());
}

[HideFromIl2Cpp]
private string MotionsDir()
{
    try { return System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "SFMOnline_motions"); }
    catch { return System.IO.Path.Combine(Environment.CurrentDirectory, "BepInEx", "SFMOnline_motions"); }
}

[HideFromIl2Cpp]
private void EnsureMotionClipsLoaded()
{
    if (_motionClipsLoaded) return;
    _motionClipsLoaded = true;
    try
    {
        var dir = MotionsDir();
        if (!System.IO.Directory.Exists(dir)) return;
        foreach (var file in System.IO.Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var d = MiniJson.ParseObject(System.IO.File.ReadAllText(file));
                if (d == null) continue;
                var clip = new MotionClip
                {
                    Key = JsonHelper.Str(d, "key"),
                    Name = JsonHelper.Str(d, "name"),
                    Loop = JsonHelper.Int(d, "loop") != 0,
                    Hold = JsonHelper.Int(d, "hold") != 0,
                    Mode = JsonHelper.Int(d, "mode"),
                    Offs = ParseFloats(d.TryGetValue("offs", out var ofs) ? ofs : null),
                    Times = ParseFloats(d.TryGetValue("times", out var tv) ? tv : null),
                    Quats = ParseFloats(d.TryGetValue("quats", out var qv) ? qv : null)
                };
                if (clip.Key.Length > 0 && clip.Times.Length > 0 && clip.Quats.Length >= clip.Times.Length * CoreBoneNames.Length * 4)
                    _motionClips[clip.Key] = clip;
            }
            catch { }
        }
    }
    catch { }
}

[HideFromIl2Cpp]
private void SaveMotionClip(MotionClip clip)
{
    try
    {
        var dir = MotionsDir();
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        string clipName = clip.Name.Length > 0 ? clip.Name : clip.Key;
        string safe = clipName.Replace("/", "_").Replace("\\", "_");
        var d = new Dictionary<string, object>
        {
            ["key"] = clip.Key, ["name"] = clip.Name, ["loop"] = clip.Loop ? 1 : 0, ["hold"] = clip.Hold ? 1 : 0, ["mode"] = clip.Mode,
            ["times"] = NumListF(clip.Times), ["quats"] = NumListF(clip.Quats), ["offs"] = NumListF(clip.Offs)
        };
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, safe + ".json"), MiniJson.Serialize(d));
    }
    catch { }
}

[HideFromIl2Cpp]
private Transform FindLocalBone(string name)
{
    if (_localBoneCache.TryGetValue(name, out var t) && t != null && t.gameObject.activeInHierarchy) return t;
    var avatar = PlayerFacade.Instance != null && PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.AvatorTransform : null;
    t = avatar != null ? FindNamed(avatar, name) : null;
    _localBoneCache[name] = t;
    return t;
}

[HideFromIl2Cpp]
private float SampleBodyOffset()
{
    var avatar = PlayerFacade.Instance != null && PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.AvatorTransform : null;
    if (avatar == null) return 0f;
    float lowest = avatar.position.y;
    foreach (var n in new[] { "Hips", "Foot_L", "Foot_R", "Toe_L", "Toe_R" })
    {
        var t = FindLocalBone(n);
        if (t != null && t.position.y < lowest) lowest = t.position.y;
    }
    return Mathf.Clamp(avatar.position.y - lowest, 0f, 2f);
}

[HideFromIl2Cpp]
private float[] SampleCoreQuats()
{
    var avatar = PlayerFacade.Instance != null && PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.AvatorTransform : null;
    var q = new float[CoreBoneNames.Length * 4];
    if (avatar == null) return q;
    for (int i = 0; i < CoreBoneNames.Length; i++)
    {
        var t = FindLocalBone(CoreBoneNames[i]);
        if (t == null) { q[i * 4] = 0f; q[i * 4 + 1] = 0f; q[i * 4 + 2] = 0f; q[i * 4 + 3] = 1f; continue; }
        var r = t.localRotation;
        q[i * 4] = (float)Math.Round(r.x, 3); q[i * 4 + 1] = (float)Math.Round(r.y, 3);
        q[i * 4 + 2] = (float)Math.Round(r.z, 3); q[i * 4 + 3] = (float)Math.Round(r.w, 3);
    }
    return q;
}

[HideFromIl2Cpp]
private void UpdateMotionCapture()
{
    if (!InGame) return;
    if (Time.unscaledTime - _captureLastSample < 1f / 30f) return;
    if (!_captureActive)
    {
        if (_cachedMotionMoving)
        {
            string dir = MoveDirBucket(_cachedMotionRotY, _cachedMotionVelocity);
            string key = "move_" + dir + (_cachedMotionCrouch ? "_c" : "") + (_cachedMotionDash ? "_d" : "") + (_cachedMotionEcstasy ? "_e" : "");
            if (!_motionClips.ContainsKey(key) && Time.unscaledTime - _lastAutoClipAt > 10f)
            {
                _lastAutoClipAt = Time.unscaledTime;
                _captureKey = key;
                _captureActive = true;
                _captureStart = Time.unscaledTime;
                _captureTimes.Clear(); _captureOffs.Clear(); _captureOffs.Clear();
                _captureFrames.Clear();
            }
        }
        return;
    }
    _captureLastSample = Time.unscaledTime;
    var q = SampleCoreQuats();
    _captureOffs.Add((float)Math.Round(SampleBodyOffset(), 3));
    float t = Time.unscaledTime - _captureStart;
    _captureTimes.Add((float)Math.Round(t, 3));
    _captureFrames.Add(q);
    if (_captureKey.StartsWith("move_"))
    {
        string dir = MoveDirBucket(_cachedMotionRotY, _cachedMotionVelocity);
        string expected = "move_" + dir + (_cachedMotionCrouch ? "_c" : "") + (_cachedMotionDash ? "_d" : "");
        if (!_cachedMotionMoving || _captureKey != expected)
        {
            StopMotionCapture(true);
            if (_cachedMotionMoving && !_motionClips.ContainsKey(expected))
            {
                _captureKey = expected;
                _captureActive = true;
                _captureStart = Time.unscaledTime;
                _captureTimes.Clear(); _captureOffs.Clear(); _captureOffs.Clear();
                _captureFrames.Clear();
            }
            return;
        }
    }
    bool autoClip = _captureKey.StartsWith("move_");
    if (t > (autoClip ? 3f : 8f)) StopMotionCapture(true);
}

[HideFromIl2Cpp]
private void ToggleMotionCapture()
{
    if (_captureActive) { StopMotionCapture(true); return; }
    EnsureMotionClipsLoaded();
    int act = _cachedMotionAction;
    _captureKey = "act_" + act + (_cachedMotionEcstasy ? "_e" : "") + (_cachedAnimatorAction > 0 ? "_v" + _cachedAnimatorAction : "");
    _captureActive = true;
    _captureStart = Time.unscaledTime;
    _captureTimes.Clear();
    _captureFrames.Clear();
    Toast("录制中：act_" + act + "，再按 Shift+F9 停止保存");
}

[HideFromIl2Cpp]
private void StopMotionCapture(bool save)
{
    _captureActive = false;
    if (!save || _captureTimes.Count < 3)
    {
        _captureTimes.Clear(); _captureOffs.Clear(); _captureFrames.Clear();
        Toast("录制数据太少，未保存");
        return;
    }
    int stride = CoreBoneNames.Length * 4;
    var flat = new float[_captureFrames.Count * stride];
    for (int i = 0; i < _captureFrames.Count; i++)
        Array.Copy(_captureFrames[i], 0, flat, i * stride, stride);
    bool loop = _captureKey.StartsWith("move_");
    bool hold = false;
    int mode = loop ? 0 : 2;
    if (_captureKey.StartsWith("act_"))
    {
        int actId = -1;
        string idPart = _captureKey.Substring(4).Split('_')[0];
        int.TryParse(idPart, out actId);
        if (actId == 10001 || actId == 10002 || actId == 10003 || actId == 10004 || actId == 10005 || actId == 10012 || actId == 10013)
            mode = 2;
        else
            mode = ClipIsStatic(_captureTimes.ToArray(), flat) ? 1 : 0;
        hold = mode == 1;
    }
    var clip = new MotionClip { Key = _captureKey, Name = _captureName, Loop = loop, Hold = hold, Mode = mode, Times = _captureTimes.ToArray(), Quats = flat, Offs = _captureOffs.ToArray() };
    _motionClips[_captureKey] = clip;
    SaveMotionClip(clip);
    _captureTimes.Clear(); _captureFrames.Clear();
    Toast("已保存动作骨骼 " + clip.Key + "（" + clip.Times.Length + " 帧）");
}

[HideFromIl2Cpp]
private static bool ClipIsStatic(float[] times, float[] quats)
{
    int stride = CoreBoneNames.Length * 4;
    if (quats == null || quats.Length < stride * 2) return true;
    float maxDiff = 0f;
    for (int i = 0; i < CoreBoneNames.Length; i++)
    {
        int b0 = i * 4;
        float x0 = quats[b0], y0 = quats[b0 + 1], z0 = quats[b0 + 2], w0 = quats[b0 + 3];
        for (int f = 1; f < quats.Length / stride; f++)
        {
            int b = f * stride + i * 4;
            float d = Mathf.Abs(quats[b] - x0) + Mathf.Abs(quats[b + 1] - y0) + Mathf.Abs(quats[b + 2] - z0) + Mathf.Abs(quats[b + 3] - w0);
            if (d > maxDiff) maxDiff = d;
        }
    }
    return maxDiff < 0.03f;
}

[HideFromIl2Cpp]
private static string MoveDirBucket(float rotY, Vector3 vel)
{
    if (vel.sqrMagnitude < 0.05f) return "fwd";
    var fwd = Quaternion.Euler(0f, rotY, 0f) * Vector3.forward;
    var right = Quaternion.Euler(0f, rotY, 0f) * Vector3.right;
    float fd = Vector3.Dot(fwd, vel.normalized);
    float rd = Vector3.Dot(right, vel.normalized);
    if (fd > 0.5f) return "fwd";
    if (fd < -0.5f) return "back";
    if (rd > 0.5f) return "right";
    if (rd < -0.5f) return "left";
    return "fwd";
}

[HideFromIl2Cpp]
private static bool IsHoldAction(int id)
{
    switch (id)
    {
        case 4: case 6: case 7: case 8: case 10018: case 10019: case 10021: case 10023: case 10026:
        case 10027: case 50001: case 50002: case 50003: case 50005: case 50006: case 50007:
        case 50008: case 50009: case 50010: case 50011: case 50012: case 50013: case 50014:
        case 50015: case 50016: case 50017: case 50018: case 50019: case 50020: case 50021:
        case 50022: case 60000: case 60001:
            return true;
    }
    return false;
}

[HideFromIl2Cpp]
private void StartAutoCaptureAll()
{
    if (_autoCaptureIdx >= 0) { Toast("批量录制进行中"); return; }
    EnsureMotionClipsLoaded();
    _autoCaptureIdx = 0;
    _autoCapturePhase = 0;
    Toast("开始批量录制全部动作，共 " + KnownActionIds.Length + " 个；请保持角色可行动，约 2-3 分钟");
}

[HideFromIl2Cpp]
private void UpdateAutoCaptureAll()
{
    if (_autoCaptureIdx < 0) return;
    if (_autoCaptureIdx >= KnownActionIds.Length)
    {
        Toast("全部动作录制完成（" + KnownActionIds.Length + " 个）→ BepInEx/SFMOnline_motions");
        _autoCaptureIdx = -1;
        return;
    }
    int id = KnownActionIds[_autoCaptureIdx];
    string name = _autoCaptureIdx < KnownActionNames.Length ? KnownActionNames[_autoCaptureIdx] : ("act_" + id);
    if (_autoCapturePhase == 0)
    {
        _captureKey = "act_" + id;
        _captureName = name;
        _captureActive = true;
        _captureStart = Time.unscaledTime;
        _captureTimes.Clear(); _captureOffs.Clear();
        _captureFrames.Clear();
        try { PlayerFacade.Instance.TransAction((ActionType)id); } catch { }
        _autoCapturePhase = 1;
        _autoCapturePhaseAt = Time.unscaledTime;
        Toast("录制动作 " + name);
    }
    else if (Time.unscaledTime - _autoCapturePhaseAt >= 3f)
    {
        StopMotionCapture(true);
        _autoCaptureIdx++;
        _autoCapturePhase = 0;
    }
}

[HideFromIl2Cpp]
private void SendRelayBones()
{
    if (!_relayConnected || _relayRoomId.Length == 0 || !InGame) return;
    // 骨同步随房间人数降频：2人内 8Hz，3人 6Hz，4人及以上 5Hz，减轻多人卡顿。
    float boneInterval = _relayPlayers.Count >= 4 ? 0.20f : (_relayPlayers.Count == 3 ? 0.16f : 0.12f);
    if (Time.unscaledTime - _lastRelayBoneAt < boneInterval) return;
    _lastRelayBoneAt = Time.unscaledTime;
    var avatar = PlayerFacade.Instance != null && PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.AvatorTransform : null;
    if (avatar == null) return;
    var q = new List<float>();
    for (int i = 0; i < CoreBoneNames.Length; i++)
    {
        var t = FindLocalBone(CoreBoneNames[i]);
        if (t == null) { q.Add(0f); q.Add(0f); q.Add(0f); q.Add(1f); continue; }
        var r = t.localRotation;
        q.Add((float)Math.Round(r.x, 3)); q.Add((float)Math.Round(r.y, 3));
        q.Add((float)Math.Round(r.z, 3)); q.Add((float)Math.Round(r.w, 3));
    }
    // 增量检测：静止（无变化）时降到 1.5Hz 心跳，变化时立即发送，显著省带宽
    bool changed = _lastRelayBoneQ == null || _lastRelayBoneQ.Count != q.Count;
    if (!changed)
    {
        int diff = 0;
        for (int i = 0; i < q.Count; i++)
            if (Math.Abs(q[i] - _lastRelayBoneQ[i]) > 0.004f) { diff++; if (diff >= 3) break; }
        changed = diff >= 3;
    }
    if (!changed && Time.unscaledTime - _lastRelayBoneChangedAt < 0.65f) return;
    if (changed) _lastRelayBoneChangedAt = Time.unscaledTime;
    _lastRelayBoneQ = q;
    RelayTcp.Send(new Dictionary<string, object> { ["t"] = "bone_sync", ["slot"] = RelayMySlot(), ["q"] = q });
}

[HideFromIl2Cpp]
private void SendRelayMotion()
{
    if (!_relayConnected || _relayRoomId.Length == 0 || !InGame) return;
    RefreshLocalMotion();

    bool layersChanged = _cachedMotionLayerHashes.Length != _lastRelaySentLayerHashes.Length;
    if (!layersChanged)
    {
        for (int i = 0; i < _cachedMotionLayerHashes.Length; i++)
        {
            if (_cachedMotionLayerHashes[i] != _lastRelaySentLayerHashes[i]) { layersChanged = true; break; }
        }
    }
    bool stateChanged = !_relayMotionStateSent ||
        _cachedMotionMoving != _lastRelaySentMoving ||
        _cachedMotionCrouch != _lastRelaySentCrouch ||
        _cachedMotionStrafe != _lastRelaySentStrafe ||
        _cachedMotionDash != _lastRelaySentDash ||
        _cachedMotionAction != _lastRelaySentAction ||
        _cachedMotionHash != _lastRelaySentHash ||
        _cachedAnimatorActionId != _lastRelaySentActionId ||
        _cachedAnimatorAction != _lastRelaySentActionParam ||
        _cachedAnimatorOldActionId != _lastRelaySentOldActionId ||
        Mathf.Abs(_cachedAnimatorAnotherMotion - _lastRelaySentAnotherMotion) > 0.001f ||
        layersChanged;

    int hz = Math.Max(10, Math.Min(30, Settings.SyncRateHz.Value));
    float now = Time.unscaledTime;
    // 状态变化时也要节流，避免动画层 hash 抖动导致每帧狂发 motion_sync/action_sync。
    float minMotionInterval = 1f / Math.Max(hz, 20f);
    if (now - _lastRelayMotionSend < minMotionInterval) return;
    _lastRelayMotionSend = now;

    bool needActionSync = stateChanged || now - _lastRelayActionSyncAt >= 0.18f;
    if (needActionSync && now - _lastRelayActionSyncAt >= 1f / 12f)
    {
        _lastRelayActionSyncAt = now;
        _relayMotionStateSent = true;
        _lastRelaySentMoving = _cachedMotionMoving;
        _lastRelaySentCrouch = _cachedMotionCrouch;
        _lastRelaySentStrafe = _cachedMotionStrafe;
        _lastRelaySentDash = _cachedMotionDash;
        _lastRelaySentAction = _cachedMotionAction;
        _lastRelaySentHash = _cachedMotionHash;
        _lastRelaySentActionId = _cachedAnimatorActionId;
        _lastRelaySentActionParam = _cachedAnimatorAction;
        _lastRelaySentOldActionId = _cachedAnimatorOldActionId;
        _lastRelaySentAnotherMotion = _cachedAnimatorAnotherMotion;
        _lastRelaySentLayerHashes = (int[])_cachedMotionLayerHashes.Clone();
        RelayTcp.Send(new Dictionary<string, object>
        {
            ["t"] = "action_sync", ["slot"] = RelayMySlot(), ["act"] = _cachedMotionAction, ["hash"] = _cachedMotionHash,
            ["aid"] = _cachedAnimatorActionId, ["apm"] = _cachedAnimatorAction,
            ["old"] = _cachedAnimatorOldActionId, ["ami"] = (float)Math.Round(_cachedAnimatorAnotherMotion, 3),
            ["e"] = _cachedMotionEcstasy ? 1 : 0,
            ["lh"] = NumListI(_cachedMotionLayerHashes), ["lt"] = NumListF(_cachedMotionLayerTimes),
            ["lw"] = NumListF(_cachedMotionLayerWeights)
        });
    }

    var packet = new Dictionary<string, object>
    {
        ["t"] = "motion_sync", ["slot"] = RelayMySlot(), ["vx"] = (float)Math.Round(_cachedMotionVelocity.x, 3),
        ["vz"] = (float)Math.Round(_cachedMotionVelocity.z, 3), ["ry"] = (float)Math.Round(_cachedMotionRotY, 2),
        ["moving"] = _cachedMotionMoving ? 1 : 0, ["crouch"] = _cachedMotionCrouch ? 1 : 0,
        ["strafe"] = _cachedMotionStrafe ? 1 : 0, ["dash"] = _cachedMotionDash ? 1 : 0,
        ["dir"] = MoveDirBucket(_cachedMotionRotY, _cachedMotionVelocity), ["e"] = _cachedMotionEcstasy ? 1 : 0,
        ["gy"] = (float)Math.Round(_cachedGroundOffset, 3),
        ["hpx"] = (float)Math.Round(_cachedHipsLocal.x, 3),
        ["hpy"] = (float)Math.Round(_cachedHipsLocal.y, 3),
        ["hpz"] = (float)Math.Round(_cachedHipsLocal.z, 3),
        ["hrx"] = (float)Math.Round(_cachedHipsLocalRot.x, 4),
        ["hry"] = (float)Math.Round(_cachedHipsLocalRot.y, 4),
        ["hrz"] = (float)Math.Round(_cachedHipsLocalRot.z, 4),
        ["hrw"] = (float)Math.Round(_cachedHipsLocalRot.w, 4),
        ["ms"] = (float)Math.Round(_cachedAnimatorMoveSpeed, 3),
        ["lms"] = (float)Math.Round(_cachedAnimatorLocomotionSpeed, 3),
        ["sx"] = (float)Math.Round(_cachedAnimatorStrafeX, 3),
        ["sy"] = (float)Math.Round(_cachedAnimatorStrafeY, 3),
        ["act"] = _cachedMotionAction, ["hash"] = _cachedMotionHash
    };
    // 每 0.2 秒及开始/停止/换动作时附带一次坐标；平时仍只发送速度和状态字段。
    if (stateChanged || now - _lastRelayMotionPosAt >= 0.2f)
    {
        _lastRelayMotionPosAt = now;
        var avatar = PlayerFacade.Instance.pca.AvatorTransform;
        packet["x"] = (float)Math.Round(avatar.position.x, 3);
        packet["y"] = (float)Math.Round(avatar.position.y, 3);
        packet["z"] = (float)Math.Round(avatar.position.z, 3);
    }
    RelayTcp.Send(packet);
}

// ========== 跟随 ==========
[HideFromIl2Cpp]
private void UpdatePendingFollow()
{
    if (_pendingFollowStage < 0) return;
    if (Time.unscaledTime - _pendingFollowTime > 20f)
    {
        _pendingFollowStage = -1;
        Toast(Lang.Get("toast_follow_timeout"));
        return;
    }
    if (InGame && CurrentStageInt() == _pendingFollowStage)
    {
        TryWarp(_pendingFollowPos, _pendingFollowRot);
        Toast(Lang.Get("toast_follow_ok"));
        _pendingFollowStage = -1;
    }
}

// ========== 自动重连 ==========
[HideFromIl2Cpp]
private void UpdateAutoReconnect()
{
    if (!_clientAutoReconnect) return;
    if (_client != null && _client.Connected)
    {
        _reconnectAttempts = 0;
        return;
    }
    if (Time.unscaledTime < _reconnectAt) return;
    _reconnectAttempts++;
    if (_reconnectAttempts > 10)
    {
        _clientAutoReconnect = false;
        _client = null;
        _peers.Clear();
        Toast(Lang.Get("toast_reconnect_fail"));
        return;
    }
    _reconnectAt = Time.unscaledTime + 5f;
    Toast(string.Format(Lang.Get("toast_reconnect"), _reconnectAttempts));
    var parsed = ParseAddress(_reconnectAddress);
    var nc = new NetClient();
    if (nc.Connect(parsed.Item1, parsed.Item2, _nickname, _passwordText, out var err))
    {
        _client = nc;
        _peers.Clear();
        _lastStates.Clear();
        _eventsInitialized = false;
        Toast(Lang.Get("toast_reconnect_ok"));
    }
    else
    {
        nc.Disconnect();
        _client = nc;
    }
}

// ========== 服务器心跳 ==========
[HideFromIl2Cpp]
private void UpdateServerHeartbeat()
{
    if (!_serverIsHosting || string.IsNullOrEmpty(_serverMyRoomId)) return;
    if (Time.unscaledTime - _serverHeartbeatTime < 25f) return;
    _serverHeartbeatTime = Time.unscaledTime;
    var roomId = _serverMyRoomId;
    var token = _serverMyRoomToken;
    RunServer(() => ServerAPI.Heartbeat(roomId, token),
        ok =>
        {
            if (!ok)
            {
                _serverIsHosting = false;
                if (IsHosting) StopHosting();
                Toast(Lang.Get("refresh_failed") + " heartbeat");
            }
        },
        err => Toast(err));
}

// ========== 模拟玩家 ==========
[HideFromIl2Cpp]
private void ToggleSim(bool on)
{
    _simEnabled = on;
    if (!on)
    {
        RemoveGhost(SimPeerId);
        _peers.Remove(SimPeerId);
        _lastStates.Remove(SimPeerId);
        Toast(Lang.Get("toast_sim_remove"));
        return;
    }
    _peers[SimPeerId] = new PeerInfo { Id = SimPeerId, Name = "模拟玩家", IsHost = false, RttMs = 0 };
    Toast(Lang.Get("toast_sim_spawn"));
}

[HideFromIl2Cpp]
private void UpdateSimPlayer()
{
    if (!_simEnabled || !InGame) return;
    if (Time.unscaledTime - _lastSimUpdateTime < 0.1f) return;
    _lastSimUpdateTime = Time.unscaledTime;

    if (!_peers.ContainsKey(SimPeerId))
        _peers[SimPeerId] = new PeerInfo { Id = SimPeerId, Name = "模拟玩家", IsHost = false, RttMs = 0 };

    try
    {
        int avId = PlayerFacade.Instance.pca.AvatorTransform.GetInstanceID();
        if (avId != _lastSimAvatarId)
        {
            _lastSimAvatarId = avId;
            RemoveGhost(SimPeerId);
        }
    }
    catch { }

    if (_ghosts.TryGetValue(SimPeerId, out var sg2) && sg2 != null && sg2.Root != null &&
        !sg2.HasMarker && sg2.BoneMapCount > 0 &&
        sg2.BoneMapCount < SourceCoreBoneCount() * 0.8f)
    {
        RemoveGhost(SimPeerId);
    }

    var ghost = GetOrCreateGhost(SimPeerId);
    if (ghost == null) return;

    var st = SampleLocalState();
    if (st == null) return;

    float t = Time.unscaledTime;
    var local = PlayerFacade.Instance.pca.AvatorTransform;
    st.Pos = local.position + new Vector3(Mathf.Sin(t * 0.6f) * 2.2f, 0f, Mathf.Cos(t * 0.6f) * 2.2f);
    st.RotY = local.eulerAngles.y + Mathf.Sin(t * 0.4f) * 60f;
    st.Stage = CurrentStageInt();
    st.AnimSpeed = 1f;

    try
    {
        for (int i = 0; i < st.FloatNames.Length; i++)
            if (st.FloatNames[i] == "MoveSpeed") st.FloatVals[i] = 1.5f;
        for (int i = 0; i < st.IntNames.Length; i++)
            if (st.IntNames[i] == "Action" || st.IntNames[i] == "ActionId")
                st.IntVals[i] = -1;
    }
    catch { }

    if (st.LayerStateHashes != null)
        for (int i = 0; i < st.LayerStateHashes.Length; i++) st.LayerStateHashes[i] = 0;

    _lastStates[SimPeerId] = st;
    ghost.Apply(st, true);
}

// ========== 主机/客户端控制 ==========
[HideFromIl2Cpp]
private void StartHosting()
{
    if (IsHosting) return;
    if (IsClient) DisconnectClient();
    int port = ParseInt(_portText, Settings.Port.Value);
    int max=Math.Max(2,Math.Min(10,ParseInt(_maxPlayersText,Settings.MaxPlayers.Value)));_maxPlayersText=max.ToString();EnsureLanDiscovery();
    var host = new NetHost();
    if (!host.Start(port, _passwordText, max, _nickname))
    {
        Toast(string.Format(Lang.Get("toast_port_occupied"), port));
        return;
    }
    _host = host;
    _peers.Clear();
    _peers["host"] = new PeerInfo { Id = "host", Name = _nickname, IsHost = true };
    _lastStates.Clear();
    _eventsInitialized=false;_directStateSyncCount=0;_lastDirectAppearanceSig="";
    host.BroadcastPlayers();
    LanDiscovery.StartAdvertising(_nickname, port, OnlineCount(), max, !string.IsNullOrEmpty(_passwordText));
    Toast(string.Format(Lang.Get("toast_host_start"), port, GetLanIp()));
}

[HideFromIl2Cpp]
private void StopHosting()
{
    if (_host != null) _host.Stop();
    _host = null;
    LanDiscovery.StopAdvertising();
    _peers.Clear();
    RemoveAllGhosts();
    _lastStates.Clear();
    _eventsInitialized = false;
    _toyLinkedTargets.Clear(); _toyLinkedTarget = ""; _toyLinkedController = ""; _toyInviteFrom = "";
    ResetToyLocal();
    Toast(Lang.Get("toast_host_stop"));
}

[HideFromIl2Cpp]
private void JoinRoom()
{
    if (IsHosting)
    {
        Toast(Lang.Get("toast_already_host"));
        return;
    }
    if (IsClient) DisconnectClient();
    var parsed = ParseAddress(_addressText);
    _reconnectAddress = _addressText;
    _client = new NetClient();
    if (!_client.Connect(parsed.Item1, parsed.Item2, _nickname, _passwordText, out var err))
    {
        _client = null;
        ClientLog.Write("TCP连接失败 " + parsed.Item1 + ":" + parsed.Item2 + " -> " + err);
        // 服务器已登记玩家但TCP连不上时，立即清除服务器记录，避免假人堆积
        if (_isServerConnected && !string.IsNullOrEmpty(_serverMyRoomId))
            LeaveServerRoom();
        Toast(FormatLanConnectError(parsed.Item1, parsed.Item2, err));
        return;
    }
    _clientAutoReconnect = Settings.AutoReconnect.Value;
    _reconnectAttempts = 0;
    _reconnectAt = 0;
    _peers.Clear();
    _lastStates.Clear();
    _eventsInitialized=false;_directStateSyncCount=0;_lastDirectAppearanceSig="";
    ClientLog.Write("TCP连接成功 " + parsed.Item1 + ":" + parsed.Item2);
    // 三步握手最后一步：TCP已连上，告诉服务器“我已连接”，后台才显示在线
    if (_isServerConnected && !string.IsNullOrEmpty(_serverMyRoomId) && !string.IsNullOrEmpty(_serverJoinPlayerId))
    {
        var rid = _serverMyRoomId;
        var pid = _serverJoinPlayerId;
        RunServer(() => ServerAPI.ConfirmJoin(rid, pid), ok => { }, err => { });
    }
    Toast(Lang.Get("connecting") + " " + parsed.Item1 + ":" + parsed.Item2 + " ...");
}

[HideFromIl2Cpp]
private void DisconnectClient()
{
    _clientAutoReconnect = false;
    if (_client != null) _client.Disconnect();
    _client = null;
    _peers.Clear();
    RemoveAllGhosts();
    _lastStates.Clear();
    _eventsInitialized = false;
    _toyLinkedTargets.Clear(); _toyLinkedTarget = ""; _toyLinkedController = ""; _toyInviteFrom = "";
    ResetToyLocal();
    Toast(Lang.Get("toast_disconnect"));
}

[HideFromIl2Cpp]
private void Send(byte type, byte[] payload)
{
    if (_relayMode)
    {
        _relayOutQueue.Enqueue((type, payload));
        return;
    }
    if (string.IsNullOrEmpty(PeerId)) return;
    if (IsHosting) _host.SendToClients(type, payload, null);
    else if (IsClient) _client.Send(type, payload);
}

// ========== 服务器连接方法 ==========
private readonly ConcurrentQueue<Action> _mainQueue = new ConcurrentQueue<Action>();

[HideFromIl2Cpp]
private void RunOnMain(Action a)
{
    _mainQueue.Enqueue(a);
}

// 统一后台任务入口：结果/异常都回到主线程再改 UI，避免 Unity 线程问题
[HideFromIl2Cpp]
private async void RunServer<T>(Func<Task<T>> op, Action<T> onOk, Action<string> onError = null)
{
    T result = default;
    string error = null;
    try { result = await op(); }
    catch (Exception ex) { error = ex.Message; }
    var r = result;
    var e = error;
    RunOnMain(() =>
    {
        try
        {
            if (e != null) onError?.Invoke(e);
            else onOk?.Invoke(r);
        }
        catch (Exception ex2) { PluginInfo.Warn("server callback: " + ex2); }
    });
}

[HideFromIl2Cpp]
private void ConnectToServer()
{
    if (_nickname.Trim().Length == 0)
    {
        Toast(Lang.Get("toast_nickname_required"));
        return;
    }
    if (string.IsNullOrEmpty(_serverAddress))
    {
        Toast(Lang.Get("server_address") + " " + Lang.Get("error"));
        return;
    }
    if (!int.TryParse(_serverPortText, out int port) || port <= 0) port = 7000;
    _serverPortText = port.ToString();
    // 新联机链路：公共服务器连接也走 relay（TCP），不再用旧 HTTP 接口
    ConnectSelectedServer();
}

[HideFromIl2Cpp]
private void DisconnectFromServer()
{
    _isServerConnected = false;
    _serverIsAdmin = false;
    _serverAdminUser = "";
    _serverAdminPassword = "";
    _serverRooms.Clear();
    _serverAnnouncement = "";
    _serverCaptchaVerified = false;
    _serverCaptchaTex = null;
    _serverCaptchaImageBase64 = "";
    if (_serverIsHosting || !string.IsNullOrEmpty(_serverMyRoomId))
    {
        LeaveServerRoom();
        if (IsHosting) StopHosting();
    }
    ServerAPI.Logout();
    Toast(Lang.Get("toast_server_disconnected"));
}

[HideFromIl2Cpp]
private void RefreshServerRoomList()
{
    if (_isRefreshingServer || !_isServerConnected) return;
    _isRefreshingServer = true;
    _serverRoomListStatus = Lang.Get("refreshing");

    // 一次请求同时拿：房间列表 + 公告 + 服务器时间
    RunServer(() => ServerAPI.Sync("", _serverMyRoomId), r =>
    {
        _isRefreshingServer = false;
        if (r != null && r.code == 0)
        {
            _serverRooms = new List<ServerRoomInfo>(r.rooms ?? new ServerRoomInfo[0]);
            _serverRoomListStatus = string.Format(Lang.Get("room_list_status"), _serverRooms.Count);
            if (r.is_full == 1)
                _serverRoomListStatus += " | " + string.Format(Lang.Get("rooms_full"), r.max_rooms);
            if (!string.IsNullOrEmpty(r.announcement))
                _serverAnnouncement = r.announcement;
            if (!string.IsNullOrEmpty(r.server_name))
                _serverName = r.server_name;
            if (r.messages != null && r.messages.Length > 0)
            {
                _serverChatMessages.Clear();
                foreach (var m in r.messages)
                    _serverChatMessages.Add(m.player_name + ": " + m.message);
            }

            // 管理员在服务器后台强制删除了自己的房间 → 关闭本地房/断开连接
            if (!string.IsNullOrEmpty(_serverMyRoomId) &&
                !_serverRooms.Exists(rm => rm.room_id == _serverMyRoomId))
            {
                if (_serverIsHosting)
                {
                    _serverIsHosting = false;
                    if (IsHosting) StopHosting();
                }
                else if (IsClient)
                {
                    DisconnectClient();
                }
                _serverMyRoomId = "";
                _serverMyRoomToken = "";
                _serverJoinPlayerId = "";
                _serverMyRoomPassword = "";
                _serverCaptchaVerified = false;
                _serverCaptchaInput = "";
                _serverCaptchaDisplay = "";
                _serverCaptchaTex = null;
                _serverCaptchaImageBase64 = "";
                Toast(Lang.Get("toast_room_deleted_by_admin"));
            }
        }
        else
        {
            _serverRoomListStatus = Lang.Get("refresh_failed");
        }
    }, err =>
    {
        _isRefreshingServer = false;
        _serverRoomListStatus = Lang.Get("refresh_failed");
    });
}

[HideFromIl2Cpp]
private void RequestServerCaptcha()
{
    RunServer(() => ServerAPI.GetCaptcha(), cap =>
    {
        if (cap != null && !string.IsNullOrEmpty(cap.imageBase64))
        {
            _serverCaptchaImageBase64 = cap.imageBase64;
            _serverCaptchaTex = LoadCaptchaTexture(cap.imageBase64);
            _serverCaptchaDisplay = "";
            Toast(Lang.Get("toast_captcha_image"));
        }
        else if (cap != null && !string.IsNullOrEmpty(cap.text))
        {
            _serverCaptchaDisplay = cap.text; // 旧服务器文字兜底
            _serverCaptchaTex = null;
            Toast(string.Format(Lang.Get("toast_captcha"), cap.text));
        }
    }, err => Toast(err));
}

[HideFromIl2Cpp]
private Texture2D LoadCaptchaTexture(string data)
{
    try
    {
        if (data.StartsWith("data:image/png;base64,")) data = data.Substring(22);
        else if (data.StartsWith("data:image/bmp;base64,")) data = data.Substring(22);
        byte[] bytes = Convert.FromBase64String(data);
        var tex = new Texture2D(2, 2);
        if (tex.LoadImage(bytes)) return tex;
        return null;
    }
    catch { return null; }
}

[HideFromIl2Cpp]
private void VerifyServerCaptcha()
{
    if (string.IsNullOrEmpty(_serverCaptchaInput)) return;
    var code = _serverCaptchaInput;
    RunServer(() => ServerAPI.VerifyCaptcha(code), ok =>
    {
        if (ok)
        {
            _serverCaptchaVerified = true;
            Toast(Lang.Get("toast_captcha_ok"));
        }
        else
        {
            Toast(Lang.Get("toast_captcha_fail"));
            RequestServerCaptcha();
        }
    }, err => Toast(err));
}

[HideFromIl2Cpp]
private void LoginToServer()
{
    if (string.IsNullOrEmpty(_serverAdminUser) || string.IsNullOrEmpty(_serverAdminPassword))
    {
        Toast(Lang.Get("server_username") + "/" + Lang.Get("server_password") + " " + Lang.Get("error"));
        return;
    }
    var user = _serverAdminUser;
    var pwd = _serverAdminPassword;
    RunServer(() => ServerAPI.Login(user, pwd), ok =>
    {
        if (ok)
        {
            // 登录后先读取服务器下发的管理能力，确认后才显示管理功能
            RunServer(() => ServerAPI.GetAdminInfo(), info =>
            {
                if (info)
                {
                    _serverIsAdmin = true;
                    Toast(Lang.Get("toast_admin_login_ok"));
                }
                else
                {
                    ServerAPI.Logout();
                    Toast(Lang.Get("admin_login_denied"));
                }
            }, err =>
            {
                ServerAPI.Logout();
                Toast(err);
            });
        }
        else Toast(Lang.Get("toast_admin_login_fail"));
    }, err => Toast(err));
}

[HideFromIl2Cpp]
private void CreateServerRoom()
{
    if (string.IsNullOrEmpty(_serverCreateRoomName))
    {
        Toast(Lang.Get("room_name") + " " + Lang.Get("error"));
        return;
    }
    if (!_serverCaptchaVerified)
    {
        Toast(Lang.Get("toast_need_captcha"));
        return;
    }

    int port = ParseInt(_portText, Settings.Port.Value);
    var roomName = _serverCreateRoomName;
    var pwd = _serverCreateRoomPassword;
    _passwordText = pwd;
    bool relay = _isServerConnected && Settings.RelayMode.Value;
    if (!relay)
    {
        // 本地TCP房必须使用同一个房间密码，否则别人用房间密码连接会失败
        if (!IsHosting) StartHosting();
        if (!IsHosting) return;
    }
    var cap = _serverCaptchaInput;
    int max = Math.Max(2, Math.Min(10, ParseInt(_serverCreateMaxPlayersText, 8)));

    RunServer(() => ServerAPI.CreateRoom(_nickname, roomName, port, pwd, max, cap, _serverPublicAddress, _serverJoinServerPwd), r =>
    {
        if (r.ok)
        {
            _serverMyRoomId = r.roomId;
            _serverMyRoomToken = r.token;
            _serverMyRoomPassword = pwd;
            _serverAdminRoomIdInput = r.roomId;
            _serverIsHosting = true;
            Toast(string.Format(Lang.Get("toast_room_created"), r.roomId));
            _serverHeartbeatTime = Time.unscaledTime;
            // 验证码已使用，下一次建房需要重新验证
            _serverCaptchaVerified = false;
            _serverCaptchaInput = "";
            _serverCaptchaDisplay = "";
            _serverCaptchaTex = null;
            _serverCaptchaImageBase64 = "";
            if (relay)
            {
                StartHostingRelay(r.roomId, pwd);
                if (!IsHosting)
                {
                    _serverIsHosting = false;
                    Toast(Lang.Get("relay_fail"));
                    return;
                }
            }
            ClientLog.Write("创建服务器房间 " + r.roomId);
            RefreshServerRoomList();
        }
        else
        {
            ClientLog.Write("创建房间失败 -> " + (r.errorKey ?? r.error));
            Toast(Lang.GetFallback(r.errorKey, string.IsNullOrEmpty(r.error) ? Lang.Get("error") : r.error));
            if (IsHosting) StopHosting();
        }
    }, err =>
    {
        Toast(err);
        if (IsHosting) StopHosting();
    });
}

[HideFromIl2Cpp]
private string RelayBase()
{
    return ServerAPI.GetApiBase().Replace("index.php", "relay.php");
}

[HideFromIl2Cpp]
private void StartHostingRelay(string roomId, string pwd)
{
    if (IsHosting) return;
    if (IsClient) DisconnectClient();
    int max = Math.Max(2, Math.Min(10, ParseInt(_serverCreateMaxPlayersText, 8)));
    var host = new NetHost();
    if (!host.StartRelay(RelayBase(), roomId, pwd, max, _nickname))
    {
        Toast(Lang.Get("relay_fail"));
        return;
    }
    _host = host;
    _peers.Clear();
    _peers["host"] = new PeerInfo { Id = "host", Name = _nickname, IsHost = true };
    _lastStates.Clear();
    _eventsInitialized = false;
    Toast(Lang.Get("relay_on") + " " + roomId);
}

[HideFromIl2Cpp]
private void JoinRoomRelay(string roomId, string pwd)
{
    if (IsHosting) return;
    if (IsClient) DisconnectClient();
    _reconnectAddress = "";
    _client = new NetClient();
    if (!_client.ConnectRelay(RelayBase(), roomId, _nickname, pwd, out var err))
    {
        _client = null;
        ClientLog.Write("PHP中转加入失败: " + err);
        if (_isServerConnected && !string.IsNullOrEmpty(_serverMyRoomId)) LeaveServerRoom();
        Toast(FormatLanConnectError(RelayBase(), 0, err));
        return;
    }
    _clientAutoReconnect = false;
    _peers.Clear();
    _lastStates.Clear();
    _eventsInitialized = false;
    if (_isServerConnected && !string.IsNullOrEmpty(_serverMyRoomId) && !string.IsNullOrEmpty(_serverJoinPlayerId))
    {
        var rid = _serverMyRoomId;
        var pid = _serverJoinPlayerId;
        RunServer(() => ServerAPI.ConfirmJoin(rid, pid), ok => { }, err2 => { });
    }
    ClientLog.Write("PHP中转加入成功 " + roomId);
    Toast(Lang.Get("connecting") + " PHP中转...");
}

// ========== Mod 总服 ==========
[HideFromIl2Cpp]
private void ConnectMaster(bool silent = false)
{
    if (_masterBusy) return;
    _masterBusy = true;
    RunServer(() => MasterClient.Ping(), ok =>
    {
        _masterBusy = false;
        if (ok)
        {
            _masterConnected = true;
            _lastMasterReport = 0;
            if (!silent) Toast(Lang.Get("master_connected"));
            _masterDataPending = true;
        }
        else if (!silent) Toast(Lang.Get("master_fail"));
    }, err => { _masterBusy = false; if (!silent) Toast(err); });
}

[HideFromIl2Cpp]
private void UpdateMasterAuto()
{
    if (_masterConnected) return;
    if (Time.unscaledTime - _lastMasterAttempt < 30f) return;
    _lastMasterAttempt = Time.unscaledTime;
    ConnectMaster(true); // 模组加载后自动强制连接总服，失败每30秒重试
}

[HideFromIl2Cpp]
private void RefreshMasterData()
{
    RunServer(() => MasterClient.GetAnnouncement(), a =>
    {
        _masterAnnTitle = a.title;
        _masterAnnContent = a.content;
    }, err => { });
    RunServer(() => MasterClient.GetServers(_masterPage), r =>
    {
        _masterServers = r.servers;
        _masterTotalPages = r.totalPages;
        foreach (var server in _masterServers)
        {
            var target = server;
            RunServer(() => MasterClient.MeasureLatency(target.address, target.port), ms => target.latency_ms = ms, err => { });
        }
    }, err => { });
    RunServer(() => MasterClient.GetVersion(), v =>
    {
        _masterLatestVersion = v.version;
        _masterLatestUrl = v.url;
        _masterLatestNote = v.note;
        string localMd5 = MasterClient.SelfMd5();
        bool fileMatches = v.ok && v.md5.Length > 0 && localMd5.Length > 0 && string.Equals(localMd5, v.md5, StringComparison.OrdinalIgnoreCase);
        bool sameVer = v.ok && string.Equals(v.version, PluginInfo.Version, StringComparison.OrdinalIgnoreCase);
        bool staged = v.ok && v.md5.Length > 0 && MasterClient.StagedMatches(v.md5);
        _masterUpdateDownloaded = _masterUpdateDownloaded || staged;
        _masterUpdateReady = v.ok && v.version.Length > 0 && !sameVer && (IsNewerVersion(v.version) || v.forceReplace == 1);
        _masterForceUpdate = v.ok && (v.force == 1 || v.forceReplace == 1) && _masterUpdateReady && !fileMatches;
        _masterClientTampered = v.ok && sameVer && v.md5.Length > 0 && localMd5.Length > 0 && !fileMatches;
        // 客户端被修改（同版本但 md5 不符）：直接视为需要更新，自动下载官方文件覆盖，不再卡登录
        if (_masterClientTampered && v.ok && v.md5.Length > 0 && v.url.Length > 0)
        {
            _masterForceUpdate = true;
            _masterUpdateReady = true;
        }
        if (_masterForceUpdate && !_masterUpdateDownloaded && _masterLatestUrl.Length > 0) DownloadMasterUpdate();
    }, err => { });
    RunServer(() => MasterClient.Credits(), r =>
    {
        if (r.ok) _credits = r.credits;
    }, err => { });

}

[HideFromIl2Cpp]
private void ManualRefreshMasterServers()
{
    if (_manualServerRefreshCount >= 5)
    {
        Toast("速度太快了，请等待");
        return;
    }

    float remaining = 5f - (Time.unscaledTime - _lastManualServerRefreshAt);
    if (remaining > 0f)
    {
        Toast("刷新冷却中，请等待 " + Mathf.CeilToInt(remaining) + " 秒");
        return;
    }

    _manualServerRefreshCount++;
    _lastManualServerRefreshAt = Time.unscaledTime;
    RefreshMasterData();
    Toast("正在刷新服务器列表（剩余 " + (5 - _manualServerRefreshCount) + " 次）");
}
[HideFromIl2Cpp]
private bool IsNewerVersion(string v)
{
    try
    {
        var a = ParseVersion(PluginInfo.Version);
        var b = ParseVersion(v);
        for (int i = 0; i < 4; i++)
        {
            if (b[i] > a[i]) return true;
            if (b[i] < a[i]) return false;
        }
        return false;
    }
    catch { return false; }
}

private static int[] ParseVersion(string v)
{
    var parts = (v ?? "").Split('.');
    var r = new int[4];
    for (int i = 0; i < 4; i++)
    {
        int x = 0;
        if (i < parts.Length) int.TryParse(parts[i], out x);
        r[i] = x;
    }
    return r;
}

[HideFromIl2Cpp]
private void MasterPage(int delta)
{
    _masterPage = Math.Max(1, Math.Min(_masterTotalPages, _masterPage + delta));
    RefreshMasterData();
}

[HideFromIl2Cpp]
private void SelectServer(MasterServerInfo s)
{
    if (s == null) return;
    _serverAddress = s.address;
    _serverPortText = s.port.ToString();
    _serverJoinServerPwd = s.password ?? "";
    _relayToken = "";
    Toast("已选择 " + s.name + "，请点下方连接服务器");
}

[HideFromIl2Cpp]
private void ConnectSelectedServer()
{
    if (!_loggedIn) { Toast(Lang.Get("auth_title") + "：" + "请先登录"); return; }
    if (_relayConnectFlowBusy || _relayConnecting) { Toast("正在连接，请勿重复点击"); return; }
    string host = _serverAddress.Trim();
    if (host.Length == 0) { Toast("请先在列表选择服务器"); return; }
    if (!int.TryParse(_serverPortText, out int port)) port = 7000;
    string pwd = _serverJoinServerPwd;
    _relayConnectFlowBusy = true;
    ClientLog.Write("开始连接联机服: " + host + ":" + port);
    RunServer(() => ConnectRelayFlow(host, port, pwd), r =>
    {
        _relayConnectFlowBusy = false;
        if (!r.ok)
        {
            ClientLog.Write("连接联机服失败: " + host + ":" + port + " code=" + r.code + " msg=" + r.msg);
            Toast((r.code == -100 ? "无法连接联机服：" : "联机许可被拒绝(" + r.code + ")：") + r.msg + RelayConnectHint(r.msg));
            return;
        }
        _relayToken = r.token;
        _relayConnecting = true;
        _relayConnected = false;
        _relayConnectStartedAt = Time.unscaledTime;
        RelayTcp.Hello(_authUid, _authUsername, "auto", _relayToken, pwd);
        _relayChat = new List<string>();
        _relayChatScroll = Vector2.zero;
        _relayServerName = "";
        _relayAnnounceTitle = "";
        _relayAnnounceContent = "";
        _relayRoomId = "";
        Toast("正在验证联机服连接 " + host + ":" + port);
    }, err =>
    {
        _relayConnectFlowBusy = false;
        ClientLog.Write("连接联机服任务异常: " + err);
        Toast("连接失败，请稍后重试");
    });
}

[HideFromIl2Cpp]
private async Task<(bool ok, string msg, int code, string token)> ConnectRelayFlow(string host, int port, string pwd)
{
    string tcpIdentity = "";
    bool connected = false;
    for (int i = 1; i <= 3 && !connected; i++)
    {
        connected = RelayTcp.Connect(host, port, out tcpIdentity);
        if (!connected && i < 3) await Task.Delay(3000);
    }
    if (!connected) return (false, "TCP 端口不可达，已重试 3 次" + (RelayTcp.LastError.Length > 0 ? "（" + RelayTcp.LastError + "）" : ""), -100, "");

    var apply = (ok: false, msg: "network error", code: -1, token: "");
    for (int i = 1; i <= 3; i++)
    {
        apply = await MasterClient.RelayApply(host, port, pwd, _authUid, _authUsername, tcpIdentity, _authToken);
        if (apply.ok) break;
        if (i < 3) await Task.Delay(3000);
    }
    if (!apply.ok) RelayTcp.Close();
    return apply;
}
[HideFromIl2Cpp]
private void ConnectToMasterServer(MasterServerInfo s)
{
    if (_masterBusy || s == null) return;
    if (!string.IsNullOrEmpty(s.required_mods))
    {
        _modsPromptHost = s.address;
        _modsPromptPort = s.port;
        _modsPromptList = s.required_mods;
        return;
    }
    ClientLog.Write("连接总服列表服务器: " + s.name + " " + s.address + ":" + s.port);
    ConnectGameServerWithRetry(s.address, s.port);
}

[HideFromIl2Cpp]
private void ContinueModsConnect()
{
    string host = _modsPromptHost;
    int port = _modsPromptPort;
    _modsPromptHost = "";
    if (host.Length == 0) return;
    ConnectGameServerWithRetry(host, port);
}

[HideFromIl2Cpp]
private void ConnectToMasterCustom()
{
    if (_masterBusy) return;
    var text = _masterCustomAddr.Trim();
    if (text.Length == 0) { Toast(Lang.Get("admin_room_id_required")); return; }
    string host = text;
    int port = 80;
    int idx = text.LastIndexOf(':');
    if (idx > 0 && int.TryParse(text.Substring(idx + 1), out int p)) { host = text.Substring(0, idx); port = p; }
    ClientLog.Write("连接自定义服务器: " + host + ":" + port);
    ConnectGameServerWithRetry(host, port);
}

[HideFromIl2Cpp]
private async void ConnectGameServerWithRetry(string host, int port)
{
    _masterBusy = true;
    _relayServerHost = host ?? "";
    for (int i = 1; i <= 3; i++)
    {
        ServerAPI.SetServerAddress(host, port);
        bool ok = await ServerAPI.ProbeAsync();
        if (ok)
        {
            RunOnMain(() =>
            {
                _masterBusy = false;
                _isServerConnected = true;
                _serverAddress = host;
                Toast(Lang.Get("toast_server_connected"));
                RefreshServerRoomList();
            });
            ReportMaster("server", host + ":" + port);
            return;
        }
        if (i < 3)
        {
            Toast(string.Format(Lang.Get("master_retry"), i));
            await Task.Delay(3000);
        }
    }
    RunOnMain(() => { _masterBusy = false; Toast(Lang.Get("master_connect_fail")); });
}

[HideFromIl2Cpp]
private void ReportMaster(string state, string serverAddr)
{
    RunServer(() => MasterClient.Report(PluginInfo.Version, state, serverAddr), r =>
    {
        if (r.ok) _masterOnline = r.online;
    }, err => { });
}

[HideFromIl2Cpp]
private void UpdateMasterReport()
{
    if (!_loggedIn || _authToken.Length == 0) return;
    if (Time.unscaledTime - _lastMasterReport < 30f) return;
    _lastMasterReport = Time.unscaledTime;
    string addr = _isServerConnected ? ServerAPI.GetServerAddress() : "";
    string sname = _serverName;
    RunServer(() => MasterClient.Heartbeat(_authToken, PluginInfo.Version, addr, sname, ""), r =>
    {
        if (!r.ok && r.code == -3)
        {
            if (_isServerConnected) DisconnectFromServer();
            Toast(Lang.Get("domain_blocked"));
            return;
        }
        if (!r.ok && r.code == -1)
        {
            Relogin();
            return;
        }
        if (r.ok) _masterOnline = r.online;
    }, err => { });
    RunServer(() => MasterClient.TokenCheck(_authToken), r =>
    {
        if (!r.ok)
        {
            Relogin();
        }
        else ApplyAdminInfo(r.data);
    }, err => { });
}

[HideFromIl2Cpp]
private void DownloadMasterUpdate()
{
    if (_masterUpdateDownloading || _masterUpdateDownloaded || _masterLatestUrl.Length == 0) return;
    _masterUpdateDownloading = true;
    var updDir = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "SFMOnline_versions");
    System.IO.Directory.CreateDirectory(updDir);
    var dest = System.IO.Path.Combine(updDir, "SFMOnline.dll");
    var url = _masterLatestUrl;
    RunServer(() => MasterClient.Download(url, dest), ok =>
    {
        _masterUpdateDownloading = false;
        if (ok)
        {
            _masterUpdateDownloaded = true;
            _modDisabled = true;
            Toast(Lang.Get("update_downloaded"));
            if (Plugin.ReplaceFromStaging()) { _masterReplaceDone = true; _masterForceUpdate = false; }
        }
        else Toast(Lang.Get("update_fail"));
    }, err => { _masterUpdateDownloading = false; Toast(err); });
}private void CheckPendingUpdate()
{
    try
    {
        var flag = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "SFMOnline_Update", ".updated");
        _masterPendingRestart = System.IO.File.Exists(flag);
    }
    catch { _masterPendingRestart = false; }
}

// ========== 账号登录 ==========
[HideFromIl2Cpp]
private string TokenPath() =>
    System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "SFMOnline_token.dat");

[HideFromIl2Cpp]
private string MachineId()
{
    try { return UnityEngine.SystemInfo.deviceUniqueIdentifier; }
    catch { return "unknown"; }
}

[HideFromIl2Cpp]
private void LoadSavedToken()
{
    if (!Settings.AutoLogin.Value) return;
    string tokenPath = TokenPath();
    string credPath = CredPath();
    Task.Run(() =>
    {
        var loaded = new string[6];
        try
        {
            if (System.IO.File.Exists(credPath))
            {
                string raw = MasterClient.DecryptLocal(System.IO.File.ReadAllText(credPath));
                string[] parts = (raw ?? "").Split('\n');
                if (parts.Length >= 2) { loaded[0] = parts[0]; loaded[1] = parts[1]; }
                if (parts.Length >= 5) { loaded[2] = parts[2]; loaded[3] = parts[3]; loaded[4] = parts[4]; }
            }
            if (System.IO.File.Exists(tokenPath))
                loaded[5] = MasterClient.DecryptLocal(System.IO.File.ReadAllText(tokenPath));
        }
        catch { }
        return loaded;
    }).ContinueWith(t =>
    {
        if (t.Status != TaskStatus.RanToCompletion || t.Result == null) return;
        var loaded = t.Result;
        _mainQueue.Enqueue(() =>
        {
            _authAccount = loaded[0] ?? "";
            _authSavedPass = loaded[1] ?? "";
            _agUserV = loaded[2] ?? "";
            _agPrivacyV = loaded[3] ?? "";
            _agNonce = loaded[4] ?? "";
            _authToken = loaded[5] ?? "";
            if (_authToken.Length == 0) return;
            string token = _authToken;
            RunServer(() => MasterClient.TokenCheck(token), r =>
            {
                if (r.ok)
                {
                    _loggedIn = true;
                    _authUid = JsonHelper.Int(r.data, "uid");
                    _authUsername = JsonHelper.Str(r.data, "username");
                    _authEmailBound = JsonHelper.Str(r.data, "email");
                    _authTitle = JsonHelper.Str(r.data, "title");
                    _authTitleColor = JsonHelper.Str(r.data, "title_color");
                    ApplyAdminInfo(r.data);
                    Toast(Lang.Get("auth_logged_in") + " " + _authUsername);
                }
                else
                {
                    _authToken = "";
                    try { System.IO.File.Delete(tokenPath); } catch { }
                    // Token 过期时使用本机保存的凭据续取；RefreshLogin 会先取当天/当前 IP 的协议凭证，
                    // 随后的 login 请求再执行一次性客户端安全计算。
                    if (_authAccount.Trim().Length > 0 && _authSavedPass.Length > 0) Relogin();
                }
            }, err => { });
        });
    });
}

[HideFromIl2Cpp]
private void SaveToken()
{
    if (!Settings.AutoLogin.Value) { try { System.IO.File.Delete(TokenPath()); } catch { } return; }
    try
    {
        System.IO.File.WriteAllText(TokenPath(), MasterClient.EncryptLocal(_authToken));
    }
    catch { }
}

[HideFromIl2Cpp]
private string CredPath() =>
    System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "SFMOnline_cred.dat");

[HideFromIl2Cpp]
private void SaveCreds()
{
    try
    {
        if (_authAccount.Trim().Length == 0 || _authSavedPass.Length == 0)
        {
            System.IO.File.Delete(CredPath());
            return;
        }
        System.IO.File.WriteAllText(CredPath(), MasterClient.EncryptLocal(_authAccount.Trim() + "\n" + _authSavedPass + "\n" + _agUserV + "\n" + _agPrivacyV + "\n" + _agNonce));
    }
    catch { }
}

[HideFromIl2Cpp]
private void LoadCreds()
{
    try
    {
        if (!System.IO.File.Exists(CredPath())) return;
        string raw = MasterClient.DecryptLocal(System.IO.File.ReadAllText(CredPath()));
        if (raw.Length == 0) return;
        string[] parts = raw.Split('\n');
        if (parts.Length >= 2)
        {
            _authAccount = parts[0];
            _authSavedPass = parts[1];
        }
        if (parts.Length >= 5)
        {
            _agUserV = parts[2];
            _agPrivacyV = parts[3];
            _agNonce = parts[4];
        }
    }
    catch { }
}

[HideFromIl2Cpp]
private void Relogin()
{
    if (_authAccount.Trim().Length == 0 || _authSavedPass.Length == 0) LoadCreds();
    if (_reloginBusy || _authAccount.Trim().Length == 0 || _authSavedPass.Length == 0) return;
    _reloginBusy = true;
    RunServer(() => MasterClient.RefreshLogin(_authAccount.Trim(), _authSavedPass, MachineId()), r =>
    {
        _reloginBusy = false;
        if (r.ok)
        {
            AuthSuccess(r.token, r.uid, r.username, r.email);
            Toast("登录已自动续期");
        }
        else
        {
            _loggedIn = false;
            _authToken = "";
            try { System.IO.File.Delete(TokenPath()); } catch { }
            Toast(r.msg);
        }
    }, err => { _reloginBusy = false; });
}
// 检测服务器 auth.php 是否为新版（含协议/滑块接口），旧版时登录门直接提示
[HideFromIl2Cpp]
private void UpdateAuthServerCheck()
{
    if (!_showMenu) return;
    if (_loggedIn) return;
    if (Time.unscaledTime - _lastAuthServerCheck < 60f) return;
    _lastAuthServerCheck = Time.unscaledTime;
    RunServer(() => MasterClient.AuthPing(), r =>
    {
        _authServerVersion = r.authVersion;
        _authServerOld = !r.ok;
        if (_authServerOld)
        {
            ClientLog.Write("auth.php 版本过旧或缺少协议接口，登录无法进行");
            _lastAgreementFetch = -999f;
        }
    }, err => { });
}

// ========== 协议（云端更新，强制重新同意） ==========
[HideFromIl2Cpp]
private string AgreementPath() =>
    System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "SFMOnline_agreement.dat");

[HideFromIl2Cpp]
private void LoadAgreementLocal()
{
    string path = AgreementPath();
    Task.Run(() =>
    {
        try
        {
            if (!System.IO.File.Exists(path)) return null;
            string raw = MasterClient.DecryptLocal(System.IO.File.ReadAllText(path));
            var parts = (raw ?? "").Split('|');
            return parts.Length >= 2 ? new[] { parts[0], parts[1] } : null;
        }
        catch { return null; }
    }).ContinueWith(t =>
    {
        if (t.Status != TaskStatus.RanToCompletion || t.Result == null) return;
        var parts = t.Result;
        _mainQueue.Enqueue(() =>
        {
            _agLocalUserV = parts[0];
            _agLocalPrivacyV = parts[1];
        });
    });
}

[HideFromIl2Cpp]
private void SaveAgreementLocal()
{
    try
    {
        System.IO.File.WriteAllText(AgreementPath(), MasterClient.EncryptLocal(_agUserV + "|" + _agPrivacyV));
        _agLocalUserV = _agUserV;
        _agLocalPrivacyV = _agPrivacyV;
        _agNotice = "";
    }
    catch { }
}

[HideFromIl2Cpp]
private bool AgreementOutdated()
{
    return _agLoaded && (_agLocalUserV != _agUserV || _agLocalPrivacyV != _agPrivacyV);
}

[HideFromIl2Cpp]
private bool AgreementOkLocal()
{
    return _agLoaded && _agLocalUserV == _agUserV && _agLocalPrivacyV == _agPrivacyV;
}

[HideFromIl2Cpp]
private void UpdateAgreement()
{
    if (!_showMenu) return;
    if (Time.unscaledTime - _lastAgreementFetch < 60f) return;
    _lastAgreementFetch = Time.unscaledTime;
    RunServer(() => MasterClient.AgreementInfo(), r =>
    {
        if (!r.ok)
        {
            _authAgreementMissing = true;
            ClientLog.Write("协议接口缺失：请先在服务器部署新版接口");
            return;
        }
        _authAgreementMissing = false;
        _agUserV = r.userV;
        _agUserUrl = r.userUrl;
        _agUserTitle = r.userTitle;
        _agPrivacyV = r.privacyV;
        _agPrivacyUrl = r.privacyUrl;
        _agPrivacyTitle = r.privacyTitle;
        _agNonce = r.nonce;
        _agLoaded = true;
        _agNotice = AgreementOutdated() ? Lang.Get("auth_agreement_outdated") : "";
    }, err => { });
}

[HideFromIl2Cpp]
private void OpenAgreement(bool user)
{
    string url = user ? _agUserUrl : _agPrivacyUrl;
    if (string.IsNullOrEmpty(url)) { Toast(Lang.Get("auth_agreement_loading")); return; }
    Application.OpenURL(url);
}

[HideFromIl2Cpp]
private void AuthGetCaptcha()
{
    if (_authBusy) return;
    _authBusy = true;
    RunServer(() => MasterClient.Captcha(), r =>
    {
        _authBusy = false;
        if (r.ok)
        {
            _authSid = r.sid;
            _authCaptchaImage = r.image;
            _authCaptchaTex = LoadCaptchaTexture(r.image);
            _authCaptcha = "";
            Toast(Lang.Get("auth_slider_tip"));
        }
        else Toast(r.msg.Length > 0 ? r.msg : Lang.Get("master_fail"));
    }, err => { _authBusy = false; Toast(err); });

    if (_codeSentAt > 0 && !_codeActionTaken && Time.unscaledTime - _codeSentAt > 10)
    {
        _uiY += 4f;
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW - 110f, 46f), Lang.Get("resend_tip"), WrapStyle());
        if (_canButton && SButton(new Rect(_uiX + _uiW - 104f, _uiY, 100f, 24f), Lang.Get("resend_btn")))
            AuthResendCode();
        _uiY += 50f;
    }}

[HideFromIl2Cpp]
private void AuthResendCode()
{
    string type = _authMode == "register" ? "register" : (_authMode == "forgot" ? "forgot" : "login");
    string email = _authMode == "login" && _authUseCode ? _authAccount.Trim() : _authEmail.Trim();
    if (email.Length == 0) { Toast(Lang.Get("auth_email_required")); return; }
    _codeActionTaken = false;
    RunServer(() => MasterClient.ResendCode(type, email, _authSid, _authCaptcha.Trim().ToUpperInvariant()), r =>
    {
        Toast(r.msg);
        if (r.ok) _codeSentAt = Time.unscaledTime;
    }, err => Toast(err));
}
[HideFromIl2Cpp]
private void AuthSendCode()
{
    if (_authBusy) return;
    if (_authSid.Length == 0) { Toast(Lang.Get("auth_need_slider")); AuthGetCaptcha(); return; }
    if (_authCaptcha.Trim().Length < 4) { Toast(Lang.Get("auth_slider_tip")); return; }
    string email = _authMode == "login" && _authUseCode ? _authAccount.Trim() : _authEmail.Trim();
        if (email.IndexOf("@") < 0) { Toast(Lang.Get("auth_email_required")); return; }
    if (email.Length == 0) { Toast(Lang.Get("auth_email_required")); return; }
    _authBusy = true;
    string type = _authMode == "register" ? "register" : (_authMode == "forgot" ? "forgot" : "login");
    _codeSentAt = Time.unscaledTime; _codeActionTaken = false;
    RunServer(() => MasterClient.SendCode(type, email, _authSid, _authCaptcha.Trim().ToUpperInvariant(), MachineId()), r =>
    {
        _authBusy = false;
        _authSid = "";
        _authCaptchaImage = "";
        _authCaptchaTex = null;
        _authCaptcha = "";
        Toast(r.ok ? Lang.Get("auth_code_sent") : r.msg);
    }, err => { _authBusy = false; Toast(err); });
}

[HideFromIl2Cpp]
private void AuthSubmit()
{
    if (_masterForceUpdate) { Toast(Lang.Get("update_force")); return; }
    _codeActionTaken = true;
    if (_authBusy) return;
    if (!_agLoaded) { Toast(Lang.Get("auth_agreement_loading")); return; }
    if (!_agAcceptUser || !_agAcceptPrivacy) { Toast(Lang.Get("auth_agree_required")); return; }
    SaveAgreementLocal();
    if (_authMode == "login")
    {
        if (_authUseCode && _authCode.Length == 0) { Toast(Lang.Get("auth_code_required")); return; }
        if (!_authUseCode && _authPass.Length == 0) { Toast(Lang.Get("auth_pass_required")); return; }
        _authBusy = true;
        if (!_authUseCode && _authNeedCaptcha && (_authSid.Length == 0 || _authCaptcha.Trim().Length < 4))
        {
            Toast("登录失败过一次，请先通过图形验证码再登录");
            AuthGetCaptcha();
            return;
        }
        string loginSid = _authNeedCaptcha ? _authSid : "";
        string loginCap = _authNeedCaptcha ? _authCaptcha.Trim().ToUpperInvariant() : "";
        RunServer(() => MasterClient.LoginFull(_authAccount.Trim(), _authUseCode ? "" : _authPass, _authUseCode ? _authCode : "", MachineId(),
            _agUserV, _agPrivacyV, _agNonce, loginSid, loginCap), r =>
        {
            _authBusy = false;
            if (r.ok)
            {
                _authNeedCaptcha = false;
                _authOnline = r.online;
                _authRegistered = r.registered;
                _authTitle = r.title;
                _authTitleColor = r.titleColor;
                SaveAgreementLocal();
                AuthSuccess(r.token, r.uid, r.username, r.email);
            }
            else if (r.code == -4) { _agAcceptUser = false; _agAcceptPrivacy = false; _agNotice = Lang.Get("auth_agreement_outdated"); _lastAgreementFetch = -999f; Toast(r.msg); }
            else
            {
                _authNeedCaptcha = true;
                _authSid = "";
                _authCaptchaImage = "";
                _authCaptchaTex = null;
                _authCaptcha = "";
                AuthGetCaptcha();
                Toast(r.msg);
            }
        }, err => { _authBusy = false; Toast(err); });
    }
    else if (_authMode == "register")
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(_authAccount.Trim(), "^[A-Za-z0-9_]{4,20}$"))
        { Toast(Lang.Get("rename_rule")); return; }
        if (_authPass != _authPass2) { Toast(Lang.Get("auth_pass_mismatch")); return; }
        if (_authAccount.Trim().Length == 0 || _authEmail.Trim().Length == 0 || _authPass.Length == 0 || _authCode.Length == 0)
        { Toast(Lang.Get("auth_fill_all")); return; }
        _authBusy = true;
        RunServer(() => MasterClient.RegisterNoLogin(_authAccount.Trim(), _authEmail.Trim(), _authPass, _authCode, true, MachineId(),
            _agUserV, _agPrivacyV, _agNonce, _authSid, _authCaptcha.Trim().ToUpperInvariant()), r =>
        {
            _authBusy = false;
            if (r.ok)
            {
                _authMode = "login";
                _authAccount = _authEmail.Trim();
                _authCode = "";
                _authPass = "";
                _authMsg = r.msg;
            }
            else if (r.code == -4) { _agAcceptUser = false; _agAcceptPrivacy = false; _agNotice = Lang.Get("auth_agreement_outdated"); _lastAgreementFetch = -999f; Toast(r.msg); }
            else Toast(r.msg);
        }, err => { _authBusy = false; Toast(err); });
    }
    else if (_authMode == "forgot")
    {
        if (_authEmail.Trim().Length == 0 || _authCode.Length == 0 || _authPass.Length == 0)
        { Toast(Lang.Get("auth_fill_all")); return; }
        _authBusy = true;
        RunServer(() => MasterClient.Forgot(_authEmail.Trim(), _authCode, _authPass, _agUserV, _agPrivacyV, _agNonce), r =>
        {
            _authBusy = false;
            if (r.ok) _authMode = "login";
            else if (r.code == -4) { _agAcceptUser = false; _agAcceptPrivacy = false; _agNotice = Lang.Get("auth_agreement_outdated"); _lastAgreementFetch = -999f; Toast(r.msg); return; }
            Toast(r.ok ? Lang.Get("auth_reset_ok") : r.msg);
        }, err => { _authBusy = false; Toast(err); });
    }
}

[HideFromIl2Cpp]
private void AuthSuccess(string token, long uid, string username, string email)
{
    _authToken = token;
    _authUid = uid;
    _authUsername = username;
    _authEmailBound = email;
    if (_authPass.Length > 0) _authSavedPass = _authPass;
    _loggedIn = true;
    SaveToken();
    SaveCreds();
    _agAcceptUser = true;
    _agAcceptPrivacy = true;
    Toast(Lang.Get("auth_logged_in") + " " + username);
    RunServer(() => MasterClient.TokenCheck(token), r =>
    {
        if (r.ok) ApplyAdminInfo(r.data);
    }, err => { });
    ConnectMaster(true);
}

[HideFromIl2Cpp]
private void AuthLogout()
{
    _loggedIn = false;
    _authToken = "";
    try { System.IO.File.Delete(TokenPath()); } catch { }
    _masterConnected = false;
    _renameInput = "";
    _authIsAdmin = false; _authAdminLevel = 0; _authAdminActions = new List<string>(); _captchaBig = false;
}
        [HideFromIl2Cpp]
        private void ApplyAdminInfo(Dictionary<string, object> data)
        {
            _authIsAdmin = JsonHelper.Int(data, "is_admin") == 1;
            _authAdminLevel = JsonHelper.Int(data, "admin_level");
            _authAdminActions = JsonHelper.StrList(data, "admin_actions");
        }


// 协议更新后的强制重新同意面板
[HideFromIl2Cpp]
private void DrawReAgreePanel()
{
    const float h = 22f;
    const float step = 27f;
    if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("auth_title") + " ──");
    _uiY += step;
    if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW, h), "⚠️ " + Lang.Get("auth_agreement_outdated"));
    _uiY += step;
    if (_agUserTitle.Length > 0 && _canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW, h), _agUserTitle);
    _uiY += step;
    _agAcceptUser = GUI.Toggle(new Rect(_uiX, _uiY, _uiW - 64f, h), _agAcceptUser, Lang.Get("auth_agree_user"));
    if (_canButton && GUI.Button(new Rect(_uiX + _uiW - 58f, _uiY, 56f, h), Lang.Get("auth_view")))
        OpenAgreement(true);
    _uiY += step;
    if (_agPrivacyTitle.Length > 0 && _canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW, h), _agPrivacyTitle);
    _uiY += step;
    _agAcceptPrivacy = GUI.Toggle(new Rect(_uiX, _uiY, _uiW - 64f, h), _agAcceptPrivacy, Lang.Get("auth_agree_privacy"));
    if (_canButton && GUI.Button(new Rect(_uiX + _uiW - 58f, _uiY, 56f, h), Lang.Get("auth_view")))
        OpenAgreement(false);
    _uiY += step;
    if (_canButton && GUI.Button(new Rect(_uiX, _uiY, _uiW, 24f), Lang.Get("auth_submit")))
    {
        if (_agAcceptUser && _agAcceptPrivacy)
        {
            SaveAgreementLocal();
            Toast(Lang.Get("auth_agree_ok"));
        }
        else Toast(Lang.Get("auth_agree_required"));
    }
    _uiY += 30f;
    if (_canButton && GUI.Button(new Rect(_uiX, _uiY, _uiW, 24f), Lang.Get("btn_logout")))
        AuthLogout();
    _uiY += 30f;
}

[HideFromIl2Cpp]
private void DoRename()
{
    string name = _renameInput.Trim();
    if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9\\u4E00-\\u9FFF]{2,20}$"))
    {
        Toast(Lang.Get("rename_rule"));
        return;
    }
    if (name == _authUsername) { Toast(Lang.Get("rename_same")); return; }
    RunServer(() => MasterClient.Rename(_authToken, name), r =>
    {
        if (r.ok)
        {
            _authUsername = r.username;
            _renameInput = "";
        }
        Toast(r.ok ? Lang.Get("rename_ok") : r.msg);
    }, err => Toast(err));
}

[HideFromIl2Cpp]
private void DrawAuthGate()
{
    const float h = 26f;
    const float step = 31f;
    if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("auth_title") + " ──");
    _uiY += step;
    if (_authServerOld && _canLabel)
    {
        GUI.Label(new Rect(_uiX, _uiY, _uiW, h), "⚠️ 服务器账号接口未更新（auth.php 过旧），无法登录，请联系管理员上传最新 auth.php");
        _uiY += step;
    }
    if (_canButton && GUI.Button(new Rect(_uiX, _uiY, 70f, 22f), Lang.Get("auth_login_tab")))
    { _authMode = "login"; _authMsg = ""; }
    if (_canButton && GUI.Button(new Rect(_uiX + 76f, _uiY, 70f, 22f), Lang.Get("auth_register_tab")))
    { _authMode = "register"; _authMsg = ""; }
    if (_canButton && GUI.Button(new Rect(_uiX + 152f, _uiY, 70f, 22f), Lang.Get("auth_forgot_tab")))
    { _authMode = "forgot"; _authMsg = ""; }
    if (_canButton && GUI.Button(new Rect(_uiX + _uiW - 118f, _uiY, 116f, 22f),
        Lang.Get("lang_switch") + ": " + (Lang.Current == Language.Chinese ? "中文" : "EN")))
    {
        Lang.ToggleLanguage();
        Settings.Language.Value = Lang.Current == Language.Chinese ? "Chinese" : "English";
    }

    _uiY += 28f;

    if (_authMode == "login" || _authMode == "register")
    {
        if (_authMode == "login")
        {
            if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, 90f, h), Lang.Get("auth_account"));
            _authAccount = UiTextField("auth_user", new Rect(_uiX + 94f, _uiY, 150f, h), _authAccount, false, out _);
            _uiY += step;
            if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("auth_account_hint"));
            _uiY += step;
            if (_canToggle) { Settings.AutoLogin.Value = GUI.Toggle(new Rect(_uiX, _uiY, _uiW, h), Settings.AutoLogin.Value, Lang.Get("remember_login")); }
            _uiY += step;
        }
        else
        {
            if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, 60f, h), Lang.Get("username"));
            _authAccount = UiTextField("auth_user", new Rect(_uiX + 64f, _uiY, 140f, h), _authAccount, false, out _);
            _uiY += step;
        }
        if (_authMode == "register")
        {
            var hintStyle = WrapStyle();
            string hintText = Lang.Get("auth_register_hint");
            float hintH = hintStyle.CalcHeight(new GUIContent(hintText), _uiW);
            GUI.Label(new Rect(_uiX, _uiY, _uiW, hintH), hintText, hintStyle);
            _uiY += hintH + 8f;
            if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, 60f, h), Lang.Get("email"));
            _authEmail = UiTextField("auth_email", new Rect(_uiX + 64f, _uiY, 160f, h), _authEmail, false, out _);
            _uiY += step;
        }
        if (_authMode == "login" && _canToggle)
        {
            _authUseCode = GUI.Toggle(new Rect(_uiX, _uiY, _uiW, h), _authUseCode, Lang.Get("auth_code_login"));
            _uiY += step;
        }
        if (!_authUseCode)
        {
            if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, 60f, h), Lang.Get("password"));
            _authPass = UiTextField("auth_pass", new Rect(_uiX + 64f, _uiY, 170f, h), _authPass, true, out _);
            _uiY += step;
            if (_authMode == "register")
            {
                if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, 60f, h), Lang.Get("auth_pass2"));
                _authPass2 = UiTextField("auth_pass2", new Rect(_uiX + 64f, _uiY, 170f, h), _authPass2, true, out _);
                _uiY += step;
            }
        }
        if (_authUseCode || _authMode == "register")
        {
            if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, 60f, h), Lang.Get("auth_code"));
            _authCode = UiTextField("auth_code", new Rect(_uiX + 64f, _uiY, 80f, h), _authCode, false, out _);
            if (_canButton && GUI.Button(new Rect(_uiX + 148f, _uiY, 90f, h), Lang.Get("auth_send_code")))
                AuthSendCode();
            _uiY += step;

        }
        if (_authCaptchaImage.Length > 0 && _authCaptchaTex != null)
        {
            GUI.DrawTexture(new Rect(_uiX, _uiY, 220f, 60f), _authCaptchaTex);
            if (_canButton && GUI.Button(new Rect(_uiX + 224f, _uiY, 44f, 60f), Lang.Get("auth_refresh")))
                AuthGetCaptcha();
            _uiY += 66f;
            if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, 86f, h), Lang.Get("auth_captcha"));
            _authCaptcha = UiTextField("auth_captcha", new Rect(_uiX + 92f, _uiY, 100f, h), _authCaptcha, false, out _);
            if (_canButton && GUI.Button(new Rect(_uiX + 170f, _uiY, 70f, h), Lang.Get("auth_captcha_zoom"))) _captchaBig = true;
            _uiY += step;
        }
        else if (_canButton && GUI.Button(new Rect(_uiX, _uiY, _uiW, 22f), (_authNeedCaptcha && _authMode == "login" && !_authUseCode ? "⚠ 需先通过图形验证码（登录失败过一次）" : Lang.Get("auth_get_slider"))))
            AuthGetCaptcha();
    
    _uiY += 28f;
        if (_canButton && GUI.Button(new Rect(_uiX, _uiY, _uiW, 24f), Lang.Get("auth_submit")))
            AuthSubmit();
        _uiY += 30f;
    }
    else
    {
        if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, 60f, h), Lang.Get("email"));
        _authEmail = UiTextField("auth_email2", new Rect(_uiX + 64f, _uiY, 160f, h), _authEmail, false, out _);
        _uiY += step;
        if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, 60f, h), Lang.Get("auth_code"));
        _authCode = UiTextField("auth_code2", new Rect(_uiX + 64f, _uiY, 80f, h), _authCode, false, out _);
        if (_canButton && GUI.Button(new Rect(_uiX + 148f, _uiY, 90f, h), Lang.Get("auth_send_code")))
            AuthSendCode();
        _uiY += step;
        if (_authCaptchaImage.Length > 0 && _authCaptchaTex != null)
        {
            GUI.DrawTexture(new Rect(_uiX, _uiY, 220f, 60f), _authCaptchaTex);
            if (_canButton && GUI.Button(new Rect(_uiX + 224f, _uiY, 44f, 60f), Lang.Get("auth_refresh")))
                AuthGetCaptcha();
            _uiY += 66f;
            if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, 86f, h), Lang.Get("auth_captcha"));
            _authCaptcha = UiTextField("auth_captcha2", new Rect(_uiX + 64f, _uiY, 100f, h), _authCaptcha, false, out _);
            if (_canButton && GUI.Button(new Rect(_uiX + 170f, _uiY, 70f, h), Lang.Get("auth_captcha_zoom"))) _captchaBig = true;
            _uiY += step;
        }
        else if (_canButton && GUI.Button(new Rect(_uiX, _uiY, _uiW, 22f), (_authNeedCaptcha && _authMode == "login" && !_authUseCode ? "⚠ 需先通过图形验证码（登录失败过一次）" : Lang.Get("auth_get_slider"))))
            AuthGetCaptcha();
    
    _uiY += 28f;
        if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, 60f, h), Lang.Get("auth_new_pass"));
        _authPass = UiTextField("auth_newpass", new Rect(_uiX + 64f, _uiY, 140f, h), _authPass, true, out _);
        _uiY += step;
        if (_canButton && GUI.Button(new Rect(_uiX, _uiY, _uiW, 24f), Lang.Get("auth_submit")))
            AuthSubmit();
        _uiY += 30f;
    }
    // ===== 协议区（登录/注册/找回密码都必须同意）=====
    if (_agLoaded)
    {
        if (_agNotice.Length > 0 && _canLabel)
        {
            GUI.Label(new Rect(_uiX, _uiY, _uiW, h), "⚠️ " + _agNotice);
            _uiY += step;
        }
        _agAcceptUser = GUI.Toggle(new Rect(_uiX, _uiY, _uiW - 64f, h), _agAcceptUser, Lang.Get("auth_agree_user"));
        if (_canButton && GUI.Button(new Rect(_uiX + _uiW - 58f, _uiY, 56f, h), Lang.Get("auth_view")))
            OpenAgreement(true);
        _uiY += step;
        _agAcceptPrivacy = GUI.Toggle(new Rect(_uiX, _uiY, _uiW - 64f, h), _agAcceptPrivacy, Lang.Get("auth_agree_privacy"));
        if (_canButton && GUI.Button(new Rect(_uiX + _uiW - 58f, _uiY, 56f, h), Lang.Get("auth_view")))
            OpenAgreement(false);
        _uiY += step;
    }
    else
    {
        if (_canLabel)
        {
            GUI.Label(new Rect(_uiX, _uiY, _uiW, h),
                _authAgreementMissing
                    ? "协议信息暂未加载，可先点击下方查看"
                    : (_authServerOld ? "服务器账号接口未更新，可先查看协议" : Lang.Get("auth_agreement_loading")));
            _uiY += step;
        }
        float agreementButtonW = (_uiW - 6f) * 0.5f;
        if (_canButton && GUI.Button(new Rect(_uiX, _uiY, agreementButtonW, h), "查看用户协议")) OpenAgreement(true);
        if (_canButton && GUI.Button(new Rect(_uiX + agreementButtonW + 6f, _uiY, agreementButtonW, h), "查看隐私政策")) OpenAgreement(false);
        _uiY += step;
    }
    if (_authMsg.Length > 0 && _canLabel)
    {
        GUI.Label(new Rect(_uiX, _uiY, _uiW, h), _authMsg);
        _uiY += step;
    }
}

// ========== 登录后子面板 ==========
[HideFromIl2Cpp]
private void DrawFriendPanel()
{
    const float h = 22f;
    const float step = 27f;
    if (Time.unscaledTime - _lastFriendRefresh > 15f)
    {
        _lastFriendRefresh = Time.unscaledTime;
        RefreshFriends();
    }    if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("menu_friend") + " ──");
    _uiY += step;
    if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, 46f, h), Lang.Get("search"));
    _friendKw = UiTextField("f_kw", new Rect(_uiX + 48f, _uiY, 140f, h), _friendKw, false, out _);
    if (_canButton && GUI.Button(new Rect(_uiX + 192f, _uiY, 60f, h), Lang.Get("search")))
        RunServer(() => MasterClient.FriendSearch(_authToken, _friendKw.Trim()), r =>
        {
            _friendResults = r.users;
        }, err => Toast(err));
    _uiY += step;
    if (_canButton && GUI.Button(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("friend_list")))
        RefreshFriends();
    _uiY += step;
    foreach (var u in _friendResults)
    {
        long uid = JsonHelper.Int(u, "uid");
        if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW - 74f, 20f),
            uid + " " + JsonHelper.Str(u, "username") + " " + ColorTitle(JsonHelper.Str(u, "title"), JsonHelper.Str(u, "title_color")));
        if (_canButton && GUI.Button(new Rect(_uiX + _uiW - 68f, _uiY, 64f, 20f), Lang.Get("friend_add")))
            RunServer(() => MasterClient.FriendAdd(_authToken, uid), ok =>
            { Toast(ok ? Lang.Get("friend_request_sent") : Lang.Get("refresh_failed")); }, err => Toast(err));
        _uiY += 22f;
    }
    if (_friendRequests.Count > 0)
    {
        if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("friend_requests") + " ──");
        _uiY += step;
        foreach (var rq in _friendRequests)
        {
            long uid = JsonHelper.Int(rq, "uid");
            if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW - 150f, 20f),
                uid + " " + JsonHelper.Str(rq, "username") + " " + ColorTitle(JsonHelper.Str(rq, "title"), JsonHelper.Str(rq, "title_color")));
            if (_canButton && GUI.Button(new Rect(_uiX + _uiW - 146f, _uiY, 68f, 20f), Lang.Get("friend_accept")))
                RunServer(() => MasterClient.FriendAccept(_authToken, uid), ok =>
                { Toast(ok ? Lang.Get("friend_accept_ok") : Lang.Get("refresh_failed")); if (ok) RefreshFriends(); }, err => Toast(err));
            if (_canButton && GUI.Button(new Rect(_uiX + _uiW - 72f, _uiY, 68f, 20f), Lang.Get("friend_reject")))
                RunServer(() => MasterClient.FriendReject(_authToken, uid), ok =>
                { Toast(ok ? Lang.Get("friend_reject_ok") : Lang.Get("refresh_failed")); if (ok) RefreshFriends(); }, err => Toast(err));
            _uiY += 22f;
        }
    }
    foreach (var f in _friendList)
    {
        long uid = JsonHelper.Int(f, "uid");
        string name = JsonHelper.Str(f, "username");
        string title = ColorTitle(JsonHelper.Str(f, "title"), JsonHelper.Str(f, "title_color"));
        bool online = JsonHelper.Int(f, "online") == 1;
        bool locked = JsonHelper.Int(f, "locked") == 1;
        if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW - 220f, 20f),
            (online ? "🟢 " : "⚪ ") + (locked ? "🔒 " : "") + uid + " " + name + " " + title);
        if (_canButton && GUI.Button(new Rect(_uiX + _uiW - 216f, _uiY, 44f, 20f), Lang.Get("dm_open")))
        { _dmPeerUid = uid; _dmList = new List<string>(); _dmLastId = 0; _lastDmRefresh = -999f; _menuTab = "dm"; LoadDm(); }
        if (_canButton && GUI.Button(new Rect(_uiX + _uiW - 168f, _uiY, 68f, 20f), Lang.Get("profile_view")))
        { _profileUid = uid; _profileInfo = "UID：" + uid + "\n" + Lang.Get("username") + "：" + name; _menuTab = "profile"; }
        if (!locked && _canButton && GUI.Button(new Rect(_uiX + _uiW - 96f, _uiY, 74f, 20f), Lang.Get("friend_delete")))
            RunServer(() => MasterClient.FriendDelete(_authToken, uid), ok =>
            { if (ok) { _friendList = _friendList.Where(x => JsonHelper.Int(x, "uid") != uid).ToList(); } else Toast(Lang.Get("refresh_failed")); }, err => { });
        _uiY += 22f;
    }
    if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("friend_settings"));
    _uiY += step;
    if (_canToggle)
    {
        _friendHideServer = GUI.Toggle(new Rect(_uiX, _uiY, 170f, h), _friendHideServer, Lang.Get("friend_hide_server"));
        _friendAllowSearch = GUI.Toggle(new Rect(_uiX + 178f, _uiY, 180f, h), _friendAllowSearch, Lang.Get("friend_allow_search"));
    }
    if (_canButton && GUI.Button(new Rect(_uiX, _uiY + h + 2f, _uiW, 22f), Lang.Get("btn_save")))
        RunServer(() => MasterClient.FriendSettings(_authToken, _friendHideServer, _friendAllowSearch), ok =>
        { Toast(ok ? Lang.Get("auth_reset_ok") : Lang.Get("refresh_failed")); }, err => Toast(err));
}

[HideFromIl2Cpp]
private void LoadDm()
{
    if (_dmLoading || _dmPeerUid <= 0) return;
    _dmLoading = true;
    long after = _dmLastId;
    RunServer(() => MasterClient.DmList(_authToken, _dmPeerUid, after), msgs =>
    {
        _dmLoading = false;
        foreach (var m in msgs)
        {
            long id = JsonHelper.Long(m, "id");
            if (id > _dmLastId) _dmLastId = id;
            bool mine = JsonHelper.Long(m, "from_uid") == _authUid;
            _dmList.Add((mine ? "我" : "对方") + ": " + JsonHelper.Str(m, "message"));
        }
    }, err => { _dmLoading = false; });
}
[HideFromIl2Cpp]
private void RefreshFriends()
{
    _lastFriendRefresh = Time.unscaledTime;
    RunServer(() => MasterClient.FriendList(_authToken), r => { _friendList = r.friends; }, err => { });
    RunServer(() => MasterClient.FriendRequests(_authToken), r => { _friendRequests = r.requests; }, err => { });
    RunServer(() => MasterClient.FriendSettingsGet(_authToken), r =>
    { if (r.ok) { _friendHideServer = r.hide; _friendAllowSearch = r.allow; } }, err => { });
}
[HideFromIl2Cpp]
private void DrawPubChatPanel()
{
    const float h = 22f;
    const float step = 27f;
    if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("menu_pubchat") + " ──");
    _uiY += step;
    GUI.Box(new Rect(_uiX, _uiY, _uiW, 130f), "");
    float ly = _uiY + 4f;
    int start = Math.Max(0, _pubMsgs.Count - 6);
    for (int i = start; i < _pubMsgs.Count; i++)
    {
        var m = _pubMsgs[i];
        string name = JsonHelper.Str(m, "sender_name");
        string title = ColorTitle(JsonHelper.Str(m, "title"), JsonHelper.Str(m, "title_color"));
        string msg = JsonHelper.Str(m, "message");
        string tag = JsonHelper.Int(m, "official") == 1 ? "[官方] " : "";
        GUI.Label(new Rect(_uiX + 6f, ly, _uiW - 12f, 18f), tag + name + title + ": " + msg);
        ly += 20f;
    }
    _uiY += 136f;
    _pubInput = UiTextField("pub_in", new Rect(_uiX, _uiY, _uiW - 74f, h), _pubInput, false, out bool submit);
    if (_canButton && GUI.Button(new Rect(_uiX + _uiW - 68f, _uiY, 68f, h), Lang.Get("btn_send")))
        SendPubChat();
    if (submit) SendPubChat();
}

[HideFromIl2Cpp]
private void SendPubChat()
{
    if (!PrepareOutgoingChat(_pubInput, out string text)) return;
    _pubInput = "";
    RunServer(() => MasterClient.PubChatSend(_authToken, text), r =>
    {
        if (!r.ok) Toast(r.msg);
        UpdatePubPoll();
    }, err => Toast(err));
}

[HideFromIl2Cpp]
private void UpdateRelayPoll()
{
    if (!_relayConnected && !_relayConnecting) return;
    string line;
    int handled = 0;
    long pollStarted = Stopwatch.GetTimestamp();
    while (handled < 48 && RelayTcp.TryDequeue(out line))
    {
        if (!string.IsNullOrWhiteSpace(line)) HandleRelayLine(line);
        handled++;
        if (handled >= 6 && (Stopwatch.GetTimestamp() - pollStarted) / (double)Stopwatch.Frequency >= 0.0025)
            break;
    }
    // UDP 数据面收包（高频同步消息）
    try
    {
        int udpHandled = 0;
        while (udpHandled < 32 && RelayTcp.TryDequeueUdp(out string udpLine))
        {
            if (!string.IsNullOrWhiteSpace(udpLine)) HandleRelayLine(udpLine);
            udpHandled++;
        }
    }
    catch { }
    if (_relayConnecting && (!RelayTcp.Connected || Time.unscaledTime - _relayConnectStartedAt > 15f))
    {
        RelayTcp.Close();
        _relayConnecting = false;
        _relayConnected = false;
        Toast("联机服验证超时，请重新连接");
        return;
    }
    if (!_relayConnected) return;
    if (Time.unscaledTime - _lastRelayStats > 10f)
    {
        _lastRelayStats = Time.unscaledTime;
        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "ping" });
    }
    if (Time.unscaledTime - _lastRelayHttpStats > 10f)
    {
        _lastRelayHttpStats = Time.unscaledTime;
        RunServer(() => MasterClient.RelayStats(_authToken, MasterClient.GetBase()), r =>
        {
            if (r.ok) { _masterRelayOnline = r.online; _masterRelayMaxOnline = r.maxOnline; }
        }, err => { });
    }
}

[HideFromIl2Cpp]
private void HandleRelayLine(string line)
{
    try
    {
        var m = MiniJson.ParseObject(line);
        if (m == null) return;
        string t = JsonHelper.Str(m, "t");
        if (t == "ok")
        {
            _relayConnecting = false;
            _relayConnected = true;
            _relayServerName = JsonHelper.Str(m, "server_name");
            _relayOnline = JsonHelper.Int(m, "online");
            _relayMaxOnline = JsonHelper.Int(m, "max_online");
            // 启动 UDP 数据面通道（高频同步走 UDP，缓解 TCP 压力与端口封锁）
            try
            {
                RelayTcp.CacheIdentity(_authUid.ToString(), _relayServerHost);
                RelayTcp.StartUdp(_relayServerHost);
                RelayTcp.RegisterUdp("");
            }
            catch { }
            // 缓存服务器插件/mod 列表（供 Ext API 查询）
            _relayServerMods = JsonHelper.List(m, "mods");
            SFMOnline.Ext.SfmExtNet.SetServerMods(_relayServerMods);
            var ann = JsonHelper.Object(m, "announcement");
            if (ann != null && ann.Count > 0)
            {
                _relayAnnounceTitle = JsonHelper.Str(ann, "title");
                _relayAnnounceContent = JsonHelper.Str(ann, "content");
                if (_relayAnnounceTitle.Length > 0 || _relayAnnounceContent.Length > 0)
                    AddRelayLine("[公告] " + _relayAnnounceTitle + " " + _relayAnnounceContent);
            }
            var pub = JsonHelper.List(m, "pubchat");
            foreach (var p in pub)
                AddRelayLine("[公屏] " + JsonHelper.Str(p, "name") + ": " + JsonHelper.Str(p, "text"));
            _relayRoomId = "";
            _relayPlayers = new List<Dictionary<string, object>>();
            RelayTcp.Send(new Dictionary<string, object> { ["t"] = "room_list" });
            Toast("联机服连接验证成功");
            string missingMods = CheckRelayMods(JsonHelper.List(m, "mods"));
            if (missingMods.Length > 0)
            {
                AddRelayLine("[模组] 缺少必需模组: " + missingMods);
                Toast("缺少必需模组，无法连接: " + missingMods);
                RelayTcp.Close();
                _relayConnected = false;
                return;
            }
        }
        else if (t == "announcement")
        {
            _relayAnnounceTitle = JsonHelper.Str(m, "title");
            _relayAnnounceContent = JsonHelper.Str(m, "content");
            AddRelayLine("[公告] " + _relayAnnounceTitle + " " + _relayAnnounceContent);
        }
        else if (t == "pub_chat")
            AddRelayLine("[公屏] " + JsonHelper.Str(m, "name") + ": " + JsonHelper.Str(m, "text"));
        else if (t == "chat")
        {
            if (JsonHelper.Str(m, "uid") == _authUid.ToString() && _lastRelayChatLine.Length > 0)
            {
                int pendingIndex = _relayChat.LastIndexOf(_lastRelayChatLine);
                if (pendingIndex >= 0) _relayChat.RemoveAt(pendingIndex);
                _lastRelayChatLine = "";
            }
            AddRelayLine(JsonHelper.Str(m, "name") + ": " + JsonHelper.Str(m, "d"));
        }
        else if (t == "mod_list_push")
        {
            // 服务器推送房主模组清单 → 比对本地 → 有缺失则弹窗提示
            try
            {
                var hostFiles = JsonHelper.List(m, "files");
                if (hostFiles == null || hostFiles.Count == 0) return;
                _hostModList = hostFiles;
                _modNeedList = SFMOnline.ModLoader.DiffManifest(hostFiles);
                if (_modNeedList.Count > 0)
                {
                    _modPromptOpen = true;
                    _modPromptModeInstall = true;
                    _modDownloadError = "";
                    AddRelayLine("[模组] 房主有 " + _modNeedList.Count + " 个模组文件需要同步");
                }
                else
                {
                    AddRelayLine("[模组] 已拥有房主的全部模组");
                }
            }
            catch (Exception ex) { PluginInfo.Warn("mod_list_push: " + ex.Message); }
        }
        else if (t == "mod_file_push")
        {
            // 服务器转发房主的文件数据 → 写入本地 → 继续下载下一个
            try
            {
                string fname = JsonHelper.Str(m, "file");
                string dataB64 = JsonHelper.Str(m, "data");
                if (fname.Length > 0 && dataB64.Length > 0)
                {
                    byte[] data = Convert.FromBase64String(dataB64);
                    SFMOnline.ModLoader.SaveDownloaded(fname, data);
                    AddRelayLine("[模组] 已下载 " + fname);
                }
                _modDownloadIndex++;
                if (_modDownloadIndex < _modNeedList.Count)
                {
                    RelayTcp.Send(new Dictionary<string, object> { ["t"] = "mod_file_req", ["file"] = _modNeedList[_modDownloadIndex] });
                }
                else
                {
                    _modDownloading = false;
                    _modPromptModeInstall = false;
                    _modReloadAfter = Time.unscaledTime + 1f;
                    int loaded = SFMOnline.ModLoader.HotLoadAll(_modNeedList);
                    AddRelayLine("[模组] 同步完成，加载 " + loaded + " 个新模组，即将重进游戏");
                }
            }
            catch (Exception ex) { PluginInfo.Warn("mod_file_push: " + ex.Message); }
        }
        else if (t == "mod_err")
        {
            string em = JsonHelper.Str(m, "m");
            _modDownloading = false;
            _modDownloadError = em;
            AddRelayLine("[模组] " + em);
        }
        else if (t == "ext_item_drop" || t == "ext_item_perm" || t == "ext_item_pick_ok" || t == "ext_item_pick_deny" || t == "ext_item_collect_all")
        {
            HandleRemoteDrop(t, m);
        }
        else if (t == "room_created")
        {
            _relayRoomId = JsonHelper.Str(m, "room_id");
            _relayCaptchaTex = null;
            _relayCaptchaInput = "";
            _relayRoomPassword = "";
            _relayHostUid = JsonHelper.Str(m, "host");
            _relayPlayers=JsonHelper.List(m,"players");_roomAllowGameBonuses=m.ContainsKey("allow_game_bonuses")&&JsonHelper.Int(m,"allow_game_bonuses")!=0;PrepareRelayAppearanceSync();
            AddRelayLine("[房间] 已创建 " + _relayRoomId + (_relayHostUid == _authUid.ToString() ? "（你是房主）" : ""));
            // UDP 数据面：登记房间
            try { RelayTcp.SetUdpIdentity(_authUid.ToString(), _relayRoomId); RelayTcp.RegisterUdp(_relayRoomId); } catch { }
            // 房主上报模组清单（加入房间的玩家会自动比对并提示安装）
            try
            {
                if (_relayHostUid == _authUid.ToString())
                {
                    var manifest = SFMOnline.ModLoader.CollectManifest();
                    RelayTcp.Send(new Dictionary<string, object> { ["t"] = "mod_list", ["files"] = manifest });
                }
            }
            catch (Exception ex) { PluginInfo.Warn("mod_list 上报: " + ex.Message); }
        }
        else if (t == "room_joined")
        {
            _relayRoomId = JsonHelper.Str(m, "room_id");
            _relayCaptchaTex = null;
            _relayCaptchaInput = "";
            _relayHostUid = JsonHelper.Str(m, "host");
            _relayPlayers=JsonHelper.List(m,"players");_roomAllowGameBonuses=m.ContainsKey("allow_game_bonuses")&&JsonHelper.Int(m,"allow_game_bonuses")!=0;PrepareRelayAppearanceSync();
            AddRelayLine("[房间] 已加入 " + _relayRoomId);
            // UDP 数据面：登记房间（建房/入房后高频同步走 UDP）
            try { RelayTcp.SetUdpIdentity(_authUid.ToString(), _relayRoomId); RelayTcp.RegisterUdp(_relayRoomId); } catch { }
        }
        else if (t == "room_player_join")
        {
            _relayPlayers = JsonHelper.List(m, "players");
            AddRelayLine("[房间] " + JsonHelper.Str(m, "name") + " 加入");
            _lastRelayAppearanceSig = "";
            _relayGhostIgnoreUntil.Remove(JsonHelper.Str(m, "uid"));
        }
        else if (t == "room_leave")
        {
            AddRelayLine("[房间] " + JsonHelper.Str(m, "name") + " 离开");
            string lu = JsonHelper.Str(m, "uid");
            _relayPlayers.RemoveAll(p => JsonHelper.Str(p, "uid") == lu);
            _relayPositions.Remove(lu);
            _relayGhostLastSeen.Remove(lu);
            _relayGhostIgnoreUntil[lu] = Time.unscaledTime + 8f;
            RemoveRelayGhost(lu);
            // 离开的玩家是我的控制器/被控对象时，立即解除链接，防止"原地卡死"
            if (_toyLinkedController == lu || _toyLinkedTarget == lu)
            {
                _toyLinkedTargets.Remove(lu);
                _toyLinkedTarget = "";
                _toyLinkedController = "";
                _leashOverSince = 0f;
                ResetToyLocal();
                ApplyCrouch(false);
                if (_leashLine != null) _leashLine.enabled = false;
                AddRelayLine("[主仆] " + JsonHelper.Str(m, "name") + " 已离开，控制关系解除");
            }
        }
        else if (t == "room_closed")
        {
            string rid = JsonHelper.Str(m, "room_id");
            AddRelayLine("[房间] 已关闭 " + rid);
            if (_relayRoomId == rid)
            {
                _relayRoomId = "";
                RelayTcp.Send(new Dictionary<string, object> { ["t"] = "room_list" });
            }
        }
        else if(t=="appearance_request"){_lastRelayAppearanceSig="";_stateSyncCount=0;}
        else if(t=="action_sync"){string u=JsonHelper.Str(m,"uid");ApplyRelayAction(u,m);}
        else if(t=="bone_sync"){string u=JsonHelper.Str(m,"uid");ApplyRelayBones(u,m);}
        else if(t=="motion_sync"){string u=JsonHelper.Str(m,"uid");ApplyRelayMotion(u,m);}
        else if (t == "presence")
            _relayOnline = JsonHelper.Int(m, "online");
        else if (t == "kicked")
            AddRelayLine("[系统] 你已被踢出");
        else if (t == "rejected")
        {
            string rejectMessage = JsonHelper.Str(m, "msg");
            string rejectCode = JsonHelper.Str(m, "code");
            AddRelayLine("[拒绝] " + rejectMessage + " (" + rejectCode + ")");
            Toast(rejectMessage.Length > 0 ? rejectMessage : "联机服拒绝连接");
            if (_relayConnecting)
            {
                RelayTcp.Close();
                _relayConnecting = false;
                _relayConnected = false;
            }
        }
        else if (t == "captcha")
        {
            _relayCaptchaTex = LoadCaptchaTexture(JsonHelper.Str(m, "image"));
            _relayCaptchaInput = "";
            if (_relayCaptchaTex != null) AddRelayLine("[验证码] 请输入图中数字");
            else { AddRelayLine("[验证码] 图片加载失败，请点击换一张"); Toast("验证码图片加载失败"); }
        }
        else if (t == "err")
        {
            string code = JsonHelper.Str(m, "code");
            string message = JsonHelper.Str(m, "m");
            if (message.Length == 0) message = JsonHelper.Str(m, "msg");
            if (message.Length == 0) message = code.Length > 0 ? code : "服务器拒绝了请求";
            // err 不再写入聊天框（防止未知消息/旧玩法消息刷屏），仅弹提示
            Toast(message);
            if (code.IndexOf("captcha", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("验证码", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _relayCaptchaTex = null;
                _relayCaptchaInput = "";
                if (message.IndexOf("频繁", StringComparison.OrdinalIgnoreCase) < 0)
                    RelayTcp.Send(new Dictionary<string, object> { ["t"] = "captcha" });
            }
        }
        else if (t == "room_list")
        {
            _relayRooms = JsonHelper.List(m, "rooms");
        }
        else if (t == "kicked_from_room")
        {
            AddRelayLine("[系统] 你已被房主移出房间");
            _relayRoomId = "";
            _relayPlayers = new List<Dictionary<string, object>>();
            RelayTcp.Send(new Dictionary<string, object> { ["t"] = "room_list" });
        }
        else if (t == "pong")
        {
            _relayOnline = JsonHelper.Int(m, "online");
            _relayMaxOnline = JsonHelper.Int(m, "max_online");
            _relayRooms = JsonHelper.List(m, "rooms");
        }
        else if (t == "toy_invite")
        {
            _toyInviteFrom = JsonHelper.Str(m, "from");
            _toyInviteFromName = JsonHelper.Str(m, "from_name");
            AddRelayLine("[玩具] " + _toyInviteFromName + " 请求控制你");
        }
        else if (t == "toy_accepted")
        {
            string accepted = JsonHelper.Str(m, "to");
            AddToyTarget(accepted);
            AddRelayLine("[玩具] 对方已同意");
        }
        else if (t == "toy_rejected")
        {
            AddRelayLine("[玩具] 对方拒绝了控制请求");
        }
        else if (t == "toy_revoked")
        {
            _toyLinkedTarget = "";
            _toyLinkedTargets.Clear();
            _toyLinkedController = "";
            ResetToyLocal();
            AddRelayLine("[玩具] 控制已解除");
        }
        else if (t == "toy_link")
        {
            if (JsonHelper.Str(m, "target") == _authUid.ToString()) _toyLinkedController = JsonHelper.Str(m, "controller");
            if (JsonHelper.Str(m, "controller") == _authUid.ToString()) AddToyTarget(JsonHelper.Str(m, "target"));
        }
        else if (t == "toy_state")
            ApplyToyState(m);
        else if (t == "pos")
        {
            string pu = JsonHelper.Str(m, "uid");
            if (pu.Length > 0 && pu != _authUid.ToString())
                _relayPositions[pu] = new RelayPos { X = (float)JsonHelper.Double(m, "x"), Y = (float)JsonHelper.Double(m, "y"), Z = (float)JsonHelper.Double(m, "z"), RotY = (float)JsonHelper.Double(m, "ry"), Stage = JsonHelper.Int(m, "stage", -1) };
        }
        else if (t == "npc_state")
        {
            int st = JsonHelper.Int(m, "stage", -1);
            string authority = JsonHelper.Str(m, "uid");
            var arr = JsonHelper.List(m, "npcs");
            var pts = new List<Vector3>();
            _syncNpcTargets.Clear();
            _syncNpcVelocity.Clear();
            _syncNpcRotY.Clear();
            _syncNpcMoving.Clear();
            _syncNpcActionHash.Clear();
            int fallbackIndex = 0;
            foreach (var d in arr)
            {
                Vector3 p = new Vector3((float)JsonHelper.Double(d, "x"), (float)JsonHelper.Double(d, "y"), (float)JsonHelper.Double(d, "z"));
                pts.Add(p);
                int index = JsonHelper.Int(d, "i", fallbackIndex);
                if (index >= 0)
                {
                    _syncNpcTargets[index] = p;
                    _syncNpcVelocity[index] = new Vector3((float)JsonHelper.Double(d, "vx"), 0f, (float)JsonHelper.Double(d, "vz"));
                    _syncNpcRotY[index] = (float)JsonHelper.Double(d, "ry");
                    _syncNpcMoving[index] = JsonHelper.Int(d, "moving") != 0;
                    _syncNpcActionHash[index] = JsonHelper.Int(d, "hash");
                }
                fallbackIndex++;
            }
            _syncNpcAuthority = authority;
            _syncNpcStage = st;
            if (st >= 0) _syncNpcs[st] = pts;
        }
        else if (t == "room_settings")
        {
            if (m.ContainsKey("allow_game_bonuses")) _roomAllowGameBonuses = JsonHelper.Int(m, "allow_game_bonuses") != 0;
        }
        else if (t == "time_sync")
        {
            // 时间同步：应用房主的时间（房主权威）
            try
            {
                var gsd = typeof(GameStateData).GetProperty("GameStateData", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.GetValue(null) as GameStateData;
                if (gsd != null) gsd.IsDaytime = JsonHelper.Int(m, "daytime") != 0;
            }
            catch { }
        }

        else if (t == "state_sync")
        {
            string su = JsonHelper.Str(m, "uid");
            if (su.Length > 0 && su != _authUid.ToString())
                ApplyRelayState(su, m);
        }
        else if (t != null && t.StartsWith("ext_"))
        {
            try
            {
                string from = JsonHelper.Str(m, "uid");
                if (from.Length == 0) from = JsonHelper.Str(m, "from");
                SFMOnline.Ext.OnlineCoreExt.HandleExtMessage(t, m, from);
            }
            catch { }
        }
        else
            AddRelayLine(JsonHelper.Str(m, "m"));
    }
    catch { }
}

[HideFromIl2Cpp]
private void DisconnectRelayServer()
{
    RelayTcp.Close();
    _relayConnecting = false;
    _relayConnected = false;
    _relayRoomId = "";
    _relayPlayers = new List<Dictionary<string, object>>();
    _relayRooms = new List<Dictionary<string, object>>();
    _relayChat = new List<string>();
    _relayCaptchaTex = null;
    _relayPositions.Clear();
    _syncNpcTargets.Clear();
    _syncNpcAuthority = "";
    _syncNpcStage = -1;
    foreach (var g in _relayGhosts.Values) { if (g != null && g.Root != null) { try { UnityEngine.Object.Destroy(g.Root); } catch { } } }
    _relayGhosts.Clear();
    _gameRedPlayers.Clear();
    ResetToyLocal();
    Toast("已断开联机服");
}

[HideFromIl2Cpp]
private string FormatLanConnectError(string host, int port, string err)
{
    string low = (err ?? "").ToLowerInvariant();
    string target = port > 0 ? host + ":" + port : host;
    if (low.IndexOf("refused") >= 0 || low.IndexOf("拒绝") >= 0)
        return string.Format(Lang.Get("toast_join_fail"), target + "：目标主动拒绝连接。请检查房主是否已开房间、Windows 防火墙是否放行该端口，或改用“联机服务器”连接。");
    if (low.IndexOf("timed out") >= 0 || low.IndexOf("timeout") >= 0 || low.IndexOf("超时") >= 0)
        return string.Format(Lang.Get("toast_join_fail"), target + "：连接超时。请检查网络、加速器，或改用“联机服务器”连接。");
    return string.Format(Lang.Get("toast_join_fail"), target + " " + err);
}

[HideFromIl2Cpp]
private int RelayMySlot()
{
    int slot = 1;
    if (_relayPlayers != null)
    {
        int idx = _relayPlayers.FindIndex(p => JsonHelper.Str(p, "uid") == _authUid.ToString());
        if (idx >= 0) slot = idx + 1;
    }
    return slot;
}

[HideFromIl2Cpp]
private string RelayConnectHint(string err)
{
    string low = (err ?? "").ToLowerInvariant();
    if (low.IndexOf("refused") >= 0 || low.IndexOf("拒绝") >= 0)
        return "（联机服端口未开放或本机防火墙拦截，请检查网络或稍后重试）";
    if (low.IndexOf("timed out") >= 0 || low.IndexOf("timeout") >= 0 || low.IndexOf("超时") >= 0)
        return "（连接超时，请检查网络或加速器后重试）";
    return "";
}

[HideFromIl2Cpp]
private static string CompTypeName(Component comp)
{
    try
    {
        string n = comp.GetType().FullName;
        if (!string.IsNullOrEmpty(n) && n != "UnityEngine.Component") return n;
    }
    catch { }
    try
    {
        string t = comp.ToString();
        if (!string.IsNullOrEmpty(t))
        {
            int i = t.IndexOf('(');
            if (i > 0) t = t.Substring(0, i).Trim();
            if (t.Length > 0) return t;
        }
    }
    catch { }
    return "Component";
}

[HideFromIl2Cpp]
private void DumpToyControllers()
{
    try
    {
        if (PlayerFacade.Instance == null) { Toast("不在游戏内"); return; }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== SFMOnline 玩具/排尿/水控制器导出 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
        var typeCounts = new Dictionary<string, int>();
        var objNames = new Dictionary<string, int>();
        int objCount = 0;
        Action<Transform> walk = null;
        walk = t =>
        {
            if (t == null) return;
            try
            {
                objCount++;
                string on = t.name ?? "";
                if (on.Length > 0) objNames[on] = objNames.TryGetValue(on, out var ov) ? ov + 1 : 1;
                var comps = t.GetComponents<Component>();
                foreach (var comp in comps)
                {
                    if (comp == null) continue;
                    string tn = CompTypeName(comp);
                    if (tn.Length > 0) typeCounts[tn] = typeCounts.TryGetValue(tn, out var v) ? v + 1 : 1;
                }
                for (int i = 0; i < t.childCount; i++) walk(t.GetChild(i));
            }
            catch { }
        };
        foreach (var rootGo in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            walk(rootGo.transform);
        sb.AppendLine("场景对象总数: " + objCount);
        sb.AppendLine("--- 组件类型 Top 300 ---");
        foreach (var kv in typeCounts.OrderByDescending(p => p.Value).Take(300))
            sb.AppendLine(kv.Value + " x " + kv.Key);
        sb.AppendLine("--- 玩家子物体组件（含关键词，含未启用）---");
        if (PlayerFacade.Instance != null && PlayerFacade.Instance.pca != null)
        {
            sb.AppendLine("玩家对象: " + (PlayerFacade.Instance.pca.GameObject != null ? PlayerFacade.Instance.pca.GameObject.name : "?"));
            foreach (var comp in PlayerFacade.Instance.pca.GameObject.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                string tn = CompTypeName(comp);
                string on = "";
                try { on = comp.gameObject != null ? comp.gameObject.name : ""; } catch { }
                string low = (tn + " " + on).ToLowerInvariant();
                if (low.IndexOf("piston") >= 0 || low.IndexOf("pee") >= 0 || low.IndexOf("urin") >= 0 ||
                    low.IndexOf("water") >= 0 || low.IndexOf("vibrat") >= 0 || low.IndexOf("climax") >= 0 ||
                    low.IndexOf("ecstasy") >= 0 || low.IndexOf("splash") >= 0 || low.IndexOf("rotor") >= 0 ||
                    low.IndexOf("kuri") >= 0 || low.IndexOf("tit") >= 0 || low.IndexOf("handcuff") >= 0 ||
                    low.IndexOf("shio") >= 0 || low.IndexOf("particle") >= 0)
                    sb.AppendLine("组件: " + tn + " | 对象: " + on);
            }
        }
        string path = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "SFMOnline_export.txt");
        System.IO.File.WriteAllText(path, sb.ToString());
        PluginInfo.Info("导出完成: " + path);
        Toast("已导出 BepInEx/SFMOnline_export.txt（" + objCount + " 个对象）");
    }
    catch (Exception ex) { PluginInfo.Warn("控制器导出失败: " + ex); Toast("导出失败"); }
}
[HideFromIl2Cpp]
private void AddRelayLine(string s)
{
    _relayChat.Add(s); if(_relayChat.Count>60)_relayChat.RemoveAt(0); _relayChatScroll.y=float.MaxValue;
}

[HideFromIl2Cpp]
private void AddToyTarget(string uid)
{
    if (uid.Length == 0 || uid == ToySelfId()) return;
    if (!_toyLinkedTargets.Contains(uid)) _toyLinkedTargets.Add(uid);
    _toyLinkedTarget = uid;
}

// ========== Ext 玩法控制（模组远程玩法接口：ext_play 通道） ==========
/// <summary>本地立即执行玩法命令（d 为 ApplyToyState 命令集，见 ApplyToyState）。</summary>
[HideFromIl2Cpp]
public bool ExtPlayLocal(string d, int act = 0, int stage = 0, int mode = 0, bool on = false)
{
    if (d.Length == 0) return false;
    try
    {
        ApplyToyState(new Dictionary<string, object>
        {
            ["from"] = "ext", ["d"] = d, ["act"] = act, ["stage"] = stage,
            ["mode"] = mode, ["type"] = act, ["duration"] = mode, ["on"] = on
        });
        return true;
    }
    catch { return false; }
}

/// <summary>发送玩法命令到远端玩家（直连/relay 双模式，经 ext_play 通道）。</summary>
[HideFromIl2Cpp]
public void ExtPlayRemote(string targetUid, string d, int act = 0, int stage = 0, int mode = 0, bool on = false)
{
    if (d.Length == 0 || targetUid.Length == 0 || targetUid == "self") return;
    try
    {
        var payload = new Dictionary<string, object>
        {
            ["t"] = "ext_play", ["to"] = targetUid, ["d"] = d,
            ["act"] = act, ["stage"] = stage, ["mode"] = mode, ["on"] = on
        };
        if (_relayMode) RelayTcp.Send(payload);
        else SendExtDirect(payload, targetUid);
    }
    catch { }
}

/// <summary>广播玩法命令到全房间（relay 经服务器转发；直连经房主转发）。</summary>
[HideFromIl2Cpp]
public void ExtPlayBroadcast(string d, int act = 0, int stage = 0, int mode = 0, bool on = false)
{
    if (d.Length == 0) return;
    try
    {
        var payload = new Dictionary<string, object>
        {
            ["t"] = "ext_play", ["to"] = "", ["d"] = d,
            ["act"] = act, ["stage"] = stage, ["mode"] = mode, ["on"] = on
        };
        if (_relayMode) RelayTcp.Send(payload);
        else SendExtDirect(payload, "");
    }
    catch { }
}

/// <summary>组合入口：uid 为空或 "self" 本地执行；"*" 广播；其它定向发送。</summary>
[HideFromIl2Cpp]
public bool ExtPlay(string targetUid, string d, int act = 0, int stage = 0, int mode = 0, bool on = false)
{
    if (d.Length == 0) return false;
    if (targetUid.Length == 0 || targetUid == "self") return ExtPlayLocal(d, act, stage, mode, on);
    if (targetUid == "*") { ExtPlayBroadcast(d, act, stage, mode, on); return true; }
    if (targetUid == PeerId || targetUid == ToySelfId()) return ExtPlayLocal(d, act, stage, mode, on);
    ExtPlayRemote(targetUid, d, act, stage, mode, on);
    return true;
}

/// <summary>传送指定玩家到坐标（经 ext_tp 通道，带坐标）。</summary>
[HideFromIl2Cpp]
public void ExtTeleportRemote(string targetUid, float x, float y, float z)
{
    if (targetUid.Length == 0) return;
    try
    {
        var payload = new Dictionary<string, object> { ["t"] = "ext_tp", ["to"] = targetUid, ["x"] = x, ["y"] = y, ["z"] = z };
        if (_relayMode) RelayTcp.Send(payload);
        else SendExtDirect(payload, targetUid);
    }
    catch { }
}

/// <summary>广播传送全员（带坐标）。</summary>
[HideFromIl2Cpp]
public void ExtTeleportBroadcast(float x, float y, float z)
{
    try
    {
        var payload = new Dictionary<string, object> { ["t"] = "ext_tp", ["to"] = "", ["x"] = x, ["y"] = y, ["z"] = z };
        if (_relayMode) RelayTcp.Send(payload);
        else SendExtDirect(payload, "");
    }
    catch { }
}

[HideFromIl2Cpp]
private string ToySelfId()
{
    return _relayConnected && _relayRoomId.Length > 0 ? _authUid.ToString() : PeerId;
}

/// <summary>Ext 用的公开自我 ID（relay/直连兼容）。</summary>
[HideFromIl2Cpp]
public string ToySelfIdPublic() => ToySelfId();

[HideFromIl2Cpp]
private void SendDirectControl(string kind, string target, string command = "", int intArg = 0, bool boolArg = false)
{
    if (!Connected) return;
    var w = new WireWriter();
    w.WriteString(PeerId);
    w.WriteString(target ?? "");
    w.WriteString(kind ?? "");
    w.WriteString(command ?? "");
    w.WriteInt(intArg);
    w.WriteBool(boolArg);
    Send(MsgTypes.Control, w.ToArray());
}

[HideFromIl2Cpp]
private void HandleDirectControl(NetMsg msg)
{
    var r = new WireReader(msg.Payload);
    string sender = r.ReadString();
    string target = r.ReadString();
    if (IsHosting && !string.IsNullOrEmpty(msg.SourceId) && msg.SourceId != "server") sender = msg.SourceId;
    string kind = r.ReadString();
    string command = r.ReadString();
    int intArg = r.ReadInt();
    bool boolArg = r.ReadBool();
    if (sender.Length == 0 || sender == PeerId) return;
    if (target.Length > 0 && target != PeerId) return;

    if (kind == "ext")
    {
        // 直连模式 Ext 通道：解包 JSON 分发给扩展框架
        try
        {
            var obj = MiniJson.ParseObject(command);
            if (obj != null)
            {
                string t2 = JsonHelper.Str(obj, "t");
                if (t2 != null && t2.StartsWith("ext_"))
                {
                    string from = sender;
                    // 房主模式下：客户端发来的消息也要分发给房主本地插件 + 广播给其它客户端
                    if (IsHosting)
                    {
                        try { SFMOnline.Ext.OnlineCoreExt.HandleExtMessage(t2, obj, from); } catch { }
                        // 房主转发给其它客户端（除发送者）
                        var fw = new WireWriter();
                        fw.WriteString(PeerId);
                        fw.WriteString("");
                        fw.WriteString("ext");
                        fw.WriteString(command);
                        fw.WriteInt(0);
                        fw.WriteBool(false);
                        _host.SendToClients(MsgTypes.Control, fw.ToArray(), msg.SourceId);
                    }
                    else
                    {
                        try { SFMOnline.Ext.OnlineCoreExt.HandleExtMessage(t2, obj, from); } catch { }
                    }
                }
            }
        }
        catch { }
        return;
    }

    if (kind == "invite")
    {
        _toyInviteFrom = sender;
        _toyInviteFromName = GetPeerName(sender);
        AddChat("[控制] " + _toyInviteFromName + " 请求控制你");
        return;
    }
    if (kind == "accept")
    {
        AddToyTarget(sender);
        AddChat("[控制] " + GetPeerName(sender) + " 已同意");
        return;
    }
    if (kind == "reject")
    {
        AddChat("[控制] " + GetPeerName(sender) + " 已拒绝");
        return;
    }
    if (kind == "revoke")
    {
        _toyLinkedTargets.Remove(sender);
        if (_toyLinkedTarget == sender) _toyLinkedTarget = "";
        if (_toyLinkedController == sender)
        {
            _toyLinkedController = "";
            ResetToyLocal();
        }
        AddChat("[控制] 与 " + GetPeerName(sender) + " 的控制关系已解除");
        return;
    }
    if (kind == "setting")
    {
        if (_peers.TryGetValue(sender, out var owner) && owner.IsHost)
            _roomAllowGameBonuses = intArg != 0;
        return;
    }
    if (kind != "control" || _toyLinkedController != sender) return;
    ApplyToyState(new Dictionary<string, object>
    {
        ["from"] = sender,
        ["d"] = command,
        ["act"] = intArg,
        ["stage"] = intArg,
        ["mode"] = intArg,
        ["type"] = intArg,
        ["duration"] = 0,
        ["on"] = boolArg
    });
}

[HideFromIl2Cpp]
private void SendToyAll(string d)
{
    foreach (var t in _toyLinkedTargets)
        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = t, ["d"] = d });
}

[HideFromIl2Cpp]
private void ApplyToyState(Dictionary<string, object> m)
{
    string from = JsonHelper.Str(m, "from");
    string d = JsonHelper.Str(m, "d");
    string target = JsonHelper.Str(m, "target");
    bool isFx = d == "fx";
    if (from == ToySelfId() && !isFx) return;
    if (isFx)
    {
        string kind = JsonHelper.Str(m, "kind");
        if (kind.Length == 0) kind = "shiofuki";
        int fxMode = JsonHelper.Int(m, "mode", 1);
        string fxName = kind == "pee" ? "排尿" : "强制高潮";
        string fxLine = "[控制] " + fxName + (fxMode >= 2 ? "（持续）" : "（一次）");
        if (Connected && !_relayConnected) AddChat(fxLine); else AddRelayLine(fxLine);
        if (target.Length == 0 || target == ToySelfId()) ApplyLocalFx(kind, fxMode);
        else PlayGhostFx(target, kind, fxMode);
        return;
    }
    string controlLine = "[控制] " + ToyCommandLabel(d) + (d == "vibrate" ? " 档位" + JsonHelper.Int(m, "stage") : (d == "action" ? " " + ActionLabel(JsonHelper.Int(m, "act")) : ""));
    if (Connected && !_relayConnected) AddChat(controlLine); else AddRelayLine(controlLine);
    if (!InGame) return;
    if (target.Length > 0 && target != ToySelfId()) return;
    try
    {
        switch (d)
        {
            case "vibrate": ApplyVibrate(JsonHelper.Int(m, "stage")); break;
            case "thrust": ApplyThrust(); break;
            case "thrust_set": ApplyThrustSet(JsonHelper.Int(m, "stage")); break;
            case "goods": ApplyRemoteGoods(JsonHelper.Int(m, "type")); break;
            case "action": ApplyRemoteAction(JsonHelper.Int(m, "act")); break;
            case "pee": ApplyForcePee(JsonHelper.Int(m, "mode")); break;
            case "pee_stop": ApplyForcePeeStop(); break;
            case "ecstasy": ApplyForceEcstasy(); break;
            case "finger": ApplyFingerRemote(m); break;
            case "finger_pleasure": ApplyFingerPleasure(); break;
            case "reset_all": ApplyResetAll(); break;
            case "handcuff": ApplyHandcuff(JsonHelper.Int(m, "mode"), JsonHelper.Int(m, "duration")); break;
            case "undress_cycle": ApplyUndressCycle(); break;
            case "undress_reset": ApplyUndressReset(); break;
            case "bareta": ApplyBareta(JsonHelper.Bool(m, "on")); break;
            case "sit_toggle": ApplySitToggle(); break;
            case "goods_off": ApplyRemoteGoodsOff(JsonHelper.Int(m, "type")); break;
            case "unlock": ApplyUnlockHandcuff(); break;
            case "undress": ApplyUndress(JsonHelper.Int(m, "stage")); break;
            case "climax": ApplyClimax(JsonHelper.Bool(m, "on")); break;
            case "crouch": ApplyCrouch(JsonHelper.Bool(m, "on")); break;
            case "collar": ApplyCollar(true); break;
            case "uncollar": ApplyCollar(false); break;
            case "crawl": ApplyCrouch(true); break;
            case "stand": ApplyCrouch(false); break;
            case "pleasure": ApplyPleasure(); break;
            case "handcuff_back": ApplyHandcuffBack(m); break;
        }
    }
    catch (Exception ex)
    {
        if (Time.unscaledTime - _lastToyErrorLogAt > 5f)
        {
            _lastToyErrorLogAt = Time.unscaledTime;
            PluginInfo.Warn("玩具执行失败 d=" + d + ": " + ex.Message);
        }
    }
}
[HideFromIl2Cpp]
private void ApplyVibrate(int stage)
{
    var pf = PlayerFacade.Instance;
    if (pf == null) return;
    EnsureGoodsRecord(MAdultGoodsType.Vibrator);
    bool vibOk = true;
    try { pf.ForceChangeAdultGoods(MAdultGoodsType.Vibrator, stage > 0); } catch { vibOk = false; }
    if (!vibOk && stage > 0)
        try { pf.TransAction(ActionType.SwitchVibrator); } catch { }
    SetGoodsVisual(MAdultGoodsType.Vibrator, stage > 0);
    VibrationModeType mode = stage == 3 ? VibrationModeType.Random :
        stage == 2 ? VibrationModeType.High :
        stage == 1 ? VibrationModeType.Low : VibrationModeType.Off;
    try { CommonVibratorController.ForceSetVibrationMode(mode); } catch { }
    SetToyObjectsActive(new[] { "vibrat" }, stage > 0);
}

[HideFromIl2Cpp]
private void ApplyThrustSet(int stage)
{
    var pf = PlayerFacade.Instance;
    if (pf == null || pf.pca == null) return;
    if (stage < 0) stage = 0;
    if (stage > 3) stage = 3;
    try
    {
        var pm = pf.pca.PistonMachineController;
        if (pm != null)
        {
            try { pm.CurrentSpeedType = stage; } catch { }
            try { pm.isRandomMode = stage >= 3; } catch { }
            foreach (var an in new[] { pm.animator, pm.analAnimator, pm.pussyAnimator })
                if (an != null) { try { an.SetFloat(Animator.StringToHash("MoveSpeed"), stage); } catch { } }
            try { pm.OnChangeSwitch(); } catch { }
            try { pm.Apply(); } catch { }
            try { pm.OnUpdate(); } catch { }
            try { if (pm.onahoObject != null) pm.onahoObject.SetActive(stage > 0); } catch { }
            try { if (pm.analPistonObject != null) pm.analPistonObject.SetActive(stage > 0); } catch { }
            try { if (pm.pussyPistonObject != null) pm.pussyPistonObject.SetActive(stage > 0); } catch { }
        }
    }
    catch { }
    SetToyObjectsActive(new[] { "piston" }, stage > 0);
    bool on = stage > 0;
    EnsureGoodsRecord(MAdultGoodsType.PistonPussy);
    EnsureGoodsRecord(MAdultGoodsType.PistonAnal);
    EnsureGoodsRecord(MAdultGoodsType.PistonFuta);
    bool pistonOk = true;
    try { pf.ForceChangeAdultGoods(MAdultGoodsType.PistonPussy, on); } catch { pistonOk = false; }
    try { pf.ForceChangeAdultGoods(MAdultGoodsType.PistonAnal, on); } catch { }
    try { pf.ForceChangeAdultGoods(MAdultGoodsType.PistonFuta, on); } catch { }
    if (!pistonOk && on)
        try { pf.TransAction(ActionType.SwitchPistonMachine); } catch { }
}

[HideFromIl2Cpp]
private void ApplyForcePee(int mode)
{
    var pf = PlayerFacade.Instance;
    if (pf == null || pf.pca == null) return;
    try
    {
        var pee = pf.pca.PlayerPeeController;
        if (pee == null) return;
        if (mode == 2)
        {
            try { pee.peeRemainTime = 99999f; } catch { }
            try { pee.reduceMoistureSpeed = 0f; } catch { }
        }
        pee.StartPee();
        try
        {
            var mgr = PeeDecalManager.Instance;
            if (mgr != null && PlayerFacade.Instance.pca != null)
                mgr.CreatePeeDecal(PlayerFacade.Instance.pca.AvatorTransform.position + Vector3.down * 0.1f, Vector3.up);
        }
        catch { }
    }
    catch { }
}

[HideFromIl2Cpp]
private void ApplyForceShiofuki()
{
    var pf = PlayerFacade.Instance;
    if (pf == null || pf.pca == null) return;
    try
    {
        var sh = pf.pca.PlayerShiofukiController;
        if (sh != null) sh.EmitShiofuki();
    }
    catch { }
}

[HideFromIl2Cpp]
private void ApplyForceEcstasy()
{
    var pf = PlayerFacade.Instance;
    if (pf == null || pf.pca == null) return;
    try { pf.pca.PlayerAnimationManager.SetEcstasy(true); } catch { }
    ApplyForceShiofuki();
}

[HideFromIl2Cpp]
private void ApplyLocalFx(string kind, int mode)
{
    if (kind == "pee") ApplyForcePee(mode);
    else ApplyForceEcstasy();
}

[HideFromIl2Cpp]
private void SendToyFx(string target, string kind, int mode)
{
    if (target.Length == 0) return;
    if (_relayConnected && _relayRoomId.Length > 0)
        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = target, ["d"] = "fx", ["kind"] = kind, ["mode"] = mode });
    else
    {
        SendDirectControl("control", target, kind == "pee" ? "pee" : "ecstasy", mode, false);
        PlayGhostFx(target, kind, mode);
    }
}

[HideFromIl2Cpp]
private void PlayGhostFx(string uid, string kind, int mode)
{
    GhostPlayer g = null;
    if (!_relayGhosts.TryGetValue(uid, out g) || g == null)
        _ghosts.TryGetValue(uid, out g);
    if (g == null || g.Root == null) return;
    try { g.PlayFx(kind, mode); } catch { }
}

[HideFromIl2Cpp]
private void SendToyAction(string target, int act, bool relayMode)
{
    if (target.Length == 0 || act < 0) return;
    if (relayMode)
        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = target, ["d"] = "action", ["act"] = act });
    else
        SendDirectControl("control", target, "action", act, false);
}

[HideFromIl2Cpp]
private void DrawToyActionSection(float col, float h, float step, bool relayMode)
{
    try
    {
        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), _toyActionExpanded ? "动作列表：收缩" : "动作列表：展开（全部动作）"))
            _toyActionExpanded = !_toyActionExpanded;
        _uiY += step;
        if (!_toyActionExpanded) return;
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, 86f, h), "动作ID");
        _toyActionCustom = UiTextField("toy_action_custom", new Rect(_uiX + 90f, _uiY, 84f, h), _toyActionCustom, false, out _);
        if (_canButton && SButton(new Rect(_uiX + 182f, _uiY, col - 182f, h), "发送该动作"))
        {
            if (int.TryParse(_toyActionCustom.Trim(), out int customAct) && customAct >= 0)
                SendToyAction(_toyLinkedTarget, customAct, relayMode);
            else Toast("请输入有效动作ID");
        }
        _uiY += step;
        float listH = Mathf.Min(200f, 30f * 8f + 8f);
        float innerW = Mathf.Max(80f, _uiW - 22f);
        int perRow = 4;
        float bw = Mathf.Max(50f, (innerW - 3f * 4f) / perRow);
        float contentH = ((KnownActionIds.Length + perRow - 1) / perRow) * (h + 2f) + 4f;
        _toyActionScroll = GUI.BeginScrollView(new Rect(_uiX, _uiY, _uiW, listH), _toyActionScroll, new Rect(0f, 0f, innerW, contentH));
        for (int i = 0; i < KnownActionIds.Length; i++)
        {
            int ac = KnownActionIds[i];
            int r = i / perRow;
            int c = i % perRow;
            if (_canButton && SButton(new Rect(c * (bw + 4f), r * (h + 2f), bw, h), ActionLabel(ac)))
                SendToyAction(_toyLinkedTarget, ac, relayMode);
        }
        GUI.EndScrollView();
        _uiY += listH + 6f;
    }
    catch (Exception ex)
    {
        if (Time.unscaledTime - _lastToyErrorLogAt > 5f)
        {
            _lastToyErrorLogAt = Time.unscaledTime;
            PluginInfo.Warn("动作列表绘制异常: " + ex.Message);
        }
    }
}

[HideFromIl2Cpp]
private void ApplyRemoteGoods(int type)
{
    var pf = PlayerFacade.Instance;
    if (pf == null || type < 0) return;
    var t = (MAdultGoodsType)type;
    EnsureGoodsRecord(t);
    try { pf.ForceChangeAdultGoods(t, true); } catch { }
    SetGoodsVisual(t, true);
    try
    {
        if (t == MAdultGoodsType.Handcuff || t == MAdultGoodsType.KeyHandcuff || t == MAdultGoodsType.TimerHandcuff) pf.TransAction(ActionType.AttachHandcuffs);
        else if (t == MAdultGoodsType.EyeMask) pf.TransAction(ActionType.AttachEyeMask);
        else if (t == MAdultGoodsType.AnalPlug) pf.TransAction(ActionType.InsertAnalPlug);
        else if (t == MAdultGoodsType.Vibrator) pf.TransAction(ActionType.SwitchVibrator);
    }
    catch { }
    string[] kws = t == MAdultGoodsType.AnalPlug ? new[] { "analplug", "anal_plug", "anal plug" } :
        t == MAdultGoodsType.TitRotor ? new[] { "tit", "nipple", "chikubi" } :
        t == MAdultGoodsType.KuriRotor ? new[] { "kuri", "clit" } :
        t == MAdultGoodsType.Vibrator ? new[] { "vibrat" } :
        (t == MAdultGoodsType.PistonPussy || t == MAdultGoodsType.PistonAnal || t == MAdultGoodsType.PistonFuta) ? new[] { "piston" } : null;
    if (kws != null) SetToyObjectsActive(kws, true);
}

[HideFromIl2Cpp]
private void ApplyRemoteGoodsOff(int type)
{
    var pf = PlayerFacade.Instance;
    if (pf == null || type < 0) return;
    var t = (MAdultGoodsType)type;
    EnsureGoodsRecord(t);
    try { pf.ForceChangeAdultGoods(t, false); } catch { }
    SetGoodsVisual(t, false);
    try
    {
        if (t == MAdultGoodsType.Handcuff || t == MAdultGoodsType.KeyHandcuff || t == MAdultGoodsType.TimerHandcuff) pf.TransAction(ActionType.UnlockHandcuffsAtMap);
        else if (t == MAdultGoodsType.AnalPlug) pf.TransAction(ActionType.ExtractAnalPlug);
        else if (t == MAdultGoodsType.Vibrator)
        {
            try { CommonVibratorController.ForceSetVibrationMode(VibrationModeType.Off); } catch { }
        }
    }
    catch { }
    string[] kws2 = t == MAdultGoodsType.AnalPlug ? new[] { "analplug", "anal_plug", "anal plug" } :
        t == MAdultGoodsType.TitRotor ? new[] { "tit", "nipple", "chikubi" } :
        t == MAdultGoodsType.KuriRotor ? new[] { "kuri", "clit" } :
        t == MAdultGoodsType.Vibrator ? new[] { "vibrat" } :
        (t == MAdultGoodsType.PistonPussy || t == MAdultGoodsType.PistonAnal || t == MAdultGoodsType.PistonFuta) ? new[] { "piston" } : null;
    if (kws2 != null) SetToyObjectsActive(kws2, false);
}

[HideFromIl2Cpp]
private static void EnsureGoodsRecord(MAdultGoodsType type)
{
    try
    {
        var pf = PlayerFacade.Instance;
        if (pf == null || pf.pca == null) return;
        var ps = pf.pca.PlayerState;
        if (ps != null && ps.EquippingAdultGoods == null)
        {
            var rec = MAdultGoods.Get(type);
            if (rec != null) ps.EquippingAdultGoods = rec;
        }
    }
    catch { }
}

[HideFromIl2Cpp]
private void SetGoodsVisual(MAdultGoodsType type, bool on)
{
    try
    {
        var r = PlayerFacade.Instance.pca.PlayerAvatarObjectReferencer;
        if (r == null) return;
        if (type == MAdultGoodsType.Vibrator && r.VibratorRemocon != null) r.VibratorRemocon.SetActive(on);
        if ((type == MAdultGoodsType.Handcuff || type == MAdultGoodsType.KeyHandcuff || type == MAdultGoodsType.TimerHandcuff) && r.Handcuffs != null) r.Handcuffs.SetActive(on);
    }
    catch { }
}

[HideFromIl2Cpp]
private void ApplyRemoteAction(int act)
{
    var pf = PlayerFacade.Instance;
    if (pf == null || act < 0) return;
    try { pf.TransAction((ActionType)act); } catch { }
}


[HideFromIl2Cpp]
private void ApplyThrust()
{
    PlayerFacade.Instance.ForceChangeAdultGoods(MAdultGoodsType.PistonPussy, true);
}

[HideFromIl2Cpp]
private void ApplyHandcuff(int mode, int duration)
{
    var pf = PlayerFacade.Instance;
    if (pf == null) return;
    MAdultGoodsType t = mode == 0 ? MAdultGoodsType.Handcuff :
        mode == 1 ? MAdultGoodsType.KeyHandcuff : MAdultGoodsType.TimerHandcuff;
    try { pf.TransAction(ActionType.AttachHandcuffs); } catch { }
    EnsureGoodsRecord(t);
    try { pf.ForceChangeAdultGoods(t, true); } catch { }
    SetGoodsVisual(t, true);
}

[HideFromIl2Cpp]
private void ApplyResetAll()
{
    try
    {
        _forceClimax = false;
        _forceCrouch = false;
        _forceFollow = false;
        _followTargetUid = "";
        _fingerActive = false;
        _fingerInfinite = false;
        _beingFingered = false;
        ResetToyLocal();
        ApplyUndressReset();
        ApplyForcePeeStop();
        ApplyBareta(false);
        Toast("已强制恢复所有控制状态");
    }
    catch { }
}

// ========== 掉落道具同步（v1.0.10） ==========
        // 轮询游戏内掉落物（DroppedItemController），检测新增 → 广播；接收 → 标记；拾取 → 裁决
        [HideFromIl2Cpp]
        private void UpdateDropSync()
        {
            if (!_relayConnected || _relayRoomId.Length == 0 || !InGame) return;
            float now = Time.unscaledTime;
            // 1) 扫描本地掉落物（新增广播）
            if (now - _lastDropScanAt >= 0.5f)
            {
                _lastDropScanAt = now;
                try { ScanLocalDrops(); } catch { }
            }
            // 2) 定期广播拾取权限
            if (now - _lastDropPermBroadcastAt >= 3f)
            {
                _lastDropPermBroadcastAt = now;
                RelayTcp.Send(new Dictionary<string, object>
                {
                    ["t"] = "ext_item_perm", ["allow"] = _dropAllowOthers ? 1 : 0,
                    ["uids"] = new List<object>(_dropAllowUids)
                });
            }
            // 3) 渲染远程掉落标记（名字标签）
            foreach (var kv in _remoteDrops)
            {
                var rd = kv.Value;
                if (rd.Marker == null) rd.Marker = CreateDropMarker(rd);
                if (rd.Marker != null)
                    rd.Marker.transform.position = rd.Pos + Vector3.up * 0.9f;
            }
            // 4) 交互拾取检测（靠近远程标记按 F）
            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E))
            {
                TryPickRemoteDrop();
            }
        }

        [HideFromIl2Cpp]
        private void ScanLocalDrops()
        {
            try
            {
                var controllers = UnityEngine.Object.FindObjectsOfType<DroppedItemController>();
                if (controllers == null) return;
                var sb = new System.Text.StringBuilder();
                foreach (var dc in controllers)
                {
                    if (dc == null) continue;
                    string type = "";
                    try { type = dc.type.ToString(); } catch { }
                    if (type.Length == 0 || type == "None") continue;
                    var pos = dc.transform.position;
                    sb.Append(type).Append('@').Append(Mathf.RoundToInt(pos.x)).Append(',').Append(Mathf.RoundToInt(pos.y)).Append(',').Append(Mathf.RoundToInt(pos.z)).Append(';');
                }
                string sig = sb.ToString();
                if (sig == _dropSig) return;
                _dropSig = sig;
                // 广播本次扫描到的掉落物（去重：同类型同位置只发一次）
                if (Time.unscaledTime - _lastDropBroadcastAt >= 0.8f)
                {
                    _lastDropBroadcastAt = Time.unscaledTime;
                    var seen = new HashSet<string>();
                    foreach (var dc in controllers)
                    {
                        if (dc == null) continue;
                        string type = "";
                        try { type = dc.type.ToString(); } catch { }
                        if (type.Length == 0 || type == "None") continue;
                        var pos = dc.transform.position;
                        string key = type + "@" + Mathf.RoundToInt(pos.x) + "," + Mathf.RoundToInt(pos.y) + "," + Mathf.RoundToInt(pos.z);
                        if (!seen.Add(key)) continue;
                        RelayTcp.Send(new Dictionary<string, object>
                        {
                            ["t"] = "ext_item_drop", ["type"] = type,
                            ["x"] = (float)Math.Round(pos.x, 2), ["y"] = (float)Math.Round(pos.y, 2), ["z"] = (float)Math.Round(pos.z, 2)
                        });
                    }
                }
            }
            catch { }
        }

        [HideFromIl2Cpp]
        private UnityEngine.GameObject CreateDropMarker(RemoteDrop rd)
        {
            try
            {
                var go = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cylinder);
                go.name = "SFM_RemoteDrop_" + rd.Type;
                go.transform.localScale = new Vector3(0.5f, 0.08f, 0.5f);
                var r = go.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(1f, 0.85f, 0.2f, 0.85f);
                var collider = go.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                // 名字标签（世界标记）
                SFMOnline.Ext.SfmExtHud.CreateMarker("drop_" + rd.Type + "_" + rd.Owner, rd.Pos + Vector3.up * 1.1f,
                    (rd.OwnerName.Length > 0 ? rd.OwnerName + " - " : "") + DropTypeName(rd.Type));
                return go;
            }
            catch { return null; }
        }

        [HideFromIl2Cpp]
        private void TryPickRemoteDrop()
        {
            try
            {
                if (_remoteDrops.Count == 0) return;
                var self = PlayerFacade.Instance.pca.AvatorTransform.position;
                string bestKey = null;
                float bestD = 2.5f;
                foreach (var kv in _remoteDrops)
                {
                    float d = Vector3.Distance(self, kv.Value.Pos);
                    if (d <= bestD) { bestD = d; bestKey = kv.Key; }
                }
                if (bestKey == null) return;
                var rd = _remoteDrops[bestKey];
                // 请求拾取（服务器裁决权限）
                RelayTcp.Send(new Dictionary<string, object>
                {
                    ["t"] = "ext_item_pick", ["type"] = rd.Type, ["owner"] = rd.Owner,
                    ["x"] = rd.Pos.x, ["y"] = rd.Pos.y, ["z"] = rd.Pos.z
                });
            }
            catch { }
        }

        [HideFromIl2Cpp]
        private static string DropTypeName(string type)
        {
            switch (type)
            {
                case "Coat": return "外套";
                case "Hoodie": return "卫衣";
                case "Pants": return "内裤";
                case "Bra": return "胸罩";
                case "HandcuffKey": return "手铐钥匙";
                case "VibeRemocon": return "振动遥控";
                case "DildoFloor": return "跳蛋(地)";
                case "DildoWall": return "跳蛋(墙)";
                case "Basket": return "篮子";
                default: return type;
            }
        }

        // 处理远程掉落消息（在 HandleRelayLine 调用）
        [HideFromIl2Cpp]
        private void HandleRemoteDrop(string t, Dictionary<string, object> m)
        {
            try
            {
                if (t == "ext_item_drop")
                {
                    string type = JsonHelper.Str(m, "type");
                    string owner = JsonHelper.Str(m, "uid");
                    string ownerName = JsonHelper.Str(m, "name");
                    if (type.Length == 0 || owner.Length == 0 || owner == _authUid.ToString()) return;
                    var pos = new Vector3((float)JsonHelper.Double(m, "x"), (float)JsonHelper.Double(m, "y"), (float)JsonHelper.Double(m, "z"));
                    string key = type + "@" + owner;
                    // 更新或新建
                    if (_remoteDrops.TryGetValue(key, out var rd))
                    {
                        rd.Pos = pos;
                        rd.CreatedAt = Time.unscaledTime;
                    }
                    else
                    {
                        _remoteDrops[key] = new RemoteDrop { Type = type, Owner = owner, OwnerName = ownerName, Pos = pos, CreatedAt = Time.unscaledTime };
                        AddRelayLine("[道具] " + (ownerName.Length > 0 ? ownerName : owner) + " 掉落了 " + DropTypeName(type));
                    }
                }
                else if (t == "ext_item_perm")
                {
                    // 服务器广播的拾取权限（仅房主/归属者设置时有用；此处记录他人设置用于提示）
                }
                else if (t == "ext_item_pick_ok")
                {
                    // 拾取成功：本地 Collect 该类型
                    string type = JsonHelper.Str(m, "type");
                    string owner = JsonHelper.Str(m, "owner");
                    if (type.Length > 0)
                    {
                        try
                        {
                            var dim = DroppedItemManager.Instance;
                            if (dim != null)
                            {
                                // DropItemType 为游戏内部枚举，经字符串反射调用 Collect
                                var mi = dim.GetType().GetMethod("Collect");
                                if (mi != null)
                                {
                                    var enumType = mi.GetParameters().Length > 0 ? mi.GetParameters()[0].ParameterType : null;
                                    if (enumType != null && enumType.IsEnum)
                                        mi.Invoke(dim, new object[] { Enum.Parse(enumType, type, true) });
                                }
                            }
                        }
                        catch { }
                        string key = type + "@" + owner;
                        if (key != type + "@" + _authUid.ToString())
                        {
                            if (_remoteDrops.TryGetValue(key, out var rd))
                            {
                                if (rd.Marker != null) UnityEngine.Object.Destroy(rd.Marker);
                                SFMOnline.Ext.SfmExtHud.RemoveText("drop_" + type + "_" + owner);
                                _remoteDrops.Remove(key);
                            }
                        }
                        AddRelayLine("[道具] 拾取了 " + DropTypeName(type));
                    }
                }
                else if (t == "ext_item_pick_deny")
                {
                    string msg = JsonHelper.Str(m, "m");
                    AddRelayLine("[道具] " + (msg.Length > 0 ? msg : "无权拾取该道具"));
                }
                else if (t == "ext_item_collect_all")
                {
                    // 某玩家回收了全部道具
                    string owner = JsonHelper.Str(m, "uid");
                    string ownerName = JsonHelper.Str(m, "name");
                    if (owner.Length > 0)
                    {
                        foreach (var key in new List<string>(_remoteDrops.Keys))
                        {
                            if (_remoteDrops[key].Owner == owner)
                            {
                                if (_remoteDrops[key].Marker != null) UnityEngine.Object.Destroy(_remoteDrops[key].Marker);
                                SFMOnline.Ext.SfmExtHud.RemoveText("drop_" + _remoteDrops[key].Type + "_" + owner);
                                _remoteDrops.Remove(key);
                            }
                        }
                        AddRelayLine("[道具] " + (ownerName.Length > 0 ? ownerName : owner) + " 回收了所有掉落道具");
                    }
                }
            }
            catch (Exception ex) { PluginInfo.Warn("drop sync: " + ex.Message); }
        }

        // F1：回收自己的全部掉落道具
        [HideFromIl2Cpp]
        private void CollectMyDrops()
        {
            try
            {
                var dim = DroppedItemManager.Instance;
                if (dim != null) dim.CollectAll();
                RelayTcp.Send(new Dictionary<string, object> { ["t"] = "ext_item_collect_all" });
                AddRelayLine("[道具] 已回收全部掉落道具");
                _dropSig = "";
            }
            catch (Exception ex) { PluginInfo.Warn("collect all: " + ex.Message); }
        }

// 模组同步完成后重进游戏：优先用游戏系统菜单的回标题入口（反射探测），
        // 找不到就提示玩家手动重进（模组 DLL 已热加载，重进后正式生效）
        [HideFromIl2Cpp]
        private void ReturnToTitle()
        {
            try
            {
                var mgr = InGameManager.Instance;
                if (mgr != null)
                {
                    foreach (var mn in new[] { "ReturnTitle", "BackToTitle", "GoTitle", "LeaveGame", "EndGame", "QuitGame" })
                    {
                        try
                        {
                            var mi = mgr.GetType().GetMethod(mn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (mi != null) { mi.Invoke(mgr, null); return; }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            try
            {
                var scenes = new[] { "Title", "TitleScene", "00_Title", "MainMenu" };
                foreach (var s in scenes)
                {
                    try { UnityEngine.SceneManagement.SceneManager.LoadScene(s); return; }
                    catch { }
                }
            }
            catch { }
            Toast("模组已更新，请按 Esc 回主菜单后重新进入游戏");
        }

        [HideFromIl2Cpp]
        private void ForceResetAll()
{
    ApplyResetAll();
    if (!_relayConnected && !Connected) return;
    try
    {
        foreach (var t in _toyLinkedTargets)
        {
            if (_relayConnected && _relayRoomId.Length > 0)
                RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = t, ["d"] = "reset_all" });
            else
                SendDirectControl("control", t, "reset_all", 0, false);
        }
        if (_toyLinkedController.Length > 0)
        {
            if (_relayConnected && _relayRoomId.Length > 0)
                RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = _toyLinkedController, ["d"] = "reset_all" });
            else
                SendDirectControl("control", _toyLinkedController, "reset_all", 0, false);
        }
        _toyLinkedTargets.Clear();
        _toyLinkedTarget = "";
        _toyLinkedController = "";
    }
    catch { }
}

[HideFromIl2Cpp]
private void ApplyForcePeeStop()
{
    var pf = PlayerFacade.Instance;
    if (pf == null || pf.pca == null) return;
    try
    {
        var pee = pf.pca.PlayerPeeController;
        if (pee == null) return;
        try { pee.peeRemainTime = 0f; } catch { }
        try { pee.reduceMoistureSpeed = 0.2f; } catch { }
        try { pee.lastDecalTime = 0f; } catch { }
    }
    catch { }
}

[HideFromIl2Cpp]
private void SetToyObjectsActive(string[] keywords, bool on)
{
    try
    {
        var pca = PlayerFacade.Instance.pca;
        if (pca == null || pca.GameObject == null) return;
        var all = pca.GameObject.GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t == null) continue;
            string n = "";
            try { n = t.name; } catch { }
            string low = n.ToLowerInvariant();
            foreach (var kw in keywords)
            {
                if (low.IndexOf(kw) >= 0)
                {
                    try { t.gameObject.SetActive(on); } catch { }
                    break;
                }
            }
        }
    }
    catch { }
}
[HideFromIl2Cpp]
private void ApplyUndressCycle()
{
    var pf = PlayerFacade.Instance;
    if (pf == null || pf.pca == null) return;
    var pam = pf.pca.PlayerAnimationManager;
    if (pam == null) return;
    _undressDegree++;
    if (_undressDegree > 3) _undressDegree = 0;
    ApplyUndressDegree(_undressDegree);
}

[HideFromIl2Cpp]
private void ApplyUndressReset()
{
    _undressDegree = 0;
    ApplyUndressDegree(0);
}

[HideFromIl2Cpp]
private void ApplyUndressDegree(int degree)
{
    var pf = PlayerFacade.Instance;
    if (pf == null || pf.pca == null) return;
    var pam = pf.pca.PlayerAnimationManager;
    if (pam == null) return;
    try
    {
        switch (degree)
        {
            case 0:
                pam.ForceSetClothesStateBlend(PlayerStateModel.ClothesState.CloseA);
                try { pam.ForceSetClothesStateBlend(PlayerStateModel.ClothesState.CloseB); } catch { }
                break;
            case 1:
                pam.ForceSetClothesStateBlend(PlayerStateModel.ClothesState.OpenA1);
                break;
            case 2:
                pam.ForceSetClothesStateBlend(PlayerStateModel.ClothesState.OpenA3);
                break;
            case 3:
                pam.ForceSetClothesStateBlend(PlayerStateModel.ClothesState.DropClothes);
                break;
        }
    }
    catch { }
}

[HideFromIl2Cpp]
private void ApplyBareta(bool on)
{
    var pf = PlayerFacade.Instance;
    if (pf == null || pf.pca == null) return;
    var pam = pf.pca.PlayerAnimationManager;
    if (pam == null) return;
    try { pam.SetBareta(on); } catch { }
    try { pam.SetBaretaFace(on); } catch { }
    if (!on)
    {
        try { var ps = pf.pca.PlayerState; if (ps != null) ps.CurrentState = PlayerStateModel.PlayerState.Idle; } catch { }
        try { pf.TransAction(ActionType.PoseEnd); } catch { }
    }
}

[HideFromIl2Cpp]
private void ApplySitToggle()
{
    var pf = PlayerFacade.Instance;
    if (pf == null || pf.pca == null) return;
    try
    {
        var ps = pf.pca.PlayerState;
        bool sit = ps != null && ps.IsSit;
        if (ps != null) ps.IsSit = !sit;
        if (sit) pf.TransAction(ActionType.StandUp);
        else pf.TransAction(ActionType.SitDown);
    }
    catch { }
}


[HideFromIl2Cpp]
private void SendToyCmd(string d, int intArg, bool relayMode)
{
    if (_toyLinkedTarget.Length == 0) return;
    if (relayMode)
        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = _toyLinkedTarget, ["d"] = d, ["stage"] = intArg, ["mode"] = intArg, ["type"] = intArg, ["on"] = intArg != 0 });
    else
        SendDirectControl("control", _toyLinkedTarget, d, intArg, intArg != 0);
}

[HideFromIl2Cpp]
private void SendToyHandcuff(int mode, int duration, bool relayMode)
{
    if (_toyLinkedTarget.Length == 0) return;
    if (relayMode)
        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_control", ["to"] = _toyLinkedTarget, ["d"] = "handcuff_back", ["mode"] = mode, ["duration"] = duration });
    else
        SendDirectControl("control", _toyLinkedTarget, "handcuff_back", mode, duration > 0);
}

[HideFromIl2Cpp]
private void DrawToySections(float col, float h, float step, bool relayMode)
{
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 振动器（4档） ──");
    _uiY += step;
    for (int row = 0; row < 2; row++)
    {
        for (int column = 0; column < 2; column++)
        {
            int st = row * 2 + column;
            if (_canButton && SButton(new Rect(_uiX + column * (col + 6f), _uiY, col, h), "振动：" + _vibStages[st]))
                SendToyCmd("vibrate", st, relayMode);
        }
        _uiY += step;
    }
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 伸缩棒（4档） ──");
    _uiY += step;
    for (int row = 0; row < 2; row++)
    {
        for (int column = 0; column < 2; column++)
        {
            int st = row * 2 + column;
            if (_canButton && SButton(new Rect(_uiX + column * (col + 6f), _uiY, col, h), "伸缩：" + _vibStages[st]))
                SendToyCmd("thrust_set", st, relayMode);
        }
        _uiY += step;
    }
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 玩具穿戴 ──");
    _uiY += step;
    string[] wearLabels = { "阴蒂", "塞肛", "眼罩" };
    int[] wearTypes = { 7, 0, 2 };
    for (int i = 0; i < wearLabels.Length; i++)
    {
        if (_canButton && SButton(new Rect(_uiX + (i % 2) * (col + 6f), _uiY, col, h), "穿戴：" + wearLabels[i]))
            SendToyCmd("goods", wearTypes[i], relayMode);
        if (i % 2 == 1) _uiY += step;
    }
    if (wearLabels.Length % 2 == 1) _uiY += step;
    for (int i = 0; i < wearLabels.Length; i++)
    {
        if (_canButton && SButton(new Rect(_uiX + (i % 2) * (col + 6f), _uiY, col, h), "脱下：" + wearLabels[i]))
            SendToyCmd("goods_off", wearTypes[i], relayMode);
        if (i % 2 == 1) _uiY += step;
    }
    if (wearLabels.Length % 2 == 1) _uiY += step;

    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 坐/站 ──");
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "坐姿切换（点一下自动换坐/站）"))
        SendToyCmd("sit_toggle", 0, relayMode);
    _uiY += step;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 衣服穿脱（点一下切换程度） ──");
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, col, h), "脱衣程度：穿上→打开→半脱→全脱"))
        SendToyCmd("undress_cycle", 0, relayMode);
    if (_canButton && SButton(new Rect(_uiX + col + 6f, _uiY, col, h), "回归所有衣服"))
        SendToyCmd("undress_reset", 0, relayMode);
    _uiY += step;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 露出 ──");
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, col, h), "露出：开"))
        SendToyCmd("bareta", 1, relayMode);
    if (_canButton && SButton(new Rect(_uiX + col + 6f, _uiY, col, h), "露出：关"))
        SendToyCmd("bareta", 0, relayMode);
    _uiY += step;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 玩乳头 ──");
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, col, h), "穿戴乳头玩具"))
        SendToyCmd("goods", 6, relayMode);
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, col, h), "脱下乳头玩具"))
        SendToyCmd("goods_off", 6, relayMode);
    _uiY += step;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 玩屁股 ──");
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, col, h), "扭臀"))
        SendToyCmd("action", 50002, relayMode);
    if (_canButton && SButton(new Rect(_uiX + col + 6f, _uiY, col, h), "蹲臀"))
        SendToyCmd("action", 50003, relayMode);
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, col, h), "插入肛塞"))
        SendToyCmd("action", 10006, relayMode);
    if (_canButton && SButton(new Rect(_uiX + col + 6f, _uiY, col, h), "拔出肛塞"))
        SendToyCmd("action", 10007, relayMode);
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, col, h), "穿戴塞肛"))
        SendToyCmd("goods", 0, relayMode);
    if (_canButton && SButton(new Rect(_uiX + col + 6f, _uiY, col, h), "脱下塞肛"))
        SendToyCmd("goods_off", 0, relayMode);
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX + col + 6f, _uiY, col, h), "趴下"))
        SendToyCmd("crawl", 0, relayMode);
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, col, h), "站起"))
        SendToyCmd("stand", 0, relayMode);
    if (_canButton && SButton(new Rect(_uiX + col + 6f, _uiY, col, h), _forceCrouch ? "恢复站立" : "强制蹲走"))
    {
        _forceCrouch = !_forceCrouch;
        SendToyCmd("crouch", _forceCrouch ? 1 : 0, relayMode);
    }
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), _forceClimax ? "停止高潮" : "强制高潮"))
    {
        _forceClimax = !_forceClimax;
        SendToyCmd("climax", _forceClimax ? 1 : 0, relayMode);
    }
    _uiY += step;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 排尿 / 高潮 ──");
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, col, h), "排尿·排空"))
        SendToyFx(_toyLinkedTarget, "pee", 1);
    if (_canButton && SButton(new Rect(_uiX + col + 6f, _uiY, col, h), "排尿·永久"))
        SendToyFx(_toyLinkedTarget, "pee", 2);
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "停止排尿"))
        SendToyCmd("pee_stop", 0, relayMode);
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "强制高潮一次"))
        SendToyFx(_toyLinkedTarget, "shiofuki", 1);
    _uiY += step;
}

[HideFromIl2Cpp]
private void ApplyHandcuffBack(Dictionary<string, object> m)
{
    int stage = JsonHelper.Int(m, "stage", -1);
    _pendingHandcuff = true;
    _pendingHandcuffMode = JsonHelper.Int(m, "mode", 1);
    _pendingHandcuffDuration = JsonHelper.Int(m, "duration", 0);
    _pendingHandcuffAt = Time.unscaledTime + 0.35f;
    _pendingHandcuffDeadline = Time.unscaledTime + 10f;
    if (stage >= 0 && (!InGame || CurrentStageInt() != stage))
        DoFollow(stage, new Vector3((float)JsonHelper.Double(m, "x"), (float)JsonHelper.Double(m, "y"), (float)JsonHelper.Double(m, "z")), 0f);
}

[HideFromIl2Cpp]
private void UpdatePendingHandcuff()
{
    if (!_pendingHandcuff) return;
    if (Time.unscaledTime > _pendingHandcuffDeadline)
    {
        _pendingHandcuff = false;
        PluginInfo.Warn("手铐同步等待场景超时，已取消以避免卡死");
        return;
    }
    if (_pendingFollowStage >= 0 || !InGame || Time.unscaledTime < _pendingHandcuffAt) return;
    _pendingHandcuff = false;
    ApplyHandcuff(_pendingHandcuffMode, _pendingHandcuffDuration);
}

[HideFromIl2Cpp]
private void ApplyUnlockHandcuff()
{
    var pf = PlayerFacade.Instance;
    if (pf == null) return;
    try { pf.TransAction(ActionType.UnlockHandcuffsAtMap); } catch { }
    try { pf.ForceChangeAdultGoods(MAdultGoodsType.Handcuff, false); } catch { }
    try { pf.ForceChangeAdultGoods(MAdultGoodsType.KeyHandcuff, false); } catch { }
    try { pf.ForceChangeAdultGoods(MAdultGoodsType.TimerHandcuff, false); } catch { }
    SetGoodsVisual(MAdultGoodsType.Handcuff, false);
    SetGoodsVisual(MAdultGoodsType.KeyHandcuff, false);
    SetGoodsVisual(MAdultGoodsType.TimerHandcuff, false);
}

[HideFromIl2Cpp]
private void ApplyUndress(int stage)
{
    var pf = PlayerFacade.Instance;
    var pam = pf.pca != null ? pf.pca.PlayerAnimationManager : null;
    switch (stage)
    {
        case 0:
            pf.TransAction(ActionType.TakeOnPants);
            pf.TransAction(ActionType.TakeOnBra);
            break;
        case 1:
            pf.TransAction(ActionType.TakeOffBra);
            break;
        case 2:
            pf.TransAction(ActionType.TakeOffPants);
            break;
        case 3:
            pf.TransAction(ActionType.TakeOffPants);
            pf.TransAction(ActionType.TakeOffBra);
            if (pam != null) { try { pam.DirectDropClothes(); } catch { } }
            break;
    }
}

[HideFromIl2Cpp]
private void ApplyClimax(bool on)
{
    var pam = PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.PlayerAnimationManager : null;
    if (pam != null) pam.SetEcstasy(on);
}

[HideFromIl2Cpp]
private void ApplyCrouch(bool on)
{
    var ps = PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.PlayerState : null;
    if (ps != null) ps.IsCrouch = on;
    if (on) PlayerFacade.Instance.TransAction(ActionType.GanimataWalk);
}

[HideFromIl2Cpp]
private void ApplyCollar(bool on)
{
    _toyCollar = on;
    try { PlayerFacade.Instance.SetCosplayActive(CosplayType.Cultist, (int)CostumeCultistPart.SisterCollar, on); } catch { }
    ApplyToyVisual();
}

[HideFromIl2Cpp]
private void ApplyPleasure()
{
    try { PlayerFacade.Instance.AddAddMoisture(15f); } catch { }
    try { PlayerFacade.Instance.TransAction(ActionType.OldOnaniNormal); } catch { }
}

[HideFromIl2Cpp]
private void DoPleasureToTarget(string targetUid)
{
    if (!InGame || targetUid.Length == 0) return;
    try
    {
        var pca = PlayerFacade.Instance.pca;
        var self = pca.AvatorTransform.position;
        if (_relayPositions.TryGetValue(targetUid, out var rp))
        {
            Vector3 target = new Vector3(rp.X, rp.Y, rp.Z);
            Vector3 dir = target - self;
            dir.y = 0f;
            if (dir.magnitude > 0.01f)
            {
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                PlayerFacade.Instance.SmoothRotateY(yaw);
            }
            if (Vector3.Distance(self, target) > 1.2f)
                StartNavFollow(target); // 走近对方
            EnsureBodyOffsets();
            var targetRot = Quaternion.Euler(0f, rp.RotY, 0f);
            _fingerTarget = target + targetRot * _selfPussyLocalOffset;
            _fingerActive = true;
            _fingerUntil = Time.unscaledTime + 3f;
        }
        PlayerFacade.Instance.TransAction(ActionType.OldOnaniNormal);
    }
    catch { }
}

[HideFromIl2Cpp]
private void ApplyFollow(bool on, Dictionary<string, object> m)
{
    if (on)
    {
        int stage = JsonHelper.Int(m, "stage", -1);
        Vector3 pos = new Vector3((float)JsonHelper.Double(m, "x"), (float)JsonHelper.Double(m, "y"), (float)JsonHelper.Double(m, "z"));
        _followTargetUid = JsonHelper.Str(m, "from");
        int cur = CurrentStageInt();
        if (cur >= 0 && stage >= 0 && cur != stage)
        {
            DoFollow(stage, pos, 0f);
            return;
        }
        _followTargetPos = pos;
        StartNavFollow(pos);
    }
    else
    {
        _followTargetUid = "";
        StopNavFollow();
    }
}

[HideFromIl2Cpp]
private void StartNavFollow(Vector3 pos)
{
    try
    {
        var agent = PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.NavAgent : null;
        if (agent == null) return;
        if (!agent.enabled) agent.enabled = true;
        if (Vector3.Distance(agent.transform.position, pos) < 1f)
        {
            agent.isStopped = true;
            return;
        }
        if (_hasNavDest && Vector3.Distance(_lastNavDest, pos) < 1f) return; // 目标几乎没变，不重算路径
        _lastNavDest = pos;
        _hasNavDest = true;
        agent.isStopped = false;
        agent.destination = pos;
    }
    catch (Exception ex) { PluginInfo.Warn("寻路跟随失败: " + ex.Message); }
}

[HideFromIl2Cpp]
private void StopNavFollow()
{
    _hasNavDest = false;
    try
    {
        var agent = PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.NavAgent : null;
        if (agent != null) agent.isStopped = true;
    }
    catch { }
}

[HideFromIl2Cpp]
private void ResetToyLocal()
{
    _forceClimax = false;
    _forceCrouch = false;
    _forceFollow = false;
    _followTargetUid = "";
    if (_toyCollar) { try { PlayerFacade.Instance.SetCosplayActive(CosplayType.Cultist, (int)CostumeCultistPart.SisterCollar, false); } catch { } }
    _toyCollar = false;
    StopNavFollow();
    try
    {
        var pf = PlayerFacade.Instance;
        pf.ForceChangeAdultGoods(MAdultGoodsType.Vibrator, false);
        pf.ForceChangeAdultGoods(MAdultGoodsType.PistonPussy, false);
        pf.ForceChangeAdultGoods(MAdultGoodsType.PistonAnal, false);
        pf.ForceChangeAdultGoods(MAdultGoodsType.PistonFuta, false);
        pf.ForceChangeAdultGoods(MAdultGoodsType.Handcuff, false);
        pf.ForceChangeAdultGoods(MAdultGoodsType.KeyHandcuff, false);
        pf.ForceChangeAdultGoods(MAdultGoodsType.TimerHandcuff, false);
        if (pf.pca != null)
        {
            if (pf.pca.PlayerAnimationManager != null) pf.pca.PlayerAnimationManager.SetEcstasy(false);
            if (pf.pca.PlayerState != null) pf.pca.PlayerState.IsCrouch = false;
        }
    }
    catch { }
}

private sealed class RelayPos
{
    public float X;
    public float Y;
    public float Z;
    public float RotY;
    public int Stage;
}

[HideFromIl2Cpp]
private void UpdateToyFollowSend()
{
    if (!_forceFollow || _toyLinkedTargets.Count == 0 || !InGame) return;
    if (Time.unscaledTime - _lastFollowSend < 2.5f) return;
    _lastFollowSend = Time.unscaledTime;
    var t = PlayerFacade.Instance.pca.AvatorTransform;
    foreach (var tg in _toyLinkedTargets)
        RelayTcp.Send(new Dictionary<string, object>
        {
            ["t"] = "toy_control", ["to"] = tg, ["d"] = "follow", ["on"] = true,
            ["stage"] = CurrentStageInt(), ["x"] = t.position.x, ["y"] = t.position.y, ["z"] = t.position.z
        });
}

private sealed class NpcSyncPoint
{
    public int Id;
    public Vector3 Pos;
    public Vector3 Vel;
    public float RotY;
    public bool Moving;
    public int ActionHash;
    public float DistSq;
}

[HideFromIl2Cpp]
private List<NpcSyncPoint> CollectLocalNpcs()
{
    var list = new List<NpcSyncPoint>();
    try
    {
        var nm = NpcManager.Instance;
        if (nm != null && nm.ExistNpcList != null)
        {
            Vector3 self = InGame ? PlayerFacade.Instance.pca.AvatorTransform.position : Vector3.zero;
            for (int i = 0; i < nm.ExistNpcList.Count; i++)
            {
                var npc = nm.ExistNpcList[i];
                if (npc == null || npc.NpcComponent == null) continue;
                Vector3 p = npc.NpcComponent.transform.position;
                // 全图采集（同 stage 的所有 NPC 都由该地图权威者同步，不再限 45 米）
                int actionHash = 0;
                try { var npcAnimator = npc.NpcComponent.GetComponentInChildren<Animator>(); if (npcAnimator != null && npcAnimator.layerCount > 0) actionHash = npcAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash; } catch { }
                int npcId = 0;
                try { npcId = npc.NpcComponent.id; } catch { }
                if (npcId <= 0) npcId = 100000 + i;
                float dx = p.x - Mathf.Round(p.x * 50f) / 50f;
                float dy = p.y - Mathf.Round(p.y * 50f) / 50f;
                float dz = p.z - Mathf.Round(p.z * 50f) / 50f;
                list.Add(new NpcSyncPoint { Id = npcId, Pos = p - new Vector3(dx, dy, dz), RotY = npc.NpcComponent.transform.eulerAngles.y, ActionHash = actionHash, DistSq = 0f });
            }
            // 全图 NPC 上限保护（太多会导致包过大）
            if (list.Count > 60) list.RemoveRange(60, list.Count - 60);
        }
    }
    catch { }
    return list;
}

[HideFromIl2Cpp]
private void SendTimeSync()
{
    try
    {
        // 时间同步：房主每 2 秒广播一次昼夜时间，房间其他人应用房主的时间
        bool isHost = _relayPlayers.Count > 0 && JsonHelper.Str(_relayPlayers[0], "uid") == _authUid.ToString();
        if (!isHost) return;
        if (Time.unscaledTime - _lastTimeSyncAt < 2f) return;
        _lastTimeSyncAt = Time.unscaledTime;
        bool daytime = false;
        try
        {
            var gsd = typeof(GameStateData).GetProperty("GameStateData", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.GetValue(null) as GameStateData;
            daytime = gsd != null && gsd.IsDaytime;
        }
        catch { }
        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "time_sync", ["daytime"] = daytime ? 1 : 0 });
    }
    catch { }
}

[HideFromIl2Cpp]
private void SendNpcSync()
{
    if (!_relayConnected || _relayRoomId.Length == 0 || !InGame) return;
    float npcInterval = _relayPlayers.Count >= 8 ? 1f : (_relayPlayers.Count >= 5 ? 0.75f : 0.5f);
    if (Time.unscaledTime - _lastNpcSync < npcInterval) return;
    _lastNpcSync = Time.unscaledTime;
    var npcs = CollectLocalNpcs();
    float sampleNow = Time.unscaledTime;
    float sampleDt = _npcLastSampleAt > 0f ? Mathf.Clamp(sampleNow - _npcLastSampleAt, 0.05f, 0.5f) : 0.25f;
    var seenNpcIds = new HashSet<int>();
    foreach (var p in npcs)
    {
        seenNpcIds.Add(p.Id);
        if (_npcLastSentPos.TryGetValue(p.Id, out var previous)) p.Vel = (p.Pos - previous) / sampleDt;
        p.Vel.y = 0f;
        p.Vel = Vector3.ClampMagnitude(p.Vel, 6f);
        p.Moving = p.Vel.sqrMagnitude > 0.01f;
        _npcLastSentPos[p.Id] = p.Pos;
    }
    _npcLastSampleAt = sampleNow;
    var staleNpcIds = new List<int>();
    foreach (var npcId in _npcLastSentPos.Keys) if (!seenNpcIds.Contains(npcId)) staleNpcIds.Add(npcId);
    foreach (var npcId in staleNpcIds) _npcLastSentPos.Remove(npcId);
    var sb = new System.Text.StringBuilder();
    foreach (var p in npcs) sb.Append(p.Id).Append(':').Append((int)(p.Pos.x * 50f)).Append(',').Append((int)(p.Pos.y * 50f)).Append(',').Append((int)(p.Pos.z * 50f)).Append(',').Append((int)(p.RotY * 2f)).Append(',').Append(p.Moving ? 1 : 0).Append(';');
    string sig = sb.ToString();
    if (sig == _lastNpcSig) return;
    _lastNpcSig = sig;
    var arr = new List<object>();
    foreach (var p in npcs)
        arr.Add(new Dictionary<string, object> { ["i"] = p.Id, ["x"] = p.Pos.x, ["y"] = p.Pos.y, ["z"] = p.Pos.z, ["vx"] = (float)Math.Round(p.Vel.x, 2), ["vz"] = (float)Math.Round(p.Vel.z, 2), ["ry"] = (float)Math.Round(p.RotY, 1), ["moving"] = p.Moving ? 1 : 0, ["hash"] = p.ActionHash });
    RelayTcp.Send(new Dictionary<string, object> { ["t"] = "npc_sync", ["stage"] = CurrentStageInt(), ["npcs"] = arr });
}

[HideFromIl2Cpp]
private void ApplySyncedNpcs()
{
    if (!InGame || _syncNpcAuthority.Length == 0 || _syncNpcAuthority == _authUid.ToString() || _syncNpcStage != CurrentStageInt()) return;
    try
    {
        var nm = NpcManager.Instance;
        if (nm == null || nm.ExistNpcList == null) return;
        var idToNpc = new Dictionary<int, NpcController>();
        try
        {
            for (int i = 0; i < nm.ExistNpcList.Count; i++)
            {
                var npc = nm.ExistNpcList[i];
                if (npc == null || npc.NpcComponent == null) continue;
                int npcId = 0;
                try { npcId = npc.NpcComponent.id; } catch { }
                if (npcId <= 0) npcId = 100000 + i;
                idToNpc[npcId] = npc;
            }
        }
        catch { }
        float k = 1f - Mathf.Exp(-12f * Mathf.Max(0.001f, Time.unscaledDeltaTime));
        foreach (var kv in _syncNpcTargets)
        {
            NpcController npc = null;
            if (!idToNpc.TryGetValue(kv.Key, out npc) || npc == null || npc.NpcComponent == null) continue;
            Transform t = npc.NpcComponent.transform;
            Vector3 predicted = kv.Value;
            if (_syncNpcVelocity.TryGetValue(kv.Key, out var npcVelocity)) predicted += npcVelocity * 0.2f;
            float d = Vector3.Distance(t.position, predicted);
            t.position = d > 4f ? predicted : Vector3.Lerp(t.position, predicted, k);
            if (_syncNpcRotY.TryGetValue(kv.Key, out var npcRotY)) t.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(t.eulerAngles.y, npcRotY, k), 0f);
            try
            {
                var animator = npc.NpcComponent.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    bool moving = _syncNpcMoving.TryGetValue(kv.Key, out var npcMoving) && npcMoving;
                    animator.SetFloat(Animator.StringToHash("MoveSpeed"), moving ? Mathf.Clamp(npcVelocity.magnitude, 0.6f, 2f) : 0f);
                    animator.SetBool(Animator.StringToHash("IsStrafe"), moving);
                    if (_syncNpcActionHash.TryGetValue(kv.Key, out var npcHash) && npcHash != 0 && (!_npcLastAppliedHash.TryGetValue(kv.Key, out var oldHash) || oldHash != npcHash))
                    {
                        animator.Play(npcHash, 0, 0f);
                        _npcLastAppliedHash[kv.Key] = npcHash;
                    }
                }
            }
            catch { }
        }
    }
    catch { }
}

[HideFromIl2Cpp]
private List<object> NumListF(float[] a) { var l = new List<object>(); if (a != null) foreach (var v in a) l.Add((float)Math.Round(v, 3)); return l; }
[HideFromIl2Cpp]
private List<object> NumListI(int[] a) { var l = new List<object>(); if (a != null) foreach (var v in a) l.Add(v); return l; }
[HideFromIl2Cpp]
private List<object> StrListS(string[] a) { var l = new List<object>(); if (a != null) foreach (var v in a) l.Add(v); return l; }
[HideFromIl2Cpp]
private List<object> BoolListB(bool[] a) { var l = new List<object>(); if (a != null) foreach (var v in a) l.Add(v); return l; }

[HideFromIl2Cpp]
private float[] ParseFloats(object o)
{
    if (o is List<object> l)
    {
        var r = new float[l.Count];
        for (int i = 0; i < l.Count; i++) { var v = l[i]; if (v is double dd) r[i] = (float)dd; else if (v is long ll) r[i] = (float)ll; }
        return r;
    }
    return new float[0];
}
[HideFromIl2Cpp]
private int[] ParseInts(object o)
{
    if (o is List<object> l)
    {
        var r = new int[l.Count];
        for (int i = 0; i < l.Count; i++) { var v = l[i]; if (v is double dd) r[i] = (int)dd; else if (v is long ll) r[i] = (int)ll; }
        return r;
    }
    return new int[0];
}
[HideFromIl2Cpp]
private string[] ParseStrs(object o)
{
    if (o is List<object> l)
    {
        var r = new string[l.Count];
        for (int i = 0; i < l.Count; i++) r[i] = l[i] == null ? "" : l[i].ToString();
        return r;
    }
    return new string[0];
}
[HideFromIl2Cpp]
private bool[] ParseBools(object o)
{
    if (o is List<object> l)
    {
        var r = new bool[l.Count];
        for (int i = 0; i < l.Count; i++) { var v = l[i]; r[i] = v is bool b ? b : (v is long ll && ll != 0); }
        return r;
    }
    return new bool[0];
}

[HideFromIl2Cpp]
private void PrepareRelayAppearanceSync(){_stateSyncCount=0;_lastRelayKeyframe=-999f;_lastRelayMotionSend=-999f;_lastRelayMotionPosAt=-999f;_lastRelayActionSyncAt=-999f;_lastRelayBoneAt=-999f;_lastRelayAppearanceSig="";_lastRelaySentAction=int.MinValue;_lastRelaySentHash=int.MinValue;_lastRelaySentActionId=int.MinValue;_lastRelaySentActionParam=int.MinValue;_lastRelaySentOldActionId=int.MinValue;_lastRelaySentAnotherMotion=float.NaN;_lastRelaySentLayerHashes=new int[0];_relayMotionStateSent=false;_appearanceRequestRoom=_relayRoomId;if(_relayConnected&&_relayRoomId.Length>0)RelayTcp.Send(new Dictionary<string,object>{{"t","appearance_request"}});}
[HideFromIl2Cpp]
private void UpdateRelayAppearanceHandshake(){if(!_relayConnected||_relayRoomId.Length==0||!InGame){if(_relayRoomId.Length==0)_appearanceRequestRoom="";return;}if(_appearanceRequestRoom!=_relayRoomId)PrepareRelayAppearanceSync();}

[HideFromIl2Cpp]
private void SendRelayState()
{
    if (!_relayConnected || _relayRoomId.Length == 0 || !InGame) return;
    if (Time.unscaledTime - _lastRelayKeyframe < 1f) return;
    _lastRelayKeyframe = Time.unscaledTime;
    try
    {
        _stateSyncCount++;
        string appearanceSig = CurrentAppearanceSignature();
        bool sendAppearance = _stateSyncCount == 1 || appearanceSig != _lastRelayAppearanceSig;
        if (sendAppearance) _lastRelayAppearanceSig = appearanceSig;
        var st = SampleLocalState(false, false, sendAppearance);
        if (st == null) return;
        var d = new Dictionary<string, object>
        {
            ["slot"] = RelayMySlot(),
            ["x"] = (float)Math.Round(st.Pos.x, 3), ["y"] = (float)Math.Round(st.Pos.y, 3), ["z"] = (float)Math.Round(st.Pos.z, 3),
            ["ry"] = (float)Math.Round(st.RotY, 2),
            ["sx"] = (float)Math.Round(st.Scale.x, 3), ["sy"] = (float)Math.Round(st.Scale.y, 3), ["sz"] = (float)Math.Round(st.Scale.z, 3),
            ["anim"] = (float)Math.Round(st.AnimSpeed, 3), ["act"] = st.ActionType, ["ps"] = st.PlayerState,
            ["crouch"] = st.IsCrouch, ["dash"] = st.IsDash, ["ecstasy"] = st.IsEcstasy, ["gaman"] = st.IsGaman,
            ["gy"] = (float)Math.Round(st.GroundY, 3),
            ["hp"] = NumListF(new float[] { _cachedHipsLocal.x, _cachedHipsLocal.y, _cachedHipsLocal.z, _cachedHipsLocalRot.x, _cachedHipsLocalRot.y, _cachedHipsLocalRot.z, _cachedHipsLocalRot.w })
        };
        if (sendAppearance) d["ap"] = StrListS(st.ActivePaths != null ? st.ActivePaths.ToArray() : new string[0]);
        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "state_sync", ["uid"] = PeerId, ["s"] = d });
    }
    catch { }
}

[HideFromIl2Cpp]
private void ApplyRelayBones(string uid, Dictionary<string, object> m)
{
    if (uid.Length == 0 || uid == _authUid.ToString() || !InGame) return;
    var q = ParseFloats(m.TryGetValue("q", out var qv) ? qv : null);
    if (q == null || q.Length < CoreBoneNames.Length * 4) return;
    float nowStamp = Time.unscaledTime;
    if (_relayGhostIgnoreUntil.TryGetValue(uid, out float ig) && nowStamp < ig) return;
    _relayGhostLastSeen[uid] = nowStamp;
    GhostPlayer ghost;
    if (!_relayGhosts.TryGetValue(uid, out ghost) || ghost == null || ghost.Root == null)
    {
        if (_ghostRoot == null) _ghostRoot = new GameObject("SFMOnline_Ghosts");
        try { ghost = new GhostPlayer(uid, _ghostRoot.transform); } catch { ghost = null; }
        _relayGhosts[uid] = ghost;
    }
    if (ghost == null) return;
    ghost.ApplyCoreBones(q);
}

[HideFromIl2Cpp]
private void ApplyRelayMotion(string uid, Dictionary<string, object> m)
{
    if (uid.Length == 0 || uid == _authUid.ToString() || !SceneSyncReady) return;
    float nowStamp = Time.unscaledTime;
    if (_relayGhostIgnoreUntil.TryGetValue(uid, out float ignoreUntil) && nowStamp < ignoreUntil) return;
    _relayGhostLastSeen[uid] = nowStamp;
    GhostPlayer ghost;
    if (!_relayGhosts.TryGetValue(uid, out ghost) || ghost == null || ghost.Root == null)
    {
        if (_ghostRoot == null) _ghostRoot = new GameObject("SFMOnline_Ghosts");
        try { ghost = new GhostPlayer(uid, _ghostRoot.transform); } catch { ghost = null; }
        _relayGhosts[uid] = ghost;
    }
    if (ghost == null) return;
    EnsureMotionClipsLoaded();
    bool remoteMoving = JsonHelper.Int(m, "moving") != 0;
    string remoteDir = JsonHelper.Str(m, "dir");
    if (remoteDir.Length == 0) remoteDir = "fwd";
    bool rc = JsonHelper.Int(m, "crouch") != 0;
    bool rd = JsonHelper.Int(m, "dash") != 0;
    string moveClipKey = remoteMoving ? "move_" + remoteDir + (rc ? "_c" : "") + (rd ? "_d" : "") : "";
    MotionClip moveClip = null;
    if (moveClipKey.Length > 0 && !_motionClips.TryGetValue(moveClipKey, out moveClip))
    {
        moveClipKey = "move_" + remoteDir;
        if (!_motionClips.TryGetValue(moveClipKey, out moveClip))
        {
            moveClipKey = rc ? "crouch" : (rd ? "run" : "walk");
            _motionClips.TryGetValue(moveClipKey, out moveClip);
        }
    }
    if (moveClipKey.Length > 0 && moveClip != null)
    {
        float locoOriginY = 0f;
        if (_relayPositions.TryGetValue(uid, out var rpL)) locoOriginY = rpL.Y;
        ghost.PlayLocomotionClip(moveClip, locoOriginY);
    }
    else if (!remoteMoving)
        ghost.StopLocomotionClip();
    var velocity = new Vector3((float)JsonHelper.Double(m, "vx"), 0f, (float)JsonHelper.Double(m, "vz"));
    velocity = Vector3.ClampMagnitude(velocity, 9f);
    bool hasPosition = m.ContainsKey("x") && m.ContainsKey("y") && m.ContainsKey("z");
    Vector3 networkPosition = hasPosition
        ? new Vector3((float)JsonHelper.Double(m, "x"), (float)JsonHelper.Double(m, "y"), (float)JsonHelper.Double(m, "z"))
        : Vector3.zero;
    float groundY = (float)JsonHelper.Double(m, "gy", 0);
    _relayGroundY[uid] = groundY;
    ghost.SetMotionDetailed(velocity, (float)JsonHelper.Double(m, "ry"), JsonHelper.Int(m, "moving") != 0,
        JsonHelper.Int(m, "crouch") != 0, JsonHelper.Int(m, "strafe") != 0, JsonHelper.Int(m, "dash") != 0,
        JsonHelper.Int(m, "act", -1), JsonHelper.Int(m, "hash"),
        (float)JsonHelper.Double(m, "ms"), (float)JsonHelper.Double(m, "lms", 1),
        (float)JsonHelper.Double(m, "sx"), (float)JsonHelper.Double(m, "sy"),
        networkPosition, hasPosition);
    if (m.ContainsKey("hpx") && m.ContainsKey("hpy") && m.ContainsKey("hpz") && ghost != null)
    {
        Quaternion hr = Quaternion.identity;
        if (m.ContainsKey("hrx") && m.ContainsKey("hrw"))
            hr = new Quaternion((float)JsonHelper.Double(m, "hrx"), (float)JsonHelper.Double(m, "hry"), (float)JsonHelper.Double(m, "hrz"), (float)JsonHelper.Double(m, "hrw"));
        ghost.ApplyHipsFull(new Vector3((float)JsonHelper.Double(m, "hpx"), (float)JsonHelper.Double(m, "hpy"), (float)JsonHelper.Double(m, "hpz")), hr, true);
    }
    if (hasPosition)
    {
        if (_relayPositions.TryGetValue(uid, out var old))
            _relayPositions[uid] = new RelayPos { X = networkPosition.x, Y = networkPosition.y, Z = networkPosition.z, RotY = (float)JsonHelper.Double(m, "ry"), Stage = old.Stage };
        else
            _relayPositions[uid] = new RelayPos { X = networkPosition.x, Y = networkPosition.y, Z = networkPosition.z, RotY = (float)JsonHelper.Double(m, "ry"), Stage = CurrentStageInt() };
    }
}

[HideFromIl2Cpp]
private void ApplyRelayAction(string uid, Dictionary<string, object> m)
{
    if (uid.Length == 0 || uid == _authUid.ToString() || !InGame) return;
    float nowStamp = Time.unscaledTime;
    if (_relayGhostIgnoreUntil.TryGetValue(uid, out float ignoreUntil) && nowStamp < ignoreUntil) return;
    _relayGhostLastSeen[uid] = nowStamp;
    int action = JsonHelper.Int(m, "act", -1);
    int stateHash = JsonHelper.Int(m, "hash");
    _relayActionHints[uid] = action;
    GhostPlayer ghost;
    if (!_relayGhosts.TryGetValue(uid, out ghost) || ghost == null || ghost.Root == null)
    {
        if (_ghostRoot == null) _ghostRoot = new GameObject("SFMOnline_Ghosts");
        try { ghost = new GhostPlayer(uid, _ghostRoot.transform); } catch { ghost = null; }
        _relayGhosts[uid] = ghost;
    }
    if (ghost == null) return;
    EnsureMotionClipsLoaded();
    if (action < 0) { ghost.StopActionClip(); return; }
    int apm = JsonHelper.Int(m, "apm", action);
    bool ecs = JsonHelper.Int(m, "e") != 0;
    string actKey = "act_" + action + (ecs ? "_e" : "") + (apm > 0 ? "_v" + apm : "");
    MotionClip actionClip = null;
    if (!_motionClips.TryGetValue(actKey, out actionClip))
        _motionClips.TryGetValue("act_" + action, out actionClip);
    if (actionClip != null)
    {
        float actOriginY = 0f;
        if (_relayPositions.TryGetValue(uid, out var rpA)) actOriginY = rpA.Y;
        ghost.PlayActionClip(actionClip, actOriginY);
        return;
    }
    ghost.MarkActionDetailed(action, stateHash,
        JsonHelper.Int(m, "aid", action), JsonHelper.Int(m, "apm", action),
        JsonHelper.Int(m, "old", -1), (float)JsonHelper.Double(m, "ami"),
        ParseInts(m.TryGetValue("lh", out var lh) ? lh : null),
        ParseFloats(m.TryGetValue("lt", out var lt) ? lt : null),
        ParseFloats(m.TryGetValue("lw", out var lw) ? lw : null));
}

[HideFromIl2Cpp]
private void ApplyRelayState(string uid, Dictionary<string, object> m)
{
    if (!SceneSyncReady) return;
    float nowStamp = Time.unscaledTime;
    if (_relayGhostIgnoreUntil.TryGetValue(uid, out float ignoreUntil) && nowStamp < ignoreUntil) return;
    _relayGhostLastSeen[uid] = nowStamp;
    var d = JsonHelper.Object(m, "s");
    if (d == null) return;
    GhostPlayer ghost;
    if (!_relayGhosts.TryGetValue(uid, out ghost) || ghost == null || ghost.Root == null)
    {
        if (!InGame) return;
        if (_ghostRoot == null) _ghostRoot = new GameObject("SFMOnline_Ghosts");
        try { ghost = new GhostPlayer(uid, _ghostRoot.transform); } catch { ghost = null; }
        _relayGhosts[uid] = ghost;
    }
    if (ghost == null || ghost.Root == null) return;
    try
    {
        var st = new RemoteState();
        st.Pos = new Vector3((float)JsonHelper.Double(d, "x"), (float)JsonHelper.Double(d, "y"), (float)JsonHelper.Double(d, "z"));
        st.RotY = (float)JsonHelper.Double(d, "ry");
        if (float.IsNaN(st.Pos.x) || float.IsNaN(st.Pos.y) || float.IsNaN(st.Pos.z) ||
            float.IsInfinity(st.Pos.x) || float.IsInfinity(st.Pos.y) || float.IsInfinity(st.Pos.z) ||
            Mathf.Abs(st.Pos.x) > 100000f || Mathf.Abs(st.Pos.y) > 100000f || Mathf.Abs(st.Pos.z) > 100000f) return;
        float stateGy = (float)JsonHelper.Double(m, "gy", 0);
        if (stateGy > 0.001f) _relayGroundY[uid] = stateGy;
        st.Scale = new Vector3((float)JsonHelper.Double(d, "sx", 1), (float)JsonHelper.Double(d, "sy", 1), (float)JsonHelper.Double(d, "sz", 1));
        st.AnimSpeed = (float)JsonHelper.Double(d, "anim", 1);
        st.LayerWeights = ParseFloats(d.TryGetValue("lw", out var lw) ? lw : null);
        st.LayerStateHashes = ParseInts(d.TryGetValue("lsh", out var lsh) ? lsh : null);
        st.LayerStateTimes = ParseFloats(d.TryGetValue("lst", out var lst) ? lst : null);
        st.BonePaths = new string[0];
        st.BoneQuats = new float[0];
        st.FloatNames = ParseStrs(d.TryGetValue("fn", out var fn) ? fn : null);
        st.FloatVals = ParseFloats(d.TryGetValue("fv", out var fv) ? fv : null);
        st.IntNames = ParseStrs(d.TryGetValue("in", out var inn) ? inn : null);
        st.IntVals = ParseInts(d.TryGetValue("iv", out var iv) ? iv : null);
        st.BoolNames = ParseStrs(d.TryGetValue("bn", out var bn) ? bn : null);
        st.BoolVals = ParseBools(d.TryGetValue("bv", out var bv) ? bv : null);
        st.ActionType=JsonHelper.Int(d,"act",-1);if(_relayActionHints.TryGetValue(uid,out var hinted))st.ActionType=hinted;
        st.PlayerState = JsonHelper.Int(d, "ps", -1);
        st.IsCrouch = JsonHelper.Int(d, "crouch") != 0;
        st.IsDash = JsonHelper.Int(d, "dash") != 0;
        st.IsEcstasy = JsonHelper.Int(d, "ecstasy") != 0;
        st.IsGaman = JsonHelper.Int(d, "gaman") != 0;
        st.ActivePaths = new List<string>(ParseStrs(d.TryGetValue("ap", out var ap) ? ap : null));
        _lastStates[uid] = st;
        if (_relayPositions.TryGetValue(uid, out var oldRelayPos))
            _relayPositions[uid] = new RelayPos { X = st.Pos.x, Y = st.Pos.y, Z = st.Pos.z, RotY = st.RotY, Stage = oldRelayPos.Stage };
        else
            _relayPositions[uid] = new RelayPos { X = st.Pos.x, Y = st.Pos.y, Z = st.Pos.z, RotY = st.RotY, Stage = CurrentStageInt() };
        var hpList = ParseFloats(d.TryGetValue("hp", out var hpv) ? hpv : null);
        if (hpList != null && hpList.Length >= 7)
            ghost.ApplyHipsFull(new Vector3(hpList[0], hpList[1], hpList[2]), new Quaternion(hpList[3], hpList[4], hpList[5], hpList[6]), true);
        else if (hpList != null && hpList.Length >= 3)
            ghost.ApplyHipsFull(new Vector3(hpList[0], hpList[1], hpList[2]), Quaternion.identity, true);
        ghost.Apply(st, true);
    }
    catch { }
}

[HideFromIl2Cpp]
private void UpdateRelayPos()
{
    if (!_relayConnected || _relayRoomId.Length == 0 || !InGame) return;
    if (Time.unscaledTime - _lastPosSend < 1f) return;
    _lastPosSend = Time.unscaledTime;
    var t = PlayerFacade.Instance.pca.AvatorTransform;
    RelayTcp.Send(new Dictionary<string, object>
    {
        ["t"] = "pos", ["x"] = t.position.x, ["y"] = t.position.y, ["z"] = t.position.z,
        ["ry"] = t.eulerAngles.y, ["stage"] = CurrentStageInt()
    });
}

[HideFromIl2Cpp]
private void UpdateRelayGhosts()
{
    if (!_relayConnected || _relayRoomId.Length == 0 || !InGame) return;
    if (Time.unscaledTime - _lastGhostUpdate < 0.2f) return;
    _lastGhostUpdate = Time.unscaledTime;
    if (_ghostRoot == null) _ghostRoot = new GameObject("SFMOnline_Ghosts");
    float nowStamp = Time.unscaledTime;
    var staleGhosts = new List<string>();
    foreach (var kv in _relayGhostLastSeen)
        if (nowStamp - kv.Value > 40f) staleGhosts.Add(kv.Key);
    foreach (var staleUid in staleGhosts)
    {
        _relayPositions.Remove(staleUid);
        _relayGhostLastSeen.Remove(staleUid);
        RemoveRelayGhost(staleUid);
    }
    foreach (var kv in _relayPositions)
    {
        string uid = kv.Key;
        if (uid == _authUid.ToString()) continue;
        GhostPlayer ghost;
        if (!_relayGhosts.TryGetValue(uid, out ghost) || ghost == null || ghost.Root == null)
        {
            try { ghost = new GhostPlayer(uid, _ghostRoot.transform); } catch { ghost = null; }
            _relayGhosts[uid] = ghost;
        }
        if (ghost == null || ghost.Root == null) continue;
        bool sameStage = kv.Value.Stage < 0 || kv.Value.Stage == CurrentStageInt();
        Vector3 remotePos = new Vector3(kv.Value.X, kv.Value.Y, kv.Value.Z);
        float distance = Vector3.Distance(PlayerFacade.Instance.pca.AvatorTransform.position, remotePos);
        bool visible = sameStage && distance <= 60f;
        ghost.SetLodVisible(visible);
        // 高频 state_sync 已负责姿态；这里仅做可见性管理，避免用 2 秒一次的 pos 包覆盖预测位置。
        ghost.SetHighlight(visible && _gameRedPlayers.Contains(uid) ? (Color?)Color.red : null);
        if (Time.unscaledTime - _lastGhostVisLogAt > 5f)
        {
            _lastGhostVisLogAt = Time.unscaledTime;
            PluginInfo.Info("分身可见性: " + uid + " dist=" + distance.ToString("0.0") + " stage=" + kv.Value.Stage + " local=" + CurrentStageInt() + " visible=" + visible + " 启用=" + ghost.CountEnabledRenderers() + " 标记=" + ghost.HasMarker);
        }
    }
}

[HideFromIl2Cpp]
private void RemoveRelayGhost(string uid)
{
    if (_relayGhosts.TryGetValue(uid, out var g) && g != null && g.Root != null)
    {
        try { UnityEngine.Object.Destroy(g.Root); } catch { }
    }
    _relayGhosts.Remove(uid);
}

[HideFromIl2Cpp]
private void HandleGameState(Dictionary<string, object> m)
{
    _gameMode = JsonHelper.Str(m, "mode");
    _gamePhase = JsonHelper.Str(m, "phase");
    if (_gamePhase == "propose")
        AddRelayLine("[玩法] 有人提议捉迷藏，请投票");
    else if (_gamePhase == "playing" || _gamePhase == "vote_mvp")
    {
        _gameCaughtCount = JsonHelper.Int(m, "caught_count");
        _gameCatchTarget = JsonHelper.Int(m, "catch_target");
        _gameEscapedCount = JsonHelper.Int(m, "escaped_count");
        var lc = JsonHelper.Object(m, "lc");
        if (lc != null)
        {
            double lb = JsonHelper.Double(lc, "bareyasusa");
            double lm = JsonHelper.Double(lc, "max_bareyasusa");
            if (lb > 0) _lcBareyasusa = (float)lb;
            if (lm > 0) _lcMaxBareyasusa = (float)lm;
            _lcAttrOff = JsonHelper.Int(lc, "attr_off") != 0;
        }
        ApplyGameState(m);
    }
    else if (_gamePhase == "idle")
        ResetGameLocal();
}

[HideFromIl2Cpp]
private void HandleGameEvent(Dictionary<string, object> m)
{
    string kind = JsonHelper.Str(m, "kind");
    if (kind == "start")
    {
        _gamePhase = "playing";
        _gameRedPlayers.Clear();
        ApplyGameState(m);
        AddRelayLine("[玩法] 捉迷藏开始！");
        int map = JsonHelper.Int(m, "map", -1);
        if (map >= 0 && InGame)
            DoFollow(map, PlayerFacade.Instance.pca.AvatorTransform.position, 0f);
    }
    else if (kind == "caught")
    {
        string target = JsonHelper.Str(m, "target");
        _gameCaughtCount = JsonHelper.Int(m, "caught_count");
        _gameCatchTarget = JsonHelper.Int(m, "target_count");
        AddRelayLine("[玩法] " + GetGamePlayerName(target) + " 被抓了");
        _gameRedPlayers.Add(target);
        if (target == _authUid.ToString())
        {
            _gameCaughtRed = true;
            _gameRedUntil = Time.unscaledTime + 5f;
            _gameStopUntil = Time.unscaledTime + 10f;
            _gameBoostUntil = Time.unscaledTime + 4f;
            _gameNotice = "你被抓了！";
            _gameNoticeUntil = Time.unscaledTime + 5f;
            ApplyGameEffect();
        }
    }
    else if (kind == "escaped")
    {
        _gameRedPlayers.Remove(JsonHelper.Str(m, "uid"));
        AddRelayLine("[玩法] " + GetGamePlayerName(JsonHelper.Str(m, "uid")) + " 逃离成功");
    }
    else if (kind == "point")
        AddRelayLine("[玩法] 路口 " + JsonHelper.Int(m, "opened") + "/" + JsonHelper.Int(m, "need") + " 已开启");
    else if (kind == "exits")
        AddRelayLine("[玩法] 出口已开放！躲藏者按 F 逃离");
    else if (kind == "lc_start")
    {
        if (JsonHelper.Str(m, "uid") == _authUid.ToString())
        {
            _gameLcPoint = JsonHelper.Int(m, "point", -1);
            _gameLcSeconds = (float)JsonHelper.Double(m, "seconds");
            _gameLcStartAt = Time.unscaledTime;
        }
        AddRelayLine("[玩法] " + GetGamePlayerName(JsonHelper.Str(m, "uid")) + " 开始露出口点");
    }
    else if (kind == "lc_penalty")
    {
        if (JsonHelper.Str(m, "uid") == _authUid.ToString())
            _gameLcSeconds = (float)JsonHelper.Double(m, "seconds");
        AddRelayLine("[玩法] 被 NPC 发现，露出口点 +20 秒");
    }
    else if (kind == "found")
    {
        string fu = JsonHelper.Str(m, "uid");
        if (fu.Length > 0) _gameRedPlayers.Add(fu);
        AddRelayLine("[玩法] " + GetGamePlayerName(fu) + " 被 NPC 发现了");
    }
    else if (kind == "end_vote")
        AddRelayLine("[玩法] 结束投票 " + JsonHelper.Int(m, "votes") + "/" + JsonHelper.Int(m, "need"));
    else if (kind == "speed")
    {
        if (JsonHelper.Str(m, "uid") == _authUid.ToString())
        {
            _gameSpeed = (float)JsonHelper.Double(m, "speed");
            ApplyGameEffect();
        }
    }
    else if (kind == "end")
    {
        _gamePhase = "vote_mvp";
        _gameWinner = JsonHelper.Str(m, "winner");
        _gameRedPlayers.Clear();
        _gameNotice = "游戏结束！";
        _gameNoticeUntil = Time.unscaledTime + 5f;
        AddRelayLine("[玩法] 游戏结束，进入 10 秒投票阶段");
    }
    else if (kind == "finish")
    {
        _gameMvp = JsonHelper.Str(m, "mvp");
        _gamePhase = "ended";
        AddRelayLine("[玩法] MVP：" + GetGamePlayerName(_gameMvp));
    }
}

[HideFromIl2Cpp]
private string GetGamePlayerName(string uid)
{
    if (uid.Length == 0) return "";
    foreach (var pl in _relayPlayers)
        if (JsonHelper.Str(pl, "uid") == uid) return JsonHelper.Str(pl, "name");
    return GetPeerName(uid);
}

/// <summary>Ext 用的玩家名查询（relay/直连兼容）。</summary>
[HideFromIl2Cpp]
public string GetGamePlayerNamePublic(string uid) => GetGamePlayerName(uid);

[HideFromIl2Cpp]
private void ApplyGameState(Dictionary<string, object> m)
{
    var players = JsonHelper.Object(m, "players");
    var catchers = JsonHelper.StrList(m, "catchers");
    _gameCatchers.Clear();
    if (catchers != null) _gameCatchers.AddRange(catchers);
    if (players != null && players.TryGetValue(_authUid.ToString(), out var mev) && mev is Dictionary<string, object> me)
    {
        _gameRole = JsonHelper.Str(me, "role");
        _gameLives = JsonHelper.Int(me, "lives");
        _gameSpeed = (float)JsonHelper.Double(me, "speed");
        _gameBlindfold = JsonHelper.Int(me, "blindfold") != 0;
        double h = JsonHelper.Double(me, "height");
        if (h > 0) _gameHeight = (float)h;
        ApplyGameEffect();
    }
}

[HideFromIl2Cpp]
private void ResetGameLocal()
{
    _gameMode = "";
    _gamePhase = "idle";
    _gameRole = "";
    _gameLives = 0;
    _gameBlindfold = false;
    _gameSpeed = 1f;
    _gameCaughtRed = false;
    _gameCaughtCount = 0;
    _gameCatchTarget = 0;
    _gameEscapedCount = 0;
    ApplyGameEffect();
}

[HideFromIl2Cpp]
private void ApplyGameEffect()
{
    if (!InGame) return;
    try
    {
        float speed = _gameSpeed;
        if (Time.unscaledTime < _gameBoostUntil) speed *= 2f;
        if (Time.unscaledTime < _gameSlowUntil) speed *= 0.6f;
        PlayerFacade.Instance.SetAnimationMoveSpeed(speed);
        PlayerFacade.Instance.ForceChangeAdultGoods(MAdultGoodsType.EyeMask, _gameBlindfold);
        ApplyToyVisual();
        var adj = PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.PlayerBodyCustomizeAdjuster : null;
        if (adj != null && _gameHeight > 0f) adj._HeightScale_k__BackingField = _gameHeight;
    }
    catch (Exception ex) { PluginInfo.Warn("game effect: " + ex.Message); }
}

[HideFromIl2Cpp]
private void ApplyToyVisual()
{
    Color? tint = null;
    if (_gameCaughtRed && Time.unscaledTime < _gameRedUntil) tint = Color.red;
    SetSelfTint(tint);
}

[HideFromIl2Cpp]
private void SetSelfTint(Color? tint)
{
    try
    {
        var r = PlayerFacade.Instance.pca != null && PlayerFacade.Instance.pca.PlayerAvatarObjectReferencer != null
            ? PlayerFacade.Instance.pca.PlayerAvatarObjectReferencer.bodyMeshRenderer : null;
        if (r == null) return;
        if (tint.HasValue)
        {
            if (!_redOrigColorSaved) { _redOrigColor = r.material.color; _redOrigColorSaved = true; }
            r.material.color = tint.Value;
        }
        else if (_redOrigColorSaved)
        {
            r.material.color = _redOrigColor;
            _redOrigColorSaved = false;
        }
    }
    catch { }
}

[HideFromIl2Cpp]
private void GameFKey()
{
    // 捉迷藏玩法已从服务器移除，不再发送 game_* 消息（防止服务器 err 刷屏）
    return;
}

[HideFromIl2Cpp]
private string FindNearestOther()
{
    if (!InGame) return "";
    var self = PlayerFacade.Instance.pca.AvatorTransform.position;
    string best = "";
    float bestD = 2.0f;
    foreach (var kv in _relayPositions)
    {
        if (kv.Key == _authUid.ToString()) continue;
        float d = Vector3.Distance(self, new Vector3(kv.Value.X, kv.Value.Y, kv.Value.Z));
        if (d <= bestD) { bestD = d; best = kv.Key; }
    }
    return best;
}

[HideFromIl2Cpp]
private bool CatcherNear(float range)
{
    if (!InGame || _gameCatchers.Count == 0) return false;
    var self = PlayerFacade.Instance.pca.AvatorTransform.position;
    foreach (var c in _gameCatchers)
    {
        if (_relayPositions.TryGetValue(c, out var rp))
        {
            float d = Vector3.Distance(self, new Vector3(rp.X, rp.Y, rp.Z));
            if (d <= range) return true;
        }
    }
    return false;
}

[HideFromIl2Cpp]
private void DisableSkills()
{
    try
    {
        // 反射拿 GameStateData 单例（类名与静态属性同名，C# 无法直接写）
        var prop = typeof(GameStateData).GetProperty("GameStateData", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        var gsd = prop != null ? prop.GetValue(null) as GameStateData : null;
        if (gsd != null && gsd.PlayerPassiveSkillActiveDict != null)
        {
            var keys = new List<SkillType>();
            foreach (var k in gsd.PlayerPassiveSkillActiveDict.Keys) keys.Add(k);
            foreach (var k in keys) gsd.PlayerPassiveSkillActiveDict[k] = false;
        }
    }
    catch { }
    try
    {
        var rem = PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.PlayerReinforceEffectManager : null;
        if (rem != null)
        {
            var d = rem.cacheNewData;
            d.Bareyasusa100 = 0;
            rem.cacheNewData = d;
        }
    }
    catch { }
}


[HideFromIl2Cpp]
private void UpdateRoomSkillPolicy()
{
    bool shouldSuppress = _relayConnected && _relayRoomId.Length > 0 && !_roomAllowGameBonuses;
    if (Time.unscaledTime - _lastRoomSkillPolicy < 1f && shouldSuppress == _roomSkillsSuppressed) return;
    _lastRoomSkillPolicy = Time.unscaledTime;
    try
    {
        var prop = typeof(GameStateData).GetProperty("GameStateData", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        var gsd = prop != null ? prop.GetValue(null) as GameStateData : null;
        if (gsd == null || gsd.PlayerPassiveSkillActiveDict == null) return;
        if (shouldSuppress)
        {
            if (!_roomSkillsSuppressed)
            {
                _roomSkillSnapshot.Clear();
                foreach (var k in gsd.PlayerPassiveSkillActiveDict.Keys) _roomSkillSnapshot[k] = gsd.PlayerPassiveSkillActiveDict[k];
            }
            var keys = new List<SkillType>();
            foreach (var k in gsd.PlayerPassiveSkillActiveDict.Keys) keys.Add(k);
            foreach (var k in keys) gsd.PlayerPassiveSkillActiveDict[k] = false;
            _roomSkillsSuppressed = true;
        }
        else if (_roomSkillsSuppressed)
        {
            foreach (var kv in _roomSkillSnapshot) gsd.PlayerPassiveSkillActiveDict[kv.Key] = kv.Value;
            _roomSkillSnapshot.Clear();
            _roomSkillsSuppressed = false;
        }
    }
    catch { }
}

[HideFromIl2Cpp]
private void ApplyLcMode()
{
    if (!InGame || _gamePhase != "playing") return;
    try
    {
        if (_lcAttrOff) DisableSkills();
        var pca = PlayerFacade.Instance.pca;
        if (_gameRole != "hider") return;
        var ps = pca != null ? pca.PlayerState : null;
        if (ps != null && _lcBareyasusa > 0f) ps.Bareyasusa = _lcBareyasusa;
        if (pca != null && _lcMaxBareyasusa > 0f) PlayerStrangenessController.MaxBareyasusa = _lcMaxBareyasusa;
        if (!_lcReduceSet)
        {
            try { StrangenessValueManager.ReducePerSecond = 0f; _lcReduceSet = true; } catch { }
        }
    }
    catch (Exception ex) { PluginInfo.Warn("LC mode: " + ex.Message); }
}

[HideFromIl2Cpp]
private void CheckFoundByNpc()
{
    if (_gamePhase != "playing" || _gameRole != "hider" || !InGame) return;
    try
    {
        var bareta = PlayerFacade.Instance.pca != null ? PlayerFacade.Instance.pca.PlayerBaretaState : null;
        bool found = bareta != null && bareta.FoundNpc != null;
        if (found)
        {
            if (!_gameFoundSent)
            {
                _gameFoundSent = true;
                _gameCaughtRed = true;
                _gameRedUntil = Time.unscaledTime + 5f;
                _gameStopUntil = Time.unscaledTime + 10f;
                ApplyGameEffect();
            }
        }
        else _gameFoundSent = false;
    }
    catch { }
}

[HideFromIl2Cpp]
private void UpdateLcMode()
{
    if (_gamePhase != "playing") return;
    CheckFoundByNpc();
    if (_gameLcPoint >= 0 && _gameRole == "hider")
    {
        // 捉迷藏玩法已移除，不再发送 game_lc
    }
    if (Time.unscaledTime - _lcLastApply < 1f) return;
    _lcLastApply = Time.unscaledTime;
    ApplyLcMode();
}

[HideFromIl2Cpp]
private bool NearNpc(float range)
{
    if (!InGame) return false;
    try
    {
        var self = PlayerFacade.Instance.pca.AvatorTransform.position;
        var nm = NpcManager.Instance;
        if (nm != null && nm.ExistNpcList != null)
            for (int i = 0; i < nm.ExistNpcList.Count; i++)
            {
                var npc = nm.ExistNpcList[i];
                if (npc == null || npc.NpcComponent == null) continue;
                if (Vector3.Distance(self, npc.NpcComponent.transform.position) <= range) return true;
            }
    }
    catch { }
    return false;
}

[HideFromIl2Cpp]
private void UpdateLeashLine()
{
    bool hasLink = (_toyLinkedController.Length > 0) || (_toyLinkedTarget.Length > 0);
    if (!hasLink || !InGame)
    {
        if (_leashLine != null) _leashLine.enabled = false;
        return;
    }
    try
    {
        if (_leashLine == null)
        {
            var go = new GameObject("SFM_Leash");
            _leashLine = go.AddComponent<LineRenderer>();
            _leashLine.startColor = Color.black;
            _leashLine.endColor = Color.black;
            _leashLine.startWidth = 0.03f;
            _leashLine.endWidth = 0.03f;
            _leashLine.positionCount = 2;
            try { var sh = Shader.Find("Sprites/Default"); if (sh != null) _leashLine.material = new Material(sh); } catch { }
        }
        _leashLine.enabled = true;
        var self = PlayerFacade.Instance.pca.AvatorTransform.position;
        string otherUid = _toyLinkedController.Length > 0 ? _toyLinkedController : _toyLinkedTarget;
        Vector3 other = self;
        if (_relayPositions.TryGetValue(otherUid, out var rp))
            other = new Vector3(rp.X, rp.Y, rp.Z);

        // 被牵引者：超出 3 米 -> 先阻止(爬行)，持续 3 秒仍超则断开（除非永不断开）
        float dist = Vector3.Distance(self, other);
        // 控制器失联防护：对方不在位置表/超过 40 秒无更新，自动解除链接，防止"原地卡死"
        if (_toyLinkedController.Length > 0)
        {
            bool controllerMissing = !_relayPositions.TryGetValue(_toyLinkedController, out var _);
            if (controllerMissing || !_relayGhostLastSeen.TryGetValue(_toyLinkedController, out var lastSeenT) ||
                Time.unscaledTime - lastSeenT > 40f)
            {
                RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_revoke" });
                _toyLinkedTargets.Remove(_toyLinkedController);
                _toyLinkedTarget = "";
                _toyLinkedController = "";
                _leashOverSince = 0f;
                ResetToyLocal();
                ApplyCrouch(false);
                if (_leashLine != null) _leashLine.enabled = false;
                AddRelayLine("[主仆] 控制器已失联，自动解除");
                return;
            }
        }
        if (_toyLinkedController.Length > 0 && dist > 3f)
        {
            ApplyCrouch(true);
            if (_leashOverSince <= 0f) _leashOverSince = Time.unscaledTime;
            if (!_toyNeverBreak && Time.unscaledTime - _leashOverSince > 3f)
            {
                RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_revoke" });
                _toyLinkedTarget = "";
                _toyLinkedController = "";
                ResetToyLocal();
                if (_leashLine != null) _leashLine.enabled = false;
                AddRelayLine("[主仆] 距离过远，牵引已断开");
                return;
            }
        }
        else if (_toyLinkedController.Length > 0 && _leashOverSince > 0f)
        {
            _leashOverSince = 0f;
            ApplyCrouch(false);
        }

        // 近则下垂弯曲，远则拉直
        Vector3 a0 = self + Vector3.up * 1.2f;
        Vector3 b0 = other + Vector3.up * 1.2f;
        float sag = Mathf.Max(0f, (3f - dist) * 0.25f);
        Vector3 mid = (a0 + b0) * 0.5f + Vector3.down * sag;
        _leashLine.positionCount = 3;
        _leashLine.SetPosition(0, a0);
        _leashLine.SetPosition(1, mid);
        _leashLine.SetPosition(2, b0);
    }
    catch { }
}

[HideFromIl2Cpp]
private void ShowGameRules()
{
    AddRelayLine("[规则] 捉迷藏：开局传送商场，随机抓 1 个抓捕者(6人以上2个)。");
    AddRelayLine("[规则] 躲藏者戴眼罩速度 0.95，抓者 1.15 且会逐渐提速到 1.5。");
    AddRelayLine("[规则] 每人 2 命；F 键 2 米内抓捕。被抓：抓者停 5 秒减速 3 秒，被抓者 2 倍速 4 秒。");
    AddRelayLine("[规则] 躲藏者按 F 在露出口点停留 1.5 分钟；人前露出(近 NPC)速度翻倍；被 NPC 发现 +20 秒(单点上限 3 分钟)。");
    AddRelayLine("[规则] 5 个点全开后开放 2 个出口，按 F 逃离。抓 80% 判抓者赢，赛后投票 MVP。");
}

[HideFromIl2Cpp]
private void UpdateGameEffect()
{
    if (_gamePhase != "playing") return;
    if (_gameCaughtRed && Time.unscaledTime >= _gameRedUntil)
    {
        _gameCaughtRed = false;
        ApplyGameEffect();
    }
}

[HideFromIl2Cpp]
private int CurrentFloorNumber()
{
    try
    {
        var ec = ElevatorController.Instance;
        if (ec != null) return ec.CurrentFloor;
    }
    catch { }
    if (!InGame) return -1;
    return Mathf.RoundToInt(PlayerFacade.Instance.pca.AvatorTransform.position.y / 3f);
}

[HideFromIl2Cpp]
private int NextViewStage()
{
    var stages = new List<int>();
    int own = CurrentStageInt();
    if (own >= 0) stages.Add(own);
    foreach (var rp in _relayPositions.Values)
        if (rp.Stage >= 0 && !stages.Contains(rp.Stage)) stages.Add(rp.Stage);
    if (stages.Count == 0) return -1;
    int idx = stages.IndexOf(_viewStage);
    idx = (idx + 1) % stages.Count;
    return stages[idx];
}

private static Texture2D BuildMapMarkerTexture(int kind)
{
    Texture2D tex = new Texture2D(32, 32);
    tex.hideFlags = HideFlags.HideAndDontSave;
    tex.filterMode = FilterMode.Bilinear;
    tex.wrapMode = TextureWrapMode.Clamp;
    for (int py = 0; py < 32; py++)
    {
        for (int px = 0; px < 32; px++)
        {
            float nx = (px + 0.5f - 16f) / 16f;
            float ny = (py + 0.5f - 16f) / 16f;
            float d = Mathf.Sqrt(nx * nx + ny * ny);
            bool on = kind == 0 ? d <= 0.92f :
                (kind == 1 ? d >= 0.66f && d <= 0.94f :
                ny >= -0.78f && ny <= 0.88f && Mathf.Abs(nx) <= (0.88f - ny) * 0.58f);
            tex.SetPixel(px, py, on ? Color.white : Color.clear);
        }
    }
    tex.Apply();
    return tex;
}

private static Texture2D MapCircleTex()
{
    if (_mapCircleTex == null) _mapCircleTex = BuildMapMarkerTexture(0);
    return _mapCircleTex;
}

private static Texture2D MapRingTex()
{
    if (_mapRingTex == null) _mapRingTex = BuildMapMarkerTexture(1);
    return _mapRingTex;
}

private static Texture2D MapTriangleTex()
{
    if (_mapTriangleTex == null) _mapTriangleTex = BuildMapMarkerTexture(2);
    return _mapTriangleTex;
}

[HideFromIl2Cpp]
private void DrawMap(float x, float y, float size)
{
    int floor = CurrentFloorNumber();
    int ownStage = CurrentStageInt();
    int viewStage = _viewStage >= 0 ? _viewStage : ownStage;
    GUI.Box(new Rect(x, y, size, size), "");
    if (_canLabel)
        GUI.Label(new Rect(x + 10f, y + 5f, size - 20f, 22f),
            (viewStage >= 0 ? StageName(viewStage) : "平面地图") + (floor >= 0 ? " · " + floor + " 层" : ""));
    if (!InGame)
    {
        if (_canLabel) GUI.Label(new Rect(x + 10f, y + 34f, size - 20f, 24f), "进入游戏后显示地图");
        return;
    }

    var self = PlayerFacade.Instance.pca.AvatorTransform;
    Vector3 selfPos = self.position;
    if (!_mapHasStart || _mapStartStage != ownStage)
    {
        _mapHasStart = true;
        _mapStartPos = selfPos;
        _mapStartStage = ownStage;
    }

    if (Time.unscaledTime - _mapCacheAt > 5f)
    {
        _mapCacheAt = Time.unscaledTime;
        _mapNpcPts.Clear(); _mapPortalPts.Clear(); _mapRoutePts.Clear(); _mapDoorPts.Clear(); _mapObstaclePts.Clear();
        _mapObstacleSizes.Clear(); _mapWallCenters.Clear(); _mapWallSizes.Clear();
        ClientLog.Write(string.Format("地图采集 stage={0} 楼层={1}", CurrentStageInt(), CurrentFloorNumber()));
        try
        {
            var nm = NpcManager.Instance;
            if (nm != null && nm.ExistNpcList != null)
                for (int i = 0; i < nm.ExistNpcList.Count && _mapNpcPts.Count < 40; i++)
                {
                    var npc = nm.ExistNpcList[i];
                    if (npc != null && npc.NpcComponent != null) _mapNpcPts.Add(npc.NpcComponent.transform.position);
                }
        }
        catch { }
        try
        {
            var portals = UnityEngine.Object.FindObjectsOfType<PortalController>();
            if (portals != null)
                for (int i = 0; i < portals.Length && _mapPortalPts.Count < 30; i++)
                    if (portals[i] != null) _mapPortalPts.Add(portals[i].transform.position);
        }
        catch { }
        try
        {
            var routes = UnityEngine.Object.FindObjectsOfType<RoutePoint>();
            if (routes != null)
                for (int i = 0; i < routes.Length && _mapRoutePts.Count < 60; i++)
                    if (routes[i] != null) _mapRoutePts.Add(routes[i].transform.position);
        }
        catch { }
        try
        {
            var doors = UnityEngine.Object.FindObjectsOfType<Door>();
            if (doors != null)
                for (int i = 0; i < doors.Length && _mapDoorPts.Count < 40; i++)
                    if (doors[i] != null) _mapDoorPts.Add(doors[i].transform.position);
        }
        catch { }
        try
        {
            var cols = UnityEngine.Object.FindObjectsOfType<Collider>();
            if (cols != null)
                for (int i = 0; i < cols.Length; i++)
                {
                    var col = cols[i];
                    if (col == null || col.isTrigger || !col.enabled) continue;
                    Vector3 cs = col.bounds.size;
                    float longSide = Mathf.Max(cs.x, Mathf.Max(cs.y, cs.z));
                    float shortSide = Mathf.Min(cs.x, Mathf.Min(cs.y, cs.z));
                    // 墙壁/地面/天花板：一个方向远大于其它方向（薄而长的墙片）
                    if (longSide > 3f && (shortSide < 0.6f || cs.y > 2.5f))
                    {
                        if (_mapWallCenters.Count < 60)
                        {
                            _mapWallCenters.Add(col.bounds.center);
                            _mapWallSizes.Add(cs);
                        }
                        continue;
                    }
                    if (longSide > 60f) continue; // 忽略超大的环境碰撞体
                    if (_mapObstaclePts.Count < 120)
                    {
                        _mapObstaclePts.Add(col.bounds.center);
                        _mapObstacleSizes.Add(cs);
                    }
                }
        }
        catch { }
    }
        ClientLog.Write("MAPDBG stage=" + CurrentStageInt() + " npc=" + _mapNpcPts.Count + " portal=" + _mapPortalPts.Count + " route=" + _mapRoutePts.Count + " door=" + _mapDoorPts.Count);

    List<Vector3> npcPts = _mapNpcPts;
    if (viewStage >= 0 && _syncNpcs.TryGetValue(viewStage, out var syncedNpcPts) && syncedNpcPts.Count > 0)
        npcPts = syncedNpcPts;

    Rect mapRect = new Rect(x + 9f, y + 29f, size - 18f, size - 66f);
    GUI.color = new Color(0.055f, 0.075f, 0.095f, 0.98f);
    GUI.DrawTexture(mapRect, WhiteTex());
    GUI.color = new Color(0.24f, 0.30f, 0.36f, 0.5f);
    for (int i = 1; i < 8; i++)
    {
        float gx = mapRect.x + mapRect.width * i / 8f;
        float gy = mapRect.y + mapRect.height * i / 8f;
        GUI.DrawTexture(new Rect(gx, mapRect.y, 1f, mapRect.height), WhiteTex());
        GUI.DrawTexture(new Rect(mapRect.x, gy, mapRect.width, 1f), WhiteTex());
    }

    _mapFitPts.Clear();
    _mapFitPts.Add(selfPos);
    if (_mapStartStage == viewStage) _mapFitPts.Add(_mapStartPos);
    foreach (var p in _mapPortalPts) if (Mathf.Abs(p.y - selfPos.y) < 10f) _mapFitPts.Add(p);
    foreach (var p in _mapRoutePts) if (Mathf.Abs(p.y - selfPos.y) < 10f) _mapFitPts.Add(p);
    foreach (var p in _mapDoorPts) if (Mathf.Abs(p.y - selfPos.y) < 10f) _mapFitPts.Add(p);
    foreach (var p in npcPts) if (Mathf.Abs(p.y - selfPos.y) < 10f) _mapFitPts.Add(p);
    foreach (var kv in _relayPositions)
        if (kv.Value.Stage < 0 || viewStage < 0 || kv.Value.Stage == viewStage)
            _mapFitPts.Add(new Vector3(kv.Value.X, kv.Value.Y, kv.Value.Z));
    if (Connected)
        foreach (var kv in _lastStates)
            if (kv.Key != PeerId && (kv.Value.Stage < 0 || viewStage < 0 || kv.Value.Stage == viewStage))
                _mapFitPts.Add(kv.Value.Pos);

    float minX = selfPos.x - 18f, maxX = selfPos.x + 18f;
    float minZ = selfPos.z - 18f, maxZ = selfPos.z + 18f;
    foreach (var p in _mapFitPts)
    {
        minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
        minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
    }
    float spanX = Mathf.Max(36f, maxX - minX);
    float spanZ = Mathf.Max(36f, maxZ - minZ);
    float worldCx = (minX + maxX) * 0.5f;
    float worldCz = (minZ + maxZ) * 0.5f;
    float scale = Mathf.Min(mapRect.width / (spanX * 1.16f), mapRect.height / (spanZ * 1.16f));

    Vector2 Project(Vector3 p)
    {
        return new Vector2(mapRect.center.x + (p.x - worldCx) * scale,
            mapRect.center.y - (p.z - worldCz) * scale);
    }

    void CircleMarker(Vector3 world, Color fill, float radius, string label)
    {
        Vector2 pos = Project(world);
        if (!mapRect.Contains(pos)) return;
        GUI.color = new Color(0f, 0f, 0f, 0.92f);
        GUI.DrawTexture(new Rect(pos.x - radius - 2f, pos.y - radius - 2f, radius * 2f + 4f, radius * 2f + 4f), MapRingTex());
        GUI.color = fill;
        GUI.DrawTexture(new Rect(pos.x - radius, pos.y - radius, radius * 2f, radius * 2f), MapCircleTex());
        if (!string.IsNullOrEmpty(label) && _canLabel)
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(pos.x + radius + 3f, pos.y - 9f, 110f, 18f), label);
        }
    }

    // 墙壁：画成深灰色粗线（反映真实长度与厚度），有厚度的墙用矩形块
    for (int i = 0; i < _mapWallCenters.Count; i++)
    {
        Vector3 wc = _mapWallCenters[i];
        if (Mathf.Abs(wc.y - selfPos.y) > 10f) continue;
        Vector3 ws = _mapWallSizes[i];
        float wallW = Mathf.Max(ws.x, ws.z);
        float wallT = Mathf.Min(ws.x, ws.z);
        if (wallT <= 0f) wallT = 0.3f;
        Vector2 w1 = Project(wc);
        if (!mapRect.Contains(w1)) continue;
        float wallWPx = wallW * scale;
        float wallTPx = Mathf.Max(1.5f, wallT * scale);
        GUI.color = new Color(0.16f, 0.18f, 0.20f, 0.90f);
        if (ws.x > ws.z)
            GUI.DrawTexture(new Rect(w1.x - wallWPx * 0.5f, w1.y - wallTPx * 0.5f, wallWPx, wallTPx), WhiteTex());
        else
            GUI.DrawTexture(new Rect(w1.x - wallTPx * 0.5f, w1.y - wallWPx * 0.5f, wallTPx, wallWPx), WhiteTex());
    }

    // 障碍物（家具/设备等）：按真实尺寸画灰色矩形，尺寸越大越显眼
    for (int i = 0; i < _mapObstaclePts.Count; i++)
    {
        Vector3 p = _mapObstaclePts[i];
        if (Mathf.Abs(p.y - selfPos.y) < 10f)
        {
            Vector2 opos = Project(p);
            if (!mapRect.Contains(opos)) continue;
            Vector3 os = _mapObstacleSizes[i];
            float oxPx = Mathf.Max(2f, os.x * scale);
            float ozPx = Mathf.Max(2f, os.z * scale);
            GUI.color = new Color(0.55f, 0.55f, 0.58f, 0.75f);
            GUI.DrawTexture(new Rect(opos.x - oxPx * 0.5f, opos.y - ozPx * 0.5f, oxPx, ozPx), WhiteTex());
        }
    }
    foreach (var p in _mapRoutePts)
        if (Mathf.Abs(p.y - selfPos.y) < 10f) CircleMarker(p, new Color(0.20f, 0.82f, 0.94f, 0.95f), 2.5f, "");
    foreach (var p in _mapDoorPts)
        if (Mathf.Abs(p.y - selfPos.y) < 10f) CircleMarker(p, new Color(1f, 0.55f, 0.12f, 1f), 4f, "门");
    foreach (var p in _mapPortalPts)
        if (Mathf.Abs(p.y - selfPos.y) < 10f) CircleMarker(p, new Color(0.72f, 0.34f, 1f, 1f), 4f, "");
    foreach (var p in npcPts)
        if (Mathf.Abs(p.y - selfPos.y) < 10f) CircleMarker(p, new Color(1f, 0.82f, 0.12f, 1f), 3.5f, "");

    if (_mapStartStage == viewStage)
        CircleMarker(_mapStartPos, new Color(0.18f, 0.92f, 0.34f, 1f), 6f, "起点");

    foreach (var kv in _relayPositions)
    {
        if (kv.Value.Stage >= 0 && viewStage >= 0 && kv.Value.Stage != viewStage) continue;
        CircleMarker(new Vector3(kv.Value.X, kv.Value.Y, kv.Value.Z),
            new Color(0.22f, 0.58f, 1f, 1f), 6f, GetGamePlayerName(kv.Key));
    }
    if (Connected)
    {
        foreach (var kv in _lastStates)
        {
            if (kv.Key == PeerId) continue;
            if (kv.Value.Stage >= 0 && viewStage >= 0 && kv.Value.Stage != viewStage) continue;
            CircleMarker(kv.Value.Pos, new Color(0.22f, 0.58f, 1f, 1f), 6f, GetPeerName(kv.Key));
        }
    }

    if (viewStage < 0 || ownStage < 0 || viewStage == ownStage)
    {
        CircleMarker(selfPos, new Color(1f, 0.22f, 0.20f, 1f), 7f, "自己");
        Vector2 selfScreen = Project(selfPos);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(self.eulerAngles.y, selfScreen);
        GUI.color = new Color(1f, 0.22f, 0.20f, 1f);
        GUI.DrawTexture(new Rect(selfScreen.x - 5f, selfScreen.y - 22f, 10f, 13f), MapTriangleTex());
        GUI.matrix = oldMatrix;
    }

    GUI.color = Color.white;
    if (_canLabel)
        GUI.Label(new Rect(x + 9f, y + size - 33f, size - 18f, 30f),
            "红=自己  蓝=玩家  绿=起点  黄=NPC  紫=传送点  青=出发点  橙=门  灰=障碍");
}

[HideFromIl2Cpp]
private string CheckRelayMods(List<Dictionary<string, object>> mods)
{
    var missing = new List<string>();
    foreach (var mod in mods)
    {
        if (JsonHelper.Int(mod, "required") != 1) continue;
        var checks = JsonHelper.StrList(mod, "checks");
        bool present = false;
        foreach (var rel in checks)
        {
            if (string.IsNullOrEmpty(rel)) continue;
            string full = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, rel.Replace('/', System.IO.Path.DirectorySeparatorChar).Replace('\\', System.IO.Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(full) || System.IO.Directory.Exists(full)) { present = true; break; }
        }
        if (!present) missing.Add(JsonHelper.Str(mod, "name"));
    }
    return string.Join(", ", missing);
}

[HideFromIl2Cpp]
private void ScrollAnnounce(ref Vector2 scroll, Rect rect, float maxScroll)
{
    if (maxScroll <= 0f) { scroll.y = 0f; return; }
    if (rect.Contains(UnityEngine.Event.current.mousePosition) && UnityEngine.Event.current.type == UnityEngine.EventType.ScrollWheel)
    {
        scroll.y += UnityEngine.Event.current.delta.y * 22f;
        UnityEngine.Event.current.Use();
    }
    scroll.y = Mathf.Clamp(scroll.y, 0f, maxScroll);
}
[HideFromIl2Cpp]
private void UpdatePubPoll()
{
    if (!_loggedIn) return;
    float interval=(_showMenu&&_menuTab=="chat")?1f:4f;
    if(Time.unscaledTime-_lastPubPoll<interval)return;
    _lastPubPoll = Time.unscaledTime;
    RunServer(() => MasterClient.PubChatList(_pubAfter), r =>
    {
        if (r.ok && r.messages.Count > 0)
        {
            foreach (var m in r.messages) _pubMsgs.Add(m);
            long maxId = _pubAfter;
            foreach (var m in r.messages) maxId = Math.Max(maxId, JsonHelper.Int(m, "id"));
            _pubAfter = maxId;
            if (_pubMsgs.Count > 100) _pubMsgs.RemoveRange(0, _pubMsgs.Count - 100);
        }
    }, err => { });
    if (_dmPeerUid > 0 && Time.unscaledTime - _lastDmRefresh >= 5f)
    {
        _lastDmRefresh = Time.unscaledTime;
        LoadDm();
    }
    if (Time.unscaledTime - _lastFriendRefresh >= 15f)
    {
        _lastFriendRefresh = Time.unscaledTime;
        RefreshFriends();
    }}

[HideFromIl2Cpp]
private void DrawAboutPanel()
{
    const float h = 22f;
    const float step = 27f;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── SFM Online 关于 ──");
    _uiY += step;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "Mod：SFM Online  v" + PluginInfo.Version);
    _uiY += step;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("about_creator") + "：@wuwupuo  @qwer");
    _uiY += step;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h * 2), Lang.Get("about_server"));
    _uiY += step * 2;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h * 2), Lang.Get("about_share"));
    _uiY += step * 2;
    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "🔗 " + Lang.Get("about_sponsor") + "：" + "https://zanzhu.wuwupuo.ccwu.cc/"))
        OpenExternalUrl("https://zanzhu.wuwupuo.ccwu.cc/");
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "🔗 Telegram：https://t.me/SFMMM11"))
        OpenExternalUrl("https://t.me/SFMMM11");
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "🔗 SFM Online 开服源码：GitHub"))
        OpenExternalUrl("https://github.com/wuwupuo/manaka-sfm-mod-");
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "🔗 其他项目：https://github.com/wuwupuo/manaka-sfm-mod-/releases"))
        OpenExternalUrl("https://github.com/wuwupuo/manaka-sfm-mod-/releases");
    _uiY += step;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "QQ群：1095532943");
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, 140f, h), Lang.Get("btn_back"))) _menuTab = "online";
}

[HideFromIl2Cpp]
private void OpenExternalUrl(string url)
{
    if (string.IsNullOrEmpty(url)) return;
    try { Application.OpenURL(url); }
    catch (Exception ex) { ClientLog.Write("打开链接失败: " + url + " " + ex.Message); }
}
[HideFromIl2Cpp]
private void DrawCreditsPanel()
{
    const float h = 22f;
    const float step = 27f;
    if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("menu_credits") + " ──");
    _uiY += step;
    foreach (var c in _credits)
    {
        if (_canLabel) GUI.Label(new Rect(_uiX, _uiY, _uiW, h),
            "[" + JsonHelper.Str(c, "region") + "] " + JsonHelper.Str(c, "name") + " - " + JsonHelper.Str(c, "contribution"));
        _uiY += step;
    }
    if (_credits.Count == 0 && _canLabel)
        GUI.Label(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("master_no_server"));
}

[HideFromIl2Cpp]
private void JoinServerRoom(string roomId)
{
    // 复用同一个玩家ID，避免重复加入导致服务器“假人”无限增长
    var playerId = !string.IsNullOrEmpty(_serverJoinPlayerId)
        ? _serverJoinPlayerId
        : _nickname + "_" + DateTime.Now.Ticks.ToString();
    var pwd = _serverJoinPasswordInput;
    RunServer(() => ServerAPI.JoinRoom(roomId, _nickname, playerId, pwd, _serverJoinServerPwd), r =>
    {
        if (r.ok)
        {
            _serverMyRoomId = r.roomId;
            _serverAdminRoomIdInput = r.roomId;
            _serverMyRoomToken = "";
            _serverJoinPlayerId = playerId;
            _passwordText = pwd; // TCP 连接必须使用同一个密码
            // 服务器返回的 host_address 可能已包含端口
            string addr = r.hostAddress;
            if (string.IsNullOrEmpty(addr)) addr = _serverAddress;
            if (!addr.Contains(":")) addr = addr + ":" + r.port;
            _addressText = addr;
            Toast(string.Format(Lang.Get("toast_room_join"), roomId));
            ClientLog.Write("服务器登记加入房间 " + roomId + "（玩家ID: " + playerId + "）");
            if (_isServerConnected && Settings.RelayMode.Value)
                JoinRoomRelay(roomId, pwd);
            else
                JoinRoom();
        }
        else
        {
            ClientLog.Write("加入房间失败 " + roomId + " -> " + (r.errorKey ?? r.error));
            Toast(Lang.GetFallback(r.errorKey, string.IsNullOrEmpty(r.error) ? Lang.Get("error") : r.error));
        }
    }, err => Toast(err));
}

[HideFromIl2Cpp]
private void LeaveServerRoom()
{
    if (string.IsNullOrEmpty(_serverMyRoomId)) return;
    var roomId = _serverMyRoomId;
    var token = _serverMyRoomToken;
    var joinPlayerId = _serverJoinPlayerId;
    _serverMyRoomId = "";
    _serverMyRoomToken = "";
    _serverJoinPlayerId = "";
    _serverIsHosting = false;
    var playerId = !string.IsNullOrEmpty(token) ? "host" : joinPlayerId;
    RunServer(() => ServerAPI.LeaveRoom(roomId, playerId, token), ok => RefreshServerRoomList(), err => RefreshServerRoomList());
}

// 主动关闭自己在公共服务器上的房间（保持服务器连接）
[HideFromIl2Cpp]
private void CloseServerRoom()
{
    if (string.IsNullOrEmpty(_serverMyRoomId)) return;
    var roomId = _serverMyRoomId;
    var token = _serverMyRoomToken;
    var joinPlayerId = _serverJoinPlayerId;
    _serverMyRoomId = "";
    _serverMyRoomToken = "";
    _serverJoinPlayerId = "";
    _serverMyRoomPassword = "";
    _serverIsHosting = false;
    _serverCaptchaVerified = false;
    _serverCaptchaInput = "";
    _serverCaptchaDisplay = "";
    _serverCaptchaTex = null;
    _serverCaptchaImageBase64 = "";
    if (IsHosting) StopHosting();
    var playerId = !string.IsNullOrEmpty(token) ? "host" : joinPlayerId;
    RunServer(() => ServerAPI.LeaveRoom(roomId, playerId, token), ok =>
    {
        Toast(ok ? Lang.Get("toast_room_closed") : Lang.Get("refresh_failed"));
        RefreshServerRoomList();
    }, err => Toast(err));
}

// ========== 客户端管理操作（登录并确认后可用） ==========
[HideFromIl2Cpp]
private void AdminDeleteSelectedRoom()
{
    if (string.IsNullOrEmpty(_selectedServerRoomId))
    {
        Toast(Lang.Get("admin_select_room"));
        return;
    }
    var roomId = _selectedServerRoomId;
    RunServer(() => ServerAPI.AdminDeleteRoom(roomId), ok =>
    {
        Toast(ok ? Lang.Get("toast_room_deleted") : Lang.Get("refresh_failed"));
        if (ok) RefreshServerRoomList();
    }, err => Toast(err));
}

[HideFromIl2Cpp]
private void AdminBanIp()
{
    var ip = _serverAdminIpInput.Trim();
    if (ip.Length == 0)
    {
        Toast(Lang.Get("admin_ip_required"));
        return;
    }
    RunServer(() => ServerAPI.AdminBanIP(ip, "客户端管理操作", 7),
        ok => Toast(ok ? Lang.Get("admin_banned") : Lang.Get("refresh_failed")), err => Toast(err));
}

[HideFromIl2Cpp]
private void AdminUnbanIp()
{
    var ip = _serverAdminIpInput.Trim();
    if (ip.Length == 0)
    {
        Toast(Lang.Get("admin_ip_required"));
        return;
    }
    RunServer(() => ServerAPI.AdminUnbanIP(ip),
        ok => Toast(ok ? Lang.Get("admin_unbanned") : Lang.Get("refresh_failed")), err => Toast(err));
}

[HideFromIl2Cpp]
private void AdminSetAnnouncement()
{
    var content = _serverAdminAnnouncement.Trim();
    if (content.Length == 0)
    {
        Toast(Lang.Get("admin_announcement_required"));
        return;
    }
    RunServer(() => ServerAPI.AdminSetAnnouncement(content), ok =>
    {
        if (ok)
        {
            _serverAnnouncement = content;
            Toast(Lang.Get("admin_announcement_updated"));
        }
        else Toast(Lang.Get("refresh_failed"));
    }, err => Toast(err));
}

[HideFromIl2Cpp]
private void AdminExportLogs()
{
    RunServer(() => ServerAPI.AdminExportLogs(),
        ok => Toast(ok ? Lang.Get("admin_logs_exported") : Lang.Get("refresh_failed")), err => Toast(err));
}

[HideFromIl2Cpp]
private void AdminClearAnnouncement()
{
    RunServer(() => ServerAPI.AdminClearAnnouncement(),
        ok =>
        {
            if (ok)
            {
                _serverAnnouncement = "";
                Toast(Lang.Get("admin_announcement_cleared"));
            }
            else Toast(Lang.Get("refresh_failed"));
        }, err => Toast(err));
}

[HideFromIl2Cpp]
private void AdminFetchRoomChat()
{
    var roomId = _serverAdminRoomIdInput.Trim();
    if (roomId.Length == 0)
    {
        Toast(Lang.Get("admin_room_id_required"));
        return;
    }
    RunServer(() => ServerAPI.AdminRoomChat(roomId), msgs =>
    {
        _serverAdminChat = msgs ?? new List<ServerChatMessage>();
        _serverAdminChatStatus = _serverAdminChat.Count == 0 ? Lang.Get("admin_chat_empty") : "";
    }, err => Toast(err));
}

[HideFromIl2Cpp]
private void AdminSendRoomMsg()
{
    var roomId = _serverAdminRoomIdInput.Trim();
    var msg = _serverAdminRoomMsgInput.Trim();
    if (roomId.Length == 0 || msg.Length == 0)
    {
        Toast(Lang.Get("admin_msg_required"));
        return;
    }
    RunServer(() => ServerAPI.AdminSendRoomMessage(roomId, msg), ok =>
    {
        if (ok)
        {
            _serverAdminRoomMsgInput = "";
            Toast(Lang.Get("admin_msg_sent"));
            AdminFetchRoomChat();
        }
        else Toast(Lang.Get("refresh_failed"));
    }, err => Toast(err));
}

[HideFromIl2Cpp]
private void AdminLoadSettings()
{
    RunServer(() => ServerAPI.AdminGetSettings(), s =>
    {
        if (s == null)
        {
            Toast(Lang.Get("refresh_failed"));
            return;
        }
        _serverSettings = s;
        _cfgMaxRoomsTotal = s.max_rooms_total.ToString();
        _cfgMaxRoomsPerIp = s.max_rooms_per_ip.ToString();
        _cfgMaxRoomsPerHour = s.max_rooms_per_hour.ToString();
        _cfgRoomLifetime = s.room_lifetime.ToString();
        _cfgRoomTimeout = s.room_timeout.ToString();
        _cfgMaxPlayers = s.max_players.ToString();
        _cfgChatLogDays = s.chat_log_days.ToString();
        _cfgActionLogDays = s.action_log_days.ToString();
        _cfgCaptchaExpire = s.captcha_expire.ToString();
        Toast(Lang.Get("admin_settings_loaded"));
    }, err => Toast(err));
}

[HideFromIl2Cpp]
private void AdminSaveSettings()
{
    var s = new ServerSettingsInfo();
    bool okParse =
        int.TryParse(_cfgMaxRoomsTotal.Trim(), out s.max_rooms_total) &&
        int.TryParse(_cfgMaxRoomsPerIp.Trim(), out s.max_rooms_per_ip) &&
        int.TryParse(_cfgMaxRoomsPerHour.Trim(), out s.max_rooms_per_hour) &&
        int.TryParse(_cfgRoomLifetime.Trim(), out s.room_lifetime) &&
        int.TryParse(_cfgRoomTimeout.Trim(), out s.room_timeout) &&
        int.TryParse(_cfgMaxPlayers.Trim(), out s.max_players) &&
        int.TryParse(_cfgChatLogDays.Trim(), out s.chat_log_days) &&
        int.TryParse(_cfgActionLogDays.Trim(), out s.action_log_days) &&
        int.TryParse(_cfgCaptchaExpire.Trim(), out s.captcha_expire);
    if (!okParse)
    {
        Toast(Lang.Get("admin_settings_invalid"));
        return;
    }
    RunServer(() => ServerAPI.AdminSaveSettings(s), ok =>
    {
        if (ok)
        {
            _serverSettings = s;
            Toast(Lang.Get("admin_settings_saved"));
        }
        else Toast(Lang.Get("refresh_failed"));
    }, err => Toast(err));
}

// ========== 服务器聊天 ==========
[HideFromIl2Cpp]
private void SendServerChat()
{
    if (!_isServerConnected || string.IsNullOrEmpty(_serverMyRoomId)) return;
    if (!PrepareOutgoingChat(_serverChatInput, out string text)) return;
    _serverChatInput = "";
    _serverChatMessages.Add((_authUsername.Length > 0 ? _authUsername : _nickname) + ": " + text);
    if (_serverChatMessages.Count > 100) _serverChatMessages.RemoveAt(0);
    var roomId = _serverMyRoomId;
    var playerId = _nickname + "_" + DateTime.Now.Ticks.ToString();
    RunServer(() => ServerAPI.SendChat(roomId, playerId, _nickname, text),
        ok =>
        {
            if (ok) RefreshServerChat();
            else Toast(Lang.Get("refresh_failed"));
        },
        err => Toast(err));
}

[HideFromIl2Cpp]
private void RefreshServerChat()
{
    if (!_isServerConnected || string.IsNullOrEmpty(_serverMyRoomId)) return;
    var roomId = _serverMyRoomId;
    RunServer(() => ServerAPI.GetChat(roomId, 30), msgs =>
    {
        if (msgs != null)
        {
            _serverChatMessages.Clear();
            foreach (var m in msgs)
                _serverChatMessages.Add(m.player_name + ": " + m.message);
        }
    }, err => { });
}

[HideFromIl2Cpp]
private void DrawProfilePanel()
{
    const float h = 22f;
    const float step = 27f;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("menu_profile") + " ──");
    _uiY += step;
    if (_profileUid > 0)
    {
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h * 2), _profileInfo);
        _uiY += step * 2;
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, 60f, h), Lang.Get("profile_report_reason"));
        _reportReason = UiTextField("report_reason", new Rect(_uiX + 64f, _uiY, 180f, h), _reportReason, false, out _);
        if (_canButton && SButton(new Rect(_uiX + 248f, _uiY, 80f, h), Lang.Get("profile_report")))
            RunServer(() => MasterClient.ReportAdd(_authToken, _profileUid, _reportReason), r =>
            { Toast(r.msg); if (r.ok) { _reportReason = ""; _menuTab = "friend"; } }, err => Toast(err));
        _uiY += step;
        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("btn_back")))
        { _profileUid = 0; _menuTab = "friend"; }
        _uiY += step;
        return;
    }
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, 80f, h), Lang.Get("profile_bio"));
    _bioInput = UiTextField("bio", new Rect(_uiX + 84f, _uiY, 220f, h), _bioInput, false, out _);
    if (_canButton && SButton(new Rect(_uiX + 308f, _uiY, 80f, h), Lang.Get("profile_save")))
        RunServer(() => MasterClient.ProfileSet(_authToken, _bioInput), r => Toast(r.msg), err => Toast(err));
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("btn_logout")))
        AuthLogout();
    _uiY += step;
}

[HideFromIl2Cpp]
private void DrawDmPanel()
{
    const float h = 22f;
    const float step = 27f;
    if (_dmLastId == 0 || Time.unscaledTime - _lastDmRefresh > 5f)
    {
        _lastDmRefresh = Time.unscaledTime;
        LoadDm();
    }    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("dm_title") + "（UID " + _dmPeerUid + "）──");
    _uiY += step;
    foreach (var m in _dmList)
    {
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, 18f), m);
        _uiY += 20f;
    }
    _dmInput = UiTextField("dm_input", new Rect(_uiX, _uiY, _uiW - 70f, h), _dmInput, false, out bool dmSubmit);
    if (dmSubmit || (_canButton && SButton(new Rect(_uiX + _uiW - 64f, _uiY, 60f, h), Lang.Get("dm_send"))))
    {
        string msg = _dmInput.Trim();
        if (msg.Length > 0)
        {
            RunServer(() => MasterClient.DmSend(_authToken, _dmPeerUid, msg), r =>
            {
                Toast(r.msg);
                if (r.ok) { _dmInput = ""; LoadDm(); }
            }, err => Toast(err));
        }
    }
    _uiY += step;
    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("btn_back"))) _menuTab = "friend";
}

        [HideFromIl2Cpp]
        private void DrawAdminPanel()
        {
            const float h = 22f;
            const float step = 27f;
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("menu_admin") + " L" + _authAdminLevel + " ──");
            _uiY += step;
            if (_authAdminActions.Count == 0)
            {
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h * 2), Lang.Get("admin_no_perm"));
                _uiY += step * 2;
                return;
            }

            if (!_adminUsersLoaded && HasAdminPerm("admin_users")) AdminSearchUser();
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, 92f, h), Lang.Get("admin_search_user"));
            _adminSearchName = UiTextField("admin_search_name", new Rect(_uiX + 96f, _uiY, 140f, h), _adminSearchName, false, out _);
            if (_canButton && SButton(new Rect(_uiX + 242f, _uiY, 62f, h), Lang.Get("admin_search_btn")))
                AdminSearchUser();
            _uiY += step;
            int showU = Math.Min(_adminUsers.Count, 6);
            for (int i = 0; i < showU; i++)
            {
                var u = _adminUsers[i];
                long uid = JsonHelper.Long(u, "uid");
                string nm = JsonHelper.Str(u, "username");
                string tt = ColorTitle(JsonHelper.Str(u, "title"), JsonHelper.Str(u, "title_color"));
                if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW - 6f, h),
                    "UID " + uid + "  " + nm + tt))
                {
                    _adminSearchUid = uid;
                    _adminSearchName = nm;
                    Toast(Lang.Get("admin_target") + ": " + nm);
                }
                _uiY += step;
            }

            if (_canLabel) SLabel(new Rect(_uiX, _uiY, 34f, h), "账号ID");
            string uidStr = _adminSearchUid > 0 ? _adminSearchUid.ToString() : "";
            uidStr = UiTextField("admin_uid", new Rect(_uiX + 38f, _uiY, 110f, h), uidStr, false, out _);
            if (!long.TryParse(uidStr, out _adminSearchUid)) _adminSearchUid = 0;
            if (_canLabel) SLabel(new Rect(_uiX + 154f, _uiY, _uiW - 154f, h),
                Lang.Get("admin_target") + ": " + (_adminSearchUid > 0 ? "UID " + _adminSearchUid + (_adminSearchName.Length > 0 ? " " + _adminSearchName : "") : "-"));
            _uiY += step;

            float x = _uiX;
            if (HasAdminPerm("admin_mute") && _canButton && SButton(new Rect(x, _uiY, 86f, h), Lang.Get("admin_mute")))
            { if (AdminTargetOk()) RunServer(() => MasterClient.AdminCall(_authToken, _authUsername, "admin_mute", _adminSearchUid, ""), r => Toast(r.ok ? "OK" : JsonHelper.Str(r.data, "msg")), err => Toast(err)); }
            x += 92f;
            if (HasAdminPerm("admin_unmute") && _canButton && SButton(new Rect(x, _uiY, 86f, h), Lang.Get("admin_unmute")))
            { if (AdminTargetOk()) RunServer(() => MasterClient.AdminCall(_authToken, _authUsername, "admin_unmute", _adminSearchUid, ""), r => Toast(r.ok ? "OK" : JsonHelper.Str(r.data, "msg")), err => Toast(err)); }
            x += 92f;
            if (HasAdminPerm("admin_user_action") && _canButton && SButton(new Rect(x, _uiY, 86f, h), Lang.Get("admin_ban")))
            { if (AdminTargetOk()) RunServer(() => MasterClient.AdminCall(_authToken, _authUsername, "admin_user_action", _adminSearchUid, "ban"), r => Toast(r.ok ? "OK" : JsonHelper.Str(r.data, "msg")), err => Toast(err)); }
            x += 92f;
            if (HasAdminPerm("admin_user_action") && _canButton && SButton(new Rect(x, _uiY, 86f, h), Lang.Get("admin_unban")))
            { if (AdminTargetOk()) RunServer(() => MasterClient.AdminCall(_authToken, _authUsername, "admin_user_action", _adminSearchUid, "unban"), r => Toast(r.ok ? "OK" : JsonHelper.Str(r.data, "msg")), err => Toast(err)); }
            _uiY += step;

            if (HasAdminPerm("admin_rename"))
            {
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, 90f, h), Lang.Get("admin_rename"));
                _adminRenameInput = UiTextField("admin_rename_in", new Rect(_uiX + 94f, _uiY, 120f, h), _adminRenameInput, false, out _);
                if (_canButton && SButton(new Rect(_uiX + 220f, _uiY, 90f, h), Lang.Get("admin_rename_btn")))
                { if (AdminTargetOk()) RunServer(() => MasterClient.AdminCall(_authToken, _authUsername, "admin_rename", _adminSearchUid, _adminRenameInput), r => { Toast(r.ok ? "OK" : JsonHelper.Str(r.data, "msg")); if (r.ok) _adminRenameInput = ""; }, err => Toast(err)); }
                _uiY += step;
            }
            if (HasAdminPerm("admin_set_title"))
            {
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, 90f, h), Lang.Get("admin_set_title"));
                _adminTitleInput = UiTextField("admin_title_in", new Rect(_uiX + 94f, _uiY, 120f, h), _adminTitleInput, false, out _);
                if (_canButton && SButton(new Rect(_uiX + 220f, _uiY, 90f, h), Lang.Get("admin_title_btn")))
                { if (AdminTargetOk()) RunServer(() => MasterClient.AdminCall(_authToken, _authUsername, "admin_set_title", _adminSearchUid, _adminTitleInput, TitleColorHex()), r => { Toast(r.ok ? "OK" : JsonHelper.Str(r.data, "msg")); if (r.ok) _adminTitleInput = ""; }, err => Toast(err)); }
                _uiY += step;

                if (_canLabel) SLabel(new Rect(_uiX + 94f, _uiY, 48f, h), "颜色RGB");
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(_adminTitleR, _adminTitleG, _adminTitleB, 1f);
                GUI.Box(new Rect(_uiX + 144f, _uiY + 2f, 24f, h - 4f), "");
                GUI.backgroundColor = oldBg;
                _adminTitleR = GUI.HorizontalSlider(new Rect(_uiX + 174f, _uiY + 6f, 64f, h), _adminTitleR, 0f, 1f);
                _adminTitleG = GUI.HorizontalSlider(new Rect(_uiX + 242f, _uiY + 6f, 64f, h), _adminTitleG, 0f, 1f);
                _adminTitleB = GUI.HorizontalSlider(new Rect(_uiX + 310f, _uiY + 6f, 64f, h), _adminTitleB, 0f, 1f);
                _uiY += step;
            }
            if (HasAdminPerm("admin_level_set"))
            {
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, 90f, h), Lang.Get("admin_level_set"));
                for (int lv = 1; lv <= 5; lv++)
                {
                    if (_canButton && SButton(new Rect(_uiX + 94f + (lv - 1) * 34f, _uiY, 30f, h), "L" + lv))
                    { int lvl = lv; if (AdminTargetOk()) RunServer(() => MasterClient.AdminCall(_authToken, _authUsername, "admin_level_set", _adminSearchUid, lvl.ToString()), r => Toast(r.ok ? "OK" : JsonHelper.Str(r.data, "msg")), err => Toast(err)); }
                }
                _uiY += step;
            }
            if (HasAdminPerm("admin_block_name"))
            {
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, 90f, h), Lang.Get("admin_block_name"));
                _adminWordInput = UiTextField("admin_word_in", new Rect(_uiX + 94f, _uiY, 120f, h), _adminWordInput, false, out _);
                if (_canButton && SButton(new Rect(_uiX + 220f, _uiY, 90f, h), Lang.Get("admin_block_name")))
                { if (AdminTargetOk()) RunServer(() => MasterClient.AdminCall(_authToken, _authUsername, "admin_block_name", _adminSearchUid, _adminWordInput), r => { Toast(r.ok ? "OK" : JsonHelper.Str(r.data, "msg")); if (r.ok) _adminWordInput = ""; }, err => Toast(err)); }
                _uiY += step;
            }
            if (HasAdminPerm("admin_dm_view"))
            {
                if (_canButton && SButton(new Rect(_uiX, _uiY, 180f, h), Lang.Get("admin_dm_view")))
                { if (AdminTargetOk()) RunServer(() => MasterClient.AdminCall(_authToken, _authUsername, "admin_dm_view", _adminSearchUid, ""), r => { if (r.ok) { _adminDmView = ""; foreach (var m in JsonHelper.List(r.data, "messages")) _adminDmView += JsonHelper.Str(m, "from_name") + "(" + JsonHelper.Long(m, "from_uid") + ") → " + JsonHelper.Str(m, "to_name") + "(" + JsonHelper.Long(m, "to_uid") + "): " + JsonHelper.Str(m, "message") + "\n"; } else Toast(JsonHelper.Str(r.data, "msg")); }, err => Toast(err)); }
                _uiY += step;
                if (_adminDmView.Length > 0)
                {
                    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, 140f), _adminDmView);
                    _uiY += 145f;
                }
            }

            if (HasAdminPerm("admin_uid_change"))
            {
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, 90f, h), Lang.Get("admin_change_uid"));
                _adminNewUid = UiTextField("admin_new_uid", new Rect(_uiX + 94f, _uiY, 120f, h), _adminNewUid, false, out _);
                if (_canButton && SButton(new Rect(_uiX + 220f, _uiY, 90f, h), Lang.Get("admin_title_btn")))
                {
                    if (AdminTargetOk() && long.TryParse(_adminNewUid.Trim(), out long nuid))
                        RunServer(() => MasterClient.AdminUidChange(_authToken, _authUsername, _adminSearchUid, nuid), r =>
                        { Toast(r.ok ? "OK" : r.msg); if (r.ok) { _adminNewUid = ""; } }, err => Toast(err));
                }
                _uiY += step;
            }            if (HasAdminPerm("admin_report_list"))
            {
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("admin_reports") + " ──");
                _uiY += step;
                if (_canButton && SButton(new Rect(_uiX, _uiY, 100f, h), Lang.Get("admin_report_refresh")))
                    AdminLoadReports();
                _uiY += step;
                if (_adminReportsLoaded && _adminReports.Count == 0)
                {
                    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("admin_report_empty"));
                    _uiY += step;
                }
                int showR = Math.Min(_adminReports.Count, 20);
                for (int i = 0; i < showR; i++)
                {
                    var rp = _adminReports[i];
                    long rid = JsonHelper.Long(rp, "id");
                    string st = JsonHelper.Str(rp, "status");
                    string line = "#" + rid + " " + JsonHelper.Long(rp, "reporter_uid") + " → " + JsonHelper.Long(rp, "target_uid") + " [" + st + "] " + JsonHelper.Str(rp, "reason");
                    if (line.Length > 110) line = line.Substring(0, 110) + "...";
                    bool canHandle = HasAdminPerm("admin_report_handle") && st == "pending";
                    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW - (canHandle ? 150f : 0f), h * 2), line);
                    if (canHandle)
                    {
                        if (_canButton && SButton(new Rect(_uiX + _uiW - 144f, _uiY, 66f, h), Lang.Get("admin_report_pass")))
                            AdminHandleReport(rid, "processed");
                        if (_canButton && SButton(new Rect(_uiX + _uiW - 74f, _uiY, 66f, h), Lang.Get("admin_report_reject")))
                            AdminHandleReport(rid, "rejected");
                    }
                    _uiY += h * 2 + 3f;
                }
            }

            if (HasAdminPerm("admin_pubchat_list"))
            {
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("admin_pubchat_view") + " ──");
                _uiY += step;
                if (_canButton && SButton(new Rect(_uiX, _uiY, 100f, h), Lang.Get("admin_pubchat_refresh")))
                    AdminLoadPubchat();
                _uiY += step;
                if (_adminPubchatLoaded && _adminPubchat.Count == 0)
                {
                    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("admin_pubchat_empty"));
                    _uiY += step;
                }
                int showP = Math.Min(_adminPubchat.Count, 20);
                for (int i = 0; i < showP; i++)
                {
                    var m = _adminPubchat[i];
                    long mid = JsonHelper.Long(m, "id");
                    string official = JsonHelper.Int(m, "official") == 1 ? "[官方] " : "";
                    string ttl = ColorTitle(JsonHelper.Str(m, "title"), JsonHelper.Str(m, "title_color"));
                    string line = "#" + mid + " " + official + JsonHelper.Long(m, "sender_uid") + " " + JsonHelper.Str(m, "sender_name") + ttl + ": " + JsonHelper.Str(m, "message");
                    if (line.Length > 120) line = line.Substring(0, 120) + "...";
                    bool canDel = HasAdminPerm("admin_pubchat_del");
                    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW - (canDel ? 64f : 0f), h * 2), line);
                    if (canDel && _canButton && SButton(new Rect(_uiX + _uiW - 58f, _uiY, 58f, h), Lang.Get("admin_pubchat_del")))
                        AdminDeletePubchat(mid);
                    _uiY += h * 2 + 3f;
                }
            }

            if (HasAdminPerm("admin_users") && _canButton && SButton(new Rect(_uiX, _uiY, 140f, h), Lang.Get("profile_view")))
            {
                _profileUid = _adminSearchUid;
                _profileInfo = "UID：" + _adminSearchUid;
                _menuTab = "profile";
            }
        }

        [HideFromIl2Cpp]
        private bool AdminTargetOk()
        {
            if (_adminSearchUid < 100001 || _adminSearchUid > 999999)
            {
                Toast(Lang.Get("admin_need_uid"));
                return false;
            }
            return true;
        }

        [HideFromIl2Cpp]
        private void AdminSearchUser()
        {
            _adminUsersLoaded = true;
    string kw = _adminSearchName.Trim();
            if (kw.Length == 0) { Toast(Lang.Get("admin_need_uid")); return; }
            RunServer(() => MasterClient.AdminUsers(_authToken, _authUsername, kw), list =>
            {
                _adminUsers = list ?? new List<Dictionary<string, object>>();
                _adminUsersLoaded = true;
                Toast(_adminUsers.Count == 0 ? Lang.Get("admin_no_user") : Lang.Get("admin_target") + ": " + _adminUsers.Count);
            }, err => Toast(err));
        }

        [HideFromIl2Cpp]
        private void AdminLoadReports()
        {
            RunServer(() => MasterClient.AdminReportList(_authToken, _authUsername), list =>
            {
                _adminReports = list ?? new List<Dictionary<string, object>>();
                _adminReportsLoaded = true;
                Toast(Lang.Get("admin_reports") + ": " + _adminReports.Count);
            }, err => Toast(err));
        }

        [HideFromIl2Cpp]
        private void AdminHandleReport(long id, string status)
        {
            RunServer(() => MasterClient.AdminReportHandle(_authToken, _authUsername, id, status, ""), r =>
            {
                Toast(r.ok ? Lang.Get("admin_report_handled") : r.msg);
                if (r.ok) AdminLoadReports();
            }, err => Toast(err));
        }

        [HideFromIl2Cpp]
        private void AdminLoadPubchat()
        {
            RunServer(() => MasterClient.AdminPubchatList(_authToken, _authUsername), list =>
            {
                _adminPubchat = list ?? new List<Dictionary<string, object>>();
                _adminPubchatLoaded = true;
                Toast(Lang.Get("admin_pubchat_view") + ": " + _adminPubchat.Count);
            }, err => Toast(err));
        }

        [HideFromIl2Cpp]
        private void AdminDeletePubchat(long id)
        {
            RunServer(() => MasterClient.AdminPubchatDelete(_authToken, _authUsername, id), r =>
            {
                Toast(r.ok ? "OK" : r.msg);
                if (r.ok) AdminLoadPubchat();
            }, err => Toast(err));
        }
        [HideFromIl2Cpp]
        private bool HasAdminPerm(string p)
        {
            return _authAdminActions != null && _authAdminActions.Contains(p);
        }


// 模组同步弹窗：安装提示（确定/取消）与下载进度（独立模态框，避免与其它 UI 重叠）
[HideFromIl2Cpp]
private void DrawModSyncPrompt()
{
    float w = 460f, h = _modDownloading ? 150f : 190f;
    var rect = new Rect(Screen.width / 2f - w / 2f, Screen.height / 2f - h / 2f, w, h);
    float x = rect.x + 14f;
    float y = rect.y + 12f;
    float iw = rect.width - 28f;
    GUI.Box(rect, "");
    GUI.Label(new Rect(x, y, iw, 24f), _modDownloading ? "正在同步房主模组..." : "⚠ 房主使用了模组，需要安装才能体验完整玩法");
    y += 30f;
    if (_modDownloading)
    {
        int total = Math.Max(1, _modNeedList.Count);
        int done = Math.Min(total, _modDownloadIndex);
        GUI.Label(new Rect(x, y, iw, 20f), string.Format("进度：{0}/{1}", done, total));
        y += 26f;
        var bar = new Rect(x, y, iw, 16f);
        GUI.Box(bar, "");
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * (done / (float)total), bar.height), WhiteTex());
        y += 30f;
        if (_modDownloadError.Length > 0)
        {
            GUI.Label(new Rect(x, y, iw, 20f), "失败：" + _modDownloadError);
            y += 28f;
        }
        if (_canButton && GUI.Button(new Rect(x + iw - 80f, y, 80f, 24f), "取消"))
        {
            _modDownloading = false;
            _modNeedList.Clear();
            _modDownloadError = "";
        }
        return;
    }
    int shown = Math.Min(4, _modNeedList.Count);
    float listH = shown * 18f + 4f;
    GUI.Box(new Rect(x, y, iw, listH), "");
    float ly = y + 3f;
    for (int i = 0; i < shown; i++)
    {
        string n = _modNeedList[i];
        if (n.Length > 42) n = n.Substring(0, 42) + "...";
        GUI.Label(new Rect(x + 6f, ly, iw - 12f, 16f), n);
        ly += 18f;
    }
    if (_modNeedList.Count > shown)
        GUI.Label(new Rect(x + 6f, ly, iw - 12f, 16f), "... 共 " + _modNeedList.Count + " 个文件");
    y += listH + 8f;
    GUI.Label(new Rect(x, y, iw, 18f), "确定后自动下载并重进游戏；取消可直接进入（功能可能缺失）");
    y += 26f;
    if (_canButton && GUI.Button(new Rect(x, y, iw * 0.5f - 6f, 28f), "确定安装"))
    {
        _modPromptOpen = false;
        _modDownloading = true;
        _modDownloadError = "";
        _modDownloadIndex = 0;
        _modDownloadStartedAt = Time.unscaledTime;
        AddRelayLine("[模组] 开始下载 " + _modNeedList.Count + " 个文件");
        if (_modNeedList.Count > 0)
            RelayTcp.Send(new Dictionary<string, object> { ["t"] = "mod_file_req", ["file"] = _modNeedList[0] });
    }
    if (_canButton && GUI.Button(new Rect(x + iw * 0.5f + 6f, y, iw * 0.5f - 6f, 28f), "取消（直接进入）"))
    {
        _modPromptOpen = false;
        _modNeedList.Clear();
    }
}

[HideFromIl2Cpp]
private void DrawChatMenu()
{
    var rect = new Rect(30f, 40f, 340f, 460f);
    GUI.Box(rect, Lang.Get("menu_chat"));
    float x = rect.x + 10f;
    float y = rect.y + 30f;
    float w = rect.width - 20f;
    const float h = 22f;
    // 页签：总服聊天 / 房间聊天（直连时仅局域网聊天）
    if (Connected)
    {
        DrawChatMenuDirect(x, y, w, h, rect);
        return;
    }
    bool hasRelay = _relayConnected && _relayRoomId.Length > 0;
    if (hasRelay || _loggedIn || _isServerConnected)
    {
        // 页签栏
        string tabRoom = Lang.Get("menu_chat_room");
        string tabPub = Lang.Get("menu_pubchat");
        if (SButton(new Rect(x, y, w * 0.5f - 3f, 22f), (_chatTab == "room" ? "▶ " : "") + tabRoom)) _chatTab = "room";
        if (SButton(new Rect(x + w * 0.5f + 3f, y, w * 0.5f - 3f, 22f), (_chatTab == "pub" ? "▶ " : "") + tabPub)) _chatTab = "pub";
        y += 28f;
        if (_chatTab == "pub")
        {
            DrawChatPubTab(x, y, w, h, rect);
            return;
        }
        if (hasRelay)
        {
            GUI.Box(new Rect(x, y, w, 250f), "");
            int relayChatStart = Math.Max(0, _relayChat.Count - 11);
            float relayChatY = y + 5f;
            for (int i = relayChatStart; i < _relayChat.Count; i++)
            {
                GUI.Label(new Rect(x + 6f, relayChatY, w - 12f, 17f), _relayChat[i]);
                relayChatY += 18f;
            }
            y += 260f;
            _relayChatInput = UiTextField("relay_chat_f11", new Rect(x, y, w - 70f, h), _relayChatInput, false, out bool relaySubmit);
            if (relaySubmit || (_canButton && GUI.Button(new Rect(x + w - 64f, y, 64f, h), Lang.Get("btn_send"))))
            {
                if (PrepareOutgoingChat(_relayChatInput, out string message))
                {
                    RelayTcp.Send(new Dictionary<string, object> { ["t"] = "chat", ["room_id"] = _relayRoomId, ["d"] = message });
                    _lastRelayChatLine = (_authUsername.Length > 0 ? _authUsername : "我") + ": " + message;
                    AddRelayLine(_lastRelayChatLine);
                    _relayChatInput = "";
                }
            }
            return;
        }
        DrawChatServerTab(x, y, w, h, rect);
        return;
    }
    GUI.Label(new Rect(x, y, w, h), Lang.Get("no_connection"));
}

// 直连（局域网/房主）聊天页
[HideFromIl2Cpp]
private void DrawChatMenuDirect(float x, float y, float w, float h, Rect rect)
{
    GUI.Box(new Rect(x, y, w, 250f), "");
    int directChatStart = Math.Max(0, _chatMessages.Count - 11);
    float directChatY = y + 5f;
    for (int i = directChatStart; i < _chatMessages.Count; i++)
    {
        GUI.Label(new Rect(x + 6f, directChatY, w - 12f, 17f), _chatMessages[i]);
        directChatY += 18f;
    }
    y += 260f;
    _chatInput = UiTextField("lan_chat_f11", new Rect(x, y, w - 70f, h), _chatInput, false, out bool directSubmit);
    if (directSubmit || (_canButton && GUI.Button(new Rect(x + w - 64f, y, 64f, h), Lang.Get("btn_send"))))
        SendChat(_chatInput);
    y += 30f;
    if (IsHosting)
    {
        if (_canLabel) GUI.Label(new Rect(x, y, w, h), Lang.Get("online_players") + ":");
        y += 24f;
        foreach (var p in _peers.Values)
        {
            if (p.Id == PeerId) continue;
            if (_canLabel) GUI.Label(new Rect(x, y, w - 70f, 20f), p.Name + " (" + p.Id + ")");
            if (_canButton && GUI.Button(new Rect(x + w - 64f, y, 60f, 20f), Lang.Get("kick")))
                _host?.KickClient(p.Id);
            y += 22f;
            if (y > rect.y + rect.height - 30f) break;
        }
    }
}

// 总服公共聊天页（F11）
[HideFromIl2Cpp]
private void DrawChatPubTab(float x, float y, float w, float h, Rect rect)
{
    GUI.Box(new Rect(x, y, w, 250f), "");
    int start = Math.Max(0, _pubMsgs.Count - 11);
    float ly = y + 5f;
    for (int i = start; i < _pubMsgs.Count; i++)
    {
        var m = _pubMsgs[i];
        string from = JsonHelper.Str(m, "from");
        string msg = JsonHelper.Str(m, "m");
        if (msg.Length == 0) msg = JsonHelper.Str(m, "message");
        GUI.Label(new Rect(x + 6f, ly, w - 12f, 17f), from + ": " + msg);
        ly += 18f;
    }
    y += 260f;
    _pubInput = UiTextField("pub_chat_f11", new Rect(x, y, w - 70f, h), _pubInput, false, out bool pubSubmit);
    if (pubSubmit || (_canButton && GUI.Button(new Rect(x + w - 64f, y, 64f, h), Lang.Get("btn_send"))))
    {
        if (_pubInput.Trim().Length > 0)
            SendPubChat();
    }
    y += 30f;
    if (_canButton && GUI.Button(new Rect(x, y, w, 24f), Lang.Get("refresh")))
        UpdatePubPoll();
}

// 旧总服房间聊天页（F11 保留）
[HideFromIl2Cpp]
private void DrawChatServerTab(float x, float y, float w, float h, Rect rect)
{
    GUI.Box(new Rect(x, y, w, 250f), "");
    int start = Math.Max(0, _serverChatMessages.Count - 11);
    float ly = y + 5f;
    for (int i = start; i < _serverChatMessages.Count; i++)
    {
        GUI.Label(new Rect(x + 6f, ly, w - 12f, 17f), _serverChatMessages[i]);
        ly += 18f;
    }
    y += 260f;
    _serverChatInput = UiTextField("srv_chat_f11", new Rect(x, y, w - 70f, h), _serverChatInput, false, out bool submit);
    if (_canButton && GUI.Button(new Rect(x + w - 64f, y, 64f, h), Lang.Get("btn_send")))
        SendServerChat();
    if (submit) SendServerChat();
    y += 30f;
    if (_canButton && GUI.Button(new Rect(x, y, w, 24f), Lang.Get("refresh")))
        RefreshServerChat();
}
[HideFromIl2Cpp]
private void DrawJoinPwdPrompt()
{
    var modal = new Rect(Screen.width / 2f - 160f, Screen.height / 2f - 70f, 320f, 140f);
    GUI.Box(modal, Lang.Get("room_need_password"));
    float x = modal.x + 14f;
    float y = modal.y + 34f;
    _joinPwdPromptInput = UiTextField("srv_pwd_prompt",
        new Rect(x, y, modal.width - 28f, 24f), _joinPwdPromptInput, true, out bool submit);
    y += 34f;
    if (_canButton && GUI.Button(new Rect(x, y, 120f, 26f), Lang.Get("btn_confirm")))
    {
        _serverJoinPasswordInput = _joinPwdPromptInput;
        string room = _joinPwdPromptRoom;
        _joinPwdPromptRoom = "";
        _joinPwdPromptInput = "";
        JoinServerRoom(room);
    }
    if (_canButton && GUI.Button(new Rect(x + 130f, y, 120f, 26f), Lang.Get("btn_cancel")))
    {
        _joinPwdPromptRoom = "";
        _joinPwdPromptInput = "";
    }
    if (submit)
    {
        _serverJoinPasswordInput = _joinPwdPromptInput;
        string room = _joinPwdPromptRoom;
        _joinPwdPromptRoom = "";
        _joinPwdPromptInput = "";
        JoinServerRoom(room);
    }
}
// ========== UI绘制 ==========

internal void OnGUI()
{
    if (!_languageInitialized)
    {
        if (Time.realtimeSinceStartup < _uiReadyAt && !_showMenu && !_showChatMenu) return;
        EnsureLanguage();
    }
    EnsureFont();
    ProbeUiOnce();
    try { SFMOnline.Ext.OnlineCoreExt.TickGui(); } catch { }
    if (_canInput) { try { Input.imeCompositionMode = UnityEngine.IMECompositionMode.On; } catch { } }
    if (Settings.ShowHud.Value) DrawHud();
    
    bool composing = _canInput && !string.IsNullOrEmpty(Input.compositionString);
    // 菜单键随时可用；动作键在输入框或输入法组词时禁用，避免误操作。
    bool menuHotkeysAllowed = true;
    bool actionHotkeysAllowed = !composing && string.IsNullOrEmpty(_focusedField);
    if (menuHotkeysAllowed && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F12)
    {
        bool closeOrdinaryMenu = _showMenu && !_onlineMenuOnly;
        _onlineMenuOnly = false;
        _showMenu = !closeOrdinaryMenu;
        if (_showMenu && (_menuTab == "online" || _menuTab == "room")) _menuTab = "profile";
        _focusedField = "";
        GUI.FocusControl("");
        if (!_showMenu) _menuScrollY = 0f;
        Event.current.Use();
    }
    if (menuHotkeysAllowed && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F11)
    {
        _showChatMenu = !_showChatMenu;
        if (!_showChatMenu) _focusedField = "";
        Event.current.Use();
    }
    if (actionHotkeysAllowed && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F9 && Event.current.shift)
    {
        DumpToyControllers();
        Event.current.Use();
    }
    if (actionHotkeysAllowed && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F9 && !Event.current.shift)
    {
        _ghostDebug = !_ghostDebug;
        Event.current.Use();
        Toast(_ghostDebug ? Lang.Get("toast_ghost_debug_on") : Lang.Get("toast_ghost_debug_off"));
    }

    if (actionHotkeysAllowed && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F3 && Event.current.alt)
    {
        ForceResetAll();
        Event.current.Use();
    }
    if (menuHotkeysAllowed && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F10)
    {
        // F10 联机菜单已改在 Update() 里用 Input.GetKeyDown 检测，这里不再重复处理
        Event.current.Use();
    }
    if (_showMenu)
    {
        if (_menuDisabled)
        {
            _showMenu = false;
        }
        else
        {
            bool inGroup = false;
            try
            {
                // 低分辨率自适应：菜单不超过屏幕，超高部分用右侧滑块滚动查看
                float availH = Screen.height - 60f;
                float availW = Screen.width - 60f;
                _menuRect.width = Mathf.Min(520f, Mathf.Max(300f, availW));
                _menuRect.height = Mathf.Min(900f, Mathf.Max(320f, availH));
                _menuRect.x = Mathf.Min(_menuRect.x, Screen.width - _menuRect.width - 10f);
                _menuRect.y = Mathf.Min(_menuRect.y, Screen.height - _menuRect.height - 10f);

                GUI.Box(_menuRect, Lang.Get("title"));
                float viewH = _menuRect.height - 40f;
                float maxScroll = Mathf.Max(0f, _menuContentHeight - viewH);
                if (maxScroll > 0f)
                {
                    _menuScrollY = GUI.VerticalSlider(
                        new Rect(_menuRect.xMax - 24f, _menuRect.y + 34f, 18f, viewH),
                        _menuScrollY, maxScroll, 0f);
                    _menuScrollY = Mathf.Clamp(_menuScrollY, 0f, maxScroll);
                    var evt = Event.current;
                    if (evt.type == EventType.ScrollWheel &&
                        new Rect(_menuRect.x, _menuRect.y + 34f, _menuRect.width - 28f, viewH).Contains(evt.mousePosition))
                    {
                        _menuScrollY = Mathf.Clamp(_menuScrollY + evt.delta.y * 24f, 0f, maxScroll);
                        evt.Use();
                    }
                }
                else
                {
                    _menuScrollY = 0f;
                }

                GUI.BeginGroup(new Rect(_menuRect.x + 10f, _menuRect.y + 32f,
                    _menuRect.width - 40f, viewH + 4f));
                inGroup = true;
                try
                {
                    DrawMenu();
                    _menuContentHeight = Mathf.Max(_menuContentHeight, _uiY + 24f + _menuScrollY);
                }
                catch (Exception ex)
                {
                    PluginInfo.Warn("Menu error: " + ex);
                    _menuErrors++;
                    if (_menuErrors > 10)
                    {
                        _menuDisabled = true;
                        PluginInfo.Warn("菜单连续出错，已自动停用 F12 菜单，游戏继续运行。");
                    }
                    GUI.Label(new Rect(2f, 2f, _menuRect.width - 52f, 140f),
                        "Menu error: " + ex.Message + "\nSee BepInEx/LogOutput.log");
                }
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("Menu setup error: " + ex);
                _menuErrors++;
                if (_menuErrors > 10)
                {
                    _menuDisabled = true;
                    PluginInfo.Warn("菜单连续出错，已自动停用 F12 菜单，游戏继续运行。");
                }
            }
            finally
            {
                if (inGroup) GUI.EndGroup();
            }

            // 菜单打开时吃掉菜单外的鼠标事件，避免误点到游戏主界面对应位置
            var blk = Event.current;
            bool inChatRect = _showChatMenu &&
                new Rect(30f, 40f, 340f, 430f).Contains(blk.mousePosition);
            bool inPwdRect = !string.IsNullOrEmpty(_joinPwdPromptRoom) &&
                new Rect(Screen.width / 2f - 160f, Screen.height / 2f - 70f, 320f, 140f).Contains(blk.mousePosition);
            bool inZoom = _captchaBig && _authCaptchaTex != null && CaptchaZoomRect().Contains(blk.mousePosition);
            if (_canEventMouse && blk.isMouse && !inZoom && !_menuRect.Contains(blk.mousePosition) && !inChatRect && !inPwdRect)
                blk.Use();
        }
    }
    if (_showChatMenu)
    {
        try
        {
            DrawChatMenu();
        }
        catch (Exception ex)
        {
            PluginInfo.Warn("Chat menu error: " + ex);
            _showChatMenu = false;
        }
    }
    // 模组同步弹窗（安装提示 / 下载进度）
    if (_modPromptOpen || _modDownloading)
    {
        try { DrawModSyncPrompt(); }
        catch (Exception ex) { PluginInfo.Warn("mod prompt: " + ex.Message); }
    }
    if (!string.IsNullOrEmpty(_joinPwdPromptRoom))
    {
        DrawJoinPwdPrompt();
    }
        if (_captchaBig && _authCaptchaTex != null)
        {
            try { DrawCaptchaZoom(); }
            catch (Exception ex) { PluginInfo.Warn("Captcha zoom error: " + ex); }
        }
    if (_showChatMenu || !string.IsNullOrEmpty(_joinPwdPromptRoom))
    {
        var blk2 = Event.current;
        bool inMain = _showMenu && _menuRect.Contains(blk2.mousePosition);
        bool inChat = _showChatMenu && new Rect(30f, 40f, 340f, 430f).Contains(blk2.mousePosition);
        bool inPwd = !string.IsNullOrEmpty(_joinPwdPromptRoom) &&
            new Rect(Screen.width / 2f - 160f, Screen.height / 2f - 70f, 320f, 140f).Contains(blk2.mousePosition);
        bool inZoom2 = _captchaBig && _authCaptchaTex != null && CaptchaZoomRect().Contains(blk2.mousePosition);
        if (_canEventMouse && blk2.isMouse &&
            !inZoom2 && !inMain && !inChat && !inPwd)
            blk2.Use();
    }
    if (_modsPromptHost.Length > 0)
        DrawModsPrompt();
    DrawGhostTags();
    if (_masterForceUpdate)
    {
        var ur = new Rect(Screen.width / 2f - 240f, Screen.height / 2f - 120f, 480f, 260f);
        GUI.Box(ur, "⚠ " + Lang.Get("update_force") + " v" + _masterLatestVersion);
        if (_canLabel) GUI.Label(new Rect(ur.x + 14f, ur.y + 30f, ur.width - 28f, 40f), _masterLatestNote);
        if (_masterUpdateDownloading) { if (_canLabel) GUI.Label(new Rect(ur.x + 14f, ur.y + 76f, ur.width - 28f, 22f), Lang.Get("update_downloading")); }
        else if (_masterUpdateDownloaded)
        {
            var srcDir = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "SFMOnline_versions");
            var dst = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "plugins", "SFMOnline.dll");
            if (_canLabel) GUI.Label(new Rect(ur.x + 14f, ur.y + 60f, ur.width - 28f, 70f), "✅ 下载位置：\n" + srcDir + "\n\n请把里面的 SFMOnline.dll 覆盖到：\n" + dst + "\n然后重启游戏。");
        }
        else if (_canButton && GUI.Button(new Rect(ur.x + 14f, ur.y + 76f, ur.width - 90f, 30f), Lang.Get("btn_update") + " v" + _masterLatestVersion)) DownloadMasterUpdate();
        if (_canButton && GUI.Button(new Rect(ur.x + ur.width - 70f, ur.y + 76f, 60f, 30f), "稍后")) _masterForceUpdate = false;
        if (_canLabel) GUI.Label(new Rect(ur.x + 14f, ur.y + 112f, ur.width - 28f, 22f), _masterPendingRestart ? "ℹ " + Lang.Get("update_restart") : "");
    }    DrawToasts();
}

        [HideFromIl2Cpp]
        private Rect CaptchaZoomRect()
        {
            return new Rect(Screen.width / 2f - 260f, Screen.height / 2f - 120f, 520f, 250f);
        }

        [HideFromIl2Cpp]
        private void DrawCaptchaZoom()
        {
            var rect = CaptchaZoomRect();
            GUI.Box(rect, Lang.Get("auth_captcha"));
            float x = rect.x + 12f;
            float y = rect.y + 30f;
            if (_authCaptchaTex != null)
                GUI.DrawTexture(new Rect(x, y, 496f, 135f), _authCaptchaTex);
            y += 145f;
            if (_canLabel) GUI.Label(new Rect(x, y, 60f, 26f), Lang.Get("auth_captcha"));
            _authCaptcha = UiTextField("captcha_zoom_input", new Rect(x + 64f, y, 180f, 26f), _authCaptcha, false, out _);
            if (_canButton && GUI.Button(new Rect(x + 250f, y, 70f, 26f), Lang.Get("auth_refresh")))
                AuthGetCaptcha();
            if (_canButton && GUI.Button(new Rect(x + 326f, y, 76f, 22f), Lang.Get("auth_captcha_close")))
                _captchaBig = false;
        }

[HideFromIl2Cpp]
private void DrawModsPrompt()
{
    var modal = new Rect(Screen.width / 2f - 180f, Screen.height / 2f - 80f, 360f, 160f);
    GUI.Box(modal, Lang.Get("mods_required"));
    float x = modal.x + 12f;
    float y = modal.y + 30f;
    GUI.Label(new Rect(x, y, modal.width - 24f, 44f), Lang.Get("mods_list") + ": " + _modsPromptList);
    y += 50f;
    if (_canButton && GUI.Button(new Rect(x, y, 100f, 26f), Lang.Get("btn_install")))
        Toast(Lang.Get("mods_restart"));
    if (_canButton && GUI.Button(new Rect(x + 108f, y, 100f, 26f), Lang.Get("btn_confirm")))
        ContinueModsConnect();
    if (_canButton && GUI.Button(new Rect(x + 216f, y, 100f, 26f), Lang.Get("btn_cancel")))
        _modsPromptHost = "";
}

private static readonly Dictionary<string, string> _uiEn = new Dictionary<string, string>
{
    {"【主菜单 F12】","[Main Menu F12]"},
    {"【联机菜单 F10】","[Online Menu F10]"},
    {"── 振动器（4档） ──","── Vibrator (4 stages) ──"},
    {"振动：关闭","Vibrate: Off"},{"振动：轻微","Vibrate: Light"},{"振动：中","Vibrate: Medium"},{"振动：重","Vibrate: Heavy"},
    {"── 伸缩棒（4档） ──","── Piston (4 stages) ──"},
    {"伸缩：关闭","Piston: Off"},{"伸缩：轻微","Piston: Light"},{"伸缩：中","Piston: Medium"},{"伸缩：重","Piston: Heavy"},
    {"── 玩具穿戴 ──","── Toy Wear ──"},
    {"穿戴：阴蒂","Wear: Clit"},{"穿戴：塞肛","Wear: Anal Plug"},{"穿戴：眼罩","Wear: Blindfold"},
    {"脱下：阴蒂","Remove: Clit"},{"脱下：塞肛","Remove: Anal Plug"},{"脱下：眼罩","Remove: Blindfold"},
    {"── 坐/站 ──","── Sit/Stand ──"},
    {"坐姿切换（点一下自动换坐/站）","Toggle Sit/Stand"},
    {"── 衣服穿脱（点一下切换程度） ──","── Clothes (tap to cycle) ──"},
    {"脱衣程度：穿上→打开→半脱→全脱","Undress: On -> Open -> Half -> Full"},
    {"回归所有衣服","Restore All Clothes"},
    {"── 露出 ──","── Exposure ──"},
    {"露出：开","Exposure: On"},{"露出：关","Exposure: Off"},
    {"── 玩乳头 ──","── Nipple Play ──"},
    {"穿戴乳头玩具","Wear Nipple Toy"},{"脱下乳头玩具","Remove Nipple Toy"},
    {"── 玩屁股 ──","── Butt Play ──"},
    {"扭臀","Hip Shake"},{"蹲臀","Squat Hip"},{"插入肛塞","Insert Plug"},{"拔出肛塞","Remove Plug"},
    {"穿戴塞肛","Wear Anal Plug"},{"脱下塞肛","Remove Anal Plug"},
    {"趴下","Crawl"},{"站起","Stand"},{"恢复站立","Stand Up"},{"强制蹲走","Force Crouch-Walk"},
    {"停止高潮","Stop Climax"},{"强制高潮","Force Climax"},
    {"── 排尿 / 高潮 ──","── Pee / Climax ──"},
    {"排尿·排空","Pee (Empty)"},{"排尿·永久","Pee (Permanent)"},{"停止排尿","Stop Peeing"},{"强制高潮一次","Force Climax Once"},
    {"动作列表：收缩","Actions: Collapse"},{"动作列表：展开（全部动作）","Actions: Expand (All)"},
    {"动作ID","Action ID"},{"发送该动作","Send Action"},{"请输入有效动作ID","Enter a valid action ID"},
    {"高级控制：收缩","Advanced: Collapse"},{"高级控制：展开","Advanced: Expand"},
    {"── 已授权控制 ","── Authorized: "},{" 人 ──"," people ──"},{"选择：","Select: "},{"▶ 当前：","▶ Current: "},
    {"解除控制关系","Release Control"},{"你正被 ","You are controlled by "},{" 控制",""},{"反悔并解除控制","Cancel & Release"},
    {"收到 ","Invite from "},{" 的控制请求",""},{"同意","Accept"},{"拒绝","Decline"},
    {"── 房间玩家与控制 ──","── Players & Control ──"},{"向 ","Request control from "},{" 请求控制",""},{"将 ","Kick "},{" 踢出房间",""},
    {"── 联机服务器房间 ──","── Relay Rooms ──"},{"局域网/内网","LAN / Tunnel"},{"总服务器列表","Server List"},
    {"文字大小","Font Size"},{"＋","+"},{"－","-"},
    {"── 局域网 / 内网穿透房间 ──","── LAN / Tunnel Rooms ──"},{"端口","Port"},{"密码","Password"},
    {"创建局域网房间","Create LAN Room"},{"关闭局域网房间","Close LAN Room"},{"断开局域网房间","Disconnect LAN"},
    {"刷新房间","Refresh Rooms"},{"未发现房间，可点“刷新房间”或在下方输入地址","No rooms found. Press Refresh or enter an address"},
    {"连接","Connect"},{"尚未连接联机服务器。请先到“总服务器列表”选择服务器，连接后这里会显示该服务器自己的房间列表。","Not connected to a relay. Pick one in Server List first."},
    {"打开总服务器列表","Open Server List"},{"── 联机服 ──","── Relay ──"},{"在线 ","Online "},
    {"发送","Send"},{"── 房间列表 ──","── Room List ──"},{"加入","Join"},{"暂无房间","No rooms"},{"房间密码","Room Password"},
    {"确认加入","Join"},{"加入房间","Join Room"},{"── 创建房间 ──","── Create Room ──"},
    {"获取验证码","Get Captcha"},{"换一张","New Image"},{"创建房间","Create Room"},{"刷新房间列表","Refresh Room List"},
    {"── 聊天 ──","── Chat ──"},{"发送消息","Send Message"},
    {"强制恢复 (Alt+F3)","Force Reset (Alt+F3)"},
    {"局域网/内网房间","LAN/Tunnel Room"},{"当前局域网房间（房主）","LAN Room (Host)"},{"当前局域网/内网房间","LAN/Tunnel Room"},
    {"召集所有玩家到房主","Summon All to Host"},{"打开大地图","Open Map"},{"收起大地图","Close Map"},
    {"切换其他玩家所在地图","Switch to Other Map"},{"离开房间","Leave Room"},{"房间 ","Room "},
    {"已授权控制","Authorized"},{"当前：","Current: "},
    {"技能与服装属性：","Bonuses: "},{"房主已开启","Host ON"},{"房主已关闭","Host OFF"},
    {"关闭技能与服装属性加成","Disable Stat Bonuses"},{"允许技能与服装属性加成","Enable Stat Bonuses"},
    {"自动距离断开：关闭","Auto-Disconnect: Off"},{"自动距离断开：开启","Auto-Disconnect: On"},
    {"已断开联机服","Disconnected from relay"},{"开始连接联机服","Connecting to relay"},
    {"退出房间","Leave Room"},{"进入该服务器的房间页面","Open Room Page"},{"断开当前服务器","Disconnect Server"},
};
private static string S(string s)
{
    if (string.IsNullOrEmpty(s)) return s;
    if (Lang.Current != Language.English) return s;
    if (_uiEn.TryGetValue(s, out var en)) return en;
    return s;
}
[HideFromIl2Cpp]
private GUIStyle WrapStyle()
{
    if (_wrapStyle == null)
    {
        _wrapStyle = new GUIStyle();
        _wrapStyle.wordWrap = true;
        _wrapStyle.fontSize = GUI.skin.label != null && GUI.skin.label.font != null ? GUI.skin.label.font.fontSize : 16;
    }
    return _wrapStyle;
}private static void SLabel(Rect r, string s) { GUI.Label(r, S(s)); }
private static void SLabel(Rect r, string s, GUIStyle st) { GUI.Label(r, S(s), st ?? GUI.skin.label); }

private GUIStyle HintStyle()
{
    if (_hintStyle == null)
    {
        _hintStyle = new GUIStyle();
        _hintStyle.wordWrap = true;
        _hintStyle.fontStyle = FontStyle.Bold;
        _hintStyle.normal.textColor = new Color(1f, 0.95f, 0.58f, 1f);
        _hintStyle.padding = new RectOffset();
        _hintStyle.padding.left = 7; _hintStyle.padding.right = 7;
        _hintStyle.padding.top = 4; _hintStyle.padding.bottom = 4;
    }
    return _hintStyle;
}

private GUIStyle HudStyle()
{
    if (_hudStyle == null)
    {
        _hudStyle = new GUIStyle();
        _hudStyle.fontStyle = FontStyle.Bold;
        _hudStyle.fontSize = Mathf.Max(15, Settings.UiFontSize.Value);
        _hudStyle.normal.textColor = new Color(1f, 0.95f, 0.48f, 1f);
    }
    return _hudStyle;
}

private static bool IsHexColor(string s)
{
    if (s == null || s.Length != 6) return false;
    foreach (char c in s) if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))) return false;
    return true;
}

private static string ColorTitle(string title, string color)
{
    string t = (title ?? "").Trim().Replace("<", "").Replace(">", "");
    if (t.Length == 0) return "";
    string c = (color ?? "").Trim().ToUpperInvariant();
    if (!IsHexColor(c)) return "《" + t + "》";
    return "<color=#" + c + ">《" + t + "》</color>";
}

[HideFromIl2Cpp]
private string TitleColorHex()
{
    int r = Mathf.Clamp(Mathf.RoundToInt(_adminTitleR * 255f), 0, 255);
    int g = Mathf.Clamp(Mathf.RoundToInt(_adminTitleG * 255f), 0, 255);
    int b = Mathf.Clamp(Mathf.RoundToInt(_adminTitleB * 255f), 0, 255);
    return r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
}
private static bool SButton(Rect r, string s) { return GUI.Button(r, S(s)); }

[HideFromIl2Cpp]
private void DrawMenu()
{
    _uiX = 2f;
    _uiY = 2f - _menuScrollY;
    _uiW = _menuRect.width - 54f;
    const float h = 22f;
    const float step = 27f;

    var hintBg = GUI.backgroundColor;
    GUI.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 1f);
    GUI.Box(new Rect(_uiX, _uiY, _uiW, 43f), "");
    GUI.backgroundColor = hintBg;
    if (_canLabel) SLabel(new Rect(_uiX + 5f, _uiY + 2f, _uiW - 10f, 39f),
        "快捷键：F10 联机/房间｜F12 普通菜单/关闭｜F11 聊天｜F9 分身调试｜Shift+F10 诊断",
        HintStyle());
    _uiY += 48f;

    // 账号登录门：未登录只显示登录/注册界面
    if (_masterClientTampered)
    {
        // 客户端被修改：显示警告 + 自动更新（复用 _masterForceUpdate 流程），不阻断操作
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, 30f), "⛔ " + Lang.Get("tamper_warn") + "（已自动修复）");
        _uiY += 38f;
    }    if (_masterForceUpdate)
    {
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, 26f), "⚠️ " + Lang.Get("update_force") + " v" + _masterLatestVersion);
        _uiY += 32f;
        if (!string.IsNullOrEmpty(_masterLatestNote) && _canLabel)
        {
            SLabel(new Rect(_uiX, _uiY, _uiW, 40f), _masterLatestNote);
            _uiY += 46f;
        }
        if (_masterUpdateDownloading)
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, 24f), Lang.Get("update_downloading"));
            _uiY += 30f;
        }
        else if (_masterUpdateDownloaded)
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, 24f), "✅ " + Lang.Get("update_restart"));
            _uiY += 30f;
        }
        else if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, 32f), Lang.Get("btn_update") + " v" + _masterLatestVersion))
            DownloadMasterUpdate();
        _uiY += 38f;
        return;
    }
    if (!_loggedIn)
    {
        DrawAuthGate();
        return;
    }
    // 协议更新后强制重新同意，否则不允许使用任何功能
    if (_agLoaded && !AgreementOkLocal())
    {
        DrawReAgreePanel();
        return;
    }

    if (_canButton && SButton(new Rect(_uiX, _uiY, 110f, 22f), _languageButtonText))
    {
        Lang.ToggleLanguage();
        _languageButtonText = Lang.Get("lang_chinese") + "/" + Lang.Get("lang_english");
        Settings.Language.Value = Lang.Current == Language.Chinese ? "Chinese" : "English";
        Toast(Lang.Current == Language.Chinese ? "已切换到中文" : "Switched to English");
    }
    // 联机菜单 / 主菜单 引导切换按钮：让玩家能互相跳转，不会找不到另一个菜单
    if (_onlineMenuOnly)
    {
        if (_canButton && SButton(new Rect(_uiX + 114f, _uiY, 128f, 22f), "【主菜单 F12】"))
        {
            _onlineMenuOnly = false;
            if (_menuTab == "room" || _menuTab == "online") _menuTab = "profile";
            _focusedField = "";
            GUI.FocusControl("");
        }
    }
    else
    {
        if (_canButton && SButton(new Rect(_uiX + 114f, _uiY, 128f, 22f), "【联机菜单 F10】"))
        {
            _onlineMenuOnly = true;
            _menuTab = "room";
            _focusedField = "";
            GUI.FocusControl("");
        }
    }
    _uiY += 26f;

    // F10 放联机/房间/好友；F12 放账号、好友与普通社交功能
    const float tbW = 66f;
    int totalTabs = _onlineMenuOnly ? 3 : (_authAdminActions.Count > 0 ? 6 : 5);
    float maxTabScroll = Mathf.Max(0f, totalTabs * tbW - _uiW + 44f);
    _tabScroll = Mathf.Clamp(_tabScroll, 0f, maxTabScroll);
    if (_canButton && SButton(new Rect(_uiX, _uiY, 26f, 22f), "◀")) _tabScroll = Mathf.Max(0f, _tabScroll - tbW);
    if (_canButton && SButton(new Rect(_uiX + _uiW - 26f, _uiY, 26f, 22f), "▶")) _tabScroll = Mathf.Min(maxTabScroll, _tabScroll + tbW);
    GUI.BeginGroup(new Rect(_uiX + 28f, _uiY, _uiW - 56f, 22f));
    float tx = -_tabScroll;
    if (_onlineMenuOnly)
    {
        if(_canButton&&SButton(new Rect(tx,0f,60f,22f),Lang.Get("menu_room")))_menuTab="room";
        tx+=tbW;
        if (_canButton && SButton(new Rect(tx, 0f, 60f, 22f), Lang.Get("menu_online"))) _menuTab = "online";
        tx+=tbW;
        // 好友：有请求时显示红点
        if (_canButton && SButton(new Rect(tx, 0f, 60f, 22f), Lang.Get("menu_friend") + (_friendRequests.Count > 0 ? " ●" : ""))) _menuTab = "friend";
        if (_friendRequests.Count > 0)
        {
            GUI.color = new Color(1f, 0.15f, 0.15f, 1f);
            GUI.DrawTexture(new Rect(tx + 52f, 2f, 10f, 10f), WhiteTex());
            GUI.color = Color.white;
        }
    }
    else
    {
        if (_canButton && SButton(new Rect(tx, 0f, 60f, 22f), Lang.Get("menu_friend") + (_friendRequests.Count > 0 ? " ●" : ""))) _menuTab = "friend";
        if (_friendRequests.Count > 0)
        {
            GUI.color = new Color(1f, 0.15f, 0.15f, 1f);
            GUI.DrawTexture(new Rect(tx + 52f, 2f, 10f, 10f), WhiteTex());
            GUI.color = Color.white;
        }
        tx += tbW;
        if (_canButton && SButton(new Rect(tx, 0f, 60f, 22f), Lang.Get("menu_pubchat"))) _menuTab = "chat";
        tx += tbW;
        if (_canButton && SButton(new Rect(tx, 0f, 60f, 22f), Lang.Get("menu_credits"))) _menuTab = "credits";
        tx += tbW;
        if (_canButton && SButton(new Rect(tx, 0f, 60f, 22f), Lang.Get("menu_profile"))) _menuTab = "profile";
        tx += tbW;
        if (_authAdminActions.Count > 0 && _canButton && SButton(new Rect(tx, 0f, 60f, 22f), Lang.Get("menu_admin"))) _menuTab = "admin";
        if (_authAdminActions.Count > 0) tx += tbW;
        if (_canButton && SButton(new Rect(tx, 0f, 60f, 22f), Lang.Get("menu_about"))) _menuTab = "about";
    }
    GUI.EndGroup();
    _uiY += 28f;

    // ========== 账号信息 / 修改用户名 ==========
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW - 84f, h),
            Lang.Get("account_info") + "：" + _authUsername + " ｜ " + Lang.Get("account_uid") + "：" + _authUid);
        if (_canButton && SButton(new Rect(_uiX + _uiW - 80f, _uiY, 80f, h), Lang.Get("btn_logout")))
            AuthLogout();
        _uiY += step;
    if (!_onlineMenuOnly)
    {
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, 48f, h), Lang.Get("username"));
        _renameInput = UiTextField("rename", new Rect(_uiX + 50f, _uiY, 140f, h), _renameInput, false, out _);
        if (_canButton && SButton(new Rect(_uiX + 194f, _uiY, 90f, h), Lang.Get("btn_rename"))) DoRename();
        _uiY += step;
    }

    if (_menuTab == "room") { DrawRoomPanel(); return; }
    if (_menuTab == "online") { DrawServerConnectPanel(); return; }
    if (_menuTab == "friend") { DrawFriendPanel(); return; }
    if (_menuTab == "chat") { DrawPubChatPanel(); return; }
    if (_menuTab == "credits") { DrawCreditsPanel(); return; }
    if (_menuTab == "profile") { DrawProfilePanel(); return; }
    if (_menuTab == "dm") { DrawDmPanel(); return; }
    if (_menuTab == "admin") { DrawAdminPanel(); return; }
    if (_menuTab == "about") { DrawAboutPanel(); return; }

    // ========== 语言切换 ==========
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, 64f, h), Lang.Get("setting_language"));
    if (_canButton && SButton(new Rect(_uiX + 68f, _uiY, 120f, h), _languageButtonText))
    {
        Lang.ToggleLanguage();
        _languageButtonText = Lang.Get("lang_chinese") + "/" + Lang.Get("lang_english");
        Settings.Language.Value = Lang.Current == Language.Chinese ? "Chinese" : "English";
        Toast(Lang.Current == Language.Chinese ? "已切换到中文" : "Switched to English");
    }
    _uiY += step;

    // ========== 昵称 ==========
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, 48f, h), Lang.Get("nickname"));
    string oldNick = _nickname;
    _nickname = UiTextField("nick", new Rect(_uiX + 50f, _uiY, 180f, h), _nickname, false, out _);
    if (_nickname != oldNick) Settings.Nickname.Value = _nickname;
    _uiY += step;

    // ========== 状态 ==========
    string statusText;
    if (!Connected && !_serverIsHosting)
        statusText = Lang.Get("status") + "：" + Lang.Get("no_connection");
    else if (IsHosting)
        statusText = Lang.Get("status") + "：" + Lang.Get("hosting");
    else if (_serverIsHosting)
        statusText = Lang.Get("status") + "：" + Lang.Get("hosting") + " (服务器)";
    else if (IsClient)
        statusText = Lang.Get("status") + "：" + Lang.Get("connected") + "  ID: " + PeerId;
    else
        statusText = Lang.Get("status") + "：" + Lang.Get("no_connection");
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), statusText);
    _uiY += step;

    // ========== 主机（开房间） ==========
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("menu_host"));
    _uiY += step;

    if (_canLabel) SLabel(new Rect(_uiX, _uiY, 48f, h), Lang.Get("port"));
    _portText = UiTextField("port", new Rect(_uiX + 50f, _uiY, 70f, h), _portText, false, out _);
    if (_canLabel) SLabel(new Rect(_uiX + 128f, _uiY, 64f, h), Lang.Get("max_players"));
    _maxPlayersText = UiTextField("maxp", new Rect(_uiX + 194f, _uiY, 50f, h), _maxPlayersText, false, out _);
    _uiY += step;

    if (_canLabel) SLabel(new Rect(_uiX, _uiY, 48f, h), Lang.Get("password"));
    _passwordText = UiTextField("pass", new Rect(_uiX + 50f, _uiY, 180f, h), _passwordText, true, out _);
    _uiY += step;

    if (!IsHosting)
    {
        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, 28f), Lang.Get("btn_create_room")))
            StartHosting();
        _uiY += 35f;
    }
    else
    {
        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, 26f), Lang.Get("btn_close_room")))
            StopHosting();
        _uiY += 33f;
        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, 24f), Lang.Get("btn_follow")))
            SendFollowFromLocal();
        _uiY += 31f;
    }

    // ========== 加入房间 ==========
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("menu_join"));
    _uiY += step;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, 48f, h), Lang.Get("server_address"));
    _addressText = UiTextField("addr", new Rect(_uiX + 50f, _uiY, 250f, h), _addressText, false, out _);
    _uiY += step;
    
    if (!IsHosting)
    {
        if (!IsClient)
        {
            if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, 28f), Lang.Get("btn_join_room")))
                JoinRoom();
            _uiY += 35f;
        }
        else
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("connected") + "，ID: " + PeerId);
            _uiY += step;
            if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, 26f), Lang.Get("btn_disconnect")))
                DisconnectClient();
            _uiY += 33f;
        }
    }

    // ========== 局域网/内网房间（按钮始终显示） ==========
    if (!IsHosting && !IsClient && _canButton && SButton(new Rect(_uiX, _uiY, (_uiW - 6f) * 0.5f, 26f), "创建局域网房间"))
        StartHosting();
    if (_canButton && SButton(new Rect(_uiX + (_uiW + 6f) * 0.5f, _uiY, (_uiW - 6f) * 0.5f, 26f), "刷新局域网房间"))
    {
        LanDiscovery.Probe();
        Toast("正在搜索局域网/内网房间");
    }
    _uiY += 33f;
    var lanRooms = LanDiscovery.Snapshot();
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 局域网/内网房间 " + lanRooms.Count + " ──");
    _uiY += step;
    foreach (var lan in lanRooms)
    {
        string lanLabel = lan.Name + "  " + lan.Players + "/" + lan.MaxPlayers + (lan.HasPassword ? "  🔒" : "");
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW - 72f, h), lanLabel);
        if (!IsHosting && !IsClient && _canButton && SButton(new Rect(_uiX + _uiW - 68f, _uiY, 64f, h), "加入"))
        {
            _addressText = lan.Address + ":" + lan.Port;
            Settings.HostAddress.Value = _addressText;
            JoinRoom();
        }
        _uiY += step;
    }
    if (lanRooms.Count == 0)
    {
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "正在搜索同一局域网内的房间…");
        _uiY += step;
    }

    // ========== 公共服务器 ==========
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("menu_server"));
    _uiY += step;

    if (_canLabel) SLabel(new Rect(_uiX, _uiY, 60f, h), Lang.Get("server_address"));
    string oldAddr = _serverAddress;
    _serverAddress = UiTextField("srv_addr", new Rect(_uiX + 64f, _uiY, 150f, h), _serverAddress, false, out _);
    if (_serverAddress != oldAddr) Settings.ServerAddress.Value = _serverAddress;
    if (_canLabel) SLabel(new Rect(_uiX + 220f, _uiY, 30f, h), Lang.Get("port"));
    string oldPort = _serverPortText;
    _serverPortText = UiTextField("srv_port", new Rect(_uiX + 252f, _uiY, 46f, h), _serverPortText, false, out _);
    if (_serverPortText != oldPort && int.TryParse(_serverPortText, out var p))
        Settings.ServerPort.Value = p;
    _uiY += step;

    if (!_isServerConnected)
    {
        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, 26f), Lang.Get("btn_connect_server")))
            ConnectToServer();
        _uiY += 33f;
    }
    else
    {
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h),
            Lang.Get("connected") + " " + _serverAddress +
            (_serverName.Length > 0 ? "（" + _serverName + "）" : ""));
        _uiY += step;
        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, 26f), Lang.Get("btn_disconnect_server")))
            DisconnectFromServer();
        _uiY += 33f;
    }

    // 服务器管理员登录
    if (_isServerConnected)
    {
        if (!_serverIsAdmin)
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, 48f, h), Lang.Get("server_username"));
            _serverAdminUser = UiTextField("srv_admin_user", new Rect(_uiX + 50f, _uiY, 120f, h), _serverAdminUser, false, out _);
            _uiY += step;
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, 48f, h), Lang.Get("server_password"));
            _serverAdminPassword = UiTextField("srv_admin", new Rect(_uiX + 50f, _uiY, 120f, h), _serverAdminPassword, true, out _);
            if (_canButton && SButton(new Rect(_uiX + 176f, _uiY, 90f, h), Lang.Get("btn_admin_login")))
                LoginToServer();
            _uiY += step;
        }
        else
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "✅ " + Lang.Get("server_admin"));
            _uiY += step;
            if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, 24f), Lang.Get("btn_admin_logout")))
            {
                _serverIsAdmin = false;
                ServerAPI.Logout();
                Toast(Lang.Get("toast_server_disconnected"));
            }
            _uiY += 31f;
        }

        // 客户端管理操作（登录后由服务器确认过才显示）
        if (_serverIsAdmin)
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("server_admin_ops") + " ──");
            _uiY += step;

            if (_canButton && SButton(new Rect(_uiX, _uiY, 150f, 22f), Lang.Get("admin_delete_room")))
                AdminDeleteSelectedRoom();
            _uiY += 27f;

            if (_canLabel) SLabel(new Rect(_uiX, _uiY, 60f, h), Lang.Get("ip"));
            _serverAdminIpInput = UiTextField("srv_admin_ip", new Rect(_uiX + 64f, _uiY, 106f, h), _serverAdminIpInput, false, out _);
            if (_canButton && SButton(new Rect(_uiX + 174f, _uiY, 56f, 22f), Lang.Get("ban")))
                AdminBanIp();
            if (_canButton && SButton(new Rect(_uiX + 234f, _uiY, 56f, 22f), Lang.Get("unban")))
                AdminUnbanIp();
            _uiY += step;

            if (_canLabel) SLabel(new Rect(_uiX, _uiY, 60f, h), Lang.Get("server_announcement"));
            _serverAdminAnnouncement = UiTextField("srv_admin_ann", new Rect(_uiX + 64f, _uiY, 96f, h), _serverAdminAnnouncement, false, out _);
            if (_canButton && SButton(new Rect(_uiX + 164f, _uiY, 84f, 22f), Lang.Get("admin_set_announcement")))
                AdminSetAnnouncement();
            if (_canButton && SButton(new Rect(_uiX + 252f, _uiY, 100f, 22f), Lang.Get("admin_clear_announcement")))
                AdminClearAnnouncement();
            _uiY += step;

            if (_canButton && SButton(new Rect(_uiX, _uiY, 150f, 22f), Lang.Get("admin_export_logs")))
                AdminExportLogs();
            _uiY += 27f;

            // 房间聊天查询 / 发送消息
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, 46f, h), Lang.Get("room_id"));
            _serverAdminRoomIdInput = UiTextField("srv_admin_room", new Rect(_uiX + 48f, _uiY, 100f, h), _serverAdminRoomIdInput, false, out _);
            if (_canButton && SButton(new Rect(_uiX + 152f, _uiY, 84f, 22f), Lang.Get("admin_query_chat")))
                AdminFetchRoomChat();
            if (_canButton && SButton(new Rect(_uiX + 240f, _uiY, 96f, 22f), Lang.Get("btn_send")))
                AdminSendRoomMsg();
            _uiY += step;

            if (_canLabel) SLabel(new Rect(_uiX, _uiY, 46f, h), Lang.Get("message"));
            _serverAdminRoomMsgInput = UiTextField("srv_admin_msg", new Rect(_uiX + 48f, _uiY, 200f, h), _serverAdminRoomMsgInput, false, out _);
            _uiY += step;

            if (_serverAdminChat.Count > 0 || _serverAdminChatStatus.Length > 0)
            {
                GUI.Box(new Rect(_uiX, _uiY, _uiW, 96f), "");
                float ly = _uiY + 6f;
                if (_serverAdminChat.Count == 0)
                {
                    SLabel(new Rect(_uiX + 6f, ly, _uiW - 12f, 17f), _serverAdminChatStatus);
                }
                else
                {
                    int shown = Math.Min(_serverAdminChat.Count, 5);
                    for (int i = Math.Max(0, _serverAdminChat.Count - shown); i < _serverAdminChat.Count; i++)
                    {
                        var cm = _serverAdminChat[i];
                        SLabel(new Rect(_uiX + 6f, ly, _uiW - 12f, 17f),
                            $"{cm.created_at} [{cm.player_name}] ({cm.ip}) {cm.message}");
                        ly += 18f;
                    }
                }
                _uiY += 102f;
            }

            // 服务器配置
            if (_canButton && SButton(new Rect(_uiX, _uiY, 150f, 22f), Lang.Get("admin_server_settings")))
            {
                _showServerSettings = !_showServerSettings;
                if (_showServerSettings) AdminLoadSettings();
            }
            _uiY += 27f;

            if (_showServerSettings)
            {
                float colW = (_uiW - 10f) / 2f;
                float lx = _uiX;
                float rx = _uiX + colW + 10f;

                SLabel(new Rect(lx, _uiY, 70f, h), Lang.Get("setting_max_rooms_total"));
                _cfgMaxRoomsTotal = UiTextField("cfg1", new Rect(lx + 72f, _uiY, colW - 72f, h), _cfgMaxRoomsTotal, false, out _);
                SLabel(new Rect(rx, _uiY, 70f, h), Lang.Get("setting_max_rooms_per_ip"));
                _cfgMaxRoomsPerIp = UiTextField("cfg2", new Rect(rx + 72f, _uiY, colW - 72f, h), _cfgMaxRoomsPerIp, false, out _);
                _uiY += step;

                SLabel(new Rect(lx, _uiY, 70f, h), Lang.Get("setting_max_rooms_per_hour"));
                _cfgMaxRoomsPerHour = UiTextField("cfg3", new Rect(lx + 72f, _uiY, colW - 72f, h), _cfgMaxRoomsPerHour, false, out _);
                SLabel(new Rect(rx, _uiY, 70f, h), Lang.Get("setting_room_lifetime"));
                _cfgRoomLifetime = UiTextField("cfg4", new Rect(rx + 72f, _uiY, colW - 72f, h), _cfgRoomLifetime, false, out _);
                _uiY += step;

                SLabel(new Rect(lx, _uiY, 70f, h), Lang.Get("setting_room_timeout"));
                _cfgRoomTimeout = UiTextField("cfg5", new Rect(lx + 72f, _uiY, colW - 72f, h), _cfgRoomTimeout, false, out _);
                SLabel(new Rect(rx, _uiY, 70f, h), Lang.Get("setting_max_players"));
                _cfgMaxPlayers = UiTextField("cfg6", new Rect(rx + 72f, _uiY, colW - 72f, h), _cfgMaxPlayers, false, out _);
                _uiY += step;

                SLabel(new Rect(lx, _uiY, 70f, h), Lang.Get("setting_chat_log_days"));
                _cfgChatLogDays = UiTextField("cfg7", new Rect(lx + 72f, _uiY, colW - 72f, h), _cfgChatLogDays, false, out _);
                SLabel(new Rect(rx, _uiY, 70f, h), Lang.Get("setting_action_log_days"));
                _cfgActionLogDays = UiTextField("cfg8", new Rect(rx + 72f, _uiY, colW - 72f, h), _cfgActionLogDays, false, out _);
                _uiY += step;

                SLabel(new Rect(lx, _uiY, 70f, h), Lang.Get("setting_captcha_expire"));
                _cfgCaptchaExpire = UiTextField("cfg9", new Rect(lx + 72f, _uiY, colW - 72f, h), _cfgCaptchaExpire, false, out _);
                if (_canButton && SButton(new Rect(rx, _uiY, 100f, 24f), Lang.Get("btn_save")))
                    AdminSaveSettings();
                _uiY += step;
            }
        }
    }

    // 服务器房间列表
    if (_isServerConnected)
    {
        bool relayMode = Settings.RelayMode.Value;
        bool relayNew = _canToggle && GUI.Toggle(new Rect(_uiX, _uiY, _uiW, h), relayMode, Lang.Get("relay_mode"));
        if (relayNew != relayMode) Settings.RelayMode.Value = relayNew;
        _uiY += step;

        if (_canLabel) SLabel(new Rect(_uiX, _uiY, 80f, h), Lang.Get("server_pwd"));
        _serverJoinServerPwd = UiTextField("srv_server_pwd", new Rect(_uiX + 84f, _uiY, 130f, h), _serverJoinServerPwd, true, out _);
        _uiY += step;

        // 已加入房间后隐藏列表；退出后只刷新一次再重新显示。
        if (string.IsNullOrEmpty(_serverMyRoomId))
        {
        // 按房间ID直接加入（放在最上方，带分隔线）
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("direct_join") + " ──");
        _uiY += step;
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, 60f, h), Lang.Get("room_id"));
        _serverDirectJoinId = UiTextField("srv_dj_id", new Rect(_uiX + 64f, _uiY, 80f, h), _serverDirectJoinId, false, out _);
        _serverDirectJoinPwd = UiTextField("srv_dj_pwd", new Rect(_uiX + 148f, _uiY, 80f, h), _serverDirectJoinPwd, true, out _);
        if (_canButton && SButton(new Rect(_uiX + 232f, _uiY, 66f, h), Lang.Get("btn_join_room")))
        {
            var rid = _serverDirectJoinId.Trim();
            if (rid.Length == 0)
                Toast(Lang.Get("admin_room_id_required"));
            else
            {
                _serverJoinPasswordInput = _serverDirectJoinPwd;
                JoinServerRoom(rid);
            }
        }
        _uiY += step;
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "──────────────────");
        _uiY += step;

        if (_canLabel) SLabel(new Rect(_uiX, _uiY, 60f, h), Lang.Get("search"));
        _serverRoomSearch = UiTextField("srv_search", new Rect(_uiX + 64f, _uiY, 130f, h), _serverRoomSearch, false, out _);
        _uiY += step;

        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, 24f), Lang.Get("btn_refresh_list")))
            RefreshServerRoomList();
        _uiY += 31f;

        var filteredRooms = _serverRooms;
        if (!string.IsNullOrWhiteSpace(_serverRoomSearch))
        {
            var kw = _serverRoomSearch.Trim();
            filteredRooms = _serverRooms.Where(r =>
                (r.room_name ?? "").IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (r.room_id ?? "").IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        if (filteredRooms.Count > 0)
        {
            int maxDisplay = Math.Min(filteredRooms.Count, 6);
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), _serverRoomListStatus);
            _uiY += step;
            
            for (int i = 0; i < maxDisplay; i++)
            {
                var room = filteredRooms[i];
                string lockIcon = ServerAPI.TranslatePasswordStatus(room) == Lang.Get("field_password_yes") ? "🔒" : "🔓";
                string statusTag = ServerAPI.TranslateRoomStatus(room);
                string label = $"{lockIcon} {room.room_name} ({room.room_id}) | {Lang.Get("field_host_name")}:{room.host_name} | {room.player_display} | {statusTag}";
                
                if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW - 74f, 20f), label))
                {
                    _selectedServerRoomId = room.room_id;
                    if (ServerAPI.TranslatePasswordStatus(room) == Lang.Get("field_password_yes"))
                    {
                        if (_serverJoinPasswordInput.Length == 0)
                        {
                            _joinPwdPromptRoom = room.room_id;
                            _joinPwdPromptInput = "";
                        }
                        else
                            JoinServerRoom(room.room_id);
                    }
                    else
                        JoinServerRoom(room.room_id);
                }
                _uiY += 22f; // 房间按钮单独一行，密码/连接按钮放下一行
                if (_canLabel && !string.IsNullOrEmpty(_selectedServerRoomId) && _selectedServerRoomId == room.room_id)
                {
                    SLabel(new Rect(_uiX, _uiY, 60f, 20f), Lang.Get("password") + ":");
                    _serverJoinPasswordInput = UiTextField("srv_join_pwd_" + room.room_id,
                        new Rect(_uiX + 64f, _uiY, 90f, 20f), _serverJoinPasswordInput, true, out _);
                    if (_canButton && SButton(new Rect(_uiX + 158f, _uiY, 64f, 20f), Lang.Get("btn_join_room")))
                        JoinServerRoom(room.room_id);
                    _uiY += 24f;
                }
            }
            if (filteredRooms.Count > 6)
            {
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), $"... 还有 {filteredRooms.Count - 6} 个");
                _uiY += step;
            }
        }
        else
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), _serverRoomListStatus);
            _uiY += step;
        }

        }
        // 我的服务器房间：房主和加入者都显示当前房间控制。
        if (!string.IsNullOrEmpty(_serverMyRoomId))
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h),
                Lang.Get("my_room") + ": " + _serverMyRoomId +
                (_serverMyRoomPassword.Length > 0 ? "  " + Lang.Get("password") + ": " + _serverMyRoomPassword : ""));
            _uiY += step;
            if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, 24f), _serverIsHosting ? Lang.Get("btn_close_server_room") : "离开当前房间"))
            {
                if (_serverIsHosting) CloseServerRoom(); else LeaveServerRoom();
            }
            _uiY += 31f;
        }

        if (string.IsNullOrEmpty(_serverMyRoomId))
        {
        // 服务器创建房间
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("btn_create_server_room") + " ──");
        _uiY += step;

        if (_canLabel) SLabel(new Rect(_uiX, _uiY, 66f, h), Lang.Get("field_room_name"));
        _serverCreateRoomName = UiTextField("srv_create_name", new Rect(_uiX + 70f, _uiY, 120f, h), _serverCreateRoomName, false, out _);
        _uiY += step;

        if (_canLabel) SLabel(new Rect(_uiX, _uiY, 66f, h), Lang.Get("max_players"));
        _serverCreateMaxPlayersText = UiTextField("srv_create_max", new Rect(_uiX + 70f, _uiY, 50f, h), _serverCreateMaxPlayersText, false, out _);
        _uiY += step;

        if (_canLabel) SLabel(new Rect(_uiX, _uiY, 108f, h), Lang.Get("field_public_address"));
        _serverPublicAddress = UiTextField("srv_pub_addr", new Rect(_uiX + 112f, _uiY, 170f, h), _serverPublicAddress, false, out _);
        _uiY += step;
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, 16f), Lang.Get("field_public_address_hint"));
        _uiY += 20f;

        if (_canLabel) SLabel(new Rect(_uiX, _uiY, 66f, h), Lang.Get("password"));
        _serverCreateRoomPassword = UiTextField("srv_create_pwd", new Rect(_uiX + 70f, _uiY, 120f, h), _serverCreateRoomPassword, true, out _);
        _uiY += step;

        if (!_serverCaptchaVerified)
        {
            if (_canButton && SButton(new Rect(_uiX, _uiY, 100f, 24f), Lang.Get("captcha_get")))
                RequestServerCaptcha();
            if (_serverCaptchaTex != null)
            {
                GUI.DrawTexture(new Rect(_uiX + 106f, _uiY, 170f, 48f), _serverCaptchaTex);
                _uiY += 24f;
            }
            else if (!string.IsNullOrEmpty(_serverCaptchaDisplay))
            {
                if (_canLabel) SLabel(new Rect(_uiX + 106f, _uiY, 200f, h), Lang.Get("captcha") + ": " + _serverCaptchaDisplay);
            }
            _uiY += step + 6f;

            if (_canLabel) SLabel(new Rect(_uiX, _uiY, 60f, h), Lang.Get("captcha_input"));
            _serverCaptchaInput = UiTextField("srv_captcha", new Rect(_uiX + 64f, _uiY, 100f, h), _serverCaptchaInput, false, out _);
            if (_canButton && SButton(new Rect(_uiX + 170f, _uiY, 60f, h), Lang.Get("btn_verify")))
                VerifyServerCaptcha();
            _uiY += step;
        }
        else
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "✅ " + Lang.Get("captcha_verified"));
            _uiY += step;
        }

        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, 24f), Lang.Get("btn_create_server_room")))
            CreateServerRoom();
        _uiY += 31f;
        }

        // 公告
        if (!string.IsNullOrEmpty(_serverAnnouncement))
        {
            if (_announceStyle == null)
            {
                _announceStyle = new GUIStyle();
                _announceStyle.fontSize = 13;
                _announceStyle.fontStyle = FontStyle.Bold;
                _announceStyle.wordWrap = true;
                _announceStyle.alignment = TextAnchor.UpperLeft;
                _announceStyle.normal.textColor = new Color(1f, 0.92f, 0.45f);
            }
            string full = "📢 " + Lang.Get("server_announcement") + "：" + _serverAnnouncement;
            float boxH = _announceExpanded ? 150f : 44f;
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.4f, 0.32f, 0.05f, 0.95f);
            GUI.Box(new Rect(_uiX, _uiY, _uiW, boxH), "");
            GUI.backgroundColor = oldBg;
            if (_announceExpanded)
            {
                float viewW = _uiW - 34f;
                var viewRect = new Rect(_uiX + 6f, _uiY + 4f, viewW - 14f, boxH - 30f);
                if (_announceCachedText != full)
                {
                    _announceCachedText = full;
                    _announceContentHeight = Mathf.Max(150f,
                        _announceStyle.CalcHeight(new GUIContent(full), viewRect.width - 8f) + 16f);
                }
                float maxScroll = Mathf.Max(0f, _announceContentHeight - viewRect.height);
                ScrollAnnounce(ref _announceScroll, new Rect(_uiX, _uiY, _uiW, boxH), maxScroll);
                _announceScroll.y = GUI.VerticalSlider(
                    new Rect(viewRect.xMax + 2f, viewRect.y, 12f, viewRect.height),
                    _announceScroll.y, maxScroll, 0f);
                _announceScroll.y = Mathf.Clamp(_announceScroll.y, 0f, maxScroll);
                GUI.BeginGroup(viewRect);
                if (_canLabel) SLabel(new Rect(2f, 2f - _announceScroll.y, viewRect.width - 4f, _announceContentHeight), full, _announceStyle);
                GUI.EndGroup();
            }
            else
            {
                // 收起时文字裁剪在框内，不溢出
                GUI.BeginGroup(new Rect(_uiX + 8f, _uiY + 4f, _uiW - 16f, 36f));
                if (_canLabel) SLabel(new Rect(0f, 0f, _uiW - 16f, 60f), full, _announceStyle);
                GUI.EndGroup();
            }
            if (_canButton && SButton(new Rect(_uiX + _uiW - 74f, _uiY + boxH - 24f, 68f, 20f),
                _announceExpanded ? Lang.Get("announce_collapse") : Lang.Get("announce_expand")))
            {
                _announceExpanded = !_announceExpanded;
                _announceScroll = Vector2.zero;
            }
            _uiY += boxH + 14f;
        }

    }

    // ========== Mod 总服 ==========
    CheckPendingUpdate();
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── " + Lang.Get("menu_master") + " ──");
    _uiY += step;
    if (!_masterConnected)
    {
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("master_connecting_hint"));
        _uiY += step;
    }
    else
    {
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h),
            Lang.Get("master_online") + ": " + _masterOnline);
        _uiY += step;
        if (_masterForceUpdate)
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "⚠ " + Lang.Get("update_force"));
            _uiY += step;
            if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, 24f), Lang.Get("btn_update")))
                DownloadMasterUpdate();
            _uiY += 31f;
            return;
        }
        if (!string.IsNullOrEmpty(_masterAnnTitle))
        {
            if (_announceStyle == null)
            {
                _announceStyle = new GUIStyle();
                _announceStyle.fontSize = 13;
                _announceStyle.fontStyle = FontStyle.Bold;
                _announceStyle.wordWrap = true;
                _announceStyle.normal.textColor = new Color(1f, 0.92f, 0.45f);
            }
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW - 70f, h), "📢 " + _masterAnnTitle);
            if (_canButton && SButton(new Rect(_uiX + _uiW - 64f, _uiY, 60f, h), _masterAnnExpand ? Lang.Get("announce_collapse") : Lang.Get("announce_expand")))
            { _masterAnnExpand = !_masterAnnExpand; _masterAnnScroll = Vector2.zero; }
            _uiY += step;
            float abh = _masterAnnExpand ? 120f : 40f;
            var annRect = new Rect(_uiX, _uiY, _uiW, abh);
            GUI.Box(annRect, "");
            if (_masterAnnExpand)
            {
                if (_masterAnnCachedText != _masterAnnContent || Mathf.Abs(_masterAnnCachedWidth - (_uiW - 22f)) > 1f)
                {
                    _masterAnnCachedText = _masterAnnContent;
                    _masterAnnCachedWidth = _uiW - 22f;
                    _masterAnnCachedHeight = _announceStyle.CalcHeight(new GUIContent(_masterAnnContent), _uiW - 22f);
                }
                float ch = _masterAnnCachedHeight;
                float ms = Mathf.Max(0f, ch - abh + 4f);
                ScrollAnnounce(ref _masterAnnScroll, annRect, ms);
                _masterAnnScroll.y = GUI.VerticalSlider(new Rect(_uiX + _uiW - 14f, _uiY, 12f, abh), _masterAnnScroll.y, ms, 0f);
                _masterAnnScroll.y = Mathf.Clamp(_masterAnnScroll.y, 0f, ms);
                GUI.BeginGroup(new Rect(_uiX + 4f, _uiY + 2f, _uiW - 22f, abh - 4f));
                if (_canLabel) SLabel(new Rect(0f, -_masterAnnScroll.y, _uiW - 22f, ch), _masterAnnContent, _announceStyle);
                GUI.EndGroup();
            }
            else
            {
                GUI.BeginGroup(new Rect(_uiX + 4f, _uiY + 2f, _uiW - 8f, abh - 4f));
                if (_canLabel) SLabel(new Rect(0f, 0f, _uiW - 8f, abh), _masterAnnContent, _announceStyle);
                GUI.EndGroup();
            }
            _uiY += abh + 6f;
        }
        if (_masterPendingRestart && _canLabel)
        {
            SLabel(new Rect(_uiX, _uiY, _uiW, h), "⚠ " + Lang.Get("update_restart"));
            _uiY += step;
        }
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h),
            Lang.Get("master_server_list") + "  " + _masterPage + "/" + _masterTotalPages);
        _uiY += step;
        int shown = 0;
        foreach (var s in _masterServers)
        {
            string lat = s.latency_ms < 0 ? "--" : s.latency_ms + "ms";
            string pwd = s.has_password == 1 ? " 🔒密码服" : "";
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW - 76f, 34f),
                $"{s.name} [{s.region}] {s.address}:{s.port}\n在线 {s.players}/{s.max_online} 房 {s.rooms}/{s.max_rooms} {lat}{pwd}");
            if (_canButton && SButton(new Rect(_uiX + _uiW - 70f, _uiY, 66f, 22f), "选择"))
                SelectServer(s);
            _uiY += 37f;
            shown++;
            if (shown >= 5) break;
        }        if (_masterServers.Count == 0)
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("master_no_server"));
            _uiY += step;
        }
        if (_masterTotalPages > 1)
        {
            if (_canButton && SButton(new Rect(_uiX, _uiY, 60f, 20f), Lang.Get("btn_prev_page")))
                MasterPage(-1);
            if (_canButton && SButton(new Rect(_uiX + 66f, _uiY, 60f, 20f), Lang.Get("btn_next_page")))
                MasterPage(1);
            _uiY += 26f;
        }
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, 46f, h), Lang.Get("server_address"));
        _masterCustomAddr = UiTextField("master_custom", new Rect(_uiX + 48f, _uiY, 150f, h), _masterCustomAddr, false, out _);
        if (_canButton && SButton(new Rect(_uiX + 202f, _uiY, 66f, h), "连接"))
            ConnectToMasterCustom();
        if (!string.IsNullOrEmpty(_serverAddress))
        {
            _uiY += step;
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "已选：" + _serverAddress + ":" + _serverPortText + (_serverJoinServerPwd.Length > 0 ? " (密码服)" : ""));
            _uiY += step;
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, 60f, h), "服务器密码");
            _serverJoinServerPwd = UiTextField("master_pwd", new Rect(_uiX + 64f, _uiY, 130f, h), _serverJoinServerPwd, true, out _);
            if (_canButton && SButton(new Rect(_uiX + 202f, _uiY, 90f, h), _relayConnected ? "断开服务器" : "连接服务器"))
            {
                if (_relayConnected) DisconnectRelayServer();
                else ConnectSelectedServer();
            }
        }
        _uiY += step;
        if (_masterLatestVersion.Length > 0)
        {
            string verLine = Lang.Get("master_version") + ": " + PluginInfo.Version + " / " + _masterLatestVersion;
            if (_masterUpdateReady) verLine += " ★" + Lang.Get("update_available");
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW - 80f, h), verLine);
            if (_masterUpdateReady && !_masterUpdateDownloaded &&
                _canButton && SButton(new Rect(_uiX + _uiW - 74f, _uiY, 70f, h), Lang.Get("btn_update")))
                DownloadMasterUpdate();
            if (_masterUpdateDownloaded && _canLabel)
                SLabel(new Rect(_uiX, _uiY + h, _uiW, h), Lang.Get("update_downloaded_hint"));
            _uiY += step + (_masterUpdateDownloaded ? step : 0f);
        }
        string refreshLabel = _manualServerRefreshCount >= 5
            ? "刷新服务器列表（本次游戏已用完）"
            : "刷新服务器列表（剩余 " + (5 - _manualServerRefreshCount) + " 次）";
        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, 22f), refreshLabel))
            ManualRefreshMasterServers();
    
    _uiY += 28f;
    }

    // ========== 本地测试 ==========
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("menu_local"));
    _uiY += step;
    if (_canButton)
    {
        if (!_simEnabled)
        {
            if (SButton(new Rect(_uiX, _uiY, _uiW, 26f), Lang.Get("btn_toggle_sim")))
                ToggleSim(true);
        }
        else
        {
            if (SButton(new Rect(_uiX, _uiY, _uiW, 26f), Lang.Get("btn_remove_sim")))
                ToggleSim(false);
        }
        _uiY += 33f;
    }

    // ========== 内网穿透 ==========
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("menu_tunnel"));
    _uiY += step;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("lan_ip") + ": " + GetLanIp());
    _uiY += step;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), string.Format(Lang.Get("tunnel_hint"), _portText));
    _uiY += step;
    
    if (_canButton)
    {
        if (SButton(new Rect(_uiX, _uiY, 120f, 24f), Lang.Get("btn_copy_sakura")))
        {
            if (_canClipboard)
            {
                GUIUtility.systemCopyBuffer = "隧道类型: TCP\n本地IP: 127.0.0.1\n本地端口: " + _portText + "\n队友连接: 映射地址:映射端口";
                Toast(Lang.Get("toast_copied"));
            }
            else Toast(Lang.Get("toast_clipboard_unavailable"));
        }
        if (SButton(new Rect(_uiX + 126f, _uiY, 100f, 24f), Lang.Get("btn_copy_frp")))
        {
            if (_canClipboard)
            {
                GUIUtility.systemCopyBuffer = "[common]\nserver_addr = 你的frp服务器\nserver_port = 7000\ntoken = 你的token\n\n[sfm]\ntype = tcp\nlocal_ip = 127.0.0.1\nlocal_port = " + _portText + "\nremote_port = 27570";
                Toast(Lang.Get("toast_copied"));
            }
            else Toast(Lang.Get("toast_clipboard_unavailable"));
        }
        _uiY += 31f;
    }

    // ========== 玩家列表 ==========
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("menu_players"));
    _uiY += step;
    if (_canLabel)
    {
        foreach (var peer in _peers.Values)
        {
            string self = peer.Id == PeerId ? "（我）" : "";
            int pstage = GetPeerStage(peer.Id);
            string sceneMark = pstage >= 0 && CurrentStageInt() >= 0 && pstage != CurrentStageInt()
                ? Lang.Get("hud_diff_scene") : "";
            string ghostMark = Lang.Get("hud_no_ghost");
            if (_ghosts.TryGetValue(peer.Id, out var g) && g != null && g.Root != null && (g.RendererCount > 0 || g.HasMarker))
            {
                ghostMark = g.HasMarker
                    ? string.Format(Lang.Get("hud_marker"), g.CountActiveRenderers(), g.RendererCount)
                    : string.Format(Lang.Get("hud_ghost"), g.CountActiveRenderers(), g.RendererCount);
            }
            SLabel(new Rect(_uiX, _uiY, _uiW, 18f), $"{peer.Name}{self} {peer.Id}  {peer.RttMs}ms  {StageName(pstage)}{sceneMark}{ghostMark}");
            _uiY += 19f;
        }
    }

    _uiY += 6f;
    
    // ========== 设置 ==========
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("menu_settings"));
    _uiY += step;

    if (_canToggle)
    {
        _syncActions = GUI.Toggle(new Rect(_uiX, _uiY, _uiW, h), _syncActions, Lang.Get("setting_sync_actions"));
        Settings.SyncActions.Value = _syncActions;
        _uiY += step;
        _autoFollow = GUI.Toggle(new Rect(_uiX, _uiY, _uiW, h), _autoFollow, Lang.Get("setting_auto_follow"));
        Settings.AutoFollowHost.Value = _autoFollow;
        _uiY += step;
        _showHud = GUI.Toggle(new Rect(_uiX, _uiY, _uiW, h), _showHud, Lang.Get("setting_show_hud"));
        Settings.ShowHud.Value = _showHud;
        _uiY += step;
    }
    else if (_canButton)
    {
        if (SButton(new Rect(_uiX, _uiY, _uiW, 24f), (_syncActions ? "[开] " : "[关] ") + Lang.Get("setting_sync_actions")))
            _syncActions = !_syncActions;
        Settings.SyncActions.Value = _syncActions;
        _uiY += 30f;
        if (SButton(new Rect(_uiX, _uiY, _uiW, 24f), (_autoFollow ? "[开] " : "[关] ") + Lang.Get("setting_auto_follow")))
            _autoFollow = !_autoFollow;
        Settings.AutoFollowHost.Value = _autoFollow;
        _uiY += 30f;
        if (SButton(new Rect(_uiX, _uiY, _uiW, 24f), (_showHud ? "[开] " : "[关] ") + Lang.Get("setting_show_hud")))
            _showHud = !_showHud;
        Settings.ShowHud.Value = _showHud;
        _uiY += 30f;
    }

    if (_canLabel) SLabel(new Rect(_uiX, _uiY, 64f, h), Lang.Get("setting_sync_rate"));
    string oldRate = _syncRateText;
    _syncRateText = UiTextField("rate", new Rect(_uiX + 66f, _uiY, 46f, h), _syncRateText, false, out _);
    if (_syncRateText != oldRate && int.TryParse(_syncRateText, out var hz))
        Settings.SyncRateHz.Value = Math.Max(10, Math.Min(30, hz));
    _uiY += step;

    // ========== 聊天 ==========
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), Lang.Get("menu_chat"));
    _uiY += step;
    if (_canLabel)
    {
        GUI.Box(new Rect(_uiX, _uiY, _uiW, 108f), "");
        int start = Math.Max(0, _chatMessages.Count - 6);
        float ly = _uiY + 5f;
        for (int i = start; i < _chatMessages.Count; i++)
        {
            SLabel(new Rect(_uiX + 6f, ly, _uiW - 12f, 17f), _chatMessages[i]);
            ly += 17f;
        }
    }
    _uiY += 114f;

    _chatInput = UiTextField("chat", new Rect(_uiX, _uiY, _uiW - 74f, h), _chatInput, false, out bool chatSubmit);
    if (_canButton && SButton(new Rect(_uiX + _uiW - 68f, _uiY, 68f, h), Lang.Get("btn_send")))
        SendChat(_chatInput);
    if (chatSubmit) SendChat(_chatInput);
    _uiY += step + 4f;

    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, 18f), 
        string.Format(Lang.Get("version_info"), PluginInfo.Version));
    _uiY += 20f;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, 18f), Lang.Get("mod_sync_warn"));
}
// ========== HUD ==========
[HideFromIl2Cpp]
private void DrawHud()
{
    string status;
    if (IsHosting)
    {
        status = string.Format(Lang.Get("hud_host"), _host.Port, OnlineCount());
    }
    else if (IsClient)
    {
        status = string.Format(Lang.Get("hud_client"), _addressText, OnlineCount());
    }
    else if (_serverIsHosting)
    {
        status = string.Format(Lang.Get("hud_host"), ParseInt(_portText, 27570), OnlineCount());
    }
    else
    {
        status = Lang.Get("hud_offline");
    }
    var hudBg = GUI.backgroundColor;
    GUI.backgroundColor = new Color(0.035f, 0.045f, 0.065f, 0.98f);
    GUI.Box(new Rect(4f, 4f, Mathf.Min(940f, Screen.width - 8f), 50f), "");
    GUI.backgroundColor = hudBg;
    GUI.Label(new Rect(10, 8, 520, 21), status, HudStyle());
    GUI.Label(new Rect(10, 30, 920, 21),
        "F10 联机/房间 | F12 普通菜单/关闭 | F11 聊天 | F9 分身调试 | Shift+F10 诊断", HudStyle());

    int y = 48;
    foreach (var p in _peers.Values)
    {
        if (p.Id == PeerId) continue;
        string tag = p.IsHost ? "[房主]" : "";
        int pstage = GetPeerStage(p.Id);
        string sceneMark = pstage >= 0 && CurrentStageInt() >= 0 && pstage != CurrentStageInt()
            ? Lang.Get("hud_diff_scene") : "";
        string ghostMark = Lang.Get("hud_no_ghost");
        if (_ghosts.TryGetValue(p.Id, out var g) && g != null && g.Root != null && (g.RendererCount > 0 || g.HasMarker))
        {
            ghostMark = g.HasMarker
                ? string.Format(Lang.Get("hud_marker"), g.CountActiveRenderers(), g.RendererCount)
                : string.Format(Lang.Get("hud_ghost"), g.CountActiveRenderers(), g.RendererCount);
        }
        GUI.Label(new Rect(8, y, 620, 18), 
            string.Format(Lang.Get("hud_player"), tag, p.Name, p.Id, p.RttMs, StageName(pstage), sceneMark, ghostMark));
        y += 18;
        if (y > 210) break;
    }

    if (InGame)
    {
        var t = PlayerFacade.Instance.pca.AvatorTransform.position;
        int stage = CurrentStageInt();
        int floor = CurrentFloorNumber();
        if ((_relayConnected || Connected) && _canButton)
        {
            if (GUI.Button(new Rect(Screen.width - 190f, 8f, 182f, 26f), S("强制恢复 (Alt+F3)")))
                ForceResetAll();
        }
        string loc = "坐标 " + FmtCoord(t.x) + ", " + FmtCoord(t.y) + ", " + FmtCoord(t.z)
            + " | 地图 " + StageName(stage) + (floor >= 0 ? " | " + floor + " 层" : "");
        GUI.Label(new Rect(8, y, 620, 18), loc);
    }
}

[HideFromIl2Cpp]
private int GetPeerStage(string id)
{
    return _lastStates.TryGetValue(id, out var st) ? st.Stage : -1;
}

[HideFromIl2Cpp]
private int OnlineCount()
{
    int n = 0;
    foreach (var p in _peers.Values) if (p.Id != PeerId) n++;
    return n + 1;
}


[HideFromIl2Cpp]
private void DrawServerConnectPanel()
{
    float h = Mathf.Max(26f, Settings.UiFontSize.Value + 10f);
    float step = h + 6f;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 联机服务器 ──");
    _uiY += step;
    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW - 92f, h), "想开属于自己的服务器？");
    if (_canButton && SButton(new Rect(_uiX + _uiW - 86f, _uiY, 82f, h), "前往 GitHub"))
        OpenExternalUrl("https://github.com/wuwupuo/manaka-sfm-mod-");
    _uiY += step;

    if (_relayConnected)
    {
        string connectedName = _relayServerName.Length > 0 ? _relayServerName : (_serverAddress + ":" + _serverPortText);
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "已连接：" + connectedName);
        _uiY += step;
        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "进入该服务器的房间页面")) _menuTab = "room";
        _uiY += step;
        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "断开当前服务器")) DisconnectRelayServer();
        _uiY += step;
        return;
    }

    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW - 110f, h), "只选择一个服务器；创建和加入房间统一在“房间”页操作。");
    string refreshLabel = _manualServerRefreshCount >= 5 ? "刷新已用完" : "刷新 " + (5 - _manualServerRefreshCount) + "/5";
    if (_canButton && SButton(new Rect(_uiX + _uiW - 104f, _uiY, 100f, h), refreshLabel)) ManualRefreshMasterServers();
    _uiY += step + 4f;

    int shown = 0;
    foreach (var s in _masterServers)
    {
        string lat = s.latency_ms < 0 ? "--" : s.latency_ms + "ms";
        string selected = _serverAddress == s.address && _serverPortText == s.port.ToString() ? "● " : "";
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW - 96f, h * 1.6f), selected + s.name + "  " + s.region + "\n" + s.address + ":" + s.port + "  在线 " + s.players + "/" + s.max_online + "  " + lat);
        if (_canButton && SButton(new Rect(_uiX + _uiW - 90f, _uiY, 86f, h), "选择")) SelectServer(s);
        _uiY += h * 1.6f + 6f;
        shown++;
        if (shown >= 6) break;
    }
    if (_masterServers.Count == 0)
    {
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "暂未获取到服务器，请点击刷新。");
        _uiY += step;
    }
    if (_masterTotalPages > 1)
    {
        float bw = (_uiW - 6f) * 0.5f;
        if (_canButton && SButton(new Rect(_uiX, _uiY, bw, h), "上一页")) MasterPage(-1);
        if (_canButton && SButton(new Rect(_uiX + bw + 6f, _uiY, bw, h), "下一页")) MasterPage(1);
        _uiY += step;
    }

    if (!string.IsNullOrEmpty(_serverAddress))
    {
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "当前选择：" + _serverAddress + ":" + _serverPortText);
        _uiY += step;
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, 90f, h), "服务器密码");
        _serverJoinServerPwd = UiTextField("single_server_pwd", new Rect(_uiX + 94f, _uiY, _uiW - 94f, h), _serverJoinServerPwd, true, out _);
        _uiY += step;
        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "连接这个服务器")) ConnectSelectedServer();
        _uiY += step;
    }
}

    [HideFromIl2Cpp]
    private void DrawDirectRoomSession(float h, float step)
    {
        if (!Connected) return;
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h),
            (IsHosting ? "当前局域网房间（房主）" : "当前局域网/内网房间") + "  ·  " + OnlineCount() + "/10 人");
        _uiY += step;

        float half = (_uiW - 6f) * 0.5f;
        if (_canButton && SButton(new Rect(_uiX, _uiY, half, h), _showMap ? "收起大地图" : "打开大地图"))
        {
            _showMap = !_showMap;
            if (_showMap) { _mapHasStart = false; _viewStage = CurrentStageInt(); }
        }
        if (IsHosting && _canButton && SButton(new Rect(_uiX + half + 6f, _uiY, half, h), "召集所有玩家到房主")) SendFollowFromLocal();
        _uiY += step;
        if (_showMap)
        {
            float mapSize = Mathf.Min(_uiW, 430f);
            DrawMap(_uiX, _uiY, mapSize);
            _uiY += mapSize + step;
        }

        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 房间聊天（可靠优先）──");
        _uiY += step;
        GUI.Box(new Rect(_uiX, _uiY, _uiW, 108f), "");
        int chatStart = Math.Max(0, _chatMessages.Count - 6);
        float chatY = _uiY + 5f;
        for (int i = chatStart; i < _chatMessages.Count; i++)
        {
            if (_canLabel) SLabel(new Rect(_uiX + 6f, chatY, _uiW - 12f, 17f), _chatMessages[i]);
            chatY += 17f;
        }
        _uiY += 114f;
        _chatInput = UiTextField("lan_room_chat", new Rect(_uiX, _uiY, _uiW - 70f, h), _chatInput, false, out bool submit);
        if (submit || (_canButton && SButton(new Rect(_uiX + _uiW - 64f, _uiY, 60f, h), "发送")))
            SendChat(_chatInput);
        _uiY += step;

        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 房间玩家与控制 ──");
        _uiY += step;
        foreach (var peer in _peers.Values)
        {
            if (peer.Id == PeerId) continue;
            string tag = peer.IsHost ? "（房主）" : "";
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), peer.Name + tag + "  " + peer.RttMs + "ms");
            _uiY += step;
            if (!_toyLinkedTargets.Contains(peer.Id))
            {
                if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "向 " + peer.Name + " 请求控制"))
                    SendDirectControl("invite", peer.Id);
                _uiY += step;
            }
            if (IsHosting)
            {
                if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "将 " + peer.Name + " 踢出房间"))
                    _host?.KickClient(peer.Id);
                _uiY += step;
            }
        }

        if (_toyInviteFrom.Length > 0 && _peers.ContainsKey(_toyInviteFrom))
        {
            string inviter = _toyInviteFrom;
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "收到 " + GetPeerName(inviter) + " 的控制请求");
            _uiY += step;
            if (_canButton && SButton(new Rect(_uiX, _uiY, 90f, h), "同意"))
            {
                _toyLinkedController = inviter;
                SendDirectControl("accept", inviter);
                _toyInviteFrom = "";
            }
            if (_canButton && SButton(new Rect(_uiX + 98f, _uiY, 90f, h), "拒绝"))
            {
                SendDirectControl("reject", inviter);
                _toyInviteFrom = "";
            }
            _uiY += step;
        }

        var directTargets = new List<string>();
        foreach (var id in _toyLinkedTargets) if (_peers.ContainsKey(id)) directTargets.Add(id);
        if (directTargets.Count > 0)
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 已授权控制 " + directTargets.Count + "/5 人 ──");
            _uiY += step;
            foreach (var id in directTargets)
            {
                if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h),
                    (id == _toyLinkedTarget ? "▶ 当前：" : "选择：") + GetPeerName(id))) _toyLinkedTarget = id;
                _uiY += step;
            }
            if (!_peers.ContainsKey(_toyLinkedTarget)) _toyLinkedTarget = directTargets[0];
            float col = (_uiW - 6f) * 0.5f;
            DrawToySections(col, h, step, false);
            DrawToyActionSection(col, h, step, false);

            if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "解除控制关系"))
            {
                foreach (var id in directTargets) SendDirectControl("revoke", id);
                foreach (var id in directTargets) _toyLinkedTargets.Remove(id);
                _toyLinkedTarget = "";
                _forceClimax = false;
                _forceCrouch = false;
            }
            _uiY += step;
        }

        if (_toyLinkedController.Length > 0 && _peers.ContainsKey(_toyLinkedController))
        {
            string controller = _toyLinkedController;
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "你正被 " + GetPeerName(controller) + " 控制");
            _uiY += step;
            if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "反悔并解除控制"))
            {
                SendDirectControl("revoke", controller);
                _toyLinkedController = "";
                ResetToyLocal();
            }
            _uiY += step;
        }

        if (IsHosting)
        {
            if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h),
                _roomAllowGameBonuses ? "关闭技能/服装属性加成" : "允许技能/服装属性加成"))
            {
                _roomAllowGameBonuses = !_roomAllowGameBonuses;
                SendDirectControl("setting", "", "", _roomAllowGameBonuses ? 1 : 0);
            }
        }
        else if (_canLabel)
            SLabel(new Rect(_uiX, _uiY, _uiW, h), "技能/服装属性加成：" + (_roomAllowGameBonuses ? "房主已开启" : "关闭"));
        _uiY += step;
    }

    [HideFromIl2Cpp]
    private void DrawRoomPanel()
    {
        EnsureLanDiscovery();
        float h = Mathf.Max(24f, Settings.UiFontSize.Value + 10f);
        float step = h + 5f;
        float sectionW = (_uiW - 8f) / 3f;
        if (_canButton && SButton(new Rect(_uiX, _uiY, sectionW, h), _roomSection == "relay" ? "● 联机服务器房间" : "联机服务器房间")) _roomSection = "relay";
        if (_canButton && SButton(new Rect(_uiX + sectionW + 4f, _uiY, sectionW, h), _roomSection == "lan" ? "● 局域网/内网" : "局域网/内网")) _roomSection = "lan";
        if (_canButton && SButton(new Rect(_uiX + (sectionW + 4f) * 2f, _uiY, sectionW, h), "总服务器列表")) { _menuTab = "online"; return; }
        _uiY += step;
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, 100f, h), "文字大小 " + Settings.UiFontSize.Value);
        if (_canButton && SButton(new Rect(_uiX + 106f, _uiY, 42f, h), "－")) Settings.UiFontSize.Value = Mathf.Max(13, Settings.UiFontSize.Value - 1);
        if (_canButton && SButton(new Rect(_uiX + 152f, _uiY, 42f, h), "＋")) Settings.UiFontSize.Value = Mathf.Min(20, Settings.UiFontSize.Value + 1);
        _uiY += step + 2f;

        if (_roomSection == "lan")
        {
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 局域网 / 内网穿透房间 ──");
        _uiY += step;
        if (_canLabel) SLabel(new Rect(_uiX, _uiY, 42f, h), "端口");
        _portText = UiTextField("room_lan_port", new Rect(_uiX + 46f, _uiY, 64f, h), _portText, false, out _);
        if (_canLabel) SLabel(new Rect(_uiX + 116f, _uiY, 42f, h), "密码");
        _passwordText = UiTextField("room_lan_pwd", new Rect(_uiX + 160f, _uiY, 100f, h), _passwordText, true, out _);
        _uiY += step;
        if (!IsHosting && !IsClient)
        {
            if (_canButton && SButton(new Rect(_uiX, _uiY, (_uiW - 6f) * 0.5f, 26f), "创建局域网房间")) StartHosting();
        }
        else if (IsHosting)
        {
            if (_canButton && SButton(new Rect(_uiX, _uiY, (_uiW - 6f) * 0.5f, 26f), "关闭局域网房间")) StopHosting();
        }
        else
        {
            if (_canButton && SButton(new Rect(_uiX, _uiY, (_uiW - 6f) * 0.5f, 26f), "断开局域网房间")) DisconnectClient();
        }
        if (_canButton && SButton(new Rect(_uiX + (_uiW + 6f) * 0.5f, _uiY, (_uiW - 6f) * 0.5f, 26f), "刷新房间"))
        {
            LanDiscovery.Probe();
            Toast("正在主动搜索局域网/内网房间");
        }
        _uiY += 33f;
        var lanRoomsInPanel = LanDiscovery.Snapshot();
        foreach (var lan in lanRoomsInPanel)
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW - 70f, h),
                lan.Name + "  " + lan.Address + ":" + lan.Port + "  " + lan.Players + "/" + lan.MaxPlayers + (lan.HasPassword ? " 🔒" : ""));
            if (!IsHosting && !IsClient && _canButton && SButton(new Rect(_uiX + _uiW - 64f, _uiY, 60f, h), "加入"))
            {
                _addressText = lan.Address + ":" + lan.Port;
                JoinRoom();
            }
            _uiY += step;
        }
        if (lanRoomsInPanel.Count == 0)
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "未发现房间，可点“刷新房间”或在下方输入地址");
            _uiY += step;
        }
        _addressText = UiTextField("room_lan_addr", new Rect(_uiX, _uiY, _uiW - 72f, h), _addressText, false, out _);
        if (!IsHosting && !IsClient && _canButton && SButton(new Rect(_uiX + _uiW - 66f, _uiY, 62f, h), "连接")) JoinRoom();
        _uiY += step + 6f;
        DrawDirectRoomSession(h, step);
        return;
        }

        if (!_relayConnected)
        {
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h * 2f), "尚未连接联机服务器。请先到“总服务器列表”选择服务器，连接后这里会显示该服务器自己的房间列表。");
            _uiY += step * 2f;
            if (_canButton && SButton(new Rect(_uiX, _uiY, 180f, h), "打开总服务器列表")) _menuTab = "online";
            return;
        }

        if (_relayConnected)
        {
            if (_relayRoomId.Length == 0 && Time.unscaledTime - _lastRelayRoomListAt > 3f)
            {
                _lastRelayRoomListAt = Time.unscaledTime;
                RelayTcp.Send(new Dictionary<string, object> { ["t"] = "room_list" });
            }
            _uiY += step;
            string relayTitle = _relayServerName.Length > 0 ? ("── " + _relayServerName + " ──") : "── 联机服 ──";
            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), relayTitle + "  在线 " + _relayOnline + "/" + _relayMaxOnline);
            _uiY += step;
            if (_relayAnnounceTitle.Length > 0 || _relayAnnounceContent.Length > 0)
            {
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h * 2), "[公告] " + _relayAnnounceTitle + " " + _relayAnnounceContent);
                _uiY += step * 2;
            }
            var chatRect = new Rect(_uiX, _uiY, _uiW, 110f);
            GUI.Box(chatRect, "");
            int visibleChatCount = Mathf.Min(120, _relayChat.Count);
            int visibleChatStart = Mathf.Max(0, _relayChat.Count - visibleChatCount);
            float chatContentH = Mathf.Max(106f, visibleChatCount * 18f + 6f);
            float chatMaxScroll = Mathf.Max(0f, chatContentH - 106f);
            ScrollAnnounce(ref _relayChatScroll, chatRect, chatMaxScroll);
            GUI.BeginGroup(new Rect(_uiX + 4f, _uiY + 2f, _uiW - 8f, 106f));
            float ly = -_relayChatScroll.y;
            for (int i = visibleChatStart; i < _relayChat.Count; i++)
            {
                if (_canLabel) SLabel(new Rect(0f, ly, _uiW - 8f, 17f), _relayChat[i]);
                ly += 18f;
            }
            GUI.EndGroup();
            _uiY += 116f;
            _relayChatInput = UiTextField("relay_chat", new Rect(_uiX, _uiY, _uiW - 64f, h), _relayChatInput, false, out bool relayChatSubmit);
            if (relayChatSubmit || (_canButton && SButton(new Rect(_uiX + _uiW - 60f, _uiY, 56f, h), "发送")))
            {
                if (PrepareOutgoingChat(_relayChatInput, out string m))
                {
                    if (_relayRoomId.Length > 0)
                    {
                        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "chat", ["room_id"] = _relayRoomId, ["d"] = m });
                        _lastRelayChatLine = (_authUsername.Length > 0 ? _authUsername : "我") + ": " + m;
                        AddRelayLine(_lastRelayChatLine);
                    }
                    else
                    {
                        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "pub_chat", ["text"] = m });
                    }
                    _relayChatInput = "";
                }
            }
            _uiY += step;

            if (_relayRoomId.Length > 0)
            {
                bool isHost = _relayHostUid == _authUid.ToString();
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "房间 " + _relayRoomId + (isHost ? "（房主）" : ""));
                _uiY += step;
                float roomCol = (_uiW - 6f) * 0.5f;
                if (_canButton && SButton(new Rect(_uiX, _uiY, roomCol, h), _showMap ? "收起大地图" : "打开大地图"))
                {
                    _showMap = !_showMap;
                    if (_showMap) { _mapHasStart = false; _viewStage = CurrentStageInt(); }
                }
                if (_canButton && SButton(new Rect(_uiX + roomCol + 6f, _uiY, roomCol, h), "离开房间"))
                {
                    RelayTcp.Send(new Dictionary<string, object> { ["t"] = "room_leave" });
                    _relayRoomId = "";
                    _relayPlayers = new List<Dictionary<string, object>>();
                    _showMap = false;
                    RelayTcp.Send(new Dictionary<string, object> { ["t"] = "room_list" });
                }
                _uiY += step;
                if (_showMap)
                {
                    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "切换其他玩家所在地图"))
                        _viewStage = NextViewStage();
                    _uiY += step;
                    float mapSize = Mathf.Min(_uiW, 430f);
                    DrawMap(_uiX, _uiY, mapSize);
                    _uiY += mapSize + step;
                }
                foreach (var pl in _relayPlayers)
                {
                    string pu = JsonHelper.Str(pl, "uid");
                    string pn = JsonHelper.Str(pl, "name");
                    bool isSelfPlayer = pu == _authUid.ToString();
                    string distanceText = "";
                    if (!isSelfPlayer && InGame && _relayPositions.TryGetValue(pu, out var playerPos))
                    {
                        float distance = Vector3.Distance(PlayerFacade.Instance.pca.AvatorTransform.position, new Vector3(playerPos.X, playerPos.Y, playerPos.Z));
                        distanceText = "  ·  " + distance.ToString("0.0") + "m";
                    }
                    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), pn + (isSelfPlayer ? "（自己）" : "") + distanceText);
                    _uiY += step;
                    if (!isSelfPlayer && !_toyLinkedTargets.Contains(pu))
                    {
                        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "向 " + pn + " 请求控制"))
                            RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_invite", ["to"] = pu });
                        _uiY += step;
                    }
                    if (isHost && !isSelfPlayer)
                    {
                        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "将 " + pn + " 踢出房间"))
                            RelayTcp.Send(new Dictionary<string, object> { ["t"] = "room_kick", ["uid"] = pu });
                        _uiY += step;
                    }
                }

                if (_toyInviteFrom.Length > 0)
                {
                    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "收到 " + _toyInviteFromName + " 的控制请求");
                    _uiY += step;
                    if (_canButton && SButton(new Rect(_uiX, _uiY, 70f, h), "同意"))
                    { RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_accept", ["from"] = _toyInviteFrom }); _toyInviteFrom = ""; }
                    if (_canButton && SButton(new Rect(_uiX + 78f, _uiY, 70f, h), "拒绝"))
                    { RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_reject", ["from"] = _toyInviteFrom }); _toyInviteFrom = ""; }
                    _uiY += step;
                }

                // ---- 掉落道具拾取权限（v1.0.10） ----
                if (_relayPlayers.Count > 0)
                {
                    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h),
                        "掉落道具拾取：" + (_dropAllowOthers ? "允许他人拾取" : "禁止他人拾取")))
                    {
                        _dropAllowOthers = !_dropAllowOthers;
                        _dropAllowUids.Clear();
                        RelayTcp.Send(new Dictionary<string, object>
                        {
                            ["t"] = "ext_item_perm", ["allow"] = _dropAllowOthers ? 1 : 0,
                            ["uids"] = new List<object>()
                        });
                    }
                    _uiY += step;
                    if (_dropAllowOthers)
                    {
                        if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, 16f), "指定允许拾取的玩家（勾选=允许）：");
                        _uiY += 18f;
                        foreach (var pl in _relayPlayers)
                        {
                            string pu = JsonHelper.Str(pl, "uid");
                            string pn = JsonHelper.Str(pl, "name");
                            if (pu.Length == 0 || pu == _authUid.ToString()) continue;
                            bool checkedVal = _dropAllowUids.Contains(pu);
                            bool newVal = GUI.Toggle(new Rect(_uiX, _uiY, 24f, 20f), checkedVal, "");
                            if (newVal != checkedVal)
                            {
                                if (newVal) _dropAllowUids.Add(pu); else _dropAllowUids.Remove(pu);
                                RelayTcp.Send(new Dictionary<string, object>
                                {
                                    ["t"] = "ext_item_perm", ["allow"] = 1,
                                    ["uids"] = new List<object>(_dropAllowUids)
                                });
                            }
                            if (_canLabel) SLabel(new Rect(_uiX + 28f, _uiY, _uiW - 28f, 20f), pn);
                            _uiY += 22f;
                        }
                        if (_dropAllowUids.Count == 0)
                            if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, 16f), "（未指定=所有人都可拾取）");
                        _uiY += step;
                    }
                }
                if (_toyLinkedTargets.Count > 0)
                {
                    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 已授权控制 " + _toyLinkedTargets.Count + "/5 人 ──");
                    _uiY += step;
                    foreach (var tu in _toyLinkedTargets)
                    {
                        string tn = GetGamePlayerName(tu);
                        if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), (tu == _toyLinkedTarget ? "▶ 当前：" : "选择：") + tn)) _toyLinkedTarget = tu;
                        _uiY += step;
                    }
                    if (string.IsNullOrEmpty(_toyLinkedTarget)) _toyLinkedTarget = _toyLinkedTargets[0];
                    float controlCol = (_uiW - 6f) * 0.5f;
                    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), _toyAdvancedExpanded ? "高级控制：收缩" : "高级控制：展开"))
                    {
                        _toyAdvancedExpanded = !_toyAdvancedExpanded;
                    }
                    _uiY += step;
                    if (_toyAdvancedExpanded)
                    {
                        DrawToySections(controlCol, h, step, true);
                        DrawToyActionSection(controlCol, h, step, true);
                    }
                    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "解除控制关系"))
                    {
                        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_revoke" });
                        _toyLinkedTarget = ""; _forceClimax = false; _forceCrouch = false; _forceFollow = false;
                    }
                    _uiY += step;
                }

                if (_toyLinkedController.Length > 0)
                {
                    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "你正被 " + GetGamePlayerName(_toyLinkedController) + " 控制");
                    _uiY += step;
                    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), _toyNeverBreak ? "自动距离断开：关闭" : "自动距离断开：开启")) _toyNeverBreak = !_toyNeverBreak;
                    _uiY += step;
                    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "反悔并解除控制"))
                    {
                        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "toy_revoke" });
                        _toyLinkedController = "";
                        ResetToyLocal();
                    }
                    _uiY += step;
                }

                // ---- 房间设置（仅房主可修改）----
                if (_relayHostUid == _authUid.ToString())
                {
                    if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), _roomAllowGameBonuses ? "关闭技能与服装属性加成" : "允许技能与服装属性加成"))
                        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "room_setting", ["allow_game_bonuses"] = _roomAllowGameBonuses ? 0 : 1 });
                }
                else if (_canLabel)
                {
                    SLabel(new Rect(_uiX, _uiY, _uiW, h), "技能与服装属性：" + (_roomAllowGameBonuses ? "房主已开启" : "房主已关闭"));
                }
                _uiY += step;


            }
            else
            {
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 创建房间 ──");
                _uiY += step;
                if (_relayCaptchaTex == null)
                {
                    if (_canButton && SButton(new Rect(_uiX, _uiY, 90f, h), "获取验证码"))
                        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "captcha" });
                    _uiY += step;
                }
                else
                {
                    GUI.DrawTexture(new Rect(_uiX, _uiY, 90f, 28f), _relayCaptchaTex);
                    _relayCaptchaInput = UiTextField("relay_cap", new Rect(_uiX + 96f, _uiY, 58f, h), _relayCaptchaInput, false, out _);
                    if (_canButton && SButton(new Rect(_uiX + 158f, _uiY, 58f, h), "换一张"))
                        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "captcha" });
                    _uiY += step;
                }
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, 80f, h), "密码(可空)");
                _relayRoomPassword = UiTextField("relay_pwd", new Rect(_uiX + 84f, _uiY, 120f, h), _relayRoomPassword, true, out _);
                if (_canButton && SButton(new Rect(_uiX + 206f, _uiY, 64f, h), "创建房间"))
                    RelayTcp.Send(new Dictionary<string,object>{{"t","room_create"},{"captcha",_relayCaptchaInput.Trim()},{"password",_relayRoomPassword},{"max_players",10}});
                _uiY += step;

                if (_canButton && SButton(new Rect(_uiX, _uiY, _uiW, h), "刷新房间列表"))
                {
                    _lastRelayRoomListAt = Time.unscaledTime;
                    RelayTcp.Send(new Dictionary<string, object> { ["t"] = "room_list" });
                }
                _uiY += step;
                if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "── 房间列表 ──");
                _uiY += step;
                foreach (var rm in _relayRooms)
                {
                    string rid = JsonHelper.Str(rm, "room_id");
                    int np = JsonHelper.Int(rm, "players");
                    int mx = JsonHelper.Int(rm, "max");
                    int hp = JsonHelper.Int(rm, "has_password");
                    if (_canLabel) SLabel(new Rect(_uiX, _uiY, _uiW - 70f, h), rid + "  " + np + "/" + mx + (hp == 1 ? " 🔒" : ""));
                    if (_canButton && SButton(new Rect(_uiX + _uiW - 64f, _uiY, 60f, h), "加入"))
                    {
                        if (hp == 1) { _joinPwdRoomId = rid; _joinPwdInput = ""; }
                        else RelayTcp.Send(new Dictionary<string, object> { ["t"] = "room_join", ["room_id"] = rid, ["password"] = "" });
                    }
                    _uiY += step;
                }
                if (_relayRooms.Count == 0 && _canLabel) SLabel(new Rect(_uiX, _uiY, _uiW, h), "暂无房间");
                _uiY += step;

                if (_joinPwdRoomId.Length > 0)
                {
                    if (_canLabel) SLabel(new Rect(_uiX, _uiY, 90f, h), "房间密码");
                    _joinPwdInput = UiTextField("join_pwd", new Rect(_uiX + 94f, _uiY, 120f, h), _joinPwdInput, true, out _);
                    if (_canButton && SButton(new Rect(_uiX + 220f, _uiY, 60f, h), "确认加入"))
                    {
                        RelayTcp.Send(new Dictionary<string, object> { ["t"] = "room_join", ["room_id"] = _joinPwdRoomId, ["password"] = _joinPwdInput });
                        _joinPwdRoomId = "";
                    }
                    if (_canButton && SButton(new Rect(_uiX + 286f, _uiY, 50f, h), "取消")) _joinPwdRoomId = "";
                    _uiY += step;
                }

                _relayRoomInput = UiTextField("relay_room_in", new Rect(_uiX, _uiY, 120f, h), _relayRoomInput, false, out _);
                if (_canButton && SButton(new Rect(_uiX + 126f, _uiY, 64f, h), "加入房间"))
                    RelayTcp.Send(new Dictionary<string, object> { ["t"] = "room_join", ["room_id"] = _relayRoomInput.Trim(), ["password"] = _relayRoomPassword });
                _uiY += step;
            }
        }

    }

    // ========== Toasts ==========
[HideFromIl2Cpp]
private void DrawToasts()
{
    if (_toasts.Count == 0) return;
    if (_toastStyle == null)
    {
        _toastStyle = new GUIStyle();
        _toastStyle.alignment = TextAnchor.MiddleCenter;
        _toastStyle.fontSize = 15;
        _toastStyle.normal.textColor = Color.white;
    }
    float y = Screen.height * 0.12f;
    for (int i = 0; i < _toasts.Count; i++)
    {
        var t = _toasts[i];
        if (Time.unscaledTime > t.expire) continue;
        GUI.Label(new Rect(Screen.width / 2f - 220f, y, 440f, 24f), t.text, _toastStyle);
        y += 26f;
    }
}

[HideFromIl2Cpp]
private void Toast(string text)
{
    _toasts.Add((text, Time.unscaledTime + 5f));
    if (_toasts.Count > 5) _toasts.RemoveAt(0);
}

// ========== Ghost标签 ==========
[HideFromIl2Cpp]
private void DrawGhostTags()
{
    try
    {
        if (_ghosts.Count == 0 && _relayGhosts.Count == 0) return;
        if (_tagStyle == null)
        {
            _tagStyle = new GUIStyle();
            _tagStyle.alignment = TextAnchor.MiddleCenter;
            _tagStyle.fontSize = 14;
            _tagStyle.normal.textColor = Color.yellow;
        }
        Camera cam = null;
        try { cam = Camera.main; } catch { }
        if (cam == null) cam = UnityEngine.Object.FindObjectOfType<Camera>();
        if (cam == null) return;

        foreach (var kv in _ghosts)
        {
            var g = kv.Value;
            if (g == null || g.Root == null || (g.RendererCount == 0 && !g.HasMarker)) continue;
            if (!_peers.TryGetValue(kv.Key, out var p) || string.IsNullOrEmpty(p.Name)) continue;
            Vector3 wp = g.Root.transform.position + Vector3.up * 2.2f;
            Vector3 sp = cam.WorldToScreenPoint(wp);
            if (sp.z <= 0f) continue;
            GUI.Label(new Rect(sp.x - 70f, Screen.height - sp.y - 14f, 140f, 22f), p.Name, _tagStyle);
        }

        foreach (var kv in _relayGhosts)
        {
            var g = kv.Value;
            if (g == null || g.Root == null || (g.RendererCount == 0 && !g.HasMarker)) continue;
            string playerName = GetGamePlayerName(kv.Key);
            if (string.IsNullOrEmpty(playerName)) continue;
            Vector3 wp = g.Root.transform.position + Vector3.up * 2.2f;
            Vector3 sp = cam.WorldToScreenPoint(wp);
            if (sp.z <= 0f || sp.x < -100f || sp.x > Screen.width + 100f || sp.y < -40f || sp.y > Screen.height + 80f) continue;
            GUI.Label(new Rect(sp.x - 90f, Screen.height - sp.y - 14f, 180f, 22f), playerName, _tagStyle);
        }

        if (_ghostDebug)
        {
            float dy = 60f;
            foreach (var kv in _ghosts)
            {
                var g = kv.Value;
                if (g == null || g.Root == null) continue;
                float dist = -1f;
                try
                {
                    var lp = PlayerFacade.Instance != null && PlayerFacade.Instance.pca != null
                        ? PlayerFacade.Instance.pca.AvatorTransform.position
                        : Vector3.zero;
                    dist = Vector3.Distance(lp, g.Root.transform.position);
                }
                catch { }
                string info = $"{GetPeerName(kv.Key)}: {g.Root.transform.position} 距离={dist:F1} " +
                              $"有效网格={g.CountValidRenderers()} 激活={g.CountActiveRenderers()} " +
                              $"可见={g.CountVisibleRenderers()} 标记={g.HasMarker} " +
                              g.FreshRendererSummary() + " " + g.AnimationReadback();
                GUI.Box(new Rect(8f, dy, 620f, 24f), "");
                GUI.Label(new Rect(12f, dy + 2f, 610f, 20f), info);
                dy += 28f;
                string detail = g.RendererDebugSummary(5);
                if (detail.Length > 0)
                {
                    string[] lines = detail.TrimEnd().Split('\n');
                    float h = lines.Length * 18f + 6f;
                    GUI.Box(new Rect(8f, dy, 620f, h), "");
                    float ly = dy + 4f;
                    foreach (var line in lines)
                    {
                        GUI.Label(new Rect(12f, ly, 610f, 18f), line);
                        ly += 18f;
                    }
                    dy += h + 4f;
                }
            }
        }
    }
    catch { }
}

// ========== UI辅助 ==========
[HideFromIl2Cpp]
private void EnsureLanguage()
{
    if (_languageInitialized) return;
    if (Settings.Language.Value == "English") Lang.SetLanguage(Language.English);
    else Lang.SetLanguage(Language.Chinese);
    _languageButtonText = Lang.Get("lang_chinese") + "/" + Lang.Get("lang_english");
    _languageInitialized = true;
}

[HideFromIl2Cpp]
private void EnsureFont()
{
    if (_font == null && !_showMenu && !_showChatMenu) return;
    if (_font == null)
    {
        try
        {
            var names = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray(new[]
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "DengXian",
                "Noto Sans CJK SC", "Arial Unicode MS", "Arial"
            });
            _font = Font.CreateDynamicFontFromOSFont(names, 18);
        }
        catch { }
        if (_font == null) { try { _font = Font.CreateDynamicFontFromOSFont("Arial", 18); } catch { } }
        if (_font == null) _font = Font.GetDefault();
    }
    if (GUI.skin != null)
    {
        int fs = Mathf.Clamp(Settings.UiFontSize.Value, 13, 20);
        GUI.skin.font = _font;
        GUI.skin.label.richText = true;
        GUI.skin.button.richText = true;
        GUI.skin.label.fontSize = fs;
        GUI.skin.button.fontSize = fs;
        GUI.skin.textField.fontSize = fs + 1;
        GUI.skin.label.clipping = TextClipping.Overflow;
        GUI.skin.label.normal.textColor = Color.white;
        GUI.skin.label.hover.textColor = Color.white;
        GUI.skin.label.active.textColor = Color.white;
        GUI.skin.textField.normal.textColor = Color.black;
        GUI.skin.textField.focused.textColor = Color.black;
    }
}

        private static Texture2D _whiteTex = null;
        private static Texture2D WhiteTex()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }
            return _whiteTex;
        }

[HideFromIl2Cpp]
private void ProbeUiOnce()
{
    if (_uiProbed) return;
    _uiProbed = true;
    _canLabel = TryGui(() => GUI.Label(new Rect(-9999f, -9999f, 8f, 8f), "t"));
    // 不再探测 GUI.TextField/TextEditor：IL2CPP 裁剪版本中该探测本身会很慢且会抛异常。
    _canTextField = false;
    _canPassword = false;
    _canButton = TryGui(() => GUI.Button(new Rect(-9999f, -9999f, 8f, 8f), "t"));
    _canToggle = TryGui(() => GUI.Toggle(new Rect(-9999f, -9999f, 8f, 8f), false, "t"));
    _canControlName = TryGui(() => { GUI.SetNextControlName("p"); GUI.GetNameOfFocusedControl(); });
    _canClipboard = TryGui(() => { var v = GUIUtility.systemCopyBuffer; });
    _canEventChar = TryGui(() => { var c = Event.current.character; });
    _canEventKey = TryGui(() => { var k = Event.current.keyCode; });
    _canEventMouse = TryGui(() => { var m = Event.current.mousePosition; });
    _canInput = TryGui(() => { var s = Input.inputString; var b = Input.GetKeyDown(KeyCode.Backspace); });
}

private static bool TryGui(Action act)
{
    try { act(); return true; }
    catch { return false; }
}

[StructLayout(LayoutKind.Sequential)]
private struct NativePoint
{
    public int X;
    public int Y;
}

[DllImport("user32.dll")]
private static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
private static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style,
    int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
[DllImport("user32.dll", SetLastError = true)]
private static extern bool DestroyWindow(IntPtr hwnd);
[DllImport("user32.dll", SetLastError = true)]
private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int width, int height, bool repaint);
[DllImport("user32.dll")]
private static extern IntPtr SetFocus(IntPtr hwnd);
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
private static extern bool SetWindowTextW(IntPtr hwnd, string text);
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
private static extern int GetWindowTextLengthW(IntPtr hwnd);
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
private static extern int GetWindowTextW(IntPtr hwnd, StringBuilder text, int maxCount);
[DllImport("user32.dll")]
private static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
private static extern IntPtr SendMessageW(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
private delegate IntPtr NativeWndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
private static extern IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr newValue);
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
private static extern IntPtr CallWindowProcW(IntPtr previous, IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
[DllImport("user32.dll")]
private static extern short GetAsyncKeyState(int virtualKey);
[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
private static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation, int weight,
    uint italic, uint underline, uint strikeOut, uint charSet, uint outputPrecision, uint clipPrecision,
    uint quality, uint pitchAndFamily, string faceName);
[DllImport("gdi32.dll")]
private static extern bool DeleteObject(IntPtr obj);
[DllImport("imm32.dll")]
private static extern IntPtr ImmGetContext(IntPtr hWnd);
[DllImport("imm32.dll")]
private static extern bool ImmSetOpenStatus(IntPtr hIMC, bool open);
[DllImport("imm32.dll")]
private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);
[DllImport("imm32.dll", CharSet = CharSet.Unicode)]
private static extern int ImmGetCompositionStringW(IntPtr hIMC, uint index, IntPtr buffer, uint bufferLength);

[HideFromIl2Cpp]
private IntPtr NativeEditWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
{
    const uint WM_KEYDOWN = 0x0100;
    const uint WM_CHAR = 0x0102;
    const uint WM_KILLFOCUS = 0x0008;
    int key = unchecked((int)wParam.ToInt64());
    if (message == WM_KEYDOWN)
    {
        if (key == 0x0D) { _nativeSubmitPending = true; return IntPtr.Zero; }
        if (key == 0x1B) { _nativeCancelPending = true; return IntPtr.Zero; }
    }
    if (message == WM_CHAR && (key == 0x0D || key == 0x1B)) return IntPtr.Zero;
    if (message == WM_KILLFOCUS) _nativeFocusLostPending = true;
    return _nativeOldWndProc != IntPtr.Zero
        ? CallWindowProcW(_nativeOldWndProc, hwnd, message, wParam, lParam)
        : IntPtr.Zero;
}

[HideFromIl2Cpp]
private void TryOpenSystemIme()
{
    if (Time.unscaledTime - _lastImeOpenTime < 0.15f) return;
    _lastImeOpenTime = Time.unscaledTime;
    try
    {
        IntPtr hwnd = _nativeEdit != IntPtr.Zero ? _nativeEdit : GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;
        IntPtr imc = ImmGetContext(hwnd);
        if (imc == IntPtr.Zero) return;
        ImmSetOpenStatus(imc, true);
        ImmReleaseContext(hwnd, imc);
    }
    catch { }
}

[HideFromIl2Cpp]
private void MoveNativeEditor(Rect rect)
{
    if (_nativeEdit == IntPtr.Zero || _nativeEditParent == IntPtr.Zero) return;
    int x = Mathf.RoundToInt(rect.x);
    int y = Mathf.RoundToInt(rect.y);
    try
    {
        Vector2 screenPoint = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
        // 原生 EDIT 使用独立置顶弹出层，坐标必须是桌面屏幕坐标。
        x = Mathf.RoundToInt(screenPoint.x);
        y = Mathf.RoundToInt(screenPoint.y);
    }
    catch { }
    int width = Math.Max(80, Mathf.RoundToInt(rect.width));
    int height = Math.Max(28, Mathf.RoundToInt(rect.height));
    MoveWindow(_nativeEdit, x, y, width, height, true);
}

[HideFromIl2Cpp]
private bool OpenNativeEditor(string id, Rect rect, string value, bool masked)
{
    try
    {
        if (_nativeEdit != IntPtr.Zero && _nativeEditField == id && _nativeEditMasked == masked)
        {
            _nativeEditSeenFrame = Time.frameCount;
            MoveNativeEditor(rect);
            SetFocus(_nativeEdit);
            TryOpenSystemIme();
            return true;
        }

        CloseNativeEditor();
        IntPtr parent = GetForegroundWindow();
        if (parent == IntPtr.Zero) return false;

        const uint WS_POPUP = 0x80000000;
        const uint WS_VISIBLE = 0x10000000;
        const uint WS_BORDER = 0x00800000;
        const uint WS_TABSTOP = 0x00010000;
        const uint ES_AUTOHSCROLL = 0x00000080;
        const uint ES_PASSWORD = 0x00000020;
        const uint WS_EX_TOPMOST = 0x00000008;
        const uint WS_EX_TOOLWINDOW = 0x00000080;
        const uint WS_EX_CLIENTEDGE = 0x00000200;
        uint style = WS_POPUP | WS_VISIBLE | WS_BORDER | WS_TABSTOP | ES_AUTOHSCROLL;
        if (masked) style |= ES_PASSWORD;

        _nativeEditParent = parent;
        _nativeEdit = CreateWindowExW(WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_CLIENTEDGE, "EDIT", value ?? "", style,
            0, 0, Math.Max(80, Mathf.RoundToInt(rect.width)), Math.Max(28, Mathf.RoundToInt(rect.height)),
            parent, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (_nativeEdit == IntPtr.Zero)
        {
            _nativeEditParent = IntPtr.Zero;
            return false;
        }

        _nativeEditField = id;
        _nativeEditOriginal = value ?? "";
        _nativeEditMasked = masked;
        _nativeSubmitPending = false;
        _nativeCancelPending = false;
        _nativeEnterDown = false;
        _nativeEscapeDown = false;
        _nativeFocusLostPending = false;
        _nativeEditSeenFrame = Time.frameCount;
        if (!_runInBackgroundOverridden)
        {
            _runInBackgroundWas = Application.runInBackground;
            Application.runInBackground = true;
            _runInBackgroundOverridden = true;
        }
        _nativeWndProc = NativeEditWindowProc;
        _nativeOldWndProc = SetWindowLongPtrW(_nativeEdit, -4, Marshal.GetFunctionPointerForDelegate(_nativeWndProc));

        if (_nativeEditFont == IntPtr.Zero)
        {
            int fontHeight = -Mathf.Clamp(Settings.UiFontSize.Value + 6, 19, 28);
            _nativeEditFont = CreateFontW(fontHeight, 0, 0, 0, 500, 0, 0, 0, 1, 0, 0, 5, 0, "Microsoft YaHei UI");
        }
        if (_nativeEditFont != IntPtr.Zero)
            SendMessageW(_nativeEdit, 0x0030, _nativeEditFont, new IntPtr(1));

        SetWindowTextW(_nativeEdit, value ?? "");
        MoveNativeEditor(rect);
        SetFocus(_nativeEdit);
        int selEnd = (value ?? "").Length;
        SendMessageW(_nativeEdit, 0x00B1, new IntPtr(selEnd), new IntPtr(selEnd));
        TryOpenSystemIme();
        return true;
    }
    catch
    {
        CloseNativeEditor();
        return false;
    }
}

[HideFromIl2Cpp]
private string ReadNativeEditorText()
{
    if (_nativeEdit == IntPtr.Zero) return "";
    try
    {
        int length = Math.Max(0, GetWindowTextLengthW(_nativeEdit));
        var buffer = new StringBuilder(length + 2);
        GetWindowTextW(_nativeEdit, buffer, buffer.Capacity);
        return buffer.ToString();
    }
    catch { return _nativeEditOriginal ?? ""; }
}

[HideFromIl2Cpp]
private string ReadNativeComposition()
{
    if (_nativeEdit == IntPtr.Zero) return "";
    IntPtr imc = IntPtr.Zero;
    IntPtr buffer = IntPtr.Zero;
    try
    {
        imc = ImmGetContext(_nativeEdit);
        if (imc == IntPtr.Zero) return "";
        const uint GCS_COMPSTR = 0x0008;
        int byteCount = ImmGetCompositionStringW(imc, GCS_COMPSTR, IntPtr.Zero, 0);
        if (byteCount <= 0) return "";
        buffer = Marshal.AllocHGlobal(byteCount + 2);
        Marshal.WriteInt16(buffer, byteCount, 0);
        int copied = ImmGetCompositionStringW(imc, GCS_COMPSTR, buffer, (uint)byteCount);
        return copied > 0 ? (Marshal.PtrToStringUni(buffer, copied / 2) ?? "") : "";
    }
    catch { return ""; }
    finally
    {
        if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
        if (imc != IntPtr.Zero) ImmReleaseContext(_nativeEdit, imc);
    }
}

[HideFromIl2Cpp]
private void UpdateNativeEditor()
{
    if (_nativeEdit == IntPtr.Zero) return;
    try
    {
        bool enterDown = (GetAsyncKeyState(0x0D) & 0x8000) != 0;
        bool escapeDown = (GetAsyncKeyState(0x1B) & 0x8000) != 0;
        if (enterDown && !_nativeEnterDown) _nativeSubmitPending = true;
        if (escapeDown && !_nativeEscapeDown) _nativeCancelPending = true;
        _nativeEnterDown = enterDown;
        _nativeEscapeDown = escapeDown;

        if (string.IsNullOrEmpty(_focusedField) || Time.frameCount - _nativeEditSeenFrame > 3)
            CloseNativeEditor();
    }
    catch { CloseNativeEditor(); }
}

[HideFromIl2Cpp]
private void CloseNativeEditor()
{
    try
    {
        if (_nativeEdit != IntPtr.Zero)
        {
            if (_nativeOldWndProc != IntPtr.Zero) SetWindowLongPtrW(_nativeEdit, -4, _nativeOldWndProc);
            DestroyWindow(_nativeEdit);
        }
    }
    catch { }
    if (_runInBackgroundOverridden)
    {
        try { Application.runInBackground = _runInBackgroundWas; } catch { }
        _runInBackgroundOverridden = false;
    }
    _nativeEdit = IntPtr.Zero;
    _nativeEditParent = IntPtr.Zero;
    _nativeEditField = "";
    _nativeEditOriginal = "";
    _nativeEditMasked = false;
    _nativeSubmitPending = false;
    _nativeCancelPending = false;
    _nativeEnterDown = false;
    _nativeEscapeDown = false;
    _nativeFocusLostPending = false;
    _nativeOldWndProc = IntPtr.Zero;
    _nativeWndProc = null;
    _nativeEditSeenFrame = -1;
}

[HideFromIl2Cpp]
private void DrawHighContrastInput(Rect rect, string value, bool masked, bool focused, string nativeComposition = null)
{
    Color old = GUI.color;
    GUI.color = new Color(0.025f, 0.035f, 0.06f, 0.98f);
    GUI.DrawTexture(rect, WhiteTex());
    GUI.color = focused ? new Color(0.15f, 0.78f, 1f, 1f) : new Color(0.88f, 0.92f, 1f, 1f);
    GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), WhiteTex());
    GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), WhiteTex());
    GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), WhiteTex());
    GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), WhiteTex());
    GUI.color = Color.white;

    if (_inputTextStyle == null)
    {
        _inputTextStyle = new GUIStyle();
        _inputTextStyle.alignment = TextAnchor.MiddleLeft;
        _inputTextStyle.clipping = TextClipping.Clip;
        _inputTextStyle.richText = false;
    }
    _inputTextStyle.font = _font;
    _inputTextStyle.fontSize = Mathf.Clamp(Settings.UiFontSize.Value + 1, 14, 22);
    _inputTextStyle.normal.textColor = Color.white;
    _inputTextStyle.hover.textColor = Color.white;
    _inputTextStyle.active.textColor = Color.white;
    _inputTextStyle.focused.textColor = Color.white;

    string compose = nativeComposition;
    if (compose == null)
        compose = (focused && _canInput) ? (Input.compositionString ?? "") : "";
    string raw = (value ?? "") + (string.IsNullOrEmpty(compose) ? "" : "【" + compose + "】");
    string shown = masked ? new string('●', (value == null ? 0 : value.Length) + (compose == null ? 0 : compose.Length)) : raw;
    if (focused && ((int)(Time.realtimeSinceStartup * 2f) & 1) == 0) shown += "|";
    GUI.Label(new Rect(rect.x + 7f, rect.y + 1f, rect.width - 14f, rect.height - 2f), shown, _inputTextStyle);
    GUI.color = old;
}

[HideFromIl2Cpp]
private string UiTextField(string id, Rect rect, string value, bool masked, out bool submit)
{
    submit = false;
    string next = value ?? "";
    var evt = Event.current;
    GUI.SetNextControlName(id);

    bool clicked = false;
    if (_canEventMouse)
        clicked = evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition);
    else
        clicked = Input.GetMouseButtonDown(0) && rect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));

    if (clicked)
    {
        _focusedField = id;
        GUI.FocusControl(id);
        OpenNativeEditor(id, rect, next, masked);
        if (_canEventMouse) evt.Use();
    }

    bool guiFocused = false;
    if (_canControlName)
    {
        try { guiFocused = GUI.GetNameOfFocusedControl() == id; } catch { _canControlName = false; }
    }
    bool focused = _focusedField == id || guiFocused;

    if (focused && _nativeEdit != IntPtr.Zero && _nativeEditField == id)
    {
        _nativeEditSeenFrame = Time.frameCount;
        next = ReadNativeEditorText();
        MoveNativeEditor(rect);
        string nativeComposition = ReadNativeComposition();
        // 原生 EDIT 可能被 Unity 的全屏交换链盖住；Unity 层始终保留一份可见文字镜像。
        DrawHighContrastInput(rect, next, masked, true, nativeComposition);

        if (_nativeCancelPending)
        {
            next = _nativeEditOriginal ?? "";
            _focusedField = "";
            GUI.FocusControl("");
            CloseNativeEditor();
        }
        else if (_nativeSubmitPending)
        {
            submit = true;
            _focusedField = "";
            GUI.FocusControl("");
            CloseNativeEditor();
        }
        else if (_nativeFocusLostPending)
        {
            // Clicking a Unity Send button first commits and closes the native editor.
            if (IsChatInputField(id))
                submit = true;
            CloseNativeEditor();
            // 保持聚焦，避免报错/点按钮后输入框彻底失焦、按键全部失效。
            _focusedField = id;
            GUI.FocusControl(id);
        }
        if (IsChatInputField(id) && next.Length > 2000) next = next.Substring(0, 2000);
        return next;
    }

    DrawHighContrastInput(rect, next, masked, focused);

    if (focused && _canInput)
    {
        TryOpenSystemIme();
        try
        {
            Input.imeCompositionMode = UnityEngine.IMECompositionMode.On;
            Input.compositionCursorPos = GUIUtility.GUIToScreenPoint(new Vector2(rect.x + 7f, rect.yMax));
        }
        catch { }

        if (_manualInputFrame != Time.frameCount)
        {
            _manualInputFrame = Time.frameCount;
            bool composingNow = !string.IsNullOrEmpty(Input.compositionString);
            if (!composingNow && (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete)) && next.Length > 0)
                next = next.Substring(0, next.Length - 1);

            bool pasted = false;
            if (!composingNow && PastePressed(true) && _canClipboard)
            {
                try
                {
                    next += GUIUtility.systemCopyBuffer ?? "";
                    pasted = true;
                }
                catch { _canClipboard = false; }
            }

            string inputText = Input.inputString ?? "";
            if (string.IsNullOrEmpty(inputText) && _canEventChar && evt.type == EventType.KeyDown && evt.character >= ' ')
                inputText = evt.character.ToString();
            if (!pasted && !string.IsNullOrEmpty(inputText))
            {
                foreach (char ch in inputText)
                {
                    if (ch == '\b' || ch == '\n' || ch == '\r') continue;
                    if (ch >= ' ') next += ch;
                }
            }
        }
    }

    if (focused)
    {
        bool composing = _canInput && !string.IsNullOrEmpty(Input.compositionString);
        bool enter = evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);
        if (enter && !composing)
        {
            submit = true;
            GUI.FocusControl("");
            _focusedField = "";
            evt.Use();
        }
        else if (!composing && evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
        {
            GUI.FocusControl("");
            _focusedField = "";
            evt.Use();
        }
    }
    if (IsChatInputField(id) && next.Length > 2000) next = next.Substring(0, 2000);
    return next;
}

[HideFromIl2Cpp]
private bool PastePressed(bool frameFresh)
{
    if (_canEventKey && _canEventMouse)
    {
        var evt = Event.current;
        if (evt.type == EventType.KeyDown && evt.control && evt.keyCode == KeyCode.V)
        {
            evt.Use();
            return true;
        }
        return false;
    }
    return frameFresh &&
           (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
           Input.GetKeyDown(KeyCode.V);
}

// ========== 工具方法 ==========
private static Transform FindNamed(Transform root, string name)
        {
            if (root == null) return null;
            var stack = new Stack<Transform>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == null) continue;
                if (cur.name == name) return cur;
                for (int i = cur.childCount - 1; i >= 0; i--)
                    stack.Push(cur.GetChild(i));
            }
            return null;
        }

        private static (string, int) ParseAddress(string text)
{
    string host = "127.0.0.1";
    int port = Settings.Port.Value;
    if (!string.IsNullOrWhiteSpace(text))
    {
        var parts = text.Trim().Split(':');
        if (parts.Length >= 1 && parts[0].Length > 0) host = parts[0];
        if (parts.Length >= 2) port = ParseInt(parts[1], port);
    }
    return (host, port);
}

private static int ParseInt(string text, int fallback)
{
    return int.TryParse(text, out var v) ? v : fallback;
}

private static string GetLanIp()
{
    try
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var b = ua.Address.GetAddressBytes();
                if (b[0] == 192 && b[1] == 168) return ua.Address.ToString();
                if (b[0] == 10) return ua.Address.ToString();
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return ua.Address.ToString();
            }
        }
    }
    catch { }
    return "未知";
}

[HideFromIl2Cpp]
private string GetPeerName(string id)
{
    if (_peers.TryGetValue(id, out var p) && !string.IsNullOrEmpty(p.Name)) return p.Name;
    return id;
}

[HideFromIl2Cpp]
private void AddChat(string line)
{
    _chatMessages.Add(line);
    if (_chatMessages.Count > 100) _chatMessages.RemoveAt(0);
}

private static string StageName(int stage)
{
    switch (stage)
    {
        case 0: return "公寓";
        case 1: return "东京街头";
        case 3: return "郊外";
        case 4: return "街道";
        case 5: return "城市";
        case 6: return "便利店";
        case 7: return "时装店";
        case 8: return "理发店";
        case 9: return "洗衣店";
        case 10: return "住宅";
        case 11: return "商场";
        case 12: return "站前";
        case 13: return "地下通道";
        case 14: return "公园";
        case 15: return "豪宅";
        default: return "未知场景";
    }
}
        // ========== 发送聊天 ==========
        [HideFromIl2Cpp]
        private void SendChat(string text)
        {
            if (!Connected || !PrepareOutgoingChat(text, out string message)) return;
            var w = new WireWriter();
            w.WriteString(PeerId);
            w.WriteString(message);
            Send(MsgTypes.Chat, w.ToArray());
            AddChat((_nickname.Length > 0 ? _nickname : "我") + ": " + message);
            _chatInput = "";
        }

        [HideFromIl2Cpp]
        private bool PrepareOutgoingChat(string input, out string text)
        {
            text = (input ?? "").Trim();
            if (text.Length == 0) return false;
            if (text.Length > 2000) text = text.Substring(0, 2000);
            float stamp = Time.unscaledTime;
            while (_chatSendTimes.Count > 0 && stamp - _chatSendTimes.Peek() >= 5f)
                _chatSendTimes.Dequeue();
            if (_chatSendTimes.Count >= 2)
            {
                Toast("发送过快，请稍后再试");
                return false;
            }
            _chatSendTimes.Enqueue(stamp);
            return true;
        }

        [HideFromIl2Cpp]
        private static bool IsChatInputField(string id)
        {
            return !string.IsNullOrEmpty(id) &&
                   (id.IndexOf("chat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    id == "pub_in" || id == "dm_input");
        }

        [HideFromIl2Cpp]
        private void SendFollowFromLocal()
        {
            if (!IsHosting || !InGame) return;
            var avatar = PlayerFacade.Instance.pca.AvatorTransform;
            var w = new WireWriter();
            w.WriteString(PeerId);
            w.WriteInt(CurrentStageInt());
            w.WriteFloat(avatar.position.x); w.WriteFloat(avatar.position.y); w.WriteFloat(avatar.position.z);
            w.WriteFloat(avatar.eulerAngles.y);
            Send(MsgTypes.Follow, w.ToArray());
            Toast(Lang.Get("toast_follow"));
        }

        // ========== 消息处理 ==========
        [HideFromIl2Cpp]
        private void HandleRemote(NetMsg msg)
        {
            if (msg == null) return;
            try
            {
                switch (msg.Type)
                {
                    case MsgTypes.Welcome: HandleWelcome(msg.Payload); break;
                    case MsgTypes.Players: HandlePlayers(msg.Payload); break;
                    case MsgTypes.State: HandleState(msg); break;
                    case MsgTypes.Event: HandleEvent(msg); break;
                    case MsgTypes.Chat: HandleChat(msg); break;
                    case MsgTypes.Pong: HandlePong(msg); break;
                    case MsgTypes.Bye: HandleBye(msg); break;
                    case MsgTypes.Error: HandleError(msg); break;
                    case MsgTypes.Follow: HandleFollow(msg); break;
                    case MsgTypes.Motion: HandleMotion(msg); break;
                    case MsgTypes.Control: HandleDirectControl(msg); break;
                }
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("消息处理异常: " + ex.Message);
            }
        }

        [HideFromIl2Cpp]
        private void HandleWelcome(byte[] payload)
        {
            try
            {
                var r = new WireReader(payload);
                string id = r.ReadString();
                string serverVersion = r.ReadString();
                _peers.Clear();
                int count = r.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    var p = new PeerInfo
                    {
                        Id = r.ReadString(),
                        Name = r.ReadString(),
                        IsHost = r.ReadBool(),
                        RttMs = r.ReadLong()
                    };
                    if (!_peers.ContainsKey(p.Id)) _peers[p.Id] = p;
                }
                _lastStateTime = 0;
                _hostPeerId = "host";
                foreach (var p in _peers.Values)
                {
                    if (p.IsHost) { _hostPeerId = p.Id; break; }
                }
                Toast(string.Format(Lang.Get("toast_join_success"), id));
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("Welcome 解析失败: " + ex.Message);
            }
        }

        [HideFromIl2Cpp]
        private void HandlePlayers(byte[] payload)
        {
            try
            {
                var r = new WireReader(payload);
                int count = r.ReadInt();
                var seen = new HashSet<string>();
                for (int i = 0; i < count; i++)
                {
                    var p = new PeerInfo
                    {
                        Id = r.ReadString(),
                        Name = r.ReadString(),
                        IsHost = r.ReadBool(),
                        RttMs = r.ReadLong()
                    };
                    seen.Add(p.Id);
                    if (_peers.TryGetValue(p.Id, out var old))
                    {
                        old.Name = p.Name;
                        old.IsHost = p.IsHost;
                        old.LastSeen = DateTime.UtcNow;
                    }
                    else
                    {
                        _peers[p.Id] = p;
                    }
                }
                var stale = new List<string>();
                foreach (var kv in _peers)
                {
                    if (kv.Key == PeerId) continue;
                    if (kv.Key == SimPeerId) continue;
                    if (!seen.Contains(kv.Key)) stale.Add(kv.Key);
                }
                foreach (var id in stale)
                {
                    _peers.Remove(id);
                    RemoveGhost(id);
                }
                _hostPeerId = "host";
                foreach (var p in _peers.Values)
                {
                    if (p.IsHost) { _hostPeerId = p.Id; break; }
                }
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("玩家列表解析失败: " + ex.Message);
            }
        }

        [HideFromIl2Cpp]
        private void HandleMotion(NetMsg msg)
        {
            try
            {
                var r = new WireReader(msg.Payload);
                string senderId = r.ReadString();
                if (string.IsNullOrEmpty(senderId) || senderId == PeerId) return;
                float vx = r.ReadFloat();
                float vz = r.ReadFloat();
                float ry = r.ReadFloat();
                bool moving = r.ReadBool();
                bool crouch = r.ReadBool();
                int action = r.ReadInt();
                int hash = r.ReadInt();
                var ghost = GetOrCreateGhost(senderId);
                if (ghost != null)
                {
                    // New LAN packets append only compact Animator parameters; old packets remain valid.
                    if (r.Remaining >= 34)
                    {
                        bool strafe = r.ReadBool();
                        bool dash = r.ReadBool();
                        float moveSpeed = r.ReadFloat();
                        float locomotionSpeed = r.ReadFloat();
                        float strafeX = r.ReadFloat();
                        float strafeY = r.ReadFloat();
                        int actionId = r.ReadInt();
                        int actionParam = r.ReadInt();
                        int oldActionId = r.ReadInt();
                        float anotherMotion = r.ReadFloat();
                        ghost.SetMotionDetailed(new Vector3(vx, 0f, vz), ry, moving, crouch, strafe, dash,
                            action, hash, moveSpeed, locomotionSpeed, strafeX, strafeY, Vector3.zero, false);
                        ghost.MarkActionDetailed(action, hash, actionId, actionParam, oldActionId, anotherMotion,
                            Array.Empty<int>(), Array.Empty<float>(), Array.Empty<float>());
                    }
                    else
                    {
                        ghost.SetMotion(new Vector3(vx, 0f, vz), ry, moving, crouch, action, hash);
                    }
                }
            }
            catch { }
        }

        [HideFromIl2Cpp]
        private void HandleState(NetMsg msg)
        {
            var st = ParseState(msg.Payload, out var senderId);
            if (st == null || string.IsNullOrEmpty(senderId)) return;
            if (senderId == PeerId) return;
            _lastStates[senderId] = st;

            try
            {
                int avId = PlayerFacade.Instance.pca.AvatorTransform.GetInstanceID();
                if (avId != _lastLocalAvatarId)
                {
                    _lastLocalAvatarId = avId;
                    _sourceCoreBoneCount = -1;
                    RemoveAllGhosts();
                }
            }
            catch { }

            int myStage = CurrentStageInt();
            if (st.Stage >= 0 && myStage >= 0 && st.Stage != myStage)
            {
                RemoveGhost(senderId);
                return;
            }

            if (_ghosts.TryGetValue(senderId, out var eg) && eg != null && eg.Root != null &&
                !eg.HasMarker && eg.CountValidRenderers() < 5 && SourceHasValidMeshes())
            {
                RemoveGhost(senderId);
            }

            if (_ghosts.TryGetValue(senderId, out var bg) && bg != null && bg.Root != null &&
                !bg.HasMarker && bg.BoneMapCount > 0 &&
                bg.BoneMapCount < SourceCoreBoneCount() * 0.8f)
            {
                PluginInfo.Warn("分身骨骼数量偏少，重建: " + senderId + " " + bg.BoneMapCount + "/" + SourceCoreBoneCount());
                RemoveGhost(senderId);
            }

            var ghost = GetOrCreateGhost(senderId);
            if (ghost != null)
            {
                ghost.ApplyHipsFull(st.HipsLocal, st.HipsLocalRot, true);
                ghost.Apply(st, true);
            }

            if (Settings.AutoFollowHost.Value && senderId == _hostPeerId && st.Stage >= 0)
            {
                bool first = !_followedHostStage.ContainsKey(senderId);
                if (first || _followedHostStage[senderId] != st.Stage)
                {
                    _followedHostStage[senderId] = st.Stage;
                    if (first)
                    {
                        if (CurrentStageInt() != st.Stage) DoFollow(senderId);
                    }
                    else
                    {
                        DoFollow(senderId);
                    }
                }
            }
        }

        [HideFromIl2Cpp]
        private void HandleEvent(NetMsg msg)
        {
            var r = new WireReader(msg.Payload);
            string senderId = r.ReadString();
            string name = r.ReadString();
            int intArg = r.ReadInt();
            string stringArg = r.ReadString();
            if (senderId == PeerId) return;
            string pname = GetPeerName(senderId);

            switch (name)
            {
                case "action":
                    Toast($"{pname} 开始动作: {ActionName(intArg)}");
                    if(_ghosts.TryGetValue(senderId,out var actionGhost)&&actionGhost!=null)actionGhost.MarkAction(intArg);
                    break;
                case "playerState":
                    Toast($"{pname} 状态变化: {(PlayerStateModel.PlayerState)intArg}");
                    break;
                case "clothes":
                    Toast($"{pname} 更换了衣服");
                    break;
                case "clothesState":
                    break;
                case "stage":
                    Toast($"{pname} 进入了 {StageName(intArg)}");
                    if (Settings.AutoFollowHost.Value && senderId == _hostPeerId)
                        DoFollow(senderId);
                    break;
                case "sex":
                    Toast(intArg == 1 ? $"{pname} 进入了性爱模式" : $"{pname} 结束了性爱模式");
                    break;
                case "gameover":
                    Toast(intArg == 1 ? $"{pname} 被发现了！" : $"{pname} 脱离了危险");
                    break;
                case "mission":
                    Toast($"{pname} 完成了任务: {stringArg}");
                    break;
            }
        }

        private static string ActionName(int actionType)
        {
            try { return ((ActionType)actionType).ToString(); }
            catch { return "未知"; }
        }

        [HideFromIl2Cpp]
        private void HandleChat(NetMsg msg)
        {
            var r = new WireReader(msg.Payload);
            string senderId = r.ReadString();
            string text = r.ReadString();
            if (senderId == PeerId) return;
            AddChat($"{GetPeerName(senderId)}: {text}");
        }

        [HideFromIl2Cpp]
        private void HandlePong(NetMsg msg)
        {
            var r = new WireReader(msg.Payload);
            string pingSender = r.ReadString();
            string responderId = r.ReadString();
            long tick = r.ReadLong();
            if (pingSender != PeerId || string.IsNullOrEmpty(responderId)) return;
            long rttMs = (Stopwatch.GetTimestamp() - tick) * 1000L / Stopwatch.Frequency;
            if (rttMs < 0) rttMs = 0;
            if (_peers.TryGetValue(responderId, out var p)) p.RttMs = rttMs;
            else _peers[responderId] = new PeerInfo { Id = responderId, Name = responderId, RttMs = rttMs };
        }

        [HideFromIl2Cpp]
        private void HandleBye(NetMsg msg)
        {
            if (msg.Payload == null || msg.Payload.Length == 0)
            {
                if (_client == null) return;
                Toast("与服务器断开连接");
                return;
            }
            string senderId;
            try
            {
                var r = new WireReader(msg.Payload);
                senderId = r.ReadString();
            }
            catch { return; }
            if (senderId == PeerId) return;
            string pname = GetPeerName(senderId);
            _peers.Remove(senderId);
            RemoveGhost(senderId);
            _lastStates.Remove(senderId);
            Toast($"{pname} 离开了房间");
        }

        [HideFromIl2Cpp]
        private void HandleError(NetMsg msg)
        {
            try
            {
                var r = new WireReader(msg.Payload);
                Toast("服务器提示: " + r.ReadString());
            }
            catch { }
        }

        [HideFromIl2Cpp]
        private void HandleFollow(NetMsg msg)
        {
            var r = new WireReader(msg.Payload);
            string senderId = r.ReadString();
            int stage = r.ReadInt();
            float x = r.ReadFloat(); float y = r.ReadFloat(); float z = r.ReadFloat();
            float rotY = r.ReadFloat();
            if (senderId == PeerId) return;
            Toast($"{GetPeerName(senderId)} 召集你前往 {StageName(stage)}");
            DoFollow(stage, new Vector3(x, y, z), rotY);
        }

        [HideFromIl2Cpp]
        private void DoFollow(string senderId)
        {
            if (!_lastStates.TryGetValue(senderId, out var st)) return;
            DoFollow(st.Stage, st.Pos, st.RotY);
        }

        [HideFromIl2Cpp]
        private void DoFollow(int stage, Vector3 pos, float rotY)
        {
            if (stage < 0) return;
            int cur = CurrentStageInt();
            if (cur < 0)
            {
                Toast(Lang.Get("toast_cant_follow"));
                return;
            }
            if (cur == stage)
            {
                TryWarp(pos, rotY);
                return;
            }
            _pendingFollowStage = stage;
            _pendingFollowPos = pos;
            _pendingFollowRot = rotY;
            _pendingFollowTime = Time.unscaledTime;
            var stc = InGameManager.Instance != null ? InGameManager.Instance.StageTransController : null;
            if (stc == null)
            {
                _pendingFollowStage = -1;
                Toast(Lang.Get("toast_cant_follow"));
                return;
            }
            try
            {
                if (!stc.IsAbleTransStage())
                {
                    _pendingFollowStage = -1;
                    Toast(Lang.Get("toast_cant_follow"));
                    return;
                }
                stc.TransStage((StageType)cur, (StageType)stage, null, 0.2f, null);
                Toast(string.Format(Lang.Get("toast_follow_warp"), StageName(stage)));
            }
            catch (Exception ex)
            {
                _pendingFollowStage = -1;
                Toast(string.Format(Lang.Get("toast_follow_warp_fail"), ex.Message));
            }
        }

        [HideFromIl2Cpp]
        private void TryWarp(Vector3 pos, float rotY)
        {
            var pc = PlayerController.Instance;
            if (pc == null) return;
            try { pc.Warp(pos, Quaternion.Euler(0f, rotY, 0f)); }
            catch (Exception ex) { PluginInfo.Warn("Warp 失败: " + ex.Message); }
        }

        // ========== 事件检测 ==========
        [HideFromIl2Cpp]
        private void CheckEvents()
        {
            if (!Connected) return;
            if (!InGame)
            {
                _eventsInitialized = false;
                return;
            }

            var ps = PlayerFacade.Instance.pca.PlayerState;
            int action = ps.CurrentAction != null ? (int)ps.CurrentAction.Type : -1;
            int pstate = (int)ps.CurrentState;
            int clothes = ps.CurrentClothes != null ? ps.CurrentClothes.TypeId : -1;
            int clothesB = (int)ps.CurrentClothesBStateCache;
            int stage = CurrentStageInt();
            int sex = SexManager.Instance != null && SexManager.Instance.IsSexMode ? 1 : 0;
            int go = InGameManager.Instance != null &&
                     InGameManager.Instance.TempInGameState != null &&
                     InGameManager.Instance.TempInGameState.IsGameOver ? 1 : 0;

            if (!_eventsInitialized)
            {
                _lastAction = action;
                _lastPlayerState = pstate;
                _lastClothesType = clothes;
                _lastClothesB = clothesB;
                _lastStage = stage;
                _lastSex = sex;
                _lastGameOver = go;
                CollectMissions(true);
                _eventsInitialized = true;
                return;
            }

            if (action != _lastAction && action >= 0)
                SendEvent("action", action, ((ActionType)action).ToString());
            if (pstate != _lastPlayerState)
                SendEvent("playerState", pstate, "");
            if (clothes != _lastClothesType)
                SendEvent("clothes", clothes, "");
            if (clothesB != _lastClothesB)
                SendEvent("clothesState", clothesB, "");
            if (stage != _lastStage && stage >= 0)
                SendEvent("stage", stage, StageName(stage));
            if (sex != _lastSex)
                SendEvent("sex", sex, "");
            if (go != _lastGameOver)
                SendEvent("gameover", go, "");

            _lastAction = action;
            _lastPlayerState = pstate;
            _lastClothesType = clothes;
            _lastClothesB = clothesB;
            _lastStage = stage;
            _lastSex = sex;
            _lastGameOver = go;
            CollectMissions(false);
        }

        [HideFromIl2Cpp]
        private void CollectMissions(bool onlyMark)
        {
            try
            {
                var mm = MissionManager.Instance;
                if (mm == null || mm.MissionList == null) return;
                var list = mm.MissionList;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    if (m == null) continue;
                    if (m.IsComplete && !_sentMissions.Contains(m.UniqueMissionId))
                    {
                        _sentMissions.Add(m.UniqueMissionId);
                        if (!onlyMark)
                            SendEvent("mission", m.UniqueMissionId, m.MissionName ?? "");
                    }
                }
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("任务检测异常: " + ex.Message);
            }
        }

        [HideFromIl2Cpp]
        private void SendEvent(string name, int intArg, string stringArg)
        {
            var w = new WireWriter();
            w.WriteString(PeerId);
            w.WriteString(name);
            w.WriteInt(intArg);
            w.WriteString(stringArg ?? "");
            Send(MsgTypes.Event, w.ToArray());
        }

        // ========== 状态同步 ==========
        [HideFromIl2Cpp]
        private string CurrentAppearanceSignature()
        {
            try
            {
                if (!InGame) return "";
                var av = PlayerFacade.Instance.pca.AvatorTransform;
                int id = av.GetInstanceID();
                if (id == _appearanceProbeAvatarId && Time.unscaledTime - _appearanceProbeAt < 0.5f) return _appearanceProbeSig;
                _appearanceProbeAvatarId = id;
                _appearanceProbeAt = Time.unscaledTime;
                var parts = new List<string>();
                var ps = PlayerFacade.Instance.pca.PlayerState;
                int clothes = -1;
                if (ps.CurrentClothes != null) clothes = ps.CurrentClothes.TypeId;
                parts.Add("clothes=" + clothes + ":" + (int)ps.CurrentClothesBStateCache);
                foreach (var renderer in av.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null) continue;
                    string active = "0";
                    string enabled = "0";
                    if (renderer.gameObject.activeInHierarchy) active = "1";
                    if (renderer.enabled) enabled = "1";
                    var sig = new System.Text.StringBuilder(PathOf(renderer.transform));
                    sig.Append("|").Append(active).Append("|").Append(enabled);
                    try
                    {
                        var skinned = renderer as SkinnedMeshRenderer;
                        if (skinned != null && skinned.sharedMesh != null) sig.Append("|mesh=").Append(skinned.sharedMesh.name);
                        var materials = renderer.sharedMaterials;
                        for (int i = 0; materials != null && i < materials.Length; i++)
                            if (materials[i] != null) sig.Append("|mat=").Append(materials[i].name);
                    }
                    catch { }
                    parts.Add(sig.ToString());
                }
                parts.Sort(StringComparer.Ordinal);
                _appearanceProbeSig = string.Join("\n", parts);
                return _appearanceProbeSig;
            }
            catch { return _appearanceProbeSig; }
        }

        [HideFromIl2Cpp]
        private void SendState()
        {
            _directStateSyncCount++;
            string appearanceSig = CurrentAppearanceSignature();
            bool sampleActives = _directStateSyncCount == 1 || appearanceSig != _lastDirectAppearanceSig;
            if (sampleActives) _lastDirectAppearanceSig = appearanceSig;
            var st = SampleLocalState(false, false, sampleActives);
            if (st == null) return;
            var w = new WireWriter();
            w.WriteString(PeerId);
            WriteStateFields(w, st);
            Send(MsgTypes.State, w.ToArray());
        }

        [HideFromIl2Cpp]
        private RemoteState SampleLocalState(bool sampleBones = true, bool sampleAnimation = true, bool sampleActives = true)
        {
            var pca = PlayerFacade.Instance.pca;
            var anim = pca.Animator;
            var avatar = pca.AvatorTransform;
            if (anim == null || avatar == null) return null;

            if (sampleAnimation) EnsureAnimatorParams();

            var ps = pca.PlayerState;
            var st = new RemoteState();

            var pos = avatar.position;
            var rotY = avatar.eulerAngles.y;
            var scale = avatar.localScale;
            st.Pos = pos;
            st.RotY = rotY;
            st.Scale = scale;
            st.AnimSpeed = anim.speed;
            st.GroundY = _cachedGroundOffset;
            st.HipsLocal = _cachedHipsLocal;
            st.HipsLocalRot = _cachedHipsLocalRot;

            int layers = sampleAnimation ? anim.layerCount : 0;
            st.LayerWeights = new float[Math.Max(0, layers)];
            st.LayerStateHashes = new int[Math.Max(0, layers)];
            st.LayerStateTimes = new float[Math.Max(0, layers)];
            for (int i = 0; i < st.LayerWeights.Length; i++)
            {
                st.LayerWeights[i] = anim.GetLayerWeight(i);
                try
                {
                    var si = anim.GetCurrentAnimatorStateInfo(i);
                    st.LayerStateHashes[i] = si.shortNameHash;
                    st.LayerStateTimes[i] = si.normalizedTime;
                }
                catch { }
            }

            float[] fvals = null;
            int[] ivals = null;
            bool[] bvals = null;
            try
            {
                if (!sampleAnimation)
                {
                    fvals = new float[0]; ivals = new int[0]; bvals = new bool[0];
                }
                else
                {
                fvals = new float[_floatParams.Count];
                for (int i = 0; i < _floatParams.Count; i++) fvals[i] = anim.GetFloat(_floatParams[i].Hash);
                ivals = new int[_intParams.Count];
                for (int i = 0; i < _intParams.Count; i++) ivals[i] = anim.GetInteger(_intParams[i].Hash);
                bvals = new bool[_boolParams.Count];
                for (int i = 0; i < _boolParams.Count; i++) bvals[i] = anim.GetBool(_boolParams[i].Hash);
                }
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("动画参数读取失败（继续同步位置）: " + ex.Message);
                fvals = new float[0];
                ivals = new int[0];
                bvals = new bool[0];
            }

            st.FloatNames = new string[fvals.Length]; st.FloatVals = new float[fvals.Length];
            for (int i = 0; i < fvals.Length; i++) { st.FloatNames[i] = _floatParams[i].Name; st.FloatVals[i] = fvals[i]; }
            st.IntNames = new string[ivals.Length]; st.IntVals = new int[ivals.Length];
            for (int i = 0; i < ivals.Length; i++) { st.IntNames[i] = _intParams[i].Name; st.IntVals[i] = ivals[i]; }
            st.BoolNames = new string[bvals.Length]; st.BoolVals = new bool[bvals.Length];
            for (int i = 0; i < bvals.Length; i++) { st.BoolNames[i] = _boolParams[i].Name; st.BoolVals[i] = bvals[i]; }

            int action = ps.CurrentAction != null ? (int)ps.CurrentAction.Type : -1;
            int clothes = ps.CurrentClothes != null ? ps.CurrentClothes.TypeId : -1;
            st.PlayerState = (int)ps.CurrentState;
            st.ActionType = action;
            st.ClothesType = clothes;
            st.ClothesStateB = (int)ps.CurrentClothesBStateCache;
            st.IsCrouch = ps.IsCrouch;
            st.IsDash = ps.IsDash;
            st.IsPeeing = ps.IsPeeing;
            st.IsGaman = ps.IsGamanBaibu;
            st.IsEcstasy = ps.IsEcstasyMotion;
            st.Stage = CurrentStageInt();
            st.SexMode = SexManager.Instance != null && SexManager.Instance.IsSexMode;

            if (sampleBones) EnsureSendBoneList();
            if (sampleBones && _sendBoneTransforms.Count > 0)
            {
                st.BonePaths = new string[_sendBoneTransforms.Count];
                st.BoneQuats = new float[_sendBoneTransforms.Count * 4];
                for (int i = 0; i < _sendBoneTransforms.Count; i++)
                {
                    try
                    {
                        var q = _sendBoneTransforms[i].localRotation;
                        st.BonePaths[i] = _sendBonePaths[i];
                        int o = i * 4;
                        st.BoneQuats[o] = q.x;
                        st.BoneQuats[o + 1] = q.y;
                        st.BoneQuats[o + 2] = q.z;
                        st.BoneQuats[o + 3] = q.w;
                    }
                    catch { }
                }
            }
            else
            {
                st.BonePaths = new string[0];
                st.BoneQuats = new float[0];
            }

            if (sampleActives)
            {
                _lastPathsCollectTime = Time.unscaledTime;
                _cachedActivePaths = new List<string>();
                CollectActivePaths(avatar, avatar, "", _cachedActivePaths);
            }
            st.ActivePaths = sampleActives ? _cachedActivePaths : new List<string>();
            return st;
        }

        [HideFromIl2Cpp]
        private void EnsureSendBoneList()
        {
            if (!InGame) return;
            var avatar = PlayerFacade.Instance.pca.AvatorTransform;
            int id;
            try { id = avatar.GetInstanceID(); } catch { return; }
            if (id == _boneListAvatarId) return;
            _boneListAvatarId = id;
            _sourceCoreBoneCount = -1;
            _sendBonePaths = new List<string>();
            _sendBoneTransforms = new List<Transform>();
            var seenPaths = new HashSet<string>();
            try
            {
                var smrs = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                SkinnedMeshRenderer core = null;
                try
                {
                    var body = PlayerFacade.Instance.pca.PlayerAvatarObjectReferencer != null
                        ? PlayerFacade.Instance.pca.PlayerAvatarObjectReferencer.bodyMeshRenderer
                        : null;
                    if (body != null) core = body;
                }
                catch { }
                if (core == null && smrs.Length > 0) core = smrs[0];
                if (core == null) return;

                if (core.bones != null)
                {
                    foreach (var b in core.bones)
                    {
                        if (b == null) continue;
                        string path = RelativePath(avatar, b);
                        if (path.Length == 0 || !ShouldSyncBonePath(path) || !seenPaths.Add(path)) continue;
                        _sendBoneTransforms.Add(b);
                        _sendBonePaths.Add(path);
                    }
                }
                if (core.rootBone != null)
                {
                    string path = RelativePath(avatar, core.rootBone);
                    if (path.Length > 0 && seenPaths.Add(path))
                    {
                        _sendBoneTransforms.Add(core.rootBone);
                        _sendBonePaths.Add(path);
                    }
                }
            }
            catch { }
            PluginInfo.Info("发送骨骼列表: " + _sendBoneTransforms.Count + " 根=" + (avatar != null ? avatar.name : "null"));
            try
            {
                var sample = new System.Text.StringBuilder();
                for (int i = 0; i < Math.Min(10, _sendBonePaths.Count); i++)
                    sample.Append(_sendBonePaths[i] + " | ");
                PluginInfo.Info("发送骨骼示例: " + sample);
            }
            catch { }
        }

        private static bool ShouldSyncBonePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string p = path.ToLowerInvariant();
            string[] core = { "hips", "spine", "chest", "neck", "head", "shoulder", "arm", "hand",
                "leg", "foot", "toe", "thumb", "index", "middle", "ring", "little", "breast", "butt", "hipdb" };
            foreach (var token in core) if (p.Contains(token)) return true;
            return false;
        }

        private static string RelativePath(Transform root, Transform t)
        {
            if (root == null || t == null) return "";
            var names = new List<string>();
            var cur = t;
            while (cur != null && cur != root && cur.parent != null)
            {
                if (cur.name != "PlayerBoneScaleAdjuster") names.Add(cur.name);
                cur = cur.parent;
            }
            if (cur != root) return "";
            names.Reverse();
            return string.Join("/", names);
        }

        private static void WriteStateFields(WireWriter w, RemoteState st)
        {
            w.WriteFloat(st.Pos.x); w.WriteFloat(st.Pos.y); w.WriteFloat(st.Pos.z);
            w.WriteFloat(st.RotY);
            w.WriteFloat(st.Scale.x); w.WriteFloat(st.Scale.y); w.WriteFloat(st.Scale.z);
            w.WriteFloat(st.AnimSpeed);

            w.WriteInt(st.LayerWeights.Length);
            for (int i = 0; i < st.LayerWeights.Length; i++) w.WriteFloat(st.LayerWeights[i]);
            w.WriteInt(st.LayerStateHashes.Length);
            for (int i = 0; i < st.LayerStateHashes.Length; i++)
            {
                w.WriteInt(st.LayerStateHashes[i]);
                w.WriteFloat(st.LayerStateTimes[i]);
            }
            int boneCount = st.BonePaths != null ? st.BonePaths.Length : 0;
            w.WriteInt(boneCount);
            for (int i = 0; i < boneCount; i++)
            {
                w.WriteString(st.BonePaths[i]);
                int o = i * 4;
                w.WriteFloat(st.BoneQuats[o]);
                w.WriteFloat(st.BoneQuats[o + 1]);
                w.WriteFloat(st.BoneQuats[o + 2]);
                w.WriteFloat(st.BoneQuats[o + 3]);
            }

            w.WriteInt(st.FloatNames.Length);
            for (int i = 0; i < st.FloatNames.Length; i++) { w.WriteString(st.FloatNames[i]); w.WriteFloat(st.FloatVals[i]); }
            w.WriteInt(st.IntNames.Length);
            for (int i = 0; i < st.IntNames.Length; i++) { w.WriteString(st.IntNames[i]); w.WriteInt(st.IntVals[i]); }
            w.WriteInt(st.BoolNames.Length);
            for (int i = 0; i < st.BoolNames.Length; i++) { w.WriteString(st.BoolNames[i]); w.WriteBool(st.BoolVals[i]); }

            w.WriteInt(st.PlayerState);
            w.WriteInt(st.ActionType);
            w.WriteInt(st.ClothesType);
            w.WriteInt(st.ClothesStateB);
            w.WriteBool(st.IsCrouch);
            w.WriteBool(st.IsDash);
            w.WriteBool(st.IsPeeing);
            w.WriteBool(st.IsGaman);
            w.WriteBool(st.IsEcstasy);
            w.WriteInt(st.Stage);
            w.WriteBool(st.SexMode);

            var paths = st.ActivePaths ?? new List<string>();
            w.WriteInt(paths.Count);
            foreach (var p in paths) w.WriteString(p);
            w.WriteFloat(st.GroundY);
            w.WriteFloat(st.HipsLocal.x);
            w.WriteFloat(st.HipsLocal.y);
            w.WriteFloat(st.HipsLocal.z);
            w.WriteFloat(st.HipsLocalRot.x);
            w.WriteFloat(st.HipsLocalRot.y);
            w.WriteFloat(st.HipsLocalRot.z);
            w.WriteFloat(st.HipsLocalRot.w);
        }

        private static void CollectActivePaths(Transform root, Transform node, string prefix, List<string> paths)
        {
            if (node == null || root == null) return;
            try
            {
                for (int i = 0; i < node.childCount; i++)
                {
                    var child = node.GetChild(i);
                    if (child == null) continue;
                    string childName = child.name == "PlayerBoneScaleAdjuster" ? "" : child.name;
                    string path = prefix;
                    if (childName.Length > 0)
                        path = prefix.Length == 0 ? childName : prefix + "/" + childName;
                    if (childName.Length > 0 && child.gameObject.activeInHierarchy && HasVisibleMesh(child))
                        paths.Add(path);
                    CollectActivePaths(root, child, path, paths);
                }
            }
            catch { }
        }

        private static bool HasVisibleMesh(Transform t)
        {
            try
            {
                var sk = t.GetComponent<SkinnedMeshRenderer>();
                if (sk != null && sk.enabled && sk.sharedMesh != null && sk.sharedMaterial != null)
                    return true;
                var mr = t.GetComponent<MeshRenderer>();
                if (mr != null && mr.enabled)
                {
                    var mf = t.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && mr.sharedMaterial != null)
                        return true;
                }
            }
            catch { }
            return false;
        }

        private const byte KindFloat = 1;
        private const byte KindInt = 3;
        private const byte KindBool = 4;

        private static readonly string[] CoreBoneNames =
        {
            "Hips", "Spine", "Chest", "Neck", "Head",
            "Shoulder_L", "Shoulder_R", "UpperArm_L", "UpperArm_R",
            "LowerArm_L", "LowerArm_R", "Hand_L", "Hand_R",
            "UpperLeg_L", "UpperLeg_R", "LowerLeg_L", "LowerLeg_R", "Foot_L", "Foot_R"
        };

        private sealed class MotionClip
        {
            public string Key = "";
            public string Name = "";
            public bool Loop;
            public bool Hold;
            public int Mode;
            public float[] Offs = new float[0];
            public float[] Times = new float[0];
            public float[] Quats = new float[0];
        }

        private readonly Dictionary<string, MotionClip> _motionClips = new Dictionary<string, MotionClip>();
        private bool _motionClipsLoaded;
        private bool _captureActive;
        private string _captureKey = "";
        private float _captureStart = -999f;
        private float _captureLastSample = -999f;
        private float _lastAutoClipAt = -999f;
        private string _captureName = "";
        private int _autoCaptureIdx = -1;
        private int _autoCapturePhase;
        private float _autoCapturePhaseAt = -999f;
        private readonly List<float> _captureTimes = new List<float>();
        private readonly List<float[]> _captureFrames = new List<float[]>();
        private readonly List<float> _captureOffs = new List<float>();

        private static readonly int[] KnownActionIds =
        {
            0,1,2,3,4,5,6,7,8,
            10000,10001,10002,10003,10004,10005,10006,10007,10008,10009,10010,10011,10012,10013,
            10014,10015,10016,10017,10018,10019,10020,10021,10022,10023,10024,10025,10026,10027,
            10028,10029,
            10030,10031,10032,10033,10034,10035,10036,10037,10038,10039,10040,10041,10042,10043,10044,
            10045,10046,10047,10048,10049,10050,10051,10052,10053,10054,10055,10056,10057,10058,10059,
            10060,10061,10062,10063,10064,10065,10066,10067,10068,10076,10077,10078,
            10100,10101,10102,10103,10104,10105,10106,
            50000,50001,50002,50003,50004,50005,50006,50007,50008,50009,50010,50011,50012,50013,
            50014,50015,50016,50017,50018,50019,50020,50021,50022,
            60000,60001
        };
        private static readonly string[] KnownActionNames =
        {
            "OldOnaniNormal","OldGanimataWalk","Pinpon","ConbiniTakeGoods","CrouchCry","EatMedicine","SadHandcuffAtMap","SwitchTimeStop","SwitchPistonMachine",
            "PickingCoat","Pick","Drop","ChangeClothes","DroppingClothes","HandOver","InsertAnalPlug","ExtractAnalPlug","CommonEquip","IntoWasher","TakeFromWasher","UseBuyMachine","DrinkWater","PeeNormal",
            "TakeOffPants","TakeOnPants","TakeOffBra","TakeOnBra","Sad","AttachHandcuffs","PutHandcuffsOnMap","HandcuffsAtMap","UnlockHandcuffsAtMap","AttachEyeMask","SwitchVibrator","PickUpItem","SitDown","StandUp",
            "PutDildoFloor","PutDildoWall",
            "UseDildoFloorPussy1","UseDildoFloorPussy2","UseDildoFloorPussy3","UseDildoFloorPussy4","UseDildoFloorPussy5","UseDildoFloorAnal1","UseDildoFloorAnal2","UseDildoFloorAnal3","UseDildoFloorAnal4","UseDildoFloorAnal5","UseDildoFloorFella1","UseDildoFloorFella2","UseDildoFloorFella3","UseDildoFloorFella4","UseDildoFloorFella5",
            "UseDildoWallPussy1","UseDildoWallPussy2","UseDildoWallPussy3","UseDildoWallPussy4","UseDildoWallPussy5","UseDildoWallAnal1","UseDildoWallAnal2","UseDildoWallAnal3","UseDildoWallAnal4","UseDildoWallAnal5","UseDildoWallFella1","UseDildoWallFella2","UseDildoWallFella3","UseDildoWallFella4","UseDildoWallFella5",
            "UseDildoFloorWaitPussy","UseDildoFloorWaitAnal","UseDildoFloorWaitFella","UseDildoWallWaitPussy","UseDildoWallWaitAnal","UseDildoWallWaitFella","UseDildoFloorPussyEcstasyA","UseDildoFloorAnalEcstasyA","UseDildoFloorFellaEcstasyA","UseDildoWallPussyEcstasyA","UseDildoWallAnalEcstasyA","UseDildoWallFellaEcstasyA",
            "PickDildo","SitDildo","SitDildoPut","SitDildoPick","SitDildoMoveAnal","SitDildoMovePussy","PickDildoWall",
            "GanimataWalk","AhegaoDoublePiece","HipShake","GanimataHip","KaikyakuFella","Dogeza","DogTintin","IBalance","WakimiseCrouch","MituasiOnani","Tebura","PeeKaikyaku","PeeDog","ChikubiRotate",
            "OnaniYotuashi","OnaniNeGanimata","OnaniNormal","NeKataashiage","OnaniArmKuri","OnaniSikoru","GanimataKoshiHeko","Haigure","PeeStand",
            "DogezaUpHead","PoseEnd"
        };

        private static readonly Dictionary<int, string> _actionLabel = BuildActionLabels();
        private static Dictionary<int, string> BuildActionLabels()
        {
            var d = new Dictionary<int, string>();
            for (int i = 0; i < KnownActionIds.Length && i < KnownActionNames.Length; i++)
                d[KnownActionIds[i]] = KnownActionNames[i];
            d[0]="自慰·旧"; d[1]="蹲走·旧"; d[2]="按门铃"; d[3]="便利店取货"; d[4]="蹲哭"; d[5]="吃药";
            d[6]="地图戴铐"; d[7]="时停开关"; d[8]="活塞机开关";
            d[10000]="捡外套"; d[10001]="拾取"; d[10002]="放下"; d[10003]="换衣"; d[10004]="脱衣"; d[10005]="交出";
            d[10006]="插入肛塞"; d[10007]="拔出肛塞"; d[10008]="普通穿戴"; d[10009]="进洗衣机"; d[10010]="取洗衣机"; d[10011]="用购买机";
            d[10012]="喝水"; d[10013]="排尿"; d[10014]="脱裤"; d[10015]="穿裤"; d[10016]="脱胸罩"; d[10017]="穿胸罩";
            d[10018]="蹲下"; d[10019]="戴手铐"; d[10020]="放手铐(地图)"; d[10021]="手铐状态(地图)"; d[10022]="解锁手铐"; d[10023]="戴眼罩";
            d[10024]="振动器开关"; d[10025]="拾取道具"; d[10026]="坐下"; d[10027]="站起";
            d[10028]="放地面棒"; d[10029]="放墙面棒";
            d[10030]="地面·穴1"; d[10031]="地面·穴2"; d[10032]="地面·穴3"; d[10033]="地面·穴4"; d[10034]="地面·穴5";
            d[10035]="地面·肛1"; d[10036]="地面·肛2"; d[10037]="地面·肛3"; d[10038]="地面·肛4"; d[10039]="地面·肛5";
            d[10040]="地面·口1"; d[10041]="地面·口2"; d[10042]="地面·口3"; d[10043]="地面·口4"; d[10044]="地面·口5";
            d[10045]="墙面·穴1"; d[10046]="墙面·穴2"; d[10047]="墙面·穴3"; d[10048]="墙面·穴4"; d[10049]="墙面·穴5";
            d[10050]="墙面·肛1"; d[10051]="墙面·肛2"; d[10052]="墙面·肛3"; d[10053]="墙面·肛4"; d[10054]="墙面·肛5";
            d[10055]="墙面·口1"; d[10056]="墙面·口2"; d[10057]="墙面·口3"; d[10058]="墙面·口4"; d[10059]="墙面·口5";
            d[10060]="地面等待·穴"; d[10061]="地面等待·肛"; d[10062]="地面等待·口";
            d[10063]="墙面等待·穴"; d[10064]="墙面等待·肛"; d[10065]="墙面等待·口";
            d[10066]="地面高潮·穴"; d[10067]="地面高潮·肛"; d[10068]="地面高潮·口";
            d[10076]="墙面高潮·穴"; d[10077]="墙面高潮·肛"; d[10078]="墙面高潮·口";
            d[10100]="拾取棒"; d[10101]="坐棒"; d[10102]="坐棒·放"; d[10103]="坐棒·取"; d[10104]="坐棒·移肛"; d[10105]="坐棒·移穴"; d[10106]="取墙面棒";
            d[50000]="蹲走"; d[50001]="阿黑颜双指"; d[50002]="扭臀"; d[50003]="蹲臀"; d[50004]="跪坐口交"; d[50005]="土下座"; d[50006]="狗式"; d[50007]="I平衡"; d[50008]="腋下蹲"; d[50009]="密腿自慰"; d[50010]="手交"; d[50011]="跪坐排尿"; d[50012]="狗式排尿"; d[50013]="揉乳头"; d[50014]="四足自慰"; d[50015]="躺卧自慰"; d[50016]="自慰"; d[50017]="躺抬腿"; d[50018]="臂磨阴蒂"; d[50019]="自慰·涩"; d[50020]="蹲腰缩"; d[50021]="蛤蟆趴"; d[50022]="站立排尿";
            d[60000]="土下座抬头"; d[60001]="结束姿势";
            return d;
        }

        [HideFromIl2Cpp]
        private static string FmtCoord(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return "0.00";
            if (v > -0.005f && v < 0.005f) return "0.00";
            return v.ToString("0.00");
        }

        [HideFromIl2Cpp]
        private static string ToyCommandLabel(string d)
        {
            switch (d)
            {
                case "vibrate": return "振动器";
                case "thrust": return "伸缩棒";
                case "thrust_set": return "伸缩棒档位";
                case "goods": return "穿戴玩具";
                case "goods_off": return "脱下玩具";
                case "action": return "动作";
                case "handcuff": return "手铐";
                case "handcuff_back": return "手铐设置";
                case "unlock": return "解锁";
                case "pee": return "排尿";
                case "pee_stop": return "停止排尿";
                case "ecstasy": return "高潮";
                case "climax": return "高潮";
                case "crouch": return "蹲走";
                case "crawl": return "趴下";
                case "stand": return "站起";
                case "sit_toggle": return "坐站切换";
                case "undress_cycle": return "脱衣程度";
                case "undress_reset": return "回归衣服";
                case "bareta": return "露出";
                case "collar": return "项圈";
                case "uncollar": return "摘项圈";
                case "pleasure": return "快感";
                case "finger_pleasure": return "快感";
                default: return d;
            }
        }

        private static string ActionLabel(int id)
        {
            if (_actionLabel.TryGetValue(id, out var s)) return s;
            return "act_" + id;
        }

        private static readonly string[] KnownParamNames =
        {
            "MoveSpeed", "CoatState", "CoatStateB", "StrafeX", "StrafeY", "HeadHeight",
            "HandcuffsWeight", "LocomotionMotionSpeed", "Dokidoki", "BreastSize",
            "FutanariSize", "MotionSpeedScale", "MotionSpeedScaleWhenSlow", "WalkSpeedScale",
            "IsStrafe", "IsCrouch", "IsBareta", "BaretaFace", "BaretaEscape", "HadBasket",
            "HoldInHand", "IsHandcuffs", "IsGamanBaibu", "IsEcstasy", "PreviewNakedArmPose",
            "ActionId", "Action", "OldActionId", "AnotherMotionIndex"
        };

        [HideFromIl2Cpp]
        private void EnsureAnimatorParams()
        {
            if (_paramBuilt) return;
            _paramBuilt = true;
            try { BuildSendParamLists(); }
            catch (Exception ex)
            {
                PluginInfo.Warn("动画参数准备失败，只同步位置/场景: " + ex.Message);
                _floatParams = new List<AnimParamDef>();
                _intParams = new List<AnimParamDef>();
                _boolParams = new List<AnimParamDef>();
            }
        }

        private static byte GuessKind(string name)
        {
            switch (name)
            {
                case "MoveSpeed":
                case "CoatState":
                case "CoatStateB":
                case "StrafeX":
                case "StrafeY":
                case "HeadHeight":
                case "HandcuffsWeight":
                case "LocomotionMotionSpeed":
                case "Dokidoki":
                case "BreastSize":
                case "FutanariSize":
                case "MotionSpeedScale":
                case "MotionSpeedScaleWhenSlow":
                case "WalkSpeedScale":
                case "AnotherMotionIndex":
                    return KindFloat;
                case "IsStrafe":
                case "IsCrouch":
                case "IsBareta":
                case "BaretaFace":
                case "BaretaEscape":
                case "HadBasket":
                case "HoldInHand":
                case "IsHandcuffs":
                case "IsGamanBaibu":
                case "IsEcstasy":
                case "PreviewNakedArmPose":
                    return KindBool;
                case "ActionId":
                case "Action":
                case "OldActionId":
                    return KindInt;
                default:
                    return 0;
            }
        }

        [HideFromIl2Cpp]
        private void BuildSendParamLists()
        {
            _floatParams = new List<AnimParamDef>();
            _intParams = new List<AnimParamDef>();
            _boolParams = new List<AnimParamDef>();
            foreach (var name in KnownParamNames)
            {
                int hash = Animator.StringToHash(name);
                byte kind = GuessKind(name);
                if (kind == 0 || hash == 0) continue;
                var def = new AnimParamDef { Hash = hash, Name = name, Kind = kind };
                if (kind == KindFloat) _floatParams.Add(def);
                else if (kind == KindInt) _intParams.Add(def);
                else _boolParams.Add(def);
            }
            PluginInfo.Info("动画参数准备完成: float=" + _floatParams.Count +
                            " int=" + _intParams.Count + " bool=" + _boolParams.Count);
        }

        private sealed class AnimParamDef
        {
            public int Hash;
            public string Name;
            public byte Kind;
        }

        [HideFromIl2Cpp]
        private int CurrentStageInt()
        {
            var ig = InGameManager.Instance;
            if (ig == null || ig.CurrentStageController == null) return -1;
            return (int)ig.CurrentStageController.StageType;
        }

        // ========== 解析状态 ==========
        private static RemoteState ParseState(byte[] payload, out string senderId)
        {
            senderId = null;
            try
            {
                var r = new WireReader(payload);
                senderId = r.ReadString();
                var st = new RemoteState();
                st.Pos = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
                st.RotY = r.ReadFloat();
                st.Scale = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
                st.AnimSpeed = r.ReadFloat();
                int layers = r.ReadInt();
                st.LayerWeights = new float[Math.Max(0, layers)];
                for (int i = 0; i < st.LayerWeights.Length; i++) st.LayerWeights[i] = r.ReadFloat();
                int lsc = r.ReadInt();
                st.LayerStateHashes = new int[Math.Max(0, lsc)];
                st.LayerStateTimes = new float[Math.Max(0, lsc)];
                for (int i = 0; i < st.LayerStateHashes.Length; i++)
                {
                    st.LayerStateHashes[i] = r.ReadInt();
                    st.LayerStateTimes[i] = r.ReadFloat();
                }
                int bqc = r.ReadInt();
                st.BonePaths = new string[Math.Max(0, bqc)];
                st.BoneQuats = new float[Math.Max(0, bqc) * 4];
                for (int i = 0; i < bqc; i++)
                {
                    st.BonePaths[i] = r.ReadString();
                    int o = i * 4;
                    st.BoneQuats[o] = r.ReadFloat();
                    st.BoneQuats[o + 1] = r.ReadFloat();
                    st.BoneQuats[o + 2] = r.ReadFloat();
                    st.BoneQuats[o + 3] = r.ReadFloat();
                }

                int fc = r.ReadInt();
                st.FloatNames = new string[Math.Max(0, fc)]; st.FloatVals = new float[Math.Max(0, fc)];
                for (int i = 0; i < fc; i++) { st.FloatNames[i] = r.ReadString(); st.FloatVals[i] = r.ReadFloat(); }
                int ic = r.ReadInt();
                st.IntNames = new string[Math.Max(0, ic)]; st.IntVals = new int[Math.Max(0, ic)];
                for (int i = 0; i < ic; i++) { st.IntNames[i] = r.ReadString(); st.IntVals[i] = r.ReadInt(); }
                int bc = r.ReadInt();
                st.BoolNames = new string[Math.Max(0, bc)]; st.BoolVals = new bool[Math.Max(0, bc)];
                for (int i = 0; i < bc; i++) { st.BoolNames[i] = r.ReadString(); st.BoolVals[i] = r.ReadBool(); }

                st.PlayerState = r.ReadInt();
                st.ActionType = r.ReadInt();
                st.ClothesType = r.ReadInt();
                st.ClothesStateB = r.ReadInt();
                st.IsCrouch = r.ReadBool();
                st.IsDash = r.ReadBool();
                st.IsPeeing = r.ReadBool();
                st.IsGaman = r.ReadBool();
                st.IsEcstasy = r.ReadBool();
                st.Stage = r.ReadInt();
                st.SexMode = r.ReadBool();

                int ac = r.ReadInt();
                st.ActivePaths = new List<string>();
                for (int i = 0; i < ac; i++)
                {
                    st.ActivePaths.Add(r.ReadString());
                }
                if (r.Remaining >= 4) st.GroundY = r.ReadFloat();
                if (r.Remaining >= 12)
                    st.HipsLocal = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
                if (r.Remaining >= 16)
                    st.HipsLocalRot = new Quaternion(r.ReadFloat(), r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
                return st;
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("状态解析失败: " + ex.Message);
                return null;
            }
        }

        // ========== Ghost分身 ==========
        [HideFromIl2Cpp]
        private GhostPlayer GetOrCreateGhost(string peerId)
        {
            if (!SceneSyncReady) return null;
            if (_ghosts.TryGetValue(peerId, out var g) && g != null && g.Root != null)
            {
                if (g.HasMarker && Time.unscaledTime - g.CreatedTime > 10f)
                {
                    g.CreatedTime = Time.unscaledTime;
                    if (SourceHasValidMeshes())
                    {
                        g.Destroy();
                        _ghosts.Remove(peerId);
                        return GetOrCreateGhost(peerId);
                    }
                }
                return g;
            }

            if (_ghostCreateTimes.TryGetValue(peerId, out var lastCreate) &&
                Time.unscaledTime - lastCreate < 2f)
            {
                if (g != null && g.Root == null && _ghostWarned.Add(peerId))
                {
                    PluginInfo.Warn("分身对象被意外销毁，等待冷却后重建: " + peerId);
                }
                return null;
            }

            if (_ghostRoot == null)
            {
                _ghostRoot = new GameObject("SFMOnline_Ghosts");
            }
            try
            {
                _ghostCreateTimes[peerId] = Time.unscaledTime;
                _ghostWarned.Remove(peerId);
                var np = new GhostPlayer(peerId, _ghostRoot.transform);
                if (np.Root == null || (np.RendererCount == 0 && !np.HasMarker))
                {
                    np.Destroy();
                    _ghosts.Remove(peerId);
                    return null;
                }
                _ghosts[peerId] = np;
                PluginInfo.Info("已生成分身: " + peerId + " 渲染器=" + np.RendererCount +
                                " 有效网格=" + np.CountValidRenderers() +
                                " 标记模式=" + np.HasMarker +
                                " 动画=" + (np.Animator != null));
                DumpPlayerDiagnostics(PlayerFacade.Instance != null ? PlayerFacade.Instance.pca : null);
                if (_ghostToasted.Add(peerId))
                {
                    Toast(np.HasMarker
                        ? "暂用位置标记显示 " + GetPeerName(peerId)
                        : "已生成 " + GetPeerName(peerId) + " 的分身");
                }
                return np;
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("创建远程分身失败: " + ex.Message);
                return null;
            }
        }

        [HideFromIl2Cpp]
        private bool SourceHasValidMeshes()
        {
            if (!InGame) return false;
            var pca = PlayerFacade.Instance.pca;
            if (pca == null) return false;
            if (pca.AvatorTransform != null &&
                GhostPlayer.CountValidRenderersIn(pca.AvatorTransform.gameObject) >= 5)
                return true;
            try
            {
                var body = pca.PlayerAvatarObjectReferencer != null
                    ? pca.PlayerAvatarObjectReferencer.bodyMeshRenderer
                    : null;
                if (body != null && body.transform != null)
                {
                    var t = body.transform;
                    var stop = pca.GameObject != null ? pca.GameObject.transform : null;
                    while (t != null && t.parent != null && t.parent != stop && t.parent != t)
                        t = t.parent;
                    if (t != null && GhostPlayer.CountValidRenderersIn(t.gameObject) >= 5)
                        return true;
                }
            }
            catch { }
            if (pca.GameObject != null && GhostPlayer.CountValidRenderersIn(pca.GameObject) >= 5)
                return true;
            return false;
        }

        [HideFromIl2Cpp]
        private int SourceCoreBoneCount()
        {
            if (_sourceCoreBoneCount >= 0) return _sourceCoreBoneCount;
            _sourceCoreBoneCount = 0;
            try
            {
                if (InGame)
                {
                    var pca = PlayerFacade.Instance.pca;
                    SkinnedMeshRenderer core = null;
                    try
                    {
                        core = pca.PlayerAvatarObjectReferencer != null
                            ? pca.PlayerAvatarObjectReferencer.bodyMeshRenderer
                            : null;
                    }
                    catch { }
                    if (core == null && pca.AvatorTransform != null)
                        core = pca.AvatorTransform.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (core != null && core.bones != null) _sourceCoreBoneCount = core.bones.Length;
                }
            }
            catch { }
            return _sourceCoreBoneCount;
        }

        [HideFromIl2Cpp]
        private void RemoveGhost(string peerId)
        {
            if (_ghosts.TryGetValue(peerId, out var g))
            {
                g.Destroy();
                _ghosts.Remove(peerId);
            }
        }

        [HideFromIl2Cpp]
        private void RemoveAllGhosts()
        {
            foreach (var g in _ghosts.Values) g.Destroy();
            _ghosts.Clear();
            if (_ghostRoot != null) UnityEngine.Object.Destroy(_ghostRoot);
            _ghostRoot = null;
        }

        // ========== 诊断 ==========
        private static bool _rendererDumpDone;

        [HideFromIl2Cpp]
        private void ForceDumpDiagnostics()
        {
            _rendererDumpDone = false;
            try { DumpPlayerDiagnostics(PlayerFacade.Instance != null ? PlayerFacade.Instance.pca : null); }
            catch { }
            foreach (var kv in _ghosts)
            {
                var g = kv.Value;
                if (g == null || g.Root == null) continue;
                PluginInfo.Info("分身明细 " + kv.Key + " " + g.FreshRendererSummary() +
                                " " + g.AnimationReadback() + ":\n" + g.RendererDebugSummary(10));
            }
            Toast(Lang.Get("toast_diag_done"));
        }

        private static void DumpPlayerDiagnostics(PlayerClassAccessor pca)
        {
            if (_rendererDumpDone || pca == null) return;
            _rendererDumpDone = true;
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("===== 玩家渲染器诊断 =====");
                try
                {
                    var body = pca.PlayerAvatarObjectReferencer != null
                        ? pca.PlayerAvatarObjectReferencer.bodyMeshRenderer
                        : null;
                    sb.AppendLine("bodyMeshRenderer=" +
                                  (body != null
                                      ? PathOf(body.transform) + " mesh=" +
                                        (body.sharedMesh != null ? body.sharedMesh.name : "null")
                                      : "null"));
                }
                catch (Exception ex) { sb.AppendLine("body读取异常: " + ex.Message); }

                if (pca.GameObject != null)
                {
                    var skinned = pca.GameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var meshes = pca.GameObject.GetComponentsInChildren<MeshRenderer>(true);
                    sb.AppendLine("玩家渲染器: 骨骼=" + skinned.Length + " 网格=" + meshes.Length);
                    int shown = 0;
                    foreach (var s in skinned)
                    {
                        if (s == null || shown >= 25) continue;
                        shown++;
                        bool vis = false;
                        try { vis = s.isVisible; } catch { }
                        sb.AppendLine(shown + ") SkinnedMesh" +
                                      " 激活=" + s.gameObject.activeInHierarchy +
                                      " 启用=" + s.enabled +
                                      " 可见=" + vis +
                                      " 网格=" + (s.sharedMesh != null ? s.sharedMesh.name : "null") +
                                      " 路径=" + PathOf(s.transform));
                    }
                    foreach (var m in meshes)
                    {
                        if (m == null || shown >= 25) continue;
                        shown++;
                        string mesh = "null";
                        try
                        {
                            var mf = m.GetComponent<MeshFilter>();
                            mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "null";
                        }
                        catch { }
                        bool vis = false;
                        try { vis = m.isVisible; } catch { }
                        sb.AppendLine(shown + ") MeshRenderer" +
                                      " 激活=" + m.gameObject.activeInHierarchy +
                                      " 启用=" + m.enabled +
                                      " 可见=" + vis +
                                      " 网格=" + mesh);
                    }
                }
                else
                {
                    sb.AppendLine("pca.GameObject 为空");
                }
                sb.AppendLine("===== 诊断结束 =====");
                PluginInfo.Info(sb.ToString());
            }
            catch (Exception ex)
            {
                PluginInfo.Warn("渲染器诊断失败: " + ex.Message);
            }
        }

        private static string PathOf(Transform t)
        {
            if (t == null) return "null";
            var names = new List<string>();
            var cur = t;
            while (cur != null && names.Count < 8)
            {
                names.Add(cur.name);
                cur = cur.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        // ========== 内部数据类型 ==========

        private sealed class RemoteState
        {
            public Vector3 Pos;
            public float RotY;
            public Vector3 Scale;
            public float AnimSpeed;
            public float[] LayerWeights;
            public int[] LayerStateHashes;
            public float[] LayerStateTimes;
            public string[] BonePaths;
            public float[] BoneQuats;
            public string[] FloatNames;
            public float[] FloatVals;
            public string[] IntNames;
            public int[] IntVals;
            public string[] BoolNames;
            public bool[] BoolVals;
            public int PlayerState;
            public int ActionType;
            public int ClothesType;
            public int ClothesStateB;
            public bool IsCrouch;
            public bool IsDash;
            public bool IsPeeing;
            public bool IsGaman;
            public bool IsEcstasy;
            public bool IsRiding;
            public bool IsRidden;
            public int Stage;
            public bool SexMode;
            public float GroundY;
            public Vector3 HipsLocal;
            public Quaternion HipsLocalRot;
            public List<string> ActivePaths = new List<string>();
        }

        private sealed class GhostPlayer
        {
            private static readonly HashSet<string> KeepNativeTypes = new HashSet<string>
            {
                "Transform", "RectTransform", "Animator",
                "Renderer", "SkinnedMeshRenderer", "MeshRenderer", "MeshFilter",
                "ParticleSystem", "ParticleSystemRenderer",
                "DynamicBone", "DynamicBoneCollider", "DynamicBoneColliderBase",
                "DynamicBonePlaneCollider",
                "RigBuilder", "Rig", "ChainIKConstraint",
                "PlayerBoneScaleAdjuster", "PlayerBoneScaleAdjustManager",
                "BreastCostumeSizeAdjuster", "HipCostumeSizeAdjuster",
                "FootHeelAdjuster", "TransformAdjusterByHipSize", "HipDBCAdjuster"
            };

            public readonly string PeerId;
            public GameObject Root;
            public Animator Animator;
            private PlayerAnimationManager _nativeAnimation;
            private PlayerStateModel _nativePlayerState;
            private int _lastNativeActionType = int.MinValue;
            private int _lastNativeActionId = int.MinValue;
            private int _lastNativeActionParam = int.MinValue;
            public int RendererCount;
            [HideFromIl2Cpp]
            public int BoneMapCount => _boneMap.Count;
            public float CreatedTime;
            public bool HasMarker;
            private Vector3 _avatarOffset;
            private Transform _modelRoot;
            private readonly List<Renderer> _renderers = new List<Renderer>();
            private readonly List<SkinnedMeshRenderer> _skinned = new List<SkinnedMeshRenderer>();
            private readonly List<MeshRenderer> _meshRenderers = new List<MeshRenderer>();
            private readonly List<Transform> _boneTransforms = new List<Transform>();
            private readonly Dictionary<string, Transform> _boneMap = new Dictionary<string, Transform>();
            private float[] _lastBoneQuats;
            private string[] _lastBonePaths;
            private int _boneMatchedLast;
            private bool _boneMismatchLogged;
            private int _lastActivePathsSig = -1;
            private bool _diagnosed;
            private float _lastRenderersRefresh = -1f;
            private bool _warnedRenderersLost;
            private readonly Dictionary<int, bool> _validParams = new Dictionary<int, bool>();
            private readonly HashSet<int> _blockedRendererIds = new HashSet<int>();
            private bool _remoteRiding;
            private bool _remoteRidden;
            private Color? _highlightColor = null;
            private readonly Dictionary<Renderer, Color> _origColors = new Dictionary<Renderer, Color>();
            private Vector3 _targetPosition;
            private Vector3 _previousTargetPosition;
            private Vector3 _estimatedVelocity;
            private Vector3 _motionVelocity;
            private bool _motionMoving;
            private float _lastMotionPacketTime = -999f;
            private int _motionAction = int.MinValue;
            private int _motionHash = 0;
            private bool _tickMoving;
            private float _tickMoveSpeed;
            private float _tickLocoSpeed = 1f;
            private float _tickStrafeX;
            private float _tickStrafeY;
            private bool _tickStrafe;
            private bool _tickCrouch;
            private bool _tickDash;
            private int _tickActionType = int.MinValue;
            private int _tickActionId = int.MinValue;
            private int _tickActionParam = int.MinValue;
            private int _tickOldActionId = -1;
            private float _tickAnotherMotion;
            private int _tickStateHash;
            private int _lastPlayedStateHash;
            private Dictionary<string, Transform> _cloneBoneByName;
            private MotionClip _moveClip;
            private float _moveClipTime;
            private float _moveOriginY;
            private MotionClip _actClip;
            private float _actClipTime;
            private float _actOriginY;
            private float _lastActionLogAt = -999f;
            private float _lastMotionLogAt = -999f;
            private bool _remoteStrafe;
            private bool _remoteDash;
            private float _remoteMoveSpeed = float.NaN;
            private float _remoteLocomotionSpeed = float.NaN;
            private float _remoteStrafeX = float.NaN;
            private float _remoteStrafeY = float.NaN;
            private float _lastNetworkPoseTime = -999f;
            private float _targetRotationY;
            private bool _hasTargetPose;
            private bool _lodVisible = true;
            private int[] _lastLayerHashes=new int[0];private int _lastActionType=int.MinValue,_pendingActionType=int.MinValue;
            private int[] _lastPlayedLayerHashes = new int[0];
            private ParticleSystem _peeFx;
            private ParticleSystem _shioFx;
            private float _fxStopAtPee = -1f;
            private float _fxStopAtShio = -1f;
            public float FloorY = float.NaN;
            private float _floorCorrectionY = 0f;
            private Transform _hipsBone;
            private Vector3 _targetHipsLocal = Vector3.zero;
            private Quaternion _targetHipsLocalRot = Quaternion.identity;
            private bool _hasHips = false;

            [HideFromIl2Cpp]
            public GhostPlayer(string peerId, Transform parent, bool markerOnly = false)
            {
                PeerId = peerId;
                CreatedTime = Time.unscaledTime;
                if (!(PlayerFacade.Instance != null && PlayerFacade.Instance.pca != null && PlayerFacade.Instance.pca.AvatorTransform != null))
                    return;

                var pca = PlayerFacade.Instance.pca;
                Root = new GameObject("SFM_Ghost_" + peerId);
                Root.transform.SetParent(parent, false);
                if (markerOnly)
                {
                    CreateMarker();
                    return;
                }
                try
                {
                    var candidates = new List<GameObject>();
                    var seen = new HashSet<int>();
                    void TryAdd(GameObject go)
                    {
                        if (go != null)
                        {
                            int id = go.GetInstanceID();
                            if (seen.Add(id)) candidates.Add(go);
                        }
                    }

                    GameObject visibleRoot = null;
                    try
                    {
                        if (pca.GameObject != null)
                        {
                            var renderers = pca.GameObject.GetComponentsInChildren<Renderer>(true);
                            foreach (var r in renderers)
                            {
                                if (r == null) continue;
                                bool vis = false;
                                try { vis = r.isVisible; } catch { }
                                if (!vis) continue;
                                var t = r.transform;
                                var stop = pca.GameObject.transform;
                                while (t != null && t.parent != null && t.parent != stop && t.parent != t)
                                    t = t.parent;
                                visibleRoot = t != null ? t.gameObject : null;
                                break;
                            }
                        }
                    }
                    catch { }
                    if (visibleRoot != null) TryAdd(visibleRoot);

                    try
                    {
                        var body = pca.PlayerAvatarObjectReferencer != null
                            ? pca.PlayerAvatarObjectReferencer.bodyMeshRenderer
                            : null;
                        if (body != null && body.transform != null)
                        {
                            var t = body.transform;
                            var stop = pca.GameObject != null ? pca.GameObject.transform : null;
                            while (t != null && t.parent != null && t.parent != stop && t.parent != t)
                                t = t.parent;
                            TryAdd(t != null ? t.gameObject : null);
                        }
                    }
                    catch { }
                    if (pca.AvatorTransform != null) TryAdd(pca.AvatorTransform.gameObject);
                    if (pca.GameObject != null) TryAdd(pca.GameObject);

                    bool ok = false;
                    foreach (var cand in candidates)
                    {
                        if (cand == null) continue;
                        bool whole = cand == pca.GameObject;
                        if (cand == visibleRoot)
                        {
                            if (CountRenderersIn(cand) == 0) continue;
                            ok = BuildClone(cand, cand == pca.GameObject, PathOf(cand.transform));
                        }
                        else
                        {
                            if (CountValidRenderersIn(cand) < 5) continue;
                            ok = BuildClone(cand, whole, PathOf(cand.transform));
                        }
                        if (ok) break;
                    }
                    if (!ok)
                    {
                        PluginInfo.Warn("分身模型始终没有有效网格，改用位置标记: " + peerId);
                        CreateMarker();
                        DumpPlayerDiagnostics(pca);
                    }
                }
                catch (Exception ex)
                {
                    PluginInfo.Warn("分身创建异常: " + ex.Message);
                    try { CreateMarker(); } catch { }
                }
            }

            [HideFromIl2Cpp]
            private bool BuildClone(GameObject source, bool wholePlayer, string sourceName)
            {
                var srcRenderers = source.GetComponentsInChildren<Renderer>(true);
                var clone = UnityEngine.Object.Instantiate(source, Root.transform, false);
                clone.name = wholePlayer ? "PlayerClone" : "Avatar";
                _modelRoot = clone.transform;
                clone.SetActive(true);
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = Quaternion.identity;
                clone.transform.localScale = Vector3.one;

                StripComponents(clone);

                try
                {
                    foreach (var ps in clone.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        if (ps == null) continue;
                        try { ps.enableEmission = false; } catch { }
                        try { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); } catch { }
                    }
                }
                catch { }

                try { SetLayerRecursive(clone.transform, 0); } catch { }

                _renderers.Clear();
                _skinned.Clear();
                _meshRenderers.Clear();
                var renderers = clone.GetComponentsInChildren<Renderer>(true);
                var skinned = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var meshes = clone.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (IsSuspiciousAddonRenderer(r))
                    {
                        try { _blockedRendererIds.Add(r.GetInstanceID()); r.enabled = false; } catch { }
                        continue;
                    }
                    _renderers.Add(r);
                }
                foreach (var s in skinned)
                {
                    if (s == null) continue;
                    _skinned.Add(s);
                    try { s.updateWhenOffscreen = false; } catch { }
                }

                // 关键：Instantiate 复制的蒙皮网格仍引用源模型的骨骼。
                // 按源/克隆层级“同序”一对一映射，把骨骼指回克隆体自己；
                // 只有全部解析成功才替换，避免解析失败导致整个模型消失。
                var boneMap = new Dictionary<Transform, Transform>();
                try { BuildCloneTransformMap(source.transform, clone.transform, boneMap); } catch { }
                foreach (var s in skinned)
                {
                    if (s == null) continue;
                    try
                    {
                        if (s.bones != null && s.bones.Length > 0)
                        {
                            var newBones = new Transform[s.bones.Length];
                            bool allOk = true;
                            for (int bi = 0; bi < s.bones.Length; bi++)
                            {
                                var srcBone = s.bones[bi];
                                Transform mapped = null;
                                if (srcBone != null) boneMap.TryGetValue(srcBone, out mapped);
                                if (srcBone != null && mapped == null)
                                {
                                    string rel = RelativeBonePath(source.transform, srcBone);
                                    if (rel.Length > 0) mapped = FindPath(clone.transform, rel);
                                }
                                newBones[bi] = mapped;
                                if (srcBone == null || mapped == null) { allOk = false; break; }
                            }
                            // 只要有一个骨骼映射失败就不整体替换，避免引用错乱；失败部分保持源骨骼
                            if (allOk) s.bones = newBones;
                        }
                        if (s.rootBone != null)
                        {
                            Transform mappedRoot = null;
                            if (boneMap.TryGetValue(s.rootBone, out mappedRoot) && mappedRoot != null)
                                s.rootBone = mappedRoot;
                        }
                    }
                    catch { }
                }
                foreach (var m in meshes)
                {
                    if (m != null) _meshRenderers.Add(m);
                }

                // Bone packets are no longer part of normal networking. Animator owns all bone evaluation.
                _boneTransforms.Clear();
                _boneMap.Clear();
                _cloneBoneByName = new Dictionary<string, Transform>();
                RendererCount = renderers.Length;
                _lastRenderersRefresh = Time.unscaledTime;
                if (RendererCount == 0)
                {
                    PluginInfo.Warn("分身没有渲染器: " + PeerId);
                    return false;
                }


                int copyN = Math.Min(srcRenderers.Length, renderers.Length);
                for (int i = 0; i < copyN; i++)
                {
                    try
                    {
                        var mpb = new MaterialPropertyBlock();
                        srcRenderers[i].GetPropertyBlock(mpb);
                        renderers[i].SetPropertyBlock(mpb);
                    }
                    catch { }
                }

                try
                {
                    Animator = clone.GetComponent<Animator>();
                    if (Animator == null || Animator.runtimeAnimatorController == null)
                    {
                        var allAnims = clone.GetComponentsInChildren<Animator>(true);
                        foreach (var a in allAnims)
                        {
                            if (a != null && a.runtimeAnimatorController != null)
                            {
                                Animator = a;
                                break;
                            }
                        }
                    }
                }
                catch { }
                if (Animator != null)
                {
                    try { Animator.enabled = true; } catch { }
                    try { Animator.applyRootMotion = false; } catch { }
                    try { Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; } catch { }
                    try
                    {
                        var srcAnim = PlayerFacade.Instance != null && PlayerFacade.Instance.pca != null
                            ? PlayerFacade.Instance.pca.Animator
                            : null;
                        if (srcAnim != null)
                        {
                            if (Animator.runtimeAnimatorController == null)
                                Animator.runtimeAnimatorController = srcAnim.runtimeAnimatorController;
                            if (Animator.avatar == null)
                                Animator.avatar = srcAnim.avatar;
                        }
                    }
                    catch { }
                    try { Animator.Rebind(); Animator.Update(0f); } catch { }
                    // Isolated game animation system for this remote clone only.
                    try
                    {
                        foreach (var name in KnownParamNames)
                        {
                            int hash = Animator.StringToHash(name);
                            if (hash != 0) _validParams[hash] = true;
                        }
                    }
                    catch { }
                }

                if (Animator != null)
                {
                    try
                    {
                        string ctrl = "null";
                        try { ctrl = Animator.runtimeAnimatorController != null ? "有" : "null"; } catch { ctrl = "读取失败"; }
                        string av = "null";
                        try { av = Animator.avatar != null ? "有" : "null"; } catch { av = "读取失败"; }
                        int pc = -1, lc = -1;
                        try { pc = Animator.parameterCount; } catch { }
                        try { lc = Animator.layerCount; } catch { }
                        PluginInfo.Info("分身动画诊断: " + PeerId +
                                        " 控制器=" + ctrl +
                                        " avatar=" + av +
                                        " 参数数=" + pc +
                                        " 层数=" + lc);
                        PluginInfo.Info("分身动画回读: " + PeerId + " " + AnimationReadback());
                    }
                    catch { }
                }

                _avatarOffset = Vector3.zero; // 分身根直接对齐对方坐标
                if (wholePlayer)
                {
                    try
                    {
                        _avatarOffset = PlayerFacade.Instance.pca.AvatorTransform.position -
                                        PlayerFacade.Instance.pca.Transform.position;
                    }
                    catch { _avatarOffset = Vector3.zero; }
                }

                Root.transform.position = new Vector3(0f, -9999f, 0f);
                return true;
            }

            private static bool IsSuspiciousAddonRenderer(Renderer r)
            {
                try
                {
                    if (r == null) return false;
                    string nm = (r.gameObject != null ? r.gameObject.name : "").ToLowerInvariant();
                    Vector3 s = r.bounds.size;
                    float max = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                    // 只隐藏真正的巨型异常块（缺资源生成的大白条/超大透明体）。
                    // 注意：不能因“细长”就屏蔽——长发/辫子/飘带等合法渲染器就是细长的，否则头发会消失。
                    if (max > 16f) return true;
                    if (nm.Contains("debug") || nm.Contains("gizmo")) return true;
                }
                catch { }
                return false;
            }

            [HideFromIl2Cpp]
            public int CountEnabledRenderers()
            {
                int n = 0;
                foreach (var r in _renderers) if (r != null && r.enabled) n++;
                return n;
            }

            public int CountValidRenderers()
            {
                int n = 0;
                foreach (var s in _skinned)
                {
                    if (s == null) continue;
                    try { if (s.sharedMesh != null && s.sharedMaterial != null) n++; } catch { }
                }
                foreach (var m in _meshRenderers)
                {
                    if (m == null) continue;
                    try
                    {
                        var mf = m.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null && m.sharedMaterial != null) n++;
                    }
                    catch { }
                }
                return n;
            }

            public static int CountValidRenderersIn(GameObject root)
            {
                int n = 0;
                if (root == null) return 0;
                try
                {
                    var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    foreach (var s in skinned)
                    {
                        if (s == null) continue;
                        try { if (s.sharedMesh != null && s.sharedMaterial != null) n++; } catch { }
                    }
                    var meshes = root.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var m in meshes)
                    {
                        if (m == null) continue;
                        try
                        {
                            var mf = m.GetComponent<MeshFilter>();
                            if (mf != null && mf.sharedMesh != null && m.sharedMaterial != null) n++;
                        }
                        catch { }
                    }
                }
                catch { }
                return n;
            }

            public static int CountRenderersIn(GameObject root)
            {
                if (root == null) return 0;
                try { return root.GetComponentsInChildren<Renderer>(true).Length; }
                catch { return 0; }
            }

            internal static string TypeName(Component c)
            {
                try { return c.GetType().FullName ?? ""; }
                catch { return ""; }
            }

            [HideFromIl2Cpp]
            private void CreateMarker()
            {
                if (Root == null) return;
                HasMarker = true;
                try
                {
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    marker.name = "PositionMarker";
                    marker.transform.SetParent(Root.transform, false);
                    marker.transform.localPosition = new Vector3(0f, 1.35f, 0f);
                    marker.transform.localScale = new Vector3(0.18f, 0.38f, 0.18f);
                    marker.layer = 0;
                    var mr = marker.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.enabled = true;
                        try { mr.material.color = new Color(0.65f, 0.2f, 1f, 0.75f); } catch { }
                    }
                    _renderers.Add(marker.GetComponent<Renderer>());
                    RendererCount = 1;
                    PluginInfo.Info("已创建兜底位置标记: " + PeerId);
                }
                catch (Exception ex)
                {
                    PluginInfo.Warn("创建位置标记失败: " + ex.Message);
                }
            }

            private static void SetLayerRecursive(Transform t, int layer)
            {
                if (t == null) return;
                t.gameObject.layer = layer;
                for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
            }

            [HideFromIl2Cpp]
            public int CountActiveRenderers()
            {
                int n = 0;
                foreach (var r in _renderers)
                {
                    if (r == null) continue;
                    try
                    {
                        if (r.enabled && r.gameObject.activeInHierarchy) n++;
                    }
                    catch { }
                }
                return n;
            }

            [HideFromIl2Cpp]
            public int CountVisibleRenderers()
            {
                int n = 0;
                foreach (var r in _renderers)
                {
                    if (r == null) continue;
                    try { if (r.isVisible) n++; } catch { }
                }
                return n;
            }

            [HideFromIl2Cpp]
            public string RendererDebugSummary(int max)
            {
                var sb = new System.Text.StringBuilder();
                int shown = 0;
                foreach (var s in _skinned)
                {
                    if (s == null || shown >= max) continue;
                    shown++;
                    string path = "";
                    try
                    {
                        var names = new List<string>();
                        var t = s.transform;
                        while (t != null && names.Count < 5)
                        {
                            names.Add(t.name);
                            t = t.parent;
                        }
                        names.Reverse();
                        path = string.Join("/", names);
                    }
                    catch { }
                    bool ahier = false, en = false;
                    try { ahier = s.gameObject.activeInHierarchy; } catch { }
                    try { en = s.enabled; } catch { }
                    sb.AppendLine("  " + shown + ") SkinnedMesh 层级激活=" + ahier +
                                  " 启用=" + en +
                                  " 网格=" + (s.sharedMesh != null ? s.sharedMesh.name : "null") +
                                  " " + path);
                }
                foreach (var m in _meshRenderers)
                {
                    if (m == null || shown >= max) continue;
                    shown++;
                    string meshName = "null";
                    try
                    {
                        var mf = m.GetComponent<MeshFilter>();
                        meshName = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "null";
                    }
                    catch { }
                    bool ahier = false, en = false;
                    try { ahier = m.gameObject.activeInHierarchy; } catch { }
                    try { en = m.enabled; } catch { }
                    sb.AppendLine("  " + shown + ") MeshRenderer 层级激活=" + ahier +
                                  " 启用=" + en +
                                  " 网格=" + meshName);
                }
                return sb.ToString();
            }

            [HideFromIl2Cpp]
            public string FreshRendererSummary()
            {
                try
                {
                    if (Root == null) return "Root为空";
                    var rs = Root.GetComponentsInChildren<Renderer>(true);
                    var ss = Root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    int valid = 0;
                    int vis = 0;
                    foreach (var s in ss)
                    {
                        if (s == null) continue;
                        try
                        {
                            if (s.sharedMesh != null && s.sharedMaterial != null) valid++;
                            if (s.isVisible) vis++;
                        }
                        catch { }
                    }
                    return "实时渲染器=" + rs.Length + " 实时有效=" + valid + " 实时可见=" + vis;
                }
                catch (Exception ex)
                {
                    return "实时读取失败: " + ex.Message;
                }
            }

            [HideFromIl2Cpp]
            public string AnimationReadback()
            {
                var sb = new System.Text.StringBuilder();
                try
                {
                    if (Animator == null) return "动画器=null";
                    sb.Append("绑定=");
                    try { sb.Append(Animator.hasBoundPlayables ? "是" : "否"); } catch { sb.Append("?"); }
                    sb.Append(" 初始化=");
                    try { sb.Append(Animator.isInitialized ? "是" : "否"); } catch { sb.Append("?"); }
                    try
                    {
                        sb.Append(" MoveSpeed=" + Animator.GetFloat(Animator.StringToHash("MoveSpeed")).ToString("0.00"));
                    }
                    catch { }
                    try
                    {
                        int maxLayers = Math.Min(6, Animator.layerCount);
                        for (int l = 0; l < maxLayers; l++)
                        {
                            var si = Animator.GetCurrentAnimatorStateInfo(l);
                            sb.Append(" L" + l + "=" + si.fullPathHash +
                                      ":" + si.normalizedTime.ToString("0.00"));
                        }
                    }
                    catch { }
                    sb.Append(" 骨骼数=" + _boneTransforms.Count);
                    try
                    {
                        if (_skinned.Count > 0)
                        {
                            var smr = _skinned[0];
                            if (smr != null && smr.bones != null && smr.bones.Length > 0)
                            {
                                var b = smr.bones[0];
                                string broot = "?";
                                try { broot = b != null && b.root != null ? b.root.name : "null"; } catch { }
                                string mroot = _modelRoot != null ? _modelRoot.name : "null";
                                sb.Append(" 骨骼0=" + (b != null ? b.name : "null") +
                                          " 骨骼根=" + broot +
                                          " 模型根=" + mroot);
                            }
                        }
                    }
                    catch { }
                }
                catch { }
                return sb.ToString();
            }

            [HideFromIl2Cpp]
            public void MarkAction(int actionType){_pendingActionType=actionType;}
            [HideFromIl2Cpp]
            public void MarkAction(int actionType, int stateHash)
            {
                MarkAction(actionType, stateHash, new int[0], new float[0], new float[0]);
            }
            [HideFromIl2Cpp]
            public void MarkAction(int actionType, int stateHash, int[] layerHashes, float[] layerTimes, float[] layerWeights)
            {
                _pendingActionType = actionType;
                if (Animator == null) return;
                try
                {
                    int count = Math.Min(layerHashes != null ? layerHashes.Length : 0, Animator.layerCount);
                    if (_lastLayerHashes.Length < Animator.layerCount) Array.Resize(ref _lastLayerHashes, Animator.layerCount);
                    if (_lastPlayedLayerHashes.Length < Animator.layerCount) Array.Resize(ref _lastPlayedLayerHashes, Animator.layerCount);
                    for (int i = 0; i < count; i++)
                    {
                        int hash = layerHashes[i];
                        if (hash == 0) continue;
                        if (layerWeights != null && i < layerWeights.Length) Animator.SetLayerWeight(i, Mathf.Clamp01(layerWeights[i]));
                        if (_lastPlayedLayerHashes[i] == hash) continue;
                        _lastPlayedLayerHashes[i] = hash;
                        float nt = layerTimes != null && i < layerTimes.Length ? layerTimes[i] : 0f;
                        nt = nt - Mathf.Floor(nt);
                        try { Animator.Play(hash, i, nt); } catch { }
                    }
                    if (count == 0 && stateHash != 0 && (stateHash != _motionHash || actionType != _lastActionType))
                        Animator.Play(stateHash, 0, 0f);
                    _lastActionType = actionType;
                    _pendingActionType = int.MinValue;
                }
                catch { }
            }

            [HideFromIl2Cpp]
            public void MarkActionDetailed(int actionType, int stateHash, int actionId, int actionParam,
                int oldActionId, float anotherMotion, int[] layerHashes, float[] layerTimes, float[] layerWeights)
            {
                _tickActionType = actionType;
                _tickActionId = actionId;
                _tickActionParam = actionParam;
                _tickOldActionId = oldActionId;
                _tickAnotherMotion = anotherMotion;
                _tickStateHash = stateHash;
                if (Animator != null)
                {
                    try
                    {
                        int h = Animator.StringToHash("ActionId");
                        if (_validParams.ContainsKey(h)) Animator.SetInteger(h, actionId);
                        h = Animator.StringToHash("Action");
                        if (_validParams.ContainsKey(h)) Animator.SetInteger(h, actionParam);
                        h = Animator.StringToHash("OldActionId");
                        if (_validParams.ContainsKey(h)) Animator.SetInteger(h, oldActionId);
                        h = Animator.StringToHash("AnotherMotionIndex");
                        if (_validParams.ContainsKey(h)) Animator.SetFloat(h, anotherMotion);

                    }
                    catch { }
                }
                MarkAction(actionType, stateHash, layerHashes, layerTimes, layerWeights);
                if (Time.unscaledTime - _lastActionLogAt > 2f)
                {
                    _lastActionLogAt = Time.unscaledTime;
                    string rb0 = "";
                    try { var si0 = Animator.GetCurrentAnimatorStateInfo(0); rb0 = si0.fullPathHash + ":" + si0.normalizedTime.ToString("0.00"); } catch { }
                    string ms = "?";
                    try { ms = Animator.GetFloat(Animator.StringToHash("MoveSpeed")).ToString("0.00"); } catch { }
                    PluginInfo.Info("分身动作应用: " + PeerId + " act=" + actionType + " hash=" + stateHash + " layers=" + (layerHashes != null ? layerHashes.Length : 0) + " 回读L0=" + rb0 + " Move=" + ms);
                }
            }

            [HideFromIl2Cpp]
            public void SetMotion(Vector3 velocity, float rotY, bool moving, bool crouch, int actionType, int stateHash)
            {
                _remoteStrafe = false;
                _remoteDash = false;
                _remoteMoveSpeed = float.NaN;
                _remoteLocomotionSpeed = float.NaN;
                _remoteStrafeX = float.NaN;
                _remoteStrafeY = float.NaN;
                SetMotion(velocity, rotY, moving, crouch, actionType, stateHash, Vector3.zero, false);
            }

            [HideFromIl2Cpp]
            public void SetMotionDetailed(Vector3 velocity, float rotY, bool moving, bool crouch, bool strafe, bool dash,
                int actionType, int stateHash, float moveSpeed, float locomotionSpeed, float strafeX, float strafeY,
                Vector3 networkPosition, bool hasPosition)
            {
                _remoteStrafe = strafe;
                _remoteDash = dash;
                _remoteMoveSpeed = moveSpeed;
                _remoteLocomotionSpeed = locomotionSpeed;
                _remoteStrafeX = strafeX;
                _remoteStrafeY = strafeY;
                SetMotion(velocity, rotY, moving, crouch, actionType, stateHash, networkPosition, hasPosition);
            }

            [HideFromIl2Cpp]
            public void SetMotion(Vector3 velocity, float rotY, bool moving, bool crouch, int actionType, int stateHash,
                Vector3 networkPosition, bool hasPosition)
            {
                if (Root == null) return;
                try
                {
                    bool wasMoving = _motionMoving;
                    _motionVelocity = Vector3.ClampMagnitude(new Vector3(velocity.x, 0f, velocity.z), 9f);
                    _motionMoving = moving && _motionVelocity.sqrMagnitude > 0.0025f;
                    _lastMotionPacketTime = Time.unscaledTime;
                    _targetRotationY = rotY;
                    if (hasPosition) SetPose(networkPosition, rotY);
                    if (wasMoving && !_motionMoving)
                    {
                        _estimatedVelocity = Vector3.zero;
                        if (!hasPosition && _hasTargetPose)
                        {
                            _targetPosition = Root.transform.position;
                            _previousTargetPosition = _targetPosition;
                            _lastNetworkPoseTime = Time.unscaledTime;
                        }
                    }
                    if (Animator != null)
                    {
                        float fallbackSpeed = _motionMoving ? Mathf.Clamp(_motionVelocity.magnitude, 0.65f, 2.5f) : 0f;
                        try { if (Animator.speed <= 0f) Animator.speed = 1f; } catch { }
                        float speed = !float.IsNaN(_remoteMoveSpeed) ? _remoteMoveSpeed : fallbackSpeed;
                        if (_motionMoving && Mathf.Abs(speed) < 0.05f) speed = fallbackSpeed;
                        float locoSpeed = !float.IsNaN(_remoteLocomotionSpeed) && Mathf.Abs(_remoteLocomotionSpeed) > 0.01f
                            ? _remoteLocomotionSpeed : 1f;
                        Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, rotY, 0f)) * _motionVelocity;
                        float sx = !float.IsNaN(_remoteStrafeX) ? _remoteStrafeX : Mathf.Clamp(local.x, -1f, 1f);
                        float sy = !float.IsNaN(_remoteStrafeY) ? _remoteStrafeY : Mathf.Clamp(local.z, -1f, 1f);
                        int moveHash = Animator.StringToHash("MoveSpeed");
                        int locoHash = Animator.StringToHash("LocomotionMotionSpeed");
                        int strafeXHash = Animator.StringToHash("StrafeX");
                        int strafeYHash = Animator.StringToHash("StrafeY");
                        int strafeHash = Animator.StringToHash("IsStrafe");
                        int crouchHash = Animator.StringToHash("IsCrouch");
                        if (_validParams.ContainsKey(moveHash)) Animator.SetFloat(moveHash, speed);
                        if (_validParams.ContainsKey(locoHash)) Animator.SetFloat(locoHash, locoSpeed);
                        if (_validParams.ContainsKey(strafeXHash)) Animator.SetFloat(strafeXHash, sx);
                        if (_validParams.ContainsKey(strafeYHash)) Animator.SetFloat(strafeYHash, sy);
                        if (_validParams.ContainsKey(strafeHash)) Animator.SetBool(strafeHash, _remoteStrafe);
                        if (_validParams.ContainsKey(crouchHash)) Animator.SetBool(crouchHash, crouch);
                        _tickMoving = _motionMoving;
                        _tickMoveSpeed = speed;
                        _tickLocoSpeed = locoSpeed;
                        _tickStrafeX = sx;
                        _tickStrafeY = sy;
                        _tickStrafe = _remoteStrafe;
                        _tickCrouch = crouch;
                        _tickDash = _remoteDash;
                        _tickActionType = actionType;
                        _tickStateHash = stateHash;
                        // 走路/停止/Pose 即使 ActionType 不变，只要动画状态字段改变也立即在本地播放。
                        if (stateHash != 0 && (stateHash != _motionHash || actionType != _motionAction))
                        {
                            try { Animator.Play(stateHash, 0, 0f); } catch { }
                        }

                        if (Time.unscaledTime - _lastMotionLogAt > 3f)
                        {
                            _lastMotionLogAt = Time.unscaledTime;
                            string rb0 = "";
                            try { var si0 = Animator.GetCurrentAnimatorStateInfo(0); rb0 = si0.fullPathHash + ":" + si0.normalizedTime.ToString("0.00"); } catch { }
                            PluginInfo.Info("分身移动应用: " + PeerId + " moving=" + _motionMoving + " speed=" + speed.ToString("0.00") + " hash=" + stateHash + " 回读L0=" + rb0);
                        }
                    }
                    _motionAction = actionType;
                    _motionHash = stateHash;
                }
                catch { }
            }

            [HideFromIl2Cpp]
            public void Apply(RemoteState st, bool applyActives)
            {
                if (Root == null) return;
                RefreshRendererLists();
                SetPose(st.Pos, st.RotY);
                Root.transform.localScale = st.Scale;

                if (Animator != null)
                {
                    try
                    {
                        Animator.speed = st.AnimSpeed > 0f ? st.AnimSpeed : 1f;
                        int layers = Math.Min(st.LayerWeights.Length, Animator.layerCount);
                        for (int i = 0; i < layers; i++) Animator.SetLayerWeight(i, st.LayerWeights[i]);
                        if(_pendingActionType!=int.MinValue)st.ActionType=_pendingActionType;bool changed=st.ActionType!=int.MinValue&&st.ActionType!=_lastActionType;
                        if(_lastLayerHashes.Length<Animator.layerCount)Array.Resize(ref _lastLayerHashes,Animator.layerCount);
                        if(_lastPlayedLayerHashes.Length<Animator.layerCount)Array.Resize(ref _lastPlayedLayerHashes,Animator.layerCount);for(int i=0;i<st.LayerStateHashes.Length&&i<Animator.layerCount;i++){int hash=st.LayerStateHashes[i];if(hash!=0){if(_lastPlayedLayerHashes[i]==hash)continue;_lastPlayedLayerHashes[i]=hash;float nt=(st.LayerStateTimes!=null&&i<st.LayerStateTimes.Length&&!float.IsNaN(st.LayerStateTimes[i]))?st.LayerStateTimes[i]:0f;try{Animator.Play(hash,i,nt);}catch{}}}
                        if(st.LayerStateHashes.Length>0&&changed){_lastActionType=st.ActionType;_pendingActionType=int.MinValue;}
                        for (int i = 0; i < st.FloatNames.Length; i++)
                        {
                            if (_validParams.ContainsKey(Animator.StringToHash(st.FloatNames[i])))
                                Animator.SetFloat(Animator.StringToHash(st.FloatNames[i]), st.FloatVals[i]);
                        }
                        for (int i = 0; i < st.IntNames.Length; i++)
                        {
                            if (_validParams.ContainsKey(Animator.StringToHash(st.IntNames[i])))
                                Animator.SetInteger(Animator.StringToHash(st.IntNames[i]), st.IntVals[i]);
                        }
                        for (int i = 0; i < st.BoolNames.Length; i++)
                        {
                            if (_validParams.ContainsKey(Animator.StringToHash(st.BoolNames[i])))
                                Animator.SetBool(Animator.StringToHash(st.BoolNames[i]), st.BoolVals[i]);
                        }
                    }
                    catch { }
                }

                if (!applyActives)
                {
                    foreach (var r in _renderers)
                    {
                        if (r == null) continue;
                        try
                        {
                            var t = r.transform;
                            while (t != null && t != Root.transform)
                            {
                                t.gameObject.SetActive(true);
                                t = t.parent;
                            }
                            if (!_blockedRendererIds.Contains(r.GetInstanceID())) r.enabled = true;
                        }
                        catch { }
                    }
                    foreach (var s in _skinned)
                    {
                        if (s == null) continue;
                        try { s.updateWhenOffscreen = false; } catch { }
                    }
                    return;
                }

                if (st.BonePaths != null && st.BoneQuats != null &&
                    st.BonePaths.Length > 0 && st.BoneQuats.Length == st.BonePaths.Length * 4)
                {
                    _lastBonePaths = st.BonePaths;
                    _lastBoneQuats = st.BoneQuats;
                    _boneMatchedLast = ApplyBoneQuats(st.BonePaths, st.BoneQuats);
                    if (_boneMatchedLast < st.BonePaths.Length && !_boneMismatchLogged)
                    {
                        _boneMismatchLogged = true;
                        var missing = new System.Text.StringBuilder();
                        int shown = 0;
                        for (int i = 0; i < st.BonePaths.Length && shown < 5; i++)
                        {
                            if (!_boneMap.ContainsKey(st.BonePaths[i]))
                            {
                                missing.Append(st.BonePaths[i] + " | ");
                                shown++;
                            }
                        }
                        PluginInfo.Warn("骨骼匹配不足: " + _boneMatchedLast + "/" + st.BonePaths.Length +
                                        " 缺失示例: " + missing);
                    }
                }

                var pathBase = _modelRoot != null ? _modelRoot : Root.transform;
                int pathMatched = 0;
                var matchedIds = new HashSet<int>();
                int pathSig = 0;
                if (st.ActivePaths != null)
                {
                    pathSig = st.ActivePaths.Count;
                    foreach (var p in st.ActivePaths) pathSig = pathSig * 31 + p.GetHashCode();
                }
                bool pathsChanged = pathSig != _lastActivePathsSig;
                if (pathsChanged)
                {
                    _lastActivePathsSig = pathSig;
                    if (st.ActivePaths != null && st.ActivePaths.Count > 0)
                    {
                        foreach (var r in _renderers)
                        {
                            if (r == null) continue;
                            try
                            {
                                r.enabled = false;
                                r.gameObject.SetActive(false);
                            }
                            catch { }
                        }
                    }
                    {
                        foreach (var p in st.ActivePaths)
                        {
                            var t = FindPath(pathBase, p);
                            if (t == null) continue;
                            pathMatched++;
                            try { matchedIds.Add(t.gameObject.GetInstanceID()); } catch { }
                            var cur = t;
                            while (cur != null && cur != pathBase && cur != Root.transform)
                            {
                                cur.gameObject.SetActive(true);
                                cur = cur.parent;
                            }
                            if (t.gameObject != null) t.gameObject.SetActive(true);
                        }
                    }
                    foreach (var r in _renderers)
                    {
                        if (r == null) continue;
                        try
                        {
                            if (matchedIds.Contains(r.gameObject.GetInstanceID()) && !_blockedRendererIds.Contains(r.GetInstanceID())) r.enabled = true;
                        }
                        catch { }
                    }
                    bool appearanceMismatch = st.ActivePaths != null && st.ActivePaths.Count >= 4 && pathMatched < Mathf.Max(2, st.ActivePaths.Count / 3);
                    if (appearanceMismatch)
                    {
                        foreach (var r in _renderers) if (r != null) { try { r.enabled = false; } catch { } }
                        if (!HasMarker) CreateMarker();
                        else
                        {
                            var marker = Root.transform.Find("PositionMarker");
                            if (marker != null) { marker.gameObject.SetActive(true); var mr = marker.GetComponent<Renderer>(); if (mr != null) mr.enabled = true; }
                        }
                    }
                    else if (HasMarker)
                    {
                        var marker = Root.transform.Find("PositionMarker");
                        if (marker != null) { var mr = marker.GetComponent<Renderer>(); if (mr != null) mr.enabled = false; }
                    }
                }
                foreach (var s in _skinned)
                {
                    if (s == null) continue;
                    try { s.updateWhenOffscreen = false; } catch { }
                }

                if (!_diagnosed)
                {
                    _diagnosed = true;
                    PluginInfo.Info("分身渲染状态: " + PeerId + " 渲染器=" + RendererCount +
                                    " 激活=" + CountActiveRenderers() +
                                    " 有效网格=" + CountValidRenderers() +
                                    " 可见=" + CountVisibleRenderers() +
                                    " 路径匹配=" + pathMatched + "/" +
                                    (st.ActivePaths != null ? st.ActivePaths.Count : 0) +
                                    " 骨骼匹配=" + _boneMatchedLast + "/" +
                                    (st.BonePaths != null ? st.BonePaths.Length : 0) +
                                    " pos=" + Root.transform.position +
                                    " scale=" + Root.transform.localScale);
                }
            }

            [HideFromIl2Cpp]
            private int ApplyBoneQuats(string[] paths, float[] q)
            {
                int matched = 0;
                for (int i = 0; i < paths.Length; i++)
                {
                    if (!_boneMap.TryGetValue(paths[i], out var t)) continue;
                    matched++;
                    try
                    {
                        int o = i * 4;
                        t.localRotation = new Quaternion(q[o], q[o + 1], q[o + 2], q[o + 3]);
                    }
                    catch { }
                }
                return matched;
            }

            [HideFromIl2Cpp]
            public void LateApply()
            {
                if (Root == null) return;
                try
                {
                    if (_hasTargetPose)
                    {
                        float t = 1f - Mathf.Exp(-16f * Mathf.Max(0.001f, Time.unscaledDeltaTime));
                        float age = Mathf.Clamp(Time.unscaledTime - _lastNetworkPoseTime, 0f, 1.2f);
                        bool motionFresh = Time.unscaledTime - _lastMotionPacketTime < 0.65f;
                        // 收到明确停止字段时必须使用零速度，不能回退到旧关键帧估算速度。
                        Vector3 predictionVelocity = motionFresh
                            ? (_motionMoving ? _motionVelocity : Vector3.zero)
                            : _estimatedVelocity;
                        predictionVelocity.y = 0f;
                        Vector3 predicted = _targetPosition + predictionVelocity * age;
                        predicted += new Vector3(0f, _floorCorrectionY, 0f);
                        Root.transform.position = Vector3.Lerp(Root.transform.position, predicted, t);
                        float y = Mathf.LerpAngle(Root.transform.eulerAngles.y, _targetRotationY, t);
                        Root.transform.rotation = Quaternion.Euler(0f, y, 0f);
                    }
                }
                catch { }
            }

            [HideFromIl2Cpp]
            private Transform FindBone(params string[] names)
            {
                foreach (var kv in _boneMap)
                {
                    string path = kv.Key;
                    foreach (var name in names)
                        if (path == name || path.EndsWith("/" + name, StringComparison.OrdinalIgnoreCase)) return kv.Value;
                }
                return null;
            }

            [HideFromIl2Cpp]
            private void ApplySpecialNetworkPose()
            {
                if (!_remoteRiding && !_remoteRidden) return;
                float k = 1f - Mathf.Exp(-16f * Mathf.Max(0.001f, Time.unscaledDeltaTime));
                try
                {
                    var hips = FindBone("J_Bip_C_Hips", "C_Hips", "Hip");
                    var spine = FindBone("Spine", "C_Spine");
                    var legL = FindBone("LegL", "Bip_L_UpperLeg");
                    var legR = FindBone("LegR", "Bip_R_UpperLeg");
                    var calfL = FindBone("LowerLegL");
                    var calfR = FindBone("LowerLegR");
                    if (_remoteRidden)
                    {
                        if (hips != null) hips.localRotation = Quaternion.Slerp(hips.localRotation, hips.localRotation * Quaternion.Euler(72f, 0f, 0f), k);
                        if (spine != null) spine.localRotation = Quaternion.Slerp(spine.localRotation, spine.localRotation * Quaternion.Euler(-18f, 0f, 0f), k);
                    }
                    else if (_remoteRiding)
                    {
                        if (hips != null) hips.localRotation = Quaternion.Slerp(hips.localRotation, hips.localRotation * Quaternion.Euler(-12f, 0f, 0f), k);
                        if (spine != null) spine.localRotation = Quaternion.Slerp(spine.localRotation, spine.localRotation * Quaternion.Euler(18f, 0f, 0f), k);
                        if (legL != null) legL.localRotation = Quaternion.Slerp(legL.localRotation, legL.localRotation * Quaternion.Euler(48f, 0f, 0f), k);
                        if (legR != null) legR.localRotation = Quaternion.Slerp(legR.localRotation, legR.localRotation * Quaternion.Euler(48f, 0f, 0f), k);
                        if (calfL != null) calfL.localRotation = Quaternion.Slerp(calfL.localRotation, calfL.localRotation * Quaternion.Euler(-92f, 0f, 0f), k);
                        if (calfR != null) calfR.localRotation = Quaternion.Slerp(calfR.localRotation, calfR.localRotation * Quaternion.Euler(-92f, 0f, 0f), k);
                    }
                }
                catch { }
            }

            [HideFromIl2Cpp]
            public bool TryGetRideAnchor(out Vector3 anchor)
            {
                anchor = Root != null ? Root.transform.position + Vector3.down * 0.35f : Vector3.zero;
                try
                {
                    var hips = FindBone("J_Bip_C_Hips", "C_Hips", "Hip");
                    if (hips != null) { anchor = hips.position; return true; }
                    var spine = FindBone("Spine", "C_Spine");
                    if (spine != null) { anchor = spine.position + Vector3.down * 0.18f; return true; }
                }
                catch { }
                return false;
            }

            [HideFromIl2Cpp]
            public void SetPose(Vector3 pos, float rotY)
            {
                if (Root == null) return;
                try
                {
                    Vector3 target = pos - _avatarOffset;
                    if (_hasTargetPose) target.y = Mathf.Clamp(target.y, _previousTargetPosition.y - 2f, _previousTargetPosition.y + 2f);
                    if (!_hasTargetPose || Vector3.Distance(Root.transform.position, target) > 15f)
                    {
                        Root.transform.position = target;
                        Root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
                    }
                    float now = Time.unscaledTime;
                    if (_hasTargetPose)
                    {
                        float dt = now - _lastNetworkPoseTime;
                        if (dt > 0.015f && dt < 0.5f)
                        {
                            Vector3 v = (target - _previousTargetPosition) / dt;
                            v.y = 0f;
                            _estimatedVelocity = Vector3.ClampMagnitude(Vector3.Lerp(_estimatedVelocity, v, 0.35f), 9f);
                        }
                    }
                    else _estimatedVelocity = Vector3.zero;
                    _previousTargetPosition = target;
                    _targetPosition = target;
                    _lastNetworkPoseTime = now;
                    _targetRotationY = rotY;
                    _hasTargetPose = true;
                }
                catch { }
            }

            [HideFromIl2Cpp]
            public void ApplyCoreBones(float[] q)
            {
                if (_moveClip != null || _actClip != null) return;
                if (Root == null || _modelRoot == null || q == null) return;
                try
                {
                    if (_cloneBoneByName == null) _cloneBoneByName = new Dictionary<string, Transform>();
                    for (int i = 0; i < CoreBoneNames.Length && i * 4 + 3 < q.Length; i++)
                    {
                        string name = CoreBoneNames[i];
                        if (!_cloneBoneByName.TryGetValue(name, out var t) || t == null)
                        {
                            t = FindNamed(_modelRoot, name);
                            _cloneBoneByName[name] = t;
                        }
                        if (t == null) continue;
                        var r = new Quaternion(q[i * 4], q[i * 4 + 1], q[i * 4 + 2], q[i * 4 + 3]);
                        if (float.IsNaN(r.x) || float.IsNaN(r.y) || float.IsNaN(r.z) || float.IsNaN(r.w)) continue;
                        t.localRotation = r;
                    }
                }
                catch { }
            }

            [HideFromIl2Cpp]
            public void PlayLocomotionClip(MotionClip clip, float originY)
            {
                _moveClip = null; // 旧录制剪辑已停用，改用动画器连续驱动
            }

            [HideFromIl2Cpp]
            public void StopLocomotionClip()
            {
                _moveClip = null;
            }

            [HideFromIl2Cpp]
            public void PlayActionClip(MotionClip clip, float originY)
            {
                _actClip = null; // 旧录制剪辑已停用，改用动画器连续驱动
            }

            [HideFromIl2Cpp]
            public void StopActionClip()
            {
                _actClip = null;
            }

            [HideFromIl2Cpp]
            private void ApplyClipFrame(float dt)
            {
                if (_modelRoot == null) return;
                try
                {
                    if (_moveClip != null) AdvanceClip(_moveClip, ref _moveClipTime, dt, true);
                    if (_actClip != null) AdvanceClip(_actClip, ref _actClipTime, dt, _moveClip != null);
                }
                catch { }
            }

            [HideFromIl2Cpp]
            private void AdvanceClip(MotionClip clip, ref float clipTime, float dt, bool moveLayer)
            {
                if (clip == null || clip.Times == null || clip.Quats == null || clip.Times.Length == 0) return;
                try
                {
                    float lastT = clip.Times[clip.Times.Length - 1];
                    if (clipTime >= lastT)
                    {
                        if (clip.Mode == 0) clipTime -= lastT;
                        else if (clip.Mode == 1) clipTime = lastT;
                        else { if (moveLayer) _moveClip = null; else _actClip = null; return; }
                    }
                    int idx = 0;
                    while (idx < clip.Times.Length - 1 && clip.Times[idx + 1] <= clipTime) idx++;
                    float a = 0f;
                    if (idx < clip.Times.Length - 1)
                    {
                        float span = clip.Times[idx + 1] - clip.Times[idx];
                        if (span > 0.0001f) a = Mathf.Clamp01((clipTime - clip.Times[idx]) / span);
                    }
                    int stride = CoreBoneNames.Length * 4;
                    if (idx * stride + stride > clip.Quats.Length) return;
                    if (clip.Offs != null && clip.Offs.Length == clip.Times.Length)
                    {
                        float y0 = clip.Offs[idx];
                        float y1 = idx + 1 < clip.Offs.Length ? clip.Offs[idx + 1] : y0;
                        float yOff = y0 + (y1 - y0) * a;
                        float originY = moveLayer ? _moveOriginY : _actOriginY;
                        var rootPos = Root.transform.position;
                        rootPos.y = originY - yOff;
                        Root.transform.position = rootPos;
                    }
                    if (_cloneBoneByName == null) _cloneBoneByName = new Dictionary<string, Transform>();
                    for (int i = 0; i < CoreBoneNames.Length; i++)
                    {
                        // 移动层：只驱动盆骨+腿脚；动作层：移动时只驱动上半身，不移动时驱动全身。
                        if (moveLayer && i > 0 && i < 13) continue;
                        if (!moveLayer && _moveClip != null && (i == 0 || i >= 13)) continue;
                        string name = CoreBoneNames[i];
                        if (!_cloneBoneByName.TryGetValue(name, out var t) || t == null)
                        {
                            t = FindNamed(_modelRoot, name);
                            _cloneBoneByName[name] = t;
                        }
                        if (t == null) continue;
                        int base0 = idx * stride + i * 4;
                        int nextIdx = idx + 1 < clip.Times.Length ? idx + 1 : idx;
                        int base1 = nextIdx * stride + i * 4;
                        var q0 = new Quaternion(clip.Quats[base0], clip.Quats[base0 + 1], clip.Quats[base0 + 2], clip.Quats[base0 + 3]);
                        var q1 = new Quaternion(clip.Quats[base1], clip.Quats[base1 + 1], clip.Quats[base1 + 2], clip.Quats[base1 + 3]);
                        t.localRotation = Quaternion.Slerp(q0, q1, a);
                    }
                    clipTime += dt;
                }
                catch { }
            }

            [HideFromIl2Cpp]
            public void TickAnimator(float deltaTime)
            {
                ApplyClipFrame(deltaTime);
                TickFx();
                if (_hasHips)
                {
                    try
                    {
                        if (_hipsBone == null && _modelRoot != null)
                        {
                            _hipsBone = FindNamed(_modelRoot, "Hips");
                            if (_hipsBone == null) _hipsBone = FindNamed(_modelRoot, "J_Bip_C_Hips");
                        }
                        if (_hipsBone != null && !float.IsNaN(_targetHipsLocal.y) && _targetHipsLocal.y > -2f && _targetHipsLocal.y < 4f)
                        {
                            float k = Mathf.Min(1f, Time.unscaledDeltaTime * 10f);
                            _hipsBone.localPosition = Vector3.Lerp(_hipsBone.localPosition, _targetHipsLocal, k);
                            _hipsBone.localRotation = Quaternion.Slerp(_hipsBone.localRotation, _targetHipsLocalRot, k);
                        }
                    }
                    catch { }
                }
                if (Animator == null || !_lodVisible || Root == null) return;
                try
                {
                    if (Animator.speed <= 0f) Animator.speed = 1f;
                    int moveHash = Animator.StringToHash("MoveSpeed");
                    int locoHash = Animator.StringToHash("LocomotionMotionSpeed");
                    int strafeXHash = Animator.StringToHash("StrafeX");
                    int strafeYHash = Animator.StringToHash("StrafeY");
                    int strafeHash = Animator.StringToHash("IsStrafe");
                    int crouchHash = Animator.StringToHash("IsCrouch");
                    int dashHash = Animator.StringToHash("IsDash");
                    int actionIdHash = Animator.StringToHash("ActionId");
                    int actionHash = Animator.StringToHash("Action");
                    int oldActionHash = Animator.StringToHash("OldActionId");
                    int anotherMotionHash = Animator.StringToHash("AnotherMotionIndex");
                    if (_validParams.ContainsKey(moveHash)) Animator.SetFloat(moveHash, _tickMoveSpeed);
                    if (_validParams.ContainsKey(locoHash)) Animator.SetFloat(locoHash, _tickLocoSpeed);
                    if (_validParams.ContainsKey(strafeXHash)) Animator.SetFloat(strafeXHash, _tickStrafeX);
                    if (_validParams.ContainsKey(strafeYHash)) Animator.SetFloat(strafeYHash, _tickStrafeY);
                    if (_validParams.ContainsKey(strafeHash)) Animator.SetBool(strafeHash, _tickStrafe);
                    if (_validParams.ContainsKey(crouchHash)) Animator.SetBool(crouchHash, _tickCrouch);
                    if (_validParams.ContainsKey(dashHash)) Animator.SetBool(dashHash, _tickDash);
                    if (_validParams.ContainsKey(actionIdHash)) Animator.SetInteger(actionIdHash, _tickActionId);
                    if (_validParams.ContainsKey(actionHash)) Animator.SetInteger(actionHash, _tickActionParam);
                    if (_validParams.ContainsKey(oldActionHash)) Animator.SetInteger(oldActionHash, _tickOldActionId);
                    if (_validParams.ContainsKey(anotherMotionHash)) Animator.SetFloat(anotherMotionHash, _tickAnotherMotion);
                    if (_tickStateHash != 0 && _tickStateHash != _lastPlayedStateHash)
                    {
                        try { Animator.Play(_tickStateHash, 0, 0f); } catch { }
                        _lastPlayedStateHash = _tickStateHash;
                        if (_lastPlayedLayerHashes.Length < 1) _lastPlayedLayerHashes = new int[1];
                        _lastPlayedLayerHashes[0] = _tickStateHash;
                    }
                    // 每个克隆体按自己的骨骼本地推进动画，不再只靠引擎自动更新。
                    // 动画器保持引擎自动推进，不再手动二次更新（避免高配也掉帧）。
                    // 克隆动画器保持启用，由引擎每帧自动推进；这里持续喂动作字段，
                    // 让本地状态机持续运算走/跑动作并循环播放。
                    if (Animator.speed <= 0f) Animator.speed = 1f;
                }
                catch { }
            }

            [HideFromIl2Cpp]
            public void SetLodVisible(bool visible)
            {
                if (_lodVisible == visible || Root == null) return;
                _lodVisible = visible;
                RefreshRendererLists();
                foreach (var renderer in _renderers)
                {
                    if (renderer == null) continue;
                    try { renderer.enabled = visible; } catch { }
                }
                if (Animator != null)
                {
                    try { Animator.enabled = visible; } catch { }
                }
            }

            [HideFromIl2Cpp]
            public void SetHighlight(Color? c)
            {
                if (Root == null) return;
                if (c == _highlightColor) return;
                _highlightColor = c;
                RefreshRendererLists();
                foreach (var r in _renderers)
                {
                    if (r == null) continue;
                    try
                    {
                        if (c.HasValue)
                        {
                            if (!_origColors.ContainsKey(r)) _origColors[r] = r.material.color;
                            r.material.color = c.Value;
                        }
                        else if (_origColors.TryGetValue(r, out var orig))
                        {
                            r.material.color = orig;
                        }
                    }
                    catch { }
                }
            }

            private static void BuildCloneTransformMap(Transform srcRoot, Transform dstRoot, Dictionary<Transform, Transform> map)
            {
                if (srcRoot == null || dstRoot == null || map == null) return;
                var srcStack = new Stack<Transform>();
                var dstStack = new Stack<Transform>();
                srcStack.Push(srcRoot);
                dstStack.Push(dstRoot);
                while (srcStack.Count > 0)
                {
                    var s = srcStack.Pop();
                    var d = dstStack.Pop();
                    if (s == null || d == null) continue;
                    map[s] = d;
                    int n = Math.Min(s.childCount, d.childCount);
                    for (int i = n - 1; i >= 0; i--)
                    {
                        srcStack.Push(s.GetChild(i));
                        dstStack.Push(d.GetChild(i));
                    }
                }
            }

            private static string RelativeBonePath(Transform root, Transform target)
            {
                if (root == null || target == null) return "";
                var names = new List<string>();
                var cur = target;
                while (cur != null && cur != root)
                {
                    names.Add(cur.name);
                    cur = cur.parent;
                }
                if (cur == null) return "";
                names.Reverse();
                return string.Join("/", names);
            }

            private static Transform FindPath(Transform root, string path)
            {
                if (root == null || string.IsNullOrEmpty(path)) return null;
                var parts = path.Split('/');
                var cur = root;
                foreach (var p in parts)
                {
                    var next = FindChildSkipAdjusters(cur, p);
                    if (next == null) return null;
                    cur = next;
                }
                return cur;
            }

            private static Transform FindChildSkipAdjusters(Transform parent, string name)
            {
                if (parent == null) return null;
                for (int i = 0; i < parent.childCount; i++)
                {
                    var c = parent.GetChild(i);
                    if (c != null && c.name == name) return c;
                }
                for (int i = 0; i < parent.childCount; i++)
                {
                    var c = parent.GetChild(i);
                    if (c != null && c.name == "PlayerBoneScaleAdjuster")
                    {
                        var r = FindChildSkipAdjusters(c, name);
                        if (r != null) return r;
                    }
                }
                return null;
            }

            [HideFromIl2Cpp]
            private void RefreshRendererLists()
            {
                if (Root == null) return;
                if (Time.unscaledTime - _lastRenderersRefresh < 60f) return;
                _lastRenderersRefresh = Time.unscaledTime;
                try
                {
                    var freshRenderers = Root.GetComponentsInChildren<Renderer>(true);
                    var freshSkinned = Root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var freshMeshes = Root.GetComponentsInChildren<MeshRenderer>(true);

                    if (freshRenderers.Length == 0 && !HasMarker)
                    {
                        if (!_warnedRenderersLost)
                        {
                            _warnedRenderersLost = true;
                            string pos = "-";
                            try { pos = Root.transform.position.ToString(); } catch { }
                            PluginInfo.Warn("分身渲染器已全部消失: " + PeerId +
                                            " Root存活=" + (Root != null) +
                                            " Root位置=" + pos);
                        }
                        return;
                    }
                    _warnedRenderersLost = false;

                    _renderers.Clear();
                    _skinned.Clear();
                    _meshRenderers.Clear();
                    foreach (var r in freshRenderers)
                    {
                        if (r == null) continue;
                        int id = r.GetInstanceID();
                        if (_blockedRendererIds.Contains(id))
                        {
                            if (!IsSuspiciousAddonRenderer(r))
                            {
                                _blockedRendererIds.Remove(id);
                                _renderers.Add(r);
                            }
                            else
                            {
                                try { r.enabled = false; } catch { }
                            }
                            continue;
                        }
                        if (IsSuspiciousAddonRenderer(r))
                        {
                            _blockedRendererIds.Add(id);
                            try { r.enabled = false; } catch { }
                            continue;
                        }
                        _renderers.Add(r);
                    }
                    foreach (var s in freshSkinned)
                    {
                        if (s == null) continue;
                        _skinned.Add(s);
                        try { s.updateWhenOffscreen = false; } catch { }
                    }
                    foreach (var m in freshMeshes)
                    {
                        if (m != null) _meshRenderers.Add(m);
                    }
                    RendererCount = freshRenderers.Length;
                }
                catch (Exception ex)
                {
                    PluginInfo.Warn("刷新分身渲染器失败: " + ex.Message);
                }
            }

            private static void StripComponents(GameObject root)
            {
                if (root == null) return;
                try
                {
                    var comps = root.GetComponentsInChildren<Component>(true);
                    foreach (var c in comps)
                    {
                        if (c == null) continue;
                        string nn = NativeTypeName(c);
                        if (string.IsNullOrEmpty(nn) || KeepNativeTypes.Contains(nn)) continue;
                        try { UnityEngine.Object.Destroy(c); } catch { }
                    }
                }
                catch { }
            }

            private static string NativeTypeName(Component c)
            {
                try
                {
                    var objPtr = Il2CppInterop.Runtime.IL2CPP.Il2CppObjectBaseToPtr(c);
                    var klass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(objPtr);
                    var namePtr = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(klass);
                    if (namePtr == IntPtr.Zero) return "";
                    return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(namePtr) ?? "";
                }
                catch { return ""; }
            }

            [HideFromIl2Cpp]
            public void ApplyHipsFull(Vector3 pos, Quaternion rot, bool hasValue)
            {
                _targetHipsLocal = pos;
                _targetHipsLocalRot = rot;
                _hasHips = hasValue;
                if (!hasValue) { _hipsBone = null; }
            }

            [HideFromIl2Cpp]
            public void SetFloorY(float v)
            {
                FloorY = float.IsNaN(v) || float.IsInfinity(v) ? float.NaN : v;
            }

            [HideFromIl2Cpp]
            public float LowestPointY()
            {
                float low = float.MaxValue;
                foreach (var r in _renderers)
                {
                    if (r == null) continue;
                    try { if (r.enabled && r.bounds.min.y < low) low = r.bounds.min.y; } catch { }
                }
                return low == float.MaxValue ? (Root != null ? Root.transform.position.y : 0f) : low;
            }

            [HideFromIl2Cpp]
            public void TickFloor()
            {
                if (Root == null || float.IsNaN(FloorY)) return;
                try
                {
                    float low = LowestPointY();
                    if (low > 9000f) return;
                    float err = FloorY - low;
                    if (err < -0.05f)
                    {
                        // 模型浮空：往下压（限速，防止一次压过头）
                        float step = Mathf.Clamp(err, -0.6f, -0.02f) * Mathf.Min(1f, Time.unscaledDeltaTime * 8f);
                        _floorCorrectionY += step;
                    }
                    else if (err > 0.25f)
                    {
                        // 明显陷地：轻微抬起（限速）
                        float step = Mathf.Clamp(err, 0.02f, 0.15f) * Mathf.Min(1f, Time.unscaledDeltaTime * 6f);
                        _floorCorrectionY += step;
                    }
                    else
                    {
                        _floorCorrectionY *= 0.9f;
                        if (Mathf.Abs(_floorCorrectionY) < 0.004f) _floorCorrectionY = 0f;
                    }
                }
                catch { }
            }

            [HideFromIl2Cpp]
            public void PlayFx(string kind, int mode)
            {
                if (Root == null) return;
                bool pee = kind == "pee";
                bool loop = mode >= 2;
                ParticleSystem ps = pee ? _peeFx : _shioFx;
                if (ps == null)
                {
                    ps = FindFxParticle(pee);
                    if (ps == null) ps = CreateFxFromLocalTemplate(pee);
                    if (pee) _peeFx = ps; else _shioFx = ps;
                }
                if (ps == null) return;
                try { ps.gameObject.SetActive(true); } catch { }
                try { ps.enableEmission = true; } catch { }
                try { ps.Play(); } catch { }
                if (pee) _fxStopAtPee = loop ? -1f : Time.unscaledTime + 4f;
                else _fxStopAtShio = loop ? -1f : Time.unscaledTime + 4f;
            }

            [HideFromIl2Cpp]
            public void TickFx()
            {
                float now = Time.unscaledTime;
                if (_peeFx != null && _fxStopAtPee > 0f && now > _fxStopAtPee)
                {
                    try { _peeFx.enableEmission = false; } catch { }
                    try { _peeFx.Stop(); } catch { }
                    _fxStopAtPee = -1f;
                }
                if (_shioFx != null && _fxStopAtShio > 0f && now > _fxStopAtShio)
                {
                    try { _shioFx.enableEmission = false; } catch { }
                    try { _shioFx.Stop(); } catch { }
                    _fxStopAtShio = -1f;
                }
            }

            private Transform FindFxAnchor()
            {
                if (Root == null) return null;
                try
                {
                    if (_modelRoot != null)
                    {
                        var t = FindNamed(_modelRoot, "Pelvis");
                        if (t == null) t = FindNamed(_modelRoot, "Hips");
                        if (t == null) t = FindNamed(_modelRoot, "J_Bip_C_Hips");
                        if (t != null) return t;
                    }
                }
                catch { }
                return Root.transform;
            }

            private ParticleSystem CreateFxFromLocalTemplate(bool pee)
            {
                try
                {
                    var pf = PlayerFacade.Instance;
                    if (pf == null || pf.pca == null) return null;
                    var r = pf.pca.PlayerAvatarObjectReferencer;
                    if (r == null) return null;
                    ParticleSystem template = pee ? r.PeeParticle : r.ShiofukiParticle;
                    if (template == null) return null;
                    Transform anchorT = FindFxAnchor();
                    if (anchorT == null) return null;
                    var go = UnityEngine.Object.Instantiate(template.gameObject, anchorT, false);
                    go.name = pee ? "SFM_Fx_Pee" : "SFM_Fx_Shio";
                    var ps = go.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        try { ps.enableEmission = false; } catch { }
                        try { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); } catch { }
                    }
                    return ps;
                }
                catch { return null; }
            }

            private ParticleSystem FindFxParticle(bool pee)
            {
                if (Root == null) return null;
                try
                {
                    var all = Root.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var ps in all)
                    {
                        if (ps == null) continue;
                        string n = "";
                        try { n = ps.gameObject != null ? ps.gameObject.name : ""; } catch { }
                        string low = (n + " " + (ps.name ?? "")).ToLowerInvariant();
                        if (pee && (low.IndexOf("pee") >= 0 || low.IndexOf("urin") >= 0)) return ps;
                        if (!pee && (low.IndexOf("shiofuki") >= 0 || low.IndexOf("shio") >= 0 || low.IndexOf("squir") >= 0 ||
                                     low.IndexOf("climax") >= 0 || low.IndexOf("ecstasy") >= 0)) return ps;
                    }
                    foreach (var ps in all)
                    {
                        if (ps == null) continue;
                        string n = "";
                        try { n = ps.gameObject != null ? ps.gameObject.name : ""; } catch { }
                        string low = (n + " " + (ps.name ?? "")).ToLowerInvariant();
                        if (!pee && (low.IndexOf("splash") >= 0 || low.IndexOf("mizu") >= 0 || low.IndexOf("water") >= 0)) return ps;
                    }
                    if (pee)
                    {
                        foreach (var ps2 in all)
                        {
                            if (ps2 == null) continue;
                            string n2 = "";
                            try { n2 = ps2.gameObject != null ? ps2.gameObject.name : ""; } catch { }
                            string low2 = (n2 + " " + (ps2.name ?? "")).ToLowerInvariant();
                            if (low2.IndexOf("shio") >= 0 || low2.IndexOf("splash") >= 0 || low2.IndexOf("squir") >= 0)
                                return ps2;
                        }
                    }
                }
                catch { }
                return null;
            }
            [HideFromIl2Cpp]
            public void Destroy()
            {
                if (Root != null) UnityEngine.Object.Destroy(Root);
                Root = null;
                Animator = null;
                _nativeAnimation = null;
                _nativePlayerState = null;
                if (_peeFx != null) { try { UnityEngine.Object.Destroy(_peeFx.gameObject); } catch { } _peeFx = null; }
                if (_shioFx != null) { try { UnityEngine.Object.Destroy(_shioFx.gameObject); } catch { } _shioFx = null; }
                _fxStopAtPee = -1f;
                _fxStopAtShio = -1f;
            }
        }
    }
    internal sealed class OnlineBehaviour : MonoBehaviour
    {
        private OnlineCore _core;
        private float _nextErrorLog;
        private float _initAt;
        private bool _initAttempted;

        private void Awake()
        {
            float delay = 1.5f;
            try
            {
                int memoryMb = SystemInfo.systemMemorySize;
                if (memoryMb > 0 && memoryMb < 8192) delay = 3.5f;
            }
            catch { }
            _initAt = Time.realtimeSinceStartup + delay;
        }

        private void Update()
        {
            if (_core == null)
            {
                if (_initAttempted || Time.realtimeSinceStartup < _initAt) return;
                _initAttempted = true;
                try
                {
                    _core = new OnlineCore();
                    _core.Awake();
                }
                catch (Exception ex)
                {
                    PluginInfo.Error("联机核心启动失败：" + ex);
                    _core = null;
                    return;
                }
            }
            try { _core.Update(); }
            catch (Exception ex)
            {
                if (Time.unscaledTime >= _nextErrorLog)
                {
                    _nextErrorLog = Time.unscaledTime + 10f;
                    PluginInfo.Warn("联机核心 Update 异常：" + ex.Message);
                }
            }
        }

        private void LateUpdate()
        {
            if (_core == null) return;
            try { _core.LateUpdate(); } catch { }
        }

        private void OnGUI()
        {
            if (_core == null) return;
            try { _core.OnGUI(); }
            catch (Exception ex)
            {
                if (Time.unscaledTime >= _nextErrorLog)
                {
                    _nextErrorLog = Time.unscaledTime + 10f;
                    PluginInfo.Warn("联机菜单绘制异常：" + ex.Message);
                }
            }
        }

        private void OnDestroy()
        {
            if (_core == null) return;
            try { _core.OnDestroy(); } catch { }
            _core = null;
        }
    }

}
