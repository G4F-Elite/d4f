#include "core/capture_store.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
#include <new>
#include <utility>

namespace dff::native {
namespace {

constexpr uint8_t kCaptureSemanticColor = 0u;
constexpr uint8_t kCaptureSemanticDepth = 1u;
constexpr uint8_t kCaptureSemanticNormals = 2u;
constexpr uint8_t kCaptureSemanticAlbedo = 3u;
constexpr uint8_t kCaptureSemanticShadow = 4u;
constexpr uint8_t kCaptureSemanticAmbientOcclusion = 5u;

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

float EncodeUnit(uint32_t value, uint32_t denominator) {
  if (denominator == 0u) {
    return 0.0f;
  }

  return static_cast<float>(value) / static_cast<float>(denominator);
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

  if (request.width == 0u || request.height == 0u || request.include_alpha > 1u ||
      request.reserved1 != 0u || request.reserved2 != 0u ||
      request.reserved0 > kCaptureSemanticAmbientOcclusion) {
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
  pending.polls_until_ready = 1u;

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
  const uint8_t semantic = request.reserved0;
  const uint32_t width_denominator = request.width > 1u ? request.width - 1u : 1u;
  const uint32_t height_denominator = request.height > 1u ? request.height - 1u : 1u;

  for (uint32_t y = 0u; y < request.height; ++y) {
    for (uint32_t x = 0u; x < request.width; ++x) {
      const size_t offset = static_cast<size_t>(y) * stride +
                            static_cast<size_t>(x) * 4u;

      uint8_t out_r = clear_r;
      uint8_t out_g = clear_g;
      uint8_t out_b = clear_b;

      switch (semantic) {
        case kCaptureSemanticDepth: {
          const float depth = 1.0f - EncodeUnit(y, height_denominator);
          const uint8_t depth_u8 = EncodeColor(depth);
          out_r = depth_u8;
          out_g = depth_u8;
          out_b = depth_u8;
          break;
        }
        case kCaptureSemanticNormals: {
          const float nx = EncodeUnit(x, width_denominator) * 2.0f - 1.0f;
          const float ny = EncodeUnit(y, height_denominator) * 2.0f - 1.0f;
          const float nz = std::sqrt(std::max(0.0f, 1.0f -
              std::min(1.0f, nx * nx + ny * ny)));
          out_r = EncodeColor(nx * 0.5f + 0.5f);
          out_g = EncodeColor((-ny) * 0.5f + 0.5f);
          out_b = EncodeColor(nz);
          break;
        }
        case kCaptureSemanticAlbedo: {
          const float warm = 0.55f + EncodeUnit(x, width_denominator) * 0.35f;
          const float mid = 0.40f + EncodeUnit(y, height_denominator) * 0.40f;
          const float cool = 0.30f +
              (EncodeUnit(x, width_denominator) + EncodeUnit(y, height_denominator)) *
                  0.175f;
          out_r = EncodeColor(warm);
          out_g = EncodeColor(mid);
          out_b = EncodeColor(cool);
          break;
        }
        case kCaptureSemanticShadow:
        case kCaptureSemanticAmbientOcclusion: {
          const bool checker = (((x / 6u) + (y / 6u) + (frame_index % 2u)) % 2u) == 0u;
          const float lit = checker ? 0.62f : 0.18f;
          const float horizon = EncodeUnit(y, height_denominator) * 0.24f;
          const uint8_t light = EncodeColor(std::max(0.0f, lit - horizon));
          out_r = light;
          out_g = light;
          out_b = light;
          break;
        }
        case kCaptureSemanticColor:
        default: {
          const bool checker = (((x / 8u) + (y / 8u) + (frame_index % 2u)) % 2u) == 0u;
          const float tint = checker ? 1.0f : 0.92f;
          out_r = EncodeColor(static_cast<float>(clear_r) / 255.0f * tint);
          out_g = EncodeColor(static_cast<float>(clear_g) / 255.0f * tint);
          out_b = EncodeColor(static_cast<float>(clear_b) / 255.0f * tint);
          break;
        }
      }

      pending.pixels[offset] = out_r;
      pending.pixels[offset + 1u] = out_g;
      pending.pixels[offset + 2u] = out_b;
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

    if (it->second.polls_until_ready > 0u) {
      --it->second.polls_until_ready;
      return ENGINE_NATIVE_STATUS_OK;
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
