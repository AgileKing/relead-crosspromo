using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Relead.CrossPromo.Json
{
    /// <summary>
    /// Tiny JSON reader/writer. Objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;,
    /// numbers long (if integral and fits) or double, plus string, bool and null.
    /// Needed because JsonUtility can't read dictionaries (localized text) or nulls, and Newtonsoft is not allowed.
    /// </summary>
    public static class MiniJson
    {
        public static bool TryParse(string json, out object value, out string error)
        {
            value = null;
            error = null;
            if (json == null) { error = "null input"; return false; }
            try
            {
                var p = new Parser(json);
                value = p.ParseValue();
                p.SkipWhitespace();
                if (!p.AtEnd) throw new FormatException("Trailing characters at " + p.Position);
                return true;
            }
            catch (FormatException e)
            {
                error = e.Message;
                value = null;
                return false;
            }
        }

        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string s) { WriteString(sb, s); return; }
            if (v is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (v is IDictionary dict)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry e in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, Convert.ToString(e.Key, CultureInfo.InvariantCulture));
                    sb.Append(':');
                    Write(sb, e.Value);
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
                    Write(sb, item);
                }
                sb.Append(']');
                return;
            }
            if (v is float f) { WriteDouble(sb, f); return; }
            if (v is double d) { WriteDouble(sb, d); return; }
            if (v is decimal m) { sb.Append(m.ToString(CultureInfo.InvariantCulture)); return; }
            if (v is int || v is long || v is short || v is byte || v is uint || v is ulong || v is ushort || v is sbyte)
            {
                sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                return;
            }
            if (v is DateTime dt) { WriteString(sb, dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)); return; }
            WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        static void WriteDouble(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
            sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
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
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        sealed class Parser
        {
            readonly string _s;
            int _i;
            int _depth;
            const int MaxDepth = 64;

            public Parser(string s) { _s = s; }
            public bool AtEnd => _i >= _s.Length;
            public int Position => _i;

            public void SkipWhitespace()
            {
                while (_i < _s.Length && (_s[_i] == ' ' || _s[_i] == '\t' || _s[_i] == '\n' || _s[_i] == '\r' || _s[_i] == '﻿')) _i++;
            }

            FormatException Fail(string what) => new FormatException(what + " at " + _i);

            public object ParseValue()
            {
                SkipWhitespace();
                if (AtEnd) throw Fail("Unexpected end");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw Fail("Unexpected character '" + c + "'");
                }
            }

            void Expect(string word)
            {
                if (_i + word.Length > _s.Length || string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Fail("Expected " + word);
                _i += word.Length;
            }

            Dictionary<string, object> ParseObject()
            {
                if (++_depth > MaxDepth) throw Fail("Too deep");
                var d = new Dictionary<string, object>();
                _i++;
                SkipWhitespace();
                if (!AtEnd && _s[_i] == '}') { _i++; _depth--; return d; }
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _s[_i] != '"') throw Fail("Expected property name");
                    string key = ParseString();
                    SkipWhitespace();
                    if (AtEnd || _s[_i] != ':') throw Fail("Expected ':'");
                    _i++;
                    d[key] = ParseValue();
                    SkipWhitespace();
                    if (AtEnd) throw Fail("Unexpected end in object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; _depth--; return d; }
                    throw Fail("Expected ',' or '}'");
                }
            }

            List<object> ParseArray()
            {
                if (++_depth > MaxDepth) throw Fail("Too deep");
                var l = new List<object>();
                _i++;
                SkipWhitespace();
                if (!AtEnd && _s[_i] == ']') { _i++; _depth--; return l; }
                while (true)
                {
                    l.Add(ParseValue());
                    SkipWhitespace();
                    if (AtEnd) throw Fail("Unexpected end in array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; _depth--; return l; }
                    throw Fail("Expected ',' or ']'");
                }
            }

            string ParseString()
            {
                _i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Fail("Unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Fail("Bad escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            int code;
                            if (_i + 4 > _s.Length || !int.TryParse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                throw Fail("Bad unicode escape");
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default: throw Fail("Bad escape");
                    }
                }
            }

            object ParseNumber()
            {
                int start = _i;
                if (_s[_i] == '-') _i++;
                bool isFloat = false;
                while (!AtEnd)
                {
                    char c = _s[_i];
                    if (c >= '0' && c <= '9') { _i++; continue; }
                    if (c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') { isFloat = true; _i++; continue; }
                    break;
                }
                string num = _s.Substring(start, _i - start);
                if (!isFloat)
                {
                    long l;
                    if (long.TryParse(num, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out l)) return l;
                }
                double d;
                if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
                throw Fail("Bad number '" + num + "'");
            }
        }
    }

    /// <summary>Typed, forgiving accessors over MiniJson output. Wrong type = null, never an exception.</summary>
    public static class JsonRead
    {
        public static Dictionary<string, object> Obj(Dictionary<string, object> o, string key)
        {
            object v;
            return o != null && o.TryGetValue(key, out v) ? v as Dictionary<string, object> : null;
        }

        public static List<object> Arr(Dictionary<string, object> o, string key)
        {
            object v;
            return o != null && o.TryGetValue(key, out v) ? v as List<object> : null;
        }

        public static string Str(Dictionary<string, object> o, string key)
        {
            object v;
            return o != null && o.TryGetValue(key, out v) ? v as string : null;
        }

        public static double? Num(Dictionary<string, object> o, string key)
        {
            object v;
            if (o == null || !o.TryGetValue(key, out v) || v == null) return null;
            if (v is long) return (long)v;
            if (v is double) return (double)v;
            return null;
        }

        public static long? Long(Dictionary<string, object> o, string key)
        {
            var d = Num(o, key);
            if (d == null) return null;
            double x = d.Value;
            if (x != Math.Floor(x) || x > long.MaxValue || x < long.MinValue) return null;
            return (long)x;
        }

        public static bool? Bool(Dictionary<string, object> o, string key)
        {
            object v;
            return o != null && o.TryGetValue(key, out v) && v is bool ? (bool)v : (bool?)null;
        }
    }
}
