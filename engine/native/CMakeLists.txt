cmake_minimum_required(VERSION 3.20)

project(dff_native LANGUAGES C CXX)

set(DFF_NATIVE_PUBLIC_INCLUDE_DIR ${CMAKE_CURRENT_SOURCE_DIR}/include)
set(DFF_NATIVE_PRIVATE_INCLUDE_DIR ${CMAKE_CURRENT_SOURCE_DIR}/src)

function(dff_native_configure_target target_name)
  target_compile_features(${target_name} PUBLIC cxx_std_20)
  target_include_directories(${target_name}
    PUBLIC
      ${DFF_NATIVE_PUBLIC_INCLUDE_DIR}
    PRIVATE
      ${DFF_NATIVE_PRIVATE_INCLUDE_DIR}
  )

  if(NOT WIN32)
    set_target_properties(${target_name} PROPERTIES
      POSITION_INDEPENDENT_CODE ON)
  endif()

  if(MSVC)
    set_target_properties(${target_name} PROPERTIES
      MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
  endif()
endfunction()

#
# Native module libraries (aligned with DFF-EXT-001 module decomposition).
#
add_library(dff_platform STATIC
  src/platform/platform_state.cpp
)
dff_native_configure_target(dff_platform)

add_library(dff_content_runtime STATIC
  src/content/content_runtime.cpp
  src/content/pak_index.cpp
)
dff_native_configure_target(dff_content_runtime)

add_library(dff_render STATIC
  src/render/frame_graph_builder.cpp
  src/render/material_system.cpp
  src/render/render_graph.cpp
)
dff_native_configure_target(dff_render)

add_library(dff_vulkan STATIC
  src/rhi/pipeline_state_cache.cpp
  src/rhi/rhi_device.cpp
)
dff_native_configure_target(dff_vulkan)

add_library(dff_physics STATIC
  src/core/physics_raycast.cpp
  src/core/physics_queries.cpp
)
dff_native_configure_target(dff_physics)

add_library(dff_audio STATIC
  src/core/audio_state.cpp
)
dff_native_configure_target(dff_audio)

add_library(dff_capture STATIC
  src/core/capture_store.cpp
)
dff_native_configure_target(dff_capture)

add_library(dff_net STATIC
  src/core/net_state.cpp
)
dff_native_configure_target(dff_net)

add_library(dff_core STATIC
  src/core/engine_state.cpp
)
dff_native_configure_target(dff_core)
target_link_libraries(dff_core
  PUBLIC
    dff_audio
    dff_capture
    dff_content_runtime
    dff_net
    dff_physics
    dff_platform
    dff_render
    dff_vulkan
)

add_library(dff_bridge_capi OBJECT
  src/bridge_capi/handle_registry.cpp
  src/bridge_capi/handle_capi_engine_content.cpp
  src/bridge_capi/handle_capi_render_capture.cpp
  src/bridge_capi/handle_capi_audio_net_physics.cpp
  src/bridge_capi/engine_capi.cpp
  src/bridge_capi/net_capi.cpp
  src/bridge_capi/content_capi.cpp
  src/bridge_capi/renderer_capi.cpp
  src/bridge_capi/physics_capi.cpp
  src/bridge_capi/capture_capi.cpp
  src/bridge_capi/audio_capi.cpp
)
dff_native_configure_target(dff_bridge_capi)
target_compile_definitions(dff_bridge_capi PRIVATE
  DFF_NATIVE_BUILD_SHARED
  DFF_NATIVE_EXPORTS
)

#
# Single exported C ABI shared library.
#
add_library(dff_native SHARED
  src/bridge_capi/native_library_anchor.cpp
  $<TARGET_OBJECTS:dff_bridge_capi>
)
dff_native_configure_target(dff_native)
target_compile_definitions(dff_native PRIVATE
  DFF_NATIVE_BUILD_SHARED
  DFF_NATIVE_EXPORTS
)
target_link_libraries(dff_native
  PRIVATE
    dff_core
)

if(UNIX AND NOT APPLE)
  set_target_properties(dff_native PROPERTIES
    BUILD_RPATH "$ORIGIN"
    INSTALL_RPATH "$ORIGIN")
endif()

include(CTest)

if(BUILD_TESTING)
  add_executable(dff_native_tests
    tests/native_tests.cpp
    tests/content/content_runtime_tests.cpp
    tests/core/engine_pipeline_cache_persistence_tests.cpp
    tests/platform/platform_state_tests.cpp
    tests/render/frame_graph_builder_tests.cpp
    tests/render/material_system_tests.cpp
    tests/render/render_graph_tests.cpp
    tests/rhi/pipeline_state_cache_tests.cpp
    tests/rhi/rhi_device_tests.cpp
    src/platform/platform_state.cpp
    src/render/frame_graph_builder.cpp
    src/render/material_system.cpp
    src/render/render_graph.cpp
    src/rhi/pipeline_state_cache.cpp
    src/rhi/rhi_device.cpp
  )

  target_compile_features(dff_native_tests PRIVATE cxx_std_20)

  target_include_directories(dff_native_tests PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    ${CMAKE_CURRENT_SOURCE_DIR}/tests
  )

  target_link_libraries(dff_native_tests PRIVATE dff_native)

  if(MSVC)
    set_target_properties(dff_native_tests PROPERTIES
      MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
  endif()

  add_test(NAME dff_native_tests COMMAND dff_native_tests)

  add_executable(dff_native_capture_tests
    tests/capture/capture_capi_tests.cpp
  )

  target_compile_features(dff_native_capture_tests PRIVATE cxx_std_20)

  target_include_directories(dff_native_capture_tests PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    ${CMAKE_CURRENT_SOURCE_DIR}/tests
  )

  target_link_libraries(dff_native_capture_tests PRIVATE dff_native)

  if(MSVC)
    set_target_properties(dff_native_capture_tests PROPERTIES
      MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
  endif()

  add_test(NAME dff_native_capture_tests COMMAND dff_native_capture_tests)

  add_executable(dff_native_audio_tests
    tests/audio/audio_capi_tests.cpp
  )

  target_compile_features(dff_native_audio_tests PRIVATE cxx_std_20)

  target_include_directories(dff_native_audio_tests PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    ${CMAKE_CURRENT_SOURCE_DIR}/tests
  )

  target_link_libraries(dff_native_audio_tests PRIVATE dff_native)

  if(MSVC)
    set_target_properties(dff_native_audio_tests PROPERTIES
      MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
  endif()

  add_test(NAME dff_native_audio_tests COMMAND dff_native_audio_tests)

  add_executable(dff_native_net_tests
    tests/net/net_capi_tests.cpp
  )

  target_compile_features(dff_native_net_tests PRIVATE cxx_std_20)

  target_include_directories(dff_native_net_tests PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    ${CMAKE_CURRENT_SOURCE_DIR}/tests
  )

  target_link_libraries(dff_native_net_tests PRIVATE dff_native)

  if(MSVC)
    set_target_properties(dff_native_net_tests PROPERTIES
      MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
  endif()

  add_test(NAME dff_native_net_tests COMMAND dff_native_net_tests)

  add_executable(dff_native_handle_tests
    tests/handle/handle_capi_tests.cpp
  )

  target_compile_features(dff_native_handle_tests PRIVATE cxx_std_20)

  target_include_directories(dff_native_handle_tests PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    ${CMAKE_CURRENT_SOURCE_DIR}/tests
  )

  target_link_libraries(dff_native_handle_tests PRIVATE dff_native)

  if(MSVC)
    set_target_properties(dff_native_handle_tests PROPERTIES
      MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
  endif()

  add_test(NAME dff_native_handle_tests COMMAND dff_native_handle_tests)
endif()
