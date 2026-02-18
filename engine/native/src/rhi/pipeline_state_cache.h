#ifndef DFF_ENGINE_NATIVE_PIPELINE_STATE_CACHE_H
#define DFF_ENGINE_NATIVE_PIPELINE_STATE_CACHE_H

#include <cstddef>
#include <cstdint>
#include <list>
#include <unordered_map>

namespace dff::native::rhi {

struct PipelineStateRecord {
  uint64_t key = 0u;
  uint64_t generation = 0u;
};

class PipelineStateCache {
 public:
  explicit PipelineStateCache(size_t capacity = 256u) : capacity_(capacity) {}

  const PipelineStateRecord& GetOrCreate(uint64_t key);
  bool LoadFromFile(const char* file_path);
  bool SaveToFile(const char* file_path) const;

  void Clear();

  size_t size() const { return entries_.size(); }
  uint64_t hit_count() const { return hit_count_; }
  uint64_t miss_count() const { return miss_count_; }

 private:
  struct CacheEntry {
    PipelineStateRecord record;
    std::list<uint64_t>::iterator lru_it;
  };

  void Touch(CacheEntry* entry);
  void EvictIfNeeded();

  size_t capacity_ = 256u;
  uint64_t next_generation_ = 1u;
  uint64_t hit_count_ = 0u;
  uint64_t miss_count_ = 0u;
  std::list<uint64_t> lru_keys_;
  std::unordered_map<uint64_t, CacheEntry> entries_;
};

}  // namespace dff::native::rhi

#endif
