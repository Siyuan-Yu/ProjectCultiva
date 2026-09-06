using System.IO;
using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>LevelTester 开发存读档；从旧 HostSnapshotPanel 提取，无独立 UI。</summary>
    public static class HostLevelTesterSnapshotOps
    {
        public const string DefaultFileName = "vs04_slot0.json";

        public static int SchemaVersion => XianXia.Core.Persistence.WorldSnapshot.CurrentSchemaVersion;

        public static string SlotPath =>
            Path.Combine(Application.persistentDataPath, DefaultFileName);

        public sealed class SnapshotOpResult
        {
            public bool Success;
            public string Message = string.Empty;
        }

        public static SnapshotOpResult TrySave(PlayableHostBootstrap bootstrap)
        {
            var result = new SnapshotOpResult();
            if (bootstrap == null || !bootstrap.Session.IsInitialized)
            {
                result.Message = "Save failed: not initialized";
                return result;
            }

            HostSnapshotLocalPlacementCaptureSync.SyncLoadedLocalMapOccupantsFromViews(bootstrap);
            var captured = bootstrap.Session.CaptureSnapshotJson();
            if (captured.IsFailure)
            {
                result.Message = "Save failed: " + captured.Error;
                Debug.LogError("[LevelTesterSnapshot] " + result.Message);
                return result;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SlotPath) ?? Application.persistentDataPath);
                File.WriteAllText(SlotPath, captured.Value);
                HostLevelTesterSnapshotSummary.RecordSavedFromJson(captured.Value);
                HostLevelTesterSnapshotSummary.RecordRuntime(bootstrap.Session.World, bootstrap.Session);
                result.Success = true;
                result.Message = "Saved " + SlotPath;
                Debug.Log("[LevelTesterSnapshot] " + result.Message);
            }
            catch (System.Exception ex)
            {
                result.Message = "Save IO failed: " + ex.Message;
                Debug.LogError("[LevelTesterSnapshot] " + result.Message);
            }

            return result;
        }

        public static SnapshotOpResult TryLoad(PlayableHostBootstrap bootstrap)
        {
            var result = new SnapshotOpResult();
            if (bootstrap == null)
            {
                result.Message = "Load failed: no bootstrap";
                return result;
            }

            if (!File.Exists(SlotPath))
            {
                result.Message = "Load failed: missing " + SlotPath;
                Debug.LogWarning("[LevelTesterSnapshot] " + result.Message);
                return result;
            }

            string json;
            try
            {
                json = File.ReadAllText(SlotPath);
            }
            catch (System.Exception ex)
            {
                result.Message = "Load IO failed: " + ex.Message;
                Debug.LogError("[LevelTesterSnapshot] " + result.Message);
                return result;
            }

            var restored = bootstrap.Session.RestoreSnapshotJson(json);
            if (restored.IsFailure)
            {
                result.Message = FormatLoadFailure(restored.Error);
                Debug.LogError("[LevelTesterSnapshot] " + result.Message);
                return result;
            }

            HostSnapshotSessionRehydration.LogDomainTrace(
                bootstrap.Session,
                "AfterRestoreJson.BeforePresentation");
            var rehydrated = HostSnapshotSessionRehydration.RehydrateAfterRestore(bootstrap);
            if (rehydrated.IsFailure)
            {
                result.Message = FormatLoadFailure(rehydrated.Error);
                Debug.LogError("[LevelTesterSnapshot] " + result.Message);
                return result;
            }
            HostSnapshotSessionRehydration.LogDomainTrace(
                bootstrap.Session,
                "AfterRehydrate.BeforePresentation");
            WorldMapArmyMarkerDiagnostics.LogFormalArmyDomainAfterLoad(
                bootstrap.Session,
                "AfterRehydrate");
            HostLevelTesterSnapshotSummary.RecordRuntime(bootstrap.Session.World, bootstrap.Session);

            bootstrap.RebuildPresentationAfterLoad();
            WorldMapArmyMarkerDiagnostics.LogWorldMapArmyMarkers(bootstrap.Session);
            HostLevelTesterSnapshotSummary.RecordRuntime(bootstrap.Session.World, bootstrap.Session);
            result.Success = true;
            result.Message = "Loaded tick=" + bootstrap.Session.World.Tick.Value;
            Debug.Log("[LevelTesterSnapshot] " + result.Message);
            return result;
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
