#ifndef DFF_ENGINE_NATIVE_PLATFORM_STATE_H
#define DFF_ENGINE_NATIVE_PLATFORM_STATE_H

#include <cstdint>

#include "engine_native.h"

namespace dff::native::platform {

class PlatformState {
 public:
  engine_native_status_t PumpEvents(engine_native_input_snapshot_t* out_input,
                                    engine_native_window_events_t* out_events);

  void RequestClose();

  void SetWindowSize(uint32_t width, uint32_t height);

  uint64_t pump_count() const { return pump_count_; }

 private:
  uint64_t pump_count_ = 0;
  uint8_t should_close_ = 0u;
  uint32_t width_ = 1280u;
  uint32_t height_ = 720u;
};

}  // namespace dff::native::platform

#endif
