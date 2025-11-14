using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using InsightCleanerAI.Models;
using InsightCleanerAI.Resources;

namespace InsightCleanerAI.ViewModels
{
    public class StorageNodeViewModel : ObservableObject
    {
        private StorageNodeViewModel(StorageNode model, StorageNodeViewModel? parent)
        {
            Model = model;
            Parent = parent;
            foreach (var child in model.Children.OrderByDescending(c => c.SizeBytes))
            {
                Children.Add(new StorageNodeViewModel(child, this));
            }
        }

        public StorageNode Model { get; }

        public StorageNodeViewModel? Parent { get; }

        public string Name => Model.Name;

        public string DisplayPath => Model.DisplayPath;

        public string SizeDisplay => FormatSize(Model.SizeBytes);

        public NodeClassification Classification => Model.Classification;

        public string ClassificationLabel => Model.Classification.ToString();

        public string? Insight => Model.InsightSummary;

        public bool IsOfflineInsight => Model.IsOfflineInsight;

        public bool IsRecognitionBlocked => Model.IsRecognitionBlocked;

        public string StatusBadge
        {
            get
            {
                if (Model.IsRecognitionBlocked)
                {
                    return Strings.BlockedBadge;
                }

                return Model.IsOfflineInsight ? Strings.OfflineBadge : string.Empty;
            }
        }

        public ObservableCollection<StorageNodeViewModel> Children { get; } = new();

        public static StorageNodeViewModel FromModel(StorageNode model) => new(model, null);

        public void NotifyInsightUpdated()
        {
            RaisePropertyChanged(nameof(Classification));
            RaisePropertyChanged(nameof(ClassificationLabel));
            RaisePropertyChanged(nameof(Insight));
            RaisePropertyChanged(nameof(IsOfflineInsight));
            RaisePropertyChanged(nameof(IsRecognitionBlocked));
            RaisePropertyChanged(nameof(StatusBadge));
        }

        public void RemoveChild(StorageNodeViewModel child)
        {
            if (Children.Remove(child))
            {
                Model.RemoveChild(child.Model);
                AdjustSize(-child.Model.SizeBytes);
            }
        }

        public void AdjustSize(long delta)
        {
            Model.SizeBytes += delta;
            RaisePropertyChanged(nameof(SizeDisplay));
            Parent?.AdjustSize(delta);
        }

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            var unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return $"{value.ToString("0.##", CultureInfo.InvariantCulture)} {units[unit]}";
        }
    }
}

