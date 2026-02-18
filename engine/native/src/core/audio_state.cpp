#include "core/engine_state.h"

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <limits>
#include <new>

namespace dff::native {

namespace {

constexpr uint32_t kSoundBlobMagic = 0x424E5344u;  // DSNB
constexpr uint32_t kSoundBlobVersion = 1u;

bool IsDirectionValid(const std::array<float, 3>& direction) {
  constexpr float kEpsilon = 1e-6f;
  const float sqr_length = direction[0] * direction[0] +
                           direction[1] * direction[1] +
                           direction[2] * direction[2];
  return std::isfinite(sqr_length) && sqr_length > kEpsilon;
}

bool IsValidSoundBlob(const void* data, size_t size) {
  if (data == nullptr || size < sizeof(uint32_t) * 2u) {
    return false;
  }

  uint32_t magic = 0u;
  uint32_t version = 0u;
  std::memcpy(&magic, data, sizeof(uint32_t));
  std::memcpy(&version, static_cast<const uint8_t*>(data) + sizeof(uint32_t),
              sizeof(uint32_t));
  return magic == kSoundBlobMagic && version == kSoundBlobVersion;
}

}  // namespace

bool AudioState::IsSupportedBus(uint8_t bus) {
  return bus == ENGINE_NATIVE_AUDIO_BUS_MASTER ||
         bus == ENGINE_NATIVE_AUDIO_BUS_MUSIC ||
         bus == ENGINE_NATIVE_AUDIO_BUS_SFX ||
         bus == ENGINE_NATIVE_AUDIO_BUS_AMBIENCE;
}

bool AudioState::IsFiniteScalar(float value) {
  return std::isfinite(value);
}

bool AudioState::IsFiniteVector(const float* values, size_t count) {
  if (values == nullptr) {
    return false;
  }

  for (size_t i = 0u; i < count; ++i) {
    if (!std::isfinite(values[i])) {
      return false;
    }
  }

  return true;
}

bool AudioState::IsValidNormalizedValue(float value) {
  return std::isfinite(value) && value >= 0.0f && value <= 1.0f;
}

engine_native_status_t AudioState::CreateSoundFromBlob(
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_sound) {
  if (out_sound == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_sound = kInvalidResourceHandle;
  if (data == nullptr || size == 0u) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (!IsValidSoundBlob(data, size)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  AudioSoundResource resource;
  const auto* bytes = static_cast<const uint8_t*>(data);
  try {
    resource.bytes.assign(bytes, bytes + size);
  } catch (const std::bad_alloc&) {
    return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
  }

  ResourceHandle sound_handle{};
  const engine_native_status_t insert_status =
      sounds_.Insert(std::move(resource), &sound_handle);
  if (insert_status != ENGINE_NATIVE_STATUS_OK) {
    return insert_status;
  }

  *out_sound = EncodeResourceHandle(sound_handle);
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t AudioState::Play(
    engine_native_resource_handle_t sound,
    const engine_native_audio_play_desc_t& play_desc,
    uint64_t* out_emitter_id) {
  if (out_emitter_id == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_emitter_id = 0u;
  if (sound == kInvalidResourceHandle) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (sounds_.Get(DecodeResourceHandle(sound)) == nullptr) {
    return ENGINE_NATIVE_STATUS_NOT_FOUND;
  }

  if (!IsSupportedBus(play_desc.bus) || play_desc.loop > 1u ||
      play_desc.is_spatialized > 1u || !IsFiniteScalar(play_desc.volume) ||
      play_desc.volume < 0.0f || !IsFiniteScalar(play_desc.pitch) ||
      play_desc.pitch <= 0.0f || !IsFiniteVector(play_desc.position, 3u) ||
      !IsFiniteVector(play_desc.velocity, 3u)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (next_emitter_id_ == std::numeric_limits<uint64_t>::max()) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }

  const uint64_t emitter_id = next_emitter_id_++;
  AudioEmitterState emitter;
  emitter.sound = sound;
  emitter.volume = play_desc.volume;
  emitter.pitch = play_desc.pitch;
  emitter.bus = play_desc.bus;
  emitter.loop = play_desc.loop;
  emitter.is_spatialized = play_desc.is_spatialized;
  std::copy_n(play_desc.position, 3u, emitter.position.data());
  std::copy_n(play_desc.velocity, 3u, emitter.velocity.data());

  try {
    emitters_.emplace(emitter_id, emitter);
  } catch (const std::bad_alloc&) {
    return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
  }

  *out_emitter_id = emitter_id;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t AudioState::SetListener(
    const engine_native_listener_desc_t& listener_desc) {
  if (!IsFiniteVector(listener_desc.position, 3u) ||
      !IsFiniteVector(listener_desc.forward, 3u) ||
      !IsFiniteVector(listener_desc.up, 3u)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  AudioListenerState next_listener;
  std::copy_n(listener_desc.position, 3u, next_listener.position.data());
  std::copy_n(listener_desc.forward, 3u, next_listener.forward.data());
  std::copy_n(listener_desc.up, 3u, next_listener.up.data());
  if (!IsDirectionValid(next_listener.forward) ||
      !IsDirectionValid(next_listener.up)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  listener_ = next_listener;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t AudioState::SetEmitterParams(
    uint64_t emitter_id,
    const engine_native_emitter_params_t& params) {
  if (emitter_id == 0u) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  auto emitter_it = emitters_.find(emitter_id);
  if (emitter_it == emitters_.end()) {
    return ENGINE_NATIVE_STATUS_NOT_FOUND;
  }

  if (!IsFiniteScalar(params.volume) || params.volume < 0.0f ||
      !IsFiniteScalar(params.pitch) || params.pitch <= 0.0f ||
      !IsFiniteVector(params.position, 3u) ||
      !IsFiniteVector(params.velocity, 3u) ||
      !IsValidNormalizedValue(params.lowpass) ||
      !IsValidNormalizedValue(params.reverb_send)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  AudioEmitterState& emitter = emitter_it->second;
  emitter.volume = params.volume;
  emitter.pitch = params.pitch;
  std::copy_n(params.position, 3u, emitter.position.data());
  std::copy_n(params.velocity, 3u, emitter.velocity.data());
  emitter.lowpass = params.lowpass;
  emitter.reverb_send = params.reverb_send;
  return ENGINE_NATIVE_STATUS_OK;
}

}  // namespace dff::native
