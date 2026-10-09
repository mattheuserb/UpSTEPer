// OCCT diagnostic messages. OCCT's readers and writers report warnings, errors and statistics through
// the default messenger, which prints to stdout and would pollute CLI output. We replace its console
// printers once by a printer that forwards to the handler set with ups_set_message_handler
// (discarded while none is set); failures still reach callers through status codes.
#include "internal.hpp"

#include <Message.hxx>
#include <Message_Messenger.hxx>
#include <Message_Printer.hxx>
#include <Message_PrinterOStream.hxx>
#include <TCollection_AsciiString.hxx>

#include <mutex>

namespace
{
struct MessageHandler
{
    std::mutex mutex;
    ups_message_callback callback = nullptr;
    void* userData = nullptr;
    ups_message_level minLevel = UPS_MESSAGE_WARNING;
};

MessageHandler& Handler()
{
    static MessageHandler handler;
    return handler;
}

ups_message_level ToLevel(Message_Gravity gravity)
{
    switch (gravity)
    {
    case Message_Trace:
        return UPS_MESSAGE_TRACE;
    case Message_Info:
        return UPS_MESSAGE_INFO;
    case Message_Warning:
        return UPS_MESSAGE_WARNING;
    case Message_Alarm:
        return UPS_MESSAGE_ALARM;
    default:
        return UPS_MESSAGE_FAIL;
    }
}

// Forwards every message to the current handler. Calls are serialised by the handler mutex, which
// also guarantees that no callback runs after ups_set_message_handler has replaced it.
class CallbackPrinter : public Message_Printer
{
public:
    CallbackPrinter()
    {
        SetTraceLevel(Message_Trace); // filtered per handler instead
    }

protected:
    void send(const TCollection_AsciiString& text, const Message_Gravity gravity) const override
    {
        MessageHandler& handler = Handler();
        const std::lock_guard<std::mutex> lock(handler.mutex);
        const ups_message_level level = ToLevel(gravity);
        if (handler.callback != nullptr && level >= handler.minLevel && !text.IsEmpty())
        {
            handler.callback(handler.userData, level, text.ToCString());
        }
    }
};
} // namespace

void ups::detail::ConfigureOcctMessages()
{
    static std::once_flag once;
    std::call_once(once, [] {
        const Handle(Message_Messenger)& messenger = Message::DefaultMessenger();
        messenger->RemovePrinters(STANDARD_TYPE(Message_PrinterOStream));
        messenger->AddPrinter(new CallbackPrinter());
    });
}

extern "C" UPS_API ups_status ups_set_message_handler(
    ups_message_callback callback, void* user_data, ups_message_level min_level)
{
    return ups::detail::Guarded([&]() -> ups_status {
        if (min_level < UPS_MESSAGE_TRACE || min_level > UPS_MESSAGE_FAIL)
        {
            return ups::detail::Fail(UPS_ERROR_INVALID_ARGUMENT, "Unknown message level.");
        }

        ups::detail::ConfigureOcctMessages();
        MessageHandler& handler = Handler();
        const std::lock_guard<std::mutex> lock(handler.mutex);
        handler.callback = callback;
        handler.userData = user_data;
        handler.minLevel = min_level;
        return UPS_OK;
    });
}

std::mutex& ups::detail::TranslatorMutex()
{
    static std::mutex mutex;
    return mutex;
}
