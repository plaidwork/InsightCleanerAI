using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InsightCleanerAI.Models;
using InsightCleanerAI.Resources;

namespace InsightCleanerAI.Services
{
    /// <summary>
    /// Lightweight heuristic classifier that works offline, intended for the privacy modes.
    /// </summary>
    public class RuleBasedInsightProvider : IAiInsightProvider
    {
        public Task<NodeInsight> DescribeAsync(StorageNode node, AiConfiguration configuration, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (classification, confidence, recommendation) = Infer(node);
            var summary = BuildSummary(node, classification);
            return Task.FromResult(new NodeInsight(classification, summary, confidence, recommendation, true));
        }

        private static (NodeClassification classification, double confidence, string recommendation) Infer(StorageNode node)
        {
            if (node.IsDirectory)
            {
                if (Matches(node.Name, "Windows", "System", "WinSxS"))
                {
                    return (NodeClassification.OperatingSystem, 0.9, Strings.RuleHintOperatingSystem);
                }

                if (Matches(node.Name, "Program Files", "Common Files"))
                {
                    return (NodeClassification.Application, 0.7, Strings.RuleHintApplication);
                }

                if (Matches(node.Name, "Temp", "Cache", "Caches"))
                {
                    return (NodeClassification.Cache, 0.6, Strings.RuleHintCache);
                }
            }
            else
            {
                var ext = Path.GetExtension(node.Name).ToLowerInvariant();
                if (new[] { ".log", ".trace" }.Contains(ext))
                {
                    return (NodeClassification.Log, 0.75, Strings.RuleHintLog);
                }

                if (new[] { ".tmp" }.Contains(ext))
                {
                    return (NodeClassification.Temporary, 0.8, Strings.RuleHintTemporary);
                }

                if (new[] { ".mp4", ".mov", ".wav", ".flac", ".jpg", ".png" }.Contains(ext))
                {
                    return (NodeClassification.Media, 0.65, Strings.RuleHintMedia);
                }

                if (new[] { ".doc", ".docx", ".ppt", ".xls", ".xlsx", ".pdf" }.Contains(ext))
                {
                    return (NodeClassification.UserDocument, 0.7, Strings.RuleHintUserDocument);
                }

                if (new[] { ".zip", ".7z", ".rar" }.Contains(ext))
                {
                    return (NodeClassification.Archive, 0.7, Strings.RuleHintArchive);
                }
            }

            return (NodeClassification.Unknown, 0.3, Strings.RuleHintUnknown);
        }

        private static string BuildSummary(StorageNode node, NodeClassification classification)
        {
            var typeLabel = node.IsDirectory ? Strings.LabelDirectory : Strings.LabelFile;
            return classification switch
            {
                NodeClassification.Cache => string.Format(Strings.RuleDescriptionCache, typeLabel),
                NodeClassification.Log => string.Format(Strings.RuleDescriptionLog, typeLabel),
                NodeClassification.Temporary => string.Format(Strings.RuleDescriptionTemporary, typeLabel),
                NodeClassification.Media => string.Format(Strings.RuleDescriptionMedia, typeLabel),
                NodeClassification.UserDocument => string.Format(Strings.RuleDescriptionUserDocument, typeLabel),
                NodeClassification.Archive => string.Format(Strings.RuleDescriptionArchive, typeLabel),
                NodeClassification.Application => string.Format(Strings.RuleDescriptionApplication, typeLabel),
                NodeClassification.OperatingSystem => string.Format(Strings.RuleDescriptionOperatingSystem, typeLabel),
                _ => string.Format(Strings.RuleDescriptionUnknown, typeLabel)
            };
        }

        private static bool Matches(string source, params string[] targets)
        {
            foreach (var target in targets)
            {
                if (source.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
