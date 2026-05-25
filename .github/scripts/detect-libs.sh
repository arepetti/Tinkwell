#!/usr/bin/env bash
# Detect libraries to pack for the release workflow.
#
# Inputs:
#   $1  previous release tag (empty string for the first release)
#
# Outputs (appended to $GITHUB_OUTPUT when set, also echoed to stdout):
#   sdk_projects         space-separated csproj paths (always packed)
#   standalone_projects  space-separated csproj paths (dirty standalone libs)
#   pack_all             "true" on first release or when shared packaging
#                        metadata changed; "false" otherwise
#
# Classification is read from <TinkwellPackageGroup> in each csproj:
#   SDK                 always packed at the product version
#   Standalone          packed only if the lib (or any transitive dep) changed
#   ExcludeFromRelease  never packed here (e.g. tools with their own workflow)
#
# A missing marker defaults to ExcludeFromRelease so new, unclassified libs
# are not silently published.

set -euo pipefail

PREV_TAG="${1:-}"
LIBS_DIR="app/libs"
# Path used for git tree-ish access (always relative to repo root). The script
# is invoked with cwd inside src/, so filesystem paths drop the src/ prefix
# but git show / git cat-file calls always need the repo-root path.
GIT_PATH_PREFIX="${GIT_PATH_PREFIX:-}"
SHARED_PROPS="$LIBS_DIR/Directory.Build.props"
SHARED_PROPS_GIT="${GIT_PATH_PREFIX}${SHARED_PROPS}"

emit() {
  local name="$1" value="$2"
  if [ -n "${GITHUB_OUTPUT:-}" ]; then
    echo "$name=$value" >> "$GITHUB_OUTPUT"
  fi
  echo "$name=$value"
}

read_group() {
  local csproj="$1"
  local grp
  grp=$(tr -d '\r' < "$csproj" \
        | grep -oE '<TinkwellPackageGroup>[^<]+</TinkwellPackageGroup>' \
        | sed -E 's|<TinkwellPackageGroup>([^<]+)</TinkwellPackageGroup>|\1|' \
        | head -n1 || true)
  echo "${grp:-ExcludeFromRelease}"
}

read_refs() {
  local csproj="$1"
  tr -d '\r' < "$csproj" \
    | grep -oE '<ProjectReference[^>]*Include="[^"]+"' \
    | sed -E 's|.*Include="([^"]+)".*|\1|' \
    | while IFS= read -r inc; do
        [ -z "$inc" ] && continue
        local fwd="${inc//\\//}"
        basename "$fwd" .csproj
      done
}

declare -A GROUP
declare -A RAW_REFS
LIB_NAMES=()

for dir in "$LIBS_DIR"/*/; do
  lib=$(basename "$dir")
  csproj="$dir$lib.csproj"
  [ -f "$csproj" ] || continue

  GROUP["$lib"]=$(read_group "$csproj")
  LIB_NAMES+=("$lib")

  refs=""
  while IFS= read -r r; do
    refs="$refs $r"
  done < <(read_refs "$csproj")
  RAW_REFS["$lib"]="$refs"
done

# Keep only references to sibling libs under app/libs/ (== src/app/libs/).
declare -A DEPS
for lib in "${LIB_NAMES[@]}"; do
  known=""
  for ref in ${RAW_REFS[$lib]}; do
    if [ -n "${GROUP[$ref]:-}" ]; then
      known="$known $ref"
    fi
  done
  DEPS["$lib"]="$(echo "$known" | xargs || true)"
done

pack_everything() {
  local reason="$1"
  echo "$reason"
  local sdk=""
  local std=""
  for lib in "${LIB_NAMES[@]}"; do
    local csproj="$LIBS_DIR/$lib/$lib.csproj"
    case "${GROUP[$lib]}" in
      SDK) sdk="$sdk $csproj" ;;
      Standalone) std="$std $csproj" ;;
    esac
  done
  emit "pack_all" "true"
  emit "sdk_projects" "$(echo "$sdk" | xargs || true)"
  emit "standalone_projects" "$(echo "$std" | xargs || true)"
}

if [ -z "$PREV_TAG" ]; then
  pack_everything "No previous tag - packing all SDK and standalone libraries."
  exit 0
fi

echo "Comparing against previous tag: $PREV_TAG"

# Detect packaging-metadata changes in the shared props file. The VersionPrefix
# line is auto-bumped after every release, so it is excluded from this diff;
# any other change (authors, license, URLs, tags, etc.) forces pack_all.
pack_all="false"
if [ -f "$SHARED_PROPS" ] && git cat-file -e "$PREV_TAG:$SHARED_PROPS_GIT" 2>/dev/null; then
  old=$(git show "$PREV_TAG:$SHARED_PROPS_GIT" | tr -d '\r' | grep -v 'VersionPrefix' || true)
  new=$(tr -d '\r' < "$SHARED_PROPS" | grep -v 'VersionPrefix' || true)
  if [ "$old" != "$new" ]; then
    pack_all="true"
  fi
fi

if [ "$pack_all" = "true" ]; then
  pack_everything "Shared packaging metadata changed in $SHARED_PROPS - forcing pack_all."
  exit 0
fi

declare -A HAS_DIFF
for lib in "${LIB_NAMES[@]}"; do
  if ! git diff --quiet "$PREV_TAG" HEAD -- "${GIT_PATH_PREFIX}${LIBS_DIR}/$lib/"; then
    HAS_DIFF["$lib"]=1
  fi
done

# SDK libs are always treated as dirty (always republished at the new product
# version). Propagate dirtiness along <ProjectReference> edges so standalone
# libs consuming a changed dep get repacked with the correct PackageReference.
declare -A DIRTY
for lib in "${LIB_NAMES[@]}"; do
  if [ "${GROUP[$lib]}" = "SDK" ] || [ "${HAS_DIFF[$lib]:-0}" = "1" ]; then
    DIRTY["$lib"]=1
  fi
done

changed=1
while [ "$changed" = "1" ]; do
  changed=0
  for lib in "${LIB_NAMES[@]}"; do
    [ "${DIRTY[$lib]:-0}" = "1" ] && continue
    for dep in ${DEPS[$lib]}; do
      if [ "${DIRTY[$dep]:-0}" = "1" ]; then
        DIRTY["$lib"]=1
        changed=1
        break
      fi
    done
  done
done

sdk=""
std=""
for lib in "${LIB_NAMES[@]}"; do
  csproj="$LIBS_DIR/$lib/$lib.csproj"
  case "${GROUP[$lib]}" in
    SDK)
      sdk="$sdk $csproj"
      ;;
    Standalone)
      if [ "${DIRTY[$lib]:-0}" = "1" ]; then
        std="$std $csproj"
      fi
      ;;
    ExcludeFromRelease)
      ;;
    *)
      echo "warning: $lib has unknown TinkwellPackageGroup '${GROUP[$lib]}' - skipping." >&2
      ;;
  esac
done

sdk="$(echo "$sdk" | xargs || true)"
std="$(echo "$std" | xargs || true)"

emit "pack_all" "false"
emit "sdk_projects" "$sdk"
emit "standalone_projects" "$std"

echo "SDK libraries (always packed): ${sdk:-<none>}"
if [ -n "$std" ]; then
  echo "Changed standalone libraries: $std"
else
  echo "No standalone-library changes detected since $PREV_TAG."
fi
