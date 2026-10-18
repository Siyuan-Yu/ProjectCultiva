using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using XianXia.Core.Construction;

namespace XianXia.Unity.Host
{
    /// <summary>Source-pixel bed registration and a foreground cut from the original bed texture.</summary>
    public sealed class HostBedVisualLayers : MonoBehaviour
    {
        const string ForegroundName = "StepABedForeground";
        static readonly Dictionary<Sprite, Sprite> ForegroundSprites = new Dictionary<Sprite, Sprite>();
        static readonly Vector2[] ResidentialQuiltEdge =
        {
            new Vector2(0, 280), new Vector2(35, 279), new Vector2(50, 268),
            new Vector2(60, 263), new Vector2(75, 255), new Vector2(100, 252),
            new Vector2(150, 250), new Vector2(200, 251), new Vector2(250, 250),
            new Vector2(300, 250), new Vector2(325, 253), new Vector2(340, 258),
            new Vector2(350, 266), new Vector2(364, 280), new Vector2(403, 280)
        };
        static readonly Vector2[] MedicalQuiltEdge =
        {
            new Vector2(0, 282), new Vector2(40, 281), new Vector2(50, 274),
            new Vector2(60, 267), new Vector2(75, 259), new Vector2(100, 257),
            new Vector2(150, 254), new Vector2(200, 253), new Vector2(250, 254),
            new Vector2(300, 255), new Vector2(325, 258), new Vector2(340, 263),
            new Vector2(350, 270), new Vector2(364, 281), new Vector2(403, 281)
        };
        static readonly Vector2[] PrisonQuiltEdge =
        {
            new Vector2(0, 291), new Vector2(40, 289), new Vector2(50, 279),
            new Vector2(60, 272), new Vector2(75, 266), new Vector2(100, 261),
            new Vector2(150, 259), new Vector2(200, 257), new Vector2(250, 257),
            new Vector2(300, 260), new Vector2(325, 262), new Vector2(340, 265),
            new Vector2(350, 270), new Vector2(364, 279), new Vector2(378, 293),
            new Vector2(417, 293)
        };

        SpriteRenderer _bedRenderer;
        SpriteRenderer _foregroundRenderer;
        Sprite _configuredBedSprite;
        Vector2 _pillowSourcePixels;
        bool _configured;
        bool _sortingGroupReported;
        bool _sourceMismatchReported;

        public bool IsForegroundRenderer(SpriteRenderer renderer) => renderer != null &&
            (renderer == _foregroundRenderer || renderer.name == ForegroundName &&
             renderer.transform.parent != null && renderer.transform.parent.name == "StepAArtVisual");

        public void ClearConfiguration()
        {
            _configured = false;
            _bedRenderer = null;
            _configuredBedSprite = null;
            if (_foregroundRenderer == null)
            {
                var overlay = transform.Find("StepAArtVisual");
                var child = overlay != null ? overlay.Find(ForegroundName) : null;
                if (child != null) _foregroundRenderer = child.GetComponent<SpriteRenderer>();
            }
            if (_foregroundRenderer == null) return;
            _foregroundRenderer.enabled = false;
            _foregroundRenderer.sprite = null;
        }

        public bool Configure(SpriteRenderer bedRenderer, RoomUsage usage)
        {
            ClearConfiguration();
            if (bedRenderer == null || bedRenderer.sprite == null || !bedRenderer.enabled ||
                HasActiveSortingGroupAncestor(bedRenderer.transform)) return false;
            var sprite = bedRenderer.sprite;
            var texture = sprite.texture;
            var width = usage == RoomUsage.Prison ? 417 : 403;
            var height = usage == RoomUsage.Prison ? 734 : usage == RoomUsage.Medical ? 731 : 730;
            if (texture == null || texture.width != width || texture.height != height ||
                sprite.rect.x != 0f || sprite.rect.y != 0f ||
                sprite.rect.width != width || sprite.rect.height != height)
            {
                if (!_sourceMismatchReported)
                {
                    _sourceMismatchReported = true;
                    Debug.LogWarning("Bed foreground registration does not match its source texture: " +
                        gameObject.name, this);
                }
                return false;
            }
            var edge = usage == RoomUsage.Prison ? PrisonQuiltEdge :
                usage == RoomUsage.Medical ? MedicalQuiltEdge : ResidentialQuiltEdge;
            if (!ForegroundSprites.TryGetValue(sprite, out var foregroundSprite) || foregroundSprite == null)
            {
                foregroundSprite = CreateForegroundSprite(sprite, edge);
                ForegroundSprites[sprite] = foregroundSprite;
            }
            _bedRenderer = bedRenderer;
            _configuredBedSprite = sprite;
            _pillowSourcePixels = new Vector2(201f,
                usage == RoomUsage.Prison ? 156f : usage == RoomUsage.Medical ? 154f : 153f);
            if (_foregroundRenderer == null)
            {
                var child = new GameObject(ForegroundName);
                child.transform.SetParent(bedRenderer.transform, false);
                _foregroundRenderer = child.AddComponent<SpriteRenderer>();
            }
            var foregroundTransform = _foregroundRenderer.transform;
            foregroundTransform.SetParent(bedRenderer.transform, false);
            foregroundTransform.localPosition = Vector3.zero;
            foregroundTransform.localRotation = Quaternion.identity;
            foregroundTransform.localScale = Vector3.one;
            _foregroundRenderer.sprite = foregroundSprite;
            _configured = true;
            _sourceMismatchReported = false;
            RefreshForegroundStyle();
            return true;
        }

        public bool TryGetSleepFrame(out Vector3 pillowWorld, out Quaternion bedRotation,
            out int sortingLayerId, out int bedOrder)
        {
            pillowWorld = default;
            bedRotation = Quaternion.identity;
            sortingLayerId = 0;
            bedOrder = 0;
            if (_bedRenderer != null && _bedRenderer.sprite != _configuredBedSprite)
                ClearConfiguration();
            if (!enabled || !gameObject.activeInHierarchy || !_configured ||
                _bedRenderer == null || !_bedRenderer.enabled ||
                _bedRenderer.sprite == null || !_bedRenderer.gameObject.activeInHierarchy ||
                HasActiveSortingGroupAncestor(_bedRenderer.transform)) return false;
            RefreshForegroundStyle();
            var sprite = _bedRenderer.sprite;
            var local = SourcePoint(sprite, _pillowSourcePixels);
            if (_bedRenderer.flipX) local.x = -local.x;
            if (_bedRenderer.flipY) local.y = -local.y;
            pillowWorld = _bedRenderer.transform.TransformPoint(local);
            bedRotation = _bedRenderer.transform.rotation;
            sortingLayerId = _bedRenderer.sortingLayerID;
            bedOrder = _bedRenderer.sortingOrder;
            return true;
        }

        public bool TryGetBedBounds(out Bounds bounds)
        {
            bounds = default;
            if (!TryGetSleepFrame(out _, out _, out _, out _)) return false;
            bounds = _bedRenderer.bounds;
            return bounds.size.x > .0001f && bounds.size.y > .0001f;
        }

        bool HasActiveSortingGroupAncestor(Transform child)
        {
            for (var current = child; current != null; current = current.parent)
            {
                var group = current.GetComponent<SortingGroup>();
                if (group == null || !group.enabled || !group.gameObject.activeInHierarchy) continue;
                if (!_sortingGroupReported)
                {
                    _sortingGroupReported = true;
                    Debug.LogWarning("Bed cannot interleave its foreground and a sleeping character " +
                        "through an ancestor SortingGroup: " + gameObject.name, this);
                }
                if (_foregroundRenderer != null) _foregroundRenderer.enabled = false;
                return true;
            }
            return false;
        }

        void RefreshForegroundStyle()
        {
            if (!_configured || _bedRenderer == null || _foregroundRenderer == null) return;
            _foregroundRenderer.sharedMaterial = _bedRenderer.sharedMaterial;
            _foregroundRenderer.color = _bedRenderer.color;
            _foregroundRenderer.flipX = _bedRenderer.flipX;
            _foregroundRenderer.flipY = _bedRenderer.flipY;
            _foregroundRenderer.sortingLayerID = _bedRenderer.sortingLayerID;
            _foregroundRenderer.sortingOrder = _bedRenderer.sortingOrder + 2;
            _foregroundRenderer.enabled = _bedRenderer.enabled;
        }

        void OnEnable()
        {
            if (_configured && _bedRenderer != null &&
                !HasActiveSortingGroupAncestor(_bedRenderer.transform)) RefreshForegroundStyle();
        }

        void OnDisable()
        {
            if (_foregroundRenderer != null) _foregroundRenderer.enabled = false;
        }

        static Vector2 SourcePoint(Sprite sprite, Vector2 sourcePixels) => new Vector2(
            (sourcePixels.x - sprite.pivot.x) / sprite.pixelsPerUnit,
            (sprite.rect.height - sourcePixels.y - sprite.pivot.y) / sprite.pixelsPerUnit);

        static Sprite CreateForegroundSprite(Sprite source, Vector2[] upperEdge)
        {
            var result = Sprite.Create(source.texture, source.rect,
                new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height),
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            result.name = source.texture.name + "_QuiltForeground";
            var vertices = new Vector2[upperEdge.Length * 2];
            var triangles = new ushort[(upperEdge.Length - 1) * 6];
            for (var i = 0; i < upperEdge.Length; i++)
            {
                vertices[i * 2] = SourcePoint(source, upperEdge[i]);
                vertices[i * 2 + 1] = SourcePoint(source,
                    new Vector2(upperEdge[i].x, source.rect.height));
                if (i == upperEdge.Length - 1) continue;
                var index = i * 6;
                triangles[index] = (ushort)(i * 2);
                triangles[index + 1] = (ushort)(i * 2 + 1);
                triangles[index + 2] = (ushort)(i * 2 + 2);
                triangles[index + 3] = (ushort)(i * 2 + 2);
                triangles[index + 4] = (ushort)(i * 2 + 1);
                triangles[index + 5] = (ushort)(i * 2 + 3);
            }
            result.OverrideGeometry(vertices, triangles);
            return result;
        }
    }
}
