using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Input;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    public sealed class PlayableDayBootstrapResult
    {
        public PlayableDayBootstrapResult(
            SimulationWorld world,
            SimulationLoop loop,
            IPlayerInputPort port,
            DefinitionRegistry registry,
            LoadedContent loadedContent,
            IReadOnlyList<EntityId> characterIds,
            string scheduleDefinitionId,
            EntityId recruitableNpcId = default,
            ContinuousOutdoorOpeningCensus openingCensus = default,
            IReadOnlyList<string> openingPopulationDiagnostics = null)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Loop = loop ?? throw new ArgumentNullException(nameof(loop));
            Port = port ?? throw new ArgumentNullException(nameof(port));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            LoadedContent = loadedContent ?? throw new ArgumentNullException(nameof(loadedContent));
            CharacterIds = characterIds ?? Array.Empty<EntityId>();
            ScheduleDefinitionId = scheduleDefinitionId ?? string.Empty;
            RecruitableNpcId = recruitableNpcId;
            OpeningCensus = openingCensus;
            OpeningPopulationDiagnostics = openingPopulationDiagnostics ?? Array.Empty<string>();
        }

        public SimulationWorld World { get; }

        public SimulationLoop Loop { get; }

        public IPlayerInputPort Port { get; }

        public DefinitionRegistry Registry { get; }

        public LoadedContent LoadedContent { get; }

        public IReadOnlyList<EntityId> CharacterIds { get; }

        public string ScheduleDefinitionId { get; }

        /// <summary>VS0.5-D: single unaffiliated recruit candidate (not DirectControl).</summary>
        public EntityId RecruitableNpcId { get; }

        /// <summary>NewGame opening population census（Domain 计数，不含 EntityView）。</summary>
        public ContinuousOutdoorOpeningCensus OpeningCensus { get; }

        /// <summary>opening population normalize 诊断（含 ambiguity → Content validation error）。</summary>
        public IReadOnlyList<string> OpeningPopulationDiagnostics { get; }
    }
}
