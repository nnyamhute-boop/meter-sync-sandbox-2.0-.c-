using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MeterSync.Core.Interfaces;
using MeterSync.Core.Models;
using MeterSync.Core.Orchestrator;
using MeterSync.Writers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(config => config.AddConsole());

// Writer: read connection string from env METER_SYNC_PG
var pg = Environment.GetEnvironmentVariable("METER_SYNC_PG");
builder.Services.AddSingleton<IWriter>(sp => new PostgresWriter(pg, sp.GetService<ILogger<PostgresWriter>>()));
builder.Services.AddSingleton<IMeterOrchestrator, MeterOrchestrator>();

var app = builder.Build();

app.MapPost("/api/v1/readings", async (IncomingReading reading, IMeterOrchestrator orchestrator, ILogger<Program> logger) =>
{
    if (reading == null)
    {
        return Results.BadRequest();
    }

    logger.LogInformation("Received reading for {MeterId}", reading.MeterId);
    await orchestrator.ProcessAsync(reading);
    return Results.Accepted();
});

app.MapGet("/", () => "MeterSync API - POST /api/v1/readings to send realtime readings");

app.Run();
