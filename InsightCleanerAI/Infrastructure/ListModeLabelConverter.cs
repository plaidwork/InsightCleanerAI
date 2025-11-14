using System;
using System.Globalization;
using System.Windows.Data;
using InsightCleanerAI.Models;
using InsightCleanerAI.Resources;

namespace InsightCleanerAI.Infrastructure
{
    public class ListModeLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not PathFilterMode mode)
            {
                return value?.ToString() ?? string.Empty;
            }

            return mode switch
            {
                PathFilterMode.Blacklist => Strings.ListModeBlacklist,
                PathFilterMode.Whitelist => Strings.ListModeWhitelist,
                _ => mode.ToString()
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
