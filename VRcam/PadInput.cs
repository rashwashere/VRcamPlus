using System;
using GTA;
using GTA.Native;

namespace VRCam
{
    /// <summary>
    /// Gamepad layer (Xbox / PlayStation - the game abstracts the pad, so the
    /// same physical positions work on both: A/Cross, B/Circle, LB/L1, RB/R1,
    /// LT/L2, RT/R2).
    ///
    ///   D-pad Right (hold) + A   open the VRCam menu   (always available)
    ///   B                        back / close          (handled by the menu)
    ///   LB                       flashlight on/off
    ///   RB                       lock / unlock the flashlight position (on foot only - RB is
    ///                            the handbrake in a vehicle and stays that)
    ///   D-pad                    move the flashlight while it is visible and
    ///                            its movement is unlocked
    ///   Right stick / triggers   zoom, see CameraZoom.HandlePadZoom
    ///
    /// Never touched: Start, Back/Select, Guide, A (run), Y (enter vehicle),
    /// X, the left stick and the sticks' clicks - moving around stays normal.
    /// Everything except opening the menu waits for VRCam to be started.
    /// </summary>
    public class PadInput : Script
    {
        // Frontend control ids: the pad-facing names of the physical buttons
        public const int DPAD_UP = 188, DPAD_DOWN = 187, DPAD_LEFT = 189, DPAD_RIGHT = 190;
        public const int BTN_A = 201, BTN_B = 202, BTN_LB = 205, BTN_RB = 206, BTN_LT = 207, BTN_RT = 208;
        public const int RSTICK_Y = 221;

        private int nextDpadRepeat = 0;

        public PadInput()
        {
            Tick += OnTick;
        }

        /// <summary>True when the player's last input came from a gamepad.</summary>
        public static bool Active
        {
            get { return VRCamModConfiguration.PadEnabled && Game.LastInputMethod == InputMethod.GamePad; }
        }

        public static bool Down(int control)
        {
            return Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, 0, control);
        }

        public static bool JustDown(int control)
        {
            return Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, control);
        }

        /// <summary>Analog value, also when the control is disabled this frame.</summary>
        public static float Axis(int control)
        {
            return Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, control);
        }

        private static void Disable(params int[] controls)
        {
            foreach (int c in controls)
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, c, true);
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!Active || VRCamMenu.IsTyping)
                return;

            // --- open the menu: hold D-pad Right, press A ---
            if (!VRCamMenu.IsOpen)
            {
                if (Down(DPAD_RIGHT))
                {
                    // While the combo is being held the character must stay put: no
                    // walking off a roof, no sprint / jump / climb / enter from the A press.
                    Disable(30, 31, 32, 33, 34, 35,   // move (stick + WASD)
                            21, 22, 23, 36, 44,       // sprint, jump/climb, enter, duck, cover
                            140, 141, 142);           // melee
                    if (JustDown(BTN_A))
                    {
                        Trace.Event("Pad: D-pad Right + A -> open menu");
                        VRCamMenu.RequestOpen();
                        return;
                    }
                }
            }

            // Everything below is camera control: only once VRCam is running.
            // LB / RB keep working while the menu is open (it only uses D-pad, A, B).
            if (!AttachRatToCamera.RatActive)
                return;
            if (!CameraZoom.ZoomEnabled) // master lock (Lock controls key)
                return;

            AttachRatToCamera rig = AttachRatToCamera.Instance;
            if (rig == null)
                return;

            bool inVehicle = Function.Call<bool>(Hash.IS_PED_IN_ANY_VEHICLE, Function.Call<int>(Hash.PLAYER_PED_ID), false);

            // The "frontend" LB/RB ids are not delivered during gameplay, so read the
            // gameplay actions that live on those buttons, and take them away from
            // the game: on foot LB = weapon wheel (37), RB = cover (44).
            // --- LB: flashlight (on foot) ---
            if (!inVehicle)
            {
                Disable(37);
                if (JustDown(37) || JustDown(BTN_LB))
                {
                    Trace.Event("Pad: LB -> toggle flashlight");
                    rig.Menu_ToggleFlashlight();
                }
            }

            // --- RB: lock / unlock the flashlight's position (on foot; RB = handbrake in a vehicle) ---
            // Unlocked = the D-pad moves the flashlight; locked = the D-pad is the game's again.
            if (!inVehicle)
            {
                Disable(44); // cover
                if (JustDown(44) || JustDown(BTN_RB))
                {
                    Trace.Event("Pad: RB -> toggle flashlight movement lock");
                    rig.Menu_ToggleFlashlightLock();
                }
            }

            // --- D-pad (8-way): move the flashlight, Up/Down = front/back, Left/Right = left/right ---
            // (not while the menu is open - it navigates with the D-pad)
            if (!VRCamMenu.IsOpen && AttachRatToCamera.FlashlightVisible && !AttachRatToCamera.FlashlightMovementLocked)
            {
                Disable(27, 19, 172, 173, 174, 175, 166, 167, 168, 169); // phone + character switch live on the D-pad

                bool up = Down(DPAD_UP), down = Down(DPAD_DOWN), left = Down(DPAD_LEFT), right = Down(DPAD_RIGHT);
                if (up || down || left || right)
                {
                    if (Game.GameTime >= nextDpadRepeat)
                    {
                        nextDpadRepeat = Game.GameTime + 70;
                        rig.Pad_NudgeFlashlight((up ? 1 : 0) - (down ? 1 : 0), (right ? 1 : 0) - (left ? 1 : 0));
                    }
                }
                else
                    nextDpadRepeat = 0;
            }
        }
    }
}
