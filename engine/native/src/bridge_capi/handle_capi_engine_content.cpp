#include "bridge_capi/handle_registry.h"

#include <cstring>

namespace {

engine_native_status_t ResolveEngine(
    engine_native_engine_handle_t handle,
    engine_native_engine_t** out_engine) {
  return dff::native::bridge::ResolveEngineHandle(handle, out_engine);
}

}  // namespace

extern "C" {

engine_native_status_t engine_create_handle(
    const engine_native_create_desc_t* create_desc,
    engine_native_engine_handle_t* out_engine) {
  if (out_engine == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_engine = ENGINE_NATIVE_INVALID_HANDLE;
  engine_native_engine_t* engine = nullptr;
  const engine_native_status_t status = engine_create(create_desc, &engine);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  const engine_native_status_t register_status =
      dff::native::bridge::RegisterEngineHandle(engine, out_engine);
  if (register_status != ENGINE_NATIVE_STATUS_OK) {
    static_cast<void>(engine_destroy(engine));
  }

  return register_status;
}

engine_native_status_t engine_destroy_handle(engine_native_engine_handle_t engine) {
  engine_native_engine_t* raw_engine = nullptr;
  const engine_native_status_t resolve_status =
      ResolveEngine(engine, &raw_engine);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  dff::native::bridge::UnregisterOwnedSubsystemHandles(raw_engine);
  dff::native::bridge::UnregisterEngineHandle(raw_engine);
  return engine_destroy(raw_engine);
}

engine_native_status_t engine_pump_events_handle(
    engine_native_engine_handle_t engine,
    engine_native_input_snapshot_t* out_input,
    engine_native_window_events_t* out_events) {
  engine_native_engine_t* raw_engine = nullptr;
  const engine_native_status_t resolve_status =
      ResolveEngine(engine, &raw_engine);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return engine_pump_events(raw_engine, out_input, out_events);
}

engine_native_status_t engine_get_renderer_handle(
    engine_native_engine_handle_t engine,
    engine_native_renderer_handle_t* out_renderer) {
  if (out_renderer == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_renderer = ENGINE_NATIVE_INVALID_HANDLE;
  engine_native_engine_t* raw_engine = nullptr;
  const engine_native_status_t resolve_status =
      ResolveEngine(engine, &raw_engine);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  engine_native_renderer_t* renderer = nullptr;
  const engine_native_status_t status = engine_get_renderer(raw_engine, &renderer);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return dff::native::bridge::RegisterRendererHandle(
      renderer, raw_engine, out_renderer);
}

engine_native_status_t engine_get_physics_handle(
    engine_native_engine_handle_t engine,
    engine_native_physics_handle_t* out_physics) {
  if (out_physics == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_physics = ENGINE_NATIVE_INVALID_HANDLE;
  engine_native_engine_t* raw_engine = nullptr;
  const engine_native_status_t resolve_status =
      ResolveEngine(engine, &raw_engine);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  engine_native_physics_t* physics = nullptr;
  const engine_native_status_t status = engine_get_physics(raw_engine, &physics);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return dff::native::bridge::RegisterPhysicsHandle(
      physics, raw_engine, out_physics);
}

engine_native_status_t engine_get_audio_handle(
    engine_native_engine_handle_t engine,
    engine_native_audio_handle_t* out_audio) {
  if (out_audio == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_audio = ENGINE_NATIVE_INVALID_HANDLE;
  engine_native_engine_t* raw_engine = nullptr;
  const engine_native_status_t resolve_status =
      ResolveEngine(engine, &raw_engine);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  engine_native_audio_t* audio = nullptr;
  const engine_native_status_t status = engine_get_audio(raw_engine, &audio);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return dff::native::bridge::RegisterAudioHandle(audio, raw_engine, out_audio);
}

engine_native_status_t engine_get_net_handle(
    engine_native_engine_handle_t engine,
    engine_native_net_handle_t* out_net) {
  if (out_net == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_net = ENGINE_NATIVE_INVALID_HANDLE;
  engine_native_engine_t* raw_engine = nullptr;
  const engine_native_status_t resolve_status =
      ResolveEngine(engine, &raw_engine);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  engine_native_net_t* net = nullptr;
  const engine_native_status_t status = engine_get_net(raw_engine, &net);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return dff::native::bridge::RegisterNetHandle(net, raw_engine, false, out_net);
}

engine_native_status_t content_mount_pak_handle(
    engine_native_engine_handle_t engine,
    const char* pak_path) {
  engine_native_engine_t* raw_engine = nullptr;
  const engine_native_status_t resolve_status =
      ResolveEngine(engine, &raw_engine);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  const engine_native_string_view_t pak_path_view{
      .data = pak_path,
      .length = pak_path == nullptr ? 0u : std::strlen(pak_path),
  };
  return content_mount_pak_view(raw_engine, pak_path_view);
}

engine_native_status_t content_mount_directory_handle(
    engine_native_engine_handle_t engine,
    const char* directory_path) {
  engine_native_engine_t* raw_engine = nullptr;
  const engine_native_status_t resolve_status =
      ResolveEngine(engine, &raw_engine);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  const engine_native_string_view_t directory_path_view{
      .data = directory_path,
      .length = directory_path == nullptr ? 0u : std::strlen(directory_path),
  };
  return content_mount_directory_view(raw_engine, directory_path_view);
}

engine_native_status_t content_read_file_handle(
    engine_native_engine_handle_t engine,
    const char* asset_path,
    void* buffer,
    size_t buffer_size,
    size_t* out_size) {
  engine_native_engine_t* raw_engine = nullptr;
  const engine_native_status_t resolve_status =
      ResolveEngine(engine, &raw_engine);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  const engine_native_string_view_t asset_path_view{
      .data = asset_path,
      .length = asset_path == nullptr ? 0u : std::strlen(asset_path),
  };
  return content_read_file_view(raw_engine, asset_path_view, buffer, buffer_size,
                                out_size);
}

engine_native_status_t content_mount_pak_view_handle(
    engine_native_engine_handle_t engine,
    engine_native_string_view_t pak_path) {
  engine_native_engine_t* raw_engine = nullptr;
  const engine_native_status_t resolve_status =
      ResolveEngine(engine, &raw_engine);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return content_mount_pak_view(raw_engine, pak_path);
}

engine_native_status_t content_mount_directory_view_handle(
    engine_native_engine_handle_t engine,
    engine_native_string_view_t directory_path) {
  engine_native_engine_t* raw_engine = nullptr;
  const engine_native_status_t resolve_status =
      ResolveEngine(engine, &raw_engine);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return content_mount_directory_view(raw_engine, directory_path);
}

engine_native_status_t content_read_file_view_handle(
    engine_native_engine_handle_t engine,
    engine_native_string_view_t asset_path,
    void* buffer,
    size_t buffer_size,
    size_t* out_size) {
  engine_native_engine_t* raw_engine = nullptr;
  const engine_native_status_t resolve_status =
      ResolveEngine(engine, &raw_engine);
  if (resolve_status != ENGINE_NATIVE_STATUS_OK) {
    return resolve_status;
  }

  return content_read_file_view(raw_engine, asset_path, buffer, buffer_size,
                                out_size);
}

}  // extern "C"
