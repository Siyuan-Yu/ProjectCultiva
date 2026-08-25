using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Player move route preview — only for ActiveControlledCharacter (Phase 1).
    /// View selection may include others; preview follows Command Authority only.
    /// </summary>
    public sealed class HostPartyPathPreview : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostMoveController moveController;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] Camera worldCamera;
        [SerializeField] float lineWidth = 0.08f;
        [SerializeField] Color activePathColor = new Color(0.32f, 0.88f, 0.52f, 0.92f);
        [SerializeField] Color cursorPreviewColor = new Color(0.55f, 0.82f, 1f, 0.65f);

        Transform _root;
        readonly List<LineRenderer> _pool = new List<LineRenderer>(8);
        readonly List<Vector3> _scratch = new List<Vector3>(64);
        static Material _sharedMaterial;
        int _used;

        void Awake()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<PlayableHostBootstrap>();
            if (moveController == null)
                moveController = GetComponent<HostMoveController>();
            if (selectionController == null)
                selectionController = GetComponent<HostSelectionController>();
            if (worldCamera == null)
                worldCamera = Camera.main;
        }

        public void Bind(
            PlayableHostBootstrap host,
            HostMoveController move,
            HostSelectionController selection,
            Camera cam = null)
        {
            bootstrap = host;
            moveController = move;
            selectionController = selection;
            if (cam != null)
                worldCamera = cam;
        }

        void LateUpdate()
        {
            _used = 0;
            if (!CanDraw())
            {
                HideUnused();
                return;
            }

            if (worldCamera == null)
                worldCamera = Camera.main;

            var active = ResolveActiveForPreview();
            if (active.IsNone)
            {
                HideUnused();
                return;
            }

            var aimingMove = IsAimingMoveOrder();
            if (aimingMove &&
                worldCamera != null &&
                HostPresentationSpace.TryRaycastPlane(worldCamera, Input.mousePosition, out var click))
            {
                if (TryGetViewPos(active, out var from) &&
                    moveController.TryBuildPathPreview(from, click, _scratch) &&
                    _scratch.Count >= 2)
                {
                    DrawPolyline(_scratch, cursorPreviewColor, taper: true);
                }
            }
            else if (moveController.IsPlayerPartyPathMoving(active) &&
                     moveController.TryGetRemainingPath(active, _scratch))
            {
                DrawPolyline(_scratch, activePathColor, taper: true);
            }

            HideUnused();
        }

        EntityId ResolveActiveForPreview() =>
            HostPlayerMoveCommandGate.ResolveActiveForWorldMove(
                selectionController,
                bootstrap?.Session?.PlayerParty);

        bool IsAimingMoveOrder()
        {
            if (!Input.GetMouseButton(1))
                return false;
            if (HostInputGate.BlockWorldInteraction)
                return false;
            if (HostUiHitTest.ContainsScreenPoint(Input.mousePosition))
                return false;
            if (Input.GetKey(KeyCode.LeftAlt))
                return false;

            var workMode = bootstrap != null ? bootstrap.WorkTargetMode : null;
            if (workMode != null && workMode.IsActive && workMode.Armed != HostWorkTargetMode.ArmKind.Move)
                return false;

            var menu = bootstrap != null ? bootstrap.GetComponent<HostNpcContextMenu>() : null;
            if (menu != null && menu.IsOpen)
                return false;

            if (!HostPlayerMoveCommandGate.IsActiveCommandContext(
                    selectionController,
                    bootstrap?.Session?.PlayerParty))
                return false;

            return true;
        }

        bool CanDraw()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return false;
            if (moveController == null || selectionController == null)
                return false;
            if (bootstrap.Session.World.ContentEvents.HasActive)
                return false;
            return true;
        }

        bool TryGetViewPos(EntityId id, out Vector3 pos)
        {
            pos = default;
            var spawner = selectionController.Spawner;
            if (spawner == null || !spawner.Registry.TryGet(id, out var view) || view == null)
                return false;
            pos = view.transform.position;
            pos.z = HostPresentationSpace.EntityZ;
            return true;
        }

        void DrawPolyline(List<Vector3> points, Color color, bool taper)
        {
            if (points == null || points.Count < 2)
                return;
            var line = RentLine();
            line.positionCount = points.Count;
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                p.z = HostPresentationSpace.EntityZ;
                line.SetPosition(i, p);
            }

            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, color.a * 0.35f);
            line.startWidth = lineWidth;
            line.endWidth = taper ? lineWidth * 0.45f : lineWidth;
            line.widthMultiplier = 1f;
            line.enabled = true;
        }

        LineRenderer RentLine()
        {
            EnsureRoot();
            while (_pool.Count <= _used)
            {
                var go = new GameObject("PathLine");
                go.transform.SetParent(_root, false);
                var lr = go.AddComponent<LineRenderer>();
                ConfigureLine(lr);
                _pool.Add(lr);
            }

            return _pool[_used++];
        }

        void HideUnused()
        {
            for (var i = _used; i < _pool.Count; i++)
            {
                if (_pool[i] != null)
                    _pool[i].enabled = false;
            }
        }

        void EnsureRoot()
        {
            if (_root != null)
                return;
            var existing = transform.Find("PartyPathPreviewRoot");
            if (existing != null)
            {
                _root = existing;
                return;
            }

            var go = new GameObject("PartyPathPreviewRoot");
            go.transform.SetParent(transform, false);
            _root = go.transform;
        }

        void ConfigureLine(LineRenderer line)
        {
            if (_sharedMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");
                _sharedMaterial = new Material(shader);
            }

            line.sharedMaterial = _sharedMaterial;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.useWorldSpace = true;
            line.sortingOrder = 5200;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
        }
    }
}
