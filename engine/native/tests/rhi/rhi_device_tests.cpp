#include "rhi/rhi_device_tests.h"

#include <assert.h>

#include <array>

#include "rhi/rhi_device.h"

namespace dff::native::tests {
namespace {

void TestExplicitPassLifecycle() {
  dff::native::rhi::RhiDevice device;

  assert(device.BeginFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(device.is_frame_open());

  assert(device.ExecutePass(dff::native::rhi::RhiDevice::PassKind::kClear) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(device.ExecutePass(dff::native::rhi::RhiDevice::PassKind::kPresent) ==
         ENGINE_NATIVE_STATUS_OK);

  assert(device.executed_passes().size() == 2u);
  assert(device.executed_passes()[0] ==
         dff::native::rhi::RhiDevice::PassKind::kClear);
  assert(device.executed_passes()[1] ==
         dff::native::rhi::RhiDevice::PassKind::kPresent);

  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(!device.is_frame_open());
  assert(device.present_count() == 1u);
}

void TestLegacyClearFlowCompatibility() {
  dff::native::rhi::RhiDevice device;
  const std::array<float, 4> clear{0.2f, 0.3f, 0.4f, 1.0f};

  assert(device.BeginFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(device.Clear(clear) == ENGINE_NATIVE_STATUS_OK);
  assert(device.last_clear_color() == clear);
  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_OK);

  assert(device.executed_passes().size() == 1u);
  assert(device.executed_passes()[0] ==
         dff::native::rhi::RhiDevice::PassKind::kClear);

  assert(device.BeginFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(device.executed_passes().empty());
  assert(device.ExecutePass(dff::native::rhi::RhiDevice::PassKind::kClear) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(device.ExecutePass(dff::native::rhi::RhiDevice::PassKind::kPresent) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(device.present_count() == 2u);
}

void TestValidationAndPassOrdering() {
  dff::native::rhi::RhiDevice device;

  assert(device.ExecutePass(dff::native::rhi::RhiDevice::PassKind::kClear) ==
         ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(device.Clear({0.0f, 0.0f, 0.0f, 1.0f}) == ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(device.BeginFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(device.BeginFrame() == ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(device.ExecutePass(dff::native::rhi::RhiDevice::PassKind::kPresent) ==
         ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(device.ExecutePass(dff::native::rhi::RhiDevice::PassKind::kClear) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_INVALID_STATE);

  const auto invalid_pass_kind =
      static_cast<dff::native::rhi::RhiDevice::PassKind>(255u);
  assert(device.ExecutePass(invalid_pass_kind) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  assert(device.ExecutePass(dff::native::rhi::RhiDevice::PassKind::kPresent) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(device.ExecutePass(dff::native::rhi::RhiDevice::PassKind::kPresent) ==
         ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(device.ExecutePass(dff::native::rhi::RhiDevice::PassKind::kClear) ==
         ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(device.Clear({1.0f, 0.0f, 0.0f, 1.0f}) == ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_INVALID_STATE);
}

}  // namespace

void RunRhiDeviceTests() {
  TestExplicitPassLifecycle();
  TestLegacyClearFlowCompatibility();
  TestValidationAndPassOrdering();
}

}  // namespace dff::native::tests
