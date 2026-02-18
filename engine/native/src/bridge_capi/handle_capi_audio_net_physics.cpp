#include "bridge_capi/handle_registry.h"

namespace {

engine_native_status_t ResolveAudio(
    engine_native_audio_handle_t handle,
    engine_native_audio_t** out_audio) {
  return dff::native::bridge::ResolveAudioHandle(handle, out_audio);
}

engine_native_status_t ResolveNet(
    engine_native_net_handle_t handle,
    engine_native_net_t** out_net,
    engine_native_engine_t** out_owner,
    bool* out_owns_state) {
  return dff::native::bridge::ResolveNetHandle(
      handle, out_net, out_owner, out_owns_state);
}

engine_native_status_t ResolvePhysics(
    engine_native_physics_handle_t handle,
    engine_native_physics_t** out_physics) {
  return dff::native::bridge::ResolvePhysicsHandle(handle, out_physics);
}

}  // namespace

extern "C" {

engine_native_status_t audio_create_sound_from_blob_handle(
    engine_native_audio_handle_t audio,
    const void* data,
    size_t size,
    engine_native_resource_handle_t* out_sound) {
  engine_native_audio_t* raw_audio = nullptr;
  const engine_native_status_t resolve_status = ResolveAudio(audio, &raw_audio);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return audio_create_sound_from_blob(raw_audio, data, size, out_sound);
}

engine_native_status_t audio_play_handle(
    engine_native_audio_handle_t audio,
    engine_native_resource_handle_t sound,
    const engine_native_audio_play_desc_t* play_desc,
    uint64_t* out_emitter_id) {
  engine_native_audio_t* raw_audio = nullptr;
  const engine_native_status_t resolve_status = ResolveAudio(audio, &raw_audio);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return audio_play(raw_audio, sound, play_desc, out_emitter_id);
}

engine_native_status_t audio_set_listener_handle(
    engine_native_audio_handle_t audio,
    const engine_native_listener_desc_t* listener_desc) {
  engine_native_audio_t* raw_audio = nullptr;
  const engine_native_status_t resolve_status = ResolveAudio(audio, &raw_audio);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return audio_set_listener(raw_audio, listener_desc);
}

engine_native_status_t audio_set_emitter_params_handle(
    engine_native_audio_handle_t audio,
    uint64_t emitter_id,
    const engine_native_emitter_params_t* params) {
  engine_native_audio_t* raw_audio = nullptr;
  const engine_native_status_t resolve_status = ResolveAudio(audio, &raw_audio);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return audio_set_emitter_params(raw_audio, emitter_id, params);
}

engine_native_status_t net_create_handle(
    const engine_native_net_desc_t* desc,
    engine_native_net_handle_t* out_net) {
  if (out_net == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_net = ENGINE_NATIVE_INVALID_HANDLE;
  engine_native_net_t* net = nullptr;
  const engine_native_status_t status = net_create(desc, &net);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  const engine_native_status_t register_status =
      dff::native::bridge::RegisterNetHandle(net, nullptr, true, out_net);
  if (register_status != ENGINE_NATIVE_STATUS_OK) {
    static_cast<void>(net_destroy(net));
  }

  return register_status;
}

engine_native_status_t net_destroy_handle(engine_native_net_handle_t net) {
  engine_native_net_t* raw_net = nullptr;
  engine_native_engine_t* owner = nullptr;
  bool owns_state = false;
  const engine_native_status_t resolve_status =
      ResolveNet(net, &raw_net, &owner, &owns_state);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  if (owner != nullptr || !owns_state) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  dff::native::bridge::UnregisterNetHandle(raw_net);
  return net_destroy(raw_net);
}

engine_native_status_t net_pump_handle(
    engine_native_net_handle_t net,
    engine_native_net_events_t* out_events) {
  engine_native_net_t* raw_net = nullptr;
  const engine_native_status_t resolve_status =
      ResolveNet(net, &raw_net, nullptr, nullptr);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return net_pump(raw_net, out_events);
}

engine_native_status_t net_send_handle(
    engine_native_net_handle_t net,
    const engine_native_net_send_desc_t* send_desc) {
  engine_native_net_t* raw_net = nullptr;
  const engine_native_status_t resolve_status =
      ResolveNet(net, &raw_net, nullptr, nullptr);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return net_send(raw_net, send_desc);
}

engine_native_status_t physics_step_handle(
    engine_native_physics_handle_t physics,
    double dt_seconds) {
  engine_native_physics_t* raw_physics = nullptr;
  const engine_native_status_t resolve_status =
      ResolvePhysics(physics, &raw_physics);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return physics_step(raw_physics, dt_seconds);
}

engine_native_status_t physics_sync_from_world_handle(
    engine_native_physics_handle_t physics,
    const engine_native_body_write_t* writes,
    uint32_t write_count) {
  engine_native_physics_t* raw_physics = nullptr;
  const engine_native_status_t resolve_status =
      ResolvePhysics(physics, &raw_physics);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return physics_sync_from_world(raw_physics, writes, write_count);
}

engine_native_status_t physics_sync_to_world_handle(
    engine_native_physics_handle_t physics,
    engine_native_body_read_t* reads,
    uint32_t read_capacity,
    uint32_t* out_read_count) {
  engine_native_physics_t* raw_physics = nullptr;
  const engine_native_status_t resolve_status =
      ResolvePhysics(physics, &raw_physics);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return physics_sync_to_world(raw_physics, reads, read_capacity, out_read_count);
}

engine_native_status_t physics_raycast_handle(
    engine_native_physics_handle_t physics,
    const engine_native_raycast_query_t* query,
    engine_native_raycast_hit_t* out_hit) {
  engine_native_physics_t* raw_physics = nullptr;
  const engine_native_status_t resolve_status =
      ResolvePhysics(physics, &raw_physics);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return physics_raycast(raw_physics, query, out_hit);
}

engine_native_status_t physics_sweep_handle(
    engine_native_physics_handle_t physics,
    const engine_native_sweep_query_t* query,
    engine_native_sweep_hit_t* out_hit) {
  engine_native_physics_t* raw_physics = nullptr;
  const engine_native_status_t resolve_status =
      ResolvePhysics(physics, &raw_physics);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return physics_sweep(raw_physics, query, out_hit);
}

engine_native_status_t physics_overlap_handle(
    engine_native_physics_handle_t physics,
    const engine_native_overlap_query_t* query,
    engine_native_overlap_hit_t* hits,
    uint32_t hit_capacity,
    uint32_t* out_hit_count) {
  engine_native_physics_t* raw_physics = nullptr;
  const engine_native_status_t resolve_status =
      ResolvePhysics(physics, &raw_physics);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return physics_overlap(raw_physics, query, hits, hit_capacity, out_hit_count);
}

}  // extern "C"
