// STEP import via OCCT XDE (names, colours, assembly structure).
#include "internal.hpp"

#include <BRep_Builder.hxx>
#include <IFSelect_ReturnStatus.hxx>
#include <STEPCAFControl_Reader.hxx>

#include <TopExp.hxx>
#include <TopoDS.hxx>
#include <TopoDS_Compound.hxx>
#include <XCAFApp_Application.hxx>
#include <XCAFDoc_ColorTool.hxx>
#include <XCAFDoc_DocumentTool.hxx>
#include <XCAFDoc_ShapeTool.hxx>

#include <memory>

using ups::detail::Fail;
using ups::detail::Guarded;

ups_document::~ups_document()
{
    if (!xdeDocument.IsNull() && xdeDocument->IsOpened())
    {
        XCAFApp_Application::GetApplication()->Close(xdeDocument);
    }
}

namespace
{
std::optional<Quantity_Color> FindColor(const Handle(XCAFDoc_ColorTool)& colorTool, const TopoDS_Shape& shape)
{
    Quantity_Color color;
    if (colorTool->GetColor(shape, XCAFDoc_ColorSurf, color) || colorTool->GetColor(shape, XCAFDoc_ColorGen, color))
    {
        return color;
    }
    return std::nullopt;
}

// Resolves the display colour of every face: own colour, else owning solid,
// else the single top-level shape. Assembly instance colours are a known gap
// (tracked in docs/roadmap.md, milestone M2).
void ResolveFaceColors(ups_document& document, const ups::detail::LabelSequence& roots)
{
    const Handle(XCAFDoc_ColorTool) colorTool = XCAFDoc_DocumentTool::ColorTool(document.xdeDocument->Main());

    ups::detail::ShapeAncestorMap faceToSolids;
    TopExp::MapShapesAndAncestors(document.shape, TopAbs_FACE, TopAbs_SOLID, faceToSolids);

    std::optional<Quantity_Color> rootColor;
    if (roots.Length() == 1)
    {
        rootColor = FindColor(colorTool, XCAFDoc_ShapeTool::GetShape(roots.First()));
    }

    document.faceColors.assign(static_cast<size_t>(document.faces.Extent()), std::nullopt);
    for (int index = 1; index <= document.faces.Extent(); ++index)
    {
        const TopoDS_Shape& face = document.faces(index);
        std::optional<Quantity_Color> color = FindColor(colorTool, face);

        if (!color && faceToSolids.Contains(face))
        {
            for (const TopoDS_Shape& solid : faceToSolids.FindFromKey(face))
            {
                color = FindColor(colorTool, solid);
                if (color)
                {
                    break;
                }
            }
        }

        document.faceColors[static_cast<size_t>(index - 1)] = color ? color : rootColor;
    }
}
} // namespace

extern "C" UPS_API ups_status ups_document_load_step(const char* utf8_path, ups_document** out_document)
{
    return Guarded([&]() -> ups_status {
        if (utf8_path == nullptr || out_document == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Path and output pointer must not be null.");
        }
        *out_document = nullptr;

        auto document = std::make_unique<ups_document>();
        XCAFApp_Application::GetApplication()->NewDocument("MDTV-XCAF", document->xdeDocument);

        STEPCAFControl_Reader reader;
        reader.SetColorMode(true);
        reader.SetNameMode(true);
        reader.SetLayerMode(true);

        if (reader.ReadFile(utf8_path) != IFSelect_RetDone)
        {
            return Fail(UPS_ERROR_FILE_READ, std::string("Cannot read STEP file: ") + utf8_path);
        }
        if (!reader.Transfer(document->xdeDocument))
        {
            return Fail(UPS_ERROR_TRANSFER, "STEP file was read but contains no transferable geometry.");
        }

        const Handle(XCAFDoc_ShapeTool) shapeTool = XCAFDoc_DocumentTool::ShapeTool(document->xdeDocument->Main());
        ups::detail::LabelSequence roots;
        shapeTool->GetFreeShapes(roots);

        TopoDS_Compound compound;
        BRep_Builder builder;
        builder.MakeCompound(compound);
        for (const TDF_Label& root : roots)
        {
            builder.Add(compound, XCAFDoc_ShapeTool::GetShape(root));
        }
        document->shape = compound;

        TopExp::MapShapes(document->shape, TopAbs_FACE, document->faces);
        ResolveFaceColors(*document, roots);

        *out_document = document.release();
        return UPS_OK;
    });
}

extern "C" UPS_API void ups_document_free(ups_document* document)
{
    delete document;
}

extern "C" UPS_API ups_status ups_document_get_stats(const ups_document* document, ups_document_stats* out_stats)
{
    return Guarded([&]() -> ups_status {
        if (document == nullptr || out_stats == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Document and output pointer must not be null.");
        }

        auto count = [&](TopAbs_ShapeEnum type) {
            ups::detail::ShapeIndexedMap map;
            TopExp::MapShapes(document->shape, type, map);
            return static_cast<int32_t>(map.Extent());
        };

        out_stats->solid_count = count(TopAbs_SOLID);
        out_stats->shell_count = count(TopAbs_SHELL);
        out_stats->face_count = static_cast<int32_t>(document->faces.Extent());
        out_stats->edge_count = count(TopAbs_EDGE);
        out_stats->vertex_count = count(TopAbs_VERTEX);
        return UPS_OK;
    });
}

extern "C" UPS_API ups_status ups_document_get_face_color(
    const ups_document* document, int32_t face_id, ups_color* out_color, int32_t* out_has_color)
{
    return Guarded([&]() -> ups_status {
        if (document == nullptr || out_color == nullptr || out_has_color == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Document and output pointers must not be null.");
        }
        if (face_id < 0 || face_id >= static_cast<int32_t>(document->faceColors.size()))
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Face id out of range: " + std::to_string(face_id));
        }

        const std::optional<Quantity_Color>& color = document->faceColors[static_cast<size_t>(face_id)];
        *out_has_color = color ? 1 : 0;
        if (color)
        {
            Standard_Real r = 0.0;
            Standard_Real g = 0.0;
            Standard_Real b = 0.0;
            color->Values(r, g, b, Quantity_TOC_sRGB);
            *out_color = ups_color{static_cast<float>(r), static_cast<float>(g), static_cast<float>(b)};
        }
        return UPS_OK;
    });
}
