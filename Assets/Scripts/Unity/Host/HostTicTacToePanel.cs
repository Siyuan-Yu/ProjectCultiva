using System;
using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 简易井字棋：玩家先手（X），将老后手（O）。每日一局由对话侧用 dailyFlag 限制。
    /// </summary>
    public sealed class HostTicTacToePanel : MonoBehaviour
    {
        public enum ResultKind
        {
            None = 0,
            Win = 1,
            Lose = 2,
            Draw = 3
        }

        [SerializeField] PlayableHostBootstrap bootstrap;

        bool _open;
        bool _holdingPause;
        readonly int[] _board = new int[9]; // 0 empty, 1 player X, 2 npc O
        bool _playerTurn = true;
        bool _finished;
        ResultKind _result;
        string _status = string.Empty;
        EntityId _subject = EntityId.None;
        Action<ResultKind> _onFinished;
        Texture2D _px;
        GUIStyle _title;
        GUIStyle _cell;
        GUIStyle _body;

        static readonly Color Parchment = new Color(0.92f, 0.86f, 0.74f, 0.98f);
        static readonly Color ParchmentDark = new Color(0.70f, 0.58f, 0.42f, 1f);
        static readonly Color Ink = new Color(0.16f, 0.12f, 0.08f, 1f);

        public bool IsOpen => _open;

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState()
        {
            _open = false;
            _onFinished = null;
            _subject = EntityId.None;
            ReleasePause();
        }

        public void Open(EntityId subject, Action<ResultKind> onFinished)
        {
            _subject = subject;
            _onFinished = onFinished;
            for (var i = 0; i < 9; i++)
                _board[i] = 0;
            _playerTurn = true;
            _finished = false;
            _result = ResultKind.None;
            _status = "你执先手（X）。连成三子即胜。";
            _open = true;
        }

        void Update()
        {
            if (!_open)
            {
                ReleasePause();
                return;
            }

            HostInputGate.BlockWorldCamera = true;
            HostInputGate.BlockWorldInteraction = true;
            if (!_holdingPause && bootstrap?.Session != null)
            {
                bootstrap.Session.IsPaused = true;
                _holdingPause = true;
            }
        }

        void OnGUI()
        {
            if (!_open)
                return;
            EnsureStyles();

            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            Fill(dim, new Color(0f, 0f, 0f, 0.45f));
            HostUiHitTest.Block(dim);

            var w = 360f;
            var h = 420f;
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(rect);
            Fill(rect, Parchment);
            DrawFrame(rect, ParchmentDark);

            GUI.Label(new Rect(rect.x + 16f, rect.y + 12f, rect.width - 32f, 28f), "与将老对弈·井字棋", _title);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 44f, rect.width - 32f, 40f), _status, _body);

            var cell = 72f;
            var gap = 8f;
            var gridW = cell * 3f + gap * 2f;
            var ox = rect.x + (rect.width - gridW) * 0.5f;
            var oy = rect.y + 100f;
            for (var i = 0; i < 9; i++)
            {
                var row = i / 3;
                var col = i % 3;
                var r = new Rect(ox + col * (cell + gap), oy + row * (cell + gap), cell, cell);
                var mark = _board[i] == 1 ? "X" : (_board[i] == 2 ? "O" : "·");
                GUI.enabled = !_finished && _playerTurn && _board[i] == 0;
                if (HostImguiStyles.ParchmentBtn(r, mark) && _board[i] == 0)
                    PlayPlayer(i);
                GUI.enabled = true;
            }

            if (_finished)
            {
                var label = _result == ResultKind.Win ? "你赢了" :
                    (_result == ResultKind.Lose ? "将老胜了" : "和棋");
                GUI.Label(new Rect(rect.x + 16f, rect.yMax - 84f, rect.width - 32f, 24f), label, _body);
                if (HostImguiStyles.ParchmentBtn(
                        new Rect(rect.x + (rect.width - 120f) * 0.5f, rect.yMax - 48f, 120f, 32f),
                        "结束"))
                    CloseAndNotify();
            }
        }

        void PlayPlayer(int index)
        {
            _board[index] = 1;
            if (CheckEndAfterMove())
                return;
            _playerTurn = false;
            _status = "将老落子中…";
            PlayNpc();
        }

        void PlayNpc()
        {
            var move = ChooseNpcMove();
            if (move < 0)
            {
                Finish(ResultKind.Draw);
                return;
            }

            _board[move] = 2;
            if (CheckEndAfterMove())
                return;
            _playerTurn = true;
            _status = "轮到你了。";
        }

        int ChooseNpcMove()
        {
            for (var i = 0; i < 9; i++)
                if (_board[i] == 0 && WouldWin(_board, i, 2))
                    return i;
            for (var i = 0; i < 9; i++)
                if (_board[i] == 0 && WouldWin(_board, i, 1))
                    return i;
            if (_board[4] == 0)
                return 4;
            var corners = new[] { 0, 2, 6, 8 };
            for (var c = 0; c < corners.Length; c++)
                if (_board[corners[c]] == 0)
                    return corners[c];
            for (var i = 0; i < 9; i++)
                if (_board[i] == 0)
                    return i;
            return -1;
        }

        static bool WouldWin(int[] board, int index, int who)
        {
            var old = board[index];
            board[index] = who;
            var win = Winner(board) == who;
            board[index] = old;
            return win;
        }

        bool CheckEndAfterMove()
        {
            var w = Winner(_board);
            if (w == 1)
            {
                Finish(ResultKind.Win);
                return true;
            }

            if (w == 2)
            {
                Finish(ResultKind.Lose);
                return true;
            }

            for (var i = 0; i < 9; i++)
                if (_board[i] == 0)
                    return false;
            Finish(ResultKind.Draw);
            return true;
        }

        static int Winner(int[] b)
        {
            int[][] lines =
            {
                new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 },
                new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
                new[] { 0, 4, 8 }, new[] { 2, 4, 6 }
            };
            for (var i = 0; i < lines.Length; i++)
            {
                var a = lines[i][0];
                var b1 = lines[i][1];
                var c = lines[i][2];
                if (b[a] != 0 && b[a] == b[b1] && b[a] == b[c])
                    return b[a];
            }

            return 0;
        }

        void Finish(ResultKind result)
        {
            _finished = true;
            _result = result;
            _playerTurn = false;
            _status = result == ResultKind.Win ? "三子连珠，你胜。" :
                (result == ResultKind.Lose ? "将老三子连珠。" : "棋盘已满，和棋。");
        }

        void CloseAndNotify()
        {
            var cb = _onFinished;
            var r = _result;
            _open = false;
            _onFinished = null;
            ReleasePause();
            cb?.Invoke(r);
        }

        void ReleasePause()
        {
            if (!_holdingPause)
                return;
            _holdingPause = false;
            if (bootstrap?.Session != null)
                bootstrap.Session.IsPaused = false;
            HostInputGate.Clear();
        }

        void EnsureStyles()
        {
            if (_title != null)
                return;
            _px = Texture2D.whiteTexture;
            _title = HostImguiStyles.InkLabel(18, bold: true, ink: Ink);
            _body = HostImguiStyles.InkLabel(13, wordWrap: true, ink: Ink);
            _cell = HostImguiStyles.InkLabel(28, bold: true, ink: Ink);
            _cell.alignment = TextAnchor.MiddleCenter;
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _px != null ? _px : Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void DrawFrame(Rect r, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, 1f), c);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            Fill(new Rect(r.x, r.y, 1f, r.height), c);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }
    }
}
