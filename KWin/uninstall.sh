#!/usr/bin/env bash
# © 2025 DrSciCortex
# Licensed under the Creative Commons Attribution–ShareAlike 4.0 International License (CC-BY-SA-4.0).
# See https://creativecommons.org/licenses/by-sa/4.0/
#
# Removes the Resonite focus-toggle KWin script.

set -euo pipefail

DEST="${XDG_DATA_HOME:-$HOME/.local/share}/kwin/scripts/resonite-focus"

qdbus_bin=$(command -v qdbus6 || command -v qdbus) || qdbus_bin=""
kwriteconfig_bin=$(command -v kwriteconfig6 || command -v kwriteconfig5) || kwriteconfig_bin=""

if [[ -n "$kwriteconfig_bin" ]]; then
    "$kwriteconfig_bin" --file kwinrc --group Plugins --key resonitefocusEnabled false
fi

rm -rf "$DEST"
echo "Removed $DEST"

if [[ -n "$qdbus_bin" ]]; then
    "$qdbus_bin" org.kde.KWin /KWin reconfigure
fi

echo "Done. The Meta+F entry may linger in System Settings > Shortcuts > KWin; clear it there if you like."
