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
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Application = System.Windows.Application;

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
        private bool _isDarkTheme = false;
        private System.Collections.Generic.List<DiffResult>? _lastDiffs;
        private string? _lastExtractedText1;
        private string? _lastExtractedText2;

        private bool _isRestoringHistory = false;

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
            ApplyTheme();
            LoadConfig();
            AutoRestoreSession();
            RefreshHistoryList();
        }

        #region Configuration Storage

        private string GetAppDataFolder()
        {
            string folder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ImageTextComparer"
            );
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return folder;
        }

        private string GetConfigPath()
        {
            return System.IO.Path.Combine(GetAppDataFolder(), ConfigFileName);
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

                        bool upgraded = false;
                        if (config.Endpoint == "http://localhost:11434/v1/chat/completions" || string.IsNullOrEmpty(config.Endpoint))
                        {
                            TxtEndpoint.Text = "https://models-gateway.fujinet.net/v1/chat/completions";
                            upgraded = true;
                        }
                        if (config.ModelName == "qwen2.5-vl" || config.ModelName == "qwen-vl" || string.IsNullOrEmpty(config.ModelName))
                        {
                            TxtModelName.Text = "programmer";
                            upgraded = true;
                        }

                        // Auto-upgrade prompt if it's the old prompt (doesn't contain unbiased example)
                        if (string.IsNullOrEmpty(config.Prompt) || !config.Prompt.Contains("unbiased example"))
                        {
                            TxtPrompt.Text = "Perform strict, literal character-by-character OCR on the image. Extract each character separated by a space using this unbiased example format: \"A B C D E\". Do NOT auto-correct spelling, do NOT assume vocabulary, and do NOT change characters based on context. Specifically, in Japanese, carefully distinguish between Dakuten (゛, e.g., \"バ\", \"ズ\") and Handakuten (゜, e.g., \"パ\", \"プ\"). Output exactly what you see visually.\n\nHãy trích xuất chính xác từng ký tự dưới dạng OCR thuần túy. Xuất ra từng ký tự cách nhau bằng một dấu cách (Ví dụ: \"A B C D E\"). Tuyệt đối KHÔNG tự sửa chính tả hay sửa từ theo ngữ cảnh.\n\n画像からテキストを1文字ずつ正確に抽出（OCR）し、文字と文字の間に半角スペースを入れて出力してください（例：「A B C D E」）。文脈によるスペル修正や推測、単語の自動修正は一切行わないでください。特に濁点（゛、例：バ）と半濁点（゜、例：パ）をビジュアル通りに厳密に区別してください。";
                            upgraded = true;
                        }

                        if (upgraded)
                        {
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
            TxtEndpoint.Text = "https://models-gateway.fujinet.net/v1/chat/completions";
            TxtApiKey.Text = "";
            TxtModelName.Text = "programmer";
            TxtPrompt.Text = "Perform strict, literal character-by-character OCR on the image. Extract each character separated by a space using this unbiased example format: \"A B C D E\". Do NOT auto-correct spelling, do NOT assume vocabulary, and do NOT change characters based on context. Specifically, in Japanese, carefully distinguish between Dakuten (゛, e.g., \"バ\", \"ズ\") and Handakuten (゜, e.g., \"パ\", \"プ\"). Output exactly what you see visually.\n\nHãy trích xuất chính xác từng ký tự dưới dạng OCR thuần túy. Xuất ra từng ký tự cách nhau bằng một dấu cách (Ví dụ: \"A B C D E\"). Tuyệt đối KHÔNG tự sửa chính tả hay sửa từ theo ngữ cảnh.\n\n画像からテキストを1文字ずつ正確に抽出（OCR）し、文字と文字の間に半角スペースを入れて出力してください（例：「A B C D E」）。文脈によるスペル修正や推測、単語 của自動修正は一切行わないでください。特に濁点（゛、例：バ）と半濁点（゜、例：パ）をビジュアル通りに厳密に区別してください。".Replace("単語 của自動修正", "単語の自動修正");
            ChkBypassSsl.IsChecked = false;
            SaveConfig();
        }

        private void BtnOpenDebugFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = GetAppDataFolder();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở thư mục chẩn đoán: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool _isSidebarCollapsed = false;

        private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _isSidebarCollapsed = !_isSidebarCollapsed;
            if (_isSidebarCollapsed)
            {
                BorderSidebar.Visibility = Visibility.Collapsed;
                ColSidebarContent.Width = new GridLength(0);
                BtnToggleSidebar.Content = "▶";
                BtnToggleSidebar.ToolTip = "Mở rộng cấu hình AI";
            }
            else
            {
                BorderSidebar.Visibility = Visibility.Visible;
                ColSidebarContent.Width = new GridLength(280);
                BtnToggleSidebar.Content = "◀";
                BtnToggleSidebar.ToolTip = "Thu gọn cấu hình AI";
            }
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

        private void BtnCaptureImage1_Click(object sender, RoutedEventArgs e)
        {
            ExecuteScreenCapture(1);
        }

        private void BtnCaptureImage2_Click(object sender, RoutedEventArgs e)
        {
            ExecuteScreenCapture(2);
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
            AutoSaveSession();
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
            AutoSaveSession();
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

            // If the bitmap is too small, upscale it with NearestNeighbor scaling to keep text pixels crisp and sharp
            if (finalSource.PixelHeight < 300)
            {
                double scale = 300.0 / finalSource.PixelHeight;
                finalSource = ScaleBitmapNearestNeighbor(finalSource, scale);
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

        private static BitmapSource ScaleBitmapNearestNeighbor(BitmapSource source, double scale)
        {
            int width = (int)Math.Round(source.PixelWidth * scale);
            int height = (int)Math.Round(source.PixelHeight * scale);

            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
                context.DrawImage(source, new Rect(0, 0, width, height));
            }

            var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            target.Freeze();
            return target;
        }

        private static string NormalizeSpaceSeparatedOcr(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // Normalize newlines
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                // Replace double spaces with a temporary placeholder, remove single spaces, restore double spaces as single spaces
                line = line.Replace("  ", "___WORD_GAP___");
                line = line.Replace(" ", "");
                line = line.Replace("___WORD_GAP___", " ");
                lines[i] = line;
            }
            
            return string.Join(Environment.NewLine, lines);
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

                // Save cropped images locally for diagnostics (saved as crop_debug_1.png and crop_debug_2.png in AppData)
                try
                {
                    string debugDir = GetAppDataFolder();
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

                string text1 = NormalizeSpaceSeparatedOcr(task1.Result);
                string text2 = NormalizeSpaceSeparatedOcr(task2.Result);

                // 3. Diff and highlight results
                TxtStatus.Text = "Đang so sánh văn bản trích xuất...";
                
                _lastExtractedText1 = text1;
                _lastExtractedText2 = text2;

                var diffs = await Task.Run(() => DiffEngine.Compare(text1, text2));
                _lastDiffs = diffs;

                RichTextBoxHelper.RenderDiff(RtfText1, RtfText2, diffs, _isDarkTheme);

                TxtStatus.Text = "Đã so sánh xong!";
                AutoSaveSession();
                AddSessionToHistory();
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

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private static BitmapSource ConvertBitmapToBitmapSource(System.Drawing.Bitmap bmp)
        {
            IntPtr hBitmap = bmp.GetHbitmap();
            try
            {
                var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }



        private async void ExecuteScreenCapture(int imageNumber)
        {
            // 1. Hide main window
            this.Hide();
            await Task.Delay(300); // Wait for window to fade out completely

            var captureWindows = new System.Collections.Generic.List<ScreenCaptureWindow>();
            var tcs = new TaskCompletionSource<CapturedResult?>();

            try
            {
                // 2. Capture GDI screenshots and launch overlay windows for each monitor
                var screens = System.Windows.Forms.Screen.AllScreens;
                foreach (var screen in screens)
                {
                    var bounds = screen.Bounds;
                    using (var bmp = new System.Drawing.Bitmap(bounds.Width, bounds.Height))
                    {
                        using (var g = System.Drawing.Graphics.FromImage(bmp))
                        {
                            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bmp.Size);
                        }
                        var screenImageSource = ConvertBitmapToBitmapSource(bmp);
                        var captureWin = new ScreenCaptureWindow(screenImageSource, bounds, tcs);
                        captureWindows.Add(captureWin);
                    }
                }

                // Show all overlay windows simultaneously on their respective monitors
                foreach (var win in captureWindows)
                {
                    win.Show();
                    win.Activate();
                }

                // 3. Wait until user selects a region on any window or cancels
                var result = await tcs.Task;

                if (result != null)
                {
                    var bmpImg = new BitmapImage();
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(result.Image));
                    
                    using (var ms = new MemoryStream())
                    {
                        encoder.Save(ms);
                        ms.Seek(0, SeekOrigin.Begin);
                        
                        bmpImg.BeginInit();
                        bmpImg.StreamSource = ms;
                        bmpImg.CacheOption = BitmapCacheOption.OnLoad;
                        bmpImg.EndInit();
                        bmpImg.Freeze();
                    }

                    // 4. Load into corresponding Image slot
                    if (imageNumber == 1)
                    {
                        _imageSource1 = bmpImg;
                        Img1.Source = _imageSource1;
                        Placeholder1.Visibility = Visibility.Collapsed;
                        TxtInfo1.Text = $"Kích thước: {bmpImg.PixelWidth}x{bmpImg.PixelHeight}px (Đã chụp)";

                        GridImage1.Width = bmpImg.PixelWidth;
                        GridImage1.Height = bmpImg.PixelHeight;
                        Canvas1.Width = bmpImg.PixelWidth;
                        Canvas1.Height = bmpImg.PixelHeight;
                        Img1.Width = bmpImg.PixelWidth;
                        Img1.Height = bmpImg.PixelHeight;
                        RectSelection1.StrokeThickness = Math.Max(1.5, bmpImg.PixelWidth / 200.0);

                        ClearCropSelection(1);
                    }
                    else
                    {
                        _imageSource2 = bmpImg;
                        Img2.Source = _imageSource2;
                        Placeholder2.Visibility = Visibility.Collapsed;
                        TxtInfo2.Text = $"Kích thước: {bmpImg.PixelWidth}x{bmpImg.PixelHeight}px (Đã chụp)";

                        GridImage2.Width = bmpImg.PixelWidth;
                        GridImage2.Height = bmpImg.PixelHeight;
                        Canvas2.Width = bmpImg.PixelWidth;
                        Canvas2.Height = bmpImg.PixelHeight;
                        Img2.Width = bmpImg.PixelWidth;
                        Img2.Height = bmpImg.PixelHeight;
                        RectSelection2.StrokeThickness = Math.Max(1.5, bmpImg.PixelWidth / 200.0);

                        ClearCropSelection(2);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi chụp màn hình: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 5. Close all overlay windows and restore main window
                foreach (var win in captureWindows)
                {
                    try
                    {
                        win.Close();
                    }
                    catch { }
                }
                this.Show();
                this.Activate();
            }
        }

        #endregion

        #region Session Management

        private static string? BitmapImageToBase64(BitmapImage? bitmapImage)
        {
            if (bitmapImage == null) return null;
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        private static BitmapImage? Base64ToBitmapImage(string? base64String)
        {
            if (string.IsNullOrEmpty(base64String)) return null;
            byte[] binaryData = Convert.FromBase64String(base64String);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource = new MemoryStream(binaryData);
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }

        private void AutoSaveSession()
        {
            try
            {
                string path = System.IO.Path.Combine(GetAppDataFolder(), "last_session.json");
                SaveSessionToFile(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Không thể tự động lưu phiên: {ex.Message}");
            }
        }

        private void AutoRestoreSession()
        {
            try
            {
                string path = System.IO.Path.Combine(GetAppDataFolder(), "last_session.json");
                if (File.Exists(path))
                {
                    LoadSessionFromFile(path);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Không thể tự động khôi phục phiên: {ex.Message}");
            }
        }

        private void SaveSessionToFile(string filePath)
        {
            var session = new SessionData
            {
                ImageBase64_1 = BitmapImageToBase64(_imageSource1),
                ImageBase64_2 = BitmapImageToBase64(_imageSource2),
                
                RectX_1 = _selectionRect1.X,
                RectY_1 = _selectionRect1.Y,
                RectW_1 = _selectionRect1.Width,
                RectH_1 = _selectionRect1.Height,

                RectX_2 = _selectionRect2.X,
                RectY_2 = _selectionRect2.Y,
                RectW_2 = _selectionRect2.Width,
                RectH_2 = _selectionRect2.Height,

                Text_1 = _lastExtractedText1,
                Text_2 = _lastExtractedText2
            };

            string json = JsonSerializer.Serialize(session);
            File.WriteAllText(filePath, json);
        }

        private void LoadSessionFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return;

            string json = File.ReadAllText(filePath);
            var session = JsonSerializer.Deserialize<SessionData>(json);
            if (session == null) return;

            // 1. Restore images and selections
            var img1 = Base64ToBitmapImage(session.ImageBase64_1);
            var rect1 = (session.RectW_1 > 0 && session.RectH_1 > 0)
                ? new Rect(session.RectX_1, session.RectY_1, session.RectW_1, session.RectH_1)
                : Rect.Empty;

            if (img1 != null)
            {
                RestoreImageState(1, img1, rect1);
            }
            else
            {
                ClearImageState(1);
            }

            var img2 = Base64ToBitmapImage(session.ImageBase64_2);
            var rect2 = (session.RectW_2 > 0 && session.RectH_2 > 0)
                ? new Rect(session.RectX_2, session.RectY_2, session.RectW_2, session.RectH_2)
                : Rect.Empty;

            if (img2 != null)
            {
                RestoreImageState(2, img2, rect2);
            }
            else
            {
                ClearImageState(2);
            }

            // 2. Restore texts and diff rendering
            _lastExtractedText1 = session.Text_1;
            _lastExtractedText2 = session.Text_2;

            if (!string.IsNullOrEmpty(_lastExtractedText1) || !string.IsNullOrEmpty(_lastExtractedText2))
            {
                string t1 = _lastExtractedText1 ?? "";
                string t2 = _lastExtractedText2 ?? "";
                var diffs = DiffEngine.Compare(t1, t2);
                _lastDiffs = diffs;
                RichTextBoxHelper.RenderDiff(RtfText1, RtfText2, diffs, _isDarkTheme);
            }
            else
            {
                RtfText1.Document.Blocks.Clear();
                RtfText2.Document.Blocks.Clear();
                _lastDiffs = null;
            }
        }

        private void RestoreImageState(int imageNumber, BitmapImage bitmap, Rect selectionRect)
        {
            if (imageNumber == 1)
            {
                _imageSource1 = bitmap;
                Img1.Source = bitmap;
                Placeholder1.Visibility = Visibility.Collapsed;
                TxtInfo1.Text = $"Kích thước: {bitmap.PixelWidth}x{bitmap.PixelHeight} | Quét toàn bộ ảnh";

                GridImage1.Width = bitmap.PixelWidth;
                GridImage1.Height = bitmap.PixelHeight;
                Canvas1.Width = bitmap.PixelWidth;
                Canvas1.Height = bitmap.PixelHeight;
                Img1.Width = bitmap.PixelWidth;
                Img1.Height = bitmap.PixelHeight;
                RectSelection1.StrokeThickness = Math.Max(1.5, bitmap.PixelWidth / 200.0);

                _selectionRect1 = selectionRect;
                if (!selectionRect.IsEmpty && selectionRect.Width > 0 && selectionRect.Height > 0)
                {
                    RectSelection1.Visibility = Visibility.Visible;
                    Canvas.SetLeft(RectSelection1, selectionRect.X);
                    Canvas.SetTop(RectSelection1, selectionRect.Y);
                    RectSelection1.Width = selectionRect.Width;
                    RectSelection1.Height = selectionRect.Height;
                    BtnClearCrop1.Visibility = Visibility.Visible;
                    TxtInfo1.Text = $"Kích thước: {bitmap.PixelWidth}x{bitmap.PixelHeight} | Đang khoanh vùng chọn";
                }
                else
                {
                    RectSelection1.Visibility = Visibility.Collapsed;
                    BtnClearCrop1.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                _imageSource2 = bitmap;
                Img2.Source = bitmap;
                Placeholder2.Visibility = Visibility.Collapsed;
                TxtInfo2.Text = $"Kích thước: {bitmap.PixelWidth}x{bitmap.PixelHeight} | Quét toàn bộ ảnh";

                GridImage2.Width = bitmap.PixelWidth;
                GridImage2.Height = bitmap.PixelHeight;
                Canvas2.Width = bitmap.PixelWidth;
                Canvas2.Height = bitmap.PixelHeight;
                Img2.Width = bitmap.PixelWidth;
                Img2.Height = bitmap.PixelHeight;
                RectSelection2.StrokeThickness = Math.Max(1.5, bitmap.PixelWidth / 200.0);

                _selectionRect2 = selectionRect;
                if (!selectionRect.IsEmpty && selectionRect.Width > 0 && selectionRect.Height > 0)
                {
                    RectSelection2.Visibility = Visibility.Visible;
                    Canvas.SetLeft(RectSelection2, selectionRect.X);
                    Canvas.SetTop(RectSelection2, selectionRect.Y);
                    RectSelection2.Width = selectionRect.Width;
                    RectSelection2.Height = selectionRect.Height;
                    BtnClearCrop2.Visibility = Visibility.Visible;
                    TxtInfo2.Text = $"Kích thước: {bitmap.PixelWidth}x{bitmap.PixelHeight} | Đang khoanh vùng chọn";
                }
                else
                {
                    RectSelection2.Visibility = Visibility.Collapsed;
                    BtnClearCrop2.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ClearImageState(int imageNumber)
        {
            if (imageNumber == 1)
            {
                _imageSource1 = null;
                Img1.Source = null;
                Placeholder1.Visibility = Visibility.Visible;
                TxtInfo1.Text = "Kích thước: -";
                _selectionRect1 = Rect.Empty;
                RectSelection1.Visibility = Visibility.Collapsed;
                BtnClearCrop1.Visibility = Visibility.Collapsed;
            }
            else
            {
                _imageSource2 = null;
                Img2.Source = null;
                Placeholder2.Visibility = Visibility.Visible;
                TxtInfo2.Text = "Kích thước: -";
                _selectionRect2 = Rect.Empty;
                RectSelection2.Visibility = Visibility.Collapsed;
                BtnClearCrop2.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnSaveSession_Click(object sender, RoutedEventArgs e)
        {
            if (_imageSource1 == null && _imageSource2 == null)
            {
                MessageBox.Show("Không có dữ liệu phiên làm việc để lưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string sessionFolder = System.IO.Path.Combine(baseDir, "SavedSessions");
                if (!Directory.Exists(sessionFolder))
                {
                    Directory.CreateDirectory(sessionFolder);
                }

                string fileName = $"session_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string filePath = System.IO.Path.Combine(sessionFolder, fileName);

                SaveSessionToFile(filePath);
                MessageBox.Show($"Lưu phiên làm việc thành công tại:\n{filePath}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu phiên làm việc: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLoadSession_Click(object sender, RoutedEventArgs e)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string sessionFolder = System.IO.Path.Combine(baseDir, "SavedSessions");

            var dialog = new OpenFileDialog
            {
                Filter = "Session Files|*.json",
                Title = "Mở phiên làm việc",
                InitialDirectory = Directory.Exists(sessionFolder) ? sessionFolder : baseDir
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    LoadSessionFromFile(dialog.FileName);
                    AutoSaveSession(); // Update last_session auto-save
                    MessageBox.Show("Khôi phục phiên làm việc thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi khôi phục phiên làm việc: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string GetHistoryFolder()
        {
            string folder = System.IO.Path.Combine(GetAppDataFolder(), "History");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return folder;
        }

        private void AddSessionToHistory()
        {
            try
            {
                string folder = GetHistoryFolder();
                string fileName = $"session_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string filePath = System.IO.Path.Combine(folder, fileName);

                SaveSessionToFile(filePath);

                // Cap the history at 20 items (delete oldest if exceeded)
                var files = Directory.GetFiles(folder, "session_*.json");
                if (files.Length > 20)
                {
                    Array.Sort(files);
                    for (int i = 0; i < files.Length - 20; i++)
                    {
                        try { File.Delete(files[i]); } catch { }
                    }
                }

                RefreshHistoryList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Không thể thêm lịch sử phiên: {ex.Message}");
            }
        }

        public class HistoryItem
        {
            public string DisplayText { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;

            public override string ToString() => DisplayText;
        }

        private void RefreshHistoryList()
        {
            try
            {
                LstHistory.SelectionChanged -= LstHistory_SelectionChanged; // Disable event triggers during list population
                LstHistory.Items.Clear();

                string folder = GetHistoryFolder();
                if (!Directory.Exists(folder)) return;

                var files = Directory.GetFiles(folder, "session_*.json");
                // Sort chronologically (names match session_yyyyMMdd_HHmmss.json format)
                Array.Sort(files);
                Array.Reverse(files);

                foreach (var file in files)
                {
                    string name = System.IO.Path.GetFileNameWithoutExtension(file);
                    if (name.Length == 23 && name.StartsWith("session_"))
                    {
                        string dateStr = name.Substring(8);
                        if (DateTime.TryParseExact(dateStr, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out DateTime dt))
                        {
                            LstHistory.Items.Add(new HistoryItem
                            {
                                DisplayText = dt.ToString("yyyy-MM-dd HH:mm:ss"),
                                FilePath = file
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Không thể tải danh sách lịch sử: {ex.Message}");
            }
            finally
            {
                LstHistory.SelectionChanged += LstHistory_SelectionChanged;
            }
        }

        private void LstHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRestoringHistory) return;
            if (LstHistory.SelectedItem is HistoryItem item)
            {
                _isRestoringHistory = true;
                try
                {
                    LoadSessionFromFile(item.FilePath);
                    AutoSaveSession(); // Save as the last active session
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi khôi phục lịch sử: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    _isRestoringHistory = false;
                }
            }
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Bạn có chắc chắn muốn xóa toàn bộ lịch sử các phiên làm việc trước đó?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    string folder = GetHistoryFolder();
                    if (Directory.Exists(folder))
                    {
                        var files = Directory.GetFiles(folder, "session_*.json");
                        foreach (var file in files)
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                    RefreshHistoryList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể xóa lịch sử: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }

    public class CapturedResult
    {
        public BitmapSource Image { get; set; }

        public CapturedResult(BitmapSource image)
        {
            Image = image;
        }
    }

    public class SessionData
    {
        public string? ImageBase64_1 { get; set; }
        public string? ImageBase64_2 { get; set; }
        
        public double RectX_1 { get; set; }
        public double RectY_1 { get; set; }
        public double RectW_1 { get; set; }
        public double RectH_1 { get; set; }

        public double RectX_2 { get; set; }
        public double RectY_2 { get; set; }
        public double RectW_2 { get; set; }
        public double RectH_2 { get; set; }

        public string? Text_1 { get; set; }
        public string? Text_2 { get; set; }
    }

    public class ScreenCaptureWindow : Window
    {
        private readonly BitmapSource _screenImage;
        private readonly System.Drawing.Rectangle _screenBounds;
        private readonly TaskCompletionSource<CapturedResult?> _tcs;

        private Point _startPoint;
        private System.Windows.Shapes.Rectangle? _selectionRect;
        private Canvas? _canvas;
        private Border? _darkMask;
        private bool _isDragging;

        public ScreenCaptureWindow(BitmapSource screenImage, System.Drawing.Rectangle screenBounds, TaskCompletionSource<CapturedResult?> tcs)
        {
            _screenImage = screenImage;
            _screenBounds = screenBounds;
            _tcs = tcs;

            // Window Setup
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = System.Windows.Media.Brushes.Transparent;
            this.Topmost = true;
            this.ShowInTaskbar = false;
            this.Cursor = System.Windows.Input.Cursors.Cross;

            // Translate physical pixel coordinates to WPF DIPs based on primary monitor DPI ratio
            double primaryWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
            double primaryHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
            
            double sysScaleX = 1.0;
            double sysScaleY = 1.0;
            
            if (primaryWidth > 0 && primaryHeight > 0)
            {
                sysScaleX = (double)(System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Width ?? 1920) / primaryWidth;
                sysScaleY = (double)(System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Height ?? 1080) / primaryHeight;
            }

            // Position and size the window to cover the screen bounds exactly (keeps WindowState.Normal)
            this.Left = screenBounds.X / sysScaleX;
            this.Top = screenBounds.Y / sysScaleY;
            this.Width = screenBounds.Width / sysScaleX;
            this.Height = screenBounds.Height / sysScaleY;

            InitializeCaptureUI();
        }

        private void InitializeCaptureUI()
        {
            var grid = new Grid();

            // Background image (screenshot of this monitor)
            var image = new Image
            {
                Source = _screenImage,
                Stretch = System.Windows.Media.Stretch.Fill
            };
            grid.Children.Add(image);

            // Dark overlay
            _darkMask = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 0, 0, 0))
            };
            grid.Children.Add(_darkMask);

            // Drawing canvas
            _canvas = new Canvas
            {
                Background = System.Windows.Media.Brushes.Transparent
            };
            _canvas.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
            _canvas.MouseMove += Canvas_MouseMove;
            _canvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;

            // Escape key cancels capture
            this.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    _tcs.TrySetResult(null);
                }
            };



            // Selection dashed rectangle
            _selectionRect = new System.Windows.Shapes.Rectangle
            {
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x63, 0x66, 0xF1)),
                StrokeThickness = 2,
                Fill = System.Windows.Media.Brushes.Transparent,
                Visibility = Visibility.Collapsed
            };
            _canvas.Children.Add(_selectionRect);
            grid.Children.Add(_canvas);

            this.Content = grid;
        }

        private void Canvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDragging = true;
            _startPoint = e.GetPosition(_canvas);
            _selectionRect!.Visibility = Visibility.Visible;
            Canvas.SetLeft(_selectionRect, _startPoint.X);
            Canvas.SetTop(_selectionRect, _startPoint.Y);
            _selectionRect.Width = 0;
            _selectionRect.Height = 0;
        }

        private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDragging) return;

            var currentPoint = e.GetPosition(_canvas);
            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(currentPoint.X - _startPoint.X);
            double h = Math.Abs(currentPoint.Y - _startPoint.Y);

            Canvas.SetLeft(_selectionRect, x);
            Canvas.SetTop(_selectionRect, y);
            _selectionRect!.Width = w;
            _selectionRect!.Height = h;

            // Clip mask to highlight target region
            var outerRect = new RectangleGeometry(new Rect(0, 0, this.ActualWidth, this.ActualHeight));
            var innerRect = new RectangleGeometry(new Rect(x, y, w, h));
            var geom = new CombinedGeometry(GeometryCombineMode.Exclude, outerRect, innerRect);
            _darkMask!.Clip = geom;
        }

        private void Canvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;

            var currentPoint = e.GetPosition(_canvas);
            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(currentPoint.X - _startPoint.X);
            double h = Math.Abs(currentPoint.Y - _startPoint.Y);

            if (w > 5 && h > 5)
            {
                // Translate selection DIPs to physical pixels of this monitor
                double scaleX = _screenImage.PixelWidth / this.ActualWidth;
                double scaleY = _screenImage.PixelHeight / this.ActualHeight;

                double cropX = x * scaleX;
                double cropY = y * scaleY;
                double cropW = w * scaleX;
                double cropH = h * scaleY;

                cropX = Math.Max(0, Math.Min(_screenImage.PixelWidth - 1, cropX));
                cropY = Math.Max(0, Math.Min(_screenImage.PixelHeight - 1, cropY));
                cropW = Math.Max(1, Math.Min(_screenImage.PixelWidth - cropX, cropW));
                cropH = Math.Max(1, Math.Min(_screenImage.PixelHeight - cropY, cropH));

                var cropped = new CroppedBitmap(_screenImage, new Int32Rect((int)cropX, (int)cropY, (int)cropW, (int)cropH));
                _tcs.TrySetResult(new CapturedResult(cropped));
            }
        }
    }
}