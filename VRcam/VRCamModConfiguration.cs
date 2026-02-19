using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;

namespace VRCam
{
    public static class VRCamModConfiguration
    {
        // Global switch for smoothing on shared mods.
        public static bool DisableSmoothing { get; set; } = false;

        // Viewfinder configuration.
        public static float BaseWidth { get; set; } = 0.5f;
        public static float[] PresetHeights { get; set; } = new float[] { 0.35f, 0.27f };
        public static string[] AspectRatioLabels { get; set; } = new string[] { "16:9", "21:9" };

        // Smoothing factors for the viewfinder transitions.
        public static float ViewfinderSmoothingFactor { get; set; } = 0.1f;
        public static float AspectSmoothingFactor { get; set; } = 0.1f;

        // Zoom settings.
        public static float DefaultZoom { get; set; } = 1.0f;
        public static float[] ZoomLevels { get; set; } = new float[] { 0.80f, 1.0f, 1.5f, 2.0f, 3.0f, 4.0f };

        // Settings for rat attachment smoothing.
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

        // Flashlight settings
        public static float FlashlightOffsetX { get; set; } = 0.0f;
        public static float FlashlightOffsetY { get; set; } = 0.0f;
        public static float FlashlightOffsetZ { get; set; } = 0.0f;
        public static float FlashlightRotationX { get; set; } = 0.0f;
        public static bool FlashlightMovementLocked { get; set; } = true;

        // Last used preset
        public static string LastPreset { get; set; } = "";

        private const string IniPath = "VRcam.ini";

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
            try
            {
                List<string> lines = new List<string>();
                
                // Write configuration settings
                lines.Add("; VRCam Configuration File");
                lines.Add("; Edit values and reload game or press Ctrl+F10 to apply");
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
                lines.Add("");
                lines.Add("; Flashlight Settings");
                lines.Add("FlashlightOffsetX=" + FlashlightOffsetX.ToString("F3", CultureInfo.InvariantCulture));
                lines.Add("FlashlightOffsetY=" + FlashlightOffsetY.ToString("F3", CultureInfo.InvariantCulture));
                lines.Add("FlashlightOffsetZ=" + FlashlightOffsetZ.ToString("F3", CultureInfo.InvariantCulture));
                lines.Add("FlashlightRotationX=" + FlashlightRotationX.ToString("F1", CultureInfo.InvariantCulture));
                lines.Add("FlashlightMovementLocked=" + FlashlightMovementLocked.ToString().ToLower());
                lines.Add("");
                lines.Add("; Last Used Preset");
                lines.Add("LastPreset=" + LastPreset);
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

                // Build the new preset entry
                string presetName = GeneratePresetName();
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

                // Write all old presets back, then append the new one
                foreach (string pName in existingPresetOrder)
                    lines.Add(pName + "=" + existingPresets[pName]);
                lines.Add(presetName + "=" + presetData);
                LastPreset = presetName;
                
                File.WriteAllLines(IniPath, lines);
            }
            catch (Exception ex)
            {
                GTA.UI.Screen.ShowSubtitle("Error saving: " + ex.Message, 3000);
            }
        }

        /// <summary>
        /// Generates preset name in format: 35MM_FLSH125-000_16JAN26-1430
        /// Where: 35MM = focal length, FLSH125 = XYZ (1 digit each), 000 = rotation degrees
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
            
            // Format: 35MM_FLSH125-000_16JAN26-1430
            return string.Format("{0}MM_FLSH{1}{2}{3}-{4:000}_{5}", mm, x, y, z, rot, date);
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
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fVal))
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
            else if (parts.Length >= 8) // Legacy format support
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