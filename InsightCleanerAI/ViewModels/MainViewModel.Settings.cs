using System;
using InsightCleanerAI.Infrastructure;

namespace InsightCleanerAI.ViewModels
{
    public partial class MainViewModel
    {
        private void ApplyConfig(UserConfig config)
        {
            _rootPath = string.IsNullOrWhiteSpace(config.RootPath)
                ? _rootPath
                : config.RootPath;
            _privacyMode = config.PrivacyMode;
            _includeHidden = config.IncludeHidden;
            _maxDepth = config.MaxDepth <= 0 ? _maxDepth : config.MaxDepth;
            _maxNodes = config.MaxNodes <= 0 ? _maxNodes : config.MaxNodes;
            _delayPerNodeMs = config.DelayPerNodeMs;
            _cacheDirectory = string.IsNullOrWhiteSpace(config.CacheDirectory) ? _cacheDirectory : config.CacheDirectory;
            _databasePath = string.IsNullOrWhiteSpace(config.DatabasePath) ? _databasePath : config.DatabasePath;
            _ignoreCacheSize = config.IgnoreCacheSize;
            _scanListEnabled = config.ScanBlacklistEnabled;
            _scanListEntries = config.ScanBlacklistEntries ?? string.Empty;
            _scanListMode = config.ScanListMode;
            _recognitionListEnabled = config.RecognitionBlacklistEnabled;
            _recognitionListEntries = config.RecognitionBlacklistEntries ?? string.Empty;
            _recognitionListMode = config.RecognitionListMode;
            _persistApiKeys = config.PersistApiKeys;

            SelectedAiMode = config.AiMode;
            var remoteEndpoint = string.IsNullOrWhiteSpace(config.CloudEndpoint)
                ? _defaultCloudEndpoint
                : config.CloudEndpoint;
            RemoteServerUrl = remoteEndpoint;
            AiConfiguration.CloudApiKey = config.CloudApiKey ?? AiConfiguration.CloudApiKey;
            AiConfiguration.CloudModel = string.IsNullOrWhiteSpace(config.CloudModel)
                ? AiConfiguration.CloudModel
                : config.CloudModel;
            AiConfiguration.CloudRequestTimeoutSeconds = config.CloudRequestTimeoutSeconds;
            AiConfiguration.CloudConcurrencyLimit = config.CloudConcurrencyLimit;
            AiConfiguration.InquiryScope = config.InquiryScope;
            AiConfiguration.AiBatchSize = config.AiBatchSize;
            AiConfiguration.AiTotalLimit = config.AiTotalLimit;
            AiConfiguration.SearchApiEndpoint = config.SearchApiEndpoint ?? AiConfiguration.SearchApiEndpoint;
            AiConfiguration.SearchApiKey = config.SearchApiKey ?? AiConfiguration.SearchApiKey;
            AiConfiguration.LocalLlmEndpoint = config.LocalLlmEndpoint ?? AiConfiguration.LocalLlmEndpoint;
            AiConfiguration.LocalLlmModel = config.LocalLlmModel ?? AiConfiguration.LocalLlmModel;
            AiConfiguration.LocalLlmApiKey = config.LocalLlmApiKey ?? AiConfiguration.LocalLlmApiKey;

            RaisePropertyChanged(nameof(RootPath));
            RaisePropertyChanged(nameof(SelectedPrivacyMode));
            RaisePropertyChanged(nameof(IncludeHidden));
            RaisePropertyChanged(nameof(MaxDepth));
            RaisePropertyChanged(nameof(MaxNodes));
            RaisePropertyChanged(nameof(DelayPerNodeMs));
            RaisePropertyChanged(nameof(CacheDirectory));
            RaisePropertyChanged(nameof(DatabasePath));
            RaisePropertyChanged(nameof(IgnoreCacheSize));
            RaisePropertyChanged(nameof(ScanListEnabled));
            RaisePropertyChanged(nameof(ScanListEntries));
            RaisePropertyChanged(nameof(ScanListMode));
            RaisePropertyChanged(nameof(RecognitionListEnabled));
            RaisePropertyChanged(nameof(RecognitionListEntries));
            RaisePropertyChanged(nameof(RecognitionListMode));
            RaisePropertyChanged(nameof(PersistApiKeys));
            RaisePropertyChanged(nameof(SelectedAiMode));
            RaisePropertyChanged(nameof(IsCloudConfigEnabled));
            RaisePropertyChanged(nameof(IsSearchApiConfigEnabled));
            RaisePropertyChanged(nameof(IsLocalLlmConfigEnabled));
        }

        public void SaveConfiguration(bool includeSensitive = true)
        {
            var config = new UserConfig
            {
                RootPath = _rootPath,
                PrivacyMode = _privacyMode,
                IncludeHidden = _includeHidden,
                MaxDepth = _maxDepth,
                MaxNodes = _maxNodes,
                DelayPerNodeMs = _delayPerNodeMs,
                CacheDirectory = _cacheDirectory,
                DatabasePath = _databasePath,
                IgnoreCacheSize = _ignoreCacheSize,
                ScanBlacklistEnabled = _scanListEnabled,
                ScanBlacklistEntries = _scanListEntries,
                ScanListMode = _scanListMode,
                RecognitionBlacklistEnabled = _recognitionListEnabled,
                RecognitionBlacklistEntries = _recognitionListEntries,
                RecognitionListMode = _recognitionListMode,
                PersistApiKeys = _persistApiKeys,
                AiMode = SelectedAiMode,
                CloudEndpoint = AiConfiguration.CloudEndpoint,
                CloudApiKey = AiConfiguration.CloudApiKey,
                CloudModel = AiConfiguration.CloudModel,
                CloudRequestTimeoutSeconds = AiConfiguration.CloudRequestTimeoutSeconds,
                CloudConcurrencyLimit = AiConfiguration.CloudConcurrencyLimit,
                InquiryScope = AiConfiguration.InquiryScope,
                AiBatchSize = AiConfiguration.AiBatchSize,
                AiTotalLimit = AiConfiguration.AiTotalLimit,
                SearchApiEndpoint = AiConfiguration.SearchApiEndpoint,
                SearchApiKey = AiConfiguration.SearchApiKey,
                LocalLlmEndpoint = AiConfiguration.LocalLlmEndpoint,
                LocalLlmModel = AiConfiguration.LocalLlmModel,
                LocalLlmApiKey = AiConfiguration.LocalLlmApiKey
            };

            var shouldPersistKeys = includeSensitive && _persistApiKeys;
            if (!shouldPersistKeys)
            {
                config.CloudApiKey = null;
                config.SearchApiKey = null;
                config.LocalLlmApiKey = null;
            }

            UserConfigStore.Save(config);
        }
    }
}
