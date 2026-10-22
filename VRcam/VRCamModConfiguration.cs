using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;

namespace VRCam
{
    public static class VRCamModConfiguration
    {
        // Turns the camera rat's position and rotation smoothing off (rigid follow).
        public static bool DisableSmoothing { get; set; } = false;

        // Viewfinder configuration.
        public static float BaseWidth { get; set; } = 0.5f;
        public static float[] PresetHeights { get; set; } = new float[] { 0.35f, 0.27f };
        public static string[] AspectRatioLabels { get; set; } = new string[] { "16:9", "21:9" };

        // Smoothing factors for the viewfinder transitions.
        public static float ViewfinderSmoothingFactor { get; set; } = 0.1f;
        public static float AspectSmoothingFactor { get; set; } = 0.1f;

        // Viewfinder zoom levels.
        public static float DefaultZoom { get; set; } = 1.0f;
        public static float[] ZoomLevels { get; set; } = new float[] { 0.80f, 1.0f, 1.5f, 2.0f, 3.0f, 4.0f };

        // Camera rat smoothing.
        public static float PositionalSmoothingFactor { get; set; } = 5.0f;
        public static float RotationSmoothingFactor { get; set; } = 0.1f;

        // Camera zoom settings
        public static float ZoomInSmoothness { get; set; } = 0.15f;
        public static float ZoomOutSmoothness { get; set; } = 0.15f;
        public static float CurrentFOV { get; set; } = 68f;
        public static bool UseClickMode { get; set; } = false;

        // Soft-lock FOV range settings
        public static bool ZoomInLocked { get; set; } = false;
        public static bool ZoomOutLocked { get; set; } = false;
        public static float SoftMinFOV { get; set; } = 45f;
        public static float SoftMaxFOV { get; set; } = 60f;
        // Zoom range. The game accepts a camera FOV from 1 to 130 degrees.
        public static float ZoomRangeMinFOV { get; set; } = 1f;
        public static float ZoomRangeMaxFOV { get; set; } = 90f;

        // Flashlight settings
        public static float FlashlightOffsetX { get; set; } = 0.0f;
        public static float FlashlightOffsetY { get; set; } = 0.0f;
        public static float FlashlightOffsetZ { get; set; } = 0.0f;
        public static float FlashlightRotationX { get; set; } = 0.0f;
        public static bool FlashlightMovementLocked { get; set; } = true;
        // "Follow VR view" places the flashlight from the rendered camera each tick (1:1 with the view), not from the
        // smoothed rat, with its own offset (right, forward, up in metres). Not part of presets.
        public static bool FlashlightFollowView { get; set; } = false;
        public static float FlashlightViewOffsetX { get; set; } = 0.20f;
        public static float FlashlightViewOffsetY { get; set; } = 0.35f;
        public static float FlashlightViewOffsetZ { get; set; } = -0.15f;
        // The torch prop's own light is only on at night (its model flags), so VRCam draws its own beam from the
        // flashlight every frame. Brightness, range in metres, width = cone radius in degrees.
        public static bool FlashlightBeam { get; set; } = true;
        public static float FlashlightBrightness { get; set; } = 12f;
        public static float FlashlightRange { get; set; } = 20f;
        public static float FlashlightWidth { get; set; } = 30f;
        public static bool FlashlightShadows { get; set; } = false;   // a shadowed light every frame costs frame rate
        // Spot light edge: falloff (how far the edge fades out, higher = softer) and roundness (edge smoothness).
        public static float FlashlightFeather { get; set; } = 1f;
        public static float FlashlightRoundness { get; set; } = 0f;
        // Camera Light: the same beam from the camera itself (no torch, no rat): height above the lens (cm) and a
        // tilt (degrees, negative = down).
        public static bool CameraLight { get; set; } = false;
        // Draw the light from the Rockstar Editor's camera while the Editor is open.
        public static bool EditorLight { get; set; } = true;
        public static float CameraLightHeightCm { get; set; } = 5f;
        public static float CameraLightPitch { get; set; } = -5f;
        // Light presets as beam settings (brightness, range, width). The last entry, Custom, means the values were
        // set by hand; choosing it changes nothing.
        public static readonly string[] FlashlightPresetNames = { "Torch", "Bright", "Flood", "Spot", "Max", "Custom" };
        private static readonly float[,] FlashlightPresetValues =
            { { 12f, 20f, 30f }, { 25f, 35f, 30f }, { 20f, 25f, 60f }, { 40f, 60f, 12f }, { 50f, 100f, 30f } };
        public static int FlashlightPreset
        {
            get
            {
                for (int i = 0; i < FlashlightPresetValues.GetLength(0); i++)
                    if (Math.Abs(FlashlightBrightness - FlashlightPresetValues[i, 0]) < 0.05f &&
                        Math.Abs(FlashlightRange - FlashlightPresetValues[i, 1]) < 0.5f &&
                        Math.Abs(FlashlightWidth - FlashlightPresetValues[i, 2]) < 0.5f) return i;
                return FlashlightPresetNames.Length - 1;
            }
        }
        public static void SetFlashlightPreset(int i)
        {
            int n = FlashlightPresetValues.GetLength(0);
            // "Custom" is not a preset: stepping onto it from a preset carries on round to the next preset
            if (i == n) i = FlashlightPreset == 0 ? n - 1 : 0;
            if (i < 0 || i >= n) return;
            FlashlightBrightness = FlashlightPresetValues[i, 0];
            FlashlightRange = FlashlightPresetValues[i, 1];
            FlashlightWidth = FlashlightPresetValues[i, 2];
            FlashlightBeam = true;
            SaveSettingsOnly();
        }

        // Menu / HUD settings (the on-screen text is off by default; turn it on in the menu under HUD & Overlay)
        public static bool ShowHudInfo { get; set; } = false;      // zoom/offset/status text
        public static bool ShowControlsHelp { get; set; } = false; // right-side keybind list
        public static int ZoomMode { get; set; } = 0;              // 0 wheel, 1 click, 2 mouse slide (not per preset)
        public static bool BmxRiderEnabled { get; set; } = false;    // the Start key swaps control to an invisible BMX rider ped
        public static bool FocusFollowsCamera { get; set; } = false; // keep streaming/LOD focus on the rendering camera
        public static bool TraceLog { get; set; } = true;          // write VRcam_trace.log (diagnostics)
        public static bool PadEnabled { get; set; } = true;        // gamepad layer (PadInput.cs)
        public static int PadZoomMode { get; set; } = 1;           // see CameraZoom.PadZoomModeNames
        public static float PadZoomSpeed { get; set; } = 0.6f;     // rate modes: FOV degrees per frame at full deflection (60 fps)
        public static float PadDeadzone { get; set; } = 0.15f;
        public static float PadDirectResponse { get; set; } = 0.35f; // direct pad modes: 0.05 soft .. 1 instant, same both ways
        public static float SlideSensitivity { get; set; } = 0.35f; // mouse-slide zoom speed
        public static bool AutoHidePistol { get; set; } = true;    // delete GTAVive's hand pistol prop when VR starts
        public static bool ShowCompanionTip { get; set; } = true;  // one tip per game start when GTAVive is not loaded

        // Last used preset
        public static string LastPreset { get; set; } = "";

        internal const string IniPath = "VRcam.ini";

        // Static constructor loads the INI file once at startup.
        static VRCamModConfiguration()
        {
            Load(IniPath);
        }

        /// <summary>
        /// Converts FOV to approximate millimeter equivalent for 35mm film
        /// </summary>
        public static int FOVToMillimeter(float fov)
        {
            // Formula: focal_length = (sensor_width / 2) / tan(fov_horizontal / 2)
            // For 35mm film, sensor width is 36mm
            float fovRad = fov * (float)Math.PI / 180f;
            float focalLength = (36f / 2f) / (float)Math.Tan(fovRad / 2f);
            return (int)Math.Round(focalLength);
        }

        /// <summary>
        /// Converts millimeter to FOV
        /// </summary>
        public static float MillimeterToFOV(int mm)
        {
            // Inverse of above formula
            float fovRad = 2f * (float)Math.Atan((36f / 2f) / mm);
            return fovRad * 180f / (float)Math.PI;
        }

        /// <summary>
        /// Saves current settings to INI file with preset
        /// </summary>
        public static void Save()
        {
            SaveInternal(true, null);
        }

        /// <summary>
        /// Saves settings and appends a preset under a user-chosen name
        /// (falls back to the auto-generated name when empty).
        /// </summary>
        public static void Save(string customPresetName)
        {
            SaveInternal(true, customPresetName);
        }

        /// <summary>
        /// Makes a string safe as an ini key: strips characters that would
        /// break the line format.
        /// </summary>
        public static string SanitizePresetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";
            name = name.Trim();
            foreach (char c in "=;[]")
                name = name.Replace(c, '-');
            if (name.Length > 60)
                name = name.Substring(0, 60);
            return name;
        }

        /// <summary>
        /// Renames a preset in the ini. Returns false if the old name wasn't
        /// found or the new name already exists.
        /// </summary>
        public static bool RenamePreset(string oldName, string newName)
        {
            newName = SanitizePresetName(newName);
            if (string.IsNullOrEmpty(newName) || newName == oldName || !File.Exists(IniPath))
                return false;

            string[] lines = File.ReadAllLines(IniPath);
            bool inSection = false, renamed = false;

            // refuse a collision
            foreach (var line in lines)
            {
                string t = line.Trim();
                if (t.Contains("===== PRESETS =====")) { inSection = true; continue; }
                if (!inSection || t.StartsWith(";")) continue;
                int eq = t.IndexOf('=');
                if (eq > 0 && t.Substring(0, eq).Trim() == newName)
                    return false;
            }

            inSection = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.Contains("===== PRESETS =====")) { inSection = true; continue; }
                if (!inSection || string.IsNullOrEmpty(t) || t.StartsWith(";")) continue;
                int eq = t.IndexOf('=');
                if (eq > 0 && t.Substring(0, eq).Trim() == oldName)
                {
                    lines[i] = newName + "=" + t.Substring(eq + 1).Trim();
                    renamed = true;
                    break;
                }
            }
            if (!renamed)
                return false;

            // keep the LastPreset pointer in step
            if (LastPreset == oldName)
            {
                LastPreset = newName;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Trim().StartsWith("LastPreset="))
                    {
                        lines[i] = "LastPreset=" + newName;
                        break;
                    }
                }
            }

            File.WriteAllLines(IniPath, lines);
            return true;
        }

        /// <summary>
        /// Saves current settings without creating a new preset entry
        /// (used by the menu so toggling options doesn't spam presets).
        /// </summary>
        public static void SaveSettingsOnly()
        {
            SaveInternal(false, null);
        }

        private static void SaveInternal(bool appendNewPreset, string customPresetName)
        {
            try
            {
                List<string> lines = new List<string>();

                // Write configuration settings
                lines.Add("; VRCam Configuration File");
                lines.Add("; Edit values while the game is closed, or change them in the in-game menu (Ctrl+F5 by default).");
                lines.Add("");
                lines.Add("DisableSmoothing=" + DisableSmoothing.ToString().ToLower());
                lines.Add("BaseWidth=" + BaseWidth.ToString("F2", CultureInfo.InvariantCulture));
                lines.Add("PresetHeights=" + string.Join(",", PresetHeights.Select(h => h.ToString("F2", CultureInfo.InvariantCulture))));
                lines.Add("AspectRatioLabels=" + string.Join(",", AspectRatioLabels));
                lines.Add("ViewfinderSmoothingFactor=" + ViewfinderSmoothingFactor.ToString("F2", CultureInfo.InvariantCulture));
                lines.Add("AspectSmoothingFactor=" + AspectSmoothingFactor.ToString("F2", CultureInfo.InvariantCulture));
                lines.Add("DefaultZoom=" + DefaultZoom.ToString("F1", CultureInfo.InvariantCulture));
                lines.Add("ZoomLevels=" + string.Join(",", ZoomLevels.Select(z => z.ToString("F2", CultureInfo.InvariantCulture))));
                lines.Add("PositionalSmoothingFactor=" + PositionalSmoothingFactor.ToString("F1", CultureInfo.InvariantCulture));
                lines.Add("RotationSmoothingFactor=" + RotationSmoothingFactor.ToString("F2", CultureInfo.InvariantCulture));
                lines.Add("");
                lines.Add("; Camera Zoom Settings");
                lines.Add("ZoomInSmoothness=" + ZoomInSmoothness.ToString("F2", CultureInfo.InvariantCulture));
                lines.Add("ZoomOutSmoothness=" + ZoomOutSmoothness.ToString("F2", CultureInfo.InvariantCulture));
                lines.Add("CurrentFOV=" + CurrentFOV.ToString("F1", CultureInfo.InvariantCulture));
                lines.Add("UseClickMode=" + UseClickMode.ToString().ToLower());
                lines.Add("");
                lines.Add("; Soft-Lock FOV Range Settings");
                lines.Add("ZoomInLocked=" + ZoomInLocked.ToString().ToLower());
                lines.Add("ZoomOutLocked=" + ZoomOutLocked.ToString().ToLower());
                lines.Add("SoftMinFOV=" + SoftMinFOV.ToString("F1", CultureInfo.InvariantCulture));
                lines.Add("SoftMaxFOV=" + SoftMaxFOV.ToString("F1", CultureInfo.InvariantCulture));
                lines.Add("ZoomRangeMinFOV=" + ZoomRangeMinFOV.ToString("F1", CultureInfo.InvariantCulture));
                lines.Add("ZoomRangeMaxFOV=" + ZoomRangeMaxFOV.ToString("F1", CultureInfo.InvariantCulture));
                lines.Add("");
                lines.Add("; Flashlight Settings");
                lines.Add("FlashlightOffsetX=" + FlashlightOffsetX.ToString("F3", CultureInfo.InvariantCulture));
                lines.Add("FlashlightOffsetY=" + FlashlightOffsetY.ToString("F3", CultureInfo.InvariantCulture));
                lines.Add("FlashlightOffsetZ=" + FlashlightOffsetZ.ToString("F3", CultureInfo.InvariantCulture));
                lines.Add("FlashlightRotationX=" + FlashlightRotationX.ToString("F1", CultureInfo.InvariantCulture));
                lines.Add("FlashlightMovementLocked=" + FlashlightMovementLocked.ToString().ToLower());
                lines.Add("; true = the flashlight follows the VR view 1:1 (rendered camera), not the camera rat");
                lines.Add("FlashlightFollowView=" + FlashlightFollowView.ToString().ToLower());
                lines.Add("FlashlightViewOffsetX=" + FlashlightViewOffsetX.ToString("F3", CultureInfo.InvariantCulture));
                lines.Add("FlashlightViewOffsetY=" + FlashlightViewOffsetY.ToString("F3", CultureInfo.InvariantCulture));
                lines.Add("FlashlightViewOffsetZ=" + FlashlightViewOffsetZ.ToString("F3", CultureInfo.InvariantCulture));
                lines.Add("; VRCam's own flashlight beam (the prop's light is only on at night)");
                lines.Add("FlashlightBeam=" + FlashlightBeam.ToString().ToLower());
                lines.Add("FlashlightBrightness=" + FlashlightBrightness.ToString("F1", CultureInfo.InvariantCulture));
                lines.Add("FlashlightRange=" + FlashlightRange.ToString("F0", CultureInfo.InvariantCulture));
                lines.Add("FlashlightWidth=" + FlashlightWidth.ToString("F0", CultureInfo.InvariantCulture));
                lines.Add("FlashlightShadows=" + FlashlightShadows.ToString().ToLower());
                lines.Add("FlashlightFeather=" + FlashlightFeather.ToString("F1", CultureInfo.InvariantCulture));
                lines.Add("FlashlightRoundness=" + FlashlightRoundness.ToString("F1", CultureInfo.InvariantCulture));
                lines.Add("; Camera Light: the beam from the camera itself, no torch (height cm above the lens, pitch degrees)");
                lines.Add("CameraLight=" + CameraLight.ToString().ToLower());
                lines.Add("EditorLight=" + EditorLight.ToString().ToLower());
                lines.Add("CameraLightHeightCm=" + CameraLightHeightCm.ToString("F0", CultureInfo.InvariantCulture));
                lines.Add("CameraLightPitch=" + CameraLightPitch.ToString("F0", CultureInfo.InvariantCulture));
                lines.Add("");
                lines.Add("; Menu / HUD Settings");
                lines.Add("ShowHudInfo=" + ShowHudInfo.ToString().ToLower());
                lines.Add("ShowControlsHelp=" + ShowControlsHelp.ToString().ToLower());
                lines.Add("ZoomMode=" + ZoomMode);
                lines.Add("BmxRiderEnabled=" + BmxRiderEnabled.ToString().ToLower());
                lines.Add("FocusFollowsCamera=" + FocusFollowsCamera.ToString().ToLower());
                lines.Add("TraceLog=" + TraceLog.ToString().ToLower());
                lines.Add("PadEnabled=" + PadEnabled.ToString().ToLower());
                lines.Add("PadZoomMode=" + PadZoomMode);
                lines.Add("PadZoomSpeed=" + PadZoomSpeed.ToString("F2", CultureInfo.InvariantCulture));
                lines.Add("PadDeadzone=" + PadDeadzone.ToString("F2", CultureInfo.InvariantCulture));
                lines.Add("PadDirectResponse=" + PadDirectResponse.ToString("F2", CultureInfo.InvariantCulture));
                lines.Add("SlideSensitivity=" + SlideSensitivity.ToString("F2", CultureInfo.InvariantCulture));
                lines.Add("AutoHidePistol=" + AutoHidePistol.ToString().ToLower());
                lines.Add("; 1 = one tip per game start when the GTAVive VR mod is not loaded, 0 = never");
                lines.Add("ShowCompanionTip=" + (ShowCompanionTip ? "1" : "0"));
                lines.Add("");
                lines.Add("; Last Used Preset");
                lines.Add("LastPreset=" + LastPreset);
                lines.Add("");
                lines.AddRange(KeyBinds.IniLines());
                lines.Add("");
                lines.Add("; ===== PRESETS =====");
                lines.Add("; Format: PresetName=FOV,ZoomInSmooth,ZoomOutSmooth,UseClick,ZoomInLock,ZoomOutLock,SoftMin,SoftMax,OffsetX,OffsetY,OffsetZ,RotationX,FlashLock");

                // Read all existing presets from the file so we don't lose them
                Dictionary<string, string> existingPresets = new Dictionary<string, string>();
                List<string> existingPresetOrder = new List<string>();
                if (File.Exists(IniPath))
                {
                    bool inSection = false;
                    foreach (var fileLine in File.ReadAllLines(IniPath))
                    {
                        string trimmed = fileLine.Trim();
                        if (trimmed.Contains("===== PRESETS =====")) { inSection = true; continue; }
                        if (!inSection || string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";")) continue;
                        int eqIdx = trimmed.IndexOf('=');
                        if (eqIdx > 0)
                        {
                            string pName = trimmed.Substring(0, eqIdx).Trim();
                            string pData = trimmed.Substring(eqIdx + 1).Trim();
                            if (!existingPresets.ContainsKey(pName))
                            {
                                existingPresets[pName] = pData;
                                existingPresetOrder.Add(pName);
                            }
                        }
                    }
                }

                // Write all old presets back
                foreach (string pName in existingPresetOrder)
                    lines.Add(pName + "=" + existingPresets[pName]);

                if (appendNewPreset)
                {
                    // Build and append the new preset entry
                    string presetName = SanitizePresetName(customPresetName);
                    if (string.IsNullOrEmpty(presetName) || existingPresets.ContainsKey(presetName))
                        presetName = GeneratePresetName();
                    string presetData = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
                        CurrentFOV.ToString("F1", CultureInfo.InvariantCulture),
                        ZoomInSmoothness.ToString("F2", CultureInfo.InvariantCulture),
                        ZoomOutSmoothness.ToString("F2", CultureInfo.InvariantCulture),
                        UseClickMode.ToString().ToLower(),
                        ZoomInLocked.ToString().ToLower(),
                        ZoomOutLocked.ToString().ToLower(),
                        SoftMinFOV.ToString("F1", CultureInfo.InvariantCulture),
                        SoftMaxFOV.ToString("F1", CultureInfo.InvariantCulture),
                        FlashlightOffsetX.ToString("F3", CultureInfo.InvariantCulture),
                        FlashlightOffsetY.ToString("F3", CultureInfo.InvariantCulture),
                        FlashlightOffsetZ.ToString("F3", CultureInfo.InvariantCulture),
                        FlashlightRotationX.ToString("F1", CultureInfo.InvariantCulture),
                        FlashlightMovementLocked.ToString().ToLower());
                    lines.Add(presetName + "=" + presetData);
                    LastPreset = presetName;
                }

                File.WriteAllLines(IniPath, lines);
            }
            catch (Exception ex)
            {
                GTA.UI.Screen.ShowSubtitle("Error saving: " + ex.Message, 3000);
            }
        }

        /// <summary>
        /// Generates a preset name in the format {mm}MM_FLSH{x}{y}{z}-{rot}_{DDMMMYY-HHMM}:
        /// focal length, flashlight offset X/Y/Z (whole numbers), pitch in degrees, save time.
        /// </summary>
        private static string GeneratePresetName()
        {
            int mm = FOVToMillimeter(CurrentFOV);
            DateTime now = DateTime.Now;

            // Round X, Y, Z to nearest whole number (single digit format)
            int x = (int)Math.Round(Math.Abs(FlashlightOffsetX));
            int y = (int)Math.Round(Math.Abs(FlashlightOffsetY));
            int z = (int)Math.Round(Math.Abs(FlashlightOffsetZ));
            int rot = (int)Math.Round(Math.Abs(FlashlightRotationX));

            // Clamp to single digits (0-9), but allow up to 2 digits if needed
            x = Math.Min(x, 99);
            y = Math.Min(y, 99);
            z = Math.Min(z, 99);

            string date = now.ToString("ddMMMyy-HHmm", CultureInfo.InvariantCulture).ToUpper();

            return string.Format("{0}MM_FLSH{1}{2}{3}-{4:000}_{5}", mm, x, y, z, rot, date);
        }

        private static bool ParseBool(string value, out bool result)
        {
            if (value == "1") { result = true; return true; }
            if (value == "0") { result = false; return true; }
            return bool.TryParse(value, out result);
        }

        /// <summary>
        /// Loads configuration values from the specified INI file.
        /// </summary>
        private static void Load(string path)
        {
            if (!File.Exists(path))
                return;

            string[] lines = File.ReadAllLines(path);
            Dictionary<string, string> presets = new Dictionary<string, string>();
            bool inPresetsSection = false;
            bool inKeysSection = false;

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";"))
                {
                    // Check if we're entering the presets section
                    if (trimmed.Contains("===== PRESETS ====="))
                    {
                        inPresetsSection = true;
                    }
                    continue;
                }

                if (!inPresetsSection && trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    inKeysSection = string.Equals(trimmed, "[Keys]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                int idx = trimmed.IndexOf('=');
                if (idx < 0)
                    continue;

                string key = trimmed.Substring(0, idx).Trim();
                string value = trimmed.Substring(idx + 1).Trim();

                // If we're in the presets section, treat all entries as presets
                if (inPresetsSection)
                {
                    presets[key] = value;
                    continue;
                }

                if (inKeysSection)
                {
                    KeyBinds.LoadEntry(key, value);
                    continue;
                }

                bool bVal;
                float fVal;
                switch (key)
                {
                    case "DisableSmoothing":
                        if (bool.TryParse(value, out bVal))
                            DisableSmoothing = bVal;
                        break;
                    case "BaseWidth":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            BaseWidth = fVal;
                        break;
                    case "PresetHeights":
                        PresetHeights = value.Split(',')
                            .Select(s =>
                            {
                                float result;
                                return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? result : 0.35f;
                            }).ToArray();
                        break;
                    case "AspectRatioLabels":
                        AspectRatioLabels = value.Split(',')
                            .Select(s => s.Trim()).ToArray();
                        break;
                    case "ViewfinderSmoothingFactor":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            ViewfinderSmoothingFactor = fVal;
                        break;
                    case "AspectSmoothingFactor":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            AspectSmoothingFactor = fVal;
                        break;
                    case "DefaultZoom":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            DefaultZoom = fVal;
                        break;
                    case "ZoomLevels":
                        ZoomLevels = value.Split(',')
                            .Select(s =>
                            {
                                float result;
                                return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? result : 1.0f;
                            }).ToArray();
                        break;
                    case "PositionalSmoothingFactor":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            PositionalSmoothingFactor = fVal;
                        break;
                    case "RotationSmoothingFactor":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            RotationSmoothingFactor = fVal;
                        break;
                    case "ZoomInSmoothness":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            ZoomInSmoothness = fVal;
                        break;
                    case "ZoomOutSmoothness":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            ZoomOutSmoothness = fVal;
                        break;
                    case "CurrentFOV":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal) && !float.IsNaN(fVal) && !float.IsInfinity(fVal))
                            CurrentFOV = fVal;
                        break;
                    case "UseClickMode":
                        if (bool.TryParse(value, out bVal))
                            UseClickMode = bVal;
                        break;
                    case "ZoomInLocked":
                        if (bool.TryParse(value, out bVal))
                            ZoomInLocked = bVal;
                        break;
                    case "ZoomOutLocked":
                        if (bool.TryParse(value, out bVal))
                            ZoomOutLocked = bVal;
                        break;
                    case "SoftMinFOV":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            SoftMinFOV = fVal;
                        break;
                    case "SoftMaxFOV":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            SoftMaxFOV = fVal;
                        break;
                    case "ZoomRangeMinFOV":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal) && !float.IsNaN(fVal) && !float.IsInfinity(fVal))
                            ZoomRangeMinFOV = fVal;
                        break;
                    case "ZoomRangeMaxFOV":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal) && !float.IsNaN(fVal) && !float.IsInfinity(fVal))
                            ZoomRangeMaxFOV = fVal;
                        break;
                    case "FlashlightOffsetX":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            FlashlightOffsetX = fVal;
                        break;
                    case "FlashlightOffsetY":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            FlashlightOffsetY = fVal;
                        break;
                    case "FlashlightOffsetZ":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            FlashlightOffsetZ = fVal;
                        break;
                    case "FlashlightRotationX":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            FlashlightRotationX = fVal;
                        break;
                    case "FlashlightMovementLocked":
                        if (bool.TryParse(value, out bVal))
                            FlashlightMovementLocked = bVal;
                        break;
                    case "FlashlightFollowView":
                        if (bool.TryParse(value, out bVal))
                            FlashlightFollowView = bVal;
                        break;
                    case "FlashlightViewOffsetX":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            FlashlightViewOffsetX = fVal;
                        break;
                    case "FlashlightViewOffsetY":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            FlashlightViewOffsetY = fVal;
                        break;
                    case "FlashlightViewOffsetZ":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            FlashlightViewOffsetZ = fVal;
                        break;
                    case "FlashlightBeam":
                        if (bool.TryParse(value, out bVal)) FlashlightBeam = bVal;
                        break;
                    case "FlashlightBrightness":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            FlashlightBrightness = Math.Max(0.5f, Math.Min(50f, fVal));
                        break;
                    case "FlashlightRange":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            FlashlightRange = Math.Max(5f, Math.Min(150f, fVal));
                        break;
                    case "FlashlightWidth":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            FlashlightWidth = Math.Max(5f, Math.Min(80f, fVal));
                        break;
                    case "FlashlightShadows":
                        if (bool.TryParse(value, out bVal)) FlashlightShadows = bVal;
                        break;
                    case "FlashlightFeather":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            FlashlightFeather = Math.Max(0.1f, Math.Min(20f, fVal));
                        break;
                    case "FlashlightRoundness":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            FlashlightRoundness = Math.Max(0f, Math.Min(80f, fVal));
                        break;
                    case "EditorLight":
                        if (bool.TryParse(value, out bVal)) EditorLight = bVal;
                        break;
                    case "CameraLight":
                        if (bool.TryParse(value, out bVal)) CameraLight = bVal;
                        break;
                    case "CameraLightHeightCm":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            CameraLightHeightCm = Math.Max(-30f, Math.Min(30f, fVal));
                        break;
                    case "CameraLightPitch":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
                            CameraLightPitch = Math.Max(-45f, Math.Min(45f, fVal));
                        break;
                    case "ShowHudInfo":
                        if (bool.TryParse(value, out bVal))
                            ShowHudInfo = bVal;
                        break;
                    case "ShowControlsHelp":
                        if (bool.TryParse(value, out bVal))
                            ShowControlsHelp = bVal;
                        break;
                    case "BmxRiderEnabled":
                        if (bool.TryParse(value, out bVal)) BmxRiderEnabled = bVal;
                        break;
                    case "FocusFollowsCamera":
                        if (bool.TryParse(value, out bVal)) FocusFollowsCamera = bVal;
                        break;
                    case "TraceLog":
                        if (bool.TryParse(value, out bVal)) TraceLog = bVal;
                        break;
                    case "PadEnabled":
                        if (bool.TryParse(value, out bVal)) PadEnabled = bVal;
                        break;
                    case "PadZoomMode":
                        int pzm; if (int.TryParse(value, out pzm)) PadZoomMode = Math.Max(0, Math.Min(5, pzm));
                        break;
                    case "PadZoomSpeed":
                        float pzs; if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out pzs)) PadZoomSpeed = pzs;
                        break;
                    case "PadDeadzone":
                        float pdz; if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out pdz)) PadDeadzone = Math.Max(0.02f, Math.Min(0.6f, pdz));
                        break;
                    case "PadDirectResponse":
                        float pdr; if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out pdr)) PadDirectResponse = Math.Max(0.02f, Math.Min(1f, pdr));
                        break;
                    case "ZoomMode":
                        int zm; if (int.TryParse(value, out zm)) ZoomMode = Math.Max(0, Math.Min(2, zm));
                        break;
                    case "SlideSensitivity":
                        float ss; if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out ss)) SlideSensitivity = ss;
                        break;
                    case "AutoHidePistol":
                        if (bool.TryParse(value, out bVal))
                            AutoHidePistol = bVal;
                        break;
                    case "ShowCompanionTip":
                        if (ParseBool(value, out bVal)) ShowCompanionTip = bVal;
                        break;
                    case "LastPreset":
                        LastPreset = value;
                        break;
                }
            }

            // Load last preset if it exists
            if (!string.IsNullOrEmpty(LastPreset) && presets.ContainsKey(LastPreset))
            {
                LoadPreset(LastPreset, presets[LastPreset]);
            }
        }

        /// <summary>
        /// Loads a preset from string data
        /// </summary>
        private static void LoadPreset(string name, string data)
        {
            string[] parts = data.Split(',');
            if (parts.Length >= 13)
            {
                if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float fov))
                    CurrentFOV = fov;
                if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float zIn))
                    ZoomInSmoothness = zIn;
                if (float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float zOut))
                    ZoomOutSmoothness = zOut;
                if (bool.TryParse(parts[3], out bool click))
                    UseClickMode = click;
                if (bool.TryParse(parts[4], out bool zInLock))
                    ZoomInLocked = zInLock;
                if (bool.TryParse(parts[5], out bool zOutLock))
                    ZoomOutLocked = zOutLock;
                if (float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float softMin))
                    SoftMinFOV = softMin;
                if (float.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float softMax))
                    SoftMaxFOV = softMax;
                if (float.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out float offX))
                    FlashlightOffsetX = offX;
                if (float.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out float offY))
                    FlashlightOffsetY = offY;
                if (float.TryParse(parts[10], NumberStyles.Float, CultureInfo.InvariantCulture, out float offZ))
                    FlashlightOffsetZ = offZ;
                if (float.TryParse(parts[11], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotX))
                    FlashlightRotationX = rotX;
                if (bool.TryParse(parts[12], out bool flashLock))
                    FlashlightMovementLocked = flashLock;
            }
            else if (parts.Length >= 8) // older 8-field preset format
            {
                if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float fov))
                    CurrentFOV = fov;
                if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float zIn))
                    ZoomInSmoothness = zIn;
                if (float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float zOut))
                    ZoomOutSmoothness = zOut;
                if (bool.TryParse(parts[3], out bool click))
                    UseClickMode = click;
                if (float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float offX))
                    FlashlightOffsetX = offX;
                if (float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float offY))
                    FlashlightOffsetY = offY;
                if (float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float offZ))
                    FlashlightOffsetZ = offZ;
                if (float.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotX))
                    FlashlightRotationX = rotX;
            }
        }

        /// <summary>
        /// Get all preset names from INI file
        /// </summary>
        public static List<string> GetPresetNames()
        {
            List<string> presets = new List<string>();

            if (!File.Exists(IniPath))
                return presets;

            string[] lines = File.ReadAllLines(IniPath);
            bool inPresetsSection = false;

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";"))
                {
                    // Check if we're entering the presets section
                    if (trimmed.Contains("===== PRESETS ====="))
                    {
                        inPresetsSection = true;
                    }
                    continue;
                }

                // If we're in the presets section, all non-comment lines with = are presets
                if (inPresetsSection)
                {
                    int idx = trimmed.IndexOf('=');
                    if (idx > 0)
                    {
                        string key = trimmed.Substring(0, idx).Trim();
                        presets.Add(key);
                    }
                }
            }

            return presets;
        }

        /// <summary>
        /// Load a specific preset by name
        /// </summary>
        public static void LoadPresetByName(string presetName)
        {
            if (!File.Exists(IniPath))
                return;

            string[] lines = File.ReadAllLines(IniPath);
            bool inPresetsSection = false;

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";"))
                {
                    // Check if we're entering the presets section
                    if (trimmed.Contains("===== PRESETS ====="))
                    {
                        inPresetsSection = true;
                    }
                    continue;
                }

                if (!inPresetsSection)
                    continue;

                int idx = trimmed.IndexOf('=');
                if (idx < 0)
                    continue;

                string key = trimmed.Substring(0, idx).Trim();
                string value = trimmed.Substring(idx + 1).Trim();

                if (key == presetName)
                {
                    LoadPreset(presetName, value);
                    LastPreset = presetName;
                    break;
                }
            }
        }
    }
}
