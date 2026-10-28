using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DiceThrower.Core
{
    /// <summary>
    /// Minimal JSON for the save file. Core has no Unity (so no JsonUtility) to lean on.
    /// Values are null, bool, long, double, string, List&lt;object&gt; and Dictionary&lt;string, object&gt;.
    /// </summary>
    public static class Json
    {
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value);
            return sb.ToString();
        }

        /// <summary>Throws FormatException when the text is not valid JSON.</summary>
        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("JSON text is null");
            var parser = new Parser(text);
            object value = parser.ReadValue();
            parser.SkipWhite();
            if (!parser.AtEnd) throw parser.Error("unexpected text after the value");
            return value;
        }

        // ------------------------------------------------------------------ writing

        private static void WriteValue(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is int || v is long) { sb.Append(Convert.ToInt64(v).ToString(CultureInfo.InvariantCulture)); return; }
            if (v is double) { sb.Append(((double)v).ToString("R", CultureInfo.InvariantCulture)); return; }

            var dict = v as IDictionary<string, object>;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object> pair in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, pair.Key);
                    sb.Append(':');
                    WriteValue(sb, pair.Value);
                }
                sb.Append('}');
                return;
            }

            var list = v as IList<object>;
            if (list != null)
            {
                sb.Append('[');
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    WriteValue(sb, list[i]);
                }
                sb.Append(']');
                return;
            }

            throw new ArgumentException("Unsupported JSON value type " + v.GetType().Name);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------------ parsing

        private sealed class Parser
        {
            private const string NumberChars = "+-0123456789.eE";
            private const int HexDigitsInEscape = 4;

            private readonly string _s;
            private int _i;

            public Parser(string text) { _s = text; }

            public bool AtEnd { get { return _i >= _s.Length; } }

            public FormatException Error(string message)
            {
                return new FormatException("Invalid JSON at position " + _i + ": " + message);
            }

            public void SkipWhite()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            public object ReadValue()
            {
                SkipWhite();
                if (AtEnd) throw Error("unexpected end of text");
                switch (_s[_i])
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ReadNumber();
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error("unexpected character");
                _i += word.Length;
            }

            private object ReadObject()
            {
                _i++; // {
                var dict = new Dictionary<string, object>();
                SkipWhite();
                if (!AtEnd && _s[_i] == '}') { _i++; return dict; }
                while (true)
                {
                    SkipWhite();
                    if (AtEnd || _s[_i] != '"') throw Error("expected a property name");
                    string key = ReadString();
                    SkipWhite();
                    if (AtEnd || _s[_i] != ':') throw Error("expected ':'");
                    _i++;
                    dict[key] = ReadValue();
                    SkipWhite();
                    if (AtEnd) throw Error("unterminated object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return dict; }
                    throw Error("expected ',' or '}'");
                }
            }

            private object ReadArray()
            {
                _i++; // [
                var list = new List<object>();
                SkipWhite();
                if (!AtEnd && _s[_i] == ']') { _i++; return list; }
                while (true)
                {
                    list.Add(ReadValue());
                    SkipWhite();
                    if (AtEnd) throw Error("unterminated array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return list; }
                    throw Error("expected ',' or ']'");
                }
            }

            private string ReadString()
            {
                _i++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }

                    if (AtEnd) throw Error("unterminated escape");
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
                            if (_i + HexDigitsInEscape > _s.Length
                                || !int.TryParse(_s.Substring(_i, HexDigitsInEscape), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out code))
                                throw Error("bad \\u escape");
                            sb.Append((char)code);
                            _i += HexDigitsInEscape;
                            break;
                        default: throw Error("bad escape");
                    }
                }
            }

            private object ReadNumber()
            {
                int start = _i;
                while (_i < _s.Length && NumberChars.IndexOf(_s[_i]) >= 0) _i++;
                if (_i == start) throw Error("unexpected character '" + _s[_i] + "'");

                string token = _s.Substring(start, _i - start);
                if (token.IndexOfAny(new[] { '.', 'e', 'E' }) < 0)
                {
                    long whole;
                    if (long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out whole)) return whole;
                    throw Error("bad number");
                }
                double real;
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out real)) return real;
                throw Error("bad number");
            }
        }
    }
}
