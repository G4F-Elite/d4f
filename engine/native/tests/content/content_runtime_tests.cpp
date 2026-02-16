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

constexpr uint32_t kCompiledManifestMagic = 0x4D464644u;  // DFFM
constexpr uint32_t kCompiledManifestVersion = 1u;

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

struct ManifestEntry {
  std::string path;
  std::string kind;
  std::string compiled_path;
  int64_t size_bytes = 0;
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

void WriteCompiledManifest(
    const std::filesystem::path& manifest_path,
    const std::vector<ManifestEntry>& entries) {
  std::ofstream stream(manifest_path, std::ios::binary | std::ios::trunc);
  assert(stream.is_open());

  const int32_t entry_count = static_cast<int32_t>(entries.size());
  stream.write(reinterpret_cast<const char*>(&kCompiledManifestMagic),
               sizeof(kCompiledManifestMagic));
  stream.write(reinterpret_cast<const char*>(&kCompiledManifestVersion),
               sizeof(kCompiledManifestVersion));
  stream.write(reinterpret_cast<const char*>(&entry_count), sizeof(entry_count));

  for (const ManifestEntry& entry : entries) {
    WriteDotNetString(&stream, entry.path);
    WriteDotNetString(&stream, entry.kind);
    WriteDotNetString(&stream, entry.compiled_path);
    stream.write(reinterpret_cast<const char*>(&entry.size_bytes),
                 sizeof(entry.size_bytes));
  }
}

void TestMountPakAndReadFile() {
  ScopedTempDirectory temp("content_pak");
  const std::filesystem::path compiled_root = temp.path / "compiled" / "text";
  std::filesystem::create_directories(compiled_root);

  const std::string payload = "hello";
  const std::filesystem::path compiled_file = compiled_root / "example.txt.bin";
  {
    std::ofstream file(compiled_file, std::ios::binary | std::ios::trunc);
    assert(file.is_open());
    file.write(payload.data(), static_cast<std::streamsize>(payload.size()));
  }

  const std::filesystem::path pak_path = temp.path / "content.pak";
  {
    std::ofstream file(pak_path, std::ios::trunc);
    assert(file.is_open());
    file << "{}";
  }

  WriteCompiledManifest(
      temp.path / "compiled.manifest.bin",
      {ManifestEntry{
          .path = "assets/example.txt",
          .kind = "text",
          .compiled_path = "text/example.txt.bin",
          .size_bytes = static_cast<int64_t>(payload.size()),
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
                           buffer.size(), &out_size) ==
         ENGINE_NATIVE_STATUS_OK);
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
