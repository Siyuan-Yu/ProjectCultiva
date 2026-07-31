using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace XianXia.Data.Serialization
{
    /// <summary>
    /// Minimal JSON reader for M1 content/snapshot DTOs. No UnityEngine.JsonUtility.
    /// </summary>
    public static class SimpleJson
    {
        public static JsonValue Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var parser = new Parser(text);
            var value = parser.ParseValue();
            parser.SkipWs();
            if (!parser.Eof)
                throw new FormatException("Trailing content in JSON.");
            return value;
        }

        public static string Stringify(JsonValue value)
        {
            var sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, JsonValue value)
        {
            switch (value.Kind)
            {
                case JsonValueKind.Null:
                    sb.Append("null");
                    break;
                case JsonValueKind.Boolean:
                    sb.Append(value.Bool ? "true" : "false");
                    break;
                case JsonValueKind.Number:
                    sb.Append(value.Number.ToString(CultureInfo.InvariantCulture));
                    break;
                case JsonValueKind.String:
                    WriteString(sb, value.String);
                    break;
                case JsonValueKind.Array:
                    sb.Append('[');
                    for (var i = 0; i < value.Array.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        Write(sb, value.Array[i]);
                    }
                    sb.Append(']');
                    break;
                case JsonValueKind.Object:
                    sb.Append('{');
                    var first = true;
                    foreach (var kv in value.Object)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, kv.Key);
                        sb.Append(':');
                        Write(sb, kv.Value);
                    }
                    sb.Append('}');
                    break;
            }
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s != null)
            {
                foreach (var c in s)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default: sb.Append(c); break;
                    }
                }
            }
            sb.Append('"');
        }

        sealed class Parser
        {
            readonly string _text;
            int _i;

            public Parser(string text) { _text = text; }

            public bool Eof => _i >= _text.Length;

            public void SkipWs()
            {
                while (_i < _text.Length && char.IsWhiteSpace(_text[_i])) _i++;
            }

            public JsonValue ParseValue()
            {
                SkipWs();
                if (Eof) throw new FormatException("Unexpected end of JSON.");
                var c = _text[_i];
                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == '"') return JsonValue.FromString(ParseString());
                if (c == 't' || c == 'f') return JsonValue.FromBool(ParseBool());
                if (c == 'n') { ParseNull(); return JsonValue.Null; }
                return JsonValue.FromNumber(ParseNumber());
            }

            JsonValue ParseObject()
            {
                _i++; // {
                var obj = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
                SkipWs();
                if (Peek('}')) { _i++; return JsonValue.FromObject(obj); }
                while (true)
                {
                    SkipWs();
                    var key = ParseString();
                    SkipWs();
                    Expect(':');
                    var val = ParseValue();
                    obj[key] = val;
                    SkipWs();
                    if (Peek('}')) { _i++; break; }
                    Expect(',');
                }
                return JsonValue.FromObject(obj);
            }

            JsonValue ParseArray()
            {
                _i++; // [
                var list = new List<JsonValue>();
                SkipWs();
                if (Peek(']')) { _i++; return JsonValue.FromArray(list); }
                while (true)
                {
                    list.Add(ParseValue());
                    SkipWs();
                    if (Peek(']')) { _i++; break; }
                    Expect(',');
                }
                return JsonValue.FromArray(list);
            }

            string ParseString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (!Eof)
                {
                    var c = _text[_i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (Eof) throw new FormatException("Unterminated escape.");
                        var e = _text[_i++];
                        switch (e)
                        {
                            case '"':
                            case '\\':
                            case '/': sb.Append(e); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            default: throw new FormatException("Unsupported escape \\" + e);
                        }
                    }
                    else sb.Append(c);
                }
                throw new FormatException("Unterminated string.");
            }

            bool ParseBool()
            {
                if (Match("true")) return true;
                if (Match("false")) return false;
                throw new FormatException("Invalid boolean.");
            }

            void ParseNull()
            {
                if (!Match("null")) throw new FormatException("Invalid null.");
            }

            double ParseNumber()
            {
                var start = _i;
                if (Peek('-')) _i++;
                while (!Eof && char.IsDigit(_text[_i])) _i++;
                if (Peek('.'))
                {
                    _i++;
                    while (!Eof && char.IsDigit(_text[_i])) _i++;
                }
                if (Peek('e') || Peek('E'))
                {
                    _i++;
                    if (Peek('+') || Peek('-')) _i++;
                    while (!Eof && char.IsDigit(_text[_i])) _i++;
                }
                var slice = _text.Substring(start, _i - start);
                if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                    throw new FormatException("Invalid number: " + slice);
                return n;
            }

            bool Match(string literal)
            {
                if (_i + literal.Length > _text.Length) return false;
                if (string.CompareOrdinal(_text, _i, literal, 0, literal.Length) != 0) return false;
                _i += literal.Length;
                return true;
            }

            bool Peek(char c) => !Eof && _text[_i] == c;

            void Expect(char c)
            {
                SkipWs();
                if (!Peek(c)) throw new FormatException("Expected '" + c + "'.");
                _i++;
            }
        }
    }

    public enum JsonValueKind
    {
        Null,
        Boolean,
        Number,
        String,
        Array,
        Object
    }

    public sealed class JsonValue
    {
        public JsonValueKind Kind { get; private set; }
        public bool Bool { get; private set; }
        public double Number { get; private set; }
        public string String { get; private set; }
        public List<JsonValue> Array { get; private set; }
        public Dictionary<string, JsonValue> Object { get; private set; }

        public static JsonValue Null { get; } = new JsonValue { Kind = JsonValueKind.Null };

        public static JsonValue FromBool(bool v) => new JsonValue { Kind = JsonValueKind.Boolean, Bool = v };
        public static JsonValue FromNumber(double v) => new JsonValue { Kind = JsonValueKind.Number, Number = v };
        public static JsonValue FromString(string v) => new JsonValue { Kind = JsonValueKind.String, String = v ?? string.Empty };
        public static JsonValue FromArray(List<JsonValue> v) => new JsonValue { Kind = JsonValueKind.Array, Array = v };
        public static JsonValue FromObject(Dictionary<string, JsonValue> v) => new JsonValue { Kind = JsonValueKind.Object, Object = v };

        public bool TryGetProperty(string name, out JsonValue value)
        {
            value = null;
            if (Kind != JsonValueKind.Object || Object == null) return false;
            return Object.TryGetValue(name, out value);
        }

        public string GetString(string name, string fallback = null)
        {
            if (!TryGetProperty(name, out var v) || v.Kind != JsonValueKind.String) return fallback;
            return v.String;
        }

        public double GetNumber(string name, double fallback = 0)
        {
            if (!TryGetProperty(name, out var v) || v.Kind != JsonValueKind.Number) return fallback;
            return v.Number;
        }
    }
}
