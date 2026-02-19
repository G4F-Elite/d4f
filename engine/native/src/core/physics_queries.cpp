#include "core/engine_state.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <vector>

namespace dff::native {

namespace {

constexpr uint8_t kColliderShapeBox = 0u;
constexpr uint8_t kColliderShapeSphere = 1u;
constexpr uint8_t kColliderShapeCapsule = 2u;
constexpr float kEpsilon = 0.00001f;
constexpr float kDistanceTieEpsilon = 0.00001f;

float Dot(const std::array<float, 3>& lhs, const std::array<float, 3>& rhs) {
  return lhs[0] * rhs[0] + lhs[1] * rhs[1] + lhs[2] * rhs[2];
}

float Length(const std::array<float, 3>& value) {
  return std::sqrt(Dot(value, value));
}

std::array<float, 3> Scale(const std::array<float, 3>& value, float scalar) {
  return {value[0] * scalar, value[1] * scalar, value[2] * scalar};
}

std::array<float, 3> Add(const std::array<float, 3>& lhs,
                         const std::array<float, 3>& rhs) {
  return {lhs[0] + rhs[0], lhs[1] + rhs[1], lhs[2] + rhs[2]};
}

std::array<float, 3> Subtract(const std::array<float, 3>& lhs,
                              const std::array<float, 3>& rhs) {
  return {lhs[0] - rhs[0], lhs[1] - rhs[1], lhs[2] - rhs[2]};
}

bool Normalize(const std::array<float, 3>& source, std::array<float, 3>* out) {
  const float length = Length(source);
  if (!std::isfinite(length) || length <= kEpsilon) {
    return false;
  }

  *out = Scale(source, 1.0f / length);
  return true;
}

bool IsFiniteVector3(const std::array<float, 3>& value) {
  return std::isfinite(value[0]) && std::isfinite(value[1]) &&
         std::isfinite(value[2]);
}

bool IsSupportedShape(uint8_t shape) {
  return shape == kColliderShapeBox || shape == kColliderShapeSphere ||
         shape == kColliderShapeCapsule;
}

bool IsValidShapeDimensions(uint8_t shape, const std::array<float, 3>& dimensions) {
  if (!IsFiniteVector3(dimensions)) {
    return false;
  }

  if (dimensions[0] <= 0.0f || dimensions[1] <= 0.0f || dimensions[2] <= 0.0f) {
    return false;
  }

  if (shape == kColliderShapeSphere) {
    return dimensions[0] == dimensions[1] && dimensions[1] == dimensions[2];
  }

  if (shape == kColliderShapeCapsule) {
    return dimensions[1] > dimensions[0] * 2.0f;
  }

  return true;
}

float BoundingSphereRadius(uint8_t shape, const std::array<float, 3>& dimensions) {
  switch (shape) {
    case kColliderShapeBox: {
      const std::array<float, 3> half = Scale(dimensions, 0.5f);
      return Length(half);
    }
    case kColliderShapeSphere:
      return dimensions[0] * 0.5f;
    case kColliderShapeCapsule:
      return std::max(dimensions[0] * 0.5f, dimensions[1] * 0.5f);
    default:
      return 0.0f;
  }
}

bool RayIntersectsSphere(const std::array<float, 3>& origin,
                         const std::array<float, 3>& direction,
                         const std::array<float, 3>& center,
                         float radius,
                         float max_distance,
                         float* out_distance) {
  const std::array<float, 3> offset = Subtract(origin, center);
  const float b = Dot(offset, direction);
  const float c = Dot(offset, offset) - radius * radius;
  const float discriminant = b * b - c;
  if (discriminant < 0.0f) {
    return false;
  }

  const float sqrt_discriminant = std::sqrt(discriminant);
  float distance = -b - sqrt_discriminant;
  if (distance < 0.0f) {
    distance = -b + sqrt_discriminant;
  }

  if (distance < 0.0f || distance > max_distance) {
    return false;
  }

  *out_distance = distance;
  return true;
}

}  // namespace

engine_native_status_t PhysicsState::Sweep(const engine_native_sweep_query_t& query,
                                           engine_native_sweep_hit_t* out_hit) const {
  if (out_hit == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const std::array<float, 3> origin{query.origin[0], query.origin[1], query.origin[2]};
  std::array<float, 3> direction{query.direction[0], query.direction[1],
                                 query.direction[2]};
  const std::array<float, 3> shape_dimensions{
      query.shape_dimensions[0], query.shape_dimensions[1], query.shape_dimensions[2]};

  if (!IsFiniteVector3(origin) || !Normalize(direction, &direction) ||
      !std::isfinite(query.max_distance) ||
      query.max_distance <= 0.0f || query.include_triggers > 1u ||
      !IsSupportedShape(query.shape_type) ||
      !IsValidShapeDimensions(query.shape_type, shape_dimensions)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  out_hit->has_hit = 0u;
  out_hit->is_trigger = 0u;
  out_hit->reserved0 = 0u;
  out_hit->reserved1 = 0u;
  out_hit->body = 0u;
  out_hit->distance = 0.0f;
  out_hit->point[0] = 0.0f;
  out_hit->point[1] = 0.0f;
  out_hit->point[2] = 0.0f;
  out_hit->normal[0] = 0.0f;
  out_hit->normal[1] = 0.0f;
  out_hit->normal[2] = 1.0f;

  const float query_radius = BoundingSphereRadius(query.shape_type, shape_dimensions);

  bool found = false;
  float best_distance = query.max_distance;
  engine_native_resource_handle_t best_body = 0u;
  std::array<float, 3> best_point{0.0f, 0.0f, 0.0f};
  std::array<float, 3> best_normal{0.0f, 0.0f, 1.0f};
  uint8_t best_trigger = 0u;

  for (const auto& body_pair : bodies_) {
    const PhysicsBodyState& body = body_pair.second;
    if (query.include_triggers == 0u && body.is_trigger != 0u) {
      continue;
    }

    const float body_radius =
        BoundingSphereRadius(body.collider_shape, body.collider_dimensions);
    float distance = 0.0f;
    if (!RayIntersectsSphere(origin, direction, body.position,
                             query_radius + body_radius,
                             query.max_distance, &distance)) {
      continue;
    }

    const bool has_better_distance =
        !found || (distance + kDistanceTieEpsilon) < best_distance;
    const bool is_distance_tie =
        found && std::fabs(distance - best_distance) <= kDistanceTieEpsilon;
    const bool has_smaller_body = is_distance_tie && body_pair.first < best_body;
    if (!has_better_distance && !has_smaller_body) {
      continue;
    }

    const std::array<float, 3> center_at_hit = Add(origin, Scale(direction, distance));
    std::array<float, 3> normal = Subtract(center_at_hit, body.position);
    if (!Normalize(normal, &normal)) {
      normal = Scale(direction, -1.0f);
    }

    found = true;
    best_distance = distance;
    best_body = body_pair.first;
    best_trigger = body.is_trigger;
    best_normal = normal;
    best_point = Subtract(center_at_hit, Scale(normal, query_radius));
  }

  if (!found) {
    return ENGINE_NATIVE_STATUS_OK;
  }

  out_hit->has_hit = 1u;
  out_hit->is_trigger = best_trigger;
  out_hit->body = best_body;
  out_hit->distance = best_distance;
  std::copy_n(best_point.data(), 3u, out_hit->point);
  std::copy_n(best_normal.data(), 3u, out_hit->normal);
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t PhysicsState::Overlap(const engine_native_overlap_query_t& query,
                                             engine_native_overlap_hit_t* hits,
                                             uint32_t hit_capacity,
                                             uint32_t* out_hit_count) const {
  if (out_hit_count == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  *out_hit_count = 0u;
  if (hit_capacity > 0u && hits == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const std::array<float, 3> center{query.center[0], query.center[1], query.center[2]};
  const std::array<float, 3> shape_dimensions{
      query.shape_dimensions[0], query.shape_dimensions[1], query.shape_dimensions[2]};

  if (!IsFiniteVector3(center) || query.include_triggers > 1u ||
      !IsSupportedShape(query.shape_type) ||
      !IsValidShapeDimensions(query.shape_type, shape_dimensions)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  const float query_radius = BoundingSphereRadius(query.shape_type, shape_dimensions);
  struct OverlapCandidate {
    engine_native_resource_handle_t body = 0u;
    uint8_t is_trigger = 0u;
  };
  std::vector<OverlapCandidate> overlaps;
  overlaps.reserve(bodies_.size());

  for (const auto& body_pair : bodies_) {
    const PhysicsBodyState& body = body_pair.second;
    if (query.include_triggers == 0u && body.is_trigger != 0u) {
      continue;
    }

    const float body_radius =
        BoundingSphereRadius(body.collider_shape, body.collider_dimensions);
    const std::array<float, 3> delta = Subtract(body.position, center);
    if (Length(delta) > query_radius + body_radius) {
      continue;
    }

    overlaps.push_back({body_pair.first, body.is_trigger});
  }

  std::sort(overlaps.begin(), overlaps.end(),
            [](const OverlapCandidate& lhs, const OverlapCandidate& rhs) {
              return lhs.body < rhs.body;
            });

  const uint32_t written =
      std::min(hit_capacity, static_cast<uint32_t>(overlaps.size()));
  for (uint32_t i = 0u; i < written; ++i) {
    engine_native_overlap_hit_t& hit = hits[i];
    hit.body = overlaps[i].body;
    hit.is_trigger = overlaps[i].is_trigger;
    hit.reserved0 = 0u;
    hit.reserved1 = 0u;
    hit.reserved2 = 0u;
  }

  *out_hit_count = written;
  return ENGINE_NATIVE_STATUS_OK;
}

}  // namespace dff::native
