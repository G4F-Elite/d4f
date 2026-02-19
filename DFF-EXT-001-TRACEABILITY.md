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
- ⚠️ Native UI module/API parity is limited (UI draw items are passed via render packet, but no dedicated native ui_* API surface)

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
- ⚠️ Forward+/CSM are not explicitly named as dedicated implementations in current code

## 6) Physics

- ✅ Batched sync + step + query path (raycast/sweep/overlap), deterministic behavior, strict input validation
  - `engine/native/src/core/engine_state.cpp`
  - `engine/native/src/core/physics_raycast.cpp`
  - `engine/native/src/core/physics_queries.cpp`
  - `engine/native/tests/native_tests.cpp`
- ✅ Managed wrappers and character controller
  - `engine/managed/src/Engine.Physics/*`
  - `engine/managed/tests/Engine.Tests/Physics/*`
- ⚠️ Collider set currently: box/sphere/capsule; static mesh collider not yet evident

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
- ⚠️ Formalized “clean machine run” acceptance evidence is external to repo CI/tests (not a direct in-repo system-test artifact)

## Remaining high-priority gaps (actionable)

1. ⚠️ Explicit Forward+/CSM evidence (if required as strict MVP wording)
2. ⚠️ Static mesh collider support in physics (if required by strict P-01 wording)
3. ⚠️ Dedicated native UI module/API parity (if required beyond packeted UI draw data)
