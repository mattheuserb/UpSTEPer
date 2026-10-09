// Bridges OCCT progress indication to the ups_progress_callback of the C ABI.
#include "internal.hpp"

#include <Message_ProgressScope.hxx>

#include <algorithm>

namespace ups::detail
{
namespace
{
// Reports in between are dropped: a GUI cannot show more, and a managed callback is not free.
constexpr std::chrono::milliseconds ReportInterval{50};
} // namespace

CallbackProgress::CallbackProgress(ups_progress_callback callback, void* userData)
    : myCallback(callback), myUserData(userData)
{
}

void CallbackProgress::SetPhase(const char* phase)
{
    {
        const std::lock_guard<std::mutex> lock(myMutex);
        myPhase = phase;
    }
    Report(/*force*/ true);
}

bool CallbackProgress::IsCancelled() const
{
    const std::lock_guard<std::mutex> lock(myMutex);
    return myCancelled;
}

void CallbackProgress::ReportDone()
{
    Report(/*force*/ true, /*done*/ true);
}

bool CallbackProgress::UserBreak()
{
    // OCCT polls this between work items; the poll doubles as a throttled progress report so a
    // cancel request is noticed even while the fraction does not move.
    Report(/*force*/ false);
    return IsCancelled();
}

void CallbackProgress::Show(const Message_ProgressScope& /*scope*/, const bool isForce)
{
    Report(isForce);
}

void CallbackProgress::Report(bool force, bool done)
{
    if (myCallback == nullptr)
    {
        return;
    }

    const std::lock_guard<std::mutex> lock(myMutex);
    const auto now = std::chrono::steady_clock::now();
    if (!force && now - myLastReport < ReportInterval)
    {
        return;
    }

    // GetPosition() is updated by OCCT under its own lock and may lag behind by one step when
    // read from another worker; keep the reported value monotonic.
    myLastFraction = done ? 1.0 : std::max(myLastFraction, std::min(GetPosition(), 1.0));
    myLastReport = now;
    if (myCallback(myUserData, myLastFraction, myPhase) != 0)
    {
        myCancelled = true;
    }
}
} // namespace ups::detail
