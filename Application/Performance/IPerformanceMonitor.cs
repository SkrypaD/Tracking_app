// <copyright file="IPerformanceMonitor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CartridgeApp.Application.Performance;

/// <summary>
/// Abstraction for timing code sections and recording performance metrics.
/// Implementations write structured log entries that can be aggregated by
/// any log-analysis tool (Seq, Grafana Loki, Elasticsearch, etc.).
/// </summary>
public interface IPerformanceMonitor
{
    /// <summary>
    /// Starts timing an operation.  Dispose the returned handle to stop the
    /// timer and flush the metric to the log.
    /// </summary>
    /// <param name="operationName">
    /// Dot-separated name that identifies the operation, e.g.
    /// <c>"CartridgeService.GetAll"</c> or <c>"Dashboard.Query"</c>.
    /// </param>
    /// <param name="context">
    /// Optional dictionary of additional key-value pairs that will be included
    /// in the structured log entry (e.g. filter arguments, row counts).
    /// </param>
    IOperationTimer Start(string operationName, IReadOnlyDictionary<string, object>? context = null);
}

/// <summary>
/// A disposable timer handle returned by <see cref="IPerformanceMonitor.Start"/>.
/// Call <see cref="IDisposable.Dispose"/> — or use inside a <c>using</c> block —
/// to record the elapsed time.
/// </summary>
public interface IOperationTimer : IDisposable
{
    /// <summary>Gets the elapsed time since the timer was started.</summary>
    TimeSpan Elapsed { get; }

    /// <summary>
    /// Marks the operation as failed.  The log entry will include
    /// <c>Success=false</c> and the provided reason string.
    /// </summary>
    void Fail(string reason);
}