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

  engine_native_input_snapshot_t input{};
  engine_native_window_events_t events{};
  assert(engine_pump_events(engine, &input, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(input.frame_index == 1u);
  assert(events.should_close == 0u);

  engine_native_renderer_t* renderer = nullptr;
  engine_native_physics_t* physics = nullptr;
  assert(engine_get_renderer(engine, &renderer) == ENGINE_NATIVE_STATUS_OK);
  assert(engine_get_physics(engine, &physics) == ENGINE_NATIVE_STATUS_OK);

  engine_native_draw_item_t draw_items[2]{};
  draw_items[0].mesh = 11u;
  draw_items[0].material = 21u;
  draw_items[0].sort_key_high = 1u;
  draw_items[0].sort_key_low = 100u;
  draw_items[1].mesh = 12u;
  draw_items[1].material = 22u;
  draw_items[1].sort_key_high = 1u;
  draw_items[1].sort_key_low = 50u;

  engine_native_render_packet_t packet{
      .draw_items = draw_items,
      .draw_item_count = 2u,
      .ui_items = nullptr,
      .ui_item_count = 0u};

  assert(renderer_submit(renderer, &packet) ==
         ENGINE_NATIVE_STATUS_INVALID_STATE);

  void* frame_memory = nullptr;
  assert(renderer_begin_frame(renderer, 1024u, 64u, &frame_memory) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(frame_memory != nullptr);
  assert(renderer_submit(renderer, &packet) == ENGINE_NATIVE_STATUS_OK);
  assert(renderer_present(renderer) == ENGINE_NATIVE_STATUS_OK);

  assert(renderer_present(renderer) == ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(physics_step(physics, 1.0 / 60.0) == ENGINE_NATIVE_STATUS_INVALID_STATE);
  engine_native_body_write_t writes[1]{};
  writes[0].body = 1001u;
  writes[0].position[0] = 2.0f;
  writes[0].rotation[3] = 1.0f;
  writes[0].linear_velocity[0] = 3.0f;
  assert(physics_sync_from_world(physics, writes, 1u) == ENGINE_NATIVE_STATUS_OK);
  assert(physics_step(physics, 0.0) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(physics_step(physics, 1.0 / 60.0) == ENGINE_NATIVE_STATUS_OK);
  engine_native_body_read_t reads[2]{};
  uint32_t read_count = 0u;
  assert(physics_sync_to_world(physics, reads, 2u, &read_count) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(read_count == 1u);
  assert(reads[0].body == 1001u);
  assert(reads[0].position[0] == 2.0f);
  assert(reads[0].linear_velocity[0] == 3.0f);
  assert(physics_sync_to_world(physics, reads, 2u, &read_count) ==
         ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(physics_sync_from_world(physics, nullptr, 1u) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(renderer_begin_frame(renderer, 128u, 3u, &frame_memory) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

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
