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
        static Sprite _missingPrefab;
        static Sprite _bed;
        static Sprite _meleeSlash;
        static Sprite _rangedBolt;
        static Sprite _caveFloor;

        /// <summary>Neutral stone fallback for cave ground; avoids the legacy RockTile road placeholder.</summary>
        public static Sprite CaveFloorSprite()
        {
            if (_caveFloor != null) return _caveFloor;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "HostCaveStoneFloor", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
            var colors = new Color[size * size];
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                // Periodic low-contrast stone grain; no opaque prop outline or repeating road stripes.
                var broad = Mathf.Sin(x * Mathf.PI * 2f / size) * Mathf.Cos(y * Mathf.PI * 2f / size);
                var grain = ((x * 37 + y * 53 + x * y * 7) % 17) / 16f - .5f;
                var shade = .34f + broad * .025f + grain * .025f;
                colors[y * size + x] = new Color(shade * .94f, shade, shade * .98f, 1f);
            }
            texture.SetPixels(colors); texture.Apply(false, true);
            _caveFloor = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 32f,
                0, SpriteMeshType.FullRect);
            return _caveFloor;
        }

        public static Sprite UnitSprite()
        {
            if (_unit != null)
                return _unit;
            var fromRes = Resources.Load<Sprite>("HostSprites/Unit");
            if (fromRes != null)
                return _unit = fromRes;
            return _unit = MakeOutlinedUnitSprite(24, 32, "HostUnit");
        }

        /// <summary>通用近战挥砍弧（程序化；暂作全员共用）。</summary>
        public static Sprite MeleeSlashSprite()
        {
            if (_meleeSlash != null)
                return _meleeSlash;
            var fromRes = Resources.Load<Sprite>("HostSprites/MeleeSlash");
            if (fromRes != null)
                return _meleeSlash = fromRes;
            return _meleeSlash = MakeSlashArcSprite(48, 24, "HostMeleeSlash");
        }

        /// <summary>统一远程弹道光核（程序化软圆；纱衣／日后远程共用）。</summary>
        public static Sprite RangedProjectileSprite()
        {
            if (_rangedBolt != null)
                return _rangedBolt;
            var fromRes = Resources.Load<Sprite>("HostSprites/RangedBolt");
            if (fromRes != null)
                return _rangedBolt = fromRes;
            return _rangedBolt = MakeSoftOrbSprite(32, "HostRangedBolt");
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
            return _ring = MakeRingSprite(48, new Color(0.15f, 1f, 0.35f, 1f), "HostRing");
        }

        /// <summary>MapLayout prefab 缺失时的占位图（洋红／黑棋盘格）。</summary>
        public static Sprite MissingPrefabSprite()
        {
            if (_missingPrefab != null)
                return _missingPrefab;
            var fromRes = Resources.Load<Sprite>("HostSprites/MissingPrefab");
            if (fromRes != null)
                return _missingPrefab = fromRes;
            return _missingPrefab = MakeCheckerboardSprite(32, 32, "HostMissingPrefab");
        }

        /// <summary>Neutral bed body; each runtime instance is colored from its parent room usage.</summary>
        public static Sprite BedSprite()
        {
            if (_bed != null)
                return _bed;
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                name = "HostBed",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var edge = new Color(0.18f, 0.14f, 0.12f, 1f);
            var pillow = new Color(0.78f, 0.78f, 0.76f, 1f);
            for (var y = 0; y < 32; y++)
            for (var x = 0; x < 32; x++)
            {
                var border = x < 2 || y < 2 || x >= 30 || y >= 30;
                var head = y >= 23 && x >= 5 && x < 27;
                tex.SetPixel(x, y, border ? edge : head ? pillow : Color.white);
            }
            tex.Apply(false, true);
            return _bed = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 32f);
        }

        /// <summary>White fill + dark outline so tinted units stay readable on grass／dirt.</summary>
        static Sprite MakeOutlinedUnitSprite(int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var fill = Color.white;
            var edge = new Color(0.08f, 0.08f, 0.1f, 1f);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                tex.SetPixel(x, y, border ? edge : fill);
            }

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.15f), 32f);
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

        static Sprite MakeCheckerboardSprite(int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var a = new Color(0.92f, 0.08f, 0.72f, 1f);
            var b = new Color(0.08f, 0.08f, 0.08f, 1f);
            var cell = Mathf.Max(4, w / 4);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var checker = ((x / cell) + (y / cell)) % 2 == 0;
                tex.SetPixel(x, y, checker ? a : b);
            }

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
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

        /// <summary>横向新月形挥砍（pivot 在弧心，运行时按攻击方向旋转拉伸）。</summary>
        static Sprite MakeSlashArcSprite(int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var cx = (w - 1) * 0.5f;
            var cy = (h - 1) * 0.5f;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var nx = (x - cx) / cx;
                var ny = (y - cy) / cy;
                // 椭圆环带 + 右半更亮，像一记横斩
                var ell = nx * nx * 0.55f + ny * ny;
                var band = ell > 0.22f && ell < 0.95f && nx > -0.85f;
                if (!band)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                var edge = 1f - Mathf.Abs(ell - 0.55f) / 0.45f;
                var tip = Mathf.Clamp01(0.35f + nx * 0.65f);
                var a = Mathf.Clamp01(edge * tip);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        }

        /// <summary>径向衰减软圆，作弹道光核／命中爆闪。</summary>
        static Sprite MakeSoftOrbSprite(int size, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var cx = (size - 1) * 0.5f;
            var maxR = size * 0.48f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x - cx;
                var dy = y - cx;
                var d = Mathf.Sqrt(dx * dx + dy * dy) / maxR;
                if (d >= 1f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                // 内核亮、外缘软；略扁尖头感靠运行时 scale
                var core = Mathf.Clamp01(1f - d * d);
                var halo = Mathf.Clamp01(1f - d);
                var a = Mathf.Clamp01(core * 0.85f + halo * 0.35f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
