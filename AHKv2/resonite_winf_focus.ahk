; © 2025 DrSciCortex
; Licensed under the Creative Commons Attribution–NonCommercial–ShareAlike 4.0 International License (CC-BY-NC-SA-4.0).
; See https://creativecommons.org/licenses/by-nc-sa/4.0/
; This file contains original code by the author under the above license.

#Requires AutoHotkey v2.0
#SingleInstance Force
#UseHook
CoordMode "Mouse", "Screen"  ; store/restore in screen coordinates

; Win+F cycles focus Delphium -> Resonite -> previous window, restoring mouse only for the previous window.
; Whichever of Delphium/Resonite isn't running is skipped, so with just one it's a plain toggle.
#f::
{
    static busy := false
    static lastWin := 0
    static lastMx := "", lastMy := ""   ; mouse pos for the previous (non-Resonite) window

    if busy
        return
    busy := true

    ; Cycle order. Delphium (Resonite private universe) runs the same renderer; only the window title differs.
    targetCriteria := [
        "Delphium ahk_class UnityWndClass ahk_exe Renderite.Renderer.exe",
        "Resonite ahk_class UnityWndClass ahk_exe Renderite.Renderer.exe"
    ]
    targets := []
    for crit in targetCriteria
        if hwnd := WinExist(crit)
            targets.Push(hwnd)
    if !targets.Length {
        MsgBox "Delphium/Resonite window not found.", "Resonite Focus", "Icon!"
        busy := false
        return
    }

    curr := WinActive("A")
    MouseGetPos &mx, &my

    ; Position of the active window in the cycle (0 = not one of ours)
    idx := 0
    for i, hwnd in targets
        if (hwnd = curr)
            idx := i

    if (idx < targets.Length) {
        if !idx {
            ; Entering the cycle: remember the current window + its mouse pos
            lastWin := curr
            lastMx := mx, lastMy := my
        }
        nextHwnd := targets[idx + 1]

        ; If it's minimized, restore it first
        if WinGetMinMax("ahk_id " nextHwnd) = -1
            WinRestore "ahk_id " nextHwnd

        WinActivate "ahk_id " nextHwnd
        WinWaitActive "ahk_id " nextHwnd, , 0.4
        ; Do NOT move mouse here (Resonite manages its own cursor)
    } else {
        ; Leaving the last one: just go back and restore previous window's mouse pos if known
        if lastWin && WinExist("ahk_id " lastWin) {
            WinActivate "ahk_id " lastWin
            WinWaitActive "ahk_id " lastWin, , 0.4
            if (lastMx != "" && lastMy != "")
                MouseMove lastMx, lastMy, 0
        } else {
            ; No valid previous window: minimize them all (no mouse restore)
            for hwnd in targets
                WinMinimize "ahk_id " hwnd
        }
    }

    ; Debounce key-up to avoid focus bounce
    KeyWait "f"
    KeyWait "LWin"
    KeyWait "RWin"

    busy := false
}

