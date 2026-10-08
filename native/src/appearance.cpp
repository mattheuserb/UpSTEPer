// Face and body colours stored in the XDE document (written to STEP by step_export.cpp).
#include "internal.hpp"

#include <TopExp.hxx>
#include <XCAFDoc_ColorTool.hxx>
#include <XCAFDoc_DocumentTool.hxx>
#include <XCAFDoc_ShapeTool.hxx>

using ups::detail::Fail;
using ups::detail::Guarded;

namespace
{
// Resolves a target id to its shape; returns false (with the error set) for invalid ids.
ups_status ResolveTarget(const ups_document& document, ups_target_kind kind, int32_t id, TopoDS_Shape& shape)
{
    const ups::detail::ShapeIndexedMap* map = nullptr;
    switch (kind)
    {
    case UPS_TARGET_FACE:
        map = &document.faces;
        break;
    case UPS_TARGET_BODY:
        map = &document.solids;
        break;
    default:
        return Fail(UPS_ERROR_INVALID_ARGUMENT, "Unknown target kind: " + std::to_string(static_cast<int>(kind)));
    }

    if (id < 0 || id >= map->Extent())
    {
        return Fail(UPS_ERROR_INVALID_ARGUMENT, "Target id out of range: " + std::to_string(id));
    }

    shape = (*map)(id + 1);
    return UPS_OK;
}

// The XDE label holding attributes of shape, or a null label when it has none yet.
TDF_Label FindLabel(const ups_document& document, const TopoDS_Shape& shape)
{
    const Handle(XCAFDoc_ShapeTool) shapeTool = XCAFDoc_DocumentTool::ShapeTool(document.xdeDocument->Main());
    TDF_Label label;
    return shapeTool->Search(shape, label) ? label : TDF_Label();
}

// Finds the label of shape or creates a sub-shape label under the top-level part that contains it.
// Shapes inside assembly instances are not supported yet (their faces live in referenced parts
// with instance locations, docs/roadmap.md M2).
TDF_Label FindOrAddLabel(const ups_document& document, const TopoDS_Shape& shape)
{
    TDF_Label label = FindLabel(document, shape);
    if (!label.IsNull())
    {
        return label;
    }

    const Handle(XCAFDoc_ShapeTool) shapeTool = XCAFDoc_DocumentTool::ShapeTool(document.xdeDocument->Main());
    for (const TDF_Label& root : document.roots)
    {
        if (XCAFDoc_ShapeTool::IsAssembly(root))
        {
            continue;
        }

        ups::detail::ShapeIndexedMap subShapes;
        TopExp::MapShapes(XCAFDoc_ShapeTool::GetShape(root), shape.ShapeType(), subShapes);
        if (subShapes.Contains(shape))
        {
            return shapeTool->AddSubShape(root, shape);
        }
    }

    return TDF_Label();
}

void ToUps(const Quantity_Color& color, ups_color& out)
{
    double r = 0.0;
    double g = 0.0;
    double b = 0.0;
    color.Values(r, g, b, Quantity_TOC_sRGB);
    out = ups_color{static_cast<float>(r), static_cast<float>(g), static_cast<float>(b)};
}
} // namespace

extern "C" UPS_API ups_status ups_document_get_face_body(const ups_document* document, int32_t face_id, int32_t* out_body_id)
{
    return Guarded([&]() -> ups_status {
        if (document == nullptr || out_body_id == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Document and output pointer must not be null.");
        }

        TopoDS_Shape face;
        if (const ups_status status = ResolveTarget(*document, UPS_TARGET_FACE, face_id, face); status != UPS_OK)
        {
            return status;
        }

        *out_body_id = -1;
        if (document->faceToSolids.Contains(face))
        {
            const NCollection_List<TopoDS_Shape>& solids = document->faceToSolids.FindFromKey(face);
            if (!solids.IsEmpty())
            {
                *out_body_id = static_cast<int32_t>(document->solids.FindIndex(solids.First())) - 1;
            }
        }
        return UPS_OK;
    });
}

extern "C" UPS_API ups_status ups_document_get_assigned_color(
    const ups_document* document, ups_target_kind kind, int32_t id, ups_color* out_color, int32_t* out_has_color)
{
    return Guarded([&]() -> ups_status {
        if (document == nullptr || out_color == nullptr || out_has_color == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Document and output pointers must not be null.");
        }

        TopoDS_Shape shape;
        if (const ups_status status = ResolveTarget(*document, kind, id, shape); status != UPS_OK)
        {
            return status;
        }

        *out_has_color = 0;
        const TDF_Label label = FindLabel(*document, shape);
        if (label.IsNull())
        {
            return UPS_OK;
        }

        const Handle(XCAFDoc_ColorTool) colorTool = XCAFDoc_DocumentTool::ColorTool(document->xdeDocument->Main());
        Quantity_Color color;
        if (colorTool->GetColor(label, XCAFDoc_ColorSurf, color) || colorTool->GetColor(label, XCAFDoc_ColorGen, color))
        {
            ToUps(color, *out_color);
            *out_has_color = 1;
        }
        return UPS_OK;
    });
}

extern "C" UPS_API ups_status ups_document_set_color(
    ups_document* document, ups_target_kind kind, int32_t id, const ups_color* color)
{
    return Guarded([&]() -> ups_status {
        if (document == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Document must not be null.");
        }

        TopoDS_Shape shape;
        if (const ups_status status = ResolveTarget(*document, kind, id, shape); status != UPS_OK)
        {
            return status;
        }

        const Handle(XCAFDoc_ColorTool) colorTool = XCAFDoc_DocumentTool::ColorTool(document->xdeDocument->Main());
        if (color == nullptr)
        {
            // Nothing assigned means nothing to remove.
            const TDF_Label label = FindLabel(*document, shape);
            if (!label.IsNull())
            {
                colorTool->UnSetColor(label, XCAFDoc_ColorSurf);
                colorTool->UnSetColor(label, XCAFDoc_ColorGen);
            }
        }
        else
        {
            const TDF_Label label = FindOrAddLabel(*document, shape);
            if (label.IsNull())
            {
                return Fail(UPS_ERROR_OPERATION, "Colouring shapes inside assembly instances is not supported yet.");
            }

            // Replace a generic colour so the new surface colour is the only one on this label.
            colorTool->UnSetColor(label, XCAFDoc_ColorGen);
            colorTool->SetColor(label, Quantity_Color(color->r, color->g, color->b, Quantity_TOC_sRGB), XCAFDoc_ColorSurf);
        }

        ups::detail::ResolveFaceColors(*document);
        return UPS_OK;
    });
}
