#ifndef DFF_ENGINE_NATIVE_FRAME_GRAPH_BUILDER_H
#define DFF_ENGINE_NATIVE_FRAME_GRAPH_BUILDER_H

#include <string>
#include <vector>

#include "render/render_graph.h"
#include "rhi/rhi_device.h"

namespace dff::native::render {

struct FrameGraphBuildConfig {
  bool has_draws = false;
  bool has_ui = false;
};

struct FrameGraphBuildOutput {
  std::vector<RenderPassId> pass_order;
  std::vector<rhi::RhiDevice::PassKind> pass_kinds_by_id;
};

engine_native_status_t BuildCanonicalFrameGraph(const FrameGraphBuildConfig& config,
                                                RenderGraph* graph,
                                                FrameGraphBuildOutput* output,
                                                std::string* out_error);

}  // namespace dff::native::render

#endif
