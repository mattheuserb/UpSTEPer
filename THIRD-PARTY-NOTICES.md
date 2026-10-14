# Third-party notices

UpSTEPer is licensed under the MIT License (see `LICENSE`). It uses the following third-party
components. Binary distributions must include this file and the referenced licence texts.

| Component | Use | Licence | Linking |
|---|---|---|---|
| [Open CASCADE Technology](https://dev.opencascade.org/) 8.x | Geometry kernel, STEP I/O | LGPL-2.1 with Open CASCADE exception | Dynamic, unmodified |
| [FreeType](https://freetype.org/) (OCCT dependency) | Font rendering in OCCT | FreeType License (BSD-style) | Dynamic |
| [System.CommandLine](https://github.com/dotnet/command-line-api) | CLI parsing | MIT | NuGet |
| [Lucide](https://lucide.dev/) 1.56.0 | Icons in the app (converted to geometry resources, `src/UpSTEPer.App/Themes/Icons.axaml`) | ISC (`src/UpSTEPer.App/Themes/Lucide-LICENSE.txt`) | Embedded in the app assembly |
| [xUnit.net](https://xunit.net/) | Tests only, not distributed | Apache-2.0 | NuGet |

Planned: Avalonia (MIT), Silk.NET (MIT), CommunityToolkit.Mvvm (MIT), ACadSharp (MIT).

## Open CASCADE Technology (LGPL-2.1)

OCCT is distributed as separate shared libraries. You may replace them with your own build of
a compatible OCCT version. Source code: <https://github.com/Open-Cascade-SAS/OCCT>.
The licence texts (`LICENSE_LGPL_21.txt`, `OCCT_LGPL_EXCEPTION.txt`) are installed by vcpkg to
`share/opencascade/copyright` and must be shipped with binary releases.

## Example data

`examples/ESKV 12 RAL 7035 10066410.stp` — © CADENAS, licensed CC BY-ND 4.0, redistributed unmodified.
