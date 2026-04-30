#!/bin/bash
# ============================================================
#  pack_unitypackage.sh — Builds a .unitypackage from CalqFramework.Config
#  compiled DLLs (including dependencies) without requiring Unity Editor.
#
#  Usage: ./pack_unitypackage.sh [version]
#    version  Optional. If omitted, resolves from latest git tag.
#
#  Output: CalqFramework.Config.<version>.unitypackage
# ============================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PACKAGE_NAME="CalqFramework.Config"
ASSET_ROOT="Assets/CalqFramework.Config"
PROJECT_DIR="$SCRIPT_DIR/CalqFramework.Config"
LICENSE_FILE="$SCRIPT_DIR/LICENSES/LicenseRef-CCL.txt"
TFM="netstandard2.1"

# --- Resolve version ---
if [ -n "$1" ]; then
    VERSION="$1"
else
    RAW_TAG=$(git describe --tags --abbrev=0 2>/dev/null || true)
    if [ -n "$RAW_TAG" ]; then
        VERSION="${RAW_TAG#v}"
    else
        VERSION="0.0.0"
    fi
fi

echo "[pack_unity] Building $PACKAGE_NAME v$VERSION"

# --- Publish the project to get all DLLs ---
PUBLISH_DIR="$PROJECT_DIR/bin/UnityPublish/$TFM"
dotnet publish "$PROJECT_DIR/$PACKAGE_NAME.csproj" \
    -c Release \
    -f "$TFM" \
    -o "$PUBLISH_DIR" \
    /p:Version="$VERSION"

echo "[pack_unity] Published to $PUBLISH_DIR"

# --- Create temp workspace ---
TEMP_DIR=$(mktemp -d)
FILE_COUNT=0

# --- Helper: add a DLL to the package ---
add_dll() {
    local src="$1"
    local asset_path="$2"

    GUID=$(printf '%s' "$asset_path" | md5sum | cut -c1-32)
    mkdir -p "$TEMP_DIR/$GUID"

    printf '%s' "$asset_path" > "$TEMP_DIR/$GUID/pathname"
    cp "$src" "$TEMP_DIR/$GUID/asset"

    cat > "$TEMP_DIR/$GUID/asset.meta" <<EOF
fileFormatVersion: 2
guid: $GUID
PluginImporter:
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 1
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  userData:
  assetBundleName:
  assetBundleVariant:
EOF

    FILE_COUNT=$((FILE_COUNT + 1))
}

# --- Helper: add a plain file (license, readme, etc.) ---
add_file() {
    local src="$1"
    local asset_path="$2"

    GUID=$(printf '%s' "$asset_path" | md5sum | cut -c1-32)
    mkdir -p "$TEMP_DIR/$GUID"

    printf '%s' "$asset_path" > "$TEMP_DIR/$GUID/pathname"
    cp "$src" "$TEMP_DIR/$GUID/asset"

    cat > "$TEMP_DIR/$GUID/asset.meta" <<EOF
fileFormatVersion: 2
guid: $GUID
TextScriptImporter:
  serializedVersion: 2
  userData:
  assetBundleName:
  assetBundleVariant:
EOF

    FILE_COUNT=$((FILE_COUNT + 1))
}

# --- Helper: add a folder .meta entry ---
add_folder() {
    local asset_path="$1"

    GUID=$(printf '%s' "$asset_path" | md5sum | cut -c1-32)
    mkdir -p "$TEMP_DIR/$GUID"

    printf '%s' "$asset_path" > "$TEMP_DIR/$GUID/pathname"

    cat > "$TEMP_DIR/$GUID/asset.meta" <<EOF
fileFormatVersion: 2
guid: $GUID
folderAsset: yes
DefaultImporter:
  serializedVersion: 2
  userData:
  assetBundleName:
  assetBundleVariant:
EOF

    FILE_COUNT=$((FILE_COUNT + 1))
}

# --- Add folder entries ---
add_folder "$ASSET_ROOT"
add_folder "$ASSET_ROOT/Plugins"

# --- Add license ---
add_file "$LICENSE_FILE" "$ASSET_ROOT/LICENSE.txt"

# --- Add project DLL ---
add_dll "$PUBLISH_DIR/$PACKAGE_NAME.dll" "$ASSET_ROOT/Plugins/$PACKAGE_NAME.dll"

# --- Add dependency DLLs (exclude system/runtime assemblies) ---
for dll in "$PUBLISH_DIR"/*.dll; do
    dll_name=$(basename "$dll")

    # Skip the project DLL itself (already added)
    if [ "$dll_name" = "$PACKAGE_NAME.dll" ]; then
        continue
    fi

    # Skip .NET runtime / system assemblies that Unity already provides
    case "$dll_name" in
        System.*|Microsoft.*|netstandard.*|mscorlib.*) continue ;;
    esac

    add_dll "$dll" "$ASSET_ROOT/Plugins/$dll_name"
done

echo "[pack_unity] Packaged $FILE_COUNT entries"

# --- Create .unitypackage (gzipped tarball) ---
OUTPUT="$SCRIPT_DIR/$PACKAGE_NAME.$VERSION.unitypackage"
tar -czf "$OUTPUT" -C "$TEMP_DIR" .

# --- Cleanup ---
rm -rf "$TEMP_DIR"

echo "[pack_unity] Created $OUTPUT"
