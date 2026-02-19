using System;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

namespace VRCam
{
    public class CameraZoom : Script
    {
        private float targetFov = 68f;
        private float currentFov = 68f;
        private float defaultFov = 68f;
        private float minFov = 10f;
        private float maxFov = 90f;
        private float zoomInSmoothness = 0.15f;
        private float zoomOutSmoothness = 0.15f;
        private float zoomStep = 2f;
        
        public static bool ZoomEnabled = true;
        
        private int lastMouseWheel = 0;
        
        private bool useClickMode = false;
        public static bool UseClickMode { get; private set; } = false;

        // Soft-lock settings
        private bool zoomInLocked = false;
        private bool zoomOutLocked = false;
        private float softMinFov = 45f;
        private float softMaxFov = 60f;

        // Public properties for ViewfinderText to access
        public static float CurrentFOV { get; private set; } = 68f;
        public static float ZoomInSpeed { get; private set; } = 0.15f;
        public static float ZoomOutSpeed { get; private set; } = 0.15f;
        public static float MinFOV { get; private set; } = 10f;
        public static float MaxFOV { get; private set; } = 90f;
        public static bool ZoomInLocked { get; private set; } = false;
        public static bool ZoomOutLocked { get; private set; } = false;
        public static float SoftMinFOV { get; private set; } = 45f;
        public static float SoftMaxFOV { get; private set; } = 60f;

        public CameraZoom()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            
            // Load from configuration
            currentFov = VRCamModConfiguration.CurrentFOV;
            targetFov = currentFov;
            zoomInSmoothness = VRCamModConfiguration.ZoomInSmoothness;
            zoomOutSmoothness = VRCamModConfiguration.ZoomOutSmoothness;
            useClickMode = VRCamModConfiguration.UseClickMode;
            zoomInLocked = VRCamModConfiguration.ZoomInLocked;
            zoomOutLocked = VRCamModConfiguration.ZoomOutLocked;
            softMinFov = VRCamModConfiguration.SoftMinFOV;
            softMaxFov = VRCamModConfiguration.SoftMaxFOV;
            
            UseClickMode = useClickMode;
            CurrentFOV = currentFov;
            ZoomInSpeed = zoomInSmoothness;
            ZoomOutSpeed = zoomOutSmoothness;
            MinFOV = minFov;
            MaxFOV = maxFov;
            ZoomInLocked = zoomInLocked;
            ZoomOutLocked = zoomOutLocked;
            SoftMinFOV = softMinFov;
            SoftMaxFOV = softMaxFov;
        }

        // Set to true by RefreshFromConfig() so OnTick re-reads smoothness next frame
        private static bool pendingRefresh = false;

        /// <summary>
        /// Called after a preset is loaded to push the new smoothness/lock values into the live instance.
        /// </summary>
        public static void RefreshFromConfig()
        {
            pendingRefresh = true;
        }

        private void SaveToConfig()
        {
            VRCamModConfiguration.CurrentFOV = currentFov;
            VRCamModConfiguration.ZoomInSmoothness = zoomInSmoothness;
            VRCamModConfiguration.ZoomOutSmoothness = zoomOutSmoothness;
            VRCamModConfiguration.UseClickMode = useClickMode;
            VRCamModConfiguration.ZoomInLocked = zoomInLocked;
            VRCamModConfiguration.ZoomOutLocked = zoomOutLocked;
            VRCamModConfiguration.SoftMinFOV = softMinFov;
            VRCamModConfiguration.SoftMaxFOV = softMaxFov;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!ZoomEnabled) return;

            // Ctrl+CapsLock = toggle zoom mode (click vs scroll)
            if (e.Control && !e.Shift && !e.Alt && e.KeyCode == KeyBinds.ZoomModeToggle && KeyBinds.ZoomModeToggle_Ctrl)
            {
                useClickMode = !useClickMode;
                UseClickMode = useClickMode;
                SaveToConfig();
                string mode = useClickMode ? "Left/Right Click" : "Mouse Wheel";
                GTA.UI.Screen.ShowSubtitle($"Zoom Mode: {mode}", 2000);
            }
            // Ctrl+1 = lock/unlock zoom IN at current FOV
            else if (e.Control && !e.Shift && !e.Alt && e.KeyCode == KeyBinds.ZoomInLock && KeyBinds.ZoomInLock_Ctrl)
            {
                if (!zoomInLocked)
                {
                    softMinFov = currentFov;
                    SoftMinFOV = softMinFov;
                }
                zoomInLocked = !zoomInLocked;
                ZoomInLocked = zoomInLocked;
                SaveToConfig();
                string status = zoomInLocked ? "LOCKED" : "UNLOCKED";
                int minMm = VRCamModConfiguration.FOVToMillimeter(softMinFov);
                GTA.UI.Screen.ShowSubtitle($"Zoom IN {status} at {softMinFov:F1}° ({minMm}mm)", 2000);
            }
            // Ctrl+2 = lock/unlock zoom OUT at current FOV
            else if (e.Control && !e.Shift && !e.Alt && e.KeyCode == KeyBinds.ZoomOutLock && KeyBinds.ZoomOutLock_Ctrl)
            {
                if (!zoomOutLocked)
                {
                    softMaxFov = currentFov;
                    SoftMaxFOV = softMaxFov;
                }
                zoomOutLocked = !zoomOutLocked;
                ZoomOutLocked = zoomOutLocked;
                SaveToConfig();
                string status = zoomOutLocked ? "LOCKED" : "UNLOCKED";
                int maxMm = VRCamModConfiguration.FOVToMillimeter(softMaxFov);
                GTA.UI.Screen.ShowSubtitle($"Zoom OUT {status} at {softMaxFov:F1}° ({maxMm}mm)", 2000);
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!ZoomEnabled)
                return;

            // A preset was just loaded — pull the new values into our live fields
            if (pendingRefresh)
            {
                pendingRefresh = false;
                zoomInSmoothness = VRCamModConfiguration.ZoomInSmoothness;
                zoomOutSmoothness = VRCamModConfiguration.ZoomOutSmoothness;
                targetFov = VRCamModConfiguration.CurrentFOV;
                useClickMode = VRCamModConfiguration.UseClickMode;
                zoomInLocked = VRCamModConfiguration.ZoomInLocked;
                zoomOutLocked = VRCamModConfiguration.ZoomOutLocked;
                softMinFov = VRCamModConfiguration.SoftMinFOV;
                softMaxFov = VRCamModConfiguration.SoftMaxFOV;
                UseClickMode = useClickMode;
                ZoomInSpeed = zoomInSmoothness;
                ZoomOutSpeed = zoomOutSmoothness;
                ZoomInLocked = zoomInLocked;
                ZoomOutLocked = zoomOutLocked;
                SoftMinFOV = softMinFov;
                SoftMaxFOV = softMaxFov;
            }
            
            // Ctrl+Middle Mouse = reset zoom to default
            if (Game.IsKeyPressed(Keys.ControlKey) && Game.IsControlPressed(GTA.Control.LookBehind))
            {
                targetFov = defaultFov;
                currentFov = defaultFov;
            }
            
            int scrollDelta = 0;
            
            if (useClickMode)
            {
                bool ctrlPressed = Game.IsKeyPressed(Keys.ControlKey);
                bool shiftPressed = Game.IsKeyPressed(Keys.ShiftKey);
                
                // Disable in-game attack and aim controls
                Game.DisableControlThisFrame(GTA.Control.Attack);
                Game.DisableControlThisFrame(GTA.Control.Aim);
                Game.DisableControlThisFrame(GTA.Control.MeleeAttackLight);
                Game.DisableControlThisFrame(GTA.Control.MeleeAttackHeavy);
                Game.DisableControlThisFrame(GTA.Control.MeleeAttackAlternate);
                
                if (Game.IsControlPressed(GTA.Control.Attack)) // LMB
                {
                    if (shiftPressed && !ctrlPressed)
                    {
                        // Shift+LMB = zoom IN speed up
                        zoomInSmoothness += 0.001f;
                        zoomInSmoothness = Math.Max(0.01f, Math.Min(1.0f, zoomInSmoothness));
                        ZoomInSpeed = zoomInSmoothness;
                    }
                    else if (ctrlPressed && !shiftPressed)
                    {
                        // Ctrl+LMB = zoom OUT speed down
                        zoomOutSmoothness -= 0.001f;
                        zoomOutSmoothness = Math.Max(0.01f, Math.Min(1.0f, zoomOutSmoothness));
                        ZoomOutSpeed = zoomOutSmoothness;
                    }
                    else if (!shiftPressed && !ctrlPressed)
                    {
                        // LMB = zoom in
                        targetFov -= zoomStep * 0.5f;
                        targetFov = Math.Max(minFov, Math.Min(maxFov, targetFov));
                    }
                }
                else if (Game.IsControlPressed(GTA.Control.Aim)) // RMB
                {
                    if (shiftPressed && !ctrlPressed)
                    {
                        // Shift+RMB = zoom IN speed down
                        zoomInSmoothness -= 0.001f;
                        zoomInSmoothness = Math.Max(0.01f, Math.Min(1.0f, zoomInSmoothness));
                        ZoomInSpeed = zoomInSmoothness;
                    }
                    else if (ctrlPressed && !shiftPressed)
                    {
                        // Ctrl+RMB = zoom OUT speed up
                        zoomOutSmoothness += 0.001f;
                        zoomOutSmoothness = Math.Max(0.01f, Math.Min(1.0f, zoomOutSmoothness));
                        ZoomOutSpeed = zoomOutSmoothness;
                    }
                    else if (!shiftPressed && !ctrlPressed)
                    {
                        // RMB = zoom out
                        targetFov += zoomStep * 0.5f;
                        targetFov = Math.Max(minFov, Math.Min(maxFov, targetFov));
                    }
                }
            }
            else
            {
                // Disable weapon wheel when using scroll wheel for zoom
                Game.DisableControlThisFrame(GTA.Control.WeaponWheelNext);
                Game.DisableControlThisFrame(GTA.Control.WeaponWheelPrev);
                
                scrollDelta = Game.IsControlPressed(GTA.Control.CursorScrollUp) ? 1 : 
                             Game.IsControlPressed(GTA.Control.CursorScrollDown) ? -1 : 0;

                if (scrollDelta != 0 && scrollDelta != lastMouseWheel)
                {
                    bool shiftPressed = Game.IsKeyPressed(Keys.ShiftKey);
                    bool ctrlPressed = Game.IsKeyPressed(Keys.ControlKey);

                    if (shiftPressed && !ctrlPressed)
                    {
                        // Shift+Scroll = adjust zoom IN speed
                        zoomInSmoothness += scrollDelta * 0.02f;
                        zoomInSmoothness = Math.Max(0.01f, Math.Min(1.0f, zoomInSmoothness));
                        ZoomInSpeed = zoomInSmoothness;
                        GTA.UI.Screen.ShowSubtitle($"Zoom IN Smoothness: {zoomInSmoothness:F2}", 1000);
                    }
                    else if (ctrlPressed && !shiftPressed)
                    {
                        // Ctrl+Scroll = adjust zoom OUT speed
                        zoomOutSmoothness += scrollDelta * 0.02f;
                        zoomOutSmoothness = Math.Max(0.01f, Math.Min(1.0f, zoomOutSmoothness));
                        ZoomOutSpeed = zoomOutSmoothness;
                        GTA.UI.Screen.ShowSubtitle($"Zoom OUT Smoothness: {zoomOutSmoothness:F2}", 1000);
                    }
                    else if (!shiftPressed && !ctrlPressed)
                    {
                        // Scroll = zoom
                        targetFov -= scrollDelta * zoomStep;
                        targetFov = Math.Max(minFov, Math.Min(maxFov, targetFov));
                    }
                }
                lastMouseWheel = scrollDelta;
            }

            // Apply soft-lock limits
            if (zoomInLocked && targetFov < softMinFov)
                targetFov = softMinFov;
            if (zoomOutLocked && targetFov > softMaxFov)
                targetFov = softMaxFov;

            Camera cam = World.RenderingCamera;
            if (cam != null)
            {
                bool zoomingIn = targetFov < currentFov;
                float activeSmoothness = zoomingIn ? zoomInSmoothness : zoomOutSmoothness;
                
                currentFov = Lerp(currentFov, targetFov, activeSmoothness);
                cam.FieldOfView = currentFov;
                CurrentFOV = currentFov;
                
                SaveToConfig();
            }
        }

        private float Lerp(float start, float end, float amount)
        {
            return start + (end - start) * amount;
        }
    }
}