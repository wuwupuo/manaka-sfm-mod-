using System;
using UnityEngine;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  远程玩法控制库（联机玩法核心）：
    //  通过 ext_play 通道向指定玩家/全员发送玩法命令。
    //  uid 参数约定：
    //    "" 或 "self"  = 本地玩家执行
    //    "uid值"        = 定向发送给该玩家执行
    //    "*"            = 广播到全房间
    //  所有命令底层复用 OnlineCore.ApplyToyState 命令集。
    // ====================================================================
    public static class SfmExtRemote
    {
        private static OnlineCore Core() => OnlineCore.Instance;

        // ---------- 内部调用 ----------
        private static void Do(string d, string uid, int act = 0, int stage = 0, int mode = 0, bool on = false)
        {
            try { var c = Core(); if (c != null) c.ExtPlay(uid, d, act, stage, mode, on); } catch { }
        }

        // ---------- 动作 ----------
        /// <summary>让玩家执行指定动作（动作ID）。</summary>
        [SfmExtFunction("remote.action")]
        public static SfmExtValue FnAction(SfmExtParams p, SfmExtValue u)
        { Do("action", p.Get("uid").ToString(), (int)p.Get("act").ToFloat()); return SfmExtValue.Null; }

        /// <summary>让玩家切换动作（动作ID，替代当前动作）。</summary>
        [SfmExtFunction("remote.action_set")]
        public static SfmExtValue FnActionSet(SfmExtParams p, SfmExtValue u)
        { Do("action_set", p.Get("uid").ToString(), (int)p.Get("act").ToFloat()); return SfmExtValue.Null; }

        // ---------- 振动 / 活塞 ----------
        /// <summary>设置玩家振动档位（0=关，1低 2高 3随机）。</summary>
        [SfmExtFunction("remote.vibrate")]
        public static SfmExtValue FnVibrate(SfmExtParams p, SfmExtValue u)
        { Do("vibrate", p.Get("uid").ToString(), stage: (int)p.Get("stage").ToFloat()); return SfmExtValue.Null; }

        /// <summary>执行一次抽插。</summary>
        [SfmExtFunction("remote.thrust")]
        public static SfmExtValue FnThrust(SfmExtParams p, SfmExtValue u)
        { Do("thrust", p.Get("uid").ToString()); return SfmExtValue.Null; }

        /// <summary>设置活塞档位（0=关 1-3）。</summary>
        [SfmExtFunction("remote.thrust_set")]
        public static SfmExtValue FnThrustSet(SfmExtParams p, SfmExtValue u)
        { Do("thrust_set", p.Get("uid").ToString(), stage: (int)p.Get("stage").ToFloat()); return SfmExtValue.Null; }

        // ---------- 道具 / 服装 ----------
        /// <summary>给玩家戴上道具（type: 道具类型ID，见道具表）。</summary>
        [SfmExtFunction("remote.goods")]
        public static SfmExtValue FnGoods(SfmExtParams p, SfmExtValue u)
        { Do("goods", p.Get("uid").ToString(), (int)p.Get("type").ToFloat()); return SfmExtValue.Null; }

        /// <summary>给玩家摘下道具。</summary>
        [SfmExtFunction("remote.goods_off")]
        public static SfmExtValue FnGoodsOff(SfmExtParams p, SfmExtValue u)
        { Do("goods_off", p.Get("uid").ToString(), (int)p.Get("type").ToFloat()); return SfmExtValue.Null; }

        /// <summary>脱衣（stage: 0=脱1件 1=全脱）。</summary>
        [SfmExtFunction("remote.undress")]
        public static SfmExtValue FnUndress(SfmExtParams p, SfmExtValue u)
        { Do("undress", p.Get("uid").ToString(), stage: (int)p.Get("stage").ToFloat()); return SfmExtValue.Null; }

        /// <summary>循环脱衣。</summary>
        [SfmExtFunction("remote.undress_cycle")]
        public static SfmExtValue FnUndressCycle(SfmExtParams p, SfmExtValue u)
        { Do("undress_cycle", p.Get("uid").ToString()); return SfmExtValue.Null; }

        /// <summary>穿回全部衣服。</summary>
        [SfmExtFunction("remote.dress")]
        public static SfmExtValue FnDress(SfmExtParams p, SfmExtValue u)
        { Do("undress_reset", p.Get("uid").ToString()); return SfmExtValue.Null; }

        // ---------- 身体状态 ----------
        /// <summary>强制高潮（mode: 1=一次 2=持续）。</summary>
        [SfmExtFunction("remote.orgasm")]
        public static SfmExtValue FnOrgasm(SfmExtParams p, SfmExtValue u)
        { Do("ecstasy", p.Get("uid").ToString(), mode: (int)p.Get("mode").ToFloat()); return SfmExtValue.Null; }

        /// <summary>强制排尿（mode: 1=一次 2=持续）。</summary>
        [SfmExtFunction("remote.pee")]
        public static SfmExtValue FnPee(SfmExtParams p, SfmExtValue u)
        { Do("pee", p.Get("uid").ToString(), mode: (int)p.Get("mode").ToFloat()); return SfmExtValue.Null; }

        /// <summary>停止排尿。</summary>
        [SfmExtFunction("remote.pee_stop")]
        public static SfmExtValue FnPeeStop(SfmExtParams p, SfmExtValue u)
        { Do("pee_stop", p.Get("uid").ToString()); return SfmExtValue.Null; }

        /// <summary>高潮状态切换（on: true=进入 高潮中）。</summary>
        [SfmExtFunction("remote.climax")]
        public static SfmExtValue FnClimax(SfmExtParams p, SfmExtValue u)
        { Do("climax", p.Get("uid").ToString(), on: p.Get("on").ToBool()); return SfmExtValue.Null; }

        /// <summary>给玩家快感。</summary>
        [SfmExtFunction("remote.pleasure")]
        public static SfmExtValue FnPleasure(SfmExtParams p, SfmExtValue u)
        { Do("pleasure", p.Get("uid").ToString()); return SfmExtValue.Null; }

        // ---------- 姿态 ----------
        /// <summary>蹲下/站立（on）。</summary>
        [SfmExtFunction("remote.crouch")]
        public static SfmExtValue FnCrouch(SfmExtParams p, SfmExtValue u)
        { Do("crouch", p.Get("uid").ToString(), on: p.Get("on").ToBool()); return SfmExtValue.Null; }

        /// <summary>爬行。</summary>
        [SfmExtFunction("remote.crawl")]
        public static SfmExtValue FnCrawl(SfmExtParams p, SfmExtValue u)
        { Do("crawl", p.Get("uid").ToString()); return SfmExtValue.Null; }

        /// <summary>坐下/站起切换。</summary>
        [SfmExtFunction("remote.sit")]
        public static SfmExtValue FnSit(SfmExtParams p, SfmExtValue u)
        { Do("sit_toggle", p.Get("uid").ToString()); return SfmExtValue.Null; }

        /// <summary>站立。</summary>
        [SfmExtFunction("remote.stand")]
        public static SfmExtValue FnStand(SfmExtParams p, SfmExtValue u)
        { Do("stand", p.Get("uid").ToString()); return SfmExtValue.Null; }

        // ---------- 束缚 ----------
        /// <summary>锁手铐（mode: 手铐类型 1=前 2=后 3=计时）。</summary>
        [SfmExtFunction("remote.handcuff")]
        public static SfmExtValue FnHandcuff(SfmExtParams p, SfmExtValue u)
        { Do("handcuff", p.Get("uid").ToString(), mode: (int)p.Get("mode").ToFloat(), stage: (int)p.Get("duration").ToFloat()); return SfmExtValue.Null; }

        /// <summary>背后手铐。</summary>
        [SfmExtFunction("remote.handcuff_back")]
        public static SfmExtValue FnHandcuffBack(SfmExtParams p, SfmExtValue u)
        { Do("handcuff_back", p.Get("uid").ToString(), mode: (int)p.Get("mode").ToFloat(), stage: (int)p.Get("duration").ToFloat()); return SfmExtValue.Null; }

        /// <summary>解锁手铐。</summary>
        [SfmExtFunction("remote.unlock")]
        public static SfmExtValue FnUnlock(SfmExtParams p, SfmExtValue u)
        { Do("unlock", p.Get("uid").ToString()); return SfmExtValue.Null; }

        /// <summary>戴项圈/取下。</summary>
        [SfmExtFunction("remote.collar")]
        public static SfmExtValue FnCollar(SfmExtParams p, SfmExtValue u)
        { Do(p.Get("on").ToBool() ? "collar" : "uncollar", p.Get("uid").ToString()); return SfmExtValue.Null; }

        /// <summary>蒙眼（on）。</summary>
        [SfmExtFunction("remote.bareta")]
        public static SfmExtValue FnBareta(SfmExtParams p, SfmExtValue u)
        { Do("bareta", p.Get("uid").ToString(), on: p.Get("on").ToBool()); return SfmExtValue.Null; }

        /// <summary>重置玩家状态（卸下所有道具/停止动作/站立）。</summary>
        [SfmExtFunction("remote.reset")]
        public static SfmExtValue FnReset(SfmExtParams p, SfmExtValue u)
        { Do("reset_all", p.Get("uid").ToString()); return SfmExtValue.Null; }

        // ---------- 特效 ----------
        /// <summary>播放特效（kind: "shiofuki"潮吹 "pee"排尿，mode: 1=一次 2=持续）。</summary>
        [SfmExtFunction("remote.fx")]
        public static SfmExtValue FnFx(SfmExtParams p, SfmExtValue u)
        { Do("fx", p.Get("uid").ToString(), mode: (int)p.Get("mode").ToFloat(), on: p.Get("kind").ToString() == "pee"); return SfmExtValue.Null; }

        // ---------- 位置 ----------
        /// <summary>传送玩家到坐标（uid: ""/self=自己 "*"=全员 其它=指定玩家）。</summary>
        [SfmExtFunction("remote.teleport")]
        public static SfmExtValue FnTeleport(SfmExtParams p, SfmExtValue u)
        {
            var uid = p.Get("uid").ToString();
            try
            {
                var c = Core();
                if (c == null) return SfmExtValue.Null;
                var x = (float)p.Get("x").ToFloat(); var y = (float)p.Get("y").ToFloat(); var z = (float)p.Get("z").ToFloat();
                if (uid.Length == 0 || uid == "self" || uid == c.PeerId || uid == c.ToySelfIdPublic())
                    c.SetPlayerPosition(new UnityEngine.Vector3(x, y, z));
                else if (uid == "*")
                    c.ExtTeleportBroadcast(x, y, z);
                else
                    c.ExtTeleportRemote(uid, x, y, z);
            }
            catch { }
            return SfmExtValue.Null;
        }
    }
}
