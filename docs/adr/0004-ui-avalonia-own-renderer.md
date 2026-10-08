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
