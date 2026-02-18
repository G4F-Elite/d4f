# game-template

Base game project template used by `engine-cli init`.

Structure:
- `project.json` - game metadata with `__GAME_NAME__` token.
- `src/Game.Runtime/` - runtime demo project (renamed to `<GameName>.Runtime` on init).
  - procedural level + mesh/material/texture generation bootstrap.
  - UI preview tree with virtualized list.
  - deterministic in-memory multiplayer snapshot bootstrap.
  - procedural audio blob generation bootstrap.
- `assets/` - source assets and valid `manifest.json` (versioned).
- `tests/` - project tests placeholder.
