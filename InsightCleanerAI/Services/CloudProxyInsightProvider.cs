using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InsightCleanerAI.Infrastructure;
using InsightCleanerAI.Models;
using InsightCleanerAI.Resources;

namespace InsightCleanerAI.Services
{
    public class CloudProxyInsightProvider : IAiInsightProvider
    {
        private static readonly HttpClient HttpClient = new();
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> ConcurrencyThrottles = new();
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public async Task<NodeInsight> DescribeAsync(
            StorageNode node,
            AiConfiguration configuration,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (configuration.Mode != AiMode.KeyOnline)
            {
                return NodeInsight.Empty();
            }

            return await DescribeViaCloudAsync(node, configuration, cancellationToken);
        }

        private async Task<NodeInsight> DescribeViaCloudAsync(
            StorageNode node,
            AiConfiguration configuration,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(configuration.CloudApiKey))
            {
                return NodeInsight.Empty();
            }

            var endpoint = string.IsNullOrWhiteSpace(configuration.CloudEndpoint)
                ? "https://qianfan.baidubce.com/v2/ai_search/web_search"
                : configuration.CloudEndpoint;

            if (IsOpenAiStyleEndpoint(endpoint))
            {
                return await DescribeViaOpenAiChatAsync(
                    node,
                    endpoint,
                    configuration.CloudApiKey,
                    configuration.CloudModel,
                    configuration.CloudRequestTimeoutSeconds,
                    configuration,
                    cancellationToken);
            }

            return await DescribeViaBaiduSearchAsync(
                node,
                endpoint,
                configuration.CloudApiKey,
                configuration,
                cancellationToken);
        }

        private async Task<NodeInsight> DescribeViaBaiduSearchAsync(
            StorageNode node,
            string endpoint,
            string apiKey,
            AiConfiguration configuration,
            CancellationToken cancellationToken)
        {
            var requestBody = new BaiduSearchRequest
            {
                Messages = new[]
                {
                    new BaiduMessage { Role = "user", Content = BuildQuery(node, configuration) }
                },
                SearchSource = "baidu_search_v2",
                ResourceTypeFilter = new[]
                {
                    new BaiduResourceFilter { Type = "web", TopK = 10 }
                }
            };

            SemaphoreSlim? throttle = GetThrottle(configuration.CloudConcurrencyLimit);
            try
            {
                if (throttle is not null)
                {
                    await throttle.WaitAsync(cancellationToken);
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Accept.ParseAdd("application/json");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = JsonContent.Create(requestBody, options: SerializerOptions);

                DebugLog.Info($"云端搜索（Baidu）：{node.FullPath ?? node.Name}");
                using var response = await HttpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    DebugLog.Warning($"云端搜索失败（Baidu）：HTTP {(int)response.StatusCode}");
                    return NodeInsight.Empty();
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var result = await JsonSerializer.DeserializeAsync<BaiduSearchResponse>(
                    stream,
                    SerializerOptions,
                    cancellationToken);

                var reference = result?.References?.FirstOrDefault();
                if (reference is null)
                {
                    return NodeInsight.Empty(NodeClassification.Unknown);
                }

                var summary = $"{reference.Title} —— {reference.Content}";
                if (!string.IsNullOrWhiteSpace(reference.WebAnchor))
                {
                    summary += $"（来源：{reference.WebAnchor}）";
                }

                DebugLog.Info($"云端搜索成功（Baidu）：{node.FullPath ?? node.Name}");
                return new NodeInsight(
                    NodeClassification.Application,
                    summary,
                    0.7,
                    "信息来自百度搜索，仅供参考。",
                    false);
            }
            catch (Exception ex)
            {
                DebugLog.Error("云端搜索异常（Baidu）", ex);
                return NodeInsight.Empty();
            }
            finally
            {
                throttle?.Release();
            }
        }


        private async Task<NodeInsight> DescribeViaOpenAiChatAsync(
            StorageNode node,
            string endpoint,
            string apiKey,
            string? model,
            int timeoutSeconds,
            AiConfiguration configuration,
            CancellationToken cancellationToken)
        {
            var requestBody = new OpenAiChatRequest
            {
                Model = string.IsNullOrWhiteSpace(model) ? "deepseek-chat" : model,
                Messages = new[]
                {
                    new OpenAiMessage { Role = "system", Content = "你是一名文件分析助手，请向普通用户解释该文件/文件夹的来源、用途，以及是否可以删除。" },
                    new OpenAiMessage { Role = "user", Content = BuildChatPrompt(node, configuration) }
                }
            };

            SemaphoreSlim? throttle = GetThrottle(configuration.CloudConcurrencyLimit);
            try
            {
                if (throttle is not null)
                {
                    await throttle.WaitAsync(cancellationToken);
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Accept.ParseAdd("application/json");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = JsonContent.Create(requestBody, options: SerializerOptions);

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (timeoutSeconds > 0)
                {
                    linkedCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 120)));
                }

                DebugLog.Info($"云端搜索（OpenAI 风格）：{node.FullPath ?? node.Name}");
                using var response = await HttpClient.SendAsync(request, linkedCts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    DebugLog.Warning($"云端搜索失败（OpenAI 风格）：HTTP {(int)response.StatusCode}");
                    return NodeInsight.Empty();
                }

                await using var stream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
                var result = await JsonSerializer.DeserializeAsync<OpenAiChatResponse>(
                    stream,
                    SerializerOptions,
                    linkedCts.Token);

                var message = result?.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrWhiteSpace(message))
                {
                    return NodeInsight.Empty(NodeClassification.Unknown);
                }

                DebugLog.Info($"云端搜索成功（OpenAI 风格）：{node.FullPath ?? node.Name}");
                return new NodeInsight(
                    GuessClassification(message),
                    message,
                    0.65,
                    "信息来自云端模型，仅供参考。",
                    false);
            }
            catch (Exception ex)
            {
                DebugLog.Error("云端搜索异常（OpenAI 风格）", ex);
                return NodeInsight.Empty();
            }
            finally
            {
                throttle?.Release();
            }
        }

        private static string BuildQuery(StorageNode node, AiConfiguration configuration)
        {
            var builder = new StringBuilder();
            builder.Append(node.Name);
            builder.Append(' ');
            builder.Append(node.IsDirectory ? Strings.LabelDirectory : Strings.LabelFile);

            if (node.SizeBytes > 0)
            {
                builder.Append(' ');
                builder.Append($"{node.SizeBytes / 1024 / 1024} MB");
            }

            if (node.IsDirectory)
            {
                var summary = BuildChildSummaryInline(node, configuration.InquiryScope);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    builder.Append(' ');
                    builder.Append(summary);
                }
            }

            builder.Append(' ');
            builder.Append(Strings.PromptBelongsTo);
            return builder.ToString();
        }

        private static string BuildChatPrompt(StorageNode node, AiConfiguration configuration)
        {
            var builder = new StringBuilder();
            builder.AppendLine(Strings.LocalLlmPromptIntro);
            builder.AppendLine(string.Format(Strings.LocalLlmPromptName, node.Name));
            var typeLabel = node.IsDirectory ? Strings.LabelDirectory : Strings.LabelFile;
            builder.AppendLine(string.Format(Strings.LocalLlmPromptType, typeLabel));
            builder.AppendLine(string.Format(Strings.LocalLlmPromptPath, node.FullPath ?? node.DisplayPath));
            var sizeText = node.SizeBytes > 0 ? $"{node.SizeBytes / 1024 / 1024} MB" : Strings.LocalLlmUnknown;
            builder.AppendLine(string.Format(Strings.LocalLlmPromptSize, sizeText));

            if (node.IsDirectory)
            {
                var summary = BuildChildSummaryMultiline(node, configuration.InquiryScope);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    builder.AppendLine(summary.TrimEnd());
                }
            }

            builder.AppendLine(Strings.LocalLlmPromptInstruction);
            builder.AppendLine(Strings.LocalLlmPromptFormat);
            return builder.ToString();
        }

        private static string BuildChildSummaryInline(StorageNode node, InquiryScope scope)
        {
            if (!node.IsDirectory || scope == InquiryScope.FolderOnly)
            {
                return string.Empty;
            }

            var filtered = (scope == InquiryScope.AllFiles
                    ? node.Children
                    : node.Children.Where(c => c.IsDirectory))
                .ToList();

            if (filtered.Count == 0)
            {
                return Strings.ChildListEmpty;
            }

            var preview = filtered
                .Take(10)
                .Select(c => $"{(c.IsDirectory ? Strings.ChildListItemDirectory : Strings.ChildListItemFile)} {c.Name}")
                .ToList();

            var builder = new StringBuilder();
            builder.Append(string.Format(Strings.ChildListPrefix, string.Join(", ", preview)));
            if (filtered.Count > preview.Count)
            {
                builder.Append(string.Format(Strings.ChildListMore, filtered.Count - preview.Count));
            }

            return builder.ToString();
        }

        private static string BuildChildSummaryMultiline(StorageNode node, InquiryScope scope)
        {
            if (!node.IsDirectory || scope == InquiryScope.FolderOnly)
            {
                return string.Empty;
            }

            var filtered = (scope == InquiryScope.AllFiles
                    ? node.Children
                    : node.Children.Where(c => c.IsDirectory))
                .ToList();

            if (filtered.Count == 0)
            {
                return Strings.ChildListEmpty;
            }

            var builder = new StringBuilder();
            const int limit = 10;
            for (var i = 0; i < filtered.Count && i < limit; i++)
            {
                var child = filtered[i];
                builder.AppendLine($"- {(child.IsDirectory ? Strings.ChildListItemDirectory : Strings.ChildListItemFile)} {child.Name}");
            }

            if (filtered.Count > limit)
            {
                builder.AppendLine(string.Format(Strings.ChildListMore, filtered.Count - limit));
            }

            return builder.ToString();
        }

        private static SemaphoreSlim? GetThrottle(int limit)
        {
            if (limit <= 0)
            {
                return null;
            }

            return ConcurrencyThrottles.GetOrAdd(limit, l => new SemaphoreSlim(l, l));
        }

        private static NodeClassification GuessClassification(string text)
        {
            var lower = text.ToLowerInvariant();
            if (lower.Contains("缓存") || lower.Contains("cache"))
            {
                return NodeClassification.Cache;
            }

            if (lower.Contains("日志") || lower.Contains("log"))
            {
                return NodeClassification.Log;
            }

            if (lower.Contains("临时") || lower.Contains("temp"))
            {
                return NodeClassification.Temporary;
            }

            if (lower.Contains("系统") || lower.Contains("windows"))
            {
                return NodeClassification.OperatingSystem;
            }

            if (lower.Contains("应用") || lower.Contains("程序") || lower.Contains("app"))
            {
                return NodeClassification.Application;
            }

            return NodeClassification.Unknown;
        }

        private static bool IsOpenAiStyleEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return false;
            }

            var normalized = endpoint.ToLowerInvariant();
            return normalized.Contains("chat/completions") ||
                   normalized.Contains("/v1/chat");
        }

        private static NodeClassification ParseClassification(string? text)
        {
            if (Enum.TryParse<NodeClassification>(text, true, out var classification))
            {
                return classification;
            }

            return NodeClassification.Unknown;
        }

        private record BaiduSearchRequest
        {
            public BaiduMessage[] Messages { get; init; } = Array.Empty<BaiduMessage>();
            public string SearchSource { get; init; } = "baidu_search_v2";
            public BaiduResourceFilter[] ResourceTypeFilter { get; init; } = Array.Empty<BaiduResourceFilter>();
        }

        private record BaiduMessage
        {
            public string Role { get; init; } = "user";
            public string Content { get; init; } = string.Empty;
        }

        private record BaiduResourceFilter
        {
            public string Type { get; init; } = "web";
            public int TopK { get; init; } = 10;
        }

        private record BaiduSearchResponse
        {
            public BaiduReference[]? References { get; init; }
        }

        private record BaiduReference
        {
            public string? Title { get; init; }
            public string? Content { get; init; }
            public string? WebAnchor { get; init; }
        }

        private record OpenAiChatRequest
        {
            public string Model { get; init; } = "deepseek-chat";
            public OpenAiMessage[] Messages { get; init; } = Array.Empty<OpenAiMessage>();
        }

        private record OpenAiMessage
        {
            public string Role { get; init; } = "user";
            public string Content { get; init; } = string.Empty;
        }

        private record OpenAiChatResponse
        {
            public OpenAiChoice[]? Choices { get; init; }
        }

        private record OpenAiChoice
        {
            public OpenAiMessage? Message { get; init; }
        }

    }
}
