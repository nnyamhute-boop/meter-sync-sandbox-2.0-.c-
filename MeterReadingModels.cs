using System;

namespace Axis.Sync.Modules.MeterReadings.Domain;

public record MeterReadingItem(
    string MeterSerialNumber,
    string SourceErpType,       // "PROMUN" or "SAGE"
    decimal ConsumptionValue,
    DateTime CaptureDateTime,
    string FieldOperatorId,
    string AccountNumber
);

public record RawPromunMeterEntity(
    string m_serial_no,
    string curr_reading,
    DateTime run_date,
    string operator_name,
    string acc_no
);

public record MeterSyncSummary(
    int TotalProcessed,
    int TotalCommitted,
    bool IsSuccess,
    string DiagnosticMessage = ""
);