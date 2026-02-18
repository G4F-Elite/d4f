#include "rhi/pipeline_state_cache.h"

#include <fstream>
#include <limits>

namespace dff::native::rhi {

namespace {

constexpr uint32_t kPipelineCacheDiskMagic = 0x43465044u;    // DPFC
constexpr uint32_t kPipelineCacheDiskVersion = 1u;

struct PipelineCacheDiskHeader {
  uint32_t magic = 0u;
  uint32_t version = 0u;
  uint32_t key_count = 0u;
  uint32_t reserved0 = 0u;
};

bool IsFilePathValid(const char* file_path) {
  return file_path != nullptr && file_path[0] != '\0';
}

}  // namespace

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

bool PipelineStateCache::LoadFromFile(const char* file_path) {
  if (!IsFilePathValid(file_path)) {
    return false;
  }

  std::ifstream stream(file_path, std::ios::binary);
  if (!stream.is_open()) {
    return false;
  }

  PipelineCacheDiskHeader header{};
  stream.read(reinterpret_cast<char*>(&header), sizeof(header));
  if (!stream.good()) {
    return false;
  }
  if (header.magic != kPipelineCacheDiskMagic ||
      header.version != kPipelineCacheDiskVersion) {
    return false;
  }

  if (header.key_count >
      static_cast<uint32_t>(std::numeric_limits<size_t>::max())) {
    return false;
  }

  Clear();
  for (uint32_t i = 0u; i < header.key_count; ++i) {
    uint64_t key = 0u;
    stream.read(reinterpret_cast<char*>(&key), sizeof(key));
    if (!stream.good()) {
      Clear();
      return false;
    }

    static_cast<void>(GetOrCreate(key));
  }

  hit_count_ = 0u;
  miss_count_ = 0u;
  return true;
}

bool PipelineStateCache::SaveToFile(const char* file_path) const {
  if (!IsFilePathValid(file_path)) {
    return false;
  }

  std::ofstream stream(file_path, std::ios::binary | std::ios::trunc);
  if (!stream.is_open()) {
    return false;
  }

  if (entries_.size() >
      static_cast<size_t>(std::numeric_limits<uint32_t>::max())) {
    return false;
  }

  PipelineCacheDiskHeader header{
      .magic = kPipelineCacheDiskMagic,
      .version = kPipelineCacheDiskVersion,
      .key_count = static_cast<uint32_t>(entries_.size()),
      .reserved0 = 0u,
  };
  stream.write(reinterpret_cast<const char*>(&header), sizeof(header));
  if (!stream.good()) {
    return false;
  }

  for (uint64_t key : lru_keys_) {
    stream.write(reinterpret_cast<const char*>(&key), sizeof(key));
    if (!stream.good()) {
      return false;
    }
  }

  return true;
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
