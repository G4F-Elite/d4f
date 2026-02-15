# dff tools

## Golden path

1. Create a new game project:
   `dotnet run --project engine/tools/engine-cli -- init --name MyGame --output .`
2. Prepare assets:
   Create `MyGame/assets/manifest.json` and make sure every file in `assets[].path` exists.
3. Build the project:
   `dotnet run --project engine/tools/engine-cli -- build --project MyGame --configuration Debug`
4. Pack assets:
   `dotnet run --project engine/tools/engine-cli -- pack --project MyGame --manifest assets/manifest.json --output dist/content.pak`
5. Run:
   `dotnet run --project engine/tools/engine-cli -- run --project MyGame --configuration Debug`

## Standalone asset compiler

- Build pak from manifest:
  `dotnet run --project engine/tools/assetc -- build --manifest MyGame/assets/manifest.json --output MyGame/dist/content.pak`
- List pak entries:
  `dotnet run --project engine/tools/assetc -- list --pak MyGame/dist/content.pak`

If manifest or pak is invalid, or referenced files are missing, commands exit with code `1`.
