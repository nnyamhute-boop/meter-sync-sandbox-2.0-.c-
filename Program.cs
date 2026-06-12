using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Hangfire;
using Axis.Sync.Modules.MeterReadings;
using Axis.Sync.Modules.MeterReadings.Services;

var builder = Host.CreateApplicationBuilder(args);

// Add the Meter Reading module
builder.Services.AddMeterReadingSyncModule(builder.Configuration);

// Add Hangfire (you likely already have this)
builder.Services.AddHangfire(config => config.UseInMemoryStorage());
builder.Services.AddHangfireServer();

var host = builder.Build();

// Schedule recurring job
using (var scope = host.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<MeterReadingSyncService>(
        "meter-reading-sync",
        service => service.RunSinkingPipelineAsync(CancellationToken.None),
        Cron.Hourly);
}

host.Run();