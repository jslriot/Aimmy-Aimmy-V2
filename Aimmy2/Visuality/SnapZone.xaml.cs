using Aimmy2.Class;
using Aimmy2.Theme;
using Class;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace Visuality
{
    public partial class SnapZone : Window
    {
        // Windows API for forcing window position
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private bool _isInitialized = false;

        public SnapZone()
        {
            InitializeComponent();

            // Subscribe to display changes early
            DisplayManager.DisplayChanged += OnDisplayChanged;

            // Subscribe to Exclusion
            ThemeManager.ExcludeWindowFromBackground(this);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Make window click-through
            ClickThroughOverlay.MakeClickThrough(new WindowInteropHelper(this).Handle);

            // Now that we have a window handle, position the window
            if (!_isInitialized)
            {
                _isInitialized = true;
                ForceReposition();
            }
        }

        private void OnDisplayChanged(object? sender, DisplayChangedEventArgs e)
        {
            // Update position when display changes
            Application.Current.Dispatcher.Invoke(() =>
            {
                ForceReposition();
            });
        }

        public void ForceReposition()
        {
            try
            {
                // Get window handle
                var hwnd = _isInitialized ? new WindowInteropHelper(this).Handle : IntPtr.Zero;

                // Set window state to normal first
                this.WindowState = WindowState.Normal;

                // Position window to cover the current display (accounting for DPI scaling)
                this.Left = DisplayManager.ScreenLeft / WinAPICaller.scalingFactorX;
                this.Top = DisplayManager.ScreenTop / WinAPICaller.scalingFactorY;
                this.Width = DisplayManager.ScreenWidth / WinAPICaller.scalingFactorX;
                this.Height = DisplayManager.ScreenHeight / WinAPICaller.scalingFactorY;

                // Force position with Windows API if we have a handle
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, IntPtr.Zero,
                        DisplayManager.ScreenLeft,
                        DisplayManager.ScreenTop,
                        DisplayManager.ScreenWidth,
                        DisplayManager.ScreenHeight,
                        SWP_NOZORDER | SWP_NOACTIVATE);
                }

                // Maximize to cover entire display
                this.WindowState = WindowState.Maximized;

                // Center the snap zone shape on the current display
                var centerX = (DisplayManager.ScreenWidth / 2.0) / WinAPICaller.scalingFactorX;
                var centerY = (DisplayManager.ScreenHeight / 2.0) / WinAPICaller.scalingFactorY;

                double halfSize = SnapZoneStrictEnclosure.Width / 2.0;

                SnapZoneStrictEnclosure.Margin = new Thickness(
                    centerX - halfSize,
                    centerY - halfSize,
                    0, 0);

                // Force layout update
                this.UpdateLayout();
            }
            catch (Exception ex)
            {
            }
        }

        public void UpdateSnapZoneColor(Color newColor)
        {
            var brush = new SolidColorBrush(newColor);
            Circle.Stroke = brush;
            RectangleShape.Stroke = brush;
        }

        public void UpdateSnapZoneSize(double newSize)
        {
            Circle.Width = Circle.Height = newSize;
            RectangleShape.Width = RectangleShape.Height = newSize;
            SnapZoneStrictEnclosure.Width = SnapZoneStrictEnclosure.Height = newSize;
            ForceReposition();
        }

        protected override void OnClosed(EventArgs e)
        {
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            base.OnClosed(e);
        }
    }
}
