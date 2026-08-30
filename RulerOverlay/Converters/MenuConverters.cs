using System;
using System.Globalization;
using System.Windows.Data;

namespace RulerOverlay.Converters
{
    /// <summary>
    /// True when the bound value equals the converter parameter.
    /// Drives the check marks on the unit, colour, opacity and rotation menu items;
    /// comparison is done on the string form so one converter covers all of them.
    /// </summary>
    public class EqualsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            var left = System.Convert.ToString(value, CultureInfo.InvariantCulture);
            var right = System.Convert.ToString(parameter, CultureInfo.InvariantCulture);

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>
    /// Picks one of two strings based on a boolean, so a menu item can read
    /// "Enable X" or "Disable X" from a single toggle property.
    /// The parameter carries both captions as "whenFalse|whenTrue".
    /// </summary>
    public class BoolToTextConverter : IValueConverter
    {
        private const char Separator = '|';

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var options = parameter?.ToString()?.Split(Separator);
            if (options is not { Length: 2 })
                return string.Empty;

            bool isTrue = value is bool flag && flag;
            return isTrue ? options[1] : options[0];
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
