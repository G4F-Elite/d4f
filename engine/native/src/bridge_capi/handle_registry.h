#ifndef DFF_ENGINE_NATIVE_HANDLE_REGISTRY_H
#define DFF_ENGINE_NATIVE_HANDLE_REGISTRY_H

#include "engine_native.h"

namespace dff::native::bridge {

engine_native_status_t RegisterEngineHandle(
    engine_native_engine_t* engine,
    engine_native_engine_handle_t* out_handle);

engine_native_status_t ResolveEngineHandle(
    engine_native_engine_handle_t handle,
    engine_native_engine_t** out_engine);

void UnregisterEngineHandle(engine_native_engine_t* engine);

engine_native_status_t RegisterRendererHandle(
    engine_native_renderer_t* renderer,
    engine_native_engine_t* owner,
    engine_native_renderer_handle_t* out_handle);

engine_native_status_t ResolveRendererHandle(
    engine_native_renderer_handle_t handle,
    engine_native_renderer_t** out_renderer);

void UnregisterRendererHandle(engine_native_renderer_t* renderer);

engine_native_status_t RegisterPhysicsHandle(
    engine_native_physics_t* physics,
    engine_native_engine_t* owner,
    engine_native_physics_handle_t* out_handle);

engine_native_status_t ResolvePhysicsHandle(
    engine_native_physics_handle_t handle,
    engine_native_physics_t** out_physics);

void UnregisterPhysicsHandle(engine_native_physics_t* physics);

engine_native_status_t RegisterAudioHandle(
    engine_native_audio_t* audio,
    engine_native_engine_t* owner,
    engine_native_audio_handle_t* out_handle);

engine_native_status_t ResolveAudioHandle(
    engine_native_audio_handle_t handle,
    engine_native_audio_t** out_audio);

void UnregisterAudioHandle(engine_native_audio_t* audio);

engine_native_status_t RegisterNetHandle(
    engine_native_net_t* net,
    engine_native_engine_t* owner,
    bool owns_state,
    engine_native_net_handle_t* out_handle);

engine_native_status_t ResolveNetHandle(
    engine_native_net_handle_t handle,
    engine_native_net_t** out_net,
    engine_native_engine_t** out_owner,
    bool* out_owns_state);

void UnregisterNetHandle(engine_native_net_t* net);

void UnregisterOwnedSubsystemHandles(engine_native_engine_t* owner);

}  // namespace dff::native::bridge

#endif
