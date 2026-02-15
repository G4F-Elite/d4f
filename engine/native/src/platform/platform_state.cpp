#include "platform/platform_state.h"

namespace dff::native::platform {

engine_native_status_t PlatformState::PumpEvents(engine_native_input_snapshot_t* out_input,
                                                 engine_native_window_events_t* out_events) {
  if (out_input == nullptr || out_events == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  ++pump_count_;

  out_input->frame_index = pump_count_;
  out_input->buttons_mask = 0u;
  out_input->mouse_x = 0.0f;
  out_input->mouse_y = 0.0f;

  out_events->should_close = should_close_;
  out_events->width = width_;
  out_events->height = height_;
  return ENGINE_NATIVE_STATUS_OK;
}

void PlatformState::RequestClose() {
  should_close_ = 1u;
}

void PlatformState::SetWindowSize(uint32_t width, uint32_t height) {
  if (width > 0u) {
    width_ = width;
  }

  if (height > 0u) {
    height_ = height;
  }
}

}  // namespace dff::native::platform
