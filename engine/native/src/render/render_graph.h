#ifndef DFF_ENGINE_NATIVE_RENDER_GRAPH_H
#define DFF_ENGINE_NATIVE_RENDER_GRAPH_H

#include <cstdint>
#include <string>
#include <vector>

#include "engine_native.h"

namespace dff::native::render {

using RenderPassId = uint32_t;

class RenderGraph {
 public:
  engine_native_status_t AddPass(const std::string& name, RenderPassId* out_pass_id);

  engine_native_status_t AddDependency(RenderPassId before, RenderPassId after);

  engine_native_status_t AddRead(RenderPassId pass_id, const std::string& resource_name);

  engine_native_status_t AddWrite(RenderPassId pass_id, const std::string& resource_name);

  engine_native_status_t Compile(std::vector<RenderPassId>* out_order,
                                 std::string* out_error) const;

  void Clear();

  size_t PassCount() const { return passes_.size(); }

 private:
  struct PassNode {
    std::string name;
    std::vector<RenderPassId> explicit_dependencies;
    std::vector<std::string> reads;
    std::vector<std::string> writes;
  };

  bool IsValidPassId(RenderPassId pass_id) const;

  std::vector<PassNode> passes_;
};

}  // namespace dff::native::render

#endif
