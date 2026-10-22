using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace VRCam
{
    /// <summary>
    /// VRCam's own shared block for the menu link with GTAVive. VRCam is the only writer; GTAVive only reads it.
    ///
    ///   name   Local\vrcam_state_v1, 32 bytes
    ///   +0   u32  magic        'VCS1' = 0x31534356, written last when the block is first set up
    ///   +4   u32  version      1
    ///   +8   u32  pid          the game's process id
    ///   +12  u32  menuKey      VRCam's OpenMenu key: virtual-key code in bits 0-7, bit 8 Ctrl, bit 9 Shift, bit 10 Alt
    ///   +16  u64  heartbeatMs  GetTickCount64() at the last menu tick (kept fresh while the text input box is open)
    ///   +24  u32  openGtavr    incremented each time the "GTAVR Settings" row is selected: GTAVive opens its menu
    ///   +28  u32  menuOpen     1 while VRCam's menu or its text input box is open, else 0
    ///
    /// GTAVive treats the block as valid only when magic, version and pid match and the heartbeat is under 2 seconds
    /// old. While it is valid and menuKey equals GTAVive's own menu key, that key opens only VRCam's menu.
    /// openGtavr is never reset, so a script reload does not read as a request.
    /// </summary>
    public static class VrCamState
    {
        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        private const uint Magic = 0x31534356;
        private const uint KeyCtrl = 0x100, KeyShift = 0x200, KeyAlt = 0x400;

        private static readonly object sync = new object();
        private static MemoryMappedFile map;
        private static MemoryMappedViewAccessor view;
        private static bool tried;
        private static Timer keepAlive;

        private static bool Ready()
        {
            if (view != null) return true;
            if (tried) return false;
            tried = true;
            try
            {
                map = MemoryMappedFile.CreateOrOpen("Local\\vrcam_state_v1", 32, MemoryMappedFileAccess.ReadWrite);
                view = map.CreateViewAccessor(0, 32, MemoryMappedFileAccess.ReadWrite);
                view.Write(4, 1u);
                view.Write(8, (uint)System.Diagnostics.Process.GetCurrentProcess().Id);
                view.Write(16, GetTickCount64());
                Thread.MemoryBarrier();
                view.Write(0, Magic);
                return true;
            }
            catch (Exception ex)
            {
                Trace.Event("vrcam_state_v1 not available: " + ex.Message);
                if (view != null) { view.Dispose(); view = null; }
                if (map != null) { map.Dispose(); map = null; }
                return false;
            }
        }

        /// <summary>Call once per menu tick.</summary>
        public static void Publish(KeyBind menuKey, bool menuOpen)
        {
            lock (sync)
            {
                if (!Ready()) return;
                try
                {
                    uint key = ((uint)menuKey.Key & 0xFF) | (menuKey.Ctrl ? KeyCtrl : 0) |
                               (menuKey.Shift ? KeyShift : 0) | (menuKey.Alt ? KeyAlt : 0);
                    view.Write(12, key);
                    view.Write(28, menuOpen ? 1u : 0u);
                    view.Write(16, GetTickCount64());
                }
                catch { }
            }
        }

        /// <summary>Asks GTAVive to open its settings menu.</summary>
        public static void RequestGtavrMenu()
        {
            lock (sync)
            {
                if (!Ready()) return;
                try { view.Write(24, view.ReadUInt32(24) + 1); }
                catch { }
            }
        }

        /// <summary>
        /// The game's text input box stops the menu tick while it is open; this keeps the heartbeat fresh until
        /// EndInputBox, so GTAVive does not take the menu key back meanwhile.
        /// </summary>
        public static void BeginInputBox()
        {
            lock (sync)
            {
                if (keepAlive == null)
                    keepAlive = new Timer(_ => Beat(), null, 0, 500);
            }
        }

        public static void EndInputBox()
        {
            lock (sync)
            {
                if (keepAlive != null) { keepAlive.Dispose(); keepAlive = null; }
            }
        }

        private static void Beat()
        {
            lock (sync)
            {
                if (view == null) return;
                try { view.Write(16, GetTickCount64()); }
                catch { }
            }
        }

        /// <summary>Script stopped: GTAVive sees VRCam as absent at once.</summary>
        public static void Stop()
        {
            EndInputBox();
            lock (sync)
            {
                if (view == null) return;
                try
                {
                    view.Write(28, 0u);
                    view.Write(16, 0UL);
                }
                catch { }
            }
        }
    }
}
