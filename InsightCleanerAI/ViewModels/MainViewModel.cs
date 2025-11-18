using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using InsightCleanerAI.Infrastructure;
using InsightCleanerAI.Models;
using InsightCleanerAI.Resources;
using InsightCleanerAI.Persistence;
using InsightCleanerAI.Services;

namespace InsightCleanerAI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private const int MaxNodesLimit = 1_000_000;
        private readonly string _defaultCloudEndpoint = "https://qianfan.baidubce.com/v2/ai_search/web_search";

        private readonly IStorageScanner _scanner;
        private readonly AiInsightCoordinator _insightCoordinator;
        private readonly ConcurrentDictionary<StorageNode, StorageNodeViewModel> _nodeViewModels = new();
        private readonly ModelListService _modelListService = new();

        private IInsightStore? _insightStore;
        private CancellationTokenSource? _scanCts;

        private string _rootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private PrivacyMode _privacyMode = PrivacyMode.Public;
        private bool _isScanning;
        private string _statusMessage = Strings.StatusSelectFolder;
        private int _maxDepth = 5;
        private bool _includeHidden;
        private double _progressValue;
        private int _delayPerNodeMs = 5;
        private int _maxNodes = 20_000;
        private string _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            UserConfigStore.CurrentAppFolderName,
            "cache");
        private string _databasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            UserConfigStore.CurrentAppFolderName,
            "insights.db");
        private string _remoteServerUrl = "https://qianfan.baidubce.com/v2/ai_search/web_search";

        private StorageNodeViewModel? _selectedNode;
        private bool _ignoreCacheSize;
        private bool _scanListEnabled;
        private string _scanListEntries = string.Empty;
        private PathFilterMode _scanListMode = PathFilterMode.Blacklist;
        private bool _recognitionListEnabled;
        private string _recognitionListEntries = string.Empty;
        private PathFilterMode _recognitionListMode = PathFilterMode.Blacklist;
        private string[] _activeRecognitionList = Array.Empty<string>();
        private bool _persistApiKeys;

        // 模型列表相关
        private ObservableCollection<string> _cloudModels = new();
        private ObservableCollection<string> _localModels = new();
        private bool _isLoadingCloudModels;
        private bool _isLoadingLocalModels;

        public MainViewModel()
        {
            _scanner = new FileSystemScanner();
            var heuristicProvider = new RuleBasedInsightProvider();
            var localLlmProvider = new LocalLlmInsightProvider();
            var cloudProvider = new CloudProxyInsightProvider();
            _insightCoordinator = new AiInsightCoordinator(heuristicProvider, localLlmProvider, cloudProvider);

            Nodes = new ObservableCollection<StorageNodeViewModel>();
            AiConfiguration = new AiConfiguration
            {
                Mode = AiMode.Local,
                CloudEndpoint = _defaultCloudEndpoint
            };

            DebugLog.Info("=== MainViewModel构造函数开始 ===");
            var config = UserConfigStore.Load();
            ApplyConfig(config);

            // 保存配置中的模型名称，然后清空显示
            var savedLocalModel = AiConfiguration.LocalLlmModel;
            var savedCloudModel = AiConfiguration.CloudModel;
            AiConfiguration.LocalLlmModel = string.Empty;
            AiConfiguration.CloudModel = string.Empty;
            DebugLog.Info($"启动时清空模型名称显示 - 保存的本地模型: {savedLocalModel}, 保存的云端模型: {savedCloudModel}");

            // 启动时自动获取模型列表并验证保存的模型
            DebugLog.Info("启动时自动获取模型列表");
            _ = Task.Run(async () =>
            {
                try
                {
                    // 根据当前AI模式自动获取相应的模型列表
                    if (SelectedAiMode == AiMode.LocalLlm && !string.IsNullOrWhiteSpace(AiConfiguration.LocalLlmEndpoint))
                    {
                        await LoadLocalModelsAsync();

                        // 验证保存的本地模型是否在可用列表中
                        if (!string.IsNullOrWhiteSpace(savedLocalModel))
                        {
                            if (LocalModels.Count > 0 && LocalModels.Contains(savedLocalModel))
                            {
                                DebugLog.Info($"恢复本地模型: {savedLocalModel}");
                                AiConfiguration.LocalLlmModel = savedLocalModel;
                                RaisePropertyChanged(nameof(LocalLlmModel));
                            }
                            else
                            {
                                DebugLog.Warning($"配置的本地模型 '{savedLocalModel}' 不在可用列表中，保持为空");
                            }
                        }
                        else
                        {
                            DebugLog.Info("未配置本地模型，保持为空");
                        }
                    }
                    else if (SelectedAiMode == AiMode.KeyOnline && !string.IsNullOrWhiteSpace(AiConfiguration.CloudEndpoint))
                    {
                        await LoadCloudModelsAsync();

                        // 验证保存的云端模型是否在可用列表中
                        if (!string.IsNullOrWhiteSpace(savedCloudModel))
                        {
                            if (CloudModels.Count > 0 && CloudModels.Contains(savedCloudModel))
                            {
                                DebugLog.Info($"恢复云端模型: {savedCloudModel}");
                                AiConfiguration.CloudModel = savedCloudModel;
                                RaisePropertyChanged(nameof(CloudModel));
                            }
                            else
                            {
                                DebugLog.Warning($"配置的云端模型 '{savedCloudModel}' 不在可用列表中，保持为空");
                            }
                        }
                        else
                        {
                            DebugLog.Info("未配置云端模型，保持为空");
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLog.Error("启动时自动获取模型列表失败", ex);
                }
            });

            DebugLog.Info($"构造函数完成 - 模型名称已清空，等待获取后验证");

            InitializeStorage();
        }

        public ObservableCollection<StorageNodeViewModel> Nodes { get; }

        public StorageNodeViewModel? SelectedNode
        {
            get => _selectedNode;
            set => SetField(ref _selectedNode, value);
        }

        public AiConfiguration AiConfiguration { get; }

        public bool IgnoreCacheSize
        {
            get => _ignoreCacheSize;
            set => SetField(ref _ignoreCacheSize, value);
        }

        public bool ScanListEnabled
        {
            get => _scanListEnabled;
            set => SetField(ref _scanListEnabled, value);
        }

        public PathFilterMode ScanListMode
        {
            get => _scanListMode;
            set => SetField(ref _scanListMode, value);
        }

        public string ScanListEntries
        {
            get => _scanListEntries;
            set => SetField(ref _scanListEntries, value);
        }

        public bool RecognitionListEnabled
        {
            get => _recognitionListEnabled;
            set => SetField(ref _recognitionListEnabled, value);
        }

        public PathFilterMode RecognitionListMode
        {
            get => _recognitionListMode;
            set => SetField(ref _recognitionListMode, value);
        }

        public string RecognitionListEntries
        {
            get => _recognitionListEntries;
            set => SetField(ref _recognitionListEntries, value);
        }

        public bool PersistApiKeys
        {
            get => _persistApiKeys;
            set => SetField(ref _persistApiKeys, value);
        }

        public string RootPath
        {
            get => _rootPath;
            set => SetField(ref _rootPath, value);
        }

        public PrivacyMode SelectedPrivacyMode
        {
            get => _privacyMode;
            set => SetField(ref _privacyMode, value);
        }

        public AiMode SelectedAiMode
        {
            get => AiConfiguration.Mode;
            set
            {
                if (AiConfiguration.Mode != value)
                {
                    AiConfiguration.Mode = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsCloudConfigEnabled));
                    RaisePropertyChanged(nameof(IsSearchApiConfigEnabled));
                    RaisePropertyChanged(nameof(IsLocalLlmConfigEnabled));
                }
            }
        }

        public bool IncludeHidden
        {
            get => _includeHidden;
            set => SetField(ref _includeHidden, value);
        }

        public int DelayPerNodeMs
        {
            get => _delayPerNodeMs;
            set => SetField(ref _delayPerNodeMs, Math.Clamp(value, 0, 1000));
        }

        public int MaxDepth
        {
            get => _maxDepth;
            set => SetField(ref _maxDepth, Math.Clamp(value, 1, 32));
        }

        public int MaxNodes
        {
            get => _maxNodes;
            set => SetField(ref _maxNodes, Math.Clamp(value, 100, MaxNodesLimit));
        }

        public string CacheDirectory
        {
            get => _cacheDirectory;
            set => SetField(ref _cacheDirectory, value);
        }

        public string DatabasePath
        {
            get => _databasePath;
            set => SetField(ref _databasePath, value);
        }

        public string RemoteServerUrl
        {
            get => _remoteServerUrl;
            set
            {
                if (SetField(ref _remoteServerUrl, value))
                {
                    AiConfiguration.CloudEndpoint = _remoteServerUrl;
                }
            }
        }

        public string? CloudApiKey
        {
            get => AiConfiguration.CloudApiKey;
            set
            {
                if (AiConfiguration.CloudApiKey != value)
                {
                    AiConfiguration.CloudApiKey = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string CloudModel
        {
            get => AiConfiguration.CloudModel;
            set
            {
                if (AiConfiguration.CloudModel != value)
                {
                    AiConfiguration.CloudModel = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int CloudRequestTimeoutSeconds
        {
            get => AiConfiguration.CloudRequestTimeoutSeconds;
            set
            {
                if (AiConfiguration.CloudRequestTimeoutSeconds != value)
                {
                    AiConfiguration.CloudRequestTimeoutSeconds = Math.Clamp(value, 5, 120);
                    RaisePropertyChanged();
                }
            }
        }

        public int CloudConcurrencyLimit
        {
            get => AiConfiguration.CloudConcurrencyLimit;
            set
            {
                if (AiConfiguration.CloudConcurrencyLimit != value)
                {
                    AiConfiguration.CloudConcurrencyLimit = Math.Clamp(value, 1, 9999);
                    RaisePropertyChanged();
                }
            }
        }

        public InquiryScope InquiryScope
        {
            get => AiConfiguration.InquiryScope;
            set
            {
                if (AiConfiguration.InquiryScope != value)
                {
                    AiConfiguration.InquiryScope = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int AiBatchSize
        {
            get => AiConfiguration.AiBatchSize;
            set
            {
                if (AiConfiguration.AiBatchSize != value)
                {
                    AiConfiguration.AiBatchSize = Math.Clamp(value, 0, 10_000);
                    RaisePropertyChanged();
                }
            }
        }

        public int AiTotalLimit
        {
            get => AiConfiguration.AiTotalLimit;
            set
            {
                if (AiConfiguration.AiTotalLimit != value)
                {
                    AiConfiguration.AiTotalLimit = Math.Clamp(value, 0, 50_000);
                    RaisePropertyChanged();
                }
            }
        }

        public string? SearchApiEndpoint
        {
            get => AiConfiguration.SearchApiEndpoint;
            set
            {
                if (AiConfiguration.SearchApiEndpoint != value)
                {
                    AiConfiguration.SearchApiEndpoint = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string? SearchApiKey
        {
            get => AiConfiguration.SearchApiKey;
            set
            {
                if (AiConfiguration.SearchApiKey != value)
                {
                    AiConfiguration.SearchApiKey = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string LocalLlmEndpoint
        {
            get => AiConfiguration.LocalLlmEndpoint;
            set
            {
                if (AiConfiguration.LocalLlmEndpoint != value)
                {
                    AiConfiguration.LocalLlmEndpoint = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string LocalLlmModel
        {
            get
            {
                DebugLog.Info($"LocalLlmModel getter调用 - 当前值={AiConfiguration.LocalLlmModel}");
                return AiConfiguration.LocalLlmModel;
            }
            set
            {
                DebugLog.Info($"LocalLlmModel setter调用 - 旧值={AiConfiguration.LocalLlmModel}, 新值={value}");
                if (AiConfiguration.LocalLlmModel != value)
                {
                    AiConfiguration.LocalLlmModel = value;
                    RaisePropertyChanged();
                    DebugLog.Info($"LocalLlmModel已更新 - 最终值={AiConfiguration.LocalLlmModel}");
                }
                else
                {
                    DebugLog.Info("LocalLlmModel未更新 - 值相同");
                }
            }
        }

        public string? LocalLlmApiKey
        {
            get => AiConfiguration.LocalLlmApiKey;
            set
            {
                if (AiConfiguration.LocalLlmApiKey != value)
                {
                    AiConfiguration.LocalLlmApiKey = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool IsCloudConfigEnabled => SelectedAiMode == AiMode.KeyOnline;

        public bool IsSearchApiConfigEnabled => SelectedAiMode == AiMode.KeyOnline;

        public bool IsLocalLlmConfigEnabled => SelectedAiMode == AiMode.LocalLlm;

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetField(ref _statusMessage, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            private set => SetField(ref _progressValue, value);
        }

        public bool IsScanning
        {
            get => _isScanning;
            private set => SetField(ref _isScanning, value);
        }

        public ObservableCollection<string> CloudModels
        {
            get => _cloudModels;
            set => SetField(ref _cloudModels, value);
        }

        public ObservableCollection<string> LocalModels
        {
            get => _localModels;
            set => SetField(ref _localModels, value);
        }

        public bool IsLoadingCloudModels
        {
            get => _isLoadingCloudModels;
            set => SetField(ref _isLoadingCloudModels, value);
        }

        public bool IsLoadingLocalModels
        {
            get => _isLoadingLocalModels;
            set => SetField(ref _isLoadingLocalModels, value);
        }

        public async Task ScanAsync()
        {
            if (IsScanning)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(RootPath) || !Directory.Exists(RootPath))
            {
                StatusMessage = Strings.StatusPathMissing;
                DebugLog.Warning($"Scan skipped: root path '{RootPath}' does not exist.");
                return;
            }

            // 检查AI模式下模型是否可用
            if (SelectedAiMode == AiMode.LocalLlm)
            {
                if (LocalModels.Count == 0)
                {
                    StatusMessage = Strings.StatusNoLocalModels;
                    DebugLog.Warning($"Scan skipped: LocalLlm mode selected but no local models available.");
                    return;
                }
            }
            else if (SelectedAiMode == AiMode.KeyOnline)
            {
                if (CloudModels.Count == 0)
                {
                    StatusMessage = Strings.StatusNoCloudModels;
                    DebugLog.Warning($"Scan skipped: KeyOnline mode selected but no cloud models available.");
                    return;
                }
            }

            var scanFilter = BuildScanFilterEntries();
            var options = new ScanOptions
            {
                PrivacyMode = SelectedPrivacyMode,
                IncludeHidden = IncludeHidden,
                FollowReparsePoints = false,
                MaxDepth = MaxDepth,
                MaxNodes = MaxNodes,
                DelayPerNodeMs = DelayPerNodeMs,
                ExcludedPaths = scanFilter,
                UseWhitelist = _scanListMode == PathFilterMode.Whitelist && scanFilter.Length > 0
            };

            _activeRecognitionList = BuildRecognitionList();

            _scanCts = new CancellationTokenSource();

            try
            {
                IsScanning = true;
                StatusMessage = Strings.StatusPreparingScan;
                ProgressValue = 0;
                DebugLog.Info($"=== 扫描开始 ===");
                DebugLog.Info($"扫描参数 - Root='{RootPath}', MaxNodes={MaxNodes}, MaxDepth={MaxDepth}");
                DebugLog.Info($"AI配置 - Mode={SelectedAiMode}, LocalLlmEndpoint={AiConfiguration.LocalLlmEndpoint}");
                DebugLog.Info($"AI配置 - LocalLlmModel={AiConfiguration.LocalLlmModel}, CloudModel={AiConfiguration.CloudModel}");

                var progress = new Progress<ScanProgress>(p =>
                {
                    ProgressValue = p.Percent;
                    StatusMessage = string.Format(Strings.StatusScanningFormat, p.CurrentPath, p.ProcessedNodes, p.NodeBudget);
                });

                var rootNode = await _scanner.ScanAsync(RootPath, options, progress, _scanCts.Token);

                Nodes.Clear();
                _nodeViewModels.Clear();
                SelectedNode = null;
                var rootViewModel = StorageNodeViewModel.FromModel(rootNode);
                RegisterViewModels(rootViewModel);
                Nodes.Add(rootViewModel);
                SelectedNode = rootViewModel;

                await EnrichNodeAsync(rootNode, _scanCts.Token);

                StatusMessage = string.Format(Strings.StatusScanComplete, FormatSize(rootNode.SizeBytes));
                ProgressValue = 1;
                DebugLog.Info($"Scan completed. TotalSize={rootNode.SizeBytes} bytes");
                SaveConfiguration();
            }
            catch (OperationCanceledException)
            {
                StatusMessage = Strings.StatusScanCanceled;
                DebugLog.Warning("Scan cancelled by user.");
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Strings.StatusScanFailed, ex.Message);
                DebugLog.Error("Scan failed", ex);
            }
            finally
            {
                IsScanning = false;
                _scanCts?.Dispose();
                _scanCts = null;
            }
        }

        public void CancelScan() => _scanCts?.Cancel();

        public void ApplyMaxNodesPreset() => MaxNodes = MaxNodesLimit;

        public bool TryRemoveNode(StorageNodeViewModel node)
        {
            if (node.Parent is null)
            {
                StatusMessage = Strings.StatusRootDeletionBlocked;
                return false;
            }

            var parent = node.Parent;
            parent.RemoveChild(node);
            if (SelectedNode == node)
            {
                SelectedNode = parent;
            }

            UnregisterViewModels(node);

            StatusMessage = string.Format(Strings.StatusRemovedFromCache, node.DisplayPath);
            return true;
        }

        public bool TryClearCache(out string message)
        {
            if (IsScanning)
            {
                message = Strings.StatusClearingWhileScanning;
                DebugLog.Warning("Attempted to clear cache while scanning.");
                return false;
            }

            try
            {
                _insightStore = null;
                SqliteConnection.ClearAllPools();

                if (!string.IsNullOrWhiteSpace(DatabasePath) && File.Exists(DatabasePath))
                {
                    File.Delete(DatabasePath);
                }

                InitializeStorage();
                message = Strings.ClearCacheSuccessMessage;
                DebugLog.Info("Cache database cleared successfully.");
                return true;
            }
            catch (Exception ex)
            {
                message = string.Format(Strings.ClearCacheErrorMessage, ex.Message);
                DebugLog.Error("Failed to clear cache database", ex);
                return false;
            }
        }
        private async Task EnrichNodeAsync(StorageNode node, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SelectedAiMode == AiMode.Disabled)
            {
                ApplyEmptyInsightRecursively(node);
                return;
            }

            var jobs = new List<StorageNode>();
            var totalLimit = AiConfiguration.AiTotalLimit <= 0 ? int.MaxValue : AiConfiguration.AiTotalLimit;
            CollectInsightJobs(node, jobs, ref totalLimit, cancellationToken);

            var batchSize = AiConfiguration.AiBatchSize <= 0 ? jobs.Count : AiConfiguration.AiBatchSize;
            DebugLog.Info($"Scheduling AI jobs: count={jobs.Count}, batchSize={batchSize}");
            for (var offset = 0; offset < jobs.Count; offset += batchSize)
            {
                var slice = jobs.Skip(offset).Take(batchSize).ToList();
                DebugLog.Info($"Submitting AI batch {offset / batchSize + 1} with {slice.Count} nodes");
                await ProcessInsightJobsAsync(slice, cancellationToken).ConfigureAwait(false);
            }
        }

        private void CollectInsightJobs(
            StorageNode node,
            List<StorageNode> jobs,
            ref int remainingBudget,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var shouldDescribe = ShouldDescribeNode(node);
            if (shouldDescribe && remainingBudget > 0)
            {
                jobs.Add(node);
                remainingBudget--;
            }
            else if (!shouldDescribe || remainingBudget <= 0)
            {
                ApplyInsight(node, NodeInsight.Empty());
            }

            foreach (var child in node.Children)
            {
                CollectInsightJobs(child, jobs, ref remainingBudget, cancellationToken);
            }
        }

        private string[] BuildScanFilterEntries()
        {
            if (!ScanListEnabled)
            {
                return Array.Empty<string>();
            }

            return NormalizeEntries(ScanListEntries);
        }

        private string[] BuildRecognitionList()
        {
            if (!RecognitionListEnabled)
            {
                return Array.Empty<string>();
            }

            return NormalizeEntries(RecognitionListEntries);
        }

        private bool IsRecognitionBlocked(StorageNode node)
        {
            if (_activeRecognitionList.Length == 0 || string.IsNullOrWhiteSpace(node.FullPath))
            {
                return false;
            }

            var matches = _activeRecognitionList.Any(blocked =>
                node.FullPath.StartsWith(blocked, StringComparison.OrdinalIgnoreCase));

            if (_recognitionListMode == PathFilterMode.Blacklist)
            {
                return matches;
            }

            // whitelist mode: block anything not explicitly listed when entries exist
            if (_recognitionListMode == PathFilterMode.Whitelist && _activeRecognitionList.Length > 0)
            {
                return !matches;
            }

            return false;
        }

        private static string[] NormalizeEntries(string entries)
        {
            if (string.IsNullOrWhiteSpace(entries))
            {
                return Array.Empty<string>();
            }

            var separators = new[] { '\r', '\n', ';' };
            return entries
                .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Trim())
                .Where(entry => entry.Length > 0)
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string NormalizePath(string rawPath)
        {
            try
            {
                return Path.GetFullPath(rawPath);
            }
            catch
            {
                return rawPath;
            }
        }

        private async Task ProcessInsightJobsAsync(
            IReadOnlyCollection<StorageNode> jobs,
            CancellationToken cancellationToken)
        {
            if (jobs.Count == 0)
            {
                return;
            }

            var queue = new ConcurrentQueue<StorageNode>(jobs);
            var workers = Math.Clamp(AiConfiguration.CloudConcurrencyLimit, 1, 9999);
            var tasks = new Task[workers];

            for (var i = 0; i < workers; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    while (queue.TryDequeue(out var job))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var insight = await TryLoadInsightAsync(job, cancellationToken).ConfigureAwait(false);

                        var blocked = IsRecognitionBlocked(job);
                        if (blocked)
                        {
                            job.IsRecognitionBlocked = true;
                            if (insight is null)
                            {
                                insight = new NodeInsight(
                                    NodeClassification.Unknown,
                                    Strings.BlacklistRecognitionMessage,
                                    0,
                                    string.Empty,
                                    false);
                            }

                            ApplyInsight(job, insight);
                            continue;
                        }
                        else
                        {
                            job.IsRecognitionBlocked = false;
                        }

                        if (insight is null)
                        {
                            if (SelectedAiMode == AiMode.Disabled)
                            {
                                insight = NodeInsight.Empty();
                            }
                            else
                            {
                                insight = await _insightCoordinator.DescribeAsync(job, AiConfiguration, cancellationToken)
                                    .ConfigureAwait(false);
                                await TryPersistInsightAsync(job, insight, cancellationToken).ConfigureAwait(false);
                            }
                        }

                        ApplyInsight(job, insight ?? NodeInsight.Empty());
                    }
                }, cancellationToken);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private bool ShouldDescribeNode(StorageNode node)
        {
            if (node.IsDirectory)
            {
                return true;
            }

            return AiConfiguration.InquiryScope == InquiryScope.AllFiles;
        }
        private void ApplyEmptyInsightRecursively(StorageNode node)
        {
            ApplyInsight(node, NodeInsight.Empty());
            foreach (var child in node.Children)
            {
                ApplyEmptyInsightRecursively(child);
            }
        }

        /// <summary>
        /// 获取云端可用模型列表
        /// </summary>
        public async Task LoadCloudModelsAsync()
        {
            if (IsLoadingCloudModels)
            {
                return;
            }

            IsLoadingCloudModels = true;
            try
            {
                var models = await _modelListService.GetCloudModelsAsync(
                    AiConfiguration.CloudEndpoint,
                    AiConfiguration.CloudApiKey);

                CloudModels.Clear();
                foreach (var model in models)
                {
                    CloudModels.Add(model);
                }

                DebugLog.Info($"已加载 {CloudModels.Count} 个云端模型");
            }
            catch (Exception ex)
            {
                DebugLog.Error("加载云端模型列表失败", ex);
            }
            finally
            {
                IsLoadingCloudModels = false;
            }
        }

        /// <summary>
        /// 获取本地LLM可用模型列表
        /// </summary>
        public async Task LoadLocalModelsAsync()
        {
            if (IsLoadingLocalModels)
            {
                return;
            }

            IsLoadingLocalModels = true;
            try
            {
                var models = await _modelListService.GetLocalModelsAsync(
                    AiConfiguration.LocalLlmEndpoint,
                    AiConfiguration.LocalLlmApiKey);

                LocalModels.Clear();
                foreach (var model in models)
                {
                    LocalModels.Add(model);
                }

                DebugLog.Info($"已加载 {LocalModels.Count} 个本地模型");
            }
            catch (Exception ex)
            {
                DebugLog.Error("加载本地模型列表失败", ex);
            }
            finally
            {
                IsLoadingLocalModels = false;
            }
        }

    }
}













