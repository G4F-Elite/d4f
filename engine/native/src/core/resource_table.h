#ifndef DFF_ENGINE_NATIVE_RESOURCE_TABLE_H
#define DFF_ENGINE_NATIVE_RESOURCE_TABLE_H

#include <cstddef>
#include <limits>
#include <new>
#include <optional>
#include <utility>
#include <vector>

#include "engine_native.h"

namespace dff::native {

struct ResourceHandle {
  uint32_t index;
  uint32_t generation;
};

constexpr engine_native_resource_handle_t kInvalidResourceHandle = 0;

inline engine_native_resource_handle_t EncodeResourceHandle(
    ResourceHandle handle) {
  return (static_cast<engine_native_resource_handle_t>(handle.generation) << 32u) |
         static_cast<engine_native_resource_handle_t>(handle.index);
}

inline ResourceHandle DecodeResourceHandle(
    engine_native_resource_handle_t handle) {
  return ResourceHandle{
      .index = static_cast<uint32_t>(handle & 0xFFFFFFFFu),
      .generation = static_cast<uint32_t>((handle >> 32u) & 0xFFFFFFFFu)};
}

template <typename T>
class ResourceTable {
 public:
  engine_native_status_t Insert(T value, ResourceHandle* out_handle) {
    if (out_handle == nullptr) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    try {
      if (!free_indices_.empty()) {
        const uint32_t index = free_indices_.back();
        free_indices_.pop_back();
        Slot& slot = slots_[index];
        slot.value.emplace(std::move(value));
        *out_handle = ResourceHandle{.index = index, .generation = slot.generation};
        ++size_;
        return ENGINE_NATIVE_STATUS_OK;
      }

      if (slots_.size() >=
          static_cast<size_t>(std::numeric_limits<uint32_t>::max()) + 1u) {
        return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
      }

      Slot slot;
      slot.value.emplace(std::move(value));
      slots_.push_back(std::move(slot));
      *out_handle = ResourceHandle{
          .index = static_cast<uint32_t>(slots_.size() - 1u),
          .generation = 1u};
      ++size_;
      return ENGINE_NATIVE_STATUS_OK;
    } catch (const std::bad_alloc&) {
      return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
    }
  }

  bool Remove(ResourceHandle handle) {
    if (handle.index >= slots_.size()) {
      return false;
    }

    Slot& slot = slots_[handle.index];
    if (!slot.value.has_value() || slot.generation != handle.generation) {
      return false;
    }

    slot.value.reset();
    slot.generation = (slot.generation == std::numeric_limits<uint32_t>::max())
                          ? 1u
                          : slot.generation + 1u;
    free_indices_.push_back(handle.index);
    --size_;
    return true;
  }

  T* Get(ResourceHandle handle) {
    if (handle.index >= slots_.size()) {
      return nullptr;
    }

    Slot& slot = slots_[handle.index];
    if (!slot.value.has_value() || slot.generation != handle.generation) {
      return nullptr;
    }

    return &slot.value.value();
  }

  const T* Get(ResourceHandle handle) const {
    if (handle.index >= slots_.size()) {
      return nullptr;
    }

    const Slot& slot = slots_[handle.index];
    if (!slot.value.has_value() || slot.generation != handle.generation) {
      return nullptr;
    }

    return &slot.value.value();
  }

  void Clear() {
    free_indices_.clear();
    free_indices_.reserve(slots_.size());

    for (size_t index = 0; index < slots_.size(); ++index) {
      Slot& slot = slots_[index];
      if (slot.value.has_value()) {
        slot.value.reset();
        slot.generation = (slot.generation == std::numeric_limits<uint32_t>::max())
                              ? 1u
                              : slot.generation + 1u;
      }
      free_indices_.push_back(static_cast<uint32_t>(index));
    }

    size_ = 0u;
  }

  size_t Size() const { return size_; }

 private:
  struct Slot {
    std::optional<T> value;
    uint32_t generation = 1u;
  };

  std::vector<Slot> slots_;
  std::vector<uint32_t> free_indices_;
  size_t size_ = 0u;
};

}  // namespace dff::native

#endif
