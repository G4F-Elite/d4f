#ifndef DFF_ENGINE_NATIVE_RHI_DEVICE_H
#define DFF_ENGINE_NATIVE_RHI_DEVICE_H

#include <array>
#include <cstdint>

#include "engine_native.h"

namespace dff::native::rhi {

class RhiDevice {
 public:
  engine_native_status_t BeginFrame();

  engine_native_status_t Clear(const std::array<float, 4>& color);

  engine_native_status_t EndFrame();

  bool is_frame_open() const { return frame_open_; }

  uint64_t present_count() const { return present_count_; }

  const std::array<float, 4>& last_clear_color() const { return last_clear_color_; }

 private:
  bool frame_open_ = false;
  bool clear_called_in_frame_ = false;
  uint64_t present_count_ = 0;
  std::array<float, 4> last_clear_color_{0.0f, 0.0f, 0.0f, 1.0f};
};

}  // namespace dff::native::rhi

#endif
