#include "bridge_capi/bridge_state.h"
#include "bridge_capi/handle_registry.h"

#include <new>

namespace {

engine_native_status_t ValidateNet(engine_native_net_t* net) {
  if (net == nullptr || net->state == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (net->owner != nullptr) {
    if (net != &net->owner->net || net->owned_state != nullptr) {
      return ENGINE_NATIVE_STATUS_INVALID_STATE;
    }
    return ENGINE_NATIVE_STATUS_OK;
  }

  if (net->owned_state == nullptr || net->owned_state != net->state) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  return ENGINE_NATIVE_STATUS_OK;
}

}  // namespace

extern "C" {

engine_native_status_t net_create(const engine_native_net_desc_t* desc,
                                  engine_native_net_t** out_net) {
  if (desc == nullptr || out_net == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_net = nullptr;

  engine_native_net* net = new (std::nothrow) engine_native_net();
  if (net == nullptr) {
    return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
  }

  dff::native::NetState* state = new (std::nothrow) dff::native::NetState();
  if (state == nullptr) {
    delete net;
    return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
  }

  const engine_native_status_t configure_status = state->Configure(*desc);
  if (configure_status != ENGINE_NATIVE_STATUS_OK) {
    delete state;
    delete net;
    return configure_status;
  }

  net->state = state;
  net->owner = nullptr;
  net->owned_state = state;
  *out_net = net;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t net_destroy(engine_native_net_t* net) {
  if (net == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (net->owner != nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }
  if (net->owned_state == nullptr || net->owned_state != net->state) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  dff::native::bridge::UnregisterNetHandle(net);
  delete net->owned_state;
  net->owned_state = nullptr;
  net->state = nullptr;
  delete net;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t net_pump(engine_native_net_t* net,
                                engine_native_net_events_t* out_events) {
  const engine_native_status_t validation_status = ValidateNet(net);
  if (validation_status != ENGINE_NATIVE_STATUS_OK) {
    return validation_status;
  }
  if (out_events == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return net->state->Pump(out_events);
}

engine_native_status_t net_send(engine_native_net_t* net,
                                const engine_native_net_send_desc_t* send_desc) {
  const engine_native_status_t validation_status = ValidateNet(net);
  if (validation_status != ENGINE_NATIVE_STATUS_OK) {
    return validation_status;
  }
  if (send_desc == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return net->state->Send(*send_desc);
}

}  // extern "C"
