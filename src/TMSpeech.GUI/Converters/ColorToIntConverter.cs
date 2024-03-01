using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace TMSpeech.GUI.Converters;

public class IntToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int || value is uint || value is long)
        {
            var intValue = (uint)value;
            return Color.FromUInt32(intValue);
        }

        return Colors.Black;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color color)
        {
            return color.ToUint32();
        }

        return 0;
    }
}