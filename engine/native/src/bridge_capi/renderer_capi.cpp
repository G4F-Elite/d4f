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

engine_native_status_t renderer_begin_frame(engine_native_renderer_t* renderer) {
  const engine_native_status_t status = ValidateRenderer(renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return renderer->state->BeginFrame();
}

engine_native_status_t renderer_submit(engine_native_renderer_t* renderer,
                                       const engine_native_render_packet_t* packet,
                                       engine_native_resource_handle_t* out_submission) {
  const engine_native_status_t status = ValidateRenderer(renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }
  if (packet == nullptr || out_submission == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return renderer->state->Submit(*packet, out_submission);
}

engine_native_status_t renderer_present(engine_native_renderer_t* renderer) {
  const engine_native_status_t status = ValidateRenderer(renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return renderer->state->Present();
}

}  // extern "C"