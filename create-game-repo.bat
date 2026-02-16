@echo off
setlocal EnableExtensions

set "GAME_NAME="
set "OUTPUT_DIR="
set "NO_PAUSE=0"

:parse_args
if "%~1"=="" goto :after_parse
if /I "%~1"=="--no-pause" (
  set "NO_PAUSE=1"
) else (
  if not defined GAME_NAME (
    set "GAME_NAME=%~1"
  ) else (
    if not defined OUTPUT_DIR (
      set "OUTPUT_DIR=%~1"
    ) else (
      echo [WARN] Extra argument "%~1" was ignored.
    )
  )
)
shift
goto :parse_args

:after_parse
if not defined GAME_NAME goto :usage

for %%I in ("%~dp0.") do set "D4F_ROOT=%%~fI"
for %%I in ("%D4F_ROOT%\..") do set "DEFAULT_OUTPUT_DIR=%%~fI"

if not defined OUTPUT_DIR set "OUTPUT_DIR=%DEFAULT_OUTPUT_DIR%"

set "ENGINE_CLI_PROJECT=%D4F_ROOT%\engine\tools\engine-cli"
set "GAME_DIR=%OUTPUT_DIR%\%GAME_NAME%"

if not exist "%ENGINE_CLI_PROJECT%\Engine.Cli.csproj" (
  echo [ERROR] Cannot find engine-cli project at: "%ENGINE_CLI_PROJECT%"
  set "EXIT_CODE=1"
  goto :finish
)

if exist "%GAME_DIR%" (
  echo [ERROR] Target directory already exists: "%GAME_DIR%"
  set "EXIT_CODE=1"
  goto :finish
)

echo [INFO] Creating game from d4f template...
dotnet run --project "%ENGINE_CLI_PROJECT%" -- init --name "%GAME_NAME%" --output "%OUTPUT_DIR%"
if errorlevel 1 (
  echo [ERROR] Failed to initialize game project.
  set "EXIT_CODE=1"
  goto :finish
)

if not exist "%GAME_DIR%" (
  echo [ERROR] Game directory was not created: "%GAME_DIR%"
  set "EXIT_CODE=1"
  goto :finish
)

if exist "%GAME_DIR%\.git" (
  rmdir /s /q "%GAME_DIR%\.git"
)
if exist "%GAME_DIR%\.gitmodules" (
  del /f /q "%GAME_DIR%\.gitmodules"
)

if not exist "%GAME_DIR%\.gitignore" (
  (
    echo .vs/
    echo bin/
    echo obj/
    echo dist/
    echo build/
    echo *.user
  ) > "%GAME_DIR%\.gitignore"
)

where git >nul 2>&1
if errorlevel 1 (
  echo [WARN] git is not available in PATH. Project is created without repository initialization.
  echo [DONE] Game created at: "%GAME_DIR%"
  set "EXIT_CODE=0"
  goto :finish
)

pushd "%GAME_DIR%"
git init -b main >nul 2>&1
if errorlevel 1 (
  git init >nul 2>&1
)

git add . >nul 2>&1
git commit -m "Initial scaffold from d4f template" >nul 2>&1
if errorlevel 1 (
  echo [WARN] Initial commit was skipped ^(likely git user.name/user.email is not configured^).
)
popd

echo [DONE] Separate game repository created at: "%GAME_DIR%"
set "EXIT_CODE=0"
goto :finish

:usage
echo Usage:
echo   %~nx0 ^<GameName^> [OutputDirectory] [--no-pause]
echo.
echo Examples:
echo   %~nx0 MyGame
echo   %~nx0 MyGame D:\Games
echo   %~nx0 MyGame --no-pause
echo.
echo Default output directory:
echo   Parent folder of the current d4f repository.
set "EXIT_CODE=1"
goto :finish

:finish
if "%EXIT_CODE%"=="0" (
  echo [SUCCESS] All operations completed successfully. You can close this window.
) else (
  echo [FAILED] Script finished with errors. Review messages above.
)

if not "%NO_PAUSE%"=="1" (
  echo.
  pause
)

exit /b %EXIT_CODE%
