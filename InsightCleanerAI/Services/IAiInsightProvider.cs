using System.Threading;
using System.Threading.Tasks;
using InsightCleanerAI.Models;

namespace InsightCleanerAI.Services
{
    public interface IAiInsightProvider
    {
        Task<NodeInsight> DescribeAsync(StorageNode node, AiConfiguration configuration, CancellationToken cancellationToken);
    }
}

