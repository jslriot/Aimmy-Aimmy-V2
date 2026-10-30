using Aimmy2.Theme;
using Aimmy2.Other;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace UISections
{
    public class RecoilStepRow : INotifyPropertyChanged
    {
        private double _dx;
        private double _dy;

        public int ShotNumber { get; set; }
        public string ShotLabel => $"#{ShotNumber}";

        public double Dx
        {
            get => _dx;
            set { _dx = value; OnPropertyChanged(nameof(Dx)); }
        }

        public double Dy
        {
            get => _dy;
            set { _dy = value; OnPropertyChanged(nameof(Dy)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class RecoilPatternEditor : Window
    {
        /// <summary>Fires with the saved pattern's name after a successful save.</summary>
        public event Action<string>? PatternSaved;

        public ObservableCollection<RecoilStepRow> Steps { get; } = new();

        public RecoilPatternEditor(string? existingPatternName)
        {
            InitializeComponent();

            ThemeManager.TrackWindow(this);
            ThemeManager.RegisterElement(this);

            StepsList.ItemsSource = Steps;

            if (!string.IsNullOrWhiteSpace(existingPatternName) && existingPatternName != "None")
            {
                RecoilManager.LoadPattern(existingPatternName);
                var pattern = RecoilManager.CurrentPattern;

                PatternNameBox.Text = existingPatternName;
                RpmBox.Text = (pattern?.Rpm ?? 600).ToString(CultureInfo.InvariantCulture);

                if (pattern != null)
                {
                    for (int i = 0; i < pattern.Steps.Count; i++)
                    {
                        Steps.Add(new RecoilStepRow { ShotNumber = i + 1, Dx = pattern.Steps[i].Dx, Dy = pattern.Steps[i].Dy });
                    }
                }
            }
            else
            {
                PatternNameBox.Text = "New Pattern";
                RpmBox.Text = "600";
            }

            // Editing a pattern that already has real shots in it -> show the list so
            // the existing numbers are visible. Starting fresh -> drawing is the easier
            // way in, so open straight into Draw mode.
            bool hasExistingShots = Steps.Count > 0;

            if (Steps.Count == 0)
            {
                AddStep();
            }

            if (hasExistingShots)
                ShowListMode();
            else
                ShowDrawMode();
        }

        private void AddStep()
        {
            Steps.Add(new RecoilStepRow { ShotNumber = Steps.Count + 1, Dx = 0, Dy = 0 });
        }

        private void AddStep_Click(object sender, RoutedEventArgs e)
        {
            AddStep();
        }

        private void RemoveStep_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not RecoilStepRow row) return;

            Steps.Remove(row);
            RenumberSteps();
        }

        private void RenumberSteps()
        {
            for (int i = 0; i < Steps.Count; i++)
            {
                Steps[i].ShotNumber = i + 1;
            }
            // Refresh the list so the renumbered labels redraw.
            StepsList.Items.Refresh();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string name = PatternNameBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name) || name == "None")
            {
                MessageBox.Show("Please enter a pattern name (not \"None\").", "Recoil Pattern Editor");
                return;
            }

            if (!double.TryParse(RpmBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double rpm) || rpm <= 0)
            {
                MessageBox.Show("Fire Rate (RPM) must be a positive number.", "Recoil Pattern Editor");
                return;
            }

            var pattern = new RecoilPattern
            {
                Rpm = rpm,
                Steps = Steps.Select(s => new RecoilStep(s.Dx, s.Dy)).ToList()
            };

            if (RecoilManager.SavePattern(name, pattern))
            {
                PatternSaved?.Invoke(name);
                Close();
            }
            else
            {
                MessageBox.Show("Failed to save the pattern. Check the console/log for details.", "Recoil Pattern Editor");
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #region Draw Pattern mode

        // Raw freehand points captured while the mouse is down on DrawCanvas,
        // in canvas-local coordinates. The first point is treated as the
        // origin (no pull yet) - everything after it is resampled and turned
        // into per-shot Dx/Dy deltas by ApplyDrawing_Click.
        private readonly List<Point> _drawnPoints = new();
        private Polyline? _drawPolyline;
        private bool _isDrawing;

        private void ModeList_Click(object sender, RoutedEventArgs e) => ShowListMode();

        private void ModeDraw_Click(object sender, RoutedEventArgs e) => ShowDrawMode();

        private void ShowListMode()
        {
            ListViewPanel.Visibility = Visibility.Visible;
            DrawPanel.Visibility = Visibility.Collapsed;
            ListModeHint.Visibility = Visibility.Visible;
            DrawModeHint.Visibility = Visibility.Collapsed;

            ModeListButton.Background = new SolidColorBrush(ThemeManager.ThemeColor);
            ModeDrawButton.ClearValue(BackgroundProperty);
        }

        private void ShowDrawMode()
        {
            ListViewPanel.Visibility = Visibility.Collapsed;
            DrawPanel.Visibility = Visibility.Visible;
            ListModeHint.Visibility = Visibility.Collapsed;
            DrawModeHint.Visibility = Visibility.Visible;

            ModeDrawButton.Background = new SolidColorBrush(ThemeManager.ThemeColor);
            ModeListButton.ClearValue(BackgroundProperty);
        }

        private void DrawCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDrawing = true;
            _drawnPoints.Clear();
            DrawCanvas.Children.Clear();

            var start = e.GetPosition(DrawCanvas);
            _drawnPoints.Add(start);

            var startDot = new Ellipse { Width = 6, Height = 6, Fill = Brushes.LimeGreen };
            Canvas.SetLeft(startDot, start.X - 3);
            Canvas.SetTop(startDot, start.Y - 3);
            DrawCanvas.Children.Add(startDot);

            _drawPolyline = new Polyline
            {
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 2,
                Points = new PointCollection { start }
            };
            DrawCanvas.Children.Add(_drawPolyline);

            DrawCanvas.CaptureMouse();
        }

        private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing || _drawPolyline == null) return;

            var point = e.GetPosition(DrawCanvas);
            _drawnPoints.Add(point);
            _drawPolyline.Points.Add(point);

            DrawPointCountLabel.Text = "Drawing... release to finish.";
        }

        private void DrawCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            EndDrawing();
        }

        private void DrawCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            EndDrawing();
        }

        private void EndDrawing()
        {
            if (!_isDrawing) return;

            _isDrawing = false;
            DrawCanvas.ReleaseMouseCapture();

            if (_drawnPoints.Count > 2)
            {
                // Raw mouse-move samples are jagged - smooth into one continuous
                // curve before it's shown or resampled into shots.
                var smoothed = ChaikinSmooth(_drawnPoints, iterations: 2);
                _drawnPoints.Clear();
                _drawnPoints.AddRange(smoothed);

                if (_drawPolyline != null)
                    _drawPolyline.Points = new PointCollection(_drawnPoints);
            }

            DrawPointCountLabel.Text = _drawnPoints.Count > 1
                ? "Path drawn. Set Shots/Scale below, then Apply."
                : "Click and drag to start drawing.";
        }

        /// <summary>
        /// Chaikin's corner-cutting algorithm: repeatedly replaces each line
        /// segment with two points closer to it, rounding every corner into a
        /// smooth curve. Endpoints are kept exact so the start/end of the
        /// pattern don't drift.
        /// </summary>
        private static List<Point> ChaikinSmooth(List<Point> points, int iterations)
        {
            if (points.Count < 3) return new List<Point>(points);

            var current = points;
            for (int iter = 0; iter < iterations; iter++)
            {
                var smoothed = new List<Point> { current[0] };
                for (int i = 0; i < current.Count - 1; i++)
                {
                    var p0 = current[i];
                    var p1 = current[i + 1];
                    smoothed.Add(new Point(p0.X * 0.75 + p1.X * 0.25, p0.Y * 0.75 + p1.Y * 0.25));
                    smoothed.Add(new Point(p0.X * 0.25 + p1.X * 0.75, p0.Y * 0.25 + p1.Y * 0.75));
                }
                smoothed.Add(current[^1]);
                current = smoothed;
            }
            return current;
        }

        #region Load from image

        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Load Recoil Pattern Image"
            };

            if (dialog.ShowDialog() != true) return;

            List<Point>? tracedPath;
            try
            {
                tracedPath = TraceImagePath(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not read that image.\n{ex.Message}", "Recoil Pattern Editor");
                return;
            }

            if (tracedPath == null || tracedPath.Count < 2)
            {
                MessageBox.Show(
                    "Couldn't find a clear line in that image. This works best with a photo or screenshot of a single bright line on a plain dark background - the kind of recoil pattern diagrams people share.",
                    "Recoil Pattern Editor");
                return;
            }

            var fitted = FitToCanvas(tracedPath, DrawCanvas.Width, DrawCanvas.Height);
            var smoothed = ChaikinSmooth(fitted, iterations: 2);

            _isDrawing = false;
            _drawnPoints.Clear();
            _drawnPoints.AddRange(smoothed);

            DrawCanvas.Children.Clear();

            var startDot = new Ellipse { Width = 6, Height = 6, Fill = Brushes.LimeGreen };
            Canvas.SetLeft(startDot, _drawnPoints[0].X - 3);
            Canvas.SetTop(startDot, _drawnPoints[0].Y - 3);
            DrawCanvas.Children.Add(startDot);

            _drawPolyline = new Polyline
            {
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 2,
                Points = new PointCollection(_drawnPoints)
            };
            DrawCanvas.Children.Add(_drawPolyline);

            DrawPointCountLabel.Text = $"Traced {_drawnPoints.Count} points from image. Set Shots/Scale below, then Apply.";
        }

        /// <summary>
        /// Loads an image of a bright line on a plain dark background (the kind of
        /// hand-drawn or published "recoil pattern" diagrams people share) and walks
        /// the line into an ordered path, starting from its topmost point.
        /// </summary>
        private static List<Point>? TraceImagePath(string filePath)
        {
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            // Downscale very large images so pixel scanning stays fast - we only need
            // enough resolution to follow the shape of the line, not its exact pixels.
            BitmapSource source = bitmapImage;
            const int maxDimension = 600;
            double scaleDown = Math.Min(1.0, maxDimension / (double)Math.Max(source.PixelWidth, source.PixelHeight));
            if (scaleDown < 1.0)
                source = new TransformedBitmap(source, new ScaleTransform(scaleDown, scaleDown));

            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = width * 4;
            var pixels = new byte[height * stride];
            converted.CopyPixels(pixels, stride, 0);

            // A "line" pixel is noticeably brighter than the image's average (the plain
            // dark background), and either colorful (has real hue, not just gray noise)
            // or very bright - covers both colored lines and plain white/gray ones.
            long totalBrightness = 0;
            for (int i = 0; i < pixels.Length; i += 4)
                totalBrightness += (pixels[i] + pixels[i + 1] + pixels[i + 2]) / 3;
            double avgBrightness = totalBrightness / (double)(width * height);
            double brightnessThreshold = Math.Max(avgBrightness * 1.8, 90);

            var isLine = new bool[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = (y * stride) + (x * 4);
                    byte b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
                    int brightness = (r + g + b) / 3;
                    int maxC = Math.Max(r, Math.Max(g, b));
                    int minC = Math.Min(r, Math.Min(g, b));
                    bool colorful = (maxC - minC) > 40;

                    if (brightness > brightnessThreshold && (colorful || brightness > avgBrightness * 2.5))
                        isLine[x, y] = true;
                }
            }

            var largestComponent = LargestConnectedComponent(isLine, width, height);
            if (largestComponent.Count < 10) return null;

            return WalkPathGreedy(largestComponent);
        }

        /// <summary>
        /// Keeps only the single biggest blob of connected line pixels (8-connectivity),
        /// so stray JPEG-compression specks or background noise don't throw off the trace.
        /// </summary>
        private static List<Point> LargestConnectedComponent(bool[,] isLine, int width, int height)
        {
            var visited = new bool[width, height];
            var best = new List<Point>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!isLine[x, y] || visited[x, y]) continue;

                    var component = new List<Point>();
                    var queue = new Queue<(int x, int y)>();
                    queue.Enqueue((x, y));
                    visited[x, y] = true;

                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        component.Add(new Point(cx, cy));

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = cx + dx, ny = cy + dy;
                                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                                if (!isLine[nx, ny] || visited[nx, ny]) continue;
                                visited[nx, ny] = true;
                                queue.Enqueue((nx, ny));
                            }
                        }
                    }

                    if (component.Count > best.Count)
                        best = component;
                }
            }

            return best;
        }

        /// <summary>
        /// Walks a blob of line pixels into an ordered path by repeatedly jumping to the
        /// nearest not-yet-visited pixel, starting from the topmost point (recoil pattern
        /// diagrams read top to bottom - first shot at the top). Uses a spatial grid so
        /// this stays fast even for a blob with several thousand pixels in it.
        /// </summary>
        private static List<Point> WalkPathGreedy(List<Point> blob)
        {
            const int cellSize = 6;
            var grid = new Dictionary<(int, int), List<Point>>();
            (int, int) CellOf(Point p) => ((int)(p.X / cellSize), (int)(p.Y / cellSize));

            foreach (var p in blob)
            {
                var cell = CellOf(p);
                if (!grid.TryGetValue(cell, out var list))
                {
                    list = new List<Point>();
                    grid[cell] = list;
                }
                list.Add(p);
            }

            var start = blob.OrderBy(p => p.Y).First();
            var path = new List<Point> { start };
            RemoveFromGrid(grid, start, cellSize);

            var current = start;
            const double maxJump = 40; // a far-away "nearest" point means it's a separate, disconnected piece

            while (true)
            {
                var next = FindNearest(grid, current, cellSize);
                if (next == null) break;

                if (Distance(current, next.Value) > maxJump) break;

                path.Add(next.Value);
                RemoveFromGrid(grid, next.Value, cellSize);
                current = next.Value;
            }

            return path;
        }

        private static Point? FindNearest(Dictionary<(int, int), List<Point>> grid, Point from, int cellSize)
        {
            var (cx, cy) = ((int)(from.X / cellSize), (int)(from.Y / cellSize));

            for (int radius = 0; radius <= 20; radius++)
            {
                Point? best = null;
                double bestDist = double.MaxValue;

                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue; // outer ring only - inner cells already scanned at a smaller radius

                        if (grid.TryGetValue((cx + dx, cy + dy), out var points))
                        {
                            foreach (var p in points)
                            {
                                double d = Distance(from, p);
                                if (d < bestDist)
                                {
                                    bestDist = d;
                                    best = p;
                                }
                            }
                        }
                    }
                }

                if (best != null) return best;
            }

            return null;
        }

        private static void RemoveFromGrid(Dictionary<(int, int), List<Point>> grid, Point p, int cellSize)
        {
            var cell = ((int)(p.X / cellSize), (int)(p.Y / cellSize));
            if (grid.TryGetValue(cell, out var list))
            {
                list.Remove(p);
                if (list.Count == 0) grid.Remove(cell);
            }
        }

        private static List<Point> FitToCanvas(List<Point> path, double canvasWidth, double canvasHeight)
        {
            double minX = path.Min(p => p.X), maxX = path.Max(p => p.X);
            double minY = path.Min(p => p.Y), maxY = path.Max(p => p.Y);
            double spanX = Math.Max(maxX - minX, 1);
            double spanY = Math.Max(maxY - minY, 1);

            const double margin = 10;
            double availW = canvasWidth - margin * 2;
            double availH = canvasHeight - margin * 2;
            double scale = Math.Min(availW / spanX, availH / spanY);

            return path.Select(p => new Point(
                margin + (p.X - minX) * scale,
                margin + (p.Y - minY) * scale
            )).ToList();
        }

        #endregion

        private void ClearDrawing_Click(object sender, RoutedEventArgs e)
        {
            _isDrawing = false;
            _drawnPoints.Clear();
            DrawCanvas.Children.Clear();
            _drawPolyline = null;
            DrawPointCountLabel.Text = "Click and drag to start drawing.";
        }

        private void ApplyDrawing_Click(object sender, RoutedEventArgs e)
        {
            if (_drawnPoints.Count < 2)
            {
                MessageBox.Show("Draw a path first - click and drag across the canvas to trace the pull pattern.", "Recoil Pattern Editor");
                return;
            }

            if (!int.TryParse(DrawShotCountBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out int shotCount) || shotCount < 1)
            {
                MessageBox.Show("Shots to Generate must be a positive whole number.", "Recoil Pattern Editor");
                return;
            }

            if (!double.TryParse(DrawScaleBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double scale) || scale <= 0)
            {
                MessageBox.Show("Pull Scale must be a positive number.", "Recoil Pattern Editor");
                return;
            }

            var resampled = ResampleByArcLength(_drawnPoints, shotCount);

            Steps.Clear();
            for (int i = 1; i < resampled.Count; i++)
            {
                double dx = (resampled[i].X - resampled[i - 1].X) * scale;
                double dy = (resampled[i].Y - resampled[i - 1].Y) * scale;
                Steps.Add(new RecoilStepRow { ShotNumber = i, Dx = Math.Round(dx, 1), Dy = Math.Round(dy, 1) });
            }

            ModeList_Click(sender, e);
        }

        /// <summary>
        /// Resamples a freehand-drawn path into `count` evenly-spaced points
        /// by distance traveled along the path (not by time), so a slow part
        /// of the drag isn't over-weighted versus a fast part. The returned
        /// list always starts with the original first point, so it has
        /// count + 1 points total (count deltas = count shots).
        /// </summary>
        private static List<Point> ResampleByArcLength(List<Point> rawPoints, int count)
        {
            var result = new List<Point> { rawPoints[0] };
            if (count < 1) return result;

            double totalLength = 0;
            for (int i = 1; i < rawPoints.Count; i++)
                totalLength += Distance(rawPoints[i - 1], rawPoints[i]);

            if (totalLength <= 0)
            {
                for (int i = 0; i < count; i++) result.Add(rawPoints[0]);
                return result;
            }

            double step = totalLength / count;
            double targetDistance = step;
            double accumulated = 0;
            int segment = 1;

            while (result.Count <= count && segment < rawPoints.Count)
            {
                double segLength = Distance(rawPoints[segment - 1], rawPoints[segment]);

                if (accumulated + segLength >= targetDistance)
                {
                    double remaining = targetDistance - accumulated;
                    double t = segLength <= 0 ? 0 : remaining / segLength;
                    result.Add(Lerp(rawPoints[segment - 1], rawPoints[segment], t));
                    targetDistance += step;
                }
                else
                {
                    accumulated += segLength;
                    segment++;
                }
            }

            // Floating point slop can leave us one short at the very end of the path.
            while (result.Count <= count)
                result.Add(rawPoints[^1]);

            return result;
        }

        private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
        private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        #endregion

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
