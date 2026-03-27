using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Medabots.Rom.WPFEditor.Models;

public sealed class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is System.Windows.Media.Color color
            ? new SolidColorBrush(color)
            : System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
