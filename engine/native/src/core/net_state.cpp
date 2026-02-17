#include "core/net_state.h"

#include <algorithm>
#include <cstddef>
#include <new>
#include <utility>

namespace dff::native {

namespace {

constexpr uint32_t kDefaultLocalPeerId = 1u;
constexpr uint32_t kDefaultMaxEventsPerPump = 1024u;
constexpr uint32_t kDefaultMaxPayloadBytes = 64u * 1024u;

bool IsValidEventKind(uint8_t kind) {
  return kind == ENGINE_NATIVE_NET_EVENT_KIND_CONNECTED ||
         kind == ENGINE_NATIVE_NET_EVENT_KIND_DISCONNECTED ||
         kind == ENGINE_NATIVE_NET_EVENT_KIND_MESSAGE;
}

}  // namespace

NetState::NetState() {
  engine_native_net_desc_t default_desc{};
  default_desc.local_peer_id = kDefaultLocalPeerId;
  default_desc.max_events_per_pump = kDefaultMaxEventsPerPump;
  default_desc.max_payload_bytes = kDefaultMaxPayloadBytes;
  default_desc.loopback_enabled = 1u;
  default_desc.reserved0 = 0u;
  default_desc.reserved1 = 0u;
  default_desc.reserved2 = 0u;

  static_cast<void>(Configure(default_desc));
}

engine_native_status_t NetState::Configure(const engine_native_net_desc_t& desc) {
  if (desc.local_peer_id == 0u ||
      desc.max_events_per_pump == 0u ||
      desc.max_payload_bytes == 0u ||
      desc.loopback_enabled > 1u) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  local_peer_id_ = desc.local_peer_id;
  max_events_per_pump_ = desc.max_events_per_pump;
  max_payload_bytes_ = desc.max_payload_bytes;
  loopback_enabled_ = desc.loopback_enabled != 0u;
  pending_events_.clear();
  active_events_.clear();
  pump_events_view_.clear();

  return QueueEvent(
      ENGINE_NATIVE_NET_EVENT_KIND_CONNECTED,
      /*channel=*/0u,
      local_peer_id_,
      /*payload=*/nullptr,
      /*payload_size=*/0u);
}

engine_native_status_t NetState::Pump(engine_native_net_events_t* out_events) {
  if (out_events == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  out_events->events = nullptr;
  out_events->event_count = 0u;
  ResetPumpViews();

  if (pending_events_.empty()) {
    return ENGINE_NATIVE_STATUS_OK;
  }

  const uint32_t event_limit = std::min<uint32_t>(
      max_events_per_pump_, static_cast<uint32_t>(pending_events_.size()));
  try {
    active_events_.reserve(event_limit);
    for (uint32_t i = 0u; i < event_limit; ++i) {
      active_events_.push_back(std::move(pending_events_[i]));
    }
    pending_events_.erase(
        pending_events_.begin(),
        pending_events_.begin() + static_cast<ptrdiff_t>(event_limit));

    pump_events_view_.resize(event_limit);
  } catch (const std::bad_alloc&) {
    ResetPumpViews();
    return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
  }

  for (uint32_t i = 0u; i < event_limit; ++i) {
    const QueuedEvent& source = active_events_[i];
    engine_native_net_event_t& destination = pump_events_view_[i];
    destination.kind = source.kind;
    destination.channel = source.channel;
    destination.reserved0 = source.reserved0;
    destination.peer_id = source.peer_id;
    destination.payload = source.payload.empty() ? nullptr : source.payload.data();
    destination.payload_size = static_cast<uint32_t>(source.payload.size());
  }

  out_events->events = pump_events_view_.data();
  out_events->event_count = event_limit;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t NetState::Send(const engine_native_net_send_desc_t& send_desc) {
  if (send_desc.peer_id == 0u ||
      send_desc.payload_size > max_payload_bytes_ ||
      (send_desc.payload_size > 0u && send_desc.payload == nullptr)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (!loopback_enabled_) {
    return ENGINE_NATIVE_STATUS_OK;
  }

  return QueueEvent(
      ENGINE_NATIVE_NET_EVENT_KIND_MESSAGE,
      send_desc.channel,
      send_desc.peer_id,
      send_desc.payload,
      send_desc.payload_size);
}

engine_native_status_t NetState::QueueEvent(uint8_t kind,
                                            uint8_t channel,
                                            uint32_t peer_id,
                                            const uint8_t* payload,
                                            uint32_t payload_size) {
  if (!IsValidEventKind(kind) ||
      peer_id == 0u ||
      payload_size > max_payload_bytes_ ||
      (payload_size > 0u && payload == nullptr)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  QueuedEvent event;
  event.kind = kind;
  event.channel = channel;
  event.reserved0 = 0u;
  event.peer_id = peer_id;

  try {
    if (payload_size > 0u) {
      event.payload.assign(payload, payload + payload_size);
    }

    pending_events_.push_back(std::move(event));
  } catch (const std::bad_alloc&) {
    return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
  }

  return ENGINE_NATIVE_STATUS_OK;
}

void NetState::ResetPumpViews() {
  active_events_.clear();
  pump_events_view_.clear();
}

}  // namespace dff::native
