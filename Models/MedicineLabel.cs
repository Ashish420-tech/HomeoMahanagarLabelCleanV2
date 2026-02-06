using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HomeoMahanagarLabelCleanV2.Models
{
    public class MedicineLabel : INotifyPropertyChanged
    {
        private string _latinName;
        private string _commonName;
        private string _potency;
        private string _dose;
        private string _time;
        private string _displayName;
        public string DisplayName
        {
            get => _displayName;
            set
            {
                // Allow setting DisplayName but avoid triggering propertychanged if value hasn't changed
                if (_displayName == value) return;
                _displayName = value;
                OnPropertyChanged();
            }
        }

        public string LatinName
        {
            get => _latinName;
            set
            {
                _latinName = value;
                OnPropertyChanged();
            }
        }

        public string CommonName
        {
            get => _commonName;
            set
            {
                _commonName = value;
                OnPropertyChanged();
            }
        }

        // 🔥 AUTO-UPPERCASE
        public string Potency
        {
            get => _potency;
            set
            {
                _potency = value?.ToUpper();
                OnPropertyChanged();
            }
        }

        // 🔥 AUTO-UPPERCASE
        public string Dose
        {
            get => _dose;
            set
            {
                _dose = value?.ToUpper();
                OnPropertyChanged();
            }
        }

        // 🔥 AUTO-UPPERCASE
        public string Time
        {
            get => _time;
            set
            {
                _time = value?.ToUpper();
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
