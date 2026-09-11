# W2A Surface Geography handoff (2026-09-11)

## Checked-in source and bake

- Authoring source: `ContentAuthoring/Worlds/w2a_surface_geography_source_v1.json`.
- Checked-in runtime bake: `Content/BaseGame/Data/Worlds/w2a_surface_geography_baked_v1.json`.
- Unity menu: `XianXia > Content > Bake W2A Surface Geography`.
- The bake reads `main_wilderness_surface_v1.json` for Surface origin, cell size and chunk size. Those metrics are not duplicated in authoring.
- To move the bridge, edit feature `w2a:bridge:north_crossing` (`worldX/worldY/worldWidth/worldHeight`) and move the crossing segment in `w2a:road:bank_bridge_road.points` with it, then run the menu. The bake rejects a bridge that does not span its declared river or connect to its declared road.
- Bake writes a temporary file, parses and validates it, then atomically replaces the last good output. It only emits the 5x5 W2A coverage.

## Main Surface location

- Coverage chunks: X=7..11, Y=8..12.
- World bounds (half-open): X=[8.40,15.40), Y=[9.80,16.80).
- West landmark: `W2A 西岸起点`, WorldPosition=(9.10,11.15), chunk (7,8).
- East landmark: `W2A 东岸终点`, WorldPosition=(14.62,12.00), chunk (11,9).
- Bridge center is approximately (12.10,14.72), across the X=12.60 chunk seam and outside the west start's initial radius-1 neighborhood.
- From a normal NewGame at the Huangcun opening area (arrival near 6.50,11.25), stay on the Main Continuous Surface and travel east (+X / screen right) about 2.6 world units. Enter the W2A bounds at X=8.40 and continue to the labelled west landmark at (9.10,11.15).

## One bake, three consumers

The source SHA-256, revision, coverage and +X east/+Y north coordinates are carried by one bake.

1. `chunkRows` is the per-chunk visual/passability mask. Loader validation compares every chunk cell against the whole-region rows. The active radius-1 streamer materializes only loaded chunks, and its composite WalkGrid overlays water/solid without clearing Site or dynamic blockers.
2. `rows` creates the complete W2A `SurfaceGroundNavigation` in Core. It remains available when bridge chunks have no GameObjects. PlayerParty planning uses it first only when both physical endpoints are covered, then the existing LocalVisible executor consumes successive safe route points through the loaded WalkGrid.
3. `mapPrimitives`, landmarks and navigation WorldBounds are drawn through the same `HexMapViewportProjection`. The geography toggle does not replace the Hex/faction/army layers. Surface-route preview uses the actual planned WorldPosition route.

Water and solid cells block. Bridge cells are walkable only after bake-time river/road connection validation. Road cells are visual/cost metadata and never erase water, rock, Site, flag, door or other blockers. The optional `hexSummary` remains derived-only and does not mutate HexWorld.

Outside the complete W2A cells, PlayerParty retains existing Hex terrain/passability compatibility. A segment crossing the coverage boundary must satisfy the legacy side and the covered destination cell. FormalArmy remains entirely Hex-based.

## Focused producer route

1. Reach the west landmark. Right-click water around (12.0,12.0): it must be rejected. Right-click ordinary road/ground: it may plan normally.
2. Open WorldMap and right-click the east landmark/nearby east ground. The cyan Surface preview should run north along the west bank, cross near (12.10,14.72), then return southeast. Closing WorldMap executes it through radius-1 streaming.
3. At the bridge, compare river/bridge/party marker on WorldMap. Two empty points such as (13.60,12.00) and (13.90,12.15) are both Hex (8,8) at hexSize=1 but remain distinct physical goals.

Unity Play acceptance remains producer-owned; this handoff does not mark W2A Producer Accepted.
