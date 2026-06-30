using Xunit;

namespace Photo2PCB.Core.Tests;

/// <summary>
/// Not a real assertion test — a developer harness that renders PNG previews of the
/// line-screen output to %TEMP%/p2pcb_preview so the result can be eyeballed.
/// </summary>
public class PreviewDump
{
    [Fact]
    public void DumpPreviews()
    {
        if (!TestImages.Available) return;   // sample photos are not committed

        string outDir = Path.Combine(Path.GetTempPath(), "p2pcb_preview");
        Directory.CreateDirectory(outDir);

        string headshot = TestImages.All.FirstOrDefault(p =>
            p.Contains("ChrisDenney", StringComparison.OrdinalIgnoreCase)) ?? TestImages.All.First();

        // Variable-aperture traces with differing background cutoffs (0 = full raster, higher = whiter bg).
        foreach (var (minA, maxA, cutoff) in new[] { (0.127, 0.6, 0.0), (0.127, 0.6, 0.25), (0.2, 0.9, 0.35) })
        {
            var settings = new ConversionSettings
            {
                HeightMm = 100, LineCount = 140, MinApertureMm = minA, MaxApertureMm = maxA,
                BackgroundCutoff = cutoff,
            };
            RasterDocument doc = RasterConverter.Convert(headshot, settings);
            byte[] png = RasterRenderer.RenderPng(doc, maxDimension: 900);
            File.WriteAllBytes(Path.Combine(outDir, $"chris_{minA}-{maxA}_cut{cutoff}.png"), png);
        }
    }
}
