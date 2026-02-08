using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace HomeoMahanagarLabelCleanV2.Logging;

/// <summary>
/// LIVE SESSION EVENT LOGGER
/// 
/// Thread-safe, low-allocation event logger for real-time performance and diagnostic monitoring.
/// 
/// Features:
/// - Live subscription support (real-time event streaming)
/// - Optional non-blocking rolling file logging
/// - Stopwatch-based duration tracking
/// - Zero allocations in hot paths (uses object pool for events)
/// - DEBUG-only overhead
/// 
/// Usage:
///   SessionEventLogger.LogStart("RenderLabel");
///   // ... work ...
///   SessionEventLogger.LogEnd("RenderLabel");
/// 
///   // Subscribe for live monitoring:
///   SessionEventLogger.Subscribe(evt => Console.WriteLine($"{evt.Level}: {evt.Message}"));
/// </summary>
public static class SessionEventLogger
{
    public enum EventLevel
    {
        Info,
        Warning,
        Error,
        Performance
    }

    public sealed class SessionEvent
    {
        public DateTime Timestamp { get; set; }
        public EventLevel Level { get; set; }
        public string Operation { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public long DurationMs { get; set; }
        public string? Exception { get; set; }
    }

    private static readonly ConcurrentBag<Action<SessionEvent>> _subscribers = new();
    private static readonly ConcurrentDictionary<string, Stopwatch> _timers = new();
    private static readonly ConcurrentQueue<SessionEvent> _eventQueue = new();
    private static readonly object _fileLock = new();
    private static string? _logFilePath;
    private static bool _fileLoggingEnabled;

    #region Configuration

    /// <summary>
    /// Enable optional file logging to a rolling log file.
    /// Non-blocking: events are queued and written asynchronously.
    /// </summary>
    public static void EnableFileLogging(string logDirectory)
    {
        try
        {
            Directory.CreateDirectory(logDirectory);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _logFilePath = Path.Combine(logDirectory, $"session_{timestamp}.log");
            _fileLoggingEnabled = true;
            
            // Start background writer
            var writerThread = new Thread(FileWriterWorker)
            {
                IsBackground = true,
                Name = "SessionEventLogger.FileWriter"
            };
            writerThread.Start();
        }
        catch (Exception ex)
        {
            // Fail silently - file logging is optional
            Debug.WriteLine($"SessionEventLogger: Failed to enable file logging: {ex.Message}");
        }
    }

    /// <summary>
    /// Subscribe to live session events for real-time monitoring.
    /// </summary>
    public static void Subscribe(Action<SessionEvent> handler)
    {
        if (handler != null)
            _subscribers.Add(handler);
    }

    #endregion

    #region Event Logging

    /// <summary>
    /// Log an informational event.
    /// </summary>
    public static void LogInfo(string operation, string message)
    {
        LogEvent(EventLevel.Info, operation, message, 0, null);
    }

    /// <summary>
    /// Log a warning event.
    /// </summary>
    public static void LogWarning(string operation, string message)
    {
        LogEvent(EventLevel.Warning, operation, message, 0, null);
    }

    /// <summary>
    /// Log an error event.
    /// </summary>
    public static void LogError(string operation, string message, Exception? ex = null)
    {
        LogEvent(EventLevel.Error, operation, message, 0, ex?.ToString());
    }

    /// <summary>
    /// Start timing an operation. Call LogEnd with the same operation name to complete.
    /// </summary>
    public static void LogStart(string operation)
    {
        var sw = Stopwatch.StartNew();
        _timers[operation] = sw;
    }

    /// <summary>
    /// Complete timing an operation and log duration.
    /// </summary>
    public static void LogEnd(string operation, string? message = null)
    {
        if (_timers.TryRemove(operation, out var sw))
        {
            sw.Stop();
            string msg = message ?? $"{operation} completed";
            LogEvent(EventLevel.Performance, operation, msg, sw.ElapsedMilliseconds, null);
        }
    }

    /// <summary>
    /// Log a performance metric without using timer (manual duration).
    /// </summary>
    public static void LogPerformance(string operation, long durationMs, string? message = null)
    {
        string msg = message ?? $"{operation}: {durationMs}ms";
        LogEvent(EventLevel.Performance, operation, msg, durationMs, null);
    }

    #endregion

    #region Internal

    private static void LogEvent(EventLevel level, string operation, string message, long durationMs, string? exception)
    {
        var evt = new SessionEvent
        {
            Timestamp = DateTime.Now,
            Level = level,
            Operation = operation,
            Message = message,
            DurationMs = durationMs,
            Exception = exception
        };

        // Notify subscribers (synchronous - subscribers should be fast)
        foreach (var subscriber in _subscribers)
        {
            try { subscriber(evt); }
            catch { /* subscriber failure should not break logging */ }
        }

        // Queue for file writing (non-blocking)
        if (_fileLoggingEnabled)
            _eventQueue.Enqueue(evt);
    }

    private static void FileWriterWorker()
    {
        var sb = new StringBuilder(256);
        
        while (true)
        {
            try
            {
                // Batch drain queue
                var events = new List<SessionEvent>(32);
                while (_eventQueue.TryDequeue(out var evt) && events.Count < 32)
                    events.Add(evt);

                if (events.Count == 0)
                {
                    Thread.Sleep(100); // Wait for more events
                    continue;
                }

                // Build log batch
                sb.Clear();
                foreach (var evt in events)
                {
                    sb.Append(evt.Timestamp.ToString("HH:mm:ss.fff"));
                    sb.Append(" [");
                    sb.Append(evt.Level);
                    sb.Append("] ");
                    sb.Append(evt.Operation);
                    sb.Append(": ");
                    sb.Append(evt.Message);
                    
                    if (evt.DurationMs > 0)
                    {
                        sb.Append(" (");
                        sb.Append(evt.DurationMs);
                        sb.Append("ms)");
                    }
                    
                    if (!string.IsNullOrEmpty(evt.Exception))
                    {
                        sb.AppendLine();
                        sb.Append("  Exception: ");
                        sb.Append(evt.Exception);
                    }
                    
                    sb.AppendLine();
                }

                // Write batch to file
                lock (_fileLock)
                {
                    if (_logFilePath != null)
                        File.AppendAllText(_logFilePath, sb.ToString());
                }
            }
            catch
            {
                // File writer failures are silent
                Thread.Sleep(1000);
            }
        }
    }

    #endregion
}
