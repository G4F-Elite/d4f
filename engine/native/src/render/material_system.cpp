#include "render/material_system.h"

namespace dff::native::render {

namespace {

constexpr uint32_t kFeatureNormalMap = 1u << 0u;
constexpr uint32_t kFeatureMetalRough = 1u << 1u;
constexpr uint32_t kFeatureAlphaMask = 1u << 2u;
constexpr uint32_t kVariantShadowBit = 1u << 8u;

constexpr uint32_t kAllowedFeatureMask =
    kFeatureNormalMap | kFeatureMetalRough | kFeatureAlphaMask;

}  // namespace

engine_native_status_t MaterialSystem::RegisterMaterial(
    engine_native_resource_handle_t material,
    uint32_t feature_flags) {
  if (material == 0u) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if ((feature_flags & ~kAllowedFeatureMask) != 0u) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  feature_flags_by_material_[material] = feature_flags;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t MaterialSystem::ResolveVariant(
    engine_native_resource_handle_t material,
    bool shadows_enabled,
    ShaderVariantKey* out_variant) const {
  if (material == 0u || out_variant == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  uint32_t feature_flags = 0u;
  const auto material_it = feature_flags_by_material_.find(material);
  if (material_it != feature_flags_by_material_.end()) {
    feature_flags = material_it->second;
  }

  out_variant->value = feature_flags;
  if (shadows_enabled) {
    out_variant->value |= kVariantShadowBit;
  }

  return ENGINE_NATIVE_STATUS_OK;
}

void MaterialSystem::RemoveMaterial(engine_native_resource_handle_t material) {
  if (material == 0u) {
    return;
  }

  feature_flags_by_material_.erase(material);
}

}  // namespace dff::native::render
