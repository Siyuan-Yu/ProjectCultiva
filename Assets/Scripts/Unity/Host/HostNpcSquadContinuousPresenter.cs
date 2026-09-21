using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Near-field view projection for modern NPC SquadWorldMotion authority.</summary>
    public sealed class HostNpcSquadContinuousPresenter : MonoBehaviour
    {
        readonly List<ulong> _members = new List<ulong>(16);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        readonly HashSet<string> _rejectedPlayerSquads = new HashSet<string>();
#endif
        PlayableHostBootstrap _bootstrap;
        ulong _lastTick = ulong.MaxValue;
        object _lastWorld;
        public void Bind(PlayableHostBootstrap bootstrap) => _bootstrap = bootstrap;

        void Update()
        {
            var session = _bootstrap?.Session;
            var runtime = _bootstrap?.ContinuousOutdoorSurfaceRuntime;
            if (session == null || !session.IsInitialized || runtime == null || !runtime.IsActive) { _lastTick = ulong.MaxValue; return; }
            var world = session.World;
            if (!ReferenceEquals(_lastWorld, world)) { _lastWorld = world; _lastTick = ulong.MaxValue; }
            if (_lastTick == world.Tick.Value) return;
            _lastTick = world.Tick.Value;
            foreach (var pair in world.Strategic.SquadWorldMotions.Motions)
            {
                if (!world.Strategic.Squads.TryGet(pair.Key, out var squad)) continue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (SquadWorldMotionService.IsPlayerPartySquad(world, squad) &&
                    _rejectedPlayerSquads.Add(squad.SquadId))
                    Debug.LogWarning("Ignored stale PlayerParty SquadWorldMotion: " + squad.SquadId);
#endif
                if (!SquadWorldMotionService.IsActiveNpcSquadAuthority(world, squad, pair.Value) ||
                    !runtime.IsFieldSquadInLoadedNeighborhood(squad, pair.Value)) continue;
                _members.Clear();
                for (var i = 0; i < squad.MemberCharacterIds.Count; i++) _members.Add(squad.MemberCharacterIds[i]);
                _members.Sort();
                for (var slot = 0; slot < _members.Count; slot++)
                {
                    var id = new EntityId(_members[slot]);
                    if (world.Strategic.PlayerPartyContext?.IsMember(id) == true ||
                        !CharacterLifeStateQuery.IsLivingForMacroOrder(world, id) ||
                        ActualBattleParticipantQuery.TryFind(world.Strategic.Participants, id, out _) ||
                        !runtime.TryResolveSquadMemberPresentationPosition(squad, pair.Value, slot, null, out var presentation) ||
                        !_bootstrap.ViewSpawner.Registry.TryGet(id, out var view) || view == null) continue;
                    view.transform.position = presentation;
                    if (world.Entities.TryGet(id, out var entity))
                    {
                        if (!entity.TryGet<EntityLocationComponent>(out var location)) { location = new EntityLocationComponent(); entity.AddComponent(location); }
                        location.SetPresentationOverride(presentation.x, presentation.y);
                    }
                }
            }
        }
    }
}
