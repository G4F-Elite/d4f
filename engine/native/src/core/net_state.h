#ifndef DFF_ENGINE_NATIVE_NET_STATE_H
#define DFF_ENGINE_NATIVE_NET_STATE_H

#include <stdint.h>

#include <vector>

#include "engine_native.h"

namespace dff::native {

class NetState {
 public:
  NetState();

  engine_native_status_t Configure(const engine_native_net_desc_t& desc);
  engine_native_status_t Pump(engine_native_net_events_t* out_events);
  engine_native_status_t Send(const engine_native_net_send_desc_t& send_desc);

  uint32_t local_peer_id() const { return local_peer_id_; }
  uint32_t max_events_per_pump() const { return max_events_per_pump_; }
  uint32_t max_payload_bytes() const { return max_payload_bytes_; }
  bool loopback_enabled() const { return loopback_enabled_; }
  size_t pending_event_count() const { return pending_events_.size(); }

 private:
  struct QueuedEvent {
    uint8_t kind = ENGINE_NATIVE_NET_EVENT_KIND_MESSAGE;
    uint8_t channel = 0u;
    uint16_t reserved0 = 0u;
    uint32_t peer_id = 0u;
    std::vector<uint8_t> payload;
  };

  engine_native_status_t QueueEvent(uint8_t kind,
                                    uint8_t channel,
                                    uint32_t peer_id,
                                    const uint8_t* payload,
                                    uint32_t payload_size);
  void ResetPumpViews();

  uint32_t local_peer_id_ = 1u;
  uint32_t max_events_per_pump_ = 1024u;
  uint32_t max_payload_bytes_ = 64u * 1024u;
  bool loopback_enabled_ = true;
  std::vector<QueuedEvent> pending_events_;
  std::vector<QueuedEvent> active_events_;
  std::vector<engine_native_net_event_t> pump_events_view_;
};

}  // namespace dff::native

#endif
