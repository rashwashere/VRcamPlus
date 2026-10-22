using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace VRCam
{
    /// <summary>
    /// One key combination: a key plus the exact modifier state it needs.
    /// Matching is exact: a bind without modifiers does not fire while Ctrl, Shift or Alt is held.
    /// </summary>
    public struct KeyBind
    {
        public Keys Key;
        public bool Ctrl, Shift, Alt;

        public KeyBind(Keys key, bool ctrl, bool shift, bool alt)
        {
            Key = key; Ctrl = ctrl; Shift = shift; Alt = alt;
        }

        public bool Matches(KeyEventArgs e)
        {
            return Key != Keys.None && e.KeyCode == Key && e.Control == Ctrl && e.Shift == Shift && e.Alt == Alt;
        }

        public bool SameAs(KeyBind other)
        {
            return Key == other.Key && Ctrl == other.Ctrl && Shift == other.Shift && Alt == other.Alt;
        }

        public override string ToString()
        {
            return KeyBinds.Format(this);
        }
    }

    /// <summary>A rebindable keyboard function: its ini name, default, menu title and help text.</summary>
    public sealed class BindDef
    {
        public readonly string IniName;
        public readonly string DefaultText;
        public readonly string Title;
        public readonly string Description;
        public KeyBind Bind;

        public BindDef(string iniName, string defaultText, string title, string description)
        {
            IniName = iniName; DefaultText = defaultText; Title = title; Description = description;
            Bind = Default;
        }

        public KeyBind Default
        {
            get { KeyBind b; KeyBinds.TryParse(DefaultText, out b); return b; }
        }

        // While GTAVive's settings menu is open its navigation keys belong to that menu, so VRCam's keys stay quiet
        public bool Pressed(KeyEventArgs e) { return Bind.Matches(e) && !GtaViveArmedFlag.IsMenuOpen(); }
        public string Text { get { return KeyBinds.Format(Bind); } }
    }

    /// <summary>
    /// Every VRCam keyboard function. Binds are stored in the [Keys] section of VRcam.ini and edited on the
    /// menu's Keys page. Text format: optional Ctrl / Shift / Alt and one key name joined with '+', for example
    /// Ctrl+F5, Ctrl+Shift+K or J. Key names: A-Z, 0-9, F1-F24, Numpad0-Numpad9, Home, End, Insert, Delete,
    /// PageUp, PageDown, Up, Down, Left, Right, Space, Tab, Enter, Esc, Backspace, Pause, NumLock, ScrollLock,
    /// CapsLock, NumpadAdd, NumpadSub, NumpadMul, NumpadDiv, NumpadDot, Comma, Period, Slash, Semicolon, Quote,
    /// LBracket, RBracket, Backslash, Minus, Equals, Grave, or a virtual-key code such as 0x5D.
    /// None (or an empty value) unbinds a function; the menu key always stays bound.
    /// </summary>
    public static class KeyBinds
    {
        // Declared before the binds so the Def() calls below can add to it during static initialisation.
        private static readonly List<BindDef> all = new List<BindDef>();
        public static IList<BindDef> All { get { return all; } }

        private static BindDef Def(string ini, string def, string title, string desc)
        {
            BindDef d = new BindDef(ini, def, title, desc);
            all.Add(d);
            return d;
        }

        // ---- main ----
        public static readonly BindDef OpenMenu = Def("OpenMenu", "Ctrl+F5", "Open / close the menu",
            "Opens and closes the VRCam menu.");
        public static readonly BindDef StartCamera = Def("StartCamera", "Ctrl+F11", "Start VRCam / toggle viewfinder",
            "Spawns the camera rat if it is missing and toggles the viewfinder. With BMX Camera Rider on, the first press also swaps control to the rider. With GTAVive, the camera rat also spawns by itself when VR starts.");
        public static readonly BindDef ShowHideRat = Def("ShowHideRat", "F5", "Show / hide the rat",
            "Makes the camera rat visible and solid so it can be found, or hides it again.");
        public static readonly BindDef LockControls = Def("LockControls", "F10", "Lock / unlock controls",
            "Master switch for the rat, flashlight and zoom controls.");
        public static readonly BindDef SavePreset = Def("SavePreset", "Ctrl+F10", "Save preset",
            "Saves the current zoom and flashlight settings as a new preset with an automatic name.");
        public static readonly BindDef NextPreset = Def("NextPreset", "Ctrl+Equals", "Next preset",
            "Loads the next saved preset.");
        public static readonly BindDef PrevPreset = Def("PrevPreset", "Ctrl+Minus", "Previous preset",
            "Loads the previous saved preset.");

        // ---- flashlight ----
        public static readonly BindDef ToggleFlashlight = Def("ToggleFlashlight", "CapsLock", "Flashlight on / off",
            "Shows or removes the flashlight (needs the camera rat).");
        public static readonly BindDef FlashlightLock = Def("FlashlightLock", "Ctrl+3", "Flashlight: lock / unlock movement",
            "Locks the flashlight position keys so the flashlight cannot be moved by accident.");
        public static readonly BindDef FlashlightForward = Def("FlashlightForward", "Up", "Flashlight: forward",
            "Moves the unlocked flashlight forward (Y).");
        public static readonly BindDef FlashlightBack = Def("FlashlightBack", "Down", "Flashlight: back",
            "Moves the unlocked flashlight back (Y).");
        public static readonly BindDef FlashlightLeft = Def("FlashlightLeft", "Left", "Flashlight: left",
            "Moves the unlocked flashlight left (X).");
        public static readonly BindDef FlashlightRight = Def("FlashlightRight", "Right", "Flashlight: right",
            "Moves the unlocked flashlight right (X).");
        public static readonly BindDef FlashlightUp = Def("FlashlightUp", "PageUp", "Flashlight: up",
            "Moves the unlocked flashlight up (Z).");
        public static readonly BindDef FlashlightDown = Def("FlashlightDown", "PageDown", "Flashlight: down",
            "Moves the unlocked flashlight down (Z).");
        public static readonly BindDef FlashlightPitchUp = Def("FlashlightPitchUp", "Ctrl+Up", "Flashlight: pitch up",
            "Tilts the unlocked flashlight up by 5 degrees.");
        public static readonly BindDef FlashlightPitchDown = Def("FlashlightPitchDown", "Ctrl+Down", "Flashlight: pitch down",
            "Tilts the unlocked flashlight down by 5 degrees.");

        // ---- zoom ----
        public static readonly BindDef ZoomModeToggle = Def("ZoomModeToggle", "Ctrl+CapsLock", "Zoom mode",
            "Cycles the mouse zoom mode: wheel, LMB / RMB click, mouse slide.");
        public static readonly BindDef ZoomInLock = Def("ZoomInLock", "Ctrl+1", "Lock zoom IN here",
            "Locks or unlocks the zoom-in limit at the current FOV.");
        public static readonly BindDef ZoomOutLock = Def("ZoomOutLock", "Ctrl+2", "Lock zoom OUT here",
            "Locks or unlocks the zoom-out limit at the current FOV.");

        // ---- viewfinder (only while the viewfinder is shown) ----
        public static readonly BindDef ViewfinderZoomOut = Def("ViewfinderZoomOut", "J", "Viewfinder: zoom out",
            "Previous viewfinder zoom level (only while the viewfinder is shown).");
        public static readonly BindDef ViewfinderZoomIn = Def("ViewfinderZoomIn", "K", "Viewfinder: zoom in",
            "Next viewfinder zoom level (only while the viewfinder is shown).");
        public static readonly BindDef AspectRatio = Def("AspectRatio", "H", "Viewfinder: aspect ratio",
            "Cycles the viewfinder frame shape (only while the viewfinder is shown).");
        public static readonly BindDef ToggleGrid = Def("ToggleGrid", "N", "Viewfinder: thirds grid",
            "Turns the rule-of-thirds grid on or off (only while the viewfinder is shown).");

        // Mouse zoom reset (mouse button, not a keyboard bind)
        public const string ZoomResetText = "Ctrl+MiddleMouse";

        // Display text for the on-screen controls list
        public static string UI_OpenMenu { get { return OpenMenu.Text; } }
        public static string UI_ToggleUI { get { return StartCamera.Text; } }
        public static string UI_ShowHideRat { get { return ShowHideRat.Text; } }
        public static string UI_LockUnlockControls { get { return LockControls.Text; } }
        public static string UI_SavePreset { get { return SavePreset.Text; } }
        public static string UI_CyclePresets { get { return NextPreset.Text + " / " + PrevPreset.Text; } }
        public static string UI_ToggleFlashlight { get { return ToggleFlashlight.Text; } }
        public static string UI_FlashlightLock { get { return FlashlightLock.Text; } }
        public static string UI_FlashlightMove { get { return FlashlightForward.Text + "/" + FlashlightBack.Text; } }
        public static string UI_FlashlightMoveLeftRight { get { return FlashlightLeft.Text + "/" + FlashlightRight.Text; } }
        public static string UI_FlashlightMoveUpDown { get { return FlashlightUp.Text + "/" + FlashlightDown.Text; } }
        public static string UI_FlashlightRotate { get { return FlashlightPitchUp.Text + "/" + FlashlightPitchDown.Text; } }
        public static string UI_ZoomModeToggle { get { return ZoomModeToggle.Text; } }
        public static string UI_ZoomReset { get { return ZoomResetText; } }
        public static string UI_ZoomInLock { get { return ZoomInLock.Text; } }
        public static string UI_ZoomOutLock { get { return ZoomOutLock.Text; } }
        public static string UI_ViewfinderZoomInOut { get { return ViewfinderZoomIn.Text + "/" + ViewfinderZoomOut.Text; } }
        public static string UI_ChangeAspectRatio { get { return AspectRatio.Text; } }
        public static string UI_ToggleGrid { get { return ToggleGrid.Text; } }

        public static BindDef Find(string iniName)
        {
            foreach (BindDef d in all)
                if (string.Equals(d.IniName, iniName, StringComparison.OrdinalIgnoreCase))
                    return d;
            return null;
        }

        /// <summary>Another bind with the same key combination, or null.</summary>
        public static BindDef FindDuplicate(BindDef def)
        {
            foreach (BindDef d in all)
                if (d != def && d.Bind.Key != Keys.None && d.Bind.SameAs(def.Bind))
                    return d;
            return null;
        }

        public static void ResetAll()
        {
            foreach (BindDef d in all)
                d.Bind = d.Default;
        }

        // ------------------------------------------------------------
        // Text format
        // ------------------------------------------------------------
        // Display spelling first; when a key has two names, the first one is written back.
        private static readonly KeyValuePair<string, int>[] Names =
        {
            Pair("Home", 0x24), Pair("End", 0x23), Pair("Insert", 0x2D), Pair("Delete", 0x2E),
            Pair("PageUp", 0x21), Pair("PageDown", 0x22), Pair("PgUp", 0x21), Pair("PgDn", 0x22),
            Pair("Up", 0x26), Pair("Down", 0x28), Pair("Left", 0x25), Pair("Right", 0x27),
            Pair("Space", 0x20), Pair("Tab", 0x09), Pair("Enter", 0x0D), Pair("Return", 0x0D),
            Pair("Esc", 0x1B), Pair("Escape", 0x1B), Pair("Backspace", 0x08), Pair("Pause", 0x13),
            Pair("NumLock", 0x90), Pair("ScrollLock", 0x91), Pair("CapsLock", 0x14), Pair("Caps", 0x14),
            Pair("NumpadAdd", 0x6B), Pair("NumpadSub", 0x6D), Pair("NumpadMul", 0x6A), Pair("NumpadDiv", 0x6F),
            Pair("NumpadDot", 0x6E),
            Pair("Comma", 0xBC), Pair("Period", 0xBE), Pair("Slash", 0xBF), Pair("Semicolon", 0xBA),
            Pair("Quote", 0xDE), Pair("LBracket", 0xDB), Pair("RBracket", 0xDD), Pair("Backslash", 0xDC),
            Pair("Minus", 0xBD), Pair("Equals", 0xBB), Pair("Grave", 0xC0), Pair("Tilde", 0xC0),
        };

        private static KeyValuePair<string, int> Pair(string name, int vk)
        {
            return new KeyValuePair<string, int>(name, vk);
        }

        /// <summary>True for Ctrl, Shift, Alt and Windows keys, which can only be modifiers.</summary>
        public static bool IsModifierKey(Keys k)
        {
            switch (k)
            {
                case Keys.ControlKey: case Keys.LControlKey: case Keys.RControlKey:
                case Keys.ShiftKey: case Keys.LShiftKey: case Keys.RShiftKey:
                case Keys.Menu: case Keys.LMenu: case Keys.RMenu:
                case Keys.LWin: case Keys.RWin:
                    return true;
            }
            return false;
        }

        private static int NameToVk(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            if (name.Length == 1)
            {
                char c = char.ToUpperInvariant(name[0]);
                if (c >= 'A' && c <= 'Z') return c;
                if (c >= '0' && c <= '9') return c;
            }
            if ((name[0] == 'F' || name[0] == 'f') && name.Length >= 2 && name.Length <= 3)
            {
                int n;
                if (int.TryParse(name.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out n) && n >= 1 && n <= 24)
                    return 0x70 + n - 1;
            }
            if (name.Length == 7 && name.StartsWith("numpad", StringComparison.OrdinalIgnoreCase) && char.IsDigit(name[6]))
                return 0x60 + (name[6] - '0');
            foreach (var p in Names)
                if (string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase))
                    return p.Value;
            if (name.Length > 2 && name[0] == '0' && (name[1] == 'x' || name[1] == 'X'))
            {
                int vk;
                if (int.TryParse(name.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out vk))
                    return vk > 0 && vk < 256 ? vk : 0;
                return 0;
            }
            int dec;
            if (int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out dec))
                return dec > 0 && dec < 256 ? dec : 0;
            return 0;
        }

        private static string VkName(int vk)
        {
            if ((vk >= 'A' && vk <= 'Z') || (vk >= '0' && vk <= '9')) return ((char)vk).ToString();
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x70 + 1);
            if (vk >= 0x60 && vk <= 0x69) return "Numpad" + (vk - 0x60);
            foreach (var p in Names)
                if (p.Value == vk)
                    return p.Key;
            return "0x" + vk.ToString("X2", CultureInfo.InvariantCulture);
        }

        /// <summary>Parses "Ctrl+Shift+K". Returns false (bind = none) when no usable key is named.</summary>
        public static bool TryParse(string text, out KeyBind bind)
        {
            bind = new KeyBind(Keys.None, false, false, false);
            if (string.IsNullOrWhiteSpace(text)) return false;
            KeyBind b = new KeyBind(Keys.None, false, false, false);
            foreach (string raw in text.Split('+'))
            {
                string t = raw.Replace(" ", "").Replace("\t", "");
                if (t.Length == 0) continue;
                if (t.Equals("ctrl", StringComparison.OrdinalIgnoreCase) || t.Equals("control", StringComparison.OrdinalIgnoreCase))
                    b.Ctrl = true;
                else if (t.Equals("shift", StringComparison.OrdinalIgnoreCase))
                    b.Shift = true;
                else if (t.Equals("alt", StringComparison.OrdinalIgnoreCase))
                    b.Alt = true;
                else
                    b.Key = (Keys)NameToVk(t);
            }
            if (b.Key == Keys.None) return false;
            bind = b;
            return true;
        }

        public static string Format(KeyBind b)
        {
            if (b.Key == Keys.None) return "None";
            return (b.Ctrl ? "Ctrl+" : "") + (b.Shift ? "Shift+" : "") + (b.Alt ? "Alt+" : "") + VkName((int)b.Key);
        }

        /// <summary>True for an empty value or "None": the function has no key.</summary>
        public static bool IsNoneText(string text)
        {
            return string.IsNullOrWhiteSpace(text) || string.Equals(text.Trim(), "None", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Applies one [Keys] line from VRcam.ini. Unknown names are ignored. Empty or "None" unbinds the function
        /// (except the menu key, which always stays bound); unreadable values keep the default.
        /// </summary>
        public static void LoadEntry(string iniName, string value)
        {
            BindDef d = Find(iniName);
            if (d == null) return;
            KeyBind b;
            if (IsNoneText(value))
            {
                if (d == OpenMenu)
                {
                    d.Bind = d.Default;
                    Trace.Event("Key OpenMenu cannot be None - using " + d.DefaultText);
                }
                else
                    d.Bind = new KeyBind(Keys.None, false, false, false);
                return;
            }
            d.Bind = TryParse(value, out b) ? b : d.Default;
        }

        /// <summary>The [Keys] section as ini lines.</summary>
        public static List<string> IniLines()
        {
            List<string> lines = new List<string>();
            lines.Add("[Keys]");
            lines.Add("; Optional Ctrl / Shift / Alt plus one key, joined with '+' (e.g. Ctrl+F5, Ctrl+Shift+K, J).");
            lines.Add("; Matching is exact: a bind without modifiers does not fire while Ctrl, Shift or Alt is held.");
            lines.Add("; Key names: A-Z, 0-9, F1-F24, Numpad0-9, Home, End, Insert, Delete, PageUp, PageDown, Up, Down, Left,");
            lines.Add("; Right, Space, Tab, Enter, Esc, Backspace, Pause, NumLock, ScrollLock, CapsLock, NumpadAdd, NumpadSub,");
            lines.Add("; NumpadMul, NumpadDiv, NumpadDot, Comma, Period, Slash, Semicolon, Quote, LBracket, RBracket, Backslash,");
            lines.Add("; Minus, Equals, Grave, or a virtual-key code such as 0x5D.");
            lines.Add("; Set a key to None (or leave it empty) to unbind it; the menu key always stays bound.");
            foreach (BindDef d in all)
                lines.Add(d.IniName + "=" + Format(d.Bind));
            return lines;
        }
    }
}
