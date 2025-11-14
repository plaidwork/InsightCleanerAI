using InsightCleanerAI.Models;

namespace InsightCleanerAI.Infrastructure
{
    public class UserConfig
    {
        public string? RootPath { get; set; }
        public PrivacyMode PrivacyMode { get; set; } = PrivacyMode.Public;
        public bool IncludeHidden { get; set; }
        public int MaxDepth { get; set; } = 5;
        public int MaxNodes { get; set; } = 20_000;
        public int DelayPerNodeMs { get; set; } = 5;
        public string? CacheDirectory { get; set; }
        public string? DatabasePath { get; set; }

        public AiMode AiMode { get; set; } = AiMode.Local;
        public string? CloudEndpoint { get; set; }
        public string? CloudApiKey { get; set; }
        public string? CloudModel { get; set; }
        public int CloudRequestTimeoutSeconds { get; set; } = 30;
        public int CloudConcurrencyLimit { get; set; } = 2;
        public InquiryScope InquiryScope { get; set; } = InquiryScope.AllFiles;
        public int AiBatchSize { get; set; } = 1000;
        public int AiTotalLimit { get; set; } = 2000;

        public string? SearchApiEndpoint { get; set; }
        public string? SearchApiKey { get; set; }

        public string? LocalLlmEndpoint { get; set; }
        public string? LocalLlmModel { get; set; }
        public string? LocalLlmApiKey { get; set; }

        public bool IgnoreCacheSize { get; set; }

        public bool ScanBlacklistEnabled { get; set; }

        public string? ScanBlacklistEntries { get; set; }

        public PathFilterMode ScanListMode { get; set; } = PathFilterMode.Blacklist;

        public bool RecognitionBlacklistEnabled { get; set; }

        public string? RecognitionBlacklistEntries { get; set; }

        public PathFilterMode RecognitionListMode { get; set; } = PathFilterMode.Blacklist;

        public bool PersistApiKeys { get; set; }
    }
}

