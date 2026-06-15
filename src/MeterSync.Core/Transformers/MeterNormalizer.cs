using System;
using MeterSync.Core.Models;

namespace MeterSync.Core.Transformers
{
    public class MeterNormalizer
    {
        public NormalizedReading Normalize(IncomingReading input)
        {
            var normalized = new NormalizedReading
            {
                MeterId = input.MeterId ?? "unknown",
                Timestamp = input.Timestamp == default ? DateTime.UtcNow : input.Timestamp,
                Value = input.Value,
                Source = input.Source ?? "realtime-webhook",
                Quality = "good"
            };

            // basic normalization: clamp negative values
            if (normalized.Value < 0) normalized.Value = 0;

            return normalized;
        }
    }
}
