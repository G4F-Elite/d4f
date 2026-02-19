#ifndef DFF_ENGINE_NATIVE_RHI_DEVICE_H
#define DFF_ENGINE_NATIVE_RHI_DEVICE_H

#include <array>
#include <cstdint>
#include <vector>

#include "engine_native.h"

namespace dff::native::rhi {

class RhiDevice {
 public:
  enum class BackendKind : uint8_t {
    kUnknown = ENGINE_NATIVE_RENDER_BACKEND_UNKNOWN,
    kVulkan = ENGINE_NATIVE_RENDER_BACKEND_VULKAN,
    kNoop = ENGINE_NATIVE_RENDER_BACKEND_NOOP,
  };

  enum class PassKind : uint8_t {
    kSceneOpaque = 0u,
    kUiOverlay = 1u,
    kPresent = 2u,
    kShadowMap = 3u,
    kPbrOpaque = 4u,
    kTonemap = 5u,
    kBloom = 6u,
    kColorGrading = 7u,
    kFxaa = 8u,
    kDebugDepth = 9u,
    kDebugNormals = 10u,
    kDebugAlbedo = 11u,
    kDebugRoughness = 12u,
    kDebugAmbientOcclusion = 13u,
    kAmbientOcclusion = 14u,
  };

  engine_native_status_t BeginFrame();

  engine_native_status_t Clear(const std::array<float, 4>& color);

  engine_native_status_t ExecutePass(PassKind pass_kind);

  engine_native_status_t EndFrame();

  void SetBackendKind(BackendKind backend_kind) { backend_kind_ = backend_kind; }

  BackendKind backend_kind() const { return backend_kind_; }

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
  BackendKind backend_kind_ = BackendKind::kVulkan;
};

}  // namespace dff::native::rhi

#endif
