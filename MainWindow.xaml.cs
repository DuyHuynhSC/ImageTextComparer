using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace ImageTextComparer
{
    public partial class MainWindow : Window
    {
        // Image sources
        private BitmapImage? _imageSource1;
        private BitmapImage? _imageSource2;

        // Selection drawing state
        private bool _isDrawing1;
        private Point _startPoint1;
        private Rect _selectionRect1;

        private bool _isDrawing2;
        private Point _startPoint2;
        private Rect _selectionRect2;

        // Theme and Diff Cache
        private bool _isDarkTheme = true;
        private System.Collections.Generic.List<DiffResult>? _lastDiffs;

        private const string ConfigFileName = "config.json";

        public class AppConfig
        {
            public string Endpoint { get; set; } = string.Empty;
            public string ApiKey { get; set; } = string.Empty;
            public string ModelName { get; set; } = string.Empty;
            public string Prompt { get; set; } = string.Empty;
            public bool BypassSsl { get; set; } = false;
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
        }

        #region Configuration Storage

        private string GetConfigPath()
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
        }

        private void LoadConfig()
        {
            try
            {
                string path = GetConfigPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    if (config != null)
                    {
                        TxtEndpoint.Text = config.Endpoint;
                        TxtApiKey.Text = config.ApiKey;
                        TxtModelName.Text = config.ModelName;
                        TxtPrompt.Text = config.Prompt;
                        ChkBypassSsl.IsChecked = config.BypassSsl;

                        // Auto-upgrade prompt if it's the old prompt (doesn't contain Japanese instructions)
                        if (string.IsNullOrEmpty(config.Prompt) || !config.Prompt.Contains("画像からテキスト"))
                        {
                            TxtPrompt.Text = "Hãy trích xuất chính xác từng ký tự trong hình ảnh này dưới dạng OCR thuần túy. Phân biệt cực kỳ rõ ràng giữa dakuten (゛ - ví dụ: バ) và handakuten (゜ - ví dụ: パ). Tuyệt đối không tự sửa lỗi chính tả theo ngữ cảnh.\n\n画像からテキストを正確に抽出（OCR）してください。文脈による自動修正は一切行わないでください。特に日本語の濁点（゛、例：バ）と半濁点（゜、例：パ）を厳密に区別し、見たままの文字を出力してください。";
                            SaveConfig();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải cấu hình: {ex.Message}", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveConfig()
        {
            try
            {
                var config = new AppConfig
                {
                    Endpoint = TxtEndpoint.Text.Trim(),
                    ApiKey = TxtApiKey.Text.Trim(),
                    ModelName = TxtModelName.Text.Trim(),
                    Prompt = TxtPrompt.Text,
                    BypassSsl = ChkBypassSsl.IsChecked ?? false
                };

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(GetConfigPath(), json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể lưu cấu hình: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            SaveConfig();
            MessageBox.Show("Đã lưu cấu hình AI thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnResetConfig_Click(object sender, RoutedEventArgs e)
        {
            TxtEndpoint.Text = "http://localhost:11434/v1/chat/completions";
            TxtApiKey.Text = "";
            TxtModelName.Text = "qwen2.5-vl";
            TxtPrompt.Text = "Hãy trích xuất chính xác từng ký tự trong hình ảnh này dưới dạng OCR thuần túy. Phân biệt cực kỳ rõ ràng giữa dakuten (゛ - ví dụ: バ) và handakuten (゜ - ví dụ: パ). Tuyệt đối không tự sửa lỗi chính tả theo ngữ cảnh.\n\n画像からテキストを正確に抽出（OCR）してください。文脈による自動修正は一切行わないでください。特に日本語の濁点（゛、例：バ）と半濁点（゜、例：パ）を厳密に区別し、見たままの文字を出力してください。";
            ChkBypassSsl.IsChecked = false;
            SaveConfig();
        }

        #endregion

        #region Image Loading

        private void BtnLoadImage1_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp|All Files|*.*",
                Title = "Chọn ảnh thứ 1"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(dialog.FileName);
                    bitmap.EndInit();

                    bitmap.Freeze(); // Freeze bitmap to allow cross-thread access

                    _imageSource1 = bitmap;
                    Img1.Source = bitmap;
                    Placeholder1.Visibility = Visibility.Collapsed;
                    TxtInfo1.Text = $"Kích thước: {bitmap.PixelWidth}x{bitmap.PixelHeight} | File: {System.IO.Path.GetFileName(dialog.FileName)}";

                    // Bind dimensions to image pixel size for native Viewbox scaling
                    GridImage1.Width = bitmap.PixelWidth;
                    GridImage1.Height = bitmap.PixelHeight;
                    Canvas1.Width = bitmap.PixelWidth;
                    Canvas1.Height = bitmap.PixelHeight;
                    Img1.Width = bitmap.PixelWidth;
                    Img1.Height = bitmap.PixelHeight;
                    RectSelection1.StrokeThickness = Math.Max(1.5, bitmap.PixelWidth / 200.0);

                    ClearCropSelection(1);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi tải hình ảnh 1: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnLoadImage2_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp|All Files|*.*",
                Title = "Chọn ảnh thứ 2"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(dialog.FileName);
                    bitmap.EndInit();

                    bitmap.Freeze(); // Freeze bitmap to allow cross-thread access

                    _imageSource2 = bitmap;
                    Img2.Source = bitmap;
                    Placeholder2.Visibility = Visibility.Collapsed;
                    TxtInfo2.Text = $"Kích thước: {bitmap.PixelWidth}x{bitmap.PixelHeight} | File: {System.IO.Path.GetFileName(dialog.FileName)}";

                    // Bind dimensions to image pixel size for native Viewbox scaling
                    GridImage2.Width = bitmap.PixelWidth;
                    GridImage2.Height = bitmap.PixelHeight;
                    Canvas2.Width = bitmap.PixelWidth;
                    Canvas2.Height = bitmap.PixelHeight;
                    Img2.Width = bitmap.PixelWidth;
                    Img2.Height = bitmap.PixelHeight;
                    RectSelection2.StrokeThickness = Math.Max(1.5, bitmap.PixelWidth / 200.0);

                    ClearCropSelection(2);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi tải hình ảnh 2: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Mouse Cropping / Selection Logic

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var canvas = (Canvas)sender;
            bool isImg1 = canvas == Canvas1;

            if (isImg1)
            {
                if (_imageSource1 == null) return;
                _isDrawing1 = true;
                _startPoint1 = e.GetPosition(canvas);
                canvas.CaptureMouse();

                Canvas.SetLeft(RectSelection1, _startPoint1.X);
                Canvas.SetTop(RectSelection1, _startPoint1.Y);
                RectSelection1.Width = 0;
                RectSelection1.Height = 0;
                RectSelection1.Visibility = Visibility.Visible;
            }
            else
            {
                if (_imageSource2 == null) return;
                _isDrawing2 = true;
                _startPoint2 = e.GetPosition(canvas);
                canvas.CaptureMouse();

                Canvas.SetLeft(RectSelection2, _startPoint2.X);
                Canvas.SetTop(RectSelection2, _startPoint2.Y);
                RectSelection2.Width = 0;
                RectSelection2.Height = 0;
                RectSelection2.Visibility = Visibility.Visible;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var canvas = (Canvas)sender;
            bool isImg1 = canvas == Canvas1;

            if (isImg1 && _isDrawing1)
            {
                var currentPoint = e.GetPosition(canvas);
                double x = Math.Min(_startPoint1.X, currentPoint.X);
                double y = Math.Min(_startPoint1.Y, currentPoint.Y);
                double w = Math.Abs(_startPoint1.X - currentPoint.X);
                double h = Math.Abs(_startPoint1.Y - currentPoint.Y);

                // Clamp within Canvas actual dimensions
                x = Math.Max(0, Math.Min(canvas.ActualWidth, x));
                y = Math.Max(0, Math.Min(canvas.ActualHeight, y));
                w = Math.Min(canvas.ActualWidth - x, w);
                h = Math.Min(canvas.ActualHeight - y, h);

                Canvas.SetLeft(RectSelection1, x);
                Canvas.SetTop(RectSelection1, y);
                RectSelection1.Width = w;
                RectSelection1.Height = h;

                _selectionRect1 = new Rect(x, y, w, h);
            }
            else if (!isImg1 && _isDrawing2)
            {
                var currentPoint = e.GetPosition(canvas);
                double x = Math.Min(_startPoint2.X, currentPoint.X);
                double y = Math.Min(_startPoint2.Y, currentPoint.Y);
                double w = Math.Abs(_startPoint2.X - currentPoint.X);
                double h = Math.Abs(_startPoint2.Y - currentPoint.Y);

                // Clamp within Canvas actual dimensions
                x = Math.Max(0, Math.Min(canvas.ActualWidth, x));
                y = Math.Max(0, Math.Min(canvas.ActualHeight, y));
                w = Math.Min(canvas.ActualWidth - x, w);
                h = Math.Min(canvas.ActualHeight - y, h);

                Canvas.SetLeft(RectSelection2, x);
                Canvas.SetTop(RectSelection2, y);
                RectSelection2.Width = w;
                RectSelection2.Height = h;

                _selectionRect2 = new Rect(x, y, w, h);
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var canvas = (Canvas)sender;
            bool isImg1 = canvas == Canvas1;

            if (isImg1 && _isDrawing1)
            {
                _isDrawing1 = false;
                canvas.ReleaseMouseCapture();

                // If selection is too small, cancel it
                if (_selectionRect1.Width < 5 || _selectionRect1.Height < 5)
                {
                    ClearCropSelection(1);
                }
                else
                {
                    BtnClearCrop1.Visibility = Visibility.Visible;
                    TxtInfo1.Text = $"Kích thước: {_imageSource1!.PixelWidth}x{_imageSource1!.PixelHeight} | Đang khoanh vùng chọn";
                }
            }
            else if (!isImg1 && _isDrawing2)
            {
                _isDrawing2 = false;
                canvas.ReleaseMouseCapture();

                if (_selectionRect2.Width < 5 || _selectionRect2.Height < 5)
                {
                    ClearCropSelection(2);
                }
                else
                {
                    BtnClearCrop2.Visibility = Visibility.Visible;
                    TxtInfo2.Text = $"Kích thước: {_imageSource2!.PixelWidth}x{_imageSource2!.PixelHeight} | Đang khoanh vùng chọn";
                }
            }
        }

        private void BtnClearCrop1_Click(object sender, RoutedEventArgs e)
        {
            ClearCropSelection(1);
        }

        private void BtnClearCrop2_Click(object sender, RoutedEventArgs e)
        {
            ClearCropSelection(2);
        }

        private void ClearCropSelection(int imageIndex)
        {
            if (imageIndex == 1)
            {
                _selectionRect1 = Rect.Empty;
                RectSelection1.Visibility = Visibility.Collapsed;
                BtnClearCrop1.Visibility = Visibility.Collapsed;
                if (_imageSource1 != null)
                {
                    TxtInfo1.Text = $"Kích thước: {_imageSource1.PixelWidth}x{_imageSource1.PixelHeight} | Quét toàn bộ ảnh";
                }
            }
            else
            {
                _selectionRect2 = Rect.Empty;
                RectSelection2.Visibility = Visibility.Collapsed;
                BtnClearCrop2.Visibility = Visibility.Collapsed;
                if (_imageSource2 != null)
                {
                    TxtInfo2.Text = $"Kích thước: {_imageSource2.PixelWidth}x{_imageSource2.PixelHeight} | Quét toàn bộ ảnh";
                }
            }
        }

        #endregion

        #region Coordinate Mapping and Cropping

        private byte[] GetProcessedImageBytes(BitmapSource original, Rect uiRect)
        {
            BitmapSource finalSource = original;

            if (!uiRect.IsEmpty && uiRect.Width > 0 && uiRect.Height > 0)
            {
                double pixelWidth = original.PixelWidth;
                double pixelHeight = original.PixelHeight;

                // Coordinates are already mapped 1-to-1 to pixels thanks to Viewbox layout scaling
                double cropX = uiRect.X;
                double cropY = uiRect.Y;
                double cropW = uiRect.Width;
                double cropH = uiRect.Height;

                // Clamp to image boundaries just to prevent rounding errors
                cropX = Math.Max(0, Math.Min(pixelWidth - 1, cropX));
                cropY = Math.Max(0, Math.Min(pixelHeight - 1, cropY));
                cropW = Math.Max(1, Math.Min(pixelWidth - cropX, cropW));
                cropH = Math.Max(1, Math.Min(pixelHeight - cropY, cropH));

                try
                {
                    finalSource = new CroppedBitmap(original, new Int32Rect((int)cropX, (int)cropY, (int)cropW, (int)cropH));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi crop ảnh: {ex.Message}");
                    finalSource = original;
                }
            }

            // If the bitmap is too small, upscale it with high-quality scaling for better AI Vision OCR accuracy
            if (finalSource.PixelHeight < 150)
            {
                double scale = 300.0 / finalSource.PixelHeight;
                var scaleTransform = new ScaleTransform(scale, scale);
                var scaledBitmap = new TransformedBitmap(finalSource, scaleTransform);
                scaledBitmap.Freeze();
                finalSource = scaledBitmap;
            }

            // Convert BitmapSource to PNG bytes
            using (var stream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(finalSource));
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        #endregion

        #region Core Orchestration: OCR & Diff

        private async void BtnCompare_Click(object sender, RoutedEventArgs e)
        {
            if (_imageSource1 == null || _imageSource2 == null)
            {
                MessageBox.Show("Vui lòng tải đủ 2 hình ảnh trước khi so sánh.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string endpoint = TxtEndpoint.Text.Trim();
            string apiKey = TxtApiKey.Text.Trim();
            string modelName = TxtModelName.Text.Trim();
            string prompt = TxtPrompt.Text;

            // Apply SSL bypass setting
            VisionApiService.BypassSslValidation = ChkBypassSsl.IsChecked ?? false;

            // UI feedback
            BtnCompare.IsEnabled = false;
            TxtStatus.Text = "Đang xử lý...";
            RtfText1.Document.Blocks.Clear();
            RtfText2.Document.Blocks.Clear();

            try
            {
                // 1. Prepare image bytes
                TxtStatus.Text = "Chuẩn bị hình ảnh và cắt vùng quét...";
                
                byte[] imgBytes1 = await Task.Run(() => GetProcessedImageBytes(_imageSource1, _selectionRect1));
                byte[] imgBytes2 = await Task.Run(() => GetProcessedImageBytes(_imageSource2, _selectionRect2));

                // Save cropped images locally for diagnostics (saved as crop_debug_1.png and crop_debug_2.png)
                try
                {
                    string debugDir = AppDomain.CurrentDomain.BaseDirectory;
                    File.WriteAllBytes(System.IO.Path.Combine(debugDir, "crop_debug_1.png"), imgBytes1);
                    File.WriteAllBytes(System.IO.Path.Combine(debugDir, "crop_debug_2.png"), imgBytes2);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Không thể lưu ảnh debug: {ex.Message}");
                }

                // 2. Call Vision API (concurrently to save time)
                string cropDetail1 = _selectionRect1.IsEmpty ? "Toàn bộ" : $"Cắt {(int)_selectionRect1.Width}x{(int)_selectionRect1.Height}";
                string cropDetail2 = _selectionRect2.IsEmpty ? "Toàn bộ" : $"Cắt {(int)_selectionRect2.Width}x{(int)_selectionRect2.Height}";
                TxtStatus.Text = $"AI đang trích xuất... (Ảnh 1: {cropDetail1}, Ảnh 2: {cropDetail2})";
                
                var task1 = VisionApiService.ExtractTextAsync(endpoint, apiKey, modelName, prompt, imgBytes1);
                var task2 = VisionApiService.ExtractTextAsync(endpoint, apiKey, modelName, prompt, imgBytes2);

                await Task.WhenAll(task1, task2);

                string text1 = task1.Result;
                string text2 = task2.Result;

                // 3. Diff and highlight results
                TxtStatus.Text = "Đang so sánh văn bản trích xuất...";
                
                var diffs = await Task.Run(() => DiffEngine.Compare(text1, text2));
                _lastDiffs = diffs;

                RichTextBoxHelper.RenderDiff(RtfText1, RtfText2, diffs, _isDarkTheme);

                TxtStatus.Text = "Đã so sánh xong!";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Lỗi xử lý!";
                string details = GetFullExceptionMessage(ex);
                MessageBox.Show($"Xảy ra lỗi trong quá trình xử lý:\n{details}", "Lỗi hệ thống", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnCompare.IsEnabled = true;
                SaveConfig(); // Auto-save settings on successful/attempted runs
            }
        }

        private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            if (_isDarkTheme)
            {
                BtnToggleTheme.Content = "☀️ Sáng";

                Resources["WindowBg"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x12));
                Resources["PanelBg"] = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1C));
                Resources["PanelBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2A));
                Resources["TextFg"] = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF5));
                Resources["TextLabelFg"] = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xAA));
                Resources["TextBoxBg"] = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x14));
                Resources["TextBoxBorder"] = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46));
                Resources["ActionBtnBg"] = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2A));
                Resources["ActionBtnFg"] = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF5));
                Resources["ImageAreaBg"] = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0C));
            }
            else
            {
                BtnToggleTheme.Content = "🌙 Tối";

                Resources["WindowBg"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
                Resources["PanelBg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                Resources["PanelBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                Resources["TextFg"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
                Resources["TextLabelFg"] = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                Resources["TextBoxBg"] = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                Resources["TextBoxBorder"] = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
                Resources["ActionBtnBg"] = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                Resources["ActionBtnFg"] = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
                Resources["ImageAreaBg"] = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
            }

            // Re-render text comparison highlights with new theme colors
            if (_lastDiffs != null)
            {
                RichTextBoxHelper.RenderDiff(RtfText1, RtfText2, _lastDiffs, _isDarkTheme);
            }
        }

        private string GetFullExceptionMessage(Exception ex)
        {
            var messages = new System.Collections.Generic.List<string>();
            var current = ex;
            while (current != null)
            {
                messages.Add(current.Message);
                current = current.InnerException;
            }
            return string.Join("\n--> ", messages);
        }

        #endregion
    }
}