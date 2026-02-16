#include <assert.h>

#include <cstdint>
#include <cmath>
#include <initializer_list>
#include <string>
#include <vector>

#include "bridge_capi/bridge_state.h"
#include "content/content_runtime_tests.h"
#include "core/resource_table.h"
#include "engine_native.h"
#include "platform/platform_state_tests.h"
#include "render/frame_graph_builder_tests.h"
#include "render/material_system_tests.h"
#include "render/render_graph_tests.h"
#include "rhi/pipeline_state_cache_tests.h"
#include "rhi/rhi_device_tests.h"

namespace {

void AssertPassOrder(const std::vector<std::string>& actual,
                     std::initializer_list<const char*> expected) {
  assert(actual.size() == expected.size());

  size_t index = 0u;
  for (const char* expected_name : expected) {
    assert(actual[index] == expected_name);
    ++index;
  }
}

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
  const auto* internal_engine = reinterpret_cast<const engine_native_engine*>(engine);
  assert(internal_engine->state.platform.pump_count() == 1u);

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
  AssertPassOrder(internal_engine->state.renderer.last_executed_rhi_passes(),
                  {"shadow", "pbr_opaque", "bloom", "tonemap", "color_grading",
                   "present"});
  engine_native_renderer_frame_stats_t renderer_stats{};
  assert(renderer_get_last_frame_stats(renderer, &renderer_stats) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(renderer_stats.draw_item_count == 2u);
  assert(renderer_stats.ui_item_count == 0u);
  assert(renderer_stats.executed_pass_count == 6u);
  assert(renderer_stats.present_count == 1u);
  assert(renderer_stats.pipeline_cache_hits == 0u);
  assert(renderer_stats.pipeline_cache_misses == 2u);
  assert((renderer_stats.pass_mask &
          (static_cast<uint64_t>(1u) << 3u)) != 0u);  // shadow
  assert((renderer_stats.pass_mask &
          (static_cast<uint64_t>(1u) << 6u)) != 0u);  // bloom
  assert((renderer_stats.pass_mask &
          (static_cast<uint64_t>(1u) << 7u)) != 0u);  // color grading
  assert((renderer_stats.pass_mask &
          (static_cast<uint64_t>(1u) << 2u)) != 0u);  // present
  assert(internal_engine->state.renderer.pipeline_cache_misses() == 2u);
  assert(internal_engine->state.renderer.pipeline_cache_hits() == 0u);
  assert(internal_engine->state.renderer.cached_pipeline_count() == 2u);
  assert(internal_engine->state.rhi_device.present_count() == 1u);
  const auto clear_color = internal_engine->state.renderer.last_clear_color();
  assert(clear_color[0] == 0.05f);
  assert(clear_color[1] == 0.07f);
  assert(clear_color[2] == 0.10f);
  assert(clear_color[3] == 1.0f);

  assert(renderer_present(renderer) == ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(physics_step(physics, 1.0 / 60.0) == ENGINE_NATIVE_STATUS_INVALID_STATE);
  engine_native_body_write_t writes[1]{};
  writes[0].body = 1001u;
  writes[0].body_type = 1u;
  writes[0].collider_shape = 0u;
  writes[0].is_trigger = 0u;
  writes[0].collider_dimensions[0] = 1.0f;
  writes[0].collider_dimensions[1] = 1.0f;
  writes[0].collider_dimensions[2] = 1.0f;
  writes[0].friction = 0.5f;
  writes[0].restitution = 0.1f;
  writes[0].position[0] = 2.0f;
  writes[0].rotation[3] = 1.0f;
  writes[0].linear_velocity[0] = 3.0f;
  assert(physics_sync_from_world(physics, writes, 1u) == ENGINE_NATIVE_STATUS_OK);
  assert(physics_step(physics, 0.0) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(physics_step(physics, 1.0 / 60.0) == ENGINE_NATIVE_STATUS_OK);
  engine_native_raycast_query_t query{};
  query.origin[0] = 0.0f;
  query.origin[1] = 0.0f;
  query.origin[2] = 0.0f;
  query.direction[0] = 1.0f;
  query.direction[1] = 0.0f;
  query.direction[2] = 0.0f;
  query.max_distance = 10.0f;
  query.include_triggers = 1u;
  engine_native_raycast_hit_t raycast_hit{};
  assert(physics_raycast(physics, &query, &raycast_hit) == ENGINE_NATIVE_STATUS_OK);
  assert(raycast_hit.has_hit == 1u);
  assert(raycast_hit.body == 1001u);
  assert(std::fabs(raycast_hit.distance - 1.55f) < 0.001f);
  engine_native_sweep_query_t sweep_query{};
  sweep_query.origin[0] = 0.0f;
  sweep_query.origin[1] = 0.0f;
  sweep_query.origin[2] = 0.0f;
  sweep_query.direction[0] = 1.0f;
  sweep_query.direction[1] = 0.0f;
  sweep_query.direction[2] = 0.0f;
  sweep_query.max_distance = 10.0f;
  sweep_query.include_triggers = 1u;
  sweep_query.shape_type = 1u;
  sweep_query.shape_dimensions[0] = 1.0f;
  sweep_query.shape_dimensions[1] = 1.0f;
  sweep_query.shape_dimensions[2] = 1.0f;
  engine_native_sweep_hit_t sweep_hit{};
  assert(physics_sweep(physics, &sweep_query, &sweep_hit) == ENGINE_NATIVE_STATUS_OK);
  assert(sweep_hit.has_hit == 1u);
  assert(sweep_hit.body == 1001u);
  assert(std::fabs(sweep_hit.distance - 0.684f) < 0.01f);

  engine_native_overlap_query_t overlap_query{};
  overlap_query.center[0] = 2.05f;
  overlap_query.center[1] = 0.0f;
  overlap_query.center[2] = 0.0f;
  overlap_query.include_triggers = 1u;
  overlap_query.shape_type = 0u;
  overlap_query.shape_dimensions[0] = 1.0f;
  overlap_query.shape_dimensions[1] = 1.0f;
  overlap_query.shape_dimensions[2] = 1.0f;
  engine_native_overlap_hit_t overlap_hits[1]{};
  uint32_t overlap_count = 0u;
  assert(physics_overlap(physics, &overlap_query, overlap_hits, 1u, &overlap_count) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(overlap_count == 1u);
  assert(overlap_hits[0].body == 1001u);
  assert(overlap_hits[0].is_trigger == 0u);

  engine_native_body_read_t reads[2]{};
  uint32_t read_count = 0u;
  assert(physics_sync_to_world(physics, reads, 2u, &read_count) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(read_count == 1u);
  assert(reads[0].body == 1001u);
  assert(std::fabs(reads[0].position[0] - 2.05f) < 0.001f);
  assert(reads[0].linear_velocity[0] == 3.0f);
  assert(physics_sync_to_world(physics, reads, 2u, &read_count) ==
         ENGINE_NATIVE_STATUS_INVALID_STATE);

  assert(physics_sync_from_world(physics, nullptr, 1u) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  engine_native_body_write_t invalid_write[1]{};
  invalid_write[0].body = 555u;
  invalid_write[0].body_type = 9u;
  invalid_write[0].collider_shape = 0u;
  invalid_write[0].collider_dimensions[0] = 1.0f;
  invalid_write[0].collider_dimensions[1] = 1.0f;
  invalid_write[0].collider_dimensions[2] = 1.0f;
  invalid_write[0].friction = 0.2f;
  invalid_write[0].restitution = 0.3f;
  assert(physics_sync_from_world(physics, invalid_write, 1u) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  engine_native_raycast_query_t invalid_query{};
  invalid_query.direction[0] = 0.0f;
  invalid_query.direction[1] = 0.0f;
  invalid_query.direction[2] = 0.0f;
  invalid_query.max_distance = 10.0f;
  engine_native_sweep_query_t invalid_sweep_query{};
  invalid_sweep_query.direction[0] = 0.0f;
  invalid_sweep_query.direction[1] = 0.0f;
  invalid_sweep_query.direction[2] = 0.0f;
  invalid_sweep_query.max_distance = 10.0f;
  invalid_sweep_query.shape_type = 0u;
  invalid_sweep_query.shape_dimensions[0] = 1.0f;
  invalid_sweep_query.shape_dimensions[1] = 1.0f;
  invalid_sweep_query.shape_dimensions[2] = 1.0f;
  engine_native_overlap_query_t invalid_overlap_query{};
  invalid_overlap_query.shape_type = 1u;
  invalid_overlap_query.shape_dimensions[0] = 1.0f;
  invalid_overlap_query.shape_dimensions[1] = 2.0f;
  invalid_overlap_query.shape_dimensions[2] = 1.0f;
  assert(physics_raycast(physics, nullptr, &raycast_hit) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(physics_raycast(physics, &invalid_query, &raycast_hit) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(physics_raycast(physics, &query, nullptr) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(physics_sweep(physics, nullptr, &sweep_hit) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(physics_sweep(physics, &invalid_sweep_query, &sweep_hit) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(physics_sweep(physics, &sweep_query, nullptr) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(physics_overlap(physics, nullptr, overlap_hits, 1u, &overlap_count) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(physics_overlap(physics, &invalid_overlap_query, overlap_hits, 1u,
                         &overlap_count) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(physics_overlap(physics, &overlap_query, nullptr, 1u, &overlap_count) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(physics_overlap(physics, &overlap_query, overlap_hits, 1u, nullptr) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(renderer_begin_frame(renderer, 128u, 3u, &frame_memory) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(renderer_get_last_frame_stats(renderer, nullptr) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(renderer_get_last_frame_stats(nullptr, &renderer_stats) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  assert(engine_destroy(engine) == ENGINE_NATIVE_STATUS_OK);
}

void TestRendererPassOrderForDrawAndUiScenarios() {
  engine_native_create_desc_t create_desc{
      .api_version = ENGINE_NATIVE_API_VERSION,
      .user_data = nullptr};

  engine_native_engine_t* engine = nullptr;
  assert(engine_create(&create_desc, &engine) == ENGINE_NATIVE_STATUS_OK);
  assert(engine != nullptr);

  auto* internal_engine = reinterpret_cast<const engine_native_engine*>(engine);

  engine_native_renderer_t* renderer = nullptr;
  assert(engine_get_renderer(engine, &renderer) == ENGINE_NATIVE_STATUS_OK);
  assert(renderer != nullptr);

  void* frame_memory = nullptr;
  assert(renderer_begin_frame(renderer, 1024u, 64u, &frame_memory) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(frame_memory != nullptr);

  engine_native_draw_item_t draw_batch_a[1]{};
  draw_batch_a[0].mesh = 1u;
  draw_batch_a[0].material = 2u;

  engine_native_draw_item_t draw_batch_b[1]{};
  draw_batch_b[0].mesh = 3u;
  draw_batch_b[0].material = 4u;

  engine_native_render_packet_t draw_packet_a{
      .draw_items = draw_batch_a,
      .draw_item_count = 1u,
      .ui_items = nullptr,
      .ui_item_count = 0u};
  engine_native_render_packet_t draw_packet_b{
      .draw_items = draw_batch_b,
      .draw_item_count = 1u,
      .ui_items = nullptr,
      .ui_item_count = 0u};

  assert(renderer_submit(renderer, &draw_packet_a) == ENGINE_NATIVE_STATUS_OK);
  assert(renderer_submit(renderer, &draw_packet_b) == ENGINE_NATIVE_STATUS_OK);
  assert(renderer_present(renderer) == ENGINE_NATIVE_STATUS_OK);
  AssertPassOrder(internal_engine->state.renderer.last_executed_rhi_passes(),
                  {"shadow", "pbr_opaque", "bloom", "tonemap", "color_grading",
                   "present"});

  frame_memory = nullptr;
  assert(renderer_begin_frame(renderer, 1024u, 64u, &frame_memory) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(frame_memory != nullptr);

  engine_native_ui_draw_item_t ui_batch[2]{};
  ui_batch[0].texture = 10u;
  ui_batch[0].vertex_count = 6u;
  ui_batch[0].index_count = 6u;
  ui_batch[1].texture = 11u;
  ui_batch[1].vertex_count = 6u;
  ui_batch[1].index_count = 6u;

  engine_native_render_packet_t ui_packet{
      .draw_items = nullptr,
      .draw_item_count = 0u,
      .ui_items = ui_batch,
      .ui_item_count = 2u};

  assert(renderer_submit(renderer, &ui_packet) == ENGINE_NATIVE_STATUS_OK);
  assert(renderer_present(renderer) == ENGINE_NATIVE_STATUS_OK);
  AssertPassOrder(internal_engine->state.renderer.last_executed_rhi_passes(),
                  {"ui", "present"});

  frame_memory = nullptr;
  assert(renderer_begin_frame(renderer, 1024u, 64u, &frame_memory) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(frame_memory != nullptr);

  engine_native_draw_item_t draw_batch[1]{};
  draw_batch[0].mesh = 20u;
  draw_batch[0].material = 30u;

  engine_native_render_packet_t draw_and_ui_packet{
      .draw_items = draw_batch,
      .draw_item_count = 1u,
      .ui_items = ui_batch,
      .ui_item_count = 2u};

  assert(renderer_submit(renderer, &draw_and_ui_packet) == ENGINE_NATIVE_STATUS_OK);
  assert(renderer_present(renderer) == ENGINE_NATIVE_STATUS_OK);
  AssertPassOrder(internal_engine->state.renderer.last_executed_rhi_passes(),
                  {"shadow", "pbr_opaque", "bloom", "tonemap", "color_grading",
                   "ui", "present"});

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
  TestRendererPassOrderForDrawAndUiScenarios();
  dff::native::tests::RunContentRuntimeTests();
  TestResourceTableGeneration();
  dff::native::tests::RunPlatformStateTests();
  dff::native::tests::RunFrameGraphBuilderTests();
  dff::native::tests::RunMaterialSystemTests();
  dff::native::tests::RunPipelineStateCacheTests();
  dff::native::tests::RunRhiDeviceTests();
  dff::native::tests::RunRenderGraphTests();

  assert(engine_destroy(nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  return 0;
}
