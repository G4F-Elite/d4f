#ifndef DFF_ENGINE_NATIVE_RHI_DEVICE_H
#define DFF_ENGINE_NATIVE_RHI_DEVICE_H

#include <array>
#include <cstdint>
#include <vector>

#include "engine_native.h"

namespace dff::native::rhi {

class RhiDevice {
 public:
  enum class PassKind : uint8_t {
    kSceneOpaque = 0u,
    kUiOverlay = 1u,
    kPresent = 2u,
    kShadowMap = 3u,
    kPbrOpaque = 4u,
    kTonemap = 5u,
    kBloom = 6u,
    kColorGrading = 7u,
  };

  engine_native_status_t BeginFrame();

  engine_native_status_t Clear(const std::array<float, 4>& color);

  engine_native_status_t ExecutePass(PassKind pass_kind);

  engine_native_status_t EndFrame();

  bool is_frame_open() const { return frame_open_; }

  uint64_t present_count() const { return present_count_; }

  const std::vector<PassKind>& executed_passes() const { return executed_passes_; }

  const std::array<float, 4>& last_clear_color() const { return last_clear_color_; }

 private:
  bool frame_open_ = false;
  bool clear_called_in_frame_ = false;
  bool present_pass_called_in_frame_ = false;
  uint64_t present_count_ = 0;
  std::array<float, 4> last_clear_color_{0.0f, 0.0f, 0.0f, 1.0f};
  std::vector<PassKind> executed_passes_;
};

}  // namespace dff::native::rhi

#endif
