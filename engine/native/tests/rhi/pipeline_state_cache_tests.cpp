#include "rhi/pipeline_state_cache_tests.h"

#include <assert.h>

#include <chrono>
#include <filesystem>
#include <string>

#include "rhi/pipeline_state_cache.h"

namespace dff::native::tests {
namespace {

std::filesystem::path MakeTempCachePath() {
  const auto stamp =
      std::chrono::high_resolution_clock::now().time_since_epoch().count();
  return std::filesystem::temp_directory_path() /
         ("dff-pipeline-cache-tests-" + std::to_string(stamp) + ".bin");
}

void TestCacheTracksHitAndMissCounters() {
  dff::native::rhi::PipelineStateCache cache(8u);

  const auto& first = cache.GetOrCreate(0xAAu);
  const auto& second = cache.GetOrCreate(0xAAu);
  const auto& third = cache.GetOrCreate(0xBBu);

  assert(first.key == 0xAAu);
  assert(second.key == 0xAAu);
  assert(first.generation == second.generation);
  assert(third.key == 0xBBu);
  assert(cache.miss_count() == 2u);
  assert(cache.hit_count() == 1u);
  assert(cache.size() == 2u);
}

void TestCacheEvictsLeastRecentlyUsedEntry() {
  dff::native::rhi::PipelineStateCache cache(2u);

  const auto first = cache.GetOrCreate(1u);
  static_cast<void>(cache.GetOrCreate(2u));
  static_cast<void>(cache.GetOrCreate(3u));

  assert(cache.size() == 2u);
  assert(cache.miss_count() == 3u);
  assert(cache.hit_count() == 0u);

  const auto reinserted_first = cache.GetOrCreate(1u);
  assert(cache.miss_count() == 4u);
  assert(cache.hit_count() == 0u);
  assert(reinserted_first.generation > first.generation);
}

void TestCacheCanPersistAndRestoreEntries() {
  const std::filesystem::path path = MakeTempCachePath();

  dff::native::rhi::PipelineStateCache source_cache(8u);
  static_cast<void>(source_cache.GetOrCreate(0xABCDu));
  static_cast<void>(source_cache.GetOrCreate(0x1001u));
  static_cast<void>(source_cache.GetOrCreate(0x1002u));
  assert(source_cache.SaveToFile(path.string().c_str()));

  dff::native::rhi::PipelineStateCache restored_cache(8u);
  assert(restored_cache.LoadFromFile(path.string().c_str()));
  assert(restored_cache.size() == 3u);
  assert(restored_cache.hit_count() == 0u);
  assert(restored_cache.miss_count() == 0u);

  static_cast<void>(restored_cache.GetOrCreate(0xABCDu));
  assert(restored_cache.hit_count() == 1u);
  assert(restored_cache.miss_count() == 0u);

  std::error_code remove_error;
  std::filesystem::remove(path, remove_error);
}

void TestCacheRejectsInvalidPersistenceInputs() {
  dff::native::rhi::PipelineStateCache cache(4u);
  static_cast<void>(cache.GetOrCreate(77u));
  assert(!cache.SaveToFile(nullptr));
  assert(!cache.LoadFromFile(nullptr));

  const std::filesystem::path missing_path = MakeTempCachePath();
  std::error_code remove_error;
  std::filesystem::remove(missing_path, remove_error);
  assert(!cache.LoadFromFile(missing_path.string().c_str()));
}

}  // namespace

void RunPipelineStateCacheTests() {
  TestCacheTracksHitAndMissCounters();
  TestCacheEvictsLeastRecentlyUsedEntry();
  TestCacheCanPersistAndRestoreEntries();
  TestCacheRejectsInvalidPersistenceInputs();
}

}  // namespace dff::native::tests
