using System.Globalization;

namespace Aimmy2.Class
{
    internal static class AimSettings
    {
        public static int ImageSize
        {
            get => int.Parse(GetDropdown("Image Size"), CultureInfo.InvariantCulture);
            set => SetDropdown("Image Size", value.ToString(CultureInfo.InvariantCulture));
        }

        public static string ScreenCaptureMethod
        {
            get => GetDropdown("Screen Capture Method");
            set => SetDropdown("Screen Capture Method", value);
        }

        public static string DetectionAreaType => GetDropdown("Detection Area Type");
        public static string TargetClass => GetDropdown("Target Class");
        public static string PredictionMethod => GetDropdown("Prediction Method");
        public static string AimingBoundariesAlignment => GetDropdown("Aiming Boundaries Alignment");
        public static string TracerPosition => GetDropdown("Tracer Position");
        public static string MovementPath => GetDropdown("Movement Path");
        public static string MouseMovementMethod => GetDropdown("Mouse Movement Method");
        public static string Esp32ComPort => GetDropdown("ESP32 COM Port");

        public static bool UseDistantTargetModel => GetToggle("Use Distant Target Model");
        public static float DistantTargetMinConfidence => (float)(GetSlider("Distant Target Min Confidence") / 100.0);
        public static string DistantTargetModelPath => GetFileLocation("Distant Target Model Location");

        public static bool TeamCheck => GetToggle("Team Check");
        public static float TeamCheckRegionHeightPercent => (float)(GetSlider("Team Check Region Height %") / 100.0);
        public static float TeamCheckRegionGapPercent => (float)(GetSlider("Team Check Region Gap %") / 100.0);
        public static bool TeamCheckDebug => GetToggle("Team Check Debug");
        public static float TeamCheckHueTolerance => (float)GetSlider("Team Check Hue Tolerance");
        public static float TeamCheckMinSaturation => (float)(GetSlider("Team Check Min Saturation") / 100.0);
        public static float TeamCheckMinMatchRatio => (float)(GetSlider("Team Check Min Match Ratio") / 100.0);

        public static bool RecoilCompensation => GetToggle("Recoil Compensation");
        public static float RecoilStrength => (float)(GetSlider("Recoil Strength") / 100.0);
        public static string RecoilWeaponProfile => GetDropdown("Recoil Weapon Profile");
        public static float GamepadStickSensitivity => (float)(GetSlider("Gamepad Stick Sensitivity") / 100.0);

        public static bool BurstMode => GetToggle("Burst Mode");
        public static int BurstCount => (int)GetSlider("Burst Count");
        public static int BurstCooldownMilliseconds => (int)GetSlider("Burst Cooldown");

        public static bool PrecisionLock => GetToggle("Precision Lock");
        public static double PrecisionLockRadius => GetSlider("Precision Lock Radius");

        public static bool SnapZone => GetToggle("Snap Zone");
        public static bool SnapZoneHardCutoff => GetToggle("Snap Zone Hard Cutoff");
        public static double SnapZoneSize => GetSlider("Snap Zone Size");
        public static float SnapZoneOutsideStrength => (float)(GetSlider("Snap Zone Outside Strength %") / 100.0);

        public static System.Windows.Media.Color TeamCheckNameplateColor
        {
            get
            {
                try
                {
                    var converted = System.Windows.Media.ColorConverter.ConvertFromString(
                        Convert.ToString(Dictionary.colorState["Team Check Nameplate Color"], CultureInfo.InvariantCulture));
                    return converted is System.Windows.Media.Color c ? c : System.Windows.Media.Colors.Gold;
                }
                catch
                {
                    return System.Windows.Media.Colors.Gold;
                }
            }
        }

        public static double FovSize => GetSlider("FOV Size");
        public static float MinimumConfidence => (float)(GetSlider("AI Minimum Confidence") / 100.0);
        public static double StickyAimThreshold => GetSlider("Sticky Aim Threshold");
        public static double OverlayOpacity => GetSlider("Opacity");
        public static double YOffset => GetSlider("Y Offset (Up/Down)");
        public static double XOffset => GetSlider("X Offset (Left/Right)");
        public static double YOffsetPercent => GetSlider("Y Offset (%)");
        public static double XOffsetPercent => GetSlider("X Offset (%)");
        public static int AutoTriggerDelayMilliseconds => (int)(GetSlider("Auto Trigger Delay") * 1000);
        public static int MouseJitter => (int)GetSlider("Mouse Jitter");
        public static double MouseSensitivity => GetSlider("Mouse Sensitivity (+/-)");
        public static double KalmanLeadTime => GetSlider("Kalman Lead Time");
        public static double WiseTheFoxLeadTime => GetSlider("WiseTheFox Lead Time");
        public static double ShalloeLeadMultiplier => GetSlider("Shalloe Lead Multiplier");
        public static int AiFpsLimit => Math.Max(0, (int)Math.Round(GetSlider("AI FPS Limit")));

        // Auto Trigger is on if either the toggle is permanently on, or the hold
        // keybind is currently held - lets you keep the toggle off by default and
        // only fire while actively holding a key, without changing saved state.
        public static bool AutoTrigger => GetToggle("Auto Trigger") || InputLogic.InputBindingManager.IsHoldingBinding("Auto Trigger Keybind");
        public static bool ConstantAiTracking => GetToggle("Constant AI Tracking");
        public static bool SprayMode => GetToggle("Spray Mode");
        public static bool CursorCheck => GetToggle("Cursor Check");
        public static bool AimAssist => GetToggle("Aim Assist");
        public static bool ShowDetectedPlayer => GetToggle("Show Detected Player");
        public static bool ShowFov => GetToggle("FOV");
        public static bool ShowAiConfidence => GetToggle("Show AI Confidence");
        public static bool ShowTracers => GetToggle("Show Tracers");
        public static bool UseXAxisPercentageAdjustment => GetToggle("X Axis Percentage Adjustment");
        public static bool UseYAxisPercentageAdjustment => GetToggle("Y Axis Percentage Adjustment");
        public static bool Predictions => GetToggle("Predictions");
        public static bool StickyAim => GetToggle("Sticky Aim");
        public static bool CollectDataWhilePlaying => GetToggle("Collect Data While Playing");
        public static bool AutoLabelData => GetToggle("Auto Label Data");
        public static bool ThirdPersonSupport => GetToggle("Third Person Support");

        public static double GetSlider(string key) =>
            Convert.ToDouble(Dictionary.sliderSettings[key], CultureInfo.InvariantCulture);

        public static bool GetToggle(string key) =>
            Convert.ToBoolean(Dictionary.toggleState[key], CultureInfo.InvariantCulture);

        public static string GetDropdown(string key) =>
            Convert.ToString(Dictionary.dropdownState[key], CultureInfo.InvariantCulture) ?? string.Empty;

        public static string GetFileLocation(string key) =>
            Convert.ToString(Dictionary.filelocationState[key], CultureInfo.InvariantCulture) ?? string.Empty;

        private static void SetDropdown(string key, string value)
        {
            Dictionary.dropdownState[key] = value;
        }
    }
}
