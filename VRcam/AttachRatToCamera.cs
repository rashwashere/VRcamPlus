using System;
using System.Windows.Forms;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;

namespace VRCam
{
    public class AttachRatToCamera : Script
    {
        Ped rat;
        Vector3 offset = new Vector3(0.0f, 0.0f, 0.0f);

        private float vehicleInertiaCompensationFactor = 0.18f;

        private bool f10Enabled = true;
        private bool visibilityCollisionEnabled = false;
        private bool focusSet = false;
        private bool armedWas = false;      // GTAVive's VR state last tick (VRCam starts on each rise)
        private const float RatSnapDistance = 25f;  // metres: a camera jump this big in one tick is a teleport
        private const float RatStuckDistance = 300f; // metres: a rat this far behind is stuck, whatever the speed
        private Vector3 lastCamPos;
        private bool haveLastCamPos = false;
        private bool ratWasFar = false;            // logs a jump once, not every tick if it did not take
        private int lastFlashlightRespawn = 0;     // Game.GameTime of the last automatic flashlight respawn
        private const int FlashlightShadowId = 7831; // any fixed id: the game caches the beam's shadow under it

        private Prop flashlight;
        private bool flashlightVisible = false;
        private Vector3 flashlightOffset = new Vector3(0.0f, 0.0f, 0.0f);
        private Vector3 flashlightRotation = new Vector3(0.0f, 0.0f, 0.0f);
        private float moveStep = 0.01f;
        private float rotateStep = 5.0f;
        private bool flashlightMovementLocked = true; // Locked by default
        // Follow VR view: flashlightOffset is always the ACTIVE mode's offset (what the keys, pad and menu edit);
        // each mode keeps its own, swapped on toggle. Rat mode: offset from the camera rat (smoothed, can trail
        // the view). View mode: offset from the rendered camera itself, placed every tick (1:1 with the view),
        // in camera axes: X right, Y forward, Z up.
        private bool flashlightFollowView = false;
        private Vector3 ratModeOffset = Vector3.Zero;
        private Vector3 viewModeOffset = new Vector3(0.20f, 0.35f, -0.15f);

        private int saveMessageTimer = 0;

        // Preset management
        private List<string> presetNames = new List<string>();
        private int currentPresetIndex = -1;

        // Public properties for ViewfinderText to access
        public static bool RatActive { get; private set; } = false;
        public static bool FlashlightVisible { get; private set; } = false;
        public static bool F10Locked { get; private set; } = true;
        public static Vector3 FlashlightOffset { get; private set; } = Vector3.Zero;
        public static Vector3 FlashlightRotation { get; private set; } = Vector3.Zero;
        public static bool SaveMessageActive { get; private set; } = false;
        public static bool FlashlightMovementLocked { get; private set; } = true;
        public static bool FlashlightFollowView { get; private set; } = false;

        // Bridge for the VRCam menu
        public static AttachRatToCamera Instance { get; private set; }
        public static bool RatVisibleState
        {
            get { return Instance != null && Instance.visibilityCollisionEnabled; }
        }

        public AttachRatToCamera()
        {
            Instance = this;
            Tick += OnTick;
            KeyDown += OnKeyDown;
            // no torch left behind when the script is reloaded
            Aborted += (s, e) => { if (focusSet) Function.Call(Hash.CLEAR_FOCUS); DeleteFlashlight(); };

            // Load presets and apply last used
            LoadPresetsFromConfig();
            ApplyConfigToRuntime();
        }

        private static string NoRatMessage
        {
            get { return "No rat spawned yet (press " + KeyBinds.StartCamera.Text + " or use Spawn Rat)"; }
        }

        // ---------------- menu bridge methods ----------------

        public void Menu_SpawnRat()
        {
            if (rat == null || !Safe.Exists(rat))
                SpawnRat();
            else
                GTA.UI.Screen.ShowSubtitle("Rat already spawned", 1500);
        }

        public void Menu_ToggleRatVisibility()
        {
            if (rat == null || !Safe.Exists(rat))
            {
                GTA.UI.Screen.ShowSubtitle(NoRatMessage, 2000);
                return;
            }
            ToggleRatVisibility();
        }

        public void Menu_ToggleFlashlight()
        {
            if (!flashlightVisible && (rat == null || !Safe.Exists(rat)))   // switching OFF never needs the rat
            {
                GTA.UI.Screen.ShowSubtitle(NoRatMessage, 2000);
                return;
            }
            ToggleFlashlight();
        }

        public void Menu_ToggleFlashlightLock()
        {
            ToggleFlashlightLock();
        }

        public void Menu_ToggleMasterLock()
        {
            ToggleMasterLock();
        }

        // axis: 'X', 'Y', 'Z' = offset components, 'P' = pitch (rotation X)
        public void Menu_SetFlashlightOffset(char axis, float value)
        {
            switch (axis)
            {
                case 'X': flashlightOffset.X = value; break;
                case 'Y': flashlightOffset.Y = value; break;
                case 'Z': flashlightOffset.Z = value; break;
                case 'P': flashlightRotation.X = value; break;
            }
            FlashlightOffset = flashlightOffset;
            FlashlightRotation = flashlightRotation;
            SaveRuntimeToConfig();
        }

        // Switches between the rat-relative flashlight and the 1:1 VR-view flashlight, keeping each mode's offset.
        public void Menu_SetFlashlightFollowView(bool follow)
        {
            if (follow == flashlightFollowView) return;
            if (flashlightFollowView) viewModeOffset = flashlightOffset; else ratModeOffset = flashlightOffset;
            flashlightFollowView = follow;
            flashlightOffset = follow ? viewModeOffset : ratModeOffset;
            FlashlightFollowView = flashlightFollowView;
            FlashlightOffset = flashlightOffset;
            SaveRuntimeToConfig();
            Trace.Event("FlashlightFollowView -> " + follow);
            GTA.UI.Screen.ShowSubtitle(follow ? "Flashlight follows the VR view (1:1)" : "Flashlight follows the camera rat", 2000);
        }

        // Gamepad D-pad, all 8 directions: Up/Down = forward/back (Y),
        // Left/Right = left/right (X); a diagonal moves on both at once.
        public void Pad_NudgeFlashlight(int forward, int right)
        {
            flashlightOffset.Y += forward * moveStep;
            flashlightOffset.X += right * moveStep;
            FlashlightOffset = flashlightOffset;
            SaveRuntimeToConfig();
            GTA.UI.Screen.ShowSubtitle($"Flashlight  left/right X: {flashlightOffset.X:F3}   front/back Y: {flashlightOffset.Y:F3}", 400);
        }

        public void Menu_SavePreset()
        {
            Menu_SavePreset(null);
        }

        public void Menu_SavePreset(string customName)
        {
            SaveRuntimeToConfig();
            VRCamModConfiguration.Save(customName);
            LoadPresetsFromConfig();
            saveMessageTimer = 120;
            GTA.UI.Screen.ShowSubtitle($"Saved preset: {VRCamModConfiguration.LastPreset}", 2000);
        }

        public void Menu_LoadPresetByName(string presetName)
        {
            VRCamModConfiguration.LoadPresetByName(presetName);
            currentPresetIndex = presetNames.IndexOf(presetName);
            ApplyConfigToRuntime();
            CameraZoom.RefreshFromConfig();
            GTA.UI.Screen.ShowSubtitle($"Preset: {presetName}", 2000);
        }

        // ---------------- shared toggle logic ----------------

        private void ToggleRatVisibility()
        {
            Trace.Event("ToggleRatVisibility -> " + !visibilityCollisionEnabled);
            visibilityCollisionEnabled = !visibilityCollisionEnabled;

            Function.Call(Hash.SET_ENTITY_VISIBLE, rat.Handle, visibilityCollisionEnabled, false);
            Function.Call(Hash.SET_ENTITY_COLLISION, rat.Handle, visibilityCollisionEnabled, false);

            string state = visibilityCollisionEnabled ? "enabled" : "disabled";
            GTA.UI.Screen.ShowSubtitle("Rat visibility/collision " + state, 2000);
        }

        private void ToggleFlashlight()
        {
            Trace.Event("ToggleFlashlight -> " + !flashlightVisible);
            flashlightVisible = !flashlightVisible;
            FlashlightVisible = flashlightVisible;

            if (flashlightVisible && (flashlight == null || !Safe.Exists(flashlight)))
            {
                SpawnFlashlight();
            }

            if (!flashlightVisible)
            {
                // removed, not just hidden: a hidden torch can still shine its model light; switching on spawns a
                // fresh one
                DeleteFlashlight();
            }
            else if (flashlight != null && Safe.Exists(flashlight))
            {
                Function.Call(Hash.SET_ENTITY_VISIBLE, flashlight.Handle, true, false);
            }

            string state = flashlightVisible ? "visible" : "hidden";
            GTA.UI.Screen.ShowSubtitle("Flashlight " + state, 2000);
        }

        private void ToggleFlashlightLock()
        {
            flashlightMovementLocked = !flashlightMovementLocked;
            FlashlightMovementLocked = flashlightMovementLocked;
            SaveRuntimeToConfig();
            string status = flashlightMovementLocked ? "LOCKED" : "UNLOCKED";
            GTA.UI.Screen.ShowSubtitle($"Flashlight Movement {status}", 2000);
        }

        private void ToggleMasterLock()
        {
            Trace.Event("ToggleMasterLock -> " + !f10Enabled);
            f10Enabled = !f10Enabled;
            F10Locked = f10Enabled;
            CameraZoom.ZoomEnabled = f10Enabled;

            string state = f10Enabled ? "enabled" : "disabled";
            GTA.UI.Screen.ShowSubtitle("Rat/flashlight/zoom controls " + state, 2000);
        }

        private void LoadPresetsFromConfig()
        {
            presetNames = VRCamModConfiguration.GetPresetNames();
            if (!string.IsNullOrEmpty(VRCamModConfiguration.LastPreset))
            {
                currentPresetIndex = presetNames.IndexOf(VRCamModConfiguration.LastPreset);
            }
        }

        private void ApplyConfigToRuntime()
        {
            ratModeOffset = new Vector3(VRCamModConfiguration.FlashlightOffsetX, VRCamModConfiguration.FlashlightOffsetY,
                VRCamModConfiguration.FlashlightOffsetZ);
            viewModeOffset = new Vector3(VRCamModConfiguration.FlashlightViewOffsetX, VRCamModConfiguration.FlashlightViewOffsetY,
                VRCamModConfiguration.FlashlightViewOffsetZ);
            flashlightFollowView = VRCamModConfiguration.FlashlightFollowView;
            flashlightOffset = flashlightFollowView ? viewModeOffset : ratModeOffset;
            flashlightRotation.X = VRCamModConfiguration.FlashlightRotationX;
            flashlightMovementLocked = VRCamModConfiguration.FlashlightMovementLocked;
            FlashlightOffset = flashlightOffset;
            FlashlightRotation = flashlightRotation;
            FlashlightMovementLocked = flashlightMovementLocked;
            FlashlightFollowView = flashlightFollowView;
        }

        private void SaveRuntimeToConfig()
        {
            // the active offset belongs to the active mode; the other mode's stays as stored
            if (flashlightFollowView) viewModeOffset = flashlightOffset; else ratModeOffset = flashlightOffset;
            VRCamModConfiguration.FlashlightOffsetX = ratModeOffset.X;
            VRCamModConfiguration.FlashlightOffsetY = ratModeOffset.Y;
            VRCamModConfiguration.FlashlightOffsetZ = ratModeOffset.Z;
            VRCamModConfiguration.FlashlightViewOffsetX = viewModeOffset.X;
            VRCamModConfiguration.FlashlightViewOffsetY = viewModeOffset.Y;
            VRCamModConfiguration.FlashlightViewOffsetZ = viewModeOffset.Z;
            VRCamModConfiguration.FlashlightFollowView = flashlightFollowView;
            VRCamModConfiguration.FlashlightRotationX = flashlightRotation.X;
            VRCamModConfiguration.FlashlightMovementLocked = flashlightMovementLocked;
        }

        private void LoadPresetAt(int index)
        {
            VRCamModConfiguration.LoadPresetByName(presetNames[index]);
            ApplyConfigToRuntime();
            CameraZoom.RefreshFromConfig();
            GTA.UI.Screen.ShowSubtitle($"Preset: {presetNames[index]}", 2000);
        }

        private void NudgeOffset(char axis, float delta)
        {
            string label;
            float value;
            switch (axis)
            {
                case 'X': flashlightOffset.X += delta; label = "Left/right X"; value = flashlightOffset.X; break;
                case 'Y': flashlightOffset.Y += delta; label = "Forward/back Y"; value = flashlightOffset.Y; break;
                default: flashlightOffset.Z += delta; label = "Up/down Z"; value = flashlightOffset.Z; break;
            }
            FlashlightOffset = flashlightOffset;
            SaveRuntimeToConfig();
            GTA.UI.Screen.ShowSubtitle($"{label}: {value:F3}", 500);
        }

        private void NudgePitch(float delta)
        {
            flashlightRotation.X += delta;
            FlashlightRotation = flashlightRotation;
            GTA.UI.Screen.ShowSubtitle($"Pitch: {flashlightRotation.X:F1}°", 500);
        }

        private static bool IsFlashlightMoveKey(KeyEventArgs e)
        {
            return KeyBinds.FlashlightForward.Pressed(e) || KeyBinds.FlashlightBack.Pressed(e) ||
                   KeyBinds.FlashlightLeft.Pressed(e) || KeyBinds.FlashlightRight.Pressed(e) ||
                   KeyBinds.FlashlightUp.Pressed(e) || KeyBinds.FlashlightDown.Pressed(e) ||
                   KeyBinds.FlashlightPitchUp.Pressed(e) || KeyBinds.FlashlightPitchDown.Pressed(e);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (VRCamMenu.SuppressHotkeys)
                return;

            if (KeyBinds.StartCamera.Pressed(e))
            {
                Trace.Event("StartCamera key pressed (AttachRatToCamera) ratExists=" + (rat != null && Safe.Exists(rat)));
                Script.Wait(100);

                if (rat == null || !Safe.Exists(rat))
                {
                    SpawnRat();
                }
            }
            else if (!RatActive)
            {
                // Only the Start key (above) and the menu work before VRCam has been started.
            }
            else if (KeyBinds.SavePreset.Pressed(e))
            {
                if (f10Enabled)
                {
                    Menu_SavePreset();
                }
                else
                {
                    GTA.UI.Screen.ShowSubtitle("Save is locked (press " + KeyBinds.LockControls.Text + " to unlock)", 2000);
                }
            }
            else if (KeyBinds.NextPreset.Pressed(e))
            {
                if (presetNames.Count > 0)
                {
                    currentPresetIndex = (currentPresetIndex + 1) % presetNames.Count;
                    LoadPresetAt(currentPresetIndex);
                }
            }
            else if (KeyBinds.PrevPreset.Pressed(e))
            {
                if (presetNames.Count > 0)
                {
                    currentPresetIndex--;
                    if (currentPresetIndex < 0)
                        currentPresetIndex = presetNames.Count - 1;
                    LoadPresetAt(currentPresetIndex);
                }
            }
            else if (KeyBinds.FlashlightLock.Pressed(e))
            {
                ToggleFlashlightLock();
            }
            else if (KeyBinds.LockControls.Pressed(e))
            {
                ToggleMasterLock();
            }
            else if (KeyBinds.ShowHideRat.Pressed(e) && rat != null && Safe.Exists(rat))
            {
                if (f10Enabled)
                {
                    ToggleRatVisibility();
                }
                else
                {
                    GTA.UI.Screen.ShowSubtitle("Show / hide the rat is disabled (press " + KeyBinds.LockControls.Text + " to enable)", 2000);
                }
            }
            else if (KeyBinds.ToggleFlashlight.Pressed(e) && (flashlightVisible || (rat != null && Safe.Exists(rat))))
            {
                if (f10Enabled)
                {
                    ToggleFlashlight();
                }
                else
                {
                    GTA.UI.Screen.ShowSubtitle("The flashlight key is disabled (press " + KeyBinds.LockControls.Text + " to enable)", 2000);
                }
            }
            else if (f10Enabled && flashlightVisible && flashlight != null && Safe.Exists(flashlight) && !flashlightMovementLocked)
            {
                // Only allow flashlight movement when unlocked. Axes: X right, Y forward, Z up (as the D-pad and menu).
                if (KeyBinds.FlashlightPitchUp.Pressed(e)) NudgePitch(rotateStep);
                else if (KeyBinds.FlashlightPitchDown.Pressed(e)) NudgePitch(-rotateStep);
                else if (KeyBinds.FlashlightForward.Pressed(e)) NudgeOffset('Y', moveStep);
                else if (KeyBinds.FlashlightBack.Pressed(e)) NudgeOffset('Y', -moveStep);
                else if (KeyBinds.FlashlightLeft.Pressed(e)) NudgeOffset('X', -moveStep);
                else if (KeyBinds.FlashlightRight.Pressed(e)) NudgeOffset('X', moveStep);
                else if (KeyBinds.FlashlightUp.Pressed(e)) NudgeOffset('Z', moveStep);
                else if (KeyBinds.FlashlightDown.Pressed(e)) NudgeOffset('Z', -moveStep);
            }
            else if (f10Enabled && flashlightVisible && flashlight != null && Safe.Exists(flashlight) && flashlightMovementLocked)
            {
                // Show locked message when trying to move flashlight while locked
                if (IsFlashlightMoveKey(e))
                {
                    GTA.UI.Screen.ShowSubtitle("Flashlight movement is LOCKED (" + KeyBinds.FlashlightLock.Text + " to unlock)", 1500);
                }
            }
        }

        private void SpawnRat()
        {
            Model ratModel = new Model("a_c_rat");
            ratModel.Request(500);

            if (ratModel.IsInCdImage && ratModel.IsValid)
            {
                while (!ratModel.IsLoaded)
                {
                    Script.Wait(100);
                }
                rat = Safe.CreatePed(ratModel, Game.Player.Character.Position + Safe.Forward(Game.Player.Character) * 2);
                Trace.Event("SpawnRat -> handle " + (rat == null ? 0 : rat.Handle));
                if (rat == null)
                {
                    GTA.UI.Screen.ShowSubtitle("VRCam: could not spawn the camera rat (CREATE_PED failed)", 3000);
                    return;
                }
                Function.Call(Hash.SET_ENTITY_COLLISION, rat.Handle, false, false);
                Function.Call(Hash.SET_ENTITY_HAS_GRAVITY, rat.Handle, false);
                Function.Call(Hash.SET_ENTITY_VISIBLE, rat.Handle, false, false);
                rat.Task.ClearAll();
                Function.Call(Hash.SET_PED_TO_RAGDOLL, rat.Handle, 10000, 10000, 0, true, true, false);
                ratModel.MarkAsNoLongerNeeded();

                visibilityCollisionEnabled = false;
                RatActive = true;
            }
        }

        private void SpawnFlashlight(bool automatic = false)
        {
            Model flashlightModel = new Model("prop_cs_police_torch_02");
            flashlightModel.Request(500);

            if (flashlightModel.IsInCdImage && flashlightModel.IsValid)
            {
                // bounded: this also runs from OnTick, which must never stall or throw (a throw stops the script)
                for (int i = 0; i < 20 && !flashlightModel.IsLoaded; i++)
                    Script.Wait(100);

                flashlight = flashlightModel.IsLoaded ? Safe.CreateProp(flashlightModel, rat.Position, false) : null;
                Trace.Event("SpawnFlashlight -> handle " + (flashlight == null ? 0 : flashlight.Handle) + (automatic ? " (automatic)" : ""));
                flashlightModel.MarkAsNoLongerNeeded();
                if (flashlight == null) return;   // model not loaded or the object pool full: try again later
                Function.Call(Hash.SET_ENTITY_COLLISION, flashlight.Handle, false, false);
                Function.Call(Hash.SET_ENTITY_HAS_GRAVITY, flashlight.Handle, false);
                Function.Call(Hash.SET_ENTITY_VISIBLE, flashlight.Handle, flashlightVisible, false);

                if (!automatic)
                {
                    string lockStatus = flashlightMovementLocked ? " (LOCKED - " + KeyBinds.FlashlightLock.Text + " to unlock)" : "";
                    GTA.UI.Screen.ShowSubtitle("Flashlight spawned - use the flashlight keys to position it" + lockStatus, 3000);
                }
            }
        }

        private Vector3 GetForwardVector(Camera cam)
        {
            float pitchRad = cam.Rotation.X * (float)Math.PI / 180f;
            float yawRad = cam.Rotation.Z * (float)Math.PI / 180f;
            return new Vector3(
                (float)(-Math.Sin(yawRad) * Math.Cos(pitchRad)),
                (float)(Math.Cos(yawRad) * Math.Cos(pitchRad)),
                (float)Math.Sin(pitchRad)
            );
        }

        private Vector3 GetInertiaVelocity()
        {
            // Ped.CurrentVehicle reads memory (crashes on Enhanced); natives instead
            int currentVehicle = Safe.CurrentVehicle(Game.Player.Character);
            if (!Safe.Exists(currentVehicle))
                return Vector3.Zero;

            int parentHandle = Function.Call<int>(Hash.GET_ENTITY_ATTACHED_TO, currentVehicle);
            if (parentHandle != 0)
            {
                int modelHash = Function.Call<int>(Hash.GET_ENTITY_MODEL, parentHandle);
                bool isVehicle = Function.Call<bool>(Hash.IS_MODEL_A_VEHICLE, modelHash);

                if (isVehicle)
                {
                    Vector3 parentVelocity = Function.Call<Vector3>(Hash.GET_ENTITY_VELOCITY, parentHandle);
                    return parentVelocity;
                }
            }
            return Safe.Velocity(currentVehicle);
        }

        private void OnTick(object sender, EventArgs e)
        {
            Trace.Sample(rat, flashlight);

            // Each time GTAVive's VR view starts (its own key, its auto-start or its menu): spawn the camera rat if
            // missing and, if set, hide GTAVive's hand pistol. Both steps are harmless when repeated.
            bool armedNow = GtaViveArmedFlag.IsArmed();
            bool armedEdge = armedNow && !armedWas;
            armedWas = armedNow;
            if (armedEdge)
            {
                Trace.Event("GTAVive VR started - starting VRCam (ratExists=" + (rat != null && Safe.Exists(rat)) + ")");
                if (rat == null || !Safe.Exists(rat))
                    SpawnRat();
                if (VRCamModConfiguration.AutoHidePistol)
                {
                    HideGTAVivePistol.DeleteNowRequested = true;
                    HideGTAVivePistol.RequestDeleteSoon();
                }
            }
            if (saveMessageTimer > 0)
            {
                saveMessageTimer--;
                SaveMessageActive = true;
            }
            else
            {
                SaveMessageActive = false;
            }

            DrawCameraLight();   // needs no rat, so before the rat block

            if (rat != null && Safe.Exists(rat))
            {
                Function.Call(Hash.SET_ENTITY_HEALTH, rat.Handle, 10, 0); // native: Ped.Health reads memory on Enhanced
                Function.Call(Hash.SET_ENTITY_COLLISION, rat.Handle, false, false);
                Function.Call(Hash.SET_ENTITY_HAS_GRAVITY, rat.Handle, false);
                Function.Call(Hash.SET_ENTITY_DYNAMIC, rat.Handle, true);
                Function.Call(Hash.SET_PED_TO_RAGDOLL, rat.Handle, 10000, 10000, 0, true, true, false);

                if (visibilityCollisionEnabled)
                {
                    Function.Call(Hash.SET_ENTITY_VISIBLE, rat.Handle, true, false);
                    Function.Call(Hash.SET_ENTITY_COLLISION, rat.Handle, true, false);
                }

                Camera cam = World.RenderingCamera;
                if (cam != null)
                {
                    Vector3 camPos = cam.Position;

                    // Streaming/LOD (and the underwater fog test) follow the player ped,
                    // not the camera. Pin them to the camera so a displaced player ped
                    // can't turn the world around the camera into low-detail geometry.
                    if (VRCamModConfiguration.FocusFollowsCamera)
                    {
                        Function.Call(Hash.SET_FOCUS_POS_AND_VEL, camPos.X, camPos.Y, camPos.Z, 0f, 0f, 0f);
                        focusSet = true;
                    }
                    else if (focusSet) { Function.Call(Hash.CLEAR_FOCUS); focusSet = false; }
                    Vector3 camRot = cam.Rotation;
                    Vector3 camForward = GetForwardVector(cam);
                    Vector3 targetPos = camPos + camForward + offset;

                    bool isInVehicle = Game.Player.Character.IsInVehicle();
                    float activeSmoothingFactor = VRCamModConfiguration.PositionalSmoothingFactor;
                    float activeRotationSmoothingFactor = VRCamModConfiguration.RotationSmoothingFactor;

                    if (isInVehicle)
                    {
                        Vector3 inertiaVelocity = GetInertiaVelocity();
                        targetPos += inertiaVelocity * vehicleInertiaCompensationFactor;
                    }

                    // A teleport (a load, a scene change) can leave the rat in an area that streams out, where
                    // velocity no longer moves it. Move it to the camera when the camera itself jumped (a
                    // teleport, not fast travel, where the rat trails by speed / smoothing), or when the rat is so
                    // far off that it can only be stuck.
                    bool camJumped = haveLastCamPos && (camPos - lastCamPos).Length() > RatSnapDistance;
                    lastCamPos = camPos; haveLastCamPos = true;
                    bool ratFar = camJumped || (targetPos - rat.Position).Length() > RatStuckDistance;
                    if (ratFar && !ratWasFar) Trace.Event("Rat " + (targetPos - rat.Position).Length().ToString("F0") + " m from the camera - moved to it");
                    ratWasFar = ratFar;

                    if (!VRCamModConfiguration.DisableSmoothing && !ratFar)
                    {
                        Vector3 currentPos = rat.Position;
                        Vector3 delta = targetPos - currentPos;
                        Vector3 desiredVelocity = delta * activeSmoothingFactor;

                        if (isInVehicle)
                        {
                            desiredVelocity += GetInertiaVelocity() * vehicleInertiaCompensationFactor;
                        }
                        Function.Call(Hash.SET_ENTITY_VELOCITY, rat.Handle, desiredVelocity.X, desiredVelocity.Y, desiredVelocity.Z);
                    }
                    else
                    {
                        if (isInVehicle)
                        {
                            rat.Position = targetPos + GetInertiaVelocity() * vehicleInertiaCompensationFactor;
                        }
                        else
                        {
                            rat.Position = targetPos;
                        }
                    }

                    if (!VRCamModConfiguration.DisableSmoothing)
                    {
                        Vector3 currentRotation = rat.Rotation;
                        Vector3 smoothedRotation = LerpRotation(currentRotation, camRot, activeRotationSmoothingFactor);
                        rat.Rotation = smoothedRotation;
                    }
                    else
                    {
                        rat.Rotation = camRot;
                    }
                }

                if (flashlight != null && Safe.Exists(flashlight) && flashlightFollowView)
                {
                    // Follow VR view: straight from the rendered camera this tick, no rat and no smoothing.
                    // Without a scripted camera (VR off) it follows the gameplay camera instead.
                    Function.Call(Hash.SET_ENTITY_COLLISION, flashlight.Handle, false, false);
                    Function.Call(Hash.SET_ENTITY_HAS_GRAVITY, flashlight.Handle, false);
                    // World.RenderingCamera is never null (it wraps GET_RENDERING_CAM, -1 with no scripted camera),
                    // so test the handle itself; otherwise use whatever camera was rendered last.
                    int camHandle = Function.Call<int>(Hash.GET_RENDERING_CAM);
                    bool scripted = camHandle != -1 && Function.Call<bool>(Hash.DOES_CAM_EXIST, camHandle);
                    Vector3 vPos = scripted ? Function.Call<Vector3>(Hash.GET_CAM_COORD, camHandle)
                                            : Function.Call<Vector3>(Hash.GET_FINAL_RENDERED_CAM_COORD);
                    Vector3 vRot = scripted ? Function.Call<Vector3>(Hash.GET_CAM_ROT, camHandle, 2)
                                            : Function.Call<Vector3>(Hash.GET_FINAL_RENDERED_CAM_ROT, 2);
                    Vector3 right, forward, up;
                    CameraAxes(vRot, out right, out forward, out up);
                    Vector3 flashPos = vPos + right * flashlightOffset.X + forward * flashlightOffset.Y + up * flashlightOffset.Z;
                    flashlight.Position = flashPos;
                    flashlight.Rotation = vRot + flashlightRotation;
                    DrawFlashlightBeam(flashPos, vRot + flashlightRotation);
                }
                else if (flashlight != null && Safe.Exists(flashlight))
                {
                    Function.Call(Hash.SET_ENTITY_COLLISION, flashlight.Handle, false, false);
                    Function.Call(Hash.SET_ENTITY_HAS_GRAVITY, flashlight.Handle, false);

                    // GET_ENTITY_MATRIX instead of Right/UpVector: those read the
                    // entity matrix from memory, which crashes on GTA V Enhanced.
                    // Native output order is (forward, right, up, position).
                    OutputArgument oRight = new OutputArgument(), oForward = new OutputArgument(),
                        oUp = new OutputArgument(), oPos = new OutputArgument();
                    Function.Call(Hash.GET_ENTITY_MATRIX, rat.Handle, oForward, oRight, oUp, oPos);
                    Vector3 ratRight = oRight.GetResult<Vector3>();
                    Vector3 ratForward = oForward.GetResult<Vector3>();
                    Vector3 ratUp = oUp.GetResult<Vector3>();

                    Vector3 flashlightWorldPos = rat.Position +
                        ratRight * flashlightOffset.X +
                        ratForward * flashlightOffset.Y +
                        ratUp * flashlightOffset.Z;

                    flashlight.Position = flashlightWorldPos;
                    flashlight.Rotation = rat.Rotation + flashlightRotation;
                    DrawFlashlightBeam(flashlightWorldPos, rat.Rotation + flashlightRotation);
                }
                else if (flashlightVisible && Game.GameTime - lastFlashlightRespawn > 2000)
                {
                    // the game deleted the prop (streaming, a load): bring it back, at most every 2 s
                    lastFlashlightRespawn = Game.GameTime;
                    Trace.Event("Flashlight prop gone while on - spawning a new one");
                    SpawnFlashlight(true);
                }
            }
        }

        // VRCam's own beam, drawn every frame from the flashlight along the torch's light axis (the rotation it was
        // just given, so no entity memory is read). The torch model's built-in light (spot, intensity 12, 20 m,
        // cone 60, along local -Y from a lens at y -0.219) is only on from 17:00 to 06:59 by its time flags.
        private void DrawFlashlightBeam(Vector3 pos, Vector3 rot)
        {
            if (!flashlightVisible || !VRCamModConfiguration.FlashlightBeam) return;
            Vector3 right, forward, up;
            CameraAxes(rot, out right, out forward, out up);
            forward = -forward;                    // the torch shines along its local -Y (Pitch 160 turns that ahead)
            Vector3 from = pos + forward * 0.25f;  // just past the lens (0.219), so the beam does not light the torch
            DrawSpot(from, forward, FlashlightShadowId);
        }

        private void DeleteFlashlight()
        {
            if (flashlight == null) return;
            try
            {
                if (Safe.Exists(flashlight))
                {
                    int handle = flashlight.Handle;
                    Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, handle, true, true);
                    OutputArgument h = new OutputArgument(handle);
                    Function.Call(Hash.DELETE_OBJECT, h);
                    Trace.Event("Flashlight prop " + handle + " removed");
                }
            }
            catch (Exception ex)
            {
                // could not delete: at least hide it, so an untracked torch never keeps shining
                Trace.Event("Flashlight remove failed (" + ex.Message + "), hiding it");
                try { Function.Call(Hash.SET_ENTITY_VISIBLE, flashlight.Handle, false, false); } catch (Exception) { }
            }
            flashlight = null;
        }

        // One spot light with the Beam settings (brightness, range, width, shadows), shared by the torch and the camera
        private static void DrawSpot(Vector3 from, Vector3 dir, int shadowId)
        {
            float range = VRCamModConfiguration.FlashlightRange, bright = VRCamModConfiguration.FlashlightBrightness;
            float width = VRCamModConfiguration.FlashlightWidth;
            if (VRCamModConfiguration.FlashlightShadows)
                Function.Call(Hash.DRAW_SHADOWED_SPOT_LIGHT, from.X, from.Y, from.Z, dir.X, dir.Y, dir.Z,
                    255, 244, 225, range, bright, Math.Min(VRCamModConfiguration.FlashlightRoundness, width), width, VRCamModConfiguration.FlashlightFeather, shadowId);
            else
                Function.Call(Hash.DRAW_SPOT_LIGHT, from.X, from.Y, from.Z, dir.X, dir.Y, dir.Z,
                    255, 244, 225, range, bright, Math.Min(VRCamModConfiguration.FlashlightRoundness, width), width, VRCamModConfiguration.FlashlightFeather);
        }

        // Camera Light: the beam straight from the rendered camera (the VR view or the rendering camera), like a
        // light on top of a camcorder. No torch prop and no rat, so it also works with the camera rat not spawned.
        private const int CameraLightShadowId = 7832;

        // Rockstar Editor diagnostics: one trace line when the Editor opens / closes, then every 2 s while it is
        // open (that this script ticks, the camera it draws from, the cutscene / fade flags)
        private static bool editorWas = false;
        private static int editorTraceAt = 0;
        private static void EditorTrace(bool editor)
        {
            int now = Environment.TickCount;
            if (editor == editorWas && (!editor || unchecked(now - editorTraceAt) < 2000)) return;
            editorTraceAt = now;
            if (editor != editorWas) Trace.Event("Rockstar Editor " + (editor ? "UP (script cleanup requested)" : "gone"));
            editorWas = editor;
            if (!editor) return;
            Vector3 p = Function.Call<Vector3>(Hash.GET_FINAL_RENDERED_CAM_COORD);
            Trace.Event(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Editor: VRCam ticking, rendered cam ({0:F1},{1:F1},{2:F1}) scripted={3} cutscene={4} fadedOut={5} light={6}",
                p.X, p.Y, p.Z, Function.Call<int>(Hash.GET_RENDERING_CAM), Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE),
                Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT), VRCamModConfiguration.EditorLight));
        }
        private static void DrawCameraLight()
        {
            // A script light is not part of what the Rockstar Editor records, so while the Editor is open the beam
            // is drawn live from the Editor's own camera (old clips and new alike). The Editor counts as open while
            // REPLAY_SYSTEM_HAS_REQUESTED_A_SCRIPT_CLEANUP (0x95AB8B5C992C7B58) is true.
            bool editor = Function.Call<bool>((Hash)0x95AB8B5C992C7B58UL);
            EditorTrace(editor);
            // in the Editor: only with Light in Rockstar Editor on AND a light that was on in game (Camera Light or the
            // flashlight), so nobody gets an unasked beam; outside it: Camera Light
            if (editor ? !(VRCamModConfiguration.EditorLight && (VRCamModConfiguration.CameraLight || FlashlightVisible))
                       : !VRCamModConfiguration.CameraLight) return;
            // not in a cutscene, a fade or a loading screen (the camera there can sit at the map origin): natives
            // IS_CUTSCENE_ACTIVE 0x991251AFC3981F84, IS_SCREEN_FADED_OUT 0xB16FCE9DDC7BA182, GET_IS_LOADING_SCREEN_ACTIVE
            // 0x10D0A8F259E93EC9 (the Editor may report a cutscene, so that one is not checked there)
            if (!editor && (Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE) || Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT)
                || Function.Call<bool>(Hash.GET_IS_LOADING_SCREEN_ACTIVE))) return;
            if (editor && Function.Call<bool>(Hash.GET_IS_LOADING_SCREEN_ACTIVE)) return;
            int camHandle = Function.Call<int>(Hash.GET_RENDERING_CAM);
            bool scripted = camHandle != -1 && Function.Call<bool>(Hash.DOES_CAM_EXIST, camHandle);
            Vector3 cPos = scripted ? Function.Call<Vector3>(Hash.GET_CAM_COORD, camHandle)
                                    : Function.Call<Vector3>(Hash.GET_FINAL_RENDERED_CAM_COORD);
            Vector3 cRot = scripted ? Function.Call<Vector3>(Hash.GET_CAM_ROT, camHandle, 2)
                                    : Function.Call<Vector3>(Hash.GET_FINAL_RENDERED_CAM_ROT, 2);
            cRot.X += VRCamModConfiguration.CameraLightPitch;   // aim it a little down, like a real camera light
            Vector3 right, forward, up;
            CameraAxes(cRot, out right, out forward, out up);
            // from just above and in front of the lens, so the light never sits inside the camera
            Vector3 from = cPos + up * (VRCamModConfiguration.CameraLightHeightCm / 100f) + forward * 0.10f;
            DrawSpot(from, forward, CameraLightShadowId);
        }

        // Camera axes from a GTA camera rotation (X pitch, Y roll, Z yaw, degrees, rotation order 2):
        // C = Rz(yaw) * Rx(pitch) * Ry(roll); forward matches GetForwardVector above.
        private static void CameraAxes(Vector3 rot, out Vector3 right, out Vector3 forward, out Vector3 up)
        {
            double p = rot.X * Math.PI / 180.0, r = rot.Y * Math.PI / 180.0, y = rot.Z * Math.PI / 180.0;
            float sp = (float)Math.Sin(p), cp = (float)Math.Cos(p);
            float sr = (float)Math.Sin(r), cr = (float)Math.Cos(r);
            float sy = (float)Math.Sin(y), cy = (float)Math.Cos(y);
            right = new Vector3(cy * cr - sy * sp * sr, sy * cr + cy * sp * sr, -cp * sr);
            forward = new Vector3(-sy * cp, cy * cp, sp);
            up = new Vector3(cy * sr + sy * sp * cr, sy * sr - cy * sp * cr, cp * cr);
        }

        private Vector3 LerpRotation(Vector3 current, Vector3 target, float factor)
        {
            return new Vector3(
                LerpAngle(current.X, target.X, factor),
                LerpAngle(current.Y, target.Y, factor),
                LerpAngle(current.Z, target.Z, factor)
            );
        }

        private float LerpAngle(float current, float target, float factor)
        {
            float delta = (target - current + 540) % 360 - 180;
            return current + delta * factor;
        }
    }
}
