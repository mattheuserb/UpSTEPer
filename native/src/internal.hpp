// Internal definitions shared by the native facade. Not part of the public ABI.
#pragma once

#include <upsteper/upsteper.h>

#include <NCollection_IndexedDataMap.hxx>
#include <NCollection_IndexedMap.hxx>
#include <NCollection_List.hxx>
#include <NCollection_Sequence.hxx>
#include <Quantity_Color.hxx>
#include <Standard_Failure.hxx>
#include <TDF_Label.hxx>
#include <TDocStd_Document.hxx>
#include <TopTools_ShapeMapHasher.hxx>
#include <TopoDS_Shape.hxx>

#include <exception>
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

    // Face id (0-based) == index in this map - 1. Ids are stable until the
    // shape is modified; see docs/adr/0005-topology-identity.md.
    ups::detail::ShapeIndexedMap faces;

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
        return Fail(UPS_ERROR_OPERATION,
                    std::string("OCCT ") + failure.DynamicType()->Name() + ": " + (text != nullptr ? text : ""));
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
