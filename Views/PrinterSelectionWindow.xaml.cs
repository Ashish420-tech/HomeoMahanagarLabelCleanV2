using System.Windows;
using System.Printing;

namespace HomeoMahanagarLabelCleanV2.Views
{
    public partial class PrinterSelectionWindow : Window
    {
        public PrinterSelectionWindow()
        {
            InitializeComponent();
            var server = new LocalPrintServer();
            foreach (var pq in server.GetPrintQueues())
            {
                PrinterCombo.Items.Add(pq.Name);
            }

            // select current if set
            string current = HomeoMahanagarLabelCleanV2.Services.AppState.Storage.LabelPrinterName;
            if (!string.IsNullOrWhiteSpace(current))
                PrinterCombo.SelectedItem = current;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PrinterCombo.SelectedItem is string name)
                {
                    HomeoMahanagarLabelCleanV2.Helpers.RawPrinterHelper.SetLabelPrinterName(name);
                }
                this.DialogResult = true;
                this.Close();
            }
            catch (System.Exception ex)
            {
                try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(ex); } catch { }
                MessageBox.Show("Failed to save printer selection: " + ex.Message, "Printer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
