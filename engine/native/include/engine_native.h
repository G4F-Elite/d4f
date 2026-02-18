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

#define ENGINE_NATIVE_API_VERSION 14u

typedef struct engine_native_engine engine_native_engine_t;
typedef struct engine_native_renderer engine_native_renderer_t;
typedef struct engine_native_physics engine_native_physics_t;
typedef struct engine_native_audio engine_native_audio_t;
typedef struct engine_native_net engine_native_net_t;

typedef uint64_t engine_native_resource_handle_t;
typedef uint64_t engine_native_engine_handle_t;
typedef uint64_t engine_native_renderer_handle_t;
typedef uint64_t engine_native_physics_handle_t;
typedef uint64_t engine_native_audio_handle_t;
typedef uint64_t engine_native_net_handle_t;

#define ENGINE_NATIVE_INVALID_HANDLE 0ull

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

typedef struct engine_native_string_view {
  const char* data;
  size_t length;
} engine_native_string_view_t;

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
  float scissor_x;
  float scissor_y;
  float scissor_width;
  float scissor_height;
} engine_native_ui_draw_item_t;

typedef struct engine_native_mesh_cpu_data {
  const float* positions;
  uint32_t vertex_count;
  const uint32_t* indices;
  uint32_t index_count;
} engine_native_mesh_cpu_data_t;

typedef struct engine_native_texture_cpu_data {
  const uint8_t* rgba8;
  uint32_t width;
  uint32_t height;
  uint32_t stride;
} engine_native_texture_cpu_data_t;

typedef enum engine_native_debug_view_mode {
  ENGINE_NATIVE_DEBUG_VIEW_NONE = 0,
  ENGINE_NATIVE_DEBUG_VIEW_DEPTH = 1,
  ENGINE_NATIVE_DEBUG_VIEW_NORMALS = 2,
  ENGINE_NATIVE_DEBUG_VIEW_ALBEDO = 3,
  ENGINE_NATIVE_DEBUG_VIEW_ROUGHNESS = 4,
  ENGINE_NATIVE_DEBUG_VIEW_AMBIENT_OCCLUSION = 5
} engine_native_debug_view_mode_t;

#define ENGINE_NATIVE_RENDER_FLAG_DISABLE_AUTO_EXPOSURE 0x01u
#define ENGINE_NATIVE_RENDER_FLAG_DISABLE_JITTER_EFFECTS 0x02u

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
  uint64_t triangle_count;
  uint64_t upload_bytes;
  uint64_t gpu_memory_bytes;
} engine_native_renderer_frame_stats_t;

typedef enum engine_native_capture_format {
  ENGINE_NATIVE_CAPTURE_FORMAT_RGBA8_UNORM = 1
} engine_native_capture_format_t;

#define ENGINE_NATIVE_CAPTURE_SEMANTIC_COLOR 0u
#define ENGINE_NATIVE_CAPTURE_SEMANTIC_DEPTH 1u
#define ENGINE_NATIVE_CAPTURE_SEMANTIC_NORMALS 2u
#define ENGINE_NATIVE_CAPTURE_SEMANTIC_ALBEDO 3u
#define ENGINE_NATIVE_CAPTURE_SEMANTIC_SHADOW 4u
#define ENGINE_NATIVE_CAPTURE_SEMANTIC_AMBIENT_OCCLUSION 5u

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

typedef enum engine_native_audio_bus {
  ENGINE_NATIVE_AUDIO_BUS_MASTER = 0,
  ENGINE_NATIVE_AUDIO_BUS_MUSIC = 1,
  ENGINE_NATIVE_AUDIO_BUS_SFX = 2,
  ENGINE_NATIVE_AUDIO_BUS_AMBIENCE = 3
} engine_native_audio_bus_t;

typedef struct engine_native_audio_play_desc {
  float volume;
  float pitch;
  uint8_t bus;
  uint8_t loop;
  uint8_t is_spatialized;
  uint8_t reserved0;
  float position[3];
  float velocity[3];
} engine_native_audio_play_desc_t;

typedef struct engine_native_listener_desc {
  float position[3];
  float forward[3];
  float up[3];
} engine_native_listener_desc_t;

typedef struct engine_native_emitter_params {
  float volume;
  float pitch;
  float position[3];
  float velocity[3];
  float lowpass;
  float reverb_send;
} engine_native_emitter_params_t;

typedef enum engine_native_net_event_kind {
  ENGINE_NATIVE_NET_EVENT_KIND_CONNECTED = 1,
  ENGINE_NATIVE_NET_EVENT_KIND_DISCONNECTED = 2,
  ENGINE_NATIVE_NET_EVENT_KIND_MESSAGE = 3
} engine_native_net_event_kind_t;

typedef struct engine_native_net_desc {
  uint32_t local_peer_id;
  uint32_t max_events_per_pump;
  uint32_t max_payload_bytes;
  uint8_t loopback_enabled;
  uint8_t reserved0;
  uint8_t reserved1;
  uint8_t reserved2;
} engine_native_net_desc_t;

typedef struct engine_native_net_send_desc {
  uint32_t peer_id;
  uint8_t channel;
  uint8_t reserved0;
  uint8_t reserved1;
  uint8_t reserved2;
  const uint8_t* payload;
  uint32_t payload_size;
} engine_native_net_send_desc_t;

typedef struct engine_native_net_event {
  uint8_t kind;
  uint8_t channel;
  uint16_t reserved0;
  uint32_t peer_id;
  const uint8_t* payload;
  uint32_t payload_size;
} engine_native_net_event_t;

typedef struct engine_native_net_events {
  const engine_native_net_event_t* events;
  uint32_t event_count;
} engine_native_net_events_t;

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

ENGINE_NATIVE_API engine_native_status_t engine_get_audio(
    engine_native_engine_t* engine,
    engine_native_audio_t** out_audio);

ENGINE_NATIVE_API engine_native_status_t engine_get_net(
    engine_native_engine_t* engine,
    engine_native_net_t** out_net);

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

ENGINE_NATIVE_API engine_native_status_t content_mount_pak_view(
    engine_native_engine_t* engine,
    engine_native_string_view_t pak_path);

ENGINE_NATIVE_API engine_native_status_t content_mount_directory_view(
    engine_native_engine_t* engine,
    engine_native_string_view_t directory_path);

ENGINE_NATIVE_API engine_native_status_t content_read_file_view(
    engine_native_engine_t* engine,
    engine_native_string_view_t asset_path,
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

ENGINE_NATIVE_API engine_native_status_t renderer_present_with_stats(
    engine_native_renderer_t* renderer,
    engine_native_renderer_frame_stats_t* out_stats);

ENGINE_NATIVE_API engine_native_status_t renderer_create_mesh_from_blob(
    engine_native_renderer_t* renderer,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_mesh);

ENGINE_NATIVE_API engine_native_status_t renderer_create_mesh_from_cpu(
    engine_native_renderer_t* renderer,
    const engine_native_mesh_cpu_data_t* mesh_data,
    engine_native_resource_handle_t* out_mesh);

ENGINE_NATIVE_API engine_native_status_t renderer_create_texture_from_blob(
    engine_native_renderer_t* renderer,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_texture);

ENGINE_NATIVE_API engine_native_status_t renderer_create_texture_from_cpu(
    engine_native_renderer_t* renderer,
    const engine_native_texture_cpu_data_t* texture_data,
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

ENGINE_NATIVE_API engine_native_status_t audio_create_sound_from_blob(
    engine_native_audio_t* audio,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_sound);

ENGINE_NATIVE_API engine_native_status_t audio_play(
    engine_native_audio_t* audio,
    engine_native_resource_handle_t sound,
    const engine_native_audio_play_desc_t* play_desc,
    uint64_t* out_emitter_id);

ENGINE_NATIVE_API engine_native_status_t audio_set_listener(
    engine_native_audio_t* audio,
    const engine_native_listener_desc_t* listener_desc);

ENGINE_NATIVE_API engine_native_status_t audio_set_emitter_params(
    engine_native_audio_t* audio,
    uint64_t emitter_id,
    const engine_native_emitter_params_t* params);

ENGINE_NATIVE_API engine_native_status_t net_create(
    const engine_native_net_desc_t* desc,
    engine_native_net_t** out_net);

ENGINE_NATIVE_API engine_native_status_t net_destroy(
    engine_native_net_t* net);

ENGINE_NATIVE_API engine_native_status_t net_pump(
    engine_native_net_t* net,
    engine_native_net_events_t* out_events);

ENGINE_NATIVE_API engine_native_status_t net_send(
    engine_native_net_t* net,
    const engine_native_net_send_desc_t* send_desc);

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

ENGINE_NATIVE_API engine_native_status_t engine_create_handle(
    const engine_native_create_desc_t* create_desc,
    engine_native_engine_handle_t* out_engine);

ENGINE_NATIVE_API engine_native_status_t engine_destroy_handle(
    engine_native_engine_handle_t engine);

ENGINE_NATIVE_API engine_native_status_t engine_pump_events_handle(
    engine_native_engine_handle_t engine,
    engine_native_input_snapshot_t* out_input,
    engine_native_window_events_t* out_events);

ENGINE_NATIVE_API engine_native_status_t engine_get_renderer_handle(
    engine_native_engine_handle_t engine,
    engine_native_renderer_handle_t* out_renderer);

ENGINE_NATIVE_API engine_native_status_t engine_get_physics_handle(
    engine_native_engine_handle_t engine,
    engine_native_physics_handle_t* out_physics);

ENGINE_NATIVE_API engine_native_status_t engine_get_audio_handle(
    engine_native_engine_handle_t engine,
    engine_native_audio_handle_t* out_audio);

ENGINE_NATIVE_API engine_native_status_t engine_get_net_handle(
    engine_native_engine_handle_t engine,
    engine_native_net_handle_t* out_net);

ENGINE_NATIVE_API engine_native_status_t content_mount_pak_handle(
    engine_native_engine_handle_t engine,
    const char* pak_path);

ENGINE_NATIVE_API engine_native_status_t content_mount_directory_handle(
    engine_native_engine_handle_t engine,
    const char* directory_path);

ENGINE_NATIVE_API engine_native_status_t content_read_file_handle(
    engine_native_engine_handle_t engine,
    const char* asset_path,
    void* buffer,
    size_t buffer_size,
    size_t* out_size);

ENGINE_NATIVE_API engine_native_status_t content_mount_pak_view_handle(
    engine_native_engine_handle_t engine,
    engine_native_string_view_t pak_path);

ENGINE_NATIVE_API engine_native_status_t content_mount_directory_view_handle(
    engine_native_engine_handle_t engine,
    engine_native_string_view_t directory_path);

ENGINE_NATIVE_API engine_native_status_t content_read_file_view_handle(
    engine_native_engine_handle_t engine,
    engine_native_string_view_t asset_path,
    void* buffer,
    size_t buffer_size,
    size_t* out_size);

ENGINE_NATIVE_API engine_native_status_t renderer_begin_frame_handle(
    engine_native_renderer_handle_t renderer,
    size_t requested_bytes,
    size_t alignment,
    void** out_frame_memory);

ENGINE_NATIVE_API engine_native_status_t renderer_submit_handle(
    engine_native_renderer_handle_t renderer,
    const engine_native_render_packet_t* packet);

ENGINE_NATIVE_API engine_native_status_t renderer_present_handle(
    engine_native_renderer_handle_t renderer);

ENGINE_NATIVE_API engine_native_status_t renderer_present_with_stats_handle(
    engine_native_renderer_handle_t renderer,
    engine_native_renderer_frame_stats_t* out_stats);

ENGINE_NATIVE_API engine_native_status_t renderer_create_mesh_from_blob_handle(
    engine_native_renderer_handle_t renderer,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_mesh);

ENGINE_NATIVE_API engine_native_status_t renderer_create_mesh_from_cpu_handle(
    engine_native_renderer_handle_t renderer,
    const engine_native_mesh_cpu_data_t* mesh_data,
    engine_native_resource_handle_t* out_mesh);

ENGINE_NATIVE_API engine_native_status_t renderer_create_texture_from_blob_handle(
    engine_native_renderer_handle_t renderer,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_texture);

ENGINE_NATIVE_API engine_native_status_t renderer_create_texture_from_cpu_handle(
    engine_native_renderer_handle_t renderer,
    const engine_native_texture_cpu_data_t* texture_data,
    engine_native_resource_handle_t* out_texture);

ENGINE_NATIVE_API engine_native_status_t renderer_create_material_from_blob_handle(
    engine_native_renderer_handle_t renderer,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_material);

ENGINE_NATIVE_API engine_native_status_t renderer_destroy_resource_handle(
    engine_native_renderer_handle_t renderer,
    engine_native_resource_handle_t handle);

ENGINE_NATIVE_API engine_native_status_t renderer_get_last_frame_stats_handle(
    engine_native_renderer_handle_t renderer,
    engine_native_renderer_frame_stats_t* out_stats);

ENGINE_NATIVE_API engine_native_status_t capture_request_handle(
    engine_native_renderer_handle_t renderer,
    const engine_native_capture_request_t* request,
    uint64_t* out_request_id);

ENGINE_NATIVE_API engine_native_status_t audio_create_sound_from_blob_handle(
    engine_native_audio_handle_t audio,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_sound);

ENGINE_NATIVE_API engine_native_status_t audio_play_handle(
    engine_native_audio_handle_t audio,
    engine_native_resource_handle_t sound,
    const engine_native_audio_play_desc_t* play_desc,
    uint64_t* out_emitter_id);

ENGINE_NATIVE_API engine_native_status_t audio_set_listener_handle(
    engine_native_audio_handle_t audio,
    const engine_native_listener_desc_t* listener_desc);

ENGINE_NATIVE_API engine_native_status_t audio_set_emitter_params_handle(
    engine_native_audio_handle_t audio,
    uint64_t emitter_id,
    const engine_native_emitter_params_t* params);

ENGINE_NATIVE_API engine_native_status_t net_create_handle(
    const engine_native_net_desc_t* desc,
    engine_native_net_handle_t* out_net);

ENGINE_NATIVE_API engine_native_status_t net_destroy_handle(
    engine_native_net_handle_t net);

ENGINE_NATIVE_API engine_native_status_t net_pump_handle(
    engine_native_net_handle_t net,
    engine_native_net_events_t* out_events);

ENGINE_NATIVE_API engine_native_status_t net_send_handle(
    engine_native_net_handle_t net,
    const engine_native_net_send_desc_t* send_desc);

ENGINE_NATIVE_API engine_native_status_t physics_step_handle(
    engine_native_physics_handle_t physics,
    double dt_seconds);

ENGINE_NATIVE_API engine_native_status_t physics_sync_from_world_handle(
    engine_native_physics_handle_t physics,
    const engine_native_body_write_t* writes,
    uint32_t write_count);

ENGINE_NATIVE_API engine_native_status_t physics_sync_to_world_handle(
    engine_native_physics_handle_t physics,
    engine_native_body_read_t* reads,
    uint32_t read_capacity,
    uint32_t* out_read_count);

ENGINE_NATIVE_API engine_native_status_t physics_raycast_handle(
    engine_native_physics_handle_t physics,
    const engine_native_raycast_query_t* query,
    engine_native_raycast_hit_t* out_hit);

ENGINE_NATIVE_API engine_native_status_t physics_sweep_handle(
    engine_native_physics_handle_t physics,
    const engine_native_sweep_query_t* query,
    engine_native_sweep_hit_t* out_hit);

ENGINE_NATIVE_API engine_native_status_t physics_overlap_handle(
    engine_native_physics_handle_t physics,
    const engine_native_overlap_query_t* query,
    engine_native_overlap_hit_t* hits,
    uint32_t hit_capacity,
    uint32_t* out_hit_count);

#ifdef __cplusplus
}
#endif

#endif
