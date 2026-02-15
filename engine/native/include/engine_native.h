#ifndef ENGINE_NATIVE_H
#define ENGINE_NATIVE_H

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32) || defined(__CYGWIN__)
#if defined(DFF_NATIVE_BUILD_SHARED)
#if defined(DFF_NATIVE_EXPORTS)
#define ENGINE_NATIVE_API __declspec(dllexport)
#else
#define ENGINE_NATIVE_API __declspec(dllimport)
#endif
#else
#define ENGINE_NATIVE_API
#endif
#else
#define ENGINE_NATIVE_API __attribute__((visibility("default")))
#endif

#define ENGINE_NATIVE_API_VERSION 1u

typedef struct engine_native_engine engine_native_engine_t;
typedef struct engine_native_renderer engine_native_renderer_t;
typedef struct engine_native_physics engine_native_physics_t;

typedef uint64_t engine_native_resource_handle_t;

typedef enum engine_native_status {
  ENGINE_NATIVE_STATUS_OK = 0,
  ENGINE_NATIVE_STATUS_INVALID_ARGUMENT = 1,
  ENGINE_NATIVE_STATUS_INVALID_STATE = 2,
  ENGINE_NATIVE_STATUS_VERSION_MISMATCH = 3,
  ENGINE_NATIVE_STATUS_OUT_OF_MEMORY = 4,
  ENGINE_NATIVE_STATUS_NOT_FOUND = 5,
  ENGINE_NATIVE_STATUS_INTERNAL_ERROR = 100
} engine_native_status_t;

typedef struct engine_native_create_desc {
  uint32_t api_version;
  void* user_data;
} engine_native_create_desc_t;

typedef struct engine_native_render_packet {
  uint32_t entity_id;
  const char* debug_label;
} engine_native_render_packet_t;

#ifdef __cplusplus
extern "C" {
#endif

ENGINE_NATIVE_API engine_native_status_t engine_create(
    const engine_native_create_desc_t* create_desc,
    engine_native_engine_t** out_engine);

ENGINE_NATIVE_API engine_native_status_t engine_destroy(
    engine_native_engine_t* engine);

ENGINE_NATIVE_API engine_native_status_t engine_pump_events(
    engine_native_engine_t* engine);

ENGINE_NATIVE_API engine_native_status_t engine_get_renderer(
    engine_native_engine_t* engine,
    engine_native_renderer_t** out_renderer);

ENGINE_NATIVE_API engine_native_status_t engine_get_physics(
    engine_native_engine_t* engine,
    engine_native_physics_t** out_physics);

ENGINE_NATIVE_API engine_native_status_t renderer_begin_frame(
    engine_native_renderer_t* renderer);

ENGINE_NATIVE_API engine_native_status_t renderer_submit(
    engine_native_renderer_t* renderer,
    const engine_native_render_packet_t* packet,
    engine_native_resource_handle_t* out_submission);

ENGINE_NATIVE_API engine_native_status_t renderer_present(
    engine_native_renderer_t* renderer);

ENGINE_NATIVE_API engine_native_status_t physics_step(
    engine_native_physics_t* physics,
    double dt_seconds);

ENGINE_NATIVE_API engine_native_status_t physics_sync_from_world(
    engine_native_physics_t* physics);

ENGINE_NATIVE_API engine_native_status_t physics_sync_to_world(
    engine_native_physics_t* physics);

#ifdef __cplusplus
}
#endif

#endif