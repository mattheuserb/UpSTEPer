# 0004. Avalonia UI with an own OpenGL renderer

- Status: Proposed — to be validated by the M0 rendering spike
- Date: 2026-10-08

## Context

The desktop app must run on Windows, Linux and macOS and show 3D models with per-face colours
and per-face picking.

## Decision

- UI framework: **Avalonia** (MVVM with CommunityToolkit.Mvvm).
- 3D: **own renderer** using Silk.NET OpenGL 3.3 core inside an Avalonia `OpenGlControlBase`,
  fed with `MeshData` from the kernel. Picking via an ID buffer (triangle → `FaceId`).

## Alternatives considered

| Option | Why not |
|---|---|
| WPF / WinForms | Windows only |
| .NET MAUI | No Linux desktop support |
| OCCT visualisation (AIS/V3d) in an embedded native window | Native window embedding is fragile (Wayland, macOS); OCCT renders with OpenGL, which Apple deprecated; couples UI to the kernel |
| Veldrid | Unmaintained |

## Consequences

- + Rendering is decoupled from OCCT and fully under our control; picking and colouring are simple.
- − Navigation, edge display, transparency and selection highlighting must be implemented (estimate 2–4 weeks).
- − OpenGL on macOS is deprecated but works (up to 4.1). If Apple removes it, the backend can be
  swapped (e.g. Vulkan via MoltenVK); the renderer only consumes `MeshData`.

## Addendum (2026-10-08, M0 spike, Avalonia 12.1)

- Avalonia 12 composes with **Metal** on macOS by default (`[Metal, OpenGl, Software]`, despite the
  API docs saying OpenGL). `OpenGlControlBase` then gets no GL context. The app forces
  `AvaloniaNativeRenderingMode.OpenGl` first. A future Metal backend for the renderer would remove
  this constraint.
- On Linux, Avalonia blocklists the `llvmpipe` (Mesa software) and `SVGA3D` (VMware) GL renderers and
  falls back to software composition without GL. The app clears that list so the 3D view also works
  in VMs and without GPU drivers (slower, but usable).
- Windows uses ANGLE (OpenGL ES 3.0 on Direct3D 11); the renderer selects GLSL ES 3.00 or GLSL 3.30
  core from the context type.
- Verified by the CI viewer screenshot test on all three platforms.
