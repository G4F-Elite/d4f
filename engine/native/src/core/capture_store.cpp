#include "core/capture_store.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
#include <new>
#include <utility>

namespace dff::native {
namespace {

bool TryMultiplySize(size_t lhs, size_t rhs, size_t* out_value) {
  if (out_value == nullptr) {
    return false;
  }
  if (lhs == 0u || rhs == 0u) {
    *out_value = 0u;
    return true;
  }
  if (lhs > std::numeric_limits<size_t>::max() / rhs) {
    return false;
  }

  *out_value = lhs * rhs;
  return true;
}

uint8_t EncodeColor(float value) {
  const float clamped = std::clamp(value, 0.0f, 1.0f);
  const int rounded = static_cast<int>(std::lround(clamped * 255.0f));
  return static_cast<uint8_t>(std::clamp(rounded, 0, 255));
}

}  // namespace

engine_native_status_t CaptureStore::QueueCapture(
    const engine_native_capture_request_t& request,
    const std::array<float, 4>& clear_color,
    uint64_t frame_index,
    uint64_t* out_request_id) {
  if (out_request_id == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_request_id = 0u;

  if (request.width == 0u || request.height == 0u || request.include_alpha > 1u) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const size_t width = static_cast<size_t>(request.width);
  const size_t height = static_cast<size_t>(request.height);
  size_t stride = 0u;
  size_t pixel_bytes = 0u;

  if (!TryMultiplySize(width, 4u, &stride) ||
      !TryMultiplySize(stride, height, &pixel_bytes)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  PendingCapture pending;
  pending.width = request.width;
  pending.height = request.height;
  pending.stride = static_cast<uint32_t>(stride);

  try {
    pending.pixels.assign(pixel_bytes, static_cast<uint8_t>(0u));
  } catch (const std::bad_alloc&) {
    return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
  }

  const uint8_t clear_r = EncodeColor(clear_color[0]);
  const uint8_t clear_g = EncodeColor(clear_color[1]);
  const uint8_t clear_b = EncodeColor(clear_color[2]);
  const uint8_t clear_a = request.include_alpha == 0u
                              ? static_cast<uint8_t>(255u)
                              : EncodeColor(clear_color[3]);

  for (uint32_t y = 0u; y < request.height; ++y) {
    for (uint32_t x = 0u; x < request.width; ++x) {
      const bool checker = (((x / 8u) + (y / 8u) + (frame_index % 2u)) % 2u) == 0u;
      const float tint = checker ? 1.0f : 0.92f;
      const size_t offset = static_cast<size_t>(y) * stride +
                            static_cast<size_t>(x) * 4u;
      pending.pixels[offset] = EncodeColor(static_cast<float>(clear_r) / 255.0f * tint);
      pending.pixels[offset + 1u] =
          EncodeColor(static_cast<float>(clear_g) / 255.0f * tint);
      pending.pixels[offset + 2u] =
          EncodeColor(static_cast<float>(clear_b) / 255.0f * tint);
      pending.pixels[offset + 3u] = clear_a;
    }
  }

  std::lock_guard<std::mutex> guard(mutex_);
  uint64_t request_id = next_request_id_;
  while (request_id == 0u || pending_.contains(request_id)) {
    ++request_id;
  }

  next_request_id_ = request_id + 1u;
  pending_.emplace(request_id, std::move(pending));
  *out_request_id = request_id;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t CaptureStore::PollCapture(
    uint64_t request_id,
    engine_native_capture_result_t* out_result,
    uint8_t* out_is_ready) {
  if (request_id == 0u || out_result == nullptr || out_is_ready == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_result = engine_native_capture_result_t{};
  *out_is_ready = 0u;

  PendingCapture pending;
  {
    std::lock_guard<std::mutex> guard(mutex_);
    auto it = pending_.find(request_id);
    if (it == pending_.end()) {
      return ENGINE_NATIVE_STATUS_NOT_FOUND;
    }

    pending = std::move(it->second);
    pending_.erase(it);
  }

  const size_t pixel_bytes = pending.pixels.size();
  uint8_t* pixel_copy = nullptr;
  if (pixel_bytes > 0u) {
    pixel_copy = new (std::nothrow) uint8_t[pixel_bytes];
    if (pixel_copy == nullptr) {
      return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
    }

    std::memcpy(pixel_copy, pending.pixels.data(), pixel_bytes);
  }

  out_result->width = pending.width;
  out_result->height = pending.height;
  out_result->stride = pending.stride;
  out_result->format = static_cast<uint32_t>(ENGINE_NATIVE_CAPTURE_FORMAT_RGBA8_UNORM);
  out_result->pixels = pixel_copy;
  out_result->pixel_bytes = pixel_bytes;
  *out_is_ready = 1u;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t CaptureStore::FreeCaptureResult(
    engine_native_capture_result_t* result) {
  if (result == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (result->pixels != nullptr) {
    delete[] const_cast<uint8_t*>(result->pixels);
  }

  *result = engine_native_capture_result_t{};
  return ENGINE_NATIVE_STATUS_OK;
}

void CaptureStore::Reset() {
  std::lock_guard<std::mutex> guard(mutex_);
  pending_.clear();
}

CaptureStore& GetCaptureStore() {
  static CaptureStore store;
  return store;
}

}  // namespace dff::native
