// © 2025 DrSciCortex
// Licensed under the Creative Commons Attribution–ShareAlike 4.0 International License (CC-BY-SA-4.0).
// See https://creativecommons.org/licenses/by-sa/4.0/
// This file contains original code by the author under the above license.
//
// KDE Plasma (KWin 6) equivalent of AHKv2/resonite_winf_focus.ahk.
// Meta+F toggles focus to/from Resonite; Meta+Shift+F dumps window identity to the log.

const CONFIG = {
    shortcut: "Meta+F",
    debugShortcut: "Meta+Shift+F",

    // Lowercase substrings tested against the window's resourceClass / resourceName.
    // Proton exports WM_CLASS as steam_app_<appid>; a non-Steam/Wine launch usually
    // exports the executable name instead. Add your own here if neither matches.
    classMatches: [
        "steam_app_2519830",
        "renderite.renderer.exe",
        "renderite",
        "resonite.exe"
    ],

    // Fallback for a non-Steam launch: window title must *start with* one of these,
    // AND the window must look like a Wine/Proton window (see looksLikeWine). Both
    // halves are needed — an editor holding resonite-notes.txt and Steam's own game
    // page (resourceClass "steam", caption exactly "Resonite") would otherwise match.
    captionMatches: [
        "resonite"
    ],

    // Never a Resonite window, whatever it calls itself.
    classExcludes: [
        "steam",
        "steamwebhelper"
    ]
};

// internalId (as a string) of the window we came from, so we can go back to it.
let lastWindowId = null;

function norm(value) {
    return String(value === undefined || value === null ? "" : value).toLowerCase();
}

function allWindows() {
    // windowList() is KWin 6; clientList() is the KWin 5 spelling.
    if (typeof workspace.windowList === "function") {
        return workspace.windowList();
    }
    return workspace.clientList();
}

// Proton exports steam_app_<appid>; a plain Wine prefix exports the executable name.
// Native Wayland/X11 apps do neither, which is what keeps the caption fallback honest.
function looksLikeWine(cls, name) {
    return cls.indexOf("steam_app_") === 0 ||
           cls.indexOf("wine") !== -1 ||
           /\.exe$/.test(cls) ||
           /\.exe$/.test(name);
}

function isResonite(w) {
    if (!w || !w.normalWindow) {
        return false;
    }
    const cls = norm(w.resourceClass);
    const name = norm(w.resourceName);

    for (const m of CONFIG.classExcludes) {
        if (cls === m || name === m) {
            return false;
        }
    }

    for (const m of CONFIG.classMatches) {
        if (cls.indexOf(m) !== -1 || name.indexOf(m) !== -1) {
            return true;
        }
    }

    if (looksLikeWine(cls, name)) {
        const caption = norm(w.caption);
        for (const m of CONFIG.captionMatches) {
            if (caption.indexOf(m) === 0) {
                return true;
            }
        }
    }
    return false;
}

function findResonite() {
    const windows = allWindows();
    let fallback = null;
    for (const w of windows) {
        if (!isResonite(w)) {
            continue;
        }
        // Prefer a window that is actually mapped; a minimized one is still usable.
        if (!w.minimized) {
            return w;
        }
        if (fallback === null) {
            fallback = w;
        }
    }
    return fallback;
}

function findById(id) {
    if (!id) {
        return null;
    }
    for (const w of allWindows()) {
        if (String(w.internalId) === id) {
            return w;
        }
    }
    return null;
}

function activate(w) {
    if (w.minimized) {
        w.minimized = false;
    }
    // Follow the window if it lives on another virtual desktop or activity.
    if (!w.onAllDesktops && w.desktops && w.desktops.length > 0) {
        workspace.currentDesktop = w.desktops[0];
    }
    if (w.activities && w.activities.length > 0 &&
        w.activities.indexOf(workspace.currentActivity) === -1) {
        workspace.currentActivity = w.activities[0];
    }
    workspace.activeWindow = w;
    if (typeof workspace.raiseWindow === "function") {
        workspace.raiseWindow(w);
    }
}

function toggleFocus() {
    const target = findResonite();
    if (!target) {
        print("resonite-focus: no Resonite window found. " +
              "Press " + CONFIG.debugShortcut + " with Resonite running to list window classes.");
        return;
    }

    const active = workspace.activeWindow;
    const onResonite = active && String(active.internalId) === String(target.internalId);

    if (onResonite) {
        // Leaving Resonite: return to the window we came from.
        const previous = findById(lastWindowId);
        if (previous && String(previous.internalId) !== String(target.internalId)) {
            activate(previous);
        } else {
            // No valid previous window: get Resonite out of the way instead.
            target.minimized = true;
        }
    } else {
        // Going to Resonite: remember where we were first.
        if (active && active.normalWindow && !isResonite(active)) {
            lastWindowId = String(active.internalId);
        }
        activate(target);
    }
}

function dumpWindows() {
    print("resonite-focus: --- window list ---");
    for (const w of allWindows()) {
        if (!w.normalWindow) {
            continue;
        }
        print("resonite-focus: class=" + w.resourceClass +
              " name=" + w.resourceName +
              " caption=" + w.caption +
              " minimized=" + w.minimized +
              (isResonite(w) ? "  <== MATCHES" : ""));
    }
    print("resonite-focus: --- end ---");
}

registerShortcut("ResoniteFocusToggle",
                 "Toggle focus between Resonite and the desktop",
                 CONFIG.shortcut,
                 toggleFocus);

registerShortcut("ResoniteFocusDebug",
                 "Log window classes (Resonite focus troubleshooting)",
                 CONFIG.debugShortcut,
                 dumpWindows);

print("resonite-focus: loaded, " + CONFIG.shortcut + " toggles focus.");
