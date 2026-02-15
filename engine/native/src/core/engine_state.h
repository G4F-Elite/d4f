#ifndef DFF_ENGINE_NATIVE_ENGINE_STATE_H
#define DFF_ENGINE_NATIVE_ENGINE_STATE_H

#include <cstddef>
#include <stdint.h>

#include <string>

#include "engine_native.h"
#include "resource_table.h"

namespace dff::native {

struct RenderSubmission {
  uint32_t entity_id;
  std::string debug_label;
};

class RendererState {
 public:
  engine_native_status_t BeginFrame();
  engine_native_status_t Submit(const engine_native_render_packet_t& packet,
                                engine_native_resource_handle_t* out_submission);
  engine_native_status_t Present();

  bool is_frame_open() const { return frame_open_; }
  size_t pending_submissions() const { return submissions_.Size(); }

 private:
  bool frame_open_ = false;
  ResourceTable<RenderSubmission> submissions_;
};

class PhysicsState {
 public:
  engine_native_status_t Step(double dt_seconds);
  engine_native_status_t SyncFromWorld();
  engine_native_status_t SyncToWorld();

  uint64_t step_count() const { return step_count_; }

 private:
  bool synced_from_world_ = false;
  bool stepped_since_sync_ = false;
  uint64_t step_count_ = 0;
};

struct EngineState {
  RendererState renderer;
  PhysicsState physics;
  uint64_t event_pump_count = 0;
};

}  // namespace dff::native

#endif
