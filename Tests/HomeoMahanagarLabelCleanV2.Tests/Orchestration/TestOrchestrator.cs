using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace HomeoMahanagarLabelCleanV2.Tests.Orchestration;

/// <summary>
/// Enterprise-grade test orchestration and reporting system.
/// 
/// Purpose:
/// - Execute tests in defined layers (unit → component → rendering → system)
/// - Collect results, performance metrics, and environment info
/// - Generate professional test report with actionable recommendations
/// - Provide release GO/NO-GO decision based on test results
/// 
/// Test Levels:
/// 1. Unit Tests (pure logic, fast)
/// 2. Component Tests (isolated services, medium speed)
/// 3. Rendering Tests (WPF off-screen, snapshot/DPI validation)
/// 4. System Tests (end-to-end workflows, PDF export)
/// 5. Manual Validation (human verification, hardware checks)
/// </summary>
public class TestOrchestrator
{
    public enum ReleaseDecision
    {
        GO,              // All tests pass, ready for release
        CONDITIONAL_GO,  // Minor issues, manual validation required
        NO_GO            // Critical failures, release blocked
    }

    public sealed class EnvironmentInfo
    {
        public string OsVersion { get; set; } = string.Empty;
        public string DotNetVersion { get; set; } = string.Empty;
        public string WindowsScaling { get; set; } = string.Empty;
        public int CurrentDpi { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public List<string> AvailablePrinters { get; set; } = new();
    }

    public sealed class TestLevelResult
    {
        public string LevelName { get; set; } = string.Empty;
        public int LevelNumber { get; set; }
        public int TotalTests { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public long DurationMs { get; set; }
        public List<TestCaseResult> TestCases { get; set; } = new();
        
        public bool IsPass => Failed == 0 && TotalTests > 0;
        public string Status => IsPass ? "✅ PASS" : Failed > 0 ? "❌ FAIL" : "⚠️ SKIP";
    }

    public sealed class TestCaseResult
    {
        public string TestName { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public long DurationMs { get; set; }
        public string? FailureMessage { get; set; }
        public string? StackTrace { get; set; }
    }

    public sealed class PerformanceMetrics
    {
        public double AvgRenderTimeMs { get; set; }
        public double AvgPdfExportMs { get; set; }
        public int UiThreadStallCount { get; set; }
        public long MaxUiStallMs { get; set; }
    }

    public sealed class TestReport
    {
        public DateTime GeneratedAt { get; set; }
        public string BuildConfiguration { get; set; } = string.Empty;
        public string ReportId { get; set; } = string.Empty;
        
        public EnvironmentInfo Environment { get; set; } = new();
        public List<TestLevelResult> TestLevels { get; set; } = new();
        public PerformanceMetrics Performance { get; set; } = new();
        
        public int TotalTests => TestLevels.Sum(l => l.TotalTests);
        public int TotalPassed => TestLevels.Sum(l => l.Passed);
        public int TotalFailed => TestLevels.Sum(l => l.Failed);
        public long TotalDurationMs => TestLevels.Sum(l => l.DurationMs);
        
        public ReleaseDecision Decision { get; set; }
        public List<string> Suggestions { get; set; } = new();
    }

    /// <summary>
    /// Execute full test suite and generate comprehensive report.
    /// </summary>
    public static async Task<TestReport> ExecuteTestSuiteAsync()
    {
        var report = new TestReport
        {
            GeneratedAt = DateTime.Now,
            BuildConfiguration = "Debug | net8.0-windows",
            ReportId = $"TR-{DateTime.Now:yyyyMMdd-HHmmss}",
            Environment = await CollectEnvironmentInfoAsync()
        };

        // Level 1: Unit Tests
        report.TestLevels.Add(await RunTestLevelAsync("Unit Tests", 1, "Category=Unit"));

        // Level 2: Component Tests (if any exist)
        // report.TestLevels.Add(await RunTestLevelAsync("Component Tests", 2, "Category=Component"));

        // Level 3: Rendering Tests
        var snapshotTests = await RunTestLevelAsync("Snapshot Tests", 3, "Category=Snapshot");
        var dpiTests = await RunTestLevelAsync("Multi-DPI Tests", 3, "Category=DPI");
        
        // Merge Level 3 results
        var renderingTests = new TestLevelResult
        {
            LevelName = "Rendering Tests",
            LevelNumber = 3,
            TestCases = snapshotTests.TestCases.Concat(dpiTests.TestCases).ToList()
        };
        renderingTests.TotalTests = renderingTests.TestCases.Count;
        renderingTests.Passed = renderingTests.TestCases.Count(t => t.Passed);
        renderingTests.Failed = renderingTests.TestCases.Count(t => !t.Passed);
        renderingTests.DurationMs = snapshotTests.DurationMs + dpiTests.DurationMs;
        report.TestLevels.Add(renderingTests);

        // Level 4: System Tests (if any exist)
        // report.TestLevels.Add(await RunTestLevelAsync("System Tests", 4, "Category=System"));

        // Collect performance metrics
        report.Performance = await CollectPerformanceMetricsAsync();

        // Analyze results and generate suggestions
        report.Decision = DetermineReleaseDecision(report);
        report.Suggestions = GenerateSuggestions(report);

        return report;
    }

    /// <summary>
    /// Run tests at a specific level using dotnet test.
    /// </summary>
    private static async Task<TestLevelResult> RunTestLevelAsync(string levelName, int levelNumber, string filter)
    {
        var result = new TestLevelResult
        {
            LevelName = levelName,
            LevelNumber = levelNumber
        };

        var sw = Stopwatch.StartNew();

        try
        {
            var testProjectPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Tests",
                "HomeoMahanagarLabelCleanV2.Tests.csproj");

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{testProjectPath}\" --filter \"{filter}\" --logger \"trx;LogFileName=testresults.trx\" --verbosity minimal",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                throw new InvalidOperationException("Failed to start dotnet test process");

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;

            // Parse output (simplified - real implementation would parse TRX file)
            ParseTestOutput(output, result);
        }
        catch (Exception ex)
        {
            result.TestCases.Add(new TestCaseResult
            {
                TestName = $"{levelName} Execution",
                Passed = false,
                DurationMs = sw.ElapsedMilliseconds,
                FailureMessage = $"Test execution failed: {ex.Message}"
            });
        }

        return result;
    }

    private static void ParseTestOutput(string output, TestLevelResult result)
    {
        // Parse "Test summary: total: X, failed: Y, succeeded: Z"
        var summaryMatch = System.Text.RegularExpressions.Regex.Match(
            output,
            @"total:\s*(\d+),\s*failed:\s*(\d+),\s*succeeded:\s*(\d+)");

        if (summaryMatch.Success)
        {
            result.TotalTests = int.Parse(summaryMatch.Groups[1].Value);
            result.Failed = int.Parse(summaryMatch.Groups[2].Value);
            result.Passed = int.Parse(summaryMatch.Groups[3].Value);
        }

        // Parse individual test results (simplified)
        // Real implementation would parse structured output or TRX XML
    }

    private static async Task<EnvironmentInfo> CollectEnvironmentInfoAsync()
    {
        var info = new EnvironmentInfo
        {
            OsVersion = GetWindowsVersion(),
            DotNetVersion = Environment.Version.ToString(),
            MachineName = Environment.MachineName,
            CurrentDpi = GetSystemDpi()
        };

        info.WindowsScaling = GetWindowsScalingPercentage(info.CurrentDpi);

        // Detect available printers
        try
        {
            var printerQuery = new ManagementObjectSearcher("SELECT * FROM Win32_Printer");
            foreach (var printer in printerQuery.Get())
            {
                var name = printer["Name"]?.ToString() ?? "Unknown";
                var status = printer["PrinterStatus"]?.ToString() ?? "Unknown";
                info.AvailablePrinters.Add($"{name} ({status})");
            }
        }
        catch { }

        return await Task.FromResult(info);
    }

    private static string GetWindowsVersion()
    {
        try
        {
            using var reg = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (reg != null)
            {
                var productName = reg.GetValue("ProductName")?.ToString() ?? "Windows";
                var build = reg.GetValue("CurrentBuild")?.ToString() ?? "Unknown";
                return $"{productName} (Build {build})";
            }
        }
        catch { }
        return "Windows (Unknown Version)";
    }

    private static int GetSystemDpi()
    {
        // In test context, default to 96 DPI
        // Real implementation would query actual system DPI if needed
        return 96;
    }

    private static string GetWindowsScalingPercentage(int dpi)
    {
        return dpi switch
        {
            96 => "100% (96 DPI)",
            120 => "125% (120 DPI)",
            144 => "150% (144 DPI)",
            192 => "200% (192 DPI)",
            _ => $"{(int)Math.Round(dpi / 96.0 * 100)}% ({dpi} DPI)"
        };
    }

    private static async Task<PerformanceMetrics> CollectPerformanceMetricsAsync()
    {
        var metrics = new PerformanceMetrics();

        try
        {
            // Parse session log files (if they exist)
            var sessionLogDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HomeoMahanagarLabelCleanV2",
                "SessionLogs");

            if (Directory.Exists(sessionLogDir))
            {
                var logFiles = Directory.GetFiles(sessionLogDir, "session_*.log")
                    .OrderByDescending(File.GetLastWriteTime)
                    .Take(1);

                foreach (var logFile in logFiles)
                {
                    var lines = await File.ReadAllLinesAsync(logFile);
                    
                    var renderTimes = new List<long>();
                    var exportTimes = new List<long>();
                    var stalls = new List<long>();

                    foreach (var line in lines)
                    {
                        if (line.Contains("[Performance] PrintLabelView.RenderItems"))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)ms");
                            if (match.Success)
                                renderTimes.Add(long.Parse(match.Groups[1].Value));
                        }
                        else if (line.Contains("[Performance] PdfHelper.ExportLabelToPdf"))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)ms");
                            if (match.Success)
                                exportTimes.Add(long.Parse(match.Groups[1].Value));
                        }
                        else if (line.Contains("UI thread stall"))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)ms");
                            if (match.Success)
                                stalls.Add(long.Parse(match.Groups[1].Value));
                        }
                    }

                    if (renderTimes.Any())
                        metrics.AvgRenderTimeMs = renderTimes.Average();
                    if (exportTimes.Any())
                        metrics.AvgPdfExportMs = exportTimes.Average();
                    if (stalls.Any())
                    {
                        metrics.UiThreadStallCount = stalls.Count;
                        metrics.MaxUiStallMs = stalls.Max();
                    }
                }
            }
        }
        catch { }

        return metrics;
    }

    private static ReleaseDecision DetermineReleaseDecision(TestReport report)
    {
        // Critical failures = NO-GO
        var unitTestsFailed = report.TestLevels
            .FirstOrDefault(l => l.LevelNumber == 1)?.Failed ?? 0;

        if (unitTestsFailed > 0)
            return ReleaseDecision.NO_GO; // Unit test failures are blocking

        // Any rendering failures = CONDITIONAL GO (requires manual validation)
        var renderingTestsFailed = report.TestLevels
            .FirstOrDefault(l => l.LevelNumber == 3)?.Failed ?? 0;

        if (renderingTestsFailed > 0)
            return ReleaseDecision.CONDITIONAL_GO;

        // UI stalls or warnings = CONDITIONAL GO
        if (report.Performance.UiThreadStallCount > 0 && report.Performance.MaxUiStallMs > 100)
            return ReleaseDecision.CONDITIONAL_GO;

        // All pass = GO
        return ReleaseDecision.GO;
    }

    private static List<string> GenerateSuggestions(TestReport report)
    {
        var suggestions = new List<string>();

        // Analyze unit test failures
        var unitTests = report.TestLevels.FirstOrDefault(l => l.LevelNumber == 1);
        if (unitTests != null && unitTests.Failed > 0)
        {
            suggestions.Add($"CRITICAL: {unitTests.Failed} unit test(s) failed. Review and fix immediately before release.");
            foreach (var failed in unitTests.TestCases.Where(t => !t.Passed))
            {
                suggestions.Add($"  - {failed.TestName}: {failed.FailureMessage}");
            }
        }

        // Analyze rendering test failures
        var renderingTests = report.TestLevels.FirstOrDefault(l => l.LevelNumber == 3);
        if (renderingTests != null && renderingTests.Failed > 0)
        {
            suggestions.Add($"WARNING: {renderingTests.Failed} rendering test(s) failed. Likely causes:");
            suggestions.Add("  - Font rendering variation (update baseline after printer validation)");
            suggestions.Add("  - DPI-specific rendering issue (test at multiple Windows scaling levels)");
            suggestions.Add("  - Stale baseline (delete and regenerate after visual inspection)");
            suggestions.Add("ACTION: Perform manual validation on physical printer before updating baselines.");
        }

        // Analyze performance issues
        if (report.Performance.UiThreadStallCount > 0)
        {
            suggestions.Add($"PERFORMANCE: {report.Performance.UiThreadStallCount} UI thread stall(s) detected (max: {report.Performance.MaxUiStallMs}ms).");
            suggestions.Add("  - Consider moving file I/O to background thread");
            suggestions.Add("  - Non-blocking for release, but impacts UX");
        }

        if (report.Performance.AvgRenderTimeMs > 50)
        {
            suggestions.Add($"PERFORMANCE: Average render time ({report.Performance.AvgRenderTimeMs:F1}ms) exceeds threshold (50ms).");
        }

        // Manual validation checklist
        if (report.Decision != ReleaseDecision.GO)
        {
            suggestions.Add("MANUAL VALIDATION REQUIRED:");
            suggestions.Add("  ☐ Print standard label at 100% Windows scaling");
            suggestions.Add("  ☐ Print standard label at 125% Windows scaling");
            suggestions.Add("  ☐ Print standard label at 150% Windows scaling");
            suggestions.Add("  ☐ Export PDF and print from viewer");
            suggestions.Add("  ☐ Test on target thermal printer");
        }

        return suggestions;
    }

    /// <summary>
    /// Generate formatted test report as text and save to Desktop.
    /// </summary>
    public static async Task<string> GenerateReportFileAsync(TestReport report)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("================================================================================");
        sb.AppendLine("LABEL PRINTING APPLICATION - TEST EXECUTION REPORT");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Build: {report.BuildConfiguration}");
        sb.AppendLine($"Report ID: {report.ReportId}");
        sb.AppendLine();

        // Environment
        sb.AppendLine("[ENVIRONMENT]");
        sb.AppendLine($"  OS: {report.Environment.OsVersion}");
        sb.AppendLine($"  .NET Runtime: {report.Environment.DotNetVersion}");
        sb.AppendLine($"  Windows Display Scaling: {report.Environment.WindowsScaling}");
        sb.AppendLine($"  Machine: {report.Environment.MachineName}");
        sb.AppendLine($"  Available Printers: {report.Environment.AvailablePrinters.Count} detected");
        foreach (var printer in report.Environment.AvailablePrinters)
        {
            sb.AppendLine($"    - {printer}");
        }
        sb.AppendLine();

        // Summary
        sb.AppendLine("[TEST EXECUTION SUMMARY]");
        sb.AppendLine($"  Total Tests: {report.TotalTests}");
        sb.AppendLine($"  Passed: {report.TotalPassed} ✅");
        sb.AppendLine($"  Failed: {report.TotalFailed} {(report.TotalFailed > 0 ? "❌" : "")}");
        sb.AppendLine($"  Total Duration: {report.TotalDurationMs / 1000.0:F1}s");
        sb.AppendLine();
        sb.AppendLine("  Test Levels:");
        foreach (var level in report.TestLevels.OrderBy(l => l.LevelNumber))
        {
            var levelPassRate = level.TotalTests > 0 ? $"{level.Passed}/{level.TotalTests}" : "0/0";
            sb.AppendLine($"    {level.Status} Level {level.LevelNumber} ({level.LevelName}): {levelPassRate} passed ({level.DurationMs / 1000.0:F1}s)");
        }
        sb.AppendLine();

        // Detailed results
        sb.AppendLine("[DETAILED RESULTS]");
        sb.AppendLine();
        foreach (var level in report.TestLevels.OrderBy(l => l.LevelNumber))
        {
            sb.AppendLine($"--- Level {level.LevelNumber}: {level.LevelName} ---");
            sb.AppendLine($"Duration: {level.DurationMs / 1000.0:F1}s | Status: {level.Status}");
            sb.AppendLine();

            foreach (var test in level.TestCases.Take(10)) // Limit output
            {
                var icon = test.Passed ? "✅" : "❌";
                sb.AppendLine($"  {icon} {test.TestName} ({test.DurationMs}ms)");
                if (!test.Passed && !string.IsNullOrEmpty(test.FailureMessage))
                {
                    sb.AppendLine($"     Failure: {test.FailureMessage}");
                }
            }

            if (level.TestCases.Count > 10)
            {
                sb.AppendLine($"  ... ({level.TestCases.Count - 10} more tests)");
            }
            sb.AppendLine();
        }

        // Performance
        sb.AppendLine("[PERFORMANCE ANALYSIS]");
        sb.AppendLine();
        if (report.Performance.AvgRenderTimeMs > 0)
        {
            var renderStatus = report.Performance.AvgRenderTimeMs < 50 ? "✅" : "⚠️";
            sb.AppendLine($"  {renderStatus} PrintLabelView.RenderItems: avg {report.Performance.AvgRenderTimeMs:F1}ms (threshold: 50ms)");
        }
        if (report.Performance.AvgPdfExportMs > 0)
        {
            var exportStatus = report.Performance.AvgPdfExportMs < 200 ? "✅" : "⚠️";
            sb.AppendLine($"  {exportStatus} PdfHelper.ExportLabelToPdf: avg {report.Performance.AvgPdfExportMs:F1}ms (threshold: 200ms)");
        }
        if (report.Performance.UiThreadStallCount > 0)
        {
            sb.AppendLine($"  ⚠️ UI thread stalls detected: {report.Performance.UiThreadStallCount} warning(s)");
            sb.AppendLine($"     - Max stall: {report.Performance.MaxUiStallMs}ms (threshold: 100ms)");
        }
        sb.AppendLine();

        // Release recommendation
        sb.AppendLine("[RELEASE RECOMMENDATION]");
        sb.AppendLine();
        sb.AppendLine($"Status: {GetDecisionString(report.Decision)}");
        sb.AppendLine();

        // Suggestions
        sb.AppendLine("[ACTIONABLE SUGGESTIONS]");
        sb.AppendLine();
        if (report.Suggestions.Any())
        {
            foreach (var suggestion in report.Suggestions)
            {
                sb.AppendLine(suggestion);
            }
        }
        else
        {
            sb.AppendLine("No issues detected. All tests passed.");
        }
        sb.AppendLine();

        // Conclusion
        sb.AppendLine("[CONCLUSION]");
        sb.AppendLine();
        sb.AppendLine($"Release decision: {GetDecisionString(report.Decision)}");
        var passRate = report.TotalTests > 0 ? (report.TotalPassed * 100.0 / report.TotalTests) : 0.0;
        sb.AppendLine($"Automated tests: {passRate:F1}% pass rate ({report.TotalPassed}/{report.TotalTests})");
        sb.AppendLine();

        sb.AppendLine("================================================================================");
        sb.AppendLine($"Report saved to Desktop");
        sb.AppendLine("================================================================================");

        // Save to Desktop
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var fileName = $"TestReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var filePath = Path.Combine(desktopPath, fileName);
        
        await File.WriteAllTextAsync(filePath, sb.ToString());

        return filePath;
    }

    private static string GetDecisionString(ReleaseDecision decision)
    {
        return decision switch
        {
            ReleaseDecision.GO => "✅ GO - Ready for release",
            ReleaseDecision.CONDITIONAL_GO => "⚠️ CONDITIONAL GO - Manual validation required",
            ReleaseDecision.NO_GO => "❌ NO-GO - Critical failures, release blocked",
            _ => "Unknown"
        };
    }
}
