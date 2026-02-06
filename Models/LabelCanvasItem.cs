using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows;

namespace HomeoMahanagarLabelCleanV2.Models
{
    public class LabelCanvasItem : INotifyPropertyChanged
    {
        private string _text = "Text";
    private double _x;
    private double _y;
    private double _fontSize = 9;
    private TextAlignment _alignment = TextAlignment.Left;
    private int _zIndex;
    private const double EPSILON = 0.01;

    public string Text
    {
        get => _text;
        set
        {
            var newVal = (value ?? string.Empty).ToUpperInvariant();
            if (_text == newVal) return;
            _text = newVal;
            OnPropertyChanged();
        }
    }

    public double X
    {
        get => _x;
        set
        {
            if (Math.Abs(_x - value) < EPSILON) return;
            _x = value;
            OnPropertyChanged();
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            if (Math.Abs(_y - value) < EPSILON) return;
            _y = value;
            OnPropertyChanged();
        }
    }

    public double FontSize
    {
        get => _fontSize;
        set
        {
            if (Math.Abs(_fontSize - value) < EPSILON) return;
            _fontSize = value;
            OnPropertyChanged();
        }
    }

    // Boldness removed: all text rendered with normal weight by design

    public TextAlignment Alignment
    {
        get => _alignment;
        set
        {
            if (_alignment == value) return;
            _alignment = value;
            OnPropertyChanged();
        }
    }

    public int ZIndex
    {
        get => _zIndex;
        set
        {
            if (_zIndex == value) return;
            _zIndex = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

}
}
