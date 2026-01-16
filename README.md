# CyberFingerMod

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that makes Hand Tracking better by disabling the laser geometry override when the dash is open.
Should be used in combination with moving the Tool Anchor to the palm of the hand. Also recommended is a Finger Mounted Gamepad (https://scicortex.com/products/cyberfinger-v1-0-beta), and redefining the steam gestures to disable several conflicting gestures (more details coming soon).

## Resolute Installation

Install Resolute Mod manager (https://github.com/Gawdl3y/resolute), and enable CyberFingerMod under "Hardware Integration".

## Manual Installation
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Place [CyberFingerMod.dll](https://github.com/DrSciCortex/CyberFingerMod/releases/latest/download/CyberFingerMod.dll) into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a default install. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create this folder for you.
3. Start the game. If you want to verify that the mod is working you can check your Resonite logs.

## Configuration

1. Pair both left and right CyberFinger with PC over bluetooth (they will initially appear as "input" on first pairing)
1. For Meta Quest devices, SteamLink over Virtual Desktop is recommended, due to handtracking limitations of the latter
1. Disable auto switching between controller and hand-tracking in Quest OS settings (Manually select handtracking when using CyberFinger before launching SteamLink).
1. Remove all hand gestures for hand input controller in SteamVR
1. Disable steam input for the CyberFinger gamepad for resonite
1. Load Resonite and enable "keep gamepad in focus"
1. Use ResoniteModSettings to configure the CyberFingerMod in resonite
1. Edit your avatar to move your laser anchor to emanate from the palm of your hand, comfortably between your index finger and thumb.

## Default Controls Layout

<img width="535" height="376" alt="wrist_module_buttons" src="https://github.com/user-attachments/assets/6cabc6d2-16d0-4a36-9d04-cb4e6534bbce" />

<img width="461" height="399" alt="rightfinger_module_buttons" src="https://github.com/user-attachments/assets/a2998312-5db2-436a-bd97-4ba38568185b" />

Buttons can be reassigned by uploading a revised config .JSON using the quick_cfg.py provided by the CyberFinger firmware. 

## Recommendation: Install win-f resonite/desktop focus solution

AutoHotKey v2 script for resonite is under ./AHKv2
Refer to AutoHotKey v2 docs for how to install it and have it auto-launch. 

This script enables keyboard & mouse focus to be swapped between Resonite, and the window manager desktop by pressing win-f.
This is very useful for interacting with you desktop using mouse and keyboard, something you will spontaneously be interested in doing now that you have CyberFingers!

## Known Issues

The following are know issues we are working on fixes for:
1. Resonite RawDataTool support is incomplete, as they assume VR Controllers as datasources ... this affects objects that use them under the hood, like laser pointers.
1. Laser noise - this can be addressed with some filtering to allow more accurate pointing
1. Left controller is high latency (~1s) for the first 30s-1min of booting up... After this warm-up period, latency becomes very usable. This is likely an ESP-NOW config issue.
1. One hint specific to the SciCortex official CYBRFNGR hardware: **We don't have much data yet how strong the plug linking the ring module to the wrist module and its soldier joints are**.  We have tried to stabilize it as much possible. **We suggest to avoid unplugging the plug**. Its not needed for normal use. If you do, do so gently, and only if the ring module needs to be replaced, or if needed for other troubleshooting or servicing. 


## License 

All code in this repository that did not originate from the "Resonite Mod Template" are © 2025 DrSciCortex and licensed under the
[GNU Lesser General Public License version 3][lgpl-3].

See LICENSE.md file for more details. 

[lgpl-3]: https://www.gnu.org/licenses/lgpl-3.0.en.html

