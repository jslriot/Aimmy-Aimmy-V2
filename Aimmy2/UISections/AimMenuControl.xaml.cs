using Aimmy2.AILogic;
using Aimmy2.Class;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Aimmy2.UILibrary;
using Class;
using InputLogic;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using Other;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UILibrary;

namespace Aimmy2.Controls
{
    public partial class AimMenuControl : UserControl
    {
        //--
        UISections.ColorPicker colorPickerInstance = null;
        UISections.ColorPicker fovColorPickerInstance = null;
        UISections.ColorPicker teamCheckColorPickerInstance = null;
        UISections.ColorPicker snapZoneColorPickerInstance = null;
        UISections.TargetPointPicker targetPointPickerInstance = null;
        UISections.RecoilPatternEditor recoilPatternEditorInstance = null;
        //--
        private MainWindow? _mainWindow;
        private bool _isInitialized;

        // Local minimize state management
        private readonly Dictionary<string, bool> _localMinimizeState = new()
        {
            { "Aim Assist", false },
            { "Aim Config", false },
            { "Predictions", false },
            { "Auto Trigger", false },
            { "FOV Config", false },
            { "ESP Config", false }
        };

        // Public properties for MainWindow access
        public StackPanel AimAssistPanel => AimAssist;
        public StackPanel TriggerBotPanel => TriggerBot;
        public StackPanel ESPConfigPanel => ESPConfig;
        public StackPanel AimConfigPanel => AimConfig;
        public StackPanel PredictionsPanel => Predictions;
        public StackPanel FOVConfigPanel => FOVConfig;
        public ScrollViewer AimMenuScrollViewer => AimMenu;

        public AimMenuControl()
        {
            InitializeComponent();
        }

        public void Initialize(MainWindow mainWindow)
        {
            if (_isInitialized) return;

            _mainWindow = mainWindow;
            _isInitialized = true;

            // Load minimize states from global dictionary if they exist
            LoadMinimizeStatesFromGlobal();

            AIManager.ImageSizeUpdated += OnImageSizeChanged;

            // Load all sections
            LoadAimAssist();
            LoadAimConfig();
            LoadPredictions();
            LoadTriggerBot();
            LoadFOVConfig();
            LoadESPConfig();

            // Apply minimize states after loading
            ApplyMinimizeStates();
        }

        #region Minimize State Management

        private void LoadMinimizeStatesFromGlobal()
        {
            foreach (var key in _localMinimizeState.Keys.ToList())
            {
                if (Dictionary.minimizeState.ContainsKey(key))
                {
                    _localMinimizeState[key] = Dictionary.minimizeState[key];
                }
            }
        }

        private void SaveMinimizeStatesToGlobal()
        {
            foreach (var kvp in _localMinimizeState)
            {
                Dictionary.minimizeState[kvp.Key] = kvp.Value;
            }
        }

        private void ApplyMinimizeStates()
        {
            ApplyPanelState("Aim Assist", AimAssistPanel);
            ApplyPanelState("Aim Config", AimConfigPanel);
            ApplyPanelState("Predictions", PredictionsPanel);
            ApplyPanelState("Auto Trigger", TriggerBotPanel);
            ApplyPanelState("FOV Config", FOVConfigPanel);
            ApplyPanelState("ESP Config", ESPConfigPanel);
        }

        private void ApplyPanelState(string stateName, StackPanel panel)
        {
            if (_localMinimizeState.TryGetValue(stateName, out bool isMinimized))
            {
                SetPanelVisibility(panel, !isMinimized);
            }
        }

        private void SetPanelVisibility(StackPanel panel, bool isVisible)
        {
            foreach (UIElement child in panel.Children)
            {
                // Keep titles, spacers, and bottom rectangles always visible
                bool shouldStayVisible = child is ATitle || child is ASpacer || child is ARectangleBottom;

                child.Visibility = shouldStayVisible
                    ? Visibility.Visible
                    : (isVisible ? Visibility.Visible : Visibility.Collapsed);
            }
        }

        private void TogglePanel(string stateName, StackPanel panel)
        {
            if (!_localMinimizeState.ContainsKey(stateName)) return;

            // Toggle the state
            _localMinimizeState[stateName] = !_localMinimizeState[stateName];

            // Apply the new visibility
            SetPanelVisibility(panel, !_localMinimizeState[stateName]);

            // Save to global dictionary
            SaveMinimizeStatesToGlobal();
        }


        #endregion

        #region Menu Section Loaders

        private void LoadAimAssist()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, AimAssist);

            builder
                .AddTitle("Aim Assist", true, t =>
                {
                    uiManager.AT_Aim = t;
                    t.Minimize.Click += (s, e) =>
                    {
                        TogglePanel("Aim Assist", AimAssistPanel);
                        _mainWindow?.UpdateAimAssistSliderVisibility();
                    };
                })
                .AddToggle("Aim Assist", t =>
                {
                    uiManager.T_AimAligner = t;
                    t.Reader.Click += (s, e) =>
                    {
                        if (Dictionary.toggleState["Aim Assist"] && Dictionary.lastLoadedModel == "N/A")
                        {
                            Dictionary.toggleState["Aim Assist"] = false;
                            _mainWindow.UpdateToggleUI(t, false);
                            LogManager.Log(LogManager.LogLevel.Warning, "Please load a model first", true);
                        }
                    };
                }, tooltip: "Turn aim assist on or off. You must load a model first.")
                .AddToggle("Constant AI Tracking", t =>
                {
                    uiManager.T_ConstantAITracking = t;
                    t.Reader.Click += (s, e) =>
                    {
                        if (Dictionary.toggleState["Constant AI Tracking"])
                        {
                            if (Dictionary.lastLoadedModel == "N/A")
                            {
                                Dictionary.toggleState["Constant AI Tracking"] = false;
                                _mainWindow.UpdateToggleUI(t, false);
                            }
                            else
                            {
                                Dictionary.toggleState["Aim Assist"] = true;
                                _mainWindow.UpdateToggleUI(uiManager.T_AimAligner, true);
                            }
                        }
                    };
                }, tooltip: "Always track targets without holding a key. When off, you must hold the aim keybind.")
                .AddToggle("Sticky Aim", t => uiManager.T_StickyAim = t,
                    tooltip: "Lock onto a target until it moves out of range instead of switching targets.")
                .AddSlider("Sticky Aim Threshold", "Pixels", 1, 1, 0, 100, s =>
                {
                    uiManager.S_StickyAimThreshold = s;
                    // Set initial visibility based on toggle state
                    s.Visibility = Dictionary.toggleState["Sticky Aim"]
                        ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "How far a target must move before switching to a new one. Higher = stays locked longer.")
                .AddToggle("Team Check", t => uiManager.T_TeamCheck = t,
                    tooltip: "Skip targets with a nameplate above their head (your teammates). Enemies won't be targeted if they also show nameplates in your game.")
                .AddSlider("Team Check Region Height %", "%", 1, 1, 10, 300, s =>
                {
                    uiManager.S_TeamCheckRegionHeight = s;
                    s.Visibility = Dictionary.toggleState["Team Check"] ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "How tall a region above each target's head to scan for a nameplate, as a percentage of their bounding box height.")
                .AddSlider("Team Check Region Gap %", "%", 1, 0, 0, 100, s =>
                {
                    uiManager.S_TeamCheckRegionGap = s;
                    s.Visibility = Dictionary.toggleState["Team Check"] ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "Small gap left between the top of the target's box and the nameplate scan region.")
                .AddColorChanger("Team Check Nameplate Color", c =>
                {
                    uiManager.CC_TeamCheckNameplateColor = c;
                    c.Reader.Click += (s, e) =>
                    {
                        if (teamCheckColorPickerInstance != null && teamCheckColorPickerInstance.IsVisible)
                        {
                            teamCheckColorPickerInstance.Activate();
                            return;
                        }

                        Color initialColor = Colors.Gold;
                        if (c.ColorChangingBorder.Background is SolidColorBrush scb)
                            initialColor = scb.Color;
                        teamCheckColorPickerInstance = new UISections.ColorPicker(initialColor, "Team Check Nameplate Color");

                        teamCheckColorPickerInstance.ColorChanged += (color) =>
                        {
                            c.ColorChangingBorder.Background = new SolidColorBrush(color);
                            Dictionary.colorState["Team Check Nameplate Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                        };

                        teamCheckColorPickerInstance.Closed += (sender, args) =>
                        {
                            teamCheckColorPickerInstance = null;
                        };

                        teamCheckColorPickerInstance.Show();
                    };
                })
                .AddSlider("Team Check Hue Tolerance", "deg", 1, 0, 1, 90, s =>
                {
                    uiManager.S_TeamCheckHueTolerance = s;
                    s.Visibility = Dictionary.toggleState["Team Check"] ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "How far a pixel's color can differ from the nameplate color (in hue degrees) and still count as a match. Lower = stricter.")
                .AddSlider("Team Check Min Saturation", "%", 1, 0, 0, 100, s =>
                {
                    uiManager.S_TeamCheckMinSaturation = s;
                    s.Visibility = Dictionary.toggleState["Team Check"] ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "Minimum color saturation for a pixel to be considered. Filters out gray/desaturated background.")
                .AddSlider("Team Check Min Match Ratio", "%", 1, 1, 0, 100, s =>
                {
                    uiManager.S_TeamCheckMinMatchRatio = s;
                    s.Visibility = Dictionary.toggleState["Team Check"] ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "Minimum percentage of the scan region that must match the nameplate color to flag it as a teammate. Lower = catches smaller/farther nameplates but more false positives.")
                .AddToggle("Team Check Debug", t => uiManager.T_TeamCheckDebug = t,
                    tooltip: "Saves screenshots to bin/images/teamcheck_debug showing exactly what region is being scanned for a nameplate, with the measured match % in the filename. Use this to diagnose detection issues, then turn it back off.")
                .AddKeyChanger("Aim Keybind", k => uiManager.C_Keybind = k,
                    tooltip: "The key you hold to activate aim assist.")
                .AddKeyChanger("Second Aim Keybind", tooltip: "An alternate key to activate aim assist.")
                .AddSeparator();
        }

        private void LoadAimConfig()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, AimConfig);

            builder
                .AddTitle("Aim Config", true, t =>
                {
                    uiManager.AT_AimConfig = t;
                    t.Minimize.Click += (s, e) =>
                    {
                        TogglePanel("Aim Config", AimConfigPanel);
                        _mainWindow?.UpdateAimConfigSliderVisibility();
                    };
                })
                .AddDropdown("Mouse Movement Method", d =>
                {
                    uiManager.D_MouseMovementMethod = d;
                    d.DropdownBox.SelectedIndex = -1;  // Prevent auto-selection

                    // Add options
                    _mainWindow.AddDropdownItem(d, "Mouse Event");
                    _mainWindow.AddDropdownItem(d, "SendInput");
                    uiManager.DDI_LGHUB = _mainWindow.AddDropdownItem(d, "LG HUB");
                    uiManager.DDI_RazerSynapse = _mainWindow.AddDropdownItem(d, "Razer Synapse (Require Razer Peripheral)");
                    uiManager.DDI_ddxoft = _mainWindow.AddDropdownItem(d, "ddxoft Virtual Input Driver");
                    uiManager.DDI_ESP32Serial = _mainWindow.AddDropdownItem(d, "ESP32 Serial");
                    uiManager.DDI_ESP32Gamepad = _mainWindow.AddDropdownItem(d, "ESP32 Gamepad");

                    // Setup handlers
                    uiManager.DDI_LGHUB.Selected += async (s, e) =>
                    {
                        if (!new LGHubMain().Load())
                            await ResetToMouseEvent();
                    };

                    uiManager.DDI_RazerSynapse.Selected += async (s, e) =>
                    {
                        if (!await RZMouse.Load())
                            await ResetToMouseEvent();
                    };

                    uiManager.DDI_ddxoft.Selected += async (s, e) =>
                    {
                        if (!await DdxoftMain.Load())
                            await ResetToMouseEvent();
                    };

                    uiManager.DDI_ESP32Serial.Selected += async (s, e) =>
                    {
                        string comPort = Dictionary.dropdownState.TryGetValue("ESP32 COM Port", out var port)
                            ? port?.ToString() ?? "COM3"
                            : "COM3";

                        bool connected = await Task.Run(() => MouseManager.InitializeESP32(comPort));
                        if (!connected)
                        {
                            LogManager.Log(LogManager.LogLevel.Error, $"Could not connect to ESP32 on {comPort}. Check the COM port and try again.", true);
                            await ResetToMouseEvent();
                        }
                        else
                        {
                            LogManager.Log(LogManager.LogLevel.Info, $"Connected to ESP32 on {comPort}.", true);
                        }
                    };

                    uiManager.DDI_ESP32Gamepad.Selected += async (s, e) =>
                    {
                        string comPort = Dictionary.dropdownState.TryGetValue("ESP32 COM Port", out var port)
                            ? port?.ToString() ?? "COM3"
                            : "COM3";

                        bool connected = await Task.Run(() => MouseManager.InitializeESP32(comPort));
                        if (!connected)
                        {
                            LogManager.Log(LogManager.LogLevel.Error, $"Could not connect to ESP32 on {comPort}. Check the COM port and make sure it's flashed with the gamepad sketch, not the mouse one.", true);
                            await ResetToMouseEvent();
                        }
                        else
                        {
                            LogManager.Log(LogManager.LogLevel.Info, $"Connected to ESP32 (Gamepad) on {comPort}.", true);
                        }
                    };

                    // Swap Mouse Sensitivity <-> Gamepad Stick Sensitivity (and any other
                    // controller-only controls) whenever the movement method changes.
                    d.DropdownBox.SelectionChanged += (s, e) => _mainWindow?.UpdateMouseMovementSliderVisibility();
                }, tooltip: "How mouse movements are sent. Try different options if aim assist isn't working.")
                .AddDropdown("ESP32 COM Port", d =>
                {
                    uiManager.D_ESP32ComPort = d;
                    PopulateEsp32ComPorts(d);

                    d.DropdownBox.SelectionChanged += (s, e) =>
                    {
                        if (d.DropdownBox.SelectedItem is not ComboBoxItem selected) return;
                        string comPort = selected.Content?.ToString() ?? "COM3";

                        // Reconnect immediately if an ESP32 mode is already active
                        if (AimSettings.MouseMovementMethod == "ESP32 Serial" || AimSettings.MouseMovementMethod == "ESP32 Gamepad")
                        {
                            MouseManager.DisableESP32();
                            if (!MouseManager.InitializeESP32(comPort))
                            {
                                LogManager.Log(LogManager.LogLevel.Error, $"Could not connect to ESP32 on {comPort}.", true);
                            }
                        }
                    };
                }, tooltip: "COM port the ESP32 is connected to. Only used when Mouse Movement Method is set to ESP32 Serial.")
                .AddButton("Refresh COM Ports", b =>
                {
                    uiManager.B_ESP32RefreshPorts = b;
                    b.Reader.Click += (s, e) =>
                    {
                        if (uiManager.D_ESP32ComPort != null)
                            PopulateEsp32ComPorts(uiManager.D_ESP32ComPort);
                    };
                }, tooltip: "Rescan for available COM ports.")
                .AddSlider("Gamepad Stick Sensitivity", "%", 1, 0, 10, 300, s =>
                {
                    uiManager.S_GamepadStickSensitivity = s;
                    // Start collapsed - visibility will be set by UpdateMouseMovementSliderVisibility
                    s.Visibility = Visibility.Collapsed;
                }, tooltip: "Scales how far the right stick deflects for a given amount of aim error. Only used when Mouse Movement Method is set to ESP32 Gamepad. Higher = more aggressive stick push for the same target offset.")
                .AddDropdown("Movement Path", d =>
                {
                    d.DropdownBox.SelectedIndex = 0;
                    uiManager.D_MovementPath = d;
                    _mainWindow.AddDropdownItem(d, "Cubic Bezier");
                    _mainWindow.AddDropdownItem(d, "Exponential");
                    _mainWindow.AddDropdownItem(d, "Linear");
                    _mainWindow.AddDropdownItem(d, "Adaptive");
                    _mainWindow.AddDropdownItem(d, "Perlin Noise");
                }, tooltip: "The curve style used when moving to a target. Affects how natural the movement looks.")
                .AddDropdown("Detection Area Type", d =>
                {
                    d.DropdownBox.SelectedIndex = -1;
                    uiManager.D_DetectionAreaType = d;
                    uiManager.DDI_ClosestToCenterScreen = _mainWindow.AddDropdownItem(d, "Closest to Center Screen");
                    _mainWindow.AddDropdownItem(d, "Closest to Mouse");

                    uiManager.DDI_ClosestToCenterScreen.Selected += async (s, e) =>
                    {
                        await Task.Delay(100);
                        MainWindow.FOVWindow.FOVStrictEnclosure.Margin = new Thickness(
                            Convert.ToInt16((WinAPICaller.ScreenWidth / 2) / WinAPICaller.scalingFactorX) - 320,
                            Convert.ToInt16((WinAPICaller.ScreenHeight / 2) / WinAPICaller.scalingFactorY) - 320,
                            0, 0);
                    };
                }, tooltip: "How targets are prioritized. Center screen is best for most games.")
                .AddDropdown("Aiming Boundaries Alignment", d =>
                {
                    d.DropdownBox.SelectedIndex = -1;
                    uiManager.D_AimingBoundariesAlignment = d;
                    _mainWindow.AddDropdownItem(d, "Center");
                    _mainWindow.AddDropdownItem(d, "Top");
                    _mainWindow.AddDropdownItem(d, "Bottom");
                }, tooltip: "Where to aim on the detected target box. Center is usually best.");

            // Add sliders with validation
            AddConfigSliders(builder, uiManager);
            builder.AddSeparator();
        }

        private void AddConfigSliders(SectionBuilder builder, UI uiManager)
        {
            builder
                .AddSlider("Mouse Sensitivity (+/-)", "Sensitivity", 0.01, 0.01, 0.01, 1, s =>
                {
                    uiManager.S_MouseSensitivity = s;
                    s.Slider.PreviewMouseLeftButtonUp += (sender, e) =>
                    {
                        var value = s.Slider.Value;
                        if (value >= 0.98)
                            LogManager.Log(LogManager.LogLevel.Warning,
                                "The Mouse Sensitivity you have set can cause Aimmy to be unable to aim, please decrease if you suffer from this problem", true);
                        else if (value <= 0.1)
                            LogManager.Log(LogManager.LogLevel.Warning,
                                "The Mouse Sensitivity you have set can cause Aimmy to be unstable to aim, please increase if you suffer from this problem", true);
                    };
                }, tooltip: "How fast the aim moves. Lower = faster and snappier, higher = slower and smoother.")
                .AddSlider("Mouse Jitter", "Jitter", 1, 1, 0, 15, s => uiManager.S_MouseJitter = s,
                    tooltip: "Adds random small movements to make aim look more human-like.")
                .AddToggle("Precision Lock", t => uiManager.T_PrecisionLock = t,
                    tooltip: "Stops the crosshair from shimmying near a stationary target by snapping fully onto it once close enough, instead of endlessly chasing with eased/jittered movement.")
                .AddSlider("Precision Lock Radius", "px", 1, 1, 1, 50, s => uiManager.S_PrecisionLockRadius = s,
                    tooltip: "How close (in pixels) the crosshair must get to the target before it snaps fully into place. Keep this small - it's meant to kill the last bit of shimmy, not replace your normal aim movement.")
                .AddToggle("Y Axis Percentage Adjustment", t => uiManager.T_YAxisPercentageAdjustment = t,
                    tooltip: "Enable the Y Offset (%) slider to adjust aim vertically by percentage.")
                .AddToggle("X Axis Percentage Adjustment", t => uiManager.T_XAxisPercentageAdjustment = t,
                    tooltip: "Enable the X Offset (%) slider to adjust aim horizontally by percentage.")
                .AddButton("Pick Target Point", b =>
                {
                    uiManager.B_PickTargetPoint = b;
                    b.Reader.Click += (s, e) =>
                    {
                        if (targetPointPickerInstance != null && targetPointPickerInstance.IsVisible)
                        {
                            targetPointPickerInstance.Activate();
                            return;
                        }

                        double initialX = uiManager.S_XOffsetPercent?.Slider.Value ?? 50;
                        double initialY = uiManager.S_YOffsetPercent?.Slider.Value ?? 70;

                        targetPointPickerInstance = new UISections.TargetPointPicker(initialX, initialY);

                        // Each axis only turns on (and only gets overridden) when the
                        // user actually interacts with that axis in the picker. Picking
                        // a body zone (Head/Chest/etc) must not silently change your
                        // horizontal aim point, and vice versa for the lean row.
                        targetPointPickerInstance.YPercentSelected += (yPercent) =>
                        {
                            Dictionary.toggleState["Y Axis Percentage Adjustment"] = true;
                            if (uiManager.T_YAxisPercentageAdjustment != null)
                                _mainWindow.UpdateToggleUI(uiManager.T_YAxisPercentageAdjustment, true);
                            _mainWindow.Toggle_Action("Y Axis Percentage Adjustment");

                            if (uiManager.S_YOffsetPercent != null) uiManager.S_YOffsetPercent.Slider.Value = yPercent;
                        };

                        targetPointPickerInstance.XPercentSelected += (xPercent) =>
                        {
                            Dictionary.toggleState["X Axis Percentage Adjustment"] = true;
                            if (uiManager.T_XAxisPercentageAdjustment != null)
                                _mainWindow.UpdateToggleUI(uiManager.T_XAxisPercentageAdjustment, true);
                            _mainWindow.Toggle_Action("X Axis Percentage Adjustment");

                            if (uiManager.S_XOffsetPercent != null) uiManager.S_XOffsetPercent.Slider.Value = xPercent;
                        };

                        targetPointPickerInstance.Closed += (sender, args) =>
                        {
                            targetPointPickerInstance = null;
                        };

                        targetPointPickerInstance.Show();
                    };
                }, tooltip: "Open a visual picker to choose exactly where on the target to aim (head, chest, etc).")
                .AddSlider("Y Offset (Up/Down)", "Offset", 1, 1, -150, 150, s =>
                {
                    uiManager.S_YOffset = s;
                    // Set initial visibility based on toggle state
                    s.Visibility = Dictionary.toggleState["Y Axis Percentage Adjustment"]
                        ? Visibility.Collapsed : Visibility.Visible;
                }, tooltip: "Move aim point up (negative) or down (positive) in pixels.")
                .AddSlider("Y Offset (%)", "Percent", 1, 1, 0, 100, s =>
                {
                    uiManager.S_YOffsetPercent = s;
                    // Set initial visibility based on toggle state
                    s.Visibility = Dictionary.toggleState["Y Axis Percentage Adjustment"]
                        ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "Move aim point up or down as a percentage of the target box height.")
                .AddSlider("X Offset (Left/Right)", "Offset", 1, 1, -150, 150, s =>
                {
                    uiManager.S_XOffset = s;
                    // Set initial visibility based on toggle state
                    s.Visibility = Dictionary.toggleState["X Axis Percentage Adjustment"]
                        ? Visibility.Collapsed : Visibility.Visible;
                }, tooltip: "Move aim point left (negative) or right (positive) in pixels.")
                .AddSlider("X Offset (%)", "Percent", 1, 1, 0, 100, s =>
                {
                    uiManager.S_XOffsetPercent = s;
                    // Set initial visibility based on toggle state
                    s.Visibility = Dictionary.toggleState["X Axis Percentage Adjustment"]
                        ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "Move aim point left or right as a percentage of the target box width.");
        }

        private void LoadPredictions()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, Predictions);

            builder
                .AddTitle("Predictions", true, t =>
                {
                    uiManager.AT_Predictions = t;
                    t.Minimize.Click += (s, e) =>
                    {
                        TogglePanel("Predictions", PredictionsPanel);
                        _mainWindow?.UpdatePredictionSliderVisibility();
                    };
                })
                .AddToggle("Predictions", t => uiManager.T_Predictions = t,
                    tooltip: "Predict where a moving target will be. Helps track fast-moving targets.")
                .AddDropdown("Prediction Method", d =>
                {
                    d.DropdownBox.SelectedIndex = -1;
                    uiManager.D_PredictionMethod = d;
                    _mainWindow.AddDropdownItem(d, "Kalman Filter");
                    _mainWindow.AddDropdownItem(d, "Shall0e's Prediction");
                    _mainWindow.AddDropdownItem(d, "wisethef0x's EMA Prediction");

                    // Update slider visibility when prediction method changes
                    d.DropdownBox.SelectionChanged += (s, e) => _mainWindow?.UpdatePredictionSliderVisibility();
                }, tooltip: "The algorithm used to predict target movement. Try different ones to see what works best.")
                .AddSlider("Kalman Lead Time", "Seconds", 0.01, 0.01, 0.02, 0.30, s =>
                {
                    uiManager.S_KalmanLeadTime = s;
                    // Start collapsed - visibility will be set by LoadDropdownStates
                    s.Visibility = Visibility.Collapsed;
                }, tooltip: "How far ahead to predict target position. Higher = more prediction, may overshoot.")
                .AddSlider("WiseTheFox Lead Time", "Seconds", 0.01, 0.01, 0.02, 0.30, s =>
                {
                    uiManager.S_WiseTheFoxLeadTime = s;
                    // Start collapsed - visibility will be set by LoadDropdownStates
                    s.Visibility = Visibility.Collapsed;
                }, tooltip: "How far ahead to predict target position. Higher = more prediction, may overshoot.")
                .AddSlider("Shalloe Lead Multiplier", "Frames", 0.5, 0.5, 1, 10, s =>
                {
                    uiManager.S_ShalloeLeadMultiplier = s;
                    // Start collapsed - visibility will be set by LoadDropdownStates
                    s.Visibility = Visibility.Collapsed;
                }, tooltip: "How many frames ahead to predict. Higher = more prediction, may overshoot.")
                .AddToggle("EMA Smoothening", t => uiManager.T_EMASmoothing = t,
                    tooltip: "Smooth out aim movements to reduce jitter and make tracking steadier.")
                .AddSlider("EMA Smoothening", "Amount", 0.01, 0.01, 0.01, 1, s =>
                {
                    uiManager.S_EMASmoothing = s;
                    s.Slider.ValueChanged += (sender, e) =>
                    {
                        if (Dictionary.toggleState["EMA Smoothening"])
                        {
                            MouseManager.smoothingFactor = s.Slider.Value;
                        }
                    };
                }, tooltip: "How much smoothing to apply. Lower = smoother but slower, higher = faster but jittery.")
                .AddToggle("Anti-Oscillation", t => uiManager.T_AntiOscillation = t,
                    tooltip: "Reduce back-and-forth jitter when the aim keeps overshooting and correcting.")
                .AddSlider("Anti-Oscillation Deadzone", "Pixels", 1, 1, 0, 10, s =>
                {
                    uiManager.S_AntiOscillationDeadzone = s;
                    s.Slider.ValueChanged += (sender, e) =>
                    {
                        if (Dictionary.toggleState["Anti-Oscillation"])
                        {
                            MouseManager.AntiOscillationDeadzone = s.Slider.Value;
                        }
                    };
                }, tooltip: "Ignore corrections smaller than this many pixels.")
                .AddSlider("Anti-Oscillation Damping", "Strength", 0.05, 0.05, 0.1, 0.9, s =>
                {
                    uiManager.S_AntiOscillationDamping = s;
                    s.Slider.ValueChanged += (sender, e) =>
                    {
                        if (Dictionary.toggleState["Anti-Oscillation"])
                        {
                            MouseManager.AntiOscillationDamping = s.Slider.Value;
                        }
                    };
                }, tooltip: "How much to shrink a correction that just reversed direction. Lower = stronger damping.")
                .AddSeparator();
        }

        private void LoadTriggerBot()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, TriggerBot);

            builder
                .AddTitle("Auto Trigger", true, t =>
                {
                    uiManager.AT_TriggerBot = t;
                    t.Minimize.Click += (s, e) => TogglePanel("Auto Trigger", TriggerBotPanel);
                })
                .AddToggle("Auto Trigger", t => uiManager.T_AutoTrigger = t,
                    tooltip: "Automatically click when a target is detected in your crosshair area.")
                .AddKeyChanger("Auto Trigger Keybind", k => uiManager.C_AutoTriggerKeybind = k,
                    tooltip: "Hold this to enable Auto Trigger temporarily, without turning the toggle above on permanently. Defaults to Middle mouse button.")
                .AddToggle("Cursor Check", t => uiManager.T_CursorCheck = t,
                    tooltip: "Only trigger when cursor is directly on target. More accurate but may miss some shots.")
                .AddToggle("Spray Mode", t => uiManager.T_SprayMode = t,
                    tooltip: "Hold down fire instead of single clicks. Good for automatic weapons.")
                .AddToggle("Burst Mode", t => uiManager.T_BurstMode = t,
                    tooltip: "Fires a fixed number of shots, then pauses, instead of spraying continuously. Takes priority over Spray Mode when both are on. Good for weapons where only the first few bullets land before recoil kicks in.")
                .AddSlider("Burst Count", "Shots", 1, 0, 1, 10, s =>
                {
                    uiManager.S_BurstCount = s;
                    s.Visibility = Dictionary.toggleState["Burst Mode"] ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "How many shots to fire per burst before pausing.")
                .AddSlider("Burst Cooldown", "ms", 10, 0, 50, 2000, s =>
                {
                    uiManager.S_BurstCooldown = s;
                    s.Visibility = Dictionary.toggleState["Burst Mode"] ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "How long to pause after a burst finishes before firing starts again.")
                .AddToggle("Recoil Compensation", t => uiManager.T_RecoilCompensation = t,
                    tooltip: "Pulls the mouse (or gamepad stick) to counter a weapon's recoil pattern while the Fire Keybind is held. Works for both manual clicking and Auto Trigger. Requires a saved pattern - use Edit/New Pattern below to create one.")
                .AddSlider("Recoil Strength", "%", 1, 0, 0, 200, s =>
                {
                    uiManager.S_RecoilStrength = s;
                    s.Visibility = Dictionary.toggleState["Recoil Compensation"] ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "Global multiplier on the active pattern's pull strength. 100% = pattern values as saved.")
                .AddDropdown("Recoil Weapon Profile", d =>
                {
                    uiManager.D_RecoilWeaponProfile = d;
                    PopulateRecoilProfiles(d);
                    d.Visibility = Dictionary.toggleState["Recoil Compensation"] ? Visibility.Visible : Visibility.Collapsed;

                    d.DropdownBox.SelectionChanged += (s, e) =>
                    {
                        if (d.DropdownBox.SelectedItem is not ComboBoxItem selected) return;
                        string profileName = selected.Content?.ToString() ?? "None";
                        Aimmy2.Other.RecoilManager.LoadPattern(profileName);
                    };
                }, tooltip: "Which saved recoil pattern to use.")
                .AddButton("Refresh Profiles", b =>
                {
                    uiManager.B_RefreshRecoilProfiles = b;
                    b.Visibility = Dictionary.toggleState["Recoil Compensation"] ? Visibility.Visible : Visibility.Collapsed;
                    b.Reader.Click += (s, e) =>
                    {
                        if (uiManager.D_RecoilWeaponProfile != null)
                            PopulateRecoilProfiles(uiManager.D_RecoilWeaponProfile);
                    };
                }, tooltip: "Rescan bin/recoil for pattern files.")
                .AddButton("Edit / New Pattern", b =>
                {
                    uiManager.B_EditRecoilPattern = b;
                    b.Visibility = Dictionary.toggleState["Recoil Compensation"] ? Visibility.Visible : Visibility.Collapsed;
                    b.Reader.Click += (s, e) =>
                    {
                        if (recoilPatternEditorInstance != null && recoilPatternEditorInstance.IsVisible)
                        {
                            recoilPatternEditorInstance.Activate();
                            return;
                        }

                        string? currentProfile = (uiManager.D_RecoilWeaponProfile?.DropdownBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                        recoilPatternEditorInstance = new UISections.RecoilPatternEditor(currentProfile);

                        recoilPatternEditorInstance.PatternSaved += (savedName) =>
                        {
                            if (uiManager.D_RecoilWeaponProfile != null)
                            {
                                PopulateRecoilProfiles(uiManager.D_RecoilWeaponProfile);

                                for (int i = 0; i < uiManager.D_RecoilWeaponProfile.DropdownBox.Items.Count; i++)
                                {
                                    if ((uiManager.D_RecoilWeaponProfile.DropdownBox.Items[i] as ComboBoxItem)?.Content?.ToString() == savedName)
                                    {
                                        uiManager.D_RecoilWeaponProfile.DropdownBox.SelectedIndex = i;
                                        break;
                                    }
                                }
                            }

                            Aimmy2.Other.RecoilManager.LoadPattern(savedName);
                        };

                        recoilPatternEditorInstance.Closed += (sender, args) =>
                        {
                            recoilPatternEditorInstance = null;
                        };

                        recoilPatternEditorInstance.Show();
                    };
                }, tooltip: "Open the pattern editor to create a new recoil pattern or edit the selected one.")
                .AddKeyChanger("Fire Keybind", k =>
                {
                    uiManager.C_FireKeybind = k;
                    k.Visibility = Dictionary.toggleState["Recoil Compensation"] ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "The key/button recoil compensation watches to know when you're firing. Defaults to left mouse button.")
                //.AddToggle("Only When Held", t => uiManager.T_OnlyWhenHeld = t)
                .AddSlider("Auto Trigger Delay", "Seconds", 0.01, 0.1, 0.01, 1, s => uiManager.S_AutoTriggerDelay = s,
                    tooltip: "Wait time before firing after detecting a target. Helps avoid accidental shots. Not used in Burst Mode.")
                .AddSeparator();
        }

        private void LoadFOVConfig()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, FOVConfig);

            builder
                .AddTitle("FOV Config", true, t =>
                {
                    uiManager.AT_FOV = t;
                    t.Minimize.Click += (s, e) => TogglePanel("FOV Config", FOVConfigPanel);
                })
                .AddToggle("FOV", t => uiManager.T_FOV = t,
                    tooltip: "Show a circle on screen indicating the detection area.")
                .AddToggle("Dynamic FOV", t => uiManager.T_DynamicFOV = t,
                    tooltip: "Change FOV size when holding a key. Useful for scoping in.")
                .AddToggle("Third Person Support", t => uiManager.T_ThirdPersonSupport = t,
                    tooltip: "Adjust FOV position for third-person camera games.")
                .AddKeyChanger("Dynamic FOV Keybind", k => uiManager.C_DynamicFOV = k,
                    tooltip: "The key to hold for switching to the dynamic FOV size.")
                .AddDropdown("FOV Style", d =>
                {
                    uiManager.D_FOVSTYLE = d;

                    var circleItem = _mainWindow.AddDropdownItem(d, "Circle");
                    var rectangleItem = _mainWindow.AddDropdownItem(d, "Rectangle");

                    circleItem.Selected += (s, e) =>
                    {
                        MainWindow.FOVWindow.Circle.Visibility = Visibility.Visible;
                        MainWindow.FOVWindow.RectangleShape.Visibility = Visibility.Collapsed;
                    };

                    rectangleItem.Selected += (s, e) =>
                    {
                        MainWindow.FOVWindow.Circle.Visibility = Visibility.Collapsed;
                        MainWindow.FOVWindow.RectangleShape.Visibility = Visibility.Visible;
                    };
                }, tooltip: "Shape of the FOV overlay. Circle is most common.")
                .AddColorChanger("FOV Color", c =>
                {
                    c.Reader.Click += (s, e) =>
                    {
                        if (fovColorPickerInstance != null && fovColorPickerInstance.IsVisible)
                        {
                            fovColorPickerInstance.Activate();
                            return;
                        }

                        Color initialColor = Colors.White;
                        if (c.ColorChangingBorder.Background is SolidColorBrush scb)
                            initialColor = scb.Color;
                        fovColorPickerInstance = new UISections.ColorPicker(initialColor, "FOV Color");

                        fovColorPickerInstance.ColorChanged += (color) =>
                        {
                            // Update the color square
                            c.ColorChangingBorder.Background = new SolidColorBrush(color);
                            // Save to dictionary for persistence
                            Dictionary.colorState["FOV Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                            PropertyChanger.PostColor(color);
                        };

                        fovColorPickerInstance.Closed += (sender, args) =>
                        {
                            fovColorPickerInstance = null;
                        };

                        fovColorPickerInstance.Show();
                    };
                })
                .AddSlider("FOV Size", "Size", 1, 1, 10, 640, s =>
                {
                    uiManager.S_FOVSize = s;
                    s.Slider.ValueChanged += (sender, e) =>
                    {
                        _mainWindow.ActualFOV = s.Slider.Value;
                        PropertyChanger.PostNewFOVSize(_mainWindow.ActualFOV);
                    };
                }, tooltip: "Size of the detection area. Smaller = more precise, larger = wider coverage.")
                .AddSlider("Dynamic FOV Size", "Size", 1, 1, 10, 640, s =>
                {
                    uiManager.S_DynamicFOVSize = s;
                    s.Slider.ValueChanged += (sender, e) =>
                    {
                        if (Dictionary.toggleState["Dynamic FOV"])
                            PropertyChanger.PostNewFOVSize(s.Slider.Value);
                    };
                }, tooltip: "FOV size when holding the Dynamic FOV key. Usually smaller for scoped aim.")
                .AddSeparator()
                .AddToggle("Snap Zone", t => uiManager.T_SnapZone = t,
                    tooltip: "A smaller zone inside the main FOV. Targets outside it get reduced or no aim correction, depending on Hard Cutoff below.")
                .AddToggle("Snap Zone Hard Cutoff", t => uiManager.T_SnapZoneHardCutoff = t,
                    tooltip: "On: no correction at all outside the snap zone (target must enter it first). Off: reduced correction outside the zone, scaled by the strength slider below.")
                .AddSlider("Snap Zone Outside Strength %", "%", 1, 1, 0, 100, s =>
                {
                    uiManager.S_SnapZoneOutsideStrength = s;
                    s.Visibility = Dictionary.toggleState["Snap Zone Hard Cutoff"] ? Visibility.Collapsed : Visibility.Visible;
                }, tooltip: "How much correction strength remains outside the snap zone when Hard Cutoff is off. 0% = none, 100% = same as inside the zone.")
                .AddSlider("Snap Zone Size", "Size", 1, 1, 10, 640, s =>
                {
                    uiManager.S_SnapZoneSize = s;
                    s.Slider.ValueChanged += (sender, e) =>
                    {
                        MainWindow.SnapZoneWindow.UpdateSnapZoneSize(s.Slider.Value);
                    };
                }, tooltip: "Size of the snap zone. Should usually be smaller than your main FOV Size.")
                .AddDropdown("Snap Zone Style", d =>
                {
                    uiManager.D_SnapZoneStyle = d;

                    var circleItem = _mainWindow.AddDropdownItem(d, "Circle");
                    var rectangleItem = _mainWindow.AddDropdownItem(d, "Rectangle");

                    circleItem.Selected += (s, e) =>
                    {
                        MainWindow.SnapZoneWindow.Circle.Visibility = Visibility.Visible;
                        MainWindow.SnapZoneWindow.RectangleShape.Visibility = Visibility.Collapsed;
                    };

                    rectangleItem.Selected += (s, e) =>
                    {
                        MainWindow.SnapZoneWindow.Circle.Visibility = Visibility.Collapsed;
                        MainWindow.SnapZoneWindow.RectangleShape.Visibility = Visibility.Visible;
                    };
                }, tooltip: "Shape of the snap zone overlay. This is cosmetic only - the actual check is always rectangular, same as FOV.")
                .AddColorChanger("Snap Zone Color", c =>
                {
                    uiManager.CC_SnapZoneColor = c;
                    c.Reader.Click += (s, e) =>
                    {
                        if (snapZoneColorPickerInstance != null && snapZoneColorPickerInstance.IsVisible)
                        {
                            snapZoneColorPickerInstance.Activate();
                            return;
                        }

                        Color initialColor = Colors.Orange;
                        if (c.ColorChangingBorder.Background is SolidColorBrush scb)
                            initialColor = scb.Color;
                        snapZoneColorPickerInstance = new UISections.ColorPicker(initialColor, "Snap Zone Color");

                        snapZoneColorPickerInstance.ColorChanged += (color) =>
                        {
                            c.ColorChangingBorder.Background = new SolidColorBrush(color);
                            Dictionary.colorState["Snap Zone Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                            MainWindow.SnapZoneWindow.UpdateSnapZoneColor(color);
                        };

                        snapZoneColorPickerInstance.Closed += (sender, args) =>
                        {
                            snapZoneColorPickerInstance = null;
                        };

                        snapZoneColorPickerInstance.Show();
                    };
                })
                .AddToggle("Show Snap Zone", t => uiManager.T_ShowSnapZone = t,
                    tooltip: "Show the snap zone as an overlay circle/rectangle on screen.")
                .AddSeparator();
        }

        private void LoadESPConfig()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, ESPConfig);

            builder
                .AddTitle("ESP Config", true, t =>
                {
                    uiManager.AT_DetectedPlayer = t;
                    t.Minimize.Click += (s, e) => TogglePanel("ESP Config", ESPConfigPanel);
                })
                .AddToggle("Show Detected Player", t => uiManager.T_ShowDetectedPlayer = t,
                    tooltip: "Draw a box around detected targets on screen.")
                .AddToggle("Show AI Confidence", t => uiManager.T_ShowAIConfidence = t,
                    tooltip: "Display how confident the AI is about each detection (0-100%).")
                .AddToggle("Show Tracers", t => uiManager.T_ShowTracers = t,
                    tooltip: "Draw lines from screen edge to detected targets.");

            builder.AddDropdown("Tracer Position", d =>
            {
                d.DropdownBox.SelectedIndex = 0;
                uiManager.D_TracerPosition = d;
                // Changed the positions of these as top is above middle & bottom - ts (this) bothered me so i had to
                _mainWindow.AddDropdownItem(d, "Top");
                _mainWindow.AddDropdownItem(d, "Middle");
                _mainWindow.AddDropdownItem(d, "Bottom");
                d.DropdownBox.SelectionChanged += (s, e) =>
                {
                    if (Dictionary.toggleState["Show Detected Player"])
                    {
                        // simulate a click to turn it off - this is to force a reload of the ui cause tracer doesn't update otherwise - helz
                        uiManager.T_ShowDetectedPlayer.Reader.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        // simulate a click to turn it back on - same as before ^ - helz
                        uiManager.T_ShowDetectedPlayer.Reader.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    }
                    else
                    {
                        if (Dictionary.DetectedPlayerOverlay != null)
                        {
                            Dictionary.DetectedPlayerOverlay.ForceReposition();
                        }
                    }
                };
            }, tooltip: "Where tracer lines start from on the screen.");

            builder
                .AddColorChanger("Detected Player Color", c =>
                {
                    c.Reader.Click += (s, e) =>
                    {
                        if (colorPickerInstance != null && colorPickerInstance.IsVisible)
                        {
                            colorPickerInstance.Activate();
                            return;
                        }

                        Color initialColor = Colors.White;
                        if (c.ColorChangingBorder.Background is SolidColorBrush scb)
                            initialColor = scb.Color;
                        colorPickerInstance = new UISections.ColorPicker(initialColor, "ESP Color");

                        colorPickerInstance.ColorChanged += (color) =>
                        {
                            // Update the color square
                            c.ColorChangingBorder.Background = new SolidColorBrush(color);
                            // Save to dictionary for persistence
                            Dictionary.colorState["Detected Player Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                            PropertyChanger.PostDPColor(color);
                        };

                        colorPickerInstance.Closed += (sender, args) =>
                        {
                            colorPickerInstance = null;
                        };

                        colorPickerInstance.Show();
                    };
                })
                .AddSlider("AI Confidence Font Size", "Size", 1, 1, 1, 30, s =>
                {
                    uiManager.S_DPFontSize = s;
                    s.Slider.ValueChanged += (sender, e) => PropertyChanger.PostDPFontSize((int)s.Slider.Value);
                }, tooltip: "Text size for the confidence percentage display.")
                .AddSlider("Corner Radius", "Radius", 1, 1, 0, 100, s =>
                {
                    uiManager.S_DPCornerRadius = s;
                    s.Slider.ValueChanged += (sender, e) => PropertyChanger.PostDPWCornerRadius((int)s.Slider.Value);
                }, tooltip: "How rounded the detection box corners are. 0 = sharp corners.")
                .AddSlider("Border Thickness", "Thickness", 0.1, 1, 0.1, 10, s =>
                {
                    uiManager.S_DPBorderThickness = s;
                    s.Slider.ValueChanged += (sender, e) => PropertyChanger.PostDPWBorderThickness(s.Slider.Value);
                }, tooltip: "How thick the detection box outline is.")
                .AddSlider("Opacity", "Opacity", 0.1, 0.1, 0, 1, s =>
                {
                    uiManager.S_DPOpacity = s;
                    s.Slider.ValueChanged += (sender, e) => PropertyChanger.PostDPWOpacity(s.Slider.Value);
                }, tooltip: "How see-through the detection box is. 0 = invisible, 1 = solid.")
                .AddSeparator();
        }

        #endregion

        #region Helper Methods

        private void OnImageSizeChanged(int imageSize)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_mainWindow?.uiManager.S_FOVSize != null && _mainWindow?.uiManager.S_DynamicFOVSize != null)
                {
                    UpdateFovSizeSlider(_mainWindow.uiManager.S_FOVSize, imageSize);
                    UpdateFovSizeSlider(_mainWindow.uiManager.S_DynamicFOVSize, imageSize);
                }
            });
        }
        private void UpdateFovSizeSlider(ASlider slider, int imageSize = 640)
        {
            if (slider.Slider == null) return;
            if (imageSize < slider.Slider.Value)
            {
                slider.Slider.Value = imageSize;
            }
            slider.Slider.Maximum = imageSize;
        }

        private async Task ResetToMouseEvent()
        {
            await Task.Delay(500);
            _mainWindow!.uiManager.D_MouseMovementMethod!.DropdownBox.SelectedIndex = 0;
        }

        private void PopulateEsp32ComPorts(ADropdown dropdown)
        {
            string? currentSelection = (dropdown.DropdownBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? (Dictionary.dropdownState.TryGetValue("ESP32 COM Port", out var saved) ? saved?.ToString() : null);

            dropdown.DropdownBox.Items.Clear();

            string[] ports = MouseManager.GetAvailableComPorts();
            if (ports.Length == 0)
            {
                ports = new[] { "COM3" };
            }

            foreach (var port in ports)
            {
                _mainWindow!.AddDropdownItem(dropdown, port);
            }

            int selectIndex = 0;
            for (int i = 0; i < dropdown.DropdownBox.Items.Count; i++)
            {
                if ((dropdown.DropdownBox.Items[i] as ComboBoxItem)?.Content?.ToString() == currentSelection)
                {
                    selectIndex = i;
                    break;
                }
            }

            dropdown.DropdownBox.SelectedIndex = selectIndex;
        }

        private void PopulateRecoilProfiles(ADropdown dropdown)
        {
            string? currentSelection = (dropdown.DropdownBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? (Dictionary.dropdownState.TryGetValue("Recoil Weapon Profile", out var saved) ? saved?.ToString() : null);

            dropdown.DropdownBox.Items.Clear();

            var profiles = new List<string> { "None" };
            profiles.AddRange(Aimmy2.Other.RecoilManager.ListAvailablePatterns());

            foreach (var profile in profiles)
            {
                _mainWindow!.AddDropdownItem(dropdown, profile);
            }

            int selectIndex = 0;
            for (int i = 0; i < dropdown.DropdownBox.Items.Count; i++)
            {
                if ((dropdown.DropdownBox.Items[i] as ComboBoxItem)?.Content?.ToString() == currentSelection)
                {
                    selectIndex = i;
                    break;
                }
            }

            dropdown.DropdownBox.SelectedIndex = selectIndex;
        }

        private void HandleColorChange(AColorChanger colorChanger, string settingKey, Action<Color> updateAction)
        {
            var colorDialog = new System.Windows.Forms.ColorDialog();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var color = Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);
                colorChanger.ColorChangingBorder.Background = new SolidColorBrush(color);
                Dictionary.colorState[settingKey] = color.ToString();
                updateAction(color);
            }
        }

        public void Dispose()
        {
            // Save minimize states before disposing
            SaveMinimizeStatesToGlobal();
        }

        #endregion

        #region Section Builder

        private class SectionBuilder
        {
            private readonly AimMenuControl _parent;
            private readonly StackPanel _panel;

            public SectionBuilder(AimMenuControl parent, StackPanel panel)
            {
                _parent = parent;
                _panel = panel;
            }

            public SectionBuilder AddTitle(string title, bool canMinimize, Action<ATitle>? configure = null)
            {
                var titleControl = new ATitle(title, canMinimize);
                configure?.Invoke(titleControl);
                _panel.Children.Add(titleControl);
                return this;
            }

            public SectionBuilder AddToggle(string title, Action<AToggle>? configure = null, string? tooltip = null)
            {
                var toggle = _parent.CreateToggle(title, tooltip);
                configure?.Invoke(toggle);
                _panel.Children.Add(toggle);
                return this;
            }

            public SectionBuilder AddKeyChanger(string title, Action<AKeyChanger>? configure = null, string? defaultKey = null, string? tooltip = null)
            {
                var key = defaultKey ?? Dictionary.bindingSettings[title];
                var keyChanger = _parent.CreateKeyChanger(title, key, tooltip);
                configure?.Invoke(keyChanger);
                _panel.Children.Add(keyChanger);
                return this;
            }

            public SectionBuilder AddSlider(string title, string label, double frequency, double buttonSteps,
                double min, double max, Action<ASlider>? configure = null, string? tooltip = null)
            {
                var slider = _parent.CreateSlider(title, label, frequency, buttonSteps, min, max, tooltip);
                configure?.Invoke(slider);
                _panel.Children.Add(slider);
                return this;
            }

            public SectionBuilder AddDropdown(string title, Action<ADropdown>? configure = null, string? tooltip = null)
            {
                var dropdown = _parent.CreateDropdown(title, tooltip);
                configure?.Invoke(dropdown);
                _panel.Children.Add(dropdown);
                return this;
            }

            public SectionBuilder AddColorChanger(string title, Action<AColorChanger>? configure = null)
            {
                var colorChanger = _parent.CreateColorChanger(title);
                configure?.Invoke(colorChanger);
                _panel.Children.Add(colorChanger);
                return this;
            }

            public SectionBuilder AddButton(string title, Action<APButton>? configure = null, string? tooltip = null)
            {
                var button = new APButton(title, tooltip);
                configure?.Invoke(button);
                _panel.Children.Add(button);
                return this;
            }

            public SectionBuilder AddFileLocator(string title, Action<AFileLocator>? configure = null,
                string filter = "All files (*.*)|*.*", string dlExtension = "")
            {
                var fileLocator = new AFileLocator(title, title, filter, dlExtension);
                configure?.Invoke(fileLocator);
                _panel.Children.Add(fileLocator);
                return this;
            }

            public SectionBuilder AddSeparator()
            {
                _panel.Children.Add(new ARectangleBottom());
                _panel.Children.Add(new ASpacer());
                return this;
            }
        }

        #endregion

        #region Control Creation Methods

        private AToggle CreateToggle(string title, string? tooltip = null)
        {
            var toggle = new AToggle(title, tooltip);
            _mainWindow!.toggleInstances[title] = toggle;

            // Set initial state
            if (Dictionary.toggleState[title])
                toggle.EnableSwitch();
            else
                toggle.DisableSwitch();

            // Handle click
            toggle.Reader.Click += (sender, e) =>
            {
                Dictionary.toggleState[title] = !Dictionary.toggleState[title];
                _mainWindow.UpdateToggleUI(toggle, Dictionary.toggleState[title]);
                _mainWindow.Toggle_Action(title);
            };

            return toggle;
        }

        private AKeyChanger CreateKeyChanger(string title, string keybind, string? tooltip = null)
        {
            var keyChanger = new AKeyChanger(title, keybind, tooltip);

            keyChanger.Reader.Click += (sender, e) =>
            {
                keyChanger.KeyNotifier.Content = "...";
                _mainWindow!.bindingManager.StartListeningForBinding(title);

                Action<string, string>? bindingSetHandler = null;
                bindingSetHandler = (bindingId, key) =>
                {
                    if (bindingId == title)
                    {
                        keyChanger.KeyNotifier.Content = KeybindNameManager.ConvertToRegularKey(key);
                        Dictionary.bindingSettings[bindingId] = key;
                        _mainWindow.bindingManager.OnBindingSet -= bindingSetHandler;
                    }
                };

                _mainWindow.bindingManager.OnBindingSet += bindingSetHandler;
            };

            return keyChanger;
        }

        private ASlider CreateSlider(string title, string label, double frequency, double buttonSteps,
            double min, double max, string? tooltip = null)
        {
            var slider = new ASlider(title, label, buttonSteps, tooltip)
            {
                Slider = { Minimum = min, Maximum = max, TickFrequency = frequency }
            };

            slider.Slider.Value = Dictionary.sliderSettings.TryGetValue(title, out var value) ? value : min;
            slider.Slider.ValueChanged += (s, e) => Dictionary.sliderSettings[title] = slider.Slider.Value;

            return slider;
        }

        private ADropdown CreateDropdown(string title, string? tooltip = null) => new(title, title, tooltip);

        private AColorChanger CreateColorChanger(string title)
        {
            var colorChanger = new AColorChanger(title);
            colorChanger.ColorChangingBorder.Background =
                (Brush)new BrushConverter().ConvertFromString(Dictionary.colorState[title]);
            return colorChanger;
        }

        #endregion
    }
}