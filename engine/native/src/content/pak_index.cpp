#include "content/pak_index.h"

#include <algorithm>
#include <cstdint>
#include <fstream>
#include <limits>
#include <sstream>
#include <string>
#include <vector>

namespace dff::native::content {

namespace {

constexpr uint32_t kPakMagic = 0x50464644u;  // DFFP
constexpr uint32_t kPakVersion = 3u;

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

}  // namespace

engine_native_status_t ReadPakIndex(
    const std::filesystem::path& pak_path,
    std::unordered_map<std::string, PakAssetEntry>* out_entries) {
  if (out_entries == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  std::ifstream stream(pak_path, std::ios::binary);
  if (!stream.is_open()) {
    return ENGINE_NATIVE_STATUS_NOT_FOUND;
  }

  uint32_t magic = 0u;
  uint32_t version = 0u;
  int32_t entry_count = 0;
  uint32_t reserved = 0u;
  int64_t created_at_ticks = 0;

  stream.read(reinterpret_cast<char*>(&magic), sizeof(magic));
  stream.read(reinterpret_cast<char*>(&version), sizeof(version));
  stream.read(reinterpret_cast<char*>(&entry_count), sizeof(entry_count));
  stream.read(reinterpret_cast<char*>(&reserved), sizeof(reserved));
  stream.read(reinterpret_cast<char*>(&created_at_ticks), sizeof(created_at_ticks));
  if (!stream.good()) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }

  if (magic != kPakMagic || version != kPakVersion || entry_count < 0) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }

  out_entries->clear();
  out_entries->reserve(static_cast<size_t>(entry_count));

  for (int32_t i = 0; i < entry_count; ++i) {
    std::string raw_asset_path;
    std::string raw_kind;
    std::string raw_compiled_path;
    std::string raw_asset_key;
    int64_t offset_bytes = 0;
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

    status = ReadUtf8String(&stream, &raw_asset_key);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    stream.read(reinterpret_cast<char*>(&offset_bytes), sizeof(offset_bytes));
    stream.read(reinterpret_cast<char*>(&size_bytes), sizeof(size_bytes));
    if (!stream.good() || offset_bytes < 0 || size_bytes < 0) {
      return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
    }

    std::string normalized_asset_path;
    status = NormalizeAssetPath(raw_asset_path, &normalized_asset_path);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    (*out_entries)[normalized_asset_path] = PakAssetEntry{
        static_cast<uint64_t>(offset_bytes),
        static_cast<uint64_t>(size_bytes)};
  }

  stream.seekg(0, std::ios::end);
  const std::streamoff file_size_stream = stream.tellg();
  if (file_size_stream < 0) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }
  const uint64_t file_size = static_cast<uint64_t>(file_size_stream);

  for (const auto& pair : *out_entries) {
    const PakAssetEntry& entry = pair.second;
    if (entry.size_bytes == 0u) {
      continue;
    }

    if (entry.offset_bytes > file_size ||
        entry.size_bytes > file_size - entry.offset_bytes) {
      return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
    }
  }

  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t ReadPakAssetBytes(const std::filesystem::path& pak_path,
                                         const PakAssetEntry& entry,
                                         void* buffer,
                                         size_t buffer_size,
                                         size_t* out_size) {
  if (out_size == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_size = static_cast<size_t>(entry.size_bytes);

  if (buffer == nullptr) {
    if (buffer_size != 0u) {
      return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    return ENGINE_NATIVE_STATUS_OK;
  }

  if (buffer_size < static_cast<size_t>(entry.size_bytes)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  std::ifstream stream(pak_path, std::ios::binary | std::ios::ate);
  if (!stream.is_open()) {
    return ENGINE_NATIVE_STATUS_NOT_FOUND;
  }

  const std::streamoff file_size_stream = stream.tellg();
  if (file_size_stream < 0) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }
  const uint64_t file_size = static_cast<uint64_t>(file_size_stream);
  if (entry.offset_bytes > file_size ||
      entry.size_bytes > file_size - entry.offset_bytes) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }

  if (entry.size_bytes == 0u) {
    return ENGINE_NATIVE_STATUS_OK;
  }

  stream.seekg(static_cast<std::streamoff>(entry.offset_bytes), std::ios::beg);
  stream.read(static_cast<char*>(buffer),
              static_cast<std::streamsize>(entry.size_bytes));
  if (stream.gcount() != static_cast<std::streamsize>(entry.size_bytes)) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }

  return ENGINE_NATIVE_STATUS_OK;
}

}  // namespace dff::native::content
