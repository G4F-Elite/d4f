#include "content/content_runtime.h"

#include <algorithm>
#include <cstdint>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>

namespace dff::native::content {

namespace {

engine_native_status_t NormalizeAssetPath(const std::string& input_path,
                                          std::string* out_normalized_path) {
  if (out_normalized_path == nullptr || input_path.empty()) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  std::string normalized = input_path;
  std::replace(normalized.begin(), normalized.end(), '\\', '/');
  if (!normalized.empty() && normalized.front() == '/') {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  std::stringstream stream(normalized);
  std::string segment;
  std::vector<std::string> segments;
  while (std::getline(stream, segment, '/')) {
    if (segment.empty()) {
      continue;
    }
    if (segment == "." || segment == "..") {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }
    segments.push_back(segment);
  }

  if (segments.empty()) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  std::ostringstream out;
  for (size_t i = 0u; i < segments.size(); ++i) {
    if (i > 0u) {
      out << '/';
    }
    out << segments[i];
  }

  *out_normalized_path = out.str();
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t ReadBytesFromFile(const std::filesystem::path& full_path,
                                         void* buffer,
                                         size_t buffer_size,
                                         size_t* out_size) {
  if (out_size == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_size = 0u;

  std::ifstream stream(full_path, std::ios::binary | std::ios::ate);
  if (!stream.is_open()) {
    return ENGINE_NATIVE_STATUS_NOT_FOUND;
  }

  const std::streamsize size = stream.tellg();
  if (size < 0) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }

  const size_t file_size = static_cast<size_t>(size);
  *out_size = file_size;

  if (buffer == nullptr) {
    if (buffer_size != 0u) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }
    return ENGINE_NATIVE_STATUS_OK;
  }

  if (buffer_size < file_size) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  stream.seekg(0, std::ios::beg);
  if (file_size == 0u) {
    return ENGINE_NATIVE_STATUS_OK;
  }

  stream.read(static_cast<char*>(buffer), static_cast<std::streamsize>(file_size));
  if (stream.gcount() != static_cast<std::streamsize>(file_size)) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }

  return ENGINE_NATIVE_STATUS_OK;
}

}  // namespace

engine_native_status_t ContentRuntime::MountPak(const std::string& pak_path) {
  if (pak_path.empty()) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const std::filesystem::path absolute_pak_path =
      std::filesystem::absolute(std::filesystem::path(pak_path));
  if (!std::filesystem::exists(absolute_pak_path) ||
      !std::filesystem::is_regular_file(absolute_pak_path)) {
    return ENGINE_NATIVE_STATUS_NOT_FOUND;
  }

  std::unordered_map<std::string, PakAssetEntry> entry_by_asset;
  engine_native_status_t status = ReadPakIndex(absolute_pak_path, &entry_by_asset);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  PakMount mount;
  mount.pak_path = absolute_pak_path;
  mount.entry_by_asset = std::move(entry_by_asset);
  pak_mounts_.push_back(std::move(mount));
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t ContentRuntime::MountDirectory(
    const std::string& directory_path) {
  if (directory_path.empty()) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const std::filesystem::path absolute_path =
      std::filesystem::absolute(std::filesystem::path(directory_path));
  if (!std::filesystem::exists(absolute_path) ||
      !std::filesystem::is_directory(absolute_path)) {
    return ENGINE_NATIVE_STATUS_NOT_FOUND;
  }

  directory_mounts_.push_back(absolute_path);
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t ContentRuntime::ReadFile(const std::string& asset_path,
                                                void* buffer,
                                                size_t buffer_size,
                                                size_t* out_size) const {
  std::string normalized_asset_path;
  engine_native_status_t status =
      NormalizeAssetPath(asset_path, &normalized_asset_path);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }
  if (out_size == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  for (auto mount_it = pak_mounts_.rbegin(); mount_it != pak_mounts_.rend();
       ++mount_it) {
    const auto entry_it = mount_it->entry_by_asset.find(normalized_asset_path);
    if (entry_it == mount_it->entry_by_asset.end()) {
      continue;
    }

    return ReadPakAssetBytes(mount_it->pak_path, entry_it->second, buffer,
                             buffer_size, out_size);
  }

  for (auto mount_it = directory_mounts_.rbegin();
       mount_it != directory_mounts_.rend(); ++mount_it) {
    const std::filesystem::path full_path =
        *mount_it / std::filesystem::path(normalized_asset_path);
    status = ReadBytesFromFile(full_path, buffer, buffer_size, out_size);
    if (status == ENGINE_NATIVE_STATUS_OK ||
        status == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT) {
      return status;
    }
  }

  *out_size = 0u;
  return ENGINE_NATIVE_STATUS_NOT_FOUND;
}

}  // namespace dff::native::content
