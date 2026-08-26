using System;
using System.Collections.Generic;
using System.Linq;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  玩法扩展：队伍/阵营、排行榜、成就、公告、计时器、礼物、观战
    // ====================================================================
    public static class SfmExtGameplay
    {
        // ---------- 队伍 / 阵营 ----------
        private static readonly Dictionary<string, string> _teamOf = new Dictionary<string, string>(); // uid -> team
        private static readonly List<string> _teams = new List<string>();

        public static void AddTeam(string team)
        {
            if (!_teams.Contains(team)) _teams.Add(team);
        }

        public static void SetTeam(string uid, string team)
        {
            if (team.Length == 0) _teamOf.Remove(uid);
            else _teamOf[uid] = team;
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_team", ["ns"] = SfmExtEventBus.Namespace,
                ["uid"] = uid, ["team"] = team
            });
            SfmExtEvent.Emit("team_changed", new SfmExtValue(SfmExtValue.Type.List)
            {
                ["uid"] = new SfmExtValue(uid), ["team"] = new SfmExtValue(team)
            });
        }

        public static string GetTeam(string uid) => _teamOf.TryGetValue(uid, out var t) ? t : "";

        public static List<string> GetTeamMembers(string team)
            => _teamOf.Where(kv => kv.Value == team).Select(kv => kv.Key).ToList();

        public static bool SameTeam(string a, string b)
        {
            var ta = GetTeam(a); var tb = GetTeam(b);
            return ta.Length > 0 && ta == tb;
        }

        internal static void HandleRemoteTeam(string uid, string team)
        {
            if (team.Length == 0) _teamOf.Remove(uid);
            else _teamOf[uid] = team;
        }

        [SfmExtFunction("team.add")]
        public static SfmExtValue FnTeamAdd(SfmExtParams p, SfmExtValue u)
        {
            AddTeam(p.Get("name").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("team.set")]
        public static SfmExtValue FnTeamSet(SfmExtParams p, SfmExtValue u)
        {
            SetTeam(p.Get("uid").ToString(), p.Get("team").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("team.get")]
        public static SfmExtValue FnTeamGet(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(GetTeam(p.Get("uid").ToString()));

        [SfmExtFunction("team.members")]
        public static SfmExtValue FnTeamMembers(SfmExtParams p, SfmExtValue u)
        {
            var members = GetTeamMembers(p.Get("team").ToString());
            var v = new SfmExtValue(SfmExtValue.Type.List);
            for (int i = 0; i < members.Count; i++) v[i.ToString()] = new SfmExtValue(members[i]);
            return v;
        }

        // ---------- 排行榜 ----------
        private sealed class RankEntry
        {
            public string Key;
            public double Score;
            public Dictionary<string, object> Meta = new Dictionary<string, object>();
        }

        private static readonly Dictionary<string, Dictionary<string, RankEntry>> _leaderboards
            = new Dictionary<string, Dictionary<string, RankEntry>>();

        public static void LeaderboardSet(string board, string key, double score, Dictionary<string, object> meta = null)
        {
            if (!_leaderboards.TryGetValue(board, out var entries)) { entries = new Dictionary<string, RankEntry>(); _leaderboards[board] = entries; }
            if (!entries.TryGetValue(key, out var e)) { e = new RankEntry { Key = key }; entries[key] = e; }
            e.Score = score;
            if (meta != null) e.Meta = meta;
        }

        public static double LeaderboardGet(string board, string key)
        {
            return _leaderboards.TryGetValue(board, out var entries) && entries.TryGetValue(key, out var e) ? e.Score : 0;
        }

        /// <summary>返回排名列表（降序），每个元素是 (key, score)。</summary>
        public static List<(string Key, double Score)> LeaderboardTop(string board, int count = 10)
        {
            if (!_leaderboards.TryGetValue(board, out var entries)) return new List<(string, double)>();
            return entries.Values.OrderByDescending(e => e.Score).Take(count)
                .Select(e => (e.Key, e.Score)).ToList();
        }

        [SfmExtFunction("rank.set")]
        public static SfmExtValue FnRankSet(SfmExtParams p, SfmExtValue u)
        {
            LeaderboardSet(p.Get("board").ToString(), p.Get("key").ToString(), p.Get("score").ToFloat());
            return SfmExtValue.Null;
        }

        [SfmExtFunction("rank.get")]
        public static SfmExtValue FnRankGet(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(LeaderboardGet(p.Get("board").ToString(), p.Get("key").ToString()));

        [SfmExtFunction("rank.top")]
        public static SfmExtValue FnRankTop(SfmExtParams p, SfmExtValue u)
        {
            var top = LeaderboardTop(p.Get("board").ToString(), (int)p.Get("count", "10").ToFloat());
            var v = new SfmExtValue(SfmExtValue.Type.List);
            for (int i = 0; i < top.Count; i++)
            {
                var r = new SfmExtValue(SfmExtValue.Type.List);
                r["key"] = new SfmExtValue(top[i].Key);
                r["score"] = new SfmExtValue(top[i].Score);
                v[i.ToString()] = r;
            }
            return v;
        }

        // ---------- 成就 ----------
        private static readonly Dictionary<string, HashSet<string>> _achievements = new Dictionary<string, HashSet<string>>(); // uid -> 成就

        public static void UnlockAchievement(string uid, string achievement)
        {
            if (!_achievements.TryGetValue(uid, out var set)) { set = new HashSet<string>(); _achievements[uid] = set; }
            if (set.Add(achievement))
            {
                SfmExtEvent.Emit("achievement_unlocked", new SfmExtValue(SfmExtValue.Type.List)
                {
                    ["uid"] = new SfmExtValue(uid), ["achievement"] = new SfmExtValue(achievement)
                });
                SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
                {
                    ["t"] = "ext_achievement", ["ns"] = SfmExtEventBus.Namespace,
                    ["uid"] = uid, ["achievement"] = achievement
                });
            }
        }

        public static bool HasAchievement(string uid, string achievement)
            => _achievements.TryGetValue(uid, out var set) && set.Contains(achievement);

        [SfmExtFunction("achievement.unlock")]
        public static SfmExtValue FnAchUnlock(SfmExtParams p, SfmExtValue u)
        {
            UnlockAchievement(p.Get("uid").ToString(), p.Get("name").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("achievement.has")]
        public static SfmExtValue FnAchHas(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(HasAchievement(p.Get("uid").ToString(), p.Get("name").ToString()));

        // ---------- 公告 ----------
        public static void Announce(string text, string title = "")
        {
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_announce", ["ns"] = SfmExtEventBus.Namespace,
                ["title"] = title, ["text"] = text
            });
            SfmExtHud.Toast(text, 6f);
            SfmExtEvent.Emit("announce", new SfmExtValue(SfmExtValue.Type.List)
            {
                ["title"] = new SfmExtValue(title), ["text"] = new SfmExtValue(text)
            });
        }

        [SfmExtFunction("gameplay.announce")]
        public static SfmExtValue FnAnnounce(SfmExtParams p, SfmExtValue u)
        {
            Announce(p.Get("text").ToString(), p.Get("title").ToString());
            return SfmExtValue.Null;
        }

        // ---------- 计时器（倒计时） ----------
        private static readonly Dictionary<string, double> _countdowns = new Dictionary<string, double>(); // name -> 结束时间(unix)
        private static readonly Dictionary<string, Action<string>> _countdownHandlers = new Dictionary<string, Action<string>>();

        public static void StartCountdown(string name, float seconds, Action<string> onDone = null)
        {
            _countdowns[name] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + seconds;
            if (onDone != null) _countdownHandlers[name] = onDone;
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_countdown", ["ns"] = SfmExtEventBus.Namespace,
                ["op"] = "start", ["name"] = name, ["seconds"] = seconds
            });
        }

        public static void CancelCountdown(string name) => _countdowns.Remove(name);

        public static float GetCountdownRemaining(string name)
        {
            if (!_countdowns.TryGetValue(name, out var end)) return 0;
            return Math.Max(0f, (float)(end - DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }

        [SfmExtUpdate]
        public static void UpdateCountdowns()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var kv in _countdowns.ToArray())
            {
                if (now >= kv.Value)
                {
                    _countdowns.Remove(kv.Key);
                    if (_countdownHandlers.TryGetValue(kv.Key, out var h))
                    {
                        _countdownHandlers.Remove(kv.Key);
                        h(kv.Key);
                    }
                    SfmExtEvent.Emit("countdown_done", new SfmExtValue(kv.Key));
                }
            }
        }

        [SfmExtFunction("gameplay.countdown")]
        public static SfmExtValue FnCountdown(SfmExtParams p, SfmExtValue u)
        {
            StartCountdown(p.Get("name").ToString(), (float)p.Get("seconds").ToFloat());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("gameplay.countdown_remaining")]
        public static SfmExtValue FnCountdownRemaining(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(GetCountdownRemaining(p.Get("name").ToString()));

        // ---------- 礼物 ----------
        public static void GiveGift(string uid, string item, int count = 1)
        {
            SfmExtBridge.SendToPlayer?.Invoke(uid, new Dictionary<string, object>
            {
                ["t"] = "ext_gift", ["ns"] = SfmExtEventBus.Namespace,
                ["item"] = item, ["count"] = count
            });
        }

        [SfmExtFunction("gameplay.gift")]
        public static SfmExtValue FnGift(SfmExtParams p, SfmExtValue u)
        {
            GiveGift(p.Get("uid").ToString(), p.Get("item").ToString(), (int)p.Get("count", "1").ToFloat());
            return new SfmExtValue(true);
        }

        // ---------- 观战 ----------
        private static string _spectateUid = "";

        public static void Spectate(string uid)
        {
            _spectateUid = uid;
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_spectate", ["ns"] = SfmExtEventBus.Namespace,
                ["op"] = "start", ["target"] = uid
            });
        }

        public static void StopSpectate()
        {
            _spectateUid = "";
            SfmExtBridge.SendToRoom?.Invoke(new Dictionary<string, object>
            {
                ["t"] = "ext_spectate", ["ns"] = SfmExtEventBus.Namespace,
                ["op"] = "stop"
            });
        }

        public static string Spectating => _spectateUid;

        [SfmExtFunction("gameplay.spectate")]
        public static SfmExtValue FnSpectate(SfmExtParams p, SfmExtValue u)
        {
            Spectate(p.Get("uid").ToString());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("gameplay.spectate_stop")]
        public static SfmExtValue FnSpectateStop(SfmExtParams p, SfmExtValue u)
        {
            StopSpectate();
            return SfmExtValue.Null;
        }

        // ---------- 人数统计 ----------
        [SfmExtFunction("gameplay.player_count")]
        public static SfmExtValue FnPlayerCount(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue((SfmExtBridge.GetGhostUids?.Invoke() ?? new List<string>()).Count);
    }
}
