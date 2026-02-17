#ifndef DFF_ENGINE_NATIVE_BRIDGE_STATE_H
#define DFF_ENGINE_NATIVE_BRIDGE_STATE_H

#include "core/engine_state.h"

struct engine_native_engine;

struct engine_native_renderer {
  dff::native::RendererState* state = nullptr;
  engine_native_engine* owner = nullptr;
};

struct engine_native_physics {
  dff::native::PhysicsState* state = nullptr;
  engine_native_engine* owner = nullptr;
};

struct engine_native_audio {
  dff::native::AudioState* state = nullptr;
  engine_native_engine* owner = nullptr;
};

struct engine_native_engine {
  dff::native::EngineState state;
  engine_native_renderer renderer;
  engine_native_physics physics;
  engine_native_audio audio;
};

#endif
