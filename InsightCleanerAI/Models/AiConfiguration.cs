namespace InsightCleanerAI.Models
{
    public class AiConfiguration
    {
        public AiMode Mode { get; set; } = AiMode.Disabled;

        public string? LocalModelPath { get; set; }

        public string? CloudApiKey { get; set; }

        public string CloudEndpoint { get; set; } = "https://qianfan.baidubce.com/v2/ai_search/web_search";

        public int CloudRequestTimeoutSeconds { get; set; } = 30;

        public int CloudConcurrencyLimit { get; set; } = 20;

        public string CloudModel { get; set; } = "deepseek-chat";

        public InquiryScope InquiryScope { get; set; } = InquiryScope.AllFiles;

        public int AiBatchSize { get; set; } = 1000;

        public int AiTotalLimit { get; set; } = 2000;

        public string? SearchApiEndpoint { get; set; }

        public string? SearchApiKey { get; set; }

        public string LocalLlmEndpoint { get; set; } = "http://127.0.0.1:11434/api/generate";

        public string LocalLlmModel { get; set; } = "qwen2:7b";

        public string? LocalLlmApiKey { get; set; }

        public bool UseTelemetry { get; set; }
    }
}

