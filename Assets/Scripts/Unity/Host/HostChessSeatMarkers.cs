using UnityEngine;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Unity.Host
{
    /// <summary>Read-only, ground-level use-cell marks at the same two targets consumed by Core.</summary>
    public sealed class HostChessSeatMarkers : MonoBehaviour
    {
        static Material _material;
        SimulationWorld _world;
        string _boardId;
        Rect _bounds;
        readonly Transform[] _seats = new Transform[2];
        readonly LineRenderer[,] _marks = new LineRenderer[2, 3];

        public void Bind(SimulationWorld world, string boardId, Rect bounds)
        {
            _world = world; _boardId = boardId;
            var geometry = GetComponent<HostMapVisualGeometry>();
            // Picking bounds include click padding; use the actual logical footprint.
            _bounds = geometry != null ? geometry.WorldRect : bounds;
            if (_material == null) _material = new Material(Shader.Find("Sprites/Default"));
            for (var seat = 0; seat < 2; seat++)
            {
                if (_seats[seat] != null) continue;
                var root = new GameObject(seat == 0 ? "UseCell_A" : "UseCell_B");
                root.transform.SetParent(transform, false); _seats[seat] = root.transform;
                _marks[seat, 0] = MakeLine(root.transform, "Cell", new[] {
                    new Vector3(-.5f,-.5f),new Vector3(.5f,-.5f),
                    new Vector3(.5f,.5f),new Vector3(-.5f,.5f)}, .018f);
                for (var foot = 0; foot < 2; foot++)
                {
                    var x = foot == 0 ? -.16f : .16f;
                    _marks[seat, foot + 1] = MakeLine(root.transform, foot == 0 ? "LeftFoot" : "RightFoot",
                        new[] { new Vector3(x-.075f,-.23f),new Vector3(x+.065f,-.23f),
                            new Vector3(x+.09f,.07f),new Vector3(x+.07f,.20f),
                            new Vector3(x,.25f),new Vector3(x-.08f,.20f),new Vector3(x-.10f,.06f)}, .03f);
                }
            }
            LateUpdate();
        }

        static LineRenderer MakeLine(Transform parent, string name, Vector3[] points, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.sharedMaterial = _material; line.useWorldSpace = false; line.loop = true;
            line.positionCount = points.Length; line.SetPositions(points);
            line.widthMultiplier = width; line.numCornerVertices = 3; line.numCapVertices = 2;
            line.sortingOrder = -10;
            return line;
        }

        void LateUpdate()
        {
            using var timing = HostDebugHud.ChessLateUpdate.Auto();
            if (_world == null || !_world.ChessBoards.TryGetValue(_boardId ?? string.Empty, out var board) ||
                !_world.SurfaceSpatial.TryGet(board.SurfaceId, out var metric)) return;
            var scale = _bounds.width; // Board footprint is exactly one logical cell.
            var parentScale = transform.lossyScale;
            _world.ChessGames.TryGetValue(_boardId, out var game);
            for (var i = 0; i < 2; i++)
            {
                if (_seats[i] == null) continue;
                var seat = i == 0 ? board.SeatA : board.SeatB;
                var dx = (seat.X - board.Center.X) / metric.CellSize * scale;
                var dy = (seat.Y - board.Center.Y) / metric.CellSize * scale;
                _seats[i].position = new Vector3(_bounds.center.x + dx, _bounds.center.y + dy, HostPresentationSpace.GroundZ);
                _seats[i].rotation = Quaternion.Euler(0, 0, Mathf.Atan2(-dy, -dx) * Mathf.Rad2Deg - 90f);
                _seats[i].localScale = new Vector3(scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
                    scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), 1f);
                var occupied = game != null && (i == 0 ? game.PlayerA : game.PlayerB) != 0;
                var color = occupied ? new Color(.25f,.8f,.72f,.65f) : new Color(.95f,.83f,.58f,.60f);
                for (var mark = 0; mark < 3; mark++)
                {
                    var tint = color; if (mark == 0) tint.a = .18f;
                    _marks[i, mark].startColor = _marks[i, mark].endColor = tint;
                }
            }
        }
    }
}
