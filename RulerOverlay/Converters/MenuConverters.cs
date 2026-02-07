using System;
using System.Globalization;
using System.Windows.Data;

namespace RulerOverlay.Converters
{
    /// <summary>
    /// Converter to check if a string value equals a parameter
    /// Used for Unit, Color menu checkmarks
    /// </summary>
    public class StringEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null || parameter == null)
                    return false;

                return value.ToString()?.Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Converter to check if an int value equals a parameter
    /// Used for Opacity, Rotation menu checkmarks
    /// </summary>
    public class IntEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null || parameter == null)
                    return false;

                int intValue = System.Convert.ToInt32(value);
                int intParameter = System.Convert.ToInt32(parameter);

                return intValue == intParameter;
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Converts bool to "Enable/Disable Magnifier" text
    /// </summary>
    public class BoolToMagnifierTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                bool enabled = value is bool && (bool)value;
                return enabled ? "Disable Magnifier" : "Enable Magnifier";
            }
            catch
            {
                return "Enable Magnifier";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Converts bool to "Enable/Disable Edge Snapping" text
    /// </summary>
    public class BoolToEdgeSnappingTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                bool enabled = value is bool && (bool)value;
                return enabled ? "Disable Edge Snapping" : "Enable Edge Snapping";
            }
            catch
            {
                return "Enable Edge Snapping";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
