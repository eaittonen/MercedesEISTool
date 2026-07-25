using System;
using Avalonia.Data.Converters;

namespace MercedesEISTool.GUI.Converters;

public sealed class BoolToCreateSaveConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool isCreating && isCreating ? "Create" : "Save";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
