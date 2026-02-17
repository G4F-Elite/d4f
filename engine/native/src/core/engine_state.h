#ifndef DFF_ENGINE_NATIVE_ENGINE_STATE_H
#define DFF_ENGINE_NATIVE_ENGINE_STATE_H

#include <cstddef>
#include <stdint.h>

#include <array>
#include <string>
#include <unordered_map>
#include <vector>

#include "content/content_runtime.h"
#include "engine_native.h"
#include "core/resource_table.h"
#include "render/material_system.h"
#include "platform/platform_state.h"
#include "render/render_graph.h"
#include "rhi/pipeline_state_cache.h"
#include "rhi/rhi_device.h"

namespace dff::native {

class RendererState {
 public:
  enum class ResourceKind : uint8_t {
    kMesh = 1u,
    kTexture = 2u,
    kMaterial = 3u,
  };

  struct ResourceBlob {
    ResourceKind kind = ResourceKind::kMesh;
    std::vector<uint8_t> bytes;
  };

  void AttachDevice(rhi::RhiDevice* device) { rhi_device_ = device; }

  engine_native_status_t BeginFrame(size_t requested_bytes,
                                    size_t alignment,
                                    void** out_frame_memory);
  engine_native_status_t Submit(const engine_native_render_packet_t& packet);
  engine_native_status_t Present();
  engine_native_status_t CreateMeshFromBlob(
      const void* data,
      size_t size,
      engine_native_resource_handle_t* out_mesh);
  engine_native_status_t CreateMeshFromCpu(
      const engine_native_mesh_cpu_data_t& mesh_data,
      engine_native_resource_handle_t* out_mesh);
  engine_native_status_t CreateTextureFromBlob(
      const void* data,
      size_t size,
      engine_native_resource_handle_t* out_texture);
  engine_native_status_t CreateTextureFromCpu(
      const engine_native_texture_cpu_data_t& texture_data,
      engine_native_resource_handle_t* out_texture);
  engine_native_status_t CreateMaterialFromBlob(
      const void* data,
      size_t size,
      engine_native_resource_handle_t* out_material);
  engine_native_status_t DestroyResource(engine_native_resource_handle_t handle);
  engine_native_status_t GetLastFrameStats(
      engine_native_renderer_frame_stats_t* out_stats) const;

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
  size_t resource_count() const { return resources_.Size(); }

 private:
  static bool IsPowerOfTwo(size_t value);
  engine_native_status_t CreateResourceFromBlob(
      ResourceKind kind,
      const void* data,
      size_t size,
      engine_native_resource_handle_t* out_handle);
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
  engine_native_debug_view_mode_t submitted_debug_view_mode_ =
      ENGINE_NATIVE_DEBUG_VIEW_NONE;
  render::MaterialSystem material_system_;
  rhi::PipelineStateCache pipeline_cache_;
  ResourceTable<ResourceBlob> resources_;
  uint64_t last_pass_mask_ = 0u;
  engine_native_renderer_frame_stats_t last_frame_stats_{};
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

struct AudioSoundResource {
  std::vector<uint8_t> bytes;
};

struct AudioEmitterState {
  engine_native_resource_handle_t sound = kInvalidResourceHandle;
  float volume = 1.0f;
  float pitch = 1.0f;
  uint8_t bus = ENGINE_NATIVE_AUDIO_BUS_MASTER;
  uint8_t loop = 0u;
  uint8_t is_spatialized = 0u;
  uint8_t reserved0 = 0u;
  std::array<float, 3> position{0.0f, 0.0f, 0.0f};
  std::array<float, 3> velocity{0.0f, 0.0f, 0.0f};
  float lowpass = 1.0f;
  float reverb_send = 0.0f;
};

struct AudioListenerState {
  std::array<float, 3> position{0.0f, 0.0f, 0.0f};
  std::array<float, 3> forward{0.0f, 0.0f, -1.0f};
  std::array<float, 3> up{0.0f, 1.0f, 0.0f};
};

class AudioState {
 public:
  engine_native_status_t CreateSoundFromBlob(
      const void* data,
      size_t size,
      engine_native_resource_handle_t* out_sound);
  engine_native_status_t Play(
      engine_native_resource_handle_t sound,
      const engine_native_audio_play_desc_t& play_desc,
      uint64_t* out_emitter_id);
  engine_native_status_t SetListener(
      const engine_native_listener_desc_t& listener_desc);
  engine_native_status_t SetEmitterParams(
      uint64_t emitter_id,
      const engine_native_emitter_params_t& params);

  size_t sound_count() const { return sounds_.Size(); }
  size_t emitter_count() const { return emitters_.size(); }
  const AudioEmitterState* FindEmitter(uint64_t emitter_id) const {
    const auto emitter_it = emitters_.find(emitter_id);
    if (emitter_it == emitters_.end()) {
      return nullptr;
    }

    return &emitter_it->second;
  }
  const AudioListenerState& listener() const { return listener_; }

 private:
  static bool IsSupportedBus(uint8_t bus);
  static bool IsFiniteScalar(float value);
  static bool IsFiniteVector(const float* values, size_t count);
  static bool IsValidNormalizedValue(float value);

  ResourceTable<AudioSoundResource> sounds_;
  std::unordered_map<uint64_t, AudioEmitterState> emitters_;
  uint64_t next_emitter_id_ = 1u;
  AudioListenerState listener_;
};

struct EngineState {
  EngineState();

  platform::PlatformState platform;
  content::ContentRuntime content;
  rhi::RhiDevice rhi_device;
  RendererState renderer;
  PhysicsState physics;
  AudioState audio;
};

}  // namespace dff::native

#endif
