using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Axis.Sync.Modules.MeterReadings.Domain;
using Axis.Sync.Modules.MeterReadings.Infrastructure;
using Axis.Sync.Modules.MeterReadings.Transformers;

namespace Axis.Sync.Modules.MeterReadings.Services;

public class MeterReadingSyncService
{
    private readonly IMeterRepository _repository;
    private readonly ILogger<MeterReadingSyncService> _logger;
    private readonly MeterSyncOptions _options;

    public MeterReadingSyncService(
        IMeterRepository repository,
        ILogger<MeterReadingSyncService> logger,
        IOptions<MeterSyncOptions> options)
    {
        _repository = repository;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<MeterSyncSummary> RunSinkingPipelineAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting sync cycle (batch size: {BatchSize})", _options.BatchSize);

        try
        {
            var rawEntities = await _repository.GetPendingPromunReadingsAsync(_options.BatchSize, cancellationToken);

            if (rawEntities == null || rawEntities.Count == 0)
            {
                _logger.LogInformation("No pending records found.");
                return new MeterSyncSummary(0, 0, true, "Empty batch");
            }

            var cleanPayloads = rawEntities
                .Select(entity => MeterNormalizer.FromPromun(entity, _logger))
                .ToList();

            int recordsWritten = await _repository.SaveToPostgresTargetAsync(cleanPayloads, cancellationToken);

            _logger.LogInformation("Sync completed: {Processed} processed, {Committed} committed",
                rawEntities.Count, recordsWritten);
            return new MeterSyncSummary(rawEntities.Count, recordsWritten, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync pipeline failed");
            return new MeterSyncSummary(0, 0, false, ex.Message);
        }
    }
}