using UnityEngine;
using UnityEngine.SceneManagement;

namespace XianXia.Unity.Host
{
    [DefaultExecutionOrder(-50)]
    public sealed class HostGameSessionMenu : MonoBehaviour
    {
        const string Owner = "FormalGameSessionMenu";
        PlayableHostBootstrap _host;
        PlayableHostSession _pausedSession;
        bool _visible;
        string _confirmation;
        string _message = string.Empty;

        void Awake() => _host = GetComponent<PlayableHostBootstrap>();

        public void Open()
        {
            if (!HostPlayerPaths.IsFormalPlayer) return;
            _visible = true;
            Acquire();
        }

        void Acquire()
        {
            _pausedSession = _host?.Session;
            _pausedSession?.AcquireModalPause(Owner);
            HostInputGate.Acquire(Owner);
            HostUiHitTest.RegisterPersistent(Owner, new Rect(0, 0, Screen.width, Screen.height));
        }

        void Close()
        {
            _visible = false;
            _confirmation = null;
            _pausedSession?.ReleaseModalPause(Owner);
            _pausedSession = null;
            HostInputGate.Release(Owner);
            HostUiHitTest.UnregisterPersistent(Owner, retireForCurrentFrame: true);
        }

        void OnDisable() => Close();
        void OnDestroy() => Close();

        void Update()
        {
            if (!HostPlayerPaths.IsFormalPlayer) return;
            if (_visible) Acquire();
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (_visible)
            {
                if (_confirmation != null) _confirmation = null;
                else if (_host?.Session?.IsInitialized == true) Close();
            }
            else if (!HostInputGate.BlockWorldInteraction && _host?.Session?.ModalHardPaused != true)
                Open();
        }

        void OnGUI()
        {
            if (!HostPlayerPaths.IsFormalPlayer || _host == null) return;
            var failed = !_host.Session.IsInitialized;
            if (failed) Open();
            if (!_visible) return;
            Acquire();
            var previousDepth = GUI.depth;
            GUI.depth = -1000;
            var width = Mathf.Min(440f, Screen.width - 20f);
            var rect = new Rect((Screen.width - width) * .5f, Mathf.Max(10f, (Screen.height - 330f) * .5f), width, 330f);
            GUI.ModalWindow(0x50C011, rect, _ => DrawWindow(failed), failed ? "无法进入游戏" : "游戏菜单");
            GUI.depth = previousDepth;
        }

        void DrawWindow(bool failed)
        {
            var body = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Space(12f);
            if (failed)
            {
                GUILayout.Label("游戏未能载入。请确认程序与完整数据文件夹位于一起。", body);
                GUILayout.Label(_host.StatusLine, body, GUILayout.Height(100f));
                if (GUILayout.Button("重试", GUILayout.Height(30f))) _host.TryInitialize();
                if (GUILayout.Button("退出游戏", GUILayout.Height(30f))) Application.Quit();
                return;
            }
            if (_confirmation != null)
            {
                GUILayout.Label("当前未保存的进度将会丢失，已有存档会保留。是否" + _confirmation + "？", body);
                if (GUILayout.Button("确认" + _confirmation, GUILayout.Height(34f)))
                {
                    var operation = _confirmation;
                    _confirmation = null;
                    if (operation == "读取存档")
                    {
                        var result = HostLevelTesterSnapshotOps.TryLoad(_host);
                        Acquire();
                        _message = result.Success ? "存档已读取。" : result.Message;
                    }
                    else
                    {
                        Close();
                        if (operation == "新开局") SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                        else Application.Quit();
                        GUIUtility.ExitGUI();
                    }
                }
                if (GUILayout.Button("取消", GUILayout.Height(30f))) _confirmation = null;
                return;
            }
            if (GUILayout.Button("继续游戏", GUILayout.Height(32f))) Close();
            if (GUILayout.Button("保存游戏", GUILayout.Height(32f)))
            {
                var result = HostLevelTesterSnapshotOps.TrySave(_host);
                _message = result.Success ? "游戏已保存。" : result.Message;
            }
            GUI.enabled = System.IO.File.Exists(HostPlayerPaths.SavePath);
            if (GUILayout.Button("读取存档", GUILayout.Height(32f))) _confirmation = "读取存档";
            GUI.enabled = true;
            if (GUILayout.Button("新开局", GUILayout.Height(32f))) _confirmation = "新开局";
            if (GUILayout.Button("退出游戏", GUILayout.Height(32f))) _confirmation = "退出游戏";
            GUILayout.Label(_message, body, GUILayout.Height(64f));
        }
    }
}
