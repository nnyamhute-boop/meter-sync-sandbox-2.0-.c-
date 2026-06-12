using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Axis.Sync.Modules.MeterReadings.Domain;

namespace Axis.Sync.Modules.MeterReadings.Infrastructure;

public class PromunMeterRepository : IMeterRepository
{
    private readonly ILogger<PromunMeterRepository> _logger;
    private readonly MeterSyncOptions _options;
    private readonly IDbConnection _sourceConnection;   // e.g., ODBC to Progress OpenEdge

    public PromunMeterRepository(
        IOptions<MeterSyncOptions> options,
        ILogger<PromunMeterRepository> logger,
        IDbConnection sourceConnection)
    {
        _options = options.Value;
        _logger = logger;
        _sourceConnection = sourceConnection;
    }

    public async Task<List<RawPromunMeterEntity>> GetPendingPromunReadingsAsync(
        int batchSize, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching up to {BatchSize} pending records from Promun", batchSize);

        // Replace with actual Progress OpenEdge SQL
        const string sql = @"
            SELECT m_serial_no, curr_reading, run_date, operator_name, acc_no
            FROM PUB.muncmf
            WHERE synced_flag = 0
            ORDER BY run_date
            LIMIT @BatchSize";

        var result = await _sourceConnection.QueryAsync<RawPromunMeterEntity>(
            new CommandDefinition(sql, new { BatchSize = batchSize }, cancellationToken: cancellationToken));

        return result.AsList();
    }

    public async Task<int> SaveToPostgresTargetAsync(
        List<MeterReadingItem> items, CancellationToken cancellationToken)
    {
        if (items == null || items.Count == 0) return 0;

        const string sql = @"
            INSERT INTO meter_readings (
                meter_serial_number, source_erp_type, consumption_value,
                capture_date_time, field_operator_id, account_number
            ) VALUES (
                @MeterSerialNumber, @SourceErpType, @ConsumptionValue,
                @CaptureDateTime, @FieldOperatorId, @AccountNumber
            )
            ON CONFLICT (meter_serial_number, capture_date_time) 
            DO UPDATE SET
                consumption_value = EXCLUDED.consumption_value,
                field_operator_id = EXCLUDED.field_operator_id,
                account_number = EXCLUDED.account_number,
                updated_at = CURRENT_TIMESTAMP";

        using var conn = new NpgsqlConnection(_options.PostgresConnectionString);
        await conn.OpenAsync(cancellationToken);
        using var tx = await conn.BeginTransactionAsync(cancellationToken);

        int affected = 0;
        foreach (var item in items)
        {
            affected += await conn.ExecuteAsync(sql, item, transaction: tx);
        }

        await tx.CommitAsync(cancellationToken);
        _logger.LogInformation("Upserted {Count} records into PostgreSQL", affected);
        return affected;
    }
}