#include "bridge_capi/bridge_state.h"

namespace {

engine_native_status_t ValidateRenderer(engine_native_renderer_t* renderer) {
  if (renderer == nullptr || renderer->state == nullptr || renderer->owner == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (renderer != &renderer->owner->renderer) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  return ENGINE_NATIVE_STATUS_OK;
}

}  // namespace

extern "C" {

engine_native_status_t renderer_begin_frame(engine_native_renderer_t* renderer,
                                            size_t requested_bytes,
                                            size_t alignment,
                                            void** out_frame_memory) {
  const engine_native_status_t status = ValidateRenderer(renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return renderer->state->BeginFrame(requested_bytes, alignment, out_frame_memory);
}

engine_native_status_t renderer_submit(engine_native_renderer_t* renderer,
                                       const engine_native_render_packet_t* packet) {
  const engine_native_status_t status = ValidateRenderer(renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }
  if (packet == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return renderer->state->Submit(*packet);
}

engine_native_status_t renderer_present(engine_native_renderer_t* renderer) {
  const engine_native_status_t status = ValidateRenderer(renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return renderer->state->Present();
}

engine_native_status_t renderer_create_mesh_from_blob(
    engine_native_renderer_t* renderer,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_mesh) {
  const engine_native_status_t status = ValidateRenderer(renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return renderer->state->CreateMeshFromBlob(data, size, out_mesh);
}

engine_native_status_t renderer_create_mesh_from_cpu(
    engine_native_renderer_t* renderer,
    const engine_native_mesh_cpu_data_t* mesh_data,
    engine_native_resource_handle_t* out_mesh) {
  const engine_native_status_t status = ValidateRenderer(renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }
  if (mesh_data == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return renderer->state->CreateMeshFromCpu(*mesh_data, out_mesh);
}

engine_native_status_t renderer_create_texture_from_blob(
    engine_native_renderer_t* renderer,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_texture) {
  const engine_native_status_t status = ValidateRenderer(renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return renderer->state->CreateTextureFromBlob(data, size, out_texture);
}

engine_native_status_t renderer_create_texture_from_cpu(
    engine_native_renderer_t* renderer,
    const engine_native_texture_cpu_data_t* texture_data,
    engine_native_resource_handle_t* out_texture) {
  const engine_native_status_t status = ValidateRenderer(renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }
  if (texture_data == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return renderer->state->CreateTextureFromCpu(*texture_data, out_texture);
}

engine_native_status_t renderer_create_material_from_blob(
    engine_native_renderer_t* renderer,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_material) {
  const engine_native_status_t status = ValidateRenderer(renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return renderer->state->CreateMaterialFromBlob(data, size, out_material);
}

engine_native_status_t renderer_destroy_resource(
    engine_native_renderer_t* renderer,
    engine_native_resource_handle_t handle) {
  const engine_native_status_t status = ValidateRenderer(renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return renderer->state->DestroyResource(handle);
}

engine_native_status_t renderer_get_last_frame_stats(
    engine_native_renderer_t* renderer,
    engine_native_renderer_frame_stats_t* out_stats) {
  const engine_native_status_t status = ValidateRenderer(renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return renderer->state->GetLastFrameStats(out_stats);
}

}  // extern "C"
