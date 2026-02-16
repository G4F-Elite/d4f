#ifndef DFF_ENGINE_NATIVE_MATERIAL_SYSTEM_H
#define DFF_ENGINE_NATIVE_MATERIAL_SYSTEM_H

#include <cstddef>
#include <cstdint>
#include <unordered_map>

#include "engine_native.h"

namespace dff::native::render {

struct ShaderVariantKey {
  uint32_t value = 0u;
};

class MaterialSystem {
 public:
  engine_native_status_t RegisterMaterial(engine_native_resource_handle_t material,
                                          uint32_t feature_flags);

  engine_native_status_t ResolveVariant(engine_native_resource_handle_t material,
                                        bool shadows_enabled,
                                        ShaderVariantKey* out_variant) const;

  void Clear() { feature_flags_by_material_.clear(); }

  size_t material_count() const { return feature_flags_by_material_.size(); }

 private:
  std::unordered_map<engine_native_resource_handle_t, uint32_t>
      feature_flags_by_material_;
};

}  // namespace dff::native::render

#endif
