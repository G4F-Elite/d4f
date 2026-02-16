#include "content/content_runtime.h"

#include <algorithm>
#include <array>
#include <cstdint>
#include <fstream>
#include <limits>
#include <sstream>
#include <vector>

namespace dff::native::content {

namespace {

constexpr uint32_t kCompiledManifestMagic = 0x4D464644u;  // DFFM
constexpr uint32_t kCompiledManifestVersion = 1u;

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

bool IsSafeRelativePath(const std::string& value) {
  if (value.empty()) {
    return false;
  }

  std::filesystem::path path(value);
  if (path.is_absolute()) {
    return false;
  }

  for (const auto& segment : path) {
    if (segment == "." || segment == "..") {
      return false;
    }
  }

  return true;
}

engine_native_status_t Read7BitEncodedInt(std::istream* stream, uint32_t* out_value) {
  if (stream == nullptr || out_value == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  uint32_t result = 0u;
  uint32_t shift = 0u;
  for (uint32_t i = 0u; i < 5u; ++i) {
    const int raw = stream->get();
    if (raw == EOF) {
      return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
    }

    const uint8_t byte = static_cast<uint8_t>(raw);
    result |= static_cast<uint32_t>(byte & 0x7Fu) << shift;
    if ((byte & 0x80u) == 0u) {
      *out_value = result;
      return ENGINE_NATIVE_STATUS_OK;
    }

    shift += 7u;
  }

  return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
}

engine_native_status_t ReadUtf8String(std::istream* stream, std::string* out_value) {
  if (stream == nullptr || out_value == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  uint32_t byte_count = 0u;
  engine_native_status_t status = Read7BitEncodedInt(stream, &byte_count);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  if (byte_count > static_cast<uint32_t>(std::numeric_limits<int32_t>::max())) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }

  std::string buffer;
  buffer.resize(static_cast<size_t>(byte_count));
  if (byte_count > 0u) {
    stream->read(buffer.data(), static_cast<std::streamsize>(byte_count));
    if (stream->gcount() != static_cast<std::streamsize>(byte_count)) {
      return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
    }
  }

  *out_value = std::move(buffer);
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t ReadCompiledManifest(
    const std::filesystem::path& manifest_path,
    std::unordered_map<std::string, std::string>* out_compiled_path_by_asset) {
  if (out_compiled_path_by_asset == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  std::ifstream stream(manifest_path, std::ios::binary);
  if (!stream.is_open()) {
    return ENGINE_NATIVE_STATUS_NOT_FOUND;
  }

  uint32_t magic = 0u;
  uint32_t version = 0u;
  int32_t entry_count = 0;

  stream.read(reinterpret_cast<char*>(&magic), sizeof(magic));
  stream.read(reinterpret_cast<char*>(&version), sizeof(version));
  stream.read(reinterpret_cast<char*>(&entry_count), sizeof(entry_count));
  if (!stream.good()) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }

  if (magic != kCompiledManifestMagic || version != kCompiledManifestVersion ||
      entry_count < 0) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }

  out_compiled_path_by_asset->clear();
  out_compiled_path_by_asset->reserve(static_cast<size_t>(entry_count));

  for (int32_t i = 0; i < entry_count; ++i) {
    std::string raw_asset_path;
    std::string raw_kind;
    std::string raw_compiled_path;
    int64_t size_bytes = 0;

    engine_native_status_t status = ReadUtf8String(&stream, &raw_asset_path);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    status = ReadUtf8String(&stream, &raw_kind);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    status = ReadUtf8String(&stream, &raw_compiled_path);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    stream.read(reinterpret_cast<char*>(&size_bytes), sizeof(size_bytes));
    if (!stream.good() || size_bytes < 0) {
      return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
    }

    std::string normalized_asset_path;
    status = NormalizeAssetPath(raw_asset_path, &normalized_asset_path);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    if (!IsSafeRelativePath(raw_compiled_path)) {
      return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
    }

    std::string normalized_compiled_path = raw_compiled_path;
    std::replace(normalized_compiled_path.begin(), normalized_compiled_path.end(),
                 '\\', '/');
    (*out_compiled_path_by_asset)[normalized_asset_path] =
        std::move(normalized_compiled_path);
  }

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

  const std::filesystem::path pak_directory = absolute_pak_path.parent_path();
  const std::filesystem::path compiled_root = pak_directory / "compiled";
  const std::filesystem::path compiled_manifest_path =
      pak_directory / "compiled.manifest.bin";

  if (!std::filesystem::exists(compiled_root) ||
      !std::filesystem::is_directory(compiled_root)) {
    return ENGINE_NATIVE_STATUS_NOT_FOUND;
  }

  std::unordered_map<std::string, std::string> path_by_asset;
  engine_native_status_t status =
      ReadCompiledManifest(compiled_manifest_path, &path_by_asset);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  PakMount mount;
  mount.compiled_root = compiled_root;
  mount.compiled_path_by_asset = std::move(path_by_asset);
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
    const auto entry_it =
        mount_it->compiled_path_by_asset.find(normalized_asset_path);
    if (entry_it == mount_it->compiled_path_by_asset.end()) {
      continue;
    }

    const std::filesystem::path full_path =
        mount_it->compiled_root / std::filesystem::path(entry_it->second);
    return ReadBytesFromFile(full_path, buffer, buffer_size, out_size);
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
