# GTA 5 Machinima VR-Tracked Camera

Turn any SteamVR-compatible VR headset into a tracked virtual camera for the Rockstar Editor. Create camera movements that feel as if they came straight out of an official GTA V cutscene! Enjoy more realistic handheld motion, film handheld action atop a moving train, capture close-up or intense interior car shots.

## GTA: Vive

GTA: Vive is the mod that retrieves the position and rotation of your VR headset and translates that into the game. When recording in Rockstar Editor, the footage can be played back using the in-game camera—even without this mod. However, GTA: Vive does not offer adjustments aside from camera shake. Because its field of view is very wide (i.e., very zoomed out), the export must be significantly cropped for a more appropriate framing. This process may lead to wasted rendered areas and a loss in quality.

## About This Mod

This mod provides an alternative camera type that you can use alongside the standard Game Camera type. It works by spawning an invisible rat and attaching it to the game camera. Then, in Rockstar Editor, select the rat as the Free Camera along with the look and move target (usually number 1 if no other nearby peds are present) and the Rigid mount. As it acts as a free camera, you can still perform adjustments such as re-framing, zooming in and out, and using markers. If you aren’t satisfied with your VR-tracked shot, you can always switch back to the traditional Rockstar Editor free camera on the same clip!

## Smoothing

Another feature implemented with the rat camera is smoothing (also known as stabilization). This reduces the shakiness of the VR headset’s movement to create a Steadicam-like effect. The smoothing amount is adjustable in the configuration. However, if set too low, the camera may lag behind like how the smooth blends lag behind, and if set too high, it may become jumpy or glitchy due to in-game physics. It is recommended not to adjust the positional smoothing factor when in or on vehicles since the vehicle inertia compensation values are currently hard-coded.

## The Viewfinder

The viewfinder adds a preview window that helps you achieve the ideal framing for your shot, complete with a bonus rule-of-thirds grid! You can choose between 21:9 or 16:9 aspect ratios. Sometimes you might not want the default 1× zoom level. The preview size can be adjusted to simulate zooming—though you will still need to modify the zoom level in the recorded clip within Rockstar Editor before exporting.

If you wish to export with a 21:9 ratio, you must first export the video at a 16:9 resolution and then crop the top and bottom using your video editor. Alternatively, the Extended Video Export Voukoder crop filter can perform this cropping during export.

## Roll Axis

The rat camera will not move on the roll axis, even if the mount type is set to Rigid. This appears to be a limitation of the Rigid camera type. You can still manually add roll using markers if you desire an effect like the Dutch angle. The Game Camera type, on the other hand, continues to move on the roll axis as usual.

## Vehicles

If you are in or on a vehicle, an invisible bike will spawn. The camera ped rides this invisible bike, which is attached to the original vehicle. This “invisible bike workaround” is used because the GTA: Vive mod does not synchronize the position quickly enough when standing on a moving object unless directly attached. If you wish to activate this mod while standing on top of an NPC or a street-spawned car, consider adding it to the Menyoo database to prevent it from despawning immediately.

## FAQ

**Does the VR camera work in Story Mode or in the Rockstar Editor?** This mod can only be activated in Story Mode. When you enter the camera editor in Rockstar Editor, the mod deactivates. Although you cannot re-shoot an existing clip with VR inside Rockstar Editor, you can switch to the traditional Rockstar Editor free camera.

**How can I quickly get multiple takes?** If you use Menyoo, you can set task sequences on your character to loop their actions, allowing you to record as many takes as you need until you capture the perfect shot.

**Can I disable VR mode after I have enabled it?** As far as I know, GTA: Vive has not implemented a method to disable VR mode once it’s enabled. As a workaround—and without restarting the game—you can load a previous save from the game menu or go in and out of the Rockstar Editor camera editor back to Story Mode.

**Will this mod work on the new GTA V Enhanced Edition?** It depends on whether the other mods work with the Enhanced Edition. My guess is that GTA: Vive will not work on the Enhanced Edition due to the newer DirectX 12 renderer being used.

**Are you really good at programming?** _cough ChatGPT cough Copilot_

**Do people still make GTA V machinima in 2025?** Yes! Plus, Whanowa will soon be hosting the GTA V machinima contest—[MACHINIMAY 2025](https://youtu.be/2kUPh960iFc)!

## Requirements

-   **[SteamVR](https://store.steampowered.com/app/250820/SteamVR/)** and a SteamVR-compatible headset (e.g., I use a Meta Quest 3 via Virtual Desktop)
    
-   **[ScriptHookV](https://www.dev-c.com/gtav/scripthookv/)**
    
-   **[Community ScriptHookV .NET](https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/)** (nightly)
    
-   **[GTA: Vive](https://web.archive.org/web/20171002000013/https://rly.sexy/gta-vive-alpha-0-1-release/)**
    
-   **[Extended Video Export](https://www.gta5-mods.com/scripts/extended-video-export)** (not required but highly recommended!)
    
## Installation/Usage

-   You do not need the Steam version of GTA V for SteamVR to work. SteamVR will launch automatically when you start the game.
    
-   Copy `VRcam.ini` into the Grand Theft Auto V directory and copy `VRcam.dll` into the `scripts` folder within the Grand Theft Auto V directory.
    
-   When in Story Mode and ready to enter VR mode, press **L** to activate GTA: Vive along with this mod.
    
-   For the VR view to display correctly, change the aspect ratio to 5:4 in the graphics settings and turn off VSync to minimize stutters. If stuttering continues, lower your graphics settings. Remember to revert the aspect ratio back when switching to Rockstar Editor—you don’t want your exports stretched!
    
## Buttons

| Key                     | Function                              |
|-------------------------|---------------------------------------|
| L                       | Activate / Toggle UI & Viewfinder     |
| F5                      | Show / Hide Rat                       |
| F10                     | Lock / Unlock All Controls            |
| Ctrl+F10                | Save Preset                           |
| Ctrl++                  | Cycle to Next Preset                  |
| Ctrl+-                  | Cycle to Previous Preset              |

| Key                     | Function                              |
|-------------------------|---------------------------------------|
| J                       | Viewfinder Zoom Out                   |
| K                       | Viewfinder Zoom In                    |
| U                       | Toggle Rule of Thirds Grid            |
| H                       | Change Aspect Ratio                   |

| Key                     | Function                              |
|-------------------------|---------------------------------------|
| LMB / RMB               | Zoom In / Out (Click Mode)            |
| Mouse Wheel             | Zoom In / Out (Scroll Mode)           |
| Ctrl+CapsLock           | Toggle Zoom Mode (Scroll / Click)     |
| Ctrl+Middle Mouse       | Reset Zoom to Default                 |
| Ctrl+1                  | Lock / Unlock Zoom IN at current FOV  |
| Ctrl+2                  | Lock / Unlock Zoom OUT at current FOV |
| Shift+Wheel / Shift+LMB | Adjust Zoom IN Smoothness             |
| Ctrl+Wheel / Ctrl+RMB   | Adjust Zoom OUT Smoothness            |

| Key                     | Function                              |
|-------------------------|---------------------------------------|
| CapsLock                | Toggle Flashlight                     |
| Ctrl+3                  | Lock / Unlock Flashlight Movement     |
| Up / Down               | Move Flashlight Forward / Back        |
| Left / Right            | Move Flashlight Left / Right          |
| PageUp / PageDown       | Move Flashlight Up / Down             |
| Ctrl+Up / Ctrl+Down     | Rotate Flashlight Pitch               |

## Configuration

The `BaseWidth` and `PresetHeights` settings might need to be adjusted if you are using a VR headset with a field of view that differs significantly from mine. If you want to define your own aspect ratio, account for the 5:4 stretch (e.g., 50% * (9/16) * (5/4) ≈ 35%).

| Property                  | Explanation                                                                                                       |
|---------------------------|-------------------------------------------------------------------------------------------------------------------|
| **BaseWidth**             | Width of the viewfinder at 1.00x 16:9 compared to the game window (50% measured on my Quest 3)                    |
| **PresetHeights**         | Height of the viewfinder at 16:9 and 21:9 (approximately 35% and 27% respectively)                                |
| **AspectRatioLabels**     | Labels for the aspect ratio as shown in the info text under the viewfinder                                        |
| **ViewfinderSmoothingFactor** | Controls the smoothness of the viewfinder's animation                                                         |
| **AspectSmoothingFactor** | Determines the smoothness of the aspect ratio animation                                                          |
| **DefaultZoom**           | Sets the default zoom level                                                                                      |
| **ZoomLevels**            | Preset zoom levels available                                                                                     |
| **PositionalSmoothingFactor** | Adjusts the amount of positional smoothing (lower values yield a smoother effect)                             |
| **RotationSmoothingFactor**   | Adjusts the amount of rotation smoothing (lower values yield a smoother effect)                               |

# Source Code
[GitHub](https://github.com/theBmag/VRcam)
