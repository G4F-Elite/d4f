#ifndef DFF_ENGINE_NATIVE_CONTENT_PAK_INDEX_H
#define DFF_ENGINE_NATIVE_CONTENT_PAK_INDEX_H

#include <cstddef>
#include <cstdint>

#include <filesystem>
#include <string>
#include <unordered_map>

#include "engine_native.h"

namespace dff::native::content {

struct PakAssetEntry {
  uint64_t offset_bytes = 0u;
  uint64_t size_bytes = 0u;
};

engine_native_status_t ReadPakIndex(
    const std::filesystem::path& pak_path,
    std::unordered_map<std::string, PakAssetEntry>* out_entries);

engine_native_status_t ReadPakAssetBytes(
    const std::filesystem::path& pak_path,
    const PakAssetEntry& entry,
    void* buffer,
    size_t buffer_size,
    size_t* out_size);

}  // namespace dff::native::content

#endif
