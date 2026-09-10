using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SFMOnline
{
    // 轻量 JSON 编解码：Unity 的 JsonUtility 不支持 Dictionary，
    // 服务端所有 POST 都要求 JSON 对象，这里自己实现一个可靠的。
    internal static class MiniJson
    {
        public static string Serialize(object value)
        {
            var sb = new StringBuilder(256);
            WriteValue(sb, value);
            return sb.ToString();
        }

        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int pos = 0;
            var v = ParseValue(json, ref pos);
            return v;
        }

        public static Dictionary<string, object> ParseObject(string json)
        {
            return Parse(json) as Dictionary<string, object>;
        }

        private static void WriteValue(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (v is int i) { sb.Append(i.ToString(CultureInfo.InvariantCulture)); return; }
            if (v is long l) { sb.Append(l.ToString(CultureInfo.InvariantCulture)); return; }
            if (v is double d) { sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); return; }
            if (v is float f) { sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); return; }
            if (v is string s) { WriteString(sb, s); return; }
            if (v is IDictionary<string, object> dict)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, kv.Key);
                    sb.Append(':');
                    WriteValue(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }
            if (v is IEnumerable list)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in list)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteValue(sb, item);
                }
                sb.Append(']');
                return;
            }
            WriteString(sb, v.ToString());
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s != null)
            {
                foreach (char c in s)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        default:
                            if (c < ' ')
                                sb.Append("\\u").Append(((int)c).ToString("x4"));
                            else
                                sb.Append(c);
                            break;
                    }
                }
            }
            sb.Append('"');
        }

        private static void SkipWs(string s, ref int pos)
        {
            while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
        }

        private static object ParseValue(string s, ref int pos)
        {
            SkipWs(s, ref pos);
            if (pos >= s.Length) return null;
            char c = s[pos];
            if (c == '{') return ParseObj(s, ref pos);
            if (c == '[') return ParseArr(s, ref pos);
            if (c == '"') return ParseStr(s, ref pos);
            if (s.IndexOf("true", pos, System.StringComparison.Ordinal) == pos) { pos += 4; return true; }
            if (s.IndexOf("false", pos, System.StringComparison.Ordinal) == pos) { pos += 5; return false; }
            if (s.IndexOf("null", pos, System.StringComparison.Ordinal) == pos) { pos += 4; return null; }
            return ParseNum(s, ref pos);
        }

        private static Dictionary<string, object> ParseObj(string s, ref int pos)
        {
            var dict = new Dictionary<string, object>();
            pos++; // {
            SkipWs(s, ref pos);
            if (pos < s.Length && s[pos] == '}') { pos++; return dict; }
            while (pos < s.Length)
            {
                SkipWs(s, ref pos);
                if (pos >= s.Length || s[pos] != '"') break;
                string key = ParseStr(s, ref pos);
                SkipWs(s, ref pos);
                if (pos < s.Length && s[pos] == ':') pos++;
                var val = ParseValue(s, ref pos);
                dict[key] = val;
                SkipWs(s, ref pos);
                if (pos < s.Length && s[pos] == ',') { pos++; continue; }
                if (pos < s.Length && s[pos] == '}') { pos++; break; }
                break;
            }
            return dict;
        }

        private static List<object> ParseArr(string s, ref int pos)
        {
            var list = new List<object>();
            pos++; // [
            SkipWs(s, ref pos);
            if (pos < s.Length && s[pos] == ']') { pos++; return list; }
            while (pos < s.Length)
            {
                var val = ParseValue(s, ref pos);
                list.Add(val);
                SkipWs(s, ref pos);
                if (pos < s.Length && s[pos] == ',') { pos++; continue; }
                if (pos < s.Length && s[pos] == ']') { pos++; break; }
                break;
            }
            return list;
        }

        private static string ParseStr(string s, ref int pos)
        {
            var sb = new StringBuilder();
            pos++; // "
            while (pos < s.Length)
            {
                char c = s[pos++];
                if (c == '"') break;
                if (c == '\\' && pos < s.Length)
                {
                    char e = s[pos++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (pos + 4 <= s.Length &&
                                int.TryParse(s.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cp))
                            {
                                sb.Append((char)cp);
                                pos += 4;
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static object ParseNum(string s, ref int pos)
        {
            int start = pos;
            if (pos < s.Length && (s[pos] == '-' || s[pos] == '+')) pos++;
            while (pos < s.Length && char.IsDigit(s[pos])) pos++;
            bool isFloat = false;
            if (pos < s.Length && s[pos] == '.')
            {
                isFloat = true;
                pos++;
                while (pos < s.Length && char.IsDigit(s[pos])) pos++;
            }
            if (pos < s.Length && (s[pos] == 'e' || s[pos] == 'E'))
            {
                isFloat = true;
                pos++;
                if (pos < s.Length && (s[pos] == '-' || s[pos] == '+')) pos++;
                while (pos < s.Length && char.IsDigit(s[pos])) pos++;
            }
            string num = s.Substring(start, pos - start);
            if (isFloat)
                return double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0d;
            return long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l) ? l : 0L;
        }
    }

    internal static class JsonHelper
    {
        public static string Str(Dictionary<string, object> o, string key)
        {
            return o != null && o.TryGetValue(key, out var v) && v != null ? v.ToString() : "";
        }

        public static int Int(Dictionary<string, object> o, string key, int def = 0)
        {
            if (o != null && o.TryGetValue(key, out var v))
            {
                if (v is bool b) return b ? 1 : 0;
                if (v is long l) return (int)l;
                if (v is double d) return (int)d;
                if (v is string s && int.TryParse(s, out int i)) return i;
            }
            return def;
        }

        public static long Long(Dictionary<string, object> o, string key, long def = 0)
        {
            if (o != null && o.TryGetValue(key, out var v))
            {
                if (v is long l) return l;
                if (v is double d) return (long)d;
                if (v is string s && long.TryParse(s, out long l2)) return l2;
            }
            return def;
        }

        public static bool Bool(Dictionary<string, object> o, string key, bool def = false)
        {
            if (o != null && o.TryGetValue(key, out var v))
            {
                if (v is bool b) return b;
                if (v is long l) return l != 0;
                if (v is double d) return d != 0;
                if (v is string s) return s == "1" || s.ToLowerInvariant() == "true";
            }
            return def;
        }

        public static double Double(Dictionary<string, object> o, string key, double def = 0)
        {
            if (o != null && o.TryGetValue(key, out var v))
            {
                if (v is double d) return d;
                if (v is long l) return l;
                if (v is string s && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double dd)) return dd;
            }
            return def;
        }

        public static List<Dictionary<string, object>> List(Dictionary<string, object> o, string key)
        {
            var res = new List<Dictionary<string, object>>();
            if (o != null && o.TryGetValue(key, out var v) && v is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item is Dictionary<string, object> d) res.Add(d);
                }
            }
            return res;
        }

        public static Dictionary<string, object> Object(Dictionary<string, object> o, string key)
        {
            if (o != null && o.TryGetValue(key, out var v) && v is Dictionary<string, object> d) return d;
            return new Dictionary<string, object>();
        }
        public static List<string> StrList(Dictionary<string, object> o, string key)
        {
            var res = new List<string>();
            if (o != null && o.TryGetValue(key, out var v) && v is List<object> list)
            {
                foreach (var item in list) if (item != null) res.Add(item.ToString());
            }
            return res;
        }
    }
}