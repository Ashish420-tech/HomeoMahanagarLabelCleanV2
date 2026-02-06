using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Printing;
using HomeoMahanagarLabelCleanV2.Services;
using HomeoMahanagarLabelCleanV2.Logging;

namespace HomeoMahanagarLabelCleanV2.Views
{
    public partial class PrintPreviewWindow : Window
    {
        private readonly BitmapImage _image;
        public PrintQueue SelectedPrintQueue { get; private set; }

        public PrintPreviewWindow(byte[] pngBytes)
        {
            try
            {
                InitializeComponent();
            }
            catch (System.Exception initEx)
            {
                try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(initEx); } catch { }
                MessageBox.Show("Failed to open print preview window: " + initEx.Message, "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // load image
            try
            {
                if (pngBytes == null || pngBytes.Length == 0)
                    throw new System.ArgumentException("PNG data is empty.");

                using var ms = new MemoryStream(pngBytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                _image = bmp;
                PreviewImage.Source = _image;

                // Force the preview image to render at the physical label size (in DIPs)
                try
                {
                    double dipW = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.MmToDip(HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelWidthMm);
                    double dipH = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.MmToDip(HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelHeightMm);
                    PreviewImage.Width = dipW;
                    PreviewImage.Height = dipH;
                    // make the surrounding border the same size (includes padding inside PrintLabelView)
                    PreviewBorder.Width = dipW;
                    PreviewBorder.Height = dipH;
                }
                catch { }
            }
            catch (System.Exception imgEx)
            {
                try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(imgEx); } catch { }
                MessageBox.Show("Failed to load preview image: " + imgEx.Message, "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // continue so user can still select printers (no preview image shown)
            }

            try
            {
                // Log image and control sizes for debugging preview rendering issues
                try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log($"PrintPreviewWindow: image pixels={_image.PixelWidth}x{_image.PixelHeight} dpi={_image.DpiX}x{_image.DpiY}"); } catch { }

                // Defer logging of ActualWidth/Height until layout completes
                this.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log($"PrintPreviewWindow: PreviewImage.ActualSize={PreviewImage.ActualWidth}x{PreviewImage.ActualHeight} WindowSize={this.Width}x{this.Height}"); } catch { }
                }));
            }
            catch { }

            LoadPrinters();
            // populate tuning boxes
            try
            {
                DpiBox.Text = (AppState.Storage.LabelPrinterDpi > 0 ? AppState.Storage.LabelPrinterDpi.ToString() : "203");
                PaddingBox.Text = (AppState.Storage.LabelPaddingDip > 0 ? AppState.Storage.LabelPaddingDip.ToString() : HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelPaddingDip.ToString());
            }
            catch { }
        }

        private void LoadPrinters()
        {
            try
            {
                var server = new LocalPrintServer();
                var queues = server.GetPrintQueues().OrderBy(p => p.Name).ToList();

                PrinterCombo.ItemsSource = queues;

                // prefer user-configured printer from AppState if present
                string preferred = null;
                try { preferred = AppState.Storage.LabelPrinterName; } catch { }

                // fallback: commonly used target name
                if (string.IsNullOrWhiteSpace(preferred))
                    preferred = "SNBC TVSE LP 46 NEO BPLE";

                if (!string.IsNullOrWhiteSpace(preferred))
                {
                    var match = queues.FirstOrDefault(q => string.Equals(q.Name, preferred, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        PrinterCombo.SelectedItem = match;
                        return;
                    }
                    }

                if (queues.Count > 0)
                    PrinterCombo.SelectedIndex = 0;
            }
            catch { }
        }

        private void ApplyTuning_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (int.TryParse(DpiBox.Text, out int dpi) && dpi > 0)
                    AppState.Storage.LabelPrinterDpi = dpi;

                if (double.TryParse(PaddingBox.Text, out double pd))
                    AppState.Storage.LabelPaddingDip = pd;

                AppState.Save();
                AppLogger.Log($"PrintPreviewWindow: saved tuning dpi={AppState.Storage.LabelPrinterDpi}, paddingDip={AppState.Storage.LabelPaddingDip}");
                MessageBox.Show("Printer tuning saved.", "Tuning", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                try { AppLogger.Log(ex); } catch { }
                MessageBox.Show("Failed to save tuning: " + ex.Message, "Tuning Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadPrinters();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            if (PrinterCombo.SelectedItem is PrintQueue pq)
            {
                SelectedPrintQueue = pq;
                try
                {
                    // persist preferred printer so next time it is auto-selected
                    AppState.Storage.LabelPrinterName = pq.Name;
                    AppState.Save();
                    AppLogger.Log($"Preferred printer saved: {pq.Name}");
                }
                catch { }

                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("Please select a printer.", "Print", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}