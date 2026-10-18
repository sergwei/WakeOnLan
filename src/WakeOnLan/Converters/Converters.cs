using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WakeOnLan.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value is true) ^ Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class EmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class LastSeenConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime lastSeen)
            return "Nunca visto en línea";

        var elapsed = DateTime.Now - lastSeen;
        if (elapsed < TimeSpan.FromMinutes(1))
            return "Visto ahora mismo";
        if (elapsed < TimeSpan.FromHours(1))
            return $"Visto hace {(int)elapsed.TotalMinutes} min";
        if (elapsed < TimeSpan.FromDays(1))
            return $"Visto hace {(int)elapsed.TotalHours} h";
        if (elapsed < TimeSpan.FromDays(7))
            return $"Visto hace {(int)elapsed.TotalDays} d";
        // Invariante: en un formato personalizado "/" es el separador de la cultura ("." en alemán).
        return "Visto el " + lastSeen.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
