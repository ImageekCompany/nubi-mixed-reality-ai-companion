# 0024. Room awareness: Space Setup offer, fallback floor, real furniture

- Status: accepted
- Date: 2026-11-01

## Context
Nubi has to read as a character standing in the user's real room, not a VR prop. Two gaps broke that:
- **Unscanned rooms.** meta-openxr does not detect planes live; it reads the Space Setup scene model, so a room without Space Setup has no planes and no boxes, ever (`com.unity.xr.meta-openxr@20c9b19f5e28/Documentation~/features/planes.md:43`, `bounding-boxes.md:14`). With no Floor plane `FloorNavMeshBuilder` baked nothing, `come_here` answered `unreachable` and the device self-test reported `voice_come_here` DEGRADED `navmesh=no`. Nubi stood still and the app never told the user why.
- **Furniture was invisible to the model.** `walk_to` could resolve a furniture word to a plane or box centre, but the model was never told what furniture exists, Nubi walked to a point under the box and sampled the nearest floor (any side), never faced it, and seen objects could only be named by `obj_N`.

## Decision

### Room-scan flow (`Jolly.Core.Spatial`)
- **`RoomScanDetector`** (pure): `NoPermission` until `USE_SCENE` is granted; then `Scanned` once a Floor plane exists, otherwise `Pending` for `roomScanTimeoutSeconds` (8 s) and then `Unscanned`. `Restart` re-arms the timeout when the app resumes.
- **`RoomScanMonitor : IRoomScan`** polls every 0.5 s (`AppGraph` loop `room`) and, while `Unscanned`:
  - keeps a **fallback floor** around the user (below);
  - raises `OfferScan` **once per session** when Nubi is idle and silent. `AppGraph` plays the new canned line `room_scan_01` (`LineCategory.RoomScanOffer`, baked en clip, pl caption) through `InteractionLinePlayer.Request`;
  - opens Space Setup when the model calls `request_room_scan`. The request is queued until Nubi is idle and silent (at most 10 s), because the app pauses as soon as Space Setup opens and would cut the reply off.
- **Resume.** `JollyBootstrap.OnApplicationPause(false)` / `OnApplicationFocus(true)` → `AppGraph.Resume` → `RoomScanMonitor.OnResumed`: unless already scanned, `ISceneData.Requery()` and a fresh timeout. The fallback floor stays until the new floor arrives or the timeout passes again.
- The scene-capture call itself is `MetaOpenXRSessionSubsystem.TryRequestSceneCapture()` (`Runtime/Subsystems/Session/MetaOpenXRSessionSubsystem.cs:31-44`): it returns false unless `XR_FB_scene_capture` is enabled, which `ARSessionFeature` declares (`ARSessionFeature.cs:20`) and our Android OpenXR settings enable (`Assets/XR/Settings/OpenXR Package Settings.asset:2219-2221`). On success the app pauses on a later frame and resumes when the user finishes (`Documentation~/features/session.md:26-33`). It is reached through `ARSession.subsystem` (`SubsystemLifecycleManager.cs:28`; `ARSession` derives from it, `ARSession.cs:25-26`), as in the package sample (`Tests/Runtime/CodeSamples/MetaSceneCaptureSample.cs:13-17`), but with the `ARSession` bound in `SceneRefs.session` instead of `FindAnyObjectByType`.
- **Requery** stops and restarts the plane and box managers (`SubsystemLifecycleManager.cs:61-91` starts the subsystem in `OnEnable`, `:109-115` stops it in `OnDisable`). Whether the Meta provider reads a new scene model on restart is **UNVERIFIED** (device checklist).

### Fallback floor
- `FallbackFloorArea` (pure): a 24-sided polygon of radius `fallbackFloorRadius` (1.5 m) centred on the user's floor position, re-centred once the user is 0.5 m from its centre.
- `ArSceneData.SetFallbackFloor` puts it at the XR Origin's height (`XROrigin.Origin`, `XROrigin.cs:128`) only when `RequestedTrackingOriginMode == Floor` (`XROrigin.cs:69,159`; Main.unity sets `2` = Floor). Any other mode refuses rather than guess a floor height.
- `FloorNavMeshBuilder` bakes it as one more walkable `MeshCollider` (layer NavGeometry) through the existing `NavMeshBaker`, so the 2 s rebuild throttle and the floor-height sample filter apply unchanged. `Physics.SyncTransforms()` runs after moving it, because `NavMeshBaker` takes the bake volume from `collider.bounds`.
- `IWorldQuery.WalkableArea` (`None` / `Fallback` / `Scanned`, replacing `HasWalkableArea`) reports it; the first placement logs `evt=room.degraded walkable=fallback radius_m=1.5` at Warning.
- Only with the scene permission granted: a denied `USE_SCENE` keeps DoD 3.4's `unreachable`.

### Real furniture
- `IWorldQuery.GetSurfaces(List<SceneSurface>)` replaces `TryFindLabeledSurface`. `ArWorldQuery` lists every classified `ARBoundingBox` (`classifications`, `size`: `ARBoundingBox.cs:20,25`; flags `BoundingBoxClassifications.cs:14-66`) and every `HorizontalUp` `ARPlane` classified Couch/Table/Seat (`alignment`, `classifications`, `center`, `size`: `ARPlane.cs:62,81,91,101`; `PlaneClassifications.cs:18-40`; `PlaneAlignment.cs:17`). `SurfaceLabels` names them `couch, table, bed, chair, storage, screen, lamp, plant`.
- `SurfaceTargets` (pure) picks the nearest surface with a label and the floor spot 0.25 m outside the footprint edge nearest to Nubi (footprint turned by the surface's yaw; from inside it, the nearest edge). Nubi looks at the centre of the top, so it stops facing the furniture.
- **`go_to_surface {label: enum couch|table|desk|bed|chair|storage|screen}`** (Locomotion, 3 s, withheld when seated): `room_not_scanned` unless scanned, `no_surface` if none, else `walking`/`unreachable`. **Perching is not offered:** the NavMesh bakes every box as Not Walkable, the agent climbs 0.04 m, and the rig has no climb or sit clips (ADR-0017). Stopping at the edge facing it is the behaviour.
- **`request_room_scan {}`** (Exclusive, 2 s): `scan_unavailable` off Meta's runtime; otherwise queues the scan, speaks `room.scan_opening` and ends the turn.
- **`walk_to`** resolves taught place → furniture (now via `SurfaceTargets`, same as `go_to_surface`) → seen object by id **or label** (`SeenObjectLookup`: exact label anywhere wins, then whole-word match, most recent first) and faces what it walks to. **`look_at`** accepts the same labels and furniture types. Look-at stays the procedural `LookAtDriver` of ADR-0017; `Unity.Animation.Rigging` remains installed but unused.

### Prompt
- The `<context>` block gains one room line after `Scene:` (`RoomContext`): `Room: scanned. Furniture: table 1 m, couch 2.5 m.` (one entry per label, nearest first, half-metre rounding, ≤ 300 chars, labels through `PromptText.Sanitize`), or `Room: not scanned (no Space Setup); walking is limited to about 1.5 m around the user.`, or the permission-denied line; nothing while pending. It changes per turn, so it sits in the per-turn context item after the history (ADR-0020); `PromptPrefixTests` covers a scan arriving between turns.
- One static instruction sentence tells the model the context block can list scanned furniture. Tool descriptions carry the "when to call" guidance. Both change the cache prefix once (new `prompt_cache_key`), then stay static.

### Toggles (`JollyConfig`, header "Room")
`roomScanTimeoutSeconds` (8), `fallbackFloor` (on), `fallbackFloorRadius` (1.5), `offerRoomScan` (on). The offer also needs `Edges.OfferRoomScan`, which only `Edges.ForPlayer` sets: E2E tests and the device self-test do not hear an unprompted line racing their scripted turns.

## Consequences
- Contract changes: `IWorldQuery` (`WalkableArea`, `GetSurfaces`), new `SceneSurface`, `WalkableArea`, `RoomScanStatus`, `IRoomScan`, `ISceneData`, `LineCategory.RoomScanOffer`. Implementations: `ArWorldQuery`, `ArSceneData`, `RoomScanMonitor` and the test fakes.
- New scene binding `SceneRefs.session` (the existing AR Session in Main.unity); the scene validator requires it.
- The self-test `voice_come_here` passes in an unscanned room with `navmesh=fallback` and `walk=Arrived`; it is degraded only with no walkable area at all.
- Requirement 23 says lines play only when the person interacts. The Space Setup offer is the one exception: a setup notice, like the safety notice, spoken at most once per session, only while idle and silent, and switchable off with `offerRoomScan`.
- Cost: one extra NavMesh bake of a 24-gon when the disc moves (throttled to one per 2 s; sub-millisecond in the Editor, unmeasured on Quest), a 0.5 s poll with no allocation, and `GetSurfaces` once per turn for the context line. No new rendering.
- The fallback disc is only as safe as the floor around a standing user: Nubi can walk "through" an unscanned chair within 1.5 m. It is logged as degraded, said in the context line, and gone the moment Space Setup data arrives.
- When the disc moves under a following Nubi, its NavMesh is replaced; `AgentLocomotion` warps back on at the next plan (re-path every 0.25 s).

## Alternatives
- **Depth API meshing (`ARMeshManager`) for a live floor:** rejected for the hackathon: meshing costs GPU/CPU on Quest 3 and needs its own NavMesh bake; the disc gives the "come closer" beat at near-zero cost.
- **A disc centred on Nubi or a union of discs:** rejected; the area around the user is the one known to be clear, and every locomotion target (come here, follow) is user-relative.
- **Fallback with the scene permission denied:** rejected; the user said no to room data and DoD 3.4 expects `unreachable`.
- **Speaking the request line and opening Space Setup in the same frame:** rejected; the app pauses and the reply would be cut mid-word.
- **Perching on furniture:** deferred; it needs NavMesh links onto box tops and authored climb/sit clips.
