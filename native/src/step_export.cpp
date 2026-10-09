// STEP export via OCCT XDE, keeping colours, names and layers.
#include "internal.hpp"

#include <DESTEP_Parameters.hxx>
#include <IFSelect_ReturnStatus.hxx>
#include <STEPCAFControl_Writer.hxx>
#include <STEPControl_StepModelType.hxx>

using ups::detail::Fail;
using ups::detail::Guarded;

extern "C" UPS_API ups_status ups_document_save_step(ups_document* document, const char* utf8_path)
{
    return ups_document_save_step_as(document, utf8_path, UPS_STEP_AP214);
}

extern "C" UPS_API ups_status ups_document_save_step_as(ups_document* document, const char* utf8_path, ups_step_schema schema)
{
    return Guarded([&]() -> ups_status {
        if (document == nullptr || utf8_path == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Document and path must not be null.");
        }
        if (schema != UPS_STEP_AP214 && schema != UPS_STEP_AP242)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Unknown STEP schema.");
        }

        ups::detail::ConfigureOcctMessages();
        const std::lock_guard<std::mutex> lock(ups::detail::TranslatorMutex());
        STEPCAFControl_Writer writer;
        writer.SetColorMode(true);
        writer.SetNameMode(true);
        writer.SetLayerMode(true);

        // Explicit parameters instead of the process-global Interface_Static settings, so the
        // schema of one save cannot leak into another.
        DESTEP_Parameters parameters;
        parameters.InitFromStatic();
        parameters.WriteSchema = schema == UPS_STEP_AP242 ? DESTEP_Parameters::WriteMode_StepSchema_AP242DIS
                                                          : DESTEP_Parameters::WriteMode_StepSchema_AP214IS;

        if (!writer.Transfer(document->xdeDocument, parameters, STEPControl_AsIs))
        {
            return Fail(UPS_ERROR_OPERATION, "Converting the document to STEP failed.");
        }
        if (writer.Write(utf8_path) != IFSelect_RetDone)
        {
            return Fail(UPS_ERROR_FILE_WRITE, std::string("Cannot write STEP file: ") + utf8_path);
        }
        return UPS_OK;
    });
}
