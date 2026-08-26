using System;
using System.Collections.Generic;
using System.Linq;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  随机系统：随机抽取（抽奖）/ 随机选人（点名）/ 奖池 / 转盘
    // ====================================================================
    public static class SfmExtRandom
    {
        // ---------- 随机选人 ----------
        /// <summary>从房间玩家中随机选一个人。</summary>
        public static string PickRandomPlayer()
        {
            var uids = SfmExtBridge.GetGhostUids?.Invoke() ?? new List<string>();
            if (uids.Count == 0) return "";
            return uids[UnityEngine.Random.Range(0, uids.Count)];
        }

        /// <summary>从房间玩家中随机选 N 个人。</summary>
        public static List<string> PickRandomPlayers(int count, string excludeUid = "")
        {
            var uids = (SfmExtBridge.GetGhostUids?.Invoke() ?? new List<string>())
                .Where(u => u != excludeUid).ToList();
            var result = new List<string>();
            while (result.Count < count && uids.Count > 0)
            {
                int idx = UnityEngine.Random.Range(0, uids.Count);
                result.Add(uids[idx]);
                uids.RemoveAt(idx);
            }
            return result;
        }

        /// <summary>从列表里随机取一个。</summary>
        public static T Pick<T>(IList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        // ---------- 奖池（权重抽取） ----------
        private sealed class PoolEntry
        {
            public string Name;
            public double Weight;
            public int Count;              // 剩余次数，-1 无限
            public Action<string> OnWin;
        }

        private static readonly Dictionary<string, List<PoolEntry>> _pools = new Dictionary<string, List<PoolEntry>>();

        /// <summary>创建奖池并添加条目（weight 权重，times 抽取次数 -1=无限）。</summary>
        public static void PoolAdd(string pool, string name, double weight = 1, int times = -1)
        {
            if (!_pools.TryGetValue(pool, out var list)) { list = new List<PoolEntry>(); _pools[pool] = list; }
            list.Add(new PoolEntry { Name = name, Weight = Math.Max(0.0001, weight), Count = times });
        }

        public static void PoolClear(string pool) => _pools.Remove(pool);

        /// <summary>从奖池抽取一个条目。</summary>
        public static string PoolDraw(string pool)
        {
            if (!_pools.TryGetValue(pool, out var list) || list.Count == 0) return "";
            double total = list.Sum(e => e.Count == 0 ? 0 : e.Weight);
            if (total <= 0) return "";
            double r = UnityEngine.Random.Range(0f, (float)total);
            double acc = 0;
            foreach (var e in list)
            {
                if (e.Count == 0) continue;
                acc += e.Weight;
                if (r <= acc)
                {
                    if (e.Count > 0) e.Count--;
                    list.RemoveAll(x => x.Count == 0);
                    e.OnWin?.Invoke(e.Name);
                    SfmExtEvent.Emit("pool_win", new SfmExtValue(e.Name));
                    return e.Name;
                }
            }
            return "";
        }

        // ---------- 引擎函数 ----------
        [SfmExtFunction("random.pick_player")]
        public static SfmExtValue FnPickPlayer(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(PickRandomPlayer());

        [SfmExtFunction("random.pick_players")]
        public static SfmExtValue FnPickPlayers(SfmExtParams p, SfmExtValue u)
        {
            var list = PickRandomPlayers((int)p.Get("count").ToFloat(), p.Get("exclude").ToString());
            var v = new SfmExtValue(SfmExtValue.Type.List);
            for (int i = 0; i < list.Count; i++) v[i.ToString()] = new SfmExtValue(list[i]);
            return v;
        }

        [SfmExtFunction("random.pool_add")]
        public static SfmExtValue FnPoolAdd(SfmExtParams p, SfmExtValue u)
        {
            PoolAdd(p.Get("pool").ToString(), p.Get("name").ToString(),
                p.Get("weight", "1").ToFloat(), (int)p.Get("times", "-1").ToFloat());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("random.pool_draw")]
        public static SfmExtValue FnPoolDraw(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(PoolDraw(p.Get("pool").ToString()));

        // ---------- 掷骰子 ----------
        [SfmExtFunction("random.dice")]
        public static SfmExtValue FnDice(SfmExtParams p, SfmExtValue u)
        {
            int sides = (int)p.Get("sides", "6").ToFloat();
            return new SfmExtValue(UnityEngine.Random.Range(1, sides + 1));
        }

        [SfmExtFunction("random.coin")]
        public static SfmExtValue FnCoin(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(UnityEngine.Random.Range(0, 2) == 0);
    }

    // ====================================================================
    //  投票系统：发起投票 → 收集选项 → 计票 → 结果回调
    // ====================================================================
    public static class SfmExtVote
    {
        public sealed class Vote
        {
            public string Id;
            public string Title;
            public List<string> Options = new List<string>();
            public int MaxChoices = 1;
            public float Duration = 30f;
            public DateTime StartedAt;
            public bool Finished;
            public Dictionary<string, List<int>> Ballots = new Dictionary<string, List<int>>(); // uid -> 选项索引
            public Action<string, List<int>, Dictionary<int, int>> OnResult; // (id, winnerIndexes, counts)

            public bool IsOpen => !Finished && (DateTime.Now - StartedAt).TotalSeconds < Duration;
        }

        private static readonly Dictionary<string, Vote> _votes = new Dictionary<string, Vote>();

        /// <summary>发起投票（联机广播）。</summary>
        public static Vote Start(string id, string title, string[] options, float duration = 30f, int maxChoices = 1)
        {
            if (_votes.ContainsKey(id)) _votes.Remove(id);
            var v = new Vote
            {
                Id = id, Title = title, Duration = duration, MaxChoices = Math.Max(1, maxChoices),
                StartedAt = DateTime.Now
            };
            v.Options.AddRange(options ?? new string[0]);
            _votes[id] = v;
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_vote", ["ns"] = SfmExtEventBus.Namespace,
                ["op"] = "start", ["id"] = id, ["title"] = title,
                ["options"] = string.Join("\x1f", v.Options),
                ["duration"] = duration, ["max"] = maxChoices
            });
            SfmExtEvent.Emit("vote_started", new SfmExtValue(id));
            return v;
        }

        /// <summary>投票（选择选项索引列表）。</summary>
        public static bool Cast(string id, List<int> choices, string uid = "")
        {
            if (!_votes.TryGetValue(id, out var v) || !v.IsOpen) return false;
            if (uid.Length == 0) uid = SfmExtBridge.GetLocalUid?.Invoke() ?? "";
            if (uid.Length == 0) return false;
            if (choices.Count > v.MaxChoices) return false;
            v.Ballots[uid] = choices;
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_vote", ["ns"] = SfmExtEventBus.Namespace,
                ["op"] = "cast", ["id"] = id,
                ["uid"] = uid, ["choices"] = string.Join("\x1f", choices)
            });
            return true;
        }

        public static void Stop(string id)
        {
            if (_votes.TryGetValue(id, out var v)) Finish(v);
        }

        public static Vote Get(string id) => _votes.TryGetValue(id, out var v) ? v : null;

        public static Dictionary<int, int> GetCounts(string id)
        {
            var counts = new Dictionary<int, int>();
            if (!_votes.TryGetValue(id, out var v)) return counts;
            foreach (var ballot in v.Ballots.Values)
                foreach (var c in ballot)
                {
                    counts.TryGetValue(c, out var n);
                    counts[c] = n + 1;
                }
            return counts;
        }

        private static void Finish(Vote v)
        {
            if (v.Finished) return;
            v.Finished = true;
            var counts = new Dictionary<int, int>();
            foreach (var ballot in v.Ballots.Values)
                foreach (var c in ballot)
                {
                    counts.TryGetValue(c, out var n);
                    counts[c] = n + 1;
                }
            // 胜出者：票数最高（MaxChoices 个）
            var ranked = counts.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
            var winners = ranked.Take(v.MaxChoices).ToList();
            v.OnResult?.Invoke(v.Id, winners, counts);
            SfmExtEvent.Emit("vote_finished", SfmExtEvent.FromJsonObject(new Dictionary<string, object>
            {
                ["id"] = v.Id, ["winners"] = string.Join(",", winners)
            }));
            _votes.Remove(v.Id);
        }

        internal static void HandleRemote(string op, Dictionary<string, object> m)
        {
            var id = SfmExtRoom.Str(m, "id");
            switch (op)
            {
                case "start":
                    if (!_votes.ContainsKey(id))
                    {
                        var options = (SfmExtRoom.Str(m, "options") ?? "").Split(new[] { '\x1f' }, StringSplitOptions.RemoveEmptyEntries);
                        Start(id, SfmExtRoom.Str(m, "title"), options, (float)(m.TryGetValue("duration", out var d) ? Convert.ToDouble(d) : 30));
                    }
                    break;
                case "cast":
                    if (_votes.TryGetValue(id, out var v) && v.IsOpen)
                    {
                        var uid = SfmExtRoom.Str(m, "uid");
                        var choices = new List<int>();
                        foreach (var s in (SfmExtRoom.Str(m, "choices") ?? "").Split(new[] { '\x1f' }, StringSplitOptions.RemoveEmptyEntries))
                            if (int.TryParse(s, out var c)) choices.Add(c);
                        v.Ballots[uid] = choices;
                    }
                    break;
            }
        }

        // 每帧检查超时
        [SfmExtUpdate]
        public static void Update()
        {
            foreach (var v in _votes.Values.ToArray())
                if (!v.IsOpen && !v.Finished) Finish(v);
        }

        // ---------- 引擎函数 ----------
        [SfmExtFunction("vote.start")]
        public static SfmExtValue FnStart(SfmExtParams p, SfmExtValue u)
        {
            var options = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                var k = "option" + i;
                if (!p.Has(k)) break;
                options.Add(p.Get(k).ToString());
            }
            Start(p.Get("id").ToString(), p.Get("title").ToString(), options.ToArray(),
                (float)p.Get("duration", "30").ToFloat(), (int)p.Get("max", "1").ToFloat());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("vote.cast")]
        public static SfmExtValue FnCast(SfmExtParams p, SfmExtValue u)
        {
            var choices = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                var k = "choice" + i;
                if (!p.Has(k)) break;
                choices.Add((int)p.Get(k).ToFloat());
            }
            return new SfmExtValue(Cast(p.Get("id").ToString(), choices, p.Get("uid").ToString()));
        }

        [SfmExtFunction("vote.stop")]
        public static SfmExtValue FnStop(SfmExtParams p, SfmExtValue u)
        {
            Stop(p.Get("id").ToString());
            return SfmExtValue.Null;
        }
    }
}
