; Keep Resonite focused (AHK v2)
; - Forces focus back to Resonite if anything steals it
; - Restores if minimized
; - Hotkeys:
;     Ctrl+Alt+F  -> Toggle force-focus on/off
;     Ctrl+Alt+T  -> Toggle AlwaysOnTop
;     Ctrl+Alt+Q  -> Quit script

; ---------- CONFIG ----------
appExe := "Resonite.exe"    ; change if needed
intervalMs := 300           ; check interval (ms)
forceAlwaysOnTop := false
; ----------------------------

#Requires AutoHotkey v2.0
#SingleInstance Force
Persistent

Enabled := true
SetTimer(KeepFocus, intervalMs)

^!f:: {
    global Enabled
    Enabled := !Enabled
    TrayTip("Resonite Focus", "Force focus: " (Enabled ? "ON" : "OFF"), 2)
}

^!t:: {
    global forceAlwaysOnTop, appExe
    forceAlwaysOnTop := !forceAlwaysOnTop
    WinSetAlwaysOnTop forceAlwaysOnTop, "ahk_exe " appExe
    TrayTip("Resonite Focus", "AlwaysOnTop: " (forceAlwaysOnTop ? "ON" : "OFF"), 2)
}

^!q::ExitApp()

KeepFocus() {
    global Enabled, appExe, forceAlwaysOnTop
    if !Enabled
        return
    if !WinExist("ahk_exe " appExe)
        return
    
    state := WinGetMinMax("ahk_exe " appExe)
    if (state = -1)  ; minimized
        WinRestore("ahk_exe " appExe)

    if !WinActive("ahk_exe " appExe)
        WinActivate("ahk_exe " appExe)

    if forceAlwaysOnTop
        WinSetAlwaysOnTop true, "ahk_exe " appExe
}
