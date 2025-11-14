namespace InsightCleanerAI.Models
{
    public record ScanProgress(double Percent, string CurrentPath, long ProcessedNodes, long NodeBudget);
}

