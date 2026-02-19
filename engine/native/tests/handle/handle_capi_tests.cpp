#include <assert.h>

#include <array>
#include <cstdint>
#include <cstring>
#include <vector>

#include "engine_native.h"

namespace {

std::vector<uint8_t> CreateValidSoundBlob() {
  constexpr uint32_t kMagic = 0x424E5344u;   // DSNB
  constexpr uint32_t kVersion = 1u;

  std::vector<uint8_t> bytes(sizeof(uint32_t) * 2u);
  std::memcpy(bytes.data(), &kMagic, sizeof(uint32_t));
  std::memcpy(bytes.data() + sizeof(uint32_t), &kVersion, sizeof(uint32_t));
  return bytes;
}

void TestHandleLifecycleAndSubsystemAccess() {
  engine_native_create_desc_t create_desc{
      .api_version = ENGINE_NATIVE_API_VERSION,
      .user_data = nullptr};

  engine_native_engine_handle_t engine = ENGINE_NATIVE_INVALID_HANDLE;
  assert(engine_create_handle(nullptr, &engine) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(engine_create_handle(&create_desc, nullptr) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(engine_create_handle(&create_desc, &engine) == ENGINE_NATIVE_STATUS_OK);
  assert(engine != ENGINE_NATIVE_INVALID_HANDLE);

  engine_native_renderer_handle_t renderer = ENGINE_NATIVE_INVALID_HANDLE;
  engine_native_physics_handle_t physics = ENGINE_NATIVE_INVALID_HANDLE;
  engine_native_audio_handle_t audio = ENGINE_NATIVE_INVALID_HANDLE;
  engine_native_net_handle_t net = ENGINE_NATIVE_INVALID_HANDLE;

  assert(engine_get_renderer_handle(engine, &renderer) == ENGINE_NATIVE_STATUS_OK);
  assert(engine_get_physics_handle(engine, &physics) == ENGINE_NATIVE_STATUS_OK);
  assert(engine_get_audio_handle(engine, &audio) == ENGINE_NATIVE_STATUS_OK);
  assert(engine_get_net_handle(engine, &net) == ENGINE_NATIVE_STATUS_OK);

  assert(renderer != ENGINE_NATIVE_INVALID_HANDLE);
  assert(physics != ENGINE_NATIVE_INVALID_HANDLE);
  assert(audio != ENGINE_NATIVE_INVALID_HANDLE);
  assert(net != ENGINE_NATIVE_INVALID_HANDLE);

  void* frame_memory = nullptr;
  assert(renderer_begin_frame_handle(renderer, 1024u, 64u, &frame_memory) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(frame_memory != nullptr);

  engine_native_render_packet_t empty_packet{
      .draw_items = nullptr,
      .draw_item_count = 0u,
      .ui_items = nullptr,
      .ui_item_count = 0u,
      .debug_view_mode = ENGINE_NATIVE_DEBUG_VIEW_NONE,
      .reserved0 = 0u,
      .reserved1 = 0u,
      .reserved2 = 0u};
  assert(renderer_submit_handle(renderer, &empty_packet) == ENGINE_NATIVE_STATUS_OK);
  assert(renderer_present_handle(renderer) == ENGINE_NATIVE_STATUS_OK);

  engine_native_renderer_frame_stats_t stats{};
  assert(renderer_get_last_frame_stats_handle(renderer, &stats) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(stats.present_count == 1u);

  assert(physics_step_handle(physics, 1.0 / 60.0) ==
         ENGINE_NATIVE_STATUS_INVALID_STATE);

  const std::vector<uint8_t> sound_blob = CreateValidSoundBlob();
  engine_native_resource_handle_t sound = 0u;
  assert(audio_create_sound_from_blob_handle(audio,
                                             sound_blob.data(),
                                             sound_blob.size(),
                                             &sound) == ENGINE_NATIVE_STATUS_OK);
  assert(sound != 0u);

  engine_native_audio_play_desc_t play_desc{};
  play_desc.volume = 1.0f;
  play_desc.pitch = 1.0f;
  play_desc.bus = ENGINE_NATIVE_AUDIO_BUS_SFX;
  play_desc.loop = 0u;
  play_desc.is_spatialized = 0u;
  uint64_t emitter_id = 0u;
  assert(audio_play_handle(audio, sound, &play_desc, &emitter_id) ==
         ENGINE_NATIVE_STATUS_OK);
  assert(emitter_id != 0u);

  engine_native_listener_desc_t listener{};
  listener.forward[2] = -1.0f;
  listener.up[1] = 1.0f;
  assert(audio_set_listener_handle(audio, &listener) == ENGINE_NATIVE_STATUS_OK);

  engine_native_audio_bus_params_t bus_params{};
  bus_params.bus = ENGINE_NATIVE_AUDIO_BUS_SFX;
  bus_params.gain = 0.5f;
  bus_params.lowpass = 0.8f;
  bus_params.reverb_send = 0.2f;
  bus_params.muted = 0u;
  assert(audio_set_bus_params_handle(audio, &bus_params) == ENGINE_NATIVE_STATUS_OK);

  engine_native_net_events_t events{};
  assert(net_pump_handle(net, &events) == ENGINE_NATIVE_STATUS_OK);

  assert(engine_destroy_handle(engine) == ENGINE_NATIVE_STATUS_OK);

  assert(renderer_present_handle(renderer) == ENGINE_NATIVE_STATUS_NOT_FOUND);
  assert(audio_set_listener_handle(audio, &listener) == ENGINE_NATIVE_STATUS_NOT_FOUND);
  assert(audio_set_bus_params_handle(audio, &bus_params) == ENGINE_NATIVE_STATUS_NOT_FOUND);
  assert(net_pump_handle(net, &events) == ENGINE_NATIVE_STATUS_NOT_FOUND);
  assert(engine_destroy_handle(engine) == ENGINE_NATIVE_STATUS_NOT_FOUND);
}

void TestStandaloneNetHandleLifecycle() {
  engine_native_net_desc_t desc{};
  desc.local_peer_id = 42u;
  desc.max_events_per_pump = 16u;
  desc.max_payload_bytes = 4096u;
  desc.loopback_enabled = 1u;

  engine_native_net_handle_t net = ENGINE_NATIVE_INVALID_HANDLE;
  assert(net_create_handle(nullptr, &net) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(net_create_handle(&desc, nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(net_create_handle(&desc, &net) == ENGINE_NATIVE_STATUS_OK);
  assert(net != ENGINE_NATIVE_INVALID_HANDLE);

  engine_native_net_send_desc_t send_desc{};
  send_desc.peer_id = 42u;
  send_desc.channel = 0u;
  std::array<uint8_t, 4> payload{1u, 2u, 3u, 4u};
  send_desc.payload = payload.data();
  send_desc.payload_size = static_cast<uint32_t>(payload.size());
  assert(net_send_handle(net, &send_desc) == ENGINE_NATIVE_STATUS_OK);

  engine_native_net_events_t events{};
  assert(net_pump_handle(net, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count >= 1u);

  assert(net_destroy_handle(net) == ENGINE_NATIVE_STATUS_OK);
  assert(net_pump_handle(net, &events) == ENGINE_NATIVE_STATUS_NOT_FOUND);
}

void TestEngineHandleGenerationChanges() {
  engine_native_create_desc_t create_desc{
      .api_version = ENGINE_NATIVE_API_VERSION,
      .user_data = nullptr};

  engine_native_engine_handle_t first = ENGINE_NATIVE_INVALID_HANDLE;
  engine_native_engine_handle_t second = ENGINE_NATIVE_INVALID_HANDLE;
  assert(engine_create_handle(&create_desc, &first) == ENGINE_NATIVE_STATUS_OK);
  assert(engine_destroy_handle(first) == ENGINE_NATIVE_STATUS_OK);
  assert(engine_create_handle(&create_desc, &second) == ENGINE_NATIVE_STATUS_OK);
  assert(second != ENGINE_NATIVE_INVALID_HANDLE);
  assert(first != second);
  assert(engine_destroy_handle(second) == ENGINE_NATIVE_STATUS_OK);
}

}  // namespace

int main() {
  TestHandleLifecycleAndSubsystemAccess();
  TestStandaloneNetHandleLifecycle();
  TestEngineHandleGenerationChanges();
  return 0;
}
