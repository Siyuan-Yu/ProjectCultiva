using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>Presentation only: item silhouette never changes the logical footprint or blocks movement.</summary>
    public static class HostGroundLootVisual
    {
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static void Apply(GameObject root, SimulationWorld world, string itemId, string label, float size, int count = 1)
        {
            if (root == null || root.transform.Find("GroundItemArt") != null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            var art = new GameObject("GroundItemArt"); art.transform.SetParent(root.transform, false);
            if (root.TryGetComponent<HostMapVisualGeometry>(out var geometry))
            {
                var footprint = geometry.WorldRect;
                art.transform.position = new Vector3(footprint.center.x, footprint.center.y, root.transform.position.z);
                size = Mathf.Max(.1f, Mathf.Min(footprint.width, footprint.height) * .8f);
            }
            var scale = root.transform.lossyScale;
            art.transform.localScale = new Vector3(size / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                size / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 1f);
            var order = HostArtDepth.Order(art.transform.position.y) - 2;
            var shadow = new GameObject("Shadow"); shadow.transform.SetParent(art.transform, false);
            shadow.transform.localPosition = new Vector3(0f, -.27f, .01f);
            shadow.transform.localScale = new Vector3(1.15f, .24f, 1f);
            var sr = shadow.AddComponent<SpriteRenderer>(); sr.sprite = SpriteFor("shadow", null);
            sr.sortingOrder = order - 1; sr.color = new Color(0f, 0f, 0f, .32f);
            var body = art.AddComponent<SpriteRenderer>(); body.sprite = SpriteFor(itemId, HostStepAArt.ItemIcon(itemId));
            body.sortingOrder = order;
            if (string.IsNullOrEmpty(label) && world != null && world.InventoryCatalog.TryGet(itemId, out var info)) label = info.Name;
            var caption = new GameObject("ItemName"); caption.transform.SetParent(art.transform, false);
            caption.transform.localPosition = new Vector3(0f, .57f, -.02f);
            var text = caption.AddComponent<TextMesh>(); text.text = (string.IsNullOrEmpty(label) ? "物品" : label) + (count > 1 ? " ×" + count : "");
            text.fontSize = 32; text.characterSize = .085f; text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center; text.color = new Color(1f, .94f, .72f);
            caption.GetComponent<MeshRenderer>().sortingOrder = order + 1;
        }

        static Sprite SpriteFor(string id, Texture2D icon)
        {
            if (Sprites.TryGetValue(id, out var cached)) return cached;
            if (icon != null)
            {
                cached = Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height), new Vector2(.5f, .5f), Mathf.Max(icon.width, icon.height));
                Sprites[id] = cached; return cached;
            }
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "GroundItem_" + id };
            var pixels = new Color[32 * 32];
            var kind = HostStepAArt.ItemGroundKind(id);
            var scroll = kind == "scroll";
            var book = kind == "book";
            var herb = kind == "herb";
            for (var y = 0; y < 32; y++) for (var x = 0; x < 32; x++)
            {
                Color c = Color.clear;
                if (id == "shadow") { if ((x - 16) * (x - 16) + (y - 16) * (y - 16) < 210) c = Color.white; }
                else if (scroll)
                {
                    if (x >= 5 && x <= 26 && y >= 7 && y <= 24) c = new Color(.89f, .8f, .59f);
                    if ((x >= 3 && x <= 6 || x >= 25 && x <= 28) && y >= 5 && y <= 26) c = new Color(.47f, .24f, .12f);
                    if (x >= 15 && x <= 17 && y >= 7 && y <= 24) c = new Color(.66f, .2f, .14f);
                }
                else if (book)
                {
                    if (x >= 6 && x <= 26 && y >= 3 && y <= 28) c = new Color(.22f, .34f, .47f);
                    if (x >= 7 && x <= 9 && y >= 4 && y <= 27) c = new Color(.84f, .76f, .56f);
                    if (x >= 17 && x <= 23 && y >= 13 && y <= 24) c = new Color(.94f, .87f, .68f);
                    if (x >= 19 && x <= 21 && y >= 16 && y <= 22) c = new Color(.23f, .24f, .2f);
                }
                else if (herb)
                {
                    if (x >= 14 && x <= 17 && y >= 3 && y <= 24) c = new Color(.3f, .46f, .2f);
                    if ((x - 10) * (x - 10) + (y - 19) * (y - 19) < 35 ||
                        (x - 22) * (x - 22) + (y - 24) * (y - 24) < 32 ||
                        (x - 21) * (x - 21) + (y - 12) * (y - 12) < 25) c = new Color(.4f, .64f, .27f);
                    if (x >= 11 && x <= 20 && y >= 6 && y <= 8) c = new Color(.85f, .72f, .39f);
                }
                else
                {
                    if ((x - 16) * (x - 16) + (y - 13) * (y - 13) < 140 && y >= 3) c = new Color(.6f, .43f, .27f);
                    if (x >= 10 && x <= 22 && y >= 23 && y <= 26) c = new Color(.83f, .69f, .42f);
                    if (x >= 15 && x <= 17 && y >= 6 && y <= 25) c = new Color(.36f, .23f, .13f);
                }
                pixels[y * 32 + x] = c;
            }
            texture.SetPixels(pixels); texture.Apply();
            cached = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 32);
            Sprites[id] = cached; return cached;
        }
    }
}
