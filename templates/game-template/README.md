# game-template

Base game project template used by `engine-cli init`.

Structure:
- `project.json` - game metadata with `__GAME_NAME__` token.
- `src/Game.Runtime/` - bootstrap runtime project (renamed to `<GameName>.Runtime` on init).
- `assets/` - source assets and valid `manifest.json` (versioned).
- `tests/` - project tests placeholder.
