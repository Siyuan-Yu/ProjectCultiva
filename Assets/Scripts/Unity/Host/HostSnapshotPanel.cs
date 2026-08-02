using System.IO;
using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// VS0.4 Phase G: Save／Load via existing SnapshotService. No schema changes.
    /// </summary>
    public sealed class HostSnapshotPanel : MonoBehaviour
    {
        public const string DefaultFileName = "vs04_slot0.json";

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] KeyCode saveKey = KeyCode.F5;
        [SerializeField] KeyCode loadKey = KeyCode.F9;
        [SerializeField] bool showButtons = false;

        string _status = "No snapshot op";

        public string Status => _status;

        public string SlotPath => Path.Combine(Application.persistentDataPath, DefaultFileName);

        public void Bind(PlayableHostBootstrap hostBootstrap)
        {
            bootstrap = hostBootstrap;
        }

        void Update()
        {
            if (bootstrap == null || !bootstrap.Session.IsInitialized)
                return;

            if (Input.GetKeyDown(saveKey))
                TrySave();
            if (Input.GetKeyDown(loadKey))
                TryLoad();
        }

        void OnGUI()
        {
            if (!showButtons || bootstrap == null || !bootstrap.Session.IsInitialized)
                return;

            if (GUI.Button(new Rect(8f, 44f, 88f, 28f), "Save(F5)"))
                TrySave();
            if (GUI.Button(new Rect(102f, 44f, 88f, 28f), "Load(F9)"))
                TryLoad();
        }

        public bool TrySave()
        {
            if (bootstrap == null || !bootstrap.Session.IsInitialized)
            {
                _status = "Save failed: not initialized";
                return false;
            }

            var captured = bootstrap.Session.CaptureSnapshotJson();
            if (captured.IsFailure)
            {
                _status = "Save failed: " + captured.Error;
                Debug.LogError("[HostSnapshot] " + _status, this);
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SlotPath) ?? Application.persistentDataPath);
                File.WriteAllText(SlotPath, captured.Value);
                _status = "Saved " + SlotPath;
                Debug.Log("[HostSnapshot] " + _status, this);
                return true;
            }
            catch (System.Exception ex)
            {
                _status = "Save IO failed: " + ex.Message;
                Debug.LogError("[HostSnapshot] " + _status, this);
                return false;
            }
        }

        public bool TryLoad()
        {
            if (bootstrap == null)
            {
                _status = "Load failed: no bootstrap";
                return false;
            }

            if (!File.Exists(SlotPath))
            {
                _status = "Load failed: missing " + SlotPath;
                Debug.LogWarning("[HostSnapshot] " + _status, this);
                return false;
            }

            string json;
            try
            {
                json = File.ReadAllText(SlotPath);
            }
            catch (System.Exception ex)
            {
                _status = "Load IO failed: " + ex.Message;
                Debug.LogError("[HostSnapshot] " + _status, this);
                return false;
            }

            var restored = bootstrap.Session.RestoreSnapshotJson(json);
            if (restored.IsFailure)
            {
                _status = "Load failed: " + restored.Error;
                Debug.LogError("[HostSnapshot] " + _status, this);
                return false;
            }

            bootstrap.RebuildPresentationAfterLoad();
            _status = "Loaded tick=" + bootstrap.Session.World.Tick.Value;
            Debug.Log("[HostSnapshot] " + _status, this);
            return true;
        }
    }
}
