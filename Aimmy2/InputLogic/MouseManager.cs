using Aimmy2.Class;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Class;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using MouseMovementLibraries.SendInputSupport;
using Other;
using System.Drawing;
using System.Runtime.InteropServices;

namespace InputLogic
{
    internal class MouseManager
    {
        private static readonly double ScreenWidth = WinAPICaller.ScreenWidth;
        private static readonly double ScreenHeight = WinAPICaller.ScreenHeight;

        private static DateTime LastClickTime = DateTime.MinValue;
        private static bool isSpraying = false;
        private static int burstShotsFired = 0;
        private static DateTime burstCooldownUntil = DateTime.MinValue;

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private static double previousX = 0;
        private static double previousY = 0;
        public static double smoothingFactor = 0.5;
        public static bool IsEMASmoothingEnabled = false;

        private static double lastCorrectionX = 0;
        private static double lastCorrectionY = 0;
        private static int reversalCooldownX = 0;
        private static int reversalCooldownY = 0;

        public static bool IsAntiOscillationEnabled = false;
        public static double AntiOscillationDeadzone = 2;        // px
        public static double AntiOscillationDamping = 0.5;       // 0-1
        public static int AntiOscillationCooldownFrames = 3;      // frames

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        private static Random MouseRandom = new();

        private static double EmaSmoothing(double previousValue, double currentValue, double smoothingFactor) => (currentValue * smoothingFactor) + (previousValue * (1 - smoothingFactor));

        private static double ApplyAntiOscillationAxis(double rawValue, ref double lastCorrection, ref int cooldown)
        {
            if (Math.Abs(rawValue) < AntiOscillationDeadzone) rawValue = 0;

            if (lastCorrection != 0 && rawValue != 0 && Math.Sign(rawValue) != Math.Sign(lastCorrection))
            {
                cooldown = AntiOscillationCooldownFrames;
            }

            if (cooldown > 0)
            {
                rawValue *= AntiOscillationDamping;
                cooldown--;
            }

            lastCorrection = rawValue;
            return rawValue;
        }

        // ============= ESP32 INTEGRATION =============

        /// <summary>
        /// Initialize ESP32 connection
        /// </summary>
        public static bool InitializeESP32(string comPort = "COM3")
        {
            return ESP32SerialHandler.Connect(comPort, 115200);
        }

        /// <summary>
        /// Disconnect ESP32
        /// </summary>
        public static void DisableESP32()
        {
            ESP32SerialHandler.Disconnect();
        }

        /// <summary>
        /// Check if ESP32 is connected
        /// </summary>
        public static bool IsESP32Connected => ESP32SerialHandler.IsConnected;

        private static bool _esp32DisconnectWarned = false;

        /// <summary>
        /// Called everywhere ESP32 Serial is about to be used. Since there's no
        /// SendInput fallback anymore, a dropped connection means actions are
        /// silently skipped - this logs it once (not every frame/click) so it's
        /// visible instead of looking like aim assist just stopped working.
        /// </summary>
        private static void EnsureEsp32ConnectionWarning()
        {
            if (IsESP32Connected)
            {
                _esp32DisconnectWarned = false;
                return;
            }

            if (!_esp32DisconnectWarned)
            {
                _esp32DisconnectWarned = true;
                LogManager.Log(LogManager.LogLevel.Warning,
                    "ESP32 Serial is selected but the board isn't connected right now - mouse movement and clicks are being skipped rather than sent through Windows input. Reconnect it from the Mouse Movement Method dropdown.",
                    true);
            }
        }

        /// <summary>
        /// Auto-detect ESP32
        /// </summary>
        public static bool AutoDetectESP32()
        {
            return ESP32SerialHandler.AutoDetect();
        }

        /// <summary>
        /// Get available COM ports
        /// </summary>
        public static string[] GetAvailableComPorts()
        {
            return ESP32SerialHandler.GetAvailablePorts();
        }

        // Cleanup
        private static (Action down, Action up) GetMouseActions()
        {
            string mouseMovementMethod = AimSettings.MouseMovementMethod;
            Action mouseDownAction;
            Action mouseUpAction;

            switch (mouseMovementMethod)
            {
                case "SendInput":
                    mouseDownAction = () => SendInputMouse.SendMouseCommand(MOUSEEVENTF_LEFTDOWN);
                    mouseUpAction = () => SendInputMouse.SendMouseCommand(MOUSEEVENTF_LEFTUP);
                    break;
                case "LG HUB":
                    mouseDownAction = () => LGMouse.Move(1, 0, 0, 0);
                    mouseUpAction = () => LGMouse.Move(0, 0, 0, 0);
                    break;
                case "Razer Synapse (Require Razer Peripheral)":
                    mouseDownAction = () => RZMouse.mouse_click(1);
                    mouseUpAction = () => RZMouse.mouse_click(0);
                    break;
                case "ddxoft Virtual Input Driver":
                    mouseDownAction = () => DdxoftMain.ddxoftInstance.btn!(1);
                    mouseUpAction = () => DdxoftMain.ddxoftInstance.btn(2);
                    break;
                case "ESP32 Serial":
                    // No fallback to SendInput here on purpose - if ESP32 Serial is
                    // selected, everything should go through the ESP32 or nothing at
                    // all, never silently through Windows input instead.
                    mouseDownAction = () =>
                    {
                        EnsureEsp32ConnectionWarning();
                        if (IsESP32Connected) ESP32SerialHandler.SendCommand("MD,0");
                    };
                    mouseUpAction = () =>
                    {
                        EnsureEsp32ConnectionWarning();
                        if (IsESP32Connected) ESP32SerialHandler.SendCommand("MU,0");
                    };
                    break;
                case "ESP32 Gamepad":
                    // "Firing" on a gamepad is a full R2 trigger pull, not a click.
                    mouseDownAction = () =>
                    {
                        EnsureEsp32ConnectionWarning();
                        if (IsESP32Connected) ESP32SerialHandler.SendCommand("RT,127");
                    };
                    mouseUpAction = () =>
                    {
                        EnsureEsp32ConnectionWarning();
                        if (IsESP32Connected) ESP32SerialHandler.SendCommand("RT,0");
                    };
                    break;
                default:
                    mouseDownAction = () => mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    mouseUpAction = () => mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    break;
            }

            return (mouseDownAction, mouseUpAction);
        }

        public static async Task DoTriggerClick(RectangleF? detectionBox = null)
        {
            // there was a toggle for this, but i realized if it was off, it would never stop spraying. - T
            // Auto Trigger Keybind overrides this - holding it fires on its own,
            // without also needing an Aim Keybind held.
            bool aimKeyHeld = InputBindingManager.IsHoldingBinding("Aim Keybind") || InputBindingManager.IsHoldingBinding("Second Aim Keybind");
            bool autoTriggerKeybindHeld = InputBindingManager.IsHoldingBinding("Auto Trigger Keybind");

            if (!aimKeyHeld && !autoTriggerKeybindHeld)
            {
                ResetSprayState();
                ResetBurstState();
                return;
            }

            if (AimSettings.BurstMode)
            {
                await DoBurstClick();
                return;
            }

            if (AimSettings.SprayMode)
            {
                if (AimSettings.CursorCheck)
                {
                    Point mousePos = WinAPICaller.GetCursorPosition();

                    if (detectionBox.HasValue && !detectionBox.Value.Contains(mousePos.X, mousePos.Y))
                    {
                        if (isSpraying) ReleaseMouseButton();
                        return;
                    }
                }

                if (!isSpraying) HoldMouseButton();
                return;
            }

            // Single click logic if spray mode off
            int timeSinceLastClick = (int)(DateTime.UtcNow - LastClickTime).TotalMilliseconds;
            int triggerDelayMilliseconds = AimSettings.AutoTriggerDelayMilliseconds;
            const int clickDelayMilliseconds = 20;

            if (timeSinceLastClick < triggerDelayMilliseconds && LastClickTime != DateTime.MinValue)
            {
                return;
            }

            var (mouseDown, mouseUp) = GetMouseActions();

            mouseDown.Invoke();
            await Task.Delay(clickDelayMilliseconds);
            mouseUp.Invoke();

            LastClickTime = DateTime.UtcNow;
        }

        #region Spray Mode Methods
        public static void HoldMouseButton()
        {
            if (isSpraying) return;

            var (mouseDown, _) = GetMouseActions();
            mouseDown.Invoke();
            isSpraying = true;
        }

        public static void ReleaseMouseButton()
        {
            if (!isSpraying) return;

            var (_, mouseUp) = GetMouseActions();
            mouseUp.Invoke();
            isSpraying = false;
        }

        public static void ResetSprayState()
        {
            if (isSpraying)
            {
                ReleaseMouseButton();
            }
        }
        #endregion

        #region Burst Mode Methods

        /// <summary>
        /// Fires up to "Burst Count" shots back-to-back, then enforces a "Burst
        /// Cooldown" pause before firing again - instead of spraying/clicking
        /// continuously, which wastes bullets once recoil pulls later shots off
        /// target. The burst resets whenever the trigger keybind is released.
        /// </summary>
        private static async Task DoBurstClick()
        {
            if (DateTime.UtcNow < burstCooldownUntil)
            {
                return;
            }

            int burstCount = AimSettings.BurstCount;

            if (burstShotsFired >= burstCount)
            {
                burstCooldownUntil = DateTime.UtcNow.AddMilliseconds(AimSettings.BurstCooldownMilliseconds);
                burstShotsFired = 0;
                return;
            }

            int timeSinceLastClick = (int)(DateTime.UtcNow - LastClickTime).TotalMilliseconds;
            const int clickDelayMilliseconds = 20;

            // Shots within a burst fire back-to-back at the weapon's natural click
            // cadence (no extra delay between them) - only the cooldown AFTER a
            // full burst is configurable.
            if (timeSinceLastClick < clickDelayMilliseconds && LastClickTime != DateTime.MinValue)
            {
                return;
            }

            var (mouseDown, mouseUp) = GetMouseActions();

            mouseDown.Invoke();
            await Task.Delay(clickDelayMilliseconds);
            mouseUp.Invoke();

            LastClickTime = DateTime.UtcNow;
            burstShotsFired++;

            if (burstShotsFired >= burstCount)
            {
                burstCooldownUntil = DateTime.UtcNow.AddMilliseconds(AimSettings.BurstCooldownMilliseconds);
                burstShotsFired = 0;
            }
        }

        public static void ResetBurstState()
        {
            burstShotsFired = 0;
            burstCooldownUntil = DateTime.MinValue;
        }

        #endregion

        #region Recoil Compensation & Gamepad Stick (background loop)

        private static bool _wasFireHeld = false;
        private static DateTime _fireHeldSince = DateTime.MinValue;
        private static int _lastRecoilStepIndex = -1;

        private static int _lastStickX = 0;
        private static int _lastStickY = 0;
        private static DateTime _lastGamepadUpdateTime = DateTime.MinValue;

        // Starts a single background poll loop the first time this class is touched.
        // Handles two things, both independent of the AI detection loop (so they
        // work even with Aim Assist off, or with no target currently detected):
        //   1. Recoil Compensation - reacts purely to the Fire Keybind.
        //   2. Gamepad stick idle-centering - stops the view from spinning forever
        //      if MoveCrosshair stops being called (no target) while in Gamepad mode.
        static MouseManager()
        {
            _ = BackgroundInputLoop();
        }

        private static async Task BackgroundInputLoop()
        {
            while (true)
            {
                try
                {
                    UpdateRecoilCompensation();
                    UpdateGamepadIdleCentering();
                }
                catch
                {
                    // Never let a bad frame kill the loop.
                }

                await Task.Delay(8); // ~120Hz, well above any realistic weapon fire rate
            }
        }

        private static void UpdateRecoilCompensation()
        {
            if (!AimSettings.RecoilCompensation)
            {
                _wasFireHeld = false;
                _lastRecoilStepIndex = -1;
                return;
            }

            bool fireHeld = InputBindingManager.IsHoldingBinding("Fire Keybind");

            if (fireHeld && !_wasFireHeld)
            {
                // Trigger freshly pressed - start a new spray from the top of the pattern.
                _fireHeldSince = DateTime.UtcNow;
                _lastRecoilStepIndex = -1;
            }
            _wasFireHeld = fireHeld;

            if (!fireHeld) return;

            Aimmy2.Other.RecoilPattern? pattern = Aimmy2.Other.RecoilManager.CurrentPattern;
            if (pattern == null || pattern.Steps.Count == 0) return;

            double msPerShot = 60000.0 / Math.Max(1, pattern.Rpm);
            int stepIndex = (int)((DateTime.UtcNow - _fireHeldSince).TotalMilliseconds / msPerShot);

            if (stepIndex <= _lastRecoilStepIndex) return; // not time for the next shot yet
            if (stepIndex >= pattern.Steps.Count) stepIndex = pattern.Steps.Count - 1; // hold last value past pattern end
            if (stepIndex <= _lastRecoilStepIndex) return; // already applied (pattern shorter than hold time)

            // Sum every step between the last applied index and this one, in case a
            // frame hitch caused us to skip over one or more shots - this keeps
            // compensation from falling permanently behind during a long spray.
            double sumDx = 0, sumDy = 0;
            for (int i = _lastRecoilStepIndex + 1; i <= stepIndex; i++)
            {
                sumDx += pattern.Steps[i].Dx;
                sumDy += pattern.Steps[i].Dy;
            }
            _lastRecoilStepIndex = stepIndex;

            float strength = AimSettings.RecoilStrength;
            int moveX = (int)(sumDx * strength);
            int moveY = (int)(sumDy * strength);

            if (moveX != 0 || moveY != 0)
            {
                SendRelativeMouseMovement(moveX, moveY);
            }
        }

        /// <summary>
        /// Converts the raw (un-eased) target offset into a gamepad right-stick
        /// deflection and sends it. Unlike mouse movement, a stick position is
        /// absolute and persists until changed, so this intentionally bypasses the
        /// mouse-pixel pipeline's easing/smoothing - those exist to make discrete
        /// relative deltas look human, which doesn't apply to a continuously-held
        /// analog stick. Precision Lock is honored (snaps to dead center once close
        /// enough); Snap Zone is not currently applied to gamepad mode.
        /// </summary>
        private static void UpdateGamepadStick(int targetX, int targetY)
        {
            int stickX, stickY;

            if (AimSettings.PrecisionLock &&
                Math.Abs(targetX) <= AimSettings.PrecisionLockRadius &&
                Math.Abs(targetY) <= AimSettings.PrecisionLockRadius)
            {
                stickX = 0;
                stickY = 0;
            }
            else
            {
                float sensitivity = AimSettings.GamepadStickSensitivity;
                const double baseScale = 127.0 / 150.0; // matches the mouse path's +-150 clamp range
                stickX = (int)Math.Clamp(targetX * baseScale * sensitivity, -127, 127);
                stickY = (int)Math.Clamp(targetY * baseScale * sensitivity, -127, 127);
            }

            SendGamepadStick(stickX, stickY);
        }

        private static void SendGamepadStick(int x, int y)
        {
            if (x == _lastStickX && y == _lastStickY) return; // skip redundant serial writes

            _lastStickX = x;
            _lastStickY = y;
            _lastGamepadUpdateTime = DateTime.UtcNow;

            EnsureEsp32ConnectionWarning();
            if (IsESP32Connected)
            {
                ESP32SerialHandler.SendCommand($"RS,{x},{y}");
            }
        }

        private static void UpdateGamepadIdleCentering()
        {
            if (AimSettings.MouseMovementMethod != "ESP32 Gamepad") return;
            if (_lastStickX == 0 && _lastStickY == 0) return;
            if ((DateTime.UtcNow - _lastGamepadUpdateTime).TotalMilliseconds < 100) return;

            SendGamepadStick(0, 0);
        }

        #endregion

        public static void MoveCrosshair(int detectedX, int detectedY)
        {
            int halfScreenWidth = (int)ScreenWidth / 2;
            int halfScreenHeight = (int)ScreenHeight / 2;

            int targetX = detectedX - halfScreenWidth;
            int targetY = detectedY - halfScreenHeight;

            double aspectRatioCorrection = ScreenWidth / ScreenHeight;

            int MouseJitter = AimSettings.MouseJitter;
            int jitterX = MouseRandom.Next(-MouseJitter, MouseJitter);
            int jitterY = MouseRandom.Next(-MouseJitter, MouseJitter);

            Point start = new(0, 0);
            Point end = new(targetX, targetY);
            Point newPosition = new Point(0, 0);

            switch (AimSettings.MovementPath)
            {
                case "Cubic Bezier":
                    Point control1 = new Point(start.X + (end.X - start.X) / 3, start.Y + (end.Y - start.Y) / 3);
                    Point control2 = new Point(start.X + 2 * (end.X - start.X) / 3, start.Y + 2 * (end.Y - start.Y) / 3);
                    newPosition = MovementPaths.CubicBezier(start, end, control1, control2, 1 - AimSettings.MouseSensitivity);
                    break;
                case "Linear":
                    newPosition = MovementPaths.Lerp(start, end, 1 - AimSettings.MouseSensitivity);
                    break;
                case "Exponential":
                    newPosition = MovementPaths.Exponential(start, end, 1 - (AimSettings.MouseSensitivity - 0.2), 3.0);
                    break;
                case "Adaptive":
                    newPosition = MovementPaths.Adaptive(start, end, 1 - AimSettings.MouseSensitivity);
                    break;
                case "Perlin Noise":
                    newPosition = MovementPaths.PerlinNoise(start, end, 1 - AimSettings.MouseSensitivity, 20, 0.5);
                    break;
                default:
                    newPosition = MovementPaths.Lerp(start, end, 1 - AimSettings.MouseSensitivity);
                    break;
            }

            if (IsEMASmoothingEnabled)
            {
                newPosition.X = (int)EmaSmoothing(previousX, newPosition.X, smoothingFactor);
                newPosition.Y = (int)EmaSmoothing(previousY, newPosition.Y, smoothingFactor);
            }

            if (IsAntiOscillationEnabled)
            {
                newPosition.X = (int)ApplyAntiOscillationAxis(newPosition.X, ref lastCorrectionX, ref reversalCooldownX);
                newPosition.Y = (int)ApplyAntiOscillationAxis(newPosition.Y, ref lastCorrectionY, ref reversalCooldownY);
            }

            newPosition.X = Math.Clamp(newPosition.X, -150, 150);
            newPosition.Y = Math.Clamp(newPosition.Y, -150, 150);

            if (AimSettings.SnapZone)
            {
                double halfZone = AimSettings.SnapZoneSize / 2.0;
                bool inZone = Math.Abs(targetX) <= halfZone && Math.Abs(targetY) <= halfZone;

                if (!inZone)
                {
                    float scale = AimSettings.SnapZoneHardCutoff ? 0f : AimSettings.SnapZoneOutsideStrength;
                    newPosition.X = (int)(newPosition.X * scale);
                    newPosition.Y = (int)(newPosition.Y * scale);
                }
            }

            // Precision Lock: once within a small radius of the exact target point,
            // snap directly onto it and suppress jitter for this frame. Without this,
            // the eased movement path never mathematically reaches zero residual error,
            // and combined with per-frame AI detection noise and jitter, the crosshair
            // can shimmy around an otherwise-stationary target instead of settling.
            if (AimSettings.PrecisionLock &&
                Math.Abs(targetX) <= AimSettings.PrecisionLockRadius &&
                Math.Abs(targetY) <= AimSettings.PrecisionLockRadius)
            {
                newPosition.X = Math.Clamp(targetX, -150, 150);
                newPosition.Y = Math.Clamp(targetY, -150, 150);
                jitterX = 0;
                jitterY = 0;
            }

            newPosition.Y = (int)(newPosition.Y / aspectRatioCorrection);

            newPosition.X += jitterX;
            newPosition.Y += jitterY;

            if (AimSettings.MouseMovementMethod == "ESP32 Gamepad")
            {
                // Gamepad aiming uses an absolute stick deflection, not a relative
                // pixel delta - handled entirely separately from the mouse pipeline
                // above (which assumes consumption-once relative movement). See
                // UpdateGamepadStick, driven from the raw target offset, not
                // newPosition (which has already been through easing meant for
                // discrete mouse deltas and doesn't suit a continuously-held stick).
                UpdateGamepadStick(targetX, targetY);
            }
            else
            {
                SendRelativeMouseMovement(newPosition.X, newPosition.Y);
            }

            previousX = newPosition.X;
            previousY = newPosition.Y;

            if (!AimSettings.AutoTrigger)
            {
                ResetSprayState();
            }
        }

        /// <summary>
        /// Dispatches a relative mouse-movement delta through whichever movement
        /// method is currently selected. Used by MoveCrosshair's mouse-pixel path
        /// and by Recoil Compensation's per-shot pixel pull.
        /// NOTE: ESP32 Gamepad mode is NOT supported here - a one-shot relative
        /// pixel delta doesn't translate to a held stick deflection. Recoil
        /// Compensation currently has no effect in Gamepad mode; aiming itself is
        /// handled separately by UpdateGamepadStick.
        /// </summary>
        private static void SendRelativeMouseMovement(int dx, int dy)
        {
            switch (AimSettings.MouseMovementMethod)
            {
                case "SendInput":
                    SendInputMouse.SendMouseCommand(MOUSEEVENTF_MOVE, dx, dy);
                    break;

                case "LG HUB":
                    LGMouse.Move(0, dx, dy, 0);
                    break;

                case "Razer Synapse (Require Razer Peripheral)":
                    RZMouse.mouse_move(dx, dy, true);
                    break;

                case "ddxoft Virtual Input Driver":
                    DdxoftMain.ddxoftInstance.movR!(dx, dy);
                    break;

                case "ESP32 Serial":
                    // No fallback to SendInput here on purpose - see GetMouseActions.
                    EnsureEsp32ConnectionWarning();
                    if (IsESP32Connected) ESP32SerialHandler.SendCommand($"M,{dx},{dy}");
                    break;

                case "ESP32 Gamepad":
                    // No-op by design - see note above.
                    break;

                default:
                    mouse_event(MOUSEEVENTF_MOVE, (uint)dx, (uint)dy, 0, 0);
                    break;
            }
        }
    }
}
