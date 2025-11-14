using System;
using System.IO;
using System.Text.Json;

namespace InsightCleanerAI.Infrastructure
{
    public static class UserConfigStore
    {
        public const string CurrentAppFolderName = "InsightCleanerAI";

        private static readonly string AppDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private static readonly string ConfigDirectory = Path.Combine(AppDataRoot, CurrentAppFolderName);
        private static readonly string ConfigPath = Path.Combine(ConfigDirectory, "settings.json");

        public static UserConfig Load()
        {
            var config = TryLoadConfig(ConfigPath) ?? new UserConfig();
            NormalizePaths(config);
            return config;
        }

        public static void Save(UserConfig config)
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // ignored
            }
        }

        private static UserConfig? TryLoadConfig(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<UserConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        private static void NormalizePaths(UserConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.CacheDirectory))
            {
                config.CacheDirectory = Path.Combine(ConfigDirectory, "cache");
            }

            if (string.IsNullOrWhiteSpace(config.DatabasePath))
            {
                config.DatabasePath = Path.Combine(ConfigDirectory, "insights.db");
            }

            config.CloudEndpoint ??= "https://qianfan.baidubce.com/v2/ai_search/web_search";
        }
    }
}
