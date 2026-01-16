using System;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;

namespace VRCam
{
    public class AttachRatToCamera : Script
    {
        Ped rat;
        Vector3 offset = new Vector3(0.0f, 0.0f, 0.0f); // No additional offset

        // Adjust this value to fine-tune how much the parent's inertia influences the rat.
        private float vehicleInertiaCompensationFactor = 0.18f;

        // F5/F10 control variables
        private bool f10Enabled = true; // F10 controls whether F5 and CapsLock work
        private bool visibilityCollisionEnabled = false; // F5 controls this

        // Flashlight object variables
        private Prop flashlight;
        private bool flashlightVisible = false;
        private Vector3 flashlightOffset = new Vector3(0.0f, 0.0f, 0.0f); // Position offset from rat
        private Vector3 flashlightRotation = new Vector3(0.0f, 0.0f, 0.0f); // Rotation offset
        private float moveStep = 0.01f; // How much to move per key press
        private float rotateStep = 5.0f; // Degrees to rotate per key press

        // GUI control - synced with viewfinder
        private bool showGUI = false; // Starts hidden, toggled by L
        
        // Save settings tracking
        private int saveMessageTimer = 0;

        public AttachRatToCamera()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Check if any modifier key is pressed
            bool modifierPressed = e.Control || e.Shift || e.Alt;

            if (e.KeyCode == Keys.L && !modifierPressed) // Toggle rat spawn AND GUI visibility (no modifiers)
            {
                Script.Wait(100); // Fix invisible indoor issue

                if (rat == null || !rat.Exists())
                {
                    SpawnRat();
                    showGUI = true; // Show GUI when rat is spawned
                }
                else
                {
                    rat.Delete();
                    rat = null;
                    showGUI = false; // Hide GUI when rat is deleted
                    
                    // Also delete flashlight if it exists
                    if (flashlight != null && flashlight.Exists())
                    {
                        flashlight.Delete();
                        flashlight = null;
                    }
                }
            }
            else if (e.KeyCode == Keys.F10 && e.Control && !e.Shift && !e.Alt) // Ctrl+F10 = Save settings (only when unlocked)
            {
                if (f10Enabled)
                {
                    // Save flashlight offset and rotation to configuration
                    // This would require implementing save functionality in VRCamModConfiguration
                    saveMessageTimer = 120; // Show message for 2 seconds (at 60fps)
                    GTA.UI.Screen.ShowSubtitle("Settings saved!", 2000);
                }
                else
                {
                    GTA.UI.Screen.ShowSubtitle("Save is locked (press F10 to unlock)", 2000);
                }
            }
            else if (e.KeyCode == Keys.F10 && !modifierPressed) // Toggle F5, CapsLock, and arrow key functionality (no modifiers)
            {
                f10Enabled = !f10Enabled;
                
                // Also sync zoom enabled state
                CameraZoom.ZoomEnabled = f10Enabled;
                
                string state = f10Enabled ? "enabled" : "disabled";
                GTA.UI.Screen.ShowSubtitle("F5/CapsLock/Arrow/Zoom controls " + state, 2000);
            }
            else if (e.KeyCode == Keys.F5 && !modifierPressed && rat != null && rat.Exists()) // Toggle visibility/collision (no modifiers)
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
            else if (e.KeyCode == Keys.CapsLock && !modifierPressed && rat != null && rat.Exists()) // Toggle flashlight visibility (no modifiers)
            {
                if (f10Enabled)
                {
                    flashlightVisible = !flashlightVisible;
                    
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
            else if (e.KeyCode == Keys.U && !modifierPressed) // Toggle help display only (no modifiers)
            {
                showGUI = !showGUI;
                string state = showGUI ? "visible" : "hidden";
                GTA.UI.Screen.ShowSubtitle("GUI " + state, 1000);
            }
            // Arrow key controls for flashlight positioning (only when F10 is enabled and flashlight is visible)
            else if (f10Enabled && flashlightVisible && flashlight != null && flashlight.Exists())
            {
                if (e.KeyCode == Keys.Up)
                {
                    if (e.Control && !e.Shift && !e.Alt) // Ctrl + Up = Rotate pitch up (only Ctrl)
                    {
                        flashlightRotation.X += rotateStep;
                        GTA.UI.Screen.ShowSubtitle($"Pitch: {flashlightRotation.X:F1}°", 500);
                    }
                    else if (!e.Control && !e.Shift && !e.Alt) // Up = Move up (Z+) (no modifiers)
                    {
                        flashlightOffset.Z += moveStep;
                        GTA.UI.Screen.ShowSubtitle($"Up - Offset Z: {flashlightOffset.Z:F3}", 500);
                    }
                }
                else if (e.KeyCode == Keys.Down)
                {
                    if (e.Control && !e.Shift && !e.Alt) // Ctrl + Down = Rotate pitch down (only Ctrl)
                    {
                        flashlightRotation.X -= rotateStep;
                        GTA.UI.Screen.ShowSubtitle($"Pitch: {flashlightRotation.X:F1}°", 500);
                    }
                    else if (!e.Control && !e.Shift && !e.Alt) // Down = Move down (Z-) (no modifiers)
                    {
                        flashlightOffset.Z -= moveStep;
                        GTA.UI.Screen.ShowSubtitle($"Down - Offset Z: {flashlightOffset.Z:F3}", 500);
                    }
                }
                else if (e.KeyCode == Keys.Left && !modifierPressed) // Move left (Y-) (no modifiers)
                {
                    flashlightOffset.Y -= moveStep;
                    GTA.UI.Screen.ShowSubtitle($"Left - Offset Y: {flashlightOffset.Y:F3}", 500);
                }
                else if (e.KeyCode == Keys.Right && !modifierPressed) // Move right (Y+) (no modifiers)
                {
                    flashlightOffset.Y += moveStep;
                    GTA.UI.Screen.ShowSubtitle($"Right - Offset Y: {flashlightOffset.Y:F3}", 500);
                }
                else if (e.KeyCode == Keys.PageUp && !modifierPressed) // Move forward (X+) (no modifiers)
                {
                    flashlightOffset.X += moveStep;
                    GTA.UI.Screen.ShowSubtitle($"Forward - Offset X: {flashlightOffset.X:F3}", 500);
                }
                else if (e.KeyCode == Keys.PageDown && !modifierPressed) // Move backward (X-) (no modifiers)
                {
                    flashlightOffset.X -= moveStep;
                    GTA.UI.Screen.ShowSubtitle($"Backward - Offset X: {flashlightOffset.X:F3}", 500);
                }
            }
        }

        private void SpawnRat()
        {
            Model ratModel = new Model(PedHash.Rat);
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
                
                // Reset visibility/collision state when spawning
                visibilityCollisionEnabled = false;
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
                
                GTA.UI.Screen.ShowSubtitle("Flashlight spawned - Use arrow keys to position", 3000);
            }
        }

        // Computes the forward vector of the camera.
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

        // New helper method to get the proper inertia velocity.
        // If the player's vehicle is attached to another vehicle,
        // use the parent's velocity; otherwise, use the player's vehicle velocity.
        private Vector3 GetInertiaVelocity()
        {
            Vehicle currentVehicle = Game.Player.Character.CurrentVehicle;
            if (currentVehicle == null || !currentVehicle.Exists())
                return Vector3.Zero;

            // Get the handle of the entity attached to the current vehicle.
            int parentHandle = Function.Call<int>(Hash.GET_ENTITY_ATTACHED_TO, currentVehicle.Handle);
            if (parentHandle != 0)
            {
                int modelHash = Function.Call<int>(Hash.GET_ENTITY_MODEL, parentHandle);
                bool isVehicle = Function.Call<bool>(Hash.IS_MODEL_A_VEHICLE, modelHash);

                if (isVehicle)
                {
                    // Retrieve the parent's velocity directly using a native call.
                    Vector3 parentVelocity = Function.Call<Vector3>(Hash.GET_ENTITY_VELOCITY, parentHandle);
                    return parentVelocity;
                }
            }
            return currentVehicle.Velocity;
        }



        private void OnTick(object sender, EventArgs e)
        {
            // Decrement save message timer
            if (saveMessageTimer > 0)
                saveMessageTimer--;

            if (rat != null && rat.Exists())
            {
                // Ensure the rat remains in ragdoll state.
                rat.Health = 10;
                Function.Call(Hash.SET_ENTITY_COLLISION, rat.Handle, false, false);
                Function.Call(Hash.SET_ENTITY_HAS_GRAVITY, rat.Handle, false);
                Function.Call(Hash.SET_ENTITY_DYNAMIC, rat.Handle, true);
                Function.Call(Hash.SET_PED_TO_RAGDOLL, rat.Handle, 10000, 10000, 0, true, true, false);
                
                // F5 control: Override visibility/collision if enabled
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

                    // If the player is in a vehicle (or attached to one), adjust the target position
                    // by adding an offset based on the inertia (velocity) of the parent vehicle if available.
                    if (isInVehicle)
                    {
                        Vector3 inertiaVelocity = GetInertiaVelocity();
                        targetPos += inertiaVelocity * vehicleInertiaCompensationFactor;
                    }

                    // Update position.
                    if (!VRCamModConfiguration.DisableSmoothing)
                    {
                        Vector3 currentPos = rat.Position;
                        Vector3 delta = targetPos - currentPos;
                        Vector3 desiredVelocity = delta * activeSmoothingFactor;

                        // Also add the inertia influence using the parent's velocity if applicable.
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

                    // Update rotation.
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

                // Update flashlight position and rotation to follow rat with offset
                if (flashlight != null && flashlight.Exists())
                {
                    // Ensure flashlight has no collision or gravity
                    Function.Call(Hash.SET_ENTITY_COLLISION, flashlight.Handle, false, false);
                    Function.Call(Hash.SET_ENTITY_HAS_GRAVITY, flashlight.Handle, false);
                    
                    // Calculate flashlight world position based on rat's position and rotation
                    Vector3 ratForward = rat.ForwardVector;
                    Vector3 ratRight = rat.RightVector;
                    Vector3 ratUp = rat.UpVector;
                    
                    Vector3 flashlightWorldPos = rat.Position + 
                        ratRight * flashlightOffset.X + 
                        ratForward * flashlightOffset.Y + 
                        ratUp * flashlightOffset.Z;
                    
                    flashlight.Position = flashlightWorldPos;
                    
                    // Apply rotation (rat's rotation + offset rotation)
                    flashlight.Rotation = rat.Rotation + flashlightRotation;
                }
            }

            // Display GUI
            if (showGUI)
            {
                DisplayGUI();
            }
        }

        private void DisplayGUI()
        {
            // Top display - Flashlight offset
            string offsetText = $"Offset: X={flashlightOffset.X:F2} Y={flashlightOffset.Y:F2} Z={flashlightOffset.Z:F2}";
            DrawText(offsetText, 0.5f, 0.08f, 0.35f, 255, 255, 255, 255, true);

            // Bottom left - Status indicators (moved up to avoid minimap)
            string ratStatus = (rat != null && rat.Exists()) ? "ON" : "OFF";
            string lightStatus = flashlightVisible ? "ON" : "OFF";
            string lockStatus = f10Enabled ? "UNLOCKED" : "LOCKED";

            DrawText($"Rat: {ratStatus}", 0.02f, 0.70f, 0.35f, 255, 255, 255, 255, false);
            DrawText($"Light: {lightStatus}", 0.02f, 0.73f, 0.35f, 255, 255, 255, 255, false);
            DrawText($"Lock: {lockStatus}", 0.02f, 0.76f, 0.35f, 255, 255, 255, 255, false);

            // Bottom right - Complete controls help (adjusted to prevent cutoff)
            float startY = 0.35f;
            float lineHeight = 0.024f;
            float rightMargin = 0.88f; // Moved further from edge to prevent cutoff
            
            // Get zoom mode from CameraZoom
            string zoomControl = CameraZoom.UseClickMode ? "LMB/RMB = Zoom In/Out" : "Mouse Wheel = Zoom In/Out";
            string zoomAdjust1 = CameraZoom.UseClickMode ? "Shift+LMB/RMB = Zoom IN +/-" : "Shift+Wheel = Adjust Zoom IN Speed";
            string zoomAdjust2 = CameraZoom.UseClickMode ? "Ctrl+LMB/RMB = Zoom OUT +/-" : "Ctrl+Wheel = Adjust Zoom OUT Speed";
            
            DrawText("--- Main Controls ---", rightMargin, startY, 0.30f, 255, 255, 100, 255, true);
            DrawText("L = Spawn/Delete Rat & Toggle All UI", rightMargin, startY + lineHeight * 1, 0.27f, 255, 255, 255, 255, true);
            DrawText("U = Toggle This Help", rightMargin, startY + lineHeight * 2, 0.27f, 255, 255, 255, 255, true);
            
            DrawText("--- Rat Controls ---", rightMargin, startY + lineHeight * 3.5f, 0.30f, 255, 255, 100, 255, true);
            DrawText("F5 = Show/Hide Rat (when unlocked)", rightMargin, startY + lineHeight * 4.5f, 0.27f, 255, 255, 255, 255, true);
            DrawText("F10 = Lock/Unlock All Controls", rightMargin, startY + lineHeight * 5.5f, 0.27f, 255, 255, 255, 255, true);
            
            DrawText("--- Flashlight Controls ---", rightMargin, startY + lineHeight * 7f, 0.30f, 255, 255, 100, 255, true);
            DrawText("CapsLock = Toggle Flashlight", rightMargin, startY + lineHeight * 8f, 0.27f, 255, 255, 255, 255, true);
            DrawText("Arrows = Move Left/Right/Up/Down", rightMargin, startY + lineHeight * 9f, 0.27f, 255, 255, 255, 255, true);
            DrawText("Ctrl+Up/Down = Rotate Pitch", rightMargin, startY + lineHeight * 10f, 0.27f, 255, 255, 255, 255, true);
            DrawText("PgUp/PgDn = Move Forward/Back", rightMargin, startY + lineHeight * 11f, 0.27f, 255, 255, 255, 255, true);
            
            DrawText("--- Zoom Controls ---", rightMargin, startY + lineHeight * 12.5f, 0.30f, 255, 255, 100, 255, true);
            DrawText(zoomControl, rightMargin, startY + lineHeight * 13.5f, 0.27f, 255, 255, 255, 255, true);
            DrawText(zoomAdjust1, rightMargin, startY + lineHeight * 14.5f, 0.27f, 255, 255, 255, 255, true);
            DrawText(zoomAdjust2, rightMargin, startY + lineHeight * 15.5f, 0.27f, 255, 255, 255, 255, true);
            DrawText("Ctrl+CapsLock = Toggle Zoom Mode", rightMargin, startY + lineHeight * 16.5f, 0.27f, 255, 255, 255, 255, true);
            DrawText("Ctrl+MiddleMouse = Reset Zoom", rightMargin, startY + lineHeight * 17.5f, 0.27f, 255, 255, 255, 255, true);
            
            DrawText("--- Viewfinder Controls ---", rightMargin, startY + lineHeight * 19f, 0.30f, 255, 255, 100, 255, true);
            DrawText("J/K = Viewfinder Zoom In/Out", rightMargin, startY + lineHeight * 20f, 0.27f, 255, 255, 255, 255, true);
            DrawText("H = Change Aspect Ratio", rightMargin, startY + lineHeight * 21f, 0.27f, 255, 255, 255, 255, true);
            DrawText("U = Toggle Rule of Thirds Grid", rightMargin, startY + lineHeight * 22f, 0.27f, 255, 255, 255, 255, true);
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