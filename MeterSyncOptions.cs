namespace Axis.Sync.Modules.MeterReadings.Infrastructure;

public class MeterSyncOptions
{
    public string PostgresConnectionString { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 100;
}