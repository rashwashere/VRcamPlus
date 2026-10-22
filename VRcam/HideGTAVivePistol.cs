using System;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

namespace VRCam
{
    /// <summary>
    /// Deletes the pistol prop GTAVive places in the player's hand when its VR mode starts, and again when its
    /// EnterVR key re-creates it during VR (that key is read from gtaovr\gtavive.ini [Keys] EnterVR, default
    /// Ctrl+L; the file is only read, never written).
    ///
    /// Uses the GET_CLOSEST_OBJECT_OF_TYPE native (30 m) instead of
    /// World.GetAllProps/GetNearbyProps: enumerating pool props assigns script
    /// GUIDs to all of them, which exhausts the fwScriptGuid pool and makes
    /// ScriptHookVDotNet throw in FwScriptGuidPoolTask.Run(). The prop always
    /// spawns at the player, so a 30 m radius is plenty. Everything is wrapped
    /// in try/catch so a busy pool can never abort the script.
    /// </summary>
    public class HideGTAVivePistol : Script
    {
        // Model hash of the hand pistol prop (w_pi_pistol).
        private const int TargetPropHash = unchecked((int)0x5778A9B1);

        // Set by the menu ("Hide Hand Pistol Now") and when VR starts.
        public static bool DeleteNowRequested = false;

        // Nonzero = delete once this game time is reached.
        private static int pendingDeleteTime = 0;

        public HideGTAVivePistol()
        {
            KeyDown += OnKeyDown;
            Tick += OnTick;
        }

        /// <summary>Deletes the prop shortly from now (GTAVive may still be creating it).</summary>
        public static void RequestDeleteSoon()
        {
            pendingDeleteTime = Game.GameTime + 150;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (VRCamMenu.SuppressHotkeys || !VRCamModConfiguration.AutoHidePistol)
                return;

            // GTAVive's EnterVR key creates the prop; delete it shortly after.
            if (GtaViveArmedFlag.IsEnterVrKey(e))
                RequestDeleteSoon();
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (DeleteNowRequested)
            {
                DeleteNowRequested = false;
                DeleteNearestSpecificProp();
            }

            if (pendingDeleteTime != 0 && Game.GameTime >= pendingDeleteTime)
            {
                pendingDeleteTime = 0;
                DeleteNearestSpecificProp();
            }
        }

        private void DeleteNearestSpecificProp()
        {
            try
            {
                Ped player = Game.Player.Character;
                if (player == null || !Safe.Exists(player))
                    return;
                Vector3 playerPos = player.Position;

                // GET_CLOSEST_OBJECT_OF_TYPE is a plain native; World.GetNearbyProps
                // walks the entity pool in memory, which crashes on GTA V Enhanced.
                int handle = Function.Call<int>(Hash.GET_CLOSEST_OBJECT_OF_TYPE,
                    playerPos.X, playerPos.Y, playerPos.Z, 30f, TargetPropHash, false, false, false);
                if (handle != 0)
                {
                    Trace.Event("Hand pistol: deleting prop " + handle);
                    OutputArgument h = new OutputArgument(handle);
                    Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, handle, true, true);
                    Function.Call(Hash.DELETE_OBJECT, h);
                }
            }
            catch
            {
                // Entity pool busy - stay silent, the next request tries again.
            }
        }
    }
}
