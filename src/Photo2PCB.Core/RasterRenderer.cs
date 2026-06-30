using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Photo2PCB.Core;

/// <summary>
/// Renders a <see cref="RasterDocument"/> to a raster bitmap that visually matches the
/// SVG output: black traces of the aperture diameter on white, with <b>rounded ends</b>
/// (a photoplotter flashing a fixed circular aperture). Isolated flashes are drawn as
/// aperture-sized dots. Used for the live GUI preview and an optional PNG export.
/// </summary>
public static class RasterRenderer
{
    /// <summary>
    /// Render to an in-memory image. <paramref name="maxDimension"/> caps the longest
    /// side (preserving aspect ratio) so previews stay cheap regardless of canvas size.
    /// </summary>
    public static Image<Rgba32> Render(RasterDocument doc, int maxDimension = 1200)
    {
        double maxMm = Math.Max(doc.CanvasWidthMm, doc.CanvasHeightMm);
        double pxPerMm = maxMm > 0 ? maxDimension / maxMm : 1.0;

        int pw = Math.Max(1, (int)Math.Round(doc.CanvasWidthMm * pxPerMm));
        int ph = Math.Max(1, (int)Math.Round(doc.CanvasHeightMm * pxPerMm));

        var image = new Image<Rgba32>(pw, ph, Color.White);

        image.Mutate(ctx =>
        {
            foreach (ScanLine line in doc.ScanLines)
            {
                float y = (float)(line.YMm * pxPerMm);
                foreach (PenRun run in line.Runs)
                {
                    float x1 = (float)(run.XStartMm * pxPerMm);
                    float x2 = (float)(run.XEndMm * pxPerMm);
                    float r = Math.Max(0.5f, (float)(run.WidthMm * pxPerMm / 2.0));

                    // Round caps at both ends = a capsule. Zero-length run => a single dot.
                    ctx.Fill(Color.Black, new EllipsePolygon(x1, y, r));
                    if (x2 > x1)
                    {
                        ctx.Fill(Color.Black, new RectangularPolygon(x1, y - r, x2 - x1, r * 2));
                        ctx.Fill(Color.Black, new EllipsePolygon(x2, y, r));
                    }
                }
            }
        });

        return image;
    }

    /// <summary>Render and encode to PNG bytes (handy for previews and CLI image dumps).</summary>
    public static byte[] RenderPng(RasterDocument doc, int maxDimension = 1200)
    {
        using Image<Rgba32> image = Render(doc, maxDimension);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }
}
