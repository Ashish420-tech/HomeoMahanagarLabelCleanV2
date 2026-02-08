using Xunit;
using HomeoMahanagarLabelCleanV2.Services;

namespace HomeoMahanagarLabelCleanV2.Tests.UnitTests;

/// <summary>
/// UNIT TESTS: LabelTextComposer pure logic
/// 
/// Tests wrapping, normalization, and composition rules without UI dependencies.
/// These tests validate deterministic text layout behavior.
/// </summary>
[Trait("Category", "Unit")]
public class LabelTextComposerTests
{
    [Fact]
    public void Compose_NormalizeInputs_ConvertsToUpperCase()
    {
        var composer = new LabelTextComposer(189.0, fontSize: 11.0);
        
        var result = composer.Compose(
            "arnica montana",
            "200 ch",
            "morning",
            "daily",
            "clinic name"
        );
        
        // All outputs should be uppercase
        Assert.All(result, line => 
        {
            if (!string.IsNullOrWhiteSpace(line))
                Assert.Equal(line.ToUpperInvariant(), line);
        });
    }

    [Fact]
    public void Compose_Always_Produces5Lines()
    {
        var composer = new LabelTextComposer(189.0, fontSize: 11.0);
        
        var result = composer.Compose("Med", "Pot", "Dose", "Time", "Shop");
        
        Assert.Equal(5, result.Length);
    }

    [Fact]
    public void Compose_EmptyInputs_ProducesEmptyLines()
    {
        var composer = new LabelTextComposer(189.0, fontSize: 11.0);
        
        var result = composer.Compose("", "", "", "", "");
        
        Assert.Equal(5, result.Length);
        Assert.All(result, line => Assert.True(string.IsNullOrWhiteSpace(line)));
    }

    [Fact]
    public void Compose_PotencyMergedIntoSecondLine()
    {
        var composer = new LabelTextComposer(189.0, fontSize: 11.0);
        
        var result = composer.Compose(
            "SHORT",
            "200 CH",
            "DOSE",
            "TIME",
            "SHOP"
        );
        
        // Line 0: medicine name
        // Line 1: medicine continuation + potency
        Assert.Contains("200 CH", result[1]);
    }

    [Fact]
    public void MeasureTextWidth_EmptyString_ReturnsZero()
    {
        var composer = new LabelTextComposer(189.0, fontSize: 11.0);
        
        double width = composer.MeasureTextWidth("", 11.0);
        
        Assert.Equal(0.0, width);
    }

    [Fact]
    public void MeasureTextWidth_SameText_SameFontSize_ProducesSameWidth()
    {
        var composer = new LabelTextComposer(189.0, fontSize: 11.0);
        
        double width1 = composer.MeasureTextWidth("TEST", 11.0);
        double width2 = composer.MeasureTextWidth("TEST", 11.0);
        
        Assert.Equal(width1, width2);
    }

    [Fact]
    public void MeasureTextWidth_LargerFontSize_ProducesLargerWidth()
    {
        var composer = new LabelTextComposer(189.0, fontSize: 11.0);
        
        double width10 = composer.MeasureTextWidth("TEST", 10.0);
        double width12 = composer.MeasureTextWidth("TEST", 12.0);
        
        Assert.True(width12 > width10, "Larger font should produce wider measurement");
    }
}
