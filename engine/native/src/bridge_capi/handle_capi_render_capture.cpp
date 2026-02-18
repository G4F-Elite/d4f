#include "bridge_capi/handle_registry.h"

namespace {

engine_native_status_t ResolveRenderer(
    engine_native_renderer_handle_t handle,
    engine_native_renderer_t** out_renderer) {
  return dff::native::bridge::ResolveRendererHandle(handle, out_renderer);
}

}  // namespace

extern "C" {

engine_native_status_t renderer_begin_frame_handle(
    engine_native_renderer_handle_t renderer,
    size_t requested_bytes,
    size_t alignment,
    void** out_frame_memory) {
  engine_native_renderer_t* raw_renderer = nullptr;
  const engine_native_status_t resolve_status =
      ResolveRenderer(renderer, &raw_renderer);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return renderer_begin_frame(raw_renderer, requested_bytes, alignment, out_frame_memory);
}

engine_native_status_t renderer_submit_handle(
    engine_native_renderer_handle_t renderer,
    const engine_native_render_packet_t* packet) {
  engine_native_renderer_t* raw_renderer = nullptr;
  const engine_native_status_t resolve_status =
      ResolveRenderer(renderer, &raw_renderer);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return renderer_submit(raw_renderer, packet);
}

engine_native_status_t renderer_present_handle(engine_native_renderer_handle_t renderer) {
  engine_native_renderer_t* raw_renderer = nullptr;
  const engine_native_status_t resolve_status =
      ResolveRenderer(renderer, &raw_renderer);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return renderer_present(raw_renderer);
}

engine_native_status_t renderer_present_with_stats_handle(
    engine_native_renderer_handle_t renderer,
    engine_native_renderer_frame_stats_t* out_stats) {
  engine_native_renderer_t* raw_renderer = nullptr;
  const engine_native_status_t resolve_status =
      ResolveRenderer(renderer, &raw_renderer);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return renderer_present_with_stats(raw_renderer, out_stats);
}

engine_native_status_t renderer_create_mesh_from_blob_handle(
    engine_native_renderer_handle_t renderer,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_mesh) {
  engine_native_renderer_t* raw_renderer = nullptr;
  const engine_native_status_t resolve_status =
      ResolveRenderer(renderer, &raw_renderer);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return renderer_create_mesh_from_blob(raw_renderer, data, size, out_mesh);
}

engine_native_status_t renderer_create_mesh_from_cpu_handle(
    engine_native_renderer_handle_t renderer,
    const engine_native_mesh_cpu_data_t* mesh_data,
    engine_native_resource_handle_t* out_mesh) {
  engine_native_renderer_t* raw_renderer = nullptr;
  const engine_native_status_t resolve_status =
      ResolveRenderer(renderer, &raw_renderer);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return renderer_create_mesh_from_cpu(raw_renderer, mesh_data, out_mesh);
}

engine_native_status_t renderer_create_texture_from_blob_handle(
    engine_native_renderer_handle_t renderer,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_texture) {
  engine_native_renderer_t* raw_renderer = nullptr;
  const engine_native_status_t resolve_status =
      ResolveRenderer(renderer, &raw_renderer);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return renderer_create_texture_from_blob(raw_renderer, data, size, out_texture);
}

engine_native_status_t renderer_create_texture_from_cpu_handle(
    engine_native_renderer_handle_t renderer,
    const engine_native_texture_cpu_data_t* texture_data,
    engine_native_resource_handle_t* out_texture) {
  engine_native_renderer_t* raw_renderer = nullptr;
  const engine_native_status_t resolve_status =
      ResolveRenderer(renderer, &raw_renderer);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return renderer_create_texture_from_cpu(raw_renderer, texture_data, out_texture);
}

engine_native_status_t renderer_create_material_from_blob_handle(
    engine_native_renderer_handle_t renderer,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_material) {
  engine_native_renderer_t* raw_renderer = nullptr;
  const engine_native_status_t resolve_status =
      ResolveRenderer(renderer, &raw_renderer);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return renderer_create_material_from_blob(raw_renderer, data, size, out_material);
}

engine_native_status_t renderer_destroy_resource_handle(
    engine_native_renderer_handle_t renderer,
    engine_native_resource_handle_t handle) {
  engine_native_renderer_t* raw_renderer = nullptr;
  const engine_native_status_t resolve_status =
      ResolveRenderer(renderer, &raw_renderer);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return renderer_destroy_resource(raw_renderer, handle);
}

engine_native_status_t renderer_get_last_frame_stats_handle(
    engine_native_renderer_handle_t renderer,
    engine_native_renderer_frame_stats_t* out_stats) {
  engine_native_renderer_t* raw_renderer = nullptr;
  const engine_native_status_t resolve_status =
      ResolveRenderer(renderer, &raw_renderer);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return renderer_get_last_frame_stats(raw_renderer, out_stats);
}

engine_native_status_t capture_request_handle(
    engine_native_renderer_handle_t renderer,
    const engine_native_capture_request_t* request,
    uint64_t* out_request_id) {
  engine_native_renderer_t* raw_renderer = nullptr;
  const engine_native_status_t resolve_status =
      ResolveRenderer(renderer, &raw_renderer);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return capture_request(raw_renderer, request, out_request_id);
}

}  // extern "C"
