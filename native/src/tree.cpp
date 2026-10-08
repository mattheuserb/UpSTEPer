// Product structure (assemblies, parts, bodies) from the XDE document.
#include "internal.hpp"

#include <TCollection_AsciiString.hxx>
#include <TDataStd_Name.hxx>
#include <TopExp.hxx>
#include <TopoDS_Iterator.hxx>
#include <XCAFDoc_DocumentTool.hxx>
#include <XCAFDoc_ShapeTool.hxx>

#include <cmath>
#include <memory>

struct ups_tree
{
    std::vector<ups_tree_node> nodes;
    std::string names;
};

using ups::detail::Fail;
using ups::detail::Guarded;

namespace
{
std::string NameOf(const TDF_Label& label)
{
    Handle(TDataStd_Name) name;
    if (label.IsNull() || !label.FindAttribute(TDataStd_Name::GetID(), name))
    {
        return {};
    }
    // Without a replacement character the extended string is converted to UTF-8.
    return TCollection_AsciiString(name->Get()).ToCString();
}

class TreeBuilder
{
public:
    explicit TreeBuilder(const ups_document& document)
        : m_document(document)
    {
    }

    std::unique_ptr<ups_tree> Build()
    {
        for (const TDF_Label& root : m_document.roots)
        {
            Visit(root, -1, TopLoc_Location());
        }
        return std::move(m_tree);
    }

private:
    void Visit(const TDF_Label& label, int32_t parent, const TopLoc_Location& parentLocation)
    {
        TDF_Label shapeLabel = label;
        TopLoc_Location location = parentLocation;
        if (XCAFDoc_ShapeTool::IsReference(label))
        {
            XCAFDoc_ShapeTool::GetReferredShape(label, shapeLabel);
            location = parentLocation * XCAFDoc_ShapeTool::GetLocation(label);
        }

        // Instances may carry their own name; fall back to the referenced definition's name. OCCT
        // writes unnamed instances as "=>[0:1:1:n]" (a label path), which is no useful name.
        std::string name = NameOf(label);
        if (name.empty() || name.rfind("=>[", 0) == 0)
        {
            name = NameOf(shapeLabel);
        }

        if (XCAFDoc_ShapeTool::IsAssembly(shapeLabel))
        {
            const int32_t self = Add(parent, UPS_NODE_ASSEMBLY, -1, name);
            ups::detail::LabelSequence components;
            XCAFDoc_ShapeTool::GetComponents(shapeLabel, components);
            for (const TDF_Label& component : components)
            {
                Visit(component, self, location);
            }
            return;
        }

        const int32_t self = Add(parent, UPS_NODE_PART, -1, name);
        const TopoDS_Shape shape = XCAFDoc_ShapeTool::GetShape(shapeLabel).Moved(location);
        ups::detail::ShapeIndexedMap solids;
        TopExp::MapShapes(shape, TopAbs_SOLID, solids);
        for (int i = 1; i <= solids.Extent(); ++i)
        {
            const int32_t bodyId = FindBody(solids(i));
            const std::string bodyName = solids.Extent() == 1 ? name : name + " #" + std::to_string(i);
            Add(self, UPS_NODE_BODY, bodyId, bodyName);
        }
    }

    // The solid as it occurs in the document's compound. Locations may be composed differently
    // along the two paths, so fall back to comparing the underlying shape and the transformation.
    int32_t FindBody(const TopoDS_Shape& solid) const
    {
        const int index = m_document.solids.FindIndex(solid);
        if (index > 0)
        {
            return index - 1;
        }

        const gp_Trsf trsf = solid.Location().Transformation();
        for (int i = 1; i <= m_document.solids.Extent(); ++i)
        {
            const TopoDS_Shape& candidate = m_document.solids(i);
            if (candidate.TShape() != solid.TShape())
            {
                continue;
            }
            const gp_Trsf other = candidate.Location().Transformation();
            bool same = true;
            for (int row = 1; row <= 3 && same; ++row)
            {
                for (int col = 1; col <= 4 && same; ++col)
                {
                    same = std::abs(trsf.Value(row, col) - other.Value(row, col)) < 1e-9;
                }
            }
            if (same)
            {
                return i - 1;
            }
        }
        return -1;
    }

    int32_t Add(int32_t parent, ups_node_kind kind, int32_t bodyId, const std::string& name)
    {
        ups_tree_node node{};
        node.parent = parent;
        node.kind = kind;
        node.body_id = bodyId;
        node.name_offset = static_cast<int32_t>(m_tree->names.size());
        node.name_length = static_cast<int32_t>(name.size());
        m_tree->names += name;
        m_tree->nodes.push_back(node);
        return static_cast<int32_t>(m_tree->nodes.size()) - 1;
    }

    const ups_document& m_document;
    std::unique_ptr<ups_tree> m_tree = std::make_unique<ups_tree>();
};
} // namespace

extern "C" UPS_API ups_status ups_document_get_tree(const ups_document* document, ups_tree** out_tree)
{
    return Guarded([&]() -> ups_status {
        if (document == nullptr || out_tree == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Document and output pointer must not be null.");
        }
        *out_tree = TreeBuilder(*document).Build().release();
        return UPS_OK;
    });
}

extern "C" UPS_API void ups_tree_free(ups_tree* tree)
{
    delete tree;
}

extern "C" UPS_API ups_status ups_tree_get_info(const ups_tree* tree, int32_t* out_node_count, int32_t* out_names_length)
{
    return Guarded([&]() -> ups_status {
        if (tree == nullptr || out_node_count == nullptr || out_names_length == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Tree and output pointers must not be null.");
        }
        *out_node_count = static_cast<int32_t>(tree->nodes.size());
        *out_names_length = static_cast<int32_t>(tree->names.size());
        return UPS_OK;
    });
}

extern "C" UPS_API ups_status ups_tree_copy(
    const ups_tree* tree, ups_tree_node* nodes, int64_t nodes_capacity, char* names, int64_t names_capacity)
{
    return Guarded([&]() -> ups_status {
        if (tree == nullptr)
        {
            return Fail(UPS_ERROR_INVALID_ARGUMENT, "Tree must not be null.");
        }
        if (nodes != nullptr)
        {
            if (nodes_capacity < static_cast<int64_t>(tree->nodes.size()))
            {
                return Fail(UPS_ERROR_BUFFER_TOO_SMALL, "Buffer too small for nodes.");
            }
            std::copy(tree->nodes.begin(), tree->nodes.end(), nodes);
        }
        if (names != nullptr)
        {
            if (names_capacity < static_cast<int64_t>(tree->names.size()))
            {
                return Fail(UPS_ERROR_BUFFER_TOO_SMALL, "Buffer too small for names.");
            }
            std::copy(tree->names.begin(), tree->names.end(), names);
        }
        return UPS_OK;
    });
}
