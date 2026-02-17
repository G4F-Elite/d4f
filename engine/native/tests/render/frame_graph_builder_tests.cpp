#include "render/frame_graph_builder_tests.h"

#include <assert.h>

#include <string>
#include <vector>

#include "render/frame_graph_builder.h"

namespace dff::native::tests {
namespace {

using dff::native::render::FrameGraphBuildConfig;
using dff::native::render::FrameGraphBuildOutput;
using dff::native::render::RenderGraph;
using PassKind = dff::native::rhi::RhiDevice::PassKind;

void AssertKindsOrder(const FrameGraphBuildOutput& output,
                      const std::vector<PassKind>& expected_order) {
  assert(output.pass_order.size() == expected_order.size());
  for (size_t i = 0u; i < expected_order.size(); ++i) {
    const size_t pass_id = static_cast<size_t>(output.pass_order[i]);
    assert(pass_id < output.pass_kinds_by_id.size());
    assert(output.pass_kinds_by_id[pass_id] == expected_order[i]);
  }
}

void TestBuildCanonicalFrameGraphCombinations() {
  RenderGraph graph;
  FrameGraphBuildOutput output;
  std::string error;

  assert(dff::native::render::BuildCanonicalFrameGraph(
             FrameGraphBuildConfig{.has_draws = true, .has_ui = false}, &graph,
             &output, &error) == ENGINE_NATIVE_STATUS_OK);
  assert(error.empty());
  AssertKindsOrder(
      output,
      {PassKind::kShadowMap, PassKind::kPbrOpaque, PassKind::kBloom,
       PassKind::kTonemap, PassKind::kColorGrading, PassKind::kFxaa,
       PassKind::kPresent});

  assert(dff::native::render::BuildCanonicalFrameGraph(
             FrameGraphBuildConfig{.has_draws = false, .has_ui = true}, &graph,
             &output, &error) == ENGINE_NATIVE_STATUS_OK);
  assert(error.empty());
  AssertKindsOrder(output, {PassKind::kUiOverlay, PassKind::kPresent});

  assert(dff::native::render::BuildCanonicalFrameGraph(
             FrameGraphBuildConfig{.has_draws = true, .has_ui = true}, &graph,
             &output, &error) == ENGINE_NATIVE_STATUS_OK);
  assert(error.empty());
  AssertKindsOrder(
      output,
      {PassKind::kShadowMap, PassKind::kPbrOpaque, PassKind::kBloom,
       PassKind::kTonemap, PassKind::kColorGrading, PassKind::kFxaa,
       PassKind::kUiOverlay, PassKind::kPresent});

  assert(dff::native::render::BuildCanonicalFrameGraph(
             FrameGraphBuildConfig{
                 .has_draws = true,
                 .has_ui = false,
                 .debug_view_mode = ENGINE_NATIVE_DEBUG_VIEW_DEPTH},
             &graph, &output, &error) == ENGINE_NATIVE_STATUS_OK);
  assert(error.empty());
  AssertKindsOrder(
      output,
      {PassKind::kShadowMap, PassKind::kPbrOpaque, PassKind::kDebugDepth,
       PassKind::kPresent});

  assert(dff::native::render::BuildCanonicalFrameGraph(
             FrameGraphBuildConfig{
                 .has_draws = true,
                 .has_ui = true,
                 .debug_view_mode = ENGINE_NATIVE_DEBUG_VIEW_NORMALS},
             &graph, &output, &error) == ENGINE_NATIVE_STATUS_OK);
  assert(error.empty());
  AssertKindsOrder(
      output,
      {PassKind::kShadowMap, PassKind::kPbrOpaque, PassKind::kDebugNormals,
       PassKind::kUiOverlay, PassKind::kPresent});

  assert(dff::native::render::BuildCanonicalFrameGraph(
             FrameGraphBuildConfig{.has_draws = false, .has_ui = false}, &graph,
             &output, &error) == ENGINE_NATIVE_STATUS_OK);
  assert(error.empty());
  AssertKindsOrder(output, {PassKind::kPresent});
}

void TestBuildCanonicalFrameGraphValidation() {
  RenderGraph graph;
  FrameGraphBuildOutput output;
  std::string error;

  assert(dff::native::render::BuildCanonicalFrameGraph(
             FrameGraphBuildConfig{}, nullptr, &output, &error) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(dff::native::render::BuildCanonicalFrameGraph(
             FrameGraphBuildConfig{}, &graph, nullptr, &error) ==
         ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(dff::native::render::BuildCanonicalFrameGraph(
             FrameGraphBuildConfig{
                 .has_draws = false,
                 .has_ui = true,
                 .debug_view_mode = ENGINE_NATIVE_DEBUG_VIEW_ALBEDO},
             &graph, &output, &error) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(dff::native::render::BuildCanonicalFrameGraph(
             FrameGraphBuildConfig{
                 .has_draws = true,
                 .has_ui = false,
                 .debug_view_mode =
                     static_cast<engine_native_debug_view_mode_t>(255u)},
             &graph, &output, &error) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
}

}  // namespace

void RunFrameGraphBuilderTests() {
  TestBuildCanonicalFrameGraphCombinations();
  TestBuildCanonicalFrameGraphValidation();
}

}  // namespace dff::native::tests
