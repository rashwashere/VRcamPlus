using GTA;
using GTA.Native;
using System;

namespace VRCam
{
    public class ViewfinderText
    {
        private float textPosX;
        private float textPosY;
        private float textScale;
        private int textR, textG, textB, textA;
        private string infoText;

        public ViewfinderText(float posX, float posY, float scale = 0.35f, int r = 255, int g = 255, int b = 255, int a = 255)
        {
            textPosX = posX;
            textPosY = posY;
            textScale = scale;
            textR = r;
            textG = g;
            textB = b;
            textA = a;
        }

        /// <summary>
        /// Updates the info text based on the current zoom, provided aspect label,
        /// and replay recording status.
        /// 
        /// If the current aspect ratio (obtained via GET_ASPECT_RATIO) is not 5:4 (≈1.25),
        /// then the info text is replaced with a warning message and its color is set to slight yellow.
        /// Otherwise, if recording is active, the text color is set to red; if not, white.
        /// </summary>
        public void UpdateInfo(float zoom, string aspectLabel)
        {
            bool isRecording = Function.Call<bool>(Hash.IS_REPLAY_RECORDING);
            string recordingStatus = isRecording ? "RECORDING" : "Inactive";

            // Get the current aspect ratio.
            float currentAspectRatio = Function.Call<float>(Hash.GET_ASPECT_RATIO);

            // 5:4 is 1.25. Allow a small tolerance.
            if (Math.Abs(currentAspectRatio - 1.25f) > 0.01f)
            {
                infoText = "Please change the aspect ratio to 5:4 in the graphics settings!";
                // Set text color to slight yellow.
                textR = 255;
                textG = 255;
                textB = 153;
            }
            else
            {
                infoText = string.Format("{0:F2}x  {1}  Recording: {2}",
                                           zoom, aspectLabel, recordingStatus);
                // If recording is active, set text color to red; otherwise white.
                if (isRecording)
                {
                    textR = 255;
                    textG = 100;
                    textB = 100;
                }
                else
                {
                    textR = 255;
                    textG = 255;
                    textB = 255;
                }
            }
        }

        /// <summary>
        /// Updates the text position.
        /// </summary>
        public void UpdatePosition(float posX, float posY)
        {
            textPosX = posX;
            textPosY = posY;
        }

        /// <summary>
        /// Displays the text on the screen.
        /// </summary>
        public void Display()
        {
            Function.Call(Hash.SET_TEXT_FONT, 0);
            Function.Call(Hash.SET_TEXT_SCALE, textScale, textScale);
            Function.Call(Hash.SET_TEXT_COLOUR, textR, textG, textB, textA);
            Function.Call(Hash.SET_TEXT_CENTRE, true);

            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, infoText);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, textPosX, textPosY);
            
            // Also display zoom info and rat info from other scripts
            DisplayZoomInfo();
            DisplayRatInfo();
            DisplayHelpMenu();
        }

        private void DisplayZoomInfo()
        {
            float currentFov = CameraZoom.CurrentFOV;
            float minFov = CameraZoom.MinFOV;
            float maxFov = CameraZoom.MaxFOV;
            float zoomPercent = ((maxFov - currentFov) / (maxFov - minFov)) * 100f;
            string cameraMode = CameraZoom.UseClickMode ? "Click" : "Scroll";
            int mm = VRCamModConfiguration.FOVToMillimeter(currentFov);
            
            string presetInfo = "";
            if (!string.IsNullOrEmpty(VRCamModConfiguration.LastPreset))
            {
                presetInfo = $"  Preset: {VRCamModConfiguration.LastPreset}";
            }
            
            string info = string.Format("Zoom: {0:F0}%  {1}mm  FOV: {2:F1}  IN: {3:F2}  OUT: {4:F2}  Mode: {5}{6}",
                                       zoomPercent, mm, currentFov, CameraZoom.ZoomInSpeed, CameraZoom.ZoomOutSpeed, cameraMode, presetInfo);

            DrawText(info, 0.5f, 0.02f, 0.35f, 255, 255, 153, 255, true);
        }

        private void DisplayRatInfo()
        {
            // Display flashlight offset at top-middle
            var offset = AttachRatToCamera.FlashlightOffset;
            var rotation = AttachRatToCamera.FlashlightRotation;
            string lockStatus = AttachRatToCamera.FlashlightMovementLocked ? " [LOCKED]" : " [UNLOCKED]";
            string offsetText = $"Offset: X={offset.X:F2} Y={offset.Y:F2} Z={offset.Z:F2} Pitch={rotation.X:F0}°{lockStatus}";
            
            // Color the text differently based on lock status
            int offsetR = AttachRatToCamera.FlashlightMovementLocked ? 255 : 100;
            int offsetG = AttachRatToCamera.FlashlightMovementLocked ? 100 : 255;
            int offsetB = AttachRatToCamera.FlashlightMovementLocked ? 100 : 100;
            DrawText(offsetText, 0.5f, 0.08f, 0.35f, offsetR, offsetG, offsetB, 255, true);
            
            // Display status on left
            float leftStatusX = 0.12f;
            string ratStatus = AttachRatToCamera.RatActive ? "ON" : "OFF";
            string lightStatus = AttachRatToCamera.FlashlightVisible ? "ON" : "OFF";
            string lockStatus2 = AttachRatToCamera.F10Locked ? "UNLOCKED" : "LOCKED";
            string flashMoveStatus = AttachRatToCamera.FlashlightMovementLocked ? "LOCKED" : "UNLOCKED";
            
            DrawText($"Rat: {ratStatus}", leftStatusX, 0.70f, 0.35f, 255, 255, 255, 255, true);
            DrawText($"Light: {lightStatus}", leftStatusX, 0.73f, 0.35f, 255, 255, 255, 255, true);
            DrawText($"Lock: {lockStatus2}", leftStatusX, 0.76f, 0.35f, 255, 255, 255, 255, true);
            DrawText($"Flash Move: {flashMoveStatus}", leftStatusX, 0.79f, 0.35f, 255, 255, 255, 255, true);

            if (AttachRatToCamera.SaveMessageActive)
            {
                DrawText("Settings Saved!", leftStatusX, 0.83f, 0.35f, 100, 255, 100, 255, true);
            }
        }

        private void DisplayHelpMenu()
        {
            float startY = 0.35f;
            float lineHeight = 0.024f;
            float rightMargin = 0.88f;
            
            string zoomControl = CameraZoom.UseClickMode ? "LMB/RMB = Zoom In/Out" : "Mouse Wheel = Zoom In/Out";
            string zoomAdjust1 = CameraZoom.UseClickMode ? "Shift+LMB/RMB = Zoom IN +/-" : "Shift+Wheel = Adjust Zoom IN Speed";
            string zoomAdjust2 = CameraZoom.UseClickMode ? "Ctrl+LMB/RMB = Zoom OUT +/-" : "Ctrl+Wheel = Adjust Zoom OUT Speed";
            
            float textSize = 0.24f;
            float headerSize = 0.27f;
            
            DrawText("--- Main Controls ---", rightMargin, startY, headerSize, 255, 255, 100, 255, false);
            DrawText($"{KeyBinds.UI_ToggleUI} = Toggle All UI & Viewfinder", rightMargin, startY + lineHeight * 1, textSize, 255, 255, 255, 255, false);
            DrawText($"{KeyBinds.UI_ShowHideRat} = Show/Hide Rat", rightMargin, startY + lineHeight * 2, textSize, 255, 255, 255, 255, false);
            DrawText($"{KeyBinds.UI_LockUnlockControls} = Lock/Unlock All Controls", rightMargin, startY + lineHeight * 3, textSize, 255, 255, 255, 255, false);
            DrawText($"{KeyBinds.UI_SavePreset} = Save Preset", rightMargin, startY + lineHeight * 4, textSize, 255, 255, 255, 255, false);
            DrawText($"{KeyBinds.UI_CyclePresets} = Cycle Presets", rightMargin, startY + lineHeight * 5, textSize, 255, 255, 255, 255, false);
            
            DrawText("--- Flashlight Controls ---", rightMargin, startY + lineHeight * 6.5f, headerSize, 255, 255, 100, 255, false);
            DrawText($"{KeyBinds.UI_ToggleFlashlight} = Toggle Flashlight", rightMargin, startY + lineHeight * 7.5f, textSize, 255, 255, 255, 255, false);
            DrawText($"{KeyBinds.UI_FlashlightLock} = Lock/Unlock Movement", rightMargin, startY + lineHeight * 8.5f, textSize, 100, 255, 100, 255, false);
            DrawText($"{KeyBinds.UI_FlashlightMove} = Move Forward/Back (X)", rightMargin, startY + lineHeight * 9.5f, textSize, 255, 255, 255, 255, false);
            DrawText($"{KeyBinds.UI_FlashlightMoveLeftRight} = Move Left/Right (Z)", rightMargin, startY + lineHeight * 10.5f, textSize, 255, 255, 255, 255, false);
            DrawText($"{KeyBinds.UI_FlashlightMoveUpDown} = Move Up/Down (Y)", rightMargin, startY + lineHeight * 11.5f, textSize, 255, 255, 255, 255, false);
            DrawText($"{KeyBinds.UI_FlashlightRotate} = Rotate Pitch", rightMargin, startY + lineHeight * 12.5f, textSize, 255, 255, 255, 255, false);
            
            DrawText("--- Zoom Controls ---", rightMargin, startY + lineHeight * 14f, headerSize, 255, 255, 100, 255, false);
            DrawText(zoomControl, rightMargin, startY + lineHeight * 15f, textSize, 255, 255, 255, 255, false);
            DrawText(zoomAdjust1, rightMargin, startY + lineHeight * 16f, textSize, 255, 255, 255, 255, false);
            DrawText(zoomAdjust2, rightMargin, startY + lineHeight * 17f, textSize, 255, 255, 255, 255, false);
            DrawText($"{KeyBinds.UI_ZoomModeToggle} = Toggle Zoom Mode", rightMargin, startY + lineHeight * 18f, textSize, 255, 255, 255, 255, false);
            DrawText($"{KeyBinds.UI_ZoomReset} = Reset Zoom", rightMargin, startY + lineHeight * 19f, textSize, 255, 255, 255, 255, false);
            DrawText($"{KeyBinds.UI_ZoomInLock} = Lock Zoom IN at Current FOV", rightMargin, startY + lineHeight * 20f, textSize, 100, 255, 100, 255, false);
            DrawText($"{KeyBinds.UI_ZoomOutLock} = Lock Zoom OUT at Current FOV", rightMargin, startY + lineHeight * 21f, textSize, 100, 255, 100, 255, false);
            
            DrawText("--- Viewfinder Controls ---", rightMargin, startY + lineHeight * 22.5f, headerSize, 255, 255, 100, 255, false);
            DrawText($"{KeyBinds.UI_ViewfinderZoomInOut} = Viewfinder Zoom In/Out", rightMargin, startY + lineHeight * 23.5f, textSize, 255, 255, 255, 255, false);
            DrawText($"{KeyBinds.UI_ChangeAspectRatio} = Change Aspect Ratio", rightMargin, startY + lineHeight * 24.5f, textSize, 255, 255, 255, 255, false);
            DrawText($"{KeyBinds.UI_ToggleGrid} = Toggle Rule of Thirds Grid", rightMargin, startY + lineHeight * 25.5f, textSize, 255, 255, 255, 255, false);
        }

        private void DrawText(string text, float x, float y, float scale, int r, int g, int b, int a, bool center)
        {
            Function.Call(Hash.SET_TEXT_FONT, 0);
            Function.Call(Hash.SET_TEXT_SCALE, scale, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, r, g, b, a);
            Function.Call(Hash.SET_TEXT_CENTRE, center);
            Function.Call(Hash.SET_TEXT_DROPSHADOW, 2, 0, 0, 0, 255);

            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y);
        }
    }
}