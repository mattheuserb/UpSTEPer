// Display tessellation. Each face gets its own vertices so that per-face
// normals, colours and picking (via triangle -> face id) are exact.
#include "internal.hpp"

#include <BRepLib_ToolTriangulatedShape.hxx>
#include <BRepMesh_IncrementalMesh.hxx>
#include <BRep_Tool.hxx>
#include <Poly_Triangulation.hxx>
#include <TopLoc_Location.hxx>
#include <TopoDS.hxx>
#include <TopoDS_Face.hxx>
#include <gp_Vec.hxx>

#include <algorithm>
#include <memory>

using ups::detail::Fail;
using ups::detail::Guarded;

namespace
{
void AppendFace(ups_mesh& mesh, const TopoDS_Face& face, int32_t faceId)
{
    TopLoc_Location location;
    const Handle(Poly_Triangulation) triangulation = BRep_Tool::Triangulation(face, location);
    if (triangulation.IsNull() || triangulation->NbTriangles() == 0)
    {
        return;
    }
    if (!triangulation->HasNormals())
    {
        BRepLib_ToolTriangulatedShape::ComputeNormals(face, triangulation);
    }

    const gp_Trsf transform = location.Transformation();
    // OCCT convention: swap winding for reversed faces and mirroring transforms.
    const bool flipWinding = (face.Orientation() == TopAbs_REVERSED) != transform.IsNegative();
    const auto baseVertex = static_cast<uint32_t>(mesh.positions.size() / 3);
    const size_t firstNormal = mesh.normals.size();

    for (int node = 1; node <= triangulation->NbNodes(); ++node)
    {
        const gp_Pnt point = triangulation->Node(node).Transformed(transform);
        gp_Vec normal(triangulation->Normal(node));
        normal.Transform(transform);
        if (normal.SquareMagnitude() > 0.0)
        {
            normal.Normalize();
        }
        mesh.positions.insert(mesh.positions.end(), {static_cast<float>(point.X()), static_cast<float>(point.Y()), static_cast<float>(point.Z())});
        mesh.normals.insert(mesh.normals.end(), {static_cast<float>(normal.X()), static_cast<float>(normal.Y()), static_cast<float>(normal.Z())});
    }

    // Area-weighted geometric normal of the (correctly wound) triangles; used to
    // make vertex normals agree with the winding independent of OCCT version.
    gp_Vec windingNormal(0.0, 0.0, 0.0);
    gp_Vec vertexNormalSum(0.0, 0.0, 0.0);

    for (int triangle = 1; triangle <= triangulation->NbTriangles(); ++triangle)
    {
        int a = 0;
        int b = 0;
        int c = 0;
        triangulation->Triangle(triangle).Get(a, b, c);
        if (flipWinding)
        {
            std::swap(b, c);
        }

        const gp_Pnt pa = triangulation->Node(a).Transformed(transform);
        const gp_Pnt pb = triangulation->Node(b).Transformed(transform);
        const gp_Pnt pc = triangulation->Node(c).Transformed(transform);
        windingNormal += gp_Vec(pa, pb).Crossed(gp_Vec(pa, pc));

        for (int vertex : {a, b, c})
        {
            const size_t offset = firstNormal + static_cast<size_t>(vertex - 1) * 3;
            vertexNormalSum += gp_Vec(mesh.normals[offset], mesh.normals[offset + 1], mesh.normals[offset + 2]);
            mesh.indices.push_back(baseVertex + static_cast<uint32_t>(vertex - 1));
        }
        mesh.triangleFaces.push_back(faceId);
    }

    if (windingNormal.Dot(vertexNormalSum) < 0.0)
    {
        std::transform(mesh.normals.begin() + static_cast<std::ptrdiff_t>(firstNormal), mesh.normals.end(),
                       mesh.normals.begin() + static_cast<std::ptrdiff_t>(firstNormal), [](float v) { return -v; });
    }
}

template <typename T>
ups_status CopyArray(const std::vector<T>& source, T* target, int64_t capacity, const char* name)
{
    if (target == nullptr)
    {
        return UPS_OK;
    }
    if (capacity < static_cast<int64_t>(source.size()))
    {
        return Fail(UPS_ERROR_BUFFER_TOO_SMALL, std::string("Buffer too small for ") + name + ".");
    }
    std::copy(source.begin(), source.end(), target);
    return UPS_OK;
}
} // namespace

extern "C" UPS_API ups_status ups_document_tessellate(
    ups_document* document, const ups_tessellation_params* params, ups_mesh** out_mesh)
{
    return Guarded([&]() -> ups_status {
        if (document == nullptr || params == nullptr || out_mesh == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Document, parameters and output pointer must not be null.");
        }
        if (params->linear_deflection <= 0.0 || params->angular_deflection <= 0.0)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Deflection values must be positive.");
        }
        *out_mesh = nullptr;

        BRepMesh_IncrementalMesh mesher(document->shape, params->linear_deflection, params->relative != 0,
                                        params->angular_deflection, /*isInParallel*/ true);
        if (!mesher.IsDone())
        {
            return Fail(UPS_ERROR_OPERATION, "Tessellation failed.");
        }

        auto mesh = std::make_unique<ups_mesh>();
        for (int index = 1; index <= document->faces.Extent(); ++index)
        {
            AppendFace(*mesh, TopoDS::Face(document->faces(index)), index - 1);
        }

        *out_mesh = mesh.release();
        return UPS_OK;
    });
}

extern "C" UPS_API void ups_mesh_free(ups_mesh* mesh)
{
    delete mesh;
}

extern "C" UPS_API ups_status ups_mesh_get_info(const ups_mesh* mesh, ups_mesh_info* out_info)
{
    return Guarded([&]() -> ups_status {
        if (mesh == nullptr || out_info == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Mesh and output pointer must not be null.");
        }
        out_info->vertex_count = static_cast<int32_t>(mesh->positions.size() / 3);
        out_info->triangle_count = static_cast<int32_t>(mesh->triangleFaces.size());
        return UPS_OK;
    });
}

extern "C" UPS_API ups_status ups_mesh_copy(
    const ups_mesh* mesh,
    float* positions, int64_t positions_capacity,
    float* normals, int64_t normals_capacity,
    uint32_t* indices, int64_t indices_capacity,
    int32_t* triangle_faces, int64_t triangle_faces_capacity)
{
    return Guarded([&]() -> ups_status {
        if (mesh == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Mesh must not be null.");
        }
        ups_status status = CopyArray(mesh->positions, positions, positions_capacity, "positions");
        if (status == UPS_OK)
        {
            status = CopyArray(mesh->normals, normals, normals_capacity, "normals");
        }
        if (status == UPS_OK)
        {
            status = CopyArray(mesh->indices, indices, indices_capacity, "indices");
        }
        if (status == UPS_OK)
        {
            status = CopyArray(mesh->triangleFaces, triangle_faces, triangle_faces_capacity, "triangle faces");
        }
        return status;
    });
}
