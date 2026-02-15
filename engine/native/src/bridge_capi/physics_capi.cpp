#include "bridge_capi/bridge_state.h"

namespace {

engine_native_status_t ValidatePhysics(engine_native_physics_t* physics) {
  if (physics == nullptr || physics->state == nullptr || physics->owner == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (physics != &physics->owner->physics) {
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  return ENGINE_NATIVE_STATUS_OK;
}

}  // namespace

extern "C" {

engine_native_status_t physics_step(engine_native_physics_t* physics,
                                    double dt_seconds) {
  const engine_native_status_t status = ValidatePhysics(physics);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return physics->state->Step(dt_seconds);
}

engine_native_status_t physics_sync_from_world(engine_native_physics_t* physics,
                                               const engine_native_body_write_t* writes,
                                               uint32_t write_count) {
  const engine_native_status_t status = ValidatePhysics(physics);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return physics->state->SyncFromWorld(writes, write_count);
}

engine_native_status_t physics_sync_to_world(engine_native_physics_t* physics,
                                             engine_native_body_read_t* reads,
                                             uint32_t read_capacity,
                                             uint32_t* out_read_count) {
  const engine_native_status_t status = ValidatePhysics(physics);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  return physics->state->SyncToWorld(reads, read_capacity, out_read_count);
}

engine_native_status_t physics_raycast(engine_native_physics_t* physics,
                                       const engine_native_raycast_query_t* query,
                                       engine_native_raycast_hit_t* out_hit) {
  const engine_native_status_t status = ValidatePhysics(physics);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }
  if (query == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  return physics->state->Raycast(*query, out_hit);
}

}  // extern "C"
