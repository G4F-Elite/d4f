# dff tools

## Golden path

1. Create a new game project:
   `dotnet run --project engine/tools/engine-cli -- init --name MyGame --output .`
   This creates a ready-to-pack template including `src/MyGame.Runtime/MyGame.Runtime.csproj`.
2. Prepare assets:
   Create `MyGame/assets/manifest.json` and make sure every file in `assets[].path` exists.
3. Build the project:
   `dotnet run --project engine/tools/engine-cli -- build --project MyGame --configuration Debug`
4. Pack assets:
   `dotnet run --project engine/tools/engine-cli -- pack --project MyGame --manifest assets/manifest.json --output dist/content.pak --runtime win-x64 --configuration Release --zip dist/MyGame.zip`
5. Run:
   `dotnet run --project engine/tools/engine-cli -- run --project MyGame --configuration Debug`

`pack` produces:
- `dist/content.pak`, `dist/compiled.manifest.bin`, `dist/compiled/*`
- portable layout in `dist/package`:
  - `App/` (output of `dotnet publish` when runtime `.csproj` is found or passed via `--publish-project`)
  - `Content/Game.pak`, `Content/compiled.manifest.bin`, `Content/compiled/*`
  - `config/runtime.json`
- optional archive via `--zip`

## Standalone asset compiler

- Build pak from manifest:
  `dotnet run --project engine/tools/assetc -- build --manifest MyGame/assets/manifest.json --output MyGame/dist/content.pak`
- List pak entries:
  `dotnet run --project engine/tools/assetc -- list --pak MyGame/dist/content.pak`

If manifest or pak is invalid, or referenced files are missing, commands exit with code `1`.
