using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using Xunit.Sdk;
using HomeoMahanagarLabelCleanV2.Helpers;
using HomeoMahanagarLabelCleanV2.Models;
using HomeoMahanagarLabelCleanV2.Views;

namespace HomeoMahanagarLabelCleanV2.Tests.SnapshotTests;

/// <summary>
/// SNAPSHOT (GOLDEN MASTER) TESTS: Rendering Determinism
/// 
/// Purpose:
/// - Detect unintended changes to label rendering output
/// - Verify pixel-perfect parity across code changes
/// - Protect against layout regressions
/// 
/// Approach:
/// - Render PrintLabelView off-screen at fixed DPI and size
/// - Convert to PNG and compare pixel checksums
/// - Fail if rendering changes without explicit baseline update
/// 
/// WARNING:
/// - These tests WILL fail if fonts, DPI, or layout logic changes
/// - Baseline updates require manual verification on physical printer
/// - Pixel tolerance allows for minor anti-aliasing differences
/// </summary>
public class RenderingSnapshotTests
{
    private const double TEST_DPI = 96.0; // Standard WPF DPI for reproducibility
    private const int PIXEL_TOLERANCE = 5; // Allow small anti-aliasing differences
    
    [StaFact] // WPF UI tests require STA thread
    [Trait("Category", "Snapshot")]
    public void PrintLabelView_FixedInput_ProducesDeterministicOutput()
    {
        // ARRANGE: Create test label items
        var items = new[]
        {
            new LabelCanvasItem { Text = "ARNICA MONTANA", X = 86, Y = 6, FontSize = 14, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "200 CH", X = 86, Y = 26, FontSize = 11, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "5 GLOB", X = 86, Y = 42, FontSize = 10, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "MORNING/NOON/NIGHT", X = 86, Y = 52, FontSize = 10, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "HOMEO MAHANAGAR", X = 86, Y = 70, FontSize = 10, Alignment = TextAlignment.Center }
        };

        // ACT: Render to bitmap
        byte[] actualPng = RenderToPng(items);

        // ASSERT: Compare with baseline (or create baseline if not exists)
        string baselinePath = Path.Combine("SnapshotTests", "Baselines", "StandardLabel.png");
        
        if (!File.Exists(baselinePath))
        {
            // First run: create baseline
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            File.WriteAllBytes(baselinePath, actualPng);
            Assert.True(false, $"Baseline created at {baselinePath}. Re-run test to validate.");
        }
        else
        {
            // Compare with baseline
            byte[] baselinePng = File.ReadAllBytes(baselinePath);
            AssertPngSimilar(baselinePng, actualPng, PIXEL_TOLERANCE);
        }
    }

    [StaFact] // WPF UI tests require STA thread
    [Trait("Category", "Snapshot")]
    public void PrintLabelView_EmptyInputs_ProducesDeterministicOutput()
    {
        var items = new[]
        {
            new LabelCanvasItem { Text = "", X = 86, Y = 6, FontSize = 14, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "", X = 86, Y = 26, FontSize = 11, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "", X = 86, Y = 42, FontSize = 10, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "", X = 86, Y = 52, FontSize = 10, Alignment = TextAlignment.Center },
            new LabelCanvasItem { Text = "", X = 86, Y = 70, FontSize = 10, Alignment = TextAlignment.Center }
        };

        byte[] actualPng = RenderToPng(items);
        
        string baselinePath = Path.Combine("SnapshotTests", "Baselines", "EmptyLabel.png");
        
        if (!File.Exists(baselinePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            File.WriteAllBytes(baselinePath, actualPng);
            Assert.True(false, $"Baseline created at {baselinePath}. Re-run test to validate.");
        }
        else
        {
            byte[] baselinePng = File.ReadAllBytes(baselinePath);
            AssertPngSimilar(baselinePng, actualPng, PIXEL_TOLERANCE);
        }
    }

    #region Rendering Helpers

    private byte[] RenderToPng(LabelCanvasItem[] items)
    {
        // Create PrintLabelView off-screen
        var view = new PrintLabelView();
        
        // Set size to physical label dimensions in DIPs
        double dipW = PrintConstants.MmToDip(PrintConstants.LabelWidthMm);
        double dipH = PrintConstants.MmToDip(PrintConstants.LabelHeightMm);
        view.Width = dipW;
        view.Height = dipH;

        // Render items
        view.RenderItems(items);

        // Measure/Arrange/UpdateLayout (required for off-screen rendering)
        view.Measure(new Size(dipW, dipH));
        view.Arrange(new Rect(0, 0, dipW, dipH));
        view.UpdateLayout();

        // Render to bitmap at TEST_DPI
        int pixelWidth = (int)Math.Round(dipW * TEST_DPI / 96.0);
        int pixelHeight = (int)Math.Round(dipH * TEST_DPI / 96.0);

        var rtb = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            TEST_DPI,
            TEST_DPI,
            PixelFormats.Pbgra32);

        rtb.Render(view);

        // Encode to PNG
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private void AssertPngSimilar(byte[] expected, byte[] actual, int pixelTolerance)
    {
        // Simple byte-level comparison first
        if (expected.Length == actual.Length && ByteArrayEquals(expected, actual))
            return; // Exact match

        // If not exact, decode and compare pixels
        var expectedBitmap = DecodePng(expected);
        var actualBitmap = DecodePng(actual);

        Assert.Equal(expectedBitmap.PixelWidth, actualBitmap.PixelWidth);
        Assert.Equal(expectedBitmap.PixelHeight, actualBitmap.PixelHeight);

        int width = expectedBitmap.PixelWidth;
        int height = expectedBitmap.PixelHeight;
        int stride = width * 4; // 4 bytes per pixel (RGBA)

        byte[] expectedPixels = new byte[stride * height];
        byte[] actualPixels = new byte[stride * height];

        expectedBitmap.CopyPixels(expectedPixels, stride, 0);
        actualBitmap.CopyPixels(actualPixels, stride, 0);

        int differences = 0;
        for (int i = 0; i < expectedPixels.Length; i++)
        {
            int diff = Math.Abs(expectedPixels[i] - actualPixels[i]);
            if (diff > pixelTolerance)
                differences++;
        }

        // Allow up to 0.1% pixel differences (for anti-aliasing)
        double differencePercent = (double)differences / expectedPixels.Length * 100.0;
        Assert.True(differencePercent < 0.1,
            $"Rendering mismatch: {differencePercent:F2}% of pixels differ by more than {pixelTolerance}");
    }

    private bool ByteArrayEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private BitmapSource DecodePng(byte[] pngBytes)
    {
        using var ms = new MemoryStream(pngBytes);
        var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return decoder.Frames[0];
    }

    #endregion
}
