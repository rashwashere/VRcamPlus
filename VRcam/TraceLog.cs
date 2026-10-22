using System;
using System.Globalization;
using System.IO;
using GTA;
using GTA.Math;
using GTA.Native;

namespace VRCam
{
    /// <summary>
    /// Diagnostic recorder: every notable action (key press, spawn, mode change,
    /// player swap...) and a status sample every ~100 ms go to VRcam_trace.log
    /// in the game folder, timestamped so they line up with GTAVive's own
    /// log. Toggle with TraceLog=true/false in VRcam.ini.
    /// </summary>
    public static class Trace
    {
        private const string File = "VRcam_trace.log";
        private static int lastSample = 0;
        private static bool headerWritten = false;

        public static bool Enabled { get { return VRCamModConfiguration.TraceLog; } }

        private static string Stamp()
        {
            return DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " t=" + Game.GameTime;
        }

        private static void Write(string line)
        {
            try
            {
                if (!headerWritten)
                {
                    headerWritten = true;

                    // 10 samples a second is ~5 MB an hour: start over once it passes
                    // 20 MB, keeping the previous run as VRcam_trace.old.log
                    try
                    {
                        FileInfo info = new FileInfo(File);
                        if (info.Exists && info.Length > 20L * 1024 * 1024)
                        {
                            string old = "VRcam_trace.old.log";
                            if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
                            System.IO.File.Move(File, old);
                        }
                    }
                    catch { }
                    System.IO.File.AppendAllText(File, "\r\n===== VRCam trace started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " =====\r\n");
                }
                System.IO.File.AppendAllText(File, Stamp() + "  " + line + "\r\n");
            }
            catch { }
        }

        public static void Event(string what)
        {
            if (Enabled) Write("EVENT  " + what);
        }

        private static string V(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:F2},{1:F2},{2:F2})", v.X, v.Y, v.Z);
        }

        /// <summary>Status line, at most every 100 ms. Natives only.</summary>
        public static void Sample(Ped rat, Prop flashlight)
        {
            if (!Enabled || Game.GameTime - lastSample < 100)
                return;
            lastSample = Game.GameTime;

            try
            {
                int player = Function.Call<int>(Hash.PLAYER_PED_ID);
                Vector3 playerPos = Safe.Position(player);
                bool inVeh = Function.Call<bool>(Hash.IS_PED_IN_ANY_VEHICLE, player, false);
                int veh = inVeh ? Function.Call<int>(Hash.GET_VEHICLE_PED_IS_IN, player, false) : 0;
                bool visible = Function.Call<bool>(Hash.IS_ENTITY_VISIBLE, player);
                bool frozen = false;
                int attachedTo = Function.Call<int>(Hash.GET_ENTITY_ATTACHED_TO, player);

                int cam = Function.Call<int>(Hash.GET_RENDERING_CAM);
                bool scriptCams = Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT) == false; // placeholder, kept simple
                Vector3 camPos = Function.Call<Vector3>(Hash.GET_FINAL_RENDERED_CAM_COORD);
                Vector3 camRot = Function.Call<Vector3>(Hash.GET_FINAL_RENDERED_CAM_ROT, 2);
                float fov = Function.Call<float>(Hash.GET_FINAL_RENDERED_CAM_FOV);

                float water = 0f;
                OutputArgument oWater = new OutputArgument();
                bool hasWater = Function.Call<bool>(Hash.GET_WATER_HEIGHT, camPos.X, camPos.Y, camPos.Z, oWater);
                if (hasWater) water = oWater.GetResult<float>();

                string ratStr = "none";
                if (rat != null && Safe.Exists(rat))
                    ratStr = rat.Handle + "@" + V(rat.Position) + " d=" + camPos.DistanceTo(rat.Position).ToString("F2", CultureInfo.InvariantCulture);
                string flStr = flashlight != null && Safe.Exists(flashlight) ? flashlight.Handle.ToString() : "none";

                Write(string.Format(CultureInfo.InvariantCulture,
                    "SAMPLE player={0} pos={1} vis={2} inVeh={3} veh={4} attachedTo={5} | cam={6} pos={7} rot={8} fov={9:F1} camToPlayer={10:F2} water={11} | rat={12} flash={13} zoomMode={14} ratActive={15} fps={16:F0} | pad={17} padZoom={18} rsY={19:F2} lt={20:F2} rt={21:F2} menu={22}",
                    player, V(playerPos), visible ? 1 : 0, inVeh ? 1 : 0, veh, attachedTo,
                    cam, V(camPos), V(camRot), fov, camPos.DistanceTo(playerPos),
                    hasWater ? water.ToString("F2", CultureInfo.InvariantCulture) + (camPos.Z < water ? " UNDER" : "") : "-",
                    ratStr, flStr, CameraZoom.ZoomMode, AttachRatToCamera.RatActive ? 1 : 0, Game.FPS,
                    PadInput.Active ? 1 : 0, VRCamModConfiguration.PadZoomMode, PadInput.Axis(PadInput.RSTICK_Y),
                    Math.Max(PadInput.Axis(PadInput.BTN_LT), PadInput.Axis(25)), Math.Max(PadInput.Axis(PadInput.BTN_RT), PadInput.Axis(24)),
                    VRCamMenu.IsOpen ? 1 : 0));
            }
            catch (Exception ex)
            {
                Write("SAMPLE failed: " + ex.GetType().Name + " " + ex.Message);
            }
        }
    }
}
