using System.Threading;
using System.Threading.Tasks;
using InsightCleanerAI.Models;

namespace InsightCleanerAI.Persistence
{
    public interface IInsightStore
    {
        Task<NodeInsight?> GetAsync(string path, long sizeBytes, bool ignoreSizeMismatch, CancellationToken cancellationToken);

        Task SaveAsync(string path, long sizeBytes, NodeInsight insight, CancellationToken cancellationToken);
    }
}

