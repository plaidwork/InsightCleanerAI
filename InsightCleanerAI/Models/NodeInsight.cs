namespace InsightCleanerAI.Models
{
    public record NodeInsight(
        NodeClassification Classification,
        string Summary,
        double Confidence,
        string Recommendation,
        bool IsOffline = false)
    {
        public static NodeInsight Empty(
            NodeClassification classification = NodeClassification.Unknown,
            bool isOffline = true) =>
            new(classification, "尚未生成说明。", 0, "暂无建议。", isOffline);
    }
}

