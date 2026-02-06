using HomeoMahanagarLabelCleanV2.Models;
using HomeoMahanagarLabelCleanV2.ViewModels;
using System.Text.Json;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Collections.Generic;
using HomeoMahanagarLabelCleanV2.Services;

namespace HomeoMahanagarLabelCleanV2.Views
{
    public partial class MainView : UserControl
    {
        // suppress re-entrant centering updates that would modify model properties
        private bool _suppressCenteringUpdates = false;
        // inset inside preview border so text does not overlap border stroke (increase slightly for safety)
        private const double PREVIEW_INSET = 4.0;
        private ObservableCollection<LabelCanvasItem>? _adminLayout;
        private readonly Dictionary<LabelCanvasItem, TextBlock> _map = new();
        private HomeoMahanagarLabelCleanV2.ViewModels.LabelPreviewViewModel? _subscribedPreviewVm;
        // Named controls (e.g. PreviewFontSizeBox) are provided by XAML

        public MainView()
        {
            InitializeComponent();
            // ⚠ DO NOT set DataContext here
            // DataContext is already set in MainView.xaml

            this.Loaded += MainView_Loaded;
            this.DataContextChanged += MainView_DataContextChanged;
        }

        private void PreviewVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (sender is not LabelPreviewViewModel vm) return;
                if (e == null || string.IsNullOrEmpty(e.PropertyName)) return;
                if (e.PropertyName == nameof(vm.FontSize))
                {
                    _globalPreviewFontSize = vm.FontSize > 0 ? vm.FontSize : _globalPreviewFontSize;
                    foreach (var kv in _map) kv.Value.FontSize = _globalPreviewFontSize;
                    CenterPreviewLines();
                }
                else if (e.PropertyName == nameof(vm.LineSpacing))
                {
                    _globalLineSpacing = vm.LineSpacing;
                    CenterPreviewLines();
                }
                // when preview data changes, refresh the text of all preview TextBlocks
                else if (e.PropertyName == nameof(vm.MedicineName)
                      || e.PropertyName == nameof(vm.Potency)
                      || e.PropertyName == nameof(vm.Dose)
                      || e.PropertyName == nameof(vm.Time)
                      || e.PropertyName == nameof(vm.ShopAndPhone))
                {
                    try
                    {
                        // update each mapped TextBlock's text using ResolveDynamicText which maps logical keys
                        foreach (var kv in _map)
                        {
                            var item = kv.Key;
                            var tb = kv.Value;
                            tb.Text = ResolveDynamicText(item.Text);
                            tb.Measure(new Size(tb.Width > 0 ? tb.Width : PreviewCanvas.Width, double.PositiveInfinity));
                        }
                    }
                    catch { }

                    CenterPreviewLines();
                }
            }
            catch { }
        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Application.Current?.Shutdown();
            }
            catch { }
        }
        // No changes made to the file.

        // schedule selection to run after DataGrid updates selection state to avoid it being overwritten
        private void ScheduleSelect(LabelViewModel vm, MedicineLabel med, string chosenName)
        {
            try
            {
                vm.SuppressSelectedSync = true;
                // use dispatcher to schedule after grid selection changes
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    try
                    {
                        // ensure setter does not overwrite our preserved preview
                        vm.SuppressSelectedSync = true;
                        vm.SelectMedicineAndPreserveName(med, chosenName);
                        vm.SelectedMedicine = med;
                    }
                    catch (System.Exception ex)
                    {
                        Logging.AppLogger.Log(ex);
                    }
                }));
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
            }
        }

        private void DataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            try
            {
                if (DataContext is not LabelViewModel vm) return;
                // if a click handler recently scheduled a selection, avoid overwriting its preserved preview
                if (vm.SuppressSelectedSync) return;
                if (sender is not DataGrid grid) return;

                var cellInfo = grid.CurrentCell;
                if (cellInfo != null && cellInfo.Item is MedicineLabel med)
                {
                    if (cellInfo.Column is DataGridBoundColumn boundCol)
                    {
                        var binding = boundCol.Binding as System.Windows.Data.Binding;
                        var path = binding?.Path?.Path;
                        if (string.Equals(path, "CommonName", System.StringComparison.OrdinalIgnoreCase))
                            vm.Preview.MedicineName = (med.CommonName ?? string.Empty).ToUpperInvariant();
                        else
                            vm.Preview.MedicineName = (med.LatinName ?? string.Empty).ToUpperInvariant();

                        vm.SelectMedicineAndPreserveName(med, vm.Preview.MedicineName);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
            }
        }

        // Removed SelectionChanged handler because DataGrid SelectedItem is bound to ViewModel

        private void OpenPrinterSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new PrinterSelectionWindow
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
            // refresh binding for printer display
            if (DataContext is ViewModels.LabelViewModel vm)
            {
                vm.RaisePropertyChanged(nameof(vm.PrinterName));
            }
        }

        private void OpenLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var paths = HomeoMahanagarLabelCleanV2.Logging.AppLogger.GetLogPaths();
                foreach (var p in paths)
                {
                    if (System.IO.File.Exists(p))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                        {
                            FileName = p,
                            UseShellExecute = true
                        });
                        return;
                    }
                }

                MessageBox.Show("No log file found.", "Logs", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(ex);
                MessageBox.Show("Unable to open logs: " + ex.Message, "Logs", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MedicineCell_Clicked(object sender, MouseButtonEventArgs e)
        {
            // On MouseUp, set SelectedMedicine from the DataGrid's SelectedItem so both Latin and Common name selections work.
            try
            {
                if (DataContext is not LabelViewModel vm)
                    return;

                if (sender is not DataGrid grid)
                    return;

                MedicineLabel? med = null;

                try
                {
                    // First try: detect which DataGridCell was clicked by walking the visual tree from the event source.
                    DependencyObject src = e.OriginalSource as DependencyObject;
                    DataGridCell? clickedCell = null;
                    while (src != null)
                    {
                        if (src is DataGridCell cell)
                        {
                            clickedCell = cell;
                            break;
                        }
                        src = VisualTreeHelper.GetParent(src);
                    }

                    if (clickedCell != null)
                    {
                        // prefer the medicine object from the clicked cell's DataContext
                        if (clickedCell.DataContext is MedicineLabel cellMed)
                            med = cellMed;

                        if (clickedCell.Column is DataGridBoundColumn boundCol && med != null)
                        {
                            var binding = boundCol.Binding as System.Windows.Data.Binding;
                            var path = binding?.Path?.Path;
                            var chosen = string.Equals(path, "CommonName", System.StringComparison.OrdinalIgnoreCase)
                                ? (med.CommonName ?? string.Empty).ToUpperInvariant()
                                : (med.LatinName ?? string.Empty).ToUpperInvariant();

                            ScheduleSelect(vm, med, chosen);
                            return;
                        }
                    }

                    // Second try: inspect SelectedCells (works when selection unit is row/cell)
                    // If med is still unknown, try to obtain it from SelectedItem / CurrentCell / SelectedCells
                    if (med == null)
                    {
                        if (grid.SelectedItem is MedicineLabel sel) med = sel;
                        else if (grid.CurrentCell.Item is MedicineLabel cur) med = cur;
                        else if (grid.SelectedCells != null && grid.SelectedCells.Count > 0 && grid.SelectedCells[0].Item is MedicineLabel first) med = first;
                    }

                    if (med != null && grid.SelectedCells != null && grid.SelectedCells.Count > 0)
                    {
                        foreach (var ci in grid.SelectedCells)
                        {
                            if (ci.Item is MedicineLabel cellMed && ci.Column is DataGridBoundColumn scBound && cellMed == med)
                            {
                                var binding = scBound.Binding as System.Windows.Data.Binding;
                                var path = binding?.Path?.Path;
                                if (string.Equals(path, "CommonName", System.StringComparison.OrdinalIgnoreCase))
                                    ScheduleSelect(vm, med, (med.CommonName ?? string.Empty).ToUpperInvariant());
                                else
                                    ScheduleSelect(vm, med, (med.LatinName ?? string.Empty).ToUpperInvariant());

                                return;
                            }
                        }
                    }

                    // Final fallback: use CurrentCell column if available
                    var current = grid.CurrentCell;
                    if (current != null && current.Column is DataGridBoundColumn curBound)
                    {
                        var binding = curBound.Binding as System.Windows.Data.Binding;
                        if (binding?.Path?.Path == "CommonName")
                            ScheduleSelect(vm, med, (med.CommonName ?? string.Empty).ToUpperInvariant());
                        else
                            ScheduleSelect(vm, med, (med.LatinName ?? string.Empty).ToUpperInvariant());

                        return;
                    }

                    // Very last fallback: use Latin or Common name
                    var fallback = (med.LatinName ?? med.CommonName ?? string.Empty).ToUpperInvariant();
                    ScheduleSelect(vm, med, fallback);
                }
                catch (System.Exception ex)
                {
                    Logging.AppLogger.Log(ex);
                    vm.Preview.MedicineName = (med.LatinName ?? med.CommonName ?? string.Empty).ToUpperInvariant();
                    vm.SelectedMedicine = med;
                }
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
            }
        }

        // ================= ADMIN PANEL =================
        private void Admin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Admin designer window has been removed. Refresh preview controls instead.
                RefreshPreviewLineCombo();
                MessageBox.Show("Admin designer removed. Use the 'Preview' menu controls to edit label layout.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Admin Panel Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RefreshPreviewLineCombo()
        {
            try
            {
                var combo = this.FindName("PreviewLineCombo") as System.Windows.Controls.ComboBox;
                if (combo == null) return;

                combo.Items.Clear();
                if (Services.AppState.Storage?.AdminLayout != null)
                {
                    foreach (var item in Services.AppState.Storage.AdminLayout)
                    {
                        combo.Items.Add(item);
                    }
                }
                if (combo.Items.Count > 0)
                    combo.SelectedIndex = 0;
            }
            catch { }
        }

        

        private void PreviewFontSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                var combo = this.FindName("PreviewLineCombo") as System.Windows.Controls.ComboBox;
                if (combo?.SelectedItem is LabelCanvasItem item)
                {
                    item.FontSize = e.NewValue;
                }
            }
            catch { }
        }

        private double _globalPreviewFontSize = 9.0;
        private double _globalLineSpacing = 0.0;


        // replaced slider handlers with spinner (textbox + repeat buttons)
        private void PreviewFontSizeIncrease_Click(object sender, RoutedEventArgs e)
        {
            ChangePreviewFontSize(1);
        }

        private void PreviewFontSizeDecrease_Click(object sender, RoutedEventArgs e)
        {
            ChangePreviewFontSize(-1);
        }

        private void ChangePreviewFontSize(int delta)
        {
            try
            {
                if (!double.TryParse(PreviewFontSizeBox.Text, out double val)) val = _globalPreviewFontSize;
                val = Math.Max(6, Math.Min(36, val + delta));
                PreviewFontSizeBox.Text = ((int)val).ToString();
                _globalPreviewFontSize = val;
                foreach (var kv in _map) kv.Value.FontSize = _globalPreviewFontSize;
                CenterPreviewLines();
            }
            catch { }
        }

        private void PreviewFontSizeBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!double.TryParse(PreviewFontSizeBox.Text, out double val)) return;
                val = Math.Max(6, Math.Min(36, val));
                _globalPreviewFontSize = val;
                foreach (var kv in _map) kv.Value.FontSize = _globalPreviewFontSize;
                CenterPreviewLines();
            }
            catch { }
        }

        private void PreviewFontSizeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) PreviewFontSizeBox_LostFocus(sender, null);
        }

        private void PreviewLineSpacingIncrease_Click(object sender, RoutedEventArgs e)
        {
            ChangePreviewLineSpacing(1);
        }

        private void PreviewLineSpacingDecrease_Click(object sender, RoutedEventArgs e)
        {
            ChangePreviewLineSpacing(-1);
        }

        private void ChangePreviewLineSpacing(int delta)
        {
            try
            {
                if (!double.TryParse(PreviewLineSpacingBox.Text, out double val)) val = _globalLineSpacing;
                val = Math.Max(0, Math.Min(100, val + delta));
                PreviewLineSpacingBox.Text = ((int)val).ToString();
                _globalLineSpacing = val;
                CenterPreviewLines();
            }
            catch { }
        }

        private void PreviewLineSpacingBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!double.TryParse(PreviewLineSpacingBox.Text, out double val)) return;
                val = Math.Max(0, Math.Min(100, val));
                _globalLineSpacing = val;
                CenterPreviewLines();
            }
            catch { }
        }

        private void PreviewLineSpacingBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) PreviewLineSpacingBox_LostFocus(sender, null);
        }

        private void CenterPreviewLines()
        {
            try
            {
                if (_adminLayout == null || PreviewCanvas == null) return;

                // build ordered list of items by Y (top->bottom)
                var items = new List<LabelCanvasItem>(_adminLayout);
                items.Sort((a, b) => a.Y.CompareTo(b.Y));

                // only first 5 lines should be visible in preview
                if (items.Count > 5) items = items.Take(5).ToList();

                // identify shop/phone item (persisted as index 4 in admin layout) so we can pin it to bottom
                LabelCanvasItem? shopItem = null;
                try
                {
                    if (_adminLayout != null && _adminLayout.Count > 4)
                        shopItem = _adminLayout[4];
                }
                catch { shopItem = null; }

                // compute total height occupied by lines using current font size and spacing
                double totalHeight = 0;
                var heights = new List<double>();
                // exclude shopItem from centering calculation (will be pinned to bottom)
                var layoutItems = items.Where(it => shopItem == null || !object.ReferenceEquals(it, shopItem)).ToList();

                // split layoutItems into head (flowing content) and tail (last two pinned lines above shop)
                int pinnedCount = Math.Min(2, layoutItems.Count);
                var headItems = layoutItems.Take(layoutItems.Count - pinnedCount).ToList();
                var pinnedItems = layoutItems.Skip(Math.Max(0, layoutItems.Count - pinnedCount)).ToList();

                // measure head items heights
                foreach (var it in headItems)
                {
                    double fs = it.FontSize > 0 ? it.FontSize : (_globalPreviewFontSize > 0 ? _globalPreviewFontSize : 9.0);
                    // approximate text height as 1.2 * font size
                    double h = fs * 1.2;
                    heights.Add(h);
                    totalHeight += h;
                }
                // add spacing between head lines
                if (headItems.Count > 1)
                    totalHeight += _globalLineSpacing * (headItems.Count - 1);

                // starting top: prefer a small fixed inset (top-aligned) to avoid large centered gaps
                double innerAvailable = Math.Max(0, PreviewCanvas.Height - PREVIEW_INSET * 2);
                double startTop;
                const double SHOP_GAP = 6.0; // extra gap (DIPs) between last body line and shop/phone

                // measure shop item height if available so we can reserve space
                double shopTbHeight = 0.0;
                if (shopItem != null && _map.TryGetValue(shopItem, out var shopTbMeasure))
                {
                    shopTbMeasure.FontSize = shopItem.FontSize > 0 ? shopItem.FontSize : _globalPreviewFontSize;
                    shopTbMeasure.Measure(new Size(Math.Max(0, PreviewCanvas.Width - PREVIEW_INSET * 2), double.PositiveInfinity));
                    shopTbHeight = shopTbMeasure.DesiredSize.Height;
                }

                double shopBottomTop = PreviewCanvas.Height - PREVIEW_INSET - shopTbHeight - SHOP_GAP;

                if (totalHeight >= innerAvailable)
                {
                    // if content taller than available space, start at inset so it fits downward
                    startTop = PREVIEW_INSET;
                }
                else
                {
                    // top-align with a small extra gap (2 DIPs) to avoid touching the border
                    startTop = PREVIEW_INSET + 2.0;
                }

                // compute positions for pinned tail items (measure their heights)
                double lastPinnedTop = double.NaN;
                double secondLastPinnedTop = double.NaN;
                double pinnedGap = 3.0; // gap between pinned items
                if (pinnedItems.Count > 0)
                {
                    // measure pinned heights using existing TextBlocks if available
                    double[] pinnedHeights = new double[pinnedItems.Count];
                    for (int pi = 0; pi < pinnedItems.Count; pi++)
                    {
                        var p = pinnedItems[pi];
                        if (_map.TryGetValue(p, out var ptb))
                        {
                            ptb.FontSize = p.FontSize > 0 ? p.FontSize : _globalPreviewFontSize;
                            ptb.Measure(new Size(Math.Max(0, PreviewCanvas.Width - PREVIEW_INSET * 2), double.PositiveInfinity));
                            pinnedHeights[pi] = ptb.DesiredSize.Height;
                        }
                        else
                        {
                            double fs = p.FontSize > 0 ? p.FontSize : _globalPreviewFontSize;
                            pinnedHeights[pi] = fs * 1.2;
                        }
                    }

                    // position last pinned (closest to shop)
                    double lastHeight = pinnedHeights.Last();
                    double lastTop = shopBottomTop - pinnedGap - lastHeight;
                    lastPinnedTop = lastTop;

                    if (pinnedItems.Count > 1)
                    {
                        double secondHeight = pinnedHeights[pinnedHeights.Length - 2];
                        double secondTop = lastTop - _globalLineSpacing - secondHeight;
                        secondLastPinnedTop = secondTop;
                    }
                }

                // ensure the head block does not overlap the pinned items area
                double reservedTopForPinned = double.PositiveInfinity;
                if (!double.IsNaN(secondLastPinnedTop)) reservedTopForPinned = secondLastPinnedTop;
                else if (!double.IsNaN(lastPinnedTop)) reservedTopForPinned = lastPinnedTop;
                if (!double.IsInfinity(reservedTopForPinned) && startTop + totalHeight > reservedTopForPinned)
                {
                    startTop = Math.Max(PREVIEW_INSET, reservedTopForPinned - totalHeight);
                }

                // apply positions and update TextBlocks (skip shopItem here)
                for (int i = 0; i < layoutItems.Count; i++)
                {
                    var it = layoutItems[i];
                    double top = startTop + heights.Take(i).Sum() + _globalLineSpacing * i;
                    // avoid triggering PropertyChanged if value is effectively unchanged
                    if (!_suppressCenteringUpdates)
                    {
                        try
                        {
                            _suppressCenteringUpdates = true;
                            if (Math.Abs(it.Y - top) > 0.01)
                                it.Y = top; // update model so persistence sees it
                        }
                        finally { _suppressCenteringUpdates = false; }
                    }

                    if (_map.TryGetValue(it, out var tb))
                    {
                        // apply per-item font size if present
                        tb.FontSize = it.FontSize > 0 ? it.FontSize : _globalPreviewFontSize;
                        tb.TextAlignment = it.Alignment;
                        // ensure centered blocks respect the preview inset horizontally
                        tb.Width = Math.Max(0, PreviewCanvas.Width - PREVIEW_INSET * 2);
                        Canvas.SetLeft(tb, PREVIEW_INSET);
                        // re-measure so ActualHeight/DesiredSize reflect new font/width before clamping
                        tb.Measure(new Size(tb.Width, double.PositiveInfinity));
                        double tbHeight = tb.DesiredSize.Height;
                        // clamp vertical position inside inset area
                        Canvas.SetTop(tb, Math.Max(PREVIEW_INSET, Math.Min(top, PreviewCanvas.Height - PREVIEW_INSET - tbHeight)));
                    }
                }

                // position shop item at the bottom inset
                if (shopItem != null && _map.TryGetValue(shopItem, out var shopTb))
                {
                    // ensure font size applied
                    shopTb.FontSize = shopItem.FontSize > 0 ? shopItem.FontSize : _globalPreviewFontSize;
                    shopTb.TextAlignment = shopItem.Alignment;
                    shopTb.Width = Math.Max(0, PreviewCanvas.Width - PREVIEW_INSET * 2);
                    Canvas.SetLeft(shopTb, PREVIEW_INSET);
                    shopTb.Measure(new Size(shopTb.Width, double.PositiveInfinity));
                    double tbHeight = shopTb.DesiredSize.Height;
                    const double SHOP_GAP_LOCAL = 6.0;
                    double bottomTop = PreviewCanvas.Height - PREVIEW_INSET - tbHeight - SHOP_GAP_LOCAL;
                    // update model Y
                    if (!_suppressCenteringUpdates)
                    {
                        try
                        {
                            _suppressCenteringUpdates = true;
                            if (Math.Abs(shopItem.Y - bottomTop) > 0.01)
                                shopItem.Y = bottomTop;
                        }
                        finally { _suppressCenteringUpdates = false; }
                    }
                    Canvas.SetTop(shopTb, Math.Max(PREVIEW_INSET, Math.Min(bottomTop, PreviewCanvas.Height - PREVIEW_INSET - tbHeight)));
                }

                // place pinned tail items (if any)
                if (pinnedItems != null && pinnedItems.Count > 0)
                {
                    for (int pi = 0; pi < pinnedItems.Count; pi++)
                    {
                        var p = pinnedItems[pi];
                        double assignedTop = double.NaN;
                        if (pi == pinnedItems.Count - 1)
                            assignedTop = lastPinnedTop;
                        else if (pi == pinnedItems.Count - 2)
                            assignedTop = secondLastPinnedTop;

                        if (!double.IsNaN(assignedTop) && _map.TryGetValue(p, out var ptb))
                        {
                            ptb.FontSize = p.FontSize > 0 ? p.FontSize : _globalPreviewFontSize;
                            ptb.TextAlignment = p.Alignment;
                            ptb.Width = Math.Max(0, PreviewCanvas.Width - PREVIEW_INSET * 2);
                            Canvas.SetLeft(ptb, PREVIEW_INSET);
                            ptb.Measure(new Size(ptb.Width, double.PositiveInfinity));
                            double h = ptb.DesiredSize.Height;
                            Canvas.SetTop(ptb, Math.Max(PREVIEW_INSET, Math.Min(assignedTop, PreviewCanvas.Height - PREVIEW_INSET - h)));
                            // persist
                            if (!_suppressCenteringUpdates)
                            {
                                try { _suppressCenteringUpdates = true; p.Y = Canvas.GetTop(ptb); }
                                finally { _suppressCenteringUpdates = false; }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        // spinner handlers removed (reverted changes)

        private void PreviewMoveUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var combo = this.FindName("PreviewLineCombo") as System.Windows.Controls.ComboBox;
                if (combo?.SelectedItem is LabelCanvasItem item)
                {
                    item.Y = Math.Max(0, item.Y - 4);
                }
            }
            catch { }
        }

        private void PreviewMoveDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var combo = this.FindName("PreviewLineCombo") as System.Windows.Controls.ComboBox;
                if (combo?.SelectedItem is LabelCanvasItem item)
                {
                    var preview = this.FindName("PreviewCanvas") as System.Windows.Controls.Canvas;
                    double maxH = preview?.Height ?? 113.0;
                    item.Y = Math.Min(maxH - 10, item.Y + 4);
                }
            }
            catch { }
        }

        private void PreviewSpacingIncrease_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var box = this.FindName("PreviewSpacingSpinner") as System.Windows.Controls.TextBox;
                var combo = this.FindName("PreviewLineCombo") as System.Windows.Controls.ComboBox;
                if (box == null || combo == null) return;
                if (!double.TryParse(box.Text, out double val)) val = 0;
                val = val + 1; // treat spinner as absolute Y (pixels)
                box.Text = ((int)val).ToString();
                if (combo.SelectedItem is LabelCanvasItem item)
                {
                    item.Y = val;
                }
            }
            catch { }
        }

        private void PreviewSpacingDecrease_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var box = this.FindName("PreviewSpacingSpinner") as System.Windows.Controls.TextBox;
                var combo = this.FindName("PreviewLineCombo") as System.Windows.Controls.ComboBox;
                if (box == null || combo == null) return;
                if (!double.TryParse(box.Text, out double val)) val = 0;
                val = Math.Max(0, val - 1);
                box.Text = ((int)val).ToString();
                if (combo.SelectedItem is LabelCanvasItem item)
                {
                    item.Y = val;
                }
            }
            catch { }
        }

        private void PreviewSpacingSpinner_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                var box = sender as System.Windows.Controls.TextBox;
                if (box == null) return;
                if (!double.TryParse(box.Text, out double val)) return;
                val = Math.Max(0, Math.Min(1000, val));
                box.Text = ((int)val).ToString();
                var combo = this.FindName("PreviewLineCombo") as System.Windows.Controls.ComboBox;
                if (combo?.SelectedItem is LabelCanvasItem item)
                {
                    item.Y = val;
                }
            }
            catch { }
        }

        private void PreviewSpacingSpinner_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    var box = sender as System.Windows.Controls.TextBox;
                    if (box == null) return;
                    if (!double.TryParse(box.Text, out double val)) return;
                    val = Math.Max(0, Math.Min(1000, val));
                    box.Text = ((int)val).ToString();
                    var combo = this.FindName("PreviewLineCombo") as System.Windows.Controls.ComboBox;
                    if (combo?.SelectedItem is LabelCanvasItem item)
                    {
                        item.Y = val;
                    }
                }
            }
            catch { }
        }

        // No-op helper removed; spacing applied directly in handlers

        private void PreviewLineCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                var combo = sender as System.Windows.Controls.ComboBox;
                var box = this.FindName("PreviewSpacingSpinner") as System.Windows.Controls.TextBox;
                if (combo?.SelectedItem is LabelCanvasItem item && box != null)
                {
                    box.Text = ((int)item.Y).ToString();
                }
            }
            catch { }
        }

        // Preview control replacements for Admin window actions
        // The ability to add arbitrary 'NEW TEXT' items via UI was removed.
        // Persisted placeholder items named "NEW TEXT" are cleaned up on attach so they do not appear in preview.

        private void PreviewSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // persist app state (AdminLayout already bound to storage)
                Services.AppState.Save();
            }
            catch { }
        }

        private void PreviewLoad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadAdminLabelPreview();
                RefreshPreviewLineCombo();
            }
            catch { }
        }

        private void PreviewAlignLeft_Click(object sender, RoutedEventArgs e) => SetSelectedAlignment(TextAlignment.Left);
        private void PreviewAlignCenter_Click(object sender, RoutedEventArgs e) => SetSelectedAlignment(TextAlignment.Center);
        private void PreviewAlignRight_Click(object sender, RoutedEventArgs e) => SetSelectedAlignment(TextAlignment.Right);
        private void PreviewAlignTop_Click(object sender, RoutedEventArgs e) { var combo = this.FindName("PreviewLineCombo") as System.Windows.Controls.ComboBox; if (combo?.SelectedItem is LabelCanvasItem item) item.Y = 0; }
        private void PreviewAlignMiddle_Click(object sender, RoutedEventArgs e) { var combo = this.FindName("PreviewLineCombo") as System.Windows.Controls.ComboBox; if (combo?.SelectedItem is LabelCanvasItem item) item.Y = (PreviewCanvas.Height - 10) / 2; }
        private void PreviewAlignBottom_Click(object sender, RoutedEventArgs e) { var combo = this.FindName("PreviewLineCombo") as System.Windows.Controls.ComboBox; if (combo?.SelectedItem is LabelCanvasItem item) item.Y = PreviewCanvas.Height - 10; }

        private void SetSelectedAlignment(TextAlignment a)
        {
            try
            {
                var combo = this.FindName("PreviewLineCombo") as System.Windows.Controls.ComboBox;
                if (combo?.SelectedItem is LabelCanvasItem item)
                {
                    item.Alignment = a;
                }
            }
            catch { }
        }
        private void LoadAdminLabelPreview()
        {
            try
            {
                string filePath = "label-layout.json";

                if (!File.Exists(filePath))
                    return;

                var json = File.ReadAllText(filePath);
                var items = JsonSerializer.Deserialize<List<LabelTextItem>>(json);

                if (items == null)
                    return;

                PreviewCanvas.Children.Clear();

                foreach (var item in items)
                {
                    var tb = new TextBlock
                    {
                        Text = ResolveDynamicText(item.Text),
                        FontSize = item.FontSize,
                        Foreground = Brushes.Black,
                        IsHitTestVisible = false // preview only
                    };

                    Canvas.SetLeft(tb, item.X);
                    Canvas.SetTop(tb, item.Y);

                    PreviewCanvas.Children.Add(tb);
                }
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
            }
        }

        private void MainView_Loaded(object sender, RoutedEventArgs e)
        {
            AttachPreviewLayout();
        }

        private void MainView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachPreviewLayout();
        }

        private void AttachPreviewLayout()
        {
            try
            {
                // clear existing map/subscriptions
                if (_adminLayout != null)
                {
                    _adminLayout.CollectionChanged -= AdminLayout_CollectionChanged;
                    foreach (var it in _adminLayout)
                        it.PropertyChanged -= AdminItem_PropertyChanged;
                }

                // find layout from DataContext if available, else from AppState
                ObservableCollection<LabelCanvasItem>? layout = null;
                LabelPreviewViewModel? previewVm = null;
                if (DataContext is LabelViewModel vm)
                {
                    layout = vm.Preview.AdminLayout;
                    previewVm = vm.Preview;
                }
                else
                {
                    layout = Services.AppState.Storage.AdminLayout;
                }

                _adminLayout = layout;
                // keep Preview VM subscription up-to-date so changes to VM (font size / spacing)
                // are reflected in the preview rendering
                try
                {
                    if (_subscribedPreviewVm != null)
                        _subscribedPreviewVm.PropertyChanged -= PreviewVm_PropertyChanged;
                    _subscribedPreviewVm = previewVm;
                    if (_subscribedPreviewVm != null)
                        _subscribedPreviewVm.PropertyChanged += PreviewVm_PropertyChanged;

                    // initialize global preview metrics from VM so preview uses consistent sizing
                    if (previewVm != null)
                    {
                        _globalPreviewFontSize = previewVm.FontSize > 0 ? previewVm.FontSize : _globalPreviewFontSize;
                        _globalLineSpacing = previewVm.LineSpacing;
                        // update UI spinner/textbox if present
                        try { PreviewFontSizeBox.Text = ((int)_globalPreviewFontSize).ToString(); } catch { }
                    }
                }
                catch { }
                _map.Clear();
                PreviewCanvas.Children.Clear();
                if (PreviewCanvas != null)
                    PreviewCanvas.ClipToBounds = true; // prevent children drawing outside border

                if (_adminLayout == null) return;

                // no VM-controlled border thickness/inset here — keep constant inset

                // remove persisted placeholder entries named 'NEW TEXT' to avoid showing them in preview
                var placeholders = _adminLayout.Where(it => string.Equals(it.Text, "NEW TEXT", System.StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var ph in placeholders)
                {
                    try { _adminLayout.Remove(ph); } catch { }
                }

                // Ensure the first five slots correspond to the logical label lines
                var desiredKeys = new[] { "MedicineName", "Potency", "Dose", "Time", "ShopAndPhone" };
                var original = _adminLayout.ToList();
                var ordered = new List<LabelCanvasItem>();

                for (int i = 0; i < desiredKeys.Length; i++)
                {
                    var key = desiredKeys[i];
                    var match = original.FirstOrDefault(it => string.Equals(it.Text, key, System.StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        ordered.Add(match);
                        original.Remove(match);
                    }
                    else
                    {
                        // create a reasonable default item for missing keys
                        ordered.Add(new LabelCanvasItem
                        {
                            Text = key,
                            X = 86,
                            Y = 6 + i * 16,
                            FontSize = 9,
                            Alignment = System.Windows.TextAlignment.Center,
                            ZIndex = 0
                        });
                    }
                }

                // rebuild the collection so first five items are in desired logical order
                _adminLayout.Clear();
                foreach (var it in ordered) _adminLayout.Add(it);
                // append any remaining items the user may have
                foreach (var it in original) _adminLayout.Add(it);

                // build visuals — only first five logical items should be visible in the preview
                foreach (var item in _adminLayout.Take(5))
                {
                    var tb = CreateTextBlockForItem(item);
                    _map[item] = tb;
                    PreviewCanvas.Children.Add(tb);
                    item.PropertyChanged += AdminItem_PropertyChanged;
                }

                // refresh text from preview VM to avoid showing placeholder keys when DataContext becomes available
                try
                {
                    foreach (var kv in _map)
                    {
                        var item = kv.Key;
                        var tb = kv.Value;
                        tb.Text = ResolveDynamicText(item.Text);
                        tb.Measure(new Size(tb.Width > 0 ? tb.Width : PreviewCanvas.Width, double.PositiveInfinity));
                    }
                }
                catch { }

                _adminLayout.CollectionChanged += AdminLayout_CollectionChanged;
                // center after initial build
                CenterPreviewLines();
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
            }
        }

        private TextBlock CreateTextBlockForItem(LabelCanvasItem item)
        {
            var tb = new TextBlock
            {
                Text = ResolveDynamicText(item.Text),
                // use item font size if specified, else fallback to global preview font size
                FontSize = item.FontSize > 0 ? item.FontSize : _globalPreviewFontSize,
                FontWeight = FontWeights.Normal,
                Foreground = Brushes.Black,
                IsHitTestVisible = true,
                TextAlignment = item.Alignment,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.None
            };

            // allow clicking the preview text to edit inline
            tb.Tag = item;
            tb.MouseLeftButtonDown += PreviewText_MouseDown;

            // Positioning: when centered or right-aligned we make the TextBlock span the full canvas width
            if (PreviewCanvas != null)
            {
                double scale = 1.0;
                if (item.FontSize > 0)
                    scale = 9.0 / item.FontSize;

                if (item.Alignment == TextAlignment.Center || item.Alignment == TextAlignment.Right)
                {
                    // let the TextBlock span the inner canvas area and center/right align text
                    tb.Width = Math.Max(0, PreviewCanvas.Width - PREVIEW_INSET * 2);
                    Canvas.SetLeft(tb, PREVIEW_INSET);
                }
                else
                {
                    // left-aligned: respect stored X scaled to preview font size
                    tb.Width = double.NaN;
                    // measure to compute width if necessary
                    tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double left = item.X * scale;
                    // clamp left inside preview inset
                    left = Math.Max(PREVIEW_INSET, Math.Min(left, PreviewCanvas.Width - PREVIEW_INSET - tb.DesiredSize.Width));
                    Canvas.SetLeft(tb, left);
                }

                // scale Y coordinate so vertical position matches expected baseline for font size 9
                double top = item.Y * (item.FontSize > 0 ? 9.0 / item.FontSize : 1.0);
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                top = Math.Max(PREVIEW_INSET, Math.Min(top, PreviewCanvas.Height - PREVIEW_INSET - tb.DesiredSize.Height));
                Canvas.SetTop(tb, top);
            }
            else
            {
                Canvas.SetLeft(tb, item.X);
                Canvas.SetTop(tb, item.Y);
            }
            Panel.SetZIndex(tb, item.ZIndex);
            Panel.SetZIndex(tb, item.ZIndex);
            return tb;
        }

        private void AdminLayout_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // For simplicity, rebuild the preview visuals and mapping when the layout changes.
            Dispatcher.Invoke(() =>
            {
                try { AttachPreviewLayout(); } catch { }
            });
        }

        private void AdminItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not LabelCanvasItem item) return;

            Dispatcher.Invoke(() =>
            {
                if (!_map.TryGetValue(item, out var tb))
                    return;

                if (e.PropertyName == nameof(LabelCanvasItem.Text) || string.IsNullOrEmpty(e.PropertyName))
                {
                    tb.Text = ResolveDynamicText(item.Text);
                }
                // enforce preview font size from global preview control instead of hard-coded 9
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(LabelCanvasItem.FontSize))
                {
                    tb.FontSize = item.FontSize > 0 ? item.FontSize : _globalPreviewFontSize;
                }
                if (e.PropertyName == nameof(LabelCanvasItem.X) || string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(LabelCanvasItem.Alignment))
                {
                    double scale = item.FontSize > 0 ? 9.0 / item.FontSize : 1.0;
                    if (item.Alignment == TextAlignment.Center || item.Alignment == TextAlignment.Right)
                    {
                        // span inner width
                        tb.Width = Math.Max(0, PreviewCanvas.Width - PREVIEW_INSET * 2);
                        Canvas.SetLeft(tb, PREVIEW_INSET);
                    }
                    else
                    {
                        tb.Width = double.NaN;
                        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        double left = item.X * scale;
                        left = Math.Max(PREVIEW_INSET, Math.Min(left, PreviewCanvas.Width - PREVIEW_INSET - tb.DesiredSize.Width));
                        Canvas.SetLeft(tb, left);
                    }
                }
                if (e.PropertyName == nameof(LabelCanvasItem.Y) || string.IsNullOrEmpty(e.PropertyName))
                {
                    double scale = item.FontSize > 0 ? 9.0 / item.FontSize : 1.0;
                    tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double top = item.Y * scale;
                    top = Math.Max(PREVIEW_INSET, Math.Min(top, PreviewCanvas.Height - PREVIEW_INSET - tb.DesiredSize.Height));
                    Canvas.SetTop(tb, top);
                }
                if (e.PropertyName == nameof(LabelCanvasItem.ZIndex) || string.IsNullOrEmpty(e.PropertyName))
                {
                    Panel.SetZIndex(tb, item.ZIndex);
                }
                if (e.PropertyName == nameof(LabelCanvasItem.Alignment) || string.IsNullOrEmpty(e.PropertyName))
                {
                    tb.TextAlignment = item.Alignment;
                }

                // always render normal font weight in preview
                tb.FontWeight = FontWeights.Normal;

                // debug flash removed — no background changes here

                // keep preview centered after any change
                try { CenterPreviewLines(); } catch { }
            });
        }

        private void PreviewText_MouseDown(object? sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is not TextBlock tb) return;
                if (tb.Tag is not LabelCanvasItem item) return;

                // open small inline edit: prompt user with an input dialog
                var input = Microsoft.VisualBasic.Interaction.InputBox("Edit text:", "Edit Label Text", item.Text);
                if (!string.IsNullOrEmpty(input))
                {
                    item.Text = input.ToUpperInvariant();
                }
            }
            catch { }
        }

        private string ResolveDynamicText(string key)
        {
            if (DataContext is not LabelViewModel vm)
                return key;

            // Compose using LabelTextComposer so medicine name wraps into up to two lines and potency is merged
            string[] composed;
            try
            {
                double canvasWidth = (PreviewCanvas?.ActualWidth > 0) ? PreviewCanvas.ActualWidth : (PreviewCanvas?.Width ?? 120.0);
                double width = Math.Max(20.0, canvasWidth - PREVIEW_INSET * 2);
                var composer = new LabelTextComposer(width, vm.Preview.FontSize, vm.Preview.FontFamily);
                composed = composer.Compose(vm.Preview.MedicineName ?? string.Empty, vm.Preview.Potency ?? string.Empty, vm.Preview.Dose ?? string.Empty, vm.Preview.Time ?? string.Empty, vm.Preview.ShopAndPhone ?? string.Empty);
            }
            catch
            {
                composed = new string[] { vm.Preview.MedicineName ?? string.Empty, vm.Preview.Potency ?? string.Empty, vm.Preview.Dose ?? string.Empty, vm.Preview.Time ?? string.Empty, vm.Preview.ShopAndPhone ?? string.Empty };
            }

            var k = (key ?? string.Empty).Trim().ToUpperInvariant();
            return k switch
            {
                "MEDICINENAME" => composed.Length > 0 ? composed[0] : string.Empty,
                "POTENCY" => composed.Length > 1 ? composed[1] : string.Empty,
                "DOSE" => composed.Length > 2 ? composed[2] : vm.Preview.Dose ?? string.Empty,
                "TIME" => composed.Length > 3 ? composed[3] : vm.Preview.Time ?? string.Empty,
                "SHOPANDPHONE" => composed.Length > 4 ? composed[4] : vm.Preview.ShopAndPhone ?? string.Empty,
                _ => key
            };
        }

    }
}
