// Assembly occurrences: colour inheritance, locating a face/solid's occurrence, and giving an
// occurrence its own copies of shared definitions ("make independent") while keeping all ids.
#include "internal.hpp"

#include <BRepBuilderAPI_Copy.hxx>
#include <BRep_Builder.hxx>
#include <TDataStd_Name.hxx>
#include <TopExp.hxx>
#include <TopoDS_Compound.hxx>
#include <XCAFDoc_ColorTool.hxx>
#include <XCAFDoc_DocumentTool.hxx>
#include <XCAFDoc_ShapeTool.hxx>

#include <cmath>
#include <map>
#include <memory>
#include <stdexcept>

namespace
{
using ups::detail::InstanceVisitor;
using ups::detail::PartInstance;
using ups::detail::ShapeIndexedMap;

Handle(XCAFDoc_ShapeTool) ShapeTool(const ups_document& document)
{
    return XCAFDoc_DocumentTool::ShapeTool(document.xdeDocument->Main());
}

Handle(XCAFDoc_ColorTool) ColorTool(const ups_document& document)
{
    return XCAFDoc_DocumentTool::ColorTool(document.xdeDocument->Main());
}

std::optional<Quantity_Color> LabelColor(const Handle(XCAFDoc_ColorTool)& colors, const TDF_Label& label)
{
    Quantity_Color color;
    if (!label.IsNull() && (colors->GetColor(label, XCAFDoc_ColorSurf, color) || colors->GetColor(label, XCAFDoc_ColorGen, color)))
    {
        return color;
    }
    return std::nullopt;
}

bool SamePlacement(const TopLoc_Location& a, const TopLoc_Location& b)
{
    const gp_Trsf ta = a.Transformation();
    const gp_Trsf tb = b.Transformation();
    for (int row = 1; row <= 3; ++row)
    {
        for (int col = 1; col <= 4; ++col)
        {
            if (std::abs(ta.Value(row, col) - tb.Value(row, col)) > 1e-9)
            {
                return false;
            }
        }
    }
    return true;
}

bool SameOccurrence(const TopoDS_Shape& a, const TopoDS_Shape& b)
{
    return a.IsSame(b) || (a.TShape() == b.TShape() && SamePlacement(a.Location(), b.Location()));
}

void Walk(const Handle(XCAFDoc_ColorTool)& colors, const TDF_Label& label, std::vector<TDF_Label>& path,
          const TopLoc_Location& parentLocation, const std::optional<Quantity_Color>& parentColor, const InstanceVisitor& visit)
{
    TDF_Label definition = label;
    TopLoc_Location location = parentLocation;
    std::optional<Quantity_Color> instanceColor;
    const bool isReference = XCAFDoc_ShapeTool::IsReference(label);
    if (isReference)
    {
        XCAFDoc_ShapeTool::GetReferredShape(label, definition);
        location = parentLocation * XCAFDoc_ShapeTool::GetLocation(label);
        instanceColor = LabelColor(colors, label);
        path.push_back(label);
    }

    // Precedence: colour on this instance > colour on the definition > colour inherited from above.
    std::optional<Quantity_Color> color = instanceColor;
    if (!color)
    {
        color = LabelColor(colors, definition);
    }
    if (!color)
    {
        color = parentColor;
    }

    if (XCAFDoc_ShapeTool::IsAssembly(definition))
    {
        ups::detail::LabelSequence components;
        XCAFDoc_ShapeTool::GetComponents(definition, components);
        for (const TDF_Label& component : components)
        {
            Walk(colors, component, path, location, color, visit);
        }
    }
    else
    {
        visit(PartInstance{definition, path, location}, color);
    }

    if (isReference)
    {
        path.pop_back();
    }
}

void CopyAttributes(const Handle(XCAFDoc_ColorTool)& colors, const TDF_Label& from, const TDF_Label& to)
{
    Handle(TDataStd_Name) name;
    if (from.FindAttribute(TDataStd_Name::GetID(), name))
    {
        TDataStd_Name::Set(to, name->Get());
    }

    for (const XCAFDoc_ColorType type : {XCAFDoc_ColorGen, XCAFDoc_ColorSurf, XCAFDoc_ColorCurv})
    {
        Quantity_Color color;
        if (colors->GetColor(from, type, color))
        {
            colors->SetColor(to, color, type);
        }
    }
}

// Rebuilds document.shape and the face/solid maps after the product structure changed, keeping
// every id: entries in `replacements` (old id -> new located shape) move to the copy; all other
// entries must still exist in the new shape.
void RebuildKeepingIds(ups_document& document, const std::map<int, TopoDS_Shape>& faceReplacements,
                       const std::map<int, TopoDS_Shape>& solidReplacements)
{
    TopoDS_Compound compound;
    BRep_Builder builder;
    builder.MakeCompound(compound);
    for (const TDF_Label& root : document.roots)
    {
        builder.Add(compound, XCAFDoc_ShapeTool::GetShape(root));
    }

    auto remap = [&](const ShapeIndexedMap& old, TopAbs_ShapeEnum type, const std::map<int, TopoDS_Shape>& replacements) {
        ShapeIndexedMap current;
        TopExp::MapShapes(compound, type, current);

        ShapeIndexedMap result;
        for (int id = 1; id <= old.Extent(); ++id)
        {
            const auto replacement = replacements.find(id);
            const TopoDS_Shape& wanted = replacement != replacements.end() ? replacement->second : old(id);
            const int index = ups::detail::FindIndexTolerant(current, wanted);
            if (index == 0)
            {
                throw std::runtime_error("Internal error: shape lost while making an assembly occurrence independent.");
            }
            result.Add(current(index));
        }
        if (result.Extent() != current.Extent())
        {
            throw std::runtime_error("Internal error: shape count changed while making an assembly occurrence independent.");
        }
        return result;
    };

    ShapeIndexedMap faces = remap(document.faces, TopAbs_FACE, faceReplacements);
    ShapeIndexedMap solids = remap(document.solids, TopAbs_SOLID, solidReplacements);

    document.shape = compound;
    document.faces = std::move(faces);
    document.solids = std::move(solids);
    document.faceToSolids.Clear();
    TopExp::MapShapesAndAncestors(document.shape, TopAbs_FACE, TopAbs_SOLID, document.faceToSolids);
}
} // namespace

void ups::detail::ForEachPartInstance(const ups_document& document, const InstanceVisitor& visit)
{
    const Handle(XCAFDoc_ColorTool) colors = ColorTool(document);
    std::vector<TDF_Label> path;
    for (const TDF_Label& root : document.roots)
    {
        Walk(colors, root, path, TopLoc_Location(), std::nullopt, visit);
    }
}

int ups::detail::FindIndexTolerant(const ShapeIndexedMap& map, const TopoDS_Shape& shape)
{
    const int index = map.FindIndex(shape);
    if (index > 0)
    {
        return index;
    }
    for (int i = 1; i <= map.Extent(); ++i)
    {
        if (SameOccurrence(map(i), shape))
        {
            return i;
        }
    }
    return 0;
}

bool ups::detail::FindOccurrence(
    const ups_document& document, const TopoDS_Shape& located, PartInstance& instance, TopoDS_Shape& definitionSubShape)
{
    bool found = false;
    ForEachPartInstance(document, [&](const PartInstance& candidate, const std::optional<Quantity_Color>&) {
        if (found)
        {
            return;
        }
        ShapeIndexedMap subShapes;
        TopExp::MapShapes(XCAFDoc_ShapeTool::GetShape(candidate.part), located.ShapeType(), subShapes);
        for (int i = 1; i <= subShapes.Extent(); ++i)
        {
            if (SameOccurrence(subShapes(i).Moved(candidate.location), located))
            {
                instance = candidate;
                definitionSubShape = subShapes(i);
                found = true;
                return;
            }
        }
    });
    return found;
}

void ups::detail::MakeOccurrenceUnique(ups_document& document, const PartInstance& instance)
{
    const Handle(XCAFDoc_ShapeTool) shapes = ShapeTool(document);
    const Handle(XCAFDoc_ColorTool) colors = ColorTool(document);

    std::vector<TDF_Label> path = instance.path;
    std::unique_ptr<BRepBuilderAPI_Copy> copier;
    TopoDS_Shape partShape;

    for (size_t level = 0; level < path.size(); ++level)
    {
        const TDF_Label component = path[level];
        TDF_Label definition;
        XCAFDoc_ShapeTool::GetReferredShape(component, definition);

        LabelSequence users;
        if (XCAFDoc_ShapeTool::GetUsers(definition, users) <= 1)
        {
            continue;
        }

        TDF_Label clone;
        if (XCAFDoc_ShapeTool::IsAssembly(definition))
        {
            // New assembly referencing the same children; the next path level points into it.
            clone = shapes->NewShape();
            CopyAttributes(colors, definition, clone);
            LabelSequence children;
            XCAFDoc_ShapeTool::GetComponents(definition, children);
            for (const TDF_Label& child : children)
            {
                TDF_Label childDefinition;
                XCAFDoc_ShapeTool::GetReferredShape(child, childDefinition);
                const TDF_Label newChild = shapes->AddComponent(clone, childDefinition, XCAFDoc_ShapeTool::GetLocation(child));
                CopyAttributes(colors, child, newChild);
                if (level + 1 < path.size() && child == path[level + 1])
                {
                    path[level + 1] = newChild;
                }
            }
        }
        else
        {
            // Copy of the part geometry with its name and colours (also sub-shape colours).
            partShape = XCAFDoc_ShapeTool::GetShape(definition);
            copier = std::make_unique<BRepBuilderAPI_Copy>(partShape);
            clone = shapes->AddShape(copier->Shape(), false);
            CopyAttributes(colors, definition, clone);
            LabelSequence subLabels;
            XCAFDoc_ShapeTool::GetSubShapes(definition, subLabels);
            for (const TDF_Label& subLabel : subLabels)
            {
                const TDF_Label newSub = shapes->AddSubShape(clone, copier->ModifiedShape(XCAFDoc_ShapeTool::GetShape(subLabel)));
                if (!newSub.IsNull())
                {
                    CopyAttributes(colors, subLabel, newSub);
                }
            }
        }

        // Re-point this occurrence to the clone; other occurrences keep the original.
        const TDF_Label newComponent = shapes->AddComponent(component.Father(), clone, XCAFDoc_ShapeTool::GetLocation(component));
        CopyAttributes(colors, component, newComponent);
        shapes->RemoveComponent(component);
        path[level] = newComponent;
    }

    if (!copier)
    {
        return; // nothing on the path was shared
    }

    shapes->UpdateAssemblies();

    // The occurrence's faces and solids move to the copy at the same placement and keep their ids.
    auto replacements = [&](TopAbs_ShapeEnum type, const ShapeIndexedMap& ids) {
        std::map<int, TopoDS_Shape> result;
        ShapeIndexedMap subShapes;
        TopExp::MapShapes(partShape, type, subShapes);
        for (int i = 1; i <= subShapes.Extent(); ++i)
        {
            const int id = FindIndexTolerant(ids, subShapes(i).Moved(instance.location));
            if (id > 0)
            {
                result[id] = copier->ModifiedShape(subShapes(i)).Moved(instance.location);
            }
        }
        return result;
    };

    RebuildKeepingIds(document, replacements(TopAbs_FACE, document.faces), replacements(TopAbs_SOLID, document.solids));
}

void ups::detail::ResolveFaceColors(ups_document& document)
{
    const Handle(XCAFDoc_ShapeTool) shapes = ShapeTool(document);
    const Handle(XCAFDoc_ColorTool) colors = ColorTool(document);

    document.faceColors.assign(static_cast<size_t>(document.faces.Extent()), std::nullopt);
    ForEachPartInstance(document, [&](const PartInstance& instance, const std::optional<Quantity_Color>& inherited) {
        const TopoDS_Shape part = XCAFDoc_ShapeTool::GetShape(instance.part);
        auto subShapeColor = [&](const TopoDS_Shape& sub) -> std::optional<Quantity_Color> {
            TDF_Label label;
            return shapes->FindSubShape(instance.part, sub, label) ? LabelColor(colors, label) : std::nullopt;
        };

        ShapeIndexedMap faces;
        TopExp::MapShapes(part, TopAbs_FACE, faces);
        ShapeAncestorMap faceToSolids;
        TopExp::MapShapesAndAncestors(part, TopAbs_FACE, TopAbs_SOLID, faceToSolids);

        for (int i = 1; i <= faces.Extent(); ++i)
        {
            // Precedence: face colour > solid colour > colour of the occurrence (instance/definition/parents).
            std::optional<Quantity_Color> color = subShapeColor(faces(i));
            if (!color && faceToSolids.Contains(faces(i)))
            {
                for (const TopoDS_Shape& solid : faceToSolids.FindFromKey(faces(i)))
                {
                    if (!solid.IsSame(part))
                    {
                        color = subShapeColor(solid);
                    }
                    if (color)
                    {
                        break;
                    }
                }
            }
            if (!color)
            {
                color = inherited;
            }

            const int id = FindIndexTolerant(document.faces, faces(i).Moved(instance.location));
            if (id > 0)
            {
                document.faceColors[static_cast<size_t>(id - 1)] = color;
            }
        }
    });
}
