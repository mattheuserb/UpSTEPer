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
 *   - Handles are not thread-safe; callers serialise access per document. Different
 *     documents may be used from different threads concurrently: STEP reading/writing and
 *     document creation/destruction are serialised internally (OCCT translators use global state).
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
    UPS_ERROR_FILE_WRITE = 6,     /* output file cannot be written */
    UPS_ERROR_CANCELLED = 7,      /* the progress callback requested cancellation; no output was produced */
    UPS_ERROR_INTERNAL = 99
} ups_status;

/** Message describing the last failure on the calling thread ("" if none). */
UPS_API const char* ups_last_error_message(void);

/** Version of this library, e.g. "0.1.0". */
UPS_API const char* ups_version(void);

/** Version of the linked Open CASCADE Technology, e.g. "7.9.1". */
UPS_API const char* ups_occt_version(void);

/* -------------------------------------------------------------- progress -- */

/**
 * Progress report of a long operation; return non-zero to cancel it.
 *   fraction  overall progress 0..1, never decreasing within one operation
 *   phase     short English label ("Reading", "Transferring", "Meshing"); valid during the call
 * Calls are serialised but may come from any thread (OCCT meshes in parallel), and are
 * throttled to roughly 20 per second. Cancellation is not immediate: OCCT checks it between
 * work items, and not at all while a STEP file is parsed (only before transfer starts).
 * The callback must not call back into this library and must not throw or longjmp.
 */
typedef int32_t (*ups_progress_callback)(void* user_data, double fraction, const char* phase);

/* -------------------------------------------------------------- messages -- */

typedef enum ups_message_level
{
    UPS_MESSAGE_TRACE = 0,   /* OCCT internals, very verbose */
    UPS_MESSAGE_INFO = 1,    /* statistics and progress notes of translators */
    UPS_MESSAGE_WARNING = 2, /* recoverable problems, e.g. a STEP entity that was skipped */
    UPS_MESSAGE_ALARM = 3,   /* serious problems; the result may be incomplete */
    UPS_MESSAGE_FAIL = 4     /* an operation failed */
} ups_message_level;

/** Receives one OCCT diagnostic message; text is UTF-8 and valid during the call. */
typedef void (*ups_message_callback)(void* user_data, ups_message_level level, const char* text);

/**
 * Routes OCCT's diagnostic messages (process-wide) to callback, dropping those below min_level.
 * Pass NULL to discard them again (the default; OCCT never prints to the console). Calls are
 * serialised and may come from any thread; once this function returns, the previous callback is
 * never called again. The callback must not call into this library and must not throw or longjmp.
 */
UPS_API ups_status ups_set_message_handler(ups_message_callback callback, void* user_data, ups_message_level min_level);

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

typedef struct ups_bounds
{
    double min_x; /* mm */
    double min_y;
    double min_z;
    double max_x;
    double max_y;
    double max_z;
    int32_t is_void; /* non-zero: the model has no geometry; the coordinates are then 0 */
} ups_bounds;

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

/**
 * Like ups_document_load_step, reporting progress to callback (may be NULL). Returns
 * UPS_ERROR_CANCELLED, with *out_document NULL, when the callback requested cancellation.
 */
UPS_API ups_status ups_document_load_step_with_progress(
    const char* utf8_path, ups_progress_callback callback, void* user_data, ups_document** out_document);

UPS_API void ups_document_free(ups_document* document);

UPS_API ups_status ups_document_get_stats(const ups_document* document, ups_document_stats* out_stats);

/**
 * Axis-aligned bounding box of the model from its exact geometry (not a mesh). It may be
 * slightly larger than the model (tolerances, spline control points); good for scaling
 * display tolerances and views, not for measuring.
 */
UPS_API ups_status ups_document_get_bounds(const ups_document* document, ups_bounds* out_bounds);

/**
 * Colour of a face (0-based face id, identical to the ids in ups_mesh).
 * Lookup order: colour on the face itself, then on its owning solid.
 * *out_has_color is 0 when the file assigns no colour; *out_color is then untouched.
 */
UPS_API ups_status ups_document_get_face_color(
    const ups_document* document, int32_t face_id, ups_color* out_color, int32_t* out_has_color);

/* ------------------------------------------------------------ appearance -- */

typedef enum ups_target_kind
{
    UPS_TARGET_FACE = 0, /* id is a face id (0 .. face_count-1) */
    UPS_TARGET_BODY = 1  /* id is a body (solid) id (0 .. solid_count-1) */
} ups_target_kind;

/**
 * Body (solid) owning a face. *out_body_id is -1 for faces that belong to no solid
 * (e.g. loose shells or surfaces).
 */
UPS_API ups_status ups_document_get_face_body(const ups_document* document, int32_t face_id, int32_t* out_body_id);

/**
 * Colour assigned directly to a face or body in the document, ignoring inheritance
 * (a face without its own colour reports none even if its body is coloured).
 * Use ups_document_get_face_color for the resolved display colour.
 */
UPS_API ups_status ups_document_get_assigned_color(
    const ups_document* document, ups_target_kind kind, int32_t id, ups_color* out_color, int32_t* out_has_color);

/**
 * Assigns a colour to a face or body, or removes the assigned colour when color is NULL.
 * Face colours take precedence over body colours. Display colours (ups_document_get_face_color)
 * are updated immediately; existing meshes stay valid (topology is unchanged).
 * Fails with UPS_ERROR_OPERATION for shapes inside assembly instances (not supported yet).
 */
UPS_API ups_status ups_document_set_color(
    ups_document* document, ups_target_kind kind, int32_t id, const ups_color* color);

/** Writes the document as STEP (AP214) including colours, names and layers. */
UPS_API ups_status ups_document_save_step(ups_document* document, const char* utf8_path);

/* ------------------------------------------------------------ model tree -- */

typedef struct ups_tree ups_tree;

typedef enum ups_node_kind
{
    UPS_NODE_ASSEMBLY = 0, /* XDE assembly (instance or top level) */
    UPS_NODE_PART = 1,     /* simple shape (part), possibly an instance in an assembly */
    UPS_NODE_BODY = 2      /* a solid of its parent part; body_id identifies it */
} ups_node_kind;

/** One node of the product structure, flattened in depth-first order (parents first). */
typedef struct ups_tree_node
{
    int32_t parent;      /* index of the parent node, -1 for roots */
    int32_t kind;        /* ups_node_kind */
    int32_t body_id;     /* body (solid) id for UPS_NODE_BODY, else -1 */
    int32_t name_offset; /* UTF-8 name in the names buffer (not zero-terminated) */
    int32_t name_length; /* in bytes; 0 = unnamed */
} ups_tree_node;

/** Builds the product structure (assemblies, parts, bodies) from the XDE document. */
UPS_API ups_status ups_document_get_tree(const ups_document* document, ups_tree** out_tree);

UPS_API void ups_tree_free(ups_tree* tree);

UPS_API ups_status ups_tree_get_info(const ups_tree* tree, int32_t* out_node_count, int32_t* out_names_length);

UPS_API ups_status ups_tree_copy(
    const ups_tree* tree, ups_tree_node* nodes, int64_t nodes_capacity, char* names, int64_t names_capacity);

/* --------------------------------------------------------------- drawing -- */

typedef struct ups_drawing ups_drawing;

/** Viewing direction. Z is up; FRONT looks along +Y (viewer at -Y). */
typedef enum ups_view_direction
{
    UPS_VIEW_FRONT = 0,
    UPS_VIEW_TOP = 1,    /* from +Z, X right, Y up */
    UPS_VIEW_LEFT = 2,   /* from -X */
    UPS_VIEW_RIGHT = 3,  /* from +X */
    UPS_VIEW_BACK = 4,   /* from +Y */
    UPS_VIEW_BOTTOM = 5, /* from -Z */
    UPS_VIEW_ISO = 6     /* from (+1, -1, +1): front-right-top */
} ups_view_direction;

typedef enum ups_line_kind
{
    UPS_LINE_VISIBLE = 0, /* visible sharp edges and silhouettes */
    UPS_LINE_HIDDEN = 1,  /* hidden sharp edges and silhouettes */
    UPS_LINE_TANGENT = 2  /* visible smooth (tangent) edges between faces */
} ups_line_kind;

typedef enum ups_curve_type
{
    UPS_CURVE_LINE = 0,    /* data: x1 y1 x2 y2 */
    UPS_CURVE_ARC = 1,     /* data: cx cy r a0 a1 — counter-clockwise from a0 to a1, radians */
    UPS_CURVE_CIRCLE = 2,  /* data: cx cy r */
    UPS_CURVE_ELLIPSE = 3, /* data: cx cy mx my ratio t0 t1 — (mx,my) = major semi-axis vector,
                              ratio = minor/major, counter-clockwise from parameter t0 to t1 */
    UPS_CURVE_POLYLINE = 4 /* points[first_point .. first_point+point_count) */
} ups_curve_type;

/** One 2D curve of a projected view, in model units (mm) in the view plane. */
typedef struct ups_drawing_curve
{
    int32_t type; /* ups_curve_type */
    int32_t kind; /* ups_line_kind */
    double data[7];
    int32_t first_point;
    int32_t point_count;
} ups_drawing_curve;

typedef struct ups_projection_params
{
    int32_t view;            /* ups_view_direction */
    double deflection;       /* chord tolerance for curves exported as polylines (mm) */
    int32_t include_hidden;  /* non-zero: compute hidden lines */
    int32_t include_tangent; /* non-zero: include smooth edges between faces */
} ups_projection_params;

/**
 * Projects the model with exact hidden-line removal (HLRBRep_Algo). Lines, circles, arcs and
 * ellipses stay analytic; other curves become polylines within params->deflection.
 */
UPS_API ups_status ups_document_project(
    ups_document* document, const ups_projection_params* params, ups_drawing** out_drawing);

UPS_API void ups_drawing_free(ups_drawing* drawing);

UPS_API ups_status ups_drawing_get_info(const ups_drawing* drawing, int32_t* out_curve_count, int32_t* out_point_count);

/** Copies curves and polyline points (x, y pairs: capacity in doubles = 2 * point_count). */
UPS_API ups_status ups_drawing_copy(
    const ups_drawing* drawing,
    ups_drawing_curve* curves, int64_t curves_capacity,
    double* points, int64_t points_capacity);

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
 * between faces, so per-face normals and per-face picking stay exact. An existing
 * triangulation is discarded first, so a coarser call after a finer one is coarser.
 */
UPS_API ups_status ups_document_tessellate(
    ups_document* document, const ups_tessellation_params* params, ups_mesh** out_mesh);

/**
 * Like ups_document_tessellate, reporting progress to callback (may be NULL). On
 * UPS_ERROR_CANCELLED *out_mesh is NULL and the document keeps no partial triangulation.
 */
UPS_API ups_status ups_document_tessellate_with_progress(
    ups_document* document, const ups_tessellation_params* params,
    ups_progress_callback callback, void* user_data, ups_mesh** out_mesh);

UPS_API void ups_mesh_free(ups_mesh* mesh);

/* ----------------------------------------------------------------- lines -- */

typedef struct ups_lines ups_lines;

/**
 * Discretises the model's B-Rep edges into line segments for display (within deflection, mm).
 * Seam edges (e.g. the artificial seam of a cylinder) and degenerated edges are skipped.
 * Each segment carries the 0-based id of its edge.
 */
UPS_API ups_status ups_document_tessellate_edges(ups_document* document, double deflection, ups_lines** out_lines);

UPS_API void ups_lines_free(ups_lines* lines);

UPS_API ups_status ups_lines_get_info(const ups_lines* lines, int32_t* out_vertex_count, int32_t* out_segment_count);

/**
 * Copies line data. Capacities (elements): positions 3 * vertex_count, indices 2 * segment_count
 * (vertex index pairs, for GL_LINES), segment_edges segment_count. Any pointer may be NULL.
 */
UPS_API ups_status ups_lines_copy(
    const ups_lines* lines,
    float* positions, int64_t positions_capacity,
    uint32_t* indices, int64_t indices_capacity,
    int32_t* segment_edges, int64_t segment_edges_capacity);

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
