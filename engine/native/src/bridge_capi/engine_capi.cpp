#include "bridge_capi/bridge_state.h"

#include <new>

extern "C" {

engine_native_status_t engine_create(
    const engine_native_create_desc_t* create_desc,
    engine_native_engine_t** out_engine) {
  if (out_engine == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  *out_engine = nullptr;

  if (create_desc == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (create_desc->api_version != ENGINE_NATIVE_API_VERSION) {
    return ENGINE_NATIVE_STATUS_VERSION_MISMATCH;
  }

  engine_native_engine* engine = new (std::nothrow) engine_native_engine();
  if (engine == nullptr) {
    return ENGINE_NATIVE_STATUS_OUT_OF_MEMORY;
  }

  engine->renderer.state = &engine->state.renderer;
  engine->renderer.owner = engine;
  engine->physics.state = &engine->state.physics;
  engine->physics.owner = engine;

  *out_engine = engine;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t engine_destroy(engine_native_engine_t* engine) {
  if (engine == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  delete engine;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t engine_pump_events(engine_native_engine_t* engine,
                                          engine_native_input_snapshot_t* out_input,
                                          engine_native_window_events_t* out_events) {
  if (engine == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return engine->state.platform.PumpEvents(out_input, out_events);
}

engine_native_status_t engine_get_renderer(engine_native_engine_t* engine,
                                           engine_native_renderer_t** out_renderer) {
  if (engine == nullptr || out_renderer == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_renderer = &engine->renderer;
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t engine_get_physics(engine_native_engine_t* engine,
                                          engine_native_physics_t** out_physics) {
  if (engine == nullptr || out_physics == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_physics = &engine->physics;
  return ENGINE_NATIVE_STATUS_OK;
}

}  // extern "C"
