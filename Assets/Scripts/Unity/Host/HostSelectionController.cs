using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// VS0.4 Phase C: RTS click／box selection over EntityViews. No PlayerCommand／Port.
    /// </summary>
    public sealed class HostSelectionController : MonoBehaviour
    {
        [SerializeField] Camera selectionCamera;
        [SerializeField] EntityViewSpawner spawner;
        [SerializeField] float dragThresholdPixels = 6f;
        [SerializeField] KeyCode additiveKey = KeyCode.LeftShift;

        readonly HostSelectionState _state = new HostSelectionState();
        readonly List<EntityId> _boxBuffer = new List<EntityId>();
        readonly HashSet<ulong> _partyFilter = new HashSet<ulong>();

        bool _pointerDown;
        bool _dragging;
        Vector2 _pressScreen;
        Vector2 _currentScreen;
        float _lastClickTime = -1f;
        EntityId _lastClickId = EntityId.None;
        const float DoubleClickSeconds = 0.35f;

        public HostSelectionState State => _state;

        public EntityViewSpawner Spawner => spawner;

        public bool IsBoxSelecting => _dragging;

        public Rect CurrentBoxScreenRect => BuildScreenRect(_pressScreen, _currentScreen);

        public void Bind(EntityViewSpawner viewSpawner, Camera camera)
        {
            spawner = viewSpawner;
            selectionCamera = camera != null ? camera : Camera.main;
            ClearSelection();
        }

        /// <summary>Demo double-click selects this party set only.</summary>
        public void SetPartyFilter(IReadOnlyList<EntityId> partyIds)
        {
            _partyFilter.Clear();
            if (partyIds == null)
                return;
            for (var i = 0; i < partyIds.Count; i++)
            {
                if (!partyIds[i].IsNone)
                    _partyFilter.Add(partyIds[i].Value);
            }
        }

        public void ClearSelection()
        {
            _state.Clear();
            ApplyHighlights();
            CancelGesture();
        }

        void Update()
        {
            if (spawner == null || selectionCamera == null)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                if (HostUiHitTest.ContainsScreenPoint(Input.mousePosition))
                {
                    CancelGesture();
                    return;
                }

                _pointerDown = true;
                _dragging = false;
                _pressScreen = Input.mousePosition;
                _currentScreen = _pressScreen;
            }

            if (_pointerDown && Input.GetMouseButton(0))
            {
                _currentScreen = Input.mousePosition;
                if (!_dragging &&
                    Vector2.Distance(_pressScreen, _currentScreen) >= dragThresholdPixels)
                {
                    _dragging = true;
                }
            }

            if (_pointerDown && Input.GetMouseButtonUp(0))
            {
                _currentScreen = Input.mousePosition;
                // 松手仍在 HUD 上：不点选／不清空（避免点指令钮把角色面板关掉）
                if (!_dragging && HostUiHitTest.ContainsScreenPoint(_currentScreen))
                {
                    CancelGesture();
                    return;
                }

                if (_dragging)
                {
                    // Box select always replaces the set (no Shift+box append).
                    SelectByBoxScreen(BuildScreenRect(_pressScreen, _currentScreen));
                }
                else
                {
                    var shift = Input.GetKey(additiveKey) || Input.GetKey(KeyCode.RightShift);
                    HandlePointSelect(_pressScreen, shift);
                }

                CancelGesture();
            }
        }

        void HandlePointSelect(Vector2 screenPoint, bool shiftToggle)
        {
            if (!TryPickEntityAtScreenPoint(screenPoint, out var best))
            {
                if (!shiftToggle)
                {
                    _state.Clear();
                    ApplyHighlights();
                }

                _lastClickId = EntityId.None;
                return;
            }

            // Demo [49]: double-click own unit → select all party characters.
            var now = Time.unscaledTime;
            if (!shiftToggle &&
                best.EntityId == _lastClickId &&
                now - _lastClickTime <= DoubleClickSeconds &&
                spawner != null)
            {
                SelectAllBoundCharacters();
                _lastClickTime = -1f;
                _lastClickId = EntityId.None;
                return;
            }

            // Demo: only party is commandable. Plain-click NPC = inspect select only;
            // Shift+click NPC allowed as social target. Box／double-click stay party-only.
            if (!IsPartyUnit(best.EntityId) && !shiftToggle)
            {
                _state.ReplaceOne(best.EntityId);
                ApplyHighlights();
                _lastClickTime = now;
                _lastClickId = best.EntityId;
                return;
            }

            if (!IsPartyUnit(best.EntityId) && shiftToggle)
            {
                _state.Toggle(best.EntityId);
                ApplyHighlights();
                return;
            }

            _lastClickTime = now;
            _lastClickId = best.EntityId;

            if (shiftToggle)
                _state.Toggle(best.EntityId);
            else
                _state.ReplaceOne(best.EntityId);
            ApplyHighlights();
        }

        public bool IsPartyUnit(EntityId id)
        {
            if (id.IsNone)
                return false;
            // No filter configured (unit tests) → all bound views treated as selectable.
            if (_partyFilter.Count == 0)
                return true;
            return _partyFilter.Contains(id.Value);
        }

        void SelectAllBoundCharacters()
        {
            _boxBuffer.Clear();
            foreach (var view in spawner.Registry.All)
            {
                if (view == null || !view.IsBound)
                    continue;
                if (_partyFilter.Count > 0 && !_partyFilter.Contains(view.EntityId.Value))
                    continue;
                _boxBuffer.Add(view.EntityId);
            }

            if (_boxBuffer.Count == 0)
                return;
            _state.Replace(_boxBuffer);
            ApplyHighlights();
        }

        void OnGUI()
        {
            if (!_dragging)
                return;

            var rect = CurrentBoxScreenRect;
            // Convert to GUI space (y flipped).
            var guiRect = new Rect(
                rect.xMin,
                Screen.height - rect.yMax,
                rect.width,
                rect.height);
            var fill = new Color(0.2f, 0.6f, 1f, 0.15f);
            var border = new Color(0.2f, 0.6f, 1f, 0.85f);
            DrawScreenRect(guiRect, fill);
            DrawScreenRectBorder(guiRect, border, 2f);
        }

        /// <summary>Point select: shift toggles; otherwise replaces.</summary>
        public bool TrySelectAtScreenPoint(Vector2 screenPoint, bool shiftToggle)
        {
            if (spawner == null || selectionCamera == null)
                return false;

            if (!TryPickEntityAtScreenPoint(screenPoint, out var best))
            {
                if (!shiftToggle)
                {
                    _state.Clear();
                    ApplyHighlights();
                }

                return false;
            }

            if (shiftToggle)
                _state.Toggle(best.EntityId);
            else
                _state.ReplaceOne(best.EntityId);

            ApplyHighlights();
            return true;
        }

        /// <summary>Direct selection helper for tests／adapters (still Host-layer only).</summary>
        public bool SelectEntity(EntityId id, bool shiftToggle)
        {
            if (spawner == null || !spawner.Registry.Contains(id))
                return false;

            if (shiftToggle)
                _state.Toggle(id);
            else
                _state.ReplaceOne(id);

            ApplyHighlights();
            return true;
        }

        public bool TryPickEntityAtScreenPoint(Vector2 screenPoint, out EntityView best)
        {
            best = null;
            if (spawner == null || selectionCamera == null)
                return false;

            var ray = selectionCamera.ScreenPointToRay(new Vector3(screenPoint.x, screenPoint.y, 0f));
            var bestDist = float.MaxValue;
            foreach (var candidate in spawner.Registry.All)
            {
                if (candidate == null || !candidate.IsBound)
                    continue;

                float dist = 0f;
                var rend = candidate.GetComponentInChildren<Renderer>();
                var hit = rend != null && rend.bounds.extents.sqrMagnitude > 0.0001f &&
                          rend.bounds.IntersectRay(ray, out dist);
                if (!hit)
                {
                    // Fallback: capsule approximate sphere (EditMode bounds can be empty before play).
                    var radius = 0.75f * Mathf.Max(
                        candidate.transform.lossyScale.x,
                        candidate.transform.lossyScale.y);
                    hit = RayHitsSphere(ray, candidate.transform.position, radius, out dist);
                }

                if (!hit || dist < 0f || dist >= bestDist)
                    continue;

                bestDist = dist;
                best = candidate;
            }

            return best != null;
        }

        static bool RayHitsSphere(Ray ray, Vector3 center, float radius, out float distance)
        {
            var oc = ray.origin - center;
            var b = Vector3.Dot(oc, ray.direction);
            var c = Vector3.Dot(oc, oc) - radius * radius;
            var discriminant = b * b - c;
            if (discriminant < 0f)
            {
                distance = 0f;
                return false;
            }

            var sqrt = Mathf.Sqrt(discriminant);
            var t = -b - sqrt;
            if (t < 0f)
                t = -b + sqrt;
            distance = t;
            return t >= 0f;
        }

        /// <summary>Box select always covers／replaces the current selection.</summary>
        public void SelectByBoxScreen(Rect screenRect)
        {
            if (spawner == null || selectionCamera == null)
                return;

            _boxBuffer.Clear();
            foreach (var view in spawner.Registry.All)
            {
                if (view == null || !view.IsBound)
                    continue;
                if (!IsPartyUnit(view.EntityId))
                    continue;

                var screen = selectionCamera.WorldToScreenPoint(view.transform.position);
                if (screen.z < 0f)
                    continue;

                var p = new Vector2(screen.x, screen.y);
                if (screenRect.Contains(p))
                    _boxBuffer.Add(view.EntityId);
            }

            _state.Replace(_boxBuffer);
            ApplyHighlights();
        }

        public void ApplyHighlights()
        {
            if (spawner == null)
                return;

            foreach (var view in spawner.Registry.All)
            {
                if (view == null)
                    continue;
                view.SetHighlight(_state.Contains(view.EntityId));
            }
        }

        void CancelGesture()
        {
            _pointerDown = false;
            _dragging = false;
        }

        static Rect BuildScreenRect(Vector2 a, Vector2 b)
        {
            var minX = Mathf.Min(a.x, b.x);
            var maxX = Mathf.Max(a.x, b.x);
            var minY = Mathf.Min(a.y, b.y);
            var maxY = Mathf.Max(a.y, b.y);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        static void DrawScreenRect(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void DrawScreenRectBorder(Rect rect, Color color, float thickness)
        {
            DrawScreenRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            DrawScreenRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            DrawScreenRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
            DrawScreenRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        }
    }
}
