// STEP export via OCCT XDE, keeping colours, names and layers.
#include "internal.hpp"

#include <IFSelect_ReturnStatus.hxx>
#include <STEPCAFControl_Writer.hxx>
#include <STEPControl_StepModelType.hxx>

using ups::detail::Fail;
using ups::detail::Guarded;

extern "C" UPS_API ups_status ups_document_save_step(ups_document* document, const char* utf8_path)
{
    return Guarded([&]() -> ups_status {
        if (document == nullptr || utf8_path == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Document and path must not be null.");
        }

        ups::detail::ConfigureOcctMessages();
        const std::lock_guard<std::mutex> lock(ups::detail::TranslatorMutex());
        STEPCAFControl_Writer writer;
        writer.SetColorMode(true);
        writer.SetNameMode(true);
        writer.SetLayerMode(true);

        if (!writer.Transfer(document->xdeDocument, STEPControl_AsIs))
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
