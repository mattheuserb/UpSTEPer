# Changelog

All notable changes to UpSTEPer are listed here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/). Until 1.0, minor versions may break the CLI or file
formats.

## [Unreleased]

First public version (planned as 0.1.0-alpha).

### Added

- **Viewer** (Avalonia desktop app): STEP AP203/AP214/AP242, orbit/pan/zoom, standard views,
  orthographic projection, edges, hover and selection highlight, model tree of assemblies, parts
  and bodies, display quality (coarse/normal/fine), progress and cancellation for loading and
  meshing.
- **Colouring**: faces and bodies, single instances in assemblies, undo/redo, RAL Classic list,
  named favourite colours, custom colours; save as STEP AP214 or AP242.
- **Check and repair**: defects per part (gaps, invalid shapes, spline geometry that is really
  analytic, mergeable faces); healing, sewing, merging faces and exact surfaces and curves for
  such splines, colours kept, report of every change, undo of the last repair in the app.
- **DXF export**: front, top, left and optional isometric views with exact hidden-line removal;
  lines, arcs, circles and ellipses stay analytic, straight splines become lines; 1:1 in mm.
- **CLI** `upsteper`: `runtime`, `info`, `paint`, `check`, `repair`, `export-dxf`.
- English and German user interface, switchable at run time; settings file with recent files.
- App option `--screenshot <png> --window` renders the whole window for documentation pictures.

[Unreleased]: https://github.com/mattheuserb/UpSTEPer/commits/main
