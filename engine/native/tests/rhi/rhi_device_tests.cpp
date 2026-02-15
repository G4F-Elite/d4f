#include "rhi/rhi_device_tests.h"

#include <assert.h>

#include <array>

#include "rhi/rhi_device.h"

namespace dff::native::tests {
namespace {

void TestFrameLifecycle() {
  dff::native::rhi::RhiDevice device;
  const std::array<float, 4> clear{0.2f, 0.3f, 0.4f, 1.0f};

  assert(device.BeginFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(device.is_frame_open());
  assert(device.Clear(clear) == ENGINE_NATIVE_STATUS_OK);
  assert(device.last_clear_color() == clear);
  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(!device.is_frame_open());
  assert(device.present_count() == 1u);
}

void TestValidationAndStateTransitions() {
  dff::native::rhi::RhiDevice device;

  assert(device.Clear({0.0f, 0.0f, 0.0f, 1.0f}) == ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(device.BeginFrame() == ENGINE_NATIVE_STATUS_OK);
  assert(device.BeginFrame() == ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(device.Clear({1.0f, 0.0f, 0.0f, 1.0f}) == ENGINE_NATIVE_STATUS_OK);
  assert(device.EndFrame() == ENGINE_NATIVE_STATUS_OK);
}

}  // namespace

void RunRhiDeviceTests() {
  TestFrameLifecycle();
  TestValidationAndStateTransitions();
}

}  // namespace dff::native::tests
