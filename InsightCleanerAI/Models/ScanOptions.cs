using System;
using System.Collections.Generic;

namespace InsightCleanerAI.Models
{
    public class ScanOptions
    {
        public int MaxDepth { get; init; } = 5;
        public long MaxNodes { get; init; } = 10_000;
        public bool IncludeHidden { get; init; }
        public bool FollowReparsePoints { get; init; }
        public PrivacyMode PrivacyMode { get; init; } = PrivacyMode.Public;
        public int DelayPerNodeMs { get; init; }
        public IReadOnlyList<string> ExcludedPaths { get; init; } = Array.Empty<string>();
        public bool UseWhitelist { get; init; }

        public ScanOptions WithPrivacy(PrivacyMode privacyMode) =>
            new()
            {
                MaxDepth = MaxDepth,
                MaxNodes = MaxNodes,
                IncludeHidden = IncludeHidden,
                FollowReparsePoints = FollowReparsePoints,
                PrivacyMode = privacyMode,
                DelayPerNodeMs = DelayPerNodeMs,
                ExcludedPaths = ExcludedPaths,
                UseWhitelist = UseWhitelist
            };

        public static ScanOptions Default => new();

        public void EnsureValid()
        {
            if (MaxDepth <= 0 || MaxDepth > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxDepth), "MaxDepth must be between 1 and 32.");
            }

            if (MaxNodes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxNodes), "MaxNodes must be positive.");
            }

            if (DelayPerNodeMs < 0 || DelayPerNodeMs > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(DelayPerNodeMs), "Delay must be between 0 and 1000 milliseconds.");
            }
        }
    }
}
