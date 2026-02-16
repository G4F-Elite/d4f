@echo off
setlocal EnableExtensions

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Debug"

set "BUILD_DIR=engine\native\build"
set "SOURCE_DIR=engine\native"

if not exist "%SOURCE_DIR%\CMakeLists.txt" (
  echo [ERROR] Cannot find native source directory: "%SOURCE_DIR%"
  exit /b 1
)

if not exist "%BUILD_DIR%" (
  echo [INFO] Native build directory not found. Running CMake configure...
  cmake -S "%SOURCE_DIR%" -B "%BUILD_DIR%"
  if errorlevel 1 (
    echo [ERROR] CMake configure failed.
    exit /b 1
  )
)

echo [INFO] Building native target with config: %CONFIG%
cmake --build "%BUILD_DIR%" --config %CONFIG%
if errorlevel 1 (
  echo [ERROR] Native build failed.
  exit /b 1
)

echo [DONE] Native build completed.
exit /b 0
