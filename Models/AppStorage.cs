using System.Collections.Generic;

namespace HomeoMahanagarLabelCleanV2.Models
{
    public class AppStorage
    {
        public List<Medicine> Medicines { get; set; } = new();
        public List<string> Potencies { get; set; } = new();
        public List<string> Doses { get; set; } = new();
        public List<string> Times { get; set; } = new();

        public string ShopName { get; set; }
        public string Phone { get; set; }
        public System.Collections.ObjectModel.ObservableCollection<LabelCanvasItem> AdminLayout { get; set; } = new();
        public string LabelPrinterName { get; set; }
        // Optional per-printer tuning: DPI (dots per inch) and padding in DIPs
        // Default DPI 203 is common for SNBC/TVS label printers.
        public int LabelPrinterDpi { get; set; } = 203;
        public double LabelPaddingDip { get; set; } = HomeoMahanagarLabelCleanV2.Helpers.PrintConstants.LabelPaddingDip;
    }
}
