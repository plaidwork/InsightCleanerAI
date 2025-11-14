using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Windows;
using InsightCleanerAI.Infrastructure;
using InsightCleanerAI.Models;
using InsightCleanerAI.Resources;
using InsightCleanerAI.Persistence;

namespace InsightCleanerAI.ViewModels
{
    public partial class MainViewModel
    {
        private void InitializeStorage()
        {
            try
            {
                EnsureDirectoryExists(CacheDirectory);
                var databaseDirectory = Path.GetDirectoryName(DatabasePath);
                EnsureDirectoryExists(databaseDirectory);
                _insightStore = new SqliteInsightStore(DatabasePath);
            }
            catch (Exception ex)
            {
                _insightStore = null;
                StatusMessage = string.Format(Strings.StatusInitStorageFailed, ex.Message);
            }
        }

        private static void EnsureDirectoryExists(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private async Task<NodeInsight?> TryLoadInsightAsync(
            StorageNode node,
            CancellationToken cancellationToken)
        {
            if (_insightStore is null || string.IsNullOrWhiteSpace(node.FullPath))
            {
                return null;
            }

            try
            {
                return await _insightStore.GetAsync(node.FullPath, node.SizeBytes, IgnoreCacheSize, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Strings.StatusReadCacheFailed, ex.Message);
                DebugLog.Error($"Failed to read cache for {node.FullPath}", ex);
                return null;
            }
        }

        private async Task TryPersistInsightAsync(
            StorageNode node,
            NodeInsight insight,
            CancellationToken cancellationToken)
        {
            if (_insightStore is null || string.IsNullOrWhiteSpace(node.FullPath))
            {
                return;
            }

            try
            {
                await _insightStore.SaveAsync(node.FullPath, node.SizeBytes, insight, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Strings.StatusWriteCacheFailed, ex.Message);
                DebugLog.Error($"Failed to write cache for {node.FullPath}", ex);
            }
        }

        private void ApplyInsight(StorageNode node, NodeInsight insight)
        {
            node.Classification = insight.Classification;
            node.ClassificationConfidence = insight.Confidence;
            node.InsightSummary = insight.Summary;
            node.IsOfflineInsight = insight.IsOffline;

            if (_nodeViewModels.TryGetValue(node, out var viewModel))
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is null || dispatcher.CheckAccess())
                {
                    viewModel.NotifyInsightUpdated();
                }
                else
                {
                    dispatcher.BeginInvoke(new Action(viewModel.NotifyInsightUpdated));
                }
            }
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

            return $"{value:0.##} {units[unit]}";
        }

        private void RegisterViewModels(StorageNodeViewModel nodeViewModel)
        {
            _nodeViewModels[nodeViewModel.Model] = nodeViewModel;
            foreach (var child in nodeViewModel.Children)
            {
                RegisterViewModels(child);
            }
        }

        private void UnregisterViewModels(StorageNodeViewModel nodeViewModel)
        {
            _nodeViewModels.TryRemove(nodeViewModel.Model, out _);
            foreach (var child in nodeViewModel.Children)
            {
                UnregisterViewModels(child);
            }
        }
    }
}
