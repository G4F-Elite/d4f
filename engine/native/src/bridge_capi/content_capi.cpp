#include "bridge_capi/bridge_state.h"

#include <string>

extern "C" {

engine_native_status_t content_mount_pak(engine_native_engine_t* engine,
                                         const char* pak_path) {
  if (engine == nullptr || pak_path == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return engine->state.content.MountPak(std::string(pak_path));
}

engine_native_status_t content_mount_directory(engine_native_engine_t* engine,
                                               const char* directory_path) {
  if (engine == nullptr || directory_path == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return engine->state.content.MountDirectory(std::string(directory_path));
}

engine_native_status_t content_read_file(engine_native_engine_t* engine,
                                         const char* asset_path,
                                         void* buffer,
                                         size_t buffer_size,
                                         size_t* out_size) {
  if (engine == nullptr || asset_path == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return engine->state.content.ReadFile(std::string(asset_path), buffer,
                                        buffer_size, out_size);
}

}  // extern "C"
