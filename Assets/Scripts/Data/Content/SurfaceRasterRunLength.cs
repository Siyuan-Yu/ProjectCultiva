using System;
using System.Text;

namespace XianXia.Data.Content
{
    /// <summary>Checked-in geography row encoding for large continuous surfaces.</summary>
    internal static class SurfaceRasterRunLength
    {
        public static bool TryDecode(string encoded, int width, out string row)
        {
            row = string.Empty;
            if (encoded == null || width < 1) return false;
            var result = new StringBuilder(width);
            for (var cursor = 0; cursor < encoded.Length;)
            {
                var glyph = encoded[cursor++];
                var length = 0;
                var digits = 0;
                while (cursor < encoded.Length && encoded[cursor] != ';')
                {
                    var digit = encoded[cursor++];
                    var value = digit >= '0' && digit <= '9' ? digit - '0' :
                        digit >= 'A' && digit <= 'F' ? digit - 'A' + 10 : -1;
                    if (value < 0 || length > (width - value) / 16) return false;
                    length = length * 16 + value;
                    digits++;
                }
                if (cursor >= encoded.Length || encoded[cursor++] != ';' || digits == 0 ||
                    length <= 0 || result.Length > width - length) return false;
                result.Append(glyph, length);
            }
            if (result.Length != width) return false;
            row = result.ToString();
            return true;
        }
    }
}
