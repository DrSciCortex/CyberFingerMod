#!/usr/bin/env bash
# © 2025 DrSciCortex
# Licensed under the Creative Commons Attribution–ShareAlike 4.0 International License (CC-BY-SA-4.0).
# See https://creativecommons.org/licenses/by-sa/4.0/
#
# Installs the Resonite focus-toggle KWin script for the current user (Plasma 6).

set -euo pipefail

SRC="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/resonite-focus"
DEST="${XDG_DATA_HOME:-$HOME/.local/share}/kwin/scripts/resonite-focus"

if [[ "${XDG_CURRENT_DESKTOP:-}" != *KDE* ]]; then
    echo "Warning: XDG_CURRENT_DESKTOP is '${XDG_CURRENT_DESKTOP:-unset}', expected KDE." >&2
fi

qdbus_bin=$(command -v qdbus6 || command -v qdbus) || {
    echo "Error: neither qdbus6 nor qdbus found (install qt6-tools)." >&2
    exit 1
}
kwriteconfig_bin=$(command -v kwriteconfig6 || command -v kwriteconfig5) || {
    echo "Error: kwriteconfig6 not found." >&2
    exit 1
}

echo "Installing $SRC -> $DEST"
mkdir -p "$(dirname "$DEST")"
rm -rf "$DEST"
cp -r "$SRC" "$DEST"

echo "Enabling plugin 'resonitefocus' in kwinrc"
"$kwriteconfig_bin" --file kwinrc --group Plugins --key resonitefocusEnabled true

echo "Reloading KWin scripts"
"$qdbus_bin" org.kde.KWin /Scripting org.kde.kwin.Scripting.unloadScript resonitefocus >/dev/null 2>&1 || true
"$qdbus_bin" org.kde.KWin /KWin reconfigure

# reconfigure picks the script up at the next login, but a running KWin has already
# cached its package list, so load it explicitly for this session.
if [[ "$("$qdbus_bin" org.kde.KWin /Scripting org.kde.kwin.Scripting.isScriptLoaded resonitefocus 2>/dev/null)" != "true" ]]; then
    "$qdbus_bin" org.kde.KWin /Scripting org.kde.kwin.Scripting.loadScript \
        "$DEST/contents/code/main.js" resonitefocus >/dev/null
    "$qdbus_bin" org.kde.KWin /Scripting org.kde.kwin.Scripting.start
fi

if [[ "$("$qdbus_bin" org.kde.KWin /Scripting org.kde.kwin.Scripting.isScriptLoaded resonitefocus 2>/dev/null)" == "true" ]]; then
    echo "Script is loaded."
else
    echo "Warning: script did not load; check 'journalctl --user -b -g resonite-focus'." >&2
fi

cat <<'EOF'

Installed. Press Meta+F to toggle focus between Resonite and your last desktop window.

If nothing happens while Resonite is running, press Meta+Shift+F and check the log:
    journalctl --user -b -g resonite-focus
then add the reported class to CONFIG.classMatches in
    ~/.local/share/kwin/scripts/resonite-focus/contents/code/main.js
and re-run this installer.

Rebind the shortcut in System Settings > Keyboard > Shortcuts > KWin
("Toggle focus between Resonite and the desktop").
EOF
