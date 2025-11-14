using System;
using System.Threading;
using System.Threading.Tasks;
using InsightCleanerAI.Models;

namespace InsightCleanerAI.Services
{
    public interface IStorageScanner
    {
        Task<StorageNode> ScanAsync(
            string rootPath,
            ScanOptions options,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }
}

