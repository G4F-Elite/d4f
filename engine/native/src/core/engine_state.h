#ifndef DFF_ENGINE_NATIVE_ENGINE_STATE_H
#define DFF_ENGINE_NATIVE_ENGINE_STATE_H

#include <cstddef>
#include <stdint.h>

#include <array>
#include <string>
#include <unordered_map>
#include <vector>

#include "engine_native.h"
#include "render/material_system.h"
#include "platform/platform_state.h"
#include "render/render_graph.h"
#include "rhi/pipeline_state_cache.h"
#include "rhi/rhi_device.h"

namespace dff::native {

class RendererState {
 public:
  void AttachDevice(rhi::RhiDevice* device) { rhi_device_ = device; }

  engine_native_status_t BeginFrame(size_t requested_bytes,
                                    size_t alignment,
                                    void** out_frame_memory);
  engine_native_status_t Submit(const engine_native_render_packet_t& packet);
  engine_native_status_t Present();

  bool is_frame_open() const { return frame_open_; }
  uint32_t submitted_draw_count() const { return submitted_draw_count_; }
  uint32_t submitted_ui_count() const { return submitted_ui_count_; }
  uint64_t present_count() const {
    return rhi_device_ == nullptr ? 0u : rhi_device_->present_count();
  }
  const std::array<float, 4>& last_clear_color() const { return last_clear_color_; }
  const std::vector<std::string>& last_executed_rhi_passes() const {
    return last_executed_rhi_passes_;
  }
  uint64_t pipeline_cache_hits() const { return pipeline_cache_.hit_count(); }
  uint64_t pipeline_cache_misses() const { return pipeline_cache_.miss_count(); }
  size_t cached_pipeline_count() const { return pipeline_cache_.size(); }

 private:
  static bool IsPowerOfTwo(size_t value);
  engine_native_status_t BuildFrameGraph();
  engine_native_status_t ExecuteCompiledFrameGraph();
  void ResetFrameState();

  rhi::RhiDevice* rhi_device_ = nullptr;
  bool frame_open_ = false;
  std::vector<uint8_t> frame_storage_;
  void* frame_memory_ = nullptr;
  size_t frame_capacity_ = 0;
  uint32_t submitted_draw_count_ = 0;
  uint32_t submitted_ui_count_ = 0;
  render::RenderGraph frame_graph_;
  std::vector<render::RenderPassId> compiled_pass_order_;
  std::vector<rhi::RhiDevice::PassKind> pass_kinds_by_id_;
  std::vector<std::string> last_executed_rhi_passes_;
  std::array<float, 4> last_clear_color_{0.05f, 0.07f, 0.10f, 1.0f};
  std::vector<engine_native_draw_item_t> submitted_draw_items_;
  render::MaterialSystem material_system_;
  rhi::PipelineStateCache pipeline_cache_;
};

struct PhysicsBodyState {
  uint8_t body_type = 0u;
  uint8_t collider_shape = 0u;
  uint8_t is_trigger = 0u;
  uint8_t reserved0 = 0u;
  std::array<float, 3> position{0.0f, 0.0f, 0.0f};
  std::array<float, 4> rotation{0.0f, 0.0f, 0.0f, 1.0f};
  std::array<float, 3> linear_velocity{0.0f, 0.0f, 0.0f};
  std::array<float, 3> angular_velocity{0.0f, 0.0f, 0.0f};
  std::array<float, 3> collider_dimensions{1.0f, 1.0f, 1.0f};
  float friction = 0.5f;
  float restitution = 0.1f;
};

class PhysicsState {
 public:
  engine_native_status_t Step(double dt_seconds);
  engine_native_status_t SyncFromWorld(const engine_native_body_write_t* writes,
                                       uint32_t write_count);
  engine_native_status_t SyncToWorld(engine_native_body_read_t* reads,
                                     uint32_t read_capacity,
                                     uint32_t* out_read_count);
  engine_native_status_t Raycast(const engine_native_raycast_query_t& query,
                                 engine_native_raycast_hit_t* out_hit) const;
  engine_native_status_t Sweep(const engine_native_sweep_query_t& query,
                               engine_native_sweep_hit_t* out_hit) const;
  engine_native_status_t Overlap(const engine_native_overlap_query_t& query,
                                 engine_native_overlap_hit_t* hits,
                                 uint32_t hit_capacity,
                                 uint32_t* out_hit_count) const;

  uint64_t step_count() const { return step_count_; }
  size_t body_count() const { return bodies_.size(); }

 private:
  bool synced_from_world_ = false;
  bool stepped_since_sync_ = false;
  uint64_t step_count_ = 0;
  std::unordered_map<engine_native_resource_handle_t, PhysicsBodyState> bodies_;
};

struct EngineState {
  EngineState();

  platform::PlatformState platform;
  rhi::RhiDevice rhi_device;
  RendererState renderer;
  PhysicsState physics;
};

}  // namespace dff::native

#endif
