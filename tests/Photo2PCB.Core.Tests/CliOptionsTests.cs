using Xunit;

namespace Photo2PCB.Core.Tests;

public class CliOptionsTests
{
    [Fact]
    public void Defaults_AreNeutral()
    {
        var o = CliOptions.Parse(Array.Empty<string>());
        Assert.Null(o.InputPath);
        Assert.False(o.NonInteractive);
        Assert.Equal(0.5, o.Settings.Contrast, 3);
        Assert.Equal(0.5, o.Settings.Brightness, 3);
        Assert.Equal(150, o.Settings.LineCount);
    }

    [Fact]
    public void ParsesAllSwitches()
    {
        var o = CliOptions.Parse(new[]
        {
            "-i", "in.png", "-o", "out.svg",
            "--contrast", "75", "--brightness", "30",
            "--min-aperture", "0.2", "--max-aperture", "0.6", "--lines", "200", "--no-gui",
        });

        Assert.Equal("in.png", o.InputPath);
        Assert.Equal("out.svg", o.OutputPath);
        Assert.True(o.NonInteractive);
        Assert.Equal(0.75, o.Settings.Contrast, 3);
        Assert.Equal(0.30, o.Settings.Brightness, 3);
        Assert.Equal(0.2, o.Settings.MinApertureMm, 3);
        Assert.Equal(0.6, o.Settings.MaxApertureMm, 3);
        Assert.Equal(200, o.Settings.LineCount);
    }

    [Fact]
    public void BareArgument_IsTreatedAsInput()
    {
        var o = CliOptions.Parse(new[] { "photo.jpg" });
        Assert.Equal("photo.jpg", o.InputPath);
    }

    [Fact]
    public void Help_IsRecognized()
    {
        Assert.True(CliOptions.Parse(new[] { "--help" }).ShowHelp);
        Assert.True(CliOptions.Parse(new[] { "-h" }).ShowHelp);
    }

    [Theory]
    [InlineData("--lines")]          // missing value
    [InlineData("--contrast")]       // missing value
    public void MissingValue_Throws(string flag)
    {
        Assert.Throws<ArgumentException>(() => CliOptions.Parse(new[] { flag }));
    }

    [Fact]
    public void UnknownOption_Throws()
    {
        Assert.Throws<ArgumentException>(() => CliOptions.Parse(new[] { "--bogus" }));
    }

    [Fact]
    public void Settings_Normalize_ClampsToRanges()
    {
        var s = new ConversionSettings
        {
            Contrast = 5, Brightness = -1, MinApertureMm = 9, MaxApertureMm = 0.01,
            HeightMm = 9999, LineCount = 99999,
        }.Normalized();

        Assert.Equal(1.0, s.Contrast, 3);
        Assert.Equal(0.0, s.Brightness, 3);
        // Both apertures clamp into range; max is forced ≥ min.
        Assert.Equal(ConversionSettings.MaxApertureLimitMm, s.MinApertureMm, 3);
        Assert.Equal(ConversionSettings.MaxApertureLimitMm, s.MaxApertureMm, 3);
        Assert.Equal(ConversionSettings.MaxHeightMm, s.HeightMm, 3);
        // Line count is clamped to the height/max-aperture-derived cap.
        Assert.Equal(ConversionSettings.MaxLinesFor(ConversionSettings.MaxHeightMm, ConversionSettings.MaxApertureLimitMm),
            s.LineCount);
    }
}
