# Resonite Focus Toggle for KDE Plasma

The KDE Plasma equivalent of [AHKv2/resonite_winf_focus.ahk](../AHKv2/resonite_winf_focus.ahk):
**Meta+F** swaps keyboard & mouse focus between Resonite and the desktop window you
came from, so you can use your desktop while in VR.

It is a KWin script, so it works on **both Wayland and X11** and needs no extra
packages — no `xdotool`, no `ydotool`, no accessibility permissions. Tested on
Plasma 6.7 / KWin 6 (Wayland).

## Install

```bash
./install.sh
```

This copies the script to `~/.local/share/kwin/scripts/resonite-focus/`, enables it in
`kwinrc`, and loads it into the running KWin. It survives logout. To remove it:

```bash
./uninstall.sh
```

## Behaviour

Same state machine as the AHK version:

- **Not on Resonite** → remember the active window, un-minimize Resonite, follow it to
  its virtual desktop/activity if needed, and activate it.
- **On Resonite** → go back to the remembered window.
- **On Resonite with no remembered window** → minimize Resonite instead.

One difference from the AHK script: it does not save and restore the mouse position.
Wayland gives no compositor-script API for warping the pointer, and KWin's focus
follows the window anyway, so in practice the pointer lands where you left it.

## If Meta+F does nothing

Resonite's window class depends on how you launch it. With Resonite running, press
**Meta+Shift+F** and read the log:

```bash
journalctl --user -b -g resonite-focus
```

Every normal window is listed with its class, and the matched one is tagged
`<== MATCHES`. If nothing matched, add the class you see to `CONFIG.classMatches` in
[resonite-focus/contents/code/main.js](resonite-focus/contents/code/main.js) and re-run
`./install.sh`.

Out of the box it matches `steam_app_2519830` (the Steam/Proton launch), any class
containing `renderite`, and `resonite.exe`. As a fallback it accepts a window titled
`Resonite…` *only* if the window also looks like a Wine/Proton window — otherwise Steam's
own game page, whose window is literally captioned `Resonite`, would steal the shortcut.

## Rebinding

System Settings → Keyboard → Shortcuts → KWin → *"Toggle focus between Resonite and the
desktop"*. Or change `CONFIG.shortcut` in `main.js` and reinstall.
