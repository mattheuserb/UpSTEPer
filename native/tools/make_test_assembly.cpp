// Generates examples/generated/test-assembly.stp: a small, licence-free STEP assembly used by the
// tests (product structure, instances with transformations, a part with two bodies).
//
//   Top                       (assembly)
//   +- Base plate             (part, 1 body)
//   +- Pin assembly (left)    (instance of "Pin assembly", x = 20)
//   |   +- Pin                (part, 1 body)
//   |   +- Washer             (part, 2 bodies)
//   +- Pin assembly (right)   (instance of the same definition, x = 80)
//
// Build with -DUPS_BUILD_TOOLS=ON and run: upsteper_make_test_assembly <output.stp>
#include <BRepPrimAPI_MakeBox.hxx>
#include <BRepPrimAPI_MakeCylinder.hxx>
#include <BRep_Builder.hxx>
#include <IFSelect_ReturnStatus.hxx>
#include <STEPCAFControl_Writer.hxx>
#include <TDataStd_Name.hxx>
#include <TDocStd_Document.hxx>
#include <TopoDS_Compound.hxx>
#include <XCAFApp_Application.hxx>
#include <XCAFDoc_DocumentTool.hxx>
#include <XCAFDoc_ShapeTool.hxx>
#include <gp_Ax2.hxx>
#include <gp_Trsf.hxx>

#include <iostream>

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
} // namespace

int main(int argc, char** argv)
{
    if (argc != 2)
    {
        std::cerr << "usage: upsteper_make_test_assembly <output.stp>\n";
        return 2;
    }

    Handle(TDocStd_Document) document;
    XCAFApp_Application::GetApplication()->NewDocument("MDTV-XCAF", document);
    const Handle(XCAFDoc_ShapeTool) shapes = XCAFDoc_DocumentTool::ShapeTool(document->Main());

    const TDF_Label base = shapes->AddShape(BRepPrimAPI_MakeBox(100.0, 60.0, 10.0).Shape(), false);
    Name(base, "Base plate");

    const TDF_Label pin = shapes->AddShape(BRepPrimAPI_MakeCylinder(gp_Ax2(gp_Pnt(0, 30, 10), gp::DZ()), 5.0, 40.0).Shape(), false);
    Name(pin, "Pin");

    TopoDS_Compound washers;
    BRep_Builder builder;
    builder.MakeCompound(washers);
    builder.Add(washers, BRepPrimAPI_MakeCylinder(gp_Ax2(gp_Pnt(0, 30, 10), gp::DZ()), 10.0, 2.0).Shape());
    builder.Add(washers, BRepPrimAPI_MakeCylinder(gp_Ax2(gp_Pnt(0, 30, 46), gp::DZ()), 10.0, 2.0).Shape());
    const TDF_Label washer = shapes->AddShape(washers, false);
    Name(washer, "Washer");

    const TDF_Label pinAssembly = shapes->NewShape();
    Name(pinAssembly, "Pin assembly");
    shapes->AddComponent(pinAssembly, pin, TopLoc_Location());
    shapes->AddComponent(pinAssembly, washer, TopLoc_Location());

    const TDF_Label top = shapes->NewShape();
    Name(top, "Top");
    shapes->AddComponent(top, base, TopLoc_Location());
    Name(shapes->AddComponent(top, pinAssembly, Translation(20.0, 0.0, 0.0)), "Pin assembly (left)");
    Name(shapes->AddComponent(top, pinAssembly, Translation(80.0, 0.0, 0.0)), "Pin assembly (right)");
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
