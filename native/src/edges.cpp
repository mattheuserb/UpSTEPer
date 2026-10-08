// Display lines for B-Rep edges (drawn on top of the shaded mesh).
#include "internal.hpp"

#include <BRepAdaptor_Curve.hxx>
#include <BRep_Tool.hxx>
#include <GCPnts_QuasiUniformDeflection.hxx>
#include <TopExp.hxx>
#include <TopoDS.hxx>
#include <TopoDS_Edge.hxx>
#include <TopoDS_Face.hxx>

#include <algorithm>
#include <memory>

struct ups_lines
{
    std::vector<float> positions;
    std::vector<uint32_t> indices;
    std::vector<int32_t> segmentEdges;
};

using ups::detail::Fail;
using ups::detail::Guarded;

namespace
{
// A seam joins a face to itself (cylinder, cone, sphere); it is not a visible edge of the part.
bool IsSeam(const TopoDS_Edge& edge, const ups::detail::ShapeAncestorMap& edgeFaces)
{
    if (!edgeFaces.Contains(edge))
    {
        return false;
    }

    const NCollection_List<TopoDS_Shape>& faces = edgeFaces.FindFromKey(edge);
    if (faces.IsEmpty())
    {
        return false;
    }

    return std::all_of(faces.begin(), faces.end(), [&](const TopoDS_Shape& face) {
        return BRep_Tool::IsClosed(edge, TopoDS::Face(face));
    });
}

void AppendEdge(ups_lines& lines, const TopoDS_Edge& edge, int32_t edgeId, double deflection)
{
    const BRepAdaptor_Curve curve(edge);
    const double first = curve.FirstParameter();
    const double last = curve.LastParameter();

    std::vector<gp_Pnt> points;
    GCPnts_QuasiUniformDeflection sampler(curve, deflection, first, last);
    if (sampler.IsDone() && sampler.NbPoints() >= 2)
    {
        for (int i = 1; i <= sampler.NbPoints(); ++i)
        {
            points.push_back(sampler.Value(i));
        }
    }
    else
    {
        constexpr int Samples = 32;
        for (int i = 0; i <= Samples; ++i)
        {
            points.push_back(curve.Value(first + ((last - first) * i / Samples)));
        }
    }

    const auto base = static_cast<uint32_t>(lines.positions.size() / 3);
    for (const gp_Pnt& p : points)
    {
        lines.positions.insert(lines.positions.end(),
                               {static_cast<float>(p.X()), static_cast<float>(p.Y()), static_cast<float>(p.Z())});
    }
    for (uint32_t i = 0; i + 1 < points.size(); ++i)
    {
        lines.indices.insert(lines.indices.end(), {base + i, base + i + 1});
        lines.segmentEdges.push_back(edgeId);
    }
}

template <typename T>
ups_status CopyArray(const std::vector<T>& source, T* target, int64_t capacity, const char* name)
{
    if (target == nullptr)
    {
        return UPS_OK;
    }
    if (capacity < static_cast<int64_t>(source.size()))
    {
        return Fail(UPS_ERROR_BUFFER_TOO_SMALL, std::string("Buffer too small for ") + name + ".");
    }
    std::copy(source.begin(), source.end(), target);
    return UPS_OK;
}
} // namespace

extern "C" UPS_API ups_status ups_document_tessellate_edges(ups_document* document, double deflection, ups_lines** out_lines)
{
    return Guarded([&]() -> ups_status {
        if (document == nullptr || out_lines == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Document and output pointer must not be null.");
        }
        if (deflection <= 0.0)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Deflection must be positive.");
        }
        *out_lines = nullptr;

        ups::detail::ShapeIndexedMap edges;
        TopExp::MapShapes(document->shape, TopAbs_EDGE, edges);
        ups::detail::ShapeAncestorMap edgeFaces;
        TopExp::MapShapesAndAncestors(document->shape, TopAbs_EDGE, TopAbs_FACE, edgeFaces);

        auto lines = std::make_unique<ups_lines>();
        for (int index = 1; index <= edges.Extent(); ++index)
        {
            const TopoDS_Edge& edge = TopoDS::Edge(edges(index));
            if (BRep_Tool::Degenerated(edge) || IsSeam(edge, edgeFaces))
            {
                continue;
            }
            AppendEdge(*lines, edge, index - 1, deflection);
        }

        *out_lines = lines.release();
        return UPS_OK;
    });
}

extern "C" UPS_API void ups_lines_free(ups_lines* lines)
{
    delete lines;
}

extern "C" UPS_API ups_status ups_lines_get_info(const ups_lines* lines, int32_t* out_vertex_count, int32_t* out_segment_count)
{
    return Guarded([&]() -> ups_status {
        if (lines == nullptr || out_vertex_count == nullptr || out_segment_count == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Lines and output pointers must not be null.");
        }
        *out_vertex_count = static_cast<int32_t>(lines->positions.size() / 3);
        *out_segment_count = static_cast<int32_t>(lines->segmentEdges.size());
        return UPS_OK;
    });
}

extern "C" UPS_API ups_status ups_lines_copy(
    const ups_lines* lines,
    float* positions, int64_t positions_capacity,
    uint32_t* indices, int64_t indices_capacity,
    int32_t* segment_edges, int64_t segment_edges_capacity)
{
    return Guarded([&]() -> ups_status {
        if (lines == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Lines must not be null.");
        }
        ups_status status = CopyArray(lines->positions, positions, positions_capacity, "positions");
        if (status == UPS_OK)
        {
            status = CopyArray(lines->indices, indices, indices_capacity, "indices");
        }
        if (status == UPS_OK)
        {
            status = CopyArray(lines->segmentEdges, segment_edges, segment_edges_capacity, "segment edges");
        }
        return status;
    });
}
