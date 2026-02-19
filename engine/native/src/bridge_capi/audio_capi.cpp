#include "bridge_capi/bridge_state.h"

namespace {

engine_native_status_t ValidateAudio(engine_native_audio_t* audio) {
  if (audio == nullptr || audio->state == nullptr || audio->owner == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (audio != &audio->owner->audio) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  return ENGINE_NATIVE_STATUS_OK;
}

}  // namespace

extern "C" {

engine_native_status_t audio_create_sound_from_blob(
    engine_native_audio_t* audio,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_sound) {
  const engine_native_status_t status = ValidateAudio(audio);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return audio->state->CreateSoundFromBlob(data, size, out_sound);
}

engine_native_status_t audio_play(engine_native_audio_t* audio,
                                  engine_native_resource_handle_t sound,
                                  const engine_native_audio_play_desc_t* play_desc,
                                  uint64_t* out_emitter_id) {
  const engine_native_status_t status = ValidateAudio(audio);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }
  if (play_desc == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return audio->state->Play(sound, *play_desc, out_emitter_id);
}

engine_native_status_t audio_set_listener(
    engine_native_audio_t* audio,
    const engine_native_listener_desc_t* listener_desc) {
  const engine_native_status_t status = ValidateAudio(audio);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }
  if (listener_desc == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return audio->state->SetListener(*listener_desc);
}

engine_native_status_t audio_set_emitter_params(
    engine_native_audio_t* audio,
    uint64_t emitter_id,
    const engine_native_emitter_params_t* params) {
  const engine_native_status_t status = ValidateAudio(audio);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }
  if (params == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return audio->state->SetEmitterParams(emitter_id, *params);
}

engine_native_status_t audio_set_bus_params(
    engine_native_audio_t* audio,
    const engine_native_audio_bus_params_t* params) {
  const engine_native_status_t status = ValidateAudio(audio);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }
  if (params == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return audio->state->SetBusParams(*params);
}

}  // extern "C"
