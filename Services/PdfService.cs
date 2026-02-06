using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.IO;
using HomeoMahanagarLabelCleanV2.Models;

namespace HomeoMahanagarLabelCleanV2.Services
{
    public class PdfService
    {
        public string GenerateLabelPdf(LabelModel label)
        {
            try
            {
                PdfDocument document = new PdfDocument();
                PdfPage page = document.AddPage();

                // Label size (adjust if needed)
                page.Width = XUnit.FromMillimeter(60);
                page.Height = XUnit.FromMillimeter(35);

                XGraphics gfx = XGraphics.FromPdfPage(page);

                // Fonts
                XFont headerFont = new XFont("Arial", 9, XFontStyleEx.Bold);
                XFont textFont = new XFont("Arial", 8, XFontStyleEx.Regular);
                XFont footerFont = new XFont("Arial", 7, XFontStyleEx.Regular);


                // Border
                gfx.DrawRectangle(
                    XPens.Black,
                    2, 2,
                    page.Width - 4,
                    page.Height - 4
                );

                double centerX = page.Width / 2;
                double y = 8;

                // Top header
                gfx.DrawString(
                    label.MedicineName?.ToUpper() ?? string.Empty,
                    headerFont,
                    XBrushes.Black,
                    new XRect(0, y, page.Width, 10),
                    XStringFormats.TopCenter
                );

                y += 8;

                gfx.DrawString(
                    label.LatinName?.ToUpper() ?? string.Empty,
                    textFont,
                    XBrushes.Black,
                    new XRect(0, y, page.Width, 10),
                    XStringFormats.TopCenter
                );

                y += 7;

                gfx.DrawString(
                    "10 GLOB MORNING/NIGHT",
                    textFont,
                    XBrushes.Black,
                    new XRect(0, y, page.Width, 10),
                    XStringFormats.TopCenter
                );

                y += 10;

                // Footer
                gfx.DrawString(
                    "HOMEO MAHANAGAR",
                    footerFont,
                    XBrushes.Black,
                    new XRect(0, y, page.Width, 10),
                    XStringFormats.TopCenter
                );

                y += 6;

                gfx.DrawString(
                    "NEWTOWN (M) 9007728468",
                    footerFont,
                    XBrushes.Black,
                    new XRect(0, y, page.Width, 10),
                    XStringFormats.TopCenter
                );

                // Save
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "HomeoLabels");

                Directory.CreateDirectory(folder);

                string filePath = Path.Combine(
                    folder,
                    $"Label_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

                document.Save(filePath);
                document.Close();

                return filePath;
            }
            catch (System.Exception ex)
            {
                try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(ex); } catch { }
                try { Services.DiagnosticsService.Capture(ex); } catch { }
                throw;
            }
        }
    }
}
