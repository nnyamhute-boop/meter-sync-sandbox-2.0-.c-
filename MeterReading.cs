using System;

namespace Axis.Sync.MeterReading;

// The clean target format expected by the central database server
public record NormalizedMeterReading(
    string MeterId,
    string ClientErpSource,
    decimal ConsumptionValue,
    DateTime ReadingDate,
    string CapturedBy,
    string Status
);

// What raw unorganized rows from Promun look like
public record RawPromunRow(
    string MSerialNo,
    string CurrReading,
    DateTime RunDate,
    string OperatorName
);