using HomeoMahanagarLabelCleanV2.Commands;
using HomeoMahanagarLabelCleanV2.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace HomeoMahanagarLabelCleanV2.ViewModels
{
    public class AdminLabelDesignerViewModel : ViewModelBase
    {
        // Use the shared AdminLayout collection from application storage so changes are live
        public ObservableCollection<LabelCanvasItem> Items => Services.AppState.Storage.AdminLayout;

        private LabelCanvasItem? _selectedItem;
        public LabelCanvasItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        // Fixed layout: five logical lines (non-removable)
        public ICommand BringForwardCommand { get; }
        public ICommand SendBackwardCommand { get; }
        public ICommand SaveLayoutCommand { get; }
        public ICommand LoadLayoutCommand { get; }
        public ICommand PrintLayoutCommand { get; }
        // Font size is fixed to 9 by design for repeatable thermal printing.

        public AdminLabelDesignerViewModel()
        {
            // ensure defaults exist when storage is empty
            if (Services.AppState.Storage.AdminLayout == null || Services.AppState.Storage.AdminLayout.Count == 0)
            {
                // set defaults using preview-canvas coordinates (Canvas: 173x97)
                Services.AppState.Storage.AdminLayout = new System.Collections.ObjectModel.ObservableCollection<LabelCanvasItem>
                {
                    new LabelCanvasItem { Text = "MEDICINE NAME", X = 86, Y = 6, FontSize = 9, Alignment = System.Windows.TextAlignment.Center, ZIndex = 0 },
                    new LabelCanvasItem { Text = "MEDICINE NAME (CONT.) + POTENCY", X = 86, Y = 22, FontSize = 9, Alignment = System.Windows.TextAlignment.Center, ZIndex = 0 },
                    new LabelCanvasItem { Text = "DOSE", X = 86, Y = 38, FontSize = 9, Alignment = System.Windows.TextAlignment.Center, ZIndex = 0 },
                    new LabelCanvasItem { Text = "TIME", X = 86, Y = 54, FontSize = 9, Alignment = System.Windows.TextAlignment.Center, ZIndex = 0 },
                    new LabelCanvasItem { Text = "SHOP NAME", X = 86, Y = 70, FontSize = 9, Alignment = System.Windows.TextAlignment.Center, ZIndex = 0 }
                };
                Services.AppState.Save();
            }

            SelectedItem = Items.FirstOrDefault();

            BringForwardCommand = new RelayCommand(_ =>
            {
                if (SelectedItem != null)
                    SelectedItem.ZIndex++;
            });

            SendBackwardCommand = new RelayCommand(_ =>
            {
                if (SelectedItem != null)
                    SelectedItem.ZIndex--;
            });

            SaveLayoutCommand = new RelayCommand(_ => SaveLayout());
            LoadLayoutCommand = new RelayCommand(_ => LoadLayout());
            PrintLayoutCommand = new RelayCommand(_ => PrintLayout());
        }


        private void SaveLayout()
        {
            try
            {
                // Items is the shared AdminLayout collection and already contains current positions.
                // Simply persist application storage to save the current layout.
                Services.AppState.Save();
                MessageBox.Show("Layout saved to application storage.", "Save Layout", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
                MessageBox.Show("Failed to save layout: " + ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintLayout()
        {
            try
            {
                var svc = new HomeoMahanagarLabelCleanV2.Services.PrintService();
                svc.PrintLabel(Items);
            }
            catch (System.Printing.PrintSystemException pse)
            {
                Logging.AppLogger.Log(pse);
                MessageBox.Show("Printer error:\n" + pse.Message, "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
                MessageBox.Show("Unexpected print error:\n" + ex.Message, "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadLayout()
        {
            try
            {
                // Items points to the shared AdminLayout; reload is a no-op if storage is already in memory.
                // Ensure UI reflects the current stored layout.
                var stored = Services.AppState.Storage.AdminLayout;
                if (stored != null)
                {
                    Items.Clear();
                    foreach (var item in stored)
                        Items.Add(item);
                }

                SelectedItem = Items.FirstOrDefault();
                MessageBox.Show("Layout loaded from application storage.", "Load Layout", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
                MessageBox.Show("Failed to load layout: " + ex.Message, "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
