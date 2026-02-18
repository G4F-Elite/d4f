#include <assert.h>

#include <cmath>
#include <cstdint>
#include <vector>

#include "bridge_capi/bridge_state.h"
#include "engine_native.h"

namespace {

engine_native_engine_t* CreateEngine() {
  engine_native_create_desc_t create_desc{
      .api_version = ENGINE_NATIVE_API_VERSION,
      .user_data = nullptr};

  engine_native_engine_t* engine = nullptr;
  assert(engine_create(&create_desc, &engine) == ENGINE_NATIVE_STATUS_OK);
  assert(engine != nullptr);
  return engine;
}

template <typename T>
void AppendValue(std::vector<uint8_t>* bytes, const T& value) {
  const auto* raw = reinterpret_cast<const uint8_t*>(&value);
  bytes->insert(bytes->end(), raw, raw + sizeof(T));
}

std::vector<uint8_t> CreateValidSoundBlob() {
  constexpr uint32_t kMagic = 0x424E5344u;      // DSNB
  constexpr uint32_t kVersion = 1u;
  constexpr int32_t kSampleRate = 48000;
  constexpr int32_t kChannels = 1;
  constexpr uint32_t kEncoding = 100u;          // SourceEncoded
  constexpr int32_t kLoopStart = -1;
  constexpr int32_t kLoopEnd = -1;
  constexpr int32_t kPayloadSize = 4;
  const uint8_t payload[4]{1u, 2u, 3u, 4u};

  std::vector<uint8_t> bytes;
  bytes.reserve(4u * 8u + sizeof(payload));
  AppendValue(&bytes, kMagic);
  AppendValue(&bytes, kVersion);
  AppendValue(&bytes, kSampleRate);
  AppendValue(&bytes, kChannels);
  AppendValue(&bytes, kEncoding);
  AppendValue(&bytes, kLoopStart);
  AppendValue(&bytes, kLoopEnd);
  AppendValue(&bytes, kPayloadSize);
  bytes.insert(bytes.end(), payload, payload + sizeof(payload));
  return bytes;
}

void TestEngineGetAudioValidation() {
  engine_native_engine_t* engine = CreateEngine();
  engine_native_audio_t* audio = nullptr;

  assert(engine_get_audio(nullptr, &audio) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(engine_get_audio(engine, nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(engine_get_audio(engine, &audio) == ENGINE_NATIVE_STATUS_OK);
  assert(audio != nullptr);

  assert(engine_destroy(engine) == ENGINE_NATIVE_STATUS_OK);
}

void TestAudioSoundLifecycleAndPlayback() {
  engine_native_engine_t* engine = CreateEngine();
  auto* internal_engine = reinterpret_cast<const engine_native_engine*>(engine);

  engine_native_audio_t* audio = nullptr;
  assert(engine_get_audio(engine, &audio) == ENGINE_NATIVE_STATUS_OK);

  const std::vector<uint8_t> sound_blob = CreateValidSoundBlob();
  uint8_t invalid_sound_blob[8]{0u};
  engine_native_resource_handle_t sound = 0u;

  assert(audio_create_sound_from_blob(nullptr,
                                      sound_blob.data(),
                                      sound_blob.size(),
                                      &sound) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(audio_create_sound_from_blob(audio,
                                      nullptr,
                                      sound_blob.size(),
                                      &sound) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(audio_create_sound_from_blob(audio,
                                      sound_blob.data(),
                                      0u,
                                      &sound) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(audio_create_sound_from_blob(audio,
                                      sound_blob.data(),
                                      sound_blob.size(),
                                      nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(audio_create_sound_from_blob(audio,
                                      invalid_sound_blob,
                                      sizeof(invalid_sound_blob),
                                      &sound) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(audio_create_sound_from_blob(audio,
                                      sound_blob.data(),
                                      sound_blob.size(),
                                      &sound) == ENGINE_NATIVE_STATUS_OK);
  assert(sound != 0u);
  assert(internal_engine->state.audio.sound_count() == 1u);

  engine_native_audio_play_desc_t invalid_play_desc{};
  invalid_play_desc.volume = 1.0f;
  invalid_play_desc.pitch = 1.0f;
  invalid_play_desc.bus = 99u;
  uint64_t emitter_id = 0u;
  assert(audio_play(audio, sound, &invalid_play_desc, &emitter_id) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  engine_native_audio_play_desc_t play_desc{};
  play_desc.volume = 0.85f;
  play_desc.pitch = 1.1f;
  play_desc.bus = ENGINE_NATIVE_AUDIO_BUS_SFX;
  play_desc.loop = 1u;
  play_desc.is_spatialized = 1u;
  play_desc.position[0] = 4.0f;
  play_desc.position[1] = -2.0f;
  play_desc.position[2] = 1.0f;
  play_desc.velocity[0] = 0.1f;
  play_desc.velocity[1] = 0.2f;
  play_desc.velocity[2] = 0.3f;

  assert(audio_play(audio, 0u, &play_desc, &emitter_id) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(audio_play(audio, 0x100000001ULL, &play_desc, &emitter_id) ==
         ENGINE_NATIVE_STATUS_NOT_FOUND);
  assert(audio_play(audio, sound, nullptr, &emitter_id) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(audio_play(audio, sound, &play_desc, nullptr) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(audio_play(audio, sound, &play_desc, &emitter_id) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(emitter_id != 0u);
  assert(internal_engine->state.audio.emitter_count() == 1u);

  const auto* emitter = internal_engine->state.audio.FindEmitter(emitter_id);
  assert(emitter != nullptr);
  assert(emitter->sound == sound);
  assert(emitter->bus == ENGINE_NATIVE_AUDIO_BUS_SFX);
  assert(emitter->loop == 1u);
  assert(std::fabs(emitter->position[0] - 4.0f) < 0.0001f);
  assert(std::fabs(emitter->position[1] + 2.0f) < 0.0001f);
  assert(std::fabs(emitter->position[2] - 1.0f) < 0.0001f);

  assert(engine_destroy(engine) == ENGINE_NATIVE_STATUS_OK);
}

void TestAudioListenerAndEmitterUpdates() {
  engine_native_engine_t* engine = CreateEngine();
  auto* internal_engine = reinterpret_cast<const engine_native_engine*>(engine);

  engine_native_audio_t* audio = nullptr;
  assert(engine_get_audio(engine, &audio) == ENGINE_NATIVE_STATUS_OK);

  const std::vector<uint8_t> sound_blob = CreateValidSoundBlob();
  engine_native_resource_handle_t sound = 0u;
  assert(audio_create_sound_from_blob(audio, sound_blob.data(), sound_blob.size(), &sound) ==
         ENGINE_NATIVE_STATUS_OK);

  engine_native_audio_play_desc_t play_desc{};
  play_desc.volume = 1.0f;
  play_desc.pitch = 1.0f;
  play_desc.bus = ENGINE_NATIVE_AUDIO_BUS_MASTER;
  uint64_t emitter_id = 0u;
  assert(audio_play(audio, sound, &play_desc, &emitter_id) == ENGINE_NATIVE_STATUS_OK);
  assert(emitter_id != 0u);

  engine_native_listener_desc_t invalid_listener{};
  invalid_listener.forward[0] = 0.0f;
  invalid_listener.forward[1] = 0.0f;
  invalid_listener.forward[2] = 0.0f;
  invalid_listener.up[1] = 1.0f;
  assert(audio_set_listener(nullptr, &invalid_listener) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(audio_set_listener(audio, nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(audio_set_listener(audio, &invalid_listener) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  engine_native_listener_desc_t listener{};
  listener.position[0] = 2.0f;
  listener.position[1] = 3.0f;
  listener.position[2] = 4.0f;
  listener.forward[0] = 0.0f;
  listener.forward[1] = 0.0f;
  listener.forward[2] = -1.0f;
  listener.up[0] = 0.0f;
  listener.up[1] = 1.0f;
  listener.up[2] = 0.0f;
  assert(audio_set_listener(audio, &listener) == ENGINE_NATIVE_STATUS_OK);
  assert(std::fabs(internal_engine->state.audio.listener().position[0] - 2.0f) < 0.0001f);
  assert(std::fabs(internal_engine->state.audio.listener().position[1] - 3.0f) < 0.0001f);
  assert(std::fabs(internal_engine->state.audio.listener().position[2] - 4.0f) < 0.0001f);

  engine_native_emitter_params_t invalid_params{};
  invalid_params.volume = 1.0f;
  invalid_params.pitch = 1.0f;
  invalid_params.lowpass = 2.0f;
  invalid_params.reverb_send = 0.1f;
  assert(audio_set_emitter_params(nullptr, emitter_id, &invalid_params) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(audio_set_emitter_params(audio, emitter_id, nullptr) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(audio_set_emitter_params(audio, 0u, &invalid_params) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(audio_set_emitter_params(audio, emitter_id + 1u, &invalid_params) ==
         ENGINE_NATIVE_STATUS_NOT_FOUND);
  assert(audio_set_emitter_params(audio, emitter_id, &invalid_params) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  engine_native_emitter_params_t params{};
  params.volume = 0.25f;
  params.pitch = 1.2f;
  params.position[0] = 10.0f;
  params.position[1] = 5.0f;
  params.position[2] = -2.0f;
  params.velocity[0] = 0.5f;
  params.velocity[1] = 0.0f;
  params.velocity[2] = -0.5f;
  params.lowpass = 0.5f;
  params.reverb_send = 0.3f;
  assert(audio_set_emitter_params(audio, emitter_id, &params) ==
         ENGINE_NATIVE_STATUS_OK);

  const auto* emitter = internal_engine->state.audio.FindEmitter(emitter_id);
  assert(emitter != nullptr);
  assert(std::fabs(emitter->volume - 0.25f) < 0.0001f);
  assert(std::fabs(emitter->pitch - 1.2f) < 0.0001f);
  assert(std::fabs(emitter->position[0] - 10.0f) < 0.0001f);
  assert(std::fabs(emitter->position[1] - 5.0f) < 0.0001f);
  assert(std::fabs(emitter->position[2] + 2.0f) < 0.0001f);
  assert(std::fabs(emitter->lowpass - 0.5f) < 0.0001f);
  assert(std::fabs(emitter->reverb_send - 0.3f) < 0.0001f);

  assert(engine_destroy(engine) == ENGINE_NATIVE_STATUS_OK);
}

}  // namespace

int main() {
  TestEngineGetAudioValidation();
  TestAudioSoundLifecycleAndPlayback();
  TestAudioListenerAndEmitterUpdates();
  return 0;
}
