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
        bool _lastSaveSuccess;
        bool _lastLoadSuccess;
        bool _hasLastSave;
        bool _hasLastLoad;

        public string Status => _status;

        public bool ShowAcceptanceButtons
        {
            get => showButtons;
            set => showButtons = value;
        }

        public string LastSaveLabel =>
            !_hasLastSave ? "Never" : (_lastSaveSuccess ? "SUCCESS" : "FAIL");

        public string LastLoadLabel =>
            !_hasLastLoad ? "Never" : (_lastLoadSuccess ? "SUCCESS" : "FAIL");

        public int SchemaVersion => XianXia.Core.Persistence.WorldSnapshot.CurrentSchemaVersion;

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
            if (bootstrap == null || !bootstrap.Session.IsInitialized)
                return;

            if (showButtons &&
                bootstrap.WorldMapPanel != null &&
                bootstrap.WorldMapPanel.IsOpen)
                return;

            if (showButtons &&
                bootstrap.StrategicAcceptancePanel != null &&
                bootstrap.StrategicAcceptancePanel.IsVisible)
                return;

            var formalHud = bootstrap.GetComponent<HostFormalHud>();
            if (showButtons && formalHud != null && formalHud.IsHudVisible)
                return;

            if (showButtons)
            {
                GUI.Label(new Rect(8f, 8f, 420f, 18f),
                    "Save Snapshot v" + SchemaVersion + ": F5   Load Snapshot v" + SchemaVersion + ": F9");
                if (GUI.Button(new Rect(8f, 44f, 88f, 28f), "Save(F5)"))
                    TrySave();
                if (GUI.Button(new Rect(102f, 44f, 88f, 28f), "Load(F9)"))
                    TryLoad();
                GUI.Label(new Rect(200f, 48f, 360f, 22f),
                    "Last Save: " + LastSaveLabel + "   Last Load: " + LastLoadLabel);
            }
        }

        public bool TrySave()
        {
            if (bootstrap == null || !bootstrap.Session.IsInitialized)
            {
                _status = "Save failed: not initialized";
                _hasLastSave = true;
                _lastSaveSuccess = false;
                return false;
            }

            var captured = bootstrap.Session.CaptureSnapshotJson();
            if (captured.IsFailure)
            {
                _status = "Save failed: " + captured.Error;
                _hasLastSave = true;
                _lastSaveSuccess = false;
                Debug.LogError("[HostSnapshot] " + _status, this);
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SlotPath) ?? Application.persistentDataPath);
                File.WriteAllText(SlotPath, captured.Value);
                _status = "Saved " + SlotPath;
                _hasLastSave = true;
                _lastSaveSuccess = true;
                Debug.Log("[HostSnapshot] " + _status, this);
                return true;
            }
            catch (System.Exception ex)
            {
                _status = "Save IO failed: " + ex.Message;
                _hasLastSave = true;
                _lastSaveSuccess = false;
                Debug.LogError("[HostSnapshot] " + _status, this);
                return false;
            }
        }

        public bool TryLoad()
        {
            if (bootstrap == null)
            {
                _status = "Load failed: no bootstrap";
                _hasLastLoad = true;
                _lastLoadSuccess = false;
                return false;
            }

            if (!File.Exists(SlotPath))
            {
                _status = "Load failed: missing " + SlotPath;
                _hasLastLoad = true;
                _lastLoadSuccess = false;
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
                _hasLastLoad = true;
                _lastLoadSuccess = false;
                Debug.LogError("[HostSnapshot] " + _status, this);
                return false;
            }

            var restored = bootstrap.Session.RestoreSnapshotJson(json);
            if (restored.IsFailure)
            {
                _status = FormatLoadFailure(restored.Error);
                _hasLastLoad = true;
                _lastLoadSuccess = false;
                Debug.LogError("[HostSnapshot] " + _status, this);
                return false;
            }

            bootstrap.RebuildPresentationAfterLoad();
            _status = "Loaded tick=" + bootstrap.Session.World.Tick.Value;
            _hasLastLoad = true;
            _lastLoadSuccess = true;
            Debug.Log("[HostSnapshot] " + _status, this);
            return true;
        }

        static string FormatLoadFailure(XianXia.Core.Results.GameError error)
        {
            var msg = error.Message ?? string.Empty;
            if (msg.IndexOf("schema v1", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                msg.IndexOf("Schema v1", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Unsupported development save schema v1. Supported schema: v" +
                       XianXia.Core.Persistence.WorldSnapshot.CurrentSchemaVersion + ".";
            }

            return "Load failed: " + msg;
        }
    }
}
