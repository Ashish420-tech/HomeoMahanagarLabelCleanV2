using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using HomeoMahanagarLabelCleanV2.Models;

namespace HomeoMahanagarLabelCleanV2.Services
{
    public static class DiagnosticsService
    {
        private static readonly string DiagnosticsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HomeoMahanagarLabelCleanV2", "Diagnostics");

        public static void Capture(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(DiagnosticsFolder);
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string jsonPath = Path.Combine(DiagnosticsFolder, $"diagnostic_{ts}.json");
                string pngPath = Path.Combine(DiagnosticsFolder, $"screenshot_{ts}.png");

                var info = new DiagnosticInfo
                {
                    Timestamp = DateTime.Now,
                    ExceptionMessage = ex?.Message,
                    ExceptionType = ex?.GetType().FullName,
                    StackTrace = ex?.StackTrace
                };

                // include AppState if available
                try
                {
                    info.AppStorage = AppState.Storage;
                }
                catch { }

                // try to include selected medicine from main window
                try
                {
                    var main = Application.Current?.MainWindow;
                    if (main?.DataContext is ViewModels.LabelViewModel lvm)
                    {
                        info.SelectedMedicine = lvm.SelectedMedicine;
                    }
                }
                catch { }

                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(jsonPath, JsonSerializer.Serialize(info, opts));

                // screenshot main window
                try
                {
                    var main = Application.Current?.MainWindow;
                    if (main != null)
                    {
                        int w = (int)Math.Max(1, main.ActualWidth);
                        int h = (int)Math.Max(1, main.ActualHeight);
                        var rtb = new RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        rtb.Render(main);
                        var enc = new PngBitmapEncoder();
                        enc.Frames.Add(BitmapFrame.Create(rtb));
                        using var fs = File.OpenWrite(pngPath);
                        enc.Save(fs);
                    }
                }
                catch { }

                // also write a short log entry
                HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log($"Diagnostics captured: {jsonPath}");
            }
            catch
            {
                // swallow
            }
        }

        private class DiagnosticInfo
        {
            public DateTime Timestamp { get; set; }
            public string ExceptionMessage { get; set; }
            public string ExceptionType { get; set; }
            public string StackTrace { get; set; }
            public AppStorage AppStorage { get; set; }
            public Models.MedicineLabel SelectedMedicine { get; set; }
        }
    }
}
