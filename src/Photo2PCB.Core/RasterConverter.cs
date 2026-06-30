using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Photo2PCB.Core;

/// <summary>
/// Converts a photographic image into a stylized, black-on-white line-art
/// <see cref="RasterDocument"/> built from straight horizontal traces — the look of a
/// portrait drawn by a photoplotter flashing a circular aperture along horizontal scan
/// lines, where the aperture <i>swells in dark areas and thins in light areas</i> (an
/// engraving / green-CRT-scanline feel that also reads as PCB copper):
///   1. decode + convert to greyscale,
///   2. lay down <c>LineCount</c> evenly-spaced horizontal lines over the user-specified
///      <c>Height</c> (width follows the source aspect ratio). Each line is sampled along X
///      at the minimum-aperture pitch,
///   3. apply brightness/contrast,
///   4. set the local <b>line width</b> from darkness — <c>MinAperture</c> for the lightest
///      strokes up to <c>MaxAperture</c> for the darkest. Width is quantized into a few
///      levels and consecutive equal-width samples are merged into round-capped runs, so
///      each line is a continuous variable-width ribbon (no short dashes that could align
///      vertically). Truly white areas leave a gap, giving rounded trace ends.
///
/// The vertical line pitch is <c>Height / LineCount</c>. Because stroke width varies with
/// darkness, the spacing between lines varies too: light areas leave <c>Pitch − MinAperture</c>
/// of white; dark areas leave <c>Pitch − MaxAperture</c> (which may be negative ⇒ overlap).
/// Choosing <c>MaxAperture &lt; Pitch</c> guarantees white space between every line.
/// </summary>
public static class RasterConverter
{
    /// <summary>Number of discrete line-width levels between Min and Max aperture (for run merging / smoothness).</summary>
    private const int WidthLevels = 24;

    /// <summary>Hard cap on the horizontal sample count, to bound work for tiny apertures.</summary>
    private const int MaxColumns = 12_000;

    public static RasterDocument Convert(string imagePath, ConversionSettings settings)
    {
        using Image<L8> image = Image.Load<L8>(imagePath);
        return Convert(image, settings);
    }

    public static RasterDocument Convert(Stream imageStream, ConversionSettings settings)
    {
        using Image<L8> image = Image.Load<L8>(imageStream);
        return Convert(image, settings);
    }

    public static RasterDocument Convert(Image<L8> image, ConversionSettings settings)
    {
        settings = settings.Normalized();
        double minAp = settings.MinApertureMm;
        double maxAp = settings.MaxApertureMm;

        // Canvas: explicit height, width from the source aspect ratio.
        double canvasHeightMm = settings.HeightMm;
        double aspect = (double)image.Width / image.Height;
        double canvasWidthMm = canvasHeightMm * aspect;

        // LineCount rows on a constant vertical pitch; X sampled at the min-aperture pitch.
        int rows = settings.LineCount;
        double pitchMm = canvasHeightMm / rows;
        int cols = Math.Clamp((int)Math.Round(canvasWidthMm / minAp), 1, MaxColumns);
        double cellWidthMm = canvasWidthMm / cols;

        // One averaged tone per flash position — box resample avoids aliasing/moiré.
        using Image<L8> grid = image.Clone(ctx =>
            ctx.Resize(cols, rows, KnownResamplers.Box));
        double[] ink = ExtractInk(grid, settings);

        double widthSpan = maxAp - minAp;
        double cutoff = settings.BackgroundCutoff;
        var scanLines = new List<ScanLine>(rows);
        for (int y = 0; y < rows; y++)
        {
            double lineY = (y + 0.5) * pitchMm;
            int rowOffset = y * cols;

            // Walk the line; each sample's darkness sets a quantized line-width level.
            // Consecutive equal levels merge into one round-capped run; white breaks it.
            var runs = new List<PenRun>();
            int runStart = -1, prevLevel = -1;

            for (int x = 0; x < cols; x++)
            {
                double d = ink[rowOffset + x];
                int level = d < cutoff
                    ? -1
                    : Math.Clamp((int)Math.Round(d * (WidthLevels - 1)), 0, WidthLevels - 1);

                if (level != prevLevel)
                {
                    if (prevLevel >= 0)
                        runs.Add(MakeRun(runStart, x - 1, cellWidthMm, WidthFor(prevLevel, minAp, widthSpan)));
                    runStart = level >= 0 ? x : -1;
                    prevLevel = level;
                }
            }
            if (prevLevel >= 0)
                runs.Add(MakeRun(runStart, cols - 1, cellWidthMm, WidthFor(prevLevel, minAp, widthSpan)));

            scanLines.Add(new ScanLine { YMm = lineY, Runs = runs });
        }

        return new RasterDocument
        {
            CanvasWidthMm = canvasWidthMm,
            CanvasHeightMm = canvasHeightMm,
            MinApertureMm = minAp,
            MaxApertureMm = maxAp,
            LineCount = rows,
            LinePitchMm = pitchMm,
            Columns = cols,
            Rows = rows,
            SourceWidthPx = image.Width,
            SourceHeightPx = image.Height,
            ScanLines = scanLines,
        };
    }

    private static double WidthFor(int level, double minAp, double widthSpan)
        => minAp + (level / (double)(WidthLevels - 1)) * widthSpan;

    private static PenRun MakeRun(int startCol, int endCol, double cellWidthMm, double widthMm)
        => new((startCol + 0.5) * cellWidthMm, (endCol + 0.5) * cellWidthMm, widthMm);

    /// <summary>
    /// Returns ink coverage per cell (row-major) in [0,1], where 1 is full black.
    /// Brightness shifts the whole range (0 = darker, 1 = whiter; 0.5 neutral) and
    /// contrast scales around mid-grey (0 = flat, 1 = max; 0.5 neutral == factor 1).
    /// </summary>
    private static double[] ExtractInk(Image<L8> image, ConversionSettings s)
    {
        int w = image.Width, h = image.Height;
        var ink = new double[w * h];

        double brightnessOffset = (s.Brightness - 0.5) * 2.0;   // [-1,1], positive => whiter
        double contrastFactor = s.Contrast * 2.0;               // [0,2], 1 == neutral

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                Span<L8> row = accessor.GetRowSpan(y);
                int offset = y * w;
                for (int x = 0; x < w; x++)
                {
                    double luma = row[x].PackedValue / 255.0;   // 0 = black, 1 = white
                    luma = (luma - 0.5) * contrastFactor + 0.5; // contrast around mid
                    luma += brightnessOffset;                   // brightness shift
                    luma = Math.Clamp(luma, 0.0, 1.0);
                    ink[offset + x] = 1.0 - luma;               // invert to ink coverage
                }
            }
        });

        return ink;
    }
}
