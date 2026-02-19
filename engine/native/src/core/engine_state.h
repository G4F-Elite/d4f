#ifndef DFF_ENGINE_NATIVE_ENGINE_STATE_H
#define DFF_ENGINE_NATIVE_ENGINE_STATE_H

#include <cstddef>
#include <stdint.h>

#include <algorithm>
#include <array>
#include <cmath>
#include <string>
#include <unordered_map>
#include <vector>

#include "content/content_runtime.h"
#include "engine_native.h"
#include "core/net_state.h"
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
    uint64_t triangle_count = 0u;
    std::vector<uint8_t> bytes;
  };

  void AttachDevice(rhi::RhiDevice* device) { rhi_device_ = device; }

  engine_native_status_t BeginFrame(size_t requested_bytes,
                                    size_t alignment,
                                    void** out_frame_memory);
  engine_native_status_t Submit(const engine_native_render_packet_t& packet);
  engine_native_status_t UiReset();
  engine_native_status_t UiAppend(const engine_native_ui_draw_item_t* items,
                                  uint32_t item_count);
  engine_native_status_t UiGetCount(uint32_t* out_item_count) const;
  engine_native_status_t UiCopyItems(engine_native_ui_draw_item_t* out_items,
                                     uint32_t item_capacity,
                                     uint32_t* out_item_count) const;
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
  void LoadPipelineCacheFromDisk(const char* file_path);
  void SavePipelineCacheToDisk(const char* file_path) const;

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
  const std::vector<engine_native_ui_draw_item_t>& submitted_ui_items() const {
    return submitted_ui_items_;
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
  uint64_t ComputeSubmittedTriangleCount() const;
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
  std::vector<engine_native_ui_draw_item_t> submitted_ui_items_;
  engine_native_debug_view_mode_t submitted_debug_view_mode_ =
      ENGINE_NATIVE_DEBUG_VIEW_NONE;
  uint8_t submitted_render_feature_flags_ = 0u;
  render::MaterialSystem material_system_;
  rhi::PipelineStateCache pipeline_cache_;
  ResourceTable<ResourceBlob> resources_;
  uint64_t resource_upload_bytes_pending_ = 0u;
  uint64_t resource_gpu_memory_bytes_ = 0u;
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
  engine_native_resource_handle_t collider_mesh = kInvalidResourceHandle;
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

struct AudioBusState {
  float gain = 1.0f;
  float lowpass = 1.0f;
  float reverb_send = 0.0f;
  uint8_t muted = 0u;
};

struct AudioBusMixSnapshot {
  float master_gain = 0.0f;
  float music_gain = 0.0f;
  float sfx_gain = 0.0f;
  float ambience_gain = 0.0f;
  float master_bus_gain = 1.0f;
  float music_bus_gain = 1.0f;
  float sfx_bus_gain = 1.0f;
  float ambience_bus_gain = 1.0f;
  uint32_t active_emitter_count = 0u;
  uint32_t spatialized_emitter_count = 0u;
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
  engine_native_status_t SetBusParams(
      const engine_native_audio_bus_params_t& params);

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
  const AudioBusState& bus_state(uint8_t bus) const {
    return bus_states_[BusIndex(bus)];
  }
  float ComputeEmitterGain(const AudioEmitterState& emitter) const;
  AudioBusMixSnapshot BuildMixSnapshot() const;

 private:
  static constexpr size_t BusIndex(uint8_t bus) {
    switch (bus) {
      case ENGINE_NATIVE_AUDIO_BUS_MUSIC:
        return 1u;
      case ENGINE_NATIVE_AUDIO_BUS_SFX:
        return 2u;
      case ENGINE_NATIVE_AUDIO_BUS_AMBIENCE:
        return 3u;
      case ENGINE_NATIVE_AUDIO_BUS_MASTER:
      default:
        return 0u;
    }
  }

  static bool IsSupportedBus(uint8_t bus) {
    return bus == ENGINE_NATIVE_AUDIO_BUS_MASTER ||
           bus == ENGINE_NATIVE_AUDIO_BUS_MUSIC ||
           bus == ENGINE_NATIVE_AUDIO_BUS_SFX ||
           bus == ENGINE_NATIVE_AUDIO_BUS_AMBIENCE;
  }
  static bool IsFiniteScalar(float value);
  static bool IsFiniteVector(const float* values, size_t count);
  static bool IsValidNormalizedValue(float value);

  ResourceTable<AudioSoundResource> sounds_;
  std::unordered_map<uint64_t, AudioEmitterState> emitters_;
  uint64_t next_emitter_id_ = 1u;
  AudioListenerState listener_;
  std::array<AudioBusState, 4u> bus_states_{};
};

inline float AudioState::ComputeEmitterGain(const AudioEmitterState& emitter) const {
  if (!IsSupportedBus(emitter.bus)) {
    return 0.0f;
  }

  const AudioBusState& bus = bus_states_[BusIndex(emitter.bus)];
  const AudioBusState& master_bus = bus_states_[BusIndex(ENGINE_NATIVE_AUDIO_BUS_MASTER)];
  if (bus.muted != 0u || master_bus.muted != 0u) {
    return 0.0f;
  }

  const float volume = std::max(0.0f, emitter.volume);
  float lowpass =
      std::clamp(emitter.lowpass, 0.0f, 1.0f) * std::clamp(bus.lowpass, 0.0f, 1.0f);
  float reverb = std::clamp(
      emitter.reverb_send + std::clamp(bus.reverb_send, 0.0f, 1.0f),
      0.0f,
      1.0f);
  float bus_gain = std::max(0.0f, bus.gain);

  if (emitter.bus != ENGINE_NATIVE_AUDIO_BUS_MASTER) {
    lowpass *= std::clamp(master_bus.lowpass, 0.0f, 1.0f);
    reverb = std::clamp(
        reverb + std::clamp(master_bus.reverb_send, 0.0f, 1.0f),
        0.0f,
        1.0f);
    bus_gain *= std::max(0.0f, master_bus.gain);
  }

  float gain = volume * bus_gain * lowpass;
  if (emitter.is_spatialized != 0u) {
    const float dx = emitter.position[0] - listener_.position[0];
    const float dy = emitter.position[1] - listener_.position[1];
    const float dz = emitter.position[2] - listener_.position[2];
    const float distance = std::sqrt(std::max(0.0f, dx * dx + dy * dy + dz * dz));
    const float attenuation = 1.0f / (1.0f + distance);
    gain *= attenuation;
  }

  const float reverb_damping = 1.0f - (reverb * 0.35f);
  gain *= std::max(0.0f, reverb_damping);
  return std::isfinite(gain) ? std::max(0.0f, gain) : 0.0f;
}

inline AudioBusMixSnapshot AudioState::BuildMixSnapshot() const {
  AudioBusMixSnapshot snapshot{};
  snapshot.master_bus_gain = bus_states_[BusIndex(ENGINE_NATIVE_AUDIO_BUS_MASTER)].gain;
  snapshot.music_bus_gain = bus_states_[BusIndex(ENGINE_NATIVE_AUDIO_BUS_MUSIC)].gain;
  snapshot.sfx_bus_gain = bus_states_[BusIndex(ENGINE_NATIVE_AUDIO_BUS_SFX)].gain;
  snapshot.ambience_bus_gain = bus_states_[BusIndex(ENGINE_NATIVE_AUDIO_BUS_AMBIENCE)].gain;
  for (const auto& pair : emitters_) {
    const AudioEmitterState& emitter = pair.second;
    const float gain = ComputeEmitterGain(emitter);
    snapshot.active_emitter_count++;
    if (emitter.is_spatialized != 0u) {
      snapshot.spatialized_emitter_count++;
    }

    snapshot.master_gain += gain;
    switch (emitter.bus) {
      case ENGINE_NATIVE_AUDIO_BUS_MUSIC:
        snapshot.music_gain += gain;
        break;
      case ENGINE_NATIVE_AUDIO_BUS_SFX:
        snapshot.sfx_gain += gain;
        break;
      case ENGINE_NATIVE_AUDIO_BUS_AMBIENCE:
        snapshot.ambience_gain += gain;
        break;
      case ENGINE_NATIVE_AUDIO_BUS_MASTER:
      default:
        break;
    }
  }

  return snapshot;
}

struct EngineState {
  EngineState();
  ~EngineState();

  platform::PlatformState platform;
  content::ContentRuntime content;
  NetState net;
  rhi::RhiDevice rhi_device;
  RendererState renderer;
  PhysicsState physics;
  AudioState audio;
  std::string pipeline_cache_path;
};

}  // namespace dff::native

#endif
