#include "core/engine_state.h"

#include <algorithm>
#include <cstdint>
#include <cstring>
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
constexpr uint32_t kBlobVersion = 1u;
constexpr uint32_t kMeshBlobMagic = 0x424D4644u;      // DFMB
constexpr uint32_t kTextureBlobMagic = 0x42544644u;   // DFTB
constexpr uint32_t kMaterialBlobMagic = 0x424D4144u;  // DAMB
constexpr uint32_t kMeshCpuMagic = 0x4D435031u;       // MCP1
constexpr uint32_t kTextureCpuMagic = 0x54435031u;    // TCP1

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
    case rhi::RhiDevice::PassKind::kColorGrading:
      return "color_grading";
    case rhi::RhiDevice::PassKind::kFxaa:
      return "fxaa";
    case rhi::RhiDevice::PassKind::kDebugDepth:
      return "debug_depth";
    case rhi::RhiDevice::PassKind::kDebugNormals:
      return "debug_normals";
    case rhi::RhiDevice::PassKind::kDebugAlbedo:
      return "debug_albedo";
    case rhi::RhiDevice::PassKind::kDebugRoughness:
      return "debug_roughness";
    case rhi::RhiDevice::PassKind::kDebugAmbientOcclusion:
      return "debug_ambient_occlusion";
    case rhi::RhiDevice::PassKind::kAmbientOcclusion:
      return "ambient_occlusion";
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

bool IsSupportedDebugViewMode(uint8_t mode) {
  return mode == ENGINE_NATIVE_DEBUG_VIEW_NONE ||
         mode == ENGINE_NATIVE_DEBUG_VIEW_DEPTH ||
         mode == ENGINE_NATIVE_DEBUG_VIEW_NORMALS ||
         mode == ENGINE_NATIVE_DEBUG_VIEW_ALBEDO ||
         mode == ENGINE_NATIVE_DEBUG_VIEW_ROUGHNESS ||
         mode == ENGINE_NATIVE_DEBUG_VIEW_AMBIENT_OCCLUSION;
}

uint32_t ExtractMaterialFeatureFlags(
    const engine_native_draw_item_t& draw_item) {
  return draw_item.sort_key_high & 0x7u;
}

uint64_t ComposePipelineKey(engine_native_resource_handle_t material,
                            const render::ShaderVariantKey& variant) {
  return (material << 32u) ^ static_cast<uint64_t>(variant.value);
}

bool TryReadU32(const void* data,
                size_t size,
                size_t offset,
                uint32_t* out_value) {
  if (data == nullptr || out_value == nullptr ||
      offset > size ||
      size - offset < sizeof(uint32_t)) {
    return false;
  }

  uint32_t value = 0u;
  std::memcpy(&value, static_cast<const uint8_t*>(data) + offset,
              sizeof(uint32_t));
  *out_value = value;
  return true;
}

bool HasMagicAndVersion(const void* data,
                        size_t size,
                        uint32_t expected_magic,
                        uint32_t expected_version) {
  uint32_t magic = 0u;
  uint32_t version = 0u;
  return TryReadU32(data, size, 0u, &magic) &&
         TryReadU32(data, size, sizeof(uint32_t), &version) &&
         magic == expected_magic && version == expected_version;
}

bool IsValidResourceBlob(RendererState::ResourceKind kind,
                         const void* data,
                         size_t size) {
  if (data == nullptr || size == 0u) {
    return false;
  }

  switch (kind) {
    case RendererState::ResourceKind::kMesh: {
      if (HasMagicAndVersion(data, size, kMeshBlobMagic, kBlobVersion)) {
        return true;
      }

      uint32_t magic = 0u;
      return TryReadU32(data, size, 0u, &magic) && magic == kMeshCpuMagic &&
             size >= sizeof(uint32_t) * 3u;
    }
    case RendererState::ResourceKind::kTexture: {
      if (HasMagicAndVersion(data, size, kTextureBlobMagic, kBlobVersion)) {
        return true;
      }

      uint32_t magic = 0u;
      return TryReadU32(data, size, 0u, &magic) && magic == kTextureCpuMagic &&
             size >= sizeof(uint32_t) * 4u;
    }
    case RendererState::ResourceKind::kMaterial:
      return HasMagicAndVersion(data, size, kMaterialBlobMagic, kBlobVersion);
  }

  return false;
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
  submitted_debug_view_mode_ = ENGINE_NATIVE_DEBUG_VIEW_NONE;
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
  if (!IsSupportedDebugViewMode(packet.debug_view_mode)) {
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
  if (packet.debug_view_mode != ENGINE_NATIVE_DEBUG_VIEW_NONE) {
    if (submitted_debug_view_mode_ == ENGINE_NATIVE_DEBUG_VIEW_NONE) {
      submitted_debug_view_mode_ =
          static_cast<engine_native_debug_view_mode_t>(packet.debug_view_mode);
    } else if (submitted_debug_view_mode_ !=
               static_cast<engine_native_debug_view_mode_t>(
                   packet.debug_view_mode)) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }
  }

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

engine_native_status_t RendererState::CreateMeshFromBlob(
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_mesh) {
  return CreateResourceFromBlob(ResourceKind::kMesh, data, size, out_mesh);
}

engine_native_status_t RendererState::CreateMeshFromCpu(
    const engine_native_mesh_cpu_data_t& mesh_data,
    engine_native_resource_handle_t* out_mesh) {
  if (out_mesh == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  *out_mesh = kInvalidResourceHandle;

  if (mesh_data.positions == nullptr || mesh_data.indices == nullptr ||
      mesh_data.vertex_count == 0u || mesh_data.index_count == 0u ||
      (mesh_data.index_count % 3u) != 0u) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const uint32_t vertex_count = mesh_data.vertex_count;
  for (uint32_t i = 0u; i < mesh_data.index_count; ++i) {
    if (mesh_data.indices[i] >= vertex_count) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }
  }

  const size_t position_component_count =
      static_cast<size_t>(mesh_data.vertex_count) * 3u;
  if (position_component_count >
      (std::numeric_limits<size_t>::max() / sizeof(float))) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  const size_t position_bytes = position_component_count * sizeof(float);

  const size_t index_count = static_cast<size_t>(mesh_data.index_count);
  if (index_count > (std::numeric_limits<size_t>::max() / sizeof(uint32_t))) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  const size_t index_bytes = index_count * sizeof(uint32_t);

  std::vector<uint8_t> encoded_blob;
  try {
    encoded_blob.reserve(sizeof(kMeshCpuMagic) + sizeof(uint32_t) * 2u +
                         position_bytes + index_bytes);
    auto append_bytes = [&encoded_blob](const void* src, size_t bytes) {
      const auto* begin = static_cast<const uint8_t*>(src);
      encoded_blob.insert(encoded_blob.end(), begin, begin + bytes);
    };

    append_bytes(&kMeshCpuMagic, sizeof(kMeshCpuMagic));
    append_bytes(&mesh_data.vertex_count, sizeof(mesh_data.vertex_count));
    append_bytes(&mesh_data.index_count, sizeof(mesh_data.index_count));
    append_bytes(mesh_data.positions, position_bytes);
    append_bytes(mesh_data.indices, index_bytes);
  } catch (const std::bad_alloc&) {
    return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
  }

  return CreateResourceFromBlob(ResourceKind::kMesh, encoded_blob.data(),
                                encoded_blob.size(), out_mesh);
}

engine_native_status_t RendererState::CreateTextureFromBlob(
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_texture) {
  return CreateResourceFromBlob(ResourceKind::kTexture, data, size, out_texture);
}

engine_native_status_t RendererState::CreateTextureFromCpu(
    const engine_native_texture_cpu_data_t& texture_data,
    engine_native_resource_handle_t* out_texture) {
  if (out_texture == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  *out_texture = kInvalidResourceHandle;

  if (texture_data.rgba8 == nullptr || texture_data.width == 0u ||
      texture_data.height == 0u) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (texture_data.width > (std::numeric_limits<uint32_t>::max() / 4u)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const uint32_t required_stride = texture_data.width * 4u;
  const uint32_t stride =
      texture_data.stride == 0u ? required_stride : texture_data.stride;
  if (stride < required_stride) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const size_t row_bytes = static_cast<size_t>(required_stride);
  const size_t source_row_bytes = static_cast<size_t>(stride);
  const size_t row_count = static_cast<size_t>(texture_data.height);
  if (row_count == 0u ||
      row_bytes > (std::numeric_limits<size_t>::max() / row_count) ||
      source_row_bytes > (std::numeric_limits<size_t>::max() / row_count)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const size_t payload_bytes = row_bytes * row_count;
  std::vector<uint8_t> encoded_blob;
  try {
    encoded_blob.reserve(sizeof(kTextureCpuMagic) + sizeof(uint32_t) * 3u +
                         payload_bytes);
    auto append_bytes = [&encoded_blob](const void* src, size_t bytes) {
      const auto* begin = static_cast<const uint8_t*>(src);
      encoded_blob.insert(encoded_blob.end(), begin, begin + bytes);
    };

    append_bytes(&kTextureCpuMagic, sizeof(kTextureCpuMagic));
    append_bytes(&texture_data.width, sizeof(texture_data.width));
    append_bytes(&texture_data.height, sizeof(texture_data.height));
    append_bytes(&required_stride, sizeof(required_stride));

    const auto* pixels = texture_data.rgba8;
    for (size_t row = 0u; row < row_count; ++row) {
      const uint8_t* row_ptr = pixels + row * source_row_bytes;
      append_bytes(row_ptr, row_bytes);
    }
  } catch (const std::bad_alloc&) {
    return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
  }

  return CreateResourceFromBlob(ResourceKind::kTexture, encoded_blob.data(),
                                encoded_blob.size(), out_texture);
}

engine_native_status_t RendererState::CreateMaterialFromBlob(
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_material) {
  return CreateResourceFromBlob(ResourceKind::kMaterial, data, size, out_material);
}

engine_native_status_t RendererState::DestroyResource(
    engine_native_resource_handle_t handle) {
  if (handle == kInvalidResourceHandle) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const ResourceHandle resource_handle = DecodeResourceHandle(handle);
  ResourceBlob* blob = resources_.Get(resource_handle);
  if (blob == nullptr) {
    return ENGINE_NATIVE_STATUS_NOT_FOUND;
  }

  if (blob->kind == ResourceKind::kMaterial) {
    material_system_.RemoveMaterial(handle);
  }

  return resources_.Remove(resource_handle) ? ENGINE_NATIVE_STATUS_OK
                                            : ENGINE_NATIVE_STATUS_NOT_FOUND;
}

engine_native_status_t RendererState::CreateResourceFromBlob(
    ResourceKind kind,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_handle) {
  if (out_handle == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_handle = kInvalidResourceHandle;

  if (data == nullptr || size == 0u) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (!IsValidResourceBlob(kind, data, size)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  ResourceBlob blob;
  blob.kind = kind;
  const uint8_t* bytes = static_cast<const uint8_t*>(data);

  try {
    blob.bytes.assign(bytes, bytes + size);
  } catch (const std::bad_alloc&) {
    return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
  }

  ResourceHandle resource_handle{};
  const engine_native_status_t insert_status =
      resources_.Insert(std::move(blob), &resource_handle);
  if (insert_status != ENGINE_NATIVE_STATUS_OK) {
    return insert_status;
  }

  *out_handle = EncodeResourceHandle(resource_handle);
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RendererState::BuildFrameGraph() {
  render::FrameGraphBuildConfig build_config{
      .has_draws = submitted_draw_count_ > 0u,
      .has_ui = submitted_ui_count_ > 0u,
      .debug_view_mode = submitted_debug_view_mode_};
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
  submitted_debug_view_mode_ = ENGINE_NATIVE_DEBUG_VIEW_NONE;
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
