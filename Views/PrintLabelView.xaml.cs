using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HomeoMahanagarLabelCleanV2.Models;
#if DEBUG
using System.Diagnostics;
#endif

namespace HomeoMahanagarLabelCleanV2.Views
{
    public partial class PrintLabelView : UserControl
    {
        /// <summary>
        /// Dedicated visual used for label rendering before export/print.
        /// 
        /// Why created in code-behind:
        /// - This view is a simple, closed rendering surface that must produce an
        ///   identical pixel output for Preview, PDF export and printer rasterization.
        /// - Creating and updating child TextBlock elements in code-behind gives
        ///   deterministic ordering, exact control over Width/Left/Top and avoids
        ///   dependency on complex DataTemplate or binding timing during off-screen
        ///   rendering. It simplifies producing the exact visual used by the
        ///   PrintService when rasterizing to PNG or printing.
        /// </summary>
        public PrintLabelView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Render the provided label canvas items into the internal Canvas.
        /// </summary>
        /// <remarks>
        /// - Width/Height of this control must be set externally (see PrintConstants.MmToDip)
        ///   so Measure/Arrange produce a layout that matches the physical label size.
        /// - Callers MUST call <c>Measure</c> -> <c>Arrange</c> -> <c>UpdateLayout</c>
        ///   after RenderItems when rendering off-screen. Those calls ensure WPF performs
        ///   a full layout pass so VisualBrush rendering and RenderTargetBitmap capture
        ///   reflect the final positions and sizes of child elements.
        /// - The method intentionally manipulates visual children directly (code-behind)
        ///   to guarantee identical composition across Preview, PDF and Print paths.
        /// </remarks>
        /// <param name="items">Sequence of layout items representing text, position and font.</param>
        public void RenderItems(IEnumerable<LabelCanvasItem> items)
        {
#if DEBUG
            var sw = Stopwatch.StartNew();
#endif
            // 🔥 RESET STATE COMPLETELY
            RootCanvas.Children.Clear();

            try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log($"RenderItems called: items={(items == null ? 0 : System.Linq.Enumerable.Count(items))}"); } catch { }

            if (items == null) return;

            // apply padding offset so coordinates match preview (which uses Border.Padding)
            // The padding value is computed from the physical inner gap (PrintConstants.LabelPaddingMm)
            // converted to WPF DIPs. Using the same constant everywhere guarantees that
            // the inner content area seen on-screen is the same physical area used when
            // exporting or printing.
            double padding = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelPaddingDip;
            // also update the Border padding so visual layout matches the printed inner gap
            try { RootBorder.Padding = new Thickness(padding); } catch { }

            foreach (var it in items)
            {
                // Create TextBlock in code-behind to preserve exact rendering order and
                // control properties when the view is rendered off-screen. This minimizes
                // differences between on-screen preview and rasterized export/print outputs.
                var tb = new TextBlock
                {
                    Text = it.Text ?? string.Empty,
                    FontSize = it.FontSize > 0 ? it.FontSize : 9.0,
                    FontWeight = FontWeights.Normal,
                    TextAlignment = it.Alignment,
                    TextWrapping = TextWrapping.NoWrap
                };

                // compute content width (control width minus padding) so TextAlignment.Center
                // centers the text visually. Use RootCanvas.ActualWidth if available, otherwise
                // fall back to the control Width.
                // Compute content area width (control width minus inner padding) so
                // centered alignment appears visually centered relative to the label.
                // Use ActualWidth when available (running in the visual tree); otherwise
                // rely on the Width property which should already be set to the physical
                // label size converted to DIPs before off-screen rendering.
                double contentPadding = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelPaddingDip;
                double canvasW = RootCanvas.ActualWidth > 1 ? RootCanvas.ActualWidth : this.Width;
                double canvasH = RootCanvas.ActualHeight > 1 ? RootCanvas.ActualHeight : this.Height;

                // Admin layout designer used a baseline canvas size (173x97). Scale coords to current canvas.
                // This scaling preserves the relative positions chosen in the admin UI while
                // mapping them to the actual physical label size used at print time.
                const double DESIGN_W = 173.0;
                const double DESIGN_H = 97.0;
                double scaleX = (DESIGN_W > 0) ? (canvasW / DESIGN_W) : 1.0;
                double scaleY = (DESIGN_H > 0) ? (canvasH / DESIGN_H) : 1.0;

                // Ensure a sensible minimum content width to avoid extremely small or negative values
                // when a layout measurement is not yet available. This is a safety clamp for off-screen
                // scenarios where ActualWidth may be zero until Measure/Arrange are invoked.
                double contentW = Math.Max(8.0, canvasW - (2.0 * contentPadding));
                tb.Width = contentW;

                // Position the TextBlock. For centered alignment we compute the centerX in
                // scaled coordinates and then shift left by half the content width so the
                // TextBlock's internal centering produces visually centered text.
                // We clamp the left edge to the content padding to avoid rendering into the
                // label edge. The Y coordinate is scaled similarly.
                double centerX = it.X * scaleX;
                double left = centerX - (contentW / 2.0);
                left = Math.Max(contentPadding, left);
                double top = it.Y * scaleY;
                Canvas.SetLeft(tb, left);
                Canvas.SetTop(tb, top);

                Panel.SetZIndex(tb, it.ZIndex);

                RootCanvas.Children.Add(tb);
            }
#if DEBUG
            sw.Stop();
            try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log($"[PERF] PrintLabelView.RenderItems: {sw.ElapsedMilliseconds}ms ({items?.Count() ?? 0} items)"); } catch { }
#endif
        }
    }
}