using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace FuzzySearch.Utilities
{
    // Small helper to measure and log durations
    public sealed class OperationTimer : IDisposable
    {
        private readonly Stopwatch _sw;
        private readonly ILogger _logger;
        private readonly string _name;

        public OperationTimer(ILogger logger, string name)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _name = name ?? "operation";
            _sw = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _sw.Stop();
            _logger.LogInformation("{Operation} took {ElapsedMs} ms", _name, _sw.Elapsed.TotalMilliseconds);
        }
    }
}