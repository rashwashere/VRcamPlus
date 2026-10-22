using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using GTA.Native;

namespace VRCam
{
    /// <summary>
    /// Read-only view of the GTAVive VR mod's state. VRCam never writes to GTAVive or its files.
    ///
    /// IsLoaded: GTAVive.asi is a module of the game process.
    ///
    /// IsArmed: GTAVive currently renders the VR view. Two sources, in this order:
    ///  1. The shared-memory flag Local\gtavive_cam_v1, when GTAVive publishes it: magic 'VRC1' @0, version 1 @4,
    ///     process id @8, armed @12, heartbeat in GetTickCount64 milliseconds @16, settings menu open @24.
    ///  2. Otherwise, with GTAVive.asi loaded: a scripted camera is rendering and the player ped is hidden
    ///     (GTAVive hides the body in VR: invisible on foot, alpha 0 in a vehicle).
    /// Without GTAVive, IsArmed is always false.
    ///
    /// EnterVrBind: GTAVive's own Enter VR key, so VRCam can react to it (hand pistol hiding, BMX rider swap)
    /// without binding that key itself.
    /// </summary>
    public static class GtaViveArmedFlag
    {
        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        private static MemoryMappedFile map;
        private static MemoryMappedViewAccessor view;
        private static ulong lastTry;
        private static readonly uint myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

        private static bool loaded;
        private static ulong lastModuleCheck;
        private static bool moduleChecked;

        /// <summary>True when GTAVive.asi is loaded in the game process (checked at most every 5 seconds).</summary>
        public static bool IsLoaded
        {
            get
            {
                ulong now = GetTickCount64();
                if (!moduleChecked || now - lastModuleCheck >= 5000)
                {
                    moduleChecked = true;
                    lastModuleCheck = now;
                    try { loaded = GetModuleHandle("GTAVive.asi") != IntPtr.Zero; }
                    catch { loaded = false; }
                }
                return loaded;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern uint GetPrivateProfileString(string section, string key, string def, StringBuilder ret, uint size, string filePath);

        private static KeyBind enterVrBind;
        private static ulong enterVrReadAt;
        private static bool enterVrRead;

        /// <summary>
        /// GTAVive's EnterVR key from gtaovr\gtavive.ini [Keys] (default Ctrl+L), re-read at most every 5 seconds.
        /// The file is only read, never written.
        /// </summary>
        public static KeyBind EnterVrBind()
        {
            ulong now = GetTickCount64();
            if (!enterVrRead || now - enterVrReadAt >= 5000)
            {
                enterVrRead = true;
                enterVrReadAt = now;
                string text = "Ctrl+L";
                try
                {
                    string path = Path.GetFullPath(Path.Combine("gtaovr", "gtavive.ini"));
                    if (File.Exists(path))
                    {
                        StringBuilder sb = new StringBuilder(64);
                        GetPrivateProfileString("Keys", "EnterVR", "Ctrl+L", sb, (uint)sb.Capacity, path);
                        text = sb.ToString();
                    }
                }
                catch { }
                KeyBind b;
                if (KeyBinds.IsNoneText(text))
                    enterVrBind = new KeyBind(Keys.None, false, false, false);   // unbound in GTAVive: never matches
                else
                    enterVrBind = KeyBinds.TryParse(text, out b) ? b : new KeyBind(Keys.L, true, false, false);
            }
            return enterVrBind;
        }

        /// <summary>True when GTAVive is loaded and this key event is its EnterVR key.</summary>
        public static bool IsEnterVrKey(KeyEventArgs e)
        {
            return IsLoaded && EnterVrBind().Matches(e);
        }

        /// <summary>GTAVive's settings menu is open (flag field @24, 1 = open). False when the flag is not published.</summary>
        public static bool IsMenuOpen()
        {
            if (ReadSharedFlag() < 0 || view == null)
                return false;
            try
            {
                ulong now = GetTickCount64(), beat = view.ReadUInt64(16);
                return view.ReadUInt32(24) == 1 && now >= beat && now - beat < 2000;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsArmed()
        {
            int fromFlag = ReadSharedFlag();
            if (fromFlag >= 0)
                return fromFlag == 1;
            if (!IsLoaded)
                return false;
            try
            {
                int cam = Function.Call<int>(Hash.GET_RENDERING_CAM);
                if (cam == -1 || !Function.Call<bool>(Hash.DOES_CAM_EXIST, cam))
                    return false;
                int player = Function.Call<int>(Hash.PLAYER_PED_ID);
                return !Function.Call<bool>(Hash.IS_ENTITY_VISIBLE, player) ||
                       Function.Call<int>(Hash.GET_ENTITY_ALPHA, player) == 0;
            }
            catch
            {
                return false;
            }
        }

        // 1 = armed, 0 = not armed, -1 = no flag published (use the fallback)
        private static int ReadSharedFlag()
        {
            try
            {
                ulong now = GetTickCount64();
                if (view == null)
                {
                    if (now - lastTry < 1000) return -1;
                    lastTry = now;
                    map = MemoryMappedFile.OpenExisting("Local\\gtavive_cam_v1", MemoryMappedFileRights.Read);
                    view = map.CreateViewAccessor(0, 32, MemoryMappedFileAccess.Read);
                }
                uint magic = view.ReadUInt32(0), version = view.ReadUInt32(4), pid = view.ReadUInt32(8), armed = view.ReadUInt32(12);
                ulong beat = view.ReadUInt64(16);
                if (magic != 0x31435256u || version != 1 || pid != myPid)
                    return -1;
                return armed == 1 && now >= beat && now - beat < 2000 ? 1 : 0;
            }
            catch
            {
                // not published: retry at most once a second
                if (view != null) { view.Dispose(); view = null; }
                if (map != null) { map.Dispose(); map = null; }
                return -1;
            }
        }
    }
}
