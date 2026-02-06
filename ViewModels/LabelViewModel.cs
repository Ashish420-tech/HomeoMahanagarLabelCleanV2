using HomeoMahanagarLabelCleanV2.Commands;
using HomeoMahanagarLabelCleanV2.Helpers;
using HomeoMahanagarLabelCleanV2.Models;
using HomeoMahanagarLabelCleanV2.Services;
using HomeoMahanagarLabelCleanV2.Views;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Printing;
using System.Management;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace HomeoMahanagarLabelCleanV2.ViewModels
{
    public class LabelViewModel : INotifyPropertyChanged
    {
        // ================= FIELDS =================
        private MedicineLabel? _selectedMedicine;
        private string? _searchText;

        // ================= PREVIEW =================
        public LabelPreviewViewModel Preview { get; } = new LabelPreviewViewModel();

        // ================= MEDICINES =================
        public ObservableCollection<MedicineLabel> Labels { get; private set; }
        public ICollectionView MedicinesView { get; private set; }

        // ================= SUGGESTIONS =================
        public ObservableCollection<string> Potencies { get; }
        public ObservableCollection<string> Doses { get; }
        public ObservableCollection<string> Times { get; }

        private readonly SuggestionStore _store;

        // ================= COMMANDS =================
        public ICommand UploadExcelCommand { get; }
        public bool IsBusy { get; private set; }
        public ICommand PrintLabelCommand { get; }
        public ICommand EmitTsplTestCommand { get; }
        public string PrinterName => Services.AppState.Storage.LabelPrinterName ?? System.Configuration.ConfigurationManager.AppSettings["LabelPrinterName"];

        public void RaisePropertyChanged(string name) => OnPropertyChanged(name);
        public ICommand ExportPdfCommand { get; }
        public ICommand AddMedicineCommand { get; }
        public ICommand DeleteMedicineCommand { get; }
        public ICommand OpenAdminDesignerCommand { get; }

        // ================= PRINTER STATUS =================
        private const string TargetPrinterName = "SNBC TVSE LP 46 NEO BPLE";
        private bool _printerAvailable;
        private bool _labelSizeSelected;
        private bool _printerUsbConnected;

        public bool PrinterAvailable
        {
            get => _printerAvailable;
            private set { _printerAvailable = value; OnPropertyChanged(); }
        }

        public bool LabelSizeSelected
        {
            get => _labelSizeSelected;
            private set { _labelSizeSelected = value; OnPropertyChanged(); }
        }

        public bool PrinterUsbConnected
        {
            get => _printerUsbConnected;
            private set { _printerUsbConnected = value; OnPropertyChanged(); }
        }

        // Public wrapper for benchmarking
        public void PublicCheckPrinterStatus()
        {
            CheckPrinterStatus();
        }

        // ================= SELECTED MEDICINE =================
        public MedicineLabel? SelectedMedicine
        {
            get => _selectedMedicine;
            set
            {
                try
                {
                    // If a view action requested suppressing the automatic sync (e.g. cell-click preserved name), honor it
                    if (SuppressSelectedSync)
                    {
                        _selectedMedicine = value;
                        OnPropertyChanged();
                        SuppressSelectedSync = false;
                        return;
                    }

                    _selectedMedicine = value;
                    OnPropertyChanged();
                    SyncPreview();
                }
                catch (System.Exception ex)
                {
                    Logging.AppLogger.Log(ex);
                }
            }
        }

        // When true, SelectedMedicine setter will not call SyncPreview and will clear this flag.
        public bool SuppressSelectedSync { get; set; }

        // Select medicine from UI and preserve a chosen display name (used when user clicks a specific column)
        public void SelectMedicineAndPreserveName(MedicineLabel med, string? chosenName)
        {
            try
            {
                _selectedMedicine = med;
                OnPropertyChanged(nameof(SelectedMedicine));

                // prefer chosenName if provided, otherwise fall back
                var name = (chosenName ?? med.DisplayName ?? med.LatinName ?? med.CommonName ?? string.Empty).Trim().ToUpperInvariant();
                Preview.MedicineName = name;
                Preview.Potency = med.Potency;
                Preview.Dose = med.Dose;
                Preview.Time = med.Time;

                UpdateAdminLayout();
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
            }
        }

        // ================= SEARCH =================
        public string? SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                MedicinesView.Refresh();
            }
        }

        // ================= CONSTRUCTOR =================
        public LabelViewModel()
        {
            Labels = new ObservableCollection<MedicineLabel>();
            MedicinesView = CollectionViewSource.GetDefaultView(Labels);
            MedicinesView.Filter = FilterMedicines;

            _store = SuggestionStore.Load();
            Potencies = new ObservableCollection<string>(_store.Potencies);
            Doses = new ObservableCollection<string>(_store.Doses);
            Times = new ObservableCollection<string>(_store.Times);

            // 🔴 IMPORTANT: THIS WAS MISSING BEFORE
            UploadExcelCommand = new RelayCommand(_ => _ = UploadExcelAsync());

            PrintLabelCommand = new RelayCommand(_ => PrintLabel());
            EmitTsplTestCommand = new RelayCommand(_ =>
            {
                try
                {
                    var svc = new PrintService();
                    var ok = svc.EmitSimpleTsplTest();
                    Logging.AppLogger.Log($"EmitSimpleTsplTest invoked, result={ok}");
                }
                catch (System.Exception ex)
                {
                    Logging.AppLogger.Log(ex);
                }
            });
            ExportPdfCommand = new RelayCommand(param => ExportPdf(param));

            AddMedicineCommand = new RelayCommand(_ => AddMedicine());
            DeleteMedicineCommand = new RelayCommand(
                _ => DeleteMedicine(),
                _ => SelectedMedicine != null
            );

            OpenAdminDesignerCommand = new RelayCommand(
                _ => MessageBox.Show("Admin designer removed. Use the 'Preview' menu controls to edit label layout.", "Info", MessageBoxButton.OK, MessageBoxImage.Information),
                _ => SelectedMedicine != null
            );

            // Defaults
            Preview.MedicineName = "SELECT MEDICINE";
            // Load shop info from storage if present
            Preview.ShopName = Services.AppState.Storage.ShopName ?? "HOMEO MAHANAGAR";
            Preview.Phone = Services.AppState.Storage.Phone ?? "9007728468";

            // Load persisted medicines into Labels collection
            try
            {
                foreach (var m in Services.AppState.Storage.Medicines ?? new System.Collections.Generic.List<Models.Medicine>())
                {
                    var lab = new MedicineLabel
                    {
                        LatinName = m.LatinName,
                        CommonName = m.CommonName
                    };
                    lab.PropertyChanged += Medicine_PropertyChanged;
                    Labels.Add(lab);
                }
                // select first medicine automatically so preview shows a name out-of-the-box
                if (Labels.Count > 0 && SelectedMedicine == null)
                {
                    SelectedMedicine = Labels[0];
                }
                // If no medicines persisted, try seeding from remedies.xlsx located next to the executable
                if (Labels.Count == 0)
                {
                    try
                    {
                        var basePath = AppDomain.CurrentDomain.BaseDirectory;
                        var candidates = new[] {
                            System.IO.Path.Combine(basePath, "remedies.xlsx"),
                            System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "remedies.xlsx")
                        };

                        foreach (var p in candidates)
                        {
                            if (System.IO.File.Exists(p))
                            {
                                var imported = Helpers.ExcelHelper.ReadExcel(p);
                                if (imported != null && imported.Count > 0)
                                {
                                    foreach (var im in imported)
                                    {
                                        im.PropertyChanged += Medicine_PropertyChanged;
                                        Labels.Add(im);
                                        // persist into storage model as well
                                        Services.AppState.Storage.Medicines.Add(new Models.Medicine { LatinName = im.LatinName, CommonName = im.CommonName });
                                    }
                                    Services.AppState.Save();
                                    break;
                                }
                            }
                        }

                        if (Labels.Count > 0 && SelectedMedicine == null)
                            SelectedMedicine = Labels[0];
                    }
                    catch (System.Exception ex)
                    {
                        Logging.AppLogger.Log(ex);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
            }

            // keep admin preview layout in sync whenever preview data changes
            Preview.PropertyChanged += Preview_PropertyChanged;

            // initial printer status check
            CheckPrinterStatus();
        }

        private void Preview_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                // always update admin layout when preview changes
                UpdateAdminLayout();

                if (e == null || string.IsNullOrWhiteSpace(e.PropertyName))
                    return;

                // capture suggestions when user edits the preview fields (Potency/Dose/Time)
                if (string.Equals(e.PropertyName, nameof(Preview.Potency), System.StringComparison.OrdinalIgnoreCase))
                {
                    SaveIfNew(Preview.Potency, Potencies, _store.Potencies);
                }
                else if (string.Equals(e.PropertyName, nameof(Preview.Dose), System.StringComparison.OrdinalIgnoreCase))
                {
                    SaveIfNew(Preview.Dose, Doses, _store.Doses);
                }
                else if (string.Equals(e.PropertyName, nameof(Preview.Time), System.StringComparison.OrdinalIgnoreCase))
                {
                    SaveIfNew(Preview.Time, Times, _store.Times);
                }
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
            }
        }

        private void CheckPrinterStatus()
        {
            try
            {
                PrinterAvailable = false;
                LabelSizeSelected = false;

                var server = new LocalPrintServer();
                foreach (var pq in server.GetPrintQueues())
                {
                    if (pq.Name.Equals(TargetPrinterName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        PrinterAvailable = true;
                        // check USB connection via WMI: look for USB printers with this name
                        try
                        {
                            PrinterUsbConnected = false;
                            var search = new ManagementObjectSearcher("SELECT * FROM Win32_Printer");
                            foreach (ManagementObject printer in search.Get())
                            {
                                var name = (printer["Name"] as string) ?? string.Empty;
                                var port = (printer["PortName"] as string) ?? string.Empty;
                                if (name.Equals(TargetPrinterName, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    // consider typical USB port names
                                    if (port.IndexOf("USB", System.StringComparison.OrdinalIgnoreCase) >= 0 || port.IndexOf("USB", System.StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        PrinterUsbConnected = true;
                                    }
                                    break;
                                }
                            }
                        }
                        catch { }
                        // inspect default page media size name if available
                        try
                        {
                            pq.Refresh();
                            var capabilities = pq.GetPrintCapabilities();
                            // look for a media size that matches approx 50mm x 30mm
                            var mediaList = capabilities.PageMediaSizeCapability;
                            if (mediaList != null)
                            {
                                foreach (var media in mediaList)
                                {
                                    if (media != null && media.Width.HasValue && media.Height.HasValue)
                                    {
                                        // Best-effort conversion: Width/Height provided in printer-specific units; try interpreting as DIPs
                                        double wMm = media.Width.Value * 25.4 / 96.0;
                                        double hMm = media.Height.Value * 25.4 / 96.0;
                                        if ((Math.Abs(wMm - 50.0) < 4.0 && Math.Abs(hMm - 30.0) < 4.0) || (Math.Abs(wMm - 30.0) < 4.0 && Math.Abs(hMm - 50.0) < 4.0))
                                        {
                                            LabelSizeSelected = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                        break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
            }
        }

        // ================= UPLOAD EXCEL =================
        private async System.Threading.Tasks.Task UploadExcelAsync()
        {
            Logging.AppLogger.Log("UploadExcelAsync: start");
            var dlg = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                IsBusy = true;
                OnPropertyChanged(nameof(IsBusy));

                var list = await System.Threading.Tasks.Task.Run(() => ExcelHelper.ReadExcel(dlg.FileName));

                Logging.AppLogger.Log($"UploadExcelAsync: read {list.Count} rows");

                // update existing Labels collection to keep bindings intact
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Labels.Clear();
                    foreach (var med in list)
                    {
                        med.PropertyChanged += Medicine_PropertyChanged;
                        Labels.Add(med);
                    }

                    MedicinesView.Refresh();
                });
                // Persist imported medicines so they are available on next application start
                PersistMedicines();
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
                MessageBox.Show("Failed to read Excel file: " + ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsBusy));
                Logging.AppLogger.Log("UploadExcelAsync: finished");
            }
        }

        // ================= ADD MEDICINE =================
        private void AddMedicine()
        {
            var window = new AddMedicineWindow
            {
                Owner = Application.Current.MainWindow
            };

            if (window.ShowDialog() != true)
                return;

            var med = new MedicineLabel
            {
                LatinName = window.LatinName,
                CommonName = window.CommonName
            };

            med.PropertyChanged += Medicine_PropertyChanged;
            Labels.Add(med);
            SelectedMedicine = med;
            PersistMedicines();
        }

        // ================= DELETE MEDICINE =================
        private void DeleteMedicine()
        {
            if (SelectedMedicine == null)
                return;

            Labels.Remove(SelectedMedicine);
            SelectedMedicine = null;
            PersistMedicines();
        }

        // ================= ADMIN DESIGNER (kept for compatibility) =================
        private void OpenAdminDesigner()
        {
            MessageBox.Show("Admin designer removed. Use the 'Preview' menu controls to edit label layout.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ================= SYNC PREVIEW =================
        private void SyncPreview()
        {
            try
            {
                if (SelectedMedicine == null)
                    return;

                // ensure we have a sensible display name (fall back to Latin or Common name)
                var name = SelectedMedicine.DisplayName;
                if (string.IsNullOrWhiteSpace(name))
                    name = SelectedMedicine.LatinName ?? SelectedMedicine.CommonName ?? string.Empty;

                name = name.ToUpperInvariant();
                // Do not mutate the model's DisplayName here to avoid event recursion.
                Preview.MedicineName = name;
                Preview.Potency = SelectedMedicine.Potency;
                Preview.Dose = SelectedMedicine.Dose;
                Preview.Time = SelectedMedicine.Time;

                UpdateAdminLayout();
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
            }
        }

        // update the shared admin layout items with current preview values
        private void UpdateAdminLayout()
        {
            try
            {
                var layout = Services.AppState.Storage.AdminLayout;
                if (layout == null)
                    return;

                // Ensure admin layout has exactly five lines; initialize missing entries and remove extras
                var defaults = new[]
                {
                    // use logical keys so preview resolves dynamic text
                    new LabelCanvasItem { Text = "MedicineName", X = 86, Y = 6, FontSize = 14, Alignment = System.Windows.TextAlignment.Center, ZIndex = 0 },
                    new LabelCanvasItem { Text = "Potency", X = 86, Y = 26, FontSize = 11, Alignment = System.Windows.TextAlignment.Center, ZIndex = 0 },
                    new LabelCanvasItem { Text = "Dose", X = 86, Y = 42, FontSize = 10, Alignment = System.Windows.TextAlignment.Center, ZIndex = 0 },
                    new LabelCanvasItem { Text = "Time", X = 86, Y = 52, FontSize = 10, Alignment = System.Windows.TextAlignment.Center, ZIndex = 0 },
                    new LabelCanvasItem { Text = "ShopAndPhone", X = 86, Y = 70, FontSize = 10, Alignment = System.Windows.TextAlignment.Center, ZIndex = 0 }
                };

                // add missing entries
                for (int i = layout.Count; i < 5; i++)
                    layout.Add(defaults[i]);

                // remove any extras beyond 5
                while (layout.Count > 5)
                    layout.RemoveAt(layout.Count - 1);
                // Line mapping (expected): 0=Medicine Name, 1=Potency/overflow, 2=Dose, 3=Time, 4=Shop+Phone
                // If preview medicine name is empty, fall back to selected medicine fields to avoid blank line in admin preview.
                var medName = (Preview.MedicineName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(medName) && SelectedMedicine != null)
                {
                    medName = (SelectedMedicine.LatinName ?? SelectedMedicine.CommonName ?? string.Empty).Trim();
                }

                // Use LabelTextComposer to compute wrapping and potency merge
                try
                {
                    // default label width in pixels for admin canvas preview (189 px ≈ 50mm at typical DPI)
                    // preview canvas inner width in DIPs (approx 189 for 50mm at 96 DPI)
                    var composer = new LabelTextComposer(189);
                    var composed = composer.Compose(medName, Preview.Potency ?? string.Empty, Preview.Dose ?? string.Empty, Preview.Time ?? string.Empty, Preview.ShopAndPhone ?? string.Empty);

                    // Keep the item.Text as logical keys; preview will resolve their displayed text
                    layout[0].Text = "MedicineName";
                    layout[1].Text = "Potency";
                    layout[2].Text = "Dose";
                    layout[3].Text = "Time";
                    layout[4].Text = "ShopAndPhone";

                    // Apply professional sizing and spacing per line (all normal weight)
                    layout[0].FontSize = 14; layout[0].Alignment = System.Windows.TextAlignment.Center; layout[0].Y = 6;
                    layout[1].FontSize = 11; layout[1].Alignment = System.Windows.TextAlignment.Center; layout[1].Y = 26;
                    layout[2].FontSize = 10; layout[2].Alignment = System.Windows.TextAlignment.Center; layout[2].Y = 42;
                    layout[3].FontSize = 10; layout[3].Alignment = System.Windows.TextAlignment.Center; layout[3].Y = 52;
                    layout[4].FontSize = 10; layout[4].Alignment = System.Windows.TextAlignment.Center; layout[4].Y = 70;
                }
                catch
                {
                    // fallback to simple assignment on error
                    layout[0].Text = medName.ToUpperInvariant();
                    layout[1].Text = (Preview.Potency ?? string.Empty).ToUpperInvariant();
                    layout[2].Text = (Preview.Dose ?? string.Empty).ToUpperInvariant();
                    layout[3].Text = (Preview.Time ?? string.Empty).ToUpperInvariant();
                    layout[4].Text = (Preview.ShopAndPhone ?? string.Empty).ToUpperInvariant();
                }

                // Persist shop name and phone into storage so it is loaded next run
                try
                {
                    Services.AppState.Storage.ShopName = Preview.ShopName;
                    Services.AppState.Storage.Phone = Preview.Phone;
                    Services.AppState.Save();
                }
                catch (System.Exception ex)
                {
                    Logging.AppLogger.Log(ex);
                }

                // diagnostic log for debugging preview issues
                try
                {
                    Logging.AppLogger.Log($"UpdateAdminLayout: Med='{layout[0].Text}', Pot='{layout[1].Text}', Dose='{layout[2].Text}', Time='{layout[3].Text}', Shop='{layout[4].Text}'");
                }
                catch { }
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
            }
        }

        // ================= FILTER =================
        private bool FilterMedicines(object obj)
        {
            if (obj is not MedicineLabel med)
                return false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            return (med.LatinName?.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ?? false)
                || (med.CommonName?.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ?? false);
        }

        // ================= AUTOSAVE SUGGESTIONS =================
        private void Medicine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not MedicineLabel med)
                return;
            try
            {
                // Only respond to changes on Potency/Dose/Time to avoid IO on unrelated property changes
                if (string.Equals(e?.PropertyName, nameof(MedicineLabel.Potency), System.StringComparison.OrdinalIgnoreCase))
                {
                    SaveIfNew(med.Potency, Potencies, _store.Potencies);
                }
                else if (string.Equals(e?.PropertyName, nameof(MedicineLabel.Dose), System.StringComparison.OrdinalIgnoreCase))
                {
                    SaveIfNew(med.Dose, Doses, _store.Doses);
                }
                else if (string.Equals(e?.PropertyName, nameof(MedicineLabel.Time), System.StringComparison.OrdinalIgnoreCase))
                {
                    SaveIfNew(med.Time, Times, _store.Times);
                }

                // If the changed medicine is currently selected and the change affects preview, update the live preview
                if (med == SelectedMedicine && (e == null || e.PropertyName == nameof(MedicineLabel.Potency) || e.PropertyName == nameof(MedicineLabel.Dose) || e.PropertyName == nameof(MedicineLabel.Time)))
                {
                    SyncPreview();
                }
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
            }
            // persist medicines list when basic fields change (latin/common)
            if (e != null && (e.PropertyName == nameof(MedicineLabel.LatinName) || e.PropertyName == nameof(MedicineLabel.CommonName)))
            {
                PersistMedicines();
            }
        }

        private void PersistMedicines()
        {
            try
            {
                Services.AppState.Storage.Medicines = new System.Collections.Generic.List<Models.Medicine>();
                foreach (var m in Labels)
                {
                    Services.AppState.Storage.Medicines.Add(new Models.Medicine { LatinName = m.LatinName, CommonName = m.CommonName });
                }
                Services.AppState.Save();
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
            }
        }

        private void SaveIfNew(
            string? value,
            ObservableCollection<string> uiList,
            System.Collections.Generic.List<string> storeList)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            value = value.ToUpperInvariant();

            if (!uiList.Contains(value))
            {
                uiList.Add(value);
                storeList.Add(value);
                try
                {
                    _store.Save();
                    Logging.AppLogger.Log($"SuggestionStore: added '{value}' and saved.");
                }
                catch (System.Exception ex)
                {
                    Logging.AppLogger.Log(ex);
                }
            }
        }

        // ================= PRINT =================
        private void PrintLabel()
        {
            try
            {
                // Create a PrintLabelView and render current admin layout into it
                var items = Services.AppState.Storage.AdminLayout;
                if (items == null)
                {
                    MessageBox.Show("No label layout available.", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var printView = new HomeoMahanagarLabelCleanV2.Views.PrintLabelView();
                double widthMm = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelWidthMm;
                double heightMm = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelHeightMm;
                double dipW = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.MmToDip(widthMm);
                double dipH = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.MmToDip(heightMm);
                printView.Width = dipW; printView.Height = dipH;

                // Build composed/render items (resolve logical keys like "MedicineName" -> actual text)
                var composer = new HomeoMahanagarLabelCleanV2.Services.LabelTextComposer(189);
                var composed = composer.Compose(Preview.MedicineName ?? string.Empty, Preview.Potency ?? string.Empty, Preview.Dose ?? string.Empty, Preview.Time ?? string.Empty, Preview.ShopAndPhone ?? string.Empty);

                var renderItems = new System.Collections.Generic.List<HomeoMahanagarLabelCleanV2.Models.LabelCanvasItem>();
                for (int i = 0; i < 5; i++)
                {
                    var src = items.Count > i ? items[i] : new HomeoMahanagarLabelCleanV2.Models.LabelCanvasItem();
                    var li = new HomeoMahanagarLabelCleanV2.Models.LabelCanvasItem()
                    {
                        Text = (i < composed.Length ? composed[i] : string.Empty),
                        X = src.X,
                        Y = src.Y,
                        FontSize = src.FontSize > 0 ? src.FontSize : 9.0,
                        Alignment = src.Alignment,
                        ZIndex = src.ZIndex
                    };
                    renderItems.Add(li);
                }

                // Render using composed items so preview/PNG/PDF match on-screen preview
                printView.RenderItems(renderItems);
                printView.Measure(new Size(dipW, dipH));
                printView.Arrange(new Rect(0, 0, dipW, dipH));

                // Render PNG for preview (no printing here)
                byte[] png = null;
                try
                {
                    // Prefer the on-screen preview element so the preview window matches exactly
                    var previewElement = FindPreviewElement();
                    if (previewElement != null)
                    {
                        png = HomeoMahanagarLabelCleanV2.Helpers.PdfHelper.RenderElementToPngBytes(previewElement, widthMm, heightMm, 300.0);
                    }
                    else
                    {
                        png = HomeoMahanagarLabelCleanV2.Helpers.PdfHelper.RenderElementToPngBytes(printView, widthMm, heightMm, 300.0);
                    }
                }
                catch (System.Exception ex)
                {
                    HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(ex);
                }

                // Show preview window so user can select a printer
                var preview = new HomeoMahanagarLabelCleanV2.Views.PrintPreviewWindow(png ?? new byte[0]) { Owner = Application.Current?.MainWindow };
                var res = preview.ShowDialog();
                try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log($"LabelViewModel: preview closed; result={res}, selectedPrinter={(preview?.SelectedPrintQueue?.Name ?? "(null)")}, pngSize={(png?.Length ?? 0)}"); } catch { }
                if (res != true || preview.SelectedPrintQueue == null)
                    return; // user cancelled or didn't select

                // Call centralized print service (no UI printing here)
                var svc = new HomeoMahanagarLabelCleanV2.Services.PrintService();
                // Prefer using the on-screen preview element for printing so preview == printed output
                var previewElementForPrint = FindPreviewElement();
                if (previewElementForPrint != null)
                {
                    svc.PrintLabel(previewElementForPrint, renderItems, preview.SelectedPrintQueue, widthMm, heightMm);
                }
                else
                {
                    // fallback to off-screen rendered visual
                    svc.PrintLabel(renderItems, preview.SelectedPrintQueue, widthMm, heightMm);
                }
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
                MessageBox.Show("Printing failed: " + ex.Message, "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================= EXPORT PDF =================
        private void ExportPdf(object? parameter)
        {
            try
            {
                FrameworkElement? previewElement = null;

                // prefer element passed from view (named control)
                if (parameter is FrameworkElement feParam)
                    previewElement = feParam;

                if (previewElement == null)
                    previewElement = FindPreviewElement();

                if (previewElement == null)
                {
                    MessageBox.Show("Preview control not found.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var dlg = new Microsoft.Win32.SaveFileDialog()
                {
                    Filter = "PDF File (*.pdf)|*.pdf",
                    FileName = $"Label_{System.DateTime.Now:yyyyMMdd_HHmmss}.pdf"
                };

                if (dlg.ShowDialog() != true)
                    return;

                try
                {
                    // Instead of rasterizing the on-screen preview (which may include UI chrome),
                    // build an off-screen PrintLabelView with the composed items and export that.
                    var items = Services.AppState.Storage.AdminLayout;
                    if (items == null)
                    {
                        MessageBox.Show("No label layout available.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Compose logical lines (medicine/potency/dose/time/shop) so exported PDF matches preview text
                    var composer = new HomeoMahanagarLabelCleanV2.Services.LabelTextComposer(189);
                    var composed = composer.Compose(Preview.MedicineName ?? string.Empty, Preview.Potency ?? string.Empty, Preview.Dose ?? string.Empty, Preview.Time ?? string.Empty, Preview.ShopAndPhone ?? string.Empty);

                    var renderItems = new System.Collections.Generic.List<HomeoMahanagarLabelCleanV2.Models.LabelCanvasItem>();
                    for (int i = 0; i < 5; i++)
                    {
                        var src = items.Count > i ? items[i] : new HomeoMahanagarLabelCleanV2.Models.LabelCanvasItem();
                        var li = new HomeoMahanagarLabelCleanV2.Models.LabelCanvasItem()
                        {
                            Text = (i < composed.Length ? composed[i] : string.Empty),
                            X = src.X,
                            Y = src.Y,
                            FontSize = src.FontSize > 0 ? src.FontSize : 9.0,
                            Alignment = src.Alignment,
                            ZIndex = src.ZIndex
                        };
                        renderItems.Add(li);
                    }

                    var printView = new HomeoMahanagarLabelCleanV2.Views.PrintLabelView();
                    double widthMm = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelWidthMm;
                    double heightMm = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelHeightMm;
                    double dipW = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.MmToDip(widthMm);
                    double dipH = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.MmToDip(heightMm);
                    printView.Width = dipW; printView.Height = dipH;
                    printView.RenderItems(renderItems);
                    try { printView.Measure(new Size(dipW, dipH)); printView.Arrange(new Rect(0, 0, dipW, dipH)); printView.UpdateLayout(); } catch { }

                    var exportVector = Preview.ExportVectorPdf;
                    var fontFamily = Preview.FontFamily;
                    var isBold = Preview.IsBold;
                    var fontSize = Preview.FontSize;
                    var lineSpacing = Preview.LineSpacing;

                    HomeoMahanagarLabelCleanV2.Helpers.PdfHelper.ExportLabelToPdf(printView, dlg.FileName, exportVector, fontFamily, isBold, fontSize, lineSpacing);
                    MessageBox.Show("PDF exported to " + dlg.FileName, "Export PDF", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logging.AppLogger.Log(ex);
                    MessageBox.Show("Export failed: " + ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
                MessageBox.Show(ex.ToString(), "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FrameworkElement? FindPreviewElement()
        {
            try
            {
                var windows = Application.Current?.Windows;
                if (windows != null)
                {
                    // search across all application windows for 'PreviewCanvas' by name
                    foreach (Window w in windows)
                    {
                        var foundByName = FindInVisualTreeByName<FrameworkElement>(w, "PreviewCanvas");
                        if (foundByName != null)
                            return foundByName;
                    }

                    // fallback: find any element whose DataContext is the LabelPreviewViewModel instance
                    foreach (Window w in windows)
                    {
                        var found = FindInVisualTree<FrameworkElement>(w, (fe) => object.ReferenceEquals(fe.DataContext, Preview));
                        if (found != null)
                            return found;
                    }

                    // last resort: find any Canvas with approximate preview size
                    foreach (Window w in windows)
                    {
                        var canv = FindInVisualTree<Canvas>(w, (c) =>
                            ((c.Width == 189 && c.Height == 113) || (double.IsNaN(c.Width) == false && double.IsNaN(c.Height) == false && c.ActualWidth > 0)));
                        if (canv != null) return canv as FrameworkElement;
                    }
                }
            }
            catch (System.Exception ex)
            {
                try { Logging.AppLogger.Log(ex); } catch { }
            }

            return null;
        }

        private T? FindInVisualTreeByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t && t.Name == name)
                    return t;

                var result = FindInVisualTreeByName<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        private T? FindInVisualTree<T>(DependencyObject parent, System.Func<T, bool> predicate) where T : FrameworkElement
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t && predicate(t))
                    return t;

                var result = FindInVisualTree<T>(child, predicate);
                if (result != null)
                    return result;
            }
            return null;
        }

        // ================= NOTIFY =================
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
