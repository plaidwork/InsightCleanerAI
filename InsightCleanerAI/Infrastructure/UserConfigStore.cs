using System;
using System.IO;
using System.Text.Json;

namespace InsightCleanerAI.Infrastructure
{
    public static class UserConfigStore
    {
        public const string CurrentAppFolderName = "InsightCleanerAI";
        private const string LegacyAppFolderName = "SpaceSnifferAI";

        private static readonly string AppDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private static readonly string ConfigDirectory = Path.Combine(AppDataRoot, CurrentAppFolderName);
        private static readonly string LegacyConfigDirectory = Path.Combine(AppDataRoot, LegacyAppFolderName);

        private static readonly string ConfigPath = Path.Combine(ConfigDirectory, "settings.json");
        private static readonly string LegacyConfigPath = Path.Combine(LegacyConfigDirectory, "settings.json");

        public static UserConfig Load()
        {
            var config = TryLoadConfig(ConfigPath);
            var loadedFromLegacyPath = false;

            if (config is null && File.Exists(LegacyConfigPath))
            {
                config = TryLoadConfig(LegacyConfigPath);
                loadedFromLegacyPath = config is not null;
            }

            config ??= new UserConfig();

            if (loadedFromLegacyPath)
            {
                TryMigrateLegacyConfigFile();
            }

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

        private static void TryMigrateLegacyConfigFile()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                if (!File.Exists(ConfigPath) && File.Exists(LegacyConfigPath))
                {
                    File.Copy(LegacyConfigPath, ConfigPath, overwrite: false);
                }
            }
            catch
            {
                // ignored
            }
        }

        private static void NormalizePaths(UserConfig config)
        {
            config.CacheDirectory = NormalizeDirectoryPath(config.CacheDirectory)
                ?? Path.Combine(ConfigDirectory, "cache");
            config.DatabasePath = NormalizeFilePath(config.DatabasePath)
                ?? Path.Combine(ConfigDirectory, "insights.db");

            config.CloudEndpoint ??= "https://qianfan.baidubce.com/v2/ai_search/web_search";
        }

        private static string? NormalizeDirectoryPath(string? path)
        {
            if (!TryGetLegacyReplacement(path, out var legacyPath, out var newPath))
            {
                return path;
            }

            return TryMoveDirectory(legacyPath, newPath) ? newPath : path;
        }

        private static string? NormalizeFilePath(string? path)
        {
            if (!TryGetLegacyReplacement(path, out var legacyPath, out var newPath))
            {
                return path;
            }

            return TryMoveFile(legacyPath, newPath) ? newPath : path;
        }

        private static bool TryGetLegacyReplacement(string? path, out string legacyPath, out string newPath)
        {
            legacyPath = string.Empty;
            newPath = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            legacyPath = path;
            var legacyRoot = Path.Combine(AppDataRoot, LegacyAppFolderName);
            var currentRoot = Path.Combine(AppDataRoot, CurrentAppFolderName);

            if (!legacyPath.StartsWith(legacyRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            newPath = legacyPath.Replace(legacyRoot, currentRoot, StringComparison.OrdinalIgnoreCase);
            return true;
        }

        private static bool TryMoveDirectory(string legacyPath, string newPath)
        {
            try
            {
                if (Directory.Exists(newPath))
                {
                    return true;
                }

                var parent = Path.GetDirectoryName(newPath);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                if (!Directory.Exists(legacyPath))
                {
                    Directory.CreateDirectory(newPath);
                    return true;
                }

                Directory.Move(legacyPath, newPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryMoveFile(string legacyPath, string newPath)
        {
            try
            {
                var parent = Path.GetDirectoryName(newPath);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                if (File.Exists(newPath))
                {
                    return true;
                }

                if (!File.Exists(legacyPath))
                {
                    return true;
                }

                File.Move(legacyPath, newPath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
