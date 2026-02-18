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

void TestStandaloneMultiPeerRouting() {
  engine_native_net_desc_t server_desc{};
  server_desc.local_peer_id = 100u;
  server_desc.max_events_per_pump = 8u;
  server_desc.max_payload_bytes = 64u;
  server_desc.loopback_enabled = 0u;

  engine_native_net_desc_t client_a_desc = server_desc;
  client_a_desc.local_peer_id = 200u;

  engine_native_net_desc_t client_b_desc = server_desc;
  client_b_desc.local_peer_id = 300u;

  engine_native_net_t* server = nullptr;
  engine_native_net_t* client_a = nullptr;
  engine_native_net_t* client_b = nullptr;
  assert(net_create(&server_desc, &server) == ENGINE_NATIVE_STATUS_OK);
  assert(net_create(&client_a_desc, &client_a) == ENGINE_NATIVE_STATUS_OK);
  assert(net_create(&client_b_desc, &client_b) == ENGINE_NATIVE_STATUS_OK);

  engine_native_net_events_t events{};
  assert(net_pump(server, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count == 1u);
  assert(events.events[0].kind == ENGINE_NATIVE_NET_EVENT_KIND_CONNECTED);
  assert(events.events[0].peer_id == 100u);

  assert(net_pump(client_a, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count == 1u);
  assert(events.events[0].kind == ENGINE_NATIVE_NET_EVENT_KIND_CONNECTED);
  assert(events.events[0].peer_id == 200u);

  assert(net_pump(client_b, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count == 1u);
  assert(events.events[0].kind == ENGINE_NATIVE_NET_EVENT_KIND_CONNECTED);
  assert(events.events[0].peer_id == 300u);

  uint8_t payload_a[3]{1u, 2u, 3u};
  engine_native_net_send_desc_t send_to_client_a{};
  send_to_client_a.peer_id = 200u;
  send_to_client_a.channel = 7u;
  send_to_client_a.payload = payload_a;
  send_to_client_a.payload_size = 3u;
  assert(net_send(server, &send_to_client_a) == ENGINE_NATIVE_STATUS_OK);

  assert(net_pump(client_a, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count == 1u);
  assert(events.events[0].kind == ENGINE_NATIVE_NET_EVENT_KIND_MESSAGE);
  assert(events.events[0].peer_id == 100u);
  assert(events.events[0].channel == 7u);
  assert(events.events[0].payload_size == 3u);
  assert(events.events[0].payload[0] == 1u);
  assert(events.events[0].payload[1] == 2u);
  assert(events.events[0].payload[2] == 3u);

  assert(net_pump(client_b, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count == 0u);

  uint8_t payload_b[2]{9u, 4u};
  engine_native_net_send_desc_t send_to_server{};
  send_to_server.peer_id = 100u;
  send_to_server.channel = 3u;
  send_to_server.payload = payload_b;
  send_to_server.payload_size = 2u;
  assert(net_send(client_a, &send_to_server) == ENGINE_NATIVE_STATUS_OK);

  assert(net_pump(server, &events) == ENGINE_NATIVE_STATUS_OK);
  assert(events.event_count == 1u);
  assert(events.events[0].kind == ENGINE_NATIVE_NET_EVENT_KIND_MESSAGE);
  assert(events.events[0].peer_id == 200u);
  assert(events.events[0].channel == 3u);
  assert(events.events[0].payload_size == 2u);
  assert(events.events[0].payload[0] == 9u);
  assert(events.events[0].payload[1] == 4u);

  assert(net_destroy(client_b) == ENGINE_NATIVE_STATUS_OK);
  assert(net_destroy(client_a) == ENGINE_NATIVE_STATUS_OK);
  assert(net_destroy(server) == ENGINE_NATIVE_STATUS_OK);
}

}  // namespace

int main() {
  TestEngineGetNetValidation();
  TestEngineNetSendAndPumpFlow();
  TestStandaloneNetCreateDestroyAndLimits();
  TestStandaloneNetWithoutLoopbackSuppressesMessages();
  TestStandaloneMultiPeerRouting();
  return 0;
}
