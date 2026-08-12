using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Procedural sprites for Host when Demo prefabs are unavailable (EditMode／missing art).
    /// Prefers Resources under HostSprites/ when present.
    /// </summary>
    public static class HostSpriteFactory
    {
        static Sprite _unit;
        static Sprite _tile;
        static Sprite _ring;

        public static Sprite UnitSprite()
        {
            if (_unit != null)
                return _unit;
            var fromRes = Resources.Load<Sprite>("HostSprites/Unit");
            if (fromRes != null)
                return _unit = fromRes;
            return _unit = MakeSolidSprite(24, 32, new Color(0.85f, 0.85f, 0.9f, 1f), "HostUnit");
        }

        public static Sprite TileSprite()
        {
            if (_tile != null)
                return _tile;
            var fromRes = Resources.Load<Sprite>("HostSprites/Tile");
            if (fromRes != null)
                return _tile = fromRes;
            return _tile = MakeSolidSprite(32, 32, Color.white, "HostTile", new Vector2(0.5f, 0.5f));
        }

        public static Sprite SelectionRingSprite()
        {
            if (_ring != null)
                return _ring;
            var fromRes = Resources.Load<Sprite>("HostSprites/SelectRing");
            if (fromRes != null)
                return _ring = fromRes;
            return _ring = MakeRingSprite(48, new Color(0.3f, 0.95f, 0.35f, 0.9f), "HostRing");
        }

        static Sprite MakeSolidSprite(int w, int h, Color color, string name) =>
            MakeSolidSprite(w, h, color, name, new Vector2(0.5f, 0.15f));

        static Sprite MakeSolidSprite(int w, int h, Color color, string name, Vector2 pivot)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[w * h];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 32f);
        }

        static Sprite MakeRingSprite(int size, Color color, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point
            };
            var cx = (size - 1) * 0.5f;
            var outer = size * 0.45f;
            var inner = size * 0.32f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x - cx;
                var dy = y - cx;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, d <= outer && d >= inner ? color : Color.clear);
            }

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
