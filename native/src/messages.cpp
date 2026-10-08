// OCCT console output. OCCT's readers and writers print progress and statistics to stdout through
// the default messenger, which pollutes CLI output and logs. We remove its console printers once;
// failures still reach callers through status codes and ups_last_error_message.
// Set UPS_OCCT_VERBOSE=1 to keep OCCT's output for debugging.
#include "internal.hpp"

#include <Message.hxx>
#include <Message_Messenger.hxx>
#include <Message_PrinterOStream.hxx>

#include <cstdlib>
#include <cstring>
#include <mutex>

void ups::detail::ConfigureOcctMessages()
{
    static std::once_flag once;
    std::call_once(once, [] {
        const char* verbose = std::getenv("UPS_OCCT_VERBOSE");
        if (verbose != nullptr && std::strcmp(verbose, "1") == 0)
        {
            return;
        }
        Message::DefaultMessenger()->RemovePrinters(STANDARD_TYPE(Message_PrinterOStream));
    });
}

std::mutex& ups::detail::TranslatorMutex()
{
    static std::mutex mutex;
    return mutex;
}
