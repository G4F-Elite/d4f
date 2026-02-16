#include "rhi/pipeline_state_cache.h"

namespace dff::native::rhi {

const PipelineStateRecord& PipelineStateCache::GetOrCreate(uint64_t key) {
  auto entry_it = entries_.find(key);
  if (entry_it != entries_.end()) {
    ++hit_count_;
    Touch(&entry_it->second);
    return entry_it->second.record;
  }

  ++miss_count_;
  EvictIfNeeded();

  lru_keys_.push_back(key);
  CacheEntry entry{
      .record =
          PipelineStateRecord{
              .key = key,
              .generation = next_generation_++,
          },
      .lru_it = std::prev(lru_keys_.end()),
  };

  const auto [inserted_it, _] = entries_.emplace(key, entry);
  return inserted_it->second.record;
}

void PipelineStateCache::Clear() {
  lru_keys_.clear();
  entries_.clear();
  hit_count_ = 0u;
  miss_count_ = 0u;
  next_generation_ = 1u;
}

void PipelineStateCache::Touch(CacheEntry* entry) {
  if (entry == nullptr) {
    return;
  }

  lru_keys_.splice(lru_keys_.end(), lru_keys_, entry->lru_it);
  entry->lru_it = std::prev(lru_keys_.end());
}

void PipelineStateCache::EvictIfNeeded() {
  if (capacity_ == 0u) {
    entries_.clear();
    lru_keys_.clear();
    return;
  }

  while (entries_.size() >= capacity_ && !lru_keys_.empty()) {
    const uint64_t oldest_key = lru_keys_.front();
    lru_keys_.pop_front();
    entries_.erase(oldest_key);
  }
}

}  // namespace dff::native::rhi
