#include <assert.h>

#include <array>
#include <chrono>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <string>

#include "engine_native.h"

namespace {

struct ScopedTempDirectory {
  explicit ScopedTempDirectory(const char* test_name) {
    const auto suffix = std::to_string(
        std::chrono::steady_clock::now().time_since_epoch().count());
    path = std::filesystem::temp_directory_path() /
           ("dff_native_" + std::string(test_name) + "_" + suffix);
    std::filesystem::create_directories(path);
  }

  ~ScopedTempDirectory() { std::filesystem::remove_all(path); }

  std::filesystem::path path;
};

void WriteBinaryFile(const std::filesystem::path& file_path, const std::string& payload) {
  std::filesystem::create_directories(file_path.parent_path());
  std::ofstream file(file_path, std::ios::binary | std::ios::trunc);
  assert(file.is_open());
  file.write(payload.data(), static_cast<std::streamsize>(payload.size()));
}

engine_native_engine_t* CreateEngine() {
  const engine_native_create_desc_t create_desc{
      .api_version = ENGINE_NATIVE_API_VERSION,
      .user_data = nullptr,
  };

  engine_native_engine_t* engine = nullptr;
  assert(engine_create(&create_desc, &engine) == ENGINE_NATIVE_STATUS_OK);
  assert(engine != nullptr);
  return engine;
}

engine_native_engine_handle_t CreateEngineHandle() {
  const engine_native_create_desc_t create_desc{
      .api_version = ENGINE_NATIVE_API_VERSION,
      .user_data = nullptr,
  };

  engine_native_engine_handle_t engine = ENGINE_NATIVE_INVALID_HANDLE;
  assert(engine_create_handle(&create_desc, &engine) == ENGINE_NATIVE_STATUS_OK);
  assert(engine != ENGINE_NATIVE_INVALID_HANDLE);
  return engine;
}

void TestPointerStringViewContentApis() {
  ScopedTempDirectory temp("content_string_view_pointer");
  const std::filesystem::path source_root = temp.path / "source";
  const std::filesystem::path file_path = source_root / "assets" / "raw.bin";
  const std::string payload = "live";
  WriteBinaryFile(file_path, payload);

  engine_native_engine_t* engine = CreateEngine();

  const std::string source_root_path = source_root.string();
  const std::string source_root_with_suffix = source_root_path + "#ignored";
  const engine_native_string_view_t mount_view{
      .data = source_root_with_suffix.data(),
      .length = source_root_path.size(),
  };
  assert(content_mount_directory_view(engine, mount_view) == ENGINE_NATIVE_STATUS_OK);

  const std::string asset_path = "assets/raw.bin";
  const std::string asset_path_with_suffix = asset_path + "#ignored";
  const engine_native_string_view_t asset_view{
      .data = asset_path_with_suffix.data(),
      .length = asset_path.size(),
  };

  size_t out_size = 0u;
  assert(content_read_file_view(engine, asset_view, nullptr, 0u, &out_size) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(out_size == payload.size());

  std::array<char, 16> buffer{};
  assert(content_read_file_view(engine, asset_view, buffer.data(), buffer.size(),
                                &out_size) == ENGINE_NATIVE_STATUS_OK);
  assert(out_size == payload.size());
  assert(std::memcmp(buffer.data(), payload.data(), payload.size()) == 0);

  const char asset_with_embedded_null[]{
      'a', 's', 's', 'e', 't', 's', '/', 'r', 'a',
      'w', '.', 'b', 'i', 'n', '\0', 'x'};
  const engine_native_string_view_t invalid_asset_view{
      .data = asset_with_embedded_null,
      .length = sizeof(asset_with_embedded_null),
  };
  assert(content_read_file_view(engine, invalid_asset_view, buffer.data(),
                                buffer.size(), &out_size) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  const engine_native_string_view_t null_data_view{
      .data = nullptr,
      .length = 1u,
  };
  assert(content_mount_directory_view(engine, null_data_view) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  assert(engine_destroy(engine) == ENGINE_NATIVE_STATUS_OK);
}

void TestHandleStringViewContentApis() {
  ScopedTempDirectory temp("content_string_view_handle");
  const std::filesystem::path source_root = temp.path / "source";
  const std::filesystem::path file_path = source_root / "assets" / "raw.bin";
  const std::string payload = "seed";
  WriteBinaryFile(file_path, payload);

  const engine_native_engine_handle_t engine = CreateEngineHandle();

  const std::string source_root_path = source_root.string();
  const std::string source_root_with_suffix = source_root_path + "#ignored";
  const engine_native_string_view_t mount_view{
      .data = source_root_with_suffix.data(),
      .length = source_root_path.size(),
  };
  assert(content_mount_directory_view_handle(engine, mount_view) ==
         ENGINE_NATIVE_STATUS_OK);

  const std::string asset_path = "assets/raw.bin";
  const std::string asset_path_with_suffix = asset_path + "#ignored";
  const engine_native_string_view_t asset_view{
      .data = asset_path_with_suffix.data(),
      .length = asset_path.size(),
  };

  size_t out_size = 0u;
  assert(content_read_file_view_handle(engine, asset_view, nullptr, 0u, &out_size) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(out_size == payload.size());

  std::array<char, 16> buffer{};
  assert(content_read_file_view_handle(engine, asset_view, buffer.data(),
                                       buffer.size(), &out_size) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(out_size == payload.size());
  assert(std::memcmp(buffer.data(), payload.data(), payload.size()) == 0);

  const engine_native_string_view_t invalid_view{
      .data = nullptr,
      .length = 2u,
  };
  assert(content_mount_pak_view_handle(engine, invalid_view) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  assert(engine_destroy_handle(engine) == ENGINE_NATIVE_STATUS_OK);

  assert(content_mount_directory_view_handle(engine, mount_view) ==
         ENGINE_NATIVE_STATUS_NOT_FOUND);
}

}  // namespace

int main() {
  TestPointerStringViewContentApis();
  TestHandleStringViewContentApis();
  return 0;
}
