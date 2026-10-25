using System.Text;

namespace SurfaceAuthoring.Core;

internal static class SurfaceRasterRunLength
{
    public static bool HasDecodedWidth(string encoded, int width)
    {
        if (encoded == null || width < 1) return false;
        var total = 0;
        for (var i = 0; i < encoded.Length;)
        {
            var glyph = encoded[i++];
            if (glyph is not ('.' or '~' or '=' or 'B' or '#')) return false;
            var count = 0;
            var digits = 0;
            while (i < encoded.Length && encoded[i] != ';')
            {
                var digit = encoded[i++];
                var value = digit >= '0' && digit <= '9' ? digit - '0' :
                    digit >= 'A' && digit <= 'F' ? digit - 'A' + 10 : -1;
                if (value < 0 || count > (width - value) / 16) return false;
                count = count * 16 + value;
                digits++;
            }
            if (i >= encoded.Length || encoded[i++] != ';' || digits == 0 ||
                count == 0 || total > width - count) return false;
            total += count;
        }
        return total == width;
    }

    public static string Encode(string row)
    {
        var result = new StringBuilder(row.Length / 4);
        for (var i = 0; i < row.Length;)
        {
            var glyph = row[i];
            var end = i + 1;
            while (end < row.Length && row[end] == glyph) end++;
            result.Append(glyph).Append((end - i).ToString("X")).Append(';');
            i = end;
        }
        return result.ToString();
    }
}
