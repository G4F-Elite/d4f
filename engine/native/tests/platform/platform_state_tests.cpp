#include "platform/platform_state_tests.h"

#include <assert.h>

#include "platform/platform_state.h"

namespace dff::native::tests {
namespace {

void TestPumpEventsPopulatesOutputAndIncrementsFrame() {
  dff::native::platform::PlatformState platform;
  engine_native_input_snapshot_t input{};
  engine_native_window_events_t events{};

  assert(platform.PumpEvents(&input, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(input.frame_index == 1u);
  assert(events.width == 1280u);
  assert(events.height == 720u);
  assert(events.should_close == 0u);

  assert(platform.PumpEvents(&input, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(input.frame_index == 2u);
}

void TestWindowChangesAreVisible() {
  dff::native::platform::PlatformState platform;
  engine_native_input_snapshot_t input{};
  engine_native_window_events_t events{};

  platform.SetWindowSize(1920u, 1080u);
  platform.RequestClose();

  assert(platform.PumpEvents(&input, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.width == 1920u);
  assert(events.height == 1080u);
  assert(events.should_close == 1u);
}

void TestValidation() {
  dff::native::platform::PlatformState platform;
  engine_native_input_snapshot_t input{};
  engine_native_window_events_t events{};

  assert(platform.PumpEvents(nullptr, &events) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(platform.PumpEvents(&input, nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
}

}  // namespace

void RunPlatformStateTests() {
  TestPumpEventsPopulatesOutputAndIncrementsFrame();
  TestWindowChangesAreVisible();
  TestValidation();
}

}  // namespace dff::native::tests
