# CyberFingerMod

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that makes Hand Tracking better by disabling the laser geometry override when the dash is open.
Should be used in combination with moving the Tool Anchor to the palm of the hand. Also recommended is a Finger Mounted Gamepad (https://scicortex.com/products/cyberfinger-v1-0-beta). See **Configuration - CyberFingers** below.

## Resolute Installation

Install Resolute Mod manager (https://github.com/Gawdl3y/resolute), and enable CyberFingerMod under "Hardware Integration".

## Manual Installation
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Place [CyberFingerMod.dll](https://github.com/DrSciCortex/CyberFingerMod/releases/latest/download/CyberFingerMod.dll) into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a default install. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create this folder for you.
3. Start the game. If you want to verify that the mod is working you can check your Resonite logs.

## Building from Source

**Requirements**
- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) or later, since the mod targets `net10.0-windows`. Check what you have with `dotnet --list-sdks`. You don't need Visual Studio.
- Resonite with [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) installed on the build machine. The build compiles against DLLs from your Resonite folder: `FrooxEngine.dll`, `Elements.Core.dll`, `Renderite.Shared.dll`, `Libraries\ResoniteModLoader.dll` and `rml_libs\0Harmony.dll`.

**Build**
```
git clone https://github.com/DrSciCortex/CyberFingerMod.git
cd CyberFingerMod
dotnet build -c Release
```

The DLL is written to `CyberFingerMod/bin/Release/net10.0-windows/CyberFingerMod.dll`, and the build then copies it into Resonite's `rml_mods` folder, so it loads the next time you start Resonite. A Debug build (`dotnet build` without `-c Release`) goes to `CyberFingerMod/bin/Debug/net10.0-windows/`, with debug symbols embedded, and is copied the same way.

Close Resonite before you build. While it's running, the DLL in `rml_mods` is locked: the copy fails with a warning, and the build still reports success.

**Options**
- The build finds Resonite in the default Steam location: `C:\Program Files (x86)\Steam\steamapps\common\Resonite\` on Windows, or `~/.steam/steam/steamapps/common/Resonite/` on Linux. If yours is elsewhere, pass the path with forward slashes and a trailing slash, for example `dotnet build -c Release -p:ResonitePath=D:/SteamLibrary/steamapps/common/Resonite/`.
- To build without installing, add `-p:CopyToMods=false`.
- On Linux, also add `-p:EnableWindowsTargeting=true`, because the project targets Windows.

## Configuration - CyberFingers

1. **Pair**: Pair both left and right CyberFinger with PC over bluetooth (they will initially appear as "input" on first pairing)
2. **Install** the latest version of the CyberFinger bridge/server.  The installer is available here: [https://github.com/DrSciCortex/CyberFinger_SteamVR/releases](https://github.com/DrSciCortex/CyberFinger_SteamVR/releases)
3. **Launch server**: "CyberFinger Bridge" - Set the server to "gamepad mode", and press "start" button in the server.  Verify that your 2 CyberFingers are detected by the server (it takes about 5 seconds). A diagnostic GUI will appear for left and right controllers near the top of the bridge/server. Pressing joystick and buttons should produce events rendered by the diagnostic GUI. 
4. **Turn on handtracking**: Disable auto switching between controller and hand-tracking in Quest OS settings (Manually select handtracking when using CyberFinger before launching SteamLink).
5. **Connect your headset to PC with handtracking**: For Meta Quest devices, SteamLink over Virtual Desktop is recommended, due to handtracking limitations of the latter
6. **Remove all hand gestures** for hand input controller in SteamVR (see video [here](https://www.youtube.com/watch?v=185BYNgHXvI) )
7. **Disable steam input** for the CyberFinger gamepad for resonite, as it interferes with resonite setting "keep gamepad in focus".
<p align="center"><img width="500" alt="image" src="https://github.com/user-attachments/assets/e0163fc6-6ca7-4851-9906-68e2267f8a59" /></p>
  
8. **Keep gamepad in focus**: Load Resonite and enable "keep gamepad in focus" under settings. The allows you to interact with your desktop with mouse and keyboard while in VR, and the CyberFinger events will still go directly to Resonite.
9. Use ResoniteModSettings to configure the CyberFingerMod in resonite. For example, in those settings you can disable the "virtual keyboard" popping up for text input.  When you've got CyberFingers, you can finally use your real keyboard again!!
10. **Move your laser for handtracking**: If you like, edit your avatar to move your laser anchor to emanate from the palm of your hand, comfortably between your index finger and thumb.
11. **Adjust laser filtering**: Resonite offers laser filtering settings.  You may want to adjust them along the lines shown here, and in particular play with "laser smooth speed"
<p align="center"><img width="500" alt="image" src="https://github.com/user-attachments/assets/d733d496-a958-4437-9029-66b82dbefee2" /></p>

12. (Recommended) **Install win-f resonite/desktop focus solution** - see below.
13. (Recommended) **Adjust windows touch keyboard setting** - Under Windows Settings: For the "Show the touch keyboard" setting, use either "When no keyboard attached" or "Never".   
<p align="center"><img width="500" alt="image" src="https://github.com/user-attachments/assets/7ec54fa8-9fa8-4763-9bef-f19ad4ec7af3" /></p>

## Settings

Set these in ResoniteModSettings, or in `rml_config/CyberFingerMod.json`.

| Setting | Default | What it does |
|---|---|---|
| `ForceOverride` | on | With the dash open, the laser comes from where your avatar puts it (the palm, say), not snapped back to the raw controller tip. |
| `HideVirtualKeyboard` | on | Never shows Resonite's virtual keyboard: use your real one. |
| `GamepadBindings` | on | Binds the CyberFinger gamepad (the bridge's gamepad mode) to Resonite's VR inputs and to Raw Data Tools. When it's off, Resonite's own gamepad bindings apply. The change takes effect the next time Resonite binds its inputs (switching controllers, or a restart). |
| `FollowActiveController` | on | Rebinds locomotion (move, turn, jump) to the controller you're using when you switch back to one Resonite has already seen, for example CyberFingers → Quest controllers → CyberFingers. Resonite binds locomotion only to the controller it registered last, so without this setting locomotion stays on the idle one and the sticks work only in tools. |
| `FilteringEnabled` | off | Smooths the laser's direction (a sternum blend plus a UKF filter) for hand tracking. It needs `ImprovedFiltering` on too. The `Kalman*`, `DeadBand*`, `ArmBlendWeight` and `SternumOffset` settings tune it. |
| `ImprovedFiltering` | on | Allows the smoothing above; `FilteringEnabled` switches it on. |

**With the CyberFinger SteamVR driver**, the CyberFingers are SteamVR controllers, not a gamepad. Turn off `GamepadBindings`. The mod then only moves the laser and hides the keyboard.

## Default Controls Layout
<p align="center">
<img width="400" alt="wrist_module_buttons" src="https://github.com/user-attachments/assets/6cabc6d2-16d0-4a36-9d04-cb4e6534bbce" />
</br>
<img width="400" alt="rightfinger_module_buttons" src="https://github.com/user-attachments/assets/a2998312-5db2-436a-bd97-4ba38568185b" />
</p>
Buttons can be reassigned by uploading a revised config .JSON using the quick_cfg.py provided by the CyberFinger firmware. 

## Recommendation: Install win-f resonite/desktop focus solution

This enables keyboard & mouse focus to be swapped between Resonite, and the window manager desktop by pressing win-f.
This is very useful for interacting with you desktop using mouse and keyboard while in VR/Resonite, something you will spontaneously be interested in doing now that you have CyberFingers!
**For a demo, checkout this video** [here](https://www.youtube.com/watch?v=dNAswHlMzls).

**Windows:** we provide an AutoHotKey v2 script under [./AHKv2](./AHKv2) (Refer to AutoHotKey v2 docs for how to install it and have it auto-launch).

**Linux / KDE Plasma:** we provide an equivalent KWin script under [./KWin](./KWin) — run `./KWin/install.sh` and press Meta+F. It works on both Wayland and X11 and needs no extra packages. See [KWin/README.md](./KWin/README.md) for troubleshooting and rebinding.

## Known Issues

The following are know issues we are working on fixes for:
1. Resonite RawDataTool support is incomplete, as they assume VR Controllers as datasources ... this affects objects that use them under the hood, like laser pointers.
1. Laser noise - this can be addressed with filtering adjustments to allow more accurate pointing (see above **Adjust laser filtering** under **Configuration** above).
1. One hint specific to the SciCortex official CYBRFNGR hardware: **We don't have much data yet how strong the plug linking the ring module to the wrist module and its soldier joints are**.  We have tried to stabilize it as much possible. **We suggest to avoid unplugging the plug**. Its not needed for normal use. If you do, do so gently, and only if the ring module needs to be replaced, or if needed for other troubleshooting or servicing. 

## License 

All code in this repository that did not originate from the "Resonite Mod Template" are © 2025 DrSciCortex and licensed under the
[GNU Lesser General Public License version 3][lgpl-3].

See LICENSE.md file for more details. 

[lgpl-3]: https://www.gnu.org/licenses/lgpl-3.0.en.html

