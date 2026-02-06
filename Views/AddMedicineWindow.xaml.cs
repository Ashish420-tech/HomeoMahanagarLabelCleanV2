using System.Windows;

namespace HomeoMahanagarLabelCleanV2.Views
{
    public partial class AddMedicineWindow : Window
    {
        public string LatinName { get; private set; }
        public string CommonName { get; private set; }

        public AddMedicineWindow()
        {
            InitializeComponent();
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            LatinName = LatinBox.Text?.Trim();
            CommonName = CommonBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(LatinName))
            {
                MessageBox.Show("Latin Name is required");
                return;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
