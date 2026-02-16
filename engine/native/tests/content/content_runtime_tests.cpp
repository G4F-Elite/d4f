#include "content/content_runtime_tests.h"

#include <assert.h>

#include <array>
#include <chrono>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>

#include "engine_native.h"

namespace dff::native::tests {
namespace {

constexpr uint32_t kPakMagic = 0x50464644u;  // DFFP
constexpr uint32_t kPakVersion = 3u;

struct ScopedTempDirectory {
  explicit ScopedTempDirectory(const std::string& test_name) {
    const auto suffix = std::to_string(
        std::chrono::steady_clock::now().time_since_epoch().count());
    path = std::filesystem::temp_directory_path() /
           ("dff_native_" + test_name + "_" + suffix);
    std::filesystem::create_directories(path);
  }

  ~ScopedTempDirectory() { std::filesystem::remove_all(path); }

  std::filesystem::path path;
};

struct PakAsset {
  std::string path;
  std::string kind;
  std::string compiled_path;
  std::string asset_key;
  std::string payload;
};

void Write7BitEncodedInt(std::ostream* stream, uint32_t value) {
  assert(stream != nullptr);
  while (value >= 0x80u) {
    const uint8_t byte = static_cast<uint8_t>((value & 0x7Fu) | 0x80u);
    stream->put(static_cast<char>(byte));
    value >>= 7u;
  }

  stream->put(static_cast<char>(value));
}

void WriteDotNetString(std::ostream* stream, const std::string& value) {
  assert(stream != nullptr);
  Write7BitEncodedInt(stream, static_cast<uint32_t>(value.size()));
  if (!value.empty()) {
    stream->write(value.data(), static_cast<std::streamsize>(value.size()));
  }
}

size_t EncodedStringSize(const std::string& value) {
  size_t length = value.size();
  size_t size = 1u;
  while (length >= 0x80u) {
    length >>= 7u;
    ++size;
  }

  return size + value.size();
}

size_t ComputeIndexSize(const std::vector<PakAsset>& assets) {
  size_t index_size = 0u;
  for (const PakAsset& asset : assets) {
    index_size += EncodedStringSize(asset.path);
    index_size += EncodedStringSize(asset.kind);
    index_size += EncodedStringSize(asset.compiled_path);
    index_size += EncodedStringSize(asset.asset_key);
    index_size += sizeof(int64_t) * 2u;
  }

  return index_size;
}

void WritePak(const std::filesystem::path& pak_path,
              const std::vector<PakAsset>& assets) {
  std::ofstream stream(pak_path, std::ios::binary | std::ios::trunc);
  assert(stream.is_open());

  const int32_t entry_count = static_cast<int32_t>(assets.size());
  const uint32_t reserved = 0u;
  const int64_t created_at_ticks = static_cast<int64_t>(
      std::chrono::system_clock::now().time_since_epoch().count());
  const size_t header_size =
      sizeof(uint32_t) + sizeof(uint32_t) + sizeof(int32_t) + sizeof(uint32_t) +
      sizeof(int64_t);
  const size_t index_size = ComputeIndexSize(assets);
  int64_t next_offset = static_cast<int64_t>(header_size + index_size);

  stream.write(reinterpret_cast<const char*>(&kPakMagic), sizeof(kPakMagic));
  stream.write(reinterpret_cast<const char*>(&kPakVersion), sizeof(kPakVersion));
  stream.write(reinterpret_cast<const char*>(&entry_count), sizeof(entry_count));
  stream.write(reinterpret_cast<const char*>(&reserved), sizeof(reserved));
  stream.write(reinterpret_cast<const char*>(&created_at_ticks),
               sizeof(created_at_ticks));

  for (const PakAsset& asset : assets) {
    WriteDotNetString(&stream, asset.path);
    WriteDotNetString(&stream, asset.kind);
    WriteDotNetString(&stream, asset.compiled_path);
    WriteDotNetString(&stream, asset.asset_key);

    const int64_t size_bytes = static_cast<int64_t>(asset.payload.size());
    const int64_t offset_bytes = size_bytes == 0 ? 0 : next_offset;
    stream.write(reinterpret_cast<const char*>(&offset_bytes), sizeof(offset_bytes));
    stream.write(reinterpret_cast<const char*>(&size_bytes), sizeof(size_bytes));
    next_offset += size_bytes;
  }

  for (const PakAsset& asset : assets) {
    if (!asset.payload.empty()) {
      stream.write(asset.payload.data(),
                   static_cast<std::streamsize>(asset.payload.size()));
    }
  }
}

void TestMountPakAndReadFile() {
  ScopedTempDirectory temp("content_pak");
  const std::filesystem::path pak_path = temp.path / "content.pak";
  const std::string payload = "hello";

  WritePak(
      pak_path,
      {PakAsset{
          .path = "assets/example.txt",
          .kind = "text",
          .compiled_path = "text/example.txt.bin",
          .asset_key = "example_key",
          .payload = payload,
      }});

  engine_native_engine_t* engine = nullptr;
  const engine_native_create_desc_t create_desc{
      .api_version = ENGINE_NATIVE_API_VERSION,
      .user_data = nullptr,
  };
  assert(engine_create(&create_desc, &engine) == ENGINE_NATIVE_STATUS_OK);
  assert(engine != nullptr);

  size_t out_size = 0u;
  assert(content_read_file(engine, "assets/example.txt", nullptr, 0u,
                           &out_size) == ENGINE_NATIVE_STATUS_NOT_FOUND);

  assert(content_mount_pak(engine, pak_path.string().c_str()) ==
         ENGINE_NATIVE_STATUS_OK);

  assert(content_read_file(engine, "assets/example.txt", nullptr, 0u,
                           &out_size) == ENGINE_NATIVE_STATUS_OK);
  assert(out_size == payload.size());

  std::array<char, 2> too_small{};
  assert(content_read_file(engine, "assets/example.txt", too_small.data(),
                           too_small.size(), &out_size) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(out_size == payload.size());

  std::array<char, 16> buffer{};
  assert(content_read_file(engine, "assets/example.txt", buffer.data(),
                           buffer.size(), &out_size) == ENGINE_NATIVE_STATUS_OK);
  assert(out_size == payload.size());
  assert(std::memcmp(buffer.data(), payload.data(), payload.size()) == 0);

  assert(content_read_file(engine, "../bad", buffer.data(), buffer.size(),
                           &out_size) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  assert(engine_destroy(engine) == ENGINE_NATIVE_STATUS_OK);
}

void TestMountDirectoryAndValidation() {
  ScopedTempDirectory temp("content_directory");
  const std::filesystem::path source_root = temp.path / "dev";
  const std::filesystem::path asset_directory = source_root / "assets";
  std::filesystem::create_directories(asset_directory);

  const std::string payload = "live";
  {
    std::ofstream file(asset_directory / "raw.txt", std::ios::binary | std::ios::trunc);
    assert(file.is_open());
    file.write(payload.data(), static_cast<std::streamsize>(payload.size()));
  }

  engine_native_engine_t* engine = nullptr;
  const engine_native_create_desc_t create_desc{
      .api_version = ENGINE_NATIVE_API_VERSION,
      .user_data = nullptr,
  };
  assert(engine_create(&create_desc, &engine) == ENGINE_NATIVE_STATUS_OK);

  assert(content_mount_directory(engine, source_root.string().c_str()) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(content_mount_directory(engine, (temp.path / "missing").string().c_str()) ==
         ENGINE_NATIVE_STATUS_NOT_FOUND);
  assert(content_mount_pak(engine, (temp.path / "missing.pak").string().c_str()) ==
         ENGINE_NATIVE_STATUS_NOT_FOUND);

  size_t out_size = 0u;
  std::array<char, 16> buffer{};
  assert(content_read_file(engine, "assets/raw.txt", buffer.data(), buffer.size(),
                           &out_size) == ENGINE_NATIVE_STATUS_OK);
  assert(out_size == payload.size());
  assert(std::memcmp(buffer.data(), payload.data(), payload.size()) == 0);

  assert(content_read_file(engine, "assets/raw.txt", nullptr, 4u, &out_size) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(content_read_file(engine, nullptr, buffer.data(), buffer.size(),
                           &out_size) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(content_read_file(engine, "assets/raw.txt", buffer.data(), buffer.size(),
                           nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(content_mount_directory(engine, nullptr) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(content_mount_pak(engine, nullptr) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  assert(engine_destroy(engine) == ENGINE_NATIVE_STATUS_OK);
}

}  // namespace

void RunContentRuntimeTests() {
  TestMountPakAndReadFile();
  TestMountDirectoryAndValidation();
}

}  // namespace dff::native::tests
