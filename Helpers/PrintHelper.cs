using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;   // ✅ REQUIRED FOR PrintDialog
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HomeoMahanagarLabelCleanV2.Helpers
{
    public static class PrintHelper
    {
        // Print a FrameworkElement as a label, arranging to 50x28 mm (DIPs)
        public static void Print(FrameworkElement element)
        {
            if (element == null) return;

            try
            {
                var dlg = new PrintDialog();
                if (dlg.ShowDialog() != true) return;

                // Arrange element to expected label size (50x28 mm -> DIPs)
                double dipW = PrintConstants.MmToDip(PrintConstants.LabelWidthMm);
                double dipH = PrintConstants.MmToDip(PrintConstants.LabelHeightMm);

                element.Measure(new Size(dipW, dipH));
                element.Arrange(new Rect(0, 0, dipW, dipH));

                // Render to bitmap (screen DPI) and place into FixedDocument so Windows preview works
                var rtb = new RenderTargetBitmap((int)Math.Ceiling(dipW), (int)Math.Ceiling(dipH), 96, 96, PixelFormats.Pbgra32);
                rtb.Render(element);

                var img = new System.Windows.Controls.Image { Source = rtb, Width = dipW, Height = dipH };

                var fixedPage = new FixedPage { Width = dipW, Height = dipH };
                FixedPage.SetLeft(img, 0);
                FixedPage.SetTop(img, 0);
                fixedPage.Children.Add(img);

                var pageContent = new PageContent();
                ((System.Windows.Markup.IAddChild)pageContent).AddChild(fixedPage);
                var fixedDoc = new FixedDocument();
                fixedDoc.Pages.Add(pageContent);

                // PrintDocument must be called on UI thread with a non-empty FixedDocument
                if (fixedDoc.Pages.Count == 0)
                {
                    throw new System.Exception("FixedDocument contains no pages.");
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    dlg.PrintDocument(fixedDoc.DocumentPaginator, "Label Print");
                });
            }
            catch (System.Exception ex)
            {
                try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(ex); } catch { }
                try { HomeoMahanagarLabelCleanV2.Services.DiagnosticsService.Capture(ex); } catch { }
                MessageBox.Show("Printing failed: " + ex.Message, "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
