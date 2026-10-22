# VRCam Legacy

A VR-tracked camera for **GTA V Legacy**: an invisible camera "rat" that follows the rendered camera (for the Rockstar Editor), a flashlight and a camera light with beam settings, deep zoom down to FOV 1, a viewfinder with HUD, presets, gamepad zoom and an in-game settings menu. VRCam 4.0 is a ScriptHookVDotNet v3 script.

**Best with GTAVR: Legacy** (the VR mod): https://github.com/rashwashere/GTAVR/tree/legacy. GTAVive.asi gives you VR; VRCam adds the camera rat, flashlight, zoom and viewfinder. The mouse and gamepad zoom only work while GTAVR's VR view is on, and the camera rat follows the scripted camera that is rendering (GTAVR's VR camera); the camera light and the viewfinder also work without GTAVR. When GTAVR is not installed, VRCam shows a one-time tip after the game has loaded (turn it off in the menu: General > Show Companion Tip, or `ShowCompanionTip=0` in `VRcam.ini`).

Downloads: https://github.com/rashwashere/VRcamPlus/releases (source on the [`legacy` branch](https://github.com/rashwashere/VRcamPlus/tree/legacy)).

## Install

1. Install [Script Hook V](http://www.dev-c.com/gtav/scripthookv/) and [ScriptHookVDotNet v3](https://github.com/scripthookvdotnet/scripthookvdotnet/releases) (any v3 build for GTA V Legacy).
2. Copy `scripts\VRcam.dll` from the release zip into the `scripts` folder in the GTA V folder (create it if it is missing).
3. Start the game. Press **Ctrl+F5** for the menu, or **Ctrl+F11** to start VRCam. With GTAVR: Legacy, VRCam also starts by itself when VR starts.

VRCam needs no ini file to start: it uses its defaults and writes `VRcam.ini` in the game folder the first time a setting, key or preset is saved.

## Keys

Every key can be changed in the menu (**Keys** page: select a row, press Enter, press the new combination; Esc cancels; saved at once) or in the `[Keys]` section of `VRcam.ini`. A bind is optional `Ctrl`, `Shift`, `Alt` and one key joined with `+`, for example `Ctrl+F5`, `Ctrl+Shift+K` or `J`. Matching is exact: a bind without modifiers does not fire while Ctrl, Shift or Alt is held. Set a key to None (or leave it empty) to unbind it; the menu key always stays bound. On the Keys page, Delete while waiting for a key sets it to None. Rows marked `(!)` on the Keys page share their combination with another VRCam key. The ini name of each key is in brackets.

| Default | Action |
|---|---|
| Ctrl+F5 | Open / close the menu (`OpenMenu`) |
| Ctrl+F11 | Start VRCam (spawns the camera rat) and toggle the viewfinder (`StartCamera`) |
| F5 | Show / hide the camera rat (`ShowHideRat`) |
| F10 | Lock / unlock the rat, flashlight and zoom controls (`LockControls`) |
| Ctrl+F10 | Save the current settings as a new preset (`SavePreset`) |
| Ctrl+Equals / Ctrl+Minus | Next / previous preset (`NextPreset` / `PrevPreset`) |
| CapsLock | Flashlight on / off (`ToggleFlashlight`) |
| Ctrl+3 | Lock / unlock flashlight movement (`FlashlightLock`) |
| Up / Down | Flashlight forward / back (`FlashlightForward` / `FlashlightBack`) |
| Left / Right | Flashlight left / right (`FlashlightLeft` / `FlashlightRight`) |
| PageUp / PageDown | Flashlight up / down (`FlashlightUp` / `FlashlightDown`) |
| Ctrl+Up / Ctrl+Down | Flashlight pitch up / down (`FlashlightPitchUp` / `FlashlightPitchDown`) |
| Ctrl+CapsLock | Zoom mode: wheel / LMB-RMB click / mouse slide (`ZoomModeToggle`) |
| Ctrl+1 | Lock / unlock the zoom-in limit at the current FOV (`ZoomInLock`) |
| Ctrl+2 | Lock / unlock the zoom-out limit at the current FOV (`ZoomOutLock`) |
| K / J | Viewfinder zoom in / out (`ViewfinderZoomIn` / `ViewfinderZoomOut`) |
| H | Viewfinder aspect ratio (`AspectRatio`) |
| N | Viewfinder rule-of-thirds grid (`ToggleGrid`) |

Only the menu key and the Start key work before VRCam has started; the viewfinder keys work while the viewfinder is shown; the flashlight position keys work while the flashlight is on and unlocked.

**Mouse** (zoom mode Wheel): wheel = zoom, Shift+wheel / Ctrl+wheel = zoom-in / zoom-out smoothness. (Click mode): LMB / RMB = zoom in / out, Shift or Ctrl + LMB / RMB = smoothness. (Slide mode): push the mouse forward / back. Ctrl+middle mouse = reset the zoom.

**Gamepad**: hold D-pad Right and press A to open the menu, B goes back. On foot: LB = flashlight, RB = lock / unlock the flashlight position, D-pad = move the unlocked flashlight. Right stick or triggers zoom (Controller page).

The default keys do not overlap GTAVR: Legacy's default keys (Ctrl+F6, Ctrl+L, Ctrl+Shift+L, Ctrl+Shift+C, 1, 2, U and the Ctrl+Shift stabilizer keys).

## Menu

Ctrl+F5 opens it. The menu is driven by the game's own menu controls (arrow keys, Enter, Backspace, Esc, or the gamepad D-pad, A and B), so navigation always works whatever the key binds. Up / Down select a row, Left / Right change a value (hold to speed up), Enter toggles, opens a page or lets you type an exact value, Backspace or Esc goes back. Pages:

- **Main**: flashlight, flashlight follows the VR view, light preset, camera light.
- **Camera**: FOV, zoom range (down to FOV 1, up to 130), zoom smoothness, zoom mode, slide sensitivity, soft zoom locks.
- **Flashlight**: on / off, movement lock, follow VR view 1:1, offsets and pitch, beam on / off, light presets (Torch, Bright, Flood, Spot, Max), brightness, range, width, shadows, edge softness and roundness, camera light (height, tilt) and the light in the Rockstar Editor.
- **Viewfinder**: on / off, zoom level, aspect ratio, thirds grid, smoothing.
- **Rat Camera**: BMX camera rider, focus follows camera, spawn rat, rat visible, position / rotation smoothing.
- **Presets**: load (Left / Right), rename (Enter), save as new.
- **HUD & Overlay**: zoom / status text and the on-screen key list.
- **Controller**: gamepad support, zoom control mode, speed, response, deadzone.
- **Keys**: every key above, and "Reset Keys to Defaults".
- **General**: auto-hide GTAVR's hand pistol when VR starts, hide it now, show companion tip.

## Features

- **Camera rat**: an invisible rat that follows the rendered camera with steadicam smoothing, as a free-camera target for the Rockstar Editor. Vehicle inertia compensation; the rat jumps to the camera after a teleport.
- **Flashlight**: a torch prop on the rat or 1:1 on the view, with VRCam's own spot light (works day and night) and light presets.
- **Camera light**: the same beam straight from the camera, no prop needed; optionally drawn in the Rockstar Editor.
- **Zoom**: mouse wheel, click or slide, gamepad stick or triggers, soft zoom locks, deep zoom to FOV 1 with even control below FOV 10.
- **Viewfinder**: framing preview with zoom levels, aspect ratios, thirds grid and recording status.
- **Presets**: zoom and flashlight settings saved under a name, cycled with keys or from the menu.
- **BMX camera rider** (off by default): swaps control to an invisible rider attached to the vehicle under you, so your character stays in frame. It runs on the Start key, and on GTAVR's Enter VR key so the swap happens before VR hides your character.
- Diagnostics log `VRcam_trace.log` in the game folder (`TraceLog=false` in `VRcam.ini` turns it off).

## Building

Visual Studio 2022 or Build Tools with .NET Framework 4.8. `ScriptHookVDotNet3.dll` is not in the repository: download a ScriptHookVDotNet v3 release and copy `ScriptHookVDotNet3.dll` into `VRcam\` next to `VRcam.csproj`. Then:

```
msbuild VRcam\VRcam.csproj /p:Configuration=Release
```

The output is `VRcam\bin\Release\VRcam.dll`.

## Credits

- Based on VRcam by theBmag (MIT), https://github.com/theBmag/VRcam. See `LICENSE.txt`.
- Alexander Blade: Script Hook V. The ScriptHookVDotNet team: ScriptHookVDotNet.
