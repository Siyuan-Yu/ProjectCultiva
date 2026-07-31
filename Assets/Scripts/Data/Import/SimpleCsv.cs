using System;
using System.Collections.Generic;
using System.Text;

namespace XianXia.Data.Import
{
    /// <summary>
    /// Minimal RFC4180-ish CSV reader. No Excel libraries.
    /// </summary>
    public static class SimpleCsv
    {
        public static List<Dictionary<string, string>> Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var rows = new List<List<string>>();
            var current = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        current.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        current.Add(field.ToString());
                        field.Clear();
                        rows.Add(current);
                        current = new List<string>();
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }

            if (inQuotes)
                throw new FormatException("Unterminated CSV quoted field.");

            if (field.Length > 0 || current.Count > 0)
            {
                current.Add(field.ToString());
                rows.Add(current);
            }

            var result = new List<Dictionary<string, string>>();
            if (rows.Count == 0)
                return result;

            var header = rows[0];
            for (var r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0]))
                    continue;

                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var c = 0; c < header.Count; c++)
                {
                    var key = header[c].Trim();
                    if (string.IsNullOrEmpty(key))
                        continue;
                    map[key] = c < row.Count ? row[c].Trim() : string.Empty;
                }

                result.Add(map);
            }

            return result;
        }
    }
}
