#include "bridge_capi/bridge_state.h"

#include <cstring>
#include <string>

namespace {

engine_native_status_t CopyStringFromView(engine_native_string_view_t view,
                                          std::string* out_value) {
  if (out_value == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (view.data == nullptr) {
    if (view.length != 0u) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    out_value->clear();
    return ENGINE_NATIVE_STATUS_OK;
  }

  out_value->assign(view.data, view.length);
  if (out_value->find('\0') != std::string::npos) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t CopyStringFromCstr(const char* value, std::string* out_value) {
  if (value == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const engine_native_string_view_t view{
      .data = value,
      .length = std::strlen(value),
  };
  return CopyStringFromView(view, out_value);
}

}  // namespace

extern "C" {

engine_native_status_t content_mount_pak(engine_native_engine_t* engine,
                                         const char* pak_path) {
  if (engine == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  std::string pak_path_value;
  const engine_native_status_t status =
      CopyStringFromCstr(pak_path, &pak_path_value);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return engine->state.content.MountPak(pak_path_value);
}

engine_native_status_t content_mount_directory(engine_native_engine_t* engine,
                                               const char* directory_path) {
  if (engine == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  std::string directory_path_value;
  const engine_native_status_t status =
      CopyStringFromCstr(directory_path, &directory_path_value);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return engine->state.content.MountDirectory(directory_path_value);
}

engine_native_status_t content_read_file(engine_native_engine_t* engine,
                                         const char* asset_path,
                                         void* buffer,
                                         size_t buffer_size,
                                         size_t* out_size) {
  if (engine == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  std::string asset_path_value;
  const engine_native_status_t status =
      CopyStringFromCstr(asset_path, &asset_path_value);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return engine->state.content.ReadFile(asset_path_value, buffer,
                                        buffer_size, out_size);
}

engine_native_status_t content_mount_pak_view(
    engine_native_engine_t* engine,
    engine_native_string_view_t pak_path) {
  if (engine == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  std::string pak_path_value;
  const engine_native_status_t status =
      CopyStringFromView(pak_path, &pak_path_value);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return engine->state.content.MountPak(pak_path_value);
}

engine_native_status_t content_mount_directory_view(
    engine_native_engine_t* engine,
    engine_native_string_view_t directory_path) {
  if (engine == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  std::string directory_path_value;
  const engine_native_status_t status =
      CopyStringFromView(directory_path, &directory_path_value);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return engine->state.content.MountDirectory(directory_path_value);
}

engine_native_status_t content_read_file_view(
    engine_native_engine_t* engine,
    engine_native_string_view_t asset_path,
    void* buffer,
    size_t buffer_size,
    size_t* out_size) {
  if (engine == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  std::string asset_path_value;
  const engine_native_status_t status =
      CopyStringFromView(asset_path, &asset_path_value);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return engine->state.content.ReadFile(asset_path_value, buffer, buffer_size,
                                        out_size);
}

}  // extern "C"
