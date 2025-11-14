using System;
using System.Globalization;
using System.Windows.Data;
using InsightCleanerAI.Models;
using InsightCleanerAI.Resources;

namespace InsightCleanerAI.Infrastructure
{
    public class AiModeLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not AiMode mode)
            {
                return value?.ToString() ?? string.Empty;
            }

            return mode switch
            {
                AiMode.Disabled => Strings.AiModeDisabledLabel,
                AiMode.Local => Strings.AiModeLocalLabel,
                AiMode.LocalLlm => Strings.AiModeLocalLlmLabel,
                AiMode.KeyOnline => Strings.AiModeKeyOnlineLabel,
                _ => mode.ToString()
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}


