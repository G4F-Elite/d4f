#include "render/frame_graph_builder.h"

namespace dff::native::render {

namespace {

constexpr const char* kShadowPassName = "shadow";
constexpr const char* kPbrPassName = "pbr_opaque";
constexpr const char* kBloomPassName = "bloom";
constexpr const char* kTonemapPassName = "tonemap";
constexpr const char* kColorGradingPassName = "color_grading";
constexpr const char* kFxaaPassName = "fxaa";
constexpr const char* kDebugDepthPassName = "debug_depth";
constexpr const char* kDebugNormalsPassName = "debug_normals";
constexpr const char* kDebugAlbedoPassName = "debug_albedo";
constexpr const char* kUiPassName = "ui";
constexpr const char* kPresentPassName = "present";
constexpr const char* kShadowMapResourceName = "shadow_map";
constexpr const char* kHdrColorResourceName = "hdr_color";
constexpr const char* kDepthResourceName = "scene_depth";
constexpr const char* kNormalsResourceName = "scene_normals";
constexpr const char* kAlbedoResourceName = "scene_albedo";
constexpr const char* kBloomColorResourceName = "bloom_color";
constexpr const char* kTonemappedColorResourceName = "tonemapped_ldr_color";
constexpr const char* kLdrColorResourceName = "ldr_color";
constexpr const char* kFxaaColorResourceName = "fxaa_ldr_color";
constexpr const char* kDebugColorResourceName = "debug_ldr_color";

engine_native_status_t AddPass(RenderGraph* graph,
                               FrameGraphBuildOutput* output,
                               const char* pass_name,
                               rhi::RhiDevice::PassKind pass_kind,
                               RenderPassId* out_pass_id) {
  if (graph == nullptr || output == nullptr || pass_name == nullptr ||
      out_pass_id == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  engine_native_status_t status = graph->AddPass(pass_name, out_pass_id);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  const size_t pass_index = static_cast<size_t>(*out_pass_id);
  if (pass_index >= output->pass_kinds_by_id.size()) {
    output->pass_kinds_by_id.resize(pass_index + 1u,
                                    rhi::RhiDevice::PassKind::kPresent);
  }

  output->pass_kinds_by_id[pass_index] = pass_kind;
  return ENGINE_NATIVE_STATUS_OK;
}

bool IsSupportedDebugViewMode(engine_native_debug_view_mode_t mode) {
  return mode == ENGINE_NATIVE_DEBUG_VIEW_NONE ||
         mode == ENGINE_NATIVE_DEBUG_VIEW_DEPTH ||
         mode == ENGINE_NATIVE_DEBUG_VIEW_NORMALS ||
         mode == ENGINE_NATIVE_DEBUG_VIEW_ALBEDO;
}

engine_native_status_t AddDebugViewPass(engine_native_debug_view_mode_t mode,
                                        RenderGraph* graph,
                                        FrameGraphBuildOutput* output,
                                        RenderPassId* out_pass_id,
                                        const char** out_resource_name) {
  if (graph == nullptr || output == nullptr || out_pass_id == nullptr ||
      out_resource_name == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  switch (mode) {
    case ENGINE_NATIVE_DEBUG_VIEW_DEPTH:
      *out_resource_name = kDepthResourceName;
      return AddPass(graph, output, kDebugDepthPassName,
                     rhi::RhiDevice::PassKind::kDebugDepth, out_pass_id);
    case ENGINE_NATIVE_DEBUG_VIEW_NORMALS:
      *out_resource_name = kNormalsResourceName;
      return AddPass(graph, output, kDebugNormalsPassName,
                     rhi::RhiDevice::PassKind::kDebugNormals, out_pass_id);
    case ENGINE_NATIVE_DEBUG_VIEW_ALBEDO:
      *out_resource_name = kAlbedoResourceName;
      return AddPass(graph, output, kDebugAlbedoPassName,
                     rhi::RhiDevice::PassKind::kDebugAlbedo, out_pass_id);
    case ENGINE_NATIVE_DEBUG_VIEW_NONE:
      break;
  }

  return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
}

}  // namespace

engine_native_status_t BuildCanonicalFrameGraph(const FrameGraphBuildConfig& config,
                                                RenderGraph* graph,
                                                FrameGraphBuildOutput* output,
                                                std::string* out_error) {
  if (graph == nullptr || output == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (!IsSupportedDebugViewMode(config.debug_view_mode)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }
  if (config.debug_view_mode != ENGINE_NATIVE_DEBUG_VIEW_NONE &&
      !config.has_draws) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  graph->Clear();
  output->pass_order.clear();
  output->pass_kinds_by_id.clear();
  if (out_error != nullptr) {
    out_error->clear();
  }

  RenderPassId shadow_pass = 0u;
  RenderPassId pbr_pass = 0u;
  RenderPassId bloom_pass = 0u;
  RenderPassId tonemap_pass = 0u;
  RenderPassId color_grading_pass = 0u;
  RenderPassId fxaa_pass = 0u;
  RenderPassId debug_view_pass = 0u;
  RenderPassId ui_pass = 0u;
  RenderPassId present_pass = 0u;
  const char* final_color_resource = nullptr;

  engine_native_status_t status = ENGINE_NATIVE_STATUS_OK;
  if (config.has_draws) {
    status = AddPass(graph, output, kShadowPassName, rhi::RhiDevice::PassKind::kShadowMap,
                     &shadow_pass);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    status = graph->AddWrite(shadow_pass, kShadowMapResourceName);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    status = AddPass(graph, output, kPbrPassName, rhi::RhiDevice::PassKind::kPbrOpaque,
                     &pbr_pass);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    status = graph->AddRead(pbr_pass, kShadowMapResourceName);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    status = graph->AddWrite(pbr_pass, kHdrColorResourceName);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    status = graph->AddWrite(pbr_pass, kDepthResourceName);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    status = graph->AddWrite(pbr_pass, kNormalsResourceName);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    status = graph->AddWrite(pbr_pass, kAlbedoResourceName);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    if (config.debug_view_mode == ENGINE_NATIVE_DEBUG_VIEW_NONE) {
      status = AddPass(graph, output, kBloomPassName, rhi::RhiDevice::PassKind::kBloom,
                       &bloom_pass);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      status = graph->AddRead(bloom_pass, kHdrColorResourceName);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      status = graph->AddWrite(bloom_pass, kBloomColorResourceName);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      status = AddPass(graph, output, kTonemapPassName, rhi::RhiDevice::PassKind::kTonemap,
                       &tonemap_pass);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      status = graph->AddRead(tonemap_pass, kBloomColorResourceName);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      status = graph->AddWrite(tonemap_pass, kTonemappedColorResourceName);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      status = AddPass(graph, output, kColorGradingPassName,
                       rhi::RhiDevice::PassKind::kColorGrading,
                       &color_grading_pass);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      status = graph->AddRead(color_grading_pass, kTonemappedColorResourceName);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      status = graph->AddWrite(color_grading_pass, kLdrColorResourceName);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      status = AddPass(graph, output, kFxaaPassName, rhi::RhiDevice::PassKind::kFxaa,
                       &fxaa_pass);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      status = graph->AddRead(fxaa_pass, kLdrColorResourceName);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      status = graph->AddWrite(fxaa_pass, kFxaaColorResourceName);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      final_color_resource = kFxaaColorResourceName;
    } else {
      const char* debug_input_resource = nullptr;
      status = AddDebugViewPass(
          config.debug_view_mode,
          graph,
          output,
          &debug_view_pass,
          &debug_input_resource);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      status = graph->AddRead(debug_view_pass, debug_input_resource);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      status = graph->AddWrite(debug_view_pass, kDebugColorResourceName);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }

      final_color_resource = kDebugColorResourceName;
    }
  }

  if (config.has_ui) {
    status = AddPass(graph, output, kUiPassName, rhi::RhiDevice::PassKind::kUiOverlay,
                     &ui_pass);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    if (config.has_draws) {
      status = graph->AddRead(ui_pass, final_color_resource);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }
      status = graph->AddWrite(ui_pass, final_color_resource);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }
    } else {
      status = graph->AddWrite(ui_pass, kLdrColorResourceName);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }
      final_color_resource = kLdrColorResourceName;
    }
  }

  status = AddPass(graph, output, kPresentPassName, rhi::RhiDevice::PassKind::kPresent,
                   &present_pass);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  if (config.has_draws || config.has_ui) {
    status = graph->AddRead(present_pass, final_color_resource);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }
  }

  return graph->Compile(&output->pass_order, out_error);
}

}  // namespace dff::native::render
