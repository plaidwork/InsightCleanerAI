using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InsightCleanerAI.Infrastructure;
using InsightCleanerAI.Models;

namespace InsightCleanerAI.Services
{
    public class FileSystemScanner : IStorageScanner
    {
        public async Task<StorageNode> ScanAsync(
            string rootPath,
            ScanOptions options,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Root path is required", nameof(rootPath));
            }

            options.EnsureValid();
            var directoryInfo = new DirectoryInfo(rootPath);
            if (!directoryInfo.Exists)
            {
                throw new DirectoryNotFoundException($"Path '{rootPath}' not found.");
            }

            return await Task.Run(
                () =>
                {
                    long nodeCount = 0;
                    return ScanDirectory(directoryInfo, options, progress, cancellationToken, 0, ref nodeCount);
                },
                cancellationToken).ConfigureAwait(false);
        }

        private StorageNode ScanDirectory(
            DirectoryInfo directory,
            ScanOptions options,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken,
            int depth,
            ref long nodeCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            nodeCount++;
            if (nodeCount > options.MaxNodes)
            {
                throw new InvalidOperationException("Node limit exceeded. Please narrow the scope or raise the limit.");
            }

            ReportProgress(progress, options, directory.FullName, nodeCount);

            var fullPath = options.PrivacyMode == PrivacyMode.Public ? directory.FullName : null;
            var displayPath = options.PrivacyMode == PrivacyMode.Public
                ? directory.FullName
                : PathAnonymizer.MaybeHidePath(directory.FullName, options.PrivacyMode) ?? directory.Name;

            var rootNode = new StorageNode(directory.Name, fullPath, true, displayPath);

            if (depth >= options.MaxDepth)
            {
                rootNode.SizeBytes = 0;
                return rootNode;
            }

            try
            {
                foreach (var subDir in directory.EnumerateDirectories())
                {
                    if (ShouldSkip(subDir, options) ||
                        ShouldFilter(subDir.FullName, options.ExcludedPaths, options.UseWhitelist))
                    {
                        continue;
                    }

                    var child = ScanDirectory(subDir, options, progress, cancellationToken, depth + 1, ref nodeCount);
                    rootNode.AddChild(child);
                    ApplyDelay(options);
                }

                foreach (var file in directory.EnumerateFiles())
                {
                    if (ShouldSkip(file, options) ||
                        ShouldFilter(file.FullName, options.ExcludedPaths, options.UseWhitelist))
                    {
                        continue;
                    }

                    var child = CreateFileNode(file, options);
                    rootNode.AddChild(child);
                    ApplyDelay(options);
                }
            }
            catch (UnauthorizedAccessException)
            {
                rootNode.InsightSummary = "Access denied. Unable to enumerate this directory.";
            }
            catch (PathTooLongException)
            {
                rootNode.InsightSummary = "Path too long. Unable to read this directory.";
            }

            rootNode.SizeBytes = rootNode.Children.Sum(c => c.SizeBytes);
            return rootNode;
        }

        private StorageNode CreateFileNode(FileInfo file, ScanOptions options)
        {
            var fullPath = options.PrivacyMode == PrivacyMode.Public ? file.FullName : null;
            var displayPath = options.PrivacyMode == PrivacyMode.Public
                ? file.FullName
                : PathAnonymizer.MaybeHidePath(file.FullName, options.PrivacyMode) ?? file.Name;

            return new StorageNode(file.Name, fullPath, false, displayPath)
            {
                SizeBytes = file.Length
            };
        }

        private static bool ShouldSkip(FileSystemInfo info, ScanOptions options)
        {
            var attributes = info.Attributes;
            if (!options.IncludeHidden && attributes.HasFlag(FileAttributes.Hidden))
            {
                return true;
            }

            if (!options.IncludeHidden && attributes.HasFlag(FileAttributes.System))
            {
                return true;
            }

            if (!options.FollowReparsePoints && attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return true;
            }

            return false;
        }

        private static void ReportProgress(IProgress<ScanProgress>? progress, ScanOptions options, string path, long processedNodes)
        {
            if (progress is null)
            {
                return;
            }

            var displayPath = options.PrivacyMode == PrivacyMode.Public
                ? path
                : PathAnonymizer.MaybeHidePath(path, options.PrivacyMode) ?? "Path hidden";

            var estimatedTotal = Math.Max(options.MaxNodes, processedNodes * 2L);
            var percent = Math.Min(0.95, processedNodes / (double)estimatedTotal);
            progress.Report(new ScanProgress(percent, displayPath, processedNodes, options.MaxNodes));
        }

        private static void ApplyDelay(ScanOptions options)
        {
            if (options.DelayPerNodeMs <= 0)
            {
                return;
            }

            Thread.Sleep(options.DelayPerNodeMs);
        }

        private static bool ShouldFilter(string path, IReadOnlyList<string> entries, bool useWhitelist)
        {
            if (entries is null || entries.Count == 0)
            {
                return false;
            }

            var matches = entries.Any(entry =>
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    return false;
                }

                return path.StartsWith(entry, StringComparison.OrdinalIgnoreCase);
            });

            if (useWhitelist)
            {
                return !matches;
            }

            return matches;
        }
    }
}
