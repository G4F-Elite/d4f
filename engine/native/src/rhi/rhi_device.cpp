#include "rhi/rhi_device.h"

namespace dff::native::rhi {

engine_native_status_t RhiDevice::BeginFrame() {
  if (frame_open_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  frame_open_ = true;
  clear_called_in_frame_ = false;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RhiDevice::Clear(const std::array<float, 4>& color) {
  if (!frame_open_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  last_clear_color_ = color;
  clear_called_in_frame_ = true;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RhiDevice::EndFrame() {
  if (!frame_open_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  if (!clear_called_in_frame_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  frame_open_ = false;
  clear_called_in_frame_ = false;
  ++present_count_;
  return ENGINE_NATIVE_STATUS_OK;
}

}  // namespace dff::native::rhi
