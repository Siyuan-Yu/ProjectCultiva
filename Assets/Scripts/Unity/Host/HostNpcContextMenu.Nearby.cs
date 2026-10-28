using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Npc;
using XianXia.Core.Combat;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    public sealed partial class HostNpcContextMenu
    {
        sealed class NearbyTarget
        {
            public EntityId Person;
            public WorldObjectInteractionTarget Object;
            public string Label;
            public float Distance;
            public bool HasSpot;
            public HostInteractSpot Spot;
        }
        readonly List<NearbyTarget> _nearbyTargets = new List<NearbyTarget>();
        readonly HashSet<string> _nearbyKeys = new HashSet<string>();
        NearbyTarget _nearbyTarget;
        NearbyTarget _displayedNearbyTarget;
        EntityId _hintActor;
        SimulationWorld _hintWorld;
        float _nextHintScan;
        GUIStyle _hintStyle;
        bool _hasNearbySpot;
        HostInteractSpot _nearbySpot;

        bool OpenNpcMenu(EntityId actor, EntityId npc, Vector2 screen)
        {
            if (bootstrap.Session.World.Civilians.TryGet(npc, out var captured) &&
                captured.Disposition == CivilianDisposition.CapturedEscorted && captured.CarrierId != actor)
                return true;
            if (!HostRpgControls.SceneRts && !HostRpgControls.NearNpc(bootstrap, actor, npc))
            { HostWorldSiteCoreWarfare.Feedback(bootstrap, "请先走近人物，再按F或右键交互。"); return true; }
            StopMapTravelForInteraction(actor);
            _actor = actor; _targetNpc = npc;
            _worldObjectTarget = default;
            _targetControlCoreWorkAreaId = _targetFactionFlagId = _targetEntranceLocationId = string.Empty;
            _targetDestructible = null;
            _targetLabel = ResolveDisplayName(npc);
            if (CanRtsAttack(actor, npc)) { BeginAttack(); return true; }
            _prisonVisitMenu = HuangcunPrisonVisitService.Applies(bootstrap.Session.World) &&
                HuangcunSurvivalService.IsPrisoner(bootstrap.Session.World, npc);
            _menuScreen = screen; _phase = Phase.Menu;
            HostInputGate.AcquireInteraction(InputOwner);
            return true;
        }

        bool OpenObjectMenu(EntityId actor, WorldObjectInteractionTarget target, Vector2 screen)
        {
            if (!HostRpgControls.SceneRts && IsManagementObject(target.Kind)) return true;
            if (!HostWorldObjectPicker.IsTargetLocallyVisible(bootstrap, target)) return true;
            if (!HostRpgControls.SceneRts && !HostRpgControls.NearObject(bootstrap, actor, target))
            { HostWorldSiteCoreWarfare.Feedback(bootstrap, "请先走近目标，再按F或右键交互。"); return true; }
            StopMapTravelForInteraction(actor);
            _actor = actor; _targetNpc = EntityId.None;
            _targetEntranceLocationId = string.Empty;
            SetWorldObjectTarget(target); _prisonVisitMenu = false;
            _menuScreen = screen; _phase = Phase.Menu;
            HostInputGate.AcquireInteraction(InputOwner);
            return true;
        }

        public void TryOpenNearby()
        {
            if (!CanOfferNearbyInteraction()) return;
            TickNearbyHint();
            var actor = HostNpcInteraction.ResolveActiveCommandAuthority(bootstrap.Session);
            // Interact with the last visible hint, not a newly preferred unseen neighbor.
            var chosen = _displayedNearbyTarget ?? _nearbyTarget;
            if (chosen == null || !IsNearbyTargetValid(actor, chosen))
            { HostWorldSiteCoreWarfare.Feedback(bootstrap, "附近没有可交互目标，请先靠近。"); return; }
            bootstrap.WorkTargetMode?.Cancel();
            StopMapTravelForInteraction(actor);
            var screen = new Vector2(Screen.width * .5f, Screen.height * .5f);
            if (chosen.HasSpot)
            {
                _actor = actor; _nearbySpot = chosen.Spot; _hasNearbySpot = true;
                _targetLabel = chosen.Label; _phase = Phase.Menu; _menuScreen = screen;
                HostInputGate.AcquireInteraction(InputOwner);
            }
            else if (chosen.Person.IsNone) OpenObjectMenu(actor, chosen.Object, screen);
            else OpenNpcMenu(actor, chosen.Person, screen);
        }

        void CollectNearbyTargets(EntityId actor, EntityView actorView)
        {
            _nearbyTargets.Clear(); _nearbyKeys.Clear();
            var from = actorView.transform.position;
            foreach (var view in bootstrap.ViewSpawner.Registry.All)
            {
                if (view == null || !view.IsBound || view.EntityId == actor || !IsPersonInteractionCandidate(actor, view.EntityId) ||
                    !view.TryGetVisibleBodyBounds(out _) || !HostRpgControls.NearNpc(bootstrap, actor, view.EntityId)) continue;
                if (bootstrap.Session.World.Civilians.TryGet(view.EntityId, out var custody) &&
                    custody.Disposition == CivilianDisposition.CapturedEscorted && custody.CarrierId != actor) continue;
                _nearbyTargets.Add(new NearbyTarget { Person = view.EntityId, Label = ResolveDisplayName(view.EntityId),
                    Distance = Vector2.Distance(from, view.transform.position) });
            }
            // Loaded registries only, refreshed at a bounded cadence rather than every frame.
            foreach (var plot in HostMapObjectRegistry.AllPlots)
            {
                if (plot == null || !plot.gameObject.activeInHierarchy) continue;
                var point = plot.HasInteractionBounds ? new Vector3(
                    Mathf.Clamp(from.x, plot.InteractionBounds.xMin, plot.InteractionBounds.xMax),
                    Mathf.Clamp(from.y, plot.InteractionBounds.yMin, plot.InteractionBounds.yMax), from.z) : plot.transform.position;
                if (Vector2.Distance(from, point) > HostRpgControls.InteractionRange) continue;
                if (HostWorldObjectPicker.TryPickAtWorldPoint(bootstrap, point, out var target)) AddNearbyObject(actor, from, target);
                var kind = plot.IsBed ? WorldObjectTargetKind.Bed : plot.IsRoom ? WorldObjectTargetKind.Room :
                    plot.IsDoor ? WorldObjectTargetKind.Door : plot.IsPlantableField ? WorldObjectTargetKind.FarmPlot :
                    plot.IsRecoverySpot ? WorldObjectTargetKind.RecoverySpot : plot.IsPrisonDropOff ? WorldObjectTargetKind.PrisonDropOff :
                    plot.IsStorageRoom ? WorldObjectTargetKind.StorageRoom : plot.IsSpiritGate ? WorldObjectTargetKind.SpiritGate :
                    plot.IsWaystone ? WorldObjectTargetKind.Waystone : plot.InteractKind == HostInteractSpotKind.Loot ?
                    WorldObjectTargetKind.Loot : WorldObjectTargetKind.Furniture;
                AddNearbyObject(actor, from, new WorldObjectInteractionTarget(kind, plot: plot, displayLabel: plot.KindDisplayName()));
            }
            foreach (var prop in HostMapObjectRegistry.AllDestructibles)
                if (prop != null && !prop.IsDestroyed && prop.gameObject.activeInHierarchy)
                    AddNearbyObject(actor, from, new WorldObjectInteractionTarget(WorldObjectTargetKind.Destructible,
                        destructible: prop, displayLabel: prop.DisplayName));
            foreach (var pair in HostDynamicWorldObjectRegistry.All)
            {
                var entry = pair.Value;
                if (entry?.View == null || !entry.View.activeInHierarchy) continue;
                AddNearbyObject(actor, from, new WorldObjectInteractionTarget(WorldObjectTargetKind.OpportunityObject,
                    displayLabel: entry.DisplayLabel, approachPosition: entry.ApproachPosition, opportunityObject: entry));
            }
            foreach (var spot in HostInteractSpots.GetSpots(bootstrap.Session.World))
            {
                // Work is represented by actual farm/mine plots, never broad house/work-area anchors.
                if (spot.Kind != HostInteractSpotKind.Cultivate &&
                    spot.Kind != HostInteractSpotKind.Explore && spot.Kind != HostInteractSpotKind.Loot) continue;
                if (!HostRpgControls.NearPoint(bootstrap, actor, spot.WorldPosition)) continue;
                var represented = false;
                foreach (var candidate in _nearbyTargets)
                    if (candidate.Object.Plot != null && candidate.Object.Plot.LocationId == spot.LocationId &&
                        candidate.Object.Plot.InteractKind == spot.Kind) { represented = true; break; }
                if (represented || !_nearbyKeys.Add("spot:" + spot.LocationId + ":" + spot.Kind + ":" + spot.WorldPosition)) continue;
                _nearbyTargets.Add(new NearbyTarget { HasSpot = true, Spot = spot, Label = spot.Label,
                    Distance = Vector2.Distance(from, spot.WorldPosition) });
            }
            _nearbyTargets.Sort((a, b) =>
            {
                var category = (a.Person.IsNone ? 1 : 0).CompareTo(b.Person.IsNone ? 1 : 0);
                if (category != 0) return category;
                var distance = a.Distance.CompareTo(b.Distance);
                return distance != 0 ? distance : string.CompareOrdinal(NearbyKey(a), NearbyKey(b));
            });
        }

        void StopMapTravelForInteraction(EntityId actor)
        {
            if (!HostRpgControls.SceneRts && bootstrap.Session.World.PlayerPartyTravel.IsMoving)
                bootstrap.WorldMapPanel?.CancelClickTravel();
        }

        void AddNearbyObject(EntityId actor, Vector3 from, WorldObjectInteractionTarget target)
        {
            if (!IsDailyInteractionObject(target) || !HostRpgControls.NearObject(bootstrap, actor, target) ||
                !HostWorldObjectPicker.IsTargetLocallyVisible(bootstrap, target) ||
                string.IsNullOrEmpty(target.StableTargetKey) || !_nearbyKeys.Add(target.StableTargetKey)) return;
            _nearbyTargets.Add(new NearbyTarget { Object = target, Label = target.DisplayLabel,
                Distance = Vector2.Distance(from, HostRpgControls.InteractionPoint(target, from)) });
        }

        static bool IsManagementObject(WorldObjectTargetKind kind) =>
            kind == WorldObjectTargetKind.Room || kind == WorldObjectTargetKind.Door ||
            kind == WorldObjectTargetKind.Housing || kind == WorldObjectTargetKind.WorkArea ||
            kind == WorldObjectTargetKind.StorageRoom || kind == WorldObjectTargetKind.PrisonDropOff;

        bool IsDailyInteractionObject(WorldObjectInteractionTarget target)
        {
            if (IsManagementObject(target.Kind)) return false;
            switch (target.Kind)
            {
                case WorldObjectTargetKind.Bed:
                case WorldObjectTargetKind.RecoverySpot:
                case WorldObjectTargetKind.FarmPlot:
                case WorldObjectTargetKind.Loot:
                case WorldObjectTargetKind.SpiritGate:
                case WorldObjectTargetKind.Waystone:
                case WorldObjectTargetKind.OpportunityObject: return true;
                case WorldObjectTargetKind.Destructible:
                    return target.Destructible != null && (target.Destructible.IsTree ||
                        target.Destructible.IsWall && HostRpgControls.InCombat(bootstrap) &&
                        PrisonWallBreachService.CanDamage(bootstrap.Session.World,
                            HostNpcInteraction.ResolveActiveCommandAuthority(bootstrap.Session), target.StableObjectId));
                case WorldObjectTargetKind.Furniture:
                    return target.Plot != null && !target.Plot.IsRoom && !target.Plot.IsDoor &&
                        (target.Plot.IsLeisureFurniture || target.Plot.Kind == "ore" || target.Plot.Kind == "mine" ||
                         target.Plot.InteractKind == HostInteractSpotKind.Cultivate ||
                         target.Plot.InteractKind == HostInteractSpotKind.Explore);
                default: return false;
            }
        }

        bool IsPersonInteractionCandidate(EntityId actor, EntityId person)
        {
            var world = bootstrap.Session.World;
            if (!world.Entities.TryGet(person, out var entity) ||
                !entity.TryGet<LifecycleComponent>(out var life) || life.IsDead || life.IsRemoved) return false;
            if (PrisonConfinementService.TryGetRoom(world, person, out _)) return true;
            // Free followers are not social NPCs; they must not steal every nearby conversation.
            if (bootstrap.Session.PlayerParty.IsMember(person)) return false;
            if (life.IsIncapacitated || life.State == LifecycleState.Captured) return true;
            var encounter = world.Strategic.CharacterEncounter;
            if (encounter?.Opposing(actor.Value, person.Value) == true ||
                bootstrap.GetComponent<HostNpcMeleeAssault>()?.IsInFight(person) == true) return false;
            return true;
        }

        bool CanOfferNearbyInteraction()
        {
            if (IsOpen || HostInputGate.BlockWorldInteraction || HostInputGate.TextInputFocused ||
                bootstrap?.Session?.IsInitialized != true || bootstrap.Session.ModalHardPaused ||
                bootstrap.Session.World.ContentEvents.HasActive || bootstrap.ContentInterrupt?.HasBlockingInterrupt == true ||
                bootstrap.WorldMapPanel?.IsOpen == true) return false;
            var actor = HostNpcInteraction.ResolveActiveCommandAuthority(bootstrap.Session);
            return !actor.IsNone && bootstrap.Session.World.Entities.TryGet(actor, out var entity) &&
                CombatLifeStateService.CanFight(entity);
        }

        bool IsNearbyTargetValid(EntityId actor, NearbyTarget target)
        {
            if (target == null || actor != _hintActor || !ReferenceEquals(_hintWorld, bootstrap?.Session?.World)) return false;
            if (!target.Person.IsNone)
                return IsPersonInteractionCandidate(actor, target.Person) &&
                    HostRpgControls.NearNpc(bootstrap, actor, target.Person) &&
                    bootstrap.ViewSpawner.Registry.TryGet(target.Person, out var view) && view != null &&
                    view.TryGetVisibleBodyBounds(out _) &&
                    (!bootstrap.Session.World.Civilians.TryGet(target.Person, out var custody) ||
                     custody.Disposition != CivilianDisposition.CapturedEscorted || custody.CarrierId == actor);
            if (target.HasSpot) return HostRpgControls.NearPoint(bootstrap, actor, target.Spot.WorldPosition);
            return IsDailyInteractionObject(target.Object) &&
                (target.Object.Destructible == null || !target.Object.Destructible.IsDestroyed) &&
                HostWorldObjectPicker.IsTargetLocallyVisible(bootstrap, target.Object) &&
                HostRpgControls.NearObject(bootstrap, actor, target.Object);
        }

        static string NearbyKey(NearbyTarget target) => !target.Person.IsNone ? "person:" + target.Person :
            target.HasSpot ? "spot:" + target.Spot.LocationId + ":" + target.Spot.Kind + ":" + target.Spot.WorldPosition :
            target.Object.StableTargetKey;

        void ResetNearbyHint()
        { _nearbyTarget = _displayedNearbyTarget = null; _hintActor = EntityId.None; _hintWorld = null; _nextHintScan = 0; }

        void TickNearbyHint()
        {
            if (!CanOfferNearbyInteraction()) { ResetNearbyHint(); return; }
            var actor = HostNpcInteraction.ResolveActiveCommandAuthority(bootstrap.Session);
            if (actor != _hintActor || !ReferenceEquals(_hintWorld, bootstrap.Session.World)) ResetNearbyHint();
            _hintActor = actor; _hintWorld = bootstrap.Session.World;
            if (Time.unscaledTime >= _nextHintScan)
            {
                _nextHintScan = Time.unscaledTime + .2f;
                if (bootstrap.ViewSpawner == null || !bootstrap.ViewSpawner.Registry.TryGet(actor, out var view) || view == null)
                { _nearbyTarget = null; return; }
                var previousKey = _nearbyTarget == null ? null : NearbyKey(_nearbyTarget);
                CollectNearbyTargets(actor, view);
                var best = _nearbyTargets.Count == 0 ? null : _nearbyTargets[0];
                // Small hysteresis prevents the hint flickering between two adjacent NPCs.
                if (best != null && previousKey != null)
                    foreach (var candidate in _nearbyTargets)
                        if (NearbyKey(candidate) == previousKey && candidate.Person.IsNone == best.Person.IsNone &&
                            candidate.Distance <= best.Distance + .25f) { best = candidate; break; }
                _nearbyTarget = best;
            }
            if (!IsNearbyTargetValid(actor, _nearbyTarget)) _nearbyTarget = null;
        }

        void DrawNearbyHint()
        {
            if (Event.current.type != EventType.Repaint) return;
            _displayedNearbyTarget = null;
            if (!CanOfferNearbyInteraction() || !IsNearbyTargetValid(_hintActor, _nearbyTarget)) return;
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return;
            Vector3 anchor;
            if (!_nearbyTarget.Person.IsNone)
            {
                if (!bootstrap.ViewSpawner.Registry.TryGet(_nearbyTarget.Person, out var view) || view == null ||
                    !view.TryGetVisibleBodyBounds(out var bounds)) return;
                anchor = new Vector3(bounds.center.x, bounds.max.y + .25f, bounds.center.z);
            }
            else if (_nearbyTarget.HasSpot) anchor = _nearbyTarget.Spot.WorldPosition + Vector3.up * .6f;
            else
            {
                var target = _nearbyTarget.Object;
                anchor = target.Plot != null && target.Plot.HasInteractionBounds ?
                    new Vector3(target.Plot.InteractionBounds.center.x, target.Plot.InteractionBounds.yMax + .25f, target.ApproachPosition.z) :
                    target.ApproachPosition + Vector3.up * .8f;
            }
            var screen = worldCamera.WorldToScreenPoint(anchor);
            if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height ||
                HostUiHitTest.ContainsScreenPoint(screen)) return;
            if (_hintStyle == null) _hintStyle = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleCenter, fontSize = 14, normal = { textColor = new Color(1f, .93f, .65f) } };
            var text = "▼  F 交互 · " + _nearbyTarget.Label;
            var width = Mathf.Min(320f, _hintStyle.CalcSize(new GUIContent(text)).x + 18f);
            var rect = new Rect(Mathf.Clamp(screen.x - width / 2, 4, Screen.width - width - 4),
                Mathf.Clamp(Screen.height - screen.y - 28, 4, Screen.height - 32), width, 28);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, text, _hintStyle);
            _displayedNearbyTarget = _nearbyTarget;
        }

        void DrawNearbySpotMenu()
        {
            var spot = _hasNearbySpot ? _nearbySpot : _worldObjectTarget.Plot.ToInteractSpot();
            var width = 280f; var height = HasInspectEvent ? 145f : 110f;
            var x = Mathf.Clamp(_menuScreen.x, 4, Screen.width - width - 4);
            var y = Mathf.Clamp(Screen.height - _menuScreen.y, 4, Screen.height - height - 4);
            var rect = new Rect(x, y, width, height); _menuGuiRect = rect;
            HostUiHitTest.Block(rect); Fill(rect, Panel); DrawFrame(rect, Border);
            GUI.Label(new Rect(x + 8, y + 6, width - 16, 24), _targetLabel, _label);
            var label = spot.Kind == HostInteractSpotKind.Work ? "工作" : spot.Kind == HostInteractSpotKind.Cultivate ?
                "修炼" : spot.Kind == HostInteractSpotKind.Loot ? "拾取" : "探索 / 调查";
            var entranceId = string.Empty;
            var isCave = !bootstrap.Session.World.LocalMap.IsInInterior &&
                HostCaveEntranceQuery.TryPickAtWorld(bootstrap.Session.World, null, bootstrap.ContinuousOutdoorSurfaceRuntime,
                    spot.WorldPosition, out entranceId);
            if (isCave) label = "进入洞府";
            var actionY = y + 35;
            DrawInspectButton(x, ref actionY, width, 34);
            if (GUI.Button(new Rect(x + 8, actionY, width - 16, 30), label, _button))
            {
                var actor = _actor; CloseAll();
                if (!HostRpgControls.RequireNear(bootstrap, actor, spot.WorldPosition)) return;
                if (isCave)
                { _actor = actor; _targetEntranceLocationId = entranceId; BeginCaveEnter(); return; }
                bootstrap.WorkTargetMode?.TryUseNearbySpot(spot);
                return;
            }
            if (GUI.Button(new Rect(x + 8, actionY + 35, width - 16, 28), "取消", _button)) CloseAll();
            TryDismissOnOutsideClick(rect);
        }
    }
}
