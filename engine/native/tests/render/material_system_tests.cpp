#include "render/material_system_tests.h"

#include <assert.h>

#include "render/material_system.h"

namespace dff::native::tests {
namespace {

void TestRegisterAndResolveVariant() {
  dff::native::render::MaterialSystem materials;
  const engine_native_resource_handle_t material = 42u;
  constexpr uint32_t kFeatures = 0x5u;

  assert(materials.RegisterMaterial(material, kFeatures) == ENGINE_NATIVE_STATUS_OK);
  assert(materials.material_count() == 1u);

  dff::native::render::ShaderVariantKey variant{};
  assert(materials.ResolveVariant(material, /*shadows_enabled=*/true, &variant) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(variant.value == (kFeatures | (1u << 8u)));
}

void TestResolveVariantForUnknownMaterialUsesDefault() {
  dff::native::render::MaterialSystem materials;
  dff::native::render::ShaderVariantKey variant{};

  assert(materials.ResolveVariant(55u, /*shadows_enabled=*/false, &variant) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(variant.value == 0u);

  assert(materials.ResolveVariant(55u, /*shadows_enabled=*/true, &variant) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(variant.value == (1u << 8u));
}

void TestValidation() {
  dff::native::render::MaterialSystem materials;
  dff::native::render::ShaderVariantKey variant{};

  assert(materials.RegisterMaterial(0u, 0u) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(materials.RegisterMaterial(99u, 0x20u) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(materials.ResolveVariant(0u, false, &variant) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(materials.ResolveVariant(99u, false, nullptr) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
}

}  // namespace

void RunMaterialSystemTests() {
  TestRegisterAndResolveVariant();
  TestResolveVariantForUnknownMaterialUsesDefault();
  TestValidation();
}

}  // namespace dff::native::tests
