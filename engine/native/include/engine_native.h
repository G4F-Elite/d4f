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

#define ENGINE_NATIVE_API_VERSION 8u

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

typedef struct engine_native_input_snapshot {
  uint64_t frame_index;
  uint32_t buttons_mask;
  float mouse_x;
  float mouse_y;
} engine_native_input_snapshot_t;

typedef struct engine_native_window_events {
  uint8_t should_close;
  uint32_t width;
  uint32_t height;
} engine_native_window_events_t;

typedef struct engine_native_draw_item {
  engine_native_resource_handle_t mesh;
  engine_native_resource_handle_t material;
  float world[16];
  uint32_t sort_key_high;
  uint32_t sort_key_low;
} engine_native_draw_item_t;

typedef struct engine_native_ui_draw_item {
  engine_native_resource_handle_t texture;
  uint32_t vertex_offset;
  uint32_t vertex_count;
  uint32_t index_offset;
  uint32_t index_count;
} engine_native_ui_draw_item_t;

typedef enum engine_native_debug_view_mode {
  ENGINE_NATIVE_DEBUG_VIEW_NONE = 0,
  ENGINE_NATIVE_DEBUG_VIEW_DEPTH = 1,
  ENGINE_NATIVE_DEBUG_VIEW_NORMALS = 2,
  ENGINE_NATIVE_DEBUG_VIEW_ALBEDO = 3,
  ENGINE_NATIVE_DEBUG_VIEW_ROUGHNESS = 4,
  ENGINE_NATIVE_DEBUG_VIEW_AMBIENT_OCCLUSION = 5
} engine_native_debug_view_mode_t;

typedef struct engine_native_render_packet {
  const engine_native_draw_item_t* draw_items;
  uint32_t draw_item_count;
  const engine_native_ui_draw_item_t* ui_items;
  uint32_t ui_item_count;
  uint8_t debug_view_mode;
  uint8_t reserved0;
  uint8_t reserved1;
  uint8_t reserved2;
} engine_native_render_packet_t;

typedef struct engine_native_renderer_frame_stats {
  uint32_t draw_item_count;
  uint32_t ui_item_count;
  uint32_t executed_pass_count;
  uint32_t reserved0;
  uint64_t present_count;
  uint64_t pipeline_cache_hits;
  uint64_t pipeline_cache_misses;
  uint64_t pass_mask;
} engine_native_renderer_frame_stats_t;

typedef enum engine_native_capture_format {
  ENGINE_NATIVE_CAPTURE_FORMAT_RGBA8_UNORM = 1
} engine_native_capture_format_t;

typedef struct engine_native_capture_request {
  uint32_t width;
  uint32_t height;
  uint8_t include_alpha;
  uint8_t reserved0;
  uint8_t reserved1;
  uint8_t reserved2;
} engine_native_capture_request_t;

typedef struct engine_native_capture_result {
  uint32_t width;
  uint32_t height;
  uint32_t stride;
  uint32_t format;
  const uint8_t* pixels;
  size_t pixel_bytes;
} engine_native_capture_result_t;

typedef struct engine_native_body_write {
  engine_native_resource_handle_t body;
  float position[3];
  float rotation[4];
  float linear_velocity[3];
  float angular_velocity[3];
  uint8_t body_type;
  uint8_t collider_shape;
  uint8_t is_trigger;
  uint8_t reserved0;
  float collider_dimensions[3];
  float friction;
  float restitution;
} engine_native_body_write_t;

typedef struct engine_native_body_read {
  engine_native_resource_handle_t body;
  float position[3];
  float rotation[4];
  float linear_velocity[3];
  float angular_velocity[3];
  uint8_t is_active;
} engine_native_body_read_t;

typedef struct engine_native_raycast_query {
  float origin[3];
  float direction[3];
  float max_distance;
  uint8_t include_triggers;
  uint8_t reserved0;
  uint8_t reserved1;
  uint8_t reserved2;
} engine_native_raycast_query_t;

typedef struct engine_native_raycast_hit {
  uint8_t has_hit;
  uint8_t is_trigger;
  uint8_t reserved0;
  uint8_t reserved1;
  engine_native_resource_handle_t body;
  float distance;
  float point[3];
  float normal[3];
} engine_native_raycast_hit_t;

typedef struct engine_native_sweep_query {
  float origin[3];
  float direction[3];
  float max_distance;
  uint8_t include_triggers;
  uint8_t shape_type;
  uint8_t reserved0;
  uint8_t reserved1;
  float shape_dimensions[3];
} engine_native_sweep_query_t;

typedef struct engine_native_sweep_hit {
  uint8_t has_hit;
  uint8_t is_trigger;
  uint8_t reserved0;
  uint8_t reserved1;
  engine_native_resource_handle_t body;
  float distance;
  float point[3];
  float normal[3];
} engine_native_sweep_hit_t;

typedef struct engine_native_overlap_query {
  float center[3];
  uint8_t include_triggers;
  uint8_t shape_type;
  uint8_t reserved0;
  uint8_t reserved1;
  float shape_dimensions[3];
} engine_native_overlap_query_t;

typedef struct engine_native_overlap_hit {
  engine_native_resource_handle_t body;
  uint8_t is_trigger;
  uint8_t reserved0;
  uint8_t reserved1;
  uint8_t reserved2;
} engine_native_overlap_hit_t;

#ifdef __cplusplus
extern "C" {
#endif

ENGINE_NATIVE_API uint32_t engine_get_native_api_version(void);

ENGINE_NATIVE_API engine_native_status_t engine_create(
    const engine_native_create_desc_t* create_desc,
    engine_native_engine_t** out_engine);

ENGINE_NATIVE_API engine_native_status_t engine_destroy(
    engine_native_engine_t* engine);

ENGINE_NATIVE_API engine_native_status_t engine_pump_events(
    engine_native_engine_t* engine,
    engine_native_input_snapshot_t* out_input,
    engine_native_window_events_t* out_events);

ENGINE_NATIVE_API engine_native_status_t engine_get_renderer(
    engine_native_engine_t* engine,
    engine_native_renderer_t** out_renderer);

ENGINE_NATIVE_API engine_native_status_t engine_get_physics(
    engine_native_engine_t* engine,
    engine_native_physics_t** out_physics);

ENGINE_NATIVE_API engine_native_status_t content_mount_pak(
    engine_native_engine_t* engine,
    const char* pak_path);

ENGINE_NATIVE_API engine_native_status_t content_mount_directory(
    engine_native_engine_t* engine,
    const char* directory_path);

ENGINE_NATIVE_API engine_native_status_t content_read_file(
    engine_native_engine_t* engine,
    const char* asset_path,
    void* buffer,
    size_t buffer_size,
    size_t* out_size);

ENGINE_NATIVE_API engine_native_status_t renderer_begin_frame(
    engine_native_renderer_t* renderer,
    size_t requested_bytes,
    size_t alignment,
    void** out_frame_memory);

ENGINE_NATIVE_API engine_native_status_t renderer_submit(
    engine_native_renderer_t* renderer,
    const engine_native_render_packet_t* packet);

ENGINE_NATIVE_API engine_native_status_t renderer_present(
    engine_native_renderer_t* renderer);

ENGINE_NATIVE_API engine_native_status_t renderer_create_mesh_from_blob(
    engine_native_renderer_t* renderer,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_mesh);

ENGINE_NATIVE_API engine_native_status_t renderer_create_texture_from_blob(
    engine_native_renderer_t* renderer,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_texture);

ENGINE_NATIVE_API engine_native_status_t renderer_create_material_from_blob(
    engine_native_renderer_t* renderer,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_material);

ENGINE_NATIVE_API engine_native_status_t renderer_destroy_resource(
    engine_native_renderer_t* renderer,
    engine_native_resource_handle_t handle);

ENGINE_NATIVE_API engine_native_status_t renderer_get_last_frame_stats(
    engine_native_renderer_t* renderer,
    engine_native_renderer_frame_stats_t* out_stats);

ENGINE_NATIVE_API engine_native_status_t capture_request(
    engine_native_renderer_t* renderer,
    const engine_native_capture_request_t* request,
    uint64_t* out_request_id);

ENGINE_NATIVE_API engine_native_status_t capture_poll(
    uint64_t request_id,
    engine_native_capture_result_t* out_result,
    uint8_t* out_is_ready);

ENGINE_NATIVE_API engine_native_status_t capture_free_result(
    engine_native_capture_result_t* result);

ENGINE_NATIVE_API engine_native_status_t physics_step(
    engine_native_physics_t* physics,
    double dt_seconds);

ENGINE_NATIVE_API engine_native_status_t physics_sync_from_world(
    engine_native_physics_t* physics,
    const engine_native_body_write_t* writes,
    uint32_t write_count);

ENGINE_NATIVE_API engine_native_status_t physics_sync_to_world(
    engine_native_physics_t* physics,
    engine_native_body_read_t* reads,
    uint32_t read_capacity,
    uint32_t* out_read_count);

ENGINE_NATIVE_API engine_native_status_t physics_raycast(
    engine_native_physics_t* physics,
    const engine_native_raycast_query_t* query,
    engine_native_raycast_hit_t* out_hit);

ENGINE_NATIVE_API engine_native_status_t physics_sweep(
    engine_native_physics_t* physics,
    const engine_native_sweep_query_t* query,
    engine_native_sweep_hit_t* out_hit);

ENGINE_NATIVE_API engine_native_status_t physics_overlap(
    engine_native_physics_t* physics,
    const engine_native_overlap_query_t* query,
    engine_native_overlap_hit_t* hits,
    uint32_t hit_capacity,
    uint32_t* out_hit_count);

#ifdef __cplusplus
}
#endif

#endif
