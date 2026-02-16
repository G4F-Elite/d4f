#include "render/render_graph_tests.h"

#include <assert.h>

#include <string>
#include <vector>

#include "render/render_graph.h"

namespace dff::native::tests {
namespace {

using dff::native::render::RenderGraph;
using dff::native::render::RenderPassId;

void AssertOrder(const std::vector<RenderPassId>& actual,
                 const std::vector<RenderPassId>& expected) {
  assert(actual.size() == expected.size());
  for (size_t i = 0; i < expected.size(); ++i) {
    assert(actual[i] == expected[i]);
  }
}

void TestCompileBuildsOrderFromResourceHazards() {
  RenderGraph graph;
  RenderPassId gbuffer = 0;
  RenderPassId lighting = 0;
  RenderPassId post = 0;

  assert(graph.AddPass("gbuffer", &gbuffer) == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddPass("lighting", &lighting) == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddPass("post", &post) == ENGINE_NATIVE_STATUS_OK);

  assert(graph.AddWrite(gbuffer, "depth") == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddWrite(gbuffer, "albedo") == ENGINE_NATIVE_STATUS_OK);

  assert(graph.AddRead(lighting, "depth") == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddRead(lighting, "albedo") == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddWrite(lighting, "lit") == ENGINE_NATIVE_STATUS_OK);

  assert(graph.AddRead(post, "lit") == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddWrite(post, "swapchain") == ENGINE_NATIVE_STATUS_OK);

  std::vector<RenderPassId> order;
  std::string error;
  assert(graph.Compile(&order, &error) == ENGINE_NATIVE_STATUS_OK);
  assert(error.empty());
  AssertOrder(order, {gbuffer, lighting, post});
}

void TestCompileBuildsOrderFromExplicitDependencies() {
  RenderGraph graph;
  RenderPassId shadow = 0;
  RenderPassId opaque = 0;
  RenderPassId ui = 0;

  assert(graph.AddPass("shadow", &shadow) == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddPass("opaque", &opaque) == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddPass("ui", &ui) == ENGINE_NATIVE_STATUS_OK);

  assert(graph.AddDependency(shadow, opaque) == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddDependency(opaque, ui) == ENGINE_NATIVE_STATUS_OK);

  std::vector<RenderPassId> order;
  std::string error;
  assert(graph.Compile(&order, &error) == ENGINE_NATIVE_STATUS_OK);
  assert(error.empty());
  AssertOrder(order, {shadow, opaque, ui});
}

void TestCompileDetectsCycles() {
  RenderGraph graph;
  RenderPassId a = 0;
  RenderPassId b = 0;

  assert(graph.AddPass("a", &a) == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddPass("b", &b) == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddDependency(a, b) == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddDependency(b, a) == ENGINE_NATIVE_STATUS_OK);

  std::vector<RenderPassId> order;
  std::string error;
  assert(graph.Compile(&order, &error) == ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(order.empty());
  assert(!error.empty());
}

void TestCompileFailsOnUnknownReadResource() {
  RenderGraph graph;
  RenderPassId lighting = 0;

  assert(graph.AddPass("lighting", &lighting) == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddRead(lighting, "hdr_color") == ENGINE_NATIVE_STATUS_OK);

  std::vector<RenderPassId> order;
  std::string error;
  assert(graph.Compile(&order, &error) == ENGINE_NATIVE_STATUS_INVALID_STATE);
  assert(order.empty());
  assert(error.find("unknown resource") != std::string::npos);
}

void TestCompileAllowsImportedResources() {
  RenderGraph graph;
  RenderPassId post = 0;

  assert(graph.ImportResource("swapchain") == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddPass("post", &post) == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddRead(post, "swapchain") == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddWrite(post, "swapchain") == ENGINE_NATIVE_STATUS_OK);

  std::vector<RenderPassId> order;
  std::string error;
  assert(graph.Compile(&order, &error) == ENGINE_NATIVE_STATUS_OK);
  assert(error.empty());
  AssertOrder(order, {post});
}

void TestInputValidation() {
  RenderGraph graph;
  RenderPassId pass = 0;

  assert(graph.AddPass("", &pass) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(graph.AddPass("main", nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(graph.AddPass("main", &pass) == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddPass("main", &pass) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  assert(graph.ImportResource("") == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(graph.ImportResource("swapchain") == ENGINE_NATIVE_STATUS_OK);
  assert(graph.ImportResource("swapchain") == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  assert(graph.AddRead(pass, "") == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(graph.AddWrite(pass, "") == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(graph.AddRead(pass, "swapchain") == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddRead(pass, "swapchain") == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(graph.AddWrite(pass, "swapchain") == ENGINE_NATIVE_STATUS_OK);
  assert(graph.AddWrite(pass, "swapchain") == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(graph.AddRead(99u, "depth") == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(graph.AddWrite(99u, "depth") == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(graph.AddDependency(pass, pass) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);

  std::vector<RenderPassId> order;
  assert(graph.Compile(nullptr, nullptr) == ENGINE_NATIVE_STATUS_INVALID_ARGUMENT);
  assert(graph.Compile(&order, nullptr) == ENGINE_NATIVE_STATUS_OK);
  AssertOrder(order, {pass});
}

}  // namespace

void RunRenderGraphTests() {
  TestCompileBuildsOrderFromResourceHazards();
  TestCompileBuildsOrderFromExplicitDependencies();
  TestCompileDetectsCycles();
  TestCompileFailsOnUnknownReadResource();
  TestCompileAllowsImportedResources();
  TestInputValidation();
}

}  // namespace dff::native::tests
