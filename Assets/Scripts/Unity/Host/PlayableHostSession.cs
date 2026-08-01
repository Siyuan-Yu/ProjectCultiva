using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Input;
using XianXia.Core.Persistence;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using XianXia.Data.Serialization;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Pure C# playable-host session. Holds Core world／loop／port; no MonoBehaviour.
    /// </summary>
    public sealed class PlayableHostSession
    {
        public SimulationWorld World { get; private set; }

        public SimulationLoop Loop { get; private set; }

        public IPlayerInputPort Port { get; private set; }

        public DefinitionRegistry Registry { get; private set; }

        public LoadedContent LoadedContent { get; private set; }

        public string ScheduleDefinitionId { get; private set; }

        public IReadOnlyList<EntityId> CharacterIds { get; private set; } = Array.Empty<EntityId>();

        public bool IsInitialized => World != null && Loop != null;

        public bool IsPaused { get; set; } = true;

        public string LastError { get; private set; } = string.Empty;

        public Result Initialize(string packageDirectory, PlayableDayOptions options = null)
        {
            Clear();

            if (string.IsNullOrWhiteSpace(packageDirectory))
            {
                LastError = "Content package directory is empty.";
                return Result.Failure(ErrorCode.ContentLoadFailed, LastError);
            }

            var started = new PlayableDayBootstrap().Start(packageDirectory, options);
            if (started.IsFailure)
            {
                LastError = started.Error.ToString();
                return Result.Failure(started.Error);
            }

            World = started.Value.World;
            Loop = started.Value.Loop;
            Port = started.Value.Port;
            Registry = started.Value.Registry;
            LoadedContent = started.Value.LoadedContent;
            ScheduleDefinitionId = started.Value.ScheduleDefinitionId;
            CharacterIds = started.Value.CharacterIds;
            LastError = string.Empty;
            return Result.Success();
        }

        public Result Rebuild(string packageDirectory, PlayableDayOptions options = null) =>
            Initialize(packageDirectory, options);

        public void Clear()
        {
            World = null;
            Loop = null;
            Port = null;
            Registry = null;
            LoadedContent = null;
            ScheduleDefinitionId = string.Empty;
            CharacterIds = Array.Empty<EntityId>();
            IsPaused = true;
        }

        public Result TickOnce()
        {
            if (!IsInitialized)
            {
                LastError = "Host session is not initialized.";
                return Result.Failure(ErrorCode.InvalidOperation, LastError);
            }

            var result = Loop.TickOnce();
            if (result.IsFailure)
                LastError = result.Error.ToString();
            return result;
        }

        public DayClock CurrentDayClock =>
            IsInitialized ? DayClock.FromWorldTick(World.Tick) : default;

        public Result<string> CaptureSnapshotJson()
        {
            if (!IsInitialized)
            {
                LastError = "Host session is not initialized.";
                return Result.Fail<string>(ErrorCode.InvalidOperation, LastError);
            }

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var captured = service.CaptureJson(World, Loop);
            if (captured.IsFailure)
                LastError = captured.Error.ToString();
            return captured;
        }

        public Result RestoreSnapshotJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                LastError = "Snapshot json is empty.";
                return Result.Failure(ErrorCode.SnapshotInvalid, LastError);
            }

            var expectedVersion = World != null ? World.EnabledPackageVersion : null;
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var restored = service.RestoreJson(json, expectedVersion);
            if (restored.IsFailure)
            {
                LastError = restored.Error.ToString();
                return Result.Failure(restored.Error);
            }

            World = restored.Value.world;
            Loop = restored.Value.loop;
            Port = new PlayerInputPort(Loop);
            CharacterIds = CollectCharacterIds(World);
            ScheduleDefinitionId = CharacterIds.Count > 0 &&
                                   World.Entities.TryGet(CharacterIds[0], out var first) &&
                                   first.TryGet<XianXia.Core.Schedule.ScheduleComponent>(out var schedule)
                ? schedule.DefinitionId
                : ScheduleDefinitionId;
            LastError = string.Empty;
            IsPaused = true;
            return Result.Success();
        }

        static IReadOnlyList<EntityId> CollectCharacterIds(SimulationWorld world)
        {
            var list = new List<EntityId>();
            if (world == null)
                return list;
            foreach (var entity in world.Entities.All)
            {
                if ((entity.Tags & EntityTag.Character) != 0)
                    list.Add(entity.Id);
            }

            list.Sort((a, b) => a.Value.CompareTo(b.Value));
            return list;
        }
    }
}
