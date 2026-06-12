using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PromunSync.Core.Configuration;
using PromunSync.Data.Configuration;
using PromunSync.Data.ConnectionManagement;
using PromunSync.Data.Secrets;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromunSync.Service;

/// <summary>
/// Performs slow startup initialization (DB connections, migrations, job registration)
/// inside the host lifecycle so the Windows SCM handshake completes promptly.
/// </summary>
public class StartupInitializationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly IOptions<DynamicConfigurationOptions> _dynamicConfigOptions;
    private readonly IOptions<HangfireOptions> _hangfireOptions;
    private readonly IOptions<FeatureFlagsOptions> _featureFlagsOptions;
    private readonly ILogger<StartupInitializationService> _logger;

    public StartupInitializationService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IOptions<DynamicConfigurationOptions> dynamicConfigOptions,
        IOptions<HangfireOptions> hangfireOptions,
        IOptions<FeatureFlagsOptions> featureFlagsOptions,
        ILogger<StartupInitializationService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _dynamicConfigOptions = dynamicConfigOptions;
        _hangfireOptions = hangfireOptions;
        _featureFlagsOptions = featureFlagsOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small yield so the host can finish starting and signal the SCM
        await Task.Yield();

        _logger.LogInformation("Running startup initialization...");

        await InitializeDynamicConfigAsync();
        await MigrateSecretsAsync();
        await TestDatabaseConnectionsAsync(stoppingToken);
        RegisterHangfireJobs();

        _logger.LogInformation("Startup initialization complete");
    }

    private async Task InitializeDynamicConfigAsync()
    {
        var options = _dynamicConfigOptions.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("Dynamic configuration disabled, using JSON-only configuration");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var configRepository = scope.ServiceProvider.GetRequiredService<IConfigurationRepository>();

            await configRepository.InitializeAsync();
            _logger.LogInformation("Dynamic configuration database initialized");

            var deletedCount = await configRepository.CleanupAuditLogsAsync(options.AuditRetentionDays);
            if (deletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} old audit log entries", deletedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize dynamic configuration database. Using JSON-only configuration.");
        }
    }

    private async Task MigrateSecretsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var secretsRepository = scope.ServiceProvider.GetRequiredService<ISecretsRepository>();

            // Cleaned up to leverage DI container directly instead of mixing 'new' operators
            var migrationService = scope.ServiceProvider.GetService<SecretsMigrationService>() 
                ?? new SecretsMigrationService(
                    secretsRepository,
                    _configuration,
                    scope.ServiceProvider.GetRequiredService<ILogger<SecretsMigrationService>>());

            await migrationService.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to migrate sensitive configuration. Connection strings may not be encrypted.");
        }
    }

    private async Task TestDatabaseConnectionsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<ConnectionFactory>();

        _logger.LogInformation("Testing database connections...");
        var results = await connectionFactory.TestAllConnectionsAsync(stoppingToken);

        foreach (var result in results)
        {
            if (result.Value.Success)
            {
                _logger.LogInformation("  {Database} connection: SUCCESS", result.Key);
            }
            else
            {
                _logger.LogWarning("  {Database} connection: FAILED - {Error}", result.Key, result.Value.ErrorMessage);
            }
        }

        var failedConnections = results.Where(r => !r.Value.Success).Select(r => r.Key).ToList();
        if (failedConnections.Any())
        {
            _logger.LogWarning("Some database connections failed: {FailedDatabases}", string.Join(", ", failedConnections));
            _logger.LogWarning("Service will start but jobs may fail until connections are restored");
        }
        else
        {
            _logger.LogInformation("All database connections successful");
        }
    }

    private void RegisterHangfireJobs()
    {
        using var scope = _scopeFactory.CreateScope();
        var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecRecurringJobManager>() ?? scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        var hangfireOptions = _hangfireOptions.Value;
        var featureFlags = _featureFlagsOptions.Value;

        _logger.LogInformation("Configuring Hangfire recurring jobs...");

        // FIX: Changed CancellationToken.None to 'default' to prevent Hangfire lambda serialization errors
        recurringJobManager.AddOrUpdate<PromunSync.Jobs.GetTransactionsJob>(
            "get-transactions",
            job => job.ExecuteAsync(default),
            hangfireOptions.Schedules.GetTransactions,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

        recurringJobManager.AddOrUpdate<PromunSync.Jobs.PostAccountDetailsJob>(
            "post-account-details",
            job => job.ExecuteAsync(default),
            hangfireOptions.Schedules.PostAccountDetails,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

        recurringJobManager.AddOrUpdate<PromunSync.Jobs.PostRevcollJob>(
            "post-revcoll",
            job => job.ExecuteAsync(default),
            hangfireOptions.Schedules.PostRevcoll,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

        recurringJobManager.AddOrUpdate<PromunSync.Jobs.SyncExchangeRatesJob>(
            "sync-exchange-rates",
            job => job.ExecuteAsync(default),
            hangfireOptions.Schedules.SyncExchangeRates,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

        if (featureFlags.Master.RevcollEnabled)
        {
            recurringJobManager.AddOrUpdate<PromunSync.Jobs.PostRevcollJob>(
                "sync-debtor-types",
                job => job.ExecuteDebtorTypeSyncAsync(default),
                hangfireOptions.Schedules.SyncDebtorTypes,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
            _logger.LogInformation("Debtor types sync job registered: sync-debtor-types");
        }

        // UPDATE: Registered your new Meter Reading Sync Feature background job here
        recurringJobManager.AddOrUpdate<PromunSync.Jobs.Services.IMeterReadingSyncService>(
            "sync-meter-readings",
            job => job.SyncPendingMeterReadingsAsync(default),
            hangfireOptions.Schedules.SyncMeterReadings ?? "0 */2 * * *", // Default cron: Every 2 hours if not configured
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        _logger.LogInformation("Meter Reading Sync job registered successfully: sync-meter-readings");

        // Remove legacy outbox jobs (no longer used)
        recurringJobManager.RemoveIfExists("outbox-processor-online");
        recurringJobManager.RemoveIfExists("outbox-processor-revcoll");
        recurringJobManager.RemoveIfExists("outbox-processor-promun-rec");
        recurringJobManager.RemoveIfExists("outbox-cleanup");

        _logger.LogInformation("Hangfire recurring jobs configured: get-transactions, post-account-details, post-revcoll, sync-exchange-rates, sync-meter-readings");
        _logger.LogInformation("Hangfire Dashboard available at: http://localhost:5000{DashboardUrl}", hangfireOptions.DashboardUrl);
    }
}