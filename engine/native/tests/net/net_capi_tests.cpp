#include <assert.h>

#include <cstdint>

#include "engine_native.h"

namespace {

engine_native_engine_t* CreateEngine() {
  engine_native_create_desc_t create_desc{
      .api_version = ENGINE_NATIVE_API_VERSION,
      .user_data = nullptr};

  engine_native_engine_t* engine = nullptr;
  assert(engine_create(&create_desc, &engine) == ENGINE_NATIVE_STATUS_OK);
  assert(engine != nullptr);
  return engine;
}

void TestEngineGetNetValidation() {
  engine_native_engine_t* engine = CreateEngine();
  engine_native_net_t* net = nullptr;

  assert(engine_get_net(nullptr, &net) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(engine_get_net(engine, nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(engine_get_net(engine, &net) == ENGINE_NATIVE_STATUS_OK);
  assert(net != nullptr);

  assert(net_destroy(net) == ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(engine_destroy(engine) == ENGINE_NATIVE_STATUS_OK);
}

void TestEngineNetSendAndPumpFlow() {
  engine_native_engine_t* engine = CreateEngine();
  engine_native_net_t* net = nullptr;
  assert(engine_get_net(engine, &net) == ENGINE_NATIVE_STATUS_OK);
  assert(net != nullptr);

  engine_native_net_events_t events{};
  assert(net_pump(net, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count == 1u);
  assert(events.events != nullptr);
  assert(events.events[0].kind == ENGINE_NATIVE_NET_EVENT_KIND_CONNECTED);
  assert(events.events[0].peer_id == 1u);
  assert(events.events[0].payload == nullptr);
  assert(events.events[0].payload_size == 0u);

  uint8_t payload[3]{3u, 5u, 7u};
  engine_native_net_send_desc_t send_desc{};
  send_desc.peer_id = 42u;
  send_desc.channel = 9u;
  send_desc.payload = payload;
  send_desc.payload_size = 3u;
  assert(net_send(net, &send_desc) == ENGINE_NATIVE_STATUS_OK);

  assert(net_pump(net, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count == 1u);
  assert(events.events != nullptr);
  assert(events.events[0].kind == ENGINE_NATIVE_NET_EVENT_KIND_MESSAGE);
  assert(events.events[0].channel == 9u);
  assert(events.events[0].peer_id == 42u);
  assert(events.events[0].payload != nullptr);
  assert(events.events[0].payload_size == 3u);
  assert(events.events[0].payload[0] == 3u);
  assert(events.events[0].payload[1] == 5u);
  assert(events.events[0].payload[2] == 7u);

  assert(net_pump(net, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count == 0u);
  assert(events.events == nullptr);

  assert(net_send(nullptr, &send_desc) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(net_send(net, nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  send_desc.peer_id = 0u;
  assert(net_send(net, &send_desc) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  assert(net_pump(nullptr, &events) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(net_pump(net, nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  assert(engine_destroy(engine) == ENGINE_NATIVE_STATUS_OK);
}

void TestStandaloneNetCreateDestroyAndLimits() {
  engine_native_net_t* net = nullptr;
  engine_native_net_desc_t desc{};
  desc.local_peer_id = 77u;
  desc.max_events_per_pump = 1u;
  desc.max_payload_bytes = 4u;
  desc.loopback_enabled = 1u;

  assert(net_create(nullptr, &net) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(net_create(&desc, nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(net_create(&desc, &net) == ENGINE_NATIVE_STATUS_OK);
  assert(net != nullptr);

  engine_native_net_events_t events{};
  assert(net_pump(net, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count == 1u);
  assert(events.events[0].kind == ENGINE_NATIVE_NET_EVENT_KIND_CONNECTED);
  assert(events.events[0].peer_id == 77u);

  uint8_t payload_a[1]{11u};
  uint8_t payload_b[1]{13u};
  engine_native_net_send_desc_t send_desc{};
  send_desc.peer_id = 501u;
  send_desc.channel = 1u;
  send_desc.payload = payload_a;
  send_desc.payload_size = 1u;
  assert(net_send(net, &send_desc) == ENGINE_NATIVE_STATUS_OK);

  send_desc.peer_id = 502u;
  send_desc.payload = payload_b;
  assert(net_send(net, &send_desc) == ENGINE_NATIVE_STATUS_OK);

  assert(net_pump(net, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count == 1u);
  assert(events.events[0].peer_id == 501u);
  assert(events.events[0].payload_size == 1u);
  assert(events.events[0].payload[0] == 11u);

  assert(net_pump(net, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count == 1u);
  assert(events.events[0].peer_id == 502u);
  assert(events.events[0].payload_size == 1u);
  assert(events.events[0].payload[0] == 13u);

  uint8_t too_large_payload[8]{};
  send_desc.peer_id = 503u;
  send_desc.payload = too_large_payload;
  send_desc.payload_size = 8u;
  assert(net_send(net, &send_desc) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  assert(net_destroy(nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(net_destroy(net) == ENGINE_NATIVE_STATUS_OK);
}

void TestStandaloneNetWithoutLoopbackSuppressesMessages() {
  engine_native_net_t* net = nullptr;
  engine_native_net_desc_t desc{};
  desc.local_peer_id = 9u;
  desc.max_events_per_pump = 8u;
  desc.max_payload_bytes = 16u;
  desc.loopback_enabled = 0u;
  assert(net_create(&desc, &net) == ENGINE_NATIVE_STATUS_OK);

  engine_native_net_events_t events{};
  assert(net_pump(net, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count == 1u);
  assert(events.events[0].kind == ENGINE_NATIVE_NET_EVENT_KIND_CONNECTED);
  assert(events.events[0].peer_id == 9u);

  uint8_t payload[2]{1u, 2u};
  engine_native_net_send_desc_t send_desc{};
  send_desc.peer_id = 900u;
  send_desc.channel = 4u;
  send_desc.payload = payload;
  send_desc.payload_size = 2u;
  assert(net_send(net, &send_desc) == ENGINE_NATIVE_STATUS_OK);

  assert(net_pump(net, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count == 0u);
  assert(events.events == nullptr);

  assert(net_destroy(net) == ENGINE_NATIVE_STATUS_OK);
}

}  // namespace

int main() {
  TestEngineGetNetValidation();
  TestEngineNetSendAndPumpFlow();
  TestStandaloneNetCreateDestroyAndLimits();
  TestStandaloneNetWithoutLoopbackSuppressesMessages();
  return 0;
}
