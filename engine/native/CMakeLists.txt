cmake_minimum_required(VERSION 3.20)

project(dff_native LANGUAGES C CXX)

add_library(dff_native SHARED
  src/bridge_capi/engine_capi.cpp
  src/bridge_capi/renderer_capi.cpp
  src/bridge_capi/physics_capi.cpp
  src/core/engine_state.cpp
  src/core/physics_raycast.cpp
  src/core/physics_queries.cpp
  src/platform/platform_state.cpp
  src/render/frame_graph_builder.cpp
  src/render/render_graph.cpp
  src/rhi/rhi_device.cpp
)

target_compile_features(dff_native PUBLIC cxx_std_20)

target_compile_definitions(dff_native PRIVATE
  DFF_NATIVE_BUILD_SHARED
  DFF_NATIVE_EXPORTS
)

target_include_directories(dff_native
  PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
  PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src
)

include(CTest)

if(BUILD_TESTING)
  add_executable(dff_native_tests
    tests/native_tests.cpp
    tests/platform/platform_state_tests.cpp
    tests/render/frame_graph_builder_tests.cpp
    tests/render/render_graph_tests.cpp
    tests/rhi/rhi_device_tests.cpp
    src/platform/platform_state.cpp
    src/render/frame_graph_builder.cpp
    src/render/render_graph.cpp
    src/rhi/rhi_device.cpp
  )

  target_compile_features(dff_native_tests PRIVATE cxx_std_20)

  target_include_directories(dff_native_tests PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    ${CMAKE_CURRENT_SOURCE_DIR}/tests
  )

  target_link_libraries(dff_native_tests PRIVATE dff_native)

  add_test(NAME dff_native_tests COMMAND dff_native_tests)
endif()
