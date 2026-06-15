using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MeterSync.Core.Interfaces;
using MeterSync.Core.Models;
using MeterSync.Core.Transformers;

namespace MeterSync.Core.Orchestrator
{
    public class MeterOrchestrator : IMeterOrchestrator
    {
        private readonly IWriter _writer;
        private readonly MeterNormalizer _normalizer;
        private readonly ILogger<MeterOrchestrator>? _logger;

        public MeterOrchestrator(IWriter writer, ILogger<MeterOrchestrator>? logger = null)
        {
            _writer = writer;
            _normalizer = new MeterNormalizer();
            _logger = logger;
        }

        public async Task ProcessAsync(IncomingReading reading)
        {
            if (reading == null)
            {
                _logger?.LogWarning("Received null reading");
                return;
            }

            try
            {
                var normalized = _normalizer.Normalize(reading);
                _logger?.LogInformation("Normalized reading for {MeterId} at {Ts}", normalized.MeterId, normalized.Timestamp);
                await _writer.CommitAsync(normalized);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing reading");
                throw;
            }
        }
    }
}
