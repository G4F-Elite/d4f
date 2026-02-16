# Contributing to diesel-for-free (d4f)

Thanks for contributing to `diesel-for-free`.

This repository contains:

- `engine/native` (C++ runtime)
- `engine/managed` (C# runtime API)
- `engine/tools` (CLI and asset tooling)

Use this guide to keep changes aligned with architecture constraints.

## Development Setup

Prerequisites:

- .NET SDK 9
- CMake 3.20+
- C++20 toolchain

Core verification commands:

```bash
dotnet test dff.sln --nologo
cmake --build engine/native/build --config Debug
ctest --test-dir engine/native/build -C Debug --output-on-failure
```

## Architecture Guardrails

1. Keep one canonical way per concern.
2. Keep managed/native calls coarse-grained and batched.
3. Use handles, not raw native pointers, across boundaries.
4. Extend rendering through RenderGraph, not manual pass sequencing.
5. Keep runtime dependent on compiled assets only.
6. Bump interop version when ABI contracts change.

Full rules for agents and advanced contributors:

- `LLM_ENGINE_GUIDE.md`

## Coding Expectations

- Keep changes minimal and focused.
- Avoid silent fallback behavior that hides failures.
- Preserve module boundaries.
- Add or update tests for all behavior changes.
- Update documentation when behavior or workflows change.

## Commit and PR Expectations

- Use clear commit messages.
- Keep commits logically grouped.
- Ensure tests pass before opening PR.
- Include impact summary and verification steps in PR description.

Use pull request template:

- `.github/PULL_REQUEST_TEMPLATE.md`

## Reporting Issues

Use issue templates:

- Bug report: `.github/ISSUE_TEMPLATE/bug_report.md`
- Feature request: `.github/ISSUE_TEMPLATE/feature_request.md`

Security reports:

- `SECURITY.md`
