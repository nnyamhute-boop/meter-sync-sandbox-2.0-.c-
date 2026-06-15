using System;

namespace MeterSync.Core.Models
{
    public class IncomingReading
    {
        public string? MeterId { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal Value { get; set; }
        public string? Source { get; set; }
        public string? RawPayload { get; set; }
    }

    public class NormalizedReading
    {
        public string MeterId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public decimal Value { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Quality { get; set; } = "good";
    }
}
