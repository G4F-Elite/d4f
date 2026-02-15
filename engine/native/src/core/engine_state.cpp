#include "core/engine_state.h"

#include <utility>

namespace dff::native {

engine_native_status_t RendererState::BeginFrame() {
  if (frame_open_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  submissions_.Clear();
  frame_open_ = true;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RendererState::Submit(
    const engine_native_render_packet_t& packet,
    engine_native_resource_handle_t* out_submission) {
  if (out_submission == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (!frame_open_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  RenderSubmission submission{
      .entity_id = packet.entity_id,
      .debug_label = packet.debug_label != nullptr ? packet.debug_label : ""};

  ResourceHandle handle{};
  const engine_native_status_t status =
      submissions_.Insert(std::move(submission), &handle);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  *out_submission = EncodeResourceHandle(handle);
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RendererState::Present() {
  if (!frame_open_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  frame_open_ = false;
  submissions_.Clear();
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t PhysicsState::Step(double dt_seconds) {
  if (dt_seconds <= 0.0) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (!synced_from_world_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  ++step_count_;
  stepped_since_sync_ = true;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t PhysicsState::SyncFromWorld() {
  if (synced_from_world_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  synced_from_world_ = true;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t PhysicsState::SyncToWorld() {
  if (!synced_from_world_ || !stepped_since_sync_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  synced_from_world_ = false;
  stepped_since_sync_ = false;
  return ENGINE_NATIVE_STATUS_OK;
}

}  // namespace dff::native