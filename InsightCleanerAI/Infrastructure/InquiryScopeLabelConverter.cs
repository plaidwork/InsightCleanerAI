using System;
using System.Globalization;
using System.Windows.Data;
using InsightCleanerAI.Models;
using InsightCleanerAI.Resources;

namespace InsightCleanerAI.Infrastructure
{
    public class InquiryScopeLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not InquiryScope scope)
            {
                return value?.ToString() ?? string.Empty;
            }

            return scope switch
            {
                InquiryScope.FolderOnly => Strings.InquiryScopeFolderOnlyLabel,
                InquiryScope.FolderWithChildren => Strings.InquiryScopeFolderWithChildrenLabel,
                InquiryScope.AllFiles => Strings.InquiryScopeAllFilesLabel,
                _ => scope.ToString()
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}


