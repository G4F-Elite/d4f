#include "render/frame_graph_builder.h"

namespace dff::native::render {

namespace {

constexpr const char* kShadowPassName = "shadow";
constexpr const char* kPbrPassName = "pbr_opaque";
constexpr const char* kBloomPassName = "bloom";
constexpr const char* kTonemapPassName = "tonemap";
constexpr const char* kUiPassName = "ui";
constexpr const char* kPresentPassName = "present";
constexpr const char* kShadowMapResourceName = "shadow_map";
constexpr const char* kHdrColorResourceName = "hdr_color";
constexpr const char* kBloomColorResourceName = "bloom_color";
constexpr const char* kLdrColorResourceName = "ldr_color";

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

}  // namespace

engine_native_status_t BuildCanonicalFrameGraph(const FrameGraphBuildConfig& config,
                                                RenderGraph* graph,
                                                FrameGraphBuildOutput* output,
                                                std::string* out_error) {
  if (graph == nullptr || output == nullptr) {
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
  RenderPassId ui_pass = 0u;
  RenderPassId present_pass = 0u;

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

    status = graph->AddWrite(tonemap_pass, kLdrColorResourceName);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }
  }

  if (config.has_ui) {
    status = AddPass(graph, output, kUiPassName, rhi::RhiDevice::PassKind::kUiOverlay,
                     &ui_pass);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }

    if (config.has_draws) {
      status = graph->AddRead(ui_pass, kLdrColorResourceName);
      if (status != ENGINE_NATIVE_STATUS_OK) {
        return status;
      }
    }

    status = graph->AddWrite(ui_pass, kLdrColorResourceName);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }
  }

  status = AddPass(graph, output, kPresentPassName, rhi::RhiDevice::PassKind::kPresent,
                   &present_pass);
  if (status != ENGINE_NATIVE_STATUS_OK) {
    return status;
  }

  if (config.has_draws || config.has_ui) {
    status = graph->AddRead(present_pass, kLdrColorResourceName);
    if (status != ENGINE_NATIVE_STATUS_OK) {
      return status;
    }
  }

  return graph->Compile(&output->pass_order, out_error);
}

}  // namespace dff::native::render
