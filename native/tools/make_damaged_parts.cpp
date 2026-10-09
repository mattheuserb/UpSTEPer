// Generates examples/generated/damaged-parts.stp: licence-free parts with typical STEP defects,
// one defect per part, for the check and repair tests (roadmap M4, ADR 0007). Some faces are
// coloured so the tests can verify that repair keeps colours (ADR 0005).
//
//   Damaged parts             (assembly)
//   +- Healthy box            (part, valid solid; must report no problems)
//   +- Unsewn box             (part, 6 separate faces, one moved by 0.01 mm: free edges, gaps)
//   +- Split cylinder         (part, side split into 4 faces of the same surface: mergeable faces)
//   +- Spline cylinder        (part, all geometry as B-splines: canonical recognition)
//
// Build with -DUPS_BUILD_TOOLS=ON and run: upsteper_make_damaged_parts <output.stp>
#include <BRepBuilderAPI_Copy.hxx>
#include <BRepBuilderAPI_NurbsConvert.hxx>
#include <BRepBuilderAPI_Transform.hxx>
#include <BRepPrimAPI_MakeBox.hxx>
#include <BRepPrimAPI_MakeCylinder.hxx>
#include <BRepAdaptor_Surface.hxx>
#include <BRepBndLib.hxx>
#include <BRep_Builder.hxx>
#include <Bnd_Box.hxx>
#include <GeomAbs_SurfaceType.hxx>
#include <IFSelect_ReturnStatus.hxx>
#include <Quantity_Color.hxx>
#include <STEPCAFControl_Writer.hxx>
#include <ShapeUpgrade_ShapeDivideAngle.hxx>
#include <TDataStd_Name.hxx>
#include <TDocStd_Document.hxx>
#include <TopExp_Explorer.hxx>
#include <TopoDS.hxx>
#include <TopoDS_Compound.hxx>
#include <XCAFApp_Application.hxx>
#include <XCAFDoc_ColorTool.hxx>
#include <XCAFDoc_DocumentTool.hxx>
#include <XCAFDoc_ShapeTool.hxx>
#include <gp_Ax2.hxx>
#include <gp_Trsf.hxx>

#include <iostream>
#include <vector>

namespace
{
void Name(const TDF_Label& label, const char* name)
{
    TDataStd_Name::Set(label, TCollection_ExtendedString(name, /*isMultiByte*/ true));
}

TopLoc_Location Translation(double x, double y, double z)
{
    gp_Trsf trsf;
    trsf.SetTranslation(gp_Vec(x, y, z));
    return TopLoc_Location(trsf);
}

std::vector<TopoDS_Face> Faces(const TopoDS_Shape& shape)
{
    std::vector<TopoDS_Face> faces;
    for (TopExp_Explorer explorer(shape, TopAbs_FACE); explorer.More(); explorer.Next())
    {
        faces.push_back(TopoDS::Face(explorer.Current()));
    }
    return faces;
}

// Colours a face of a part through a sub-shape label, as a user (or UpSTEPer) would.
void ColorFace(const Handle(XCAFDoc_ShapeTool)& shapes, const Handle(XCAFDoc_ColorTool)& colors,
               const TDF_Label& part, const TopoDS_Shape& face, const Quantity_Color& color)
{
    const TDF_Label label = shapes->AddSubShape(part, face);
    colors->SetColor(label, color, XCAFDoc_ColorSurf);
}

// Six faces of a box that share no edges (as if each was exported on its own), the top face
// moved up by 0.01 mm: free edges everywhere, and gaps that sewing must close.
TopoDS_Shape UnsewnBox()
{
    const TopoDS_Shape box = BRepPrimAPI_MakeBox(40.0, 30.0, 20.0).Shape();
    TopoDS_Compound faces;
    BRep_Builder builder;
    builder.MakeCompound(faces);
    for (const TopoDS_Face& face : Faces(box))
    {
        TopoDS_Shape copy = BRepBuilderAPI_Copy(face).Shape();
        Bnd_Box bounds;
        BRepBndLib::Add(face, bounds);
        if (bounds.CornerMin().Z() > 19.0) // the top face
        {
            gp_Trsf lift;
            lift.SetTranslation(gp_Vec(0, 0, 0.01));
            copy = BRepBuilderAPI_Transform(copy, lift, /*copy*/ true).Shape();
        }
        builder.Add(faces, copy);
    }
    return faces;
}
} // namespace

int main(int argc, char** argv)
{
    if (argc != 2)
    {
        std::cerr << "usage: upsteper_make_damaged_parts <output.stp>\n";
        return 2;
    }

    Handle(TDocStd_Document) document;
    XCAFApp_Application::GetApplication()->NewDocument("MDTV-XCAF", document);
    const Handle(XCAFDoc_ShapeTool) shapes = XCAFDoc_DocumentTool::ShapeTool(document->Main());
    const Handle(XCAFDoc_ColorTool) colors = XCAFDoc_DocumentTool::ColorTool(document->Main());
    const Quantity_Color red(1.0, 0.0, 0.0, Quantity_TOC_sRGB);
    const Quantity_Color blue(0.2, 0.4, 0.8, Quantity_TOC_sRGB);
    const Quantity_Color green(0.34, 0.65, 0.22, Quantity_TOC_sRGB);
    const Quantity_Color yellow(1.0, 0.8, 0.0, Quantity_TOC_sRGB);

    const TDF_Label healthy = shapes->AddShape(BRepPrimAPI_MakeBox(40.0, 30.0, 20.0).Shape(), false);
    Name(healthy, "Healthy box");

    const TopoDS_Shape unsewn = UnsewnBox();
    const TDF_Label unsewnLabel = shapes->AddShape(unsewn, false);
    Name(unsewnLabel, "Unsewn box");
    ColorFace(shapes, colors, unsewnLabel, Faces(unsewn).back(), red);

    // The side of a cylinder split into four quarter faces on the same cylindrical surface;
    // two blue, one green, one uncoloured: merging them must resolve the colour conflict (blue).
    const TopoDS_Shape cylinder = BRepPrimAPI_MakeCylinder(gp_Ax2(gp::Origin(), gp::DZ()), 10.0, 30.0).Shape();
    ShapeUpgrade_ShapeDivideAngle divide(0.5 * 3.14159265358979323846, cylinder);
    divide.Perform();
    const TopoDS_Shape split = divide.Result();
    const TDF_Label splitLabel = shapes->AddShape(split, false);
    Name(splitLabel, "Split cylinder");
    int quarter = 0;
    for (const TopoDS_Face& face : Faces(split))
    {
        if (BRepAdaptor_Surface(face).GetType() != GeomAbs_Cylinder)
        {
            continue;
        }
        const Quantity_Color* color = quarter < 2 ? &blue : quarter == 2 ? &green : nullptr;
        if (color != nullptr)
        {
            ColorFace(shapes, colors, splitLabel, face, *color);
        }
        ++quarter;
    }

    // The same cylinder with every curve and surface converted to B-splines, as some exporters
    // write them: geometry is exact but no longer recognisable as a cylinder.
    BRepBuilderAPI_NurbsConvert convert(cylinder, /*copy*/ true);
    const TopoDS_Shape nurbs = convert.Shape();
    const TDF_Label nurbsLabel = shapes->AddShape(nurbs, false);
    Name(nurbsLabel, "Spline cylinder");
    for (const TopoDS_Face& face : Faces(cylinder))
    {
        if (BRepAdaptor_Surface(face).GetType() == GeomAbs_Cylinder) // the former side face: yellow
        {
            ColorFace(shapes, colors, nurbsLabel, convert.ModifiedShape(face), yellow);
        }
    }

    const TDF_Label top = shapes->NewShape();
    Name(top, "Damaged parts");
    shapes->AddComponent(top, healthy, TopLoc_Location());
    shapes->AddComponent(top, unsewnLabel, Translation(60.0, 0.0, 0.0));
    shapes->AddComponent(top, splitLabel, Translation(0.0, 60.0, 0.0));
    shapes->AddComponent(top, nurbsLabel, Translation(60.0, 60.0, 0.0));
    shapes->UpdateAssemblies();

    STEPCAFControl_Writer writer;
    writer.SetNameMode(true);
    writer.SetColorMode(true);
    if (!writer.Transfer(document, STEPControl_AsIs) || writer.Write(argv[1]) != IFSelect_RetDone)
    {
        std::cerr << "writing " << argv[1] << " failed\n";
        return 1;
    }

    std::cout << "wrote " << argv[1] << "\n";
    return 0;
}
