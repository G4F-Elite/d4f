#ifndef DFF_ENGINE_NATIVE_CONTENT_RUNTIME_H
#define DFF_ENGINE_NATIVE_CONTENT_RUNTIME_H

#include <cstddef>
#include <cstdint>

#include <filesystem>
#include <string>
#include <unordered_map>
#include <vector>

#include "engine_native.h"

namespace dff::native::content {

class ContentRuntime {
 public:
  engine_native_status_t MountPak(const std::string& pak_path);

  engine_native_status_t MountDirectory(const std::string& directory_path);

  engine_native_status_t ReadFile(const std::string& asset_path,
                                  void* buffer,
                                  size_t buffer_size,
                                  size_t* out_size) const;

  size_t pak_mount_count() const { return pak_mounts_.size(); }
  size_t directory_mount_count() const { return directory_mounts_.size(); }

 private:
  struct PakMount {
    std::filesystem::path compiled_root;
    std::unordered_map<std::string, std::string> compiled_path_by_asset;
  };

  std::vector<PakMount> pak_mounts_;
  std::vector<std::filesystem::path> directory_mounts_;
};

}  // namespace dff::native::content

#endif
