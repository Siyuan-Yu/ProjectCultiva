using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Unity.Host
{
    /// <summary>Map input and preview only. Core validates and persists the route on Save.</summary>
    public sealed class HostPatrolRouteEditor : MonoBehaviour
    {
        const string Owner = "PatrolRouteEditor";
        PlayableHostBootstrap _host;
        SimulationWorld _world;
        string _siteId;
        readonly List<WorldVec2> _points = new List<WorldVec2>();
        bool _open, _placing;
        int _openedFrame;
        Vector2 _scroll;
        string _status = string.Empty;
        GUIStyle _body, _number;

        public void Open(PlayableHostBootstrap host, string siteId)
        {
            Close();
            if (host?.Session?.World == null || string.IsNullOrEmpty(siteId)) return;
            _host = host; _world = host.Session.World; _siteId = siteId;
            _points.Clear(); _points.AddRange(SettlementPatrolRoutes.Get(_world, siteId));
            _scroll = Vector2.zero; _status = string.Empty; _placing = false; _open = true;
            _openedFrame = Time.frameCount;
            host.Session.AcquireModalPause(Owner); HostInputGate.AcquireInteraction(Owner);
        }

        public void Close()
        {
            if (_open && _host?.Session != null) _host.Session.ReleaseModalPause(Owner);
            HostInputGate.ReleaseInteraction(Owner); _open = false; _placing = false;
        }
        void OnDisable() => Close();
        void OnDestroy() => Close();

        Rect Panel => new Rect(Mathf.Max(12f, Screen.width - 396f), 24f,
            Mathf.Min(380f, Screen.width - 24f), Mathf.Min(680f, Screen.height - 48f));

        void Update()
        {
            if (!_open) return;
            if (_host?.Session?.World != _world || _host.WorldMapPanel?.IsOpen == true ||
                _host.InventoryPanel?.IsOpen == true || _host.ConstructionPanel?.IsOpen == true ||
                _host.MortalWorkPanel?.IsOpen == true || _host.QuestJournal?.IsOpen == true)
            { Close(); return; }
            HostInputGate.AcquireInteraction(Owner);
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (Input.GetMouseButtonDown(1)) { _placing = false; return; }
            var guiPoint = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (!_placing || Time.frameCount <= _openedFrame || !Input.GetMouseButtonDown(0) ||
                Panel.Contains(guiPoint) || HostUiHitTest.ContainsScreenPoint(Input.mousePosition)) return;
            var surface = _host.ContinuousOutdoorSurfaceRuntime;
            if (surface == null || !surface.IsActive ||
                !_world.Strategic.Sites.TryGet(_siteId, out var site) || surface.ActiveSurfaceId != site.CoreSurfaceId ||
                !HostPresentationSpace.TryRaycastPlane(Camera.main, Input.mousePosition, out var point))
            { _status = "请在本城的地面地图上添加巡视点。"; return; }
            surface.Mapper.PresentationToWorld(point.x, point.y, out var x, out var y);
            var worldPoint = new WorldVec2(x, y);
            if (_points.Count >= SettlementPatrolRoutes.MaxPoints)
            { _status = "巡视点已达64个。"; return; }
            if (!SettlementPatrolRoutes.CanEdit(_world, _siteId) ||
                !SettlementPatrolRoutes.ValidatePoint(_world, _siteId, worldPoint, out _status)) return;
            var grid = _host.MoveController?.WalkGrid;
            if (grid == null || !grid.TryWorldToCell(point.x, point.y, out var cx, out var cy) || !grid.IsWalkable(cx, cy))
            { _status = "这里有障碍或尚未加载，请选择空地。"; return; }
            _points.Add(worldPoint); _status = "已添加第" + _points.Count + "点；保存后生效。";
        }

        void OnGUI()
        {
            if (!_open || _world == null) return;
            if (_body == null)
            {
                _body = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 14 };
                _number = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            }
            DrawMarkers();
            var panel = Panel; HostUiHitTest.Block(panel); GUI.Box(panel, GUIContent.none);
            var x = panel.x + 12f; var w = panel.width - 24f;
            var name = _world.Strategic.Sites.TryGet(_siteId, out var site) ? site.DisplayName : _siteId;
            GUI.Label(new Rect(x, panel.y + 10f, w, 28f), name + " · 巡视点", _body);
            GUI.Label(new Rect(x, panel.y + 40f, w, 58f),
                "巡卫按编号循环，主管随机选点。到点短暂停留，点越多停得越短。当前 " + _points.Count +
                (_points.Count == 0 ? " 点，等待设置。" : " 点，停留约 " + SettlementPatrolRoutes.WaitTicks(_points.Count) + " 秒（1倍速）。"), _body);
            var editable = SettlementPatrolRoutes.CanEdit(_world, _siteId);
            var enabled = GUI.enabled;
            GUI.enabled = enabled && editable;
            if (GUI.Button(new Rect(x, panel.y + 104f, w * .57f, 30f), _placing ? "结束地图添加" : "在地图上添加"))
            { _placing = !_placing; _openedFrame = Time.frameCount; }
            if (GUI.Button(new Rect(x + w * .6f, panel.y + 104f, w * .4f, 30f), "清空所有点"))
            { _points.Clear(); _status = "草稿已清空，保存后停止按旧点巡视。"; }
            GUI.enabled = enabled;
            GUI.Label(new Rect(x, panel.y + 140f, w, 42f), !editable ? "占领此城后可编辑；当前只读。" :
                _placing ? "左键空地加点，右键结束。中键拖图、滚轮缩放。" : "可上下调序、删除；保存后生效。", _body);
            var list = new Rect(x, panel.y + 188f, w, Mathf.Max(40f, panel.height - 296f));
            _scroll = GUI.BeginScrollView(list, _scroll, new Rect(0, 0, w - 18f, _points.Count * 38f));
            for (var i = 0; i < _points.Count; i++)
            {
                var row = i * 38f;
                GUI.Label(new Rect(4, row + 4, w - 160f, 28), "巡视点 " + (i + 1), _body);
                GUI.enabled = enabled && editable && i > 0;
                if (GUI.Button(new Rect(w - 156f, row, 40, 30), "↑"))
                { var p = _points[i]; _points[i] = _points[i-1]; _points[i-1] = p; }
                GUI.enabled = enabled && editable && i + 1 < _points.Count;
                if (GUI.Button(new Rect(w - 112f, row, 40, 30), "↓"))
                { var p = _points[i]; _points[i] = _points[i+1]; _points[i+1] = p; }
                GUI.enabled = enabled && editable;
                if (GUI.Button(new Rect(w - 68f, row, 48, 30), "删除")) { _points.RemoveAt(i); break; }
            }
            GUI.enabled = enabled; GUI.EndScrollView();
            GUI.Label(new Rect(x, panel.yMax - 100f, w, 52f), _points.Count == 0 && string.IsNullOrEmpty(_status)
                ? "未设置巡视点。" : _status, _body);
            GUI.enabled = enabled && editable;
            if (GUI.Button(new Rect(x, panel.yMax - 42f, w * .57f, 30), "保存路线"))
            {
                var result = SettlementPatrolRoutes.Save(_world, _siteId, _points);
                _status = result.IsSuccess ? "巡视路线已保存。" : result.Error.Message;
                if (result.IsSuccess) _placing = false;
            }
            GUI.enabled = enabled;
            if (GUI.Button(new Rect(x + w * .6f, panel.yMax - 42f, w * .4f, 30), "关闭")) Close();
        }

        void DrawMarkers()
        {
            var surface = _host.ContinuousOutdoorSurfaceRuntime; var camera = Camera.main;
            if (surface == null || !surface.IsActive || camera == null ||
                !_world.Strategic.Sites.TryGet(_siteId, out var site) || surface.ActiveSurfaceId != site.CoreSurfaceId) return;
            var screenPoints = new List<Vector2>();
            foreach (var point in _points)
            {
                if (!surface.TryWorldToPresentation(point, out var p)) return;
                var screen = camera.WorldToScreenPoint(p);
                screenPoints.Add(new Vector2(screen.x, Screen.height - screen.y));
            }
            var color = GUI.color; GUI.color = new Color(1f, .8f, .15f, .85f);
            if (screenPoints.Count > 1)
                for (var i = 0; i < screenPoints.Count; i++)
                {
                    var a = screenPoints[i]; var b = screenPoints[(i+1)%screenPoints.Count];
                    var matrix = GUI.matrix;
                    GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y, b.x-a.x) * Mathf.Rad2Deg, a);
                    GUI.DrawTexture(new Rect(a.x, a.y, Vector2.Distance(a,b), 2), Texture2D.whiteTexture);
                    GUI.matrix = matrix;
                }
            for (var i = 0; i < screenPoints.Count; i++)
                GUI.Box(new Rect(screenPoints[i].x-14, screenPoints[i].y-14, 28, 28), (i+1).ToString(), _number);
            GUI.color = color;
        }
    }
}
