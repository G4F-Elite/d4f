#include <assert.h>

#include <cstdint>

#include "engine_native.h"

namespace {

void TestCaptureFlowAndValidation() {
  assert(engine_get_native_api_version() == ENGINE_NATIVE_API_VERSION);

  engine_native_create_desc_t create_desc{
      .api_version = ENGINE_NATIVE_API_VERSION,
      .user_data = nullptr};

  engine_native_engine_t* engine = nullptr;
  assert(engine_create(&create_desc, &engine) == ENGINE_NATIVE_STATUS_OK);
  assert(engine != nullptr);

  engine_native_renderer_t* renderer = nullptr;
  assert(engine_get_renderer(engine, &renderer) == ENGINE_NATIVE_STATUS_OK);
  assert(renderer != nullptr);

  uint64_t request_id = 0u;
  engine_native_capture_request_t request{};
  request.width = 4u;
  request.height = 2u;
  request.include_alpha = 1u;

  assert(capture_request(nullptr, &request, &request_id) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(capture_request(renderer, nullptr, &request_id) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(capture_request(renderer, &request, nullptr) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  assert(capture_request(renderer, &request, &request_id) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(request_id != 0u);

  engine_native_capture_result_t missing_result{};
  uint8_t missing_ready = 0u;
  assert(capture_poll(request_id + 1u, &missing_result, &missing_ready) ==
         ENGINE_NATIVE_STATUS_NOT_FOUND);
  assert(missing_ready == 0u);

  engine_native_capture_result_t result{};
  uint8_t is_ready = 0u;
  assert(capture_poll(request_id, &result, &is_ready) == ENGINE_NATIVE_STATUS_OK);
  assert(is_ready == 0u);
  assert(result.pixels == nullptr);

  assert(capture_poll(request_id, &result, &is_ready) == ENGINE_NATIVE_STATUS_OK);
  assert(is_ready == 1u);
  assert(result.width == 4u);
  assert(result.height == 2u);
  assert(result.stride == 16u);
  assert(result.format == ENGINE_NATIVE_CAPTURE_FORMAT_RGBA8_UNORM);
  assert(result.pixels != nullptr);
  assert(result.pixel_bytes == 32u);
  assert(result.pixels[3] > 0u);

  assert(capture_free_result(&result) == ENGINE_NATIVE_STATUS_OK);
  assert(result.pixels == nullptr);
  assert(result.pixel_bytes == 0u);

  engine_native_capture_request_t request_half = request;
  request_half.reserved1 = ENGINE_NATIVE_CAPTURE_FORMAT_RGBA16_FLOAT;
  assert(capture_request(renderer, &request_half, &request_id) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(request_id != 0u);

  assert(capture_poll(request_id, &result, &is_ready) == ENGINE_NATIVE_STATUS_OK);
  assert(is_ready == 0u);
  assert(capture_poll(request_id, &result, &is_ready) == ENGINE_NATIVE_STATUS_OK);
  assert(is_ready == 1u);
  assert(result.width == 4u);
  assert(result.height == 2u);
  assert(result.stride == 32u);
  assert(result.format == ENGINE_NATIVE_CAPTURE_FORMAT_RGBA16_FLOAT);
  assert(result.pixels != nullptr);
  assert(result.pixel_bytes == 64u);
  assert(result.pixels[1] > 0u || result.pixels[3] > 0u || result.pixels[5] > 0u);

  assert(capture_poll(request_id, &result, &is_ready) ==
         ENGINE_NATIVE_STATUS_NOT_FOUND);
  assert(is_ready == 0u);

  assert(capture_free_result(&result) == ENGINE_NATIVE_STATUS_OK);
  assert(result.pixels == nullptr);
  assert(result.pixel_bytes == 0u);

  assert(capture_poll(0u, &result, &is_ready) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(capture_poll(request_id, nullptr, &is_ready) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(capture_poll(request_id, &result, nullptr) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(capture_free_result(nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  engine_native_capture_request_t invalid_request = request;
  invalid_request.width = 0u;
  assert(capture_request(renderer, &invalid_request, &request_id) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  invalid_request = request;
  invalid_request.include_alpha = 3u;
  assert(capture_request(renderer, &invalid_request, &request_id) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  invalid_request = request;
  invalid_request.reserved0 = 6u;
  assert(capture_request(renderer, &invalid_request, &request_id) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  invalid_request = request;
  invalid_request.reserved1 = 3u;
  assert(capture_request(renderer, &invalid_request, &request_id) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  assert(engine_destroy(engine) == ENGINE_NATIVE_STATUS_OK);
}

}  // namespace

int main() {
  TestCaptureFlowAndValidation();
  return 0;
}
