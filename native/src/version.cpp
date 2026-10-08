#include "internal.hpp"

#include <Standard_Version.hxx>

extern "C" UPS_API const char* ups_version(void)
{
    return UPS_VERSION;
}

extern "C" UPS_API const char* ups_occt_version(void)
{
    return OCC_VERSION_COMPLETE;
}
