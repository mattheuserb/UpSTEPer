/*
 * UpSTEPer native facade - public C ABI.
 *
 * Design rules (see docs/adr/0002-native-interop-c-abi.md):
 *   - Plain C types only; no C++ types and no exceptions cross this boundary.
 *   - Every fallible function returns ups_status. On failure a UTF-8 message
 *     is available from ups_last_error_message() on the *same thread*.
 *   - Objects are opaque handles, released by the matching ups_*_free
 *     function. Free functions accept NULL.
 *   - Strings are UTF-8 and zero-terminated. Returned strings are owned by the
 *     library and stay valid until the next call on the same thread.
 *   - Indices exposed to callers are 0-based (OCCT is 1-based internally).
 *   - Handles are not thread-safe; callers serialise access per document.
 */
#ifndef UPSTEPER_H
#define UPSTEPER_H

#include <stdint.h>

#if defined(_WIN32)
#  if defined(UPS_BUILDING_LIBRARY)
#    define UPS_API __declspec(dllexport)
#  else
#    define UPS_API __declspec(dllimport)
#  endif
#else
#  define UPS_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* ---------------------------------------------------------------- status -- */

typedef enum ups_status
{
    UPS_OK = 0,
    UPS_ERROR_INVALID_ARGUMENT = 1,
    UPS_ERROR_FILE_READ = 2,      /* file missing, unreadable or not STEP */
    UPS_ERROR_TRANSFER = 3,       /* STEP parsed but conversion to B-Rep failed */
    UPS_ERROR_OPERATION = 4,      /* a geometric algorithm failed */
    UPS_ERROR_BUFFER_TOO_SMALL = 5,
    UPS_ERROR_INTERNAL = 99
} ups_status;

/** Message describing the last failure on the calling thread ("" if none). */
UPS_API const char* ups_last_error_message(void);

/** Version of this library, e.g. "0.1.0". */
UPS_API const char* ups_version(void);

/** Version of the linked Open CASCADE Technology, e.g. "7.9.1". */
UPS_API const char* ups_occt_version(void);

/* -------------------------------------------------------------- document -- */

typedef struct ups_document ups_document;

typedef struct ups_document_stats
{
    int32_t solid_count;
    int32_t shell_count;
    int32_t face_count;
    int32_t edge_count;
    int32_t vertex_count;
} ups_document_stats;

typedef struct ups_color
{
    float r; /* 0..1, sRGB */
    float g;
    float b;
} ups_color;

/**
 * Loads a STEP file (AP203/AP214/AP242) including colours, names and assembly
 * structure (OCCT XDE). On success *out_document receives a new handle.
 */
UPS_API ups_status ups_document_load_step(const char* utf8_path, ups_document** out_document);

UPS_API void ups_document_free(ups_document* document);

UPS_API ups_status ups_document_get_stats(const ups_document* document, ups_document_stats* out_stats);

/**
 * Colour of a face (0-based face id, identical to the ids in ups_mesh).
 * Lookup order: colour on the face itself, then on its owning solid.
 * *out_has_color is 0 when the file assigns no colour; *out_color is then untouched.
 */
UPS_API ups_status ups_document_get_face_color(
    const ups_document* document, int32_t face_id, ups_color* out_color, int32_t* out_has_color);

/* ------------------------------------------------------------------ mesh -- */

typedef struct ups_mesh ups_mesh;

typedef struct ups_tessellation_params
{
    double linear_deflection;   /* model units (mm); smaller = finer */
    double angular_deflection;  /* radians; smaller = rounder circles */
    int32_t relative;           /* non-zero: linear_deflection is relative to edge size */
} ups_tessellation_params;

typedef struct ups_mesh_info
{
    int32_t vertex_count;
    int32_t triangle_count;
} ups_mesh_info;

/**
 * Triangulates every face of the document for display. Vertices are not shared
 * between faces, so per-face normals and per-face picking stay exact.
 */
UPS_API ups_status ups_document_tessellate(
    ups_document* document, const ups_tessellation_params* params, ups_mesh** out_mesh);

UPS_API void ups_mesh_free(ups_mesh* mesh);

UPS_API ups_status ups_mesh_get_info(const ups_mesh* mesh, ups_mesh_info* out_info);

/**
 * Copies mesh data into caller-owned buffers. Required capacities (elements):
 *   positions        3 * vertex_count    (x, y, z)
 *   normals          3 * vertex_count    (unit length)
 *   indices          3 * triangle_count  (counter-clockwise seen from outside)
 *   triangle_faces   triangle_count      (0-based face id per triangle)
 * Any pointer may be NULL to skip that array.
 */
UPS_API ups_status ups_mesh_copy(
    const ups_mesh* mesh,
    float* positions, int64_t positions_capacity,
    float* normals, int64_t normals_capacity,
    uint32_t* indices, int64_t indices_capacity,
    int32_t* triangle_faces, int64_t triangle_faces_capacity);

#ifdef __cplusplus
}
#endif

#endif /* UPSTEPER_H */
