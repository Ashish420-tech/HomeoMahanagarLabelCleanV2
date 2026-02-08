using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
#if DEBUG
using System.Diagnostics;
#endif

namespace HomeoMahanagarLabelCleanV2.Helpers
{
    /// <summary>
    /// PDF helper utilities used to export a WPF visual to a PDF page sized to the
    /// physical label. The PDF is generated from a rendered WPF visual (rasterized
    /// to PNG) to guarantee pixel-perfect parity with the on-screen Preview and
    /// the bitmap used for Print. This avoids typographic differences between
    /// WPF and printer device fonts and ensures consistent DPI/layout across
    /// Preview, PDF and Print paths.
    /// </summary>
    public static class PdfHelper
    {
        // physical label size in millimeters
        // use centralized constants so Export and Print use same sizes
        private const double LABEL_MM_WIDTH = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelWidthMm;
        private const double LABEL_MM_HEIGHT = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelHeightMm;
        private const double MARGIN_MM = 0.0; // margin in mm (use 0 for edge-to-edge)

        // export DPI for rasterization
        private const double EXPORT_DPI = 300.0;

        /// <summary>
        /// Export a WPF <see cref="FrameworkElement"/> to a PDF file sized to the
        /// physical label dimensions declared in <see cref="PrintConstants"/>.
        /// </summary>
        /// <remarks>
        /// Implementation notes:
        /// - We prefer rasterizing the WPF visual to a PNG and embedding it in the PDF
        ///   because printer device fonts and WPF font metrics rarely match exactly.
        ///   Raster embedding guarantees the exported PDF visually matches the WPF
        ///   preview and the image used for raster printing.
        /// - DPI: A raster DPI is chosen by <see cref="EXPORT_DPI"/> (default 300).
        ///   The DPI must be sufficient for label quality while allowing predictable
        ///   conversion from DIPs -> pixels -> printer dots. When printing we use a
        ///   printer-tuned DPI (e.g., 203) for rasterization so the bitmap maps to
        ///   printer dots deterministically.
        /// - DIPs -> points: PDF drawing APIs expect points (1 point = 1/72 inch).
        ///   The conversion used is DIP * 72 / 96. The helper uses centralized
        ///   conversions in <see cref="PrintConstants"/> to avoid rounding drift.
        /// - Page size: the PDF page is created using the physical label dimensions
        ///   (mm) so the embedded image maps 1:1 to the real world label size.
        /// - Border hiding/edge padding: before rasterizing we temporarily hide any
        ///   decorative Borders (rounded corners) in the visual tree so the PNG
        ///   contains label content only. An edge padding parameter prevents clipping
        ///   of rounded corners during render; saved border properties are restored
        ///   after rendering.
        /// </remarks>
        /// <param name="view">The WPF element to export (should be arranged/measured to label size).</param>
        /// <param name="path">Destination PDF path to write.</param>
        /// <param name="exportVector">If true a vector/text fallback may be used when rasterization fails.</param>
        /// <param name="fontFamily">Font family used by the vector fallback.</param>
        /// <param name="isBold">Whether to use bold in the vector fallback (ignored for raster path).</param>
        /// <param name="fontSize">Font size used by the vector fallback.</param>
        /// <param name="lineSpacing">Line spacing used by the vector fallback.</param>
        public static void ExportLabelToPdf(FrameworkElement view, string path, bool exportVector = true, string fontFamily = "Segoe UI", bool isBold = false, double fontSize = 9.0, double lineSpacing = 0.0)
        {
            if (view == null) throw new System.ArgumentNullException(nameof(view));
            if (string.IsNullOrWhiteSpace(path)) throw new System.ArgumentNullException(nameof(path));
#if DEBUG
            var swTotal = Stopwatch.StartNew();
#endif

            try
            {
                double marginPoints = PdfSharp.Drawing.XUnit.FromMillimeter(MARGIN_MM).Point;

                // First attempt: render the WPF element to a PNG and embed it in the PDF.
                try
                {
                    // compute edge padding in pixels based on desired physical gap (LabelPaddingMm)
                    // Temporarily remove any visible Borders (rounded corners) in the visual tree so
                    // exported PNG/PDF contains no decorative rounded border. We save original
                    // properties and restore them after rendering. This approach ensures the
                    // embedded image contains only label content and not UI chrome.
                    var modifiedBorders = new List<(Border border, Thickness thickness, Brush brush, CornerRadius corner)>();

                    // Recursively traverse the visual tree to find Borders. Hiding all Borders
                    // is safe because we restore them immediately after rendering; the goal
                    // is to avoid capturing decoration that would appear in the PDF.
                    void CollectAndHideBorders(System.Windows.DependencyObject parent)
                    {
                        if (parent == null) return;
                        int cc = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
                        for (int i = 0; i < cc; i++)
                        {
                            var ch = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                            if (ch is Border b)
                            {
                                try
                                {
                                    modifiedBorders.Add((b, b.BorderThickness, b.BorderBrush, b.CornerRadius));
                                    b.BorderThickness = new Thickness(0);
                                    b.BorderBrush = System.Windows.Media.Brushes.Transparent;
                                    b.CornerRadius = new CornerRadius(0);
                                }

                                catch { }
                            }
                            CollectAndHideBorders(ch);
                        }
                    }

                    try
                    {
                        CollectAndHideBorders(view);
                        try { view.UpdateLayout(); } catch { }
                    }
                    catch { }

                    // Convert the physical label padding (mm) into pixels at the chosen export DPI.
                    // edgePad prevents clipping of rounded corners during rasterization; for
                    // borderless exports this can be zero, but a small positive value avoids
                    // accidental clipping when visuals have stroke widths.
                    int edgePad = (int)System.Math.Ceiling(HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelPaddingMm / 25.4 * EXPORT_DPI);
                    var png = RenderElementToPngBytes(view, LABEL_MM_WIDTH, LABEL_MM_HEIGHT, EXPORT_DPI, edgePad);

                    // restore original Border properties so the UI is not mutated by export.
                    try
                    {
                        foreach (var t in modifiedBorders)
                        {
                            try
                            {
                                t.border.BorderThickness = t.thickness;
                                t.border.BorderBrush = t.brush;
                                t.border.CornerRadius = t.corner;
                            }
                            catch { }
                        }
                        try { view.UpdateLayout(); } catch { }
                    }
                    catch { }
                    using var ms = new MemoryStream(png);

                    var doc2 = new PdfDocument();
                    var page2 = doc2.AddPage();
                    page2.Width = PdfSharp.Drawing.XUnit.FromMillimeter(LABEL_MM_WIDTH);
                    page2.Height = PdfSharp.Drawing.XUnit.FromMillimeter(LABEL_MM_HEIGHT);

                    var gfx2 = XGraphics.FromPdfPage(page2);

                    // white background
                    gfx2.DrawRectangle(XBrushes.White, 0, 0, page2.Width.Point, page2.Height.Point);

                    using var img = XImage.FromStream(ms);
                    // Draw the embedded raster at the full page area (minus marginPoints).
                    // Because the page was created using the physical label size the PNG
                    // (rendered using matching DIPs->pixels conversion) will map 1:1 to
                    // the printed area when the PDF is spooled to a printer.
                    double drawW = page2.Width.Point - marginPoints * 2.0;
                    double drawH = page2.Height.Point - marginPoints * 2.0;
                    gfx2.DrawImage(img, marginPoints, marginPoints, drawW, drawH);

                    // no additional border drawn here — the embedded PNG already contains the preview border
                    doc2.Save(path);
#if DEBUG
                    swTotal.Stop();
                    try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log($"[PERF] PdfHelper.ExportLabelToPdf (raster): {swTotal.ElapsedMilliseconds}ms"); } catch { }
#endif
                    return;
                }
                catch (System.Exception imgEx)
                {
                    try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(imgEx); } catch { }
                    try { HomeoMahanagarLabelCleanV2.Services.DiagnosticsService.Capture(imgEx); } catch { }
                    // continue to vector/text fallback
                }

                // Fallback: draw text using vector drawing from AdminLayout.
                // Note: vector fallback is retained for robustness but may not match
                // the WPF raster output exactly due to font metric differences. The
                // raster path above is preferred to guarantee visual parity.
                try
                {
                    var doc3 = new PdfDocument();
                    var page3 = doc3.AddPage();
                    page3.Width = PdfSharp.Drawing.XUnit.FromMillimeter(LABEL_MM_WIDTH);
                    page3.Height = PdfSharp.Drawing.XUnit.FromMillimeter(LABEL_MM_HEIGHT);

                    var gfx3 = XGraphics.FromPdfPage(page3);
                    gfx3.DrawRectangle(XBrushes.White, 0, 0, page3.Width.Point, page3.Height.Point);

                    // use the same marginPoints as the raster path so PDF layout matches PNG output
                    // DIP -> points multiplier used for converting stored DIPs into PDF points.
                    const double dipToPointLocal2 = 72.0 / 96.0;
                    double insetPts2 = marginPoints; // outer gap in points
                    double borderThicknessPts2 = 1.5 * dipToPointLocal2; // reasonable stroke width
                    double radius2 = 10.0 * dipToPointLocal2;
                    // Do not draw a border for exported PDFs — keep PDF content borderless per request.
                    // var rect2 = new XRect(insetPts2 / 2.0, insetPts2 / 2.0, page3.Width.Point - insetPts2, page3.Height.Point - insetPts2);
                    // gfx3.DrawRoundedRectangle(new XPen(XColors.Black, borderThicknessPts2), rect2, new XSize(radius2, radius2));

                    // draw text lines
                    var layout = HomeoMahanagarLabelCleanV2.Services.AppState.Storage.AdminLayout;
                    if (layout != null)
                    {
                        var tf = new PdfSharp.Drawing.Layout.XTextFormatter(gfx3);
                        foreach (var item in layout)
                        {
                            string text = item.Text ?? string.Empty;
                            double fontPts = (item.FontSize > 0 ? item.FontSize : fontSize) * dipToPointLocal2;
                            var xfont = new XFont(fontFamily ?? "Segoe UI", Math.Max(6, fontPts));

                            // convert item coordinates from DIPs to points and add inset/padding
                            double xPts = insetPts2 / 2.0 + item.X * dipToPointLocal2;
                            double yPts = insetPts2 / 2.0 + item.Y * dipToPointLocal2;
                            double layoutWidth = page3.Width.Point - insetPts2;
                            var layoutRect = new XRect(xPts, yPts, layoutWidth, fontPts * 3);

                            XStringFormat xf = XStringFormats.Center;
                            if (item.Alignment == System.Windows.TextAlignment.Left) xf = XStringFormats.TopLeft;
                            else if (item.Alignment == System.Windows.TextAlignment.Right) xf = XStringFormats.TopRight;

                            tf.DrawString(text, xfont, XBrushes.Black, layoutRect, xf);
                        }
                    }

                    doc3.Save(path);
#if DEBUG
                    swTotal.Stop();
                    try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log($"[PERF] PdfHelper.ExportLabelToPdf (vector): {swTotal.ElapsedMilliseconds}ms"); } catch { }
#endif
                    return;
                }
                catch (System.Exception vecEx)
                {
                    try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(vecEx); } catch { }
                    try { HomeoMahanagarLabelCleanV2.Services.DiagnosticsService.Capture(vecEx); } catch { }
                    throw; // rethrow final failure
                    }
                }
            catch (System.Exception ex)
            {
                try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(ex); } catch { }
                try { HomeoMahanagarLabelCleanV2.Services.DiagnosticsService.Capture(ex); } catch { }
                throw; // rethrow so caller shows message
            }
        }

        // Render a FrameworkElement to PNG bytes at given physical size (millimeters) and DPI.
        public static byte[] RenderElementToPngBytes(FrameworkElement view, double widthMm = 50.0, double heightMm = 30.0, double dpi = 300.0, int edgePaddingPixels = 2)
        {
            if (view == null) throw new System.ArgumentNullException(nameof(view));
#if DEBUG
            var swRender = Stopwatch.StartNew();
#endif

            // compute pixel size at requested DPI, include optional edge padding in pixels
            int contentPxW = (int)System.Math.Round(widthMm / 25.4 * dpi);
            int contentPxH = (int)System.Math.Round(heightMm / 25.4 * dpi);
            int pixelWidth = Math.Max(1, contentPxW + edgePaddingPixels * 2);
            int pixelHeight = Math.Max(1, contentPxH + edgePaddingPixels * 2);

            // compute WPF device-independent size (DIPs = 1/96 inch) for the content area
            double contentDipW = widthMm / 25.4 * 96.0;
            double contentDipH = heightMm / 25.4 * 96.0;

            // padding in DIPs corresponding to edgePaddingPixels at the given DPI
            double padDip = edgePaddingPixels / (double)dpi * 96.0;

            // arrange the view at the content DIP size so layout is up-to-date
            view.Measure(new Size(contentDipW, contentDipH));
            view.Arrange(new Rect(0, 0, contentDipW, contentDipH));
            view.UpdateLayout();

            // Render the visual into a DrawingVisual using a VisualBrush.
            // Draw the content at an offset (padDip,padDip) inside a larger canvas so border isn't clipped.
            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                // white background in DIPs for the full padded area
                dc.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, contentDipW + padDip * 2.0, contentDipH + padDip * 2.0));

                var visualBrush = new System.Windows.Media.VisualBrush(view)
                {
                    Stretch = System.Windows.Media.Stretch.None,
                    AlignmentX = System.Windows.Media.AlignmentX.Left,
                    AlignmentY = System.Windows.Media.AlignmentY.Top
                };

                // draw the view at the inset position
                dc.DrawRectangle(visualBrush, null, new Rect(padDip, padDip, contentDipW, contentDipH));
            }

            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);

            var pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(rtb));

            using var outMs = new MemoryStream();
            pngEncoder.Save(outMs);
            var result = outMs.ToArray();
#if DEBUG
            swRender.Stop();
            try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log($"[PERF] PdfHelper.RenderElementToPngBytes: {swRender.ElapsedMilliseconds}ms (DPI={dpi}, size={result.Length} bytes)"); } catch { }
#endif
            return result;
        }
    }
}
