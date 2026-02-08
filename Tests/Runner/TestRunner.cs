using System;
using System.Threading.Tasks;
using HomeoMahanagarLabelCleanV2.Tests.Orchestration;

namespace HomeoMahanagarLabelCleanV2.Tests.Runner;

/// <summary>
/// Console application to execute full test suite and generate professional report.
/// 
/// Usage:
///   dotnet run --project Tests/TestRunner/TestRunner.csproj
/// 
/// Output:
///   - Test execution results printed to console
///   - Comprehensive report saved to Desktop
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("LABEL PRINTING APPLICATION - TEST SUITE EXECUTION");
        Console.WriteLine("================================================================================");
        Console.WriteLine();

        Console.WriteLine("Executing test suite...");
        Console.WriteLine();

        try
        {
            // Execute full test suite
            var report = await TestOrchestrator.ExecuteTestSuiteAsync();

            // Print summary to console
            Console.WriteLine($"Test Execution Complete!");
            Console.WriteLine($"  Total Tests: {report.TotalTests}");
            Console.WriteLine($"  Passed: {report.TotalPassed} ✅");
            Console.WriteLine($"  Failed: {report.TotalFailed} {(report.TotalFailed > 0 ? "❌" : "")}");
            Console.WriteLine($"  Duration: {report.TotalDurationMs / 1000.0:F1}s");
            Console.WriteLine();

            // Generate report file
            var reportPath = await TestOrchestrator.GenerateReportFileAsync(report);
            
            Console.WriteLine($"Detailed report saved to:");
            Console.WriteLine($"  {reportPath}");
            Console.WriteLine();

            // Print release recommendation
            Console.WriteLine("Release Recommendation:");
            Console.WriteLine($"  {GetDecisionString(report.Decision)}");
            Console.WriteLine();

            // Print top suggestions
            if (report.Suggestions.Any())
            {
                Console.WriteLine("Top Suggestions:");
                foreach (var suggestion in report.Suggestions.Take(5))
                {
                    Console.WriteLine($"  • {suggestion}");
                }
                Console.WriteLine();
            }

            Console.WriteLine("================================================================================");
            
            // Return exit code based on decision
            return report.Decision == TestOrchestrator.ReleaseDecision.NO_GO ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test execution failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static string GetDecisionString(TestOrchestrator.ReleaseDecision decision)
    {
        return decision switch
        {
            TestOrchestrator.ReleaseDecision.GO => "✅ GO - Ready for release",
            TestOrchestrator.ReleaseDecision.CONDITIONAL_GO => "⚠️ CONDITIONAL GO - Manual validation required",
            TestOrchestrator.ReleaseDecision.NO_GO => "❌ NO-GO - Critical failures, release blocked",
            _ => "Unknown"
        };
    }
}
