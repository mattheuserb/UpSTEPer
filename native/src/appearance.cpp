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

// The label carrying the colour of a face/solid of a part occurrence: the definition label itself
// when the shape is the whole definition (single-solid part), else its sub-shape label. Returns a
// null label when the sub-shape has no label yet and `create` is false.
TDF_Label AttributeLabel(const ups_document& document, const ups::detail::PartInstance& instance,
                         const TopoDS_Shape& definitionSubShape, bool create)
{
    if (definitionSubShape.IsSame(XCAFDoc_ShapeTool::GetShape(instance.part)))
    {
        return instance.part;
    }

    const Handle(XCAFDoc_ShapeTool) shapeTool = XCAFDoc_DocumentTool::ShapeTool(document.xdeDocument->Main());
    TDF_Label label;
    if (shapeTool->FindSubShape(instance.part, definitionSubShape, label))
    {
        return label;
    }
    return create ? shapeTool->AddSubShape(instance.part, definitionSubShape) : TDF_Label();
}

// Finds the part occurrence of the target's shape in the document.
ups_status FindTargetOccurrence(const ups_document& document, ups_target_kind kind, int32_t id,
                                ups::detail::PartInstance& instance, TopoDS_Shape& definitionSubShape)
{
    TopoDS_Shape shape;
    if (const ups_status status = ResolveTarget(document, kind, id, shape); status != UPS_OK)
    {
        return status;
    }
    if (!ups::detail::FindOccurrence(document, shape, instance, definitionSubShape))
    {
        return Fail(UPS_ERROR_OPERATION, "Shape does not belong to any part of the product structure.");
    }
    return UPS_OK;
}

std::optional<Quantity_Color> AssignedColor(const Handle(XCAFDoc_ColorTool)& colors, const TDF_Label& label)
{
    Quantity_Color color;
    if (!label.IsNull() && (colors->GetColor(label, XCAFDoc_ColorSurf, color) || colors->GetColor(label, XCAFDoc_ColorGen, color)))
    {
        return color;
    }
    return std::nullopt;
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

        ups::detail::PartInstance instance;
        TopoDS_Shape subShape;
        if (const ups_status status = FindTargetOccurrence(*document, kind, id, instance, subShape); status != UPS_OK)
        {
            return status;
        }

        const Handle(XCAFDoc_ColorTool) colorTool = XCAFDoc_DocumentTool::ColorTool(document->xdeDocument->Main());
        const std::optional<Quantity_Color> color = AssignedColor(colorTool, AttributeLabel(*document, instance, subShape, false));
        *out_has_color = color ? 1 : 0;
        if (color)
        {
            ToUps(*color, *out_color);
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

        ups::detail::PartInstance instance;
        TopoDS_Shape subShape;
        if (const ups_status status = FindTargetOccurrence(*document, kind, id, instance, subShape); status != UPS_OK)
        {
            return status;
        }

        const Handle(XCAFDoc_ColorTool) colorTool = XCAFDoc_DocumentTool::ColorTool(document->xdeDocument->Main());
        if (color == nullptr && !AssignedColor(colorTool, AttributeLabel(*document, instance, subShape, false)))
        {
            return UPS_OK; // nothing assigned, nothing to remove
        }

        // Colour only this occurrence: shared definitions on its path get their own copies first
        // (project decision, docs/adr/0005). Ids are kept, so look the target up again afterwards.
        ups::detail::MakeOccurrenceUnique(*document, instance);
        if (const ups_status status = FindTargetOccurrence(*document, kind, id, instance, subShape); status != UPS_OK)
        {
            return status;
        }

        const TDF_Label label = AttributeLabel(*document, instance, subShape, true);
        if (label.IsNull())
        {
            return Fail(UPS_ERROR_OPERATION, "Cannot attach a colour to this shape.");
        }

        colorTool->UnSetColor(label, XCAFDoc_ColorGen);
        if (color == nullptr)
        {
            colorTool->UnSetColor(label, XCAFDoc_ColorSurf);
        }
        else
        {
            colorTool->SetColor(label, Quantity_Color(color->r, color->g, color->b, Quantity_TOC_sRGB), XCAFDoc_ColorSurf);
        }

        ups::detail::ResolveFaceColors(*document);
        return UPS_OK;
    });
}
