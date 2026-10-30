using Aimmy2.Theme;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace UISections
{
    public partial class TargetPointPicker : Window
    {
        /// <summary>Fires with yPercent when a body zone (Head/Neck/Chest/Stomach/Legs) is clicked. Does not affect X.</summary>
        public event Action<double>? YPercentSelected;

        /// <summary>Fires with xPercent when a lean option (Left/Center/Right) is clicked. Does not affect Y.</summary>
        public event Action<double>? XPercentSelected;

        // Y Offset % semantics (see AIManager.CalculateCoordinates): 0 = bottom of box
        // (feet), 100 = top of box (head). Values picked to land near the middle of
        // each named zone within a typical player bounding box.
        private static readonly Dictionary<string, double> ZoneYPercent = new()
        {
            { "Head", 95 },
            { "Neck", 85 },
            { "Chest", 70 },
            { "Stomach", 50 },
            { "Legs", 15 }
        };

        private static readonly SolidColorBrush SelectedBrush = new(Color.FromArgb(120, 114, 46, 209));
        private static readonly SolidColorBrush UnselectedBrush = Brushes.Transparent;

        private Border? _selectedZone;
        private Border? _selectedLean;
        private double _currentXPercent;
        private double _currentYPercent;

        public TargetPointPicker(double initialXPercent, double initialYPercent)
        {
            InitializeComponent();

            ThemeManager.TrackWindow(this);
            ThemeManager.RegisterElement(this);

            _currentXPercent = initialXPercent;
            _currentYPercent = initialYPercent;

            HighlightClosestZone(initialYPercent);
            HighlightClosestLean(initialXPercent);
            UpdateSelectionLabel();
        }

        private void HighlightClosestZone(double yPercent)
        {
            string closest = "Chest";
            double bestDiff = double.MaxValue;
            foreach (var kvp in ZoneYPercent)
            {
                double diff = Math.Abs(kvp.Value - yPercent);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    closest = kvp.Key;
                }
            }

            SetSelectedZone(closest switch
            {
                "Head" => ZoneHead,
                "Neck" => ZoneNeck,
                "Chest" => ZoneChest,
                "Stomach" => ZoneStomach,
                "Legs" => ZoneLegs,
                _ => ZoneChest
            });
        }

        private void HighlightClosestLean(double xPercent)
        {
            SetSelectedLean(xPercent < 40 ? LeanLeft : xPercent > 60 ? LeanRight : LeanCenter);
        }

        private void Zone_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.Tag is not string zoneName) return;
            if (!ZoneYPercent.TryGetValue(zoneName, out double yPercent)) return;

            _currentYPercent = yPercent;
            SetSelectedZone(border);
            UpdateSelectionLabel();
            YPercentSelected?.Invoke(_currentYPercent);
        }

        private void Lean_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.Tag is not string tagStr) return;
            if (!double.TryParse(tagStr, out double xPercent)) return;

            _currentXPercent = xPercent;
            SetSelectedLean(border);
            UpdateSelectionLabel();
            XPercentSelected?.Invoke(_currentXPercent);
        }

        private void SetSelectedZone(Border? zone)
        {
            if (_selectedZone != null) _selectedZone.Background = UnselectedBrush;
            _selectedZone = zone;
            if (_selectedZone != null) _selectedZone.Background = SelectedBrush;
        }

        private void SetSelectedLean(Border? lean)
        {
            if (_selectedLean != null) _selectedLean.Background = UnselectedBrush;
            _selectedLean = lean;
            if (_selectedLean != null) _selectedLean.Background = SelectedBrush;
        }

        private void UpdateSelectionLabel()
        {
            string zoneName = (_selectedZone?.Tag as string) ?? "Chest";
            string leanName = (_selectedLean?.Tag as string) switch
            {
                "25" => "Left",
                "75" => "Right",
                _ => "Center"
            };
            SelectionLabel.Text = $"Selected: {zoneName} / {leanName}";
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            ThemeManager.UnregisterElement(this);
            base.OnClosed(e);
        }
    }
}
