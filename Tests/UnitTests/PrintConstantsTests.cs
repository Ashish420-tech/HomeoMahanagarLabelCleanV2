using Xunit;
using HomeoMahanagarLabelCleanV2.Helpers;

namespace HomeoMahanagarLabelCleanV2.Tests.UnitTests;

/// <summary>
/// DEFENSIVE REGRESSION TESTS: PrintConstants unit conversions
/// 
/// Purpose: Fail immediately if core conversion formulas or physical constants change.
/// These tests protect deterministic rendering across Preview, PDF, and Print.
/// 
/// DO NOT MODIFY these tests without revalidating on physical hardware.
/// </summary>
[Trait("Category", "Unit")]
public class PrintConstantsTests
{
    private const double TOLERANCE = 0.0001;

    #region Physical Constants Regression Tests

    [Fact]
    public void LabelWidthMm_MustBe50mm()
    {
        // CRITICAL: Physical label width. Changing this breaks all layout.
        Assert.Equal(50.0, PrintConstants.LabelWidthMm);
    }

    [Fact]
    public void LabelHeightMm_MustBe30mm()
    {
        // CRITICAL: Physical label height. Changing this breaks all layout.
        Assert.Equal(30.0, PrintConstants.LabelHeightMm);
    }

    [Fact]
    public void LabelPaddingMm_MustBe2mm()
    {
        // CRITICAL: Inner padding. Affects sensor avoidance and text positioning.
        Assert.Equal(2.0, PrintConstants.LabelPaddingMm);
    }

    #endregion

    #region Conversion Formula Regression Tests

    [Fact]
    public void MmToDip_KnownValues_MustMatchExpectedFormula()
    {
        // Formula: mm / 25.4 * 96.0
        // Test known conversion: 50mm should be ~189.0 DIPs
        double result = PrintConstants.MmToDip(50.0);
        double expected = 50.0 / 25.4 * 96.0;

        Assert.Equal(expected, result, TOLERANCE);
        // Known value: 50mm ≈ 188.976 DIPs (exact: 50.0 / 25.4 * 96.0)
        Assert.InRange(result, 188.9, 189.1); // Adjusted range for floating point precision
    }

    [Fact]
    public void MmToDip_LabelPaddingMm_ProducesExpectedPaddingDip()
    {
        // Verify LabelPaddingDip matches conversion of LabelPaddingMm
        double expected = PrintConstants.LabelPaddingMm / 25.4 * 96.0;
        double actual = PrintConstants.LabelPaddingDip;
        
        Assert.Equal(expected, actual, TOLERANCE);
    }

    [Fact]
    public void DipToPoints_KnownValues_MustMatchExpectedFormula()
    {
        // Formula: dip * 72.0 / 96.0
        // Test: 96 DIPs = 72 points
        double result = PrintConstants.DipToPoints(96.0);
        Assert.Equal(72.0, result, TOLERANCE);
        
        // Test: 189 DIPs ≈ 141.75 points
        result = PrintConstants.DipToPoints(189.0);
        Assert.Equal(141.75, result, TOLERANCE);
    }

    [Fact]
    public void MmToPoints_KnownValues_MustMatchExpectedFormula()
    {
        // Formula: MmToDip(mm) * 72.0 / 96.0
        // = (mm / 25.4 * 96.0) * 72.0 / 96.0
        // = mm / 25.4 * 72.0
        double result = PrintConstants.MmToPoints(25.4);
        Assert.Equal(72.0, result, TOLERANCE);
        
        // 50mm label width in points
        result = PrintConstants.MmToPoints(50.0);
        double expected = 50.0 / 25.4 * 72.0;
        Assert.Equal(expected, result, TOLERANCE);
    }

    #endregion

    #region Round-trip Conversion Tests

    [Fact]
    public void MmToDip_RoundTrip_PreservesPhysicalSize()
    {
        // Given physical mm, convert to DIPs and back via printer DPI
        double originalMm = 50.0;
        double dip = PrintConstants.MmToDip(originalMm);
        
        // Simulate printer conversion: DIP -> dots -> mm (203 DPI)
        int printerDpi = 203;
        double dipToDots = printerDpi / 96.0;
        int dots = (int)Math.Round(dip * dipToDots);
        double reconstructedMm = dots / (double)printerDpi * 25.4;
        
        // Allow 1 dot rounding tolerance
        Assert.InRange(reconstructedMm, originalMm - 0.2, originalMm + 0.2);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void MmToDip_Zero_ReturnsZero()
    {
        Assert.Equal(0.0, PrintConstants.MmToDip(0.0));
    }

    [Fact]
    public void DipToPoints_Zero_ReturnsZero()
    {
        Assert.Equal(0.0, PrintConstants.DipToPoints(0.0));
    }

    [Fact]
    public void MmToPoints_Zero_ReturnsZero()
    {
        Assert.Equal(0.0, PrintConstants.MmToPoints(0.0));
    }

    #endregion
}
