#!/usr/bin/env bash
#
# Build a .deb package from a self-contained dotnet publish directory.
#
# Usage: build-deb.sh <publish-dir> <version> [arch]
#   publish-dir  Path to the dotnet publish output (flat directory with tw, Tinkwell.Coordinator, etc.)
#   version      Package version string (e.g. 0.1.0)
#   arch         Debian architecture: amd64 (default) or arm64
#
# Requirements:
#   dpkg-deb  (Debian packaging tools)
#   pandoc    (renders the man page from docs/user-guide/cli.md)

set -euo pipefail

PUBLISH_DIR="${1:?Usage: build-deb.sh <publish-dir> <version> [arch]}"
VERSION="${2:?Usage: build-deb.sh <publish-dir> <version> [arch]}"
ARCH="${3:-amd64}"

if ! command -v pandoc >/dev/null 2>&1; then
    echo "error: pandoc is required to generate the man page" >&2
    echo "       install it with: sudo apt-get install -y pandoc" >&2
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PKG_NAME="tinkwell"
DEB_NAME="${PKG_NAME}_${VERSION}_${ARCH}.deb"
STAGING="$(mktemp -d)"

trap 'rm -rf "$STAGING"' EXIT

# Install tree
INSTALL_DIR="${STAGING}/usr/lib/${PKG_NAME}"
BIN_DIR="${STAGING}/usr/bin"
MAN_DIR="${STAGING}/usr/share/man/man1"

mkdir -p "$INSTALL_DIR" "$BIN_DIR" "$MAN_DIR" "${STAGING}/DEBIAN"

cp -a "${PUBLISH_DIR}/." "$INSTALL_DIR/"
chmod +x "${INSTALL_DIR}/tw" \
         "${INSTALL_DIR}/Tinkwell.Coordinator" \
         "${INSTALL_DIR}/Tinkwell.Runner.Grpc" \
         "${INSTALL_DIR}/Tinkwell.Runner.Headless" \
         2>/dev/null || true

ln -s "../lib/${PKG_NAME}/tw" "${BIN_DIR}/tw"

# Man page. docs/user-guide/cli.md is intentionally authored as a man-page-shaped
# document (tw(1) title, SYNOPSIS / DESCRIPTION / ... sections, pandoc
# definition-list syntax for options); pandoc renders it directly.
CLI_MD="${REPO_ROOT}/docs/user-guide/cli.md"
if [[ ! -f "$CLI_MD" ]]; then
    echo "error: cannot find CLI reference at $CLI_MD" >&2
    exit 1
fi

pandoc --standalone --to man \
    --metadata title=tw \
    --metadata section=1 \
    --metadata header="Tinkwell Manual" \
    --metadata footer="Tinkwell ${VERSION}" \
    --from markdown+definition_lists \
    "$CLI_MD" |
    gzip -n9 > "${MAN_DIR}/tw.1.gz"

# Control file
sed -e "s/{VERSION}/${VERSION}/g" -e "s/{ARCH}/${ARCH}/g" \
    "${SCRIPT_DIR}/control.template" > "${STAGING}/DEBIAN/control"

# Build
dpkg-deb --root-owner-group --build "$STAGING" "$DEB_NAME"

echo "Created $DEB_NAME"
