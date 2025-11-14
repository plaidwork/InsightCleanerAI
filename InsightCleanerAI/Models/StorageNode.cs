using System.Collections.Generic;

namespace InsightCleanerAI.Models
{
    public class StorageNode
    {
        public StorageNode(string name, string? fullPath, bool isDirectory)
            : this(name, fullPath, isDirectory, fullPath ?? name)
        {
        }

        public StorageNode(string name, string? fullPath, bool isDirectory, string displayPath)
        {
            Name = name;
            FullPath = fullPath;
            IsDirectory = isDirectory;
            DisplayPath = displayPath;
        }

        public string Name { get; }

        public string? FullPath { get; }

        public string DisplayPath { get; }

        public bool IsDirectory { get; }

        public long SizeBytes { get; set; }

        public NodeClassification Classification { get; set; } = NodeClassification.Unknown;

        public double? ClassificationConfidence { get; set; }

        public string? InsightSummary { get; set; }

        public bool IsOfflineInsight { get; set; }

        public bool IsRecognitionBlocked { get; set; }

        public IList<StorageNode> Children { get; } = new List<StorageNode>();

        public void AddChild(StorageNode node)
        {
            (Children as List<StorageNode>)?.Add(node);
        }

        public bool RemoveChild(StorageNode node)
        {
            return (Children as List<StorageNode>)?.Remove(node) ?? false;
        }
    }
}

