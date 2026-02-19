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

        private Prop flashlight;
        private bool flashlightVisible = false;
        private Vector3 flashlightOffset = new Vector3(0.0f, 0.0f, 0.0f);
        private Vector3 flashlightRotation = new Vector3(0.0f, 0.0f, 0.0f);
        private float moveStep = 0.01f;
        private float rotateStep = 5.0f;
        private bool flashlightMovementLocked = true; // Locked by default

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

        public AttachRatToCamera()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            
            // Load presets and apply last used
            LoadPresetsFromConfig();
            ApplyConfigToRuntime();
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
            flashlightOffset.X = VRCamModConfiguration.FlashlightOffsetX;
            flashlightOffset.Y = VRCamModConfiguration.FlashlightOffsetY;
            flashlightOffset.Z = VRCamModConfiguration.FlashlightOffsetZ;
            flashlightRotation.X = VRCamModConfiguration.FlashlightRotationX;
            flashlightMovementLocked = VRCamModConfiguration.FlashlightMovementLocked;
            FlashlightOffset = flashlightOffset;
            FlashlightRotation = flashlightRotation;
            FlashlightMovementLocked = flashlightMovementLocked;
        }

        private void SaveRuntimeToConfig()
        {
            VRCamModConfiguration.FlashlightOffsetX = flashlightOffset.X;
            VRCamModConfiguration.FlashlightOffsetY = flashlightOffset.Y;
            VRCamModConfiguration.FlashlightOffsetZ = flashlightOffset.Z;
            VRCamModConfiguration.FlashlightRotationX = flashlightRotation.X;
            VRCamModConfiguration.FlashlightMovementLocked = flashlightMovementLocked;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            bool modifierPressed = e.Control || e.Shift || e.Alt;

            if (e.KeyCode == KeyBinds.ToggleUI && !modifierPressed)
            {
                Script.Wait(100);

                if (rat == null || !rat.Exists())
                {
                    SpawnRat();
                }
            }
            else if (e.KeyCode == KeyBinds.SavePreset && e.Control && !e.Shift && !e.Alt && KeyBinds.SavePreset_Ctrl) // Ctrl+F10 = Save
            {
                if (f10Enabled)
                {
                    SaveRuntimeToConfig();
                    VRCamModConfiguration.Save();
                    LoadPresetsFromConfig(); // Reload preset list
                    saveMessageTimer = 120;
                    GTA.UI.Screen.ShowSubtitle("Settings saved as new preset!", 2000);
                }
                else
                {
                    GTA.UI.Screen.ShowSubtitle("Save is locked (press F10 to unlock)", 2000);
                }
            }
            else if (e.KeyCode == KeyBinds.CyclePresetNext && e.Control && !e.Shift && !e.Alt && KeyBinds.CyclePresetNext_Ctrl) // Ctrl++ = Next preset
            {
                if (presetNames.Count > 0)
                {
                    currentPresetIndex = (currentPresetIndex + 1) % presetNames.Count;
                    VRCamModConfiguration.LoadPresetByName(presetNames[currentPresetIndex]);
                    ApplyConfigToRuntime();
                    CameraZoom.RefreshFromConfig();
                    GTA.UI.Screen.ShowSubtitle($"Preset: {presetNames[currentPresetIndex]}", 2000);
                }
            }
            else if (e.KeyCode == KeyBinds.CyclePresetPrev && e.Control && !e.Shift && !e.Alt && KeyBinds.CyclePresetPrev_Ctrl) // Ctrl+- = Previous preset
            {
                if (presetNames.Count > 0)
                {
                    currentPresetIndex--;
                    if (currentPresetIndex < 0)
                        currentPresetIndex = presetNames.Count - 1;
                    VRCamModConfiguration.LoadPresetByName(presetNames[currentPresetIndex]);
                    ApplyConfigToRuntime();
                    CameraZoom.RefreshFromConfig();
                    GTA.UI.Screen.ShowSubtitle($"Preset: {presetNames[currentPresetIndex]}", 2000);
                }
            }
            else if (e.KeyCode == KeyBinds.FlashlightLock && e.Control && !e.Shift && !e.Alt && KeyBinds.FlashlightLock_Ctrl) // Ctrl+3 = Toggle flashlight movement lock
            {
                flashlightMovementLocked = !flashlightMovementLocked;
                FlashlightMovementLocked = flashlightMovementLocked;
                SaveRuntimeToConfig();
                string status = flashlightMovementLocked ? "LOCKED" : "UNLOCKED";
                GTA.UI.Screen.ShowSubtitle($"Flashlight Movement {status}", 2000);
            }
            else if (e.KeyCode == KeyBinds.LockUnlockControls && !modifierPressed)
            {
                f10Enabled = !f10Enabled;
                F10Locked = f10Enabled;
                CameraZoom.ZoomEnabled = f10Enabled;
                
                string state = f10Enabled ? "enabled" : "disabled";
                GTA.UI.Screen.ShowSubtitle("F5/CapsLock/Arrow/Zoom controls " + state, 2000);
            }
            else if (e.KeyCode == KeyBinds.ShowHideRat && !modifierPressed && rat != null && rat.Exists())
            {
                if (f10Enabled)
                {
                    visibilityCollisionEnabled = !visibilityCollisionEnabled;
                    
                    Function.Call(Hash.SET_ENTITY_VISIBLE, rat.Handle, visibilityCollisionEnabled, false);
                    Function.Call(Hash.SET_ENTITY_COLLISION, rat.Handle, visibilityCollisionEnabled, false);
                    
                    string state = visibilityCollisionEnabled ? "enabled" : "disabled";
                    GTA.UI.Screen.ShowSubtitle("Rat visibility/collision " + state, 2000);
                }
                else
                {
                    GTA.UI.Screen.ShowSubtitle("F5 is currently disabled (press F10 to enable)", 2000);
                }
            }
            else if (e.KeyCode == KeyBinds.ToggleFlashlight && !modifierPressed && rat != null && rat.Exists())
            {
                if (f10Enabled)
                {
                    flashlightVisible = !flashlightVisible;
                    FlashlightVisible = flashlightVisible;
                    
                    if (flashlightVisible && (flashlight == null || !flashlight.Exists()))
                    {
                        SpawnFlashlight();
                    }
                    
                    if (flashlight != null && flashlight.Exists())
                    {
                        Function.Call(Hash.SET_ENTITY_VISIBLE, flashlight.Handle, flashlightVisible, false);
                    }
                    
                    string state = flashlightVisible ? "visible" : "hidden";
                    GTA.UI.Screen.ShowSubtitle("Flashlight " + state, 2000);
                }
                else
                {
                    GTA.UI.Screen.ShowSubtitle("CapsLock is currently disabled (press F10 to enable)", 2000);
                }
            }
            else if (f10Enabled && flashlightVisible && flashlight != null && flashlight.Exists() && !flashlightMovementLocked)
            {
                // Only allow flashlight movement when unlocked
                if (e.KeyCode == KeyBinds.FlashlightMoveForward)
                {
                    if (e.Control && !e.Shift && !e.Alt && KeyBinds.FlashlightRotate_Ctrl)
                    {
                        flashlightRotation.X += rotateStep;
                        FlashlightRotation = flashlightRotation;
                        GTA.UI.Screen.ShowSubtitle($"Pitch: {flashlightRotation.X:F1}°", 500);
                    }
                    else if (!e.Control && !e.Shift && !e.Alt)
                    {
                        flashlightOffset.X += moveStep;
                        FlashlightOffset = flashlightOffset;
                        GTA.UI.Screen.ShowSubtitle($"X: {flashlightOffset.X:F3}", 500);
                    }
                }
                else if (e.KeyCode == KeyBinds.FlashlightMoveBack)
                {
                    if (e.Control && !e.Shift && !e.Alt && KeyBinds.FlashlightRotate_Ctrl)
                    {
                        flashlightRotation.X -= rotateStep;
                        FlashlightRotation = flashlightRotation;
                        GTA.UI.Screen.ShowSubtitle($"Pitch: {flashlightRotation.X:F1}°", 500);
                    }
                    else if (!e.Control && !e.Shift && !e.Alt)
                    {
                        flashlightOffset.X -= moveStep;
                        FlashlightOffset = flashlightOffset;
                        GTA.UI.Screen.ShowSubtitle($"X: {flashlightOffset.X:F3}", 500);
                    }
                }
                else if (e.KeyCode == KeyBinds.FlashlightMoveLeft && !modifierPressed)
                {
                    flashlightOffset.Z -= moveStep;
                    FlashlightOffset = flashlightOffset;
                    GTA.UI.Screen.ShowSubtitle($"Z: {flashlightOffset.Z:F3}", 500);
                }
                else if (e.KeyCode == KeyBinds.FlashlightMoveRight && !modifierPressed)
                {
                    flashlightOffset.Z += moveStep;
                    FlashlightOffset = flashlightOffset;
                    GTA.UI.Screen.ShowSubtitle($"Z: {flashlightOffset.Z:F3}", 500);
                }
                else if (e.KeyCode == KeyBinds.FlashlightMoveUp && !modifierPressed)
                {
                    flashlightOffset.Y += moveStep;
                    FlashlightOffset = flashlightOffset;
                    GTA.UI.Screen.ShowSubtitle($"Y: {flashlightOffset.Y:F3}", 500);
                }
                else if (e.KeyCode == KeyBinds.FlashlightMoveDown && !modifierPressed)
                {
                    flashlightOffset.Y -= moveStep;
                    FlashlightOffset = flashlightOffset;
                    GTA.UI.Screen.ShowSubtitle($"Y: {flashlightOffset.Y:F3}", 500);
                }
            }
            else if (f10Enabled && flashlightVisible && flashlight != null && flashlight.Exists() && flashlightMovementLocked)
            {
                // Show locked message when trying to move flashlight while locked
                if (e.KeyCode == KeyBinds.FlashlightMoveForward || e.KeyCode == KeyBinds.FlashlightMoveBack || 
                    e.KeyCode == KeyBinds.FlashlightMoveLeft || e.KeyCode == KeyBinds.FlashlightMoveRight || 
                    e.KeyCode == KeyBinds.FlashlightMoveUp || e.KeyCode == KeyBinds.FlashlightMoveDown)
                {
                    GTA.UI.Screen.ShowSubtitle("Flashlight movement is LOCKED (Ctrl+3 to unlock)", 1500);
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
                rat = World.CreatePed(ratModel, Game.Player.Character.Position + Game.Player.Character.ForwardVector * 2);
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

        private void SpawnFlashlight()
        {
            Model flashlightModel = new Model("prop_cs_police_torch_02");
            flashlightModel.Request(500);

            if (flashlightModel.IsInCdImage && flashlightModel.IsValid)
            {
                while (!flashlightModel.IsLoaded)
                {
                    Script.Wait(100);
                }
                
                flashlight = World.CreateProp(flashlightModel, rat.Position, false, false);
                Function.Call(Hash.SET_ENTITY_COLLISION, flashlight.Handle, false, false);
                Function.Call(Hash.SET_ENTITY_HAS_GRAVITY, flashlight.Handle, false);
                Function.Call(Hash.SET_ENTITY_VISIBLE, flashlight.Handle, flashlightVisible, false);
                flashlightModel.MarkAsNoLongerNeeded();
                
                string lockStatus = flashlightMovementLocked ? " (LOCKED - Ctrl+3 to unlock)" : "";
                GTA.UI.Screen.ShowSubtitle("Flashlight spawned - Use arrow keys to position" + lockStatus, 3000);
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
            Vehicle currentVehicle = Game.Player.Character.CurrentVehicle;
            if (currentVehicle == null || !currentVehicle.Exists())
                return Vector3.Zero;

            int parentHandle = Function.Call<int>(Hash.GET_ENTITY_ATTACHED_TO, currentVehicle.Handle);
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
            return currentVehicle.Velocity;
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (saveMessageTimer > 0)
            {
                saveMessageTimer--;
                SaveMessageActive = true;
            }
            else
            {
                SaveMessageActive = false;
            }

            if (rat != null && rat.Exists())
            {
                rat.Health = 10;
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

                    if (!VRCamModConfiguration.DisableSmoothing)
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

                if (flashlight != null && flashlight.Exists())
                {
                    Function.Call(Hash.SET_ENTITY_COLLISION, flashlight.Handle, false, false);
                    Function.Call(Hash.SET_ENTITY_HAS_GRAVITY, flashlight.Handle, false);
                    
                    Vector3 ratForward = rat.ForwardVector;
                    Vector3 ratRight = rat.RightVector;
                    Vector3 ratUp = rat.UpVector;
                    
                    Vector3 flashlightWorldPos = rat.Position + 
                        ratRight * flashlightOffset.X + 
                        ratForward * flashlightOffset.Y + 
                        ratUp * flashlightOffset.Z;
                    
                    flashlight.Position = flashlightWorldPos;
                    flashlight.Rotation = rat.Rotation + flashlightRotation;
                }
            }
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