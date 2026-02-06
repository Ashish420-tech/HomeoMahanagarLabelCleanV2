using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using HomeoMahanagarLabelCleanV2.Models;
using HomeoMahanagarLabelCleanV2.Services;
using HomeoMahanagarLabelCleanV2.Logging;
using System.Windows;
using System.Windows.Input;
using HomeoMahanagarLabelCleanV2.Commands;

namespace HomeoMahanagarLabelCleanV2.ViewModels
{
    public class LabelPreviewViewModel : INotifyPropertyChanged
    {
        private string _medicineName;
        private string _potency;
        private string _dose;
        private string _time;
        private string _shopName;
        private string _phone;

        public string MedicineName
        {
            get => _medicineName;
            set
            {
                _medicineName = value;
                OnPropertyChanged();
            }
        }

        // User-controllable rendering properties
        private double _fontSize = 9.0;
        private double _lineSpacing = 2.0;
        private bool _isBold = false;
        private string _fontFamily = "Segoe UI";
        private bool _exportVectorPdf = true;

        public double FontSize
        {
            get => _fontSize;
            set { _fontSize = value; OnPropertyChanged(); }
        }

        public double LineSpacing
        {
            get => _lineSpacing;
            set { _lineSpacing = value; OnPropertyChanged(); }
        }

        public bool IsBold
        {
            get => _isBold;
            set { _isBold = value; OnPropertyChanged(); }
        }

        public string FontFamily
        {
            get => _fontFamily;
            set { _fontFamily = value; OnPropertyChanged(); }
        }

        public bool ExportVectorPdf
        {
            get => _exportVectorPdf;
            set { _exportVectorPdf = value; OnPropertyChanged(); }
        }

        public string Potency
        {
            get => _potency;
            set
            {
                _potency = value;
                OnPropertyChanged();
            }
        }

        public string Dose
        {
            get => _dose;
            set
            {
                _dose = value;
                OnPropertyChanged();
            }
        }

        public string Time
        {
            get => _time;
            set
            {
                _time = value;
                OnPropertyChanged();
            }
        }

        public string ShopName
        {
            get => _shopName;
            set
            {
                _shopName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShopAndPhone));
            }
        }

        public string Phone
        {
            get => _phone;
            set
            {
                _phone = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShopAndPhone));
            }
        }

        // 👉 SINGLE LINE FOR LABEL (SHOP + PHONE)
        public string ShopAndPhone
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ShopName) &&
                    string.IsNullOrWhiteSpace(Phone))
                    return string.Empty;

                return $"{ShopName} · {Phone}".ToUpper();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // Command to print the current AdminLayout using PrintService
        public ICommand PrintCommand { get; }

        public LabelPreviewViewModel()
        {
            PrintCommand = new RelayCommand(() =>
            {
                try
                {
                    var svc = new PrintService();
                    svc.PrintLabel(AdminLayout, widthMm: 50, heightMm: 28);
                }
                catch (System.Exception ex)
                {
                    try { AppLogger.Log(ex); } catch { }
                    try { DiagnosticsService.Capture(ex); } catch { }
                    MessageBox.Show("Printing failed: " + ex.Message, "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        // Admin layout is provided by the application storage (live)
        public System.Collections.ObjectModel.ObservableCollection<LabelCanvasItem> AdminLayout => AppState.Storage.AdminLayout;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
