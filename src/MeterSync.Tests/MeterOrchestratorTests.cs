using System;
using System.Threading.Tasks;
using MeterSync.Core.Interfaces;
using MeterSync.Core.Models;
using MeterSync.Core.Orchestrator;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MeterSync.Tests
{
    class FakeWriter : IWriter
    {
        public NormalizedReading? LastCommitted { get; private set; }
        public Task CommitAsync(NormalizedReading reading)
        {
            LastCommitted = reading;
            return Task.CompletedTask;
        }
    }

    public class MeterOrchestratorTests
    {
        [Fact]
        public async Task ProcessAsync_NormalizesAndCommits()
        {
            var writer = new FakeWriter();
            var orchestrator = new MeterOrchestrator(writer, NullLogger<MeterOrchestrator>.Instance);

            var incoming = new IncomingReading { MeterId = "m1", Timestamp = DateTime.UtcNow, Value = 12.34m, Source = "test" };
            await orchestrator.ProcessAsync(incoming);

            Assert.NotNull(writer.LastCommitted);
            Assert.Equal("m1", writer.LastCommitted.MeterId);
            Assert.Equal(12.34m, writer.LastCommitted.Value);
        }
    }
}
