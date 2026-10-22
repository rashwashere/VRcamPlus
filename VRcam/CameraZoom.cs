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
        private float minFov = EngineMinFov;   // Zoom IN Range
        private float maxFov = 90f;            // Zoom OUT Range
        private float zoomInSmoothness = 0.15f;
        private float zoomOutSmoothness = 0.15f;
        private float zoomStep = 2f;
        
        public static bool ZoomEnabled = true;
        
        private int lastMouseWheel = 0;
        
        private bool useClickMode = false;
        public static bool UseClickMode { get; private set; } = false;

        // 0 = mouse wheel, 1 = LMB/RMB click, 2 = mouse slide (push the mouse
        // forward/back; its travel is a 0..100% zoom slider between the limits)
        public static int ZoomMode { get; private set; } = 0;
        public static float SlideSensitivity { get; private set; } = 0.35f;
        public static float SlidePercent { get; private set; } = 0f;
        private int zoomMode = 0;
        private float slideSensitivity = 0.35f;
        private float slider = 0f;          // 0 = fully zoomed out, 1 = fully zoomed in
        private static int? menuZoomMode = null;
        private static float? menuSlideSensitivity = null;
        public static void Menu_SetZoomMode(int v) { menuZoomMode = v; }
        public static void Menu_SetSlideSensitivity(float v) { menuSlideSensitivity = v; }

        // Soft-lock settings
        private bool zoomInLocked = false;
        private bool zoomOutLocked = false;
        private float softMinFov = 45f;
        private float softMaxFov = 60f;

        // Public properties for ViewfinderText to access
        public static float CurrentFOV { get; private set; } = 68f;
        public static float TargetFOV { get; private set; } = 68f;
        public static float ZoomInSpeed { get; private set; } = 0.15f;
        public static float ZoomOutSpeed { get; private set; } = 0.15f;
        public static float MinFOV { get; private set; } = EngineMinFov;
        public static float MaxFOV { get; private set; } = 90f;
        public static bool ZoomInLocked { get; private set; } = false;
        public static bool ZoomOutLocked { get; private set; } = false;
        public static float SoftMinFOV { get; private set; } = 45f;
        public static float SoftMaxFOV { get; private set; } = 60f;

        // --- pending changes requested by the menu, consumed in OnTick ---
        private static float? menuFov = null;
        private static float? menuZoomInSmooth = null;
        private static float? menuZoomOutSmooth = null;
        private static bool? menuClickMode = null;
        private static bool? menuZoomInLock = null;
        private static bool? menuZoomOutLock = null;
        private static float? menuSoftMin = null;
        private static float? menuSoftMax = null;

        public static void Menu_SetFov(float v) { menuFov = v; }
        public static void Menu_SetZoomInSmooth(float v) { menuZoomInSmooth = v; }
        public static void Menu_SetZoomOutSmooth(float v) { menuZoomOutSmooth = v; }
        public static void Menu_SetClickMode(bool v) { menuClickMode = v; }
        public static void Menu_SetZoomInLock(bool v) { menuZoomInLock = v; }
        public static void Menu_SetZoomOutLock(bool v) { menuZoomOutLock = v; }
        public static void Menu_SetSoftMin(float v) { menuSoftMin = v; }
        public static void Menu_SetSoftMax(float v) { menuSoftMax = v; }
        private static float? menuRangeMin = null;
        private static float? menuRangeMax = null;
        public static void Menu_SetRangeMin(float v) { menuRangeMin = v; }
        public static void Menu_SetRangeMax(float v) { menuRangeMax = v; }

        // ---- deep zoom ----
        // The game takes a camera FOV from 1 to 130 degrees. Every zoom control works on a "zoom coordinate" u:
        // u = FOV at or above 10 degrees (linear in degrees there), and below 10 each bit of travel zooms by the same
        // RATIO (u = 10 + 10 ln(FOV/10), same value and slope at 10), so a shot at 2-4 degrees can still be framed finely.
        public const float EngineMinFov = 1f, EngineMaxFov = 130f;
        private const float KneeFov = 10f;
        public static float ZoomU(float fov)
        {
            return fov >= KneeFov ? fov : KneeFov + KneeFov * (float)Math.Log(Math.Max(EngineMinFov, fov) / KneeFov);
        }
        public static float FovFromU(float u)
        {
            return u >= KneeFov ? u : KneeFov * (float)Math.Exp((u - KneeFov) / KneeFov);
        }
        // a zoom step given in FOV degrees (exact at or above 10 degrees, proportional below)
        private static float StepFov(float fov, float deltaFov) { return FovFromU(ZoomU(fov) + deltaFov); }
        public static float SliderToFov(float s, float outFov, float inFov)
        {
            return FovFromU(ZoomU(outFov) + (ZoomU(inFov) - ZoomU(outFov)) * s);
        }
        public static float FovToSlider(float fov, float outFov, float inFov)
        {
            float span = ZoomU(inFov) - ZoomU(outFov);
            return Math.Abs(span) < 0.01f ? 0f : Math.Max(0f, Math.Min(1f, (ZoomU(fov) - ZoomU(outFov)) / span));
        }
        // Mouse Slide: mouse travel per degree above 10 is the same as for a range ending at 10; the deep part
        // below 10 is extra travel past it
        public static float SlideScale(float outFov, float inFov)
        {
            float oldSpan = Math.Max(outFov, KneeFov) - Math.Max(inFov, KneeFov);
            float newSpan = ZoomU(outFov) - ZoomU(inFov);
            if (newSpan < 0.01f) return 1f;
            oldSpan = Math.Max(oldSpan, Math.Min(newSpan, 10f));   // no jump for a range lying mostly below 10
            return oldSpan / newSpan;
        }
        // the slider ends in use (soft locks kept inside the zoom range; the lock rows span 1-130)
        private float OutEnd() { return Math.Max(minFov, Math.Min(maxFov, zoomOutLocked ? softMaxFov : maxFov)); }
        private float InEnd() { return Math.Max(minFov, Math.Min(maxFov, zoomInLocked ? softMinFov : minFov)); }
        // Stick Direct and the trigger-pressure modes: a position IS the zoom, so the same travel can't reach 1 degree
        // and keep its feel; they end at 10 degrees (the rate modes, wheel, click and Mouse Slide go deep)
        private float PadInEnd() { return Math.Max(InEnd(), Math.Min(KneeFov, maxFov)); }

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
            zoomMode = VRCamModConfiguration.ZoomMode;
            slideSensitivity = VRCamModConfiguration.SlideSensitivity;
            ZoomMode = zoomMode; SlideSensitivity = slideSensitivity;
            zoomInLocked = VRCamModConfiguration.ZoomInLocked;
            zoomOutLocked = VRCamModConfiguration.ZoomOutLocked;
            softMinFov = VRCamModConfiguration.SoftMinFOV;
            softMaxFov = VRCamModConfiguration.SoftMaxFOV;
            minFov = Math.Max(EngineMinFov, Math.Min(KneeFov, VRCamModConfiguration.ZoomRangeMinFOV));
            maxFov = Math.Max(60f, Math.Min(EngineMaxFov, VRCamModConfiguration.ZoomRangeMaxFOV));
            currentFov = Math.Max(minFov, Math.Min(maxFov, currentFov));
            targetFov = currentFov;

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
            if (VRCamMenu.SuppressHotkeys) return;
            if (!ZoomEnabled) return;
            if (!AttachRatToCamera.RatActive) return; // no zoom binds before VRCam is started

            // Zoom mode key: cycle wheel / click / slide
            if (KeyBinds.ZoomModeToggle.Pressed(e))
            {
                SetZoomMode((zoomMode + 1) % 3);
                string mode = zoomMode == 1 ? "Left/Right Click" : zoomMode == 2 ? "Mouse Slide" : "Mouse Wheel";
                GTA.UI.Screen.ShowSubtitle($"Zoom Mode: {mode}", 2000);
            }
            // lock/unlock zoom IN at current FOV
            else if (KeyBinds.ZoomInLock.Pressed(e))
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
            // lock/unlock zoom OUT at current FOV
            else if (KeyBinds.ZoomOutLock.Pressed(e))
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
            // Freeze completely while the on-screen input box is open: no
            // lerping, no input, no config writes - the typed value commits
            // against a stable state.
            if (VRCamMenu.IsTyping)
                return;

            // Apply pending menu changes (these work even while zoom controls
            // are locked, so the menu always stays functional)
            if (menuFov.HasValue) { targetFov = Math.Max(minFov, Math.Min(maxFov, menuFov.Value)); menuFov = null; externalZoomChange = true; }
            if (menuZoomInSmooth.HasValue) { zoomInSmoothness = menuZoomInSmooth.Value; ZoomInSpeed = zoomInSmoothness; menuZoomInSmooth = null; }
            if (menuZoomOutSmooth.HasValue) { zoomOutSmoothness = menuZoomOutSmooth.Value; ZoomOutSpeed = zoomOutSmoothness; menuZoomOutSmooth = null; }
            if (menuClickMode.HasValue) { useClickMode = menuClickMode.Value; UseClickMode = useClickMode; menuClickMode = null; }
            if (menuZoomMode.HasValue) { SetZoomMode(menuZoomMode.Value); menuZoomMode = null; }
            if (menuSlideSensitivity.HasValue) { slideSensitivity = Math.Max(0.02f, Math.Min(3f, menuSlideSensitivity.Value)); SlideSensitivity = slideSensitivity; VRCamModConfiguration.SlideSensitivity = slideSensitivity; VRCamModConfiguration.SaveSettingsOnly(); menuSlideSensitivity = null; }
            if (menuZoomInLock.HasValue) { zoomInLocked = menuZoomInLock.Value; ZoomInLocked = zoomInLocked; menuZoomInLock = null; externalZoomChange = true; }
            if (menuZoomOutLock.HasValue) { zoomOutLocked = menuZoomOutLock.Value; ZoomOutLocked = zoomOutLocked; menuZoomOutLock = null; externalZoomChange = true; }
            if (menuSoftMin.HasValue) { softMinFov = menuSoftMin.Value; SoftMinFOV = softMinFov; menuSoftMin = null; externalZoomChange = true; }
            if (menuSoftMax.HasValue) { softMaxFov = menuSoftMax.Value; SoftMaxFOV = softMaxFov; menuSoftMax = null; externalZoomChange = true; }
            if (menuRangeMin.HasValue || menuRangeMax.HasValue)
            {
                if (menuRangeMin.HasValue) minFov = Math.Max(EngineMinFov, Math.Min(KneeFov, menuRangeMin.Value));
                if (menuRangeMax.HasValue) maxFov = Math.Max(60f, Math.Min(EngineMaxFov, menuRangeMax.Value));
                menuRangeMin = menuRangeMax = null;
                MinFOV = minFov; MaxFOV = maxFov;
                VRCamModConfiguration.ZoomRangeMinFOV = minFov;
                VRCamModConfiguration.ZoomRangeMaxFOV = maxFov;
                VRCamModConfiguration.SaveSettingsOnly();
                targetFov = Math.Max(minFov, Math.Min(maxFov, targetFov));
                externalZoomChange = true;
            }
            TargetFOV = targetFov;

            if (!ZoomEnabled)
                return;

            // Only touch the rendering camera once VRCam has actually been
            // started. Before that, LMB/RMB or the wheel would zoom
            // whatever camera is rendering (e.g. another script's free camera).
            if (!AttachRatToCamera.RatActive)
                return;

            // The zoom input takes the look axes over (Mouse Slide: mouse X and Y; pad modes: the right stick) and,
            // on foot, LT / RT. That is only right while the headset does the looking, so the zoom runs only while
            // GTAVive renders the VR view. After VR is switched off in game, VRCam stays started but the mouse and
            // pad look around normally again.
            if (!GtaViveArmedFlag.IsArmed())
                return;

            // A preset was just loaded — pull the new values into our live fields
            if (pendingRefresh)
            {
                pendingRefresh = false;
                zoomInSmoothness = VRCamModConfiguration.ZoomInSmoothness;
                zoomOutSmoothness = VRCamModConfiguration.ZoomOutSmoothness;
                targetFov = Math.Max(minFov, Math.Min(maxFov, VRCamModConfiguration.CurrentFOV));
                externalZoomChange = true;
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
                targetFov = Math.Max(minFov, Math.Min(maxFov, defaultFov));
                externalZoomChange = true;
                currentFov = targetFov;
            }
            
            int scrollDelta = 0;

            // The slider-style modes (Mouse Slide, Stick Direct, DualTR) keep their own
            // 0..1 position. When the zoom or its limits are changed from OUTSIDE - the
            // menu's FOV row, a preset, Ctrl+MiddleMouse, a soft lock - re-seat those
            // sliders on the new zoom. Otherwise the next frame the old slider position
            // simply overwrites the change (the menu FOV row looked dead in Slide mode).
            if (externalZoomChange)
            {
                externalZoomChange = false;
                float syncOut = OutEnd();
                float seat = FovToSlider(targetFov, syncOut, InEnd());
                slider = seat;
                SlidePercent = slider * 100f;
                padSlider = FovToSlider(targetFov, syncOut, PadInEnd());
                padBaseFov = targetFov;
                padDeflected = false;
            }

            if (PadInput.Active && VRCamModConfiguration.PadZoomMode != 0 && HandlePadZoom())
            {
                // the gamepad drove the zoom this frame
            }
            else if (zoomMode == 2)
            {
                // Mouse slide: the mouse's forward/back travel IS the zoom slider.
                // The game must not also use it to look around, so take the axis
                // over for this frame and read the raw value.
                Game.DisableControlThisFrame(GTA.Control.LookUpDown);
                Game.DisableControlThisFrame(GTA.Control.LookLeftRight);
                float dy = Game.GetDisabledControlValueNormalized(GTA.Control.LookUpDown);
                float outFov = OutEnd();   // slider 0%
                float inFov = InEnd();     // slider 100%
                slider = Math.Max(0f, Math.Min(1f, slider - dy * slideSensitivity * 0.1f * SlideScale(outFov, inFov)));
                SlidePercent = slider * 100f;
                targetFov = Math.Max(minFov, Math.Min(maxFov, SliderToFov(slider, outFov, inFov)));
            }
            else if (zoomMode == 1)
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
                        targetFov = StepFov(targetFov, -zoomStep * 0.5f);
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
                        targetFov = StepFov(targetFov, zoomStep * 0.5f);
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
                        targetFov = StepFov(targetFov, -scrollDelta * zoomStep);
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
                // Direct pad modes: the stick / trigger IS the zoom. The IN/OUT glide
                // settings are for wheel and click zooms - on top of a direct control
                // they only make it lag, and unevenly (release crawled out at OUT=0.01
                // while a squeeze snapped in at IN=0.16). One symmetric response instead.
                if (padDirectThisFrame)
                    activeSmoothness = VRCamModConfiguration.PadDirectResponse;
                padDirectThisFrame = false;
                
                // eased on the zoom curve (plain degrees at or above 10; in plain degrees a slow glide out of a
                // 2-degree shot would pop out ~2.4x in a sixth of a second, then crawl)
                currentFov = FovFromU(Lerp(ZoomU(currentFov), ZoomU(targetFov), activeSmoothness));
                cam.FieldOfView = currentFov;
                CurrentFOV = currentFov;
                
                SaveToConfig();
            }
        }

        // ---------------- gamepad zoom ----------------
        // PadZoomMode: 0 off
        //   1 Stick Default   right stick Y zooms at a rate; the zoom stays where you leave it
        //   2 Stick Direct    stick deflection IS the zoom, springs back to where it was on release
        //   3 Trigger Default RT zooms in, LT zooms out (rate = pressure)
        //   4 Trigger DirectRT RT pressure IS the zoom; released = fully zoomed out
        //   5 Trigger DualTR  RT pressure IS the zoom, LT is the dead-stop: fully held keeps the
        //                     value, partly held lets it bleed back to RT, released follows RT
        // Trigger modes work on foot only - in a vehicle the triggers are gas and brake.
        public static readonly string[] PadZoomModeNames =
            { "Off", "Stick: Default", "Stick: Direct", "Triggers: Default", "Triggers: DirectRT", "Triggers: DualTR" };

        private float padBaseFov = 68f;
        private bool padDeflected = false;
        private float padSlider = 0f;     // 0 = zoomed out, 1 = zoomed in (trigger direct modes)
        private bool padStopHeld = false; // DualTR: LT was down last frame
        private bool externalZoomChange = false; // zoom / limits changed by menu, preset, reset
        private bool padDirectThisFrame = false; // a direct pad mode set the zoom this frame

        private bool HandlePadZoom()
        {
            int mode = VRCamModConfiguration.PadZoomMode;
            float dz = VRCamModConfiguration.PadDeadzone;
            float dt = Math.Max(0.001f, Game.LastFrameTime);
            float outFov = OutEnd();
            float inFov = PadInEnd();   // the position modes end at 10 degrees

            if (mode == 1 || mode == 2)
            {
                // The right stick's up/down becomes zoom; the game must not also look with it
                Game.DisableControlThisFrame(GTA.Control.LookUpDown);
                float y = PadInput.Axis(PadInput.RSTICK_Y);
                float look = Game.GetDisabledControlValueNormalized(GTA.Control.LookUpDown);
                if (Math.Abs(look) > Math.Abs(y)) y = look;
                float d = Math.Abs(y) < dz ? 0f : -(y - Math.Sign(y) * dz) / (1f - dz); // forward = +1 = zoom in

                if (mode == 1)
                {
                    targetFov = StepFov(targetFov, -d * VRCamModConfiguration.PadZoomSpeed * 60f * dt);
                }
                else if (d == 0f)
                {
                    if (padDeflected) { targetFov = padBaseFov; padDeflected = false; }
                    padBaseFov = targetFov; // at rest: whatever the zoom is now is the base
                }
                else
                {
                    padDeflected = true;
                    float uBase = ZoomU(padBaseFov);
                    // (from a deeper zoom than the 10-degree end, pushing "in" holds instead of zooming out)
                    targetFov = FovFromU(d > 0f ? uBase + (ZoomU(Math.Min(inFov, padBaseFov)) - uBase) * d
                                                : uBase + (ZoomU(outFov) - uBase) * -d);
                }
            }
            else
            {
                int player = Function.Call<int>(Hash.PLAYER_PED_ID);
                if (Function.Call<bool>(Hash.IS_PED_IN_ANY_VEHICLE, player, false))
                    return false; // gas / brake - leave the triggers alone

                // On foot LT aims and RT punches/shoots: take them over
                foreach (int c in new[] { 24, 25, 140, 141, 142, 257, 263, 264, 68, 69, 70, 91, 92 })
                    Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, c, true);

                float rt = Math.Max(PadInput.Axis(PadInput.BTN_RT), PadInput.Axis(24));
                float lt = Math.Max(PadInput.Axis(PadInput.BTN_LT), PadInput.Axis(25));
                if (rt < dz) rt = 0f;
                if (lt < dz) lt = 0f;

                if (mode == 3)
                {
                    targetFov = StepFov(targetFov, -(rt - lt) * VRCamModConfiguration.PadZoomSpeed * 60f * dt);
                }
                else if (mode == 4)
                {
                    padSlider = rt;
                    targetFov = SliderToFov(padSlider, outFov, inFov);
                }
                else
                {
                    // DualTR: RT pressure is the zoom, LT is the dead-stop.
                    // The moment LT goes down, grab the zoom AS IT IS ON SCREEN. Without
                    // this the value had already dropped to RT (0 once released) and only
                    // the picture was still easing down - so LT "held" a zero and could
                    // not stop the fall you were watching.
                    if (lt > 0f && !padStopHeld)
                    {
                        padSlider = FovToSlider(currentFov, outFov, inFov);
                    }
                    padStopHeld = lt > 0f;

                    if (lt >= 0.8f)
                    {
                        // held (most pads never report a full 1.0): the zoom stays exactly where it is
                    }
                    else if (lt > 0f)
                    {
                        // partly held: bleed toward wherever RT is, faster the more LT lets go
                        float step = (1f - lt) * 3f * dt;
                        padSlider += Math.Max(-step, Math.Min(step, rt - padSlider));
                    }
                    else
                        padSlider = rt;

                    targetFov = SliderToFov(padSlider, outFov, inFov);
                }
            }

            padDirectThisFrame = mode == 2 || mode == 4 || mode == 5;
            targetFov = Math.Max(minFov, Math.Min(maxFov, targetFov));
            return true;
        }

        private void SetZoomMode(int mode)
        {
            zoomMode = Math.Max(0, Math.Min(2, mode));
            Trace.Event("Zoom mode -> " + zoomMode);
            ZoomMode = zoomMode;
            useClickMode = zoomMode == 1;
            UseClickMode = useClickMode;
            if (zoomMode == 2)
            {
                // start the slider where the zoom currently is, so nothing jumps
                float outFov = OutEnd();
                float inFov = InEnd();
                slider = FovToSlider(targetFov, outFov, inFov);
                SlidePercent = slider * 100f;
            }
            VRCamModConfiguration.ZoomMode = zoomMode;
            SaveToConfig();
            VRCamModConfiguration.SaveSettingsOnly();
        }

        private float Lerp(float start, float end, float amount)
        {
            return start + (end - start) * amount;
        }
    }
}