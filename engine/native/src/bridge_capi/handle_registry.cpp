#include "bridge_capi/handle_registry.h"

#include <mutex>
#include <vector>

namespace dff::native::bridge {
namespace {

constexpr uint64_t kHandleIndexMask = 0xFFFFFFFFull;

struct HandleEntry {
  uint32_t generation = 1u;
  void* object = nullptr;
  engine_native_engine_t* owner = nullptr;
  uint8_t owns_state = 0u;
};

class HandleRegistry {
 public:
  engine_native_status_t GetOrCreate(
      void* object,
      engine_native_engine_t* owner,
      bool owns_state,
      uint64_t* out_handle) {
    if (object == nullptr || out_handle == nullptr) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    *out_handle = ENGINE_NATIVE_INVALID_HANDLE;
    std::lock_guard<std::mutex> guard(mutex_);

    for (size_t i = 0u; i < entries_.size(); ++i) {
      const HandleEntry& entry = entries_[i];
      if (entry.object == object) {
        *out_handle = Encode(i, entry.generation);
        return ENGINE_NATIVE_STATUS_OK;
      }
    }

    size_t index = entries_.size();
    for (size_t i = 0u; i < entries_.size(); ++i) {
      if (entries_[i].object == nullptr) {
        index = i;
        break;
      }
    }

    if (index == entries_.size()) {
      try {
        entries_.push_back(HandleEntry{});
      } catch (...) {
        return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
      }
    }

    HandleEntry& entry = entries_[index];
    if (entry.generation == 0u) {
      entry.generation = 1u;
    }

    entry.object = object;
    entry.owner = owner;
    entry.owns_state = owns_state ? 1u : 0u;
    *out_handle = Encode(index, entry.generation);
    return ENGINE_NATIVE_STATUS_OK;
  }

  engine_native_status_t Resolve(
      uint64_t handle,
      void** out_object,
      engine_native_engine_t** out_owner,
      bool* out_owns_state) const {
    if (out_object == nullptr) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    *out_object = nullptr;
    if (out_owner != nullptr) {
      *out_owner = nullptr;
    }
    if (out_owns_state != nullptr) {
      *out_owns_state = false;
    }

    DecodedHandle decoded{};
    if (!Decode(handle, &decoded)) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    std::lock_guard<std::mutex> guard(mutex_);
    if (decoded.index >= entries_.size()) {
      return ENGINE_NATIVE_STATUS_NOT_FOUND;
    }

    const HandleEntry& entry = entries_[decoded.index];
    if (entry.object == nullptr || entry.generation != decoded.generation) {
      return ENGINE_NATIVE_STATUS_NOT_FOUND;
    }

    *out_object = entry.object;
    if (out_owner != nullptr) {
      *out_owner = entry.owner;
    }
    if (out_owns_state != nullptr) {
      *out_owns_state = entry.owns_state != 0u;
    }

    return ENGINE_NATIVE_STATUS_OK;
  }

  void RemoveByObject(void* object) {
    if (object == nullptr) {
      return;
    }

    std::lock_guard<std::mutex> guard(mutex_);
    for (size_t i = 0u; i < entries_.size(); ++i) {
      if (entries_[i].object == object) {
        Invalidate(i);
      }
    }
  }

  void RemoveByOwner(engine_native_engine_t* owner) {
    if (owner == nullptr) {
      return;
    }

    std::lock_guard<std::mutex> guard(mutex_);
    for (size_t i = 0u; i < entries_.size(); ++i) {
      if (entries_[i].object != nullptr && entries_[i].owner == owner) {
        Invalidate(i);
      }
    }
  }

 private:
  struct DecodedHandle {
    size_t index = 0u;
    uint32_t generation = 0u;
  };

  static uint64_t Encode(size_t index, uint32_t generation) {
    const uint64_t encoded_index = static_cast<uint64_t>(index + 1u);
    return (static_cast<uint64_t>(generation) << 32u) | encoded_index;
  }

  static bool Decode(uint64_t handle, DecodedHandle* out_decoded) {
    if (out_decoded == nullptr || handle == ENGINE_NATIVE_INVALID_HANDLE) {
      return false;
    }

    const uint32_t encoded_index =
        static_cast<uint32_t>(handle & kHandleIndexMask);
    const uint32_t generation = static_cast<uint32_t>(handle >> 32u);
    if (encoded_index == 0u || generation == 0u) {
      return false;
    }

    out_decoded->index = static_cast<size_t>(encoded_index - 1u);
    out_decoded->generation = generation;
    return true;
  }

  void Invalidate(size_t index) {
    HandleEntry& entry = entries_[index];
    entry.object = nullptr;
    entry.owner = nullptr;
    entry.owns_state = 0u;
    ++entry.generation;
    if (entry.generation == 0u) {
      entry.generation = 1u;
    }
  }

  mutable std::mutex mutex_;
  std::vector<HandleEntry> entries_;
};

HandleRegistry& EngineRegistry() {
  static HandleRegistry registry;
  return registry;
}

HandleRegistry& RendererRegistry() {
  static HandleRegistry registry;
  return registry;
}

HandleRegistry& PhysicsRegistry() {
  static HandleRegistry registry;
  return registry;
}

HandleRegistry& AudioRegistry() {
  static HandleRegistry registry;
  return registry;
}

HandleRegistry& NetRegistry() {
  static HandleRegistry registry;
  return registry;
}

engine_native_status_t ResolveTypedHandle(
    const HandleRegistry& registry,
    uint64_t handle,
    void** out_object) {
  return registry.Resolve(handle, out_object, nullptr, nullptr);
}

}  // namespace

engine_native_status_t RegisterEngineHandle(
    engine_native_engine_t* engine,
    engine_native_engine_handle_t* out_handle) {
  return EngineRegistry().GetOrCreate(engine, nullptr, false, out_handle);
}

engine_native_status_t ResolveEngineHandle(
    engine_native_engine_handle_t handle,
    engine_native_engine_t** out_engine) {
  if (out_engine == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  void* object = nullptr;
  const engine_native_status_t status =
      ResolveTypedHandle(EngineRegistry(), handle, &object);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  *out_engine = static_cast<engine_native_engine_t*>(object);
  return ENGINE_NATIVE_STATUS_OK;
}

void UnregisterEngineHandle(engine_native_engine_t* engine) {
  EngineRegistry().RemoveByObject(engine);
}

engine_native_status_t RegisterRendererHandle(
    engine_native_renderer_t* renderer,
    engine_native_engine_t* owner,
    engine_native_renderer_handle_t* out_handle) {
  return RendererRegistry().GetOrCreate(renderer, owner, false, out_handle);
}

engine_native_status_t ResolveRendererHandle(
    engine_native_renderer_handle_t handle,
    engine_native_renderer_t** out_renderer) {
  if (out_renderer == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  void* object = nullptr;
  const engine_native_status_t status =
      ResolveTypedHandle(RendererRegistry(), handle, &object);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  *out_renderer = static_cast<engine_native_renderer_t*>(object);
  return ENGINE_NATIVE_STATUS_OK;
}

void UnregisterRendererHandle(engine_native_renderer_t* renderer) {
  RendererRegistry().RemoveByObject(renderer);
}

engine_native_status_t RegisterPhysicsHandle(
    engine_native_physics_t* physics,
    engine_native_engine_t* owner,
    engine_native_physics_handle_t* out_handle) {
  return PhysicsRegistry().GetOrCreate(physics, owner, false, out_handle);
}

engine_native_status_t ResolvePhysicsHandle(
    engine_native_physics_handle_t handle,
    engine_native_physics_t** out_physics) {
  if (out_physics == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  void* object = nullptr;
  const engine_native_status_t status =
      ResolveTypedHandle(PhysicsRegistry(), handle, &object);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  *out_physics = static_cast<engine_native_physics_t*>(object);
  return ENGINE_NATIVE_STATUS_OK;
}

void UnregisterPhysicsHandle(engine_native_physics_t* physics) {
  PhysicsRegistry().RemoveByObject(physics);
}

engine_native_status_t RegisterAudioHandle(
    engine_native_audio_t* audio,
    engine_native_engine_t* owner,
    engine_native_audio_handle_t* out_handle) {
  return AudioRegistry().GetOrCreate(audio, owner, false, out_handle);
}

engine_native_status_t ResolveAudioHandle(
    engine_native_audio_handle_t handle,
    engine_native_audio_t** out_audio) {
  if (out_audio == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  void* object = nullptr;
  const engine_native_status_t status =
      ResolveTypedHandle(AudioRegistry(), handle, &object);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  *out_audio = static_cast<engine_native_audio_t*>(object);
  return ENGINE_NATIVE_STATUS_OK;
}

void UnregisterAudioHandle(engine_native_audio_t* audio) {
  AudioRegistry().RemoveByObject(audio);
}

engine_native_status_t RegisterNetHandle(
    engine_native_net_t* net,
    engine_native_engine_t* owner,
    bool owns_state,
    engine_native_net_handle_t* out_handle) {
  return NetRegistry().GetOrCreate(net, owner, owns_state, out_handle);
}

engine_native_status_t ResolveNetHandle(
    engine_native_net_handle_t handle,
    engine_native_net_t** out_net,
    engine_native_engine_t** out_owner,
    bool* out_owns_state) {
  if (out_net == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  void* object = nullptr;
  const engine_native_status_t status =
      NetRegistry().Resolve(handle, &object, out_owner, out_owns_state);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  *out_net = static_cast<engine_native_net_t*>(object);
  return ENGINE_NATIVE_STATUS_OK;
}

void UnregisterNetHandle(engine_native_net_t* net) {
  NetRegistry().RemoveByObject(net);
}

void UnregisterOwnedSubsystemHandles(engine_native_engine_t* owner) {
  RendererRegistry().RemoveByOwner(owner);
  PhysicsRegistry().RemoveByOwner(owner);
  AudioRegistry().RemoveByOwner(owner);
  NetRegistry().RemoveByOwner(owner);
}

}  // namespace dff::native::bridge
