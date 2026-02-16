#include "core/engine_state.h"

#include <algorithm>
#include <cstdint>
#include <limits>
#include <new>
#include <string>
#include <utility>
#include <vector>

#include "render/frame_graph_builder.h"

namespace dff::native {

namespace {

constexpr uint8_t kPhysicsBodyTypeStatic = 0u;
constexpr uint8_t kPhysicsBodyTypeDynamic = 1u;
constexpr uint8_t kPhysicsBodyTypeKinematic = 2u;
constexpr uint8_t kColliderShapeBox = 0u;
constexpr uint8_t kColliderShapeSphere = 1u;
constexpr uint8_t kColliderShapeCapsule = 2u;

const char* PassNameForKind(rhi::RhiDevice::PassKind pass_kind) {
  switch (pass_kind) {
    case rhi::RhiDevice::PassKind::kShadowMap:
      return "shadow";
    case rhi::RhiDevice::PassKind::kPbrOpaque:
      return "pbr_opaque";
    case rhi::RhiDevice::PassKind::kBloom:
      return "bloom";
    case rhi::RhiDevice::PassKind::kTonemap:
      return "tonemap";
    case rhi::RhiDevice::PassKind::kSceneOpaque:
      return "scene";
    case rhi::RhiDevice::PassKind::kUiOverlay:
      return "ui";
    case rhi::RhiDevice::PassKind::kPresent:
      return "present";
  }

  return "unknown";
}

bool IsSupportedBodyType(uint8_t body_type) {
  return body_type == kPhysicsBodyTypeStatic ||
         body_type == kPhysicsBodyTypeDynamic ||
         body_type == kPhysicsBodyTypeKinematic;
}

bool IsSupportedColliderShape(uint8_t collider_shape) {
  return collider_shape == kColliderShapeBox ||
         collider_shape == kColliderShapeSphere ||
         collider_shape == kColliderShapeCapsule;
}

bool IsUnitRange(float value) {
  return value >= 0.0f && value <= 1.0f;
}

uint32_t ExtractMaterialFeatureFlags(
    const engine_native_draw_item_t& draw_item) {
  return draw_item.sort_key_high & 0x7u;
}

uint64_t ComposePipelineKey(engine_native_resource_handle_t material,
                            const render::ShaderVariantKey& variant) {
  return (material << 32u) ^ static_cast<uint64_t>(variant.value);
}

}  // namespace

EngineState::EngineState() {
  renderer.AttachDevice(&rhi_device);
}

bool RendererState::IsPowerOfTwo(size_t value) {
  return value != 0u && (value & (value - 1u)) == 0u;
}

engine_native_status_t RendererState::BeginFrame(size_t requested_bytes,
                                                 size_t alignment,
                                                 void** out_frame_memory) {
  if (out_frame_memory == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_frame_memory = nullptr;

  if (frame_open_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }
  if (rhi_device_ == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  if (requested_bytes == 0u || !IsPowerOfTwo(alignment)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (requested_bytes >
      std::numeric_limits<size_t>::max() - (alignment - static_cast<size_t>(1u))) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const size_t storage_size = requested_bytes + (alignment - static_cast<size_t>(1u));

  try {
    frame_storage_.assign(storage_size, static_cast<uint8_t>(0u));
  } catch (const std::bad_alloc&) {
    return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
  }

  if (frame_storage_.empty()) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }

  uintptr_t base = reinterpret_cast<uintptr_t>(frame_storage_.data());
  const uintptr_t aligned =
      (base + static_cast<uintptr_t>(alignment - static_cast<size_t>(1u))) &
      ~static_cast<uintptr_t>(alignment - static_cast<size_t>(1u));

  frame_memory_ = reinterpret_cast<void*>(aligned);
  frame_capacity_ = requested_bytes;
  submitted_draw_count_ = 0u;
  submitted_ui_count_ = 0u;
  submitted_draw_items_.clear();
  last_executed_rhi_passes_.clear();
  last_pass_mask_ = 0u;

  engine_native_status_t status = rhi_device_->BeginFrame();
  if (status != ENGINE_NATIVE_STATUS_OK) {
    ResetFrameState();
    return status;
  }

  status = rhi_device_->Clear(last_clear_color_);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    static_cast<void>(rhi_device_->EndFrame());
    ResetFrameState();
    return status;
  }

  frame_open_ = true;
  *out_frame_memory = frame_memory_;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RendererState::Submit(
    const engine_native_render_packet_t& packet) {
  if (!frame_open_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  if (packet.draw_item_count > 0u && packet.draw_items == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (packet.ui_item_count > 0u && packet.ui_items == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (packet.draw_item_count >
          std::numeric_limits<uint32_t>::max() - submitted_draw_count_ ||
      packet.ui_item_count >
          std::numeric_limits<uint32_t>::max() - submitted_ui_count_) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const uint32_t total_draw_count = submitted_draw_count_ + packet.draw_item_count;
  const uint32_t total_ui_count = submitted_ui_count_ + packet.ui_item_count;

  const size_t draw_bytes =
      static_cast<size_t>(total_draw_count) *
      static_cast<size_t>(sizeof(engine_native_draw_item_t));
  const size_t ui_bytes =
      static_cast<size_t>(total_ui_count) *
      static_cast<size_t>(sizeof(engine_native_ui_draw_item_t));

  if (draw_bytes > frame_capacity_ || ui_bytes > frame_capacity_ ||
      draw_bytes > std::numeric_limits<size_t>::max() - ui_bytes ||
      draw_bytes + ui_bytes > frame_capacity_) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  submitted_draw_count_ = total_draw_count;
  submitted_ui_count_ = total_ui_count;

  if (packet.draw_item_count > 0u) {
    const size_t old_size = submitted_draw_items_.size();
    const size_t added = static_cast<size_t>(packet.draw_item_count);
    submitted_draw_items_.resize(old_size + added);
    std::copy_n(packet.draw_items, packet.draw_item_count,
                submitted_draw_items_.data() + old_size);

    for (uint32_t i = 0u; i < packet.draw_item_count; ++i) {
      const engine_native_draw_item_t& draw_item = packet.draw_items[i];
      if (draw_item.material == 0u) {
        continue;
      }

      const uint32_t feature_flags = ExtractMaterialFeatureFlags(draw_item);
      const engine_native_status_t register_status =
          material_system_.RegisterMaterial(draw_item.material, feature_flags);
      if (register_status != ENGINE_NATIVE_STATUS_OK) {
        return register_status;
      }

      render::ShaderVariantKey variant;
      const engine_native_status_t resolve_status =
          material_system_.ResolveVariant(draw_item.material,
                                         /*shadows_enabled=*/true, &variant);
      if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
        return resolve_status;
      }

      pipeline_cache_.GetOrCreate(ComposePipelineKey(draw_item.material, variant));
    }
  }

  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RendererState::Present() {
  if (!frame_open_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }
  if (rhi_device_ == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  engine_native_status_t status = BuildFrameGraph();
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  status = ExecuteCompiledFrameGraph();
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  status = rhi_device_->EndFrame();
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  last_frame_stats_.draw_item_count = submitted_draw_count_;
  last_frame_stats_.ui_item_count = submitted_ui_count_;
  last_frame_stats_.executed_pass_count =
      static_cast<uint32_t>(last_executed_rhi_passes_.size());
  last_frame_stats_.reserved0 = 0u;
  last_frame_stats_.present_count = present_count();
  last_frame_stats_.pipeline_cache_hits = pipeline_cache_hits();
  last_frame_stats_.pipeline_cache_misses = pipeline_cache_misses();
  last_frame_stats_.pass_mask = last_pass_mask_;

  ResetFrameState();
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RendererState::BuildFrameGraph() {
  render::FrameGraphBuildConfig build_config{
      .has_draws = submitted_draw_count_ > 0u,
      .has_ui = submitted_ui_count_ > 0u};
  render::FrameGraphBuildOutput build_output;
  std::string compile_error;
  const engine_native_status_t status = render::BuildCanonicalFrameGraph(
      build_config, &frame_graph_, &build_output, &compile_error);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  compiled_pass_order_ = std::move(build_output.pass_order);
  pass_kinds_by_id_ = std::move(build_output.pass_kinds_by_id);
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RendererState::ExecuteCompiledFrameGraph() {
  if (rhi_device_ == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  last_executed_rhi_passes_.clear();
  last_executed_rhi_passes_.reserve(compiled_pass_order_.size());
  uint64_t pass_mask = 0u;

  for (render::RenderPassId pass_id : compiled_pass_order_) {
    const size_t pass_index = static_cast<size_t>(pass_id);
    if (pass_index >= pass_kinds_by_id_.size()) {
      return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
    }

    const rhi::RhiDevice::PassKind pass_kind = pass_kinds_by_id_[pass_index];
    const engine_native_status_t status = rhi_device_->ExecutePass(pass_kind);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    pass_mask |= static_cast<uint64_t>(1u)
                 << static_cast<uint32_t>(pass_kind);
    last_executed_rhi_passes_.push_back(PassNameForKind(pass_kind));
  }

  last_pass_mask_ = pass_mask;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RendererState::GetLastFrameStats(
    engine_native_renderer_frame_stats_t* out_stats) const {
  if (out_stats == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_stats = last_frame_stats_;
  return ENGINE_NATIVE_STATUS_OK;
}

void RendererState::ResetFrameState() {
  frame_memory_ = nullptr;
  frame_capacity_ = 0u;
  submitted_draw_count_ = 0u;
  submitted_ui_count_ = 0u;
  frame_graph_.Clear();
  compiled_pass_order_.clear();
  pass_kinds_by_id_.clear();
  submitted_draw_items_.clear();
  frame_open_ = false;
  frame_storage_.clear();
}

engine_native_status_t PhysicsState::Step(double dt_seconds) {
  if (dt_seconds <= 0.0) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (!synced_from_world_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  const float dt = static_cast<float>(dt_seconds);
  for (auto& body_pair : bodies_) {
    PhysicsBodyState& state = body_pair.second;
    if (state.body_type != kPhysicsBodyTypeDynamic) {
      continue;
    }

    for (size_t axis = 0u; axis < 3u; ++axis) {
      state.position[axis] += state.linear_velocity[axis] * dt;
    }
  }

  ++step_count_;
  stepped_since_sync_ = true;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t PhysicsState::SyncFromWorld(
    const engine_native_body_write_t* writes,
    uint32_t write_count) {
  if (write_count > 0u && writes == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (synced_from_world_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  for (uint32_t i = 0u; i < write_count; ++i) {
    const engine_native_body_write_t& write = writes[i];
    if (write.body == 0u) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }
    if (!IsSupportedBodyType(write.body_type) ||
        !IsSupportedColliderShape(write.collider_shape) || write.is_trigger > 1u ||
        !IsUnitRange(write.friction) || !IsUnitRange(write.restitution)) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }
    if (write.collider_dimensions[0] <= 0.0f || write.collider_dimensions[1] <= 0.0f ||
        write.collider_dimensions[2] <= 0.0f) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }
    if (write.collider_shape == kColliderShapeSphere &&
        (write.collider_dimensions[0] != write.collider_dimensions[1] ||
         write.collider_dimensions[1] != write.collider_dimensions[2])) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }
    if (write.collider_shape == kColliderShapeCapsule &&
        write.collider_dimensions[1] <= write.collider_dimensions[0] * 2.0f) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    PhysicsBodyState state;
    state.body_type = write.body_type;
    state.collider_shape = write.collider_shape;
    state.is_trigger = write.is_trigger;
    std::copy_n(write.position, 3, state.position.data());
    std::copy_n(write.rotation, 4, state.rotation.data());
    std::copy_n(write.linear_velocity, 3, state.linear_velocity.data());
    std::copy_n(write.angular_velocity, 3, state.angular_velocity.data());
    std::copy_n(write.collider_dimensions, 3, state.collider_dimensions.data());
    state.friction = write.friction;
    state.restitution = write.restitution;
    bodies_[write.body] = state;
  }

  synced_from_world_ = true;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t PhysicsState::SyncToWorld(engine_native_body_read_t* reads,
                                                 uint32_t read_capacity,
                                                 uint32_t* out_read_count) {
  if (out_read_count == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_read_count = 0u;

  if (read_capacity > 0u && reads == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (!synced_from_world_ || !stepped_since_sync_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  uint32_t written = 0u;
  for (const auto& body_pair : bodies_) {
    if (written >= read_capacity) {
      break;
    }

    engine_native_body_read_t& read = reads[written];
    read.body = body_pair.first;
    std::copy_n(body_pair.second.position.data(), 3, read.position);
    std::copy_n(body_pair.second.rotation.data(), 4, read.rotation);
    std::copy_n(body_pair.second.linear_velocity.data(), 3, read.linear_velocity);
    std::copy_n(body_pair.second.angular_velocity.data(), 3, read.angular_velocity);
    read.is_active = 1u;
    ++written;
  }

  *out_read_count = written;
  synced_from_world_ = false;
  stepped_since_sync_ = false;
  return ENGINE_NATIVE_STATUS_OK;
}

}  // namespace dff::native
