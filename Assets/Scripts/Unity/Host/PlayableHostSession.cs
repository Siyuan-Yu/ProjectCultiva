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

        /// <summary>VS0.6: CharacterIds + visible Npcs (presentation／selection).</summary>
        public IReadOnlyList<EntityId> ViewableEntityIds { get; private set; } = Array.Empty<EntityId>();

        public EntityId RecruitableNpcId { get; private set; }

        public bool IsInitialized => World != null && Loop != null;

        public bool IsPaused { get; set; } = true;

        public string LastError { get; private set; } = string.Empty;

        /// <summary>Level Tester／Host：优先使用的 mapLayout id（空则回退启发式）。</summary>
        public string PreferredMapLayoutId { get; set; } = string.Empty;

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
            RecruitableNpcId = started.Value.RecruitableNpcId;
            ViewableEntityIds = BuildViewableEntityIds(World, CharacterIds, RecruitableNpcId);
            LastError = string.Empty;
            return Result.Success();
        }

        public Result Rebuild(string packageDirectory, PlayableDayOptions options = null) =>
            Initialize(packageDirectory, options);

        /// <summary>动态刷出的 NPC（如战略接战）需刷新后再 Rebuild 表现层。</summary>
        public void RefreshViewableEntityIds()
        {
            if (World == null)
            {
                ViewableEntityIds = Array.Empty<EntityId>();
                return;
            }

            ViewableEntityIds = BuildViewableEntityIds(World, CharacterIds, RecruitableNpcId);
        }

        public void Clear()
        {
            World = null;
            Loop = null;
            Port = null;
            Registry = null;
            LoadedContent = null;
            ScheduleDefinitionId = string.Empty;
            CharacterIds = Array.Empty<EntityId>();
            ViewableEntityIds = Array.Empty<EntityId>();
            RecruitableNpcId = EntityId.None;
            PreferredMapLayoutId = string.Empty;
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
            CharacterIds = CollectTaggedIds(World, EntityTag.Character);
            RecruitableNpcId = FindFirstTagged(World, EntityTag.Npc);
            ViewableEntityIds = BuildViewableEntityIds(World, CharacterIds, RecruitableNpcId);
            ScheduleDefinitionId = CharacterIds.Count > 0 &&
                                   World.Entities.TryGet(CharacterIds[0], out var first) &&
                                   first.TryGet<XianXia.Core.Schedule.ScheduleComponent>(out var schedule)
                ? schedule.DefinitionId
                : ScheduleDefinitionId;
            LastError = string.Empty;
            IsPaused = true;
            return Result.Success();
        }

        static IReadOnlyList<EntityId> BuildViewableEntityIds(
            SimulationWorld world,
            IReadOnlyList<EntityId> characters,
            EntityId recruitableNpc)
        {
            var list = new List<EntityId>();
            var seen = new HashSet<ulong>();

            void Add(EntityId id)
            {
                if (id.IsNone || seen.Contains(id.Value))
                    return;
                seen.Add(id.Value);
                list.Add(id);
            }

            if (characters != null)
            {
                for (var i = 0; i < characters.Count; i++)
                    Add(characters[i]);
            }

            Add(recruitableNpc);

            // 候选列表可含全世界 NPC；真正刷图由 LocalMapVisibility.IsEntityVisible 按当前 LocalMap 过滤。
            // 禁止在此「无条件全部显示」——否则其它地点／战略 Army 会落到同一张图。
            if (world != null)
            {
                foreach (var entity in world.Entities.All)
                {
                    if ((entity.Tags & EntityTag.Npc) != 0)
                        Add(entity.Id);
                }
            }

            return list;
        }

        static IReadOnlyList<EntityId> CollectTaggedIds(SimulationWorld world, EntityTag tag)
        {
            var list = new List<EntityId>();
            if (world == null)
                return list;
            foreach (var entity in world.Entities.All)
            {
                if ((entity.Tags & tag) != 0)
                    list.Add(entity.Id);
            }

            list.Sort((a, b) => a.Value.CompareTo(b.Value));
            return list;
        }

        static EntityId FindFirstTagged(SimulationWorld world, EntityTag tag)
        {
            var ids = CollectTaggedIds(world, tag);
            return ids.Count > 0 ? ids[0] : EntityId.None;
        }
    }
}
