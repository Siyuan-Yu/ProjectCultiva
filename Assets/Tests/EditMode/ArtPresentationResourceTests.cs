using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Construction;
using XianXia.Core.Exploration;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class ArtPresentationResourceTests
    {
        [Test]
        public void PlayerCatalog_CoversEveryCatalogPathWithoutAssetDatabase()
        {
            foreach (var kind in MapKindCatalog.All)
            {
                Assert.IsTrue(MapLayoutPrefabResolver.TryLoadRuntimePrefab(kind.PrefabPath, out var prefab), kind.Kind);
                Assert.IsNotNull(prefab, kind.Kind);
            }
            Assert.IsFalse(MapLayoutPrefabResolver.TryLoadRuntimePrefab("missing/path.prefab", out _));
            Assert.IsFalse(MapLayoutPrefabResolver.TryInstantiate("unknown", "missing/path.prefab", out _, false));
        }

        [Test]
        public void EveryMappedProfile_HasOwnFallbackForEveryAction()
        {
            Assert.IsNull(HostStepAArt.CharacterFrame(HostStepAArt.Profile.None,
                HostStepAArt.Action.Attack, HostStepAArt.Direction.North, 3));
            foreach (HostStepAArt.Profile profile in System.Enum.GetValues(typeof(HostStepAArt.Profile)))
            {
                if (profile == HostStepAArt.Profile.None) continue;
                foreach (HostStepAArt.Action action in System.Enum.GetValues(typeof(HostStepAArt.Action)))
                foreach (HostStepAArt.Direction direction in System.Enum.GetValues(typeof(HostStepAArt.Direction)))
                    Assert.IsNotNull(HostStepAArt.CharacterFrame(profile, action, direction, 3), profile + "/" + action);
                Assert.IsNotNull(HostStepAArt.SleepSprite(profile), profile.ToString());
            }
            Assert.AreSame(HostStepAArt.CharacterFrame(HostStepAArt.Profile.Protagonist, HostStepAArt.Action.Work,
                    HostStepAArt.Direction.South, 0),
                HostStepAArt.CharacterFrame(HostStepAArt.Profile.Protagonist, HostStepAArt.Action.Attack,
                    HostStepAArt.Direction.North, 3));
        }

        [Test]
        public void RecoveringFallbackActor_RestoresFactionColor()
        {
            var world = new SimulationWorld();
            var entity = world.Entities.CreateCharacter(new DefinitionId("test", "unmapped")).Value;
            var root = new GameObject("ArtRecoveryTest");
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                var view = root.AddComponent<EntityView>();
                Assert.IsTrue(view.Bind(world, entity.Id));
                var color = new Color(.2f, .6f, .8f, 1f);
                view.SetBaseColor(color);
                entity.Get<LifecycleComponent>().State = LifecycleState.Incapacitated;
                Tick(view);
                Assert.AreNotEqual(color, renderer.color);
                entity.Get<LifecycleComponent>().State = LifecycleState.Alive;
                Tick(view);
                Assert.AreEqual(color, renderer.color);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ArtReplacement_PreservesGeometryAndColliderAcrossRefresh()
        {
            var root = HostMapObjectVisualFactory.Create("bed", MapKindCatalog.Cushion, "ArtGeometryTest",
                null, new Vector3(10, 20), 1, 2, -12, false);
            try
            {
                var geometry = root.GetComponent<HostMapVisualGeometry>();
                var before = geometry.WorldRect;
                var collider = root.GetComponentInChildren<Collider2D>();
                var colliderBounds = collider != null ? collider.bounds : default;
                HostStepAArt.ApplyObjectVisual(root, HostStepAArt.BedSprite(XianXia.Core.Construction.RoomUsage.Medical));
                HostStepAArt.ApplyObjectVisual(root, HostStepAArt.BedSprite(XianXia.Core.Construction.RoomUsage.Prison));
                Assert.AreEqual(before, geometry.WorldRect);
                Assert.AreEqual(HostStepAArt.BedSprite(XianXia.Core.Construction.RoomUsage.Prison),
                    root.transform.Find("StepAArtVisual").GetComponent<SpriteRenderer>().sprite);
                if (collider != null) Assert.AreEqual(colliderBounds, collider.bounds);
                Assert.Greater(HostArtDepth.Order(19f), HostArtDepth.ObjectOrder("treeL", 22f, 4f));
                Assert.Less(HostArtDepth.Order(21f), HostArtDepth.ObjectOrder("treeL", 22f, 4f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FlagResources_DoNotMapAllFactionsToRed()
        {
            Assert.IsNotNull(HostStepAArt.MapSprite("factionControlPost"));
            Assert.AreNotSame(HostStepAArt.FlagSprite("base:sect_huangcun_labor"),
                HostStepAArt.FlagSprite("base:faction_player"));
            Assert.AreNotEqual(HostStepAArt.FlagColor("base:faction_player"), HostStepAArt.FlagColor(null));
        }

        [Test]
        public void MissingAttackSheet_FallsBackToSameGuardIdle()
        {
            var frames = (System.Collections.Generic.Dictionary<string, Sprite[]>)typeof(HostStepAArt)
                .GetField("Frames", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            const string key = "ART-CHAR-004_attack_v02";
            var existed = frames.TryGetValue(key, out var previous);
            try
            {
                frames[key] = System.Array.Empty<Sprite>();
                Assert.AreSame(HostStepAArt.CharacterFrame(HostStepAArt.Profile.Guard,
                        HostStepAArt.Action.Idle, HostStepAArt.Direction.East, 0),
                    HostStepAArt.CharacterFrame(HostStepAArt.Profile.Guard,
                        HostStepAArt.Action.Attack, HostStepAArt.Direction.East, 3));
            }
            finally { if (existed) frames[key] = previous; else frames.Remove(key); }
        }

        [Test]
        public void ParentRoomUsage_RefreshesBedSpriteWithoutMovingIt()
        {
            var world = new SimulationWorld();
            var room = new OutdoorConstructedAssetState
            {
                StableAssetId = "test:room", BuildingId = "test:room", Kind = "room",
                SurfaceId = "test:surface", BoundLocationId = "test:room-place",
                WorldX = 0, WorldY = 0, WorldWidth = 8, WorldHeight = 12, CellsW = 8, CellsH = 12,
                RoomUsage = RoomUsage.Residential
            };
            var bed = new OutdoorConstructedAssetState
            {
                StableAssetId = "test:bed", BuildingId = "test:bed", Kind = "bed",
                SurfaceId = "test:surface", BoundLocationId = "test:bed-place", ParentRoomId = room.StableAssetId,
                WorldX = 2, WorldY = 2, WorldWidth = 1, WorldHeight = 2, CellsW = 1, CellsH = 2
            };
            Assert.IsTrue(world.OutdoorConstructedAssets.TryRegister(room));
            Assert.IsTrue(world.OutdoorConstructedAssets.TryRegister(bed));
            var root = HostMapObjectVisualFactory.Create("bed", MapKindCatalog.Cushion, bed.StableAssetId,
                null, new Vector3(2.5f, 3), 1, 2, -12, false);
            try
            {
                var position = root.transform.position;
                foreach (var usage in new[] { RoomUsage.Residential, RoomUsage.Medical, RoomUsage.Prison })
                {
                    room.RoomUsage = usage;
                    HostBedVisualStyle.Apply(root, world, bed.StableAssetId);
                    Assert.AreSame(HostStepAArt.BedSprite(usage),
                        root.transform.Find("StepAArtVisual").GetComponent<SpriteRenderer>().sprite);
                    Assert.AreEqual(position, root.transform.position);
                    Assert.AreEqual(1f, bed.WorldWidth);
                    Assert.AreEqual(2f, bed.WorldHeight);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CropStagesAndHarvest_RefreshOldPresentation()
        {
            foreach (var kind in new[] { "grainField", "herbField" })
            {
                var root = new GameObject("ArtCropTest");
                try
                {
                    var renderer = root.AddComponent<SpriteRenderer>();
                    var plot = root.AddComponent<HostMapPlotCell>();
                    plot.Configure(new SimulationWorld(), "test:" + kind, "test:field", HostInteractSpotKind.Work,
                        kind, 0, 0, kind);
                    var empty = renderer.color;
                    plot.SetPlanted("test:crop");
                    plot.SetCropStage(OutdoorFarmCropStage.Growing, .3f);
                    var growing = renderer.color;
                    plot.SetCropStage(OutdoorFarmCropStage.Mature);
                    Assert.AreNotEqual(growing, renderer.color);
                    plot.SetCropStage(OutdoorFarmCropStage.Empty);
                    Assert.AreEqual(empty, renderer.color);
                    Assert.IsFalse(plot.IsPlanted);
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        static void Tick(EntityView view) => typeof(EntityView)
            .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
    }
}
