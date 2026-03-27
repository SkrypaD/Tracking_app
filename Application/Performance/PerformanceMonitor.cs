// <copyright file="PerformanceMonitor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Diagnostics;

namespace CartridgeApp.Application.Performance;

/// <summary>
/// Serilog-backed implementation of <see cref="IPerformanceMonitor"/>.
///
/// Every completed operation produces a structured log entry at the
/// <c>Information</c> level (or <c>Warning</c> when the operation exceeds
/// <see cref="SlowOperationThresholdMs"/>).  The entry carries the following
/// properties that can be indexed and queried in any log-aggregation tool:
///
/// <code>
/// Operation      — dot-separated operation name
/// ElapsedMs      — wall-clock milliseconds (integer)
/// Success        — true / false
/// FailReason     — non-empty only when Success=false
/// SlowOperation  — true when ElapsedMs >= SlowOperationThresholdMs
/// </code>
///
/// Example Serilog output (console sink):
/// <code>
/// [12:34:56 INF] PERF CartridgeService.GetAll completed in 14 ms
///                {"Operation":"CartridgeService.GetAll","ElapsedMs":14,"Success":true,
///                 "SlowOperation":false,"PrinterId":null,"CompanyId":null}
/// </code>
/// </summary>
public sealed class PerformanceMonitor : IPerformanceMonitor
{
    /// <summary>
    /// Operations taking longer than this threshold are logged at
    /// <c>Warning</c> level and marked with <c>SlowOperation=true</c>.
    /// Override via appsettings: <c>"Performance:SlowThresholdMs": 200</c>.
    /// </summary>
    public const int SlowOperationThresholdMs = 200;

    private readonly ILogger<PerformanceMonitor> _logger;

    public PerformanceMonitor(ILogger<PerformanceMonitor> logger) => _logger = logger;

    /// <inheritdoc/>
    public IOperationTimer Start(
        string operationName,
        IReadOnlyDictionary<string, object>? context = null) =>
        new OperationTimer(operationName, context, _logger);

    // ── Inner timer ────────────────────────────────────────────────────────────

    private sealed class OperationTimer : IOperationTimer
    {
        private readonly string _name;
        private readonly IReadOnlyDictionary<string, object>? _context;
        private readonly ILogger _logger;
        private readonly Stopwatch _sw;
        private string? _failReason;
        private bool _disposed;

        public OperationTimer(
            string name,
            IReadOnlyDictionary<string, object>? context,
            ILogger logger)
        {
            _name = name;
            _context = context;
            _logger = logger;
            _sw = Stopwatch.StartNew();
        }

        public TimeSpan Elapsed => _sw.Elapsed;

        public void Fail(string reason) => _failReason = reason;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _sw.Stop();

            var ms = (int)_sw.ElapsedMilliseconds;
            var success = _failReason is null;
            var isSlow = ms >= SlowOperationThresholdMs;

            // Build a flat properties dictionary for structured logging
            using var scope = _logger.BeginScope(BuildScopeProperties(ms, success, isSlow));

            if (isSlow || !success)
            {
                _logger.LogWarning(
                    "PERF {Operation} {Result} in {ElapsedMs} ms{Slow}",
                    _name,
                    success ? "completed" : "FAILED",
                    ms,
                    isSlow ? " [SLOW]" : string.Empty);
            }
            else
            {
                _logger.LogInformation(
                    "PERF {Operation} completed in {ElapsedMs} ms",
                    _name, ms);
            }
        }

        private Dictionary<string, object> BuildScopeProperties(int ms, bool success, bool isSlow)
        {
            var props = new Dictionary<string, object>
            {
                ["Operation"]     = _name,
                ["ElapsedMs"]     = ms,
                ["Success"]       = success,
                ["SlowOperation"] = isSlow,
            };

            if (_failReason is not null)
                props["FailReason"] = _failReason;

            if (_context is not null)
                foreach (var (k, v) in _context)
                    props[k] = v;

            return props;
        }
    }
}