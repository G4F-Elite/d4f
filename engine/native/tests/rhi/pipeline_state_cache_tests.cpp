#include "rhi/pipeline_state_cache_tests.h"

#include <assert.h>

#include "rhi/pipeline_state_cache.h"

namespace dff::native::tests {
namespace {

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

}  // namespace

void RunPipelineStateCacheTests() {
  TestCacheTracksHitAndMissCounters();
  TestCacheEvictsLeastRecentlyUsedEntry();
}

}  // namespace dff::native::tests
