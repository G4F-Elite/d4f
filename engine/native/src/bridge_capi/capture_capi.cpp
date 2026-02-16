#include "bridge_capi/bridge_state.h"

#include "core/capture_store.h"

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

engine_native_status_t capture_request(
    engine_native_renderer_t* renderer,
    const engine_native_capture_request_t* request,
    uint64_t* out_request_id) {
  const engine_native_status_t renderer_status = ValidateRenderer(renderer);
  if (renderer_status != ENGINE_NATIVE_STATUS_OK) {
    return renderer_status;
  }
  if (request == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return dff::native::GetCaptureStore().QueueCapture(
      *request, renderer->state->last_clear_color(), renderer->state->present_count(),
      out_request_id);
}

engine_native_status_t capture_poll(
    uint64_t request_id,
    engine_native_capture_result_t* out_result,
    uint8_t* out_is_ready) {
  return dff::native::GetCaptureStore().PollCapture(
      request_id, out_result, out_is_ready);
}

engine_native_status_t capture_free_result(
    engine_native_capture_result_t* result) {
  return dff::native::GetCaptureStore().FreeCaptureResult(result);
}

}  // extern "C"
