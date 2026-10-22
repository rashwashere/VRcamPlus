using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using GTA;
using GTA.Native;

namespace VRCam
{
    // ================================================================
    // In-game settings menu drawn with UI natives: translucent dark rows,
    // cyan accent, outlined selected row, drawn checkboxes, value pills,
    // sel/total counter, scrollbar, description panel, frontend nav
    // sounds, hold-to-accelerate sliders.
    //
    // Controls: Up/Down navigate (wraps), Left/Right adjust (hold to
    // accelerate), Enter = activate / open submenu / type the exact value
    // in the GTA on-screen input box, Backspace = back, Esc = back
    // (closes only from the root page). Opens with the OpenMenu key
    // (Ctrl+F5 by default).
    // ================================================================

    public enum MenuItemKind { Button, Toggle, Float, Choice, Submenu }

    public abstract class MenuItem
    {
        public string Title;
        public string Description = "";
        public virtual MenuItemKind Kind { get { return MenuItemKind.Button; } }
        public virtual bool BoolValue { get { return false; } }
        public virtual string GetValueText() { return ""; }
        public virtual void OnLeft(float mult) { }
        public virtual void OnRight(float mult) { }
        public virtual void OnActivate(VRCamMenu menu) { }
    }

    public class SubmenuItem : MenuItem
    {
        public MenuPage Page;
        public SubmenuItem(string title, MenuPage page, string desc = "")
        {
            Title = title; Page = page; Description = desc;
        }
        public override MenuItemKind Kind { get { return MenuItemKind.Submenu; } }
        public override string GetValueText() { return ">"; }
        public override void OnActivate(VRCamMenu menu) { menu.Push(Page); }
    }

    public class ActionItem : MenuItem
    {
        private readonly Action callback;
        public ActionItem(string title, Action callback, string desc = "")
        {
            Title = title; this.callback = callback; Description = desc;
        }
        public override void OnActivate(VRCamMenu menu)
        {
            VRCamMenu.PlayNavSound("SELECT");
            callback?.Invoke();
        }
    }

    public class ToggleItem : MenuItem
    {
        private readonly Func<bool> get;
        private readonly Action<bool> set;
        public ToggleItem(string title, Func<bool> get, Action<bool> set, string desc = "")
        {
            Title = title; this.get = get; this.set = set; Description = desc;
        }
        public override MenuItemKind Kind { get { return MenuItemKind.Toggle; } }
        public override bool BoolValue { get { return get(); } }
        public override void OnLeft(float mult) { set(!get()); VRCamMenu.PlayNavSound("NAV_LEFT_RIGHT"); }
        public override void OnRight(float mult) { set(!get()); VRCamMenu.PlayNavSound("NAV_LEFT_RIGHT"); }
        public override void OnActivate(VRCamMenu menu) { set(!get()); VRCamMenu.PlayNavSound("SELECT"); }
    }

    public class FloatItem : MenuItem
    {
        private readonly Func<float> get;
        private readonly Action<float> set;
        private readonly float step, min, max;
        private readonly string format;
        public FloatItem(string title, Func<float> get, Action<float> set,
            float step, float min, float max, string format = "F2", string desc = "")
        {
            Title = title; this.get = get; this.set = set;
            this.step = step; this.min = min; this.max = max; this.format = format;
            Description = desc;
        }
        private float Clamp(float v) { return Math.Max(min, Math.Min(max, v)); }
        public override MenuItemKind Kind { get { return MenuItemKind.Float; } }
        public override string GetValueText()
        {
            return get().ToString(format, CultureInfo.InvariantCulture);
        }
        // a value already outside the range (a wider range in the ini) steps back toward it instead of
        // snapping to the edge; nudges that would move it further out are clamped
        private float Nudge(float v) { float c = get(); return (c > max && v < c) || (c < min && v > c) ? v : Clamp(v); }
        public override void OnLeft(float mult) { set(Nudge(get() - step * mult)); VRCamMenu.PlayNavSound("NAV_UP_DOWN"); }
        public override void OnRight(float mult) { set(Nudge(get() + step * mult)); VRCamMenu.PlayNavSound("NAV_UP_DOWN"); }
        public override void OnActivate(VRCamMenu menu)
        {
            // Enter = type the exact value in the GTA on-screen keyboard
            string current = get().ToString(format, CultureInfo.InvariantCulture);
            string input = menu.PromptUserInput(current);
            if (string.IsNullOrWhiteSpace(input))
                return;
            input = input.Trim().Replace(',', '.');
            float parsed;
            if (float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                set(Clamp(parsed));
                VRCamMenu.PlayNavSound("SELECT");
            }
            else
            {
                GTA.UI.Screen.ShowSubtitle("Invalid number: " + input, 1500);
            }
        }
    }

    public class ChoiceItem : MenuItem
    {
        private readonly Func<string[]> options;
        private readonly Func<int> getIndex;
        private readonly Action<int> setIndex;
        public ChoiceItem(string title, Func<string[]> options, Func<int> getIndex, Action<int> setIndex, string desc = "")
        {
            Title = title; this.options = options; this.getIndex = getIndex; this.setIndex = setIndex;
            Description = desc;
        }
        public override MenuItemKind Kind { get { return MenuItemKind.Choice; } }
        public override string GetValueText()
        {
            string[] opts = options();
            if (opts == null || opts.Length == 0)
                return "(none)";
            int i = Math.Max(0, Math.Min(opts.Length - 1, getIndex()));
            return opts[i];
        }
        private void Move(int dir)
        {
            string[] opts = options();
            if (opts == null || opts.Length == 0)
                return;
            int i = getIndex() + dir;
            if (i < 0) i = opts.Length - 1;
            if (i >= opts.Length) i = 0;
            setIndex(i);
        }
        public override void OnLeft(float mult) { Move(-1); VRCamMenu.PlayNavSound("NAV_LEFT_RIGHT"); }
        public override void OnRight(float mult) { Move(1); VRCamMenu.PlayNavSound("NAV_LEFT_RIGHT"); }
        public override void OnActivate(VRCamMenu menu) { Move(1); VRCamMenu.PlayNavSound("SELECT"); }
    }

    // Preset row: Left/Right cycles (and loads) presets; Enter opens the
    // GTA on-screen input box to RENAME the selected preset.
    public class PresetItem : MenuItem
    {
        private readonly Func<string[]> options;
        private readonly Func<int> getIndex;
        private readonly Action<int> setIndex;
        private readonly Action<VRCamMenu> onRename;
        public PresetItem(string title, Func<string[]> options, Func<int> getIndex,
            Action<int> setIndex, Action<VRCamMenu> onRename, string desc = "")
        {
            Title = title; this.options = options; this.getIndex = getIndex;
            this.setIndex = setIndex; this.onRename = onRename; Description = desc;
        }
        public override MenuItemKind Kind { get { return MenuItemKind.Choice; } }
        public override string GetValueText()
        {
            string[] opts = options();
            if (opts == null || opts.Length == 0)
                return "(none)";
            int i = Math.Max(0, Math.Min(opts.Length - 1, getIndex()));
            return opts[i];
        }
        private void Move(int dir)
        {
            string[] opts = options();
            if (opts == null || opts.Length == 0)
                return;
            int i = getIndex() + dir;
            if (i < 0) i = opts.Length - 1;
            if (i >= opts.Length) i = 0;
            setIndex(i);
        }
        public override void OnLeft(float mult) { Move(-1); VRCamMenu.PlayNavSound("NAV_LEFT_RIGHT"); }
        public override void OnRight(float mult) { Move(1); VRCamMenu.PlayNavSound("NAV_LEFT_RIGHT"); }
        public override void OnActivate(VRCamMenu menu) { onRename?.Invoke(menu); }
    }

    // Key row: shows the current bind; Enter waits for a new key combination (Esc cancels).
    public class KeyBindItem : MenuItem
    {
        public readonly BindDef Def;
        public KeyBindItem(BindDef def)
        {
            Def = def; Title = def.Title;
            Description = def.Description + (def == KeyBinds.OpenMenu
                ? " Enter, then press the new key combination (Esc cancels). This key always stays bound."
                : " Enter, then press the new key combination (Delete = None, Esc cancels).");
        }
        public override string GetValueText()
        {
            return KeyBinds.FindDuplicate(Def) != null ? Def.Text + " (!)" : Def.Text;
        }
        public override void OnActivate(VRCamMenu menu)
        {
            VRCamMenu.PlayNavSound("SELECT");
            menu.BeginKeyCapture(Def);
        }
    }

    public class MenuPage
    {
        public string Title;      // big banner text
        public string Subtitle;   // accent-bar label
        public List<MenuItem> Items = new List<MenuItem>();
        public int SelectedIndex = 0;
        public int ScrollOffset = 0;
        public Action OnOpen;
        public MenuPage(string title, string subtitle = "")
        {
            Title = title; Subtitle = subtitle;
        }
    }

    // ================================================================
    // The VRCam menu itself.
    // ================================================================
    public class VRCamMenu : Script
    {
        // --- global state other scripts check to suppress their hotkeys ---
        public static bool IsOpen { get; private set; } = false;
        private static int ignoreKeysUntil = 0;
        private static bool typing = false;
        public static bool SuppressHotkeys
        {
            get { return IsOpen || typing || Game.GameTime < ignoreKeysUntil; }
        }
        // True while the GTA on-screen input box is open. Background scripts
        // that continuously write values pause on this, so nothing changes
        // until the typed value is committed.
        public static bool IsTyping
        {
            get { return typing; }
        }

        // ---------------- theme ----------------
        // Layout metrics in design px on a 1920x1080 grid.
        private const float BaseW = 1920f, BaseH = 1080f;
        private const float WidthPx = 432f;
        private const float RowHeightPx = 38f;
        private const float TitleHeightPx = 76f;
        private const float SubtitleHeightPx = 38f;
        private const float DescMinHeightPx = 44f;
        private const float ScrollbarWidthPx = 6f;
        private const float AccentBarWidthPx = 3f;
        private const float PadXPx = 12f;
        private const float OutlinePx = 1.4f;
        private const int MaxVisibleRows = 12;

        private const int TitleFont = 1;      // House Script
        private const float TitleScale = 0.95f;
        private const int RowFont = 4;        // Chalet Comprime
        private const float RowTextScale = 0.345f;
        private const int SubFont = 0;
        private const float SubScale = 0.34f;
        private const float DescScale = 0.30f;

        // Position (top-left anchor, screen fractions).
        private const float PosX = 0.02f;
        private const float PosY = 0.07f;

        // Palette.
        private static readonly int[] TitleBg = { 17, 19, 24, 245 };
        private static readonly int[] TitleText = { 238, 242, 247, 255 };
        private static readonly int[] Accent = { 60, 200, 220, 255 };
        private static readonly int[] SubtitleBg = { 20, 23, 28, 245 };
        private static readonly int[] SubtitleText = { 150, 200, 210, 255 };
        private static readonly int[] RowBg = { 17, 19, 24, 230 };
        private static readonly int[] RowText = { 205, 210, 218, 255 };
        private static readonly int[] RowSelBg = { 30, 80, 95, 235 };
        private static readonly int[] RowSelText = { 240, 245, 250, 255 };
        private static readonly int[] DescBg = { 13, 15, 19, 240 };
        private static readonly int[] DescText = { 205, 210, 218, 255 };
        private static readonly int[] ScrollTrack = { 0, 0, 0, 120 };
        private static readonly int[] CheckboxBorder = { 90, 96, 104, 255 };
        private static readonly int[] ValuePillBg = { 0, 0, 0, 70 };
        private static readonly int[] FooterBg = { 0, 0, 0, 235 };
        private static readonly int[] FooterText = { 165, 172, 182, 255 };
        private static readonly int[] CaptureText = { 255, 200, 90, 255 };

        private readonly Stack<MenuPage> pageStack = new Stack<MenuPage>();
        private MenuPage root;

        private string[] presetCache = new string[0];
        private int presetIndex = -1;

        // input repeat/acceleration state
        private class RepeatState { public bool down; public int nextRepeat; }
        private readonly RepeatState rUp = new RepeatState();
        private readonly RepeatState rDown = new RepeatState();
        private readonly RepeatState rLeft = new RepeatState();
        private readonly RepeatState rRight = new RepeatState();
        private int holdStart = 0;
        private bool wasAdjusting = false;

        // key capture (Keys page): the bind waiting for a new combination, and when key presses start to count
        private BindDef captureDef = null;
        private int captureArmAt = 0;

        // description wrap cache (recomputing native text widths every frame is wasteful)
        private string descCacheText = null;
        private List<string> descCacheLines = new List<string>();

        public VRCamMenu()
        {
            BuildMenu();
            Tick += OnTick;
            KeyDown += OnKeyDown;
        }

        public static void PlayNavSound(string name)
        {
            Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, name, "HUD_FRONTEND_DEFAULT_SOUNDSET", 0);
        }

        // ------------------------------------------------------------
        // Menu tree
        // ------------------------------------------------------------
        private void BuildMenu()
        {
            // --- Camera ---
            MenuPage camera = new MenuPage("VRCam", "CAMERA");
            camera.Items.Add(new FloatItem("FOV",
                () => CameraZoom.TargetFOV,
                v => CameraZoom.Menu_SetFov(v),
                1.0f, CameraZoom.EngineMinFov, CameraZoom.EngineMaxFov, "F1",
                "Camera field of view. Lower = zoomed in (1 = the game's tightest: a face across a street; 3-5 = a face from a few metres). Hold Left/Right to accelerate, Enter to type a value."));
            camera.Items.Add(new FloatItem("Zoom IN Range (tightest FOV)",
                () => CameraZoom.MinFOV,
                v => CameraZoom.Menu_SetRangeMin(v),
                0.5f, CameraZoom.EngineMinFov, 10f, "F1",
                "How far the zoom can go in. 1 = as far as the game allows. Below 10 the zoom slows to an even feel, so tight shots stay easy to frame. The gamepad Stick: Direct and trigger-pressure modes stop at 10 (their travel can't cover both); the wheel, click, Mouse Slide, Stick: Default and Triggers: Default go all the way."));
            camera.Items.Add(new FloatItem("Zoom OUT Range (widest FOV)",
                () => CameraZoom.MaxFOV,
                v => CameraZoom.Menu_SetRangeMax(v),
                1f, 60f, CameraZoom.EngineMaxFov, "F0",
                "How far the zoom can go out, up to 130 (the game's widest). Mouse Slide and the trigger-pressure modes reach this at fully out."));
            camera.Items.Add(new FloatItem("Zoom IN Smoothness",
                () => CameraZoom.ZoomInSpeed,
                v => CameraZoom.Menu_SetZoomInSmooth(v),
                0.01f, 0.01f, 1.0f, "F2",
                "How fast the camera eases when zooming in. Lower = slower, smoother."));
            camera.Items.Add(new FloatItem("Zoom OUT Smoothness",
                () => CameraZoom.ZoomOutSpeed,
                v => CameraZoom.Menu_SetZoomOutSmooth(v),
                0.01f, 0.01f, 1.0f, "F2",
                "How fast the camera eases when zooming out. Lower = slower, smoother."));
            camera.Items.Add(new ChoiceItem("Zoom Mode",
                () => new string[] { "Mouse Wheel", "LMB / RMB Click", "Mouse Slide" },
                () => CameraZoom.ZoomMode,
                i => CameraZoom.Menu_SetZoomMode(i),
                "Wheel = scroll zooms. Click = LMB zooms in, RMB out. Slide = push the mouse forward/back; its travel is a 0-100% zoom slider between the limits. The Zoom mode key cycles these."));
            camera.Items.Add(new FloatItem("Slide Sensitivity",
                () => CameraZoom.SlideSensitivity,
                v => CameraZoom.Menu_SetSlideSensitivity(v),
                0.05f, 0.02f, 3.0f, "F2",
                "Mouse Slide only: how much mouse travel covers the full zoom range. Lower = more travel = finer control."));
            camera.Items.Add(new ToggleItem("Zoom IN Lock",
                () => CameraZoom.ZoomInLocked,
                v => CameraZoom.Menu_SetZoomInLock(v),
                "Stops zooming in past the Soft Min FOV below."));
            camera.Items.Add(new FloatItem("Soft Min FOV (IN limit)",
                () => CameraZoom.SoftMinFOV,
                v => CameraZoom.Menu_SetSoftMin(v),
                0.5f, CameraZoom.EngineMinFov, CameraZoom.EngineMaxFov, "F1",
                "FOV the zoom stops at while Zoom IN Lock is on."));
            camera.Items.Add(new ToggleItem("Zoom OUT Lock",
                () => CameraZoom.ZoomOutLocked,
                v => CameraZoom.Menu_SetZoomOutLock(v),
                "Stops zooming out past the Soft Max FOV below."));
            camera.Items.Add(new FloatItem("Soft Max FOV (OUT limit)",
                () => CameraZoom.SoftMaxFOV,
                v => CameraZoom.Menu_SetSoftMax(v),
                0.5f, CameraZoom.EngineMinFov, CameraZoom.EngineMaxFov, "F1",
                "FOV the zoom stops at while Zoom OUT Lock is on."));

            // --- Flashlight ---
            MenuPage flashlight = new MenuPage("VRCam", "FLASHLIGHT");
            flashlight.Items.Add(new ToggleItem("Flashlight",
                () => AttachRatToCamera.FlashlightVisible,
                v => { if (AttachRatToCamera.Instance != null) AttachRatToCamera.Instance.Menu_ToggleFlashlight(); },
                "Show or remove the flashlight prop (needs the camera rat)."));
            flashlight.Items.Add(new ToggleItem("Movement Lock",
                () => AttachRatToCamera.FlashlightMovementLocked,
                v => { if (AttachRatToCamera.Instance != null) AttachRatToCamera.Instance.Menu_ToggleFlashlightLock(); },
                "Locks the flashlight position keys so the flashlight can't be nudged by accident."));
            flashlight.Items.Add(new ToggleItem("Follow VR view (1:1)",
                () => AttachRatToCamera.FlashlightFollowView,
                v => { if (AttachRatToCamera.Instance != null) AttachRatToCamera.Instance.Menu_SetFlashlightFollowView(v); },
                "ON: the flashlight moves 1:1 with the view (placed from the rendered camera every frame, never " +
                "trails behind). OFF: it follows the camera rat. Each mode keeps its own offsets."));
            // Axes: X = left/right, Y = forward/back, Z = up/down. The offsets edit the active mode's position.
            flashlight.Items.Add(new FloatItem("Offset X (left/right)",
                () => AttachRatToCamera.FlashlightOffset.X,
                v => { if (AttachRatToCamera.Instance != null) AttachRatToCamera.Instance.Menu_SetFlashlightOffset('X', v); },
                0.01f, -5f, 5f, "F3",
                "Flashlight position, left (-) / right (+), for the active mode."));
            flashlight.Items.Add(new FloatItem("Offset Y (fwd/back)",
                () => AttachRatToCamera.FlashlightOffset.Y,
                v => { if (AttachRatToCamera.Instance != null) AttachRatToCamera.Instance.Menu_SetFlashlightOffset('Y', v); },
                0.01f, -5f, 5f, "F3",
                "Flashlight position, back (-) / forward (+), for the active mode."));
            flashlight.Items.Add(new FloatItem("Offset Z (up/down)",
                () => AttachRatToCamera.FlashlightOffset.Z,
                v => { if (AttachRatToCamera.Instance != null) AttachRatToCamera.Instance.Menu_SetFlashlightOffset('Z', v); },
                0.01f, -5f, 5f, "F3",
                "Flashlight position, down (-) / up (+), for the active mode."));
            flashlight.Items.Add(new FloatItem("Pitch",
                () => AttachRatToCamera.FlashlightRotation.X,
                v => { if (AttachRatToCamera.Instance != null) AttachRatToCamera.Instance.Menu_SetFlashlightOffset('P', v); },
                5f, -360f, 360f, "F1",
                "Flashlight pitch angle in degrees."));
            flashlight.Items.Add(new ToggleItem("Beam",
                () => VRCamModConfiguration.FlashlightBeam,
                v => { VRCamModConfiguration.FlashlightBeam = v; VRCamModConfiguration.SaveSettingsOnly(); },
                "ON: VRCam draws its own light from the flashlight every frame, day or night. OFF: only the torch " +
                "model's built-in light, which the game shows only from 17:00 to 07:00."));
            flashlight.Items.Add(new ChoiceItem("Light Preset",
                () => VRCamModConfiguration.FlashlightPresetNames,
                () => VRCamModConfiguration.FlashlightPreset,
                i => VRCamModConfiguration.SetFlashlightPreset(i),
                "Torch = 12 / 20 m. Bright = 25 / 35 m. Flood = wide. Spot = narrow, far. " +
                "Max = 50 / 100 m. Custom = the rows below set by hand."));
            flashlight.Items.Add(new FloatItem("Beam Brightness",
                () => VRCamModConfiguration.FlashlightBrightness,
                v => { VRCamModConfiguration.FlashlightBrightness = v; VRCamModConfiguration.SaveSettingsOnly(); },
                0.5f, 0.5f, 50f, "F1",
                "How bright the beam is. 12 = the Torch preset; raise it to see the beam in daylight."));
            flashlight.Items.Add(new FloatItem("Beam Range (m)",
                () => VRCamModConfiguration.FlashlightRange,
                v => { VRCamModConfiguration.FlashlightRange = v; VRCamModConfiguration.SaveSettingsOnly(); },
                5f, 5f, 150f, "F0",
                "How far the beam reaches, in metres."));
            flashlight.Items.Add(new FloatItem("Beam Width",
                () => VRCamModConfiguration.FlashlightWidth,
                v => { VRCamModConfiguration.FlashlightWidth = v; VRCamModConfiguration.SaveSettingsOnly(); },
                1f, 5f, 80f, "F0",
                "Size of the light circle (cone radius in degrees). Small = spot, large = flood."));
            flashlight.Items.Add(new ToggleItem("Beam Shadows",
                () => VRCamModConfiguration.FlashlightShadows,
                v => { VRCamModConfiguration.FlashlightShadows = v; VRCamModConfiguration.SaveSettingsOnly(); },
                "ON: things in the beam cast shadows (looks real, costs a little frame rate). OFF: no shadows."));
            flashlight.Items.Add(new FloatItem("Beam Edge Softness",
                () => VRCamModConfiguration.FlashlightFeather,
                v => { VRCamModConfiguration.FlashlightFeather = v; VRCamModConfiguration.SaveSettingsOnly(); },
                0.5f, 0.1f, 20f, "F1",
                "Falloff: how the beam fades out at its edge. 1 = default. Try values both ways."));
            flashlight.Items.Add(new FloatItem("Beam Edge Roundness",
                () => VRCamModConfiguration.FlashlightRoundness,
                v => { VRCamModConfiguration.FlashlightRoundness = v; VRCamModConfiguration.SaveSettingsOnly(); },
                1f, 0f, 80f, "F0",
                "Smoothness of the beam's circle edge (0 = default). Never more than Beam Width: it may act as an inner cone."));
            flashlight.Items.Add(new ToggleItem("Camera Light",
                () => VRCamModConfiguration.CameraLight,
                v => { VRCamModConfiguration.CameraLight = v; VRCamModConfiguration.SaveSettingsOnly(); },
                "ON: the beam comes straight from the camera (the VR view or the rendering camera), like a light on top of a camcorder. No flashlight prop or camera rat needed. Uses the Beam settings above. With the flashlight on too and Beam Shadows on, two shadowed lights cost more frame rate."));
            flashlight.Items.Add(new ToggleItem("Light in Rockstar Editor",
                () => VRCamModConfiguration.EditorLight,
                v => { VRCamModConfiguration.EditorLight = v; VRCamModConfiguration.SaveSettingsOnly(); },
                "ON: while the Rockstar Editor is open, the beam is drawn from the Editor's camera (the Editor doesn't record script lights), so it shows on old and new clips. Uses the Beam settings."));
            flashlight.Items.Add(new FloatItem("Camera Light Height (cm)",
                () => VRCamModConfiguration.CameraLightHeightCm,
                v => { VRCamModConfiguration.CameraLightHeightCm = v; VRCamModConfiguration.SaveSettingsOnly(); },
                1f, -30f, 30f, "F0",
                "How far above (+) or below (-) the lens the camera light sits."));
            flashlight.Items.Add(new FloatItem("Camera Light Tilt",
                () => VRCamModConfiguration.CameraLightPitch,
                v => { VRCamModConfiguration.CameraLightPitch = v; VRCamModConfiguration.SaveSettingsOnly(); },
                1f, -45f, 45f, "F0",
                "Aim the camera light down (-) or up (+) from where the camera looks, in degrees."));

            // --- Viewfinder ---
            MenuPage viewfinder = new MenuPage("VRCam", "VIEWFINDER");
            viewfinder.Items.Add(new ToggleItem("Viewfinder",
                () => ViewfinderScript.ViewfinderEnabled,
                v => { if (ViewfinderScript.Instance != null) ViewfinderScript.Instance.Menu_ToggleViewfinder(); },
                "The framing preview window (also toggled by the Start VRCam key)."));
            viewfinder.Items.Add(new ChoiceItem("Zoom Level",
                () =>
                {
                    string[] labels = new string[VRCamModConfiguration.ZoomLevels.Length];
                    for (int i = 0; i < labels.Length; i++)
                        labels[i] = VRCamModConfiguration.ZoomLevels[i].ToString("F2", CultureInfo.InvariantCulture) + "x";
                    return labels;
                },
                () => ViewfinderScript.Instance != null ? ViewfinderScript.Instance.ZoomIndexValue : 0,
                i => { if (ViewfinderScript.Instance != null) ViewfinderScript.Instance.Menu_SetZoomIndex(i); },
                "Simulated zoom of the viewfinder preview (viewfinder zoom keys)."));
            viewfinder.Items.Add(new ChoiceItem("Aspect Ratio",
                () => VRCamModConfiguration.AspectRatioLabels,
                () => ViewfinderScript.Instance != null ? ViewfinderScript.Instance.AspectIndexValue : 0,
                i => { if (ViewfinderScript.Instance != null) ViewfinderScript.Instance.Menu_SetAspectIndex(i); },
                "Viewfinder frame shape (aspect ratio key)."));
            viewfinder.Items.Add(new ToggleItem("Rule of Thirds Grid",
                () => ViewfinderScript.Instance != null && ViewfinderScript.Instance.GridOn,
                v => { if (ViewfinderScript.Instance != null) ViewfinderScript.Instance.Menu_ToggleGrid(); },
                "Composition grid inside the viewfinder (thirds grid key)."));
            viewfinder.Items.Add(new FloatItem("Viewfinder Smoothing",
                () => VRCamModConfiguration.ViewfinderSmoothingFactor,
                v => VRCamModConfiguration.ViewfinderSmoothingFactor = v,
                0.01f, 0.01f, 1.0f, "F2",
                "Easing of viewfinder zoom transitions."));

            // --- Rat camera ---
            MenuPage rat = new MenuPage("VRCam", "RAT CAMERA");
            rat.Items.Add(new ToggleItem("BMX Camera Rider",
                () => VRCamModConfiguration.BmxRiderEnabled,
                v => { VRCamModConfiguration.BmxRiderEnabled = v; VRCamModConfiguration.SaveSettingsOnly(); },
                "On the Start VRCam key, swap control to an invisible ped (on a BMX attached to the vehicle under you) so your real character stays in frame."));
            rat.Items.Add(new ToggleItem("Focus Follows Camera",
                () => VRCamModConfiguration.FocusFollowsCamera,
                v => { VRCamModConfiguration.FocusFollowsCamera = v; VRCamModConfiguration.SaveSettingsOnly(); },
                "Keep the game's streaming / detail focus on the rendering camera instead of the player ped. Fixes low-detail surroundings when the ped is elsewhere."));
            rat.Items.Add(new ActionItem("Spawn Rat", () =>
            {
                if (AttachRatToCamera.Instance != null) AttachRatToCamera.Instance.Menu_SpawnRat();
            },
                "Spawns the invisible rat that follows the camera - select it in the Rockstar Editor as the free-cam target."));
            rat.Items.Add(new ToggleItem("Rat Visible / Collision",
                () => AttachRatToCamera.RatVisibleState,
                v => { if (AttachRatToCamera.Instance != null) AttachRatToCamera.Instance.Menu_ToggleRatVisibility(); },
                "Make the rat visible and solid so you can find it (Show / hide the rat key)."));
            rat.Items.Add(new FloatItem("Positional Smoothing",
                () => VRCamModConfiguration.PositionalSmoothingFactor,
                v => VRCamModConfiguration.PositionalSmoothingFactor = v,
                0.1f, 0.1f, 20f, "F1",
                "Steadicam effect strength for position. Avoid changing while in a vehicle."));
            rat.Items.Add(new FloatItem("Rotation Smoothing",
                () => VRCamModConfiguration.RotationSmoothingFactor,
                v => VRCamModConfiguration.RotationSmoothingFactor = v,
                0.01f, 0.01f, 1.0f, "F2",
                "Steadicam effect strength for rotation."));
            rat.Items.Add(new ToggleItem("Disable Smoothing",
                () => VRCamModConfiguration.DisableSmoothing,
                v => VRCamModConfiguration.DisableSmoothing = v,
                "Rigidly locks the rat to the camera - raw motion, no steadicam."));

            // --- Presets ---
            MenuPage presets = new MenuPage("VRCam", "PRESETS");
            presets.OnOpen = RefreshPresetCache;
            presets.Items.Add(new PresetItem("Preset",
                () => presetCache.Length > 0 ? presetCache : new string[] { "(none saved)" },
                () => Math.Max(0, presetIndex),
                i =>
                {
                    if (presetCache.Length == 0) return;
                    presetIndex = i;
                    if (AttachRatToCamera.Instance != null)
                        AttachRatToCamera.Instance.Menu_LoadPresetByName(presetCache[i]);
                },
                menu =>
                {
                    if (presetCache.Length == 0 || presetIndex < 0)
                        return;
                    string oldName = presetCache[presetIndex];
                    string input = menu.PromptUserInput(oldName);
                    if (string.IsNullOrWhiteSpace(input) || input.Trim() == oldName)
                        return;
                    if (VRCamModConfiguration.RenamePreset(oldName, input))
                    {
                        RefreshPresetCache();
                        presetIndex = Array.IndexOf(presetCache,
                            VRCamModConfiguration.SanitizePresetName(input));
                        if (presetIndex < 0) presetIndex = 0;
                        GTA.UI.Screen.ShowSubtitle("Preset renamed", 1500);
                    }
                    else
                    {
                        GTA.UI.Screen.ShowSubtitle("Rename failed (name taken or not found)", 2000);
                    }
                },
                "Left/Right cycles and loads presets. Enter renames the shown preset."));
            presets.Items.Add(new ActionItem("Save Current As New Preset", () =>
            {
                if (AttachRatToCamera.Instance == null)
                    return;
                string name = PromptUserInput("");
                AttachRatToCamera.Instance.Menu_SavePreset(name);
                RefreshPresetCache();
                presetIndex = Array.IndexOf(presetCache, VRCamModConfiguration.LastPreset);
            },
                "Opens the input box to name the preset (leave empty for an automatic name)."));

            // --- HUD ---
            MenuPage hud = new MenuPage("VRCam", "HUD & OVERLAY");
            hud.Items.Add(new ToggleItem("HUD Info",
                () => VRCamModConfiguration.ShowHudInfo,
                v => { VRCamModConfiguration.ShowHudInfo = v; VRCamModConfiguration.SaveSettingsOnly(); },
                "The zoom/offset/status text blocks around the screen (shown with the viewfinder)."));
            hud.Items.Add(new ToggleItem("Controls Help List",
                () => VRCamModConfiguration.ShowControlsHelp,
                v => { VRCamModConfiguration.ShowControlsHelp = v; VRCamModConfiguration.SaveSettingsOnly(); },
                "The key list on the right side of the screen (shown with the viewfinder)."));

            // --- Controller ---
            MenuPage pad = new MenuPage("VRCam", "CONTROLLER");
            pad.Items.Add(new ToggleItem("Controller Support",
                () => VRCamModConfiguration.PadEnabled,
                v => { VRCamModConfiguration.PadEnabled = v; VRCamModConfiguration.SaveSettingsOnly(); },
                "Xbox / PlayStation pad. D-pad Right + A (Cross) opens this menu, B (Circle) backs out. LB/L1 = flashlight and RB/R1 = lock / unlock the flashlight position (both on foot only - in a vehicle they stay the game's), D-pad (8-way) = move the unlocked flashlight: up/down = front/back, left/right = left/right. Start, Back, Guide, A, Y, X and the left stick are never touched."));
            pad.Items.Add(new ChoiceItem("Zoom Control",
                () => CameraZoom.PadZoomModeNames,
                () => VRCamModConfiguration.PadZoomMode,
                i => { VRCamModConfiguration.PadZoomMode = i; VRCamModConfiguration.SaveSettingsOnly(); },
                "Stick Default: right stick zooms, stays where you leave it. Stick Direct: deflection IS the zoom, springs back. Triggers Default: RT in, LT out. DirectRT: RT pressure IS the zoom. DualTR: RT is the zoom, LT is the dead-stop (full = hold, partial = bleeds back to RT). Trigger modes work on foot only."));
            pad.Items.Add(new FloatItem("Zoom Speed",
                () => VRCamModConfiguration.PadZoomSpeed,
                v => { VRCamModConfiguration.PadZoomSpeed = v; VRCamModConfiguration.SaveSettingsOnly(); },
                0.05f, 0.05f, 3.0f, "F2",
                "Rate modes (Stick Default, Triggers Default): how fast full deflection zooms."));
            pad.Items.Add(new FloatItem("Direct Response",
                () => VRCamModConfiguration.PadDirectResponse,
                v => { VRCamModConfiguration.PadDirectResponse = v; VRCamModConfiguration.SaveSettingsOnly(); },
                0.05f, 0.02f, 1.0f, "F2",
                "Direct modes only (Stick Direct, DirectRT, DualTR): how tightly the zoom follows your finger, the SAME in and out. 1.00 = instant, lower = softer. These modes ignore the Camera page's Zoom IN / OUT smoothness."));
            pad.Items.Add(new FloatItem("Deadzone",
                () => VRCamModConfiguration.PadDeadzone,
                v => { VRCamModConfiguration.PadDeadzone = v; VRCamModConfiguration.SaveSettingsOnly(); },
                0.01f, 0.02f, 0.60f, "F2",
                "How far the stick / trigger must move before it counts. Raise it if the zoom drifts on its own."));

            // --- Keys ---
            MenuPage keys = new MenuPage("VRCam", "KEYS");
            foreach (BindDef d in KeyBinds.All)
                keys.Items.Add(new KeyBindItem(d));
            keys.Items.Add(new ActionItem("Reset Keys to Defaults", () =>
            {
                KeyBinds.ResetAll();
                VRCamModConfiguration.SaveSettingsOnly();
                GTA.UI.Screen.ShowSubtitle("All VRCam keys reset to their defaults", 2000);
            },
                "Restores every key above to its default. Saved at once."));

            // --- General ---
            MenuPage general = new MenuPage("VRCam", "GENERAL");
            general.Items.Add(new ToggleItem("Auto-Hide Hand Pistol",
                () => VRCamModConfiguration.AutoHidePistol,
                v => { VRCamModConfiguration.AutoHidePistol = v; VRCamModConfiguration.SaveSettingsOnly(); },
                "Deletes the pistol prop GTAVive places in your hand each time its VR mode starts."));
            general.Items.Add(new ActionItem("Hide Hand Pistol Now", () =>
            {
                HideGTAVivePistol.DeleteNowRequested = true;
            },
                "Deletes GTAVive's hand pistol prop right now."));
            general.Items.Add(new ToggleItem("Show Companion Tip",
                () => VRCamModConfiguration.ShowCompanionTip,
                v => { VRCamModConfiguration.ShowCompanionTip = v; VRCamModConfiguration.SaveSettingsOnly(); },
                "ON: when the GTAVive VR mod is not loaded, a tip about it is shown once per game start. OFF: never."));

            // --- Root ---
            // The most used rows sit on the first page; everything else is in the sub-menus below them.
            root = new MenuPage("VRCam", "MAIN MENU");
            root.Items.Add(new ToggleItem("Flashlight",
                () => AttachRatToCamera.FlashlightVisible,
                v => { if (AttachRatToCamera.Instance != null) AttachRatToCamera.Instance.Menu_ToggleFlashlight(); },
                "Show or remove the flashlight."));
            root.Items.Add(new ToggleItem("Flashlight Follows VR View",
                () => AttachRatToCamera.FlashlightFollowView,
                v => { if (AttachRatToCamera.Instance != null) AttachRatToCamera.Instance.Menu_SetFlashlightFollowView(v); },
                "ON = the flashlight moves 1:1 with the view. OFF = it follows the camera rat."));
            root.Items.Add(new ChoiceItem("Flashlight Light",
                () => VRCamModConfiguration.FlashlightPresetNames,
                () => VRCamModConfiguration.FlashlightPreset,
                i => VRCamModConfiguration.SetFlashlightPreset(i),
                "How strong the flashlight is: Torch, Bright, Flood, Spot, Max (fine-tune under Flashlight)."));
            root.Items.Add(new ToggleItem("Camera Light",
                () => VRCamModConfiguration.CameraLight,
                v => { VRCamModConfiguration.CameraLight = v; VRCamModConfiguration.SaveSettingsOnly(); },
                "A light on the camera itself, like a camcorder light: no flashlight prop needed. Uses the Beam settings."));
            root.Items.Add(new SubmenuItem("Camera", camera, "FOV, zoom smoothness, zoom mode and soft-locks."));
            root.Items.Add(new SubmenuItem("Flashlight", flashlight, "Flashlight position, pitch, lock, beam and camera light."));
            root.Items.Add(new SubmenuItem("Viewfinder", viewfinder, "Framing preview: zoom, aspect, grid."));
            root.Items.Add(new SubmenuItem("Rat Camera", rat, "The invisible camera rat and its smoothing."));
            root.Items.Add(new SubmenuItem("Presets", presets, "Load or save named setting presets."));
            root.Items.Add(new SubmenuItem("HUD & Overlay", hud, "Turn the on-screen text on or off."));
            root.Items.Add(new SubmenuItem("Controller", pad, "Gamepad zoom modes and button layout."));
            root.Items.Add(new SubmenuItem("Keys", keys, "Change any VRCam key. Saved to VRcam.ini [Keys] at once."));
            root.Items.Add(new SubmenuItem("General", general, "Hand pistol hiding and the companion tip."));
            root.Items.Add(new ToggleItem("All Controls Enabled",
                () => AttachRatToCamera.F10Locked,
                v => { if (AttachRatToCamera.Instance != null) AttachRatToCamera.Instance.Menu_ToggleMasterLock(); },
                "Master switch for the rat, flashlight and zoom controls (Lock / unlock controls key)."));
        }

        private void RefreshPresetCache()
        {
            presetCache = VRCamModConfiguration.GetPresetNames().ToArray();
            presetIndex = Array.IndexOf(presetCache, VRCamModConfiguration.LastPreset);
            if (presetIndex < 0 && presetCache.Length > 0)
                presetIndex = 0;
        }

        // ------------------------------------------------------------
        // Open / close / navigation
        // ------------------------------------------------------------
        public void Push(MenuPage page)
        {
            page.OnOpen?.Invoke();
            page.ScrollOffset = 0;
            page.SelectedIndex = 0;
            pageStack.Push(page);
            PlayNavSound("NAV_UP_DOWN");
        }

        private void Open()
        {
            pageStack.Clear();
            root.OnOpen?.Invoke();
            pageStack.Push(root);   // root keeps its previous cursor
            IsOpen = true;
        }

        private void Close()
        {
            captureDef = null;
            pageStack.Clear();
            IsOpen = false;
            ignoreKeysUntil = Game.GameTime + 200;
            PlayNavSound("BACK");
            VRCamModConfiguration.SaveSettingsOnly();
        }

        private void GoBack()
        {
            if (pageStack.Count > 1)
            {
                pageStack.Pop();
                PlayNavSound("BACK");
            }
            else
            {
                Close();
            }
        }

        public string PromptUserInput(string defaultText)
        {
            typing = true;
            string result;
            try
            {
                result = Game.GetUserInput(WindowTitle.EnterMessage60, defaultText, 15);
            }
            finally
            {
                typing = false;
                ignoreKeysUntil = Game.GameTime + 300;
            }
            return result;
        }

        // Keys page: wait for the next key combination for this bind
        public void BeginKeyCapture(BindDef def)
        {
            captureDef = def;
            captureArmAt = Game.GameTime + 150;   // the Enter that started the capture must not become the bind
        }

        private void FinishKeyCapture(KeyEventArgs e)
        {
            BindDef def = captureDef;
            captureDef = null;
            ignoreKeysUntil = Game.GameTime + 250;
            if (e.KeyCode == Keys.Escape)
            {
                PlayNavSound("BACK");
                return;
            }
            if (e.KeyCode == Keys.Delete && !e.Control && !e.Shift && !e.Alt)
            {
                // Delete = no key
                if (def == KeyBinds.OpenMenu)
                {
                    PlayNavSound("ERROR");
                    GTA.UI.Screen.ShowSubtitle("The menu key always stays bound", 2000);
                    return;
                }
                def.Bind = new KeyBind(Keys.None, false, false, false);
                VRCamModConfiguration.SaveSettingsOnly();
                PlayNavSound("SELECT");
                Trace.Event("Key " + def.IniName + " -> None");
                GTA.UI.Screen.ShowSubtitle(def.Title + ": None", 1500);
                return;
            }
            def.Bind = new KeyBind(e.KeyCode, e.Control, e.Shift, e.Alt);
            VRCamModConfiguration.SaveSettingsOnly();
            PlayNavSound("SELECT");
            Trace.Event("Key " + def.IniName + " -> " + def.Text);
            BindDef other = KeyBinds.FindDuplicate(def);
            if (other != null)
                GTA.UI.Screen.ShowSubtitle(def.Text + " is also used by: " + other.Title, 3000);
            else
                GTA.UI.Screen.ShowSubtitle(def.Title + ": " + def.Text, 1500);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (typing)
                return;

            if (!IsOpen)
            {
                if (KeyBinds.OpenMenu.Pressed(e))
                    Open();
                return;
            }

            if (captureDef != null)
            {
                if (Game.GameTime >= captureArmAt && !KeyBinds.IsModifierKey(e.KeyCode))
                    FinishKeyCapture(e);
                return;
            }

            if (Game.GameTime < ignoreKeysUntil)
                return;

            // Esc closes only from the root menu; deeper pages go back.
            if (e.KeyCode == Keys.Escape)
                GoBack();
            else if (KeyBinds.OpenMenu.Pressed(e))
                Close();
        }

        // ------------------------------------------------------------
        // Input: frontend controls (work for keyboard AND controller),
        // with repeat + acceleration.
        // ------------------------------------------------------------
        private const int CTRL_UP = 188, CTRL_DOWN = 187, CTRL_LEFT = 189, CTRL_RIGHT = 190;
        private const int CTRL_ACCEPT = 201, CTRL_CANCEL = 202;

        private static bool CtrlPressed(int c)
        {
            return Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, 0, c);
        }
        private static bool CtrlJustPressed(int c)
        {
            return Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, c);
        }

        private static bool Repeat(RepeatState b, bool downNow, int now, int initialMs, int intervalMs)
        {
            bool fire = false;
            if (downNow)
            {
                if (!b.down)
                {
                    fire = true;
                    b.nextRepeat = now + initialMs;
                }
                else if (now >= b.nextRepeat)
                {
                    fire = true;
                    b.nextRepeat = now + intervalMs;
                }
            }
            b.down = downNow;
            return fire;
        }

        private void HandleInput()
        {
            int now = Game.GameTime;

            bool upNow = CtrlPressed(CTRL_UP);
            bool downNow = CtrlPressed(CTRL_DOWN);
            bool leftNow = CtrlPressed(CTRL_LEFT);
            bool rightNow = CtrlPressed(CTRL_RIGHT);

            // Just opened from the pad (D-pad Right + A still held) or a key was just captured:
            // swallow those presses so they don't adjust/activate the row under the cursor.
            if (now < ignoreKeysUntil)
            {
                rUp.down = upNow; rDown.down = downNow; rLeft.down = leftNow; rRight.down = rightNow;
                return;
            }

            bool upFire = Repeat(rUp, upNow, now, 280, 95);
            bool downFire = Repeat(rDown, downNow, now, 280, 95);

            // Slider adjust uses an accelerating repeat: the longer you hold,
            // the faster and the bigger the step.
            bool adjusting = leftNow || rightNow;
            if (adjusting && !wasAdjusting) holdStart = now;
            wasAdjusting = adjusting;
            int heldMs = adjusting ? now - holdStart : 0;
            int interval = heldMs > 2500 ? 45 : heldMs > 1200 ? 70 : 110;
            float mult = heldMs > 2500 ? 10f : heldMs > 1200 ? 4f : heldMs > 500 ? 2f : 1f;

            bool leftFire = Repeat(rLeft, leftNow, now, 280, interval);
            bool rightFire = Repeat(rRight, rightNow, now, 280, interval);

            bool selectFire = CtrlJustPressed(CTRL_ACCEPT);
            bool backFire = CtrlJustPressed(CTRL_CANCEL);

            MenuPage page = pageStack.Peek();
            MenuItem item = page.Items.Count > 0 ? page.Items[page.SelectedIndex] : null;

            if (upFire && page.Items.Count > 0)
            {
                page.SelectedIndex--;
                if (page.SelectedIndex < 0)
                    page.SelectedIndex = page.Items.Count - 1;
                PlayNavSound("NAV_UP_DOWN");
            }
            if (downFire && page.Items.Count > 0)
            {
                page.SelectedIndex++;
                if (page.SelectedIndex >= page.Items.Count)
                    page.SelectedIndex = 0;
                PlayNavSound("NAV_UP_DOWN");
            }
            if (leftFire) item?.OnLeft(mult);
            if (rightFire) item?.OnRight(mult);
            if (selectFire) item?.OnActivate(this);
            if (backFire && captureDef == null) GoBack();
        }

        // ------------------------------------------------------------
        // Per-frame: suppress conflicting controls, input, draw
        // ------------------------------------------------------------
        // Set by PadInput (D-pad Right + A); consumed on the next tick
        private static bool openRequested = false;
        public static void RequestOpen() { openRequested = true; }

        private void OnTick(object sender, EventArgs e)
        {
            if (openRequested)
            {
                openRequested = false;
                if (!IsOpen && !typing)
                {
                    Open();
                    ignoreKeysUntil = Game.GameTime + 250; // the A that opened it must not also "accept"
                }
            }

            if (!IsOpen || pageStack.Count == 0)
                return;

            // Suppress only the controls the menu consumes plus the "escape
            // sideways" actions - movement/look stay enabled.
            int[] disable = {
                27,                 // INPUT_PHONE
                19,                 // INPUT_CHARACTER_WHEEL
                37,                 // INPUT_SELECT_WEAPON (weapon wheel)
                166, 167, 168, 169, // INPUT_SELECT_CHARACTER_*
                172, 173, 174, 175, // INPUT_PHONE_UP/DOWN/LEFT/RIGHT
                188, 187, 189, 190, // INPUT_FRONTEND_UP/DOWN/LEFT/RIGHT
                201, 202,           // INPUT_FRONTEND_ACCEPT/CANCEL
                241, 242,           // frontend scroll
                199, 200,           // pause menu (Esc is ours while open)
                24, 25,             // INPUT_ATTACK / INPUT_AIM (LMB/RMB zoom)
                14, 15,             // weapon wheel next/prev (mouse wheel zoom)
            };
            // On a gamepad the menu only needs the D-pad, A and B. Leave everything
            // else to the game (triggers, bumpers, sticks, X, Y...), and only mute
            // what those three would otherwise do: D-pad = phone / character
            // switch, A = sprint, B = melee / reload / cinematic cam.
            int[] disablePad = {
                27, 19, 166, 167, 168, 169, 172, 173, 174, 175,
                188, 187, 189, 190, 201, 202,
                21,                 // INPUT_SPRINT (A)
                140, 141, 45, 80,   // melee light/heavy, reload, vehicle cinematic cam (B)
                199, 200,
            };
            if (PadInput.Active && captureDef == null)
                disable = disablePad;

            foreach (int c in disable)
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, c, true);

            // While a key is being captured the frontend controls must not move the cursor or adjust rows
            if (captureDef == null)
                HandleInput();

            if (!IsOpen || pageStack.Count == 0)
                return;

            MenuPage page = pageStack.Peek();

            // clamp scroll window
            if (page.SelectedIndex < page.ScrollOffset)
                page.ScrollOffset = page.SelectedIndex;
            if (page.SelectedIndex >= page.ScrollOffset + MaxVisibleRows)
                page.ScrollOffset = page.SelectedIndex - MaxVisibleRows + 1;

            Draw(page);
        }

        // ------------------------------------------------------------
        // Drawing (1080p design grid -> screen fractions)
        // ------------------------------------------------------------
        private static float PxX(float px) { return px / BaseW; }
        private static float PxY(float px) { return px / BaseH; }

        private static float TextLineHeight(float scale, int font)
        {
            // GET_RENDERED_CHARACTER_HEIGHT (_GET_TEXT_SCALE_HEIGHT)
            return Function.Call<float>((Hash)0xDB88A37483346780UL, scale, font);
        }

        private static float TextWidth(string text, float scale, int font)
        {
            Function.Call((Hash)0x54CE8AC98E120CABUL, "STRING"); // BEGIN_TEXT_COMMAND_GET_WIDTH
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.SET_TEXT_FONT, font);
            Function.Call(Hash.SET_TEXT_SCALE, scale, scale);
            return Function.Call<float>((Hash)0x85F061DA64ED2F67UL, 1); // END_TEXT_COMMAND_GET_WIDTH
        }

        // Rect from TOP-LEFT corner (DRAW_RECT is centre-based).
        private static void RectTL(float x, float y, float w, float h, int[] c)
        {
            Function.Call(Hash.DRAW_RECT, x + w * 0.5f, y + h * 0.5f, w, h, c[0], c[1], c[2], c[3]);
        }

        private static void RectOutline(float x, float y, float w, float h, int[] c, float px)
        {
            float tx = PxX(px), ty = PxY(px);
            RectTL(x, y, w, ty, c);                 // top
            RectTL(x, y + h - ty, w, ty, c);        // bottom
            RectTL(x, y, tx, h, c);                 // left
            RectTL(x + w - tx, y, tx, h, c);        // right
        }

        private enum Align { Left, Center, Right }

        private static void Text(string text, float x, float y, float scale, int font, int[] c, Align align, float wrapRight = 0f)
        {
            Function.Call(Hash.SET_TEXT_FONT, font);
            Function.Call(Hash.SET_TEXT_SCALE, scale, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, c[0], c[1], c[2], c[3]);
            switch (align)
            {
                case Align.Center:
                    Function.Call(Hash.SET_TEXT_CENTRE, true);
                    break;
                case Align.Right:
                    Function.Call(Hash.SET_TEXT_JUSTIFICATION, 2);
                    Function.Call(Hash.SET_TEXT_WRAP, 0.0f, wrapRight);
                    break;
                default:
                    Function.Call(Hash.SET_TEXT_JUSTIFICATION, 1);
                    break;
            }
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y);
        }

        private List<string> WrapDescription(string text, float scale, int font, float availW)
        {
            if (text == descCacheText)
                return descCacheLines;

            List<string> lines = new List<string>();
            string line = "";
            foreach (string word in text.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && TextWidth(candidate, scale, font) > availW)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }
            if (line.Length > 0)
                lines.Add(line);

            descCacheText = text;
            descCacheLines = lines;
            return lines;
        }

        private void Draw(MenuPage page)
        {
            float wFrac = PxX(WidthPx);
            float xFrac = PosX;
            float padX = PxX(PadXPx);
            float leftEdge = xFrac + padX;
            float rightEdge = xFrac + wFrac - padX;
            float y = PosY;

            // --- Title banner ---
            float titleH = PxY(TitleHeightPx);
            RectTL(xFrac, y, wFrac, titleH, TitleBg);
            {
                float th = TextLineHeight(TitleScale, TitleFont);
                float ty = y + (titleH - th) * 0.5f - th * 0.14f;
                Text(page.Title, xFrac + wFrac * 0.5f, ty, TitleScale, TitleFont, TitleText, Align.Center);
            }
            y += titleH;

            // --- Subtitle / counter bar ---
            float subH = PxY(SubtitleHeightPx);
            RectTL(xFrac, y, wFrac, subH, SubtitleBg);
            {
                float th = TextLineHeight(SubScale, SubFont);
                float ty = y + (subH - th) * 0.5f - th * 0.14f;
                Text(page.Subtitle, leftEdge, ty, SubScale, SubFont, SubtitleText, Align.Left);
                if (page.Items.Count > 0)
                    Text((page.SelectedIndex + 1) + " / " + page.Items.Count,
                        rightEdge, ty, SubScale, SubFont, SubtitleText, Align.Right, rightEdge);
            }
            y += subH;

            // --- Rows (windowed) ---
            float rowH = PxY(RowHeightPx);
            float rowTextH = TextLineHeight(RowTextScale, RowFont);
            float rowTextY = (rowH - rowTextH) * 0.5f - rowTextH * 0.14f;
            int n = page.Items.Count;
            int start = page.ScrollOffset;
            int end = Math.Min(n, start + MaxVisibleRows);

            bool hasScrollbar = n > MaxVisibleRows;
            float sbW = hasScrollbar ? PxX(ScrollbarWidthPx) : 0f;
            float valueRight = rightEdge - sbW - PxX(2f);

            float listTop = y;
            for (int i = start; i < end; i++)
            {
                MenuItem it = page.Items[i];
                bool sel = i == page.SelectedIndex;

                int[] bg = sel ? RowSelBg : RowBg;
                int[] fg = sel ? RowSelText : RowText;

                RectTL(xFrac, y, wFrac, rowH, bg);
                if (sel)
                {
                    RectOutline(xFrac, y, wFrac, rowH, Accent, OutlinePx);   // cyan border
                    RectTL(xFrac, y, PxX(AccentBarWidthPx), rowH, Accent);   // left accent bar
                }

                float ty = y + rowTextY;
                Text(it.Title, leftEdge, ty, RowTextScale, RowFont, fg, Align.Left);

                if (it.Kind == MenuItemKind.Toggle)
                {
                    // drawn checkbox, no sprite dependency
                    float boxPx = RowHeightPx * 0.46f;
                    float boxW = PxX(boxPx), boxH = PxY(boxPx);
                    float bx = valueRight - boxW;
                    float by = y + (rowH - boxH) * 0.5f;
                    RectTL(bx, by, boxW, boxH, CheckboxBorder);
                    float ix = PxX(2f), iy = PxY(2f);
                    RectTL(bx + ix, by + iy, boxW - 2 * ix, boxH - 2 * iy, it.BoolValue ? Accent : bg);
                }
                else
                {
                    bool capturing = captureDef != null && it is KeyBindItem && ((KeyBindItem)it).Def == captureDef;
                    string rv = capturing ? "press a key..." : it.GetValueText();
                    if (!string.IsNullOrEmpty(rv))
                    {
                        bool adjustable = it.Kind == MenuItemKind.Float || it.Kind == MenuItemKind.Choice;
                        string shown = sel && adjustable ? "< " + rv + " >" : rv;
                        if (sel && adjustable)
                        {
                            // value pill behind the text so it reads as editable
                            float tw = TextWidth(shown, RowTextScale, RowFont);
                            float pillPad = PxX(6f);
                            RectTL(valueRight - tw - pillPad, y + rowH * 0.16f,
                                tw + 2 * pillPad, rowH * 0.68f, ValuePillBg);
                        }
                        Text(shown, valueRight, ty, RowTextScale, RowFont, capturing ? CaptureText : fg, Align.Right, valueRight);
                    }
                }

                y += rowH;
            }

            // --- Scrollbar ---
            if (hasScrollbar)
            {
                float listH = rowH * MaxVisibleRows;
                float sbX = xFrac + wFrac - sbW;
                RectTL(sbX, listTop, sbW, listH, ScrollTrack);
                float frac = (float)MaxVisibleRows / n;
                float thumbH = listH * frac;
                float maxOffset = n - MaxVisibleRows;
                float t = maxOffset > 0 ? page.ScrollOffset / maxOffset : 0f;
                RectTL(sbX, listTop + t * (listH - thumbH), sbW, thumbH, Accent);
            }

            // --- Description panel ---
            float contentBottom = y;
            if (page.SelectedIndex >= 0 && page.SelectedIndex < n)
            {
                string d = page.Items[page.SelectedIndex].Description;
                if (!string.IsNullOrEmpty(d))
                {
                    float availW = rightEdge - leftEdge;
                    List<string> wrapped = WrapDescription(d, DescScale, SubFont, availW);
                    int lines = Math.Max(1, wrapped.Count);

                    float lineH = TextLineHeight(DescScale, SubFont);
                    float padY = PxY(9f);
                    const float lineAdvance = 1.4f;
                    float dH = 2f * padY + lines * lineH * lineAdvance;
                    float minH = PxY(DescMinHeightPx);
                    if (dH < minH) dH = minH;
                    float dy = y + PxY(4f);
                    RectTL(xFrac, dy, wFrac, dH, DescBg);
                    float ly = dy + padY;
                    foreach (string lineStr in wrapped)
                    {
                        Text(lineStr, leftEdge, ly, DescScale, SubFont, DescText, Align.Left);
                        ly += lineH * lineAdvance;
                    }
                    contentBottom = dy + dH;
                }
            }

            // --- Footer hint ---
            {
                float fy = contentBottom + PxY(4f);
                float fH = PxY(30f);
                RectTL(xFrac, fy, wFrac, fH, FooterBg);
                float fScale = SubScale * 0.92f;
                float fh = TextLineHeight(fScale, SubFont);
                float fty = fy + (fH - fh) * 0.5f - fh * 0.14f;
                Text(captureDef != null
                        ? "Press the new key combination    Delete = None    Esc = cancel"
                        : "Enter = select / type value    Backspace = back    Esc = back/close",
                    leftEdge, fty, fScale, SubFont, captureDef != null ? CaptureText : FooterText, Align.Left);
            }
        }
    }
}
