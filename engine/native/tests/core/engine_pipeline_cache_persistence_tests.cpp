#include "core/engine_pipeline_cache_persistence_tests.h"

#include <assert.h>

#include <chrono>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <string>

#include "engine_native.h"

namespace dff::native::tests {
namespace {

constexpr const char* kPipelineCachePathEnv = "DFF_PIPELINE_CACHE_PATH";

std::filesystem::path MakeTempCachePath() {
  const auto stamp =
      std::chrono::high_resolution_clock::now().time_since_epoch().count();
  return std::filesystem::temp_directory_path() /
         ("dff-engine-pipeline-cache-" + std::to_string(stamp) + ".bin");
}

bool SetPipelineCachePathEnv(const std::filesystem::path& path) {
#if defined(_WIN32)
  return _putenv_s(kPipelineCachePathEnv, path.string().c_str()) == 0;
#else
  return setenv(kPipelineCachePathEnv, path.string().c_str(), 1) == 0;
#endif
}

bool ClearPipelineCachePathEnv() {
#if defined(_WIN32)
  return _putenv_s(kPipelineCachePathEnv, "") == 0;
#else
  return unsetenv(kPipelineCachePathEnv) == 0;
#endif
}

engine_native_status_t ExecuteSingleDrawFrame(
    engine_native_engine_t* engine,
    engine_native_resource_handle_t material,
    engine_native_renderer_frame_stats_t* out_stats) {
  if (engine == nullptr || out_stats == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  engine_native_renderer_t* renderer = nullptr;
  engine_native_status_t status = engine_get_renderer(engine, &renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  void* frame_memory = nullptr;
  status = renderer_begin_frame(renderer, 1024u, 64u, &frame_memory);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  engine_native_draw_item_t draw_items[1]{};
  draw_items[0].mesh = 10u;
  draw_items[0].material = material;
  draw_items[0].sort_key_high = 1u;
  draw_items[0].sort_key_low = 1u;

  engine_native_render_packet_t packet{
      .draw_items = draw_items,
      .draw_item_count = 1u,
      .ui_items = nullptr,
      .ui_item_count = 0u};

  status = renderer_submit(renderer, &packet);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  status = renderer_present(renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return renderer_get_last_frame_stats(renderer, out_stats);
}

void TestPipelineCachePersistsAcrossEngineLifetime() {
  const std::filesystem::path cache_path = MakeTempCachePath();
  std::error_code remove_error;
  std::filesystem::remove(cache_path, remove_error);

  assert(SetPipelineCachePathEnv(cache_path));

  engine_native_create_desc_t create_desc{
      .api_version = ENGINE_NATIVE_API_VERSION,
      .user_data = nullptr};

  engine_native_engine_t* first_engine = nullptr;
  assert(engine_create(&create_desc, &first_engine) == ENGINE_NATIVE_STATUS_OK);
  assert(first_engine != nullptr);

  engine_native_renderer_frame_stats_t first_stats{};
  assert(ExecuteSingleDrawFrame(first_engine, 501u, &first_stats) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(first_stats.pipeline_cache_hits == 0u);
  assert(first_stats.pipeline_cache_misses >= 1u);
  assert(engine_destroy(first_engine) == ENGINE_NATIVE_STATUS_OK);
  assert(std::filesystem::exists(cache_path));
  assert(std::filesystem::file_size(cache_path) > 0u);

  engine_native_engine_t* second_engine = nullptr;
  assert(engine_create(&create_desc, &second_engine) == ENGINE_NATIVE_STATUS_OK);
  assert(second_engine != nullptr);

  engine_native_renderer_frame_stats_t second_stats{};
  assert(ExecuteSingleDrawFrame(second_engine, 501u, &second_stats) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(second_stats.pipeline_cache_hits >= 1u);
  assert(second_stats.pipeline_cache_misses == 0u);
  assert(engine_destroy(second_engine) == ENGINE_NATIVE_STATUS_OK);

  assert(ClearPipelineCachePathEnv());
  std::filesystem::remove(cache_path, remove_error);
}

void TestPipelineCacheCorruptedFileIsIgnored() {
  const std::filesystem::path cache_path = MakeTempCachePath();
  std::error_code remove_error;
  std::filesystem::remove(cache_path, remove_error);

  {
    std::ofstream corrupted(cache_path, std::ios::binary | std::ios::trunc);
    assert(corrupted.is_open());
    const uint8_t bytes[4]{0xFFu, 0x00u, 0x12u, 0x77u};
    corrupted.write(reinterpret_cast<const char*>(bytes), sizeof(bytes));
  }

  assert(SetPipelineCachePathEnv(cache_path));

  engine_native_create_desc_t create_desc{
      .api_version = ENGINE_NATIVE_API_VERSION,
      .user_data = nullptr};

  engine_native_engine_t* engine = nullptr;
  assert(engine_create(&create_desc, &engine) == ENGINE_NATIVE_STATUS_OK);
  assert(engine != nullptr);

  engine_native_renderer_frame_stats_t stats{};
  assert(ExecuteSingleDrawFrame(engine, 777u, &stats) == ENGINE_NATIVE_STATUS_OK);
  assert(stats.pipeline_cache_hits == 0u);
  assert(stats.pipeline_cache_misses >= 1u);
  assert(engine_destroy(engine) == ENGINE_NATIVE_STATUS_OK);
  assert(std::filesystem::exists(cache_path));
  assert(std::filesystem::file_size(cache_path) > sizeof(uint32_t));

  assert(ClearPipelineCachePathEnv());
  std::filesystem::remove(cache_path, remove_error);
}

}  // namespace

void RunEnginePipelineCachePersistenceTests() {
  TestPipelineCachePersistsAcrossEngineLifetime();
  TestPipelineCacheCorruptedFileIsIgnored();
}

}  // namespace dff::native::tests
