using System;
using Microsoft.Extensions.Logging;
using Axis.Sync.Modules.MeterReadings.Domain;

namespace Axis.Sync.Modules.MeterReadings.Transformers;

public static class MeterNormalizer
{
    public static MeterReadingItem FromPromun(RawPromunMeterEntity entity, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(entity.m_serial_no);

        if (!decimal.TryParse(entity.curr_reading, out var parsedValue))
        {
            logger?.LogWarning("Failed to parse curr_reading '{CurrReading}' for meter {SerialNo}. Using 0.",
                entity.curr_reading, entity.m_serial_no);
        }

        return new MeterReadingItem(
            MeterSerialNumber: entity.m_serial_no,
            SourceErpType: "PROMUN",
            ConsumptionValue: parsedValue,
            CaptureDateTime: entity.run_date == default ? DateTime.UtcNow : entity.run_date,
            FieldOperatorId: string.IsNullOrWhiteSpace(entity.operator_name) ? "SYSTEM_PROMUN" : entity.operator_name,
            AccountNumber: entity.acc_no ?? "UNKNOWN_ACC"
        );
    }
}