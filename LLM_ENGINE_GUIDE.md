# diesel-for-free (d4f) LLM Engine Guide

This is the single authoritative implementation guide for LLM agents working in this repository.

Naming conventions:

- Product name: `diesel-for-free`
- Public short name: `d4f`
- Internal code prefix: `dff`

---

## 1. Mission and Scope

Use this guide to keep generated code aligned with the engine architecture.

Primary goals:

- Prevent architecture drift
- Keep managed/native boundaries stable
- Preserve one canonical implementation path per concern
- Ship features with tests and predictable packaging

---

## 2. Non-Negotiable Architecture Principles

1. One Golden Path
- Do not introduce parallel patterns for the same problem.
- Reuse existing creation/submission/update patterns.

2. Coarse Managed <-> Native Boundary
- No per-draw interop chatter.
- Use frame-level batches (`RenderPacket`, physics sync arrays).

3. Handle-Based API
- Use value handles (`MeshHandle`, `TextureHandle`, `BodyHandle`, `EntityId`).
- No raw native pointers exposed to gameplay code.

4. RenderGraph Is Mandatory
- Frame execution order must be described by pass/resource dependencies.
- Avoid manual pass ordering outside graph builder.

5. Offline Asset Pipeline
- Runtime consumes compiled artifacts.
- No source format parsing in game runtime loop.

6. Version Everything
- Keep ABI, manifest and binary formats explicitly versioned.
- Bump native API version when binary interop contracts change.

7. Observability by Default
- Track frame stats and stage timings.
- Do not add performance-sensitive code without visibility.

---

## 3. Best-Practice Game Architecture on d4f

Recommended architecture for game projects:

1. Feature-First Gameplay Modules
- Organize by gameplay domain: `Player`, `Combat`, `UI`, `Scenes`.
- Each feature contains its own components/systems.

2. ECS as Runtime Skeleton
- Components are simple data containers.
- Systems own behavior and are registered by stage.

3. Explicit Stage Pipeline
- `PrePhysics` for input/gameplay pre-step.
- Physics sync and fixed stepping.
- `PostPhysics` for gameplay reactions.
- `UI` for retained UI updates.
- `PreRender` for packet preparation.

4. Thin App Entrypoint
- `Program.cs` only boots host.
- `GameApp` configures window/start scene.

5. Asset-Driven Scenes
- Prefer scene/prefab data assets.
- Keep code scenes minimal and orchestration-focused.

---

## 4. Recommended Game Project Layout

Use this shape for generated game projects:

```text
MyGame/
  project.json
  src/
    MyGame.Runtime/
      MyGame.Runtime.csproj
      Program.cs
    MyGame.Gameplay/
      Player/
      Combat/
      UI/
      Scenes/
  assets/
    manifest.json
    ...
  tests/
```

`engine-cli init` provides runtime bootstrap and base content template.

---

## 5. Canonical Frame Execution (Must Preserve Order)

Runtime sequence:

1. Platform event pump
2. Timing update
3. ECS `PrePhysics`
4. Physics sync/step/sync
5. ECS `PostPhysics`
6. ECS `UI` + UI facade update
7. Begin renderer frame arena
8. ECS `PreRender`
9. Build `RenderPacket`
10. Submit packet
11. Present
12. Read frame observability snapshot

Do not reorder this without explicit architecture change and tests.

---

## 6. Render System Rules

Current canonical post chain:

`shadow -> pbr_opaque -> ambient_occlusion -> bloom -> tonemap -> color_grading -> (ui) -> present`

Rules:

- Add new passes through `frame_graph_builder`, not ad-hoc in execution loop.
- Every pass read must be produced earlier or explicitly imported.
- Use unique pass names and unique resource operations per pass.
- Update pass order tests whenever pass topology changes.

Files to touch for render changes:

- `engine/native/src/render/frame_graph_builder.cpp`
- `engine/native/src/render/render_graph.*`
- `engine/native/src/rhi/rhi_device.*`
- `engine/native/src/core/engine_state.cpp`
- native tests under `engine/native/tests/render` and `engine/native/tests/rhi`

---

## 7. Physics Integration Rules

- Gameplay world is authoritative for desired state input.
- Native physics is authoritative for simulated outputs.
- Sync in batches:
  - world -> physics (`SyncFromWorld`)
  - physics fixed steps (`Step`)
  - physics -> world (`SyncToWorld`)
- Queries (`raycast/sweep/overlap`) are explicit API calls with validated input.

Do not encode gameplay policy inside native physics glue.

---

## 8. Managed/Native Interop Rules

1. C ABI only
- Modify `engine_native.h` for contract changes.
- Keep blittable structs stable and explicitly laid out.

2. Version bump required
- If struct layout or function signature changes, bump `ENGINE_NATIVE_API_VERSION`.
- Mirror version in managed constants.

3. Validate arguments strictly
- Return explicit status codes.
- Do not hide invalid state with defaults.

4. Update both sides
- Native bridge implementation
- Managed interop declarations
- Runtime wrapper usage
- Tests for happy path and failure path

---

## 9. Asset and Content Rules

Manifest requirements:

- `version` field required
- `assets[]` entries require `path` and `kind`

Pipeline:

1. Source assets + manifest
2. Compile to `compiled/*` + `compiled.manifest.bin`
3. Build `content.pak`
4. Mount in runtime via content VFS

Runtime must read compiled outputs, not source formats.

---

## 10. How to Compile, Pack and Run Games

Initialize project:

```bash
dotnet run --project engine/tools/engine-cli -- init --name MyGame --output .
```

Build game stubs:

```bash
dotnet run --project engine/tools/engine-cli -- build --project MyGame --configuration Debug
```

Pack game:

```bash
dotnet run --project engine/tools/engine-cli -- pack \
  --project MyGame \
  --manifest assets/manifest.json \
  --output dist/content.pak \
  --runtime win-x64 \
  --configuration Release \
  --zip dist/MyGame.zip
```

Run build stub:

```bash
dotnet run --project engine/tools/engine-cli -- run --project MyGame --configuration Debug
```

Standalone asset compiler:

```bash
dotnet run --project engine/tools/assetc -- build --manifest MyGame/assets/manifest.json --output MyGame/dist/content.pak
dotnet run --project engine/tools/assetc -- list --pak MyGame/dist/content.pak
```

---

## 11. Testing and Definition of Done

A change is done only when:

1. Required tests are added or updated.
2. Full test suites pass.
3. Build is green for affected domains.
4. Interop contracts stay version-consistent.
5. Documentation is updated if behavior changed.

Standard verification commands:

```bash
dotnet test dff.sln --nologo
cmake -S engine/native -B engine/native/build
cmake --build engine/native/build --config Debug
ctest --test-dir engine/native/build -C Debug --output-on-failure
```

---

## 12. LLM Feature Implementation Playbooks

### Add Gameplay Feature

1. Add components in gameplay module.
2. Add systems and register in stage.
3. Add/adjust tests for stage behavior and edge cases.
4. Keep native API untouched unless absolutely needed.

### Add Render Feature (new pass/effect)

1. Extend frame graph builder with new pass and resources.
2. Extend `PassKind` and name mapping.
3. Update pass order tests and frame stats expectations.
4. Keep pass dependency data-driven through graph rules.

### Add Native API Function

1. Update `engine_native.h`.
2. Implement bridge C API function.
3. Wire engine state logic.
4. Update managed interop structs and declarations.
5. Add native + managed tests.
6. Bump API version.

### Add New Asset Kind

1. Extend asset pipeline compiler mapping.
2. Ensure compiled output path and manifest entry are deterministic.
3. Extend runtime loader logic only for compiled format.
4. Add validation tests for missing/invalid inputs.

---

## 13. Anti-Patterns (Forbidden)

- Per-call micro interop from gameplay loops into native draw operations
- Multiple APIs for the same responsibility
- Runtime source asset parsing as a fallback
- Silent error swallowing that returns fake success values
- Injecting gameplay logic into renderer/physics bridge layers
- Bypassing RenderGraph for custom manual pass order

---

## 14. Prompting Guidance for LLM Agents

When requesting code changes, include:

- target subsystem (`render`, `physics`, `assets`, `cli`, `interop`, `ecs`)
- expected stage impact (`PrePhysics`, `UI`, `PreRender`, etc.)
- required test scope (`unit`, `integration`, `native bridge`)
- acceptance criteria and edge cases

When the request affects ABI or pipeline contracts, explicitly ask for:

- version bump
- both managed + native updates
- docs update in `README.md` and this file when appropriate

---

## 15. Repository References

- Human-facing overview: `README.md`
- Contribution flow: `CONTRIBUTING.md`
- CI workflow: `.github/workflows/ci.yml`
- Game template: `templates/game-template`

Keep this file current whenever architecture decisions change.
