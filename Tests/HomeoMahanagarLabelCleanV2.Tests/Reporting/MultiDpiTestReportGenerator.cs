using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeoMahanagarLabelCleanV2.Tests.Reporting;

/// <summary>
/// Multi-DPI test report generator for Desktop log file output.
/// 
/// Purpose:
/// - Aggregate test results from unit, snapshot, and multi-DPI tests
/// - Generate structured log file on user's Desktop
/// - Provide clear release GO/NO-GO recommendation
/// - Track manual validation status as release gate
/// 
/// Output:
/// - HomeoLabel_TestReport_yyyy-MM-dd_HH-mm.log on Desktop
/// </summary>
public class MultiDpiTestReportGenerator
{
    public enum ReleaseDecision
    {
        GO,              // All tests pass, manual validation complete
        CONDITIONAL_GO,  // Tests pass, manual validation pending
        NO_GO            // Critical failures, release blocked
    }

    public sealed class TestResult
    {
        public string Category { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public long DurationMs { get; set; }
        public List<string> Failures { get; set; } = new();
        
        public bool IsPass => Failed == 0 && Total > 0;
        public double PassRate => Total > 0 ? (Passed * 100.0 / Total) : 0;
    }

    public sealed class DpiTestResult
    {
        public int Dpi { get; set; }
        public string ScalingPercent { get; set; } = string.Empty;
        public int ExpectedWidth { get; set; }
        public int ExpectedHeight { get; set; }
        public int ActualWidth { get; set; }
        public int ActualHeight { get; set; }
        public double BaselineMatchPercent { get; set; }
        public bool Passed { get; set; }
    }

    public sealed class ManualValidationResult
    {
        public string ScalingLevel { get; set; } = string.Empty;
        public int Dpi { get; set; }
        public string Status { get; set; } = "NOT VERIFIED"; // PASS / FAIL / NOT VERIFIED
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class TestReportData
    {
        public DateTime GeneratedAt { get; set; }
        public string ReportId { get; set; } = string.Empty;
        
        // Environment
        public string OsVersion { get; set; } = string.Empty;
        public int CurrentDpi { get; set; }
        public string WindowsScaling { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public string DotNetVersion { get; set; } = string.Empty;
        public long TotalExecutionTimeMs { get; set; }
        
        // Test Results
        public TestResult UnitTests { get; set; } = new();
        public TestResult SnapshotTests { get; set; } = new();
        public TestResult MultiDpiTests { get; set; } = new();
        
        // DPI Analysis
        public List<DpiTestResult> DpiResults { get; set; } = new();
        public bool PhysicalSizeInvariant { get; set; }
        public double ExpectedPhysicalWidthInches { get; set; }
        public double ExpectedPhysicalHeightInches { get; set; }
        
        // Performance
        public double AvgRenderTimeMs { get; set; }
        public double AvgPdfExportTimeMs { get; set; }
        public double PerformanceChangePercent { get; set; }
        
        // Manual Validation
        public bool ManualValidationFileFound { get; set; }
        public List<ManualValidationResult> ManualValidation { get; set; } = new();
        public string PrinterTested { get; set; } = "NOT VERIFIED";
        public string ValidationDate { get; set; } = "NOT PERFORMED";
        
        // Decision
        public ReleaseDecision Decision { get; set; }
        public List<string> Suggestions { get; set; } = new();
        public List<string> RequiredActions { get; set; } = new();
    }

    /// <summary>
    /// Generate and save test report to Desktop.
    /// </summary>
    public static async Task<string> GenerateReportAsync(TestReportData data)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("================================================================================");
        sb.AppendLine("HOMEOPATHIC LABEL PRINTING - MULTI-DPI TEST REPORT");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Report Generated: {data.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Report ID: {data.ReportId}");
        sb.AppendLine();

        // Environment
        AppendEnvironmentSection(sb, data);
        
        // Automated Test Results
        AppendAutomatedTestResults(sb, data);
        
        // DPI Analysis
        AppendDpiAnalysis(sb, data);
        
        // Performance
        AppendPerformance(sb, data);
        
        // Manual Validation
        AppendManualValidation(sb, data);
        
        // Suggestions
        AppendSuggestions(sb, data);
        
        // Final Decision
        AppendFinalDecision(sb, data);

        sb.AppendLine("================================================================================");
        sb.AppendLine("END OF REPORT");
        sb.AppendLine("================================================================================");

        // Save to Desktop
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var fileName = $"HomeoLabel_TestReport_{data.GeneratedAt:yyyy-MM-dd_HH-mm}.log";
        var filePath = Path.Combine(desktopPath, fileName);

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);

        sb.AppendLine($"Log file saved to: {filePath}");
        
        return filePath;
    }

    private static void AppendEnvironmentSection(StringBuilder sb, TestReportData data)
    {
        sb.AppendLine("[ENVIRONMENT]");
        sb.AppendLine($"  Operating System: {data.OsVersion}");
        sb.AppendLine($"  Windows Display Scaling: {data.WindowsScaling}");
        sb.AppendLine($"  Machine Name: {data.MachineName}");
        sb.AppendLine($"  .NET Runtime: {data.DotNetVersion}");
        sb.AppendLine($"  Test Execution Time: {data.TotalExecutionTimeMs / 1000.0:F1}s");
        sb.AppendLine();
        sb.AppendLine("  DPI Levels Tested:");
        sb.AppendLine("    - 96 DPI (100% scaling)");
        sb.AppendLine("    - 120 DPI (125% scaling)");
        sb.AppendLine("    - 144 DPI (150% scaling)");
        sb.AppendLine();
    }

    private static void AppendAutomatedTestResults(StringBuilder sb, TestReportData data)
    {
        sb.AppendLine("[AUTOMATED TEST RESULTS]");
        sb.AppendLine();
        
        // Unit Tests
        AppendTestCategory(sb, "Unit Tests (Category=Unit)", data.UnitTests);
        
        // Snapshot Tests
        AppendTestCategory(sb, "Snapshot Tests (Category=Snapshot)", data.SnapshotTests);
        
        // Multi-DPI Tests
        sb.AppendLine("  Multi-DPI Tests (Category=DPI):");
        sb.AppendLine($"    Status: {(data.MultiDpiTests.IsPass ? "✅ PASS" : "❌ FAIL")}");
        sb.AppendLine($"    Total: {data.MultiDpiTests.Total} | Passed: {data.MultiDpiTests.Passed} | Failed: {data.MultiDpiTests.Failed}");
        sb.AppendLine($"    Duration: {data.MultiDpiTests.DurationMs / 1000.0:F1}s");
        sb.AppendLine();
        sb.AppendLine("    Per-DPI Results:");
        
        var dpi96 = data.DpiResults.FirstOrDefault(d => d.Dpi == 96);
        var dpi120 = data.DpiResults.FirstOrDefault(d => d.Dpi == 120);
        var dpi144 = data.DpiResults.FirstOrDefault(d => d.Dpi == 144);
        
        if (dpi96 != null)
            sb.AppendLine($"      96 DPI (100%): {(dpi96.Passed ? "✅ PASS" : "❌ FAIL")}");
        if (dpi120 != null)
            sb.AppendLine($"      120 DPI (125%): {(dpi120.Passed ? "✅ PASS" : "❌ FAIL")}");
        if (dpi144 != null)
            sb.AppendLine($"      144 DPI (150%): {(dpi144.Passed ? "✅ PASS" : "❌ FAIL")}");
        
        sb.AppendLine();
    }

    private static void AppendTestCategory(StringBuilder sb, string name, TestResult result)
    {
        sb.AppendLine($"  {name}:");
        sb.AppendLine($"    Status: {(result.IsPass ? "✅ PASS" : "❌ FAIL")}");
        sb.AppendLine($"    Total: {result.Total} | Passed: {result.Passed} | Failed: {result.Failed}");
        sb.AppendLine($"    Duration: {result.DurationMs / 1000.0:F1}s");
        
        if (result.Failures.Any())
        {
            sb.AppendLine("    Failures:");
            foreach (var failure in result.Failures)
            {
                sb.AppendLine($"      - {failure}");
            }
        }
        sb.AppendLine();
    }

    private static void AppendDpiAnalysis(StringBuilder sb, TestReportData data)
    {
        sb.AppendLine("[DPI ANALYSIS]");
        sb.AppendLine();
        
        // Physical Size Invariance
        sb.AppendLine("  Physical Size Invariance:");
        sb.AppendLine($"    Status: {(data.PhysicalSizeInvariant ? "✅ PASS" : "❌ FAIL")}");
        sb.AppendLine($"    Expected: {data.ExpectedPhysicalWidthInches:F2}\" × {data.ExpectedPhysicalHeightInches:F2}\" (50mm × 30mm)");
        sb.AppendLine($"    Validated across all DPI levels: {(data.PhysicalSizeInvariant ? "YES" : "NO")}");
        sb.AppendLine();
        
        // Pixel Dimension Validation
        sb.AppendLine("  Pixel Dimension Validation:");
        foreach (var dpi in data.DpiResults)
        {
            var status = dpi.Passed ? "✅ PASS" : "❌ FAIL";
            sb.AppendLine($"    {dpi.Dpi} DPI: Expected {dpi.ExpectedWidth}×{dpi.ExpectedHeight}px | Actual: {dpi.ActualWidth}×{dpi.ActualHeight}px | {status}");
        }
        sb.AppendLine();
        
        // Snapshot Baseline Match
        sb.AppendLine("  Snapshot Baseline Match:");
        foreach (var dpi in data.DpiResults)
        {
            var status = dpi.BaselineMatchPercent >= 99.9 ? "✅ PASS" : "⚠️ MISMATCH";
            sb.AppendLine($"    {dpi.Dpi} DPI: {dpi.BaselineMatchPercent:F2}% match | {status}");
        }
        sb.AppendLine();
    }

    private static void AppendPerformance(StringBuilder sb, TestReportData data)
    {
        sb.AppendLine("[PERFORMANCE]");
        sb.AppendLine();
        
        if (data.AvgRenderTimeMs > 0)
        {
            var renderStatus = data.AvgRenderTimeMs < 50 ? "✅ PASS" : "⚠️ WARNING";
            sb.AppendLine("  Rendering Performance:");
            sb.AppendLine($"    Average Render Time: {data.AvgRenderTimeMs:F1}ms (threshold: 50ms)");
            sb.AppendLine($"    Status: {renderStatus}");
            sb.AppendLine();
        }
        
        if (data.AvgPdfExportTimeMs > 0)
        {
            var exportStatus = data.AvgPdfExportTimeMs < 200 ? "✅ PASS" : "⚠️ WARNING";
            sb.AppendLine("  PDF Export Performance:");
            sb.AppendLine($"    Average Export Time: {data.AvgPdfExportTimeMs:F1}ms (threshold: 200ms)");
            sb.AppendLine($"    Status: {exportStatus}");
            sb.AppendLine();
        }
        
        if (data.PerformanceChangePercent != 0)
        {
            var regressionStatus = Math.Abs(data.PerformanceChangePercent) < 20 ? "✅ ACCEPTABLE" : "⚠️ WARNING";
            sb.AppendLine("  Regression Analysis:");
            sb.AppendLine($"    Performance Change: {data.PerformanceChangePercent:+0.0;-0.0}%");
            sb.AppendLine($"    Status: {regressionStatus}");
            sb.AppendLine();
        }
    }

    private static void AppendManualValidation(StringBuilder sb, TestReportData data)
    {
        sb.AppendLine("[MANUAL VALIDATION STATUS]");
        sb.AppendLine();
        sb.AppendLine("  Manual validation is REQUIRED for final release approval.");
        sb.AppendLine("  Physical printer output must match preview at all scaling levels.");
        sb.AppendLine();
        
        sb.AppendLine("  Validation Evidence:");
        sb.AppendLine($"    Manual_DPI_Test_Results.md: {(data.ManualValidationFileFound ? "✅ FOUND" : "❌ NOT FOUND")}");
        sb.AppendLine();
        
        sb.AppendLine("  Scaling Level Results:");
        foreach (var validation in data.ManualValidation)
        {
            var icon = validation.Status == "PASS" ? "✅" : 
                      validation.Status == "FAIL" ? "❌" : "⚠️";
            sb.AppendLine($"    {validation.ScalingLevel}: {icon} {validation.Status}");
            if (!string.IsNullOrEmpty(validation.Notes))
            {
                sb.AppendLine($"       Notes: {validation.Notes}");
            }
        }
        sb.AppendLine();
        
        sb.AppendLine($"  Printer Tested: {data.PrinterTested}");
        sb.AppendLine($"  Validation Date: {data.ValidationDate}");
        sb.AppendLine();
    }

    private static void AppendSuggestions(StringBuilder sb, TestReportData data)
    {
        sb.AppendLine("[SUGGESTIONS]");
        sb.AppendLine();
        
        if (data.Suggestions.Any())
        {
            for (int i = 0; i < data.Suggestions.Count; i++)
            {
                sb.AppendLine($"  {i + 1}. {data.Suggestions[i]}");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("  ✅ No issues detected. All automated tests passed.");
            sb.AppendLine();
        }
    }

    private static void AppendFinalDecision(StringBuilder sb, TestReportData data)
    {
        sb.AppendLine("[FINAL DECISION]");
        sb.AppendLine();
        
        var decisionText = data.Decision switch
        {
            ReleaseDecision.GO => "✅ GO",
            ReleaseDecision.CONDITIONAL_GO => "⚠️ CONDITIONAL GO",
            ReleaseDecision.NO_GO => "❌ NO-GO",
            _ => "UNKNOWN"
        };
        
        sb.AppendLine($"  Release Recommendation: {decisionText}");
        sb.AppendLine();
        
        sb.AppendLine("  Justification:");
        sb.AppendLine($"    {GetJustification(data)}");
        sb.AppendLine();
        
        if (data.RequiredActions.Any())
        {
            sb.AppendLine("  Required Actions Before Release:");
            foreach (var action in data.RequiredActions)
            {
                sb.AppendLine($"    ☐ {action}");
            }
            sb.AppendLine();
        }
        
        // Approval Status
        var unitPassRate = data.UnitTests.PassRate;
        var snapshotPassRate = data.SnapshotTests.PassRate;
        var dpiPassRate = data.MultiDpiTests.PassRate;
        var overallPassRate = (unitPassRate + snapshotPassRate + dpiPassRate) / 3.0;
        
        var manualStatus = data.ManualValidation.All(v => v.Status == "PASS") ? "✅ COMPLETE" :
                          data.ManualValidation.Any(v => v.Status == "FAIL") ? "❌ FAILED" :
                          "⚠️ PENDING";
        
        var perfStatus = Math.Abs(data.PerformanceChangePercent) < 20 ? "✅ ACCEPTABLE" : "⚠️ DEGRADED";
        
        sb.AppendLine("  Approval Status:");
        sb.AppendLine($"    Automated Tests: {overallPassRate:F1}% pass rate");
        sb.AppendLine($"    Manual Validation: {manualStatus}");
        sb.AppendLine($"    Performance: {perfStatus}");
        sb.AppendLine();
        
        var overallStatus = data.Decision == ReleaseDecision.GO ? "✅ APPROVED" :
                           data.Decision == ReleaseDecision.CONDITIONAL_GO ? "⚠️ CONDITIONAL" :
                           "❌ REJECTED";
        sb.AppendLine($"    Overall: {overallStatus}");
        sb.AppendLine();
    }

    private static string GetJustification(TestReportData data)
    {
        if (data.Decision == ReleaseDecision.GO)
        {
            return "All automated tests passed and manual validation complete. Ready for release.";
        }
        else if (data.Decision == ReleaseDecision.CONDITIONAL_GO)
        {
            if (!data.UnitTests.IsPass)
                return "Unit test failures detected. Manual validation may proceed but code fixes required.";
            if (!data.SnapshotTests.IsPass || !data.MultiDpiTests.IsPass)
                return "Rendering test failures detected. Manual validation on physical printer required before release.";
            if (data.ManualValidation.All(v => v.Status == "NOT VERIFIED"))
                return "Automated tests passed but manual validation not yet performed. Complete manual testing before release.";
            return "Some tests passed with warnings. Manual validation required to confirm release readiness.";
        }
        else // NO-GO
        {
            if (!data.UnitTests.IsPass)
                return "CRITICAL: Unit test failures detected. Release blocked until all unit tests pass.";
            if (data.ManualValidation.Any(v => v.Status == "FAIL"))
                return "CRITICAL: Manual validation failed. Printed output does not match preview. Release blocked.";
            return "Critical failures detected. Release blocked pending investigation and fixes.";
        }
    }

    /// <summary>
    /// Determine release decision based on test results and manual validation.
    /// </summary>
    public static ReleaseDecision DetermineDecision(TestReportData data)
    {
        // CRITICAL: Unit test failures = NO-GO
        if (!data.UnitTests.IsPass)
            return ReleaseDecision.NO_GO;

        // CRITICAL: Manual validation failures = NO-GO
        if (data.ManualValidation.Any(v => v.Status == "FAIL"))
            return ReleaseDecision.NO_GO;

        // If manual validation complete and all tests pass = GO
        if (data.UnitTests.IsPass && 
            data.SnapshotTests.IsPass && 
            data.MultiDpiTests.IsPass &&
            data.ManualValidation.All(v => v.Status == "PASS"))
        {
            return ReleaseDecision.GO;
        }

        // Otherwise = CONDITIONAL GO (manual validation pending or minor failures)
        return ReleaseDecision.CONDITIONAL_GO;
    }

    /// <summary>
    /// Generate suggestions based on test failures.
    /// </summary>
    public static List<string> GenerateSuggestions(TestReportData data)
    {
        var suggestions = new List<string>();

        // Unit test failures
        if (!data.UnitTests.IsPass)
        {
            suggestions.Add("CRITICAL: Unit Test Failures\n" +
                "     Cause: Core logic or conversion formulas broken\n" +
                "     Action: Review and fix failed tests immediately\n" +
                "     Priority: BLOCKING (must fix before release)");
        }

        // Snapshot test failures
        if (!data.SnapshotTests.IsPass)
        {
            suggestions.Add("Snapshot Test Failures\n" +
                "     Cause: Font rendering variation or layout changes\n" +
                "     Action:\n" +
                "       - Review baseline mismatches in test output\n" +
                "       - Delete stale baselines if intentional change\n" +
                "       - Re-run tests to create new baselines\n" +
                "       - Print new baselines on physical printer\n" +
                "       - Commit only if printed output is correct\n" +
                "     Priority: HIGH (requires validation)");
        }

        // DPI-specific failures
        foreach (var dpi in data.DpiResults.Where(d => !d.Passed))
        {
            suggestions.Add($"DPI Test Failure at {dpi.Dpi} DPI ({dpi.ScalingPercent})\n" +
                $"     Cause: Pixel dimension mismatch or baseline variance\n" +
                $"     Expected: {dpi.ExpectedWidth}×{dpi.ExpectedHeight}px\n" +
                $"     Actual: {dpi.ActualWidth}×{dpi.ActualHeight}px\n" +
                $"     Action:\n" +
                $"       - Set Windows scaling to {dpi.ScalingPercent}\n" +
                $"       - Run app and verify label rendering\n" +
                $"       - Print on physical printer\n" +
                $"       - If output correct, update baseline\n" +
                $"     Priority: HIGH (DPI-specific issue)");
        }

        // Manual validation missing
        if (data.ManualValidation.All(v => v.Status == "NOT VERIFIED"))
        {
            suggestions.Add("Manual Validation Not Performed\n" +
                "     Cause: No manual validation evidence found\n" +
                "     Action:\n" +
                "       - Follow manual validation matrix in MULTI_DPI_TESTING_GUIDE.md\n" +
                "       - Test at 100%, 125%, 150% Windows scaling\n" +
                "       - Print test patches on physical printer\n" +
                "       - Document results in Manual_DPI_Test_Results.md\n" +
                "     Priority: CRITICAL (release gate)");
        }

        // Performance regression
        if (Math.Abs(data.PerformanceChangePercent) > 20)
        {
            suggestions.Add($"Performance Regression Detected ({data.PerformanceChangePercent:+0.0;-0.0}%)\n" +
                "     Cause: Rendering or export duration increased >20%\n" +
                "     Action:\n" +
                "       - Profile rendering pipeline\n" +
                "       - Check for new allocations or redundant operations\n" +
                "       - Review recent code changes\n" +
                "     Priority: MEDIUM (monitor)");
        }

        return suggestions;
    }
}
