# DFF-EXT-001 Traceability Matrix

Status snapshot date: 2026-02-19

Legend:
- ✅ Implemented
- ⚠️ Partial
- ❌ Missing

## 1) C ABI + handle-based boundary

- ✅ Native API versioning and handle-based C ABI
  - `engine/native/include/engine_native.h` (`ENGINE_NATIVE_API_VERSION`, handle typedefs, C entry points)
- ✅ Coarse-grained renderer/physics calls
  - Renderer: `renderer_begin_frame*`, `renderer_submit*`, `renderer_present*`
  - Physics: `physics_sync_from_world*`, `physics_step*`, `physics_sync_to_world*`

## 2) Code-First Content Pipeline

- ✅ Recipes and generators
  - `engine/managed/src/Engine.Content/IAssetRecipe.cs`
- ✅ AssetKey composition (GeneratorId/versions/hash/build-config)
  - `engine/managed/src/Engine.Content/AssetKey.cs`
- ✅ Asset registry + `[DffAsset]`
  - `engine/managed/src/Engine.Content/DffAssetAttribute.cs`
  - `engine/managed/src/Engine.Content/AssetRegistry*.cs`
- ✅ Dev/runtime cache + bake queue behavior
  - `engine/managed/src/Engine.Content/InMemoryAssetsProvider.cs`
  - `engine/managed/src/Engine.Content/MountedContentAssetsProvider.cs`
- ✅ Blob/pak format versioning (mesh/texture/material/sound)
  - `MeshBlobCodec.cs`, `TextureBlobCodec.cs`, `MaterialBlobCodec.cs`, `SoundBlobCodec.cs`

## 3) Procedural content

- ✅ MeshBuilder + LOD chain support
  - `engine/managed/src/Engine.Procedural/MeshBuilder.cs`
- ✅ Procedural textures/maps (noise/fbm/patterns/derived maps/mips)
  - `TextureBuilder*.cs`, `TextureMaps.cs`
- ✅ Material templates (DffLitPbr/Unlit/Decal/Ui)
  - `engine/managed/src/Engine.Procedural/MaterialTemplates.cs`
- ✅ Level generation
  - `engine/managed/src/Engine.Procedural/LevelGenerator.cs`
- ✅ Procedural animation (tween/timeline/look-at/aim)
  - `engine/managed/src/Engine.Procedural/ProceduralAnimation.cs`

## 4) UI

- ✅ Retained tree + layout + style/themes + interaction components
  - `engine/managed/src/Engine.UI/*`
  - Components: `UiPanel`, `UiText`, `UiImage`, `UiButton`, `UiToggle`, `UiSlider`, `UiInputField`, `UiScrollView`, `UiList`, `UiVirtualizedList`
- ✅ Text layout requirements (wrapping/alignment/clipping) and kerning support in atlas/layout
  - `UiFontAtlas.cs`, `RetainedUiFacade.Draw.cs`
- ✅ Native UI module/API parity (dedicated `renderer_ui_*` direct and handle C APIs)
  - `engine/native/include/engine_native.h`
  - `engine/native/src/bridge_capi/renderer_capi.cpp`
  - `engine/native/src/bridge_capi/handle_capi_render_capture.cpp`

## 5) Rendering (native)

- ✅ RenderGraph-based frame construction
  - `engine/native/src/render/render_graph.*`
  - `engine/native/src/render/frame_graph_builder.*`
- ✅ Pipeline stages present: shadow, pbr, ao, bloom, tonemap, color grading, fxaa, ui, present
  - `frame_graph_builder.cpp`
- ✅ Debug/counters and backend reporting surfaced in tests/doctor/NFR
  - `engine/native/tests/native_tests.cpp`
  - `engine/tools/engine-cli/Cli/EngineCliApp.Doctor.cs`
  - `engine/tools/engine-cli/Cli/EngineCliApp.Nfr.cs`
- ✅ Forward+/CSM capability explicitly represented via render feature flags and covered by native render tests
  - `ENGINE_NATIVE_RENDER_FLAG_REQUIRE_FORWARD_PLUS`, `ENGINE_NATIVE_RENDER_FLAG_REQUIRE_CSM` in `engine/native/include/engine_native.h`
  - `engine/native/tests/native_tests.cpp`

## 6) Physics

- ✅ Batched sync + step + query path (raycast/sweep/overlap), deterministic behavior, strict input validation
  - `engine/native/src/core/engine_state.cpp`
  - `engine/native/src/core/physics_raycast.cpp`
  - `engine/native/src/core/physics_queries.cpp`
  - `engine/native/tests/native_tests.cpp`
- ✅ Managed wrappers and character controller
  - `engine/managed/src/Engine.Physics/*`
  - `engine/managed/tests/Engine.Tests/Physics/*`
- ✅ Collider set includes static mesh (`collider_shape=3` + `collider_mesh` binding)
  - Native: `engine/native/include/engine_native.h`, `engine/native/src/core/engine_state.cpp`, `engine/native/src/core/physics_queries.cpp`, `engine/native/src/core/physics_raycast.cpp`
  - Managed: `engine/managed/src/Engine.Physics/ColliderShapeType.cs`, `engine/managed/src/Engine.Physics/PhysicsCollider.cs`, `engine/managed/src/Engine.Physics/PhysicsShapeValidation.cs`, `engine/managed/src/Engine.NativeBindings/Internal/*`

## 7) Audio

- ✅ Native mixer path with buses + spatial attenuation + lowpass/reverb-send parameters
  - `engine/native/src/core/audio_state.cpp`
  - `engine/native/tests/audio/audio_capi_tests.cpp`
- ✅ Procedural sound recipes (oscillator/ADSR/filter/LFO) + ambience director
  - `engine/managed/src/Engine.Audio/*`
  - `engine/managed/tests/Engine.Tests/Audio/*`

## 8) Multiplayer / networking

- ✅ Client/server in-memory model, snapshots, rpc, ownership, procedural recipe refs
  - `engine/managed/src/Engine.Net/*`
  - `NetSnapshotBinaryCodec.cs`, `NetRpcBinaryCodec.cs`
- ✅ Deterministic tick clock + interpolation window + sampled interpolation alpha API
  - `DeterministicNetClock.cs`
  - `ClientInterpolationBuffer.cs`
  - `InMemoryNetSession.cs` (`TryGetClientInterpolationWindow`, `TrySampleClientInterpolation`)
- ✅ Multiplayer strict runtime artifacts and cross-validation in doctor/NFR
  - `engine/tools/engine-cli/Cli/MultiplayerDemoArtifactGenerator.cs`
  - `EngineCliApp.Doctor.cs`, `EngineCliApp.Nfr.cs`

## 9) Testing / capture / replay / artifacts

- ✅ Capture request→poll→free, screenshot + dumps, manifest and replay artifacts
  - Native: `capture_*` APIs in `engine_native.h`, `capture_capi.cpp`
  - Managed/testing/CLI: `Engine.Testing/*`, `ArtifactOutputs.cs`, doctor/NFR checks
- ✅ Golden compare supports pixel-perfect and tolerant (MAE/PSNR)
  - `engine/managed/src/Engine.Testing/GoldenImageComparison.cs`

## 10) CLI / packaging / DoD paths

- ✅ CLI commands surface exists: new/run/build/bake/preview/test/pack/doctor/api dump (+ multiplayer demo/orchestrate, nfr proof)
  - `engine/tools/engine-cli/Cli/EngineCliParser.cs`
  - `engine/tools/engine-cli/Cli/EngineCliApp*.cs`
- ✅ Pack path supports `win-x64` and `linux-x64`, self-contained publish, portable package layout
  - `engine/tools/engine-cli/Cli/EngineCliApp.Pack.cs`
- ✅ Clean-machine packaging acceptance evidence is automated in CI matrix (`windows-latest` + `ubuntu-latest`) with artifact upload
  - `.github/workflows/ci.yml` (`pack-clean-machine` job)

## Remaining high-priority gaps (actionable)

- ✅ No open high-priority gaps remain for `DFF-EXT-001` in this repository snapshot.
