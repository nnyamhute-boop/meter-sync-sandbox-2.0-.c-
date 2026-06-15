using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MeterSync.Core.Interfaces;
using MeterSync.Core.Models;
using Npgsql;

namespace MeterSync.Writers
{
    public class PostgresWriter : IWriter, IDisposable
    {
        private readonly string? _connectionString;
        private readonly ILogger<PostgresWriter>? _logger;
        private NpgsqlConnection? _conn;

        public PostgresWriter(string? connectionString, ILogger<PostgresWriter>? logger = null)
        {
            _connectionString = connectionString;
            _logger = logger;
            if (!string.IsNullOrEmpty(_connectionString))
            {
                _conn = new NpgsqlConnection(_connectionString);
                _conn.Open();
            }
            else
            {
                _logger?.LogWarning("No Postgres connection string provided. Writer will log SQL instead of committing.");
            }
        }

        public async Task CommitAsync(NormalizedReading reading)
        {
            if (reading == null) throw new ArgumentNullException(nameof(reading));

            var sql = "INSERT INTO meter_readings (meter_id, timestamp, value, source, quality) VALUES (@m, @t, @v, @s, @q);";

            if (_conn == null)
            {
                _logger?.LogInformation("[DryRun] SQL: {Sql} Params: {MeterId},{Ts},{Val},{Src},{Q}", sql, reading.MeterId, reading.Timestamp, reading.Value, reading.Source, reading.Quality);
                await Task.CompletedTask;
                return;
            }

            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("m", reading.MeterId);
            cmd.Parameters.AddWithValue("t", reading.Timestamp);
            cmd.Parameters.AddWithValue("v", reading.Value);
            cmd.Parameters.AddWithValue("s", reading.Source);
            cmd.Parameters.AddWithValue("q", reading.Quality);
            await cmd.ExecuteNonQueryAsync();
            _logger?.LogInformation("Committed reading for {MeterId} at {Ts}", reading.MeterId, reading.Timestamp);
        }

        public void Dispose()
        {
            _conn?.Dispose();
        }
    }
}
