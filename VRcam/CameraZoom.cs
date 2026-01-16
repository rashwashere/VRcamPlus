using System;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

namespace VRCam
{
    public class CameraZoom : Script
    {
        // Camera zoom variables
        private float targetFov = 68f; // Default FOV - 68 as initial
        private float currentFov = 68f; // Current FOV for smoothing
        private float defaultFov = 68f; // Reset value
        private float minFov = 10f; // Max zoom in
        private float maxFov = 90f; // Max zoom out
        private float zoomInSmoothness = 0.15f; // Smoothness when zooming IN (decreasing FOV)
        private float zoomOutSmoothness = 0.15f; // Smoothness when zooming OUT (increasing FOV)
        private float zoomStep = 2f; // How much each scroll changes zoom
        
        // GUI control - synced with viewfinder (starts hidden)
        private bool showGUI = false;
        public static bool ZoomEnabled = true; // Static so AttachRatToCamera can lock/unlock zoom
        
        // Mouse wheel tracking
        private int lastMouseWheel = 0;
        
        // Control mode: false = mouse wheel, true = left/right click
        private bool useClickMode = false;
        public static bool UseClickMode { get; private set; } = false; // Public static for GUI access

        public CameraZoom()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            
            // Get initial FOV
            Camera initCam = World.RenderingCamera;
            if (initCam != null)
            {
                currentFov = initCam.FieldOfView;
                targetFov = currentFov;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Check if any modifier key is pressed
            bool modifierPressed = e.Control || e.Shift || e.Alt;

            // L key toggles GUI visibility (synced with viewfinder and rat GUI) (no modifiers)
            if (e.KeyCode == Keys.L && !modifierPressed)
            {
                showGUI = !showGUI;
            }
            // U key also toggles GUI (for consistency with AttachRatToCamera) (no modifiers)
            else if (e.KeyCode == Keys.U && !modifierPressed)
            {
                showGUI = !showGUI;
            }
            // Ctrl+CapsLock toggles zoom control mode (only when zoom is enabled, only Ctrl)
            else if (e.Control && !e.Shift && !e.Alt && e.KeyCode == Keys.CapsLock && ZoomEnabled)
            {
                useClickMode = !useClickMode;
                UseClickMode = useClickMode; // Update public static property
                string mode = useClickMode ? "Left/Right Click" : "Mouse Wheel";
                GTA.UI.Screen.ShowSubtitle($"Zoom Mode: {mode}", 2000);
            }

        }

        private void OnTick(object sender, EventArgs e)
        {
            // Only process zoom if enabled (not locked by F10)
            if (!ZoomEnabled)
                return;
            
            // Check for Ctrl+MiddleMouse reset
            if (Game.IsKeyPressed(Keys.ControlKey) && Game.IsControlPressed(GTA.Control.LookBehind))
            {
                targetFov = defaultFov;
                currentFov = defaultFov;
            }
            
            int scrollDelta = 0;
            
            if (useClickMode)
            {
                // Use left/right click for zoom - CONTINUOUS (hold to zoom)
                bool ctrlPressed = Game.IsKeyPressed(Keys.ControlKey);
                bool shiftPressed = Game.IsKeyPressed(Keys.ShiftKey);
                
                if (Game.IsControlPressed(GTA.Control.Attack))
                {
                    if (shiftPressed && !ctrlPressed)
                    {
                        // Shift + LMB: Increase zoom IN smoothness (gradual continuous)
                        zoomInSmoothness += 0.001f;
                        zoomInSmoothness = Math.Max(0.01f, Math.Min(1.0f, zoomInSmoothness));
                    }
                    else if (!shiftPressed && !ctrlPressed)
                    {
                        // Left click held = continuously zoom in
                        targetFov -= zoomStep * 0.5f;
                        targetFov = Math.Max(minFov, Math.Min(maxFov, targetFov));
                    }
                }
                else if (Game.IsControlPressed(GTA.Control.Aim))
                {
                    if (shiftPressed && !ctrlPressed)
                    {
                        // Shift + RMB: Decrease zoom IN smoothness (gradual continuous)
                        zoomInSmoothness -= 0.001f;
                        zoomInSmoothness = Math.Max(0.01f, Math.Min(1.0f, zoomInSmoothness));
                    }
                    else if (ctrlPressed && !shiftPressed)
                    {
                        // Ctrl + RMB: Decrease zoom OUT smoothness (gradual continuous)
                        zoomOutSmoothness -= 0.001f;
                        zoomOutSmoothness = Math.Max(0.01f, Math.Min(1.0f, zoomOutSmoothness));
                    }
                    else if (!shiftPressed && !ctrlPressed)
                    {
                        // Right click held = continuously zoom out
                        targetFov += zoomStep * 0.5f;
                        targetFov = Math.Max(minFov, Math.Min(maxFov, targetFov));
                    }
                }
                
                // Add Ctrl+LMB for increasing zoom OUT smoothness
                if (Game.IsControlPressed(GTA.Control.Attack) && ctrlPressed && !shiftPressed)
                {
                    // Ctrl + LMB: Increase zoom OUT smoothness (gradual continuous)
                    zoomOutSmoothness += 0.001f;
                    zoomOutSmoothness = Math.Max(0.01f, Math.Min(1.0f, zoomOutSmoothness));
                }
            }
            else
            {
                // Use mouse wheel for zoom - DISCRETE (per scroll)
                scrollDelta = Game.IsControlPressed(GTA.Control.CursorScrollUp) ? 1 : 
                             Game.IsControlPressed(GTA.Control.CursorScrollDown) ? -1 : 0;

                if (scrollDelta != 0 && scrollDelta != lastMouseWheel)
                {
                    bool shiftPressed = Game.IsKeyPressed(Keys.ShiftKey);
                    bool ctrlPressed = Game.IsKeyPressed(Keys.ControlKey);

                    if (shiftPressed && !ctrlPressed)
                    {
                        // Shift + Scroll: Adjust zoom IN smoothness
                        zoomInSmoothness += scrollDelta * 0.02f;
                        zoomInSmoothness = Math.Max(0.01f, Math.Min(1.0f, zoomInSmoothness));
                        GTA.UI.Screen.ShowSubtitle($"Zoom IN Smoothness: {zoomInSmoothness:F2}", 1000);
                    }
                    else if (ctrlPressed && !shiftPressed)
                    {
                        // Ctrl + Scroll: Adjust zoom OUT smoothness
                        zoomOutSmoothness += scrollDelta * 0.02f;
                        zoomOutSmoothness = Math.Max(0.01f, Math.Min(1.0f, zoomOutSmoothness));
                        GTA.UI.Screen.ShowSubtitle($"Zoom OUT Smoothness: {zoomOutSmoothness:F2}", 1000);
                    }
                    else if (!shiftPressed && !ctrlPressed)
                    {
                        // Normal scroll: Adjust zoom
                        targetFov -= scrollDelta * zoomStep;
                        targetFov = Math.Max(minFov, Math.Min(maxFov, targetFov));
                    }
                }
                lastMouseWheel = scrollDelta;
            }

            // Apply smooth zoom to camera using appropriate smoothness
            Camera cam = World.RenderingCamera;
            if (cam != null)
            {
                // Determine if we're zooming in or out
                bool zoomingIn = targetFov < currentFov;
                float activeSmoothness = zoomingIn ? zoomInSmoothness : zoomOutSmoothness;
                
                // Smooth interpolation
                currentFov = Lerp(currentFov, targetFov, activeSmoothness);
                cam.FieldOfView = currentFov;
            }

            // Display GUI if enabled
            if (showGUI)
            {
                DisplayZoomInfo();
            }
        }

        private float Lerp(float start, float end, float amount)
        {
            return start + (end - start) * amount;
        }

        private void DisplayZoomInfo()
        {
            // Calculate zoom percentage (inverted - lower FOV = more zoom)
            float zoomPercent = ((maxFov - currentFov) / (maxFov - minFov)) * 100f;
            
            // Add camera mode indicator
            string cameraMode = useClickMode ? "Click" : "Scroll";
            
            string info = string.Format("Zoom: {0:F0}%  FOV: {1:F1}  IN: {2:F2}  OUT: {3:F2}  Mode: {4}",
                                       zoomPercent, currentFov, zoomInSmoothness, zoomOutSmoothness, cameraMode);

            // Draw at top center
            float textPosX = 0.5f;
            float textPosY = 0.02f;
            float textScale = 0.35f;

            Function.Call(Hash.SET_TEXT_FONT, 0);
            Function.Call(Hash.SET_TEXT_SCALE, textScale, textScale);
            Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 153, 255); // Yellow color like aspect ratio warning
            Function.Call(Hash.SET_TEXT_CENTRE, true);

            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, info);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, textPosX, textPosY);
        }
    }
}