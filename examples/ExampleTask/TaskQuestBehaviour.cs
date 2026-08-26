// TaskQuestBehaviour.cs —— 剧情任务示例（任务链 + NPC + 区域 + 积分）
// 玩法：接任务 → 走到检查点1 → NPC 对话 → 走到检查点2 → 完成得 RP
// 进度全员同步（synced 任务）

using UnityEngine;
using SFMOnline.Ext;

namespace ExampleTask
{
    public class TaskQuestBehaviour : MonoBehaviour
    {
        private SfmExtTask _story;

        void Start()
        {
            // 创建任务（synced: true = 进度全员可见）
            _story = SfmExtTaskManager.Create("story", "神秘委托", "帮看门人找回钥匙", rpReward: 100, synced: true);

            // 检查点1：走廊尽头
            SfmExtTaskManager.AddCheckpoint(_story, "hall", new Vector3(5, 0, 5), 3f);
            // 检查点2：花园
            SfmExtTaskManager.AddCheckpoint(_story, "garden", new Vector3(20, 0, 8), 3f);

            // 事件回调
            _story.OnCheckpointChanged = (t, idx) =>
            {
                SfmExtHud.Toast("进度：" + (idx + 1) + "/" + t.Checkpoints.Count, 3f);
                if (idx == 0) SfmExtNpcManager.SpawnSynced("guard", new Vector3(5, 0, 5), "你去花园看看吧！");
            };
            _story.OnComplete = t =>
            {
                SfmExtGameplay.Announce("委托完成！获得 100 RP");
                SfmExt.CallFunction("remote.fx", new SfmExtParams().Set("uid", "*").Set("kind", "shiofuki").Set("mode", 1));
                SfmExtNpcManager.RemoveSynced("guard");
            };

            // NPC 交互触发下一段
            SfmExtEvent.On("npc_talk", v =>
            {
                SfmExtScore.Add("RP", 50, broadcast: true);
                SfmExtHud.Toast("NPC 给了你 50 RP！去花园吧");
                if (SfmExtRoom.Host) SfmExtTaskManager.SetCheckpoint(_story, 1);
            });

            // HUD：开始按钮
            var w = SfmExtHud.CreateWindow("quest", "任务", new Rect(150, 200, 220, 140));
            w.AddButton("接取任务", () => { SfmExtTaskManager.Start(_story); SfmExtHud.ShowControl("quest", false); });
            w.AddButton("关闭", () => SfmExtHud.ShowControl("quest", false));
        }

        void Update()
        {
            // 靠近 NPC 时（距离 < 2）触发对话
            var npc = SfmExtNpcManager.Get("guard");
            if (npc != null && npc.Root != null)
            {
                float d = Vector3.Distance(SfmExtBridge.GetLocalPosition?.Invoke() ?? Vector3.zero, npc.Root.position);
                if (d < 2f && Input.GetKeyDown(KeyCode.F))
                {
                    SfmExtEvent.Emit("npc_talk", new SfmExtValue("guard"));
                }
            }
        }
    }
}
