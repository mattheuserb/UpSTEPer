# Releasing

A release is a Git tag `v<version>`; the release workflow (`.github/workflows/release.yml`) does the
rest and leaves a **draft** release on GitHub, which is published by hand. Versions follow
[SemVer](https://semver.org/); `-alpha`, `-beta`, `-rc.1` mark pre-releases.

## Steps

1. On a branch, set the version in all three places (the workflow refuses a tag that disagrees):
   - `Directory.Build.props`: `VersionPrefix` (e.g. `0.1.0`) and `VersionSuffix` (e.g. `alpha`,
     empty for a final release)
   - `CHANGELOG.md`: rename `## [Unreleased]` to `## [0.1.0-alpha] - 2026-10-20`, add a new empty
     `## [Unreleased]` above it and update the links at the bottom
   - `packaging/linux/io.github.mattheuserb.UpSTEPer.metainfo.xml`: a `<release version="..." date="..."/>`
     on top of `<releases>`
2. Pull request, CI green, merge.
3. Tag the merge commit and push the tag:

   ```bash
   git checkout main && git pull
   git tag v0.1.0-alpha
   git push origin v0.1.0-alpha
   ```

4. The workflow checks the version, runs the full CI (all platforms, tests, packaging, installer
   and Flatpak smoke tests) and creates the draft with the files of every platform,
   `SHA256SUMS.txt` and the CHANGELOG section as notes.
5. Check the draft: download the installer and try it (ideally in Windows Sandbox, see
   `docs/packaging.md`), read the notes. Then **Publish release**.

## Release files

| File | Built by |
|---|---|
| `UpSTEPer-<v>-win-x64-setup.exe` | `scripts/build-installer.ps1` (Inno Setup) |
| `UpSTEPer-<v>-win-x64.zip` | `scripts/package.ps1` |
| `UpSTEPer-<v>-linux-x64.flatpak` | `scripts/build-flatpak.sh` |
| `UpSTEPer-<v>-linux-x64.tar.gz` | `scripts/package.ps1` |
| `UpSTEPer-<v>-osx-arm64-cli.tar.gz` | `scripts/package.ps1` (CLI only) |
| `SHA256SUMS.txt` | release workflow |

Every CI run builds the same files (artifact `release-<platform>`, kept 7 days), so packaging is
tested long before a release; the Flatpak only on `main`, releases, manual runs and pull requests
labelled `area: packaging`, because it downloads the Freedesktop runtime and SDK.

## If something goes wrong

A failed workflow creates no release. Fix the cause on a branch, merge, then move the tag:
`git tag -f v0.1.0-alpha <commit>` and `git push -f origin v0.1.0-alpha` (only while the release is
not published). A published release is never changed; release a new version instead.
