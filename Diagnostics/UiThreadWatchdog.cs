using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace HomeoMahanagarLabelCleanV2.Diagnostics;

/// <summary>
/// UI THREAD RESPONSIVENESS WATCHDOG (DEBUG-ONLY)
/// 
/// Monitors the WPF Dispatcher for stalls exceeding a threshold.
/// Logs warnings when UI thread responsiveness degrades.
/// 
/// Purpose:
/// - Detect rendering or layout operations that block the UI thread
/// - Identify performance bottlenecks in preview/export flows
/// - Zero overhead in Release builds
/// 
/// Usage:
///   UiThreadWatchdog.Start(); // Call once in App startup
/// 
/// Warnings logged when UI thread stalls > 100ms.
/// Does NOT throw exceptions - only logs diagnostics.
/// </summary>
#if DEBUG
public static class UiThreadWatchdog
{
    private const long STALL_THRESHOLD_MS = 100;
    private static Timer? _watchdogTimer;
    private static Dispatcher? _dispatcher;
    private static readonly Stopwatch _lastCheckStopwatch = Stopwatch.StartNew();
    private static volatile bool _isChecking;

    /// <summary>
    /// Start monitoring UI thread responsiveness.
    /// Safe to call multiple times (idempotent).
    /// </summary>
    public static void Start(Dispatcher? dispatcher = null)
    {
        if (_watchdogTimer != null)
            return; // Already started

        _dispatcher = dispatcher ?? System.Windows.Application.Current?.Dispatcher;
        if (_dispatcher == null)
        {
            Debug.WriteLine("UiThreadWatchdog: No Dispatcher available, watchdog disabled.");
            return;
        }

        // Check every 50ms
        _watchdogTimer = new Timer(CheckUiThreadResponsiveness, null, 50, 50);
        
        Logging.SessionEventLogger.LogInfo("UiThreadWatchdog", "Started monitoring UI thread responsiveness");
    }

    /// <summary>
    /// Stop monitoring (optional cleanup).
    /// </summary>
    public static void Stop()
    {
        _watchdogTimer?.Dispose();
        _watchdogTimer = null;
        
        Logging.SessionEventLogger.LogInfo("UiThreadWatchdog", "Stopped monitoring");
    }

    private static void CheckUiThreadResponsiveness(object? state)
    {
        if (_isChecking || _dispatcher == null)
            return;

        _isChecking = true;
        var checkStarted = Stopwatch.StartNew();

        try
        {
            // Post a low-priority operation to the UI thread
            // If it takes too long to execute, the UI thread is stalled
            var responseReceived = false;
            var responseTime = 0L;

            _dispatcher.BeginInvoke(new Action(() =>
            {
                responseTime = checkStarted.ElapsedMilliseconds;
                responseReceived = true;
            }), DispatcherPriority.Background);

            // Wait up to STALL_THRESHOLD_MS for response
            var timeout = Stopwatch.StartNew();
            while (!responseReceived && timeout.ElapsedMilliseconds < STALL_THRESHOLD_MS + 50)
            {
                Thread.Sleep(10);
            }

            if (!responseReceived)
            {
                // UI thread did not respond within threshold
                var stallDuration = checkStarted.ElapsedMilliseconds;
                LogStall(stallDuration);
            }
            else if (responseTime > STALL_THRESHOLD_MS)
            {
                // UI thread responded but took too long
                LogStall(responseTime);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UiThreadWatchdog: Check failed: {ex.Message}");
        }
        finally
        {
            _isChecking = false;
        }
    }

    private static void LogStall(long stallDurationMs)
    {
        var message = $"UI thread stall detected: {stallDurationMs}ms (threshold: {STALL_THRESHOLD_MS}ms)";
        
        // Log to both SessionEventLogger and AppLogger
        Logging.SessionEventLogger.LogWarning("UiThreadWatchdog", message);
        
        try
        {
            Logging.AppLogger.Log($"[UI-STALL] {message}");
        }
        catch { }

        // Also write to Debug output
        Debug.WriteLine($"⚠️ {message}");
        
        // Optional: capture stack trace of UI thread (advanced diagnostic)
        try
        {
            if (_dispatcher != null)
            {
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    var stackTrace = Environment.StackTrace;
                    Debug.WriteLine($"UI Thread Stack at stall:\n{stackTrace}");
                }), DispatcherPriority.Background);
            }
        }
        catch { }
    }
}
#else
// Release build: UI Thread Watchdog is a no-op
public static class UiThreadWatchdog
{
    public static void Start(System.Windows.Threading.Dispatcher? dispatcher = null) { }
    public static void Stop() { }
}
#endif
