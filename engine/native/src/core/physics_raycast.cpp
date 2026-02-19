#include "core/engine_state.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <limits>

namespace dff::native {

namespace {

constexpr uint8_t kColliderShapeBox = 0u;
constexpr uint8_t kColliderShapeSphere = 1u;
constexpr uint8_t kColliderShapeCapsule = 2u;
constexpr float kEpsilon = 0.00001f;

std::array<float, 3> Add(const std::array<float, 3>& lhs,
                         const std::array<float, 3>& rhs) {
  return {lhs[0] + rhs[0], lhs[1] + rhs[1], lhs[2] + rhs[2]};
}

std::array<float, 3> Subtract(const std::array<float, 3>& lhs,
                              const std::array<float, 3>& rhs) {
  return {lhs[0] - rhs[0], lhs[1] - rhs[1], lhs[2] - rhs[2]};
}

std::array<float, 3> Scale(const std::array<float, 3>& value, float scalar) {
  return {value[0] * scalar, value[1] * scalar, value[2] * scalar};
}

float Dot(const std::array<float, 3>& lhs, const std::array<float, 3>& rhs) {
  return lhs[0] * rhs[0] + lhs[1] * rhs[1] + lhs[2] * rhs[2];
}

float Length(const std::array<float, 3>& value) {
  return std::sqrt(Dot(value, value));
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

std::array<float, 3> ComputeAabbNormal(const std::array<float, 3>& point,
                                       const std::array<float, 3>& center,
                                       const std::array<float, 3>& extents) {
  const std::array<float, 3> local = Subtract(point, center);
  float best_axis_value = -1.0f;
  size_t best_axis = 0u;

  for (size_t axis = 0u; axis < 3u; ++axis) {
    if (extents[axis] <= kEpsilon) {
      continue;
    }

    const float axis_value = std::fabs(local[axis] / extents[axis]);
    if (axis_value > best_axis_value) {
      best_axis_value = axis_value;
      best_axis = axis;
    }
  }

  std::array<float, 3> normal{0.0f, 0.0f, 0.0f};
  normal[best_axis] = local[best_axis] >= 0.0f ? 1.0f : -1.0f;
  return normal;
}

bool RayIntersectsAabb(const std::array<float, 3>& origin,
                       const std::array<float, 3>& direction,
                       const std::array<float, 3>& min_bounds,
                       const std::array<float, 3>& max_bounds,
                       float max_distance,
                       float* out_distance) {
  float t_min = 0.0f;
  float t_max = max_distance;

  for (size_t axis = 0u; axis < 3u; ++axis) {
    const float dir = direction[axis];
    if (std::fabs(dir) <= kEpsilon) {
      if (origin[axis] < min_bounds[axis] || origin[axis] > max_bounds[axis]) {
        return false;
      }
      continue;
    }

    const float inverse = 1.0f / dir;
    float t0 = (min_bounds[axis] - origin[axis]) * inverse;
    float t1 = (max_bounds[axis] - origin[axis]) * inverse;
    if (t0 > t1) {
      std::swap(t0, t1);
    }

    t_min = std::max(t_min, t0);
    t_max = std::min(t_max, t1);
    if (t_min > t_max) {
      return false;
    }
  }

  *out_distance = t_min;
  return true;
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

bool RayIntersectsVerticalCapsule(const std::array<float, 3>& origin,
                                  const std::array<float, 3>& direction,
                                  const std::array<float, 3>& center,
                                  float radius,
                                  float half_height,
                                  float max_distance,
                                  float* out_distance,
                                  std::array<float, 3>* out_normal) {
  if (out_distance == nullptr || out_normal == nullptr || radius <= 0.0f ||
      half_height <= 0.0f || max_distance <= 0.0f) {
    return false;
  }

  bool found = false;
  float best_distance = max_distance;
  std::array<float, 3> best_normal{0.0f, 1.0f, 0.0f};

  const std::array<float, 3> local_origin = Subtract(origin, center);
  const float half_cylinder = std::max(0.0f, half_height - radius);

  const float a = direction[0] * direction[0] + direction[2] * direction[2];
  if (a > kEpsilon) {
    const float b = 2.0f * (local_origin[0] * direction[0] +
                            local_origin[2] * direction[2]);
    const float c = local_origin[0] * local_origin[0] +
                    local_origin[2] * local_origin[2] - radius * radius;
    const float discriminant = b * b - 4.0f * a * c;
    if (discriminant >= 0.0f) {
      const float sqrt_discriminant = std::sqrt(discriminant);
      const float inv_denominator = 0.5f / a;
      const float roots[2] = {
          (-b - sqrt_discriminant) * inv_denominator,
          (-b + sqrt_discriminant) * inv_denominator,
      };

      for (float distance : roots) {
        if (distance < 0.0f || distance > best_distance) {
          continue;
        }

        const float y = local_origin[1] + direction[1] * distance;
        if (y < -half_cylinder || y > half_cylinder) {
          continue;
        }

        const std::array<float, 3> local_hit =
            Add(local_origin, Scale(direction, distance));
        std::array<float, 3> normal{local_hit[0], 0.0f, local_hit[2]};
        if (!Normalize(normal, &normal)) {
          normal = {0.0f, 1.0f, 0.0f};
        }

        found = true;
        best_distance = distance;
        best_normal = normal;
      }
    }
  }

  const std::array<float, 3> cap_offsets[2] = {
      {0.0f, half_cylinder, 0.0f},
      {0.0f, -half_cylinder, 0.0f},
  };
  for (const std::array<float, 3>& cap_offset : cap_offsets) {
    const std::array<float, 3> cap_center = Add(center, cap_offset);
    float distance = 0.0f;
    if (!RayIntersectsSphere(origin, direction, cap_center, radius,
                             max_distance, &distance) ||
        distance > best_distance) {
      continue;
    }

    const std::array<float, 3> hit_point = Add(origin, Scale(direction, distance));
    std::array<float, 3> normal = Subtract(hit_point, cap_center);
    if (!Normalize(normal, &normal)) {
      normal = {0.0f, 1.0f, 0.0f};
    }

    found = true;
    best_distance = distance;
    best_normal = normal;
  }

  if (!found) {
    return false;
  }

  *out_distance = best_distance;
  *out_normal = best_normal;
  return true;
}

}  // namespace

engine_native_status_t PhysicsState::Raycast(
    const engine_native_raycast_query_t& query,
    engine_native_raycast_hit_t* out_hit) const {
  if (out_hit == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  std::array<float, 3> origin{query.origin[0], query.origin[1], query.origin[2]};
  std::array<float, 3> direction{query.direction[0], query.direction[1],
                                 query.direction[2]};
  if (!IsFiniteVector3(origin) || !Normalize(direction, &direction) ||
      !std::isfinite(query.max_distance) ||
      query.max_distance <= 0.0f) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (query.include_triggers > 1u) {
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

  bool found_hit = false;
  float best_distance = query.max_distance;
  engine_native_resource_handle_t best_body = 0u;
  std::array<float, 3> best_point{0.0f, 0.0f, 0.0f};
  std::array<float, 3> best_normal{0.0f, 0.0f, 1.0f};
  uint8_t best_is_trigger = 0u;

  for (const auto& body_pair : bodies_) {
    const PhysicsBodyState& state = body_pair.second;
    if (query.include_triggers == 0u && state.is_trigger != 0u) {
      continue;
    }

    float hit_distance = 0.0f;
    bool has_hit = false;
    std::array<float, 3> hit_normal{0.0f, 0.0f, 1.0f};

    switch (state.collider_shape) {
      case kColliderShapeBox: {
        const std::array<float, 3> extents = Scale(state.collider_dimensions, 0.5f);
        const std::array<float, 3> min_bounds = Subtract(state.position, extents);
        const std::array<float, 3> max_bounds = Add(state.position, extents);
        has_hit = RayIntersectsAabb(origin, direction, min_bounds, max_bounds,
                                    query.max_distance, &hit_distance);
        if (has_hit) {
          const std::array<float, 3> point =
              Add(origin, Scale(direction, hit_distance));
          hit_normal = ComputeAabbNormal(point, state.position, extents);
        }
        break;
      }

      case kColliderShapeSphere: {
        const float radius = state.collider_dimensions[0] * 0.5f;
        has_hit = RayIntersectsSphere(origin, direction, state.position, radius,
                                      query.max_distance, &hit_distance);
        if (has_hit) {
          const std::array<float, 3> point =
              Add(origin, Scale(direction, hit_distance));
          std::array<float, 3> normal = Subtract(point, state.position);
          if (!Normalize(normal, &normal)) {
            normal = {0.0f, 1.0f, 0.0f};
          }
          hit_normal = normal;
        }
        break;
      }

      case kColliderShapeCapsule: {
        const float radius = state.collider_dimensions[0] * 0.5f;
        const float half_height = state.collider_dimensions[1] * 0.5f;
        has_hit = RayIntersectsVerticalCapsule(origin,
                                               direction,
                                               state.position,
                                               radius,
                                               half_height,
                                               query.max_distance,
                                               &hit_distance,
                                               &hit_normal);
        break;
      }

      default:
        continue;
    }

    if (!has_hit || hit_distance > best_distance) {
      continue;
    }

    found_hit = true;
    best_distance = hit_distance;
    best_body = body_pair.first;
    best_point = Add(origin, Scale(direction, hit_distance));
    best_normal = hit_normal;
    best_is_trigger = state.is_trigger;
  }

  if (!found_hit) {
    return ENGINE_NATIVE_STATUS_OK;
  }

  out_hit->has_hit = 1u;
  out_hit->is_trigger = best_is_trigger;
  out_hit->body = best_body;
  out_hit->distance = best_distance;
  std::copy_n(best_point.data(), 3u, out_hit->point);
  std::copy_n(best_normal.data(), 3u, out_hit->normal);
  return ENGINE_NATIVE_STATUS_OK;
}

}  // namespace dff::native
