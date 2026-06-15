using System;
using MeterSync.Core.Models;
using MeterSync.Core.Transformers;
using Xunit;

namespace MeterSync.Tests
{
    public class MeterNormalizerTests
    {
        [Fact]
        public void Normalize_FillsDefaultsAndClampsNegative()
        {
            var normalizer = new MeterNormalizer();
            var input = new IncomingReading { MeterId = null, Timestamp = default, Value = -5.3m, Source = null };
            var result = normalizer.Normalize(input);

            Assert.NotNull(result);
            Assert.Equal("unknown", result.MeterId);
            Assert.True(result.Timestamp != default);
            Assert.Equal(0m, result.Value);
            Assert.Equal("realtime-webhook", result.Source);
            Assert.Equal("good", result.Quality);
        }
    }
}
