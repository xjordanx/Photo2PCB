using System.Xml.Linq;
using Xunit;

namespace Photo2PCB.Core.Tests;

public class ConversionTests
{
    // The sample photos are personal and not committed. When absent, image-driven tests
    // return early (pass vacuously) rather than fail — xUnit v2 has no dynamic skip.
    private static string? RequireImage() => TestImages.Available ? TestImages.All.First() : null;

    [Fact]
    public void TestFiles_AreDiscoveredWhenPresent()
    {
        if (!TestImages.Available) return;
        Assert.True(TestImages.All.Any());
    }

    [Theory]
    [MemberData(nameof(TestImages.AllAsMemberData), MemberType = typeof(TestImages))]
    public void Convert_ProducesPositiveCanvasAndRuns(string? imagePath)
    {
        if (imagePath is null) return;
        RasterDocument doc = RasterConverter.Convert(imagePath!, new ConversionSettings());

        Assert.True(doc.CanvasWidthMm > 0);
        Assert.True(doc.CanvasHeightMm > 0);
        Assert.True(doc.Columns > 0 && doc.Rows > 0);
        // A photographic image at neutral settings should yield some ink.
        Assert.True(doc.RunCount > 0, "Expected at least some pen runs for a photo.");
    }

    [Theory]
    [MemberData(nameof(TestImages.AllAsMemberData), MemberType = typeof(TestImages))]
    public void Svg_IsWellFormedAndHasMmDimensions(string? imagePath)
    {
        if (imagePath is null) return;
        RasterDocument doc = RasterConverter.Convert(imagePath!, new ConversionSettings());
        string svg = SvgWriter.Write(doc);

        XDocument xml = XDocument.Parse(svg);           // throws if malformed
        XElement root = xml.Root!;
        Assert.Equal("svg", root.Name.LocalName);
        Assert.EndsWith("mm", (string)root.Attribute("width")!);
        Assert.EndsWith("mm", (string)root.Attribute("height")!);

        XNamespace ns = "http://www.w3.org/2000/svg";
        Assert.Contains(root.Descendants(ns + "line"), _ => true);   // at least one stroke
        Assert.Contains(root.Descendants(ns + "rect"), _ => true);   // white background
    }

    [Theory]
    [MemberData(nameof(TestImages.AllAsMemberData), MemberType = typeof(TestImages))]
    public void Convert_IsDeterministic(string? imagePath)
    {
        if (imagePath is null) return;
        var settings = new ConversionSettings();
        string a = SvgWriter.Write(RasterConverter.Convert(imagePath!, settings));
        string b = SvgWriter.Write(RasterConverter.Convert(imagePath!, settings));
        Assert.Equal(a, b);
    }

    [Fact]
    public void HigherBrightness_ReducesInk()
    {
        var img = RequireImage(); if (img is null) return;
        var dark = RasterConverter.Convert(img, new ConversionSettings { Brightness = 0.2 });
        var bright = RasterConverter.Convert(img, new ConversionSettings { Brightness = 0.85 });

        // Measure actual ink area (Σ length × width); run count is not monotonic with brightness.
        static double InkArea(RasterDocument d) =>
            d.ScanLines.Sum(l => l.Runs.Sum(r => (r.XEndMm - r.XStartMm) * r.WidthMm));

        Assert.True(InkArea(bright) < InkArea(dark),
            $"Brighter setting should reduce ink (dark={InkArea(dark):0.#}, bright={InkArea(bright):0.#}).");
    }

    [Fact]
    public void LineCount_SetsRowCount_AndShrinksPitch()
    {
        var img = RequireImage(); if (img is null) return;
        var few = RasterConverter.Convert(img, new ConversionSettings { HeightMm = 100, LineCount = 50 });
        var many = RasterConverter.Convert(img, new ConversionSettings { HeightMm = 100, LineCount = 200 });

        // Line count controls the number of horizontal lines exactly.
        Assert.Equal(50, few.Rows);
        Assert.Equal(50, few.LineCount);
        Assert.Equal(200, many.Rows);

        // Canvas height is the explicit setting, independent of line count.
        Assert.Equal(100, few.CanvasHeightMm, 3);
        Assert.Equal(100, many.CanvasHeightMm, 3);

        // More lines over the same height => smaller pitch (lines pack tighter).
        Assert.True(many.LinePitchMm < few.LinePitchMm);
    }

    [Fact]
    public void Height_SetsCanvasSize_WidthFollowsAspectRatio()
    {
        var img = RequireImage(); if (img is null) return;
        var doc = RasterConverter.Convert(img, new ConversionSettings { HeightMm = 120, LineCount = 100 });

        Assert.Equal(120, doc.CanvasHeightMm, 3);
        double expectedWidth = 120.0 * doc.SourceWidthPx / doc.SourceHeightPx;
        Assert.Equal(expectedWidth, doc.CanvasWidthMm, 2);
    }

    [Fact]
    public void LineCount_IsCappedByHeightAndMaxAperture()
    {
        var img = RequireImage(); if (img is null) return;
        // floor(2 * 100 / 1.0) = 200; request far more, expect the cap.
        var doc = RasterConverter.Convert(img,
            new ConversionSettings { HeightMm = 100, MaxApertureMm = 1.0, LineCount = 5000 });

        int expectedMax = ConversionSettings.MaxLinesFor(100, 1.0);
        Assert.Equal(200, expectedMax);
        Assert.Equal(expectedMax, doc.LineCount);
    }

    [Fact]
    public void DarkLines_OverlapWhenMaxApertureExceedsPitch()
    {
        var img = RequireImage(); if (img is null) return;
        // pitch = 100/150 = 0.667 mm; max aperture 1.0 mm > pitch => dark strokes overlap.
        var doc = RasterConverter.Convert(img,
            new ConversionSettings { HeightMm = 100, MinApertureMm = 0.127, MaxApertureMm = 1.0, LineCount = 150 });

        Assert.True(doc.DarkLinesOverlap);
        Assert.True(doc.DarkGapMm < 0);
        Assert.True(doc.LightGapMm > 0);   // light strokes still leave a gap
    }

    [Fact]
    public void HigherBackgroundCutoff_RemovesLightStrokes()
    {
        var img = RequireImage(); if (img is null) return;
        static double InkArea(RasterDocument d) =>
            d.ScanLines.Sum(l => l.Runs.Sum(r => (r.XEndMm - r.XStartMm) * r.WidthMm));

        var low = RasterConverter.Convert(img, new ConversionSettings { BackgroundCutoff = 0.0 });
        var high = RasterConverter.Convert(img, new ConversionSettings { BackgroundCutoff = 0.4 });

        Assert.True(high.RunCount < low.RunCount);
        Assert.True(InkArea(high) < InkArea(low));
    }

    [Fact]
    public void RunWidths_StayWithinApertureRange()
    {
        var img = RequireImage(); if (img is null) return;
        var doc = RasterConverter.Convert(img,
            new ConversionSettings { MinApertureMm = 0.2, MaxApertureMm = 0.6, LineCount = 100 });

        foreach (var line in doc.ScanLines)
            foreach (var run in line.Runs)
                Assert.InRange(run.WidthMm, 0.2 - 1e-9, 0.6 + 1e-9);
    }

    [Fact]
    public void Runs_StayWithinCanvasBounds()
    {
        var img = RequireImage(); if (img is null) return;
        var doc = RasterConverter.Convert(img, new ConversionSettings { LineCount = 120 });

        foreach (var line in doc.ScanLines)
        {
            Assert.InRange(line.YMm, 0, doc.CanvasHeightMm);
            foreach (var run in line.Runs)
            {
                Assert.True(run.XStartMm <= run.XEndMm);
                Assert.InRange(run.XStartMm, -0.001, doc.CanvasWidthMm + 0.001);
                Assert.InRange(run.XEndMm, -0.001, doc.CanvasWidthMm + 0.001);
            }
        }
    }

    [Theory]
    [MemberData(nameof(TestImages.AllAsMemberData), MemberType = typeof(TestImages))]
    public void PngPreview_RendersNonEmpty(string? imagePath)
    {
        if (imagePath is null) return;
        RasterDocument doc = RasterConverter.Convert(imagePath!, new ConversionSettings());
        byte[] png = RasterRenderer.RenderPng(doc, maxDimension: 400);

        Assert.True(png.Length > 8);
        // PNG signature.
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
    }
}
