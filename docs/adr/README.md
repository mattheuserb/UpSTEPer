# Architecture Decision Records

We record significant decisions in the [MADR](https://adr.github.io/madr/) format. Copy
`template.md`, number it sequentially, and link it here. Superseded ADRs stay, marked as such.

| # | Decision | Status |
|---|---|---|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-native-interop-c-abi.md) | Access OCCT through a narrow C ABI and P/Invoke | Accepted |
| [0003](0003-occt-via-vcpkg-dynamic-linking.md) | Obtain OCCT via vcpkg, link dynamically | Accepted |
| [0004](0004-ui-avalonia-own-renderer.md) | Avalonia UI with an own OpenGL renderer | Proposed (validate in M0) |
| [0005](0005-topology-identity.md) | Face identity and colour preservation across edits | Accepted |
| [0006](0006-dxf-export-hlr-acadsharp.md) | DXF only, 2D views via OCCT HLR and ACadSharp | Accepted |
| [0007](0007-tiered-repair.md) | Tiered geometry repair | Accepted |
| [0008](0008-cli-first-testing.md) | CLI parity and test strategy | Accepted |
| [0009](0009-instance-colouring.md) | Colouring faces and bodies inside assembly instances | Accepted |
| [0010](0010-progress-and-cancellation.md) | Progress reporting and cancellation across the C ABI | Accepted |
| [0011](0011-repair.md) | Repair per part definition, all-or-nothing, colours through histories | Accepted |
