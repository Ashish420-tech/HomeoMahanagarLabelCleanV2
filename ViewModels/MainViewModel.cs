using HomeoMahanagarLabelCleanV2.Commands;
using HomeoMahanagarLabelCleanV2.Models;
using HomeoMahanagarLabelCleanV2.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;

namespace HomeoMahanagarLabelCleanV2.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly MedicineService _medicineService;

        public ObservableCollection<Medicine> Medicines { get; set; }
        public ICollectionView MedicinesView { get; set; }

        private Medicine _selectedMedicine;
        public Medicine SelectedMedicine
        {
            get => _selectedMedicine;
            set
            {
                _selectedMedicine = value;
                OnPropertyChanged(nameof(SelectedMedicine));

                // 🔴 ADDED: feed medicine name into preview pipeline
                SelectedMedicineName = _selectedMedicine?.LatinName;
                UpdatePreview();
            }
        }

        // 🔴 ADDED: selected medicine name for label preview
        private string _selectedMedicineName;
        public string SelectedMedicineName
        {
            get => _selectedMedicineName;
            set
            {
                _selectedMedicineName = value;
                OnPropertyChanged(nameof(SelectedMedicineName));
                UpdatePreview();
            }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                MedicinesView.Refresh();
            }
        }

        // ===============================
        // 🔴 ADDED: LABEL INPUT PROPERTIES
        // ===============================

        private string _potency;
        public string Potency
        {
            get => _potency;
            set
            {
                _potency = value;
                OnPropertyChanged(nameof(Potency));
                UpdatePreview();
            }
        }

        private string _dose;
        public string Dose
        {
            get => _dose;
            set
            {
                _dose = value;
                OnPropertyChanged(nameof(Dose));
                UpdatePreview();
            }
        }

        private string _time;
        public string Time
        {
            get => _time;
            set
            {
                _time = value;
                OnPropertyChanged(nameof(Time));
                UpdatePreview();
            }
        }

        private string _shopName;
        public string ShopName
        {
            get => _shopName;
            set
            {
                _shopName = value;
                OnPropertyChanged(nameof(ShopName));
                UpdatePreview();
            }
        }

        private string _branchPhone;
        public string BranchPhone
        {
            get => _branchPhone;
            set
            {
                _branchPhone = value;
                OnPropertyChanged(nameof(BranchPhone));
                UpdatePreview();
            }
        }

        // ===============================
        // 🔴 ADDED: PREVIEW OUTPUT
        // ===============================

        private string[] _previewLines = new string[5];
        public string[] PreviewLines
        {
            get => _previewLines;
            set
            {
                _previewLines = value;
                OnPropertyChanged(nameof(PreviewLines));
            }
        }

        public ICommand AddMedicineCommand { get; }
        public ICommand DeleteMedicineCommand { get; }

        public MainViewModel()
        {
            _medicineService = new MedicineService();

            Medicines = new ObservableCollection<Medicine>(
                _medicineService.LoadMedicines());

            MedicinesView = CollectionViewSource.GetDefaultView(Medicines);
            MedicinesView.Filter = FilterMedicines;

            AddMedicineCommand = new RelayCommand(_ => AddMedicine());
            DeleteMedicineCommand = new RelayCommand(
                _ => DeleteMedicine(),
                _ => SelectedMedicine != null);

            // 🔴 ADDED: initialize preview safely
            UpdatePreview();
        }

        private bool FilterMedicines(object obj)
        {
            if (obj is not Medicine med) return false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            return med.LatinName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || med.CommonName.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        private void AddMedicine()
        {
            Medicines.Add(new Medicine
            {
                LatinName = "New Latin Name",
                CommonName = "New Common Name"
            });

            Save();
        }

        private void DeleteMedicine()
        {
            if (SelectedMedicine == null) return;

            Medicines.Remove(SelectedMedicine);
            Save();
        }

        private void Save()
        {
            _medicineService.SaveMedicines(Medicines);
        }

        // ===============================
        // 🔴 ADDED: CORE PREVIEW LOGIC
        // ===============================
        private void UpdatePreview()
        {
            if (string.IsNullOrWhiteSpace(SelectedMedicineName))
            {
                PreviewLines = new string[5];
                return;
            }

            var composer = new LabelTextComposer(labelWidthPx: 189); // 50mm label

            PreviewLines = composer.Compose(
           SelectedMedicine?.LatinName,
           Potency,
           Dose,
           Time,
           $"{ShopName} - {BranchPhone}"
            );

        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
