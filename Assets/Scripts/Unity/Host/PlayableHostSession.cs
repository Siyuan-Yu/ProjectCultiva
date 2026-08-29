using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Input;
using XianXia.Core.Persistence;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
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

        /// <summary>RPG-First LocalMap party (Active + Followers). Phase 1 runtime truth.</summary>
        public PlayerPartyRuntime PlayerParty { get; } = new PlayerPartyRuntime();

        /// <summary>VS0.6: CharacterIds + visible Npcs (presentation／selection).</summary>
        public IReadOnlyList<EntityId> ViewableEntityIds { get; private set; } = Array.Empty<EntityId>();

        public EntityId RecruitableNpcId { get; private set; }

        public bool IsInitialized => World != null && Loop != null;

        public bool IsPaused { get; set; } = true;

        public string LastError { get; private set; } = string.Empty;

        /// <summary>Level Tester／Host：优先使用的 mapLayout id（空则回退启发式）。</summary>
        public string PreferredMapLayoutId { get; set; } = string.Empty;

        /// <summary>
        /// Phase 5R-B3B.3：NewGame 初始 WorldSite 的一次性 Bootstrap provenance（transient，
        /// 不落盘、不进入 PlayerPartyWorldMotion / Save）。由启动链记录（ApplyOpening 后初始
        /// PartyWorld.SiteId）；首次初始 Site 展开消费后清空。空 = 本次启动初始 Context 不在
        /// WorldSite（例如起点在 Wilderness）→ 任何 Site 展开都不得 BootstrapFromAuthoredLocal，
        /// 一律 ProjectCanonicalWorldToLocal。
        /// </summary>
        public string InitialBootstrapSiteId { get; private set; } = string.Empty;

        /// <summary>
        /// Phase 5R-B3B.5：初始 Site Bootstrap 是否<b>尚未消费</b>。与 InitialBootstrapSiteId 同生命周期，
        /// 但挂在 PlayableHostSession（完整运行 Session），不挂在 PlayableHostBootstrap 实例
        /// （scene／WorldMap／LocalMap 重建会让实例字段归零 → consumed 重新变 false → 再次 Bootstrap）。
        /// 真正第一次 Bootstrap 完成后 ConsumeInitialBootstrap() 清空 id + pending，之后即使：
        /// 离开初始 Site 再进入 / 开关 WorldMap / LocalMap 重建 / Host 表现层重建，
        /// 都不得重新 Bootstrap（一律 ProjectCanonicalWorldToLocal）。
        /// </summary>
        public bool InitialBootstrapPending { get; private set; }

        /// <summary>清空初始 Bootstrap provenance（不消费语义，供状态重置）。</summary>
        public void ClearInitialBootstrapSite() => InitialBootstrapSiteId = string.Empty;

        /// <summary>
        /// 消费初始 Site Bootstrap token：NewGame 初始 Site 真正第一次 Materialize 完成后调用，
        /// 一次性永久失效（清 id + pending）。此后任何 Site 展开（含再次进入初始 Site）只能
        /// ProjectCanonicalWorldToLocal。runtime opening provenance，不落盘、不入 motion。
        /// </summary>
        public void ConsumeInitialBootstrap()
        {
            InitialBootstrapSiteId = string.Empty;
            InitialBootstrapPending = false;
        }

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
            // Phase 5R-B3B.3：记录本次启动的初始 Context。若初始 Context 在 WorldSite（ApplyOpening
            // 已 EnterWorldSiteScene(startSiteId)），则初始 Site 首次展开允许 BootstrapFromAuthoredLocal
            // （StartLocation→Canonical，NewGame 专属）；否则（初始在 Wilderness 等）保持空 → 任何
            // Site 展开都只能 ProjectCanonicalWorldToLocal。仅 Host 会话 transient，不落盘。
            InitialBootstrapSiteId = string.IsNullOrWhiteSpace(World?.PartyWorld?.SiteId)
                ? string.Empty
                : World.PartyWorld.SiteId;
            InitialBootstrapPending = !string.IsNullOrWhiteSpace(InitialBootstrapSiteId);
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
            PlayerParty.Reset();
            ViewableEntityIds = Array.Empty<EntityId>();
            RecruitableNpcId = EntityId.None;
            PreferredMapLayoutId = string.Empty;
            InitialBootstrapSiteId = string.Empty;
            InitialBootstrapPending = false;
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
            var captured = service.CaptureJson(World, Loop, PlayerParty);
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
            var serializer = new JsonSnapshotSerializer();
            var service = new SnapshotService(serializer);
            var parsed = serializer.Deserialize(json);
            if (parsed.IsFailure)
            {
                LastError = parsed.Error.ToString();
                return Result.Failure(parsed.Error);
            }

            var restored = service.Restore(parsed.Value, expectedVersion);
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
            PlayerPartySnapshotRestore.Apply(
                World,
                PlayerParty,
                parsed.Value.Strategic?.PlayerParty);
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
