using HomeoMahanagarLabelCleanV2.ViewModels;
using HomeoMahanagarLabelCleanV2.Views;
using System.Windows;

namespace HomeoMahanagarLabelCleanV2
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            try
            {
                HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log("MainWindow: constructor start"); // Log constructor start
                InitializeComponent();
                HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log("MainWindow: InitializeComponent complete"); // Log component initialization

                // Create DataContext after InitializeComponent to isolate XAML instantiation issues
                try
                {
                    var vm = new LabelViewModel();
                    DataContext = vm;
                    HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log("MainWindow: DataContext set (created in code)"); // Log DataContext setting
                }
                catch (System.Exception ex)
                {
                    HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(ex);
                    throw;
                }
            }
            catch (System.Exception ex)
            {
                try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(ex); } catch { }
                MessageBox.Show("Startup error: " + ex.Message + "\nSee log files for details.", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void Admin_Click(object sender, RoutedEventArgs e)
        {
            // Admin designer removed; show info and rely on Preview menu controls
            MessageBox.Show("Admin designer removed. Use the 'Preview' menu controls to edit label layout.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
