using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InsightCleanerAI.Models;
using InsightCleanerAI.Resources;

namespace InsightCleanerAI.Services
{
    /// <summary>
    /// Connects to a user-provided local LLM service (Ollama/koboldcpp/OpenAI-compatible) via HTTP.
    /// </summary>
    public sealed class LocalLlmInsightProvider : IAiInsightProvider
    {
        private static readonly HttpClient HttpClient = new();

        public async Task<NodeInsight> DescribeAsync(
            StorageNode node,
            AiConfiguration configuration,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(configuration.LocalLlmEndpoint) ||
                string.IsNullOrWhiteSpace(configuration.LocalLlmModel))
            {
                return NodeInsight.Empty(NodeClassification.Unknown);
            }

            var prompt = BuildPrompt(node);
            var requestBody = new LocalLlmRequest
            {
                Model = configuration.LocalLlmModel,
                Prompt = prompt,
                Stream = false
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, configuration.LocalLlmEndpoint);
                request.Headers.Accept.ParseAdd("application/json");
                if (!string.IsNullOrWhiteSpace(configuration.LocalLlmApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration.LocalLlmApiKey);
                }

                request.Content = JsonContent.Create(requestBody);

                using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return NodeInsight.Empty(NodeClassification.Unknown);
                }

                var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var summary = TryExtractSummary(payload);
                if (string.IsNullOrWhiteSpace(summary))
                {
                    return NodeInsight.Empty(NodeClassification.Unknown);
                }

                return new NodeInsight(
                    GuessClassification(summary),
                    summary,
                    0.65,
                    Strings.LocalLlmSourceNote,
                    false);
            }
            catch
            {
                return NodeInsight.Empty(NodeClassification.Unknown);
            }
        }

        private static string BuildPrompt(StorageNode node)
        {
            var builder = new StringBuilder();
            var parentPath = node.FullPath is null
                ? null
                : Path.GetDirectoryName(node.FullPath);

            builder.AppendLine(Strings.LocalLlmPromptIntro);
            builder.AppendLine(string.Format(Strings.LocalLlmPromptName, node.Name));
            var typeLabel = node.IsDirectory ? Strings.LabelDirectory : Strings.LabelFile;
            builder.AppendLine(string.Format(Strings.LocalLlmPromptType, typeLabel));
            builder.AppendLine(string.Format(Strings.LocalLlmPromptPath, node.FullPath ?? node.DisplayPath));
            builder.AppendLine(string.Format(Strings.LocalLlmPromptSize, FormatSize(node.SizeBytes)));
            builder.AppendLine(string.Format(Strings.LocalLlmPromptParent, parentPath ?? Strings.LocalLlmUnknown));
            builder.AppendLine();
            builder.AppendLine(Strings.LocalLlmPromptInstruction);
            builder.AppendLine(Strings.LocalLlmPromptFormat);
            return builder.ToString();
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0)
            {
                return Strings.LocalLlmUnknown;
            }

            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            var unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return $"{value:0.##}{units[unit]}";
        }

        private static string? TryExtractSummary(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                if (root.TryGetProperty("response", out var responseProperty))
                {
                    return responseProperty.GetString();
                }

                if (root.TryGetProperty("choices", out var choicesProperty) &&
                    choicesProperty.ValueKind == JsonValueKind.Array)
                {
                    var first = choicesProperty.EnumerateArray().FirstOrDefault();
                    if (first.ValueKind == JsonValueKind.Object &&
                        first.TryGetProperty("message", out var messageProperty) &&
                        messageProperty.TryGetProperty("content", out var contentProperty))
                    {
                        return contentProperty.GetString();
                    }
                }

                if (root.TryGetProperty("content", out var content))
                {
                    return content.GetString();
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static NodeClassification GuessClassification(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                return NodeClassification.Unknown;
            }

            var text = summary.ToLowerInvariant();
            if (text.Contains("缓存") || text.Contains("cache"))
            {
                return NodeClassification.Cache;
            }

            if (text.Contains("日志") || text.Contains("log"))
            {
                return NodeClassification.Log;
            }

            if (text.Contains("临时") || text.Contains("temp"))
            {
                return NodeClassification.Temporary;
            }

            if (text.Contains("系统") || text.Contains("windows"))
            {
                return NodeClassification.OperatingSystem;
            }

            if (text.Contains("应用") || text.Contains("程序") || text.Contains("app"))
            {
                return NodeClassification.Application;
            }

            return NodeClassification.Unknown;
        }

        private record LocalLlmRequest
        {
            public string Model { get; init; } = string.Empty;

            public string Prompt { get; init; } = string.Empty;

            public bool Stream { get; init; }
        }
    }
}
