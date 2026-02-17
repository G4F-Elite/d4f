#include "rhi/rhi_device_tests.h"

#include <assert.h>

#include <array>

#include "rhi/rhi_device.h"

namespace dff::native::tests {
namespace {

using PassKind = dff::native::rhi::RhiDevice::PassKind;

void TestFrameLifecycleWithPbrPipelinePasses() {
  dff::native::rhi::RhiDevice device;
  const std::array<float, 4> clear{0.2f, 0.3f, 0.4f, 1.0f};

  assert(device.BeginFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(device.is_frame_open());
  assert(device.Clear(clear) == ENGINE_NATIVE_STATUS_OK);
  assert(device.last_clear_color() == clear);

  assert(device.ExecutePass(PassKind::kShadowMap) == ENGINE_NATIVE_STATUS_OK);
  assert(device.ExecutePass(PassKind::kPbrOpaque) == ENGINE_NATIVE_STATUS_OK);
  assert(device.ExecutePass(PassKind::kBloom) == ENGINE_NATIVE_STATUS_OK);
  assert(device.ExecutePass(PassKind::kTonemap) == ENGINE_NATIVE_STATUS_OK);
  assert(device.ExecutePass(PassKind::kColorGrading) == ENGINE_NATIVE_STATUS_OK);
  assert(device.ExecutePass(PassKind::kFxaa) == ENGINE_NATIVE_STATUS_OK);
  assert(device.ExecutePass(PassKind::kPresent) == ENGINE_NATIVE_STATUS_OK);

  assert(device.executed_passes().size() == 7u);
  assert(device.executed_passes()[0] == PassKind::kShadowMap);
  assert(device.executed_passes()[1] == PassKind::kPbrOpaque);
  assert(device.executed_passes()[2] == PassKind::kBloom);
  assert(device.executed_passes()[3] == PassKind::kTonemap);
  assert(device.executed_passes()[4] == PassKind::kColorGrading);
  assert(device.executed_passes()[5] == PassKind::kFxaa);
  assert(device.executed_passes()[6] == PassKind::kPresent);

  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(!device.is_frame_open());
  assert(device.present_count() == 1u);
}

void TestFrameLifecycleWithUiOnlyPass() {
  dff::native::rhi::RhiDevice device;
  const std::array<float, 4> clear{0.1f, 0.2f, 0.3f, 1.0f};

  assert(device.BeginFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(device.Clear(clear) == ENGINE_NATIVE_STATUS_OK);
  assert(device.ExecutePass(PassKind::kUiOverlay) == ENGINE_NATIVE_STATUS_OK);
  assert(device.ExecutePass(PassKind::kPresent) == ENGINE_NATIVE_STATUS_OK);
  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(device.present_count() == 1u);

  assert(device.executed_passes().size() == 2u);
  assert(device.executed_passes()[0] == PassKind::kUiOverlay);
  assert(device.executed_passes()[1] == PassKind::kPresent);
}

void TestValidationAndPassOrdering() {
  dff::native::rhi::RhiDevice device;

  assert(device.ExecutePass(PassKind::kSceneOpaque) == ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(device.Clear({0.0f, 0.0f, 0.0f, 1.0f}) == ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(device.BeginFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(device.BeginFrame() == ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(device.ExecutePass(PassKind::kPresent) == ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(device.Clear({1.0f, 0.0f, 0.0f, 1.0f}) == ENGINE_NATIVE_STATUS_OK);
  assert(device.ExecutePass(PassKind::kSceneOpaque) == ENGINE_NATIVE_STATUS_OK);
  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_INVALID_STATE);

  const auto invalid_pass_kind = static_cast<PassKind>(255u);
  assert(device.ExecutePass(invalid_pass_kind) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  assert(device.ExecutePass(PassKind::kPresent) == ENGINE_NATIVE_STATUS_OK);
  assert(device.ExecutePass(PassKind::kPresent) == ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(device.ExecutePass(PassKind::kUiOverlay) == ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(device.Clear({1.0f, 0.0f, 0.0f, 1.0f}) == ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_INVALID_STATE);
}

}  // namespace

void RunRhiDeviceTests() {
  TestFrameLifecycleWithPbrPipelinePasses();
  TestFrameLifecycleWithUiOnlyPass();
  TestValidationAndPassOrdering();
}

}  // namespace dff::native::tests
