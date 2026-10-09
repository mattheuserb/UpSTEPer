// Model check: counts typical STEP defects per part definition (roadmap M4, ADR 0007), so the user
// can see whether and what to repair. Read-only; nothing in the document changes.
#include "internal.hpp"

#include <BRepAdaptor_Curve.hxx>
#include <BRepAdaptor_Surface.hxx>
#include <BRepCheck_Analyzer.hxx>
#include <BRep_Tool.hxx>
#include <GCPnts_AbscissaPoint.hxx>
#include <ShapeAnalysis_CanonicalRecognition.hxx>
#include <ShapeUpgrade_UnifySameDomain.hxx>
#include <TopExp.hxx>
#include <TopoDS.hxx>
#include <XCAFDoc_DocumentTool.hxx>
#include <XCAFDoc_ShapeTool.hxx>
#include <gp_Cone.hxx>
#include <gp_Cylinder.hxx>
#include <gp_Pln.hxx>
#include <gp_Sphere.hxx>

#include <algorithm>
#include <string>
#include <vector>

using ups::detail::Fail;
using ups::detail::Guarded;

struct ups_check
{
    std::vector<ups_part_check> parts;
    std::string names;
};

namespace
{
using ups::detail::ShapeAncestorMap;
using ups::detail::ShapeIndexedMap;

int32_t Count(const TopoDS_Shape& shape, TopAbs_ShapeEnum type)
{
    ShapeIndexedMap map;
    TopExp::MapShapes(shape, type, map);
    return static_cast<int32_t>(map.Extent());
}

} // namespace

// Edges that bound exactly one face: gaps between faces or an open shell. Seams (an edge used
// twice by the same face) and degenerated edges (cone apex, sphere pole) are no defect.
int32_t ups::detail::CountFreeEdges(const TopoDS_Shape& shape)
{
    ShapeAncestorMap edgeFaces;
    TopExp::MapShapesAndAncestors(shape, TopAbs_EDGE, TopAbs_FACE, edgeFaces);
    int32_t count = 0;
    for (int index = 1; index <= edgeFaces.Extent(); ++index)
    {
        const TopoDS_Edge& edge = TopoDS::Edge(edgeFaces.FindKey(index));
        if (BRep_Tool::Degenerated(edge))
        {
            continue;
        }
        ShapeIndexedMap faces;
        for (const TopoDS_Shape& face : edgeFaces(index))
        {
            faces.Add(face);
        }
        if (faces.Extent() == 1 && !BRep_Tool::IsClosed(edge, TopoDS::Face(faces(1))))
        {
            ++count;
        }
    }
    return count;
}

// Sub-shapes (faces, edges, vertices) that BRepCheck reports as invalid.
int32_t ups::detail::CountInvalidShapes(const TopoDS_Shape& shape)
{
    const BRepCheck_Analyzer analyzer(shape);
    if (analyzer.IsValid())
    {
        return 0;
    }
    int32_t count = 0;
    for (const TopAbs_ShapeEnum type : {TopAbs_FACE, TopAbs_EDGE, TopAbs_VERTEX})
    {
        ShapeIndexedMap map;
        TopExp::MapShapes(shape, type, map);
        for (int index = 1; index <= map.Extent(); ++index)
        {
            if (!analyzer.IsValid(map(index)))
            {
                ++count;
            }
        }
    }
    // Invalid at a higher level only (e.g. a shell that is not closed): count the whole shape.
    return std::max(count, 1);
}

ups::detail::LabelSequence ups::detail::PartDefinitions(const ups_document& document)
{
    const Handle(XCAFDoc_ShapeTool) shapeTool = XCAFDoc_DocumentTool::ShapeTool(document.xdeDocument->Main());
    LabelSequence labels;
    shapeTool->GetShapes(labels);
    LabelSequence parts;
    for (const TDF_Label& label : labels)
    {
        // Assemblies consist of parts, which are handled once each, however often they are placed.
        if (!XCAFDoc_ShapeTool::IsAssembly(label) && XCAFDoc_ShapeTool::IsSimpleShape(label))
        {
            parts.Append(label);
        }
    }
    return parts;
}

namespace
{
using ups::detail::CountFreeEdges;
using ups::detail::CountInvalidShapes;

void CheckFaces(const TopoDS_Shape& shape, double tolerance, ups_part_check& result)
{
    ShapeIndexedMap faces;
    TopExp::MapShapes(shape, TopAbs_FACE, faces);
    for (int index = 1; index <= faces.Extent(); ++index)
    {
        const TopoDS_Face& face = TopoDS::Face(faces(index));
        const GeomAbs_SurfaceType type = BRepAdaptor_Surface(face, /*restriction*/ false).GetType();
        if (type != GeomAbs_BSplineSurface && type != GeomAbs_BezierSurface)
        {
            continue;
        }
        ++result.spline_faces;

        ShapeAnalysis_CanonicalRecognition recognition(face);
        gp_Pln plane;
        gp_Cylinder cylinder;
        gp_Cone cone;
        gp_Sphere sphere;
        if (recognition.IsPlane(tolerance, plane) || recognition.IsCylinder(tolerance, cylinder)
            || recognition.IsCone(tolerance, cone) || recognition.IsSphere(tolerance, sphere))
        {
            ++result.canonical_faces;
        }
    }
}

int32_t SmallEdges(const TopoDS_Shape& shape, double tolerance)
{
    ShapeIndexedMap edges;
    TopExp::MapShapes(shape, TopAbs_EDGE, edges);
    int32_t count = 0;
    for (int index = 1; index <= edges.Extent(); ++index)
    {
        const TopoDS_Edge& edge = TopoDS::Edge(edges(index));
        if (!BRep_Tool::Degenerated(edge) && BRep_Tool::IsGeometric(edge)
            && GCPnts_AbscissaPoint::Length(BRepAdaptor_Curve(edge)) < tolerance)
        {
            ++count;
        }
    }
    return count;
}

// Faces that would disappear when neighbouring faces on the same surface are merged
// (ShapeUpgrade_UnifySameDomain works on a copy; the document is not touched).
int32_t MergeableFaces(const TopoDS_Shape& shape, int32_t faceCount)
{
    ShapeUpgrade_UnifySameDomain unify(shape, /*unifyEdges*/ true, /*unifyFaces*/ true, /*concatBSplines*/ false);
    unify.Build();
    return std::max(0, faceCount - Count(unify.Shape(), TopAbs_FACE));
}

// Runs one sub-check; an OCCT failure on broken geometry (e.g. "Courbes non jointives" in a
// 374 MB engine assembly) is counted instead of aborting the check of the whole document.
template <typename TCheck>
void Try(ups_part_check& result, TCheck&& check)
{
    try
    {
        check();
    }
    catch (const std::exception&) // Standard_Failure derives from std::exception since OCCT 8.0
    {
        ++result.failed_checks;
    }
}

ups_part_check CheckPart(const TopoDS_Shape& shape, double tolerance)
{
    ups_part_check result{};
    result.solid_count = Count(shape, TopAbs_SOLID);
    result.face_count = Count(shape, TopAbs_FACE);
    Try(result, [&] { result.free_edges = CountFreeEdges(shape); });
    Try(result, [&] { result.invalid_shapes = CountInvalidShapes(shape); });
    Try(result, [&] { CheckFaces(shape, tolerance, result); });
    Try(result, [&] { result.mergeable_faces = result.face_count > 1 ? MergeableFaces(shape, result.face_count) : 0; });
    Try(result, [&] { result.small_edges = SmallEdges(shape, tolerance); });
    return result;
}
} // namespace

extern "C" UPS_API ups_status ups_document_check(const ups_document* document, double tolerance, ups_check** out_check)
{
    return Guarded([&]() -> ups_status {
        if (document == nullptr || out_check == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Document and output pointer must not be null.");
        }
        if (!(tolerance > 0.0))
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Tolerance must be positive.");
        }
        *out_check = nullptr;

        auto check = std::make_unique<ups_check>();
        for (const TDF_Label& label : ups::detail::PartDefinitions(*document))
        {
            ups_part_check part = CheckPart(XCAFDoc_ShapeTool::GetShape(label), tolerance);
            const std::string name = ups::detail::NameOf(label);
            part.name_offset = static_cast<int32_t>(check->names.size());
            part.name_length = static_cast<int32_t>(name.size());
            check->names += name;
            check->parts.push_back(part);
        }

        *out_check = check.release();
        return UPS_OK;
    });
}

extern "C" UPS_API void ups_check_free(ups_check* check)
{
    delete check;
}

extern "C" UPS_API ups_status ups_check_get_info(const ups_check* check, int32_t* out_part_count, int32_t* out_names_length)
{
    return Guarded([&]() -> ups_status {
        if (check == nullptr || out_part_count == nullptr || out_names_length == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Check and output pointers must not be null.");
        }
        *out_part_count = static_cast<int32_t>(check->parts.size());
        *out_names_length = static_cast<int32_t>(check->names.size());
        return UPS_OK;
    });
}

extern "C" UPS_API ups_status ups_check_copy(
    const ups_check* check, ups_part_check* parts, int64_t parts_capacity, char* names, int64_t names_capacity)
{
    return Guarded([&]() -> ups_status {
        if (check == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Check must not be null.");
        }
        if (parts != nullptr)
        {
            if (parts_capacity < static_cast<int64_t>(check->parts.size()))
            {
                return Fail(UPS_ERROR_BUFFER_TOO_SMALL, "Buffer too small for parts.");
            }
            std::copy(check->parts.begin(), check->parts.end(), parts);
        }
        if (names != nullptr)
        {
            if (names_capacity < static_cast<int64_t>(check->names.size()))
            {
                return Fail(UPS_ERROR_BUFFER_TOO_SMALL, "Buffer too small for names.");
            }
            std::copy(check->names.begin(), check->names.end(), names);
        }
        return UPS_OK;
    });
}
