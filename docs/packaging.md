# Packaging

How UpSTEPer is turned into something users can install. Status: Windows installer done locally;
release workflow, Linux and macOS packages follow (roadmap, "Public release", phase 3).

## Distribution folder (`scripts/publish.ps1`)

```powershell
./scripts/publish.ps1 [-Rid win-x64] [-Version 0.1.0-alpha]   # → artifacts/publish/<rid>
```

- App (`UpSTEPer.Desktop.exe`) and CLI (`upsteper.exe`) are published **self-contained** into one
  folder: no .NET installation needed. The app assembly is `UpSTEPer.Desktop` because Windows
  ignores case and `UpSTEPer.exe` would overwrite the CLI's `upsteper.exe`.
- Native code: `upsteper_native`, OCCT and its dependencies from `artifacts/native/<rid>` (copied by
  `UpSTEPer.Occt.csproj`).
- **MSVC runtime, app-local** (Windows): the native DLLs import `msvcp140.dll`, `vcruntime140.dll`
  and `vcruntime140_1.dll`, which a fresh Windows lacks. They are copied from the Visual Studio
  redist folder next to the executables, so no VC++ Redistributable and no admin rights are
  needed. The Universal CRT (`api-ms-win-crt-*`) is part of Windows 10 and later. When the native
  code starts importing another runtime DLL, add it to the list in the script.
- Licence texts of the native libraries (vcpkg `share/<port>/copyright`) go to `licenses/`;
  `LICENSE` and `THIRD-PARTY-NOTICES.md` come with the app build.
- Trimmed: only English and German satellite assemblies (`SatelliteResourceLanguages`), no debug
  symbols of native NuGet libraries (SkiaSharp, HarfBuzzSharp: about 100 MB). Result: about
  150 MB on disk.

## Windows installer (`scripts/build-installer.ps1`)

```powershell
winget install JRSoftware.InnoSetup               # once
./scripts/build-installer.ps1 [-Version 0.1.0-alpha] [-SkipPublish]
# → artifacts/installer/UpSTEPer-<version>-win-x64-setup.exe (about 45 MB)
```

[Inno Setup 6](https://jrsoftware.org/isinfo.php), script `packaging/windows/upsteper.iss`.
Chosen over Velopack (no file associations without own code), WiX/MSI (much more effort) and
MSIX (signature required, sandboxed file access); updates can come later through winget.

| Behaviour | Decision (2026-10-15) |
|---|---|
| Install scope | Current user without admin rights (`%LOCALAPPDATA%\Programs\UpSTEPer`) by default; the first page offers all users (`Program Files`, needs admin) |
| STEP files | Listed under "Open with" for `.stp`/`.step` (`OpenWithProgids`); never made the default program |
| CLI | Optional task "add to PATH" (user PATH, or system PATH for all users); removed on uninstall |
| Shortcuts | Start menu; desktop optional |
| Languages | English, German |
| Upgrades | Fixed `AppId`: a new version installs over the old one; running instances are closed |
| Uninstall | Removes program, registry entries and PATH entry; keeps settings and logs in `%LOCALAPPDATA%\UpSTEPer` |
| Signature | None yet: SmartScreen warns "unknown publisher". Later: SignPath Foundation (free for open source) |

Silent installation, e.g. for tests or company roll-outs:

```powershell
UpSTEPer-0.1.0-alpha-win-x64-setup.exe /VERYSILENT /CURRENTUSER /TASKS="openwith,addtopath"
"%LOCALAPPDATA%\Programs\UpSTEPer\unins000.exe" /VERYSILENT
```

## Manual test before a release

Ideally on a fresh Windows (Windows Sandbox: optional Windows feature "Windows Sandbox"), which
has neither .NET nor the VC++ runtime:

1. Install without admin rights, all tasks; start from the start menu.
2. Right-click a `.stp` file → Open with → UpSTEPer: the model loads.
3. Colour a face, save as STEP; export DXF.
4. New console: `upsteper runtime` and `upsteper info <file>`.
5. Install the next version over it: settings and recent files are kept.
6. Uninstall: program folder, start menu entry, "Open with" entry and PATH entry are gone.
7. Repeat 1 and 6 with "Install for all users".

Done on 2026-10-15 on the development machine (silent per-user install with all tasks, CLI and
app started from the installed folder, uninstall left nothing behind). Not yet done: Windows
Sandbox, all-users installation.
