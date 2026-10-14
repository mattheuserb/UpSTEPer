// 2D views with exact hidden-line removal, classified into analytic curves for DXF export.
#include "internal.hpp"

#include <BRepAdaptor_Curve.hxx>
#include <BRepLib.hxx>
#include <GCPnts_QuasiUniformDeflection.hxx>
#include <Geom_BSplineCurve.hxx>
#include <Geom_BezierCurve.hxx>
#include <HLRAlgo_Projector.hxx>
#include <HLRBRep_Algo.hxx>
#include <HLRBRep_HLRToShape.hxx>
#include <TopExp_Explorer.hxx>
#include <TopoDS.hxx>
#include <TopoDS_Edge.hxx>
#include <gp_Ax2.hxx>
#include <gp_Circ.hxx>
#include <gp_Elips.hxx>

#include <algorithm>
#include <cmath>
#include <memory>
#include <vector>

struct ups_drawing
{
    std::vector<ups_drawing_curve> curves;
    std::vector<double> points;
};

using ups::detail::Fail;
using ups::detail::Guarded;

namespace
{
constexpr double Pi = 3.141592653589793;
constexpr double TwoPi = 2.0 * Pi;

bool MakeProjection(int32_t view, gp_Ax2& axes)
{
    // gp_Ax2(origin, normal towards the viewer, x direction of the drawing).
    switch (view)
    {
    case UPS_VIEW_FRONT:
        axes = gp_Ax2(gp::Origin(), gp_Dir(0, -1, 0), gp_Dir(1, 0, 0));
        return true;
    case UPS_VIEW_TOP:
        axes = gp_Ax2(gp::Origin(), gp_Dir(0, 0, 1), gp_Dir(1, 0, 0));
        return true;
    case UPS_VIEW_LEFT:
        axes = gp_Ax2(gp::Origin(), gp_Dir(-1, 0, 0), gp_Dir(0, -1, 0));
        return true;
    case UPS_VIEW_RIGHT:
        axes = gp_Ax2(gp::Origin(), gp_Dir(1, 0, 0), gp_Dir(0, 1, 0));
        return true;
    case UPS_VIEW_BACK:
        axes = gp_Ax2(gp::Origin(), gp_Dir(0, 1, 0), gp_Dir(-1, 0, 0));
        return true;
    case UPS_VIEW_BOTTOM:
        axes = gp_Ax2(gp::Origin(), gp_Dir(0, 0, -1), gp_Dir(1, 0, 0));
        return true;
    case UPS_VIEW_ISO:
        axes = gp_Ax2(gp::Origin(), gp_Dir(1, -1, 1), gp_Dir(1, 1, 0));
        return true;
    default:
        return false;
    }
}

ups_drawing_curve MakeCurve(ups_curve_type type, int32_t kind)
{
    ups_drawing_curve curve{};
    curve.type = type;
    curve.kind = kind;
    return curve;
}

// HLR has no straight-line result for some edges (e.g. circles seen edge-on) and returns them as
// B-splines, often degree-1 approximations with 15 poles. A spline whose poles all lie on the chord
// is that segment (the curve stays inside the convex hull of its poles); folded curves stay polylines.
constexpr double StraightTolerance = 1e-6; // mm

void AppendLine(ups_drawing& drawing, const gp_Pnt& a, const gp_Pnt& b, int32_t kind)
{
    ups_drawing_curve curve = MakeCurve(UPS_CURVE_LINE, kind);
    curve.data[0] = a.X();
    curve.data[1] = a.Y();
    curve.data[2] = b.X();
    curve.data[3] = b.Y();
    drawing.curves.push_back(curve);
}

// Parameter of p along the segment a -> b (0 at a, length at b), and its distance from that line.
void ProjectOnSegment(const gp_Pnt& a, const gp_Pnt& b, double length, double x, double y, double& along, double& off)
{
    const double ux = (b.X() - a.X()) / length;
    const double uy = (b.Y() - a.Y()) / length;
    along = ((x - a.X()) * ux) + ((y - a.Y()) * uy);
    off = std::abs(((x - a.X()) * uy) - ((y - a.Y()) * ux));
}

bool PolesOnChord(const BRepAdaptor_Curve& adaptor, const gp_Pnt& a, const gp_Pnt& b, double length)
{
    std::vector<gp_Pnt> poles;
    if (adaptor.GetType() == GeomAbs_BSplineCurve)
    {
        const Handle(Geom_BSplineCurve) spline = adaptor.BSpline();
        for (int i = 1; i <= spline->NbPoles(); ++i)
        {
            poles.push_back(spline->Pole(i));
        }
    }
    else if (adaptor.GetType() == GeomAbs_BezierCurve)
    {
        const Handle(Geom_BezierCurve) bezier = adaptor.Bezier();
        for (int i = 1; i <= bezier->NbPoles(); ++i)
        {
            poles.push_back(bezier->Pole(i));
        }
    }
    else
    {
        return false;
    }

    // BSpline() and Bezier() return the curve with the edge's location already applied.
    return std::all_of(poles.begin(), poles.end(), [&](const gp_Pnt& pole) {
        double along = 0.0;
        double off = 0.0;
        ProjectOnSegment(a, b, length, pole.X(), pole.Y(), along, off);
        return off <= StraightTolerance;
    });
}

void AppendPolyline(ups_drawing& drawing, const BRepAdaptor_Curve& adaptor, int32_t kind, double deflection)
{
    const double first = adaptor.FirstParameter();
    const double last = adaptor.LastParameter();

    std::vector<double> points;
    GCPnts_QuasiUniformDeflection sampler(adaptor, deflection, first, last);
    if (sampler.IsDone() && sampler.NbPoints() >= 2)
    {
        for (int i = 1; i <= sampler.NbPoints(); ++i)
        {
            const gp_Pnt p = sampler.Value(i);
            points.insert(points.end(), {p.X(), p.Y()});
        }
    }
    else
    {
        constexpr int Samples = 32;
        for (int i = 0; i <= Samples; ++i)
        {
            const gp_Pnt p = adaptor.Value(first + ((last - first) * i / Samples));
            points.insert(points.end(), {p.X(), p.Y()});
        }
    }

    const gp_Pnt a = adaptor.Value(first);
    const gp_Pnt b = adaptor.Value(last);
    if (points.size() == 4)
    {
        // Two samples already describe a segment within the deflection: write it as one.
        AppendLine(drawing, gp_Pnt(points[0], points[1], 0.0), gp_Pnt(points[2], points[3], 0.0), kind);
        return;
    }
    const double length = std::hypot(b.X() - a.X(), b.Y() - a.Y());
    if (length > StraightTolerance && PolesOnChord(adaptor, a, b, length))
    {
        // Straight, but it could still run back and forth along the line: only a curve whose
        // samples stay between its end points is that one segment.
        bool between = true;
        for (std::size_t i = 0; i + 1 < points.size() && between; i += 2)
        {
            double along = 0.0;
            double off = 0.0;
            ProjectOnSegment(a, b, length, points[i], points[i + 1], along, off);
            between = along >= -StraightTolerance && along <= length + StraightTolerance;
        }
        if (between)
        {
            AppendLine(drawing, a, b, kind);
            return;
        }
    }

    ups_drawing_curve curve = MakeCurve(UPS_CURVE_POLYLINE, kind);
    curve.first_point = static_cast<int32_t>(drawing.points.size() / 2);
    curve.point_count = static_cast<int32_t>(points.size() / 2);
    drawing.points.insert(drawing.points.end(), points.begin(), points.end());
    drawing.curves.push_back(curve);
}

// HLR results lie in the projection plane (z = 0) of the projector's coordinate system.
void AppendEdges(ups_drawing& drawing, TopoDS_Shape shape, int32_t kind, double deflection)
{
    if (shape.IsNull())
    {
        return;
    }
    BRepLib::BuildCurves3d(shape);

    for (TopExp_Explorer explorer(shape, TopAbs_EDGE); explorer.More(); explorer.Next())
    {
        const BRepAdaptor_Curve adaptor(TopoDS::Edge(explorer.Current()));
        const double first = adaptor.FirstParameter();
        const double last = adaptor.LastParameter();

        switch (adaptor.GetType())
        {
        case GeomAbs_Line:
        {
            AppendLine(drawing, adaptor.Value(first), adaptor.Value(last), kind);
            break;
        }
        case GeomAbs_Circle:
        {
            const gp_Circ circle = adaptor.Circle();
            const gp_Pnt center = circle.Location();
            if (std::abs(last - first) >= TwoPi - 1e-9)
            {
                ups_drawing_curve curve = MakeCurve(UPS_CURVE_CIRCLE, kind);
                curve.data[0] = center.X();
                curve.data[1] = center.Y();
                curve.data[2] = circle.Radius();
                drawing.curves.push_back(curve);
                break;
            }

            const gp_Pnt a = adaptor.Value(first);
            const gp_Pnt b = adaptor.Value(last);
            double a0 = std::atan2(a.Y() - center.Y(), a.X() - center.X());
            double a1 = std::atan2(b.Y() - center.Y(), b.X() - center.X());
            // The parameter runs clockwise when the circle's axis points away from the viewer.
            if (circle.Axis().Direction().Z() < 0)
            {
                std::swap(a0, a1);
            }
            ups_drawing_curve curve = MakeCurve(UPS_CURVE_ARC, kind);
            curve.data[0] = center.X();
            curve.data[1] = center.Y();
            curve.data[2] = circle.Radius();
            curve.data[3] = a0;
            curve.data[4] = a1;
            drawing.curves.push_back(curve);
            break;
        }
        case GeomAbs_Ellipse:
        {
            const gp_Elips ellipse = adaptor.Ellipse();
            const gp_Pnt center = ellipse.Location();
            const gp_Dir major = ellipse.XAxis().Direction();

            // A circle seen edge-on projects to an ellipse with a vanishing minor axis. DXF rejects
            // such ellipses (ratio must be > 0), and geometrically it is a straight segment:
            // the range of cos(t) over the edge, along the major axis.
            if (ellipse.MinorRadius() <= 1e-9 * std::max(1.0, ellipse.MajorRadius()))
            {
                double lo = std::min(std::cos(first), std::cos(last));
                double hi = std::max(std::cos(first), std::cos(last));
                // cos reaches -1 at odd multiples of pi and +1 at even multiples inside [first, last].
                if (std::floor((last - Pi) / TwoPi) >= std::ceil((first - Pi) / TwoPi))
                {
                    lo = -1.0;
                }
                if (std::floor(last / TwoPi) >= std::ceil(first / TwoPi))
                {
                    hi = 1.0;
                }
                const double r = ellipse.MajorRadius();
                AppendLine(drawing,
                    gp_Pnt(center.X() + (major.X() * r * lo), center.Y() + (major.Y() * r * lo), 0.0),
                    gp_Pnt(center.X() + (major.X() * r * hi), center.Y() + (major.Y() * r * hi), 0.0), kind);
                break;
            }

            double t0 = first;
            double t1 = last;
            if (std::abs(last - first) >= TwoPi - 1e-9)
            {
                t0 = 0.0;
                t1 = TwoPi;
            }
            else if (ellipse.Axis().Direction().Z() < 0)
            {
                // Same ellipse in a counter-clockwise frame (x kept, y flipped): t -> -t.
                t0 = -last;
                t1 = -first;
            }
            ups_drawing_curve curve = MakeCurve(UPS_CURVE_ELLIPSE, kind);
            curve.data[0] = center.X();
            curve.data[1] = center.Y();
            curve.data[2] = major.X() * ellipse.MajorRadius();
            curve.data[3] = major.Y() * ellipse.MajorRadius();
            curve.data[4] = ellipse.MinorRadius() / ellipse.MajorRadius();
            curve.data[5] = t0;
            curve.data[6] = t1;
            drawing.curves.push_back(curve);
            break;
        }
        default:
            AppendPolyline(drawing, adaptor, kind, deflection);
            break;
        }
    }
}
} // namespace

extern "C" UPS_API ups_status ups_document_project(
    ups_document* document, const ups_projection_params* params, ups_drawing** out_drawing)
{
    return Guarded([&]() -> ups_status {
        if (document == nullptr || params == nullptr || out_drawing == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Document, parameters and output pointer must not be null.");
        }
        if (params->deflection <= 0.0)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Deflection must be positive.");
        }
        *out_drawing = nullptr;

        gp_Ax2 axes;
        if (!MakeProjection(params->view, axes))
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Unknown view: " + std::to_string(params->view));
        }

        Handle(HLRBRep_Algo) hlr = new HLRBRep_Algo();
        hlr->Add(document->shape);
        hlr->Projector(HLRAlgo_Projector(axes));
        hlr->Update();
        hlr->Hide();

        HLRBRep_HLRToShape shapes(hlr);
        auto drawing = std::make_unique<ups_drawing>();
        AppendEdges(*drawing, shapes.VCompound(), UPS_LINE_VISIBLE, params->deflection);
        AppendEdges(*drawing, shapes.OutLineVCompound(), UPS_LINE_VISIBLE, params->deflection);
        if (params->include_tangent != 0)
        {
            AppendEdges(*drawing, shapes.Rg1LineVCompound(), UPS_LINE_TANGENT, params->deflection);
        }
        if (params->include_hidden != 0)
        {
            AppendEdges(*drawing, shapes.HCompound(), UPS_LINE_HIDDEN, params->deflection);
            AppendEdges(*drawing, shapes.OutLineHCompound(), UPS_LINE_HIDDEN, params->deflection);
        }

        *out_drawing = drawing.release();
        return UPS_OK;
    });
}

extern "C" UPS_API void ups_drawing_free(ups_drawing* drawing)
{
    delete drawing;
}

extern "C" UPS_API ups_status ups_drawing_get_info(const ups_drawing* drawing, int32_t* out_curve_count, int32_t* out_point_count)
{
    return Guarded([&]() -> ups_status {
        if (drawing == nullptr || out_curve_count == nullptr || out_point_count == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Drawing and output pointers must not be null.");
        }
        *out_curve_count = static_cast<int32_t>(drawing->curves.size());
        *out_point_count = static_cast<int32_t>(drawing->points.size() / 2);
        return UPS_OK;
    });
}

extern "C" UPS_API ups_status ups_drawing_copy(
    const ups_drawing* drawing,
    ups_drawing_curve* curves, int64_t curves_capacity,
    double* points, int64_t points_capacity)
{
    return Guarded([&]() -> ups_status {
        if (drawing == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Drawing must not be null.");
        }
        if (curves != nullptr)
        {
            if (curves_capacity < static_cast<int64_t>(drawing->curves.size()))
            {
                return Fail(UPS_ERROR_BUFFER_TOO_SMALL, "Buffer too small for curves.");
            }
            std::copy(drawing->curves.begin(), drawing->curves.end(), curves);
        }
        if (points != nullptr)
        {
            if (points_capacity < static_cast<int64_t>(drawing->points.size()))
            {
                return Fail(UPS_ERROR_BUFFER_TOO_SMALL, "Buffer too small for points.");
            }
            std::copy(drawing->points.begin(), drawing->points.end(), points);
        }
        return UPS_OK;
    });
}
