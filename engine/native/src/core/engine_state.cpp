#include "core/engine_state.h"

#include <algorithm>
#include <cstdint>
#include <limits>
#include <new>

namespace dff::native {

bool RendererState::IsPowerOfTwo(size_t value) {
  return value != 0u && (value & (value - 1u)) == 0u;
}

engine_native_status_t RendererState::BeginFrame(size_t requested_bytes,
                                                 size_t alignment,
                                                 void** out_frame_memory) {
  if (out_frame_memory == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_frame_memory = nullptr;

  if (frame_open_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  if (requested_bytes == 0u || !IsPowerOfTwo(alignment)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (requested_bytes >
      std::numeric_limits<size_t>::max() - (alignment - static_cast<size_t>(1u))) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const size_t storage_size = requested_bytes + (alignment - static_cast<size_t>(1u));

  try {
    frame_storage_.assign(storage_size, static_cast<uint8_t>(0u));
  } catch (const std::bad_alloc&) {
    return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
  }

  if (frame_storage_.empty()) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }

  uintptr_t base = reinterpret_cast<uintptr_t>(frame_storage_.data());
  const uintptr_t aligned =
      (base + static_cast<uintptr_t>(alignment - static_cast<size_t>(1u))) &
      ~static_cast<uintptr_t>(alignment - static_cast<size_t>(1u));

  frame_memory_ = reinterpret_cast<void*>(aligned);
  frame_capacity_ = requested_bytes;
  submitted_draw_count_ = 0u;
  submitted_ui_count_ = 0u;
  frame_open_ = true;
  *out_frame_memory = frame_memory_;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RendererState::Submit(
    const engine_native_render_packet_t& packet) {
  if (!frame_open_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  if (packet.draw_item_count > 0u && packet.draw_items == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (packet.ui_item_count > 0u && packet.ui_items == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const size_t draw_bytes =
      static_cast<size_t>(packet.draw_item_count) *
      static_cast<size_t>(sizeof(engine_native_draw_item_t));
  const size_t ui_bytes =
      static_cast<size_t>(packet.ui_item_count) *
      static_cast<size_t>(sizeof(engine_native_ui_draw_item_t));

  if (draw_bytes > frame_capacity_ || ui_bytes > frame_capacity_ ||
      draw_bytes > std::numeric_limits<size_t>::max() - ui_bytes ||
      draw_bytes + ui_bytes > frame_capacity_) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  submitted_draw_count_ += packet.draw_item_count;
  submitted_ui_count_ += packet.ui_item_count;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RendererState::Present() {
  if (!frame_open_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  frame_memory_ = nullptr;
  frame_capacity_ = 0u;
  submitted_draw_count_ = 0u;
  submitted_ui_count_ = 0u;
  frame_open_ = false;
  frame_storage_.clear();
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t PhysicsState::Step(double dt_seconds) {
  if (dt_seconds <= 0.0) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (!synced_from_world_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  ++step_count_;
  stepped_since_sync_ = true;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t PhysicsState::SyncFromWorld(
    const engine_native_body_write_t* writes,
    uint32_t write_count) {
  if (write_count > 0u && writes == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (synced_from_world_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  for (uint32_t i = 0u; i < write_count; ++i) {
    const engine_native_body_write_t& write = writes[i];
    if (write.body == 0u) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    PhysicsBodyState state;
    std::copy_n(write.position, 3, state.position.data());
    std::copy_n(write.rotation, 4, state.rotation.data());
    std::copy_n(write.linear_velocity, 3, state.linear_velocity.data());
    std::copy_n(write.angular_velocity, 3, state.angular_velocity.data());
    bodies_[write.body] = state;
  }

  synced_from_world_ = true;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t PhysicsState::SyncToWorld(engine_native_body_read_t* reads,
                                                 uint32_t read_capacity,
                                                 uint32_t* out_read_count) {
  if (out_read_count == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_read_count = 0u;

  if (read_capacity > 0u && reads == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (!synced_from_world_ || !stepped_since_sync_) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  uint32_t written = 0u;
  for (const auto& body_pair : bodies_) {
    if (written >= read_capacity) {
      break;
    }

    engine_native_body_read_t& read = reads[written];
    read.body = body_pair.first;
    std::copy_n(body_pair.second.position.data(), 3, read.position);
    std::copy_n(body_pair.second.rotation.data(), 4, read.rotation);
    std::copy_n(body_pair.second.linear_velocity.data(), 3, read.linear_velocity);
    std::copy_n(body_pair.second.angular_velocity.data(), 3, read.angular_velocity);
    read.is_active = 1u;
    ++written;
  }

  *out_read_count = written;
  synced_from_world_ = false;
  stepped_since_sync_ = false;
  return ENGINE_NATIVE_STATUS_OK;
}

}  // namespace dff::native
