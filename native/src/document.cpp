// STEP import via OCCT XDE (names, colours, assembly structure).
#include "internal.hpp"

#include <BRepBndLib.hxx>
#include <BRep_Builder.hxx>
#include <Bnd_Box.hxx>
#include <IFSelect_ReturnStatus.hxx>
#include <Message_ProgressScope.hxx>
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
        const std::lock_guard<std::mutex> lock(ups::detail::TranslatorMutex());
        XCAFApp_Application::GetApplication()->Close(xdeDocument);
    }
}

extern "C" UPS_API ups_status ups_document_load_step(const char* utf8_path, ups_document** out_document)
{
    return ups_document_load_step_with_progress(utf8_path, nullptr, nullptr, out_document);
}

extern "C" UPS_API ups_status ups_document_load_step_with_progress(
    const char* utf8_path, ups_progress_callback callback, void* user_data, ups_document** out_document)
{
    return Guarded([&]() -> ups_status {
        if (utf8_path == nullptr || out_document == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Path and output pointer must not be null.");
        }
        *out_document = nullptr;

        ups::detail::ConfigureOcctMessages();
        const Handle(ups::detail::CallbackProgress) progress = new ups::detail::CallbackProgress(callback, user_data);
        progress->SetPhase("Reading");
        Message_ProgressScope scope(progress->Start(), "Loading", 10);
        auto document = std::make_unique<ups_document>();

        // Declared after `document`, so on an error path the lock is released before `document`
        // is destroyed: ~ups_document takes the same, non-recursive lock.
        const std::lock_guard<std::mutex> lock(ups::detail::TranslatorMutex());
        XCAFApp_Application::GetApplication()->NewDocument("MDTV-XCAF", document->xdeDocument);

        STEPCAFControl_Reader reader;
        reader.SetColorMode(true);
        reader.SetNameMode(true);
        reader.SetLayerMode(true);

        // Parsing reports no progress in OCCT 8; it gets a fixed share of the bar.
        if (reader.ReadFile(utf8_path) != IFSelect_RetDone)
        {
            return Fail(UPS_ERROR_FILE_READ, std::string("Cannot read STEP file: ") + utf8_path);
        }
        scope.Next(2);
        progress->SetPhase("Transferring");
        if (progress->IsCancelled())
        {
            return Fail(UPS_ERROR_CANCELLED, "Loading was cancelled.");
        }

        const bool transferred = reader.Transfer(document->xdeDocument, scope.Next(8));
        if (progress->IsCancelled())
        {
            return Fail(UPS_ERROR_CANCELLED, "Loading was cancelled.");
        }
        if (!transferred)
        {
            return Fail(UPS_ERROR_TRANSFER, "STEP file was read but contains no transferable geometry.");
        }

        const Handle(XCAFDoc_ShapeTool) shapeTool = XCAFDoc_DocumentTool::ShapeTool(document->xdeDocument->Main());
        shapeTool->GetFreeShapes(document->roots);

        TopoDS_Compound compound;
        BRep_Builder builder;
        builder.MakeCompound(compound);
        for (const TDF_Label& root : document->roots)
        {
            builder.Add(compound, XCAFDoc_ShapeTool::GetShape(root));
        }
        document->shape = compound;

        TopExp::MapShapes(document->shape, TopAbs_FACE, document->faces);
        TopExp::MapShapes(document->shape, TopAbs_SOLID, document->solids);
        TopExp::MapShapesAndAncestors(document->shape, TopAbs_FACE, TopAbs_SOLID, document->faceToSolids);
        ups::detail::ResolveFaceColors(*document);

        progress->ReportDone();
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

        out_stats->solid_count = static_cast<int32_t>(document->solids.Extent());
        out_stats->shell_count = count(TopAbs_SHELL);
        out_stats->face_count = static_cast<int32_t>(document->faces.Extent());
        out_stats->edge_count = count(TopAbs_EDGE);
        out_stats->vertex_count = count(TopAbs_VERTEX);
        return UPS_OK;
    });
}

extern "C" UPS_API ups_status ups_document_get_bounds(const ups_document* document, ups_bounds* out_bounds)
{
    return Guarded([&]() -> ups_status {
        if (document == nullptr || out_bounds == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Document and output pointer must not be null.");
        }

        *out_bounds = ups_bounds{};
        Bnd_Box box;
        BRepBndLib::Add(document->shape, box, /*useTriangulation*/ false);
        if (box.IsVoid())
        {
            out_bounds->is_void = 1;
            return UPS_OK;
        }

        box.Get(out_bounds->min_x, out_bounds->min_y, out_bounds->min_z,
                out_bounds->max_x, out_bounds->max_y, out_bounds->max_z);
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
            double r = 0.0;
            double g = 0.0;
            double b = 0.0;
            color->Values(r, g, b, Quantity_TOC_sRGB);
            *out_color = ups_color{static_cast<float>(r), static_cast<float>(g), static_cast<float>(b)};
        }
        return UPS_OK;
    });
}
