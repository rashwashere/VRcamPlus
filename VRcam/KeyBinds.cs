using System.Windows.Forms;
using GTA;

namespace VRCam
{
    /// <summary>
    /// Centralized keybind configuration for all VRCam scripts.
    /// 
    /// HOW TO USE:
    /// 1. Edit the keybinds in the "USER SETTINGS" section below
    /// 2. Don't touch anything in the "INTERNAL CODE" section (that's automatic)
    /// 3. Save and reload - all UI and controls will update automatically!
    /// </summary>
    public static class KeyBinds
    {
        // ====================================================================
        // USER SETTINGS - EDIT THESE TO CHANGE YOUR KEYBINDS
        // ====================================================================
        
        #region MAIN CONTROLS
        public static Keys ToggleUI = Keys.L;
        public static Keys ShowHideRat = Keys.F5;
        public static Keys LockUnlockControls = Keys.F10;
        public static Keys SavePreset = Keys.F10;
        public static Keys CyclePresetNext = Keys.Oemplus;  // The + key
        public static Keys CyclePresetPrev = Keys.OemMinus; // The - key
        #endregion

        #region MAIN CONTROLS - MODIFIERS (Which keys need Ctrl held down?)
        public static bool SavePreset_Ctrl = true;       // Ctrl+F10 to save
        public static bool CyclePresetNext_Ctrl = true;  // Ctrl++ to cycle
        public static bool CyclePresetPrev_Ctrl = true;  // Ctrl+- to cycle
        #endregion

        #region FLASHLIGHT CONTROLS
        public static Keys ToggleFlashlight = Keys.CapsLock;
        public static Keys FlashlightLock = Keys.D3;
        public static Keys FlashlightMoveForward = Keys.Up;
        public static Keys FlashlightMoveBack = Keys.Down;
        public static Keys FlashlightMoveLeft = Keys.Left;
        public static Keys FlashlightMoveRight = Keys.Right;
        public static Keys FlashlightMoveUp = Keys.PageUp;
        public static Keys FlashlightMoveDown = Keys.PageDown;
        public static Keys FlashlightRotateUp = Keys.Up;
        public static Keys FlashlightRotateDown = Keys.Down;
        #endregion

        #region FLASHLIGHT CONTROLS - MODIFIERS
        public static bool FlashlightLock_Ctrl = true;    // Ctrl+3 to lock/unlock
        public static bool FlashlightRotate_Ctrl = true;  // Ctrl+Up/Down to rotate
        #endregion

        #region ZOOM CONTROLS - MOUSE/SCROLL BUTTONS
        // CLICK MODE - Which mouse buttons zoom in/out?
        public static GTA.Control ZoomInButton = GTA.Control.Attack;   // Left Mouse Button
        public static GTA.Control ZoomOutButton = GTA.Control.Aim;     // Right Mouse Button
        public static string ZoomInButtonName = "LMB";         // What shows in UI
        public static string ZoomOutButtonName = "RMB";        // What shows in UI
        
        // SCROLL MODE - Which scroll direction zooms in/out?
        public static GTA.Control ZoomInScroll = GTA.Control.CursorScrollUp;
        public static GTA.Control ZoomOutScroll = GTA.Control.CursorScrollDown;
        public static string ZoomScrollName = "Mouse Wheel";   // What shows in UI
        #endregion

        #region ZOOM CONTROLS - KEYBOARD KEYS
        public static Keys ZoomModeToggle = Keys.CapsLock;
        public static Keys ZoomInLock = Keys.D1;
        public static Keys ZoomOutLock = Keys.D2;
        #endregion

        #region ZOOM CONTROLS - MODIFIERS
        public static bool ZoomModeToggle_Ctrl = true;    // Ctrl+Caps to toggle mode
        public static bool ZoomInLock_Ctrl = true;        // Ctrl+1 to lock zoom in
        public static bool ZoomOutLock_Ctrl = true;       // Ctrl+2 to lock zoom out
        public static bool ZoomInSpeedAdjust_Shift = true;  // Shift+Button/Wheel adjusts IN speed
        public static bool ZoomOutSpeedAdjust_Ctrl = true; // Ctrl+Button/Wheel adjusts OUT speed
        #endregion

        #region VIEWFINDER CONTROLS
        public static Keys ViewfinderZoomIn = Keys.J;
        public static Keys ViewfinderZoomOut = Keys.K;
        public static Keys ChangeAspectRatio = Keys.H;
        public static Keys ToggleGrid = Keys.U;
        #endregion


        // ====================================================================
        // INTERNAL CODE - DON'T EDIT BELOW THIS LINE
        // (This is automatic code that makes everything work)
        // ====================================================================
        
        #region INTERNAL - UI Display Properties (Auto-Generated)
        public static string UI_ToggleUI => FormatKey(false, ToggleUI);
        public static string UI_ShowHideRat => FormatKey(false, ShowHideRat);
        public static string UI_LockUnlockControls => FormatKey(false, LockUnlockControls);
        public static string UI_SavePreset => FormatKey(SavePreset_Ctrl, SavePreset);
        public static string UI_CyclePresets => $"{FormatKey(CyclePresetNext_Ctrl, CyclePresetNext)}/{FormatKey(CyclePresetPrev_Ctrl, CyclePresetPrev).Replace("Ctrl+", "")}";
        
        public static string UI_ToggleFlashlight => FormatKey(false, ToggleFlashlight);
        public static string UI_FlashlightLock => FormatKey(FlashlightLock_Ctrl, FlashlightLock);
        public static string UI_FlashlightMove => $"{FormatKey(false, FlashlightMoveForward)}/{FormatKey(false, FlashlightMoveBack)}";
        public static string UI_FlashlightMoveLeftRight => $"{FormatKey(false, FlashlightMoveLeft)}/{FormatKey(false, FlashlightMoveRight)}";
        public static string UI_FlashlightMoveUpDown => $"{FormatKey(false, FlashlightMoveUp)}/{FormatKey(false, FlashlightMoveDown)}";
        public static string UI_FlashlightRotate => $"{FormatKey(FlashlightRotate_Ctrl, FlashlightRotateUp)}/{FormatKey(FlashlightRotate_Ctrl, FlashlightRotateDown).Replace("Ctrl+", "")}";
        
        public static string UI_ZoomModeToggle => FormatKey(ZoomModeToggle_Ctrl, ZoomModeToggle);
        public static string UI_ZoomReset => "Ctrl+MiddleMouse";
        public static string UI_ZoomInLock => FormatKey(ZoomInLock_Ctrl, ZoomInLock);
        public static string UI_ZoomOutLock => FormatKey(ZoomOutLock_Ctrl, ZoomOutLock);
        
        public static string UI_ViewfinderZoomInOut => $"{FormatKey(false, ViewfinderZoomIn)}/{FormatKey(false, ViewfinderZoomOut)}";
        public static string UI_ChangeAspectRatio => FormatKey(false, ChangeAspectRatio);
        public static string UI_ToggleGrid => FormatKey(false, ToggleGrid);
        #endregion

        #region INTERNAL - Dynamic Zoom Text Generation
        public static string GetZoomModeName(bool isClickMode)
        {
            return isClickMode ? $"{ZoomInButtonName}/{ZoomOutButtonName}" : ZoomScrollName;
        }

        public static string GetZoomInSpeedAdjustText(bool isClickMode)
        {
            if (isClickMode)
            {
                string modifier = ZoomInSpeedAdjust_Shift ? "Shift+" : "";
                return $"{modifier}{ZoomInButtonName}/{ZoomOutButtonName} = Zoom IN +/-";
            }
            else
            {
                string modifier = ZoomInSpeedAdjust_Shift ? "Shift+" : "";
                return $"{modifier}Wheel = Adjust Zoom IN Speed";
            }
        }

        public static string GetZoomOutSpeedAdjustText(bool isClickMode)
        {
            if (isClickMode)
            {
                string modifier = ZoomOutSpeedAdjust_Ctrl ? "Ctrl+" : "";
                return $"{modifier}{ZoomInButtonName}/{ZoomOutButtonName} = Zoom OUT +/-";
            }
            else
            {
                string modifier = ZoomOutSpeedAdjust_Ctrl ? "Ctrl+" : "";
                return $"{modifier}Wheel = Adjust Zoom OUT Speed";
            }
        }
        #endregion

        #region INTERNAL - Key Name Formatting
        private static string FormatKey(bool needsCtrl, Keys key)
        {
            string result = needsCtrl ? "Ctrl+" : "";
            
            switch (key)
            {
                case Keys.D0:           result += "0"; break;
                case Keys.D1:           result += "1"; break;
                case Keys.D2:           result += "2"; break;
                case Keys.D3:           result += "3"; break;
                case Keys.D4:           result += "4"; break;
                case Keys.D5:           result += "5"; break;
                case Keys.D6:           result += "6"; break;
                case Keys.D7:           result += "7"; break;
                case Keys.D8:           result += "8"; break;
                case Keys.D9:           result += "9"; break;
                case Keys.Oemplus:      result += "+"; break;
                case Keys.OemMinus:     result += "-"; break;
                case Keys.CapsLock:     result += "Caps"; break;
                case Keys.PageUp:       result += "PgUp"; break;
                case Keys.PageDown:     result += "PgDn"; break;
                case Keys.Up:           result += "Up"; break;
                case Keys.Down:         result += "Down"; break;
                case Keys.Left:         result += "Left"; break;
                case Keys.Right:        result += "Right"; break;
                default:                result += key.ToString(); break;
            }
            
            return result;
        }
        #endregion
    }
}