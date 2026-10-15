#!/usr/bin/env bash
# Builds the Flatpak bundle from artifacts/publish/linux-x64 (scripts/publish.ps1 -Rid linux-x64).
#
# Usage: scripts/build-flatpak.sh <version>
#   → artifacts/release/UpSTEPer-<version>-linux-x64.flatpak
# Needs flatpak, flatpak-builder and an SVG loader for the icon (apt install flatpak flatpak-builder
# librsvg2-common); installs the
# Freedesktop runtime and SDK from Flathub for the current user. Design: docs/packaging.md.
# Users install the bundle with: flatpak install --user UpSTEPer-<version>-linux-x64.flatpak
set -euo pipefail

version="${1:?usage: build-flatpak.sh <version>}"
root="$(cd "$(dirname "$0")/.." && pwd)"
app_id=io.github.mattheuserb.UpSTEPer
work="$root/artifacts/flatpak"

if [ ! -x "$root/artifacts/publish/linux-x64/UpSTEPer.Desktop" ]; then
  echo "No published app in artifacts/publish/linux-x64; run scripts/publish.ps1 -Rid linux-x64 first." >&2
  exit 1
fi

flatpak remote-add --user --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo
rm -rf "$work/build" "$work/repo"
if ! flatpak-builder --user --install-deps-from=flathub --force-clean --disable-rofiles-fuse \
  --state-dir="$work/state" --repo="$work/repo" \
  "$work/build" "$root/packaging/linux/$app_id.yml"; then
  # flatpak-builder only names the AppStream problem; run compose again to print its report.
  if [ -d "$work/build/files/share" ]; then
    echo "--- AppStream compose report ---" >&2
    appstreamcli compose --verbose --no-color --origin=diagnose --prefix=/ \
      --result-root="$work/diagnose" --hints-dir="$work/hints" \
      --components="$app_id,$app_id.desktop" "$work/build/files" >&2 || true
    find "$work/hints" -type f -exec cat {} + >&2 || true
  fi
  exit 1
fi

mkdir -p "$root/artifacts/release"
bundle="$root/artifacts/release/UpSTEPer-$version-linux-x64.flatpak"
flatpak build-bundle --runtime-repo=https://dl.flathub.org/repo/flathub.flatpakrepo \
  "$work/repo" "$bundle" "$app_id"
echo "Flatpak: $bundle ($(du -h "$bundle" | cut -f1))"
