# CyberFingerMod

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that makes Hand Tracking better by disabling the laser geometry override when the dash is open.
Should be used in combination with moving the Tool Anchor to the palm of the hand. Also recommended is a Finger Mounted Gamepad (more details coming soon), and redefining the steam gestures to disable several conflicting gestures (more details coming soon).

## Installation
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
1. Place [CyberFingerMod.dll](https://github.com/DrSciCortex/CyberFingerMod/releases/latest/download/CyberFingerMod.dll) into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a default install. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create this folder for you.
1. Start the game. If you want to verify that the mod is working you can check your Resonite logs.


## Recommendation: Install Keep focus solution

AutoHotKey v2 script for resonite is under ./AHKv2
Refer to AutoHotKey v2 docs for how to install it. 

Usage:
Pin AutoHotKey to start or taskbar.  After launching once, Right click  and the script will be in the quick launch list
After launching the script, it will how up in your dock.

; Keep Resonite focused (AHK v2)
; - Forces focus back to Resonite if anything steals it
; - Restores if minimized
; - Hotkeys:
;     Ctrl+Alt+F  -> Toggle force-focus on/off
;     Ctrl+Alt+T  -> Toggle AlwaysOnTop
;     Ctrl+Alt+Q  -> Quit script

