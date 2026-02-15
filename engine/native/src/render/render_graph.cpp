#include "render/render_graph.h"

#include <algorithm>
#include <limits>
#include <queue>
#include <unordered_map>
#include <unordered_set>
#include <utility>

namespace dff::native::render {

namespace {

bool IsValidResourceName(const std::string& name) {
  return !name.empty();
}

uint64_t EncodeEdge(RenderPassId from, RenderPassId to) {
  return (static_cast<uint64_t>(from) << 32u) | static_cast<uint64_t>(to);
}

}  // namespace

engine_native_status_t RenderGraph::AddPass(const std::string& name,
                                            RenderPassId* out_pass_id) {
  if (out_pass_id == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (name.empty()) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  if (passes_.size() >=
      static_cast<size_t>(std::numeric_limits<RenderPassId>::max())) {
    return ENGINE_NATIVE_STATUS_INTERNAL_ERROR;
  }

  PassNode pass;
  pass.name = name;
  passes_.push_back(std::move(pass));
  *out_pass_id = static_cast<RenderPassId>(passes_.size() - 1u);
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RenderGraph::AddDependency(RenderPassId before,
                                                  RenderPassId after) {
  if (!IsValidPassId(before) || !IsValidPassId(after) || before == after) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  passes_[after].explicit_dependencies.push_back(before);
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RenderGraph::AddRead(RenderPassId pass_id,
                                            const std::string& resource_name) {
  if (!IsValidPassId(pass_id) || !IsValidResourceName(resource_name)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  passes_[pass_id].reads.push_back(resource_name);
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RenderGraph::AddWrite(RenderPassId pass_id,
                                             const std::string& resource_name) {
  if (!IsValidPassId(pass_id) || !IsValidResourceName(resource_name)) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  passes_[pass_id].writes.push_back(resource_name);
  return ENGINE_NATIVE_STATUS_OK;
}

engine_native_status_t RenderGraph::Compile(std::vector<RenderPassId>* out_order,
                                            std::string* out_error) const {
  if (out_order == nullptr) {
    return ENGINE_NATIVE_STATUS_INVALID_ARGUMENT;
  }

  out_order->clear();
  if (out_error != nullptr) {
    out_error->clear();
  }

  const size_t pass_count = passes_.size();
  out_order->reserve(pass_count);

  std::vector<std::vector<RenderPassId>> adjacency(pass_count);
  std::vector<uint32_t> indegree(pass_count, 0u);
  std::unordered_set<uint64_t> edge_set;

  auto add_edge = [&](RenderPassId from, RenderPassId to) {
    if (from == to) {
      return;
    }

    const uint64_t encoded = EncodeEdge(from, to);
    if (!edge_set.insert(encoded).second) {
      return;
    }

    adjacency[from].push_back(to);
    ++indegree[to];
  };

  for (RenderPassId pass_id = 0u; pass_id < static_cast<RenderPassId>(pass_count);
       ++pass_id) {
    const PassNode& pass = passes_[pass_id];
    for (RenderPassId dependency : pass.explicit_dependencies) {
      if (!IsValidPassId(dependency)) {
        if (out_error != nullptr) {
          *out_error = "RenderGraph contains invalid explicit dependency.";
        }
        return ENGINE_NATIVE_STATUS_INVALID_STATE;
      }

      add_edge(dependency, pass_id);
    }
  }

  std::unordered_map<std::string, RenderPassId> last_writer;
  std::unordered_map<std::string, std::vector<RenderPassId>> last_readers;

  for (RenderPassId pass_id = 0u; pass_id < static_cast<RenderPassId>(pass_count);
       ++pass_id) {
    const PassNode& pass = passes_[pass_id];

    for (const std::string& resource : pass.reads) {
      auto writer_it = last_writer.find(resource);
      if (writer_it != last_writer.end()) {
        add_edge(writer_it->second, pass_id);
      }

      last_readers[resource].push_back(pass_id);
    }

    for (const std::string& resource : pass.writes) {
      auto writer_it = last_writer.find(resource);
      if (writer_it != last_writer.end()) {
        add_edge(writer_it->second, pass_id);
      }

      auto readers_it = last_readers.find(resource);
      if (readers_it != last_readers.end()) {
        for (RenderPassId reader : readers_it->second) {
          add_edge(reader, pass_id);
        }

        readers_it->second.clear();
      }

      last_writer[resource] = pass_id;
    }
  }

  std::priority_queue<RenderPassId, std::vector<RenderPassId>, std::greater<>> ready;
  for (RenderPassId pass_id = 0u; pass_id < static_cast<RenderPassId>(pass_count);
       ++pass_id) {
    if (indegree[pass_id] == 0u) {
      ready.push(pass_id);
    }
  }

  while (!ready.empty()) {
    const RenderPassId current = ready.top();
    ready.pop();
    out_order->push_back(current);

    for (RenderPassId next : adjacency[current]) {
      --indegree[next];
      if (indegree[next] == 0u) {
        ready.push(next);
      }
    }
  }

  if (out_order->size() != pass_count) {
    out_order->clear();
    if (out_error != nullptr) {
      *out_error = "RenderGraph contains a dependency cycle.";
    }
    return ENGINE_NATIVE_STATUS_INVALID_STATE;
  }

  return ENGINE_NATIVE_STATUS_OK;
}

void RenderGraph::Clear() {
  passes_.clear();
}

bool RenderGraph::IsValidPassId(RenderPassId pass_id) const {
  return static_cast<size_t>(pass_id) < passes_.size();
}

}  // namespace dff::native::render
