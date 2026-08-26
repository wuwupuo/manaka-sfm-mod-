// HideSeekBehaviour.cs —— 捉迷藏玩法（房主权威模式，无需服务器插件）
// 玩法：房主按 F8 开局（全员传送到出生点、全员蹲下、倒计时30秒）
//     抓人：区域触发 → 房主广播"被抓" → 全员看到 → 被惩罚（高潮特效）
//     结束：倒计时结束或抓完所有人

using UnityEngine;
using SFMOnline.Ext;

namespace ExampleHideSeek
{
    public class HideSeekBehaviour : MonoBehaviour
    {
        private bool _running;
        private int _caught;

        void Start()
        {
            SfmExtEvent.On("hs_start", v => { if (SfmExtRoom.Host) StartGame(); });
            SfmExtEvent.On("hs_caught", v => OnCaught(v.ToString()));
            SfmExtEvent.On("hs_end", v => EndGame(v.ToString()));

            var w = SfmExtHud.CreateWindow("hs", "捉迷藏", new Rect(150, 150, 220, 150));
            w.AddButton("开始游戏(房主)", () => SfmExtEvent.Emit("hs_start"));
            w.AddButton("关闭", () => SfmExtHud.ShowControl("hs", false));
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8)) SfmExtEvent.Emit("hs_start");
            if (_running && SfmExtRoom.Host)
            {
                // 房主每 0.5 秒检测：所有没被抓的玩家是否靠近抓人者
                SfmExtTimer.Delay(0.5f, CheckCatch);
            }
        }

        void StartGame()
        {
            _running = true;
            _caught = 0;
            SfmExtGameplay.Announce("捉迷藏开始！躲好！");
            // 全员传送+蹲下
            SfmExt.CallFunction("remote.teleport", new SfmExtParams().Set("uid", "*").Set("x", 0).Set("y", 0).Set("z", 0));
            SfmExt.CallFunction("remote.crouch", new SfmExtParams().Set("uid", "*").Set("on", true));
            // 30 秒倒计时
            SfmExtGameplay.StartCountdown("hs_time", 30f, name => SfmExtEvent.EmitNet("hs_end", "时间到"));
        }

        void CheckCatch()
        {
            if (!_running) return;
            // 抓人者是房主自己：检查每个玩家与自己的距离
            var uids = SfmExtBridge.GetGhostUids?.Invoke();
            if (uids == null) return;
            foreach (var uid in uids)
            {
                float dist = SfmExtBridge.GetGhostPosition?.Invoke(uid).magnitude ?? 999;
                if (dist < 2f) // 2 米内算抓到
                {
                    SfmExtEvent.EmitNet("hs_caught", new SfmExtValue(uid));
                    return;
                }
            }
        }

        void OnCaught(string uid)
        {
            _caught++;
            SfmExtGameplay.Announce("抓到 " + uid + "！(" + _caught + " 人被抓)");
            // 惩罚：高潮特效 + 传送回起点
            SfmExt.CallFunction("remote.orgasm", new SfmExtParams().Set("uid", uid).Set("mode", 1));
            SfmExt.CallFunction("remote.teleport", new SfmExtParams().Set("uid", uid).Set("x", 0).Set("y", 0).Set("z", 0));
            if (_caught >= (SfmExtBridge.GetGhostUids?.Invoke()?.Count ?? 0))
                SfmExtEvent.EmitNet("hs_end", "全抓到了");
        }

        void EndGame(string reason)
        {
            _running = false;
            SfmExtGameplay.Announce("游戏结束：" + reason + "！抓到 " + _caught + " 人");
            SfmExt.CallFunction("remote.stand", new SfmExtParams().Set("uid", "*"));
            SfmExt.CallFunction("remote.reset", new SfmExtParams().Set("uid", "*"));
        }
    }
}
