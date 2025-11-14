using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using InsightCleanerAI.Models;
using InsightCleanerAI.Resources;

namespace InsightCleanerAI.Persistence
{
    public class SqliteInsightStore : IInsightStore
    {
        private readonly string _connectionString;

        public SqliteInsightStore(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("Database path is required", nameof(databasePath));
            }

            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath
            }.ToString();

            Initialize();
        }

        public async Task<NodeInsight?> GetAsync(string path, long sizeBytes, bool ignoreSizeMismatch, CancellationToken cancellationToken)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT SizeBytes, Classification, Confidence, Summary, Recommendation, IsOffline
                FROM FileInsights
                WHERE Path = $path;";
            command.Parameters.AddWithValue("$path", path);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var storedSize = reader.GetInt64(0);
            if (!ignoreSizeMismatch && storedSize != sizeBytes)
            {
                return null;
            }

            var classificationText = reader.GetString(1);
            var confidence = reader.IsDBNull(2) ? (double?)null : reader.GetDouble(2);
            var summary = reader.IsDBNull(3) ? null : reader.GetString(3);
            var recommendation = reader.IsDBNull(4) ? null : reader.GetString(4);
            var isOffline = reader.IsDBNull(5) ? true : reader.GetInt32(5) == 1;

            if (!Enum.TryParse<NodeClassification>(classificationText, out var classification))
            {
                classification = NodeClassification.Unknown;
            }

            return new NodeInsight(
                classification,
                summary ?? Strings.DefaultInsightPlaceholder,
                confidence ?? 0,
                recommendation ?? string.Empty,
                isOffline);
        }

        public async Task SaveAsync(string path, long sizeBytes, NodeInsight insight, CancellationToken cancellationToken)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO FileInsights (Path, SizeBytes, Classification, Confidence, Summary, Recommendation, UpdatedAt, IsOffline)
                VALUES ($path, $size, $classification, $confidence, $summary, $recommendation, $updatedAt, $isOffline)
                ON CONFLICT(Path) DO UPDATE SET
                    SizeBytes = excluded.SizeBytes,
                    Classification = excluded.Classification,
                    Confidence = excluded.Confidence,
                    Summary = excluded.Summary,
                    Recommendation = excluded.Recommendation,
                    UpdatedAt = excluded.UpdatedAt,
                    IsOffline = excluded.IsOffline;";

            command.Parameters.AddWithValue("$path", path);
            command.Parameters.AddWithValue("$size", sizeBytes);
            command.Parameters.AddWithValue("$classification", insight.Classification.ToString());
            command.Parameters.AddWithValue("$confidence", insight.Confidence);
            command.Parameters.AddWithValue("$summary", insight.Summary ?? string.Empty);
            command.Parameters.AddWithValue("$recommendation", insight.Recommendation ?? string.Empty);
            command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$isOffline", insight.IsOffline ? 1 : 0);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private void Initialize()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS FileInsights (
                    Path TEXT PRIMARY KEY,
                    SizeBytes INTEGER NOT NULL,
                    Classification TEXT NOT NULL,
                    Confidence REAL,
                    Summary TEXT,
                    Recommendation TEXT,
                    UpdatedAt TEXT NOT NULL,
                    IsOffline INTEGER NOT NULL DEFAULT 0
                );";
            command.ExecuteNonQuery();

            command.CommandText = @"ALTER TABLE FileInsights ADD COLUMN IsOffline INTEGER NOT NULL DEFAULT 0;";
            try
            {
                command.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // Column already exists; ignore.
            }
        }
    }
}
