using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using SFMOnline.Ext;

namespace ExampleQuiz
{
    // ====================================================================
    //  答题游戏模组（配合服务器插件 quiz.py）
    //  玩法：房主按 F8 从题库出题 → 全员答题（聊天框 !answer 或 HUD 按钮）
    //  → 服务器插件判分 → 广播结果 → 全员显示得分。
    // ====================================================================
    [BepInPlugin("com.example.quiz", "示例-答题游戏", "1.0.0")]
    public class Plugin : BasePlugin
    {
        public override void Load()
        {
            SfmExt.InitNow();
            AddComponent<QuizBehaviour>();
        }
    }

    public class QuizBehaviour : MonoBehaviour
    {
        private static readonly string[] Questions =
        {
            "1+1=?|2",
            "天空是什么颜色？|蓝色",
            "SFM 的引擎？|unity",
            "一年有多少个月？|12",
        };

        private int _currentIndex = -1;
        private bool _started;

        void Start()
        {
            // 房主出题
            SfmExtEvent.On("quiz_show", v =>
            {
                if (!SfmExtRoom.Host) return;
                NextQuestion();
            });

            // 服务器发来的题目（服务器从 !quiz 命令出题时走这条）
            SfmExtEvent.On("quiz_question", v =>
            {
                ShowQuestion(v.ToString());
            });

            // 服务器广播答题结果
            SfmExtEvent.On("quiz_win", v =>
            {
                SfmExtHud.Toast("有人答对了：" + v, 4f);
                SfmExt.CallFunction("remote.fx", new SfmExtParams().Set("uid", "*").Set("kind", "shiofuki").Set("mode", 1));
            });
            SfmExtEvent.On("quiz_wrong", v => SfmExtHud.Toast(v.ToString()));

            // 出题按钮（房主）
            var win = SfmExtHud.CreateWindow("quiz", "答题游戏", new Rect(150, 100, 220, 120));
            win.AddButton("出下一题", () => SfmExtEvent.Emit("quiz_show"));
            win.AddButton("关闭", () => SfmExtHud.ShowControl("quiz", false));
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8)) SfmExtEvent.Emit("quiz_show");
        }

        private void NextQuestion()
        {
            _currentIndex = (_currentIndex + 1) % Questions.Length;
            string q = Questions[_currentIndex];
            ShowQuestion(q.Split('|')[0]);
            // 正确答案由服务器裁决，这里不广播答案
        }

        private void ShowQuestion(string q)
        {
            SfmExtHud.CreateText("quiz_q", "题目：" + q, new Vector2(0.5f, 0.2f), new Color(1f, 0.9f, 0.4f), 22);
            SfmExtHud.Toast("聊天框输入 !answer 你的答案", 5f);
        }
    }
}
