using System.Threading;
using System.Threading.Tasks;
using InsightCleanerAI.Models;

namespace InsightCleanerAI.Services
{
    public class AiInsightCoordinator
    {
        private readonly IAiInsightProvider? _heuristicProvider;
        private readonly IAiInsightProvider? _localLlmProvider;
        private readonly IAiInsightProvider? _cloudProvider;

        public AiInsightCoordinator(
            IAiInsightProvider? heuristicProvider,
            IAiInsightProvider? localLlmProvider,
            IAiInsightProvider? cloudProvider)
        {
            _heuristicProvider = heuristicProvider;
            _localLlmProvider = localLlmProvider;
            _cloudProvider = cloudProvider;
        }

        public Task<NodeInsight> DescribeAsync(
            StorageNode node,
            AiConfiguration configuration,
            CancellationToken cancellationToken)
        {
            return configuration.Mode switch
            {
                AiMode.Disabled => Task.FromResult(NodeInsight.Empty()),
                AiMode.Local when _heuristicProvider is not null => _heuristicProvider.DescribeAsync(node, configuration, cancellationToken),
                AiMode.LocalLlm when _localLlmProvider is not null => _localLlmProvider.DescribeAsync(node, configuration, cancellationToken),
                AiMode.KeyOnline when _cloudProvider is not null => _cloudProvider.DescribeAsync(node, configuration, cancellationToken),
                _ => Task.FromResult(NodeInsight.Empty(NodeClassification.Unknown))
            };
        }
    }
}

