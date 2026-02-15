#ifndef DFF_ENGINE_NATIVE_ENGINE_STATE_H
#define DFF_ENGINE_NATIVE_ENGINE_STATE_H

#include <cstddef>
#include <stdint.h>

#include <array>
#include <unordered_map>
#include <vector>

#include "engine_native.h"

namespace dff::native {

class RendererState {
 public:
  engine_native_status_t BeginFrame(size_t requested_bytes,
                                    size_t alignment,
                                    void** out_frame_memory);
  engine_native_status_t Submit(const engine_native_render_packet_t& packet);
  engine_native_status_t Present();

  bool is_frame_open() const { return frame_open_; }
  uint32_t submitted_draw_count() const { return submitted_draw_count_; }
  uint32_t submitted_ui_count() const { return submitted_ui_count_; }

 private:
  static bool IsPowerOfTwo(size_t value);

  bool frame_open_ = false;
  std::vector<uint8_t> frame_storage_;
  void* frame_memory_ = nullptr;
  size_t frame_capacity_ = 0;
  uint32_t submitted_draw_count_ = 0;
  uint32_t submitted_ui_count_ = 0;
};

struct PhysicsBodyState {
  std::array<float, 3> position{0.0f, 0.0f, 0.0f};
  std::array<float, 4> rotation{0.0f, 0.0f, 0.0f, 1.0f};
  std::array<float, 3> linear_velocity{0.0f, 0.0f, 0.0f};
  std::array<float, 3> angular_velocity{0.0f, 0.0f, 0.0f};
};

class PhysicsState {
 public:
  engine_native_status_t Step(double dt_seconds);
  engine_native_status_t SyncFromWorld(const engine_native_body_write_t* writes,
                                       uint32_t write_count);
  engine_native_status_t SyncToWorld(engine_native_body_read_t* reads,
                                     uint32_t read_capacity,
                                     uint32_t* out_read_count);

  uint64_t step_count() const { return step_count_; }
  size_t body_count() const { return bodies_.size(); }

 private:
  bool synced_from_world_ = false;
  bool stepped_since_sync_ = false;
  uint64_t step_count_ = 0;
  std::unordered_map<engine_native_resource_handle_t, PhysicsBodyState> bodies_;
};

struct EngineState {
  RendererState renderer;
  PhysicsState physics;
  uint64_t event_pump_count = 0;
};

}  // namespace dff::native

#endif
