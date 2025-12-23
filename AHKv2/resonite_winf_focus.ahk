; © 2025 DrSciCortex
; Licensed under the Creative Commons Attribution–NonCommercial–ShareAlike 4.0 International License (CC-BY-NC-SA-4.0).
; See https://creativecommons.org/licenses/by-nc-sa/4.0/
; This file contains original code by the author under the above license.

#Requires AutoHotkey v2.0
#SingleInstance Force
#UseHook
CoordMode "Mouse", "Screen"  ; store/restore in screen coordinates

; Win+F toggles focus to/from Resonite, restoring mouse only for the previous window
#f::
{
    static busy := false
    static lastWin := 0
    static lastMx := "", lastMy := ""   ; mouse pos for the previous (non-Resonite) window

    if busy
        return
    busy := true

    targetCriteria := "Resonite ahk_class UnityWndClass ahk_exe Renderite.Renderer.exe"
    targetHwnd := WinExist(targetCriteria)
    if !targetHwnd {
        MsgBox "Resonite window not found.", "Resonite Focus", "Icon!"
        busy := false
        return
    }

    curr := WinActive("A")
    MouseGetPos &mx, &my

    ; If Resonite is minimized, restore it first
    if WinGetMinMax("ahk_id " targetHwnd) = -1
        WinRestore "ahk_id " targetHwnd

    if (curr != targetHwnd) {
        ; Going TO Resonite: remember the current window + its mouse pos
        lastWin := curr
        lastMx := mx, lastMy := my

        WinActivate "ahk_id " targetHwnd
        WinWaitActive "ahk_id " targetHwnd, , 0.4
        ; Do NOT move mouse here (Resonite manages its own cursor)
    } else {
        ; Leaving Resonite: just go back and restore previous window's mouse pos if known
        if lastWin && WinExist("ahk_id " lastWin) {
            WinActivate "ahk_id " lastWin
            WinWaitActive "ahk_id " lastWin, , 0.4
            if (lastMx != "" && lastMy != "")
                MouseMove lastMx, lastMy, 0
        } else {
            ; No valid previous window: minimize Resonite (no mouse restore)
            WinMinimize "ahk_id " targetHwnd
        }
    }

    ; Debounce key-up to avoid focus bounce
    KeyWait "f"
    KeyWait "LWin"
    KeyWait "RWin"

    busy := false
}

