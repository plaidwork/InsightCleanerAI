using System;
using System.Security.Cryptography;
using System.Text;
using InsightCleanerAI.Models;

namespace InsightCleanerAI.Infrastructure
{
    public static class PathAnonymizer
    {
        public static string? MaybeHidePath(string? path, PrivacyMode mode)
        {
            if (mode == PrivacyMode.Public || string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(path);
            var hash = sha256.ComputeHash(bytes);
            var token = Convert.ToHexString(hash);
            return $"anon://{token[..16]}";
        }
    }
}

