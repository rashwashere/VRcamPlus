using System;
using GTA;
using GTA.Native;

namespace VRCam
{
    /// <summary>
    /// One notification per game start when the GTAVive VR mod (GTAVive.asi) is not loaded: shown about
    /// 10 seconds after the game is playable (no loading screen, no fade, a player ped exists).
    /// Turned off with ShowCompanionTip=0 in VRcam.ini or the menu's General page.
    /// </summary>
    public class CompanionTip : Script
    {
        private const int DelayMs = 10000;
        private const string Message = "VRCam works best with GTAVR (VR for GTA V). Get it at github.com/rashwashere/GTAVR";

        private bool done = false;
        private int playableSince = -1;

        public CompanionTip()
        {
            Interval = 500;
            Tick += OnTick;
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (done)
                return;
            if (!VRCamModConfiguration.ShowCompanionTip)
            {
                done = true;
                return;
            }

            bool playable = !Function.Call<bool>(Hash.GET_IS_LOADING_SCREEN_ACTIVE)
                && !Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT)
                && Function.Call<bool>(Hash.DOES_ENTITY_EXIST, Function.Call<int>(Hash.PLAYER_PED_ID));
            if (!playable)
            {
                playableSince = -1;
                return;
            }
            if (playableSince < 0)
            {
                playableSince = Game.GameTime;
                return;
            }
            if (Game.GameTime - playableSince < DelayMs)
                return;

            done = true;
            if (GtaViveArmedFlag.IsLoaded)
                return;
            Trace.Event("GTAVive.asi not loaded - showing the companion tip");
            GTA.UI.Notification.Show(Message);
        }
    }
}
