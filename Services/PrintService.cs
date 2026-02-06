using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HomeoMahanagarLabelCleanV2.Logging;
using HomeoMahanagarLabelCleanV2.Models;
using HomeoMahanagarLabelCleanV2.Views;
using HomeoMahanagarLabelCleanV2.Helpers;

namespace HomeoMahanagarLabelCleanV2.Services
{
    public class PrintService
    {
        // Build TSPL bytes and return used DPI/padding for diagnostics
        private (byte[]? bytes, int printerDpi, int paddingDots) BuildTsplBytes(IEnumerable<LabelCanvasItem> items, double widthMm, double heightMm)
        {
            if (items == null) return (null, 0, 0);
            var sb = new StringBuilder();
            // GOLDEN TSPL HEADER (per device recommendations)
            sb.AppendLine($"SIZE {widthMm} mm,{heightMm} mm");
            // Use centralized label padding (mm) for GAP so device and app are consistent.
            // Limit gap to 2 mm for these labels.
            sb.AppendLine($"GAP {HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelPaddingMm} mm,0 mm");
            sb.AppendLine("DIRECTION 1");
            sb.AppendLine("REFERENCE 0,0");
            sb.AppendLine("DENSITY 11");
            sb.AppendLine("SPEED 4");
            sb.AppendLine("CLS");

            int printerDpi = 203;
            double paddingDip = PrintConstants.LabelPaddingDip;
            try { printerDpi = Math.Max(72, AppState.Storage.LabelPrinterDpi); } catch { }
            try { paddingDip = AppState.Storage.LabelPaddingDip; } catch { }

            double dipToDots = printerDpi / 96.0;
            int labelWidthDots = (int)Math.Round(widthMm / 25.4 * printerDpi);
            int labelHeightDots = (int)Math.Round(heightMm / 25.4 * printerDpi);
            int paddingDots = (int)Math.Round(paddingDip * dipToDots);

            AppLogger.Log($"PrintService: BuildTsplBytes using printerDpi={printerDpi}, paddingDip={paddingDip}, paddingDots={paddingDots}");

            // FINAL: Build exactly 5 centered lines with single font "2" and fixed line height
            try
            {
                // Build composed lines using LabelTextComposer so that the first two logical lines
                // (medicine + potency) are auto-wrapped together into at most two lines. The rest
                // of the lines are taken as single logical lines from the inputs.

                // Collect raw input values in order
                var raw = items.Select(i => (i.Text ?? string.Empty).Trim()).ToList();
                try
                {
                    for (int ri = 0; ri < raw.Count; ri++)
                        AppLogger.Log($"PrintService: raw[{ri}]='{raw[ri]}'");
                }
                catch { }
                string medicineName = raw.ElementAtOrDefault(0) ?? string.Empty;
                string potency = raw.ElementAtOrDefault(1) ?? string.Empty;
                string dosage = raw.ElementAtOrDefault(2) ?? string.Empty;
                string schedule = raw.ElementAtOrDefault(3) ?? string.Empty;
                // support clinic and phone possibly being separate items
                var clinicPart = raw.ElementAtOrDefault(4) ?? string.Empty;
                var phonePart = raw.ElementAtOrDefault(5) ?? string.Empty;
                var clinicPhone = string.IsNullOrWhiteSpace(phonePart) ? clinicPart : (clinicPart + " " + phonePart).Trim();

                // If potency is blank, attempt to extract a trailing potency token from medicineName
                if (string.IsNullOrWhiteSpace(potency) && !string.IsNullOrWhiteSpace(medicineName))
                {
                    try
                    {
                        // match common potency notations at the end of the medicine name e.g. "200 CH", "30CH", "6X"
                        var m = Regex.Match(medicineName, @"\b(\d{1,3}\s*(?:CH|C|X|K|%|MG|ML))$", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            potency = m.Groups[1].Value.Trim();
                            medicineName = medicineName.Substring(0, m.Index).Trim();
                            AppLogger.Log($"PrintService: extracted potency='{potency}' from medicineName; new medicineName='{medicineName}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Log(ex);
                    }
                }

                // compute label inner width in DIPs for accurate wrapping
                double labelInnerDip = PrintConstants.MmToDip(widthMm) - (2.0 * paddingDip);
                if (labelInnerDip <= 0) labelInnerDip = 189; // fallback

                var composerForWrap = new LabelTextComposer(labelInnerDip, fontSize: 11.0);
                var composed = composerForWrap.Compose(medicineName, potency, dosage, schedule, clinicPhone);

                // Clean composed lines to avoid non-ASCII characters becoming '?' when
                // encoded to ASCII for the TSPL stream. Replace common punctuation
                // and drop any remaining non-ASCII characters.
                for (int i = 0; i < composed.Length; i++)
                {
                    if (string.IsNullOrEmpty(composed[i])) continue;
                    var s = composed[i];
                    s = s.Replace('·', ' ');
                    s = s.Replace('•', ' ');
                    s = s.Replace('–', '-');
                    s = s.Replace('—', '-');
                    s = s.Replace('…', '.');
                    var sbClean = new StringBuilder(s.Length);
                    foreach (var ch in s)
                    {
                        if (ch <= 127)
                            sbClean.Append(ch);
                        else
                            sbClean.Append(' ');
                    }
                    composed[i] = sbClean.ToString().Trim();
                }

                // Build linesInfo from composed results and use the same font sizes as preview (admin layout)
                var previewFontSizes = new double[] { 12.0, 11.0, 10.0, 10.0, 10.0 };
                var linesInfo = new List<(string Text, double FontSize, TextAlignment Alignment)>();
                for (int i = 0; i < 5; i++)
                {
                    var txt = i < composed.Length ? composed[i] ?? string.Empty : string.Empty;
                    // Per preview: center primary block; follow image preference for final line left aligned
                    var align = TextAlignment.Center;
                    if (i == 4) align = TextAlignment.Left;
                    linesInfo.Add((txt, previewFontSizes[Math.Min(i, previewFontSizes.Length - 1)], align));
                }

                // Auto-shrink lines 2..4 will be applied later after we compute Insets/LineHeight

                // NOTE: Do not emit border in TSPL output per user request. Preview keeps the rounded border.

                // Centering constants
                const string FontId = "2";

                // Use printer-tuned padding (in dots) so TSPL layout respects the configured inner gap
                int BorderInset = paddingDots;

                // Use the same LabelTextComposer measurement so centering on TSPL matches preview more closely.
                var composer = new LabelTextComposer(labelInnerDip);
                // Printer fonts differ from WPF metrics; apply a conservative width scale so
                // auto-shrink reduces text enough for the TSPL printer font.
                const double printerWidthScale = 1.02;

                // Always emit exactly 5 TEXT commands (one per logical line) to preserve vertical spacing
                // even when some logical lines are empty. Empty lines are emitted as a single space so
                // the printer still advances the Y position but prints no visible text.
                // compute available printable width in dots (respect border inset)
                int availableDots = Math.Max(10, labelWidthDots - (BorderInset * 2) - 8);

                // Auto-shrink lines 0..4 if they exceed printable width so they don't overflow the border.
                for (int i = 0; i <= 4; i++)
                {
                    var info = linesInfo[i];
                    double fs = info.FontSize;
                    double measuredDip = composer.MeasureTextWidth(info.Text, fs) * printerWidthScale;
                    int textDots = (int)Math.Round(measuredDip * dipToDots);
                    while (textDots > availableDots && fs > 8.0)
                    {
                        fs -= 0.5; // step down
                        measuredDip = composer.MeasureTextWidth(info.Text, fs);
                        textDots = (int)Math.Round(measuredDip * dipToDots);
                    }
                    linesInfo[i] = (info.Text, fs, info.Alignment);
                }

                // Compute per-line heights (in dots) from the final font sizes so vertical spacing adapts
                // to the font sizes after auto-shrink and prevents overlapping when lines change size.
                double lineLeading = 1.45; // multiplier for line height (leading) - increased to avoid overlap
                var lineHeights = new int[5];
                int blockHeight = 0;
                for (int i = 0; i < 5; i++)
                {
                    var fs = linesInfo[i].FontSize;
                    int h = (int)Math.Ceiling(fs * dipToDots * lineLeading);
                    if (h < 10) h = 10;
                    lineHeights[i] = h;
                    blockHeight += h;
                }

                // Compute vertical start Y to center the block within the label vertically.
                // Ensure we don't place text inside the border inset.
                int startY = Math.Max(BorderInset + 4, (labelHeightDots - blockHeight) / 2);

                int centerX = labelWidthDots / 2;
                int y = startY;
                int bottomLimit = labelHeightDots - BorderInset - 4;

                for (int idx = 0; idx < 5; idx++)
                {
                    var info = linesInfo[idx];
                    var line = info.Text ?? string.Empty;
                    var thisLineHeight = lineHeights[idx];
                    if ((y + thisLineHeight) <= bottomLimit)
                    {
                        var renderText = string.IsNullOrWhiteSpace(line) ? " " : line;

                        // Measure text width using the per-line font size and convert DIPs -> dots
                        double measuredDip = composer.MeasureTextWidth(renderText, info.FontSize) * printerWidthScale;
                        if (measuredDip <= 0) measuredDip = renderText.Length * 6.0;
                        int textWidth = (int)Math.Round(measuredDip * dipToDots);

                        int minX = BorderInset + 4;
                        int maxX = Math.Max(minX, labelWidthDots - BorderInset - textWidth - 4);

                        int x;
                        // Center all lines by default, except if alignment is left
                        if (info.Alignment == System.Windows.TextAlignment.Left)
                        {
                            x = minX;
                        }
                        else
                        {
                            x = centerX - (textWidth / 2);
                            x = Math.Max(minX, Math.Min(x, maxX));
                        }

                        var safe = renderText.Replace('"', '\'');
                        // Remove stray question marks on the last line (may come from
                        // non-ASCII -> '?' replacement). Remove all '?' chars on last line.
                        if (idx == 4)
                        {
                            safe = safe.TrimEnd();
                            while (safe.EndsWith("?"))
                                safe = safe.Substring(0, safe.Length - 1).TrimEnd();
                            if (safe.Contains("?"))
                                safe = safe.Replace("?", string.Empty);
                        }

                        int xToUse;
                        if (idx == 4)
                        {
                            int textWidthLast = textWidth;
                            int xLast = centerX - (textWidthLast / 2);
                            xLast = Math.Max(minX, Math.Min(xLast, maxX));
                            xToUse = xLast;
                        }
                        else
                        {
                            xToUse = x;
                        }

                        sb.AppendLine($"TEXT {xToUse},{y},\"{FontId}\",0,1,1,\"{safe}\"");
                        try { AppLogger.Log($"TSPL centered/left: '{safe}' at X={xToUse},Y={y}, align={info.Alignment}"); } catch { }
                    }
                    y += thisLineHeight;
                }

                sb.AppendLine("PRINT 1");
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex);
                sb.AppendLine("PRINT 1");
            }

            // save diagnostic TSPL file
            try
            {
                var diagDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeoMahanagarLabelCleanV2", "Diagnostics");
                Directory.CreateDirectory(diagDir);
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string tsplPath = Path.Combine(diagDir, $"tspl_{ts}.txt");
                var diagSb = new StringBuilder();
                diagSb.AppendLine($"printerDpi={printerDpi}, paddingDip={paddingDip}, paddingDots={paddingDots}");
                diagSb.AppendLine("---TSPL---");
                diagSb.Append(sb.ToString());
                File.WriteAllText(tsplPath, diagSb.ToString(), Encoding.ASCII);
                AppLogger.Log($"PrintService: TSPL diagnostics written to {tsplPath}");
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex);
            }
            return (Encoding.ASCII.GetBytes(sb.ToString()), printerDpi, paddingDots);
        }

        private void SavePrinterTuning(string printerName, int dpi, int paddingDots)
        {
            try
            {
                double paddingDip = paddingDots / (double)(dpi > 0 ? dpi : 203) * 96.0;
                AppState.Storage.LabelPrinterDpi = dpi;
                AppState.Storage.LabelPaddingDip = paddingDip;
                AppState.Save();
                AppLogger.Log($"PrintService: saved printer tuning dpi={dpi}, paddingDip={paddingDip}");
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex);
            }
        }

        public void PrintLabel(IEnumerable<LabelCanvasItem> items, PrintQueue selectedQueue = null, double widthMm = PrintConstants.LabelWidthMm, double heightMm = PrintConstants.LabelHeightMm)
        {
            AppLogger.Log("PrintService: PrintLabel START");
            if (items == null || !items.Any()) { AppLogger.Log("PrintService: no items, abort"); return; }
            var printer = selectedQueue ?? ResolvePreferredPrinter();
            if (printer == null) { AppLogger.Log("PrintService: no printer"); return; }
            AppLogger.Log($"PrintService: using printer {printer.Name}");

            // create view and render once
            var view = CreatePrintView(items, widthMm, heightMm);
            try
            {
                view.RenderItems(items);
                double dipW = PrintConstants.MmToDip(widthMm);
                double dipH = PrintConstants.MmToDip(heightMm);
                view.Measure(new Size(dipW, dipH));
                view.Arrange(new Rect(0, 0, dipW, dipH));
                view.UpdateLayout();
            }
            catch (Exception ex) { AppLogger.Log(ex); }

            string pdfPath = null;
            try { pdfPath = ExportPdf(view); AppLogger.Log($"PrintService: exported PDF {pdfPath}"); } catch (Exception ex) { AppLogger.Log(ex); }

            if (IsTvsPrinter(printer))
            {
                AppLogger.Log("PrintService: trying TSPL");
                try
                {
                    var (tsplBytes, apiPrinterDpi, apiPaddingDots) = BuildTsplBytes(items, widthMm, heightMm);
                    try
                    {
                        if (tsplBytes != null && tsplBytes.Length > 0 && RawPrinterHelper.SendBytesToPrinter(printer.Name, tsplBytes))
                        {
                            AppLogger.Log($"PrintService: TSPL RAW SUCCESS ({tsplBytes.Length} bytes)");
                            try { SavePrinterTuning(printer.Name, apiPrinterDpi, apiPaddingDots); } catch { }
                            return;
                        }
                        else
                        {
                            AppLogger.Log("PrintService: TSPL RAW failed or returned false");
                        }
                    }
                    catch (Exception rex) { AppLogger.Log(rex); }

                    if (TrySendTsplToPrinter(printer, items, widthMm, heightMm)) { AppLogger.Log("PrintService: TSPL AddJob SUCCESS"); return; }
                }
                catch (Exception ex) { AppLogger.Log(ex); }
            }

            AppLogger.Log("PrintService: trying PDF spool");
            try { if (!string.IsNullOrWhiteSpace(pdfPath) && TrySpoolPdfToPrinter(printer, pdfPath)) { AppLogger.Log("PrintService: PDF spool SUCCESS"); return; } }
            catch (Exception ex) { AppLogger.Log(ex); }

            AppLogger.Log("PrintService: fallback PrintVisual");
            try { PrintViaPrintVisual(view, printer, widthMm, heightMm); }
            catch (Exception ex) { AppLogger.Log(ex); throw; }
        }

        // Print using an existing visual (on-screen preview) to ensure exact parity
        public void PrintLabel(FrameworkElement view, IEnumerable<LabelCanvasItem> items, PrintQueue selectedQueue = null, double widthMm = PrintConstants.LabelWidthMm, double heightMm = PrintConstants.LabelHeightMm)
        {
            AppLogger.Log("PrintService: PrintLabel START (using provided visual)");
            if (items == null || !items.Any()) { AppLogger.Log("PrintService: no items, abort"); return; }
            var printer = selectedQueue ?? ResolvePreferredPrinter();
            if (printer == null) { AppLogger.Log("PrintService: no printer"); return; }
            AppLogger.Log($"PrintService: using printer {printer.Name}");

            try
            {
                double dipW = PrintConstants.MmToDip(widthMm);
                double dipH = PrintConstants.MmToDip(heightMm);
                view.Measure(new Size(dipW, dipH));
                view.Arrange(new Rect(0, 0, dipW, dipH));
                view.UpdateLayout();
            }
            catch (Exception ex) { AppLogger.Log(ex); }

            string pdfPath = null;
            try { pdfPath = ExportPdf(view); AppLogger.Log($"PrintService: exported PDF {pdfPath}"); } catch (Exception ex) { AppLogger.Log(ex); }

            if (IsTvsPrinter(printer))
            {
                AppLogger.Log("PrintService: trying TSPL");
                try
                {
                    var (tsplBytes, apiPrinterDpi, apiPaddingDots) = BuildTsplBytes(items, widthMm, heightMm);
                    try
                    {
                        if (tsplBytes != null && tsplBytes.Length > 0 && RawPrinterHelper.SendBytesToPrinter(printer.Name, tsplBytes))
                        {
                            AppLogger.Log($"PrintService: TSPL RAW SUCCESS ({tsplBytes.Length} bytes)");
                            try { SavePrinterTuning(printer.Name, apiPrinterDpi, apiPaddingDots); } catch { }
                            return;
                        }
                    }
                    catch (Exception rex) { AppLogger.Log(rex); }

                    if (TrySendTsplToPrinter(printer, items, widthMm, heightMm)) { AppLogger.Log("PrintService: TSPL AddJob SUCCESS"); return; }
                }
                catch (Exception ex) { AppLogger.Log(ex); }
            }

            AppLogger.Log("PrintService: trying PDF spool");
            try { if (!string.IsNullOrWhiteSpace(pdfPath) && TrySpoolPdfToPrinter(printer, pdfPath)) { AppLogger.Log("PrintService: PDF spool SUCCESS"); return; } }
            catch (Exception ex) { AppLogger.Log(ex); }

            AppLogger.Log("PrintService: fallback PrintVisual");
            try { PrintViaPrintVisual(view, printer, widthMm, heightMm); }
            catch (Exception ex) { AppLogger.Log(ex); throw; }
        }

        private PrintLabelView CreatePrintView(IEnumerable<LabelCanvasItem> items, double widthMm, double heightMm)
        {
            var view = new PrintLabelView();
            double dipW = PrintConstants.MmToDip(widthMm);
            double dipH = PrintConstants.MmToDip(heightMm);
            view.Width = dipW; view.Height = dipH;
            return view;
        }

        private string ExportPdf(FrameworkElement view)
        {
            var diagDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeoMahanagarLabelCleanV2", "Diagnostics");
            Directory.CreateDirectory(diagDir);
            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(diagDir, $"label_{ts}.pdf");
            Helpers.PdfHelper.ExportLabelToPdf(view, path);
            return path;
        }

        private PrintQueue ResolvePreferredPrinter()
        {
            try
            {
                string preferred = null;
                try { preferred = AppState.Storage.LabelPrinterName; } catch { }
                if (string.IsNullOrWhiteSpace(preferred)) return null;

                var server = new LocalPrintServer();
                var queues = server.GetPrintQueues();
                return queues.FirstOrDefault(q => string.Equals(q.Name, preferred, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex);
                return null;
            }
        }

        private bool IsTvsPrinter(PrintQueue pq)
        {
            if (pq == null) return false;
            try
            {
                var name = (pq.Name ?? string.Empty).ToUpperInvariant();
                return name.Contains("TVS") || name.Contains("SNBC");
            }
            catch { return false; }
        }

        private bool TrySendTsplToPrinter(PrintQueue pq, IEnumerable<LabelCanvasItem> items, double widthMm, double heightMm)
        {
            if (pq == null || items == null)
            {
                AppLogger.Log("PrintService: TrySendTsplToPrinter called with null pq or items");
                return false;
            }
            // Prefer building TSPL using the centralized builder so layout logic (wrapping/centering)
            // is shared with the preview. BuildTsplBytes composes exact 5 logical lines and emits
            // centered TEXT commands. Use it to produce the bytes and then send them via AddJob.
            byte[] bytes = null;
            try
            {
                var (tsplBytes, apiPrinterDpi, apiPaddingDots) = BuildTsplBytes(items, widthMm, heightMm);
                bytes = tsplBytes;

                if (bytes == null || bytes.Length == 0)
                {
                    AppLogger.Log("PrintService: TrySendTsplToPrinter BuildTsplBytes returned no bytes");
                    return false;
                }

                var job = pq.AddJob("TSPL RAW");
                using (var stream = job.JobStream)
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }

                AppLogger.Log($"PrintService: TSPL SUCCESS ({bytes.Length} bytes)");
                try { SavePrinterTuning(pq.Name, apiPrinterDpi, apiPaddingDots); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex);
                AppLogger.Log("PrintService: TSPL AddJob/write failed, attempting RawPrinterHelper fallback");

                try
                {
                    if (bytes != null && RawPrinterHelper.SendBytesToPrinter(pq.Name, bytes))
                    {
                        AppLogger.Log($"PrintService: TSPL RawPrinterHelper SUCCESS ({bytes.Length} bytes)");
                        return true;
                    }
                    else
                    {
                        AppLogger.Log("PrintService: RawPrinterHelper failed to send bytes");
                    }
                }
                catch (Exception rex)
                {
                    AppLogger.Log(rex);
                }

                AppLogger.Log("PrintService: TSPL FAILED");
                return false;
            }
        }

        // Emit a minimal TSPL TEXT test to the given printer (or preferred printer if null).
        // Returns true if the raw bytes were sent successfully.
        public bool EmitSimpleTsplTest(PrintQueue selectedQueue = null)
        {
            try
            {
                var pq = selectedQueue ?? ResolvePreferredPrinter();
                if (pq == null)
                {
                    AppLogger.Log("PrintService: EmitSimpleTsplTest no printer available");
                    return false;
                }

                var sb = new StringBuilder();
                sb.AppendLine("CLS");
                sb.AppendLine("TEXT 50,50,\"0\",0,1,1,\"TEST\"");
                sb.AppendLine("PRINT 1");

                var bytes = Encoding.ASCII.GetBytes(sb.ToString());
                AppLogger.Log($"PrintService: EmitSimpleTsplTest sending {bytes.Length} bytes to {pq.Name}");

                if (RawPrinterHelper.SendBytesToPrinter(pq.Name, bytes))
                {
                    AppLogger.Log("PrintService: EmitSimpleTsplTest RAW SUCCESS");
                    return true;
                }

                // Fallback: try AddJob stream write
                try
                {
                    var job = pq.AddJob("TSPL Test");
                    using (var stream = job.JobStream)
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush();
                    }
                    AppLogger.Log("PrintService: EmitSimpleTsplTest AddJob SUCCESS");
                    return true;
                }
                catch (Exception ex2)
                {
                    AppLogger.Log(ex2);
                }

                AppLogger.Log("PrintService: EmitSimpleTsplTest failed to send bytes");
                return false;
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex);
                return false;
            }
        }

        private bool TrySpoolPdfToPrinter(PrintQueue pq, string pdfPath)
        {
            try
            {
                if (pq == null || string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath)) return false;
                try
                {
                    var job = pq.AddJob("Label Print (PDF)", pdfPath, false);
                    AppLogger.Log($"PrintService: PDF spooled to {pq.Name}, job id={job?.JobIdentifier}");
                    return true;
                }
                catch
                {
                    var j = pq.AddJob("Label Print (PDF)");
                    using (var fs = new FileStream(pdfPath, FileMode.Open, FileAccess.Read))
                    using (var js = j.JobStream)
                    {
                        fs.CopyTo(js);
                    }
                    AppLogger.Log($"PrintService: PDF streamed to {pq.Name}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex);
                return false;
            }
        }

        private void PrintViaPrintVisual(FrameworkElement view, PrintQueue pq, double widthMm, double heightMm)
        {
            if (pq == null) throw new ArgumentNullException(nameof(pq));
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => PrintViaPrintVisual(view, pq, widthMm, heightMm));
                return;
            }

            double dipW = PrintConstants.MmToDip(widthMm);
            double dipH = PrintConstants.MmToDip(heightMm);

            var dlg = new PrintDialog();
            dlg.PrintQueue = pq;
            try
            {
                var pt = dlg.PrintTicket ?? new System.Printing.PrintTicket();
                pt.PageMediaSize = new System.Printing.PageMediaSize(widthMm, heightMm);
                dlg.PrintTicket = pt;
            }
            catch { }

            view.Measure(new System.Windows.Size(dipW, dipH));
            view.Arrange(new Rect(0, 0, dipW, dipH));

            try
            {
                // Use a FixedDocument/FixedPage to preserve exact DPI and layout when printing.
                // PrintVisual can cause the print system to scale the visual to fit printable area,
                // producing mismatches with the on-screen preview. Wrapping the view in a FixedPage
                // and printing the document paginator preserves the element size.
                var fixedDoc = new FixedDocument();
                fixedDoc.DocumentPaginator.PageSize = new Size(dipW, dipH);

                var fixedPage = new FixedPage();
                fixedPage.Width = dipW;
                fixedPage.Height = dipH;

                // Ensure the view has the expected size and layout
                view.Measure(new Size(dipW, dipH));
                view.Arrange(new Rect(0, 0, dipW, dipH));
                view.UpdateLayout();

                // Render the view to a bitmap and add an Image to the fixed page.
                // This avoids removing the view from its on-screen parent while preserving layout.
                try
                {
                    // Use tuned printer DPI when available so the rendered bitmap matches printer sizing.
                    int printerDpi = 203;
                    try { printerDpi = Math.Max(72, AppState.Storage.LabelPrinterDpi); } catch { }

                    // Render the view to a PNG at the printer DPI and embed that PNG into the FixedPage.
                    // This uses the same rasterization path as PDF export to ensure identical output.
                    byte[] pngBytes = null;
                    try
                    {
                    // compute safe edge padding in pixels based on desired physical inner gap to avoid rounded-corner/border clipping
                    int edgePad = Math.Max(1, (int)System.Math.Ceiling(HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelPaddingMm / 25.4 * printerDpi));
                    pngBytes = HomeoMahanagarLabelCleanV2.Helpers.PdfHelper.RenderElementToPngBytes(view, widthMm, heightMm, printerDpi, edgePad);
                    }
                    catch (System.Exception ex)
                    {
                        AppLogger.Log(ex);
                    }

                    System.Windows.Media.ImageSource src = null;
                    if (pngBytes != null && pngBytes.Length > 0)
                    {
                        using var ms = new System.IO.MemoryStream(pngBytes);
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                        src = bmp;
                    }

                    var img = new System.Windows.Controls.Image
                    {
                        Source = src,
                        Width = dipW,
                        Height = dipH
                    };
                    FixedPage.SetLeft(img, 0);
                    FixedPage.SetTop(img, 0);
                    fixedPage.Children.Add(img);
                }
                catch (Exception ex)
                {
                    AppLogger.Log(ex);
                    // Fallback: add the view directly if rendering fails
                    try { fixedPage.Children.Add(view); } catch { }
                }

                var pageContent = new PageContent();
                ((System.Windows.Markup.IAddChild)pageContent).AddChild(fixedPage);
                fixedDoc.Pages.Add(pageContent);

                dlg.PrintDocument(fixedDoc.DocumentPaginator, "Label Print");
                AppLogger.Log($"PrintService: PrintDocument invoked on {pq.Name}");
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex);
                throw;
            }
        }
    }
}
