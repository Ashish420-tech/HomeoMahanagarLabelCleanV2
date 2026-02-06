using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HomeoMahanagarLabelCleanV2.Models;

namespace HomeoMahanagarLabelCleanV2.Views
{
    public partial class PrintLabelView : UserControl
    {
        public PrintLabelView()
        {
            InitializeComponent();
        }

        // Width/Height are set externally to match label physical size in DIPs.
        public void RenderItems(IEnumerable<LabelCanvasItem> items)
        {
            // 🔥 RESET STATE COMPLETELY
            RootCanvas.Children.Clear();

            try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log($"RenderItems called: items={(items == null ? 0 : System.Linq.Enumerable.Count(items))}"); } catch { }

            if (items == null) return;

            // apply padding offset so coordinates match preview (which uses Border.Padding)
            double padding = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelPaddingDip;
            // also update the Border padding so visual layout matches the printed inner gap
            try { RootBorder.Padding = new Thickness(padding); } catch { }

            foreach (var it in items)
            {
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
                double contentPadding = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelPaddingDip;
                double canvasW = RootCanvas.ActualWidth > 1 ? RootCanvas.ActualWidth : this.Width;
                double canvasH = RootCanvas.ActualHeight > 1 ? RootCanvas.ActualHeight : this.Height;

                // Admin layout designer used a baseline canvas size (173x97). Scale coords to current canvas.
                const double DESIGN_W = 173.0;
                const double DESIGN_H = 97.0;
                double scaleX = (DESIGN_W > 0) ? (canvasW / DESIGN_W) : 1.0;
                double scaleY = (DESIGN_H > 0) ? (canvasH / DESIGN_H) : 1.0;

                double contentW = Math.Max(8.0, canvasW - (2.0 * contentPadding));
                tb.Width = contentW;

                // position: center the TextBlock at the configured X coordinate so TextAlignment.Center
                // visually centers the text at scaled it.X. Clamp left to not go beyond left padding.
                double centerX = it.X * scaleX;
                double left = centerX - (contentW / 2.0);
                left = Math.Max(contentPadding, left);
                double top = it.Y * scaleY;
                Canvas.SetLeft(tb, left);
                Canvas.SetTop(tb, top);

                Panel.SetZIndex(tb, it.ZIndex);

                RootCanvas.Children.Add(tb);
            }
        }
    }
}