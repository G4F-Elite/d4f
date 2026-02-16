#ifndef DFF_ENGINE_NATIVE_CAPTURE_STORE_H
#define DFF_ENGINE_NATIVE_CAPTURE_STORE_H

#include <stdint.h>

#include <array>
#include <mutex>
#include <unordered_map>
#include <vector>

#include "engine_native.h"

namespace dff::native {

class CaptureStore {
 public:
  engine_native_status_t QueueCapture(
      const engine_native_capture_request_t& request,
      const std::array<float, 4>& clear_color,
      uint64_t frame_index,
      uint64_t* out_request_id);

  engine_native_status_t PollCapture(
      uint64_t request_id,
      engine_native_capture_result_t* out_result,
      uint8_t* out_is_ready);

  engine_native_status_t FreeCaptureResult(
      engine_native_capture_result_t* result);

  void Reset();

 private:
  struct PendingCapture {
    uint32_t width = 0u;
    uint32_t height = 0u;
    uint32_t stride = 0u;
    std::vector<uint8_t> pixels;
  };

  std::mutex mutex_;
  uint64_t next_request_id_ = 1u;
  std::unordered_map<uint64_t, PendingCapture> pending_;
};

CaptureStore& GetCaptureStore();

}  // namespace dff::native

#endif
