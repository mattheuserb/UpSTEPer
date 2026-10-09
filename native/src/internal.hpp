// Internal definitions shared by the native facade. Not part of the public ABI.
#pragma once

#include <upsteper/upsteper.h>

#include <Message_ProgressIndicator.hxx>
#include <NCollection_IndexedDataMap.hxx>
#include <NCollection_IndexedMap.hxx>
#include <NCollection_List.hxx>
#include <NCollection_Sequence.hxx>
#include <Quantity_Color.hxx>
#include <Standard_Failure.hxx>
#include <TDF_Label.hxx>
#include <TDocStd_Document.hxx>
#include <TopTools_ShapeMapHasher.hxx>
#include <TopLoc_Location.hxx>
#include <TopoDS_Shape.hxx>

#include <chrono>
#include <exception>
#include <functional>
#include <mutex>
#include <new>
#include <optional>
#include <string>
#include <utility>
#include <vector>

namespace ups::detail
{
// NCollection templates are used directly instead of the TopTools_* / TDF_*
// typedefs, which OCCT 8.0 deprecated. Minimum supported OCCT is 8.0.
using ShapeIndexedMap = NCollection_IndexedMap<TopoDS_Shape, TopTools_ShapeMapHasher>;
using ShapeAncestorMap =
    NCollection_IndexedDataMap<TopoDS_Shape, NCollection_List<TopoDS_Shape>, TopTools_ShapeMapHasher>;
using LabelSequence = NCollection_Sequence<TDF_Label>;
} // namespace ups::detail

// Opaque handle types declared in upsteper.h.
struct ups_document
{
    ups_document() = default;
    ~ups_document();
    ups_document(const ups_document&) = delete;
    ups_document& operator=(const ups_document&) = delete;

    Handle(TDocStd_Document) xdeDocument;

    // Compound of all top-level shapes; the geometry everything else refers to.
    TopoDS_Shape shape;

    // Top-level XDE labels (free shapes) the compound was built from.
    ups::detail::LabelSequence roots;

    // Face id (0-based) == index in this map - 1. Ids are stable until the
    // shape is modified; see docs/adr/0005-topology-identity.md.
    ups::detail::ShapeIndexedMap faces;

    // Body id (0-based) == index in this map - 1.
    ups::detail::ShapeIndexedMap solids;

    // Solids owning each face (key: face).
    ups::detail::ShapeAncestorMap faceToSolids;

    // Resolved display colour per face id (face colour, else solid colour).
    std::vector<std::optional<Quantity_Color>> faceColors;
};

struct ups_mesh
{
    std::vector<float> positions;
    std::vector<float> normals;
    std::vector<uint32_t> indices;
    std::vector<int32_t> triangleFaces;
};

namespace ups::detail
{
// ---- Assembly instances (instances.cpp) -------------------------------------------------------

// One placed occurrence of a part definition in the document shape.
struct PartInstance
{
    TDF_Label part;                // simple-shape definition label
    std::vector<TDF_Label> path;   // component labels from the root down to the part (empty: free part)
    TopLoc_Location location;      // accumulated placement of the part definition
};

// Calls visit for every part occurrence, depth-first, with the colour it inherits from its
// instances, definitions and parent assemblies (precedence: instance > definition > parent).
using InstanceVisitor = std::function<void(const PartInstance&, const std::optional<Quantity_Color>& inherited)>;
void ForEachPartInstance(const ups_document& document, const InstanceVisitor& visit);

// 1-based index of shape in map, or 0. Falls back to matching TShape and placement because the
// same placement can be represented by differently composed location chains.
int FindIndexTolerant(const ShapeIndexedMap& map, const TopoDS_Shape& shape);

// The part occurrence containing a located face or solid of the document shape, and the
// corresponding sub-shape of the part definition (unlocated relative to the definition).
bool FindOccurrence(const ups_document& document, const TopoDS_Shape& located, PartInstance& instance, TopoDS_Shape& definitionSubShape);

// Gives the occurrence its own copies of every shared definition on its path ("make independent")
// so it can be coloured without affecting other occurrences. Keeps all face and body ids.
void MakeOccurrenceUnique(ups_document& document, const PartInstance& instance);

// Recomputes ups_document::faceColors from the XDE colour attributes (instances.cpp).
void ResolveFaceColors(ups_document& document);

// ---- Progress (progress.cpp) ------------------------------------------------------------------

// Forwards OCCT progress to a ups_progress_callback and turns its answer into UserBreak().
// Thread-safe: OCCT calls Show/UserBreak from parallel workers (e.g. BRepMesh).
class CallbackProgress : public Message_ProgressIndicator
{
public:
    // callback may be null: then nothing is reported and nothing is cancelled.
    CallbackProgress(ups_progress_callback callback, void* userData);

    // Sets the label passed with following reports and reports immediately.
    void SetPhase(const char* phase);

    // True once the callback asked to cancel.
    bool IsCancelled() const;

    // Reports completion (fraction 1) once the operation succeeded.
    void ReportDone();

    bool UserBreak() override;
    void Show(const Message_ProgressScope& scope, const bool isForce) override;

private:
    void Report(bool force, bool done = false);

    ups_progress_callback myCallback;
    void* myUserData;
    const char* myPhase = "";
    double myLastFraction = 0.0;
    std::chrono::steady_clock::time_point myLastReport{};
    bool myCancelled = false;
    mutable std::mutex myMutex;
};

// Silences OCCT console output once per process (see messages.cpp). Call before using
// OCCT readers or writers.
void ConfigureOcctMessages();

// OCCT's STEP translators and the XDE application singleton use process-global state
// (Interface_Static parameters, protocol initialisation, document registry) that is not
// thread-safe. Every call that reads/writes STEP or creates/closes an XDE document holds this
// lock. Meshing and colour edits on *different* documents do not need it.
std::mutex& TranslatorMutex();

void SetLastError(std::string message);
void ClearLastError();
const char* LastError();

inline ups_status Fail(ups_status status, std::string message)
{
    SetLastError(std::move(message));
    return status;
}

// Runs body and converts every exception into a status code. Nothing may
// propagate across the C ABI.
template <typename TBody>
ups_status Guarded(TBody&& body) noexcept
{
    ClearLastError();
    try
    {
        return body();
    }
    catch (const Standard_Failure& failure)
    {
        // Must precede std::exception: Standard_Failure derives from it since OCCT 8.0.
        const char* text = failure.what();
        return Fail(UPS_ERROR_OPERATION, std::string("OCCT: ") + (text != nullptr && *text != '\0' ? text : "unspecified failure"));
    }
    catch (const std::bad_alloc&)
    {
        return Fail(UPS_ERROR_INTERNAL, "Out of memory.");
    }
    catch (const std::exception& exception)
    {
        return Fail(UPS_ERROR_INTERNAL, exception.what());
    }
    catch (...)
    {
        return Fail(UPS_ERROR_INTERNAL, "Unknown native exception.");
    }
}
} // namespace ups::detail
