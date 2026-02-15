#include <assert.h>

#include <cstdint>

#include "core/resource_table.h"
#include "engine_native.h"

namespace {

void TestEngineCreateValidation() {
  engine_native_engine_t* engine = nullptr;

  assert(engine_create(nullptr, &engine) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(engine == nullptr);

  engine_native_create_desc_t wrong_version_desc{
      .api_version = ENGINE_NATIVE_API_VERSION + 1u,
      .user_data = nullptr};
  assert(engine_create(&wrong_version_desc, &engine) ==
         ENGINE_NATIVE_STATUS_VERSION_MISMATCH);
  assert(engine == nullptr);

  assert(engine_create(&wrong_version_desc, nullptr) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
}

void TestEngineAndSubsystemFlow() {
  engine_native_create_desc_t create_desc{
      .api_version = ENGINE_NATIVE_API_VERSION,
      .user_data = nullptr};

  engine_native_engine_t* engine = nullptr;
  assert(engine_create(&create_desc, &engine) == ENGINE_NATIVE_STATUS_OK);
  assert(engine != nullptr);

  assert(engine_pump_events(engine) == ENGINE_NATIVE_STATUS_OK);

  engine_native_renderer_t* renderer = nullptr;
  engine_native_physics_t* physics = nullptr;
  assert(engine_get_renderer(engine, &renderer) == ENGINE_NATIVE_STATUS_OK);
  assert(engine_get_physics(engine, &physics) == ENGINE_NATIVE_STATUS_OK);

  engine_native_render_packet_t packet{
      .entity_id = 42u,
      .debug_label = "entity:42"};
  engine_native_resource_handle_t submission = 0;

  assert(renderer_submit(renderer, &packet, &submission) ==
         ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(renderer_begin_frame(renderer) == ENGINE_NATIVE_STATUS_OK);
  assert(renderer_submit(renderer, &packet, &submission) == ENGINE_NATIVE_STATUS_OK);
  assert(submission != dff::native::kInvalidResourceHandle);
  assert(renderer_present(renderer) == ENGINE_NATIVE_STATUS_OK);

  assert(renderer_present(renderer) == ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(physics_step(physics, 1.0 / 60.0) == ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(physics_sync_from_world(physics) == ENGINE_NATIVE_STATUS_OK);
  assert(physics_step(physics, 0.0) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(physics_step(physics, 1.0 / 60.0) == ENGINE_NATIVE_STATUS_OK);
  assert(physics_sync_to_world(physics) == ENGINE_NATIVE_STATUS_OK);
  assert(physics_sync_to_world(physics) == ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(engine_destroy(engine) == ENGINE_NATIVE_STATUS_OK);
}

void TestResourceTableGeneration() {
  dff::native::ResourceTable<int> table;

  dff::native::ResourceHandle first{};
  assert(table.Insert(10, &first) == ENGINE_NATIVE_STATUS_OK);
  assert(table.Size() == 1u);
  assert(table.Get(first) != nullptr);
  assert(*table.Get(first) == 10);

  const engine_native_resource_handle_t encoded =
      dff::native::EncodeResourceHandle(first);
  const dff::native::ResourceHandle decoded =
      dff::native::DecodeResourceHandle(encoded);
  assert(decoded.index == first.index);
  assert(decoded.generation == first.generation);

  assert(table.Remove(first));
  assert(table.Get(first) == nullptr);

  dff::native::ResourceHandle second{};
  assert(table.Insert(20, &second) == ENGINE_NATIVE_STATUS_OK);
  assert(second.index == first.index);
  assert(second.generation != first.generation);
  assert(table.Get(second) != nullptr);
  assert(*table.Get(second) == 20);

  assert(!table.Remove(first));

  table.Clear();
  assert(table.Size() == 0u);
  assert(table.Get(second) == nullptr);
}

}  // namespace

int main() {
  TestEngineCreateValidation();
  TestEngineAndSubsystemFlow();
  TestResourceTableGeneration();

  assert(engine_destroy(nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  return 0;
}