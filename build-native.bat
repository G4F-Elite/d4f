@echo off
setlocal EnableExtensions

set "CONFIG="
set "NO_PAUSE=0"

:parse_args
if "%~1"=="" goto :after_parse
if /I "%~1"=="--no-pause" (
  set "NO_PAUSE=1"
) else (
  if not defined CONFIG (
    set "CONFIG=%~1"
  ) else (
    echo [WARN] Extra argument "%~1" was ignored.
  )
)
shift
goto :parse_args

:after_parse
if not defined CONFIG set "CONFIG=Debug"

set "BUILD_DIR=engine\native\build"
set "SOURCE_DIR=engine\native"

if not exist "%SOURCE_DIR%\CMakeLists.txt" (
  echo [ERROR] Cannot find native source directory: "%SOURCE_DIR%"
  set "EXIT_CODE=1"
  goto :finish
)

if not exist "%BUILD_DIR%" (
  echo [INFO] Native build directory not found. Running CMake configure...
  cmake -S "%SOURCE_DIR%" -B "%BUILD_DIR%"
  if errorlevel 1 (
    echo [ERROR] CMake configure failed.
    set "EXIT_CODE=1"
    goto :finish
  )
)

echo [INFO] Building native target with config: %CONFIG%
cmake --build "%BUILD_DIR%" --config %CONFIG%
if errorlevel 1 (
  echo [ERROR] Native build failed.
  set "EXIT_CODE=1"
  goto :finish
)

echo [DONE] Native build completed.
set "EXIT_CODE=0"
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
