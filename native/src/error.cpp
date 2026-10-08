#include "internal.hpp"

namespace ups::detail
{
namespace
{
thread_local std::string t_lastError;
}

void SetLastError(std::string message)
{
    t_lastError = std::move(message);
}

void ClearLastError()
{
    t_lastError.clear();
}

const char* LastError()
{
    return t_lastError.c_str();
}
} // namespace ups::detail

extern "C" UPS_API const char* ups_last_error_message(void)
{
    return ups::detail::LastError();
}
