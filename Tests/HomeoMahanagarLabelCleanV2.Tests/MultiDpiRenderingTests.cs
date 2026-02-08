using System;
using System.IO;
using System.Windows;
using Xunit;
using HomeoMahanagarLabelCleanV2.Helpers;
using HomeoMahanagarLabelCleanV2.Models;
using HomeoMahanagarLabelCleanV2.Views;
using HomeoMahanagarLabelCleanV2.Tests.Helpers;

namespace HomeoMahanagarLabelCleanV2.Tests.DpiTests;

/// <summary>
/// MULTI-DPI RENDERING TESTS
/// 
/// Purpose:
/// - Validate that label rendering is consistent across different Windows display scaling settings
/// - Simulate 100%, 125%, and 150% scaling without changing system configuration
/// - Detect layout regressions when DPI changes
/// 
/// Approach:
/// - Render the same WPF visual at multiple DPI values (96, 120, 144)
/// - Each DPI produces a different pixel-sized bitmap representing the same physical size
/// - Compare against baselines or validate dimensions/positioning
/// 
/// What This Tests:
/// ✅ WPF layout math at different DPI scales
/// ✅ DIPs → Pixels conversion
/// ✅ Text sizing and positioning
/// ✅ Control alignment under scaling
/// 
/// What This Does NOT Test:
/// ❌ Actual Windows DWM rendering behavior
/// ❌ GPU-specific rendering
/// ❌ ClearType variations
/// ❌ Physical printer DPI handling
/// </summary>
[Trait("Category", "DPI")]
public class MultiDpiRenderingTests
{
    private const double LABEL_WIDTH_DIP = 189.0; // 50mm in DIPs
    private const double LABEL_HEIGHT_DIP = 113.0; // 30mm in DIPs
    private const double DIFFERENCE_THRESHOLD = 0.1; // Allow 0.1% pixel difference

    #region Basic DPI Rendering Tests

    [StaFact]
    public void PrintLabelView_RenderAt96Dpi_ProducesExpectedPixelDimensions()
    {
        // ARRANGE
        var view = CreateTestLabelView();
        
        // ACT
        var result = DpiRenderingHelper.RenderAtDpi(
            view,
            LABEL_WIDTH_DIP,
            LABEL_HEIGHT_DIP,
            DpiRenderingHelper.StandardDpi.Dpi100);
        
        // ASSERT
        // At 96 DPI (100% scaling), DIPs = Pixels
        Assert.Equal(96.0, result.Dpi);
        Assert.Equal(189, result.PixelWidth); // 189 DIPs × 1.0 = 189px
        Assert.Equal(113, result.PixelHeight); // 113 DIPs × 1.0 = 113px
        Assert.True(result.PngBytes.Length > 0);
    }

    [StaFact]
    public void PrintLabelView_RenderAt120Dpi_ProducesExpectedPixelDimensions()
    {
        // ARRANGE
        var view = CreateTestLabelView();
        
        // ACT
        var result = DpiRenderingHelper.RenderAtDpi(
            view,
            LABEL_WIDTH_DIP,
            LABEL_HEIGHT_DIP,
            DpiRenderingHelper.StandardDpi.Dpi125);
        
        // ASSERT
        // At 120 DPI (125% scaling), pixels = DIPs × 1.25
        Assert.Equal(120.0, result.Dpi);
        Assert.Equal(237, result.PixelWidth); // 189 DIPs × 1.25 = 236.25 → 237px
        Assert.Equal(142, result.PixelHeight); // 113 DIPs × 1.25 = 141.25 → 142px
        Assert.True(result.PngBytes.Length > 0);
    }

    [StaFact]
    public void PrintLabelView_RenderAt144Dpi_ProducesExpectedPixelDimensions()
    {
        // ARRANGE
        var view = CreateTestLabelView();
        
        // ACT
        var result = DpiRenderingHelper.RenderAtDpi(
            view,
            LABEL_WIDTH_DIP,
            LABEL_HEIGHT_DIP,
            DpiRenderingHelper.StandardDpi.Dpi150);
        
        // ASSERT
        // At 144 DPI (150% scaling), pixels = DIPs × 1.5
        Assert.Equal(144.0, result.Dpi);
        Assert.Equal(284, result.PixelWidth); // 189 DIPs × 1.5 = 283.5 → 284px
        Assert.Equal(170, result.PixelHeight); // 113 DIPs × 1.5 = 169.5 → 170px
        Assert.True(result.PngBytes.Length > 0);
    }

    #endregion

    #region Physical Size Invariance Tests

    [StaFact]
    public void PrintLabelView_PhysicalSize_InvariantAcrossDpi()
    {
        // ARRANGE
        var view = CreateTestLabelView();
        var dpiValues = new[] { 96.0, 120.0, 144.0, 192.0 };
        
        // ACT
        var results = DpiRenderingHelper.RenderAtMultipleDpi(
            view,
            LABEL_WIDTH_DIP,
            LABEL_HEIGHT_DIP,
            dpiValues);
        
        // ASSERT
        // Physical size should be identical across all DPI settings
        double expectedWidthInches = LABEL_WIDTH_DIP / 96.0;
        double expectedHeightInches = LABEL_HEIGHT_DIP / 96.0;
        
        foreach (var result in results)
        {
            Assert.Equal(expectedWidthInches, result.PhysicalWidthInches, precision: 5);
            Assert.Equal(expectedHeightInches, result.PhysicalHeightInches, precision: 5);
        }
    }

    #endregion

    #region Snapshot Baseline Tests

    [StaFact]
    public void PrintLabelView_StandardLabel_MatchesBaseline_At96Dpi()
    {
        // ARRANGE
        var view = CreateStandardLabelView();
        string baselinePath = Path.Combine("DpiTests", "Baselines", "StandardLabel_DPI96.png");
        
        // ACT
        var result = DpiRenderingHelper.RenderAtDpi(
            view,
            LABEL_WIDTH_DIP,
            LABEL_HEIGHT_DIP,
            DpiRenderingHelper.StandardDpi.Dpi100);
        
        // ASSERT
        if (!File.Exists(baselinePath))
        {
            // Create baseline on first run
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            File.WriteAllBytes(baselinePath, result.PngBytes);
            Assert.Fail($"Baseline created at {baselinePath}. Re-run test to validate.");
        }
        else
        {
            byte[] baseline = File.ReadAllBytes(baselinePath);
            double differencePercent = DpiRenderingHelper.ComparePngBytes(baseline, result.PngBytes, pixelTolerance: 5);
            Assert.True(differencePercent < DIFFERENCE_THRESHOLD,
                $"Rendering differs by {differencePercent:F2}% at 96 DPI (threshold: {DIFFERENCE_THRESHOLD}%)");
        }
    }

    [StaFact]
    public void PrintLabelView_StandardLabel_MatchesBaseline_At120Dpi()
    {
        // ARRANGE
        var view = CreateStandardLabelView();
        string baselinePath = Path.Combine("DpiTests", "Baselines", "StandardLabel_DPI120.png");
        
        // ACT
        var result = DpiRenderingHelper.RenderAtDpi(
            view,
            LABEL_WIDTH_DIP,
            LABEL_HEIGHT_DIP,
            DpiRenderingHelper.StandardDpi.Dpi125);
        
        // ASSERT
        if (!File.Exists(baselinePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            File.WriteAllBytes(baselinePath, result.PngBytes);
            Assert.Fail($"Baseline created at {baselinePath}. Re-run test to validate.");
        }
        else
        {
            byte[] baseline = File.ReadAllBytes(baselinePath);
            double differencePercent = DpiRenderingHelper.ComparePngBytes(baseline, result.PngBytes, pixelTolerance: 5);
            Assert.True(differencePercent < DIFFERENCE_THRESHOLD,
                $"Rendering differs by {differencePercent:F2}% at 120 DPI (threshold: {DIFFERENCE_THRESHOLD}%)");
        }
    }

    [StaFact]
    public void PrintLabelView_StandardLabel_MatchesBaseline_At144Dpi()
    {
        // ARRANGE
        var view = CreateStandardLabelView();
        string baselinePath = Path.Combine("DpiTests", "Baselines", "StandardLabel_DPI144.png");
        
        // ACT
        var result = DpiRenderingHelper.RenderAtDpi(
            view,
            LABEL_WIDTH_DIP,
            LABEL_HEIGHT_DIP,
            DpiRenderingHelper.StandardDpi.Dpi150);
        
        // ASSERT
        if (!File.Exists(baselinePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            File.WriteAllBytes(baselinePath, result.PngBytes);
            Assert.Fail($"Baseline created at {baselinePath}. Re-run test to validate.");
        }
        else
        {
            byte[] baseline = File.ReadAllBytes(baselinePath);
            double differencePercent = DpiRenderingHelper.ComparePngBytes(baseline, result.PngBytes, pixelTolerance: 5);
            Assert.True(differencePercent < DIFFERENCE_THRESHOLD,
                $"Rendering differs by {differencePercent:F2}% at 144 DPI (threshold: {DIFFERENCE_THRESHOLD}%)");
        }
    }

    #endregion

    #region Test Helpers

    private PrintLabelView CreateTestLabelView()
    {
        var view = new PrintLabelView();
        view.Width = LABEL_WIDTH_DIP;
        view.Height = LABEL_HEIGHT_DIP;

        var items = new[]
        {
            new LabelCanvasItem { Text = "TEST MEDICINE", X = 86, Y = 6, FontSize = 14, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "200 CH", X = 86, Y = 26, FontSize = 11, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "5 GLOB", X = 86, Y = 42, FontSize = 10, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "MORNING", X = 86, Y = 52, FontSize = 10, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "CLINIC", X = 86, Y = 70, FontSize = 10, Alignment = TextAlignment.Center }
        };

        view.RenderItems(items);
        return view;
    }

    private PrintLabelView CreateStandardLabelView()
    {
        var view = new PrintLabelView();
        view.Width = LABEL_WIDTH_DIP;
        view.Height = LABEL_HEIGHT_DIP;

        var items = new[]
        {
            new LabelCanvasItem { Text = "ARNICA MONTANA", X = 86, Y = 6, FontSize = 14, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "200 CH", X = 86, Y = 26, FontSize = 11, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "5 GLOB", X = 86, Y = 42, FontSize = 10, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "MORNING/NOON/NIGHT", X = 86, Y = 52, FontSize = 10, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "HOMEO MAHANAGAR", X = 86, Y = 70, FontSize = 10, Alignment = TextAlignment.Center }
        };

        view.RenderItems(items);
        return view;
    }

    #endregion
}
