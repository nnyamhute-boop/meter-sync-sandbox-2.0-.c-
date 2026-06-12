using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Axis.Sync.Modules.MeterReadings.Infrastructure;
using Axis.Sync.Modules.MeterReadings.Services;
using System.Data;
using System.Data.Odbc;

namespace Axis.Sync.Modules.MeterReadings;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMeterReadingSyncModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<MeterSyncOptions>(config.GetSection("MeterReadingSync"));
        services.AddScoped<IMeterRepository, PromunMeterRepository>();
        services.AddScoped<MeterReadingSyncService>();

        // Register the source DB connection (example using ODBC)
        services.AddScoped<IDbConnection>(sp =>
        {
            var connectionString = config.GetConnectionString("PromunSource");
            return new OdbcConnection(connectionString);
        });

        return services;
    }
}