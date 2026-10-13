// Repair (ADR 0007, ADR 0011): tier 1 healing (ShapeFix, sewing, merging faces on the same surface)
// and tier 2 canonical recognition (spline faces -> planes, cylinders, cones, spheres), per part definition, with colours carried over through the algorithms' histories
// (ADR 0005). All parts are computed first (cancellable) and applied to the document only at the
// end, so a cancelled or failed repair never leaves a half-repaired model behind.
#include "internal.hpp"

#include <BRepAdaptor_Surface.hxx>
#include <BRepBuilderAPI_MakeSolid.hxx>
#include <BRepBuilderAPI_Sewing.hxx>
#include <BRepGProp.hxx>
#include <BRepLib.hxx>
#include <BRepTools.hxx>
#include <BRepTools_History.hxx>
#include <BRep_Builder.hxx>
#include <BRep_Tool.hxx>
#include <GProp_GProps.hxx>
#include <GeomAPI_ProjectPointOnSurf.hxx>
#include <GeomLProp_SLProps.hxx>
#include <Geom_ConicalSurface.hxx>
#include <Geom_CylindricalSurface.hxx>
#include <Geom_Plane.hxx>
#include <Geom_SphericalSurface.hxx>
#include <Message_ProgressScope.hxx>
#include <ShapeBuild_ReShape.hxx>
#include <ShapeExtend_Status.hxx>
#include <ShapeAnalysis_CanonicalRecognition.hxx>
#include <ShapeFix_Shape.hxx>
#include <ShapeUpgrade_UnifySameDomain.hxx>
#include <TopExp.hxx>
#include <TopExp_Explorer.hxx>
#include <TopoDS.hxx>
#include <TopoDS_Compound.hxx>
#include <TopoDS_Iterator.hxx>
#include <XCAFDoc_ColorTool.hxx>
#include <XCAFDoc_DocumentTool.hxx>
#include <XCAFDoc_ShapeTool.hxx>

#include <cmath>
#include <map>
#include <string>
#include <vector>

using ups::detail::Fail;
using ups::detail::Guarded;
using ups::detail::ShapeIndexedMap;

struct ups_repair
{
    std::vector<ups_part_repair> parts;
    std::string names;
};

namespace
{
constexpr XCAFDoc_ColorType ColorTypes[] = {XCAFDoc_ColorGen, XCAFDoc_ColorSurf, XCAFDoc_ColorCurv};

// A coloured face or solid of a part and where it ended up after each repair step.
struct Tracked
{
    TopoDS_Shape original;
    std::vector<std::pair<XCAFDoc_ColorType, Quantity_Color>> colors;
    double area = 0.0; // faces: surface area, the weight in colour conflicts
    std::vector<TopoDS_Shape> images;
};

struct PartResult
{
    TDF_Label label;
    TopoDS_Shape shape;
    std::vector<Tracked> tracked;
    ups_part_repair report{};
};

int32_t Count(const TopoDS_Shape& shape, TopAbs_ShapeEnum type)
{
    ShapeIndexedMap map;
    TopExp::MapShapes(shape, type, map);
    return static_cast<int32_t>(map.Extent());
}

double Area(const TopoDS_Shape& face)
{
    GProp_GProps properties;
    BRepGProp::SurfaceProperties(face, properties);
    return properties.Mass();
}

std::vector<Tracked> CollectColored(const Handle(XCAFDoc_ColorTool)& colors, const TDF_Label& part)
{
    std::vector<Tracked> result;
    ups::detail::LabelSequence subLabels;
    XCAFDoc_ShapeTool::GetSubShapes(part, subLabels);
    for (const TDF_Label& subLabel : subLabels)
    {
        Tracked tracked;
        tracked.original = XCAFDoc_ShapeTool::GetShape(subLabel);
        for (const XCAFDoc_ColorType type : ColorTypes)
        {
            Quantity_Color color;
            if (colors->GetColor(subLabel, type, color))
            {
                tracked.colors.emplace_back(type, color);
            }
        }
        if (tracked.original.IsNull() || tracked.colors.empty())
        {
            continue;
        }
        if (tracked.original.ShapeType() == TopAbs_FACE)
        {
            tracked.area = Area(tracked.original);
        }
        tracked.images = {tracked.original};
        result.push_back(std::move(tracked));
    }
    return result;
}

// Follows every tracked shape through one history step: modified shapes are replaced by their
// images, removed ones dropped, untouched ones kept.
void Follow(std::vector<Tracked>& tracked, const Handle(BRepTools_History)& history)
{
    if (history.IsNull())
    {
        return;
    }
    for (Tracked& item : tracked)
    {
        std::vector<TopoDS_Shape> next;
        for (const TopoDS_Shape& image : item.images)
        {
            const NCollection_List<TopoDS_Shape>& modified = history->Modified(image);
            if (!modified.IsEmpty())
            {
                next.insert(next.end(), modified.cbegin(), modified.cend());
            }
            else if (!history->IsRemoved(image))
            {
                next.push_back(image);
            }
        }
        item.images = std::move(next);
    }
}

void FollowSewing(std::vector<Tracked>& tracked, const BRepBuilderAPI_Sewing& sewing)
{
    for (Tracked& item : tracked)
    {
        for (TopoDS_Shape& image : item.images)
        {
            if (sewing.IsModifiedSubShape(image))
            {
                image = sewing.ModifiedSubShape(image);
            }
        }
    }
}

// Turns closed shells of a sewn result into solids; faces keep their identity.
TopoDS_Shape SolidsFromClosedShells(const TopoDS_Shape& sewn)
{
    TopoDS_Compound result;
    BRep_Builder builder;
    builder.MakeCompound(result);
    int32_t pieces = 0;
    TopoDS_Shape single;
    for (TopExp_Explorer shells(sewn, TopAbs_SHELL); shells.More(); shells.Next())
    {
        TopoDS_Shape piece = shells.Current();
        if (BRep_Tool::IsClosed(piece))
        {
            BRepBuilderAPI_MakeSolid makeSolid(TopoDS::Shell(piece));
            if (makeSolid.IsDone())
            {
                TopoDS_Solid solid = makeSolid.Solid();
                BRepLib::OrientClosedSolid(solid);
                piece = solid;
            }
        }
        builder.Add(result, piece);
        single = piece;
        ++pieces;
    }
    for (TopExp_Explorer faces(sewn, TopAbs_FACE, TopAbs_SHELL); faces.More(); faces.Next())
    {
        builder.Add(result, faces.Current()); // loose faces sewing could not attach
        single = faces.Current();
        ++pieces;
    }
    return pieces == 1 ? single : TopoDS_Shape(result);
}

// The exact surface a spline face approximates within the tolerance, oriented so its normal points
// the same way as the spline's (the face's material side must not flip), or null.
Handle(Geom_Surface) CanonicalSurface(const TopoDS_Face& face, double tolerance, double& gap)
{
    const GeomAbs_SurfaceType type = BRepAdaptor_Surface(face, /*restriction*/ false).GetType();
    if (type != GeomAbs_BSplineSurface && type != GeomAbs_BezierSurface)
    {
        return {};
    }

    ShapeAnalysis_CanonicalRecognition recognition(face);
    gp_Pln plane;
    gp_Cylinder cylinder;
    gp_Cone cone;
    gp_Sphere sphere;
    int kind = 0;
    if (recognition.IsPlane(tolerance, plane))
    {
        kind = 1;
    }
    else if (recognition.IsCylinder(tolerance, cylinder))
    {
        kind = 2;
    }
    else if (recognition.IsCone(tolerance, cone))
    {
        kind = 3;
    }
    else if (recognition.IsSphere(tolerance, sphere))
    {
        kind = 4;
    }
    else
    {
        return {};
    }
    gap = recognition.GetGap();
    if (!(gap <= tolerance))
    {
        // The recognition may accept a surface whose measured deviation exceeds the requested
        // tolerance (0.18 mm at 0.05 mm on the engine assembly); never replace beyond it.
        return {};
    }

    auto make = [&]() -> Handle(Geom_Surface) {
        switch (kind)
        {
        case 1:
            return new Geom_Plane(plane);
        case 2:
            return new Geom_CylindricalSurface(cylinder);
        case 3:
            return new Geom_ConicalSurface(cone);
        default:
            return new Geom_SphericalSurface(sphere);
        }
    };
    Handle(Geom_Surface) surface = make();

    // Compare normals at the middle of the face's parameter range.
    double u1 = 0, u2 = 0, v1 = 0, v2 = 0;
    BRepTools::UVBounds(face, u1, u2, v1, v2);
    GeomLProp_SLProps original(BRep_Tool::Surface(face), (u1 + u2) / 2, (v1 + v2) / 2, 1, 1e-9);
    if (!original.IsNormalDefined())
    {
        return surface;
    }
    GeomAPI_ProjectPointOnSurf projection(original.Value(), surface);
    if (projection.NbPoints() == 0)
    {
        return {};
    }
    double u = 0, v = 0;
    projection.LowerDistanceParameters(u, v);
    GeomLProp_SLProps candidate(surface, u, v, 1, 1e-9);
    if (!candidate.IsNormalDefined() || original.Normal().Dot(candidate.Normal()) >= 0)
    {
        return surface;
    }

    // Opposite normal: a plane flips its normal, the others switch to a left-handed frame.
    auto flip = [](auto& primitive, bool reverseNormal) {
        gp_Ax3 position = primitive.Position();
        if (reverseNormal)
        {
            position.ZReverse();
        }
        else
        {
            position.YReverse();
        }
        primitive.SetPosition(position);
    };
    switch (kind)
    {
    case 1:
        flip(plane, true);
        break;
    case 2:
        flip(cylinder, false);
        break;
    case 3:
        flip(cone, false);
        break;
    default:
        flip(sphere, false);
        break;
    }
    return make();
}

// What the never-worse rule compares before and after a step.
struct Quality
{
    int32_t invalid = 0;
    int32_t freeEdges = 0;
    int32_t solids = 0;
    int32_t insideOutSolids = 0; // negative volume: faces oriented the wrong way
    double volume = 0.0;
};

Quality Measure(const TopoDS_Shape& shape)
{
    Quality quality{ups::detail::CountInvalidShapes(shape), ups::detail::CountFreeEdges(shape)};
    for (TopExp_Explorer solids(shape, TopAbs_SOLID); solids.More(); solids.Next())
    {
        GProp_GProps properties;
        BRepGProp::VolumeProperties(solids.Current(), properties);
        ++quality.solids;
        quality.insideOutSolids += properties.Mass() < 0 ? 1 : 0;
        quality.volume += properties.Mass();
    }
    return quality;
}

// A step is worse if it adds invalid sub-shapes, gaps or inside-out solids, or (with the same
// solids) changes the volume by more than 1 %: e.g. an exact surface with the material side flipped.
bool IsWorse(const Quality& after, const Quality& before)
{
    if (after.invalid > before.invalid || after.freeEdges > before.freeEdges || after.insideOutSolids > before.insideOutSolids)
    {
        return true;
    }
    return after.solids == before.solids && std::abs(before.volume) > 0.0
           && std::abs(after.volume - before.volume) > 0.01 * std::abs(before.volume);
}

PartResult RepairPart(const Handle(XCAFDoc_ColorTool)& colors, const TDF_Label& label, const ups_repair_params& params)
{
    PartResult result;
    result.label = label;
    const TopoDS_Shape original = XCAFDoc_ShapeTool::GetShape(label);
    result.shape = original;
    ups_part_repair& report = result.report;
    report.faces_before = Count(original, TopAbs_FACE);
    report.solids_before = Count(original, TopAbs_SOLID);
    Quality quality = Measure(original);
    report.free_edges_before = quality.freeEdges;
    report.invalid_before = quality.invalid;

    try
    {
        result.tracked = CollectColored(colors, label);
    }
    catch (const std::exception&) // Standard_Failure derives from std::exception since OCCT 8.0
    {
        report.failed = 1;
        report.faces_after = report.faces_before;
        report.solids_after = report.solids_before;
        report.free_edges_after = report.free_edges_before;
        report.invalid_after = report.invalid_before;
        return result;
    }

    // Runs one step on a copy of the colour tracking. It is kept only if it changed the shape,
    // did not throw and did not make the part worse; otherwise the part stays as before the step
    // and the other steps still run. Returns whether the step was kept.
    TopoDS_Shape current = original;
    auto attempt = [&](auto&& step) {
        std::vector<Tracked> tracked = result.tracked;
        try
        {
            const TopoDS_Shape next = step(current, tracked);
            if (next.IsSame(current))
            {
                return false;
            }
            const Quality after = Measure(next);
            if (IsWorse(after, quality))
            {
                report.reverted = 1;
                return false;
            }
            current = next;
            quality = after;
            result.tracked = std::move(tracked);
            return true;
        }
        catch (const std::exception&)
        {
            report.failed = 1;
            return false;
        }
    };

    if ((params.steps & UPS_REPAIR_FIX) != 0)
    {
        attempt([&](const TopoDS_Shape& shape, std::vector<Tracked>& tracked) {
            ShapeFix_Shape fix(shape);
            fix.SetPrecision(params.tolerance);
            fix.SetMaxTolerance(params.tolerance);
            fix.Perform();
            if (!fix.Status(ShapeExtend_DONE))
            {
                return shape; // nothing to fix
            }
            Follow(tracked, fix.Context()->History());
            return fix.Shape();
        });
    }

    if ((params.steps & UPS_REPAIR_SEW) != 0 && quality.freeEdges > 0)
    {
        attempt([&](const TopoDS_Shape& shape, std::vector<Tracked>& tracked) {
            BRepBuilderAPI_Sewing sewing(params.tolerance);
            sewing.Add(shape);
            sewing.Perform();
            FollowSewing(tracked, sewing);
            return SolidsFromClosedShells(sewing.SewedShape());
        });
    }

    // Tier 2 before merging, so faces that become the same cylinder can then be merged.
    if ((params.steps & UPS_REPAIR_CANONICAL) != 0)
    {
        int32_t converted = 0;
        double deviation = 0.0;
        const bool kept = attempt([&](const TopoDS_Shape& shape, std::vector<Tracked>& tracked) {
            converted = 0;
            deviation = 0.0;
            const Handle(ShapeBuild_ReShape) reshape = new ShapeBuild_ReShape();
            ShapeIndexedMap faces;
            TopExp::MapShapes(shape, TopAbs_FACE, faces);
            for (int index = 1; index <= faces.Extent(); ++index)
            {
                const TopoDS_Face& face = TopoDS::Face(faces(index));
                double gap = 0.0;
                const Handle(Geom_Surface) surface = CanonicalSurface(face, params.tolerance, gap);
                if (surface.IsNull())
                {
                    continue;
                }

                // Same wires (shared edges stay shared) on the exact surface; ShapeFix below adds
                // the edges' parameter curves on it.
                TopoDS_Face exact;
                BRep_Builder builder;
                builder.MakeFace(exact, surface, std::max(BRep_Tool::Tolerance(face), gap));
                for (TopoDS_Iterator wires(face, /*cumOri*/ false); wires.More(); wires.Next())
                {
                    builder.Add(exact, wires.Value());
                }
                exact.Orientation(face.Orientation());
                reshape->Replace(face, exact);
                ++converted;
                deviation = std::max(deviation, gap);
            }
            if (converted == 0)
            {
                return shape;
            }

            const TopoDS_Shape replaced = reshape->Apply(shape);
            Follow(tracked, reshape->History());
            ShapeFix_Shape fix(replaced);
            fix.Perform();
            Follow(tracked, fix.Context()->History());
            return fix.Shape();
        });
        if (kept)
        {
            report.canonical_faces = converted;
            report.max_deviation = deviation;
        }
    }

    if ((params.steps & UPS_REPAIR_MERGE_FACES) != 0 && Count(current, TopAbs_FACE) > 1)
    {
        attempt([&](const TopoDS_Shape& shape, std::vector<Tracked>& tracked) {
            ShapeUpgrade_UnifySameDomain unify(shape, /*unifyEdges*/ true, /*unifyFaces*/ true, /*concatBSplines*/ false);
            unify.SetLinearTolerance(params.tolerance);
            unify.Build();
            const TopoDS_Shape unified = unify.Shape();
            if (Count(unified, TopAbs_FACE) == Count(shape, TopAbs_FACE) && Count(unified, TopAbs_EDGE) == Count(shape, TopAbs_EDGE))
            {
                return shape; // nothing merged
            }
            Follow(tracked, unify.History());
            return unified;
        });
    }

    result.shape = current;
    report.changed = current.IsSame(original) ? 0 : 1;
    report.faces_after = Count(current, TopAbs_FACE);
    report.solids_after = Count(current, TopAbs_SOLID);
    report.free_edges_after = quality.freeEdges;
    report.invalid_after = quality.invalid;
    return result;
}

// Writes a repaired part into the document: new shape, colours re-attached to the faces and
// solids the coloured ones became. Several coloured faces merged into one take the colour that
// covered the largest area; differing colours are counted as a conflict.
void Apply(const Handle(XCAFDoc_ShapeTool)& shapes, const Handle(XCAFDoc_ColorTool)& colors, PartResult& part)
{
    ups::detail::LabelSequence oldSubLabels;
    XCAFDoc_ShapeTool::GetSubShapes(part.label, oldSubLabels);
    for (const TDF_Label& subLabel : oldSubLabels)
    {
        subLabel.ForgetAllAttributes(/*clearChildren*/ true);
    }
    shapes->SetShape(part.label, part.shape);

    for (const TopAbs_ShapeEnum type : {TopAbs_FACE, TopAbs_SOLID})
    {
        ShapeIndexedMap targets;
        TopExp::MapShapes(part.shape, type, targets);

        // Per target: colour candidates with the area they cover.
        std::map<int, std::vector<std::pair<const Tracked*, double>>> contributions;
        for (const Tracked& item : part.tracked)
        {
            if (item.original.ShapeType() != type)
            {
                continue;
            }
            for (const TopoDS_Shape& image : item.images)
            {
                const int index = targets.FindIndex(image);
                if (index > 0)
                {
                    contributions[index].emplace_back(&item, item.area);
                }
            }
        }

        for (const auto& [index, candidates] : contributions)
        {
            std::map<std::string, std::pair<const Tracked*, double>> byColor;
            for (const auto& [item, area] : candidates)
            {
                // Same colours (all types) = same key; the winner is the key covering most area.
                std::string key;
                for (const auto& [colorType, color] : item->colors)
                {
                    double r = 0, g = 0, b = 0;
                    color.Values(r, g, b, Quantity_TOC_sRGB);
                    key += std::to_string(static_cast<int>(colorType)) + "/" + std::to_string(r) + "," + std::to_string(g) + "," + std::to_string(b) + ";";
                }
                auto& entry = byColor[key];
                entry.first = item;
                entry.second += area;
            }

            const Tracked* winner = nullptr;
            double best = -1.0;
            for (const auto& [key, entry] : byColor)
            {
                if (entry.second > best)
                {
                    best = entry.second;
                    winner = entry.first;
                }
            }
            if (byColor.size() > 1)
            {
                ++part.report.colour_conflicts;
            }

            const TDF_Label subLabel = shapes->AddSubShape(part.label, targets(index));
            if (!subLabel.IsNull() && winner != nullptr)
            {
                for (const auto& [colorType, color] : winner->colors)
                {
                    colors->SetColor(subLabel, color, colorType);
                }
            }
        }
    }
}
} // namespace

extern "C" UPS_API ups_status ups_document_repair(
    ups_document* document, const ups_repair_params* params, ups_progress_callback callback, void* user_data, ups_repair** out_repair)
{
    return Guarded([&]() -> ups_status {
        if (document == nullptr || params == nullptr || out_repair == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Document, parameters and output pointer must not be null.");
        }
        if (!(params->tolerance > 0.0))
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Tolerance must be positive.");
        }
        if ((params->steps & ~(UPS_REPAIR_FIX | UPS_REPAIR_SEW | UPS_REPAIR_MERGE_FACES | UPS_REPAIR_CANONICAL)) != 0)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Unknown repair step.");
        }
        *out_repair = nullptr;

        const Handle(ups::detail::CallbackProgress) progress = new ups::detail::CallbackProgress(callback, user_data);
        progress->SetPhase("Repairing");
        const ups::detail::LabelSequence parts = ups::detail::PartDefinitions(*document);
        Message_ProgressScope scope(progress->Start(), "Repair", std::max(1, parts.Length()));

        const Handle(XCAFDoc_ShapeTool) shapes = XCAFDoc_DocumentTool::ShapeTool(document->xdeDocument->Main());
        const Handle(XCAFDoc_ColorTool) colors = XCAFDoc_DocumentTool::ColorTool(document->xdeDocument->Main());

        // Phase 1: compute every part (cancellable); the document is not touched yet.
        std::vector<PartResult> results;
        for (const TDF_Label& label : parts)
        {
            if (progress->IsCancelled())
            {
                return Fail(UPS_ERROR_CANCELLED, "Repair was cancelled; the model is unchanged.");
            }
            results.push_back(RepairPart(colors, label, *params));
            scope.Next();
        }
        if (progress->IsCancelled())
        {
            return Fail(UPS_ERROR_CANCELLED, "Repair was cancelled; the model is unchanged.");
        }

        // Phase 2: apply all changes at once.
        auto repair = std::make_unique<ups_repair>();
        bool changed = false;
        for (PartResult& result : results)
        {
            if (result.report.changed != 0)
            {
                Apply(shapes, colors, result);
                changed = true;
            }
            const std::string name = ups::detail::NameOf(result.label);
            result.report.name_offset = static_cast<int32_t>(repair->names.size());
            result.report.name_length = static_cast<int32_t>(name.size());
            repair->names += name;
            repair->parts.push_back(result.report);
        }
        if (changed)
        {
            shapes->UpdateAssemblies();
            ups::detail::RebuildTopology(*document);
        }

        progress->ReportDone();
        *out_repair = repair.release();
        return UPS_OK;
    });
}

extern "C" UPS_API void ups_repair_free(ups_repair* repair)
{
    delete repair;
}

extern "C" UPS_API ups_status ups_repair_get_info(const ups_repair* repair, int32_t* out_part_count, int32_t* out_names_length)
{
    return Guarded([&]() -> ups_status {
        if (repair == nullptr || out_part_count == nullptr || out_names_length == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Repair and output pointers must not be null.");
        }
        *out_part_count = static_cast<int32_t>(repair->parts.size());
        *out_names_length = static_cast<int32_t>(repair->names.size());
        return UPS_OK;
    });
}

extern "C" UPS_API ups_status ups_repair_copy(
    const ups_repair* repair, ups_part_repair* parts, int64_t parts_capacity, char* names, int64_t names_capacity)
{
    return Guarded([&]() -> ups_status {
        if (repair == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Repair must not be null.");
        }
        if (parts != nullptr)
        {
            if (parts_capacity < static_cast<int64_t>(repair->parts.size()))
            {
                return Fail(UPS_ERROR_BUFFER_TOO_SMALL, "Buffer too small for parts.");
            }
            std::copy(repair->parts.begin(), repair->parts.end(), parts);
        }
        if (names != nullptr)
        {
            if (names_capacity < static_cast<int64_t>(repair->names.size()))
            {
                return Fail(UPS_ERROR_BUFFER_TOO_SMALL, "Buffer too small for names.");
            }
            std::copy(repair->names.begin(), repair->names.end(), names);
        }
        return UPS_OK;
    });
}
